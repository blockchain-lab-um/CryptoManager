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

**Auth feature (completed as of 2026-03-23):**
- Login `/login`, register `/register` — both bypass the main nav layout via AuthLayoutComponent
- `AuthService` — in-memory signals for token + user, `isAuthenticated` computed, `login()`, `register()`, `logout()` (full page reload)
- `authInterceptor` — functional HTTP interceptor, attaches Bearer token
- `authGuard` — `CanActivateFn`, redirects to `/login` with returnUrl
- `AuthLayoutComponent` — wrapper layout for auth pages only
- Auth models in `src/app/auth/models/auth.models.ts`

**Current folder structure (as of 2026-03-23):**
- `src/app/auth/` — auth feature: service, guard, interceptor, models/, layout/, login/, register/
- `src/app/core/api/` — `CryptoManagerApi` service + `models.ts` DTOs (API layer)
- `src/app/core/services/` — `CryptoService` (crypto utils), `AppMessenger` (PrimeNG toast wrapper)
- `src/app/core/components/card/` — shared `Card` UI component
- `src/app/layout/` — root shell: `App` component (app.ts, app.html, app.css)
- `src/app/features/dashboard/dashboard.component/` — dashboard page (extra .component folder nesting)
- `src/app/features/keys/keys.component/` — keys list page (extra .component folder nesting)
- `src/app/features/keys/dialogs/` — key-create, key-delete, key-public, key-rotate dialogs
- `src/app/features/keys/keys.service.ts` — keys feature service (providedIn root — misplaced scope)
- `src/app/features/sign/sign.component/` — sign page (extra .component folder nesting)
- `src/app/features/verify/verify.component/` — verify page (extra .component folder nesting)
- `src/app/app.routes.ts`, `src/app/app.config.ts`

**Key structural issues identified (2026-03-23):**
- `pages/` wrapper is a redundant layer; features should be at `features/` (keys, sign, verify, dashboard)
- Extra `.component` subfolder inside each page feature folder (e.g. `keys/keys.component/keys.component.ts`) is a double-nesting anti-pattern
- `auth/` is a feature, but it sits as a sibling to `core/` and `pages/` — inconsistent placement relative to other features
- `KeysService` is `providedIn: 'root'` but is only used within the keys/dashboard/sign/verify scope — scoping concern
- `CryptoService` mixes download utilities with cryptographic operations — two separate responsibilities
- `core/components/card/` — card component belongs in `shared/` not `core/`
- `AppMessenger` wraps PrimeNG `MessageService` — thin wrapper that adds a layer of indirection without much value
- `layout/app.ts` — root shell component, correct placement but class is named `App` (should follow Angular naming: `AppComponent` or `AppShellComponent`)
- `SignComponent` and `VerifyComponent` inject `CryptoManagerApi` directly, bypassing any service layer — direct API calls from page components
- `new Date()` used in `app.ts` `currentYear` getter — violates the CLAUDE.md rule about not assuming globals
