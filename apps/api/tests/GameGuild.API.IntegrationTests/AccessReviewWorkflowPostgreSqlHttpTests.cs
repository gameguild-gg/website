using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     Runtime/database integration coverage for the unified access review &amp; certification
///     workflow (issue #390): exercises the real HTTP pipeline (authentication, tenant
///     resolution, actor context) against real PostgreSQL, and asserts that reviewer
///     attestations and campaign lifecycle transitions persist correctly.
/// </summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class AccessReviewWorkflowPostgreSqlHttpTests(ApiPostgreSqlFixture fixture, ITestOutputHelper output)
{
    private const string CampaignsRoute = "/v1/access-reviews/campaigns";

    [Fact]
    public async Task TenantAdminCertificationLifecyclePersistsAgainstPostgreSql()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var campaignId = Guid.Empty;
        var itemId = Guid.Empty;

        await SeedTenantAsync(tenantId, [
            (adminId, "TenantAdmin"),
            (reviewerId, "Member"),
        ]);

        try
        {
            using var adminClient = fixture.CreateAuthenticatedClient(adminId, tenantId);

            // Create the certification campaign.
            var createResponse = await adminClient.PostAsJsonAsync(CampaignsRoute, new
            {
                name = $"Q4 recertification {tenantId:N}",
                description = "Annual user access certification campaign",
                tenantId,
                reviewType = "UserAccessReview",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(14),
                createdBy = adminId,
            });
            createResponse.StatusCode.Should().Be(
                HttpStatusCode.Created,
                await createResponse.Content.ReadAsStringAsync());
            var created = JsonNode.Parse(await createResponse.Content.ReadAsStringAsync())!;
            campaignId = Guid.Parse(created["id"]!.GetValue<string>());
            output.WriteLine($"Created access review campaign {campaignId}");

            var persisted = await QueryCampaignAsync(campaignId);
            persisted.Should().NotBeNull();
            persisted!.Status.Should().Be(AccessReviewStatus.Draft);
            persisted.CreatedBy.Should().Be(adminId);
            persisted.Name.Should().Be($"Q4 recertification {tenantId:N}");

            // Start the campaign.
            var startResponse = await adminClient.PostAsync($"{CampaignsRoute}/{campaignId}:start", null);
            startResponse.StatusCode.Should().Be(
                HttpStatusCode.NoContent,
                await startResponse.Content.ReadAsStringAsync());
            (await QueryCampaignAsync(campaignId))!.Status.Should().Be(AccessReviewStatus.InProgress);

            // Assign a review item to the reviewer (attestation work list).
            itemId = await SeedReviewItemAsync(campaignId, reviewerId, tenantId);

            // The reviewer sees their own pending items.
            using var reviewerClient = fixture.CreateAuthenticatedClient(reviewerId, tenantId);
            var pendingResponse = await reviewerClient.GetAsync(
                $"/v1/access-reviews/items/pending?reviewerId={reviewerId}&tenantId={tenantId}");
            pendingResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var pending = JsonNode.Parse(await pendingResponse.Content.ReadAsStringAsync())!.AsArray();
            pending.Should().Contain(item => item!["id"]!.GetValue<string>() == itemId.ToString());

            // The reviewer records their attestation (approve).
            var approveResponse = await reviewerClient.PostAsJsonAsync(
                $"/v1/access-reviews/items/{itemId}:approve",
                new { reason = "Still required for support duty", notes = "Confirmed with manager" });
            approveResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await approveResponse.Content.ReadAsStringAsync());

            var attestation = await QueryItemAsync(itemId);
            attestation.Should().NotBeNull();
            attestation!.Status.Should().Be(AccessReviewItemStatus.Approved);
            attestation.Decision.Should().Be(AccessReviewDecision.Approve);
            attestation.DecisionReason.Should().Be("Still required for support duty");
            attestation.ReviewerNotes.Should().Be("Confirmed with manager");
            attestation.ReviewedAt.Should().NotBeNull();

            // The approved item no longer appears as pending.
            var afterApproval = await reviewerClient.GetAsync(
                $"/v1/access-reviews/items/pending?reviewerId={reviewerId}&tenantId={tenantId}");
            var pendingAfter = JsonNode.Parse(await afterApproval.Content.ReadAsStringAsync())!.AsArray();
            pendingAfter.Should().NotContain(item => item!["id"]!.GetValue<string>() == itemId.ToString());

            // The tenant admin completes (certifies) the campaign.
            var completeResponse = await adminClient.PostAsJsonAsync(
                $"{CampaignsRoute}/{campaignId}:complete",
                new { completedBy = adminId });
            completeResponse.StatusCode.Should().Be(
                HttpStatusCode.NoContent,
                await completeResponse.Content.ReadAsStringAsync());

            var completed = await QueryCampaignAsync(campaignId);
            completed!.Status.Should().Be(AccessReviewStatus.Completed);
            completed.CompletedBy.Should().Be(adminId);
            completed.CompletedAt.Should().NotBeNull();
        }
        finally
        {
            await CleanupAsync([tenantId], [campaignId], [itemId]);
        }
    }

    [Fact]
    public async Task AccessReviewWorkflowFailsClosedAcrossTenantsAndRoles()
    {
        var tenantAId = Guid.NewGuid();
        var tenantBId = Guid.NewGuid();
        var adminAId = Guid.NewGuid();
        var adminBId = Guid.NewGuid();
        var memberAId = Guid.NewGuid();
        var reviewerAId = Guid.NewGuid();

        await SeedTenantAsync(tenantAId, [
            (adminAId, "TenantAdmin"),
            (memberAId, "Member"),
            (reviewerAId, "Member"),
        ]);
        await SeedTenantAsync(tenantBId, [
            (adminBId, "TenantAdmin"),
        ]);

        var campaignBId = Guid.NewGuid();
        var foreignItemId = Guid.NewGuid();

        try
        {
            // Seed a campaign in tenant B with a pending item for reviewerA (the cross-tenant bait).
            await using (var scope = fixture.Factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Set<AccessReviewCampaign>().Add(new AccessReviewCampaign
                {
                    Id = campaignBId,
                    TenantId = new GameGuild.CQRS.Models.TenantId(tenantBId),
                    Name = "Tenant B campaign",
                    Description = "Belongs to tenant B",
                    ReviewType = AccessReviewType.UserAccessReview,
                    StartDate = DateTime.UtcNow.Date.AddDays(-1),
                    EndDate = DateTime.UtcNow.Date.AddDays(10),
                    Status = AccessReviewStatus.InProgress,
                    CreatedBy = adminBId,
                });
                context.Set<AccessReviewItem>().Add(new AccessReviewItem
                {
                    Id = foreignItemId,
                    CampaignId = campaignBId,
                    ReviewerId = reviewerAId,
                    SubjectUserId = Guid.NewGuid(),
                    PermissionDetails = "courses:read",
                });
                await context.SaveChangesAsync();
            }

            using var adminAClient = fixture.CreateAuthenticatedClient(adminAId, tenantAId);
            using var memberAClient = fixture.CreateAuthenticatedClient(memberAId, tenantAId);
            using var reviewerAClient = fixture.CreateAuthenticatedClient(reviewerAId, tenantAId);

            // A tenant admin cannot create a campaign for another tenant.
            var foreignTenantCreate = await adminAClient.PostAsJsonAsync(CampaignsRoute, new
            {
                name = "Cross-tenant campaign",
                description = "Should be rejected",
                tenantId = tenantBId,
                reviewType = "UserAccessReview",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(7),
                createdBy = adminAId,
            });
            foreignTenantCreate.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await foreignTenantCreate.Content.ReadAsStringAsync());

            // A plain member cannot create campaigns even in their own tenant.
            var memberCreate = await memberAClient.PostAsJsonAsync(CampaignsRoute, new
            {
                name = "Member campaign",
                description = "Should be rejected",
                tenantId = tenantAId,
                reviewType = "UserAccessReview",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(7),
                createdBy = memberAId,
            });
            memberCreate.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await memberCreate.Content.ReadAsStringAsync());

            // A tenant admin cannot claim another user's identity as creator.
            var spoofedCreate = await adminAClient.PostAsJsonAsync(CampaignsRoute, new
            {
                name = "Spoofed creator",
                description = "Should be rejected",
                tenantId = tenantAId,
                reviewType = "UserAccessReview",
                startDate = DateTime.UtcNow.Date,
                endDate = DateTime.UtcNow.Date.AddDays(7),
                createdBy = Guid.NewGuid(),
            });
            spoofedCreate.StatusCode.Should().Be(
                HttpStatusCode.Forbidden,
                await spoofedCreate.Content.ReadAsStringAsync());

            // Campaign reads for another tenant are indistinguishable from missing campaigns.
            var foreignRead = await adminAClient.GetAsync($"{CampaignsRoute}/{campaignBId}");
            foreignRead.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // A reviewer cannot ask for their pending items in another tenant's scope.
            var crossTenantPending = await reviewerAClient.GetAsync(
                $"/v1/access-reviews/items/pending?reviewerId={reviewerAId}&tenantId={tenantBId}");
            crossTenantPending.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // A reviewer cannot attest an item that belongs to another tenant's campaign.
            var foreignApprove = await reviewerAClient.PostAsJsonAsync(
                $"/v1/access-reviews/items/{foreignItemId}:approve",
                new { reason = "cross-tenant attempt" });
            foreignApprove.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // A plain member cannot start lifecycle transitions on campaigns.
            var memberStart = await memberAClient.PostAsync($"{CampaignsRoute}/{campaignBId}:start", null);
            memberStart.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Nothing was mutated by the denied requests.
            var foreignItem = await QueryItemAsync(foreignItemId);
            foreignItem!.Status.Should().Be(AccessReviewItemStatus.Pending, "denied requests must not mutate state");
            (await QueryCampaignAsync(campaignBId))!.Status.Should().Be(AccessReviewStatus.InProgress);

            // Unauthenticated requests are rejected before any workflow state is touched.
            using var anonymousClient = fixture.Factory.CreateClient();
            var anonymousRead = await anonymousClient.GetAsync($"{CampaignsRoute}/active");
            anonymousRead.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await CleanupAsync([tenantAId, tenantBId], [campaignBId], [foreignItemId]);
        }
    }

    [Fact]
    public async Task SystemAdminExpiresOnlyInProgressCampaignsPastTheirEndDate()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var systemAdminId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await SeedTenantAsync(tenantId, [
            (adminId, "TenantAdmin"),
            (memberId, "Member"),
        ]);

        var expiredCampaignId = Guid.NewGuid();
        var runningCampaignId = Guid.NewGuid();
        var expiredDraftCampaignId = Guid.NewGuid();

        try
        {
            await using (var scope = fixture.Factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                context.Set<AccessReviewCampaign>().AddRange(
                    new AccessReviewCampaign
                    {
                        Id = expiredCampaignId,
                        TenantId = new GameGuild.CQRS.Models.TenantId(tenantId),
                        Name = "Expired in-progress campaign",
                        Description = "Should be marked expired",
                        ReviewType = AccessReviewType.UserAccessReview,
                        StartDate = DateTime.UtcNow.Date.AddDays(-10),
                        EndDate = DateTime.UtcNow.Date.AddDays(-1),
                        Status = AccessReviewStatus.InProgress,
                        CreatedBy = adminId,
                    },
                    new AccessReviewCampaign
                    {
                        Id = runningCampaignId,
                        TenantId = new GameGuild.CQRS.Models.TenantId(tenantId),
                        Name = "Still running campaign",
                        Description = "Must not be touched",
                        ReviewType = AccessReviewType.UserAccessReview,
                        StartDate = DateTime.UtcNow.Date.AddDays(-1),
                        EndDate = DateTime.UtcNow.Date.AddDays(10),
                        Status = AccessReviewStatus.InProgress,
                        CreatedBy = adminId,
                    },
                    new AccessReviewCampaign
                    {
                        Id = expiredDraftCampaignId,
                        TenantId = new GameGuild.CQRS.Models.TenantId(tenantId),
                        Name = "Expired draft campaign",
                        Description = "Only in-progress campaigns expire",
                        ReviewType = AccessReviewType.UserAccessReview,
                        StartDate = DateTime.UtcNow.Date.AddDays(-10),
                        EndDate = DateTime.UtcNow.Date.AddDays(-1),
                        Status = AccessReviewStatus.Draft,
                        CreatedBy = adminId,
                    });
                await context.SaveChangesAsync();
            }

            // The expired-processing endpoint is restricted to SystemAdmin.
            using var memberClient = fixture.CreateAuthenticatedClient(memberId, tenantId);
            var memberAttempt = await memberClient.PostAsync($"{CampaignsRoute}:process-expired", null);
            memberAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            // Tenant admins are also not SystemAdmins.
            using var adminClient = fixture.CreateAuthenticatedClient(adminId, tenantId);
            var adminAttempt = await adminClient.PostAsync($"{CampaignsRoute}:process-expired", null);
            adminAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            using var systemAdminClient = fixture.CreateAuthenticatedClient(systemAdminId, tenantId, isSystemAdmin: true);
            var processResponse = await systemAdminClient.PostAsync($"{CampaignsRoute}:process-expired", null);
            processResponse.StatusCode.Should().Be(
                HttpStatusCode.OK,
                await processResponse.Content.ReadAsStringAsync());
            var processed = JsonNode.Parse(await processResponse.Content.ReadAsStringAsync())!;
            processed["processedCount"]!.GetValue<int>().Should().Be(1, "only the expired in-progress campaign counts");

            (await QueryCampaignAsync(expiredCampaignId))!.Status.Should().Be(AccessReviewStatus.Expired);
            (await QueryCampaignAsync(runningCampaignId))!.Status.Should().Be(AccessReviewStatus.InProgress);
            (await QueryCampaignAsync(expiredDraftCampaignId))!.Status.Should().Be(AccessReviewStatus.Draft);
        }
        finally
        {
            await CleanupAsync(
                [tenantId],
                [expiredCampaignId, runningCampaignId, expiredDraftCampaignId],
                []);
        }
    }

    [Fact]
    public async Task ReviewerAttestationsForUnknownItemsReturnNotFound()
    {
        var tenantId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();

        await SeedTenantAsync(tenantId, [
            (reviewerId, "Member"),
        ]);

        try
        {
            using var reviewerClient = fixture.CreateAuthenticatedClient(reviewerId, tenantId);
            var missingApprove = await reviewerClient.PostAsJsonAsync(
                $"/v1/access-reviews/items/{Guid.NewGuid()}:approve",
                new { reason = "does not exist" });
            missingApprove.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var missingRevoke = await reviewerClient.PostAsJsonAsync(
                $"/v1/access-reviews/items/{Guid.NewGuid()}:revoke",
                new { reason = "does not exist" });
            missingRevoke.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await CleanupAsync([tenantId], [], []);
        }
    }

    private async Task SeedTenantAsync(Guid tenantId, (Guid UserId, string Role)[] members)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var marker = tenantId.ToString("N");
        context.Set<Tenant>().Add(new Tenant
        {
            Id = tenantId,
            Name = $"Access review tenant {marker}",
            Slug = $"access-review-{marker}",
            AdminEmail = $"admin-{marker}@access-review.test",
            IsActive = true,
        });
        context.Set<User>().AddRange(members.Select(member =>
        {
            var user = User.CreateOAuthUser($"access-review-{member.UserId:N}@access-review.test", $"Access review user {member.UserId:N}");
            user.Id = member.UserId;
            return user;
        }));
        context.Set<TenantMember>().AddRange(members.Select(member => new TenantMember
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = member.UserId,
            Role = member.Role,
            IsActive = true,
        }));
        await context.SaveChangesAsync();
    }

    private async Task<Guid> SeedReviewItemAsync(Guid campaignId, Guid reviewerId, Guid tenantId)
    {
        var itemId = Guid.NewGuid();
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Set<AccessReviewItem>().Add(new AccessReviewItem
        {
            Id = itemId,
            CampaignId = campaignId,
            ReviewerId = reviewerId,
            SubjectUserId = Guid.NewGuid(),
            PermissionDetails = $"courses:read (tenant {tenantId:N})",
        });
        await context.SaveChangesAsync();
        return itemId;
    }

    private async Task<AccessReviewCampaign?> QueryCampaignAsync(Guid campaignId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<AccessReviewCampaign>().FirstOrDefaultAsync(campaign => campaign.Id == campaignId);
    }

    private async Task<AccessReviewItem?> QueryItemAsync(Guid itemId)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Set<AccessReviewItem>().FirstOrDefaultAsync(item => item.Id == itemId);
    }

    private async Task CleanupAsync(Guid[] tenantIds, Guid[] campaignIds, Guid[] itemIds)
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        if (itemIds.Length > 0)
        {
            await context.Set<AccessReviewItem>()
                .Where(item => itemIds.Contains(item.Id))
                .ExecuteDeleteAsync();
        }

        if (campaignIds.Length > 0)
        {
            await context.Set<AccessReviewCampaign>()
                .Where(campaign => campaignIds.Contains(campaign.Id))
                .ExecuteDeleteAsync();
        }

        var seededUserIds = await context.Set<TenantMember>()
            .Where(member => tenantIds.Contains(member.TenantId))
            .Select(member => member.UserId)
            .ToListAsync();

        await context.Set<TenantMember>()
            .Where(member => tenantIds.Contains(member.TenantId))
            .ExecuteDeleteAsync();
        await context.Set<Tenant>()
            .Where(tenant => tenantIds.Contains(tenant.Id))
            .ExecuteDeleteAsync();
        await context.Set<User>()
            .Where(user => seededUserIds.Contains(user.Id))
            .ExecuteDeleteAsync();
    }
}
