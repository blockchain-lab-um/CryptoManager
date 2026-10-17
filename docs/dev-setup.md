  ---
  1. Required Software

  ┌─────────────────────────┬──────────────────────────┬──────────────────────────────────────────────────────────┐                                                                                                                 
  │          Tool           │         Version          │                          Source                          │                                                                                                                 
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ .NET SDK                │ 9.0                      │ <TargetFramework>net9.0</TargetFramework> in all .csproj │
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ Node.js                 │ Compatible with npm 11.x │ "packageManager": "npm@11.6.2" in package.json           │
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ npm                     │ 11.6.2                   │ pinned in package.json                                   │
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ Angular CLI             │ 21.1.2                   │ "@angular/cli": "21.1.2" in devDependencies              │
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ PostgreSQL              │ 17                       │ postgres:17 in docker-compose.yml                        │
  ├─────────────────────────┼──────────────────────────┼──────────────────────────────────────────────────────────┤
  │ Docker + Docker Compose │ any current              │ required for database or full-stack                      │
  └─────────────────────────┴──────────────────────────┴──────────────────────────────────────────────────────────┘

  ---
  2. Backend Setup

  Option A — Run backend directly (requires a local PostgreSQL)

  1. Create appsettings.json

  appsettings.json is git-ignored (src/backend/.gitignore), so create it from the tracked template:

  cd src/backend/CryptoManager.API
  cp appsettings.Template.json appsettings.json

  The template is not complete. Add the sections it is missing (HsmProviders, Jwt, DefaultAdmin, WebAuthn; see "Required configuration" below) and remove the top-level Pkcs11 section, which is no longer read. Secrets belong in user-secrets (step 4), not in appsettings.json.

  Required configuration (startup fails without the first four):

  - ConnectionStrings:DefaultConnection — PostgreSQL connection string
  - Jwt:Secret — signing key for JWT tokens (use a long random value, at least 32 characters)
  - DefaultAdmin:Password — password for the bootstrap admin user, created on first run. The user name defaults to admin (DefaultAdmin:UserName) and the email to admin@cryptomanager.local (DefaultAdmin:Email)
  - HsmProviders — array with exactly one entry where IsDefault is true (see section 4)
  - WebAuthn:Origins — non-empty list of the origins the browser uses, e.g. http://localhost:4200. Passkey requests fail while it is empty. Optional: ServerDomain (default localhost), ServerName (default CryptoManager), TimeoutMs (60000), ChallengeTtlSeconds (300)

  Connection string example (or set it with user-secrets in step 4, or override via environment variable):

  export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=cryptomanager;Username=cryptouser;Password=<password>"

  2. Start PostgreSQL (easiest with Docker, database only):

  docker run -d \
    --name cryptomanager-db \
    -e POSTGRES_DB=cryptomanager \
    -e POSTGRES_USER=cryptouser \
    -e POSTGRES_PASSWORD=<password> \
    -p 5432:5432 \
    postgres:17

  Use the same user and password as in the connection string. The db service in docker-compose.yml does not publish port 5432 to the host, so a natively running API cannot reach it on localhost:5432 through `docker compose up db`. Use this command, or add a `ports` mapping to the db service.

  3. Run the API

  cd src/backend
  dotnet run --project CryptoManager.API

  - `dotnet run` uses the first launch profile in Properties/launchSettings.json, "http", which listens on http://localhost:5078. Add `--launch-profile https` to use the https profile (https://localhost:7225 and http://localhost:5078).
  - Port 44351 is the IIS Express SSL port (Visual Studio's "IIS Express" profile). It is the address environment.development.ts points the frontend at, so see Frontend Setup if you run the API with `dotnet run`.
  - EF migrations run automatically on startup (db.Database.Migrate() in Program.cs)
  - Swagger UI is enabled only in the Development environment (the launch profiles set this), at `/swagger` on the API address, e.g. http://localhost:5078/swagger

  Option B — Full stack via Docker Compose

  # from the repository root
  docker compose up

  Services started:
  - PostgreSQL 17 → internal only
  - API → http://localhost:8080 (Swagger at http://localhost:8080/swagger; compose sets ASPNETCORE_ENVIRONMENT=Development)
  - Frontend → http://localhost:99

  The development compose file provides container-only defaults for required startup configuration:

  - `Jwt__Secret`
  - `DefaultAdmin__Password`
  - `ConnectionStrings__DefaultConnection`
  - `HsmProviders__0__IsDefault=true`, `HsmProviders__1__Type=SoftHsm`, and `HsmProviders__1__IsDefault=false` so Docker constructs only soft HSM providers instead of trying to load a local Windows PKCS#11 DLL path

  These values are only for local development. Use user-secrets when running the API directly, and use real environment variables or `.env` secrets for production compose.

  The API image is built from the repository root, and .dockerignore does not exclude appsettings.json, so the copy in your working tree is included. Create it from the template first (see Option A, step 1) and add the HsmProviders entries (each one needs an `Id`, which compose does not set), WebAuthn and other sections, or the API will not start. Passkeys from the Docker frontend (http://localhost:99) also need that origin in `WebAuthn:Origins`.

  ---
  3. Frontend Setup

  cd src/frontend/crypto-manager

  # Install dependencies
  npm install

  # Run dev server
  npm start
  # equivalent to: ng serve

  The dev server points to the backend at https://localhost:44351 — set in environment.development.ts:

  export const environment = {
    production: false,
    apiBaseUrl: 'https://localhost:44351'
  };

  The app calls apiBaseUrl directly (there is no dev-server proxy; CORS allows any origin). If your API runs on a different address, update environment.development.ts before starting. For example, `dotnet run` with the default http profile listens on http://localhost:5078, so set apiBaseUrl to that.

  ---
  4. Crypto / HSM Configuration

  Signing keys live in one or more HSM providers, configured as the `HsmProviders` array. Each entry has:

  - `Id` — unique, non-empty name. Each key version stores the Id of the provider that holds it
  - `Type` — `SoftHsm` or `Pkcs11`
  - `IsDefault` — exactly one entry must be `true`, or startup fails
  - a matching options block: `SoftHsm` (optional) or `Pkcs11` (required when Type is Pkcs11)

  "HsmProviders": [
    {
      "Id": "softhsm-dev",
      "Type": "SoftHsm",
      "IsDefault": true,
      "SoftHsm": {
        "FilePath": "../../../storage/softhsm-keys.json"
      }
    },
    {
      "Id": "yubihsm-dev",
      "Type": "Pkcs11",
      "IsDefault": false,
      "Pkcs11": {
        "LibraryPath": "C:\\Program Files\\Yubico\\YubiHSM Shell\\bin\\pkcs11\\yubihsm_pkcs11.dll",
        "TokenLabel": "YubiHSM",
        "UserPin": "<token-pin>",
        "RsaKeySizeBits": 2048
      }
    }
  ]

  Program.cs builds an IHsmProviderRegistry from this array. Use cases resolve providers through the registry, and each key version's provider is the one whose Id is stored with it.

  SoftHsm (software provider, no hardware)

  `SoftHsm:FilePath` is optional. If it is set, key material is persisted to that JSON file and survives restarts. If it is omitted or empty, keys are kept in memory only and are lost when the API process restarts.

  PKCS#11 Hardware HSM (YubiHSM 2 and other PKCS#11 modules)

  Pkcs11HsmProvider is implemented. Select it by setting `Type` to `Pkcs11` and providing a `Pkcs11` block:

  ┌────────────────┬────────────────────────────────────────────────────────┐
  │     Field      │                      Description                       │
  ├────────────────┼────────────────────────────────────────────────────────┤
  │ LibraryPath    │ Absolute path to the PKCS#11 .dll / .so shared library │
  ├────────────────┼────────────────────────────────────────────────────────┤
  │ TokenLabel     │ Label of the HSM token as seen by PKCS#11              │
  ├────────────────┼────────────────────────────────────────────────────────┤
  │ UserPin        │ Token PIN or YubiHSM auth credential                   │
  ├────────────────┼────────────────────────────────────────────────────────┤
  │ RsaKeySizeBits │ RSA key size (default 2048)                            │
  └────────────────┴────────────────────────────────────────────────────────┘

  Keys are generated on the device as sensitive and non-extractable, and signatures use RSA-PSS (CKM_RSA_PKCS_PSS).

  Via environment variable override (e.g. in Docker or CI), array entries are addressed by index:

  HsmProviders__0__Id=softhsm-dev
  HsmProviders__0__Type=SoftHsm
  HsmProviders__0__IsDefault=true
  HsmProviders__1__Id=pkcs11-dev
  HsmProviders__1__Type=Pkcs11
  HsmProviders__1__IsDefault=false
  HsmProviders__1__Pkcs11__LibraryPath=/usr/lib/softhsm/libsofthsm2.so
  HsmProviders__1__Pkcs11__TokenLabel=MyToken
  HsmProviders__1__Pkcs11__UserPin=<token-pin>

  ---
  5. Development Commands

  Backend

  # Build entire solution
  dotnet build CryptoManager.sln

  # Run API (from backend root)
  dotnet run --project CryptoManager.API

  # Run all tests (CryptoManager.Tests, xUnit)
  dotnet test

  # Build Release
  dotnet publish CryptoManager.API -c Release -o ./publish

  Frontend

  # Install dependencies
  npm install

  # Dev server (uses environment.development.ts → https://localhost:44351; see Frontend Setup)
  npm start

  # Production build
  npm run build

  # Build with watch mode
  npm run watch

  # Run tests (Vitest)
  npm test

  Docker

  # Start full stack (DB + API + frontend)
  docker compose up

  # Build and start (after code changes)
  docker compose up --build

  # Database only
  docker compose up db

  # Stop all
  docker compose down

  ---
  6. Common Development Workflows

  Adding a new key and testing signing

  1. Start the API (dotnet run --project CryptoManager.API)
  2. Open Swagger at /swagger (Development only). Swagger has no Authorize button configured, and the key and crypto endpoints require a JWT. Get a token with POST /api/auth/login (body: { "userName": "admin", "password": "<admin password>" }) and send it as an `Authorization: Bearer <token>` header with each request, or use curl.
  3. POST /api/Keys — body: { "name": "<key name>", "purpose": "Sign", "allowedMechanisms": ["RSA_PSS_SHA256"] }
  4. Copy the returned keyId
  5. POST /api/Crypto/sign — body: { "keyId": "<keyId>", "mechanism": "RSA_PSS_SHA256", "digestBase64": "<base64 of a 32-byte SHA-256 digest>" }

  Frontend development against local API

  1. Start the API so that it matches environment.development.ts. https://localhost:44351 is the IIS Express profile. With `dotnet run` (http profile) the API listens on http://localhost:5078, so change apiBaseUrl to match (see Frontend Setup).
  2. cd src/frontend/crypto-manager && npm start
  3. The browser calls the API at apiBaseUrl directly (no dev-server proxy)
  4. Browser app available at http://localhost:4200 (Angular CLI default)

  Running only the database via Docker + API natively

  # Terminal 1 — start only DB
  docker compose up db

  # Terminal 2 — run API natively (faster, hot reload possible)
  cd src/backend
  dotnet run --project CryptoManager.API

  The db service does not publish port 5432 to the host, so the native API cannot reach it on localhost:5432 this way. Either add `ports: ["5432:5432"]` to the db service in docker-compose.yml, or start the database with the `docker run` command from Option A.

  Checking EF migrations

  cd src/backend
  dotnet ef migrations list --project CryptoManager.Infrastructure --startup-project CryptoManager.API

  # Add a new migration
  dotnet ef migrations add <MigrationName> --project CryptoManager.Infrastructure --startup-project CryptoManager.API

  Migrations run automatically on every API startup — no manual dotnet ef database update needed in development.

  Switching from appsettings.json credentials to User Secrets (recommended for local dev)

  The API project has a User Secrets ID configured (UserSecretsId in .csproj):

  cd src/backend/CryptoManager.API
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cryptomanager;Username=cryptouser;Password=<password>"
  dotnet user-secrets set "Jwt:Secret" "<long-random-development-secret>"
  dotnet user-secrets set "DefaultAdmin:Password" "<admin-password>"

  User secrets override appsettings.json in the Development environment without modifying tracked files.
