# Frontend ↔ Backend Integration Plan

Inventory of every backend endpoint mapped to the frontend page/feature that should consume it, with current wiring status and a prioritized task list. Source of truth for what to build next.

## Legend

- ✅ **Wired** — frontend talks to the real backend endpoint.
- 🟡 **Mocked** — page exists but uses static data or `// TODO: wire …` comments.
- ❌ **Missing** — no frontend page or component for this endpoint yet.

---

## Endpoint → Page Map

### Auth
| Endpoint                              | Frontend page / hook                       | Status |
| ------------------------------------- | ------------------------------------------ | ------ |
| `POST /auth/login`                    | `/login` (calls Keycloak directly today)   | 🟡 (uses Keycloak token endpoint, not `/auth/login`) |
| `POST /auth/refresh`                  | `apiClient` 401 interceptor                | ❌      |
| `POST /auth/logout`                   | Header logout button                       | ❌      |
| `GET /me`                             | `useTenant` / nav user chip                | 🟡      |

### Admin / SuperAdmin
| Endpoint                                                                 | Frontend page                                       | Status |
| ------------------------------------------------------------------------ | --------------------------------------------------- | ------ |
| `GET /admin/users`                                                       | `/admin/users`                                      | 🟡      |
| `GET /admin/restaurants`                                                 | `/admin/restaurants`                                | ✅      |
| `GET /admin/restaurants/:id`                                             | `/admin/restaurants/[id]`                           | ✅      |
| `POST /admin/restaurants`                                                | `/admin/restaurants/new`                            | ✅      |
| `PATCH /admin/restaurants/:id/status`                                    | `/admin/restaurants/[id]`                           | ✅      |
| `PATCH /admin/restaurants/:id/plan`                                      | `/admin/restaurants/[id]`                           | ✅      |
| `DELETE /admin/restaurants/:id`                                          | `/admin/restaurants/[id]`                           | 🟡 (UI button TBD) |
| `POST /admin/restaurants/:id/email-verification/resend`                  | `/admin/restaurants/[id]` (Resend button)           | ❌      |
| `POST /admin/restaurants/:id/email-verification/confirm`                 | `/admin/restaurants/[id]/verify` (new page)         | ❌      |

### Restaurant (Manager+)
| Endpoint                                | Frontend page                              | Status |
| --------------------------------------- | ------------------------------------------ | ------ |
| `GET /restaurant`                       | `/settings/profile` (new)                  | ❌      |
| `PUT /restaurant`                       | `/settings/profile` (new)                  | ❌      |
| `GET /restaurant/tables`                | `/settings/tables` (new)                   | ❌      |
| `POST /restaurant/tables`               | `/settings/tables` (new)                   | ❌      |
| `PUT /restaurant/tables/:id`            | `/settings/tables` (new)                   | ❌      |

### Menu
| Endpoint                                          | Frontend page                  | Status |
| ------------------------------------------------- | ------------------------------ | ------ |
| `GET /menu/items`                                 | `/menu`                        | 🟡      |
| `POST /menu/items`                                | `/menu` (new item modal)       | 🟡      |
| `PUT /menu/items/:id`                             | `/menu` (edit modal)           | 🟡      |
| `POST /menu/items/:id/image` (multipart)          | `/menu` (image picker)         | ❌      |
| `DELETE /menu/items/:id`                          | `/menu`                        | 🟡      |
| `GET /menu/categories`                            | `/menu` sidebar                | 🟡      |
| `POST /menu/categories`                           | `/menu` add-category modal     | 🟡      |
| `POST /ai/menu-items/:id/describe` *(M3.10)*      | `/menu` ✨ AI button per item   | ❌      |

### Orders (Cashier+)
| Endpoint                            | Frontend page              | Status |
| ----------------------------------- | -------------------------- | ------ |
| `GET /orders`                       | `/orders`                  | 🟡      |
| `GET /orders/:id`                   | `/orders/[id]` (new)       | ❌      |
| `POST /orders`                      | `/orders/new`              | 🟡      |
| `PATCH /orders/:id/status`          | `/orders` row action       | 🟡      |

