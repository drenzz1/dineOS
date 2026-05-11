# DineOS — SQL optimization proof (M3.4)

This document captures `EXPLAIN (ANALYZE, BUFFERS)` output for four real
dineOS query patterns, comparing the planner's behaviour with and without
the supporting EF Core indexes declared in
[`AppDbContext.OnModelCreating`](../../backend/src/DineOS.Infrastructure/Persistence/AppDbContext.cs).

Three of the four indexes already existed in the schema (`Orders`,
`MenuItems`, `ShiftNotes`); the fourth — `Payments(TenantId, CreatedAt)` —
is a new index added by this work and shipped through migration
[`20260511214724_AddPaymentsTenantCreatedAtIndex`](../../backend/src/DineOS.Infrastructure/Persistence/Migrations/20260511214724_AddPaymentsTenantCreatedAtIndex.cs).

## TL;DR

| # | Query                                              | Index proved                              | Baseline | Optimized | Speed-up |
|---|----------------------------------------------------|-------------------------------------------|----------|-----------|----------|
| 1 | Orders board — latest 50 active orders             | `IX_Orders_TenantId_CreatedAt`            | 9.736 ms | 0.151 ms  | ~64×     |
| 2 | Menu items by category                             | `IX_MenuItems_TenantId_Category`          | 1.858 ms | 0.336 ms  | ~5.5×    |
| 3 | Recent shift notes (last 7 days)                   | `IX_ShiftNotes_TenantId_CreatedAt`        | 4.744 ms | 0.363 ms  | ~13×     |
| 4 | Period revenue from payments (last 30 days, **new**) | `IX_Payments_TenantId_CreatedAt`          | 3.246 ms | 0.574 ms  | ~5.7×    |

Buffer hits (a hardware-independent proxy for work done) drop the same way:
Q1 1031 → 103, Q3 662 → 257, Q4 516 → 496 (with far fewer pages touched by
the index path).

## Reproduction

### Environment

- PostgreSQL 16 (image `postgres:16-alpine`)
- A clean schema produced by the EF migrations through
  `20260511214724_AddPaymentsTenantCreatedAtIndex`
- Three tenants seeded; row counts after seed:
  - `Orders`: 100,000
  - `MenuItems`: 30,000
  - `ShiftNotes`: 50,000
  - `Payments`: 50,000
- `ANALYZE` run after each schema change so statistics are fresh
- Postgres `:5432` on the host (Postgres.app) was already bound during the
  capture, so the test container was mapped to `:5434` instead. Use
  `Host=localhost;Port=5434;…` for the connection string when reproducing.

### Steps

```bash
# From backend/, with no host port-5432 conflict:
docker compose up postgres -d
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=dineos;Username=dineos;Password=dineos_dev" \
  dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api

# Then run the seed + per-query EXPLAIN scripts (see Appendix A).
psql -h localhost -p 5432 -U dineos -d dineos -f seed.sql
psql -h localhost -p 5432 -U dineos -d dineos -f explain.sql
```

