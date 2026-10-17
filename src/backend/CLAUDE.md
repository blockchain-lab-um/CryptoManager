# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Run from src/backend

# Build entire solution
dotnet build CryptoManager.sln

# Run API server (Swagger UI at /swagger in Development; `dotnet run` uses the "http" profile, http://localhost:5078)
dotnet run --project CryptoManager.API

# Run tests (CryptoManager.Tests, xUnit)
dotnet test CryptoManager.sln

# EF Core migrations (run from CryptoManager.API directory; AppDbContext lives in Infrastructure)
dotnet ef migrations add <Name> --project ../CryptoManager.Infrastructure --startup-project .
dotnet ef database update --project ../CryptoManager.Infrastructure --startup-project .
```

Tests live in `CryptoManager.Tests` (xUnit, net9.0). Current coverage is WebAuthn only: challenge cache and sign-count handling (`CryptoManager.Tests/Infrastructure/WebAuthnChallengeCacheTests.cs`, `WebAuthnSignCountTests.cs`).

### Required user-secrets / configuration

`appsettings.json` is git-ignored; create it from `appsettings.Template.json`, which does not yet contain the sections below. Set secrets with `dotnet user-secrets` or env vars.

Startup fails fast without the first four:

- `Jwt:Secret` — symmetric key for JWT bearer tokens
- `DefaultAdmin:Password` — seeds the bootstrap Admin user on first run (user name defaults to `admin`)
- `ConnectionStrings:DefaultConnection` — Postgres connection (Npgsql)
- `HsmProviders` — array; **exactly one** entry must have `IsDefault: true`. Each entry has a unique `Id`, `Type` (`SoftHsm` | `Pkcs11`), and a matching options block (`SoftHsm` or `Pkcs11`; `Pkcs11` is required for that type).

Also required, but read lazily (the app throws when WebAuthn is first used if it is empty):

- `WebAuthn:Origins` — non-empty list of allowed origins (scheme+host+port, exactly as the browser sends them). Optional: `ServerDomain`, `ServerName`, `TimeoutMs`, `ChallengeTtlSeconds`.

## Architecture

Clean Architecture, four production projects plus the test project, dependency flow API → Application ← Infrastructure → Domain.

- **CryptoManager.Domain** — Entities (`Key`, `KeyVersion`, `Certificate`, audit, CA), value objects, enums, `Guard`, `DomainException`. Zero external deps.
- **CryptoManager.Application** — Use cases + abstractions (`IKeyRepository`, `IHsmProvider`, `IHsmProviderRegistry`, `IAuditSink`, `IClock`, `ICertificateRepository`, `ISignedArtifactBuilder`, `ICsrBuilder`, `ICertificateValidator`, `ICertificateAuthority`, `ISignedFileVerifier`, `ICurrentUser`, `IUnitOfWork`, `IUserLookup`, `IUserCredentialRepository`, `IWebAuthnService`, `IWebAuthnChallengeCache`). Throws `NotFoundException` for missing aggregates.
- **CryptoManager.Infrastructure** — EF Core (Npgsql) persistence, ASP.NET Identity, JWT issuance, HSM providers, BouncyCastle/PDFsharp signing, soft CA, WebAuthn/FIDO2 (Fido2.AspNet).
- **CryptoManager.API** — Controllers, DTOs, `ErrorHandlingMiddleware` (NotFoundException→404, ForbiddenException→403, DomainException→400, HsmUnavailableException→503, else 500 with a generic message outside Development), JWT bearer + Identity wiring, Swagger (Development only), startup migration & admin seeding.
- **CryptoManager.Tests** — xUnit tests (see Build & Run).

## HSM Provider Model

Multiple HSM providers are registered side-by-side and selected per-key.

- Configured via the `HsmProviders` array (`HsmProviderConfig`: `Id`, `Type`, `IsDefault`, `SoftHsm`, `Pkcs11`); `Program.cs` validates it and builds an `IHsmProviderRegistry` (singleton) keyed by provider `Id`, with the single default provider.
- `SoftHsmProvider` (`SoftHsmOptions.FilePath`, optional) — in-process RSA/ECDSA, persisted to a JSON file when `FilePath` is set, otherwise memory only (dev/MVP). Generates a self-signed `X509Certificate2` per signing key.
- `Pkcs11HsmProvider` (`Pkcs11Options`: `LibraryPath`, `TokenLabel`, `UserPin`, `RsaKeySizeBits`) — real HSM via Pkcs11Interop, RSA-PSS signing. On Linux, `libdl` is redirected to `libdl.so.2` in `Program.cs`.
- Each `KeyVersion` stores a `ProviderRef` that carries the provider instance Id; use cases resolve the provider through `IHsmProviderRegistry`, never directly.

## Domain Model

`Key` is the central aggregate: name, purpose, allowed-mechanisms whitelist, collection of `KeyVersion`. The first version added becomes Primary.

- Key lifecycle (`KeyState`): Active → Disabled → Deleted.
- KeyVersion statuses (`KeyVersionStatus`): Primary → Active → Retired → Disabled → Destroyed.
- `Mechanism` is a value object with two predefined instances: `RsaPssSha256` (RSA_PSS_SHA256) and `EcdsaP256Sha256Der` (ECDSA P-256). Document signing supports RSA-PSS only; ECDSA is rejected with a `DomainException`.
- `Certificate` is a separate aggregate linked to a `KeyVersion`; enrollment goes CSR → submit-to-CA → complete (or import). Revocation is supported.

Domain invariants are enforced via `Guard` and throw `DomainException`.

## File Signing

Routed by `ISignedArtifactBuilder` (→ `SignedArtifactBuilder`):

- PDF (magic bytes `%PDF-`) → **PAdES** via `PadesSigner` + PDFsharp `DigitalSignatureHandler` and an HSM-backed `IDigitalSigner` (`HsmBackedPdfSigner`).
- Anything else → **PKCS#7 attached** (`.p7m`) via `BouncyCastlePkcs7AttachedSigner`.
- Verification: `SignedFileVerifier`.
- `POST /api/crypto/sign-file` responses set `X-Audit-Event-Id`, `X-Signed-Format` and `X-File-Name`. Program.cs CORS exposes `X-Audit-Event-Id`, `X-Key-Id`, `X-Key-Version`, `X-Mechanism`, `X-Signed-Format`, `X-File-Name`.

## API Endpoints (high level)

All routes are under `/api`. Controllers: `AuthController` (`api/auth`), `KeysController`, `CryptoController`, `CertificatesController`, `AuditController`, `SystemController`, `WebAuthnController`.

- `POST /api/auth/login`, `POST /api/auth/register` — password login and JWT issuance (roles `Admin`, `Operator`)
- `GET /api/keys`, `POST /api/keys`, `POST /api/keys/{keyId}/rotate`, `GET /api/keys/{keyId}/public`, `DELETE /api/keys/{keyId}` (`AdminOnly`)
- `POST /api/crypto/sign` (digest), `POST /api/crypto/sign-file` (multipart), `POST /api/crypto/verify-file`
- `/api/keys/{keyId}/certificates/...` — `POST enroll` (CSR), `POST enrollments/{enrollmentId}/complete`, `POST import`, `POST {certId}/revoke`, `GET active`
- `GET /api/audit/logs` (`CanOperate`)
- `GET /api/system/ca-certificate`
- `/api/auth/webauthn/...` — see Passkeys below

Authorization: a global fallback policy requires authenticated users. Policies `AdminOnly` and `CanOperate` (Admin or Operator) gate sensitive actions. Identity cookie redirects are suppressed — the API returns 401/403.

## Passkeys (WebAuthn / FIDO2)

- `WebAuthnController` (`api/auth/webauthn`): `POST register/begin` and `register/complete` (authenticated); `POST login/begin` and `login/complete` (anonymous, return a JWT like password login); `GET credentials`, `PATCH credentials/{id}` (rename) and `DELETE credentials/{id}` (authenticated, scoped to the current user).
- `WebAuthnService` (Fido2.AspNet 4.0.1) uses `WebAuthnOptions` from the `WebAuthn` config section.
- Challenges are held by `IWebAuthnChallengeCache` → `InMemoryWebAuthnChallengeCache` (singleton, `ConcurrentDictionary`; consume is a `TryRemove`, so a challenge is one-time-use). This is single-instance only; Program.cs notes that multi-instance deployments need a Redis-backed `GETDEL` implementation.
- Credentials are stored per user through `IUserCredentialRepository` (EF; migration `AddWebAuthnCredentials`).
- Error mapping: `WebAuthnException` → 400 (register, login/complete) or 401 (login/begin, which uses one message for unknown users and users without passkeys); `Fido2VerificationException` → 401 on login/complete.
- Frontend: `src/app/auth/webauthn/` and `src/app/auth/passkeys/`.

## DI Lifetimes

- **Singletons:** `IHsmProviderRegistry` (instance built in Program.cs), `IClock` (`SystemClock`), `HsmBackedCertificateFactory`, `ISoftCaBootstrapper`, `ICertificateAuthority` (soft CA), `IWebAuthnChallengeCache` (in-memory). `IHttpContextAccessor` is registered as a framework singleton.
- **Scoped:** EF repositories (`IKeyRepository`, `ICertificateRepository`, `ISystemCertificateAuthorityRepository`, `IAuditRepository`, `IUserCredentialRepository`), `IUnitOfWork`, `IAuditSink`, `IUserLookup`, all use cases, signing services (`IPadesSigner`, `IPkcs7AttachedSigner`, `ISignedArtifactBuilder`, `ISignedFileVerifier`, `ICsrBuilder`, `ICertificateValidator`), `ICurrentUser`, `TokenService`, `IWebAuthnService`.
- `WebAuthnOptions` is bound with `Configure<>` (options pattern), not a DI lifetime.

## Startup Side-Effects

`Program.cs` runs EF migrations, bootstraps the soft CA via `ISoftCaBootstrapper.EnsureInitializedAsync`, ensures roles `Admin`/`Operator` exist, and seeds the default admin user (user name from `DefaultAdmin:UserName`, default `admin`; email from `DefaultAdmin:Email`) if no user with that name exists. PDFsharp's `GlobalFontSettings.FontResolver` is wired to `InterFontResolver` against the `Fonts/` directory next to the binaries (Inter TTFs + `OFL.txt` in `CryptoManager.API/Fonts`, copied by the csproj). Don't bundle fonts whose license forbids redistribution — the repo is public.

## Library Quirks (BouncyCastle 2.6 / PDFsharp 6.2)

- BouncyCastle 2.x has no `IContentSigner`, no `X509StoreFactory`, no `CollectionStore<T>`. Use `ISignatureFactory` + `IStreamCalculator<IBlockResult>`; implement `IStore<X509Certificate>` inline. `IBlockResult` requires the `Collect(Span<byte>)` and `GetMaxResultLength()` overloads. `SignerInfoGeneratorBuilder.Build(ISignatureFactory, X509Certificate)` is the correct call.
- PDFsharp 6.2.4 targets `net10.0`. There is no `PdfDocumentSigner`/`PdfSignatureOptions` — use `DigitalSignatureHandler.ForDocument` with an `IDigitalSigner`. `PdfSharpDefaultSigner` lives in `PdfSharp.Cryptography.dll`. Don't call `AddSignatureComponentsAsync` (XML-doc only); just `document.Save` after `ForDocument`.

## Conventions

- .NET 9.0, nullable reference types on, file-scoped namespaces (some older files, e.g. `Pkcs11Options.cs` and `ErrorHandlingMiddleware.cs`, still use block namespaces; use file-scoped for new code).
- Rich domain model — business logic in entities, not anemic DTOs.
- Value objects as `record` / `record struct`.
- Actor identity flows via `ICurrentUser` (resolved from `HttpContext`); audit events record it.

## Rules
- When creating git commits, use short git commit messages and never add "Co-authored by Claude" and never mention "Claude" at all.