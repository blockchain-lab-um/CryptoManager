# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build CryptoManager.sln

# Run API server (Swagger UI available at /swagger in Development)
dotnet run --project CryptoManager.API

# Run tests
dotnet test
```

## Architecture

Clean Architecture with four projects:

- **CryptoManager.Domain** - Entities, value objects, enums, domain exceptions. Zero external dependencies.
- **CryptoManager.Application** - Use cases (CreateKey, RotateKey, SignDigest, GetPublicKey) and abstractions (IKeyRepository, IHsmProvider, IAuditSink, IClock).
- **CryptoManager.Infrastructure** - Implementations: SoftHsmProvider (in-memory RSA/ECDSA), InMemoryKeyRepository, InMemoryAuditSink, SystemClock. All registered as singletons.
- **CryptoManager.API** - ASP.NET Core controllers (KeysController, CryptoController), DTOs, ErrorHandlingMiddleware. Use cases registered as scoped.

Dependency flow: API -> Application <- Infrastructure -> Domain. Infrastructure implements Application abstractions.

## Domain Model

**Key** is the central aggregate. It holds a name, purpose, allowed mechanisms whitelist, and a collection of **KeyVersion** instances. Each KeyVersion maps to actual key material in an HSM via a **ProviderRef** value object.

Key lifecycle: Active -> Disabled -> Deleted. KeyVersion lifecycle: Primary -> Active -> Retired -> Disabled -> Destroyed. The first version added to a Key automatically becomes Primary.

**Mechanism** is a closed set of crypto operations (RSA_PSS_SHA256, ECDSA_P256_SHA256 for MVP). Keys whitelist which mechanisms they permit.

Domain invariants are enforced via `Guard` (throws `DomainException`). Application-layer not-found cases throw `NotFoundException`.

## API Endpoints

- `POST /api/keys` - Create a key
- `POST /api/keys/{keyId}/rotate` - Rotate a key (new version becomes primary)
- `GET /api/keys/{keyId}/public?version={version}` - Get public key (version optional, defaults to primary)
- `POST /api/crypto/sign` - Sign a digest

ErrorHandlingMiddleware maps DomainException -> 400, NotFoundException -> 404, unhandled -> 500.

## Key Conventions

- .NET 9.0, nullable reference types enabled, file-scoped namespaces
- Rich domain model with business logic in entities (not anemic)
- Value objects as `record` or `record struct` types
- Actor/auth is hardcoded to "dev" in the API layer (no auth yet)
- All infrastructure is in-memory for MVP (designed for swap-out via abstractions)
