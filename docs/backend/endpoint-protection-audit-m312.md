# Endpoint Protection & Validation Audit — M3.12

Reference inventory of every backend HTTP endpoint, the auth policy that gates it, the rate-limit policy applied, and the FluentValidation validator covering its request body. Acts as the baseline for future audits and as the proof-of-completion for milestone M3.12 (task #139).

## Validation error contract

All validator-driven failures return **HTTP 400** with the standard `ApiResponse` envelope:

```json
{
  "success": false,
  "message": "Validation failed.",
  "errors": [
    "Username is required.",
    "Password is required."
  ]
}
```

This is produced two ways in the codebase:

- Services returning `ServiceResult<T>.ValidationFailed(message, IReadOnlyList<string>)` — flows through `ServiceResultExtensions.ToActionResult` → `BadRequestObjectResult(ApiResponse.Fail(...))`.
- The auth flow (which uses the lighter `Result<T>` abstraction) returns `Result<T>.Failure("Validation failed.", errors)` → `AuthController.ToFailureResponse` recognizes the `"Validation failed."` sentinel and emits the same envelope shape.

ASP.NET's RFC 7807 `ValidationProblemDetails` is *available* (via `ServiceResult.ValidationFailed(message, IReadOnlyList<ValidationError>)`) but currently no production service uses the structured overload. If we adopt it later, the response shape will change to RFC 7807 — that's a deliberate forward decision, not part of this audit.

## Rate-limit policies

Defined in `backend/src/DineOS.Api/Program.cs`:

| Policy | Limit | Queue | Used by |
|---|---|---|---|
| `public` | 60/min | 5 | `POST auth/login`, `POST auth/refresh`, `GET health` |
| `authenticated` | 300/min | 20 | All other controllers (class-level) |
| `ai-expensive` | 10/min | 0 | `POST ai/menu-items/{id}/describe` |

## Public endpoints — intentional anonymous access

| Endpoint | Reason |
|---|---|
| `POST /auth/login` | Cannot require auth to obtain a token. Rate-limited via `public` policy. |
| `POST /auth/refresh` | Refresh exchange does not require an access token (the refresh token itself is the credential). Rate-limited via `public` policy. |
| `GET /health` | Used by uptime monitors and container orchestrators (k8s/docker liveness probes) which can't authenticate. Rate-limited via `public` policy. |

## No-input endpoints (validation N/A)

GET actions and DELETEs that take only a route-bound id are covered by the route template constraint (`{id:long}`) and need no DTO/validator:

- `GET admin/users`, `GET admin/restaurants`, `GET admin/restaurants/{id:long}`, `DELETE admin/restaurants/{id:long}`
- `GET menu`, `GET menu/items`, `GET menu/categories`, `DELETE menu/items/{id:long}`
- `GET orders`, `GET orders/{id:long}`
- `GET payments/open-orders`
- `GET kitchen/orders`, `GET kitchen/queue`
- `GET reports/sales`, `GET reports/orders`, `GET reports/staff`
- `GET restaurant`, `GET restaurant/tables`
- `GET shifts`, `DELETE shifts/{id:long}`
- `GET shifts/notes`, `DELETE shifts/notes/{id:long}`
- `GET staff`
- `GET me`
- `GET health`
- `POST ai/menu-items/{id:long}/describe` — id-only, no body
- `POST admin/restaurants/{tenantId:long}/email-verification/resend` — id-only, no body

Query-string filters on report/orders/shifts endpoints (`from`, `to`, `date`, `status`) are nullable and model-bound — invalid types return 400 via the ASP.NET model binder before reaching the action.

## Per-controller inventory

Auth attributes shown are the *resolved* policy (class-level merged with any action-level override). Rate-limit policies are inherited from the class unless an action overrides.

### AuthController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| Login | POST | `auth/login` | `LoginRequest` | `LoginRequestValidator` | Anonymous | `public` |
| Refresh | POST | `auth/refresh` | `RefreshTokenRequest` | `RefreshTokenRequestValidator` | Anonymous | `public` |
| Logout | POST | `auth/logout` | `LogoutRequest` | `LogoutRequestValidator` | `Authorize` | `authenticated` |

### AdminController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetUsers | GET | `admin/users` | — | — (paged) | `SuperAdminOnly` | `authenticated` |

### AdminRestaurantsController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetRestaurants | GET | `admin/restaurants` | — | — | `SuperAdminOnly` | `authenticated` |
| GetRestaurant | GET | `admin/restaurants/{id:long}` | — | — | `SuperAdminOnly` | `authenticated` |
| CreateRestaurant | POST | `admin/restaurants` | `CreateRestaurantRequest` | `CreateRestaurantRequestValidator` | `SuperAdminOnly` | `authenticated` |
| UpdateStatus | PATCH | `admin/restaurants/{id:long}/status` | `UpdateRestaurantStatusRequest` | `UpdateRestaurantStatusRequestValidator` | `SuperAdminOnly` | `authenticated` |
| UpdatePlan | PATCH | `admin/restaurants/{id:long}/plan` | `UpdateRestaurantPlanRequest` | `UpdateRestaurantPlanRequestValidator` | `SuperAdminOnly` | `authenticated` |
| DeleteRestaurant | DELETE | `admin/restaurants/{id:long}` | — | — | `SuperAdminOnly` | `authenticated` |

### AiController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| DescribeMenuItem | POST | `ai/menu-items/{id:long}/describe` | — | — | `ManagerAndAbove` | `ai-expensive` |

### EmailVerificationController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| Resend | POST | `admin/restaurants/{tenantId:long}/email-verification/resend` | — | — | `SuperAdminOnly` | `authenticated` |
| Confirm | POST | `admin/restaurants/{tenantId:long}/email-verification/confirm` | `ConfirmEmailVerificationRequest` | `ConfirmEmailVerificationRequestValidator` | `SuperAdminOnly` | `authenticated` |

### HealthController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| Get | GET | `health` | — | — | Anonymous | `public` |

### KitchenController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetKitchenOrders | GET | `kitchen/orders` | — | — | `KitchenStaffOnly` | `authenticated` |
| UpdateOrderStatus | PUT | `kitchen/orders/{id:long}/status` | `UpdateKitchenOrderStatusRequest` | `UpdateKitchenOrderStatusRequestValidator` | `KitchenStaffOnly` | `authenticated` |
| GetQueue | GET | `kitchen/queue` | — | — | `KitchenStaffOnly` | `authenticated` |

### MenuController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetMenu | GET | `menu` | — | — | `ManagerAndAbove` | `authenticated` |
| GetMenuItems | GET | `menu/items` | — | — | `Authorize` | `authenticated` |
| CreateMenuItem | POST | `menu/items` | `CreateMenuItemRequest` | `CreateMenuItemRequestValidator` | `ManagerAndAbove` | `authenticated` |
| UpdateMenuItem | PUT | `menu/items/{id:long}` | `UpdateMenuItemRequest` | `UpdateMenuItemRequestValidator` | `ManagerAndAbove` | `authenticated` |
| UploadMenuItemImage | POST | `menu/items/{id:long}/image` | `UploadMenuItemImageRequest` (multipart) | `UploadMenuItemImageRequestValidator` | `ManagerAndAbove` | `authenticated` |
| DeleteMenuItem | DELETE | `menu/items/{id:long}` | — | — | `ManagerAndAbove` | `authenticated` |
| GetMenuCategories | GET | `menu/categories` | — | — | `Authorize` | `authenticated` |
| CreateMenuCategory | POST | `menu/categories` | `CreateMenuCategoryRequest` | `CreateMenuCategoryRequestValidator` | `ManagerAndAbove` | `authenticated` |

### MeController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetMe | GET | `me` | — | — | `Authorize` | `authenticated` |

### OrdersController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetOrders | GET | `orders` | — | — | `CashierAndAbove` | `authenticated` |
| GetOrder | GET | `orders/{id:long}` | — | — | `CashierAndAbove` | `authenticated` |
| CreateOrder | POST | `orders` | `CreateOrderRequest` | `CreateOrderRequestValidator` | `CashierAndAbove` | `authenticated` |
| UpdateStatus | PATCH | `orders/{id:long}/status` | `UpdateOrderStatusRequest` | `UpdateOrderStatusRequestValidator` | `CashierAndAbove` | `authenticated` |

### PaymentsController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetOpenOrders | GET | `payments/open-orders` | — | — | `CashierAndAbove` | `authenticated` |
| ProcessPayment | POST | `payments` | `ProcessPaymentRequest` | `ProcessPaymentRequestValidator` | `CashierAndAbove` | `authenticated` |

### ReportsController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetSalesReport | GET | `reports/sales` | — | — | `ManagerAndAbove` | `authenticated` |
| GetOrdersReport | GET | `reports/orders` | — | — | `ManagerAndAbove` | `authenticated` |
| GetStaffReport | GET | `reports/staff` | — | — | `ManagerAndAbove` | `authenticated` |

### RestaurantController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetRestaurant | GET | `restaurant` | — | — | `ManagerAndAbove` | `authenticated` |
| UpdateRestaurant | PUT | `restaurant` | `UpdateRestaurantProfileRequest` | `UpdateRestaurantProfileRequestValidator` | `ManagerAndAbove` | `authenticated` |
| GetTables | GET | `restaurant/tables` | — | — | `ManagerAndAbove` | `authenticated` |
| AddTable | POST | `restaurant/tables` | `CreateRestaurantTableRequest` | `CreateRestaurantTableRequestValidator` | `ManagerAndAbove` | `authenticated` |
| UpdateTable | PUT | `restaurant/tables/{id:long}` | `UpdateRestaurantTableRequest` | `UpdateRestaurantTableRequestValidator` | `ManagerAndAbove` | `authenticated` |

### ShiftsController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetShifts | GET | `shifts` | — | — | `ManagerAndAbove` | `authenticated` |
| CreateShift | POST | `shifts` | `CreateShiftRequest` | `CreateShiftRequestValidator` | `ManagerAndAbove` | `authenticated` |
| UpdateShift | PUT | `shifts/{id:long}` | `UpdateShiftRequest` | `UpdateShiftRequestValidator` | `ManagerAndAbove` | `authenticated` |
| DeleteShift | DELETE | `shifts/{id:long}` | — | — | `ManagerAndAbove` | `authenticated` |

### ShiftNotesController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetShiftNotes | GET | `shifts/notes` | — | — | `Authorize` | `authenticated` |
| CreateShiftNote | POST | `shifts/notes` | `CreateShiftNoteRequest` | `CreateShiftNoteRequestValidator` | `ManagerAndAbove` | `authenticated` |
| DeleteShiftNote | DELETE | `shifts/notes/{id:long}` | — | — | `ManagerAndAbove` | `authenticated` |

### StaffController

| Action | Verb | Route | Body DTO | Validator | Auth | Rate limit |
|---|---|---|---|---|---|---|
| GetStaff | GET | `staff` | — | — | `ManagerAndAbove` | `authenticated` |
| AddStaff | POST | `staff` | `CreateStaffMemberRequest` | `CreateStaffMemberRequestValidator` | `ManagerAndAbove` | `authenticated` |
| UpdateStaff | PUT | `staff/{id:long}` | `UpdateStaffMemberRequest` | `UpdateStaffMemberRequestValidator` | `ManagerAndAbove` | `authenticated` |
| SetStaffActive | PATCH | `staff/{id:long}/active` | `SetStaffActiveRequest` | `SetStaffActiveRequestValidator` | `ManagerAndAbove` | `authenticated` |

## Summary

- 24 endpoints with request bodies — all have validators.
- 25 endpoints without request bodies — covered by route constraints / nullable model binding.
- 3 endpoints intentionally anonymous (login, refresh, health) — documented above.
- All authenticated endpoints carry `[EnableRateLimiting("authenticated")]` (or stricter for AI).
- All public endpoints carry `[EnableRateLimiting("public")]`.
- All controllers carry tenant isolation via `TenantIsolationMiddleware` after the auth middleware (except those scoped to `SuperAdminOnly` or anonymous endpoints which bypass tenant context).

## How to keep this current

When adding a new controller action:

1. If it has a body — add a request DTO and a co-located validator (`{Request}Validator : AbstractValidator<{Request}>` in the same file). FluentValidation autoregistration picks it up.
2. Call the validator inside the service method that consumes the DTO (match `PaymentService.ProcessPaymentAsync` for `ServiceResult<T>` services, or `KeycloakAuthService.LoginAsync` for `Result<T>` services).
3. Decorate the action with `[ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]` for the validation case, plus 401/403/429 as applicable.
4. Add the row to the table above.
