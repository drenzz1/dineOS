# DineOS Backend AI Context

Use this file as the first backend reference when starting an AI-assisted session. It summarizes the architecture, where important code lives, and the conventions that should guide backend changes.

## Backend At A Glance

- Stack: ASP.NET Core on .NET 10, C#, nullable enabled, implicit usings enabled.
- API style: versioned REST controllers under `/api/v{version:apiVersion}/...`.
- Data: EF Core 10 with PostgreSQL via Npgsql.
- Auth: Keycloak JWT bearer authentication, role policies, Redis refresh-token blacklist.
- Observability: Serilog console logging, optional Grafana Loki sink, correlation IDs.
- Tests: xUnit with unit and integration tests, `WebApplicationFactory`, EF InMemory, and Testcontainers PostgreSQL.
- Local runtime: `backend/docker-compose.yml` starts API, PostgreSQL, Keycloak, Redis, Loki, Grafana, and optional pgAdmin.

## Solution Layout

```text
backend/
  DineOS.slnx
  docker-compose.yml
  src/
    DineOS.Api/             ASP.NET Core host, controllers, auth, middleware, appsettings
    DineOS.Application/     DTOs, request models, validators, service/repository interfaces, response wrappers
    DineOS.Domain/          Entities, base entity types, enums
    DineOS.Infrastructure/  EF DbContext, migrations, repositories, service implementations
  tests/
    DineOS.Tests/           Unit and integration tests
  keycloak/                 Development realm export
  grafana/                  Loki datasource and API dashboard provisioning
  loki/                     Loki config
```

Project dependencies flow inward:

```text
DineOS.Api -> DineOS.Application + DineOS.Infrastructure
DineOS.Infrastructure -> DineOS.Application + DineOS.Domain
DineOS.Application -> DineOS.Domain
DineOS.Domain -> no project dependencies
```

Keep this direction intact. Domain should not know about EF, ASP.NET, Redis, Keycloak, or API response models.

## Entry Point And Pipeline

Main startup file: `src/DineOS.Api/Program.cs`.

Startup registers:

- `AddApplication()` from `DineOS.Application.DependencyInjection`.
- `AddInfrastructure(configuration)` from `DineOS.Infrastructure.DependencyInjection`.
- JWT bearer auth using `Keycloak:Authority`, `Keycloak:Audience`, and optional `Keycloak:MetadataAddress`.
- Role policies: `SuperAdminOnly`, `ManagerAndAbove`, `CashierAndAbove`, `KitchenStaffOnly`.
- API versioning through URL segments.
- Fixed-window rate limits: `public` and `authenticated`.
- Swagger/OpenAPI with bearer auth in Development.
- CORS policy `AllowFrontend`.
- Serilog request logging with correlation, tenant, user, and user-agent enrichment.

Runtime pipeline order matters:

```text
CorrelationIdMiddleware
ExceptionMiddleware
Serilog request logging
Swagger in Development
CORS
HTTPS redirection
Rate limiter
StatusCodePages for empty 401/403 bodies
Authentication
Authorization
TenantIsolationMiddleware
Controllers
```

The API auto-applies EF migrations at startup with `db.Database.Migrate()`.

## API Project

Path: `src/DineOS.Api`.

Important folders:

- `Controllers/`: REST endpoints.
- `Auth/KeycloakRolesTransformation.cs`: maps Keycloak role claims into ASP.NET role claims.
- `Middleware/CorrelationIdMiddleware.cs`: reads or creates `X-Correlation-ID` and pushes it into logs.
- `Middleware/ExceptionMiddleware.cs`: converts unhandled exceptions to JSON error responses.
- `Middleware/TenantIsolationMiddleware.cs`: enforces tenant context for authenticated non-SuperAdmin users.
- `appsettings.json`: default connection string, allowed frontend origins, Loki, Serilog.
- `appsettings.Development.json`: Keycloak and Redis local defaults.

Controller map:

| Controller | Route | Policy | Current state |
|---|---|---|---|
| `HealthController` | `/api/v1/health` | public rate limit | Implemented health response |
| `AuthController` | `/api/v1/auth/refresh`, `/api/v1/auth/logout` | refresh is anonymous, logout is authenticated | Implemented Keycloak refresh/logout with Redis blacklist |
| `MeController` | `/api/v1/me` | authenticated | Implemented JWT profile projection |
| `AdminRestaurantsController` | `/api/v1/admin/restaurants` | `SuperAdminOnly` | Implemented tenant/restaurant listing, create, status, plan |
| `StaffController` | `/api/v1/staff` | `ManagerAndAbove` | Implemented tenant-scoped staff CRUD-style operations |
| `AdminController` | `/api/v1/admin/...` | `SuperAdminOnly` | Placeholder/simple responses |
| `RestaurantController` | `/api/v1/restaurant...` | `ManagerAndAbove` | Placeholder/simple responses |
| `MenuController` | `/api/v1/menu...` | `ManagerAndAbove` | Placeholder/simple responses |
| `OrdersController` | `/api/v1/orders...` | `CashierAndAbove` | Placeholder/simple responses |
| `KitchenController` | `/api/v1/kitchen...` | `KitchenStaffOnly` | Placeholder/simple responses |
| `ReportsController` | `/api/v1/reports...` | `ManagerAndAbove` | Placeholder/simple responses |
| `ShiftsController` | `/api/v1/shifts...` | `ManagerAndAbove` | Placeholder/simple responses |

