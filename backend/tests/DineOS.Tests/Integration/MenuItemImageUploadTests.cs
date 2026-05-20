using DineOS.Application.Common;
using DineOS.Application.DTOs;
using DineOS.Domain.Entities;
using DineOS.Infrastructure.Persistence;
using DineOS.Application.Authorization;
using DineOS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// Integration tests for POST /api/v1/menu/items/{id}/image.
/// Each test uses a unique tenant ID (901–908) to avoid inter-test state coupling.
/// MenuItem records are seeded directly via the factory's service scope.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class MenuItemImageUploadTests(CustomWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    // ── 1. Valid PNG → 200, ImageUrl persisted in DB, file exists on disk ────────
    [Fact]
    public async Task UploadImage_ValidPng_Returns200_AndPersistsImageUrl()
    {
        var itemId = await SeedMenuItemAsync("901");
        var client = ClientWith(Jwt(Roles.Manager, "901"));

        var response = await client.PostAsync(
            $"/api/v1/menu/items/{itemId}/image",
            ImageContent("photo.png", MinimalPng(), "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<MenuItemImageUploadDto>>(response);
        Assert.True(body!.Success);
        Assert.NotNull(body.Data?.ImageUrl);
        Assert.StartsWith("/uploads/menu-items/", body.Data.ImageUrl);

        // File must be present on disk at the resolved path
        Assert.True(File.Exists(UrlToFilePath(body.Data.ImageUrl)));

        // ImageUrl must be persisted to the DB record
        Assert.Equal(body.Data.ImageUrl, await GetMenuItemImageUrlAsync(itemId));
    }

    // ── 2. Invalid content type → 400 with UNSUPPORTED_CONTENT_TYPE ─────────────
    [Fact]
    public async Task UploadImage_InvalidContentType_Returns400_WithErrorCode()
    {
        var itemId = await SeedMenuItemAsync("902");
        var client = ClientWith(Jwt(Roles.Manager, "902"));

        var response = await client.PostAsync(
            $"/api/v1/menu/items/{itemId}/image",
            ImageContent("photo.png", new byte[] { 0x25, 0x50 }, "text/plain"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("UNSUPPORTED_CONTENT_TYPE", out _));
    }

    // ── 3. File too large → 400 with FILE_TOO_LARGE ──────────────────────────────
    [Fact]
    public async Task UploadImage_FileTooLarge_Returns400_WithErrorCode()
    {
        var itemId = await SeedMenuItemAsync("903");
        var client = ClientWith(Jwt(Roles.Manager, "903"));

        // 5 MB + 1 byte exceeds the default 5 MB limit
        var response = await client.PostAsync(
            $"/api/v1/menu/items/{itemId}/image",
            ImageContent("big.png", new byte[5_242_881], "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("FILE_TOO_LARGE", out _));
    }

    // ── 4. Empty file → 400 with FILE_EMPTY ─────────────────────────────────────
    [Fact]
    public async Task UploadImage_EmptyFile_Returns400_WithErrorCode()
    {
        var itemId = await SeedMenuItemAsync("904");
        var client = ClientWith(Jwt(Roles.Manager, "904"));

        var response = await client.PostAsync(
            $"/api/v1/menu/items/{itemId}/image",
            ImageContent("empty.png", Array.Empty<byte>(), "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty("FILE_EMPTY", out _));
    }

    // ── 5. Non-existent MenuItem id → 404 ───────────────────────────────────────
    [Fact]
    public async Task UploadImage_NonExistentItem_Returns404()
    {
        var client = ClientWith(Jwt(Roles.Manager, "905"));

        var response = await client.PostAsync(
            "/api/v1/menu/items/99999/image",
            ImageContent("photo.png", MinimalPng(), "image/png"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── 6. No JWT → 401 ─────────────────────────────────────────────────────────
    [Fact]
    public async Task UploadImage_NoToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync(
            "/api/v1/menu/items/1/image",
            ImageContent("photo.png", MinimalPng(), "image/png"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── 7. Cashier role → 403 (ManagerAndAbove policy) ──────────────────────────
    [Fact]
    public async Task UploadImage_CashierRole_Returns403()
    {
        var client = ClientWith(Jwt(Roles.Cashier, "907"));

        // Authorization policy fires before the action body — item need not exist
        var response = await client.PostAsync(
            "/api/v1/menu/items/1/image",
            ImageContent("photo.png", MinimalPng(), "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── 8. Path-traversal filename is sanitized — upload succeeds, file is safe ──
    [Fact]
    public async Task UploadImage_PathTraversalFilename_IsSanitizedAndStoredSafely()
    {
        var itemId = await SeedMenuItemAsync("908");
        var client = ClientWith(Jwt(Roles.Manager, "908"));

        // SaveAsync extracts only the extension via Path.GetExtension and generates a
        // UUID-based filename, so the traversal attempt in the original name is discarded.
        var response = await client.PostAsync(
            $"/api/v1/menu/items/{itemId}/image",
            ImageContent("../../evil.png", MinimalPng(), "image/png"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await DeserializeAsync<ApiResponse<MenuItemImageUploadDto>>(response);
        Assert.NotNull(body!.Data?.ImageUrl);

        var filePath = UrlToFilePath(body.Data.ImageUrl);
        Assert.True(File.Exists(filePath));

        // File must be inside the configured uploads root, not above it
        Assert.StartsWith(factory.UploadsRoot, filePath, StringComparison.Ordinal);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private async Task<long> SeedMenuItemAsync(string tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var item = new MenuItem
        {
            TenantId    = long.Parse(tenantId),
            Name        = $"Test Item {tenantId}",
            Price       = 9.99m,
            Category    = "Mains",
            Description = "Integration test item",
        };
        db.MenuItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    private async Task<string?> GetMenuItemImageUrlAsync(long itemId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var item = await db.MenuItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == itemId);
        return item?.ImageUrl;
    }

    private string UrlToFilePath(string imageUrl)
    {
        // imageUrl = "/uploads/menu-items/{guid}.ext"
        // Strip the "/uploads" base path, then resolve against the temp uploads root
        const string urlBase = "/uploads";
        var relative = imageUrl[urlBase.Length..].TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        return Path.Combine(factory.UploadsRoot, relative);
    }

    private static MultipartFormDataContent ImageContent(string fileName, byte[] bytes, string contentType)
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "image", fileName);
        return form;
    }

    private static byte[] MinimalPng() =>
        Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI6QAAAABJRU5ErkJggg==");

    private HttpClient ClientWith(string jwt)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    private static string Jwt(string role, string tenantId)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(CustomWebApplicationFactory.TestJwtSecret));

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim("sub",          $"test-{role.ToLower()}"),
                new Claim("email",        $"{role.ToLower()}@dineos.dev"),
                new Claim("tenant_id",    tenantId),
                new Claim("realm_access", JsonSerializer.Serialize(new { roles = new[] { role } }))
            ],
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(), JsonOpts);
}
