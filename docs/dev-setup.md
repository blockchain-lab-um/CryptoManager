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

  1. Configure database connection

  appsettings.json already contains a working local default:

  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=cryptomanager;Username=postgres;Password=T3st1ranj3123."
  }

  Edit this to match your local PostgreSQL credentials, or override via environment variable:

  export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=cryptomanager;Username=postgres;Password=yourpassword"

  2. Start PostgreSQL (easiest with Docker, database only):

  docker run -d \
    --name cryptomanager-db \
    -e POSTGRES_DB=cryptomanager \
    -e POSTGRES_USER=postgres \
    -e POSTGRES_PASSWORD=T3st1ranj3123. \
    -p 5432:5432 \
    postgres:17

  3. Run the API

  cd C:\PROJEKTI\CryptoManager\src\backend
  dotnet run --project CryptoManager.API

  - EF migrations run automatically on startup (db.Database.Migrate() in Program.cs)
  - Swagger UI available at: https://localhost:44351/swagger (Development) or /swagger (always enabled — see note below)
  - Default HTTPS port for dev: 44351 (referenced in environment.development.ts)

  Note: app.UseSwagger() and app.UseSwaggerUI() are called twice in Program.cs — once inside the IsDevelopment() block and once unconditionally after it. Swagger is always active regardless of environment.

  Option B — Full stack via Docker Compose

  cd C:\PROJEKTI\CryptoManager
  docker compose up

  Services started:
  - PostgreSQL 17 → internal only
  - API → http://localhost:8080
  - Frontend → http://localhost:99

  ---
  3. Frontend Setup

  cd C:\PROJEKTI\CryptoManager\src\frontend\crypto-manager

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

  If your API runs on a different port, update environment.development.ts before starting.

  ---
  4. Crypto / HSM Configuration

  SoftHsmProvider (default — no config needed)

  SoftHsmProvider is active by default (registered in Program.cs). It is fully in-memory, zero configuration. Key material is lost when the API process restarts.

  PKCS#11 Hardware HSM (stub — not yet active)

  Configured via appsettings.json Pkcs11 section:

  "Pkcs11": {
    "LibraryPath": "C:\\Program Files\\Yubico\\YubiHSM Shell\\bin\\pkcs11\\yubihsm_pkcs11.dll",
    "TokenLabel": "YubiHSM",
    "UserPin": "0001password",
    "RsaKeySizeBits": 2048
  }

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

  To activate Pkcs11HsmProvider, change the DI registration in Program.cs:
  // Current (SoftHSM):
  builder.Services.AddSingleton<IHsmProvider, SoftHsmProvider>();

  // Switch to PKCS11 (when implemented):
  builder.Services.AddSingleton<IHsmProvider, Pkcs11HsmProvider>();

  Via environment variable override (e.g. in Docker or CI):

  Pkcs11__LibraryPath=/usr/lib/softhsm/libsofthsm2.so
  Pkcs11__TokenLabel=MyToken
  Pkcs11__UserPin=1234

  ---
  5. Development Commands

  Backend

  # Build entire solution
  dotnet build CryptoManager.sln

  # Run API (from backend root)
  dotnet run --project CryptoManager.API

  # Run all tests
  dotnet test

  # Build Release
  dotnet publish CryptoManager.API -c Release -o ./publish

  Frontend

  # Install dependencies
  npm install

  # Dev server (uses environment.development.ts → https://localhost:44351)
  npm start

  # Production build
  npm run build

  # Build with watch mode
  npm run watch

  # Run tests
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
  2. Open Swagger at /swagger
  3. POST /api/Keys — create a key with name, purpose Sign, mechanism RSA_PSS_SHA256
  4. Copy the returned keyId
  5. POST /api/Crypto/sign — send keyId, mechanism, and a base64-encoded 32-byte SHA-256 digest

  Frontend development against local API

  1. Ensure API is running on https://localhost:44351 (default dotnet run HTTPS port)
  2. cd src/frontend/crypto-manager && npm start
  3. Angular dev server proxies API calls to https://localhost:44351 via environment.development.ts
  4. Browser app available at http://localhost:4200 (Angular CLI default)

  Running only the database via Docker + API natively

  # Terminal 1 — start only DB
  docker compose up db

  # Terminal 2 — run API natively (faster, hot reload possible)
  cd src/backend
  dotnet run --project CryptoManager.API

  Checking EF migrations

  cd src/backend
  dotnet ef migrations list --project CryptoManager.Infrastructure --startup-project CryptoManager.API

  # Add a new migration
  dotnet ef migrations add <MigrationName> --project CryptoManager.Infrastructure --startup-project CryptoManager.API

  Migrations run automatically on every API startup — no manual dotnet ef database update needed in development.

  Switching from appsettings.json credentials to User Secrets (recommended for local dev)

  The API project has a User Secrets ID configured (UserSecretsId in .csproj):

  cd src/backend/CryptoManager.API
  dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=cryptomanager;Username=postgres;Password=yourpassword"

  User secrets override appsettings.json in the Development environment without modifying tracked files.