### Kitchen
| Endpoint                                | Frontend page    | Status |
| --------------------------------------- | ---------------- | ------ |
| `GET /kitchen/orders`                   | `/kitchen`       | 🟡      |
| `GET /kitchen/queue`                    | `/kitchen` chips | 🟡      |
| `PUT /kitchen/orders/:id/status`        | `/kitchen` card  | 🟡      |

### Payments
| Endpoint                          | Frontend page | Status |
| --------------------------------- | ------------- | ------ |
| `GET /payments/open-orders`       | `/payments`   | 🟡      |
| `POST /payments`                  | `/payments`   | 🟡      |

### Staff
| Endpoint                          | Frontend page | Status |
| --------------------------------- | ------------- | ------ |
| `GET /staff`                      | `/staff`      | ✅      |
| `POST /staff`                     | `/staff`      | ✅      |
| `PUT /staff/:id`                  | `/staff`      | ✅      |
| `PATCH /staff/:id/active`         | `/staff`      | ✅      |

### Shifts
| Endpoint                          | Frontend page                | Status |
| --------------------------------- | ---------------------------- | ------ |
| `GET /shifts`                     | `/shifts`                    | 🟡      |
| `POST /shifts`                    | `/shifts` (new modal)        | 🟡      |
| `PUT /shifts/:id`                 | `/shifts` (edit modal)       | 🟡      |
| `DELETE /shifts/:id`              | `/shifts`                    | 🟡      |
| `GET /shifts/notes`               | `/shifts` notes panel        | 🟡      |
| `POST /shifts/notes`              | `/shifts` notes panel        | 🟡      |
| `DELETE /shifts/notes/:id`        | `/shifts` notes panel        | 🟡      |

### Reports
| Endpoint                          | Frontend page    | Status |
| --------------------------------- | ---------------- | ------ |
| `GET /reports/sales`              | `/reports`       | 🟡      |
| `GET /reports/orders`             | `/reports`       | 🟡      |
| `GET /reports/staff`              | `/reports`       | 🟡      |

### Real-time
| Channel                           | Frontend hook                | Status |
| --------------------------------- | ---------------------------- | ------ |
| SignalR `/hubs/orders`            | `useOrderBoard` / `useKitchenBoard` | ❌ (no live socket yet) |

---

## New pages to create

The following routes are referenced by the endpoint map but **do not exist yet**. They are part of the backlog below; create each one with the standard `page.tsx` + `loading.tsx` + `error.tsx` triplet.

1. `src/app/(protected)/settings/profile/page.tsx` — restaurant profile editor (consumes `GET/PUT /restaurant`).
2. `src/app/(protected)/settings/tables/page.tsx` — tables list + create/edit (`GET/POST/PUT /restaurant/tables`).
3. `src/app/(protected)/orders/[id]/page.tsx` — order detail view (`GET /orders/{id}`).
4. `src/app/(admin)/admin/restaurants/[id]/verify-email/page.tsx` — owner verification confirm UI.

---

## Backlog (work items, ordered)

### Phase A — Auth foundation (blocks everything else)
1. **FE-101 · Replace direct Keycloak call with `POST /auth/login`.** Currently `src/lib/auth/keycloak.ts` calls Keycloak's token endpoint directly. Switch to backend `/api/v1/auth/login` so tokens flow through dineOS (audit trail, rate limit, logout/blacklist). *Owner:* Dreni. *Acceptance:* `useAuthStore.login()` exchanges username/password with backend, persists `accessToken` + `refreshToken` cookies; rotating tokens works.
2. **FE-102 · Refresh-on-401 interceptor.** Add `apiClient.interceptors.response.use` that calls `/auth/refresh` once on `401`, retries the original request, and signs the user out on a second failure.
3. **FE-103 · Logout button calls `/auth/logout`.** Send refresh token, then clear cookies + auth store, redirect to `/login`.
4. **FE-104 · `useMe` hook from `GET /me`.** Replace any `tenant_id`/`roles` decoding done in `useTenant` with a single `useMe` query.

