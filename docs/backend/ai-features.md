# AI Features — provider-backed menu assistant (M3.10)

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
      "model": "claude-sonnet-4-5",
      "inputTokens": 130,
      "outputTokens": 42,
      "latencyMs": 870
    }
  }
}
```

## Provider choice

- **Default**: Anthropic, because that was the first provider implemented for M3.10.
- **Other providers**: set `Ai:Provider` to `OpenAI` or `Google` and configure that provider's key/model section.
- **Model**: defaults to `claude-sonnet-4-5` for Anthropic, `gpt-4o-mini` for OpenAI, and `gemini-2.5-flash` for Google. Override the selected provider's `Model` key to use any model supported by that provider account and API version.
- **No SDK dependency**: the integration uses a plain `HttpClient` so we don't pin to a community-maintained SDK and keep the dependency surface small.

## Architecture

```
AiController ──► IAiMenuService ──► IAiClient ──► selected provider API
                                       │
                                       ├── AnthropicAiClient (HttpClient)
                                       ├── OpenAiClient (HttpClient)
                                       └── GoogleAiClient (HttpClient)
```

- `IAiClient` is the only interface the controller and application service depend on. `AddInfrastructure()` resolves the concrete provider from `Ai:Provider`.
- `AnthropicAiClient` posts to `/v1/messages` with a forced `tool_use` for the `report_menu_description` tool. OpenAI and Google use JSON response prompts against their content-generation APIs and are parsed into the same DTO.
- Failure modes (`HttpRequestException`, `TaskCanceledException`, non-2xx status, missing/empty content) all collapse to `AiUnavailableException`. `AiMenuService` catches that and returns `ServiceResult.UnprocessableEntity(...)` — the caller sees `422` with a human-readable fallback message and the client can prompt the user to write the description manually.

## Configuration

Select the provider with `Ai:Provider`. Provider settings live under `Anthropic`, `OpenAI`, or `GoogleAI`. **Never commit API keys** — set them via environment variable or user secrets.

```bash
# user-secrets (per-developer, recommended for local dev)
dotnet user-secrets set "Anthropic:ApiKey" "sk-ant-..." \
  --project backend/src/DineOS.Api
dotnet user-secrets set "Ai:Provider" "Anthropic" \
  --project backend/src/DineOS.Api

# or via env var (Docker / CI)
ASPNETCORE_Ai__Provider=Anthropic
ASPNETCORE_Anthropic__ApiKey=sk-ant-...
ASPNETCORE_Anthropic__Model=claude-sonnet-4-5
```

| Key | Default | Purpose |
|---|---|---|
| `Ai:Provider` | `Anthropic` | Active provider. Supported values: `Anthropic`, `OpenAI`, `Google`. |
| `Anthropic:ApiKey` | `""` | Provider API key. Empty key triggers a clean `AiUnavailable` fallback rather than a 5xx. |
| `Anthropic:Model` | `claude-sonnet-4-5` | Anthropic model id. The app validates only that this is present, so newly released model ids can be used through configuration without code changes. |
| `Anthropic:BaseUrl` | `https://api.anthropic.com` | Override only for testing/staging. |
| `Anthropic:ApiVersion` | `2023-06-01` | Sent as `anthropic-version` header. |
| `Anthropic:MaxTokens` | `400` | Per-request output cap. |
| `Anthropic:TimeoutSeconds` | `20` | Hard timeout per request. |
| `OpenAI:ApiKey` | `""` | OpenAI API key, used when `Ai:Provider=OpenAI`. |
| `OpenAI:Model` | `gpt-4o-mini` | OpenAI model id. |
| `OpenAI:BaseUrl` | `https://api.openai.com` | OpenAI-compatible base URL. |
| `GoogleAI:ApiKey` | `""` | Gemini API key, used when `Ai:Provider=Google`. |
| `GoogleAI:Model` | `gemini-2.5-flash` | Gemini model id. |
| `GoogleAI:BaseUrl` | `https://generativelanguage.googleapis.com` | Gemini API base URL. |
| `GoogleAI:ApiVersion` | `v1beta` | Gemini REST API version path. |

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
- `OpenAiClientTests` / `GoogleAiClientTests` — verify provider-specific auth headers, model wiring, and JSON response parsing.
- `AiProviderRegistrationTests` — verifies `Ai:Provider` resolves the expected `IAiClient` implementation.

All tests mock the provider — none make real network calls.
