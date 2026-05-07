# dineOS Backend — Authentication & Token Management

## Session vs Stateless Token Trade-offs

### Why JWTs are stateless by default

A JWT access token is self-contained: the server verifies it by checking the cryptographic signature against a known public key (or shared secret) without any database or cache lookup. Once issued, the token is valid until its `exp` claim passes. This means the server has no way to revoke a token before it expires — there is no central store to check. A stolen or logged-out token remains valid for its remaining lifetime unless extra infrastructure is added.

### How the Redis blacklist adds revocation

dineOS addresses this with a hybrid approach. When a user logs out or rotates their refresh token, the old token's `jti` (JWT ID) claim is stored in Redis with a TTL equal to the token's remaining lifetime:

```
POST /api/v1/auth/login    -> Keycloak password grant through backend auth service
POST /api/v1/auth/logout   -> Keycloak revoke + BlacklistAsync(jti, remainingTtl)
POST /api/v1/auth/refresh  -> IsBlacklistedAsync(jti) check -> Keycloak refresh -> BlacklistAsync(oldJti, remainingTtl)
```

Refresh requests perform one Redis lookup (`IsBlacklistedAsync`) before calling Keycloak and one Redis write (`BlacklistAsync`) after successful rotation. Logout requests write the refresh-token `jti` to Redis and also call the Keycloak revocation endpoint. Access token validation at the middleware layer remains fully stateless — no Redis is involved on normal API calls.

### TTL-based auto-cleanup

The Redis key is given the same TTL as the token's natural expiry. When the token would have expired anyway, Redis automatically removes the blacklist entry. No background job, no manual housekeeping, no growing table of revoked tokens. A token that is already past its `exp` is floored to `TimeSpan.Zero`, which causes Redis to immediately discard the entry — equivalent to not storing it, since an expired token is already harmless.

### The trade-off spectrum

| Approach | Revocation | Latency cost | Complexity | Best for |
|---|---|---|---|---|
| **Fully stateless** (no blacklist) | None — tokens live until `exp` | Zero extra lookups | Lowest | Low-security public APIs, very short-lived tokens |
| **Hybrid Redis blacklist** ← *this implementation* | Refresh and logout paths only | Redis lookup on refresh, Redis write on refresh/logout | Medium | Restaurant SaaS: logout/rotation must be instant, but API throughput should not suffer per-request cache hits |
| **Fully stateful sessions** | Immediate on every path | 1 DB/cache lookup per request | Highest | Banking, healthcare — any domain where a compromised access token must be invalidated mid-flight |

### What is appropriate for dineOS

dineOS operates in a restaurant environment where staff sessions are typically short (a shift) and the primary security concern is a staff member's device being handed to someone else or a token being intercepted in transit. The hybrid approach is a deliberate fit:

- **Access tokens** are short-lived (minutes). A stolen access token becomes useless quickly without Redis involvement.
- **Refresh tokens** are longer-lived and are the real risk surface. The Redis blacklist ensures that logout and token rotation take effect immediately — a logged-out cashier cannot silently stay authenticated by reusing an old refresh token.
- **Per-request Redis overhead is avoided** on the hot path (authenticated API calls), keeping kitchen display and order board endpoints low-latency.

If dineOS later introduces higher-risk operations (e.g., payment authorisation, tenant admin privilege escalation), those specific endpoints could add an access-token blacklist check as an additional layer without changing the rest of the architecture.

## Backend Auth Endpoints

Token responses and errors use the standard `ApiResponse` envelope. Successful logout returns `204 No Content`.

### Login

```http
POST /api/v1/auth/login
Content-Type: application/json

{
  "username": "admin@dineos.dev",
  "password": "Test1234!"
}
```

Success:

```json
{
  "success": true,
  "data": {
    "accessToken": "<jwt>",
    "refreshToken": "<jwt>",
    "expiresIn": 300,
    "refreshExpiresIn": 1800
  },
  "message": "Login successful."
}
```

Invalid credentials return `401` with `ApiResponse.Fail("Invalid username or password.")`.

### Refresh

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refreshToken": "<refresh-token>"
}
```

The backend rejects locally blacklisted refresh-token `jti` values before calling Keycloak. On a successful refresh, the old refresh-token `jti` is blacklisted until its original expiry.

### Logout

```http
POST /api/v1/auth/logout
Authorization: Bearer <access-token>
Content-Type: application/json

{
  "refreshToken": "<refresh-token>"
}
```

Logout applies the local Redis blacklist and calls the Keycloak revocation endpoint. The local blacklist is still applied if the remote revocation call fails, because the backend must stop refresh-token reuse immediately.

## Keycloak Configuration

Configuration is bound to typed `KeycloakOptions` from the `Keycloak` section.

| Key | Purpose |
|---|---|
| `Realm` | Realm name, e.g. `dineos`. |
| `Authority` | Public issuer authority used for JWT validation, e.g. `https://auth.example.com/realms/dineos`. Must match token `iss`. |
| `MetadataAddress` | Optional metadata/JWKS URL. Use an internal URL here when the API cannot reach the public authority from its runtime network. |
| `Audience` | API audience expected in access tokens, currently `dineos-api`. |
| `AuthServerUrl` | Backchannel Keycloak base URL used by the backend service for login, refresh, and revoke. |
| `PublicAuthServerUrl` | Public Keycloak base URL used to derive the issuer when `Authority` is not set. |
| `ClientId` | Client used by backend token exchange endpoints. |
| `ClientSecret` | Secret for confidential clients. Leave unset for local public-client development only. |
| `GrantType` | Login grant type, defaults to `password`. |
| `RequireHttpsMetadata` | Set `true` outside local development unless TLS is terminated in a trusted internal path. |

Local Docker uses a public issuer and internal backchannel:

```text
Keycloak__Authority=http://localhost:8080/realms/dineos
Keycloak__MetadataAddress=http://keycloak:8080/realms/dineos/.well-known/openid-configuration
Keycloak__AuthServerUrl=http://keycloak:8080
Keycloak__PublicAuthServerUrl=http://localhost:8080
```

In production, keep secrets in the platform secret store and prefer a confidential client:

```text
Keycloak__Realm=dineos
Keycloak__Authority=https://auth.example.com/realms/dineos
Keycloak__AuthServerUrl=http://keycloak.keycloak.svc.cluster.local:8080
Keycloak__MetadataAddress=http://keycloak.keycloak.svc.cluster.local:8080/realms/dineos/.well-known/openid-configuration
Keycloak__Audience=dineos-api
Keycloak__ClientId=dineos-backend
Keycloak__ClientSecret=<secret>
Keycloak__GrantType=password
Keycloak__RequireHttpsMetadata=true
```

The Keycloak client used for backend login must allow direct access grants and must issue `dineos-api` as an access-token audience.

## Frontend Contract

The frontend should call the backend auth endpoints, not Keycloak directly:

- `NEXT_PUBLIC_API_URL` should point at the backend API base URL.
- Store `accessToken`, `refreshToken`, `expiresIn`, and `refreshExpiresIn`.
- Send `Authorization: Bearer <accessToken>` on protected API calls.
- Refresh with `POST /api/v1/auth/refresh` before or after access-token expiry.
- On logout, call `POST /api/v1/auth/logout` with the current refresh token, then clear local auth state.
- Continue sending `X-Tenant-ID` for tenant-scoped users; the backend still treats the JWT `tenant_id` claim as authoritative.
