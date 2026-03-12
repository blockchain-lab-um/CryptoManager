# Developer Onboarding Guide

## 1. Project Overview

CryptoManager is a cryptographic key management and signing system. It provides a REST API and Angular frontend for managing asymmetric signing keys and performing HSM-backed signing operations.

The system currently supports:

* creating signing keys
* rotating key versions
* deleting keys
* retreiving public keys in PEM format
* signing SHA-256 digests
* signing files

  * PDF files are signed with PAdES
  * non-PDF files are signed as attached PKCS#7/CMS (`.p7m`)
* auditing all sensitive operations

The current implementation is designed as an MVP with a clear path toward real hardware-backed HSM integration. In development, signing keys are stored in an in-memory software HSM implementation. The architecture is intentionally structured so that the HSM implementation can later be swapped for a PKCS#11-based provider with minimal impact on the rest of the system.

Main technologies used:

* Backend: .NET 9, ASP.NET Core, Entity Framework Core, PostgreSQL, BouncyCastle, PDFsharp
* Frontend: Angular 21, standalone components, signals, `rxResource`, PrimeNG
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

* the API layer depends on Application
* the Application layer depends on Domain
* the Infrastructure layer depends on Application and Domain to implement required abstractions
* Domain remains isolated from framework and infrastructure concerns

The composition root is `Program.cs`, where dependency injection connects abstractions to concrete implementations.

### Layer responsibilities

**Domain**
Owns business concepts and rules:

* entities such as `Key`, `KeyVersion`, `AuditEvent`
* value objects such as `KeyId`, `Mechanism`, `ProviderRef`, `PublicKeyMaterial`
* enums for key states, audit actions, and version status
* invariant enforcement and guard logic

**Application**
Defines what the system does:

* use cases for business operations
* contracts/interfaces for infrastructure dependencies
* command and result models
* orchestration of domain rules and external services

**Infrastructure**
Implements external concerns:

* HSM provider implementations
* persistence with EF Core
* file-signing implementations
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
* use cases are HTTP-agnostic and receive explicit parameters instead of reading ambient HTTP context
* infrastructure details such as EF Core, BouncyCastle, PDFsharp, or HSM internals are hidden behind interfaces
* file type routing is content-based rather than trusting request metadata

---

## 3. Backend Architecture

The backend is split into four main projects under `src/backend/`.

### `CryptoManager.Domain`

This project has no external dependencies. It contains the core business model.

Main contents:

* entities: `Key`, `KeyVersion`, `AuditEvent`
* value objects: `KeyId`, `Mechanism`, `ProviderRef`, `PublicKeyMaterial`
* enums: `KeyState`, `KeyVersionStatus`, `KeyPurpose`, `Mechanism`, `AlgorithmFamily`, `SignatureEncoding`, `AuditAction`
* validation helpers such as `Guard`
* domain exceptions

This layer enforces business rules such as:

* allowed state transitions
* allowed signing mechanisms per key
* active/primary version handling
* key lifecycle rules

### `CryptoManager.Application`

This project defines the use cases and required abstractions.

Key interfaces:

* `IKeyRepository`
* `IHsmProvider`
* `IAuditSink`
* `IClock`
* `ISignedArtifactBuilder`

Main use cases:

* `CreateKeyUseCase`
* `RotateKeyUseCase`
* `SignDigestUseCase`
* `SignFileUseCase`
* `GetPublicKeyUseCase`
* `ListKeysUseCase`
* `DeleteKeyUseCase`

Each use case:

* is registered as a scoped service
* accepts explicit inputs, plus `actor` and `requestId`
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

* `SoftHsmProvider`: active development implementation, in-memory
* `Pkcs11HsmProvider`: future hardware-backed implementation stub

`SoftHsmProvider`:

* uses an in-memory `ConcurrentDictionary`
* supports RSA 2048 and ECDSA P-256
* generates self-signed X.509 certificates
* stores key material only in process memory
* loses all key material on process restart

#### Persistence

Implemented in `Persistence/EntityFramework`:

* `AppDbContext`
* `KeyRepository`
* `AuditSink`

Also includes `InMemoryKeyRepository` for non-database scenarios and potential testing.

#### File signing

Main components:

* `SignedArtifactBuilder`
* `PadesSigner`
* `BouncyCastlePkcs7AttachedSigner`

`SignedArtifactBuilder` routes by file content:

* PDF magic bytes `%PDF-` → PAdES signing
* anything else → PKCS#7 attached signing

#### Other implementations

* `SystemClock`

