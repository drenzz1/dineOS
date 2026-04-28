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
