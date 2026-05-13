# Implementation guide — #156 – #177

Step-by-step "how to actually build it" for every ticket in the Milestone 4 backlog. Read this alongside `EXECUTION-FLOW.md` (which dictates order) and `FRONTEND-INTEGRATION-PLAN.md` (which lists the endpoint map). The bare minimum recipe for any task is:

1. Read the controller + DTO in `backend/src/DineOS.Api/Controllers/` and `backend/src/DineOS.Application/`.
2. Mirror DTOs to TypeScript in `frontend/src/types/<feature>.ts`.
3. Add API calls to `frontend/src/lib/api/<feature>Api.ts` (axios via `apiClient`, never direct).
4. Add query keys to `frontend/src/lib/api/queryKeys.ts`.
5. Add Zod schemas to `frontend/src/lib/validations/<feature>.ts` if there's a form.
6. Wire the page with TanStack Query; ensure `loading.tsx` + `error.tsx` exist.
7. Verify with the Postman collection folder; only then open the PR.

---

## Phase 0 — Backend safety net

### #172 — Fix `AnthropicOptions.Model` default
**Files:** `backend/src/DineOS.Application/Options/AnthropicOptions.cs`, `backend/src/DineOS.Api/appsettings.json`, `backend/src/DineOS.Api/Program.cs`.

1. Change the default literal at line 11 from `"claude-sonnet-4-6"` to `"claude-sonnet-4-5"`.
2. In `Program.cs`, after `builder.Services.Configure<AnthropicOptions>(...)`, add a validation step:
   ```csharp
   builder.Services.AddOptions<AnthropicOptions>()
       .Bind(builder.Configuration.GetSection("Anthropic"))
       .Validate(o => !string.IsNullOrWhiteSpace(o.Model), "Anthropic:Model is required.")
       .ValidateOnStart();
   ```
3. Update `appsettings.json` to document the env-var override (`Anthropic__Model`).
4. Run `dotnet test --filter "FullyQualifiedName~AnthropicAiClient"` — should stay green.

**Done when:** API starts cleanly with no override; `POST /ai/menu-items/{id}/describe` returns 200 against a valid Anthropic key.

---

### #173 — Decide auth gate on `EmailVerificationController.Confirm`
**Files:** `backend/src/DineOS.Api/Controllers/EmailVerificationController.cs`, integration test in `backend/tests/DineOS.Tests/Integration/`.

1. Open the controller; you'll see `[Authorize(Policy = "SuperAdminOnly")]` at class level and both `resend` + `confirm` inherit it.
2. Pick an option (record the decision in the issue comment):
   - **Option A (owner self-confirm, recommended):** drop the class-level attribute, keep `SuperAdminOnly` on `resend`, set `confirm` to `[AllowAnonymous]` and apply `[EnableRateLimiting("public")]`. The 6-digit code is already a server-side secret.
   - **Option B (SuperAdmin-mediated):** rename the action `ConfirmOnBehalfOfOwner`, keep the gate, update docs.
3. Add an integration test in `backend/tests/DineOS.Tests/Integration/EmailVerificationAuthTests.cs` asserting the chosen status code matrix (`anon → 200/400`, `manager → 200/400` etc., or `anon → 401`).
4. Update `docs/backend/endpoint-protection-audit-m312.md` to reflect the decision.

**Done when:** the FE-403 ticket can be implemented with a clear auth contract.

---

### #174 — RabbitMQ publish fallback in `OrderService`
**Files:** `backend/src/DineOS.Infrastructure/Services/OrderService.cs`, `backend/src/DineOS.Infrastructure/Messaging/RabbitMqMessagePublisher.cs`, new hosted service.

1. In `CreateOrderAsync`, after the existing `try/catch` around the publish, add an `else` path that invokes the in-process broadcaster when (a) publish threw or (b) `RabbitMq:Enabled=false`:
   ```csharp
   var published = await _publisher.TryPublishAsync(message, ct);
   if (!published)
       await _notificationService.BroadcastOrderCreatedAsync(order, ct);
   ```
   Change `IMessagePublisher.PublishAsync` to `TryPublishAsync` returning `bool`; log a warning on `false`.
