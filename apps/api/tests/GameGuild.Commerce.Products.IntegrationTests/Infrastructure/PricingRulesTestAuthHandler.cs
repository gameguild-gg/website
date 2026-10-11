using System.Security.Claims;
using System.Text.Encodings.Web;
using GameGuild.API.Database;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.Commerce.Products.IntegrationTests.Infrastructure;

/// <summary>
/// Test authentication handler for pricing-engine endpoint tests (modeled on the
/// Payments integration test suite): authenticates every request unless
/// <c>X-Test-Unauthenticated</c> is set, and accepts permissions via
/// <c>X-Test-Permissions</c> headers.
/// </summary>
public sealed class PricingRulesTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public static readonly Guid DefaultUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DefaultTenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.ContainsKey("X-Test-Unauthenticated"))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var subjectId = Request.Headers.TryGetValue("X-Test-Subject", out var subjectValues)
            ? subjectValues.ToString()
            : DefaultUserId.ToString();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, subjectId),
            new Claim(ClaimTypes.Name, "Pricing Rules Integration Test User"),
            new Claim("sub", subjectId),
        };

        if (!Request.Headers.ContainsKey("X-Test-No-Tenant"))
        {
            var tenantId = Request.Headers.TryGetValue("X-Test-Tenant", out var tenantValues)
                ? tenantValues.ToString()
                : DefaultTenantId.ToString();
            claims.Add(new Claim("tenant_id", tenantId));
        }

        foreach (var permission in ReadHeaderValues("X-Test-Permissions"))
        {
            claims.Add(new Claim("permission", permission));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private IEnumerable<string> ReadHeaderValues(string headerName)
    {
        if (!Request.Headers.TryGetValue(headerName, out var values))
        {
            return [];
        }

        return values
            .SelectMany(value => value?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value));
    }
}

/// <summary>
/// Tenant membership/seed helpers for the pricing-engine endpoint tests.
/// </summary>
internal static class PricingRulesTestTenantServices
{
    public static void AddDefaultTenantMembership(this IServiceCollection services)
    {
        var members = new Mock<ITenantMemberRepository>();
        members
            .Setup(repository => repository.GetByUserAndTenantAsync(
                It.IsAny<Guid>(),
                PricingRulesTestAuthHandler.DefaultTenantId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid userId, Guid tenantId, CancellationToken _) => new TenantMember
            {
                UserId = userId,
                TenantId = tenantId,
                Role = "Member",
                IsActive = true
            });

        services.RemoveAll<ITenantMemberRepository>();
        services.AddSingleton<ITenantMemberRepository>(members.Object);
    }

    public static void SeedDefaultTenant(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (context.Set<Tenant>().Any(tenant => tenant.Id == PricingRulesTestAuthHandler.DefaultTenantId))
        {
            return;
        }

        context.Set<Tenant>().Add(new Tenant
        {
            Id = PricingRulesTestAuthHandler.DefaultTenantId,
            Name = "Pricing Rules Integration Test Tenant",
            Slug = "pricing-rules-integration-test",
            AdminEmail = "pricing-rules-admin@example.test",
            IsDefault = true,
            IsActive = true
        });
        context.SaveChanges();
    }
}
