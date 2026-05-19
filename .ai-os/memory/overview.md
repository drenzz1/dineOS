# overview.md

## dineOS — Multi-tenant Restaurant Operating System

**Repo layout:** monorepo with `backend/` (.NET 10), `frontend/` (Next.js 16), `docs/`, `e2e/` (reserved; specs live in `frontend/e2e`).

### Stack

**Backend (`backend/`)**
- .NET 10 / ASP.NET Core, EF Core 10 + PostgreSQL
- Keycloak JWT auth (realm imported from `backend/keycloak/realm-export.json`)
- Redis: refresh-token blacklist + SignalR backplane
- RabbitMQ messaging (see `docs/backend/rabbitmq-event-flow.md`)
- Serilog → optional Grafana Loki sink; Grafana dashboards in `backend/grafana/`
- xUnit (unit, integration, benchmarks) in `tests/DineOS.Tests/`
- Stripe billing integration

**Frontend (`frontend/`)**
- Next.js 16 App Router, React 19, TypeScript
- Tailwind CSS 4, TanStack Query, Zustand, React Hook Form, Zod
- Jest (`__tests__/`) + Playwright (`e2e/`)
- SignalR client for `/hubs/orders` realtime

### Architecture (Clean / DDD-ish)

Backend follows layered separation:
- `DineOS.Domain` — entities (`Entities/`), enums, common types
- `DineOS.Application` — feature folders (Auth, Billing, Kitchen, Menu, Orders, Payments, RestaurantProfile, RestaurantTables, Restaurants, ShiftNotes, Shifts, Signup, StaffMembers, Notifications, Messaging), DTOs, `Interfaces/`, `Options/`, `DependencyInjection.cs`
- `DineOS.Infrastructure` — EF persistence, repositories, services, jobs, email templates, messaging, `DependencyInjection.cs`
- `DineOS.Api` — Controllers, Hubs (SignalR), Middleware, Auth, `Program.cs`

Frontend route groups:
- `(public)`, `(protected)`, `(admin)`, `login/`
- `components/` by feature (admin, billing, dashboard, kitchen, menu, orders, payments, reports, shared, shifts, staff, ui)
- `lib/api/`, `lib/auth/`, `lib/realtime/`, `lib/validations/`
- `stores/` (Zustand: `authStore`, `orderWizardStore`, `uiStore`)
- `hooks/` (TanStack Query-backed: `useKitchenBoard`, `useOrderBoard`, `useReports`, `useStaff`, `useAdminAnalytics`, `useDailySummary`, `useMe`, `useTenant`, `useFocusTrap`, `useToast`)
- `types/` mirrored per domain (admin, billing, me, menu, order, payment, reports, restaurant, restaurantProfile, restaurantTable, shift, staff)

### Entry Points

- Backend API: `backend/src/DineOS.Api/Program.cs` (http://localhost:5000, Swagger `/swagger`)
- Frontend: `frontend/src/app/layout.tsx` + `frontend/src/app/page.tsx`, providers wired in `app/providers.tsx`, middleware in `src/middleware.ts`
- Auth endpoints: `POST /api/v1/auth/{login,refresh,logout}`
- Realtime hub: `/hubs/orders`
- Docker entry: `backend/docker-compose.yml` (+ override)

### Local Ports

API 5000, Keycloak 8080, Postgres 5432, Redis 6379, Grafana 4000, Loki 3100.

### Key Files

- `backend/DineOS.slnx`, `backend/Directory.Build.props`
- `frontend/next.config.ts`, `frontend/eslint.config.mjs`, `frontend/jest.config.ts`, `frontend/playwright.config.ts`
- `docs/database/{ERD,SCHEMA}.md`, `docs/keycloak-setup.md`, `docs/database-migrations.md`
- `docs/AI-Development-Log.md` (mandatory log)

### Seeded Dev Users

`admin|manager|cashier|kitchen@dineos.dev` / `Test1234!` mapped to roles SuperAdmin, Manager, Cashier, KitchenStaff.