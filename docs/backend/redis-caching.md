# DineOS — Redis cache-aside (M3.5)

`GET /api/v1/menu/items` is the first endpoint behind a Redis cache-aside
layer. Menus are read every time a cashier or kitchen client opens the menu
page, but they only change when a manager touches the data — exactly the
shape cache-aside is built for.

This doc covers the contract, the wiring, and a cold-vs-warm performance
comparison captured against real PostgreSQL and Redis containers.

## Where the code lives

| Concern                     | File                                                                                                       |
|-----------------------------|------------------------------------------------------------------------------------------------------------|
| Generic cache contract      | [`src/DineOS.Application/Interfaces/Services/ICacheService.cs`](../../backend/src/DineOS.Application/Interfaces/Services/ICacheService.cs) |
| Redis-backed implementation | [`src/DineOS.Infrastructure/Services/RedisCacheService.cs`](../../backend/src/DineOS.Infrastructure/Services/RedisCacheService.cs)         |
| Cache-aside read + invalidation | [`src/DineOS.Infrastructure/Services/MenuService.cs`](../../backend/src/DineOS.Infrastructure/Services/MenuService.cs) |
| DI registration             | [`src/DineOS.Infrastructure/DependencyInjection.cs`](../../backend/src/DineOS.Infrastructure/DependencyInjection.cs) |
| Unit tests (hit/miss/invalidate) | [`tests/DineOS.Tests/Unit/MenuServiceTests.cs`](../../backend/tests/DineOS.Tests/Unit/MenuServiceTests.cs)            |
| Manual cold/warm benchmark  | [`tests/DineOS.Tests/Benchmarks/MenuCacheBenchmark.cs`](../../backend/tests/DineOS.Tests/Benchmarks/MenuCacheBenchmark.cs) |

## Contract

```csharp
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan ttl,
        CancellationToken ct = default);
}
```

`RedisCacheService` is a thin wrapper around the existing
`IConnectionMultiplexer` singleton (the same one used for token blacklist
and SignalR backplane). It JSON-serialises values via `System.Text.Json`
and swallows `RedisException`s so a cache outage degrades to a slower
direct-DB path instead of taking the endpoint down.

## Cache key + TTL

| Key                          | TTL    | Invalidated by                                              |
|------------------------------|--------|-------------------------------------------------------------|
| `menu:items:tenant:{TenantId}` | 5 min | `POST /menu/items`, `PUT /menu/items/{id}`, `DELETE /menu/items/{id}` |

The key is **tenant-scoped**. There is no cross-tenant cache key. When a
caller has no tenant context (e.g. SuperAdmin), `MenuService` skips the
cache entirely (`MenuService.cs:25-26`) so we never serve cross-tenant
data from a tenant-scoped slot.

## Read flow

```text
GET /api/v1/menu/items
  └─ MenuController.GetMenuItems
       └─ MenuService.GetMenuItemsAsync
            ├─ if (TenantId == null) → load from DB, return (no cache)
            └─ cache.GetOrSetAsync(key, ttl, factory)
                 ├─ Redis GET key
                 │   ├─ hit  → JSON deserialize → return
                 │   └─ miss → factory()
                 │              └─ EF query → List<MenuItemDto>
                 │              → Redis SETEX key ttl json
                 │              → return
                 └─ (Redis down) → factory() runs, log warning, no cache write
```

`GetOrSetAsync` is implemented twice (once on the interface’s default
helper, once on the Redis impl). Both share the same semantics: the
factory only runs on a real miss; nothing is cached on errors.

## Write / invalidation flow

`Create`, `Update`, and `Delete` menu-item operations all do:

```csharp
db.MenuItems.{Add|Update|Remove}(item);
await db.SaveChangesAsync(ct);
await cache.RemoveAsync(MenuItemsCacheKey(tenantId), ct);
```

The remove happens **after** `SaveChangesAsync`, so a concurrent reader
that misses the cache before the write commits will read the *old* row
from the DB, populate the cache with old data, and then have its entry
deleted by the writer. Worst case the next reader sees a stale value for
the duration of one DB query; cache-aside intentionally tolerates this
narrow window to avoid the complexity of write-through.

## Cold-vs-warm performance

### Methodology

- **Data**: 1,000 `MenuItems` for `TenantId = 1`, varied categories.
- **Cold call**: `cache.RemoveAsync(key)` then time
  `MenuService.GetMenuItemsAsync()` → Redis miss → DB roundtrip → cache
  populate.
- **Warm call**: same method, no eviction → Redis hit → JSON deserialize.
- **Setup**: the benchmark issues a throwaway DB query first to ensure the
  Npgsql pool is open and EF queries are JIT-compiled. Without this step
  the first cold timing absorbs ~150 ms of unrelated warm-up cost.