### `CryptoManager.API`

This is the ASP.NET Core entry point.

Contains:

* `KeysController`
* `CryptoController`
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
* configure DI, EF migrations, Swagger, CORS

### Important backend design choices

* use cases, not controllers, own business orchestration
* infrastructure is replaceable through interfaces
* the HSM integration is abstracted through `IHsmProvider`
* `IHsmProvider` is registered as a singleton in the current SoftHSM setup because the in-memory provider itself owns the key store; changing it to scoped would break key continuity across requests
* audit writes happen near the end of each operation so the recorded outcome reflects the actual result
* actor identity is currently passed explicitly as `"dev"` rather than hidden behind HTTP context

---

## 4. Frontend Architecture

The frontend is an Angular application located under `src/frontend/crypto-manager`.

It uses:

* standalone components
* Angular signals
* `rxResource`
* PrimeNG for UI components and messaging

### Main frontend structure

`src/app/` contains:

* `core/`

  * `api/`: API client and models
  * `services/`: frontend utility services
* `layout/`

  * root layout/app shell
* `pages/`

  * `keys/`
  * `sign/`

### Key frontend services

**`CryptoManagerApi`**

* wraps `HttpClient`
* provides one method per backend endpoint
* handles file-signing response as `HttpResponse<Blob>`
* extracts custom headers

**`KeysService`**

* manages key-related data
* exposes reactive key list behavior via `rxResource`

**`CryptoService`**

* uses Web Crypto API for SHA-256 digesting
* provides base64/buffer helpers
* provides file download helpers

**`AppMessenger`**

* wraps PrimeNG `MessageService`
* standardizes user-facing toast messages

### Main pages and components

**Keys area**

* `KeysComponent`: table and orchestration for key management
* create dialog
* rotate dialog
* delete dialog
* public key dialog

**Sign area**

* `SignComponent`: digest signing and file signing UI

### Frontend communication patterns

* JSON is used for normal API calls
* `multipart/form-data` is used for file signing
* file signing responses are returned as binary and include custom metadata headers
* errors are surfaced through toast notifications

### Frontend architectural style

The frontend is organized by feature rather than by global technical type. This keeps related UI, dialogs, and services close together and makes feature ownership easier to understand.

Large business features currently include:

* key management
* signing

The frontend is already structured in a way that can support further feature growth cleanly.

---

## 5. Repository Structure

Top-level layout:

