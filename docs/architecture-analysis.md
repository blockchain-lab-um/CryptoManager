  ---                                                                                                                                                                                                                               
  1. PROJECT OVERVIEW                                                                                                                                                                                                               
                                                                                                                                                                                                                                    
  - What it does: Cryptographic key management system — create, rotate, and delete asymmetric signing keys; sign arbitrary byte digests; sign files (PDF via PAdES, all others via PKCS#7 CMS)                                      
  - Main purpose: Manage the full lifecycle of cryptographic signing keys and perform HSM-backed signing operations through a REST API                                                                                              
  - Core capabilities:                                                                                                                                                                                                              
    - Key lifecycle management (Active → Disabled → Deleted)                                                                                                                                                                        
    - Key version management and rotation                                                                                                                                                                                           
    - RSA-PSS-SHA256 and ECDSA-P256-SHA256 digest signing
    - File signing: PAdES for PDFs, PKCS#7 attached (.p7m) for other formats
    - Audit trail for all operations
    - Public key retrieval (PEM)                                                                                                                                                                                                    
  - Technologies:                                                                                                                                                                                                                   
    - Backend: .NET 9, ASP.NET Core, Entity Framework Core, PostgreSQL, BouncyCastle 2.6.2, PDFsharp 6.2.4                                                                                                                          
    - Frontend: Angular (standalone components, signals, rxResource), PrimeNG
    - Infrastructure: Docker, Nginx, Docker Compose

  ---
  2. BACKEND ARCHITECTURE (.NET)

  Architecture style: Clean Architecture — strict inward dependency flow: API → Application ← Infrastructure → Domain

  Projects and responsibilities:

  - CryptoManager.Domain — Zero external dependencies. Entities (Key, KeyVersion, AuditEvent), value objects (KeyId, Mechanism, ProviderRef, PublicKeyMaterial), enums (KeyState, KeyVersionStatus, KeyPurpose, Mechanism,
  AlgorithmFamily, SignatureEncoding, AuditAction), Guard helper, DomainException
  - CryptoManager.Application — Interfaces only (no implementations): IKeyRepository, IHsmProvider, IAuditSink, IClock, ISignedArtifactBuilder. All use cases live here as concrete classes that depend on abstractions.
  Commands/result DTOs.
  - CryptoManager.Infrastructure — Implements all Application interfaces: SoftHsmProvider, SignedArtifactBuilder, PadesSigner, BouncyCastlePkcs7AttachedSigner, KeyRepository (EF), InMemoryKeyRepository, AuditSink, SystemClock
  - CryptoManager.API — ASP.NET Core: KeysController, CryptoController, request/response DTOs, ErrorHandlingMiddleware, Program.cs (DI wiring, EF migration on startup)

  Use cases (all scoped):
  - CreateKeyUseCase, RotateKeyUseCase, SignDigestUseCase, GetPublicKeyUseCase, ListKeysUseCase, SignFileUseCase, DeleteKeyUseCase
  - Each accepts actor + requestId, writes to IAuditSink, calls HSM or repository as needed, throws DomainException (→ 400) or NotFoundException (→ 404)

  Key abstractions:

  ┌────────────────────────┬───────────┬──────────────────────────────┐
  │       Interface        │   Scope   │         Current Impl         │
  ├────────────────────────┼───────────┼──────────────────────────────┤
  │ IHsmProvider           │ Singleton │ SoftHsmProvider (in-memory)  │
  ├────────────────────────┼───────────┼──────────────────────────────┤
  │ IKeyRepository         │ Scoped    │ KeyRepository (EF Core / PG) │
  ├────────────────────────┼───────────┼──────────────────────────────┤
  │ IAuditSink             │ Scoped    │ AuditSink (EF Core)          │
  ├────────────────────────┼───────────┼──────────────────────────────┤
  │ IClock                 │ Singleton │ SystemClock                  │
  ├────────────────────────┼───────────┼──────────────────────────────┤
  │ ISignedArtifactBuilder │ Scoped    │ SignedArtifactBuilder        │
  └────────────────────────┴───────────┴──────────────────────────────┘

  Crypto/HSM components:
  - SoftHsmProvider — ConcurrentDictionary-backed in-memory RSA-2048 and ECDSA-P256; generates self-signed X509Certificate2 per key (10-year validity)
  - Pkcs11HsmProvider — stub for future real PKCS#11 hardware
  - SignedArtifactBuilder — routes by PDF magic bytes (%PDF-)
  - PadesSigner — DigitalSignatureHandler.ForDocument + PdfSharpDefaultSigner
  - BouncyCastlePkcs7AttachedSigner — custom ISignatureFactory, IStreamCalculator<IBlockResult>, IStore<X509Certificate> implementations due to BouncyCastle 2.x API

  ---
  3. FRONTEND ARCHITECTURE (Angular)

  Framework: Angular with standalone components, signals, rxResource (reactive resource pattern)

  Project structure:
  src/app/
    core/
      api/           # CryptoManagerApi service, models.ts
      services/      # CryptoService (Web Crypto), AppMessenger
    layout/          # Root app component + HTML shell
    pages/
      keys/          # Keys page + all key dialogs + KeysService
        keys.component/
        dialogs/
          key-create-dialog/
          key-public-dialog/
          key-rotate-dialog/
          key-delete-dialog/
      sign/           # Sign component (digest + file signing)
        sign.component/

  Services:
  - CryptoManagerApi — HttpClient-based, one method per API endpoint; sign-file returns HttpResponse<Blob> with custom header extraction
  - KeysService — wraps API, manages rxResource for reactive key list
  - CryptoService — Web Crypto API SHA-256 digest, base64/buffer conversions, file download helpers
  - AppMessenger — PrimeNG MessageService wrapper for toast notifications

  Key components:
  - KeysComponent — PrimeNG DataTable with signal-based dialog visibility state
  - KeyCreateDialogComponent — Reactive form: name, purpose, mechanism autocomplete
  - KeyPublicDialogComponent — Version picker, PEM display, copy/download
  - KeyRotateDialogComponent — Confirms rotation, shows new version + public key
  - KeyDeleteDialog — Confirmation before delete
  - SignComponent — Text/file input mode; digest or full-file signing; downloads result

  Backend communication:
  - Base URL from environment.apiBaseUrl (empty string, set at deploy time)
  - All JSON calls via HttpClient; sign-file uses FormData multipart
  - Response headers X-Key-Id, X-Key-Version, X-Mechanism, X-Signed-Format, X-Audit-Event-Id, X-File-Name extracted from sign-file response
  - Error surface: err?.error?.Error || err.message displayed via PrimeNG toast

  ---
  4. REPOSITORY STRUCTURE

  C:\PROJEKTI\CryptoManager\
  ├── docker-compose.yml          # Dev compose: PostgreSQL + API + frontend
  ├── compose.prod.yml            # Prod compose: pre-built images, .env secrets
  └── src/
      ├── backend/
      │   ├── CryptoManager.sln
      │   ├── CryptoManager.Domain/       # Entities, value objects, enums, Guard
      │   ├── CryptoManager.Application/  # Use cases, interfaces, commands, results
      │   ├── CryptoManager.Infrastructure/
      │   │   ├── HSM/SoftHSM/            # SoftHsmProvider
      │   │   ├── HSM/Pkcs11/             # Stub Pkcs11HsmProvider
      │   │   ├── Crypto/                 # SignedArtifactBuilder, PadesSigner, PKCS7 signer
      │   │   └── Persistence/
      │   │       ├── EntityFramework/    # AppDbContext, KeyRepository, AuditSink
      │   │       └── InMemory/           # InMemoryKeyRepository
      │   └── CryptoManager.API/
      │       ├── Controllers/            # KeysController, CryptoController
      │       ├── DTOs/                   # API request/response DTOs
      │       ├── Middleware/             # ErrorHandlingMiddleware
      │       ├── Program.cs              # DI, EF migrate, CORS, Swagger
      │       └── Dockerfile
      └── frontend/crypto-manager/
          ├── src/app/
          │   ├── core/                   # API client, services, models
          │   ├── layout/                 # Root app shell
          │   └── pages/                  # keys/, sign/
          ├── Dockerfile
          └── nginx.conf

  ---
  5. KEY WORKFLOWS

  Key Creation:
  - Entry: POST /api/Keys → KeysController.Create → CreateKeyUseCase
  - Services: IHsmProvider.CreateSigningKeyAsync (generates key material + cert), IKeyRepository.AddAsync
  - Audit: AuditAction.CreateKey
  - Output: CreateKeyResult with KeyId, PrimaryVersion (=1), PublicKeyMaterial (PEM)

  Key Rotation:
  - Entry: POST /api/Keys/{keyId}/rotate → RotateKeyUseCase
  - Services: Creates new version via HSM, promotes to Primary, moves old Primary → Active, persists via repository
  - Audit: AuditAction.RotateKey
  - Output: RotateKeyResult with NewPrimaryVersion, PublicKeyMaterial

  Digest Signing:
  - Entry: POST /api/Crypto/sign → CryptoController.Sign → SignDigestUseCase
  - Services: Validates key active, mechanism allowed, calls IHsmProvider.SignDigestAsync
  - Audit: AuditAction.Sign
  - Output: SignDigestResult with SignatureBase64, SignatureEncoding, AuditEventId

  File Signing:
  - Entry: POST /api/Crypto/sign-file (multipart, 50 MB limit) → SignFileUseCase
  - Services: ISignedArtifactBuilder.SignAsync → routes by magic bytes:
    - PDF (%PDF-) → IPadesSigner → PDFsharp PAdES signature → returns .signed.pdf
    - Other → IPkcs7AttachedSigner → BouncyCastle CMS attached → returns .p7m
  - Audit: AuditAction.SignFile
  - Output: Binary file response + X-* headers

  Key Deletion:
  - Entry: DELETE /api/Keys/{keyId} → DeleteKeyUseCase
  - Services: Destroys all versions via IHsmProvider.DestroyPrivateKeyAsync, marks key Deleted in repository
  - Audit: AuditAction.DeleteKey
  - Output: 200 OK

  Public Key Retrieval:
  - Entry: GET /api/Keys/{keyId}/public?version=N → GetPublicKeyUseCase
  - Services: IHsmProvider.GetPublicKeyAsync (or returns stored PEM from KeyVersion.PublicKey)
  - Output: GetPublicKeyResult with PEM

  ---
  6. SECURITY MODEL

  - Authentication: Not implemented. Actor is hardcoded to "dev" in all controllers
  - Authorization: Not implemented. All endpoints are open
  - Transport security: UNKNOWN / NOT CLEAR FROM CODE (CORS allows any origin/method/header — suitable for dev only)
  - Crypto key management:
    - Keys stored in SoftHsmProvider (in-memory ConcurrentDictionary) for MVP — lost on restart
    - EF Core persists key metadata (not key material) to PostgreSQL
    - Key material identified by ProviderRef — a string reference into the HSM store
    - Key lifecycle enforced via domain state machine; destroyed keys cannot be used
    - Mechanism whitelist enforced at use-case level before HSM call
  - HSM abstraction: IHsmProvider designed for swap-out to PKCS#11 hardware (Pkcs11HsmProvider stub exists)
  - Sensitive operations: All Sign/SignFile/GetPublicKey/Rotate/Delete operations are audited to AuditEvent table
  - Signing certificates: Self-signed X.509 certs generated per key by SoftHsmProvider (10-year validity); used in PAdES and CMS signerInfo

  ---
  7. CONFIGURATION

  Backend (appsettings.json / env vars):
  - ConnectionStrings__DefaultConnection — PostgreSQL connection string
  - Pkcs11 section — options for future Pkcs11HsmProvider (library path, slot, PIN, etc.)
  - No HSM-specific config for SoftHsmProvider (fully in-memory, no config needed)
  - Swagger enabled in all environments (including Production — notable)

  Frontend (environment.ts):
  - apiBaseUrl: '' — empty string; must be set at build/deploy time
  - PrimeNG theme: Noir preset with zinc color palette, dark-mode-selector: '.my-app-dark'

  Docker:
  - docker-compose.yml (dev): PostgreSQL 17 with cryptouser/cryptopass, API on port 8080, frontend on port 99
  - compose.prod.yml (prod): references .env for secrets, API bound to 127.0.0.1:5000, frontend to 127.0.0.1:3000; uses pre-built images cryptomanager/api:latest, cryptomanager/frontend:latest

  ---
  8. DEVELOPMENT SETUP

  Backend:
  dotnet build CryptoManager.sln
  dotnet run --project CryptoManager.API
  # Swagger UI available at /swagger
  # Requires PostgreSQL running (or switch DI to InMemoryKeyRepository)

  Frontend:
  cd src/frontend/crypto-manager
  npm install
  ng serve   # UNKNOWN exact command — standard Angular CLI assumed

  Docker (full stack):
  docker compose up   # dev: PostgreSQL + API + frontend

  Required tools:
  - .NET 9 SDK
  - Node.js + npm (Angular CLI)
  - Docker + Docker Compose
  - PostgreSQL (or use compose)

  Build artifacts: Multi-stage Dockerfiles for both API and frontend (Nginx)

  ---
  9. TESTING

  - No test projects found in the repository (*.Tests.csproj, *.spec.ts — none present)
  - The architecture is test-ready: all infrastructure concerns are behind interfaces, use cases accept injected abstractions
  - InMemoryKeyRepository exists and could serve as a test double
  - UNKNOWN / NOT CLEAR FROM CODE: whether tests are planned, omitted intentionally, or exist outside the scanned paths

  ---
  10. IMPORTANT DESIGN DECISIONS

  - Clean Architecture — enforces dependency inversion so infrastructure (PostgreSQL, BouncyCastle, PDFsharp) can be replaced without touching domain or application logic; IHsmProvider is the primary swap point for real HSM
  hardware
  - Use Case pattern — each business operation is a single-responsibility class, making auditing, validation, and testing consistent and explicit; avoids fat controllers and service classes
  - Rich domain model — business rules (mechanism whitelist, state transitions, version lifecycle) live in the Key aggregate, not in controllers or services; Guard enforces invariants at object construction
  - Value objects as records — Mechanism, ProviderRef, KeyId etc. are immutable by design; equality is structural
  - Actor hardcoded to "dev" — explicit placeholder acknowledging auth is not yet implemented; designed to be replaced
  - Singleton HSM provider — SoftHsmProvider must be singleton because it owns in-memory key material; scoped would lose keys between requests
  - BouncyCastle custom wrappers — v2.6.2 removed higher-level convenience types; custom ISignatureFactory and IStore<T> implementations bridge the HSM signing operation into BouncyCastle's CMS pipeline without exposing raw key
  material
  - PDF magic byte detection — format routing in SignedArtifactBuilder is intentionally content-based (not MIME type) to avoid spoofing via incorrect Content-Type header
  - EF migrations on startup — db.Database.Migrate() called in Program.cs; simplifies deployment at the cost of startup time and migration safety in production
  - Swagger always enabled — currently shown in all environments including Production; likely intentional for MVP, should be restricted before public exposure