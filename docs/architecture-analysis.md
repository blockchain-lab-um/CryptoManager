  ---
  1. PROJECT OVERVIEW

  - What it does: Cryptographic key management system — create, rotate, and delete asymmetric signing keys; sign arbitrary byte digests; sign files (PDF via PAdES, all others via PKCS#7 CMS); manage certificates for keys; verify signed files
  - Main purpose: Manage the full lifecycle of cryptographic signing keys and perform HSM-backed signing operations through a REST API
  - Core capabilities:
    - Key lifecycle management (Active → Disabled → Deleted), with per-owner key ownership
    - Key version management and rotation
    - RSA-PSS-SHA256 and ECDSA-P256-SHA256 digest signing (ECDSA is supported by the software HSM provider only; the PKCS#11 provider supports RSA-PSS only)
    - File signing: PAdES for PDFs, PKCS#7 attached (.p7m) for other formats (RSA-PSS only)
    - Certificates per key version: soft-CA enrollment from CSRs, certificate import, revocation; signing requires an active certificate
    - Signed-file verification (PAdES and PKCS#7)
    - Authentication and authorization: ASP.NET Identity with JWT bearer tokens; roles Admin and Operator
    - Passkeys (WebAuthn / FIDO2) as an additional sign-in method
    - Audit trail for all operations
    - Public key retrieval (PEM)
  - Technologies:
    - Backend: .NET 9, ASP.NET Core, ASP.NET Identity, Entity Framework Core, PostgreSQL, BouncyCastle 2.6.2, PDFsharp 6.2.4, Pkcs11Interop 5.3.0, Fido2.AspNet 4.0.1
    - Frontend: Angular 21 (standalone components, signals, rxResource), PrimeNG
    - Infrastructure: Docker, Nginx, Docker Compose

  ---
  2. BACKEND ARCHITECTURE (.NET)

  Architecture style: Clean Architecture — strict inward dependency flow: API → Application ← Infrastructure → Domain

  Projects and responsibilities:

  - CryptoManager.Domain — Zero external dependencies. Entities (Key, KeyVersion, AuditEvent, Certificate, SystemCertificateAuthority), identity entity (UserCredential, for passkeys), value objects (KeyId, KeyVersionId, CertificateId, AuditEventId, Mechanism, ProviderRef, PublicKeyMaterial, SubjectDN, Thumbprint, ValidityPeriod), enums (KeyState, KeyVersionStatus, KeyPurpose, AlgorithmFamily, SignatureEncoding, AuditAction, CertificateStatus, CertificateSource, RevocationReason), Guard helper, DomainException
  - CryptoManager.Application — Interfaces and DTOs only (no implementations): IKeyRepository, ICertificateRepository, IHsmProvider, IHsmProviderRegistry, IAuditSink, IAuditRepository, IClock, ICurrentUser, IUnitOfWork, ISignedArtifactBuilder, ICertificateAuthority, IWebAuthnService, IWebAuthnChallengeCache, and others. All use cases live here as concrete classes that depend on abstractions. Commands/result DTOs. Exceptions: NotFoundException, ForbiddenException, HsmUnavailableException, WebAuthnException.
  - CryptoManager.Infrastructure — Implements all Application interfaces: HSM providers (SoftHsmProvider, Pkcs11HsmProvider, HsmProviderRegistry), SignedArtifactBuilder, PadesSigner, BouncyCastlePkcs7AttachedSigner, SignedFileVerifier, soft CA (SoftSelfSignedCertificateAuthority, SoftCaBootstrapper), EF Core repositories (KeyRepository, CertificateRepository, SystemCertificateAuthorityRepository, UserCredentialRepository, AuditRepository), EfUnitOfWork, AuditSink, SystemClock, ASP.NET Identity types (ApplicationUser, ApplicationRole), TokenService, CurrentUser, WebAuthnService. InMemoryKeyRepository and InMemoryAuditRepository exist but are not registered in DI.
  - CryptoManager.API — ASP.NET Core: controllers (KeysController, CryptoController, CertificatesController, AuditController, AuthController, WebAuthnController, SystemController), request/response DTOs, ErrorHandlingMiddleware, Program.cs (DI wiring, JWT and Identity setup, EF migration plus role/admin seeding on startup)
  - CryptoManager.Tests — xUnit test project (references Application and Infrastructure)

  Use cases (all scoped):
  - CreateKeyUseCase, RotateKeyUseCase, SignDigestUseCase, GetPublicKeyUseCase, ListKeysUseCase, SignDocumentUseCase (file signing), VerifySignedFileUseCase, DeleteKeyUseCase, GetAuditLogsUseCase, GenerateCsrUseCase, SubmitCsrToCAUseCase, CompleteCertificateEnrollmentUseCase, ImportCertificateUseCase, RevokeCertificateUseCase, GetActiveCertificateUseCase
  - Each use case takes a command (or explicit arguments) and reads the caller from ICurrentUser; it writes to IAuditSink, calls HSM or repository as needed, and throws DomainException (→ 400), ForbiddenException (→ 403), or NotFoundException (→ 404). HsmUnavailableException maps to 503; anything else to 500.

  Key abstractions:

  ┌────────────────────────┬───────────┬──────────────────────────────────────────┐
  │       Interface        │   Scope   │               Current Impl               │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IHsmProvider           │ Singleton │ SoftHsmProvider, Pkcs11HsmProvider       │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IHsmProviderRegistry   │ Singleton │ HsmProviderRegistry                      │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IKeyRepository         │ Scoped    │ KeyRepository (EF Core / PG)             │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ ICertificateRepository │ Scoped    │ CertificateRepository (EF Core / PG)     │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IAuditSink             │ Scoped    │ AuditSink (EF Core)                      │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IUnitOfWork            │ Scoped    │ EfUnitOfWork                             │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ ICurrentUser           │ Scoped    │ CurrentUser (HTTP principal claims)      │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ IClock                 │ Singleton │ SystemClock                              │
  ├────────────────────────┼───────────┼──────────────────────────────────────────┤
  │ ISignedArtifactBuilder │ Scoped    │ SignedArtifactBuilder                    │
  └────────────────────────┴───────────┴──────────────────────────────────────────┘

  Crypto/HSM components:
  - HsmProviderRegistry — singleton holding the configured providers by Id. Exactly one provider must have IsDefault=true (startup fails otherwise). Resolve(id) routes an existing key version to the provider that holds it (an empty id falls back to the default). ResolveFirstAvailable() is used when creating keys: it returns the default provider if available, otherwise the first other available provider.
  - SoftHsmProvider — ConcurrentDictionary-backed software keys: RSA-2048 for RSA_PSS_SHA256 or ECDSA P-256 for ECDSA_P256_SHA256. Optional JSON file persistence (SoftHsm:FilePath); without a file path, keys are memory-only and lost on restart.
  - Pkcs11HsmProvider — real PKCS#11 implementation (Pkcs11Interop 5.3.0). Generates RSA key pairs on the token (RsaKeySizeBits, default 2048) with CKA_SENSITIVE=true and CKA_EXTRACTABLE=false. Signs with CKM_RSA_PKCS_PSS (SHA-256, MGF1-SHA-256, 32-byte salt). RSA_PSS_SHA256 only.
  - SoftSelfSignedCertificateAuthority / SoftCaBootstrapper — the backend's own CA. SoftCaBootstrapper creates a 10-year self-signed CA key and certificate at startup if none exists (persisted as SystemCertificateAuthority). The CA issues certificates for submitted CSRs (365-day validity), signed through the HSM provider.
  - SignedArtifactBuilder — routes by PDF magic bytes (%PDF-); rejects ECDSA mechanisms
  - PadesSigner — PDFsharp DigitalSignatureHandler with HsmBackedPdfSigner (the CMS structure is built around a digest signed by the HSM); optional visible signature stamp on page 1
  - BouncyCastlePkcs7AttachedSigner — CmsSignedDataGenerator with custom ISignatureFactory (HsmRsaPssSignatureFactory) and IStore<X509Certificate> implementations due to BouncyCastle 2.x API
  - SignedFileVerifier — validates PAdES and PKCS#7 signed files and reports format, signer and signing time

  ---
  3. FRONTEND ARCHITECTURE (Angular)

  Framework: Angular 21 with standalone components, signals, rxResource (reactive resource pattern)

  Project structure:
  src/app/
    app.config.ts    # Providers, PrimeNG theme (Noir preset)
    app.routes.ts    # Lazy-loaded routes, guarded by authGuard / adminGuard
    auth/            # Login, register, passkeys, WebAuthn client, AuthService, guards, auth interceptor
    core/
      api/           # CryptoManagerApi service, models.ts
      services/      # CryptoService (Web Crypto), AppMessenger, DownloadService
    layout/          # Root app component + HTML shell
    features/
      keys/          # Keys page, KeysService, dialogs/ (key-create, key-public, key-rotate, key-delete)
      certificates/  # Certificates page, CertificatesService, dialogs/ (issue, import, detail, revoke)
      sign/          # Sign page (text or file input) + stamp-position dialog, Signer
      verify/        # Verify page (signed-file verification)
      audit-log/     # Audit log page + AuditLogService
      dashboard/     # Dashboard page
      users/         # Users page
    shared/          # Shared UI components (e.g. card)

  Services:
  - CryptoManagerApi — HttpClient-based, one method per API endpoint (keys, crypto, certificates, audit, system); sign-file returns HttpResponse<Blob> with custom header extraction
  - KeysService, CertificatesService, AuditLogService — wrap the API and manage rxResource-based reactive lists
  - CryptoService — Web Crypto API SHA-256 digest, PEM/buffer conversions, and a pure-JS signature verification fallback for non-secure (HTTP) contexts
  - DownloadService — file download helpers (buffers and PEM)
  - AppMessenger — PrimeNG MessageService wrapper for toast notifications
  - AuthService, WebAuthnService — login/token handling and passkey registration and sign-in ceremonies; the auth interceptor attaches the bearer token to API calls

  Key components:
  - KeysPage — PrimeNG DataTable with signal-based dialog visibility state
  - KeyCreateDialog — Reactive form: name, purpose, mechanism selection
  - KeyPublicDialog — Version picker, PEM display, copy/download
  - KeyRotateDialog — Confirms rotation, shows new version + public key
  - KeyDeleteDialog — Confirmation before delete
  - SignPage — Text or file input mode. Text is hashed in the browser and sent as a digest; file input is sent to the sign-file endpoint, optionally with a visible stamp position. Downloads the result.

  Backend communication:
  - Base URL from environment.apiBaseUrl: https://localhost:44351 in development; empty string in production, where Nginx proxies /api/ to the API container (same origin)
  - All JSON calls via HttpClient; sign-file uses FormData multipart (including optional stamp fields)
  - Response headers X-Key-Id, X-Key-Version, X-Mechanism, X-Signed-Format, X-Audit-Event-Id, X-File-Name extracted from sign-file response
  - Error surface: err?.error?.Error || err.message displayed via PrimeNG toast (the API returns { Error, TraceId })

  ---
  4. REPOSITORY STRUCTURE

  CryptoManager/
  ├── docker-compose.yml          # Dev compose: PostgreSQL + API + frontend
  ├── compose.prod.yml            # Prod compose: pre-built images, .env secrets
  └── src/
      ├── backend/
      │   ├── CryptoManager.sln
      │   ├── CryptoManager.Domain/       # Entities, value objects, enums, Guard, Identity/UserCredential
      │   ├── CryptoManager.Application/  # Use cases, interfaces, commands, results
      │   ├── CryptoManager.Infrastructure/
      │   │   ├── HSM/                    # HsmProviderRegistry, HsmProviderConfig
      │   │   │   ├── SoftHSM/            # SoftHsmProvider, SoftHsmOptions
      │   │   │   └── PKCS11/             # Pkcs11HsmProvider, Pkcs11Options
      │   │   ├── Crypto/                 # SignedArtifactBuilder, PadesSigner, PKCS7 signer, soft CA, certificate validator/CSR builder
      │   │   ├── Identity/               # ApplicationUser/Role, TokenService, CurrentUser, WebAuthn/
      │   │   ├── Auditing/               # AuditSink
      │   │   └── Persistence/
      │   │       ├── EntityFramework/    # AppDbContext, EF repositories, configurations, migrations, EfUnitOfWork
      │   │       └── InMemory/           # InMemoryKeyRepository, InMemoryAuditRepository (not registered)
      │   ├── CryptoManager.API/
      │   │   ├── Controllers/            # Keys, Crypto, Certificates, Audit, Auth, WebAuthn, System
      │   │   ├── DTOs/                   # API request/response DTOs
      │   │   ├── Middleware/             # ErrorHandlingMiddleware
      │   │   ├── Program.cs              # DI, JWT/Identity, EF migrate + seed, CORS, Swagger (Development only)
      │   │   └── Dockerfile
      │   └── CryptoManager.Tests/        # xUnit tests
      └── frontend/crypto-manager/
          ├── src/app/
          │   ├── auth/                   # Login, register, passkeys, guards
          │   ├── core/                   # API client, services, models
          │   ├── layout/                 # Root app shell
          │   ├── features/               # keys/, certificates/, sign/, verify/, audit-log/, dashboard/, users/
          │   └── shared/                 # Shared components
          ├── Dockerfile
          └── nginx.conf

  ---
  5. KEY WORKFLOWS

  Key Creation:
  - Entry: POST /api/Keys (Operator or Admin) → KeysController.Create → CreateKeyUseCase
  - Services: Rejects a duplicate name for the same owner; IHsmProviderRegistry.ResolveFirstAvailable() → IHsmProvider.CreateSigningKeyAsync (generates key material and returns ProviderRef, stamped with the provider's instance id); Key aggregate created with OwnerId = current user; IKeyRepository.AddAsync (committed together with the audit write)
  - Audit: AuditAction.CreateKey (success and failure)
  - Output: CreateKeyResult with KeyId, PrimaryVersion (=1), PublicKeyMaterial (PEM)

  Key Rotation:
  - Entry: POST /api/Keys/{keyId}/rotate (Operator or Admin; owner or Admin) → RotateKeyUseCase
  - Services: Creates a new key version on the first available provider using the key's first allowed mechanism; the new version becomes Primary; the previous Primary is Retired; the previous version's private key is destroyed on its provider if that provider is available; persisted via IKeyRepository.UpdateAsync
  - Audit: AuditAction.RotateKey
  - Output: RotateKeyResult with NewPrimaryVersion, PublicKeyMaterial

  Digest Signing:
  - Entry: POST /api/Crypto/sign → CryptoController.SignDigest → SignDigestUseCase
  - Services: Owner-or-Admin check; key Active; primary version usable; mechanism in the key's whitelist; SHA-256 digest must be 32 bytes; IHsmProviderRegistry.Resolve(ProviderInstanceId) → IHsmProvider.SignDigestAsync
  - Audit: AuditAction.Sign (success and failure)
  - Output: SignDigestResult with SignatureBase64, SignatureEncoding (Raw for RSA-PSS, Der for ECDSA), AuditEventId

  File Signing:
  - Entry: POST /api/Crypto/sign-file (multipart: KeyId, Mechanism, File, optional stamp fields; 50 MB request limit) → CryptoController.SignFile → SignDocumentUseCase
  - Services: Owner-or-Admin check; key Active; primary version usable; mechanism allowed; ECDSA rejected (no document signing); an active Certificate linked to the primary version is required. If that certificate has expired, it is marked Expired, an ExpireCertificate audit event is written, and the request is rejected. A signing material object is built from the certificate chain and an HSM digest callback → ISignedArtifactBuilder.SignAsync → routes by magic bytes:
    - PDF (%PDF-) → IPadesSigner.SignPdfAsync → PAdES signature (optional visible stamp on page 1) → `<name>_signed.pdf`, application/pdf
    - Other → IPkcs7AttachedSigner.SignAttachedAsync → BouncyCastle CMS attached signature → `<name>.p7m`, application/pkcs7-mime
  - Audit: AuditAction.SignFile (records the certificate id and thumbprint)
  - Output: Binary file response + X-* headers (X-Audit-Event-Id, X-Key-Id, X-Key-Version, X-Mechanism, X-Signed-Format, X-File-Name)

  File Verification:
  - Entry: POST /api/Crypto/verify-file (Operator or Admin; 50 MB request limit) → CryptoController.VerifySignedFile → VerifySignedFileUseCase → ISignedFileVerifier
  - Services: Format is detected from the file content; PAdES or PKCS#7 CMS signature is validated
  - Audit: none
  - Output: VerifySignedFileResponseDto (IsValid, Format, Message, SignerName, CertificateSubject, SigningTime)

  Certificate Enrollment and Import:
  - Enrollment: POST /api/keys/{keyId}/certificates/enroll → GenerateCsrUseCase → SubmitCsrToCAUseCase (soft CA issues the certificate and returns an enrollment id, which it holds in memory until completion, so pending enrollments are lost on restart; a Certificate is created in PendingEnrollment). POST /api/keys/{keyId}/certificates/enrollments/{enrollmentId}/complete → CompleteCertificateEnrollmentUseCase (certificate becomes Active; any previously Active certificate for the same key version is Superseded)
  - Import: POST /api/keys/{keyId}/certificates/import → ImportCertificateUseCase. The certificate's public key must match the key version's public key; the chain is validated; duplicate thumbprints are rejected; the certificate becomes Active and the previous one is Superseded. Audit: AuditAction.ImportCertificate
  - Revoke: POST /api/keys/{keyId}/certificates/{certId}/revoke → RevokeCertificateUseCase (only Active or Superseded certificates; reason is passed to the CA). Audit: AuditAction.RevokeCertificate
  - Read: GET /api/keys/{keyId}/certificates/active → GetActiveCertificateUseCase

  Key Deletion:
  - Entry: DELETE /api/Keys/{keyId} (Admin only, AdminOnly policy) → DeleteKeyUseCase
  - Services: Destroys the private key of every Primary and Active version via its provider (IHsmProvider.DestroyPrivateKeyAsync); Key.Delete(); the key is removed from the database via IKeyRepository.DeleteAsync. Retired versions are not destroyed by this flow. No owner check is made in the use case (Admin-only by policy)
  - Audit: AuditAction.DeleteKey
  - Output: 200 OK

  Public Key Retrieval:
  - Entry: GET /api/Keys/{keyId}/public?version=N (Operator or Admin) → GetPublicKeyUseCase
  - Services: Owner-or-Admin check; resolves the primary version or the requested version; returns the PEM stored on KeyVersion.PublicKey (no HSM call)
  - Audit: AuditAction.GetPublicKey (success and failure)
  - Output: GetPublicKeyResult with PEM

  ---
  6. SECURITY MODEL

  - Authentication: ASP.NET Identity (ApplicationUser / ApplicationRole, EF Core stores; password requires at least 8 characters, a digit and an uppercase letter). JWT bearer tokens (HMAC-SHA256, signed with Jwt:Secret; issuer and audience validated; default lifetime 60 minutes). POST /api/auth/login issues a token. POST /api/auth/register is anonymous and assigns the Operator role to the new user. Passkeys: /api/auth/webauthn login begin/complete are anonymous; registration and credential management require a token.
  - Authorization: A global fallback policy requires an authenticated user for any endpoint without an explicit attribute. Policies: CanOperate (Admin or Operator) guards keys list/create/rotate/public-key, sign, sign-file, verify-file, certificate and audit-log endpoints. AdminOnly (Admin) guards DELETE /api/Keys/{keyId}. The system CA download only requires authentication.
  - Object-level checks: Sign, sign-file, digest signing, public-key retrieval, CSR generation, CSR submission, certificate import, and key rotation require owner or Admin. Key deletion relies on the AdminOnly policy only. Audit log: non-admins see only their own rows, and Operators do not see raw error messages.
  - Transport security: HTTPS redirection is enabled in the pipeline. CORS allows any origin, header and method, with the X-* response headers exposed (suitable for dev only)
  - Crypto key management:
    - Private key material lives only in the configured HSM provider(s): SoftHsmProvider (in-memory, optionally persisted to a JSON file with PKCS#8 private keys in plain text) or Pkcs11HsmProvider (token keys marked sensitive and non-extractable)
    - EF Core persists key metadata, key versions and certificates (not key material) to PostgreSQL
    - Key material identified by ProviderRef — a provider type, provider instance id and opaque reference into that provider's store
    - Key lifecycle enforced via domain state machine; destroyed keys cannot be used. Rotation destroys the previous version's key in its provider when that provider is available
    - Mechanism whitelist enforced at use-case level before HSM call
  - HSM abstraction: IHsmProvider with a config-driven IHsmProviderRegistry; Pkcs11HsmProvider is a real implementation
  - Sensitive operations: Create, Rotate, Sign, SignFile, GetPublicKey, Delete, certificate import and revoke are audited to the AuditEvent table. Verify-file is not audited
  - Signing certificates: Signing requires an active X.509 certificate linked to the key version. Certificates come from the soft CA (enrollment from a CSR; 365-day end-entity certificates under a 10-year self-signed CA created at startup) or from import. SoftHsmProvider does not generate certificates.

  ---
  7. CONFIGURATION

  Backend (appsettings.json / env vars / dotnet user-secrets):
  - ConnectionStrings__DefaultConnection — PostgreSQL connection string (EF migrations applied on startup)
  - HsmProviders — array of provider instances. Each has Id (unique, non-empty), Type ("SoftHsm" or "Pkcs11") and IsDefault (exactly one must be true). SoftHsm.FilePath — optional JSON persistence file (memory-only when empty). Pkcs11 — LibraryPath, TokenLabel, UserPin, RsaKeySizeBits (default 2048)
  - Jwt:Secret — required (set via user-secrets or environment); Jwt:Issuer and Jwt:Audience (default "CryptoManager"); Jwt:ExpiryMinutes (default 60)
  - DefaultAdmin:UserName (default "admin"), DefaultAdmin:Email, DefaultAdmin:Password — password required; the admin user, the Admin and Operator roles are seeded on startup
  - WebAuthn:ServerDomain, WebAuthn:ServerName, WebAuthn:Origins (at least one exact scheme+host+port origin), WebAuthn:TimeoutMs (default 60000), WebAuthn:ChallengeTtlSeconds (default 300)
  - Swagger enabled only in the Development environment

  Frontend (environment.ts):
  - apiBaseUrl: 'https://localhost:44351' in development; '' in production (same origin, proxied by Nginx)
  - PrimeNG theme: Noir preset with zinc color palette, dark mode via the '.dark-mode' selector

  Docker:
  - docker-compose.yml (dev): PostgreSQL 17 with cryptouser/cryptopass, API on port 8080, frontend on port 99. The API runs in Development mode with SoftHsm providers only, keys persisted to the api-data volume
  - compose.prod.yml (prod): PostgreSQL 17 and API/frontend use env_file: .env for secrets; API bound to 127.0.0.1:5000 (container port 8080), frontend to 127.0.0.1:3000 (container port 80); uses pre-built images cryptomanager/api:latest, cryptomanager/frontend:latest

  ---
  8. DEVELOPMENT SETUP

  Backend:
  dotnet build CryptoManager.sln
  dotnet user-secrets set "Jwt:Secret" "<long random string>"        # run from CryptoManager.API
  dotnet user-secrets set "DefaultAdmin:Password" "<admin password>"
  dotnet run --project CryptoManager.API
  # Swagger UI available at /swagger (Development environment only)
  # Requires PostgreSQL running (migrations run on startup)
  # The Pkcs11 provider loads its PKCS#11 library at startup; if the token is absent,
  # new keys fall back to the first available provider (e.g. SoftHsm)

  Frontend:
  cd src/frontend/crypto-manager
  npm install
  npm start   # runs ng serve

  Docker (full stack):
  docker compose up   # dev: PostgreSQL + API + frontend

  Required tools:
  - .NET 9 SDK
  - Node.js + npm (Angular CLI)
  - Docker + Docker Compose
  - PostgreSQL (or use compose)
  - PKCS#11 library and token (e.g. YubiHSM) only if the Pkcs11 provider is configured

  Build artifacts: Multi-stage Dockerfiles for both API and frontend (Nginx)

  ---
  9. TESTING

  - Test project: src/backend/CryptoManager.Tests (xUnit 2.9.2). Six tests in two files: WebAuthnChallengeCacheTests (5: one-time consume, unknown session, TTL expiry, max-entries limit, concurrent consume) and WebAuthnSignCountTests (1: sign count persisted on assertion)
  - Coverage is limited to the WebAuthn infrastructure; there are no tests yet for signing, key lifecycle, certificates, or the API
  - Frontend: one spec file (src/app/auth/webauthn/webauthn-utils.spec.ts); npm test runs ng test
  - The architecture is test-ready: infrastructure concerns are behind interfaces, and use cases accept injected abstractions (ICurrentUser, IClock, repositories, IHsmProviderRegistry)
  - InMemoryKeyRepository and InMemoryAuditRepository exist and could serve as test doubles (they are not registered in DI)

  ---
  10. IMPORTANT DESIGN DECISIONS

  - Clean Architecture — enforces dependency inversion so infrastructure (PostgreSQL, BouncyCastle, PDFsharp, PKCS#11) can be replaced without touching domain or application logic; IHsmProvider is the primary swap point for real HSM hardware
  - Use Case pattern — each business operation is a single-responsibility class, making auditing, validation, and testing consistent and explicit; avoids fat controllers and service classes
  - Rich domain model — business rules (mechanism whitelist, state transitions, version lifecycle) live in the Key aggregate, not in controllers or services; Guard enforces invariants at object construction
  - Value objects as records — Mechanism, KeyId, etc. are immutable by design; equality is structural. ProviderRef is also a record, but its ProviderInstanceId is set once by the use case after the provider creates the key
  - Config-driven HSM providers — providers are built from the HsmProviders configuration; each key version records the provider instance that holds it, so later operations route to the same provider. New keys go to the first available provider, preferring the default
  - Actor from the authenticated principal — ✅ Resolved: the hard-coded "dev" actor is gone. ICurrentUser exposes Actor (Identity user name) and UserId (NameIdentifier claim) from the JWT principal. Use cases still receive no HTTP types and remain testable with a fake ICurrentUser
  - Singleton HSM provider — providers are held as singletons by HsmProviderRegistry because SoftHsmProvider owns in-memory key material; scoped would lose keys between requests
  - BouncyCastle custom wrappers — v2.6.2 removed higher-level convenience types; custom ISignatureFactory and IStore<T> implementations bridge the HSM signing operation into BouncyCastle's CMS pipeline without exposing raw key material
  - PDF magic byte detection — format routing in SignedArtifactBuilder is intentionally content-based (not MIME type) to avoid spoofing via incorrect Content-Type header
  - Single-instance passkey challenges — WebAuthn challenges are kept in InMemoryWebAuthnChallengeCache (ConcurrentDictionary.TryRemove gives one-time consumption). Multi-instance deployments need a shared store with atomic get-and-delete (e.g. Redis GETDEL)
  - EF migrations on startup — db.Database.Migrate() called in Program.cs; simplifies deployment at the cost of startup time and migration safety in production
  - Swagger only in Development — ✅ Resolved: Swagger UI and the OpenAPI endpoint were previously enabled in all environments, including Production. They are now registered only when app.Environment.IsDevelopment()
