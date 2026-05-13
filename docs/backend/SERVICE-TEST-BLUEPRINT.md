# dineOS Backend — Service Test Blueprint

End-to-end verification checklist for every infrastructure service the API depends on. Run top to bottom; each section is independent but later checks assume the earlier services are healthy.

## 0. Prerequisites

```bash
cd backend
docker compose up -d        # postgres, keycloak, redis, rabbitmq, loki, mailhog, grafana
dotnet run --project src/DineOS.Api      # or `dotnet watch run`
```

| Service        | Port  | URL                                          | Default creds          |
| -------------- | ----- | -------------------------------------------- | ---------------------- |
| API            | 5000  | http://localhost:5000/swagger                | bearer JWT             |
| Keycloak       | 8080  | http://localhost:8080                        | admin / admin          |
| Postgres       | 5432  | -                                            | dineos / dineos_dev    |
| Redis          | 6379  | -                                            | none                   |
| RabbitMQ mgmt  | 15672 | http://localhost:15672                       | dineos / dineos_dev    |
| MailHog UI     | 8025  | http://localhost:8025                        | none                   |
| Grafana        | 4000  | http://localhost:4000                        | admin / admin          |
| Hangfire       | 5000  | http://localhost:5000/hangfire               | dev: anonymous         |
| pgAdmin (opt)  | 5050  | http://localhost:5050                        | admin@dineos.dev/admin |

> **Postman:** import `docs/backend/postman/dineOS.postman_collection.json` and the matching environment. Run **Auth → Login** first — it stores `accessToken` and `refreshToken` into the env.

---

## 1. API process is alive

```bash
curl -s http://localhost:5000/api/v1/health | jq
```
**Expect:** `200`, `{"success":true,"data":{"status":"Healthy",...}}`. Any non-200 means the API didn't start — check `dotnet run` console for missing config or DB migration errors.

---

## 2. Postgres + EF Core migrations

```bash
docker exec -it dineos-postgres psql -U dineos -d dineos -c "\dt"
```
**Expect:** at least these tables — `MenuItems`, `MenuCategories`, `Orders`, `OrderItems`, `Payments`, `StaffMembers`, `Shifts`, `ShiftNotes`, `Restaurants`, `RestaurantTables`, `EmailVerificationCodes`, `DeadLetterEmails`, `ProcessedMessages`, `__EFMigrationsHistory`.

If empty, the API didn't reach `db.Database.Migrate()` — check `appsettings.Development.json` `ConnectionStrings:DefaultConnection`.

---

## 3. Keycloak (auth)

1. Open http://localhost:8080 → **Administration Console** → log in (admin/admin).
2. Realm dropdown should show **dineos**. Realm config came from `backend/keycloak/realm-export.json`.
3. **Clients** → `dineos-frontend` must exist with **Direct Access Grants Enabled = ON**.
4. **Users** → at least one user with realm-role `SuperAdmin` (or `Manager` / `Cashier` / `KitchenStaff`) and `tenant_id` attribute set on the user.

**Live token check from Postman:** run **Auth → POST /auth/login**. Expect `200` with `data.accessToken`. Then **Auth → GET /me** should return your username, email, and roles array.

If 401: token aud/issuer mismatch. Check `Keycloak__Audience=dineos-api` and `Keycloak__Authority=http://localhost:8080/realms/dineos` in `docker-compose.yml`.

---

## 4. RBAC (endpoint protection)

Run these in Postman with three different logins (SuperAdmin / Manager / Cashier):

| Endpoint                                | SuperAdmin | Manager | Cashier | KitchenStaff |
| --------------------------------------- | ---------- | ------- | ------- | ------------ |
| `GET /admin/users`                      | 200        | 403     | 403     | 403          |
| `GET /admin/restaurants`                | 200        | 403     | 403     | 403          |
| `GET /restaurant`                       | 200        | 200     | 403     | 403          |
| `GET /menu/items`                       | 200        | 200     | 200     | 200          |
| `POST /menu/items`                      | 201        | 201     | 403     | 403          |
| `GET /orders`                           | 200        | 200     | 200     | 403          |
| `POST /payments`                        | 201/422    | 201/422 | 201/422 | 403          |
| `GET /kitchen/orders`                   | 403        | 403     | 403     | 200          |
| `GET /reports/sales`                    | 200        | 200     | 403     | 403          |
| `POST /ai/menu-items/{id}/describe`     | 200/429    | 200/429 | 403     | 403          |

