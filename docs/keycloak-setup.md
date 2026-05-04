# Keycloak Local Setup

Keycloak runs in Docker and auto-imports the `dineos` realm on first start.

## Start the stack

```bash
cd backend
docker compose up keycloak postgres
```

- **Admin UI**: http://localhost:8080 (admin / admin)
- **Realm**: `dineos`

The API picks up auth config from `appsettings.Development.json` when run with `dotnet run`:

```bash
cd backend/src/DineOS.Api
dotnet run
```

## Get a token

Use Direct Access Grant with the `dineos-frontend` public client:

```bash
curl -s -X POST http://localhost:8080/realms/dineos/protocol/openid-connect/token \
  -d "grant_type=password" \
  -d "client_id=dineos-frontend" \
  -d "username=admin@dineos.dev" \
  -d "password=Test1234!" \
  | jq -r '.access_token'
```

## Seeded users

| Email | Password | Role |
|-------|----------|------|
| admin@dineos.dev | Test1234! | SuperAdmin |
| manager@dineos.dev | Test1234! | Manager |
| cashier@dineos.dev | Test1234! | Cashier |
| kitchen@dineos.dev | Test1234! | KitchenStaff |

## Test a protected endpoint

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/realms/dineos/protocol/openid-connect/token \
  -d "grant_type=password&client_id=dineos-frontend&username=admin@dineos.dev&password=Test1234!" \
  | jq -r '.access_token')

# Who am I?
curl -H "Authorization: Bearer $TOKEN" http://localhost:5000/api/v1/me

# No token → 401
curl -v http://localhost:5000/api/v1/me
```

## Swagger UI

Open http://localhost:5000/swagger, click **Authorize**, paste the access token, then call any protected endpoint.

## Clients

| Client ID | Type | Purpose |
|-----------|------|---------|
| `dineos-api` | Bearer-only | Backend resource server |
| `dineos-frontend` | Public | SPA + local dev token fetching |
