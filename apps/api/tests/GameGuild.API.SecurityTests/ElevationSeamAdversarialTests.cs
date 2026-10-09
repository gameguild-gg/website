using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GameGuild.API.Database;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (g): elevation via JIT and impersonation seams
///     (issue #327). The just-in-time elevation surface lets an authenticated user ask
///     for a temporary permission and names a reviewer — and the reviewer identity
///     arrives in the request body. The attack under test: a user who both requests the
///     elevation and approves it, either naming themselves (blocked by the entity) or
///     impersonating another reviewer id (spoofed attribution). The deny-by-default
///     invariant under test: NO path through these seams may leave the attacker holding
///     an effective permission they were never granted.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class ElevationSeamAdversarialTests(AdversarialSecurityFixture fixture)
{
    private const string RequestEndpoint = "/v1/jit-elevations";

    [Fact]
    public async Task SelfApprovalOfOwnElevationRequestIsRejectedAndConfersNothing()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(account);
        var requestId = await RequestElevationAsync(client, account);

        var approveBody = JsonSerializer.Serialize(new
        {
            reviewerId = account.User.Id,
            comments = "adversarial self-approval",
        });
        using var approve = await client.PostAsync(
            $"{RequestEndpoint}/{requestId}:approve",
            new StringContent(approveBody, Encoding.UTF8, "application/json"));

        Assert.NotEqual(HttpStatusCode.OK, approve.StatusCode);
        await AssertElevationNotInForceAsync(account, requestId);
    }

    [Fact]
    public async Task ApprovalWithSpoofedReviewerIdentityConfersNoEffectivePermission()
    {
        // The reviewer id arrives in the request body, so an attacker can name any
        // reviewer they like. Even if the request row is transitioned this way, the
        // attacker must not end up holding the requested permission.
        var account = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(account);
        var requestId = await RequestElevationAsync(client, account);

        var spoofedReviewer = Guid.NewGuid();
        var approveBody = JsonSerializer.Serialize(new
        {
            reviewerId = spoofedReviewer,
            comments = "adversarial reviewer impersonation",
        });
        using var approve = await client.PostAsync(
            $"{RequestEndpoint}/{requestId}:approve",
            new StringContent(approveBody, Encoding.UTF8, "application/json"));

        // Whatever the endpoint answers, the security invariant is the effective
        // permission state: the requested capability must not be usable.
        await AssertElevationNotInForceAsync(account, requestId);
    }

    [Fact]
    public async Task ElevationRequestForgedForAnotherUserConfersNothingOnEitherSide()
    {
        // An attacker names a different requester id in the body. Either the request is
        // rejected or it is recorded against that other user — but the attacker must not
        // obtain anything, and the named victim must not silently end up elevated.
        var attacker = await fixture.SeedAccountAsync(membershipRole: "Member");
        var victim = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(attacker);

        var body = JsonSerializer.Serialize(new
        {
            requesterId = victim.User.Id,
            tenantId = victim.TenantId,
            permission = "features:read",
            justification = "adversarial request forgery",
            durationMinutes = 30,
        });
        using var response = await client.PostAsync(
            RequestEndpoint, new StringContent(body, Encoding.UTF8, "application/json"));

        // Security property: neither party may hold an in-force elevation afterwards.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var elevations = await db.Set<JitElevationRequest>().AsNoTracking()
                .Where(request => request.RequesterId == attacker.User.Id || request.RequesterId == victim.User.Id)
                .ToListAsync();
            Assert.DoesNotContain(elevations, elevation => elevation.IsGrantInForce());
        }

        using var victimClient = await fixture.CreateBearerClientAsync(victim);
        using var victimFeatures = await victimClient.GetAsync("/v1/features");
        Assert.True(victimFeatures.StatusCode is HttpStatusCode.Forbidden,
            $"forged requester id must not elevate the victim, got {(int)victimFeatures.StatusCode}");
    }

    [Fact]
    public async Task DelegatedAdminScopeCannotBeSelfAssignedByAMember()
    {
        var account = await fixture.SeedAccountAsync(membershipRole: "Member");
        using var client = await fixture.CreateBearerClientAsync(account);

        var body = JsonSerializer.Serialize(new
        {
            adminUserId = account.User.Id,
            tenantId = account.TenantId,
            name = "adversarial self-assigned scope",
            description = "created by the adversarial validation suite",
            managedResourceTypes = new[] { "FeatureFlag" },
            managedUserIds = new[] { account.User.Id },
            // Required by GrantDelegatedAdminValidator; without it the request dies in
            // input validation (400) before the authorization guard is ever exercised.
            allowedOperations = new[] { "read" },
        });
        using var response = await client.PostAsync(
            "/v1/delegated-admin",
            new StringContent(body, Encoding.UTF8, "application/json"));

        // A member must not be able to mint a delegated-admin scope for themselves.
        // 500 is the fail-closed generic handler for the UnauthorizedAccessException guard
        // (see findings report); the assertion still requires a non-success denial.
        Assert.True(response.StatusCode
                is HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden
                or HttpStatusCode.InternalServerError,
            $"self-assigned delegated-admin scope must be denied, got {(int)response.StatusCode}");
    }

    private static async Task<Guid> RequestElevationAsync(HttpClient client, AdversarialAccount account)
    {
        var body = JsonSerializer.Serialize(new
        {
            requesterId = account.User.Id,
            tenantId = account.TenantId,
            permission = "features:read",
            justification = "adversarial elevation probe for deny-by-default validation",
            durationMinutes = 30,
        });
        using var response = await client.PostAsync(
            RequestEndpoint, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode,
            $"elevation request should be recordable for a member, got {(int)response.StatusCode}");
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    private async Task AssertElevationNotInForceAsync(AdversarialAccount account, Guid requestId)
    {
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var stored = await db.Set<JitElevationRequest>().AsNoTracking()
                .SingleOrDefaultAsync(request => request.Id == requestId);
            Assert.NotNull(stored);
            Assert.True(!stored.IsGrantInForce(),
                $"elevation {requestId} must never be in force for the attacker (status: {stored.Status})");
        }

        using var client = await fixture.CreateBearerClientAsync(account);
        using var features = await client.GetAsync("/v1/features");
        Assert.True(features.StatusCode is HttpStatusCode.Forbidden,
            $"attacker must not hold features:read after the elevation seam attack, got {(int)features.StatusCode}");
    }
}