When adding real behavior to placeholder controllers, add proper entities/DTOs/validators/tests instead of expanding anonymous-object stubs.

## Application Project

Path: `src/DineOS.Application`.

Purpose: contracts and application-facing models. This project should stay framework-light and should not depend on Infrastructure or Api.

Important folders:

- `Common/`: standard response and pagination wrappers.
- `DTOs/`: outward-facing data shapes such as `RestaurantDto`, `StaffMemberDto`, refresh/logout DTOs.
- `Interfaces/Repositories/`: repository contracts.
- `Interfaces/Services/`: service contracts used by Api and Infrastructure.
- `Restaurants/`: restaurant request models and FluentValidation validators.
- `StaffMembers/`: staff request models and FluentValidation validators.

Response conventions:

- Successful responses use `ApiResponse<T>.Ok(data, message)` or `ApiResponse.Ok(message)`.
- Failed responses use `ApiResponse.Fail(message, errors)`.
- Offset pagination uses `PagedRequest` and `PagedResponse<T>`.
- Cursor pagination uses `CursorPagedRequest` and `CursorPagedResponse<T>`.
- Keep response envelopes camelCase through ASP.NET JSON defaults.

Validation conventions:

- Validators live beside request models in the Application project.
- Validators are registered by `AddValidatorsFromAssembly(...)` in `AddApplication()`.
- Controllers currently call validators manually and return `ApiResponse.Fail(...)` on validation errors.

## Domain Project

Path: `src/DineOS.Domain`.

Purpose: core entity model and domain enums.

Base types:

- `BaseEntity`: long `Id`.
- `BaseAuditingEntity`: `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `DeletedAt`, `DeletedBy`.
- `TenantAuditingEntity`: inherits auditing and adds `TenantId`.

Entities:

- `Tenant`: platform restaurant/tenant record. Fields include name, slug, active status, owner/contact info, city, subscription plan, order/staff/revenue metrics.
- `StaffMember`: tenant-scoped staff member with full name, email, role, hashed PIN, and active status.

Enums:

- `SubscriptionPlan`: `Free`, `Pro`.

Entity rules:

- Use `TenantAuditingEntity` for tenant-owned data so EF query filters enforce tenant and soft-delete behavior.
- Use `BaseAuditingEntity` for platform-wide audited data.
- Add new domain entities here first, then wire persistence through `AppDbContext` and migrations.

## Infrastructure Project

Path: `src/DineOS.Infrastructure`.

Purpose: concrete data access, external service adapters, EF migrations, and dependency injection.

Important folders:

- `Persistence/AppDbContext.cs`: EF Core DbContext, DbSets, query filters, seed data.
- `Persistence/Migrations/`: EF Core migration history.
- `Persistence/Interceptors/`: audit and soft-delete save interceptors.
- `Repositories/GenericRepository.cs`: generic repository for audited entities.
- `Services/`: implementations for current user, tenant context, health, PIN hashing, token blacklist.
- `DependencyInjection.cs`: registers infrastructure services, EF, Redis, repositories, interceptors.

Persistence details:

- DbSets currently include `Tenants` and `StaffMembers`.
- `AppDbContext` reads the current tenant once per scoped DbContext from `ITenantService`.
- Query filters:
  - `BaseAuditingEntity`: excludes rows where `DeletedAt` is not null.
  - `TenantAuditingEntity`: excludes soft-deleted rows and filters by current tenant when a tenant exists.
- SuperAdmin requests bypass tenant middleware and therefore do not set a tenant; tenant query filters allow platform-wide access when tenant is null.
- Seed data creates a demo tenant with ID `1`.

Save behavior:

- `AuditInterceptor` sets created/updated timestamps and user IDs on added/modified audited entities.
- `SoftDeleteInterceptor` converts EF hard deletes on audited entities into soft deletes.
- `GenericRepository<T>.DeleteAsync(...)` also soft-deletes explicitly and should be preferred when using the repository.

Service details:

- `CurrentUserService`: reads current user identity from `IHttpContextAccessor`.
- `TenantService` / `HttpContextTenantService`: resolve tenant context from HTTP context.
- `PinHasher`: hashes staff PINs with BCrypt.
- `TokenBlacklistService`: stores revoked refresh-token `jti` values in Redis with TTL.
- `HealthService`: returns API health metadata.

## Authentication And Authorization

Identity provider: Keycloak.

Development config:

- Realm import: `backend/keycloak/realm-export.json`.
- Local authority: `http://localhost:8080/realms/dineos`.
- API audience/client: `dineos-api`.

