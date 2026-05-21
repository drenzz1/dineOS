using DineOS.Domain.Entities;
using DineOS.Domain.Enums;
using DineOS.Infrastructure.Persistence;
using DineOS.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace DineOS.Tests.Integration;

/// <summary>
/// End-to-end coverage for the public demo access endpoint (#216). The job
/// graph itself is verified in <c>DemoAccessServiceTests</c>; here we only
/// assert that the HTTP surface persists the right row and short-circuits
/// on the feature flag — no Keycloak / SMTP touched.
/// </summary>
[Collection("IntegrationTests")]
[Trait("Category", "Integration")]
public class DemoAccessEndpointTests(CustomWebApplicationFactory factory)
{
    private const string Endpoint = "/api/v1/demo/request";

    [Fact]
    public async Task Post_NewEmail_Returns202AndPersistsPendingRow()
    {
        var client = factory.CreateClient();
        var email = $"demo-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            email,
            acceptedTerms = true,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.DemoUsers
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Email == email);

        Assert.Equal(DemoUserStatus.Pending, row.Status);
        Assert.True(row.ExpiresAt > DateTime.UtcNow.AddDays(6));
    }

    [Fact]
    public async Task Post_HoneypotFilled_Returns202AndDoesNotPersist()
    {
        var client = factory.CreateClient();
        var email = $"bot-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            email,
            acceptedTerms = true,
            companyName   = "Acme Bots Ltd",
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.DemoUsers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(d => d.Email == email);

        Assert.Null(row);
    }

    [Fact]
    public async Task Post_InvalidEmail_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            email         = "not-an-email",
            acceptedTerms = true,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_TermsNotAccepted_Returns400()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            email         = $"tos-{Guid.NewGuid():N}@example.com",
            acceptedTerms = false,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_RepeatedActiveEmail_ReturnsAcceptedAndUpdatesRow()
    {
        var client = factory.CreateClient();
        var email = $"reuse-{Guid.NewGuid():N}@example.com";

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.DemoUsers.Add(new DemoUser
            {
                Email          = email,
                KeycloakUserId = "kc-existing",
                Status         = DemoUserStatus.Active,
                RequestedAt    = DateTime.UtcNow.AddDays(-1),
                ExpiresAt      = DateTime.UtcNow.AddDays(5),
                CreatedAt      = DateTime.UtcNow.AddDays(-1),
            });
            await db.SaveChangesAsync();
        }

        var response = await client.PostAsJsonAsync(Endpoint, new
        {
            email,
            acceptedTerms = true,
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await verifyDb.DemoUsers
            .IgnoreQueryFilters()
            .SingleAsync(d => d.Email == email);

        // Still active, KC user id preserved — resend branch should not blow it away.
        Assert.Equal(DemoUserStatus.Active, row.Status);
        Assert.Equal("kc-existing", row.KeycloakUserId);
    }
}
