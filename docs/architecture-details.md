  ---
  1. Clean Architecture Boundaries

  ┌─────────────────────────────────────────────────┐
  │                   API Layer                      │
  │  Controllers · DTOs · Middleware · Program.cs    │
  │      (controllers mostly depend on Application)  │
  ├─────────────────────────────────────────────────┤
  │              Application Layer                   │
  │  Use Cases · Interfaces · Commands · Results     │
  │    (depends on Domain only — no infra)           │
  ├───────────────────────┬─────────────────────────┤
  │   Infrastructure      │      Domain Layer        │
  │  SoftHsm / Pkcs11     │  Key · KeyVersion        │
  │  KeyRepository (EF)   │  Certificate · Mechanism │
  │  PadesSigner          │  ProviderRef · Guard     │
  │  BC PKCS#7 signer     │  Enums · (zero deps)     │
  │  (implements Applic.  │                          │
  │   interfaces)         │                          │
  └───────────────────────┴─────────────────────────┘

  What each layer owns:

  - Domain — Entities, value objects, enums, invariant enforcement via Guard, exceptions. No framework references. Key enforces its own state machine; KeyVersion enforces its own status transitions.
  - Application — Pure orchestration. Defines what the system can do (use cases) and what it needs (interfaces). Never references Infrastructure directly.
  - Infrastructure — Implements every Application interface. All I/O (PostgreSQL, HSM providers, BouncyCastle, PDFsharp) is confined here.
  - API — Translates HTTP to use cases. Parses KeyId/Mechanism from strings, calls use case ExecuteAsync(), maps result to response DTO. Maps exceptions to HTTP status codes in ErrorHandlingMiddleware.

  ---
  2. Dependency Direction

  API  ──►  Application  ◄──  Infrastructure
                │
                ▼
              Domain

  - Infrastructure imports Application to implement its interfaces.
  - Application imports Domain for entities and value objects.
  - API imports Application to call use cases, and Infrastructure for DI wiring in Program.cs.
  - Only the composition root (Program.cs) wires Infrastructure in. Most controllers depend on Application only; AuthController and WebAuthnController are the exception, using ASP.NET Identity types (ApplicationUser, UserManager) and TokenService from Infrastructure directly.

  The only place where concrete types meet abstractions is Program.cs — DI registrations are the single seam where layers are stitched together.

  ---
  3. Use Case Implementation

  Each use case follows an identical pattern. Using SignDigestUseCase as the canonical example:

  Controller
    │  builds Command (KeyId, Mechanism, Digest)
    │  calls ExecuteAsync(command)   (caller identity comes from injected ICurrentUser)
    ▼
  Use Case
    1. Load aggregate from IKeyRepository (throws NotFoundException if missing)
    2. Authorize: key owner or Admin role (ForbiddenException otherwise)
    3. Assert domain invariants on the aggregate
         - Key.State == Active
         - KeyVersion.Status not Disabled/Destroyed
         - Key.IsMechanismAllowed(mechanism)
         - ValidateDigestLength (32 bytes for SHA-256)
       (validation failures also write a failure AuditEvent, then rethrow)
    4. Resolve the provider holding the key: IHsmProviderRegistry.Resolve(ProviderInstanceId)
    5. Call IHsmProvider.SignDigestAsync(keyVersion.ProviderRef, mechanism, digest)
    6. On HSM failure → write failure AuditEvent, rethrow
    7. On success → write success AuditEvent
    8. Return result record

  Key structural observations:

  - Use cases are sealed classes (a few are plain public classes) with constructor injection — no base class, no interface. They are registered directly as scoped services and injected into controllers by concrete type.
  - The caller is not passed as a parameter. Use cases read it from ICurrentUser (Actor, UserId, IsInRole), which is backed by the HTTP principal. The use case itself does not reference HttpContext.
  - Audit write is always the last side effect before return, ensuring the audit reflects the actual outcome.
  - AuditSink.WriteAsync calls SaveChangesAsync, so the audit write also commits pending repository changes (key creation and rotation rely on this). Certificate flows call IUnitOfWork.SaveAsync explicitly.
  - All validation that requires domain knowledge (mechanism allowed, version status) is delegated to the aggregate itself (Key.IsMechanismAllowed, Key.GetPrimaryVersion).

  ---
  4. HSM Integration

  IHsmProvider contract

  string InstanceId { get; }
  bool IsAvailable()
  Task<(ProviderRef, PublicKeyMaterial)> CreateSigningKeyAsync(string KeyName, Mechanism Mechanism)
  Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef)
  Task<byte[]>            SignDigestAsync(ProviderRef, Mechanism, byte[] digest)
  Task                    DestroyPrivateKeyAsync(ProviderRef)

  Certificates are not obtained from the HSM. They are stored as Certificate entities and their DER bytes are passed to the signers.

  IHsmProviderRegistry contract

  IHsmProvider Resolve(string providerInstanceId)   // routes an existing key version; empty id → default
  IHsmProvider ResolveDefault()                     // configured default provider
  IHsmProvider ResolveFirstAvailable()              // used when creating keys
  string DefaultProviderId { get; }

  Providers are built at startup from the HsmProviders configuration array. Each entry needs a unique, non-empty Id and a Type ("SoftHsm" or "Pkcs11"), and exactly one entry must have IsDefault=true.

  The ProviderRef value object is the link between the domain model and actual key material. It carries:
  - ProviderType — "SoftHsm" or "PKCS11"
  - ProviderInstanceId — the configured provider instance holding the key (stamped by the use case on creation)
  - Reference — an opaque string that only the provider parses:
      SoftHsm: softhsm:id=<guid>;label=<name>-<yyyyMMddHHmmss>;mech=<mech>
      PKCS#11: token=<TokenLabel>;id=<CKA_ID hex>;label=<label>
  The domain aggregate stores it but never interprets it.

  SoftHsmProvider (software provider)

  ConcurrentDictionary<string, StoredKey>
           key: ProviderRef.Reference
         value: StoredKey { Mechanism, PublicKeyPem, RSA?, ECDsa? }

  CreateSigningKeyAsync:
  RSA.Create(2048) for RSA_PSS_SHA256   OR   ECDsa.Create(nistP256) for ECDSA_P256_SHA256
    → public key exported as PEM (SubjectPublicKeyInfo)
    → StoredKey stored in dictionary
    → persisted to the JSON file if SoftHsm:FilePath is set
    → returns (ProviderRef, PublicKeyMaterial as PEM)

  SignDigestAsync:
  look up StoredKey by ProviderRef.Reference
    → verify mechanism matches stored mechanism
    → validate digest is 32 bytes (SHA-256)
    → RSA: SignHash(digest, SHA256, PSS)
    OR
    → ECDSA: SignHash(digest)   ← returns DER-encoded signature
    → return raw signature bytes

  DestroyPrivateKeyAsync:
  TryRemove from dictionary
    → stored.Rsa?.Dispose()  OR  stored.Ecdsa?.Dispose()
    → persist the store again if a file is configured
  Once removed from the dictionary the key material is gone from memory permanently.

  Persistence (optional): with SoftHsm:FilePath set, the whole store is loaded at construction and rewritten on every create/destroy. The file is a JSON array of { Reference, MechanismName, PublicKeyPem, PrivateKeyPkcs8Pem }, written to a temp file and moved over the target. Private keys are stored unencrypted. With no file path, keys are memory-only.

  Pkcs11HsmProvider (hardware / token provider)

  Real PKCS#11 implementation (Pkcs11Interop 5.3.0). The library at LibraryPath is loaded when the provider is constructed.
  - IsAvailable() — true if a slot with a token labelled TokenLabel is present
  - CreateSigningKeyAsync — generates an RSA key pair on the token (RsaKeySizeBits, default 2048, public exponent 65537). The private key has CKA_SIGN, CKA_SENSITIVE=true and CKA_EXTRACTABLE=false. Only RSA_PSS_SHA256 is accepted.
  - SignDigestAsync — logs in as CKU_USER with UserPin, then signs with CKM_RSA_PKCS_PSS (SHA-256, MGF1-SHA-256, 32-byte salt)
  - GetPublicKeyAsync — reads CKA_MODULUS and CKA_PUBLIC_EXPONENT and exports PEM
  - DestroyPrivateKeyAsync — destroys the private key object on the token
  Adding it is a configuration change (an HsmProviders entry with Type "Pkcs11"); no code change is needed.

  ---
  5. File Signing Workflow End-to-End

  HTTP POST /api/crypto/sign-file
    multipart: { KeyId, Mechanism, File, AddStamp, StampX, StampY, StampWidth, StampHeight, StampRotation }
           │
           ▼
  CryptoController.SignFile()
    - parse KeyId (Guid → KeyId value object)
    - parse Mechanism (string → Mechanism.Parse())
    - read IFormFile → byte[]
    - parse stamp fields → StampOptions
    - build SignFileCommand
    - call SignDocumentUseCase.ExecuteAsync(command, ct)
           │
           ▼
  SignDocumentUseCase.ExecuteAsync()
    1. IKeyRepository.GetByIdAsync(keyId)    ← load aggregate
    2. Assert: key owner or Admin; Key.State == Active
    3. Key.GetPrimaryVersion()               ← domain method, throws if none
    4. Assert: version.Status usable
    5. Key.IsMechanismAllowed(mechanism)     ← domain whitelist check; ECDSA rejected
    6. ICertificateRepository.GetActiveCertificateAsync(keyVersion.Id)
         → none: reject
         → expired: MarkExpired, save, write ExpireCertificate audit, reject
    7. Build DocumentSigningMaterial: certificate DER + chain, and a digest callback that
       calls IHsmProviderRegistry.Resolve(ProviderInstanceId).SignDigestAsync
    8. ISignedArtifactBuilder.SignAsync(fileName, bytes, material, stamp)
           │
           ▼
  SignedArtifactBuilder.SignAsync()
    ┌─ LooksLikePdf(bytes[0..4] == "%PDF-")
    │      YES → IPadesSigner.SignPdfAsync()
    │      NO  → IPkcs7AttachedSigner.SignAttachedAsync()
    └──────────────────────────────────────────────────
           │
     ┌─────┴─────┐
     ▼           ▼
  PadesSigner    BouncyCastlePkcs7AttachedSigner
     │                │
     │ PDFsharp: optional visible stamp on page 1
     │ DigitalSignatureHandler.ForDocument(doc, HsmBackedPdfSigner)
     │ HsmBackedPdfSigner builds the CMS SignedData;
     │ the digest is signed through the HSM callback
     │ doc.Save(ms)    │
     │ → <name>_signed.pdf │ HsmRsaPssSignatureFactory
     │                 │   → ISignatureFactory
     │                 │   → IStreamCalculator<IBlockResult>
     │                 │   → calls the HSM digest callback
     │                 │ CmsSignedDataGenerator.Generate()
     │                 │ → <name>.p7m (PKCS#7 CMS attached)
     └─────────────────┘
           │
           ▼
  SignFileResult { KeyId, KeyVersion, Mechanism, SignedFormat ("PAdES" | "PKCS7-Attached"),
                   OutputFileName, OutputContentType, SignedFileBytes, AuditEventId }
           │
           ▼
  CryptoController
    - set response headers:
        X-Audit-Event-Id, X-Key-Id, X-Key-Version, X-Mechanism,
        X-Signed-Format, X-File-Name
    - return File(bytes, contentType, downloadName)

  ---
  6. Important Abstractions and Why They Exist

  ┌─────────────────────────────────┬─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │ Abstraction                     │ Why it exists                                                                                                       │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IHsmProvider                    │ Decouples all key operations from the rest of the system. SoftHsmProvider (software keys) and Pkcs11HsmProvider     │
  │                                 │ (hardware tokens) are selected by configuration with no change to use cases. Held as singletons by                  │
  │                                 │ HsmProviderRegistry, because SoftHsm owns in-memory key material.                                                   │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IHsmProviderRegistry            │ Routes each key version to the provider instance that holds it, selects the default provider for new keys, and      │
  │                                 │ falls back to the first available provider. Registered as a singleton.                                              │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IKeyRepository                  │ Persistence is irrelevant to business logic. EF Core / PostgreSQL is the only registered implementation             │
  │                                 │ (InMemoryKeyRepository exists but is not wired). Scoped (EF requires scoped).                                       │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IAuditSink                      │ Decouples audit writing from use case logic. Could write to a DB, a message bus, or a log file; use cases don't     │
  │                                 │ care. The EF implementation calls SaveChanges, so it also commits pending repository changes.                       │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IUnitOfWork                     │ Lets certificate use cases commit several repository changes in one SaveChanges call. Key use cases rely on the     │
  │                                 │ audit write instead.                                                                                                │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IClock                          │ Makes time deterministic in tests. Use cases never call DateTime.UtcNow directly.                                   │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ICurrentUser                    │ Gives use cases the caller's identity (Actor, UserId) and role checks without any HTTP types, so ownership rules    │
  │                                 │ and the audit actor can be tested with a fake.                                                                      │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ISignedArtifactBuilder          │ Hides format-routing logic and the two signing backends (PAdES vs P7M) from the use case. The use case only knows   │
  │                                 │ it called SignAsync and got back a SignedArtifact.                                                                  │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IPadesSigner /                  │ Further separates the two signing formats inside Infrastructure. SignedArtifactBuilder depends on interfaces, not   │
  │ IPkcs7AttachedSigner            │ concrete classes; both can be swapped independently.                                                                │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ProviderRef                     │ The domain aggregate holds a reference to key material without knowing how or where that material is stored. Its    │
  │                                 │ Reference is opaque to the domain and Application layers; Application reads only ProviderInstanceId to route an     │
  │                                 │ operation to the right provider.                                                                                    │
  ├─────────────────────────────────┼─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ Mechanism                       │ A closed value object (not a string/enum) that encodes Name, AlgorithmFamily, HashAlgorithm, and SignatureEncoding  │
  │                                 │ together. Mechanisms are compared by name, not by reference.                                                        │
  └─────────────────────────────────┴─────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

  ---
  7. Where Architecture Enforces Separation of Concerns

  Domain invariants never leave the domain:
  Key.GetPrimaryVersion(), Key.AddVersion(), Key.PromoteVersionToPrimary() and Key.RetireVersion() enforce their rules with Guard calls or DomainException. Key.IsMechanismAllowed() is a whitelist lookup. Key state is changed only through the aggregate's methods (private setters), so use cases cannot bypass the rules.

  Infrastructure types are invisible above Application:
  Controllers and use cases never see SoftHsmProvider, Pkcs11HsmProvider, AppDbContext, PadesSigner, or BouncyCastlePkcs7AttachedSigner. They only see interfaces. Exception: AuthController and WebAuthnController use ASP.NET Identity types (ApplicationUser, UserManager) and TokenService directly.

  Format detection is content-based, not trust-based:
  SignedArtifactBuilder.LooksLikePdf() checks the first 5 bytes (%PDF-), not the HTTP Content-Type header. This prevents a caller from lying about file type.

  Caller identity comes from the authenticated principal:
  use cases read the caller through ICurrentUser, which is backed by the HTTP principal's claims (Identity user name and NameIdentifier). Ownership and Admin checks and the audit actor all come from there. The former hard-coded "dev" actor is gone.

  HTTP concerns stay in the API layer:
  Controllers do all HTTP-specific parsing (base64 decoding, IFormFile reading, stamp parsing, header writing). Use cases receive domain value objects (KeyId, Mechanism, byte[]) and command records, and return domain result records.