2. Move topology declaration out of `RabbitMqMessagePublisher.PublishAsync`. Create `RabbitMqTopologyHostedService : IHostedService` that runs `RabbitMqTopology.DeclareAsync` once on `StartAsync`. Register it from `AddInfrastructure()` only when `RabbitMq:Enabled=true`.
3. Add an integration test in `backend/tests/DineOS.Tests/Unit/RabbitMqOrderEventFlowTests.cs` that wires `RabbitMq:Enabled=false` and asserts `BroadcastOrderCreatedAsync` ran exactly once.

**Done when:** stopping the broker (`docker stop dineos-rabbitmq`) and creating an order still pushes a SignalR event; the log line `"RabbitMQ publish failed, falling back to in-process broadcast"` appears.

---

### #175 — Role + policy constants
**Files:** new `backend/src/DineOS.Application/Authorization/Roles.cs` and `Policies.cs`; 14 controllers; `backend/src/DineOS.Api/Program.cs`; `backend/src/DineOS.Api/Middleware/TenantIsolationMiddleware.cs`; `backend/src/DineOS.Api/Filters/SuperAdminDashboardAuthorizationFilter.cs`; two `StaffMember` validators.

1. Create the two static classes (see the issue body for the exact shape).
2. Find-and-replace every literal:
   ```bash
   rg -l '"SuperAdmin"|"Manager"|"Cashier"|"KitchenStaff"' backend/src
   rg -l '"SuperAdminOnly"|"ManagerAndAbove"|"CashierAndAbove"|"KitchenStaffOnly"' backend/src
   ```
   Replace each hit with `Roles.X` / `Policies.X` (add the `using DineOS.Application.Authorization;` import).
3. Update `Program.cs` policy registration to consume `Policies.SuperAdminOnly`, etc.
4. Add a test in `backend/tests/DineOS.Tests/Unit/RoleConstantsTests.cs` that reflects over every `[Authorize(Policy = ...)]` attribute and asserts the policy name exists on `typeof(Policies)`.
5. Re-run `dotnet test`.

**Done when:** no magic-string role/policy literal survives outside the two new classes (rg check returns nothing in `backend/src/DineOS.Api/`).

---

### #176 — `IDatabaseMigrator`
**Files:** new `backend/src/DineOS.Infrastructure/Persistence/IDatabaseMigrator.cs` + `EfDatabaseMigrator.cs`; `backend/src/DineOS.Infrastructure/DependencyInjection.cs`; `backend/src/DineOS.Api/Program.cs`; `backend/src/DineOS.Api/DineOS.Api.csproj`; `backend/src/DineOS.Infrastructure/DineOS.Infrastructure.csproj`.

1. Define the interface in `DineOS.Application` (so Api can resolve it without Infrastructure import):
   ```csharp
   public interface IDatabaseMigrator { Task MigrateAsync(CancellationToken ct = default); }
   ```
2. Implement `EfDatabaseMigrator` in `DineOS.Infrastructure/Persistence/` against `AppDbContext`.
3. Register in `AddInfrastructure()`: `services.AddScoped<IDatabaseMigrator, EfDatabaseMigrator>();`
4. In `Program.cs`, replace lines 11–12 + 318–319 with:
   ```csharp
   using (var scope = app.Services.CreateScope())
   {
       var migrator = scope.ServiceProvider.GetRequiredService<IDatabaseMigrator>();
       await migrator.MigrateAsync();
   }
   ```
   Remove the `using DineOS.Infrastructure.Persistence;` import.
5. Move `Microsoft.EntityFrameworkCore.Design` PackageReference from `DineOS.Api.csproj` to `DineOS.Infrastructure.csproj`.
6. Confirm `dotnet ef` still works locally with `--project src/DineOS.Infrastructure --startup-project src/DineOS.Api`.

**Done when:** Api has no `using DineOS.Infrastructure.*` outside DI registration.

---

## Phase 1 — Auth foundation (sequential)

### #156 — Backend login
**Files:** `frontend/src/lib/auth/keycloak.ts` (replace) → new `frontend/src/lib/api/authApi.ts`; `frontend/src/stores/authStore.ts`; `frontend/src/app/login/page.tsx`.

1. Delete the direct Keycloak token-endpoint call. Create `authApi.ts`:
   ```ts
   export async function login(req: LoginRequest): Promise<RefreshTokenResponse> {
     const res = await apiClient.post<ApiResponse<RefreshTokenResponse>>("/v1/auth/login", req);
     if (!res.data.success || !res.data.data) throw new ApiError(res.data, res.status);
     return res.data.data;
   }
   ```