Expected roles:

- `SuperAdmin`
- `Manager`
- `Cashier`
- `KitchenStaff`

Authorization policies:

- `SuperAdminOnly`: `SuperAdmin`.
- `ManagerAndAbove`: `SuperAdmin`, `Manager`.
- `CashierAndAbove`: `SuperAdmin`, `Manager`, `Cashier`.
- `KitchenStaffOnly`: `KitchenStaff`.

Tenant isolation:

- JWT `tenant_id` claim is authoritative for authenticated non-SuperAdmin users.
- `X-Tenant-ID` is only a hint and must match the JWT if provided.
- Route value `tenantId`, when present, must match the JWT tenant.
- Resolved tenant ID is stored in `HttpContext.Items["TenantId"]`.

Token revocation:

- Access-token validation stays stateless on normal API calls.
- Refresh/logout paths use Redis to blacklist refresh-token `jti` values until their natural expiry.
- See `../docs/backend/auth.md` for the rationale and trade-offs.

## Database And Migrations

Local connection string:

```text
Host=localhost;Port=5432;Database=dineos;Username=dineos;Password=dineos_dev
```

Migration commands from `backend/`:

```bash
dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api

dotnet ef migrations add <MigrationName> \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api \
  --output-dir Persistence/Migrations
```

Existing migration guide: `../docs/database-migrations.md`.

When changing entities:

- Update Domain entities first.
- Update `AppDbContext` DbSets/configuration as needed.
- Add an EF migration under `DineOS.Infrastructure/Persistence/Migrations`.
- Update tests for query filters, controllers, and integration behavior if the change affects tenant isolation or API contracts.

## Local Development

Run from `backend/`.

Start dependencies plus API:

```bash
docker compose up --build
```

Start only PostgreSQL:

```bash
docker compose up postgres -d
```

Optional pgAdmin:

```bash
docker compose --profile tools up pgadmin -d
```

Docker ports:

- API: `http://localhost:5000` mapped to container port `8080`.
- Keycloak: `http://localhost:8080`.
- PostgreSQL: `localhost:5432`.
- Redis: `localhost:6379`.
- Loki: `http://localhost:3100`.
- Grafana: `http://localhost:4000`.
- pgAdmin: `http://localhost:5050`.

Build and test from `backend/`:

```bash
dotnet build DineOS.slnx
dotnet test DineOS.slnx
```

## Testing

Path: `tests/DineOS.Tests`.

Test structure:

- `Unit/`: middleware, services, repository, controller, auth transformation tests.
- `Integration/`: API behavior, RBAC, auth error responses, staff, admin restaurants.
- `Fixtures/CustomWebApplicationFactory.cs`: integration test host setup.
- `Fixtures/IntegrationTestCollection.cs`: shared integration collection.
- `Common/ApiResponseTests.cs`: response wrapper behavior.

When adding backend behavior:

- Add unit tests for pure services, validators, middleware, and repository behavior.
- Add integration tests for controller routes, auth policies, tenant isolation, and persistence behavior.
- Prefer integration tests when the change crosses Api, Infrastructure, and EF query filters.

## Current Implementation Notes

- Restaurant/tenant administration and staff management are the most complete backend feature areas.
- Several operational controllers exist primarily to define route shape and auth policy; many return placeholder `ApiResponse` objects with anonymous data.
- EF query filters are central to tenant isolation. Be careful with `IgnoreQueryFilters()` because it can bypass soft-delete and tenant filtering.
- SuperAdmin is intentionally platform-wide. Tenant-scoped staff/restaurant users require a valid `tenant_id` claim.
- Controllers currently use `AppDbContext` directly for implemented features. A generic repository exists, but the codebase has not fully moved all controller persistence through it.
- API versioning uses URL segments. New controllers should include `[ApiVersion("1.0")]` and routes like `api/v{version:apiVersion}/resource`.
- Keep endpoint resource names kebab-case/plural where possible, matching the Swagger guidance in `Program.cs`.

## Backend Change Checklist For AI Sessions

1. Identify which project owns the change: Api, Application, Domain, Infrastructure, or Tests.
2. Preserve dependency direction; do not reference Api or Infrastructure from Domain/Application.
3. Add or update request models and validators in Application before controller wiring.
4. Add entities to Domain and DbSets/migrations in Infrastructure for persisted data.
5. Use tenant-aware base types for tenant-owned data.
6. Return `ApiResponse` envelopes consistently.
7. Apply the correct role policy and rate limit attributes on new endpoints.
8. Add tests at the smallest level that proves the behavior, plus integration coverage for cross-layer behavior.
9. Run `dotnet test DineOS.slnx` from `backend/` before considering the backend change complete.