- Timings come from
  [`MenuCacheBenchmark.cs`](../../backend/tests/DineOS.Tests/Benchmarks/MenuCacheBenchmark.cs)
  using `Stopwatch` around the same code path the controller executes —
  the only thing missing relative to `curl` is the HTTP framing, which is
  a constant cost on both sides of the comparison and would not change the
  relative speed-up.
- Postgres and Redis ran in Docker; ports `:5434` (Postgres) and `:6380`
  (Redis) were used because the host already had `Postgres.app` on `:5432`.

### Results (3 runs, same process)

| Run | Cold (Redis miss → DB) | Warm avg / min / max (Redis hit) | Speed-up |
|-----|------------------------|----------------------------------|----------|
| 1   | 180.43 ms              | 34.66 / 11.62 / 63.83 ms         | ~5.2×    |
| 2   | 100.20 ms              | 7.41 / 2.64 / 21.63 ms           | ~13.5×   |
| 3   | 36.23 ms               | 3.15 / 2.05 / 8.14 ms            | ~11.5×   |

Steady-state (run 3) is the load shape that production traffic actually
sees, since each cache entry serves many requests before it expires.
A representative warm hit lands in the **2–3 ms range**, dominated by
Redis network round-trip and JSON deserialisation.

The wide spread in Run 1’s cold timing is the Npgsql connection pool +
EF assembly load cost; once those amortise, every miss settles into the
30–40 ms range for the 1,000-row dataset.

### Per-run detail (Run 3, raw)

```
COLD: 36.23 ms  (rows: 1000)
WARM (10 runs):
  run  1: 6.20 ms
  run  2: 2.19 ms
  run  3: 2.21 ms
  run  4: 2.08 ms
  run  5: 8.14 ms
  run  6: 2.13 ms
  run  7: 2.11 ms
  run  8: 2.17 ms
  run  9: 2.19 ms
  run 10: 2.05 ms
WARM avg=3.15 ms  min=2.05  max=8.14
Speedup: 11.5x (cold 36.23 ms vs warm avg 3.15 ms)
```

`run 1` is consistently a little slower than `run 2-10` because the JSON
serializer warms up on the first deserialise.

### Reproducing locally

```bash
cd backend
# Bring up backing services. The override file remaps to free ports if
# Postgres.app or another Redis is already on the default ones.
docker compose -f docker-compose.yml -f /tmp/docker-compose.override.yml \
  up postgres redis -d

# Apply migrations (the override port goes here too).
ConnectionStrings__DefaultConnection='Host=localhost;Port=5434;Database=dineos;Username=dineos;Password=dineos_dev' \
  dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api

# Seed 1k menu items for tenant 1.
psql -h localhost -p 5434 -U dineos -d dineos <<'SQL'
INSERT INTO "MenuItems"
  ("TenantId","Name","Price","Category","Description","ImageUrl","CreatedAt")
SELECT 1, 'Item '||s, (random()*30)::numeric(10,2),
  (ARRAY['Pizza','Pasta','Salad','Dessert','Drinks','Soup','Appetizer','Special'])[1+(random()*7)::int],
  NULL, NULL, NOW()
FROM generate_series(1,1000) AS s;
SQL

# Run the benchmark (disabled by default in CI).
RUN_BENCH=1 dotnet test DineOS.slnx \
  --filter ColdVsWarm \
  --logger 'console;verbosity=detailed' \
  --no-build
```

The benchmark’s default behaviour without `RUN_BENCH=1` is to exit
immediately so it is safe to leave checked in.

## Failure modes

| Scenario                      | Behaviour                                                            |
|-------------------------------|----------------------------------------------------------------------|
| Redis is down                 | `RedisException` is logged at Warning; call falls through to DB; nothing is cached. The endpoint stays functional. |
| Cache entry is stale          | Next write invalidates it. Worst-case staleness window: the TTL (5 min) or until any write happens. |
| Concurrent write + read       | Reader may briefly repopulate cache with pre-write data; the write then deletes it on `SaveChanges` success. Self-heals on next miss. |
| SuperAdmin / no tenant context | Cache is bypassed; the underlying EF query filter still applies. |
| Multi-instance deployment     | All instances share the same Redis (`IConnectionMultiplexer` singleton points at the same backplane), so invalidation on one instance is visible to all. |

## Related docs

- [`docs/backend/sql-optimization.md`](./sql-optimization.md) — the
  `(TenantId, Category)` index that backs the *miss* path of this cache.
- [`docs/backend/auth.md`](./auth.md) — `IConnectionMultiplexer` is also
  the token blacklist and SignalR backplane.
- [`docs/database/SCHEMA.md`](../database/SCHEMA.md) — `MenuItems` table layout.
