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
| `HealthController` | `/api/v1/health` | public rate limit | Thin controller over `IHealthService` |
| `AuthController` | `/api/v1/auth/login`, `/api/v1/auth/refresh`, `/api/v1/auth/logout` | login/refresh are anonymous, logout is authenticated | Thin controller over `IKeycloakAuthService` |
| `MeController` | `/api/v1/me` | authenticated | Reads JWT claims directly — no service |
| `AdminRestaurantsController` | `/api/v1/admin/restaurants` | `SuperAdminOnly` | Thin controller over `IAdminRestaurantService` |
| `StaffController` | `/api/v1/staff` | `ManagerAndAbove` | Thin controller over `IStaffService` |
| `MenuController` | `/api/v1/menu...` | mixed (read: authenticated, write: `ManagerAndAbove`) | Thin controller over `IMenuService` |
| `OrdersController` | `/api/v1/orders...` | `CashierAndAbove` | Thin controller over `IOrderService` |
| `PaymentsController` | `/api/v1/payments...` | `CashierAndAbove` | Thin controller over `IPaymentService` |
| `ShiftNotesController` | `/api/v1/shifts/notes` | mixed (read: authenticated, write: `ManagerAndAbove`) | Thin controller over `IShiftNoteService` |
| `AdminController` | `/api/v1/admin/users` | `SuperAdminOnly` | Thin controller over `IAdminService` — cross-tenant staff listing. Keycloak login-account management is a future, separate integration |
| `RestaurantController` | `/api/v1/restaurant...` | `ManagerAndAbove` | Thin controller over `IRestaurantService` — tenant profile + tables CRUD |
| `KitchenController` | `/api/v1/kitchen...` | `KitchenStaffOnly` | Thin controller over `IKitchenService` |
| `ReportsController` | `/api/v1/reports...` | `ManagerAndAbove` | Thin controller over `IReportsService` |
| `ShiftsController` | `/api/v1/shifts` | `ManagerAndAbove` | Thin controller over `IShiftService` — shift CRUD |

All controllers now follow the service-layer convention below. When adding new endpoints, define an application interface, implement it in Infrastructure, register it in DI, and keep the controller thin.

## Service-Layer Convention

All endpoint logic (persistence, validation orchestration, DTO mapping, business rules) lives in application services. Controllers are thin: bind the request, call the service, translate the result.

Per-feature layout:

```text
DineOS.Application/Interfaces/Services/I<Feature>Service.cs   contract
DineOS.Infrastructure/Services/<Feature>Service.cs            implementation
DineOS.Api/Controllers/<Feature>Controller.cs                 thin entry point
DineOS.Infrastructure/DependencyInjection.cs                  AddScoped<I…, …>()
```

Services return `ServiceResult<T>` (in `DineOS.Application.Common`) instead of throwing or returning `IActionResult`. The result carries:

- `IsSuccess` and `IsCreated` (for 200 vs 201)
- `Value` payload
- `Error` (`ServiceErrorKind`: `NotFound`, `ValidationFailed`, `BadRequest`, `Conflict`, `Unauthorized`, `UnprocessableEntity`)
- `Message` and optional `Errors` list

Controllers translate the result with `ServiceResult<T>.ToActionResult()` (the extension method in `DineOS.Api.Controllers.ServiceResultExtensions`). The extension wraps successes in `ApiResponse<T>` and failures in `ApiResponse.Fail(...)` with the correct status code, preserving the existing API envelope.

A typical controller action collapses to a single expression:

```csharp
public async Task<IActionResult> Create([FromBody] CreateXRequest req, CancellationToken ct) =>
    (await xService.CreateAsync(req, ct)).ToActionResult();
```

Inside a service:

- Run FluentValidation via injected `IValidator<TRequest>`. On failure, return `ServiceResult<T>.ValidationFailed("Validation failed", errors)`.
- For tenant-scoped writes, read `ITenantService.TenantId` and return `BadRequest("Tenant context is required.")` if null.
- Do persistence directly on `AppDbContext` (or `IRepository<T>` where appropriate); tenant + soft-delete filters are applied by `AppDbContext`.
- Project to DTOs inside the service. Do not return entities.

## Business Logging Convention

Services emit `ILogger<TService>.LogInformation(...)` for important state-changing actions (creates, status changes, deletes, role/plan transitions). Reads usually do not log — request logging already covers them.