### Phase B — Operator pages (Manager / Cashier daily flows)
5. **FE-201 · Wire `/menu` page to real `MenuController`.** Replace mock with `useQuery(['menu','items'])`, `useMutation` for POST/PUT/DELETE; categories from `GET /menu/categories`. Add image upload component for `POST /menu/items/:id/image` (multipart).
6. **FE-202 · Add ✨ "Describe with AI" button to menu cards (M3.10).** Calls `POST /ai/menu-items/:id/describe`, shows suggestion + Save → triggers `PUT /menu/items/:id` with the new description.
7. **FE-203 · Wire `/orders` and `/orders/new`.** Replace mock board with `GET /orders?date=&status=`. Cashier flow: `POST /orders` from `/orders/new`, `PATCH /orders/:id/status` from row action menu. New `/orders/[id]` page for detail.
8. **FE-204 · Wire `/kitchen` board.** `GET /kitchen/orders` + `GET /kitchen/queue` for counters; `PUT /kitchen/orders/:id/status` on drag/drop or button.
9. **FE-205 · Wire `/payments`.** Cashier picks an open order from `GET /payments/open-orders`, submits `POST /payments` with exact total + method. Surface 422 (mismatch) and 404 (cross-tenant) clearly.
10. **FE-206 · Wire `/shifts` + shift notes panel.** Calendar from `GET /shifts?date=`; mutations for shift CRUD; right-side notes panel from `/shifts/notes`.
11. **FE-207 · Wire `/reports`.** Three TanStack-query tabs for sales / orders / staff. Date pickers map to `?from=&to=` (ISO `yyyy-MM-dd`).

### Phase C — Settings (new pages, blocked by Phase A)
12. **FE-301 · Build `/settings/profile`.** Read `GET /restaurant`, edit form bound to `UpdateRestaurantProfileRequest` schema (Zod), submit `PUT /restaurant`. Manager+ only.
13. **FE-302 · Build `/settings/tables`.** List + add + edit using `Restaurant/tables` endpoints. Includes "active" toggle from `PUT /tables/:id` body.

### Phase D — SuperAdmin
14. **FE-401 · Wire `/admin/users` to `GET /admin/users`.** Server-side pagination (`page`, `pageSize`, `search`).
15. **FE-402 · `/admin/dashboard` aggregate.** Needs a backend endpoint (TBD); track as backend follow-up before wiring.
16. **FE-403 · Email verification UI on restaurant detail.** Buttons for **Resend** (`POST .../resend` → toast "Verification email queued. JobId=N"). New page `/admin/restaurants/[id]/verify-email` with a 6-digit-code form that calls `POST .../confirm`.
17. **FE-404 · Soft-delete confirmation modal.** Hook the existing UI button to `DELETE /admin/restaurants/:id`.

### Phase E — Realtime
18. **FE-501 · SignalR client wiring for `/hubs/orders`.** Subscribe in `useOrderBoard` and `useKitchenBoard`, invalidate the corresponding TanStack queries on `orderCreated` / `orderStatusChanged`.

### Phase F — Polish
19. **FE-601 · ApiResponse envelope helper.** Centralize `unwrap<T>(res): T` that throws on `success:false` and surfaces `errors` to the form / toast layer.
20. **FE-602 · Standard error toast for 401/403/422/429.** Map status to user-friendly copy + retry button where applicable.

---

## How to pick up a task

1. Read the corresponding controller in `backend/src/DineOS.Api/Controllers/` and the DTO in `backend/src/DineOS.Application/`.
2. Add the type to `frontend/src/types/<feature>.ts` if missing (match the C# DTO 1:1).
3. Add the API call to `frontend/src/lib/api/<feature>Api.ts` — never call axios from a component.
4. Add query keys to `frontend/src/lib/api/queryKeys.ts`.
5. Add Zod schema in `frontend/src/lib/validations/<feature>.ts` if the page has a form.
6. Wire the page; ensure `loading.tsx` and `error.tsx` exist alongside `page.tsx`.
7. Run the Postman collection's matching folder end-to-end to confirm the contract before shipping.
