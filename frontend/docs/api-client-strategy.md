# API Client Strategy — dineOS Frontend

## Chosen approach: hand-written typed clients

All API calls use hand-written typed client functions in `src/lib/api/`.
Each domain has its own file (e.g. `staffApi.ts`, `restaurantApi.ts`).
This is the established project pattern per CLAUDE.md.

## Why not OpenAPI codegen

Codegen (orval, openapi-typescript) can be revisited when the backend contract
is stable across all domains. For now the hand-written approach keeps the code
readable and avoids a build-time dependency on the backend being available.

## How auth tokens flow

1. User logs in → Keycloak issues JWT
2. Login page stores token in `access_token` cookie
3. `apiClient.ts` interceptor reads the cookie and attaches `Authorization: Bearer <token>`
4. Backend validates the JWT and the `X-Tenant-ID` header

The previous `access_token=dev` interceptor bypass has been removed. The
client now attaches `Authorization: Bearer <token>` for any non-empty
`access_token` cookie value — no string is treated as an auth-skip sentinel.
Mock-data switches in `ordersApi.ts` / `kitchenApi.ts` still inspect the
cookie for the `"dev"` value but do not affect the authorization header.

## Query key conventions

All query keys are defined in `src/lib/api/queryKeys.ts`.
List keys are tenant-scoped: `[domain, tenantId, "list"]`.
Invalidate using `.all` keys after mutations.