2. Define matching TS types in `frontend/src/types/auth.ts` (`LoginRequest`, `RefreshTokenResponse`, `ApiResponse`).
3. In `useAuthStore.login()`, call `authApi.login(...)`, then:
   - Persist `accessToken` and `refreshToken` cookies (use existing helpers).
   - Decode JWT once to extract `tenantId` and `roles`; store both in Zustand.
4. Update `/login` page to call `useAuthStore.login()` (keep RHF + Zod; the schema is the same `{ username, password }`).
5. Handle error envelope status codes — `401`: "Invalid credentials"; `400`: show `errors[]` under each field; `503`: "Identity provider unavailable"; `429`: rate-limit toast.
6. Remove dev-login bypass if no longer needed (or guard with `NEXT_PUBLIC_DEV_AUTH=1`).

**Test:** Postman → **Auth → Login** with valid + invalid creds; same flow via the UI.

---

### #157 — Refresh-on-401 interceptor
**Files:** `frontend/src/lib/api/apiClient.ts`, new `frontend/src/lib/auth/refresh.ts`.

1. Add a singleton in-flight promise so concurrent 401s share one refresh call:
   ```ts
   let refreshing: Promise<string | null> | null = null;
   async function refreshOnce(): Promise<string | null> {
     refreshing ??= (async () => {
       try {
         const { accessToken, refreshToken } = await authApi.refresh(getCookie("refresh_token") ?? "");
         setCookie("access_token", accessToken);
         setCookie("refresh_token", refreshToken);
         return accessToken;
       } catch { return null; }
       finally { refreshing = null; }
     })();
     return refreshing;
   }
   ```
2. Add the response interceptor in `apiClient.ts`:
   ```ts
   apiClient.interceptors.response.use(undefined, async (err: AxiosError) => {
     const original = err.config as InternalAxiosRequestConfig & { _retry?: boolean };
     if (err.response?.status !== 401 || original._retry) throw err;
     original._retry = true;
     const newToken = await refreshOnce();
     if (!newToken) { useAuthStore.getState().logout(); throw err; }
     original.headers.Authorization = `Bearer ${newToken}`;
     return apiClient(original);
   });
   ```
3. Unit test: use `axios-mock-adapter` to assert the original request retries on a 401→200 chain.

**Done when:** opening the app with an expired access token silently refreshes and the user never sees a 401 screen.

---

### #158 — Logout
**Files:** `frontend/src/lib/api/authApi.ts`; `frontend/src/stores/authStore.ts`; the header/nav `<LogoutButton />`.

1. Add `authApi.logout(refreshToken: string)` that POSTs to `/v1/auth/logout`. Always treat the request as fire-and-forget (try/catch with no rethrow) — the user must still log out client-side.
2. In `useAuthStore.logout()`, call `authApi.logout(getCookie("refresh_token"))`, clear cookies, reset store, then `router.replace("/login")`.

**Test:** after Logout, Postman → **Auth → Refresh** with the previously-active refresh token returns 401.

---

### #159 — `useMe`
**Files:** new `frontend/src/lib/api/meApi.ts`; new `frontend/src/hooks/useMe.ts`; `frontend/src/lib/api/queryKeys.ts`; `frontend/src/hooks/useTenant.ts` (refactor).

1. `meApi.getMe()` calls `GET /v1/me`; type the response as `Me { id, email, username, name, roles }`.
2. `useMe()` returns `useQuery({ queryKey: queryKeys.me, queryFn: meApi.getMe, staleTime: Infinity })`.
3. Remove the JWT-decoder usage from `useTenant`; have it read `tenantId` from the auth store (set at login time) and pass through `useMe` for roles.
4. On logout, call `queryClient.removeQueries({ queryKey: queryKeys.me })`.

---

## Phase 2 — Operator pages

### #160 — Wire `/menu`
**Files:** `frontend/src/lib/api/menuApi.ts` (replace), `frontend/src/types/menu.ts`, `frontend/src/lib/api/queryKeys.ts`, `frontend/src/lib/validations/menuItem.ts` (already exists — verify), `/menu` page + modals.