Also: `GET /health` is anonymous → must return 200 without `Authorization`.

---

## 5. Tenant isolation

1. Log in as Tenant A. `POST /menu/items` to create item *X*.
2. `GET /menu/items` — *X* present.
3. Log in as Tenant B. `GET /menu/items` — *X* **must not appear**.
4. From Tenant B: `PUT /menu/items/{X.id}` → expect **404** (not 403 — query filter hides it).
5. Negative test: add header `X-Tenant-ID: 9999` to a Tenant A request. Expect **403 / token mismatch** (middleware rejects when JWT `tenant_id` ≠ header).

---

## 6. Validation (FluentValidation)

| Test                                                            | Expect                                                |
| --------------------------------------------------------------- | ----------------------------------------------------- |
| `POST /auth/login` with empty body                              | 400, `errors:["Username is required.","Password..."]` |
| `POST /menu/items` with `price: 0`                              | 400, `errors:["Price must be greater than zero."]`    |
| `POST /orders` with `orderType:"dine-in"` and no `tableNumber`  | 400, `TableNumber is required for dine-in orders.`    |
| `POST /staff` with `pin:"12"`                                   | 400, `Pin must be exactly 4 digits.`                  |
| `POST /payments` with `method:"BTC"`                            | 400, `Method must be Cash or Card.`                   |

Every error response must follow the **ApiResponse envelope**: `{success:false, error, errors[], traceId}`.

---

## 7. Order → Payment flow (golden path)

1. Manager: `POST /menu/items` Margherita @ 7.50.
2. Cashier: `POST /orders` dine-in, table 5, items=[Margherita ×1]. Capture `id` and `total`.
3. Cashier: `GET /payments/open-orders` — order is in the list.
4. Cashier: `POST /payments` `{orderId, amount: total, method: "Card"}` — expect **201**.
5. Cashier: `GET /orders/{id}` — `status: "Delivered"`.
6. Replay step 4 — expect **422** (order no longer payable).
7. Mismatch test: `POST /payments` with `amount: total + 1` against a fresh order → **422**.

---

## 8. RabbitMQ — order-created event flow (M3.8)

1. http://localhost:15672 → **Queues** tab. Look for `orders.order-created` and dead-letter siblings.
2. Trigger a new order (step 7.2 above). In the consumer log (`dotnet run` console) expect:
   `Order {id} created broadcast via SignalR …`
3. RabbitMQ UI → queue's **Get Messages** should show the consumer drained it.
4. Idempotency: confirm `ProcessedMessages` row was inserted (`SELECT * FROM "ProcessedMessages" ORDER BY "Id" DESC LIMIT 5;`). Re-publish same `MessageId` → no duplicate broadcast.
5. **Kill RabbitMQ:** `docker stop dineos-rabbitmq`. Create an order. API still returns **201** but no SignalR broadcast happens (known behaviour). `docker start dineos-rabbitmq` and confirm next orders flow again.

---

## 9. Hangfire — background jobs (M3.7)

