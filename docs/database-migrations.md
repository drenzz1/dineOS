# Database Migrations — Dev Workflow

## Prerequisites

- Docker running (for local Postgres)
- `dotnet-ef` tool installed: `dotnet tool install --global dotnet-ef`

## Start the database

```bash
cd backend
docker compose up postgres -d
```

## Apply all pending migrations

```bash
dotnet ef database update \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api
```

The API also auto-applies migrations on startup via `db.Database.Migrate()` in `Program.cs`.

## Add a new migration

After modifying an entity or adding a new one, create a migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api \
  --output-dir Persistence/Migrations
```

## Roll back a migration

```bash
# Roll back to a specific migration by name
dotnet ef database update <PreviousMigrationName> \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api

# Remove the last migration file (if not yet applied to DB)
dotnet ef migrations remove \
  --project src/DineOS.Infrastructure \
  --startup-project src/DineOS.Api
```

## Seed data

The `InitialCreate` migration seeds a demo tenant:

| Id | Name | Slug |
|----|------|------|
| 1 | Demo Restaurant | demo-restaurant |

Additional seed data should be added via `SeedData()` in `AppDbContext.cs`.

## Connection string

Local dev uses the connection string in `appsettings.json`:

```
Host=localhost;Port=5432;Database=dineos;Username=dineos;Password=dineos_dev
```

pgAdmin is available at `http://localhost:5050` (start with `docker compose --profile tools up`).

---

## Migration strategy

The sections above are the *mechanics*. This section is the *policy* — how
migrations are written, applied, and rolled back across environments.

### Source of truth & how schema changes happen

- The **EF Core model is the source of truth.** You never hand-write DDL against
  the database; you change an entity / `AppDbContext` configuration and scaffold
  a migration (`dotnet ef migrations add`). The generated
  `AppDbContextModelSnapshot.cs` is the canonical picture of the current schema.
- Every schema change ships as a **migration committed to the repo**, reviewed in
  the PR alongside the code that depends on it. The `helm.yml` / backend CI
  builds the project (which compiles the migrations); a migration that doesn't
  build fails CI.

### How migrations are applied per environment

| Environment | How migrations apply |
|-------------|----------------------|
| Local dev | `dotnet ef database update`, **or** automatically when the API boots — `Program.cs` calls `IDatabaseMigrator.MigrateAsync()` (`db.Database.Migrate()`) on startup. |
| project-06 / production (Kubernetes) | **Auto-applied on startup.** Each API pod runs `MigrateAsync()` before serving traffic, so a `helm upgrade` that rolls out a new image applies any pending migrations. EF takes a PostgreSQL advisory lock, so with multiple replicas only one pod migrates; the others wait. |

> **Implication:** because production migrates on deploy, a migration must be
> safe to run against live data **and** backward-compatible with the *previous*
> image for the duration of a rolling update (old and new pods run briefly at
> once). Prefer additive changes; stage destructive ones (see below).

### Forward-only, but every `Down` is real

We roll **forward** in production (fix-forward with a new migration) rather than
running `Down` against prod data. However, every migration still implements a
correct, data-preserving `Down()` so that:

- a developer can step back locally (`dotnet ef database update <previous>`), and
- `helm upgrade --atomic` rollbacks pair with a deliberate down-migration if ever needed.

### Data-preserving / zero-downtime pattern (expand → backfill → contract)

Destructive or transforming changes are split so no step loses data or breaks the
running app. The M5.3 `NormalizeMenuCategoryFkAndDropTenantAggregates` migration
is the worked example for `MenuItem.Category` (text) → `CategoryId` (FK):

1. **Expand** — add the new `CategoryId` column as **nullable** (no constraint yet).
2. **Backfill** — raw SQL creates a `MenuCategory` for each distinct existing
   category name and sets `CategoryId`; a fallback buckets any blank category
   under `Uncategorized` so the next step can't fail.
3. **Contract** — make `CategoryId` `NOT NULL`, add the FK + indexes, and drop the
   old `Category` column.

The `Down()` reverses it just as carefully (re-add `Category` text, backfill the
name from `MenuCategories`, then drop `CategoryId`).

EF does **not** scaffold data backfills — after `migrations add`, hand-edit the
generated file to insert `migrationBuilder.Sql(...)` between the expand and
contract operations, then review the emitted SQL with:

```bash
dotnet ef migrations script <fromMigration> <toMigration> --idempotent \
  --project src/DineOS.Infrastructure --startup-project src/DineOS.Api
```

### Conventions

- **Naming:** `PascalCase`, verb-first, describing the change
  (`AddOrderItems`, `NormalizeMenuCategoryFkAndDropTenantAggregates`). EF prefixes
  a UTC timestamp for ordering.
- **One concern per migration** where practical; never edit a migration that has
  already been applied to any shared environment — add a new one.
- **Review the scaffold.** Watch for EF's *"An operation was scaffolded that may
  result in the loss of data"* warning — it means a column/table is being dropped
  and you must confirm the data is either gone-on-purpose or backfilled first.
- **Seed data** goes through `modelBuilder.*.HasData(...)` (model-tracked seeding),
  not ad-hoc `INSERT`s, so it participates in the snapshot and migrations.
