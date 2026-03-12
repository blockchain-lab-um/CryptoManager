---
name: project_frontend
description: Frontend project structure, conventions, and key architectural facts for CryptoManager Angular app
type: project
---

Angular 20 frontend at `C:\PROJEKTI\CryptoManager\src\frontend\crypto-manager`.

**Why:** Internal HSM/crypto management tool. Needs auth layered on top of existing feature pages.

**How to apply:** All architectural suggestions must match these constraints exactly:
- Standalone components, no NgModules, `standalone: true` must NOT be written (it is the default)
- Signals for state: `signal()`, `computed()`
- `ChangeDetectionStrategy.OnPush` on all components
- `input()` / `output()` functions, never decorators
- `inject()` instead of constructor injection
- Reactive Forms only (FormBuilder)
- Native control flow only (`@if`, `@for`, `@switch`)
- `class` bindings, never `ngClass` or `ngStyle`
- PrimeNG component library (Aura/Noir preset)
- WCAG AA accessibility, must pass AXE checks
- Lazy-loaded feature routes via `loadComponent`

**App bootstrap:**
- `bootstrapApplication(App, appConfig)` in `main.ts`
- `appConfig` in `app.config.ts` — providers: `provideRouter`, `provideAnimationsAsync`, `providePrimeNG`, `MessageService`
- Root layout component is `App` in `src/app/layout/app.ts` — renders a full-page shell with sticky header nav, `<router-outlet>`, and footer
- API base URL from `environment.apiBaseUrl` (empty string in prod — same origin)

**Feature structure:**
- `src/app/core/api/` — `CryptoManagerApi` service + `models.ts` DTOs
- `src/app/core/services/` — shared services (e.g., `CryptoService`)
- `src/app/core/components/` — shared UI components (e.g., `Card`)
- `src/app/pages/` — feature pages: `dashboard`, `keys`, `sign`, `verify`
- Routes in `src/app/app.routes.ts`

**Auth feature planned (2026-03-12):**
- Login page `/login`, register page `/register`
- Auth service with in-memory token storage, `isAuthenticated` signal
- Functional HTTP interceptor attaches Bearer token
- `CanActivateFn` guard protecting all existing routes
- Login/register pages must bypass the main nav layout shell