1. Open http://localhost:5000/hangfire.
2. **Jobs → Recurring** should list `daily-payment-summary` and `overdue-payment-notification`.
3. Trigger an account-verification email manually: SuperAdmin → `POST /admin/restaurants/{tenantId}/email-verification/resend`. Expect **202** with `JobId=#`.
4. **Jobs → Succeeded** should show the new job within a few seconds.
5. MailHog UI (http://localhost:8025) should display the verification email.
6. DLQ test: in `appsettings.Development.json` set `Email:SimulateFailure=true`, restart API, enqueue again. After retries the job moves to **Failed**, then a `DeadLetterEmail` row appears (`SELECT * FROM "DeadLetterEmails";`).

---

## 10. Email verification (owner flow)

1. From SuperAdmin POST resend (step 9.3). Open the mail in MailHog and copy the 6-digit code.
2. `POST /admin/restaurants/{tenantId}/email-verification/confirm` `{ "code": "123456" }`.
3. Expect **200** `{success: true, data: true}` on first try.
4. Wrong code → **400** with generic message (no enumeration). Three wrong attempts within window → row's `FailedAttempts` increments; subsequent `confirm` is rate-limited.
5. Re-use confirmed code → **400**.

---

## 11. Email subsystem (MailHog)

* http://localhost:8025 should show every email the API has sent.
* Plain SMTP test from container shell:
  ```bash
  docker exec -it dineos-api curl smtp://mailhog:1025 -v
  ```
* If empty: confirm `Smtp__Host=mailhog`, `Email__Enabled=true` in `docker-compose.yml`.

---

## 12. AI menu description (M3.10)

1. Provide an Anthropic key — `export Anthropic__ApiKey=sk-...` before `dotnet run`. (Docker-compose passes through env.)
2. Manager: `POST /ai/menu-items/{id}/describe` for a real menu item id.
3. Expect **200** with `data.description` and `data.allergens[]`.
4. Run 4+ times within a minute on the same tenant → at least one **429** (the `ai-expensive` limiter is intentionally tight).
5. Drop the key → expect **422** with a clear message (not 500).

---

## 13. Image upload (M3 file storage)

```bash
curl -X POST http://localhost:5000/api/v1/menu/items/1/image \
  -H "Authorization: Bearer $TOKEN" \
  -F "image=@./photo.png;type=image/png"
```
- Expect **200**, response `data.imageUrl` like `/uploads/menu-items/<uuid>.png`.
- Upload `.exe` → **400** `UNSUPPORTED_CONTENT_TYPE`.
- Upload 6 MB file → **400** `FILE_TOO_LARGE`.
- Upload `.png` declared as `image/jpeg` → **400** `EXTENSION_MISMATCH`.

Verify on disk: `docker exec dineos-api ls /app/uploads/menu-items/`.

---

## 14. SignalR (live order updates)

Open browser console at the frontend and connect to `/hubs/orders` with the same JWT. `POST /orders` from another session — the open hub should receive an `orderCreated` event within a second.

Without a frontend you can use [`signalr-cli`](https://github.com/Microsoft/SignalR-Client-Cpp) or `curl --no-buffer http://localhost:5000/hubs/orders/negotiate` to confirm the hub is mapped.

---

## 15. Rate limiting

* Public limiter: hit `GET /health` 70× in 60s → **429** after the 60th.
* Authenticated limiter: same with `GET /me`.
* AI limiter: see step 12.4.
* Every 429 response must include `Retry-After` header.

---

## 16. Logs and traces (Loki / Grafana)

1. Open http://localhost:4000 → **Explore → Loki**.
2. Query: `{application="DineOS.Api"} |= "OrderService"` after creating an order.
3. Every request log line carries `traceId` and `tenantId`. The `traceId` value should match `traceId` on the corresponding API response envelope.

---

## 17. Automated test suites

```bash
cd backend
dotnet test DineOS.slnx -c Release                 # fast unit + integration (Docker required for Testcontainers)
dotnet test DineOS.slnx -c Release \
  --filter "Category=LiveAuth" \
  --settings tests/DineOS.Tests/live.runsettings   # M3.11 live Keycloak suite
```

**Pass criterion:** `Test Run Successful. Total tests: 205   Passed: 205`. Coverage report is generated under `tests/DineOS.Tests/coverage/`.

---

## 18. Smoke tear-down checklist

Before shutting work down for the day:

- [ ] `dotnet test` is green.
- [ ] `POST /orders` end-to-end completes (steps 7 + 8).
- [ ] MailHog shows at least one email this session.
- [ ] Hangfire dashboard has zero **Failed** jobs.
- [ ] `SELECT count(*) FROM "DeadLetterEmails" WHERE "ResolvedAt" IS NULL;` == 0.
