using DineOS.Application.Common;
using DineOS.Tests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DineOS.Tests.Integration;

[Collection("IntegrationTests")]
public class AuthValidationIntegrationTests(CustomWebApplicationFactory factory)
{
    [Fact]
    public async Task PostLogin_EmptyCredentials_Returns400_WithValidationErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            username = "",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("Validation failed.", body.Message);
        Assert.NotNull(body.Errors);
        Assert.Contains(body.Errors!, e => e.Contains("Username"));
        Assert.Contains(body.Errors!, e => e.Contains("Password"));
    }

    [Fact]
    public async Task PostRefresh_EmptyToken_Returns400_WithValidationErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body!.Success);
        Assert.Equal("Validation failed.", body.Message);
        Assert.NotNull(body.Errors);
        Assert.Contains(body.Errors!, e => e.Contains("Refresh token"));
    }
}