1. Type DTOs to match C#: `MenuItemDto`, `MenuCategoryDto`, `CreateMenuItemRequest`, `UpdateMenuItemRequest`, `CreateMenuCategoryRequest`.
2. API module:
   ```ts
   export const menuApi = {
     listItems: () => unwrap(apiClient.get<ApiResponse<MenuItemDto[]>>("/v1/menu/items")),
     createItem: (r: CreateMenuItemRequest) => unwrap(apiClient.post(...)),
     updateItem: (id: number, r: UpdateMenuItemRequest) => unwrap(apiClient.put(`/v1/menu/items/${id}`, r)),
     deleteItem: (id: number) => unwrap(apiClient.delete(`/v1/menu/items/${id}`)),
     listCategories: () => unwrap(apiClient.get<ApiResponse<MenuCategoryDto[]>>("/v1/menu/categories")),
     createCategory: (r: CreateMenuCategoryRequest) => unwrap(apiClient.post("/v1/menu/categories", r)),
     uploadImage: (id: number, file: File) => {
       const form = new FormData(); form.append("image", file);
       return unwrap(apiClient.post<ApiResponse<{ imageUrl: string }>>(`/v1/menu/items/${id}/image`, form));
     },
   };
   ```
3. Add query keys: `menu.items`, `menu.categories`.
4. `useMenu()` hook returning items + categories; mutations invalidate `menu.items` after create/update/delete and `menu.categories` after category-create.
5. In the image dropzone, on `FILE_TOO_LARGE` / `UNSUPPORTED_CONTENT_TYPE` / etc. error codes, show the matching toast message.

---

### #161 — AI Describe button
**Files:** new `frontend/src/lib/api/aiApi.ts`; `frontend/src/components/menu/MenuCard.tsx` (or wherever the card lives); types.

1. `aiApi.describeMenuItem(id: number): Promise<{ description: string; allergens: string[] }>` calls `POST /v1/ai/menu-items/{id}/describe`.
2. Add a ✨ button visible only when `me.roles.includes("Manager") || me.roles.includes("SuperAdmin")`.
3. On click → `useMutation`; show a side panel / dropdown with the suggestion + editable textarea + Save.
4. Save calls `menuApi.updateItem(id, { ...existing, description })` and invalidates `menu.items`.
5. Handle 429 (rate limited) and 422 (AI unavailable) with distinct toast copy.

---

### #162 — Orders
**Files:** `frontend/src/lib/api/ordersApi.ts` (replace), `frontend/src/types/order.ts`, `frontend/src/hooks/useOrderBoard.ts` (refactor), `frontend/src/stores/orderWizardStore.ts` (existing), `frontend/src/lib/validations/order.ts` (existing), new route `frontend/src/app/(protected)/orders/[id]/{page,loading,error}.tsx`.

1. Mirror C# types — `OrderDto`, `OrderItemDto`, `CreateOrderRequest`, `CreateOrderItemRequest`, `UpdateOrderStatusRequest`.
2. API: `listOrders({ date?, status? })`, `getOrder(id)`, `createOrder(req)`, `updateStatus(id, status)`.
3. `useOrderBoard` now consumes `ordersApi.listOrders` (server-side filters; remove client-side date filtering).
4. New `/orders/[id]/page.tsx`: server component calls `ordersApi.getOrder(id)`; render items, totals, status timeline.
5. Validation: keep the existing `CreateOrderRequest` Zod schema; backend will reject any mismatch with a 400 envelope.

---

### #163 — Payments
**Files:** new `frontend/src/lib/api/paymentsApi.ts`, new `frontend/src/types/payment.ts`, `/payments` page.

1. Type `PaymentDto`, `ProcessPaymentRequest`. API: `getOpenOrders()`, `process(req)`.
2. Page flow:
   - Left: list from `getOpenOrders` (queryKey `payments.openOrders`).
   - Right: selected order detail with method picker (Cash | Card) and a read-only Amount (locked to the order total — backend rejects mismatches).
   - On Submit: `process({ orderId, amount, method })`.
