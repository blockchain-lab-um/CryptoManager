# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build entire solution
dotnet build CryptoManager.sln

# Run API server (Swagger UI at /swagger in Development)
dotnet run --project CryptoManager.API

# EF Core migrations (run from CryptoManager.API directory; AppDbContext lives in Infrastructure)
dotnet ef migrations add <Name> --project ../CryptoManager.Infrastructure --startup-project .
dotnet ef database update --project ../CryptoManager.Infrastructure --startup-project .
```

There is no test project in the solution today.

### Required user-secrets / configuration

Startup fails fast without these (set via `dotnet user-secrets` or env):

- `Jwt:Secret` — symmetric key for JWT bearer tokens
- `DefaultAdmin:Password` — seeds the bootstrap Admin user on first run
- `ConnectionStrings:DefaultConnection` — Postgres connection (Npgsql)
- `HsmProviders` — array; **exactly one** entry must have `IsDefault: true`. Each entry has `Id`, `Type` (`SoftHsm` | `Pkcs11`), and a matching options block (`SoftHsm` or `Pkcs11`).

## Architecture

Clean Architecture, four projects, dependency flow API → Application ← Infrastructure → Domain.

- **CryptoManager.Domain** — Entities (`Key`, `KeyVersion`, `Certificate`, audit, CA), value objects, enums, `Guard`, `DomainException`. Zero external deps.
- **CryptoManager.Application** — Use cases + abstractions (`IKeyRepository`, `IHsmProviderRegistry`, `IAuditSink`, `IClock`, `ICertificateRepository`, `ISignedArtifactBuilder`, `ICsrBuilder`, `ICertificateValidator`, `ICertificateAuthority`, `ISignedFileVerifier`, `ICurrentUser`, `IUnitOfWork`). Throws `NotFoundException` for missing aggregates.
- **CryptoManager.Infrastructure** — EF Core (Npgsql) persistence, ASP.NET Identity, JWT issuance, HSM providers, BouncyCastle/PDFsharp signing, soft CA.
- **CryptoManager.API** — Controllers, DTOs, `ErrorHandlingMiddleware` (DomainException→400, NotFoundException→404, else 500), JWT bearer + Identity wiring, startup migration & admin seeding.

## HSM Provider Model

Multiple HSM providers are registered side-by-side and selected per-key.

- Configured via the `HsmProviders` array; `Program.cs` builds an `IHsmProviderRegistry` (singleton) keyed by provider `Id`.
- `SoftHsmProvider` — in-process RSA/ECDSA, optionally persisted to a JSON file (dev/MVP). Generates a self-signed `X509Certificate2` per signing key.
- `Pkcs11HsmProvider` — real HSM via Pkcs11Interop. On Linux, `libdl` is redirected to `libdl.so.2` in `Program.cs`.
- A `Key` records its provider in `ProviderRef`; use cases resolve the provider through the registry, never directly.

## Domain Model

`Key` is the central aggregate: name, purpose, allowed-mechanisms whitelist, collection of `KeyVersion`. The first version added becomes Primary.

- Key lifecycle: Active → Disabled → Deleted.
- KeyVersion lifecycle: Primary → Active → Retired → Disabled → Destroyed.
- `Mechanism` is a closed set (RSA_PSS_SHA256, ECDSA_P256_SHA256 for MVP).
- `Certificate` is a separate aggregate linked to a `KeyVersion`; enrollment goes CSR → submit-to-CA → complete (or import). Revocation is supported.

Domain invariants are enforced via `Guard` and throw `DomainException`.

## File Signing

Routed by `ISignedArtifactBuilder` (→ `SignedArtifactBuilder`):

- PDF (magic bytes `%PDF-`) → **PAdES** via `PadesSigner` + PDFsharp `DigitalSignatureHandler` and an HSM-backed `IDigitalSigner`.
- Anything else → **PKCS#7 attached** (`.p7m`) via `BouncyCastlePkcs7AttachedSigner`.
- Verification: `SignedFileVerifier`.
- Endpoints emit `X-Audit-Event-Id`, `X-Key-Id`, `X-Key-Version`, `X-Mechanism`, `X-Signed-Format`, `X-File-Name` (CORS-exposed).

## API Endpoints (high level)

- `POST /api/auth/...` — login / token issuance (JWT, roles `Admin`, `Operator`)
- `POST /api/keys`, `POST /api/keys/{id}/rotate`, `DELETE /api/keys/{id}`, `GET /api/keys`, `GET /api/keys/{id}/public`
- `POST /api/crypto/sign` (digest), `POST /api/crypto/sign-file` (multipart), verify endpoints
- `/api/certificates/...` — generate CSR, submit to CA, complete enrollment, import, revoke, get active
- `/api/audit/...`, `/api/system/...`

Authorization: a global fallback policy requires authenticated users. Policies `AdminOnly` and `CanOperate` (Admin or Operator) gate sensitive actions. Identity cookie redirects are suppressed — the API returns 401/403.

## DI Lifetimes

- **Singletons:** `IHsmProviderRegistry`, `IClock`, `HsmBackedCertificateFactory`, `ISoftCaBootstrapper`, `ICertificateAuthority` (soft CA).
- **Scoped:** EF repositories, `IUnitOfWork`, `IAuditSink`, all use cases, signing services (`IPadesSigner`, `IPkcs7AttachedSigner`, `ISignedArtifactBuilder`, `ISignedFileVerifier`, `ICsrBuilder`, `ICertificateValidator`), `ICurrentUser`, `TokenService`.

## Startup Side-Effects

`Program.cs` runs EF migrations, bootstraps the soft CA via `ISoftCaBootstrapper.EnsureInitializedAsync`, ensures roles `Admin`/`Operator` exist, and seeds the default admin user from configuration. PDFsharp's `GlobalFontSettings.FontResolver` is wired to `SwitzerFontResolver` against the deployed `Fonts/` directory.

## Library Quirks (BouncyCastle 2.6 / PDFsharp 6.2)

- BouncyCastle 2.x has no `IContentSigner`, no `X509StoreFactory`, no `CollectionStore<T>`. Use `ISignatureFactory` + `IStreamCalculator<IBlockResult>`; implement `IStore<X509Certificate>` inline. `IBlockResult` requires the `Collect(Span<byte>)` and `GetMaxResultLength()` overloads. `SignerInfoGeneratorBuilder.Build(ISignatureFactory, X509Certificate)` is the correct call.
- PDFsharp 6.2.4 targets `net10.0`. There is no `PdfDocumentSigner`/`PdfSignatureOptions` — use `DigitalSignatureHandler.ForDocument` with an `IDigitalSigner`. `PdfSharpDefaultSigner` lives in `PdfSharp.Cryptography.dll`. Don't call `AddSignatureComponentsAsync` (XML-doc only); just `document.Save` after `ForDocument`.

## Conventions

- .NET 9.0, nullable reference types on, file-scoped namespaces.
- Rich domain model — business logic in entities, not anemic DTOs.
- Value objects as `record` / `record struct`.
- Actor identity flows via `ICurrentUser` (resolved from `HttpContext`); audit events record it.