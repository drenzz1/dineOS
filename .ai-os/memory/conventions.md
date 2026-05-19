# conventions.md

## Project Conventions

### Backend (.NET)

- **Solution layout:** `src/DineOS.{Api,Application,Domain,Infrastructure}` + `tests/DineOS.Tests`. Use `DineOS.slnx` (slnx format).
- **Feature folders** (vertical slice within each layer): named by domain noun, e.g. `Application/Orders/`, `Application/Menu/`, `Application/Billing/`. DTOs, validators, handlers, and interfaces live near the feature; cross-cutting items in `Application/Common/`, `Application/Interfaces/`, `Application/Options/`.
- **DI registration:** each layer exposes a `DependencyInjection.cs` with extension method (e.g. `AddApplication`, `AddInfrastructure`) wired in `Api/Program.cs`.
- **Versioned routes:** `/api/v1/...` URL versioning.
- **Auth:** Keycloak JWT; role names match seeded realm (`SuperAdmin`, `Manager`, `Cashier`, `KitchenStaff`).
- **Persistence:** EF Core migrations via `dotnet ef ... --project src/DineOS.Infrastructure --startup-project src/DineOS.Api`.
- **Tests:** `tests/DineOS.Tests/{Unit,Integration,Authorization,Benchmarks,Fixtures,Common}`. Live integration uses `live.runsettings`; CI uses `default.runsettings`. Artifacts land in `TestResults-ci/`.
- **Logging/observability:** Serilog with Loki sink; Grafana dashboards JSON-provisioned under `backend/grafana/`.
- **Docker:** all infra services in `docker-compose.yml`; dev overrides in `docker-compose.override.yml`.

### Frontend (Next.js)

- **App Router with route groups:** `(public)`, `(protected)`, `(admin)` for access tier; `login/` standalone. Page-level auth gating in `src/middleware.ts`.
- **Component organization:** `src/components/<feature>/` mirrors backend domain (orders, kitchen, menu, payments, shifts, staff, reports, admin, billing, dashboard). Shared primitives in `components/shared/` and `components/ui/`.
- **Hooks:** TanStack Query hooks live in `src/hooks/` named `use<Domain>` (`useKitchenBoard`, `useOrderBoard`, `useReports`). Barrel via `hooks/index.ts`.
- **State:** Zustand stores in `src/stores/`, one store per concern (`authStore`, `orderWizardStore`, `uiStore`); tests beside in `stores/__tests__/`.
- **API client:** typed clients under `src/lib/api/`; auth helpers in `src/lib/auth/`; realtime (SignalR) in `src/lib/realtime/`; Zod schemas in `src/lib/validations/`.
- **Types:** one file per domain in `src/types/` (`order.ts`, `menu.ts`, etc.), barrel `index.ts`.
- **Env:** `NEXT_PUBLIC_API_URL=http://localhost:5000/api` for real backend; falls back to `/api`.
- **Testing:** Jest specs in `frontend/__tests__/`; Playwright in `frontend/e2e/` (a11y, keyboard-nav, order-creation). Test wrappers in `src/test-utils/wrapper.tsx`, `jest.setup.ts`.
- **Styling:** Tailwind CSS 4 via `postcss.config.mjs` and `app/globals.css`.
- **TS strictness:** type-check with `npx tsc --noEmit`; lint with `npm run lint`.

### Cross-cutting

- **Documentation:** feature docs in `docs/backend/<topic>.md` (auth, file-uploads, redis-caching, sql-optimization, background-jobs, rabbitmq-event-flow, ai-features, endpoint-protection-audit-*). DB docs in `docs/database/`.
- **Branch naming:** `<owner>/<issue#>-task-<name>-<slug>` (e.g. `drenzz1/170-task-dreni-signalr-client-for-hubs-orders`).
- **PRs reference issue numbers** (e.g. `(#204)`, `(FE-204)`, `(FE-501)`) in commit subjects.
- **Commit style:** Conventional Commits — `feat:`, `feat(scope):`, `fix:`, `chore:`, `test:`, with scoped optionality (`feat(signup): ...`).
- **AI Dev Log (mandatory):** every completed task appends a row to `docs/AI-Development-Log.md` with Date, Name (Dreni/Endriti/Hera), Tool Used=`Claude Code`, Purpose, Prompt, Output Quality, Time Saved, Lessons Learned; commit + push immediately.
- **No speculative scaffolds:** when asked to "scaffold pages", deliver plan + tasks; do not pre-create `.tsx` files.