```text
C:\PROJEKTI\CryptoManager\
├── docker-compose.yml
├── compose.prod.yml
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

Within Infrastructure:

* `HSM/SoftHSM/`: development HSM implementation
* `HSM/Pkcs11/`: PKCS#11 provider stub
* `Crypto/`: file-signing logic
* `Persistence/EntityFramework/`: EF Core persistence
* `Persistence/InMemory/`: in-memory persistence

Within API:

* `Controllers/`
* `DTOs/`
* `Middleware/`
* `Program.cs`

### Frontend

`src/frontend/crypto-manager/`

* Angular application
* `src/app/core/`: shared API and utility services
* `src/app/layout/`: shell
* `src/app/pages/`: main features
* `Dockerfile`
* `nginx.conf`

### What belongs where

As a rule:

* put business rules in Domain
* put business operations and orchestration in Application use cases
* put DB, HSM, PDF, PKCS#7, and other external integrations in Infrastructure
* put HTTP request/response handling in API
* put Angular feature UI inside feature folders under `pages/`

---

## 6. Key Workflows

### Key creation

Flow:

1. `POST /api/Keys`
2. `KeysController.Create`
3. `CreateKeyUseCase`
4. `IHsmProvider.CreateSigningKeyAsync`
5. repository persistence
6. audit record written
7. result returned with key metadata and PEM

Output:

* `KeyId`
* primary version
* public key material

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
2. `CryptoController.Sign`
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

1. `POST /api/Crypto/sign-file`
2. controller parses `multipart/form-data`
3. uploaded file is read into bytes
4. `SignFileUseCase` validates key and version
5. `ISignedArtifactBuilder.SignAsync` is called
6. file content is inspected
7. signing path selected:

   * PDF → `IPadesSigner`
   * other → `IPkcs7AttachedSigner`
8. signed file bytes returned with metadata headers

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

1. `DELETE /api/Keys/{keyId}`
2. `DeleteKeyUseCase`
3. all key versions destroyed through HSM
4. key marked deleted in repository
5. audit record written

Output:

* `200 OK`

### Public key retrieval

Flow:

1. `GET /api/Keys/{keyId}/public?version=N`
2. `GetPublicKeyUseCase`
3. public key material loaded
4. PEM returned

---

## 7. Security Model

The current security model is intentionally incomplete and should be treated as development-stage only.

### Current state

**Authentication**

* not implemented
* controllers use hardcoded actor `"dev"`

**Authorization**

* not implemented
* all endpoints are effectively open

**Transport and exposure**

* current CORS setup is permissive and suitable only for development
* Swagger is enabled in all environments
* this should be restricted before any public or production exposure

### Crypto and signing security

* private key material is abstracted behind `IHsmProvider`
* key metadata is persisted in PostgreSQL
* key material itself is not stored in the DB in the current SoftHSM setup
* key references are stored as opaque `ProviderRef` values
* allowed mechanisms are validated before HSM use
* all sensitive operations are audited

### Audit coverage

The following operations are audited:

* key creation
* key rotation
* digest signing
* file signing
* key deletion
* key disabling
* retreiving a public key from a key pair

### HSM model

The system is designed to support a real PKCS#11 HSM, but the current active implementation is in-memory and ephemeral. This is appropriate for development, not for production key custody.

### Important developer note

Before production use, the following are required at minimum:

* real authentication
* role/permission-based authorization
* production-safe CORS
* production-safe Swagger exposure
* real hardware-backed or secure software-backed key storage
* secure secret management for configuration

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

* runs EF migrations on startup
* exposes Swagger at `/swagger`
* uses HTTPS development settings by default

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

The frontend uses `environment.development.ts` and expects the backend at the configured local API URL.

### HSM configuration

Default development provider:

* `SoftHsmProvider`
* no configuration required
* keys are lost on API restart

PKCS#11 configuration exists in `appsettings.json` for the future provider:

* `Pkcs11:LibraryPath`
* `Pkcs11:TokenLabel`
* `Pkcs11:UserPin`
* `Pkcs11:RsaKeySizeBits`

To switch to PKCS#11, update DI registration in `Program.cs`.

### Secrets

For local development, prefer user secrets rather than committing local credentials:

```bash
cd src/backend/CryptoManager.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cryptomanager;Username=postgres;Password=yourpassword"
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

* PostgreSQL
* API on `http://localhost:8080`
* frontend on `http://localhost:99`

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

No backend or frontend test projects were identified in the repository snapshot used for this analysis.

### Current state

* no `.Tests.csproj` projects found
* no Angular `.spec.ts` tests found

### Testability of the architecture

Despite the lack of tests, the architecture is test-friendly:

* use cases depend on interfaces
* repositories and HSM access are abstracted
* `InMemoryKeyRepository` exists and could support test scenarios
* `IClock` allows deterministic time-dependent behavior
* explicit actor/requestId parameters keep use cases free from HTTP dependencies

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

1. add the feature under the appropriate area in `src/app/pages/`
2. keep UI grouped by feature
3. add or extend API client methods in `core/api/`
4. place reusable business-related UI logic in feature services, not global dumping grounds
5. use reactive forms for non-trivial form workflows
6. keep binary download handling and crypto helpers inside dedicated services

### Common feature examples

**Adding a new signing mechanism**

* update mechanism/domain representation
* extend validation logic
* extend HSM implementation
* verify output handling
* surface the option in frontend forms

**Adding a new file-signing format**

* extend `SignedArtifactBuilder`
* add a new signing abstraction/implementation if needed
* keep format-routing logic contained in Infrastructure
* avoid leaking file-format decisions into use cases

**Adding auth**

* replace hardcoded actor handling
* add real authentication middleware
* pass authenticated actor identity into use case calls
* secure endpoints with authorization

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

Until security is implemented properly, treat the system as development-only. Any contribution that moves the project toward production readiness should prioritize:

* authentication
* authorization
* secret handling
* production-safe Swagger and CORS
* real HSM integration

### Recommended near-term improvements

The highest-value improvements for new contributors are likely:

* add automated tests
* implement authentication and authorization
* finish PKCS#11 provider integration
* harden configuration and production exposure
* document API contracts and deployment behavior more formally

---

## Quick Start Summary

For a new developer, the simplest path is:

1. install .NET 9, Node, npm, Docker, and PostgreSQL
2. run PostgreSQL locally or via Docker
3. start the backend with `dotnet run --project CryptoManager.API`
4. start the frontend with `npm install` and `npm start`
5. open Swagger and exercise key creation and signing flows
6. review the Domain, Application, and Infrastructure project boundaries before making changes