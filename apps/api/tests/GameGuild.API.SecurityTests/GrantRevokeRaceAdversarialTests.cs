using System.Net;
using GameGuild.API.SecurityTests.Infrastructure;
using GameGuild.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.SecurityTests;

/// <summary>
///     Adversarial scenario family (e): TOCTOU race between permission revocation and
///     in-flight and subsequent requests (issue #327). While requests are in flight, the
///     grant is revoked through the production permission-mutation service (the same
///     guarded, versioned, audited path the admin endpoint dispatches to). Once revocation
///     is committed there must be no stale-allow window: every subsequent request is
///     denied, even a concurrent burst, and no request ever fails open through a server error.
/// </summary>
[Collection(AdversarialSecurityCollection.Name)]
public sealed class GrantRevokeRaceAdversarialTests(AdversarialSecurityFixture fixture)
{
    private const int BurstSize = 32;
    private const string FeaturesProbe = "/v1/features";

    [Fact]
    public async Task RevocationRacingInFlightRequestsNeverLeavesAStaleAllowWindow()
    {
        var account = await fixture.SeedAccountAsync(grantedPermissions: ["features:read"]);
        using var client = await fixture.CreateBearerClientAsync(account);

        // Positive control and cache warm-up: the grant authorizes the read.
        for (var i = 0; i < 3; i++)
        {
            using var warmup = await client.GetAsync(FeaturesProbe);
            Assert.Equal(HttpStatusCode.OK, warmup.StatusCode);
        }

        // Fire a burst while revoking the grant through the real mutation service.
        var inFlight = Enumerable.Range(0, BurstSize)
            .Select(_ => client.GetAsync(FeaturesProbe))
            .ToList();

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<IPermissionGrantService>();
            var revoked = await grantService.RevokeTenantPermissionAsync(
                account.User.Id,
                account.TenantId,
                ["features:read"]);
            Assert.True(revoked, "the production revoke path must report the revocation");
        }

        var inFlightStatuses = new List<HttpStatusCode>();
        foreach (var task in inFlight)
        {
            using var response = await task;
            inFlightStatuses.Add(response.StatusCode);
        }

        // Requests that raced the revocation may be allowed (evaluated earlier) or denied,
        // but they must never observe a server failure — deny-by-default fails closed even
        // under concurrency.
        Assert.DoesNotContain(inFlightStatuses, status => (int)status >= 500);
        Assert.Contains(inFlightStatuses, status => status is HttpStatusCode.OK or HttpStatusCode.Forbidden);

        // After revocation is durably committed there is no stale-allow window, even for a
        // fully concurrent burst.
        var postRevoke = Enumerable.Range(0, BurstSize)
            .Select(_ => client.GetAsync(FeaturesProbe))
            .ToList();
        var postRevokeStatuses = new List<HttpStatusCode>();
        foreach (var task in postRevoke)
        {
            using var response = await task;
            postRevokeStatuses.Add(response.StatusCode);
        }

        Assert.All(postRevokeStatuses, status =>
            Assert.True(status is HttpStatusCode.Forbidden,
                $"post-revocation request must be denied with 403, got {(int)status}"));
    }

    [Fact]
    public async Task GrantRacingRequestsNeverDeniesACommittedGrant()
    {
        // Control experiment in the opposite direction: once a grant is committed through
        // the production service, requests are allowed — proving the denials above come
        // from the revocation, not from a broken evaluation path.
        var account = await fixture.SeedAccountAsync();
        using var client = await fixture.CreateBearerClientAsync(account);

        using var before = await client.GetAsync(FeaturesProbe);
        Assert.True(before.StatusCode is HttpStatusCode.Forbidden,
            $"pre-grant request must be denied, got {(int)before.StatusCode}");

        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var grantService = scope.ServiceProvider.GetRequiredService<IPermissionGrantService>();
            await grantService.GrantTenantPermissionAsync(
                account.User.Id,
                account.TenantId,
                ["features:read"],
                account.User.Id,
                reason: "adversarial validation: grant race control");
        }

        var afterGrant = Enumerable.Range(0, BurstSize)
            .Select(_ => client.GetAsync(FeaturesProbe))
            .ToList();
        foreach (var task in afterGrant)
        {
            using var response = await task;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
