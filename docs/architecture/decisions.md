# dineOS — Architecture Decision Register

This is the "every decision justified" half of the architecture deliverable. Each
row is a deliberate choice with its rationale, the main alternative that was
rejected, and the trade-off accepted. It pairs with the diagrams in
**[README.md](README.md)**.

> Format is intentionally lightweight (a decision table rather than one ADR file
> per decision) so it stays in sync with the diagrams in the same folder. Decisions
> are grouped by tier.

---

## Application architecture

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 1 | **Clean Architecture** (`Api → Application → Infrastructure → Domain`) | Enforces an inward dependency rule; domain logic is testable without a database or web host; infrastructure is swappable behind ports | Single-project / transaction-script style | More projects and boilerplate; indirection through interfaces |
| 2 | **.NET 10 / ASP.NET Core** for the API | Team expertise; first-class DI, middleware, EF Core, SignalR, OpenAPI; strong performance and long-term support | Node/NestJS, Spring Boot | C#/.NET operational footprint; Alpine runtime tuning |
| 3 | **Next.js 16 (App Router) + React 19** | One React stack for SSR + client; server-side `/api` proxy gives a single public origin; RSC + standalone output for small images | SPA + separate BFF; plain React + Vite | Framework lock-in; App Router learning curve |
| 4 | **TypeScript everywhere on the frontend** | Compile-time safety across components, API clients, and Zod schemas | Plain JavaScript | Build/type-check step |
| 5 | **TanStack Query + Zustand split** | Query owns server cache (fetch/invalidate/retry); Zustand owns light client state — avoids one store doing both | Redux Toolkit; Context only | Two state tools to learn |
| 6 | **React Hook Form + Zod** | Shared schema validates forms on the client and mirrors backend FluentValidation rules | Formik; manual validation | Extra schema layer to maintain |

## Identity, auth & multi-tenancy

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 7 | **Keycloak as OIDC identity provider** | Standards-based OIDC/OAuth2, realm-based RBAC, PKCE, token lifecycles out of the box; self-hostable for a school cluster | Auth0/Okta (cost, external dependency); hand-rolled JWT auth (security risk) | An extra stateful service to operate |
| 8 | **JWT Bearer validation + password-grant login via the backend** | Browser never talks to Keycloak directly in the deployed demo; API validates every request against Keycloak metadata; simplifies CORS/redirects | Browser-side Authorization-Code flow | Backend handles credentials on login; relies on HTTPS |
| 9 | **Policy-based RBAC** (`SuperAdmin` / `Manager` / `Cashier` / `KitchenStaff`) with a fallback "authenticated" policy | Centralized, named policies; deny-by-default via `FallbackPolicy.RequireAuthenticatedUser()` | Per-controller attribute strings (magic strings) | Policies must be kept in sync with Keycloak roles |
| 10 | **Per-request tenant isolation middleware** | Multi-tenant data separation enforced in one place; `TenantId` enriched into logs | Tenant filter scattered per query | Every data path must respect the ambient tenant |
| 11 | **Tiered rate limiting** (public 60/min, authenticated 300/min, AI 10/min, email-verify per-IP) | Protects the API and bounds LLM cost per tenant; anonymous flows partitioned by IP | One global limit | Tuning four windows |

