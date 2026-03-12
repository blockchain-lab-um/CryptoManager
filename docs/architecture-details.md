  ---                                                                                     
  1. Clean Architecture Boundaries                                                                                                                                                                                                  
                                                                                                                                                                                                                                    
  ┌─────────────────────────────────────────────────┐                                                                                                                                                                               
  │                   API Layer                      │                                                                                                                                                                              
  │  Controllers · DTOs · Middleware · Program.cs    │
  │         (depends on Application only)            │
  ├─────────────────────────────────────────────────┤
  │              Application Layer                   │
  │  Use Cases · Interfaces · Commands · Results     │
  │    (depends on Domain only — no infra)           │
  ├───────────────────────┬─────────────────────────┤
  │   Infrastructure      │      Domain Layer        │
  │  SoftHsmProvider      │  Key · KeyVersion        │
  │  KeyRepository (EF)   │  Mechanism · ProviderRef │
  │  PadesSigner          │  Guard · Enums           │
  │  Pkcs7Signer          │  (zero dependencies)     │
  │  (implements Applic.  │                          │
  │   interfaces)         │                          │
  └───────────────────────┴─────────────────────────┘

  What each layer owns:

  - Domain — Entities, value objects, enums, invariant enforcement via Guard, exceptions. No framework references. Key enforces its own state machine; KeyVersion enforces its own status transitions.
  - Application — Pure orchestration. Defines what the system can do (use cases) and what it needs (interfaces). Never references Infrastructure directly.
  - Infrastructure — Implements every Application interface. All I/O (PostgreSQL, in-memory crypto, BouncyCastle, PDFsharp) is confined here.
  - API — Translates HTTP to use cases. Parses KeyId/Mechanism from strings, calls use case ExecuteAsync(), maps result to response DTO.

  ---
  2. Dependency Direction

  API  ──►  Application  ◄──  Infrastructure
                │
                ▼
              Domain

  - Infrastructure imports Application to implement its interfaces.
  - Application imports Domain for entities and value objects.
  - API imports Application to call use cases.
  - Nothing ever imports API or Infrastructure except the composition root (Program.cs).

  The only place where concrete types meet abstractions is Program.cs — DI registrations are the single seam where layers are stitched together.

  ---
  3. Use Case Implementation

  Each use case follows an identical pattern. Using SignDigestUseCase as the canonical example:

  Controller
    │  builds Command (KeyId, Mechanism, Digest)
    │  calls ExecuteAsync(command, actor="dev", requestId=TraceIdentifier)
    ▼
  Use Case
    1. Load aggregate from IKeyRepository (throws NotFoundException if missing)
    2. Assert domain invariants on the aggregate
         - Key.State == Active
         - KeyVersion.Status not Disabled/Destroyed
         - Key.IsMechanismAllowed(mechanism)
         - ValidateDigestLength (32 bytes for SHA-256)
    3. Call IHsmProvider.SignDigestAsync(keyVersion.ProviderRef, mechanism, digest)
    4. On HSM failure → write failure AuditEvent, rethrow
    5. On success → write success AuditEvent
    6. Return result record

  Key structural observations:

  - Use cases are sealed classes with constructor injection — no base class, no interface. They are registered directly as scoped services and injected into controllers by concrete type.
  - actor and requestId are passed as method parameters (not injected via IHttpContextAccessor), keeping use cases HTTP-agnostic.
  - Audit write is always the last side effect before return, ensuring the audit reflects the actual outcome.
  - All validation that requires domain knowledge (mechanism allowed, version status) is delegated to the aggregate itself (Key.IsMechanismAllowed, Key.GetPrimaryVersion).

  ---
  4. HSM Integration

  IHsmProvider contract

  Task<(ProviderRef, PublicKeyMaterial)> CreateSigningKeyAsync(string name, Mechanism mechanism)
  Task<byte[]>       SignDigestAsync(ProviderRef, Mechanism, byte[] digest)
  Task<X509Certificate2> GetSigningCertificateAsync(ProviderRef)
  Task<PublicKeyMaterial> GetPublicKeyAsync(ProviderRef)
  Task               DestroyPrivateKeyAsync(ProviderRef)

  The ProviderRef value object is the only link between the domain model and actual key material. It is an opaque string reference (softhsm:id=<guid>;label=<name>;mech=<mech>) — the domain aggregate stores it but never
  interprets it.

  SoftHsmProvider (MVP in-memory implementation)

  ConcurrentDictionary<string, StoredKey>
           key: ProviderRef.Reference
         value: StoredKey { RSA?, ECDsa?, X509Certificate2, Mechanism, PublicKeyPem }

  CreateSigningKeyAsync:
  RSA.Create(2048)  OR  ECDsa.Create(nistP256)
    → CertificateRequest (self-signed, CN=SoftHSM-<guid>, 10yr)
    → StoredKey stored in dictionary
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
  Once removed from the dictionary the key material is gone from memory permanently (no persistence for SoftHsm key material).

  Pkcs11HsmProvider

  Stub only. The interface contract is identical — swapping it in requires only a DI registration change in Program.cs.

  ---
  5. File Signing Workflow End-to-End

  HTTP POST /api/crypto/sign-file
    multipart: { KeyId, Mechanism, File }
           │
           ▼
  CryptoController.SignFile()
    - parse KeyId (Guid → KeyId value object)
    - parse Mechanism (string → Mechanism.Parse())
    - read IFormFile → byte[]
    - build SignFileCommand
    - call SignFileUseCase.ExecuteAsync(command, "dev", TraceIdentifier, ct)
           │
           ▼
  SignFileUseCase.ExecuteAsync()
    1. IKeyRepository.GetByIdAsync(keyId)    ← load aggregate
    2. Assert: Key.State == Active
    3. Key.GetPrimaryVersion()               ← domain method, throws if none
    4. Assert: version.Status usable
    5. Key.IsMechanismAllowed(mechanism)     ← domain whitelist check
    6. ISignedArtifactBuilder.SignAsync(
         keyVersion.ProviderRef,
         mechanism,
         originalFileName,
         fileBytes,
         ct)
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
     │ GetSigningCert  │ GetSigningCert (X509Certificate2)
     │ via IHsmProvider│
     │                 │
     │ PdfSharpDefaultSigner(cert, SHA256)
     │ DigitalSignatureHandler.ForDocument(doc, signer)
     │ doc.Save(ms)    │
     │ → .signed.pdf   │
     │                 │ HsmRsaPssSignatureFactory
     │                 │   → ISignatureFactory
     │                 │   → IStreamCalculator<IBlockResult>
     │                 │   → calls IHsmProvider.SignDigestAsync internally
     │                 │ CmsSignedDataGenerator.Generate()
     │                 │ → .p7m (PKCS#7 CMS attached)
     └─────────────────┘
           │
           ▼
  SignFileResult { KeyId, KeyVersion, Mechanism, SignedFormat, OutputFileName,
                   OutputContentType, SignedFileBytes, AuditEventId }
           │
           ▼
  CryptoController
    - set response headers:
        X-Audit-Event-Id, X-Key-Id, X-Key-Version, X-Mechanism,
        X-Signed-Format, X-File-Name
    - return File(bytes, contentType, downloadName)

  ---
  6. Important Abstractions and Why They Exist

  ┌─────────────────────────────────┬───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
  │           Abstraction           │                                                                                       Why it exists                                                                                       │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IHsmProvider                    │ Decouples all crypto operations from the rest of the system. SoftHsmProvider (dev) and Pkcs11HsmProvider (production hardware) are interchangeable without any change outside Program.cs. │
  │                                 │  Registered as Singleton because it owns in-memory key material.                                                                                                                          │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IKeyRepository                  │ Persistence is irrelevant to business logic. EF Core/PostgreSQL in production, InMemoryKeyRepository for dev/tests. Scoped (EF requires scoped).                                          │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IAuditSink                      │ Decouples audit writing from use case logic. Could write to a DB, a message bus, or a log file — use cases don't care.                                                                    │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IClock                          │ Makes time deterministic in tests. Use cases never call DateTime.UtcNow directly.                                                                                                         │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ISignedArtifactBuilder          │ Hides format-routing logic and the two signing backends (PAdES vs P7M) from the use case. The use case only knows it called SignAsync and got back a SignedArtifact.                      │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ IPadesSigner /                  │ Further separates the two signing formats inside Infrastructure. SignedArtifactBuilder depends on interfaces, not concrete classes — both can be swapped independently.                   │
  │ IPkcs7AttachedSigner            │                                                                                                                                                                                           │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ ProviderRef                     │ The domain aggregate holds a reference to key material without knowing anything about how or where that material is stored. It is opaque to the domain and Application layers.            │
  ├─────────────────────────────────┼───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
  │ Mechanism                       │ A closed value object (not a string/enum) that encodes Name, AlgorithmFamily, HashAlgorithm, and SignatureEncoding together. Mechanisms are compared by name, not by reference.           │
  └─────────────────────────────────┴───────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────────┘

  ---
  7. Where Architecture Enforces Separation of Concerns

  Domain invariants never leave the domain:
  Key.GetPrimaryVersion(), Key.IsMechanismAllowed(), Key.AddVersion(), Key.PromoteVersionToPrimary() all contain Guard calls. Use cases cannot bypass them — there is no way to modify state without going through the aggregate's
  methods.

  Infrastructure types are invisible above Application:
  No controller or use case ever sees SoftHsmProvider, AppDbContext, PadesSigner, or BouncyCastlePkcs7AttachedSigner. They only see interfaces.

  Format detection is content-based, not trust-based:
  SignedArtifactBuilder.LooksLikePdf() checks the first 5 bytes (%PDF-), not the HTTP Content-Type header. This prevents a caller from lying about file type.

  Actor/auth is not injected into use cases via ambient context:
  actor and requestId flow as explicit method parameters. This makes use case behaviour fully testable without an HTTP context. It also makes the hardcoded "dev" placeholder visible at every call site.

  HTTP concerns never enter use cases:
  Controllers do all HTTP-specific parsing (base64 decoding, IFormFile reading, header writing). Use cases receive only domain value objects (KeyId, Mechanism, byte[]) and return domain result records.