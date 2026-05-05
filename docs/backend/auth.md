# dineOS Backend — Authentication & Token Management

## Session vs Stateless Token Trade-offs

### Why JWTs are stateless by default

A JWT access token is self-contained: the server verifies it by checking the cryptographic signature against a known public key (or shared secret) without any database or cache lookup. Once issued, the token is valid until its `exp` claim passes. This means the server has no way to revoke a token before it expires — there is no central store to check. A stolen or logged-out token remains valid for its remaining lifetime unless extra infrastructure is added.

### How the Redis blacklist adds revocation

dineOS addresses this with a hybrid approach. When a user logs out or rotates their refresh token, the old token's `jti` (JWT ID) claim is stored in Redis with a TTL equal to the token's remaining lifetime:

```
POST /api/v1/auth/logout   → BlacklistAsync(jti, remainingTtl)
POST /api/v1/auth/refresh  → IsBlacklistedAsync(jti) check → BlacklistAsync(oldJti, remainingTtl)
```

Every refresh and logout request incurs **one Redis lookup** (`IsBlacklistedAsync`) and, on success, **one Redis write** (`BlacklistAsync`). Access token validation at the middleware layer remains fully stateless — no Redis is involved on normal API calls.

### TTL-based auto-cleanup

The Redis key is given the same TTL as the token's natural expiry. When the token would have expired anyway, Redis automatically removes the blacklist entry. No background job, no manual housekeeping, no growing table of revoked tokens. A token that is already past its `exp` is floored to `TimeSpan.Zero`, which causes Redis to immediately discard the entry — equivalent to not storing it, since an expired token is already harmless.

### The trade-off spectrum

| Approach | Revocation | Latency cost | Complexity | Best for |
|---|---|---|---|---|
| **Fully stateless** (no blacklist) | None — tokens live until `exp` | Zero extra lookups | Lowest | Low-security public APIs, very short-lived tokens |
| **Hybrid Redis blacklist** ← *this implementation* | Refresh and logout paths only | 1 Redis lookup on refresh/logout | Medium | Restaurant SaaS: logout/rotation must be instant, but API throughput should not suffer per-request cache hits |
| **Fully stateful sessions** | Immediate on every path | 1 DB/cache lookup per request | Highest | Banking, healthcare — any domain where a compromised access token must be invalidated mid-flight |

### What is appropriate for dineOS

dineOS operates in a restaurant environment where staff sessions are typically short (a shift) and the primary security concern is a staff member's device being handed to someone else or a token being intercepted in transit. The hybrid approach is a deliberate fit:

- **Access tokens** are short-lived (minutes). A stolen access token becomes useless quickly without Redis involvement.
- **Refresh tokens** are longer-lived and are the real risk surface. The Redis blacklist ensures that logout and token rotation take effect immediately — a logged-out cashier cannot silently stay authenticated by reusing an old refresh token.
- **Per-request Redis overhead is avoided** on the hot path (authenticated API calls), keeping kitchen display and order board endpoints low-latency.

If dineOS later introduces higher-risk operations (e.g., payment authorisation, tenant admin privilege escalation), those specific endpoints could add an access-token blacklist check as an additional layer without changing the rest of the architecture.