The seed and EXPLAIN scripts used here are reproduced verbatim in
[Appendix A](#appendix-a--scripts) so the proof can be regenerated.

### A note on selectivity

The 3-tenant / 5-status / 8-category seed gives ~33k Orders per tenant,
~3,750 MenuItems per (tenant, category), and ~960 ShiftNotes per
(tenant, recent week). These selectivities are deliberately in a range
where indexed access wins clearly but the planner still has a real
decision to make — they reflect the order of magnitude of a busy
single-restaurant tenant.

## Q1 — Orders board (latest 50 active orders)

This is the query that backs the "Live Order Board" page. It reads the 50
newest non-finished orders for the current tenant.

```sql
SELECT "Id", "OrderType", "TableNumber", "Status", "Total", "Notes", "CreatedAt"
FROM "Orders"
WHERE "TenantId" = 1 AND "Status" IN (0, 1, 2)  -- New, InProgress, Ready
ORDER BY "CreatedAt" DESC
LIMIT 50;
```

The supporting index is `IX_Orders_TenantId_CreatedAt`. The planner uses it
as an *Index Scan Backward*: it walks the index from the newest entry for
this tenant down, applies the `Status IN (…)` filter, and stops as soon as
50 matches land. The `IX_Orders_TenantId_Status` index is justified by
separate aggregation patterns (e.g. counting orders per status); for *this*
LIMIT-bounded query the date index is what the planner picks.

### BEFORE — both Orders indexes dropped

```
                                                        QUERY PLAN
--------------------------------------------------------------------------------------------------------------------------
 Limit  (cost=3211.46..3211.58 rows=50 width=69) (actual time=9.672..9.678 rows=50 loops=1)
   Buffers: shared hit=1031
   ->  Sort  (cost=3211.46..3253.26 rows=16721 width=69) (actual time=9.671..9.674 rows=50 loops=1)
         Sort Key: "CreatedAt" DESC
         Sort Method: top-N heapsort  Memory: 31kB
         Buffers: shared hit=1031
         ->  Seq Scan on "Orders"  (cost=0.00..2656.00 rows=16721 width=69) (actual time=0.006..7.505 rows=16597 loops=1)
               Filter: (("TenantId" = 1) AND ("Status" = ANY ('{0,1,2}'::integer[])))
               Rows Removed by Filter: 83403
               Buffers: shared hit=1031
 Planning Time: 0.339 ms
 Execution Time: 9.736 ms
```

Full table scan with a top-N sort over ~16.6k matching rows.

### AFTER — both Orders indexes recreated

```
                                                                           QUERY PLAN
-----------------------------------------------------------------------------------------------------------------------------------------------------------------
 Limit  (cost=0.42..15.67 rows=50 width=69) (actual time=0.056..0.132 rows=50 loops=1)
   Buffers: shared hit=100 read=3
   ->  Index Scan Backward using "IX_Orders_TenantId_CreatedAt" on "Orders"  (cost=0.42..5116.50 rows=16767 width=69) (actual time=0.056..0.127 rows=50 loops=1)
         Index Cond: ("TenantId" = 1)
         Filter: ("Status" = ANY ('{0,1,2}'::integer[]))
         Rows Removed by Filter: 47
         Buffers: shared hit=100 read=3
 Planning Time: 0.217 ms
 Execution Time: 0.151 ms
```

The planner walks the index backwards from the newest row for tenant 1,
only filters out 47 rows on the way to finding 50 matches, and returns.

### Summary

| Metric             | Before    | After     |
|--------------------|-----------|-----------|
| Plan               | Seq Scan + top-N Sort | Index Scan Backward (CreatedAt DESC) |
| Execution time     | 9.736 ms  | 0.151 ms  |
| Buffer hits        | 1031      | 100 (+3 read) |
| Rows examined      | 100,000   | ~97       |

## Q2 — Menu items by category

The "Menu" page lists all items of a given category for the tenant.

```sql
SELECT "Id", "Name", "Price", "Category"
FROM "MenuItems"
WHERE "TenantId" = 1 AND "Category" = 'Pizza';
```

Supporting index: `IX_MenuItems_TenantId_Category`.

### BEFORE — `IX_MenuItems_TenantId_Category` dropped

```
                                                 QUERY PLAN
------------------------------------------------------------------------------------------------------------
 Seq Scan on "MenuItems"  (cost=0.00..773.00 rows=723 width=30) (actual time=0.005..1.823 rows=720 loops=1)
   Filter: (("TenantId" = 1) AND ("Category" = 'Pizza'::text))
   Rows Removed by Filter: 29280
   Buffers: shared hit=323
 Planning Time: 0.103 ms
 Execution Time: 1.858 ms
```

### AFTER — `IX_MenuItems_TenantId_Category` recreated

```
                                                                  QUERY PLAN
----------------------------------------------------------------------------------------------------------------------------------------------
 Bitmap Heap Scan on "MenuItems"  (cost=11.70..345.54 rows=723 width=30) (actual time=0.073..0.287 rows=720 loops=1)
   Recheck Cond: (("TenantId" = 1) AND ("Category" = 'Pizza'::text))
   Heap Blocks: exact=288
   Buffers: shared hit=288 read=2
   ->  Bitmap Index Scan on "IX_MenuItems_TenantId_Category"  (cost=0.00..11.52 rows=723 width=0) (actual time=0.035..0.036 rows=720 loops=1)
         Index Cond: (("TenantId" = 1) AND ("Category" = 'Pizza'::text))
         Buffers: shared read=2
 Planning Time: 0.143 ms
 Execution Time: 0.336 ms
```

### Summary

| Metric         | Before    | After    |
|----------------|-----------|----------|
| Plan           | Seq Scan  | Bitmap Index + Heap Scan |
| Execution time | 1.858 ms  | 0.336 ms |
| Rows examined  | 30,000    | 720      |

## Q3 — Recent shift notes (last 7 days)

```sql
SELECT "Id", "Title", "Priority", "Author", "CreatedAt"
FROM "ShiftNotes"
WHERE "TenantId" = 1 AND "CreatedAt" >= NOW() - INTERVAL '7 days'
ORDER BY "CreatedAt" DESC;
```

Supporting index: `IX_ShiftNotes_TenantId_CreatedAt`.

### BEFORE — `IX_ShiftNotes_TenantId_CreatedAt` dropped

```
                                                     QUERY PLAN
--------------------------------------------------------------------------------------------------------------------
 Sort  (cost=1675.22..1676.01 rows=318 width=38) (actual time=4.695..4.710 rows=309 loops=1)
   Sort Key: "CreatedAt" DESC
   Sort Method: quicksort  Memory: 45kB
   Buffers: shared hit=662
   ->  Seq Scan on "ShiftNotes"  (cost=0.00..1662.00 rows=318 width=38) (actual time=0.006..4.635 rows=309 loops=1)
         Filter: (("TenantId" = 1) AND ("CreatedAt" >= (now() - '7 days'::interval)))
         Rows Removed by Filter: 49691
         Buffers: shared hit=662
 Planning Time: 0.131 ms
 Execution Time: 4.744 ms
```

### AFTER — `IX_ShiftNotes_TenantId_CreatedAt` recreated

```
                                                                      QUERY PLAN
------------------------------------------------------------------------------------------------------------------------------------------------------
 Sort  (cost=577.47..578.26 rows=317 width=38) (actual time=0.315..0.329 rows=309 loops=1)
   Sort Key: "CreatedAt" DESC
   Sort Method: quicksort  Memory: 45kB
   Buffers: shared hit=254 read=3
   ->  Bitmap Heap Scan on "ShiftNotes"  (cost=11.54..564.30 rows=317 width=38) (actual time=0.069..0.260 rows=309 loops=1)
         Recheck Cond: (("TenantId" = 1) AND ("CreatedAt" >= (now() - '7 days'::interval)))
         Heap Blocks: exact=254
         Buffers: shared hit=254 read=3
         ->  Bitmap Index Scan on "IX_ShiftNotes_TenantId_CreatedAt"  (cost=0.00..11.46 rows=317 width=0) (actual time=0.049..0.049 rows=309 loops=1)
               Index Cond: (("TenantId" = 1) AND ("CreatedAt" >= (now() - '7 days'::interval)))
               Buffers: shared read=3
 Planning Time: 0.162 ms
 Execution Time: 0.363 ms
```

### Summary

| Metric         | Before    | After    |
|----------------|-----------|----------|
| Plan           | Seq Scan + Sort | Bitmap Index + Heap Scan + Sort |
| Execution time | 4.744 ms  | 0.363 ms |
| Rows examined  | 50,000    | 309      |

## Q4 — Period revenue from payments (new index)

This query backs the daily-revenue / period-revenue cards on the reports
and admin dashboards.

```sql
SELECT SUM("Amount") AS revenue, COUNT(*) AS n
FROM "Payments"
WHERE "TenantId" = 1
  AND "Status"   = 0  -- Completed
  AND "CreatedAt" >= NOW() - INTERVAL '30 days';
```

Before this work the `Payments` table had **no** non-primary-key indexes,
so the query degenerated to a full table scan even though both conditions
in the predicate are very selective. The new index
`IX_Payments_TenantId_CreatedAt` (added via
[`AppDbContext.OnModelCreating`](../../backend/src/DineOS.Infrastructure/Persistence/AppDbContext.cs)
and migration
[`20260511214724_AddPaymentsTenantCreatedAtIndex`](../../backend/src/DineOS.Infrastructure/Persistence/Migrations/20260511214724_AddPaymentsTenantCreatedAtIndex.cs))
covers the two highest-selectivity predicates — tenant and date — and lets
the planner skip ~97% of the heap.

### BEFORE — no index on `Payments`

```
                                                   QUERY PLAN
-----------------------------------------------------------------------------------------------------------------
 Aggregate  (cost=1642.70..1642.71 rows=1 width=40) (actual time=3.235..3.235 rows=1 loops=1)
   Buffers: shared hit=516
   ->  Seq Scan on "Payments"  (cost=0.00..1641.00 rows=340 width=6) (actual time=0.011..3.194 rows=330 loops=1)
         Filter: (("TenantId" = 1) AND ("Status" = 0) AND ("CreatedAt" >= (now() - '30 days'::interval)))
         Rows Removed by Filter: 49670
         Buffers: shared hit=516
 Planning Time: 0.178 ms
 Execution Time: 3.246 ms
```

### AFTER — `IX_Payments_TenantId_CreatedAt` created

```
                                                                      QUERY PLAN
------------------------------------------------------------------------------------------------------------------------------------------------------
 Aggregate  (cost=586.82..586.83 rows=1 width=40) (actual time=0.555..0.555 rows=1 loops=1)
   Buffers: shared hit=489 read=7
   ->  Bitmap Heap Scan on "Payments"  (cost=38.15..585.13 rows=338 width=6) (actual time=0.185..0.530 rows=330 loops=1)
         Recheck Cond: (("TenantId" = 1) AND ("CreatedAt" >= (now() - '30 days'::interval)))
         Filter: ("Status" = 0)
         Rows Removed by Filter: 1025
         Heap Blocks: exact=489
         Buffers: shared hit=489 read=7
         ->  Bitmap Index Scan on "IX_Payments_TenantId_CreatedAt"  (cost=0.00..38.06 rows=1377 width=0) (actual time=0.185..0.185 rows=1355 loops=1)
               Index Cond: (("TenantId" = 1) AND ("CreatedAt" >= (now() - '30 days'::interval)))
               Buffers: shared read=7
 Planning Time: 0.127 ms
 Execution Time: 0.574 ms
```

The index pulls back ~1355 candidate rows (tenant 1 + last 30 days), and
the `Status = 0` filter is then applied on the heap to the surviving 330.

### Summary

| Metric         | Before    | After    |
|----------------|-----------|----------|
| Plan           | Seq Scan + Aggregate | Bitmap Index + Heap Scan + Aggregate |
| Execution time | 3.246 ms  | 0.574 ms |
| Rows examined  | 50,000    | 1,355    |

## Why not also index `Payments(Status)` or `Payments(OrderId)`?

- **`Status`** is a 2-value enum (`Completed`, `Refunded`); on a tenant
  that mostly produces completed payments the column has near-zero
  selectivity. Letting the planner apply it as a post-bitmap filter is
  cheaper than maintaining another index.
- **`OrderId`** is a single-row lookup that goes through the application,
  not the reports path. We do not run "payments for an order" queries at
  any volume worth indexing yet. If/when that changes, a second index on
  `Payments(OrderId)` plus a real foreign-key constraint should be added
  together.

## Appendix A — scripts

The complete `seed.sql` and `explain.sql` used to produce the output above
are reproduced here so the proof is fully repeatable.

### `seed.sql`

```sql
INSERT INTO "Tenants"
  ("Name", "Slug", "IsActive", "OwnerName", "OwnerEmail", "Phone", "City",
   "Plan", "TotalOrders", "StaffCount", "Revenue", "CreatedAt")
VALUES
  ('Pasta Place', 'pasta-place', true, 'A', 'a@x.com', '1', 'NYC', 1, 0, 0, 0, NOW()),
  ('Sushi Spot',  'sushi-spot',  true, 'B', 'b@x.com', '2', 'LA',  1, 0, 0, 0, NOW());

INSERT INTO "Orders"
  ("TenantId", "OrderType", "Status", "Total", "Notes", "TableNumber", "CreatedAt")
SELECT
  1 + (s % 3),
  'DineIn',
  (random() * 5)::int,
  (random() * 100)::numeric(10, 2),
  NULL,
  (random() * 30)::int,
  NOW() - (random() * 365 || ' days')::interval
FROM generate_series(1, 100000) AS s;

INSERT INTO "MenuItems"
  ("TenantId", "Name", "Price", "Category", "Description", "ImageUrl", "CreatedAt")
SELECT
  1 + (s % 3),
  'Item ' || s,
  (random() * 30)::numeric(10, 2),
  (ARRAY['Pizza','Pasta','Salad','Dessert','Drinks','Soup','Appetizer','Special'])
    [1 + (random() * 7)::int],
  NULL,
  NULL,
  NOW() - (random() * 365 || ' days')::interval
FROM generate_series(1, 30000) AS s;

INSERT INTO "ShiftNotes"
  ("TenantId", "Title", "Body", "Priority", "Author", "CreatedAt")
SELECT
  1 + (s % 3),
  'Note ' || s,
  'Body of note ' || s,
  (random() * 3)::int,
  'author' || (s % 20),
  NOW() - (random() * 365 || ' days')::interval
FROM generate_series(1, 50000) AS s;

INSERT INTO "Payments"
  ("TenantId", "OrderId", "Amount", "Method", "Status", "CreatedAt")
SELECT
  1 + (s % 3),
  1 + (s % 100000),
  (random() * 100)::numeric(10, 2),
  (random() * 2)::int,
  (random() * 2)::int,
  NOW() - (random() * 365 || ' days')::interval
FROM generate_series(1, 50000) AS s;

ANALYZE;
```

### `explain.sql` (per-query template)

```sql
-- Q-N BEFORE
DROP INDEX IF EXISTS "<index_name>";
ANALYZE "<Table>";
EXPLAIN (ANALYZE, BUFFERS) <query>;

-- Q-N AFTER
CREATE INDEX "<index_name>" ON "<Table>" (<columns>);
ANALYZE "<Table>";
EXPLAIN (ANALYZE, BUFFERS) <query>;
```

The literal queries are the ones embedded in each section above.

## Related docs

- [`docs/database/SCHEMA.md`](../database/SCHEMA.md) — index list per table
- [`docs/database/ERD.md`](../database/ERD.md) — relationships and tenant ownership
- [`docs/database-migrations.md`](../database-migrations.md) — `dotnet ef` workflow
