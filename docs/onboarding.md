# Developer Onboarding Guide

## 1. Project Overview

CryptoManager is a cryptographic key management and signing system. It provides a REST API and Angular frontend for managing asymmetric signing keys and performing HSM-backed signing operations.

The system currently supports:

* creating signing keys
* rotating key versions
* deleting keys
* retrieving public keys in PEM format
* signing SHA-256 digests
* signing files

  * PDF files are signed with PAdES (optionally with a visible stamp)
  * non-PDF files are signed as attached PKCS#7/CMS (`.p7m`)
* verifying signed files
* issuing, importing, and revoking X.509 certificates for keys (CSR enrollment against a soft CA, or import of externally issued certificates)
* user accounts with JWT-based login and role-based access (`Admin`, `Operator`)
* passkey (WebAuthn/FIDO2) login and passkey management
* auditing all sensitive operations

The current implementation is designed as an MVP with a clear path toward real hardware-backed HSM integration. Signing keys live in an HSM provider selected from configuration. Two provider types exist: `SoftHsm`, a software provider for development (in memory, optionally persisted to a JSON file), and `Pkcs11`, which drives a hardware HSM such as a YubiHSM. The architecture keeps the HSM behind `IHsmProvider`, so providers can be added or swapped with minimal impact on the rest of the system.

Main technologies used:

* Backend: .NET 9, ASP.NET Core, ASP.NET Core Identity, JWT Bearer, Entity Framework Core, PostgreSQL, BouncyCastle, PDFsharp, Pkcs11Interop, Fido2.AspNet
* Frontend: Angular 21, standalone components, signals, `rxResource`, PrimeNG, Tailwind CSS
* Infrastructure: Docker, Docker Compose, Nginx

---

## 2. System Architecture

The system follows a Clean Architecture approach with strict inward dependency flow.

High-level dependency direction:

```text
API  ──►  Application  ◄──  Infrastructure
              │
              ▼
            Domain
```

This means:

* the API layer depends on Application (it also references Infrastructure, but only for wiring in the composition root)
* the Application layer depends on Domain
* the Infrastructure layer depends on Application and Domain to implement required abstractions
* Domain remains isolated from framework and infrastructure concerns

The composition root is `Program.cs`, where dependency injection connects abstractions to concrete implementations.

### Layer responsibilities

**Domain**
Owns business concepts and rules:

* entities such as `Key`, `KeyVersion`, `AuditEvent`, `Certificate`, `SystemCertificateAuthority`
* value objects such as `KeyId`, `Mechanism`, `ProviderRef`, `PublicKeyMaterial`, `SubjectDN`, `Thumbprint`
* enums for key states, audit actions, version and certificate status, and revocation reasons
* invariant enforcement and guard logic

**Application**
Defines what the system does:

* use cases for business operations
* contracts/interfaces for infrastructure dependencies
* command and result models
* orchestration of domain rules and external services

**Infrastructure**
Implements external concerns:

* HSM provider implementations and the provider registry
* persistence with EF Core
* file-signing implementations and PDF stamping
* certificate authority, CSR building, and certificate validation
* identity (ASP.NET Core Identity, JWT) and WebAuthn/FIDO2 services
* audit persistence
* system clock

**API**
Exposes HTTP endpoints:

* controllers
* HTTP DTOs
* middleware
* DI configuration
* startup and environment wiring

### Architectural characteristics

The architecture intentionally enforces separation of concerns in several important ways:

* domain invariants live in domain entities and value objects, not controllers
* use cases are HTTP-agnostic: the caller's identity arrives through the `ICurrentUser` abstraction (implemented over `HttpContext` in Infrastructure), and use cases never touch HTTP types directly
* infrastructure details such as EF Core, BouncyCastle, PDFsharp, or HSM internals are hidden behind interfaces
* file type routing is content-based rather than trusting request metadata

---

## 3. Backend Architecture

The backend is split into four main projects under `src/backend/`, plus the `CryptoManager.Tests` xUnit project.

### `CryptoManager.Domain`

This project has no external dependencies. It contains the core business model.

Main contents:

