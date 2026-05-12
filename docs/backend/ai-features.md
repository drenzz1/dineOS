# AI Features — Anthropic-backed menu assistant (M3.10)

## What ships

| Endpoint | Method | Role | Rate limit | Description |
|---|---|---|---|---|
| `/api/v1/ai/menu-items/{id}/describe` | POST | `ManagerAndAbove` | `ai-expensive` (10/min) | Generates a customer-facing description + likely-allergen list for an existing menu item. Returns a **suggestion only** — the caller decides whether to persist it. |

The response shape is `ApiResponse<MenuItemDescriptionSuggestionDto>`:

```jsonc
{
  "success": true,
  "data": {
    "menuItemId": 42,
    "itemName": "Margherita Pizza",
    "category": "Pizza",
    "suggestedDescription": "Wood-fired pizza with tomato, mozzarella, and basil.",
    "suggestedAllergens": ["gluten", "dairy"],
    "metadata": {
      "model": "claude-sonnet-4-6",
      "inputTokens": 130,
      "outputTokens": 42,
      "latencyMs": 870
    }
  }
}
```

## Provider choice — Anthropic

- **Why**: aligns with the AI tooling already used by the team; tool-use API is well-suited to a structured response with a forced schema, which lets us avoid fragile text parsing.
- **Model**: `claude-sonnet-4-6` — most capable Sonnet at the cut-off used by the team.
- **No SDK dependency**: the integration uses a plain `HttpClient` so we don't pin to a community-maintained SDK and keep the dependency surface small.

## Architecture

```
AiController ──► IAiMenuService ──► IAiClient ──► Anthropic Messages API
                                       │
                                       └── AnthropicAiClient (HttpClient)
```

- `IAiClient` is the only seam that knows about the provider. The controller and application service depend on it through interfaces, so tests can substitute a fake without touching HTTP.
- `AnthropicAiClient` posts to `/v1/messages` with a forced `tool_use` for the `report_menu_description` tool. The model returns its answer as the tool's structured `input`, which we then parse into our DTO.
- Failure modes (`HttpRequestException`, `TaskCanceledException`, non-2xx status, missing/empty content) all collapse to `AiUnavailableException`. `AiMenuService` catches that and returns `ServiceResult.UnprocessableEntity(...)` — the caller sees `422` with a human-readable fallback message and the client can prompt the user to write the description manually.

## Configuration

Settings live under the `Anthropic` section. **Never commit the API key** — set it via environment variable or user secrets.

```bash
# user-secrets (per-developer, recommended for local dev)
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." \
  --project backend/src/DineOS.Api

# or via env var (Docker / CI)
ASPNETCORE_Anthropic__ApiKey=sk-ant-...
```

| Key | Default | Purpose |
|---|---|---|
| `Anthropic:ApiKey` | `""` | Provider API key. Empty key triggers a clean `AiUnavailable` fallback rather than a 5xx. |
| `Anthropic:Model` | `claude-sonnet-4-6` | Model id. |
| `Anthropic:BaseUrl` | `https://api.anthropic.com` | Override only for testing/staging. |
| `Anthropic:ApiVersion` | `2023-06-01` | Sent as `anthropic-version` header. |
| `Anthropic:MaxTokens` | `400` | Per-request output cap. |
| `Anthropic:TimeoutSeconds` | `20` | Hard timeout per request. |

The `ai-expensive` rate-limit policy is defined in `Program.cs` — fixed-window **10 requests/min** with no queue, applied at controller scope.

## Cost & safety guardrails

- **Per-tenant cap** via rate limit (10 req/min/user). A single tenant can't run up the bill.
- **Output cap** via `MaxTokens` (default 400).
- **Hard timeout** via `TimeoutSeconds` (default 20). Slow upstream → fallback.
- **No prompt content in logs** — only metadata: menu item id, model, input/output tokens, latency. Reviewers can audit cost without seeing user copy.
- **Tenant-scoped data only** — the application service relies on EF's tenant query filter, so an item from another tenant returns `404` and never reaches the provider.
- **`ManagerAndAbove` role** required — cashiers and kitchen staff can't call the endpoint.

## Demo (reviewer steps)

```bash
# 1. Set your key (one-off)
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." \
  --project backend/src/DineOS.Api

# 2. Run the stack
cd backend && docker compose up -d
dotnet run --project src/DineOS.Api

# 3. Get a menu item id (seeded data has Demo Restaurant)
curl -H "Authorization: Bearer <manager-jwt>" \
     http://localhost:5000/api/v1/menu/items | jq '.data[0]'

# 4. Ask for an AI description
curl -X POST \
     -H "Authorization: Bearer <manager-jwt>" \
     http://localhost:5000/api/v1/ai/menu-items/<id>/describe | jq .
```

To exercise the **fallback path** without breaking your key, point `Anthropic:BaseUrl` at a closed port:

```bash
ASPNETCORE_Anthropic__BaseUrl=http://localhost:1 dotnet run --project src/DineOS.Api
# → POST .../describe returns 422 with "AI assistant is temporarily unavailable..."
```

## Tests

- `AiMenuServiceTests` — happy path, 404 on unknown id, 422 on `AiUnavailableException`, tenant isolation (foreign tenant item returns 404, not leaked existence).
- `AnthropicAiClientTests` — parses tool-use response, throws `AiUnavailableException` on missing key / HTTP error / empty description; verifies request body forces the tool.

All tests mock the provider — none make real network calls.