Log message + property names should be stable and queryable in Loki/Grafana:

```csharp
logger.LogInformation(
    "Staff created: StaffId={StaffId} TenantId={TenantId} ActorUserId={ActorUserId} Role={Role}",
    staff.Id, tenantId, currentUserService.UserId, staff.Role);
```

Required structured properties when available:

- `TenantId` (from `ITenantService`)
- `ActorUserId` (from `ICurrentUserService.UserId`)
- The primary entity id (`StaffId`, `OrderId`, `RestaurantId`, …)
- Domain-specific context that helps future debugging (e.g. `Previous`/`Current` for status changes)

Correlation IDs flow into every log line automatically via `CorrelationIdMiddleware` and Serilog enrichers — services do not need to read or attach them.

Currently emitted business logs:

- `Staff created`, `Staff updated`, `Staff active-status changed`
- `Restaurant created`, `Restaurant status changed`, `Restaurant plan changed`, `Restaurant deleted`, `Restaurant profile updated`
- `Restaurant table created`, `Restaurant table updated`
- `Menu item created/updated/deleted`, `Menu category created`
- `Order created`, `Order status changed`, `Kitchen order status changed`
- `Payment processed`
- `Shift note created`, `Shift note deleted`
- `Shift created`, `Shift updated`, `Shift deleted`

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
- Application services (not controllers) call validators and translate failures into `ServiceResult<T>.ValidationFailed(...)`. See the Service-Layer Convention section below.

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
- `Order` + `OrderItem`: tenant-scoped POS order with line items, type (DineIn/Takeaway), status, total, optional notes.
- `Payment`: tenant-scoped payment record linked to an order, with method and status.
- `MenuItem` + `MenuCategory`: tenant-scoped menu data (category is a denormalized string on `MenuItem`).
- `ShiftNote`: tenant-scoped manager handoff note with priority and author.
- `RestaurantTable`: tenant-scoped table definition (number, capacity, optional location, active flag). Unique on `(TenantId, Number)`.
- `Shift`: tenant-scoped staff shift with `StaffMemberId` FK, start/end time, optional notes.

Enums:

- `SubscriptionPlan`: `Free`, `Pro`.
- `OrderStatus`: `New`, `InProgress`, `Ready`, `Delivered`, `Cancelled`.
- `PaymentMethod`: `Cash`, `Card`. `PaymentStatus`: status of the payment record.
- `ShiftNotePriority`: priority for a shift handoff note.

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

- DbSets currently include `Tenants`, `StaffMembers`, `Orders`, `OrderItems`, `Payments`, `ShiftNotes`, `MenuItems`, `MenuCategories`, `RestaurantTables`, and `Shifts`.
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
- Login, refresh, and logout are implemented through `IKeycloakAuthService`; controllers should not contain direct Keycloak HTTP calls.
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
- Controllers no longer access `AppDbContext`. Persistence and business orchestration live in application services under `DineOS.Infrastructure/Services/`. A generic repository exists but services currently use `AppDbContext` directly.
- API versioning uses URL segments. New controllers should include `[ApiVersion("1.0")]` and routes like `api/v{version:apiVersion}/resource`.
- Keep endpoint resource names kebab-case/plural where possible, matching the Swagger guidance in `Program.cs`.

## Backend Change Checklist For AI Sessions

1. Identify which project owns the change: Api, Application, Domain, Infrastructure, or Tests.
2. Preserve dependency direction; do not reference Api or Infrastructure from Domain/Application.
3. Add or update request models and validators in Application before controller wiring.
4. Add entities to Domain and DbSets/migrations in Infrastructure for persisted data.
5. Use tenant-aware base types for tenant-owned data.
6. Define an `I<Feature>Service` contract in Application, implement it in Infrastructure, and register it in DI. Keep controllers thin (bind → call → `ToActionResult()`).
7. Emit structured `LogInformation` for state-changing operations with `TenantId`, `ActorUserId`, and the entity id (see Business Logging Convention).
8. Return `ApiResponse` envelopes consistently — `ServiceResult<T>.ToActionResult()` handles this for service-backed endpoints.
9. Apply the correct role policy and rate limit attributes on new endpoints.
10. Add tests at the smallest level that proves the behavior, plus integration coverage for cross-layer behavior.
11. Run `dotnet test DineOS.slnx` from `backend/` before considering the backend change complete.