* entities: `Key`, `KeyVersion`, `AuditEvent`, `Certificate`, `SystemCertificateAuthority`
* identity: `UserCredential` (a registered passkey)
* value objects: `KeyId`, `KeyVersionId`, `AuditEventId`, `CertificateId`, `Mechanism`, `ProviderRef`, `PublicKeyMaterial`, `SubjectDN`, `Thumbprint`, `ValidityPeriod`
* enums: `KeyState`, `KeyVersionStatus`, `KeyPurpose`, `AlgorithmFamily`, `SignatureEncoding`, `AuditAction`, `CertificateSource`, `CertificateStatus`, `RevocationReason`
* validation helpers such as `Guard`
* domain exceptions

This layer enforces business rules such as:

* allowed state transitions
* allowed signing mechanisms per key
* active/primary version handling
* key lifecycle rules

### `CryptoManager.Application`

This project defines the use cases and required abstractions.

Key interfaces (not exhaustive):

* `IKeyRepository`, `ICertificateRepository`, `IAuditRepository`, `IUserCredentialRepository`
* `IHsmProvider`, `IHsmProviderRegistry`
* `IAuditSink`
* `IClock`
* `ISignedArtifactBuilder`, `ISignedFileVerifier`
* `ICertificateAuthority`, `ICsrBuilder`, `ICertificateValidator`
* `ICurrentUser`, `IUserLookup`
* `IWebAuthnService`, `IWebAuthnChallengeCache`

Main use cases:

