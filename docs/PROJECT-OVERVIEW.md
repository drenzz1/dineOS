# dineOS — Full Project Documentation

> **Multi-tenant SaaS restaurant management platform**  
> Built on .NET 10 + Next.js 16 · Kubernetes-deployed · Production-ready

---

## Table of Contents

1. [What Is dineOS?](#1-what-is-dineos)
2. [Architecture Overview](#2-architecture-overview)
3. [Frontend](#3-frontend)
4. [Backend API](#4-backend-api)
5. [Domain Model](#5-domain-model)
6. [Authentication & Authorization](#6-authentication--authorization)
7. [Real-Time & Messaging](#7-real-time--messaging)
8. [Background Jobs](#8-background-jobs)
9. [Infrastructure & DevOps](#9-infrastructure--devops)
10. [Observability Stack](#10-observability-stack)
11. [All API Endpoints](#11-all-api-endpoints)
12. [Live Deployment — project-06](#12-live-deployment--project-06)
13. [Demo Flow](#13-demo-flow)
14. [Known Limitations](#14-known-limitations)

---

## 1. What Is dineOS?

dineOS is a **multi-tenant SaaS restaurant management platform** that allows restaurants to:

- **Take and track orders** from the front-of-house (Cashier) to the kitchen (KitchenStaff) through to delivery
- **Manage their menu** with categories, items, images, and AI-generated descriptions
- **Run their team** with staff records, shift scheduling, and PIN-based session switching for shared terminals
- **Process payments** per order and track payment status
- **View business analytics** — revenue, order counts, staff activity
- **Subscribe to plans** (Free / Pro) via Stripe billing
- **Receive real-time updates** across devices via WebSocket

Each restaurant is a **separate tenant** — fully isolated data, separate Keycloak user account, separate billing. A **SuperAdmin** manages the platform and can create, monitor, or suspend any tenant.

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         BROWSER                                 │
│  Next.js 16 (App Router · React 19 · TypeScript · Tailwind 4)  │
│  SignalR WS · TanStack Query · Zustand · Zod · React Hook Form  │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTPS / WS
                    ┌──────▼──────┐
                    │  Nginx Ingress │  TLS termination
                    └──────┬───────┘
           ┌───────────────┼──────────────────┐
           │               │                  │
    ┌──────▼──────┐  ┌──────▼──────┐  ┌───────▼──────┐
    │  dineOS API │  │  Keycloak   │  │  Frontend    │
    │  .NET 10    │  │  24.0.5     │  │  Next.js Pod │
    │  ASP.NET    │  │  OIDC/JWT   │  │              │
    └──────┬──────┘  └─────────────┘  └──────────────┘
           │
    ┌──────┴────────────────────────────────────┐
    │              DATA LAYER                   │
    │  PostgreSQL 16  │  Redis 7  │  RabbitMQ 4  │
    └───────────────────────────────────────────┘
           │
    ┌──────┴───────────────────────┐
    │         OBSERVABILITY        │
    │  Prometheus · Loki · Grafana │
    │  AlertManager · Uptime Kuma  │
    └──────────────────────────────┘
```

**Request flow (authenticated user):**
1. Browser → Nginx → Next.js frontend (SSR / API routes)
2. Next.js server-side → Keycloak password grant → JWT token
3. Browser requests → `Authorization: Bearer <JWT>` → API
4. API validates JWT issuer/signature via Keycloak OIDC metadata
5. `TenantIsolationMiddleware` extracts `tenant_id` claim
6. EF Core global query filters scope all DB queries to the tenant
7. Response → JSON envelope `{ success, data, message, errors }`

---

## 3. Frontend

### Tech Stack

| Concern | Technology |
|---------|-----------|
| Framework | Next.js 16, App Router, React 19, TypeScript 5 |
| Styling | Tailwind CSS 4, mobile-first |
| Server state | TanStack Query (React Query) v5 |
| Client state | Zustand v5 |
| Forms | React Hook Form v7 + Zod v4 |
| Real-time | Microsoft SignalR v10 (WebSocket) |
| Charts | Recharts v3 |
| Drag & Drop | dnd-kit |
| HTTP client | Axios 1.16 |
| Testing | Jest 30, React Testing Library, Playwright (E2E) |

### Route Structure

```
app/
 ├── (public)/
 │    ├── login/                   # Keycloak password grant login
 │    ├── first-login/             # Forced password change on first sign-in
 │    └── demo/                    # Public demo request form
 │
 ├── (protected)/                  # Authenticated tenant routes
 │    ├── dashboard/               # Overview — orders, revenue KPIs
 │    ├── orders/                  # Order list + create form
 │    ├── kitchen/                 # Kitchen board (real-time order queue)
 │    ├── menu/                    # Menu items + categories
 │    ├── payments/                # Payment records
 │    ├── reports/                 # Sales, orders, staff analytics
 │    ├── shifts/                  # Shift schedule + notes
 │    ├── staff/                   # Staff member management
 │    ├── settings/
 │    │    ├── restaurant/         # Restaurant profile
 │    │    ├── tables/             # Seating configuration
 │    │    └── billing/            # Stripe subscription management
 │    └── select-staff/            # PIN-based staff session picker
 │
 └── (admin)/                      # SuperAdmin routes
      ├── admin/dashboard/         # Platform-wide analytics
      ├── admin/restaurants/       # All tenants list + create
      └── admin/restaurants/[id]/  # Tenant detail + status controls
```

### Authentication Flow (frontend)

```
1. User submits email + password
2. Next.js API route → POST /api/v1/auth/login
   → API calls Keycloak password grant (server-to-server, internal URL)
   → Keycloak returns JWT + refresh_token
3. API sets HttpOnly session cookie (access_token + refresh_token)
4. Browser never talks directly to Keycloak
5. Every API call: cookie → Authorization header (server-side header injection)
6. Token expiry: access=5min, refresh=30min
7. Refresh interceptor (Axios): auto-refreshes on 401
```

> **Important:** `NEXT_PUBLIC_KEYCLOAK_URL` points to the internal Keycloak service — this is intentional. The frontend uses it for server-side API routes only; the browser never makes Keycloak requests directly.

### Staff PIN Sessions

For shared POS terminals, a staff member can "switch in" with their PIN:
- Owner logs into Keycloak normally (full access)
- On the PIN selection screen, staff member taps their name + enters PIN
- API issues a short-lived HS256 staff-session token with the staff member's role
- This token is scoped — a Cashier staff-session cannot perform Manager actions
- Owner logs out or shifts end to clear the session

---

## 4. Backend API

### Tech Stack

| Concern | Technology |
|---------|-----------|
| Runtime | .NET 10, ASP.NET Core |
| ORM | Entity Framework Core 10, Npgsql |
| Auth | Keycloak JWT + custom staff-session signing (HS256) |
| Cache | Redis 7 (token blacklist + SignalR backplane) |
| Messaging | RabbitMQ 4 (topic exchange + DLX) |
| Background jobs | Hangfire (PostgreSQL-backed) |
| Real-time | ASP.NET Core SignalR |
| Logging | Serilog → Loki |
| Metrics | Prometheus (custom business + EF Core) |
| Email | MailKit SMTP |
| File storage | Local filesystem (UUID-based, per-tenant) |
| API versioning | Asp.Versioning v1.0 |
| Validation | FluentValidation |
| AI | Anthropic Claude / OpenAI GPT / Google Gemini (pluggable `IAiClient`) |
| Payments | Stripe (checkout, customer portal, webhooks) |

### Project Structure

```
DineOS.Api/            HTTP host — controllers, middleware, hubs, Program.cs
DineOS.Application/    Interfaces, DTOs, validators, domain commands/queries
DineOS.Domain/         Entities, enums, base classes
DineOS.Infrastructure/ EF Core, services, jobs, messaging, auth clients
DineOS.Tests/          xUnit, WebApplicationFactory, Testcontainers
```

### Response Envelope

Every API response uses the same shape:

```json
{
  "success": true,
  "data": { ... },
  "message": "Human-readable message",
  "errors": null
}
```

Errors return `success: false` with an `errors` array and the appropriate HTTP status code.

---

## 5. Domain Model

### Entity Overview

```
Tenant  ──< Order  ──< OrderItem
        ──< MenuItem  ──< MenuCategory (via Category field)
        ──< Payment (linked to Order)
        ──< StaffMember
        ──< Shift  ──< ShiftNote
        ──< RestaurantTable
        ──< EmailVerificationCode
        ──< TenantInvoice
        ──< DemoUser (for sandbox accounts)
```

### Key Entities

#### Tenant (restaurant/business)
```
Id, Name, Slug (unique URL-safe identifier)
OwnerName, OwnerEmail, Phone, City
Plan (Free | Pro)
BillingStatus (Active | PastDue | Canceled | Trialing)
BillingCycle (Monthly | Annual)
StripeCustomerId, StripeSubscriptionId
KeycloakUserId (Keycloak account for the owner)
IsActive, CreatedAt, DeletedAt (soft-delete)
```

#### Order
```
Id, TenantId, OrderType (dine-in | pickup)
TableNumber (nullable, required for dine-in)
Status: New → InProgress → Ready → Delivered | Cancelled
Total (decimal), Notes
Items: OrderItem[]  (Name, Quantity, UnitPrice, Notes)
CreatedAt, UpdatedAt
```

#### Payment
```
Id, TenantId, OrderId
Amount, Method (Cash | Card | Mobile | Other)
Status (Pending | Completed | Failed)
OverdueNotifiedAt  ← prevents duplicate overdue emails
CreatedAt
```

#### MenuItem
```
Id, TenantId, Name, Price
Category (string — free-form, also exists as MenuCategory)
Description, ImageUrl (UUID path)
CreatedAt, UpdatedAt, DeletedAt (soft-delete)
```

#### StaffMember
```
Id, TenantId, FullName, Email
Role (Manager | Cashier | KitchenStaff — string, mirrors Keycloak roles)
PinHash (SHA-512 with salt)
IsActive, CreatedAt, DeletedAt
```

#### Shift
```
Id, TenantId, StaffMemberId (FK → StaffMember)
StartTime, EndTime, Notes
DeletedAt (soft-delete)
```

#### ShiftNote
```
Id, TenantId, Title, Body
Priority (Info | Warning | Urgent)
Author, CreatedAt
```

#### RestaurantTable
```
Id, TenantId, Number (unique per tenant), Capacity
Location (string), IsActive
```

### Soft Delete & Tenant Isolation (EF Core)

All entities extend `TenantAuditingEntity` which carries:
- `TenantId` (enforced by global query filter)
- `DeletedAt` (nullable — null means active; soft-delete filter applies)

The global query filter translates to:

```sql
WHERE deleted_at IS NULL
AND (@tenantId IS NULL OR tenant_id = @tenantId)
```

SuperAdmin gets `@tenantId = NULL` → sees all tenants' records.  
Regular users get `@tenantId = <their tenant>` → only their data.

---

## 6. Authentication & Authorization

### Two Auth Schemes

| Scheme | Who | Token type | Lifetime |
|--------|-----|-----------|---------|
| `Bearer` (Keycloak) | Owners, Managers, Cashiers, KitchenStaff, SuperAdmin | RS256 JWT from Keycloak | 5 min access / 30 min refresh |
| `StaffSession` | PIN-selected staff on shared terminals | HS256 JWT from API | 60 min access / 12 hr refresh |

### Roles & Composite Graph

```
SuperAdmin  ← platform-wide, no tenant
  └── Full access to all endpoints

Demo        ← composite (same as Owner for demo tenant)
  ├── Owner  ← composite
  │    └── Manager
  ├── Manager
  └── KitchenStaff
```

> **Why composite?** The Owner account has full operational access (composite of Manager) so the restaurant owner can do everything without needing a separate staff PIN. The Demo role inherits Owner+Manager+KitchenStaff so demo users see all features.

### Authorization Policies

| Policy | Allowed Roles | Used For |
|--------|--------------|---------|
| `SuperAdminOnly` | SuperAdmin | Platform admin endpoints |
| `OwnerOnly` | SuperAdmin, Owner | Staff management, billing |
| `BusinessAccountOnly` | Keycloak Bearer only | Auth refresh/logout |
| `ManagerAndAbove` | SuperAdmin, Owner, Manager | Restaurant operations |
| `CashierAndAbove` | SuperAdmin, Owner, Manager, Cashier | Orders, payments |
| `KitchenAccess` | SuperAdmin, Owner, Manager, Cashier, KitchenStaff | Kitchen workflow |
| `KitchenStaffOnly` | KitchenStaff | Kitchen-only actions |

### Tenant Isolation Middleware

```
Request arrives →
  Is unauthenticated? → pass through (auth middleware handles 401)
  Is SuperAdmin? → pass through (no tenant scope)
  Has tenant_id JWT claim? → set context.Items["TenantId"]
  No tenant_id? → 403 "Tenant context is required"
```

### Token Refresh & Blacklist

- Refresh tokens stored as HttpOnly cookies
- On logout: refresh token added to Redis blacklist (TTL = remaining lifetime)
- On refresh attempt: Redis checked first — if blacklisted → 401
- Staff session tokens: jti (JWT ID) tracked in Redis for revocation on shift end

---

## 7. Real-Time & Messaging

### SignalR — Order Updates

**Hub:** `/hubs/orders` (also `/api/hubs/orders` for Kubernetes ingress compatibility)

**Auth:** JWT passed as query string `?access_token=<token>` (browsers can't set headers on WebSocket)

**Groups:** Each connection is added to group `tenant-{tenantId}` — clients only receive their own restaurant's events.

**Events fired by the server:**

```typescript
// When an order is created
OrderCreated: {
  orderId: number
  tenantId: number
  orderType: string        // "dine-in" | "pickup"
  tableNumber: number | null
  status: string           // "New"
  total: number
  notes: string | null
  createdAt: string        // ISO 8601
  items: { name, quantity, unitPrice, notes }[]
}

// When order status changes
OrderStatusChanged: {
  orderId: number
  tenantId: number
  oldStatus: string
  newStatus: string
  changedAt: string
}
```

**Scale-out:** Redis backplane (`dineos:signalr` prefix) allows multiple API replicas to broadcast to the same group.

### RabbitMQ — Order Created Event

```
POST /orders
  → OrderService.CreateOrderAsync()
  → IMessagePublisher.PublishAsync(OrderCreatedMessage)
  → RabbitMQ topic exchange: dineos.events
    routing key: orders.created
  → Queue: dineos.orders.created.notifications
  → RabbitMqOrderCreatedConsumer.HandleAsync()
     (idempotent — checks ProcessedMessages table)
  → OrderNotificationService.BroadcastOrderCreatedAsync()
  → SignalR group tenant-{id} → all connected clients

Dead Letter Exchange (DLX):
  Failed messages → dineos.events.dlx
  → dineos.orders.created.notifications.dlq
```

This decouples order creation from real-time notification. If SignalR broadcast fails, the message is retried from the DLQ.

---

## 8. Background Jobs

All jobs run via **Hangfire** backed by PostgreSQL.

### Recurring Jobs (cron)

| Job | Schedule | What it does |
|-----|----------|-------------|
| `DailySummaryJob` | `55 23 * * *` (11:55 PM) | Sends nightly payment recap email to each tenant's owner |
| `OverdueScan` | `*/5 * * * *` (every 5 min) | Finds payments pending > 30 min, sends one notification email |
| `DemoCleanup` | `0 3 * * *` (3 AM) | Soft-deletes demo user records older than TTL |

### Fire-and-Forget Jobs (triggered by API)

| Job | Triggered By | What it does |
|-----|-------------|-------------|
| `OwnerProvisioningJob` | Stripe `checkout.session.completed` webhook | Creates Keycloak user account for new restaurant owner, sends welcome email |
| `AccountVerificationEmailJob` | Owner signup | Sends 6-digit email verification code |
| `SubscriptionActivatedEmailJob` | Stripe `subscription.updated` | Sends "subscription activated" email |
| `SubscriptionCanceledEmailJob` | Stripe cancellation | Sends "subscription canceled" email |
| `PaymentFailedEmailJob` | Stripe `invoice.payment_failed` | Alerts owner of payment failure |
| `DemoProvisioningJob` | `POST /demo/request` (new email) | Creates demo Keycloak user, sets `tenant_id` attribute, assigns Demo role |
| `DemoCredentialsResendJob` | `POST /demo/request` (existing active user) | Rotates demo password, re-stamps `tenant_id`, resends credentials |
| `DemoWelcomeEmailJob` | After provisioning/resend | Sends demo login credentials email |

---

## 9. Infrastructure & DevOps

### Docker Compose (local dev)

| Service | Purpose | Ports |
|---------|---------|-------|
| `api` | ASP.NET Core backend | 5001→8080 |
| `frontend` | Next.js frontend | 3000 |
| `nginx` | Reverse proxy | 80 |
| `postgres` | Primary database | 5432 |
| `redis` | Cache + SignalR backplane | 6379 |
| `keycloak` | OIDC identity provider | 8080 |
| `rabbitmq` | Message broker | 5672, 15672 (UI) |
| `mailhog` | Dev SMTP server + UI | 1025, 8025 |
| `prometheus` | Metrics scraping | 9090 |
| `grafana` | Dashboards | 4000 |
| `loki` | Log aggregation | 3100 |
| `alertmanager` | Alert routing | 9093 |
| `uptime-kuma` | Uptime monitoring | 3001 |
| `pgadmin` *(optional)* | DB admin UI | 5050 |
| `elasticsearch` *(ELK profile)* | Centralized logging | 9200 |
| `kibana` *(ELK profile)* | Log UI | 5601 |
| `logstash` *(ELK profile)* | Log processing | 5044 |

### Kubernetes Helm Chart

Located at `deploy/helm/dineos/`

**Sub-chart dependencies:**
- `bitnami/postgresql` — primary DB
- `bitnami/redis` — cache + backplane
- `bitnami/rabbitmq` — messaging
- `keycloak/keycloak` — OIDC provider (persisted in Postgres)

**Values files:**
- `values.yaml` — defaults
- `values.project-06.yaml` — production overrides (project-06 cluster)
- `values.local.yaml` — local dev overrides (gitignored)

**Key production settings (`values.project-06.yaml`):**
- `api.replicaCount: 2` — 2 API pods
- `frontend.replicaCount: 2` — 2 frontend pods
- `ingress.host: app.project-06.gjirafa.dev`
- TLS via cert-manager + Let's Encrypt
- Images from `ghcr.io/drenzz1/dineos-*:main`

### CI/CD Pipelines

| Workflow | Trigger | Steps |
|----------|---------|-------|
| `backend-ci.yml` | Push/PR touching `backend/**` | `dotnet build` → `dotnet test` → coverage gate |
| `ci.yml` | Push/PR touching `frontend/**` | ESLint → Jest → Playwright E2E → Trivy security scan |
| `helm.yml` | Changes to `deploy/helm/**` | `helm lint` → `kubeconform` schema validation |
| `observability.yml` | Changes to `backend/prometheus/**` | `promtool check rules` → `amtool check config` |
| `commitlint.yml` | Any PR | Enforce Conventional Commits |
| `build-push.yml` | Push to `main` or `v*.*.*` tags | Docker build → push GHCR → `helm upgrade` cluster → Slack notify |
| `release-please.yml` | Push to `main` | Auto release PR → version bump → tag |

---

## 10. Observability Stack

### Metrics (Prometheus + Grafana)

Endpoint: `GET /metrics` (public, no auth)

Collected metrics:
- HTTP request duration histograms (by endpoint, method, status code)
- Order creation count, payment processing count (custom business metrics)
- EF Core query execution duration
- SignalR connection count
- Hangfire job completion rate

**Pre-built Grafana dashboards:**
- `api-overview.json` — request rate, latency percentiles, error rate
- `dineos-api.json` — business KPIs (orders/hour, revenue/day)
- `dotnet-runtime.json` — GC, thread pool, memory
- `infrastructure.json` — CPU, memory, disk per pod

### Logs (Serilog → Loki → Grafana)

Structured JSON logs enriched with:
- `TenantId` — which restaurant made the request
- `UserId` — which user (when DB record exists)
- `CorrelationId` — trace requests across pods
- `MachineName` — which pod handled the request
- `Application` — always `DineOS.Api`

Log levels:
- `Information` — all HTTP requests (with latency), key business events
- `Warning` — auth failures, unexpected states, missing config
- `Error` — unhandled exceptions, job failures

### Alerting (AlertManager → Slack)

Alert rules in `backend/prometheus/rules/`:
- `api.rules.yml` — high error rate, slow response time, Hangfire job failures
- `infra.rules.yml` — pod restarts, memory pressure, disk usage

### Uptime Monitoring (Uptime Kuma)

Monitors `GET /api/v1/health` every 30s. Public status page at `status.project-06.gjirafa.dev`.

Health endpoint returns:
```json
{
  "status": "Healthy",
  "timestamp": "...",
  "version": "1.0.0",
  "components": {
    "database": "up",
    "redis": "up"
  }
}
```

---

## 11. All API Endpoints

**Base URL:** `https://app.project-06.gjirafa.dev/api/v1/`

### Public (no auth required)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/health` | Liveness probe — DB + Redis status |
| POST | `/auth/login` | `{ username, password }` → JWT tokens |
| POST | `/auth/refresh` | `{ refreshToken }` → new access token |
| POST | `/auth/staff-session/refresh` | Staff session token refresh |
| POST | `/auth/first-login-password-change` | Forced password change on first login |
| POST | `/demo/request` | `{ email, acceptTerms }` → queues demo provisioning |
| POST | `/signup` | `{ restaurantName, ownerName, ownerEmail, phone, city }` → Stripe checkout |
| GET | `/signup/status?sessionId=` | Poll signup flow status |
| POST | `/alerts/webhook` | Alertmanager webhook (internal use) |
| POST | `/billing/webhook` | Stripe webhook (signature-verified) |
| GET | `/metrics` | Prometheus metrics scrape endpoint |

### Authenticated — Any Role

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/me` | Current user profile (id, email, roles, tenantId) |
| POST | `/auth/logout` | Revoke refresh token |
| GET | `/menu/items` | List all menu items for tenant |
| GET | `/menu/categories` | List all menu categories |
| GET | `/shifts` | List shifts (optional `?date=YYYY-MM-DD`) |
| GET | `/shifts/notes` | List shift notes |

### Cashier and Above

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/orders` | List orders (`?date=`, `?status=`) |
| GET | `/orders/{id}` | Get single order |
| POST | `/orders` | Create order `{ orderType, tableNumber?, items[], notes? }` |
| PATCH | `/orders/{id}/status` | Update order status |
| GET | `/payments/open-orders` | Open (unpaid) orders |
| POST | `/payments` | Process payment `{ orderId, method, amount }` |
| GET | `/kitchen/orders` | Active orders for kitchen |
| GET | `/kitchen/queue` | Queue summary `{ pending, inProgress, ready }` |

### Kitchen Staff and Above

| Method | Route | Purpose |
|--------|-------|---------|
| PUT | `/kitchen/orders/{id}/status` | Update kitchen order status |

### Manager and Above

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/restaurant` | Restaurant profile |
| PUT | `/restaurant` | Update profile (name, city, phone, etc.) |
| GET | `/restaurant/tables` | List tables |
| POST | `/restaurant/tables` | Create table |
| PUT | `/restaurant/tables/{id}` | Update table |
| GET | `/menu` | Full menu (items grouped) |
| POST | `/menu/items` | Create menu item `{ name, price, category, description? }` |
| PUT | `/menu/items/{id}` | Update menu item |
| POST | `/menu/items/{id}/image` | Upload item image (multipart, ≤5 MB) |
| DELETE | `/menu/items/{id}` | Soft-delete menu item |
| POST | `/menu/categories` | Create category |
| POST | `/shifts` | Create shift `{ staffMemberId, startTime, endTime }` |
| PUT | `/shifts/{id}` | Update shift |
| DELETE | `/shifts/{id}` | Soft-delete shift |
| POST | `/shifts/notes` | Create note `{ title, body, priority, author }` |
| DELETE | `/shifts/notes/{id}` | Delete note |
| GET | `/reports/sales` | Sales report (`?from=`, `?to=`) |
| GET | `/reports/orders` | Orders report |
| GET | `/reports/staff` | Staff activity report |
| POST | `/ai/menu-items/{id}/describe` | AI-generate description (Claude/OpenAI/Gemini) |

### Owner Only (Keycloak scheme only)

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/staff` | List all staff members |
| POST | `/staff` | Create staff member |
| PUT | `/staff/{id}` | Update staff member |
| PATCH | `/staff/{id}/active` | Toggle active status |
| POST | `/auth/staff-session` | Mint PIN-gated staff session token |
| POST | `/auth/staff-session/end` | End staff session (blacklist token) |
| GET | `/billing/subscription` | Current subscription details |
| POST | `/billing/checkout-session` | Create Stripe checkout session |
| POST | `/billing/portal-session` | Create Stripe customer portal URL |
| GET | `/billing/invoices` | Invoice history |

### Super Admin Only

| Method | Route | Purpose |
|--------|-------|---------|
| GET | `/admin/analytics` | Platform KPIs (total tenants, revenue, orders) |
| GET | `/admin/users` | Platform users list (searchable, paginated) |
| GET | `/admin/restaurants` | All tenants (paginated, search) |
| POST | `/admin/restaurants` | Create new tenant |
| GET | `/admin/restaurants/{id}` | Tenant details |
| PATCH | `/admin/restaurants/{id}/status` | Activate / suspend tenant |
| PATCH | `/admin/restaurants/{id}/plan` | Change subscription plan |
| DELETE | `/admin/restaurants/{id}` | Soft-delete tenant |
| POST | `/admin/restaurants/{id}/email-verification/resend` | Resend owner verification |

### Rate Limits

| Policy | Limit | Applies To |
|--------|-------|-----------|
| `public` | 60 req / min | Anonymous endpoints |
| `authenticated` | 300 req / min | All authenticated endpoints |
| `ai-expensive` | 10 req / min | AI description endpoint |
| `staff-pin` | 10 req / min | PIN session creation |
| `demo-request` | 3 / email / hour, 10 / IP / hour | Demo request |

---

## 12. Live Deployment — project-06

**Public URL:** `https://app.project-06.gjirafa.dev`  
**Keycloak Admin Console:** `https://app.project-06.gjirafa.dev/auth/admin/`  
**Status Page:** `https://status.project-06.gjirafa.dev`  
**Cluster:** Hetzner Cloud, k3s `life-cluster` (server: `159.69.213.73`)

### Running Pods (all healthy)

| Pod | Replicas | Purpose |
|-----|----------|---------|
| `dineos-api` | 2 | ASP.NET Core API |
| `dineos-frontend` | 2 | Next.js frontend |
| `dineos-keycloak` | 1 | OIDC provider (Postgres-backed) |
| `dineos-postgres` | 1 | Primary database |
| `dineos-redis-master` | 1 | Cache + SignalR backplane |
| `dineos-rabbitmq` | 1 | Message broker |
| `dineos-elasticsearch` | 1 | Log storage |
| `dineos-kibana` | 1 | Log UI |
| `dineos-logstash` | 1 | Log ingestion |
| `dineos-prometheus` | 1 | Metrics scraping |
| `dineos-alertmanager` | 1 | Alert routing |
| `grafana` | 1 | Dashboards |
| `dineos-mailhog` | 1 | Email capture UI |
| `dineos-uptime-kuma` | 1 | Uptime monitoring |

### Seed Test Users (Keycloak)

| Email | Password | Role | Tenant |
|-------|----------|------|--------|
| `admin@dineos.dev` | `Test1234!` | SuperAdmin | None (platform) |
| `manager@dineos.dev` | `Test1234!` | Manager | Demo Restaurant (TenantId=1) |
| `cashier@dineos.dev` | `Test1234!` | Cashier | Demo Restaurant (TenantId=1) |
| `kitchen@dineos.dev` | `Test1234!` | KitchenStaff | Demo Restaurant (TenantId=1) |

### Demo Tenant (TenantId=1)

Pre-seeded data on the shared demo restaurant:
- **Restaurant:** "Demo Restaurant", city Tirana, plan Pro
- **10 menu items** across 3 categories (Starters, Mains, Drinks)
- **7 tables** (Main Hall)
- **5 staff members** (2 cashiers, 2 kitchen, 1 manager)
- **3 historical orders** (for report data)

---

## 13. Demo Flow

### Option A — Log in as seed Manager

1. Go to `https://app.project-06.gjirafa.dev/login`
2. Email: `manager@dineos.dev` / Password: `Test1234!`
3. Full restaurant dashboard — orders, kitchen, reports, menu management

### Option B — Request a Demo Account

1. Go to `https://app.project-06.gjirafa.dev/demo`
2. Enter your email, accept terms
3. Receive credentials by email (rotated password, valid 7 days)
4. Log in — see the same Demo Restaurant with all data

### What to show in a demo

| Feature | Steps |
|---------|-------|
| **Order lifecycle** | Dashboard → Orders → Create Order (dine-in, Table 1, add items) → Kitchen board updates in real-time → Mark ready → Process payment → Reports update |
| **Menu management** | Menu → Add item → Upload image → Edit price → Categories |
| **Kitchen board** | Open `/kitchen` in a second tab — watch orders appear as cashier creates them |
| **Reports** | Reports → Sales/Orders/Staff — charts with real data |
| **Admin portal** | Log in as `admin@dineos.dev` → Admin dashboard → Restaurant list → Create new restaurant |
| **Real-time** | Open two browser windows — cashier creates order in one, kitchen sees it instantly in the other |

---

## 14. Known Limitations

| Item | Status | Impact |
|------|--------|--------|
| **AI menu description** (`Anthropic__ApiKey` empty) | Config gap | `POST /ai/menu-items/{id}/describe` returns 422. Set the key to enable. |
| **Staff endpoint 500 for SuperAdmin** | Code fix pushed (deploying via CI) | SuperAdmin `GET /staff` throws 500 until new image rolls out (~5 min). Tenant-scoped users (Manager, etc.) are unaffected. |
| **Stripe billing not fully wired** | In progress | Checkout session creation works; actual in-restaurant payment via Stripe not yet connected (cash/card recorded manually). |
| **Staff PIN auth Phase 2** | In progress | PIN selection UI exists; backend fully implemented; Keycloak composite role graph now correctly allows Demo users full Owner access. |
| **Demo user needs fresh login** | User action | If you used the demo credentials earlier today, log out and back in to get a token with the new composite roles. |
| **Image upload — local filesystem** | By design for now | Menu item images stored on pod filesystem (PVC-backed). Not on object storage (S3/GCS) yet — images would be lost if PVC is deleted. |
| **ELK stack** | Optional profile | Elasticsearch/Kibana/Logstash deployed but not actively routing logs in this cluster (Loki+Grafana used instead). |

---

## Quick Reference

```
# Check cluster health
kubectl --kubeconfig=project-06.kubeconfig get pods -n project-06

# Follow API logs (live)
kubectl --kubeconfig=project-06.kubeconfig logs -f deployment/dineos-api -n project-06

# Check API rollout status
kubectl --kubeconfig=project-06.kubeconfig rollout status deployment/dineos-api -n project-06

# Get Keycloak admin password
kubectl --kubeconfig=project-06.kubeconfig get secret dineos-keycloak-admin -n project-06 \
  -o jsonpath='{.data.password}' | base64 -d

# Heal composite roles (if realm re-imported fresh)
KC_BASE=http://localhost:8090 KC_ADMIN_PASSWORD=<pwd> \
  bash backend/scripts/heal-composite-roles.sh
```

---

*Generated: 2026-06-10 | Audited by Claude Code | dineOS v1.x*
