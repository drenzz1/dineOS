using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Application.Interfaces.Services;
using DineOS.Application.Options;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DineOS.Infrastructure.Services;

public sealed class AiSettingsService(
    AppDbContext db,
    IMemoryCache cache,
    IHttpClientFactory httpFactory,
    IOptions<AnthropicOptions> anthropicOptions,
    IOptions<OpenAiOptions> openAiOptions,
    IOptions<GoogleAiOptions> googleAiOptions) : IAiSettingsService
{
    private const string CacheKey = "platform:ai-settings";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public (string Provider, string ApiKey)? GetEffectiveSettings()
    {
        if (cache.TryGetValue<PlatformAiSettings>(CacheKey, out var cached) && cached is not null)
            return ToEffective(cached);

        // Synchronous DB read — acceptable in DI factory context (single fast query per scope).
        var row = db.PlatformAiSettings.AsNoTracking().IgnoreQueryFilters().FirstOrDefault();
        if (row is null) return null;

        cache.Set(CacheKey, row, CacheTtl);
        return ToEffective(row);
    }

    public async Task<AiSettingsDto> GetAsync(CancellationToken ct = default)
    {
        var row = await db.PlatformAiSettings.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(ct);
        return row is null
            ? new AiSettingsDto("Anthropic", null, null, null, null)
            : ToDto(row);
    }

    public async Task<ServiceResult<AiSettingsDto>> SaveAsync(
        SaveAiSettingsRequest request,
        CancellationToken ct = default)
    {
        if (!IsValidProvider(request.Provider))
            return ServiceResult<AiSettingsDto>.BadRequest($"Unknown provider '{request.Provider}'.");

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return ServiceResult<AiSettingsDto>.BadRequest("API key cannot be empty.");

        var row = await db.PlatformAiSettings.IgnoreQueryFilters().FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new PlatformAiSettings();
            db.PlatformAiSettings.Add(row);
        }

        row.ActiveProvider = request.Provider;
        row.UpdatedAt = DateTime.UtcNow;

        switch (request.Provider)
        {
            case "Anthropic": row.AnthropicApiKey = request.ApiKey; break;
            case "OpenAI":    row.OpenAiApiKey    = request.ApiKey; break;
            case "Google":    row.GoogleAiApiKey  = request.ApiKey; break;
        }

        await db.SaveChangesAsync(ct);
        cache.Remove(CacheKey);

        return ServiceResult<AiSettingsDto>.Ok(ToDto(row), "AI settings saved.");
    }

    public async Task<ServiceResult<TestAiConnectionResult>> TestConnectionAsync(
        TestAiConnectionRequest request,
        CancellationToken ct = default)
    {
        if (!IsValidProvider(request.Provider))
            return ServiceResult<TestAiConnectionResult>.BadRequest($"Unknown provider '{request.Provider}'.");

        if (string.IsNullOrWhiteSpace(request.ApiKey))
            return ServiceResult<TestAiConnectionResult>.BadRequest("API key cannot be empty.");

        try
        {
            var result = request.Provider switch
            {
                "OpenAI" => await TestOpenAiAsync(request.ApiKey, ct),
                "Google" => await TestGoogleAsync(request.ApiKey, ct),
                _        => await TestAnthropicAsync(request.ApiKey, ct),
            };
            return ServiceResult<TestAiConnectionResult>.Ok(result);
        }
        catch (Exception ex)
        {
            return ServiceResult<TestAiConnectionResult>.Ok(
                new TestAiConnectionResult(false, ex.Message, null));
        }
    }

    // ── Test helpers ──────────────────────────────────────────────────────

    private async Task<TestAiConnectionResult> TestAnthropicAsync(string apiKey, CancellationToken ct)
    {
        var opts = anthropicOptions.Value;
        using var client = httpFactory.CreateClient();
        client.BaseAddress  = new Uri(opts.BaseUrl);
        client.Timeout      = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("anthropic-version", opts.ApiVersion);
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var body = new
        {
            model      = opts.Model,
            max_tokens = 1,
            messages   = new[] { new { role = "user", content = "test" } },
        };

        using var response = await client.PostAsJsonAsync("/v1/messages", body, ct);

        if (response.IsSuccessStatusCode)
            return new TestAiConnectionResult(true, null, opts.Model);

        var snippet = await response.Content.ReadAsStringAsync(ct);
        var error = (int)response.StatusCode is 401 or 403
            ? "Invalid API key."
            : $"Anthropic returned HTTP {(int)response.StatusCode}: {Truncate(snippet, 200)}";

        return new TestAiConnectionResult(false, error, null);
    }

    private async Task<TestAiConnectionResult> TestOpenAiAsync(string apiKey, CancellationToken ct)
    {
        var opts = openAiOptions.Value;
        using var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(opts.BaseUrl);
        client.Timeout     = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var body = new
        {
            model      = opts.Model,
            max_tokens = 1,
            messages   = new[] { new { role = "user", content = "test" } },
        };

        using var response = await client.PostAsJsonAsync("/v1/chat/completions", body, ct);

        if (response.IsSuccessStatusCode)
            return new TestAiConnectionResult(true, null, opts.Model);

        var snippet = await response.Content.ReadAsStringAsync(ct);
        var error = (int)response.StatusCode is 401 or 403
            ? "Invalid API key."
            : $"OpenAI returned HTTP {(int)response.StatusCode}: {Truncate(snippet, 200)}";

        return new TestAiConnectionResult(false, error, null);
    }

    private async Task<TestAiConnectionResult> TestGoogleAsync(string apiKey, CancellationToken ct)
    {
        var opts = googleAiOptions.Value;
        using var client = httpFactory.CreateClient();
        client.BaseAddress = new Uri(opts.BaseUrl);
        client.Timeout     = TimeSpan.FromSeconds(10);
        client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);

        var version = opts.ApiVersion.Trim('/');
        var model = opts.Model.StartsWith("models/", StringComparison.Ordinal)
            ? opts.Model : $"models/{opts.Model}";
        var path = $"/{version}/{model}:generateContent";

        var body = new
        {
            contents         = new[] { new { role = "user", parts = new[] { new { text = "test" } } } },
            generation_config = new { max_output_tokens = 1 },
        };

        using var response = await client.PostAsJsonAsync(path, body, ct);

        if (response.IsSuccessStatusCode)
            return new TestAiConnectionResult(true, null, opts.Model);

        var snippet = await response.Content.ReadAsStringAsync(ct);
        var error = (int)response.StatusCode is 401 or 403 or 400
            ? "Invalid API key."
            : $"Google AI returned HTTP {(int)response.StatusCode}: {Truncate(snippet, 200)}";

        return new TestAiConnectionResult(false, error, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static (string Provider, string ApiKey)? ToEffective(PlatformAiSettings row)
    {
        var key = row.ActiveProvider switch
        {
            "OpenAI" => row.OpenAiApiKey,
            "Google" => row.GoogleAiApiKey,
            _        => row.AnthropicApiKey,
        };
        return string.IsNullOrWhiteSpace(key) ? null : (row.ActiveProvider, key);
    }

    private static AiSettingsDto ToDto(PlatformAiSettings row) => new(
        row.ActiveProvider,
        MaskKey(row.AnthropicApiKey),
        MaskKey(row.OpenAiApiKey),
        MaskKey(row.GoogleAiApiKey),
        row.UpdatedAt);

    private static string? MaskKey(string key) =>
        string.IsNullOrWhiteSpace(key) ? null
        : key.Length <= 8 ? new string('*', key.Length)
        : key[..8] + new string('*', 12);

    private static bool IsValidProvider(string p) =>
        p is "Anthropic" or "OpenAI" or "Google";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];
}