* `CreateKeyUseCase`
* `RotateKeyUseCase`
* `DeleteKeyUseCase`
* `ListKeysUseCase`
* `GetPublicKeyUseCase`
* `SignDigestUseCase`
* `SignDocumentUseCase` (file signing: PAdES for PDFs, PKCS#7 for other files)
* `VerifySignedFileUseCase`
* `GenerateCsrUseCase`, `SubmitCsrToCAUseCase`, `CompleteCertificateEnrollmentUseCase`
* `ImportCertificateUseCase`, `RevokeCertificateUseCase`, `GetActiveCertificateUseCase`
* `GetAuditLogsUseCase`

Each use case:

* is registered as a scoped service
* accepts a command record with the explicit inputs; the acting user (`Actor`, `UserId`) comes from `ICurrentUser`, and audit records currently pass `requestId` as `null`
* loads data from repository abstractions
* enforces business validity
* invokes HSM or signing abstractions
* writes audit records
* returns a result record

Use cases are concrete classes and are injected directly into controllers by concrete type.

### `CryptoManager.Infrastructure`

This project implements all external behavior required by Application.

#### HSM

Current implementations:

* `SoftHsmProvider`: development implementation, in memory, optionally persisted to a JSON file (`SoftHsm:FilePath`)
* `Pkcs11HsmProvider`: hardware-backed implementation using Pkcs11Interop (for example YubiHSM); currently supports `RSA_PSS_SHA256` only

`SoftHsmProvider`:

* uses an in-memory `ConcurrentDictionary`
* supports RSA 2048 and ECDSA P-256 keys
* generates key pairs; X.509 certificates are built by the certificate factory and CA, which sign through the provider
* if `FilePath` is not set, keeps key material only in process memory and loses it on process restart
* if `FilePath` is set, writes the private keys as PKCS#8 PEM to that JSON file (plain text, development only) and reloads them on startup

`Pkcs11HsmProvider`:

* finds the token by `TokenLabel` and reports itself available only while that token is present
* creates, signs with, exports the public key of, and destroys RSA keys on the token

`HsmProviderRegistry`:

* holds all providers configured under `HsmProviders`, keyed by `Id`
* `Resolve(id)` routes operations on existing keys to the provider recorded on the key version
* `ResolveDefault()` and `ResolveFirstAvailable()` choose the provider for new keys and rotations

#### Persistence

Implemented in `Persistence/EntityFramework`:

* `AppDbContext`
* repositories: `KeyRepository`, `CertificateRepository`, `SystemCertificateAuthorityRepository`, `AuditRepository`, `UserCredentialRepository`
* `EfUnitOfWork`
* `AuditSink`
* migrations, including `InitialReset` and `AddWebAuthnCredentials`

Also includes `InMemoryKeyRepository` and `InMemoryAuditRepository` in `Persistence/InMemory` for non-database scenarios and potential testing.

#### File signing

Main components:

* `SignedArtifactBuilder`
* `PadesSigner` (uses `HsmBackedPdfSigner` to sign through the HSM, with optional visible stamp)
* `BouncyCastlePkcs7AttachedSigner`
* `SignedFileVerifier` (verification of signed PDFs and `.p7m` files)

`SignedArtifactBuilder` routes by file content:

* PDF magic bytes `%PDF-` → PAdES signing
* anything else → PKCS#7 attached signing

Document signing currently rejects ECDSA; only `RSA_PSS_SHA256` keys can sign files.

#### Identity, authentication, and certificates

* `ApplicationUser` and `ApplicationRole` on ASP.NET Core Identity with EF Core stores
* `TokenService`: issues HS256 JWTs (`Jwt:ExpiryMinutes`, default 60)
* `CurrentUser`: implements `ICurrentUser` over `HttpContext`
* `WebAuthnService` (Fido2.AspNet) and `InMemoryWebAuthnChallengeCache` (single-instance only; challenges are one-time use)
* `SoftSelfSignedCertificateAuthority` issues certificates from a soft CA; `SoftCaBootstrapper` creates its key pair on startup in the first available HSM provider
* `BouncyCastleCsrBuilder` and `BouncyCastleCertificateValidator`

#### Other implementations

* `SystemClock`
* `InterFontResolver` (Inter font, SIL OFL, for PDF signature stamps)

### `CryptoManager.API`

This is the ASP.NET Core entry point.

Contains:

* controllers: `AuthController`, `WebAuthnController`, `KeysController`, `CertificatesController`, `CryptoController`, `SystemController`, `AuditController`
* request/response DTOs
* `ErrorHandlingMiddleware`
* `Program.cs`

Main responsibilities:

* parse HTTP input
* convert strings to domain value objects
* handle file upload input
* invoke use cases
* map results to HTTP responses
* set custom response headers for file signing
* configure DI, JWT authentication, authorization policies, EF migrations, Swagger, CORS, and WebAuthn
* on startup: apply migrations, initialize the soft CA, seed the `Admin` and `Operator` roles, and create the default admin user

### Important backend design choices

* use cases, not controllers, own business orchestration
* infrastructure is replaceable through interfaces
* the HSM integration is abstracted through `IHsmProvider`
* HSM providers are created once at startup from the `HsmProviders` configuration and held by a singleton `IHsmProviderRegistry`; use cases resolve providers through the registry rather than injecting `IHsmProvider` directly
* the in-memory `SoftHsmProvider` owns its key store, so the registry must stay a singleton; a per-request provider would lose key continuity across requests
* audit writes happen near the end of each operation so the recorded outcome reflects the actual result
* actor identity comes from `ICurrentUser` (the authenticated username, and the user id for ownership checks); there is no hard-coded actor anymore
* each key has an owner (`OwnerId`); non-admin users can only operate on their own keys

---

## 4. Frontend Architecture

The frontend is an Angular application located under `src/frontend/crypto-manager`.

It uses:

* standalone components
* Angular signals
* `rxResource`
* PrimeNG for UI components and messaging
* Tailwind CSS (with the `tailwindcss-primeui` theme bridge)

### Main frontend structure

`src/app/` contains:

* `app.routes.ts` and `app.config.ts`: route table and providers (HTTP client with auth interceptor, PrimeNG theme)
* `auth/`

  * login, register, and passkey management pages
  * `auth.guard.ts` and `admin.guard.ts` (route guards)
  * `auth.interceptor.ts` (adds the Bearer token) and `auth.service.ts`
  * `webauthn/`: passkey client (`webauthn.service.ts`, `webauthn-utils.ts`)
* `core/`

  * `api/`: API client and models
  * `services/`: frontend utility services
* `features/`

  * `audit-log/`, `certificates/`, `dashboard/`, `keys/`, `sign/`, `users/`, `verify/`
* `layout/`

  * root layout/app shell
* `shared/components/`

  * reusable UI pieces such as `card`

### Key frontend services

**`CryptoManagerApi`**

* wraps `HttpClient`
* provides one method per backend endpoint (keys, crypto, certificates, audit log, system CA download)
* handles file-signing response as `HttpResponse<Blob>`
* extracts custom headers

**`KeysService`** (`features/keys/`)

* manages key-related data
* exposes reactive key list behavior via `rxResource`

**`CertificatesService`** (`features/certificates/`)

* exposes the active certificate of a key via `rxResource`

**`AuditLogService`** (`features/audit-log/`)

* holds the audit log query state (page, page size, filters) and loads pages via `rxResource`

**`AuthService`** (`auth/`)

* login and registration, and the session token (kept in `sessionStorage`)
* `isAdmin()` drives the admin-only UI and the `adminGuard`

**`WebAuthnService`** (`auth/webauthn/`)

* runs the passkey registration and login ceremonies against the `/api/auth/webauthn` endpoints
* lists, renames, and deletes the user's passkeys

**`CryptoService`** (`core/services/`)

* computes SHA-256 digests with Web Crypto, falling back to `@noble/hashes` on non-secure (plain HTTP) origins
* verifies ECDSA P-256 and RSA-PSS signatures client-side for non-secure contexts
* provides PEM/buffer helpers

**`DownloadService`** (`core/services/`)

* file download helpers (binary buffers and PEM)

**`AppMessenger`** (`core/services/`)

* wraps PrimeNG `MessageService`
* standardizes user-facing toast messages

**`Signer`** and **`Verifier`** (`features/sign/signer.ts`, `features/verify/verifier.ts`)

* feature-level helpers that orchestrate digest/file signing and signature verification

### Routes and main pages

| Route | Page |
| --- | --- |
| `/login`, `/register` | sign-in and registration (public) |
| `/dashboard` | landing page |
| `/keys` | key management |
| `/keys/:keyId/certificates` | certificates of a key |
| `/sign` | digest and file signing |
| `/verify` | signature and signed-file verification |
| `/audit-log` | audit log |
| `/users` | admin only; currently a placeholder |
| `/account/passkeys` | passkey management |

All routes except login and register are guarded by `authGuard`.

**Keys area**

* `KeysPage`: table and orchestration for key management
* create dialog
* rotate dialog
* delete dialog
* public key dialog

**Certificates area**

* `CertificatesPage`: certificate status for a key
* issue dialog (CSR enrollment), import dialog, revoke dialog, detail dialog

**Sign area**

* `SignPage`: digest signing and file signing UI, including a stamp position dialog for PDFs

**Verify area**

* `VerifyPage`: verifies a detached signature file or a signed file, with the public key selected, pasted, or uploaded

**Auth area**

* login page (password, and passkey sign-in)
* register page
* passkeys page (add, rename, and remove passkeys)

### Frontend communication patterns

* JSON is used for normal API calls
* `multipart/form-data` is used for file signing and file verification
* file signing responses are returned as binary and include custom metadata headers
* every API call carries the session's Bearer token via `authInterceptor`
* errors are surfaced through toast notifications

### Frontend architectural style

The frontend is organized by feature rather than by global technical type. This keeps related UI, dialogs, and services close together and makes feature ownership easier to understand.

Large business features currently include:

* key management
* certificates
* signing and verification
* audit log

The frontend is already structured in a way that can support further feature growth cleanly.

---

## 5. Repository Structure

Top-level layout:

```text
CryptoManager/
├── docker-compose.yml
├── compose.prod.yml
├── docs/
├── packages/
│   └── yubihsm-runtime/
└── src/
    ├── backend/
    └── frontend/
```

### Backend

`src/backend/`

* `CryptoManager.sln`: backend solution file
* `CryptoManager.Domain/`: business model and rules
* `CryptoManager.Application/`: use cases and abstractions
* `CryptoManager.Infrastructure/`: infrastructure implementations
* `CryptoManager.API/`: HTTP API entry point
* `CryptoManager.Tests/`: xUnit tests

Within Infrastructure:

* `HSM/SoftHSM/`: development HSM implementation
* `HSM/PKCS11/`: PKCS#11 provider (YubiHSM)
* `Crypto/`: file-signing, CSR, certificate authority, and certificate validation logic
* `Identity/`: users, JWT, current user, and `WebAuthn/` (passkey) services
* `Persistence/EntityFramework/`: EF Core persistence and migrations
* `Persistence/InMemory/`: in-memory persistence
* `Auditing/`: audit sinks
* `Pdf/`: PDF font resolution
* `Time/`: system clock

Within API:

* `Controllers/`
* `DTOs/`
* `Middleware/`
* `Program.cs`

`packages/yubihsm-runtime/` holds the YubiHSM PKCS#11 runtime configuration used by the `Pkcs11` provider.

### Frontend

`src/frontend/crypto-manager/`

* Angular application
* `src/app/auth/`: login, register, passkeys, guards, and the auth interceptor
* `src/app/core/`: shared API and utility services
* `src/app/features/`: main features (audit log, certificates, dashboard, keys, sign, users, verify)
* `src/app/layout/`: shell
* `src/app/shared/`: reusable components
* `Dockerfile`
* `nginx.conf`

### What belongs where

As a rule:

* put business rules in Domain
* put business operations and orchestration in Application use cases
* put DB, HSM, PDF, PKCS#7, and other external integrations in Infrastructure
* put HTTP request/response handling in API
* put Angular feature UI inside feature folders under `features/`

---

## 6. Key Workflows

### Key creation

Flow:

1. `POST /api/Keys` (requires `CanOperate`)
2. `KeysController.Create`
3. `CreateKeyUseCase` (key names must be unique per owner)
4. `IHsmProvider.CreateSigningKeyAsync` on the first available provider
5. repository persistence
6. audit record written
7. result returned with key metadata and PEM

Output:

* `KeyId`
* primary version
* public key material

A new key has no certificate yet. Enroll or import one before signing files (see Certificate enrollment below).

### Key rotation

Flow:

1. `POST /api/Keys/{keyId}/rotate`
2. `RotateKeyUseCase`
3. new key material created through HSM
4. new version promoted to primary
5. audit record written
6. result returned

Output:

* new primary version
* public key material

### Digest signing

Flow:

1. `POST /api/Crypto/sign`
2. `CryptoController.SignDigest`
3. `SignDigestUseCase`
4. aggregate and version loaded
5. domain checks performed
6. HSM signs digest
7. audit record written
8. signature returned

Output:

* base64 signature
* signature encoding
* audit event ID

### File signing

Flow:

1. `POST /api/Crypto/sign-file` (requires `CanOperate`; request limit 50 MB)
2. controller parses `multipart/form-data`
3. uploaded file is read into bytes
4. `SignDocumentUseCase` checks ownership, key state, primary version, allowed mechanism, and that an active certificate exists for that version; an expired certificate is rejected and the expiry is recorded
5. `ISignedArtifactBuilder.SignAsync` is called
6. file content is inspected
7. signing path selected:

   * PDF → `IPadesSigner`
   * other → `IPkcs7AttachedSigner`
8. signed file bytes returned with metadata headers

Signed-file verification (`POST /api/Crypto/verify-file`) runs `VerifySignedFileUseCase` on the uploaded file and returns whether the signature is valid.

Output:

* signed file binary
* headers:

  * `X-Audit-Event-Id`
  * `X-Key-Id`
  * `X-Key-Version`
  * `X-Mechanism`
  * `X-Signed-Format`
  * `X-File-Name`

### Key deletion

Flow:

1. `DELETE /api/Keys/{keyId}` (requires `Admin`)
2. `DeleteKeyUseCase`
3. active and primary key versions destroyed through the HSM
4. key state set to deleted and the record removed from the repository
5. audit record written

Output:

* `200 OK`

### Public key retrieval

Flow:

1. `GET /api/Keys/{keyId}/public?version=N` (`version` is optional; the primary version is used by default)
2. `GetPublicKeyUseCase`
3. public key material loaded
4. PEM returned

### Certificate enrollment

Flow (soft CA):

1. `POST /api/keys/{keyId}/certificates/enroll` with subject fields
2. `GenerateCsrUseCase` builds a CSR for the key's primary version
3. `SubmitCsrToCAUseCase` submits the CSR to the active CA and stores a pending certificate
4. `POST /api/keys/{keyId}/certificates/enrollments/{enrollmentId}/complete`
5. `CompleteCertificateEnrollmentUseCase` activates the certificate; the soft CA completes immediately

Other operations:

* `POST /api/keys/{keyId}/certificates/import` imports an externally issued DER certificate whose public key must match the primary version
* `POST /api/keys/{keyId}/certificates/{certId}/revoke` revokes a certificate
* `GET /api/keys/{keyId}/certificates/active` returns the active certificate
* `GET /api/system/ca-certificate` downloads the soft CA certificate (requires authentication)

### Login

* `POST /api/auth/login` checks the password and returns a JWT with the user's roles
* passkey login uses `POST /api/auth/webauthn/login/begin` and `login/complete`, which also return a JWT
* passkeys are registered with `register/begin` and `register/complete` by an already signed-in user
* `POST /api/auth/register` creates a user with the `Operator` role

---

## 7. Security Model

The security model is still development-stage. Authentication and authorization exist, but several hardening items are open (see below).

### Current state

**Authentication**

* ASP.NET Core Identity users with password login (`POST /api/auth/login`) that return a JWT (HS256, `Jwt:ExpiryMinutes`, default 60 minutes)
* passkey (WebAuthn/FIDO2) login as an alternative to passwords
* password rules: at least 8 characters, with an uppercase letter and a digit
* a fallback policy requires an authenticated user on every endpoint unless it is explicitly marked `[AllowAnonymous]` (login, register, and the WebAuthn login endpoints)
* the default `Admin` user is created at startup from `DefaultAdmin:*` configuration
* `POST /api/auth/register` is open to anyone and creates `Operator` accounts; restrict it before any exposure

**Authorization**

* roles: `Admin` and `Operator`
* policy `AdminOnly` (role `Admin`): key deletion
* policy `CanOperate` (roles `Admin` or `Operator`): key listing, creation, rotation, public key retrieval, digest and file signing, file verification, certificate operations, and the audit log
* non-admin users only see and operate on keys they own; admins see all keys
* non-admin users only see their own audit events

**Transport and exposure**

* current CORS setup is permissive (any origin, header, and method) and suitable only for development
* Swagger is enabled only in the `Development` environment
* HTTPS redirection is enabled; restrict CORS before any public or production exposure

### Crypto and signing security

* private key material is abstracted behind `IHsmProvider`
* key metadata is persisted in PostgreSQL
* key material itself is not stored in the DB; with the `SoftHsm` provider it is kept in memory, or in a plain-text JSON file when `SoftHsm:FilePath` is set (development only)
* the soft CA's signing key lives in an HSM provider (the first available one at bootstrap); its certificate is stored in the DB
* key references are stored as opaque `ProviderRef` values
* allowed mechanisms are validated before HSM use
* file signing requires an active, unexpired certificate for the key's primary version
* all sensitive operations are audited

### Audit coverage

The following operations are audited:

* key creation
* key rotation
* digest signing
* file signing
* key deletion
* retrieving a public key from a key pair
* certificate issuance (enrollment completion), import, and revocation
* certificate expiry detected at signing time

`AuditAction.DisableKey` exists and `Key.Disable()` is defined in the domain, but no use case currently disables keys, so key disabling is not audited yet.

### HSM model

The system supports hardware HSMs through the `Pkcs11` provider, but only RSA-PSS SHA-256 keys are implemented there. The `SoftHsm` provider is for development and is in-memory (with optional plain-text file persistence). It is not appropriate for production key custody unless the configured default provider is a secured hardware HSM.

### Important developer note

Before production use, the following are required at minimum:

* restrict or remove self-registration (`POST /api/auth/register`) and control how roles are assigned
* production-safe CORS
* production-safe Swagger exposure (keep it Development-only)
* real hardware-backed or secure software-backed key storage (no plain-text key files)
* secure secret management for configuration (`Jwt:Secret`, `DefaultAdmin:Password`, database password, and PKCS#11 `UserPin`)
* a shared challenge store for WebAuthn if the API runs on more than one instance

---

## 8. Development Setup

### Required software

* .NET SDK 9.0
* Node.js compatible with npm 11.x
* npm 11.6.2
* Angular CLI 21.1.2
* PostgreSQL 17
* Docker and Docker Compose

### Backend setup

From `src/backend/`:

```bash
dotnet build CryptoManager.sln
dotnet run --project CryptoManager.API
```

The API:

* runs EF migrations on startup, initializes the soft CA, and seeds the `Admin` and `Operator` roles and the default admin user
* exposes Swagger at `/swagger` in the `Development` environment only
* listens on `http://localhost:5078` with `dotnet run` (the `http` launch profile); the `https` profile uses `https://localhost:7225`

Required configuration is read from `appsettings.json`, which is git-ignored. Start from `appsettings.Template.json` and add the following:

* `HsmProviders`: an array of providers. Each entry needs a non-empty `Id`, a `Type` (`SoftHsm` or `Pkcs11`), and `IsDefault`. Exactly one entry must have `IsDefault: true`, and the application fails to start otherwise. The `Pkcs11` settings sit under `Pkcs11` in the same entry, and `SoftHsm:FilePath` is optional.
* `Jwt`: `Issuer`, `Audience`, and `ExpiryMinutes`. `Jwt:Secret` is required and should come from user secrets.
* `DefaultAdmin`: `UserName` and `Email`. `DefaultAdmin:Password` is required and should come from user secrets.
* `WebAuthn`: `ServerDomain`, `ServerName`, `Origins` (at least one, for example `http://localhost:4200`), `TimeoutMs`, and `ChallengeTtlSeconds`.

Database connection is configured through:

* `appsettings.json`
* environment variable override:

  * `ConnectionStrings__DefaultConnection`

### Frontend setup

From `src/frontend/crypto-manager/`:

```bash
npm install
npm start
```

The dev server runs on `http://localhost:4200`. The frontend uses `environment.development.ts`, which points at `https://localhost:44351` (the IIS Express SSL port in `launchSettings.json`). If you run the API with `dotnet run`, change that URL to the port you use. Passkeys only work when the page origin is listed in `WebAuthn:Origins`.

### HSM configuration

Providers are defined by the `HsmProviders` array in `appsettings.json`. Program.cs builds them from configuration, so adding or switching a provider needs no code change.

Development provider:

* `"Type": "SoftHsm"`
* optional `SoftHsm:FilePath`; without it, keys are lost on API restart

PKCS#11 provider (for example YubiHSM), set per entry under `Pkcs11`:

* `Pkcs11:LibraryPath`
* `Pkcs11:TokenLabel`
* `Pkcs11:UserPin`
* `Pkcs11:RsaKeySizeBits` (optional, default 2048)

### Secrets

For local development, prefer user secrets rather than committing local credentials:

```bash
cd src/backend/CryptoManager.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cryptomanager;Username=postgres;Password=<password>"
dotnet user-secrets set "Jwt:Secret" "<at-least-32-character-secret>"
dotnet user-secrets set "DefaultAdmin:Password" "<admin-password>"
```

---

## 9. Running the System

### Option A: Run backend and frontend locally

Start PostgreSQL, then:

Backend:

```bash
cd src/backend
dotnet run --project CryptoManager.API
```

Frontend:

```bash
cd src/frontend/crypto-manager
npm install
npm start
```

### Option B: Run full stack with Docker Compose

From repository root:

```bash
docker compose up
```

Development compose starts:

* PostgreSQL (user `cryptouser`, database `cryptomanager`)
* API on `http://localhost:8080`, configured through environment variables in `docker-compose.yml`
* frontend (nginx) on `http://localhost:99`, which proxies `/api/` to the API

`compose.prod.yml` defines a production-style stack that reads its settings from `.env`. It publishes the API on `127.0.0.1:5000` and the frontend on `127.0.0.1:3000`.

### Useful development commands

Backend:

```bash
dotnet build CryptoManager.sln
dotnet run --project CryptoManager.API
dotnet test
dotnet publish CryptoManager.API -c Release -o ./publish
```

Frontend:

```bash
npm install
npm start
npm run build
npm run watch
npm test
```

Docker:

```bash
docker compose up
docker compose up --build
docker compose up db
docker compose down
```

### EF migrations

From `src/backend`:

```bash
dotnet ef migrations list --project CryptoManager.Infrastructure --startup-project CryptoManager.API
dotnet ef migrations add <MigrationName> --project CryptoManager.Infrastructure --startup-project CryptoManager.API
```

Migrations are applied automatically on API startup.

---

## 10. Testing

The repository has a small set of automated tests: a backend xUnit project and a frontend Vitest spec.

### Current state

* backend: `CryptoManager.Tests` (xUnit, run with `dotnet test`) covers the WebAuthn challenge cache and sign-count handling
* frontend: `webauthn-utils.spec.ts` (Vitest, run with `npm test`) covers the base64url helpers
* coverage is still small; the domain, use cases, and controllers have no tests yet

### Testability of the architecture

The architecture is test-friendly:

* use cases depend on interfaces
* repositories and HSM access are abstracted
* `InMemoryKeyRepository` exists and could support test scenarios
* `IClock` allows deterministic time-dependent behavior
* the caller's identity comes through `ICurrentUser`, so use cases can be tested without HTTP

### Recommended initial testing priorities

A good starting test strategy would be:

**Backend**

* unit tests for domain entities and value objects
* use case tests with mocked or in-memory dependencies
* integration tests for API + EF Core behavior
* file-signing tests for output format routing

**Frontend**

* tests for services such as `CryptoManagerApi` and `KeysService`
* component tests for key workflows
* form behavior tests for sign and key dialogs

---

## 11. Adding New Features

When adding a new feature, follow the existing architectural boundaries.

### Backend feature workflow

1. identify the business capability
2. add or extend domain concepts if the feature changes business rules
3. create a new use case in Application
4. add or extend interfaces only if a real new dependency is needed
5. implement infrastructure details in Infrastructure
6. expose the feature through an API controller and DTOs
7. add auditing if the operation is sensitive

### Backend placement guidelines

* Domain: invariants, state changes, business meaning
* Application: use case orchestration
* Infrastructure: database, crypto libraries, external services
* API: parsing HTTP, mapping DTOs, response formatting

### Frontend feature workflow

1. add the feature under the appropriate area in `src/app/features/`
2. keep UI grouped by feature
3. add or extend API client methods in `core/api/`
4. place reusable business-related UI logic in feature services, not global dumping grounds
5. use reactive forms for non-trivial form workflows
6. keep binary download handling and crypto helpers inside dedicated services

### Common feature examples

**Adding a new signing mechanism**

* update mechanism/domain representation
* extend validation logic
* extend both HSM implementations (`SoftHsmProvider` and `Pkcs11HsmProvider`)
* verify output handling
* surface the option in frontend forms

**Adding a new file-signing format**

* extend `SignedArtifactBuilder`
* add a new signing abstraction/implementation if needed
* keep format-routing logic contained in Infrastructure
* avoid leaking file-format decisions into use cases

**Adding a protected endpoint**

* authentication is already enforced globally; new controllers are protected by default
* add `[Authorize(Policy = "CanOperate")]` or `[Authorize(Policy = "AdminOnly")]` for role-restricted operations
* use `[AllowAnonymous]` only for endpoints that must be public
* the use case reads the caller from `ICurrentUser`; do not read claims from `HttpContext` in use cases

---

## 12. Contribution Guidelines

### General expectations

When working on this project:

* preserve Clean Architecture boundaries
* do not let controllers accumulate business logic
* do not let infrastructure concerns leak into Application or Domain
* keep domain rules inside domain types where possible
* keep feature organization clear on the frontend

### Backend contribution rules

* prefer one use case per business operation
* inject dependencies through constructors
* keep use cases explicit and focused
* use interfaces only at real boundaries
* do not bypass aggregate rules by mutating state from outside domain methods
* keep HSM-specific logic behind `IHsmProvider`

### Frontend contribution rules

* keep code organized by feature
* keep API communication inside API/service layers
* avoid spreading signing logic across components
* use services for shared non-trivial logic
* keep form-heavy behavior explicit and maintainable

### Security expectations

Until the remaining hardening is done, treat the system as development-only. Any contribution that moves the project toward production readiness should prioritize:

* keeping authentication and authorization intact on every endpoint
* restricting self-registration and role assignment
* secret handling
* production-safe CORS
* real HSM integration

### Recommended near-term improvements

The highest-value improvements for new contributors are likely:

* expand automated tests (use cases, controllers, and the signing and certificate flows)
* extend the PKCS#11 provider beyond RSA-PSS SHA-256 and test it against real hardware
* replace the in-memory WebAuthn challenge cache if the API is scaled out
* harden configuration and production exposure
* document API contracts and deployment behavior more formally

---

## Quick Start Summary

For a new developer, the simplest path is:

1. install .NET 9, Node, npm, Docker, and PostgreSQL
2. run PostgreSQL locally or via Docker
3. start the backend with `dotnet run --project CryptoManager.API`
4. start the frontend with `npm install` and `npm start`
5. log in as the default admin (or register), then exercise key creation, certificate enrollment, and file signing (Swagger is available at `/swagger` in Development)
6. review the Domain, Application, and Infrastructure project boundaries before making changes