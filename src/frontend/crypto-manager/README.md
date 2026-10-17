# CryptoManager frontend

Angular 21 single-page app for CryptoManager. It manages HSM-backed signing keys, signs and verifies files (PAdES for PDFs, PKCS#7 `.p7m` for other files), enrolls certificates through the demo CA, shows the audit log, and handles passkey (WebAuthn) login. It talks to the ASP.NET Core API in `src/backend`.

For the whole project, see [../../../README.md](../../../README.md). For full setup (backend, database, Docker), see [docs/dev-setup.md](../../../docs/dev-setup.md). For architecture, see [docs/onboarding.md](../../../docs/onboarding.md).

## Commands

Run these from this folder (`src/frontend/crypto-manager`):

```bash
npm install     # install dependencies (npm 11.6.2, pinned in package.json)
npm start       # dev server (ng serve) at http://localhost:4200
npm run build   # production build to dist/crypto-manager/browser
npm run watch   # development build in watch mode
npm test        # unit tests (Vitest, via ng test)
```

## API address

The backend URL is set by `apiBaseUrl` in `src/environments/`:

- `environment.development.ts` (used by `npm start`): `https://localhost:44351`, the IIS Express SSL port. With `dotnet run` the API listens on `http://localhost:5078` by default, so change `apiBaseUrl` to match the address the API prints.
- `environment.ts` (production build): `''`, i.e. same origin. In Docker, nginx (`nginx.conf`) proxies `/api/` to the API container.

The dev server does not proxy API calls; the browser calls `apiBaseUrl` directly, and the API's CORS policy allows any origin.

## Docker

The `Dockerfile` builds the app and serves it with nginx on port 80. `docker compose up` in the repository root publishes it at http://localhost:99.