## Data & messaging

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 12 | **PostgreSQL** as primary store | Mature relational DB; strong EF Core/Npgsql support; also backs Hangfire jobs | MySQL; a document DB | Relational modelling discipline |
| 13 | **EF Core 10 + auto-migrate on startup** | Code-first migrations applied automatically on boot — no manual migration step in the demo | Dapper/raw SQL; manual migration gate | Startup applies schema changes (acceptable for this project's deploy model) |
| 14 | **Redis** for refresh-token blacklist **and** SignalR backplane | One low-latency store serves both token revocation and multi-replica realtime fan-out | In-memory only (breaks with >1 replica) | Another stateful dependency |
| 15 | **RabbitMQ** for order/event notifications | Decouples order creation from kitchen-display notification; durable queues | Kafka (operationally heavy for this scale); in-process events (no decoupling) | Broker to run and monitor |
| 16 | **SignalR over WebSocket** for realtime | Native ASP.NET Core integration; token passed via `?access_token=` since browsers can't set WS auth headers | Polling; raw WebSockets | Sticky considerations solved via Redis backplane |
| 17 | **Hangfire (Postgres-backed)** for background jobs | Reuses the existing database; dashboard for visibility; SuperAdmin-gated in prod | Quartz.NET; hosted `BackgroundService` only | Job tables in the app database |

## External integrations

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 18 | **Stripe** for billing + webhook-driven tenant provisioning | Standard payments; webhooks provision the owner tenant regardless of event order | Building billing in-house | External dependency; webhook reliability handling |
| 19 | **MailKit + RazorLight** for transactional email | MailKit is the de-facto .NET SMTP client; RazorLight renders templated HTML | `SmtpClient` (deprecated); a SaaS mail API | SMTP config per environment |
| 20 | **Pluggable AI providers** (Anthropic / OpenAI / Google AI) | Optional product AI features + AIOps triage; degrade gracefully with no key; tight rate limit bounds cost | Hard-coding one provider | Abstraction over differing provider APIs |

## Observability

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 21 | **Prometheus + Alertmanager** for metrics & alerting | Pull-based scraping of `prometheus-net` `/metrics`; rule-based alerts with inhibition; Slack routing | Hosted APM (cost) | Self-managed scrape/rules |
| 22 | **Loki + ELK side by side** (locally) | Loki for fast label-based log queries in Grafana; ELK for full-text search, Nginx access analytics, geo/UA breakdowns — complementary, independently toggleable | One logging stack only | Two log pipelines locally; **Loki omitted in project-06** to save cluster resources |
| 23 | **Grafana** as the single dashboard pane | Queries Prometheus (PromQL) and Loki (LogQL) together | Separate metric/log UIs | Datasource provisioning |
| 24 | **Uptime-Kuma** with public status page | Lightweight external uptime + a `status.project-06.gjirafa.dev` page; Longhorn PVC so state survives restarts | Hosted status page | Another small service |
| 25 | **Correlation IDs end-to-end** | Every request gets `X-Correlation-ID`; enriched into Serilog (TenantId/UserId) for log correlation | No request tracing | Middleware on every request |

## Platform, delivery & security

| # | Decision | Why chosen | Alternative rejected | Trade-off accepted |
|---|---|---|---|---|
| 26 | **Docker Compose for local, Helm/Kubernetes for deploy** | Compose gives one-command full stack for devs; Helm parametrizes the same app for the cluster with per-env values files | Same tool for both (compose in prod / k8s locally) | Two topologies to keep aligned (documented in README) |
| 27 | **Multi-stage Dockerfiles, non-root users, Next.js standalone output** | Smaller, hardened images; processes never run as root | Single-stage images | More complex Dockerfiles |
| 28 | **GitHub Actions + GHCR, auto-deploy from `main`** | Six workflows (CI per stack, Helm lint, build-push, commitlint, release-please); every merge to `main` builds and `helm upgrade --atomic` deploys to project-06 | Manual deploys; external CI | Pipeline maintenance; `production` environment gate |
| 29 | **Trivy scanning** (fs on PRs, image on build) | Catches CRITICAL/HIGH vulnerabilities before deploy; blocks the deploy job on findings | No scanning | Occasional false positives (handled via `.trivyignore`) |
| 30 | **cert-manager + Let's Encrypt at the Ingress** | Automated TLS for `app.project-06` and `status.project-06`; long proxy timeouts keep SignalR WebSockets alive | Manual certs; TLS termination in-app | Depends on cluster ingress + DNS |
| 31 | **In-cluster backing services, but chart supports external managed** | Simplest for a school cluster with no managed cloud services; `dependencies.<name>.enabled=false` + `host` switches to managed without template changes | Hard-wiring in-cluster only | In-cluster DB/broker aren't HA in the demo |
| 32 | **Conventional Commits + Release Please** | Automated semver, changelog, and tagged GitHub releases from commit history | Manual versioning | Commit-message discipline (enforced by commitlint) |

---

## Open / deferred decisions

These are known and intentional, recorded so they aren't mistaken for oversights:

- **Loki in Kubernetes** — deployed locally but not in the Helm chart; project-06 relies on ELK. Adding a Loki template is deferred.
- **Standalone Grafana** — lives in `deploy/grafana-standalone.yaml` outside the chart; folding it into the chart is deferred.
- **`ASPNETCORE_ENVIRONMENT=Development` in project-06** — chosen for demo ergonomics (Swagger, anonymous Hangfire dashboard). A real production deploy would switch to `Production` and harden accordingly.
- **No HA for in-cluster Postgres/Redis/RabbitMQ** — acceptable for a demo cluster; production would use managed/replicated services via the chart's external-service flags.