3. Response mapping:
   - 201 → toast "Payment recorded"; invalidate `orders.all` + `payments.openOrders`.
   - 404 → toast "Order no longer available"; refetch list.
   - 422 → toast "Amount mismatch" or "Already settled" (use the envelope's `error` text).

---

### #164 — Kitchen board
**Files:** `frontend/src/lib/api/kitchenApi.ts` (new), `frontend/src/hooks/useKitchenBoard.ts` (refactor), `frontend/src/types/order.ts` (reuse).

1. API: `listOrders()`, `getQueue()`, `updateStatus(id, status)`.
2. `useKitchenBoard` returns `{ orders, queue, mutate: updateStatus }`.
3. After `updateStatus` succeeds, invalidate **`queryKeys.orders.all`** so the operator board, daily summary, and kitchen all refresh.

---

### #165 — Shifts + notes
**Files:** `frontend/src/lib/api/shiftApi.ts` (replace), new `frontend/src/lib/api/shiftNotesApi.ts`, `frontend/src/types/shift.ts`, `shiftNote.ts`, `/shifts` page.

1. API modules mirror `ShiftsController` and `ShiftNotesController`.
2. Calendar driven by `shiftApi.list({ date })`; create/edit modals call `create` / `update`.
3. Right-side notes panel: list (anyone authenticated), create + delete (Manager+ only — use `useMe().roles`).
4. Priority badge mapping: `Info` → neutral, `Warning` → amber, `Urgent` → red.

---

### #166 — Reports
**Files:** new `frontend/src/lib/api/reportsApi.ts`, `frontend/src/types/report.ts`, `/reports` page.

1. Three queries with their own keys: `reports.sales`, `reports.orders`, `reports.staff`.
2. ISO date pickers; serialize `from` + `to` as `YYYY-MM-DD`; default both empty so backend applies its last-30-days default.
3. Use existing chart components (no new chart library); pass through the DTOs unchanged.

---

## Phase 3 — Settings + SuperAdmin

### #167 — `/settings/profile` + `/settings/tables`
**Files:** new pages + loading/error triplets; new `restaurantProfileApi.ts`, `restaurantTablesApi.ts`; new Zod schemas in `validations/restaurantProfile.ts` and `restaurantTable.ts`; sidebar update.

1. `/settings/profile`:
   - RHF form bound to `UpdateRestaurantProfileRequest` (all fields nullable — server treats `null` as "no change").
   - On submit, `PUT /restaurant` and invalidate `restaurant.profile`.
2. `/settings/tables`:
   - Table list rendering `RestaurantTableDto[]`.
   - "Add table" modal with RHF + Zod (`number`, `capacity 1–50`, optional `location`).
   - Row inline edit calls `PUT /tables/{id}`; surface 409 ("table number already in use") as a field-level error.
3. Add a Settings nav link to `(protected)/layout.tsx` sidebar, gated by `Manager` or `SuperAdmin`.

---

### #168 — `/admin/users` + delete-restaurant modal
**Files:** `frontend/src/lib/api/adminApi.ts` (extend), `/admin/users` page, `/admin/restaurants/[id]` page.

1. `adminApi.listUsers({ search, page, pageSize })` returns `PagedResponse<PlatformUserDto>`. Use debounced search (300ms) via `useDebouncedValue`.
2. `/admin/users` table: columns Name, Email, Role, Restaurant, Status, Created.
3. On the existing restaurant detail page, add a "Delete restaurant" button → `<ConfirmModal>` with the restaurant name typed to confirm (matches existing destructive-action UX). On confirm, `DELETE /admin/restaurants/{id}` then `router.push("/admin/restaurants")`.

---

### #169 — Email verification UI
**Files:** `/admin/restaurants/[id]` (resend button), new `/admin/restaurants/[id]/verify-email/{page,loading,error}.tsx`, validation, `adminApi.ts`.

1. Add `adminApi.resendVerification(tenantId)` and `adminApi.confirmVerification(tenantId, code)`.
2. Resend button: optimistic UI ("Sending…"); on 202 → toast `"Verification email queued. JobId={data.jobId}"`.
3. Verify page: single 6-digit code input (Zod: `/^\d{6}$/`); submit calls `confirmVerification`.
4. Map status codes:
   - 200 + `data: true` → success view with "Back to restaurant" link.
   - 400 → field-level error.
   - 404 → "No pending verification" empty state.
   - 429 → cooldown toast.

---

## Phase 4 — Realtime + DX polish

### #170 — SignalR client
**Files:** new `frontend/src/lib/realtime/orderHub.ts`, `useOrderBoard`, `useKitchenBoard`.

1. Add `@microsoft/signalr` to `package.json` if missing.
2. Build a connection with `HubConnectionBuilder` against `${baseUrl}/hubs/orders`. Provide `accessTokenFactory` from `getCookie("access_token")`.
3. Subscribe to `OrderCreated` and `OrderStatusChanged`. Each handler calls `queryClient.invalidateQueries({ queryKey: queryKeys.orders.all })`.
4. Start the connection inside `useOrderBoard`/`useKitchenBoard` (via a single `useOrderHub()` hook). Stop on unmount; reconnect on `document.visibilitychange === "visible"`.
5. Reconnect policy: exponential backoff, max 30s.

---

### #171 — Envelope helper + error toasts
**Files:** new `frontend/src/lib/api/envelope.ts`, `frontend/src/lib/api/errorToast.ts`, `frontend/src/lib/queryClient.ts` (existing).

1. `unwrap<T>(promise: Promise<AxiosResponse<ApiResponse<T>>>): Promise<T>` — throws `ApiError` if `data.success === false`.
2. `ApiError` class: `{ status, error, errors?, traceId }`.
3. Refactor every `apiClient.x<{ data: T }>(...)` call to use `unwrap`.
4. `errorToast(err: ApiError)` switch on status:
   - 401 → "Session expired" + redirect to /login (only when the refresh interceptor already gave up).
   - 403 → "You don't have permission to do that."
   - 422 → show `error` text; if `errors` array present, list bullets.
   - 429 → "Slow down — try again in a moment."
   - Anything else → `error` text + (in dev) `traceId`.
5. Wire it into TanStack `QueryClient` default `onError` for both queries and mutations.

---

## #177 — Docs bundle (this PR)
**Files:** `docs/backend/postman/dineOS.postman_collection.json`, `dineOS.local.postman_environment.json`, `docs/backend/SERVICE-TEST-BLUEPRINT.md`, `docs/backend/FRONTEND-INTEGRATION-PLAN.md`, `docs/backend/EXECUTION-FLOW.md`, `docs/backend/IMPLEMENTATION-GUIDE.md` (this file), `docs/AI-Development-Log.md` (appended rows).

1. From `main`: `git checkout -b docs/backend-audit-postman-blueprint`.
2. `git add docs/backend/postman docs/backend/SERVICE-TEST-BLUEPRINT.md docs/backend/FRONTEND-INTEGRATION-PLAN.md docs/backend/EXECUTION-FLOW.md docs/backend/IMPLEMENTATION-GUIDE.md docs/AI-Development-Log.md`.
3. Commit: `docs(backend): add postman collection, service-test blueprint, frontend integration plan, execution flow + implementation guide`.
4. `git push -u origin docs/backend-audit-postman-blueprint`.
5. Open PR linking `Closes #177`.

---

## Cross-cutting conventions (apply to every FE ticket)

1. **Never call axios directly from a component.** Everything goes through `src/lib/api/`.
2. **No `useEffect` for data fetching.** TanStack `useQuery` / `useMutation` only.
3. **Types mirror C# DTOs 1:1.** Numeric ids stay numeric; never re-key a property; if the C# DTO uses `decimal`, the TS type is `number` (be aware of precision).
4. **Validation lives in Zod.** Backend FluentValidation is the source of truth; mirror its rules and let the server be the second line of defense.
5. **Every new `page.tsx` ships with `loading.tsx` + `error.tsx`** in the same folder (frontend CLAUDE.md rule).
6. **One PR per ticket.** PR title format: `feat(<area>): <imperative summary> (#NNN)`.
7. **Before requesting review:** run the matching folder in `docs/backend/postman/dineOS.postman_collection.json` against your local stack.
8. **Append the AI Development Log row after merge** (project CLAUDE.md mandate).

---

## Anti-patterns to avoid

- ❌ Mocking the API in a hook when `apiClient` is right there.
- ❌ Decoding the JWT in the browser more than once (Phase 1 #159 centralizes this).
- ❌ Hardcoding role strings in the UI — read from `useMe().roles`.
- ❌ Disabling a button until a refetch lands — use `mutation.isPending` instead.
- ❌ Catching errors silently inside an API module — let them propagate to TanStack Query so `errorToast` can react.
- ❌ Adding a new `<feature>Api.ts` without a matching `queryKeys.<feature>` entry.
