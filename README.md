# CryptoManager

**A companion project for our article on Hardware Security Modules (HSMs).**

CryptoManager is a small but complete web app. It shows how a real application uses an HSM to create and manage signing keys and to sign digests, PDFs and other files without the private key ever leaving the device. It runs against a software HSM out of the box, and with a configuration change it can use a real YubiHSM 2 over PKCS#11.

📖 **Read the article:** [Where Digital Trust Lives: An Introduction to Hardware Security Modules](https://medium.com/@blockchainlabum/where-digital-trust-lives-an-introduction-to-hardware-security-modules-520848c82395)

> [!IMPORTANT]
> This is a **demonstration project**, not a product. It isn't actively developed, has had no security audit, and makes trade-offs that are fine for a demo but **not for production** (see [Limitations](#limitations)). Use it to learn and experiment, and don't use it to protect real keys.

## What it demonstrates

- **Keys that never leave the HSM.** On the PKCS#11 path, private keys are generated on the device as `CKA_SENSITIVE` and non-`CKA_EXTRACTABLE`. The app only ever holds a handle to the key and the public key.
- **Signing through the HSM.** The app hashes the data itself and sends only the digest to the HSM (`C_Sign` with `CKM_RSA_PKCS_PSS`).
- **One abstraction, several backends.** Everything talks to a four-method `IHsmProvider` interface, so a software HSM and a hardware HSM can run side by side, and each key records which provider holds it.
- **Key lifecycle.** Keys can be created, rotated (with version history), disabled and destroyed.
- **Real-world signature formats.** PDFs get **PAdES** signatures with a visible stamp, and other files get attached **PKCS#7 / CMS** (`.p7m`) signatures. Signed files can be verified.
- **Certificates for HSM keys.** The app builds a CSR whose signature is made by the HSM, enrolls it with a built-in demo CA, and supports certificate import and revocation.
- **An audit trail.** Every sensitive operation is logged and shown in the UI.
- **Passkey login (WebAuthn / FIDO2)** alongside username and password.

## Quick start

You need Docker with Compose v2.

```bash
git clone https://github.com/abratanovic/CryptoManager.git
cd CryptoManager
```

`appsettings.json` is git-ignored, but the API image needs one for its HSM provider and passkey settings. Create `src/backend/CryptoManager.API/appsettings.json` with the following content. The secrets come from `docker-compose.yml`.

```json
{
  "HsmProviders": [
    { "Id": "softhsm", "Type": "SoftHsm", "IsDefault": true },
    { "Id": "softhsm-2", "Type": "SoftHsm", "IsDefault": false }
  ],
  "WebAuthn": {
    "ServerDomain": "localhost",
    "ServerName": "CryptoManager",
    "Origins": [ "http://localhost:99", "http://localhost:4200" ]
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

Then build and start the stack:

```bash
docker compose up --build
```

Open **http://localhost:99** and sign in as `admin` using the `DefaultAdmin__Password` value from [`docker-compose.yml`](docker-compose.yml). The API runs on http://localhost:8080.

The Docker setup uses the **software HSM**. Its keys are stored in a JSON file on a Docker volume, so you don't need any hardware.

### Things to try

1. **Keys** → create an RSA or ECDSA signing key.
2. **Sign** → upload a PDF to get a PAdES-signed copy, or any other file to get a `.p7m`.
3. **Verify** → upload the signed file and check its signature.
4. **Certificates** → enroll a certificate for your key with the demo CA.
5. **Keys** → rotate the key, then check that older signatures still verify.
6. **Audit log** → see every step you just took.

## Using a real YubiHSM 2

The API Docker image already contains the YubiHSM PKCS#11 module (from [`packages/`](packages/)). To use it:

1. Run `yubihsm-connector` on the host (default port `12345`). The container reaches it at `host.docker.internal:12345`, as configured in [`yubihsm_pkcs11.conf`](packages/yubihsm-runtime/yubihsm_pkcs11.conf).
2. Add a `Pkcs11` entry to the `HsmProviders` configuration with the module path, token label and PIN, and make it the default provider (`IsDefault: true`).
3. Create a new key. It is generated on the YubiHSM.

See [docs/dev-setup.md](docs/dev-setup.md) for the configuration details.

## Where to look in the code

| Concept                         | File                                                                                   |
| ------------------------------- | -------------------------------------------------------------------------------------- |
| HSM abstraction                 | [`IHsmProvider.cs`](src/backend/CryptoManager.Application/Abstractions/IHsmProvider.cs) |
| PKCS#11 integration (YubiHSM)   | [`Pkcs11HsmProvider.cs`](src/backend/CryptoManager.Infrastructure/HSM/PKCS11/Pkcs11HsmProvider.cs) |
| Software HSM                    | [`SoftHsmProvider.cs`](src/backend/CryptoManager.Infrastructure/HSM/SoftHSM/SoftHsmProvider.cs) |
| Choosing a provider per key     | [`HsmProviderRegistry.cs`](src/backend/CryptoManager.Infrastructure/HSM/HsmProviderRegistry.cs) |
| Key and version lifecycle       | [`Key.cs`](src/backend/CryptoManager.Domain/Entities/Key.cs), [`KeyVersion.cs`](src/backend/CryptoManager.Domain/Entities/KeyVersion.cs) |
| PAdES signing with an HSM key   | [`HsmBackedPdfSigner.cs`](src/backend/CryptoManager.Infrastructure/Crypto/HsmBackedPdfSigner.cs), [`PadesSigner.cs`](src/backend/CryptoManager.Infrastructure/Crypto/PadesSigner.cs) |
| PKCS#7 / CMS signing            | [`BouncyCastlePkcs7AttachedSigner.cs`](src/backend/CryptoManager.Infrastructure/Crypto/BouncyCastlePkcs7AttachedSigner.cs) |
| CSR signed by the HSM           | [`BouncyCastleCsrBuilder.cs`](src/backend/CryptoManager.Infrastructure/Crypto/BouncyCastleCsrBuilder.cs), [`HsmRsaPssSignatureFactory.cs`](src/backend/CryptoManager.Infrastructure/Crypto/HsmRsaPssSignatureFactory.cs) |
| Signing use case end to end     | [`SignDocumentUseCase.cs`](src/backend/CryptoManager.Application/UseCases/SignDocumentUseCase.cs) |

The backend follows Clean Architecture. **Domain** holds the rules, **Application** holds the use cases and interfaces, **Infrastructure** holds the HSM, crypto and database code, and **API** is the HTTP layer. Because of that split, the HSM code is isolated in one place. [docs/onboarding.md](docs/onboarding.md) walks through the whole design.

## Tech stack

.NET 9 · ASP.NET Core · EF Core + PostgreSQL 17 · Pkcs11Interop · BouncyCastle · PDFsharp · Fido2.AspNet · Angular 21 · PrimeNG · Docker Compose

## Running without Docker

You'll need the .NET 9 SDK, Node.js with npm 11, and PostgreSQL (you can start just the database with `docker compose up db`).

```bash
# Backend: create appsettings.json from the template, then set the secrets
cd src/backend/CryptoManager.API
cp appsettings.Template.json appsettings.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<postgres-connection-string>"
dotnet user-secrets set "Jwt:Secret" "<long-random-string>"
dotnet user-secrets set "DefaultAdmin:Password" "<admin-password>"
cd .. && dotnet run --project CryptoManager.API

# Frontend
cd src/frontend/crypto-manager
npm install && npm start          # http://localhost:4200
```

The template doesn't include everything the API needs. Add the `HsmProviders` and `WebAuthn` sections from the [Quick start](#quick-start) example to your `appsettings.json` (a single `SoftHsm` provider is enough when you run without Docker).

`dotnet run` listens on http://localhost:5078 by default, but the frontend's `src/environments/environment.development.ts` points to `https://localhost:44351`. Change `apiBaseUrl` to match the port your API is on. [docs/dev-setup.md](docs/dev-setup.md) covers the full setup.

## Limitations

These were deliberate simplifications for the demo, and each would need fixing before anyone could trust this with real keys:

- **The software HSM isn't an HSM.** Its keys live in process memory and a plain JSON file. It only exists so the demo runs without hardware.
- **The built-in CA is for demos only.** It is self-signed, and its key is managed by the app itself.
- **CORS allows any origin**, and the dev compose file contains hard-coded secrets.
- **WebAuthn challenges are cached in memory**, so the app can only run as a single instance.
- **Hardware support is limited.** The PKCS#11 provider has only been tested with a YubiHSM 2 and only uses RSA-PSS.
- **Test coverage is minimal.**

## License

Released under the [MIT License](LICENSE).
