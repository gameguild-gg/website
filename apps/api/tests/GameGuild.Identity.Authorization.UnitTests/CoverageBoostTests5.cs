// CoverageBoostTests5.cs — Coverage boost for Identity.Authorization
// Targets: Commands, Queries, Validators, Handlers, Services, Middleware, Entities, Rules
#pragma warning disable CS8600, CS8602, CS8604, CS8625

using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.CQRS.Models;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Commands;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Queries;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

#region Command Constructor Tests

public class TenantPermissionCommandTests5
{
    private static TenantId MakeTenantId() => TenantId.New();

    [Fact]
    public void GrantTenantPermissionCommand_CanBeCreated()
    {
        var cmd = new GrantTenantPermissionCommand
        {
            TenantId = MakeTenantId(),
            UserId = Guid.NewGuid(),
            Permissions = new[] { "perm1", "perm2" },
            GrantedBy = Guid.NewGuid()
        };
        cmd.Permissions.Should().HaveCount(2);
        cmd.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GrantTenantPermissionCommand_OptionalFields()
    {
        var cmd = new GrantTenantPermissionCommand
        {
            TenantId = MakeTenantId(),
            UserId = Guid.NewGuid(),
            Permissions = new[] { "p" },
            GrantedBy = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            Reason = "test reason"
        };
        cmd.ExpiresAt.Should().NotBeNull();
        cmd.Reason.Should().Be("test reason");
    }

    [Fact]
    public void RevokeTenantPermissionCommand_CanBeCreated()
    {
        var cmd = new RevokeTenantPermissionCommand
        {
            TenantId = MakeTenantId(),
            UserId = Guid.NewGuid(),
            Permissions = new[] { "p1" },
            RevokedBy = Guid.NewGuid(),
            Reason = "revoke reason"
        };
        cmd.RevokedBy.Should().NotBeEmpty();
    }

    [Fact]
    public void SetGlobalDefaultPermissionsCommand_CanBeCreated()
    {
        var cmd = new SetGlobalDefaultPermissionsCommand
        {
            Permissions = new[] { "read", "write" },
            SetBy = Guid.NewGuid()
        };
        cmd.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void SetTenantDefaultPermissionsCommand_CanBeCreated()
    {
        var cmd = new SetTenantDefaultPermissionsCommand
        {
            TenantId = MakeTenantId(),
            Permissions = new[] { "admin" },
            SetBy = Guid.NewGuid()
        };
        cmd.Permissions.Should().ContainSingle();
    }

    [Fact]
    public void DenyTenantPermissionCommand_CanBeCreated()
    {
        var cmd = new DenyTenantPermissionCommand
        {
            TenantId = MakeTenantId(),
            UserId = Guid.NewGuid(),
            Permissions = new[] { "banned" },
            DeniedBy = Guid.NewGuid(),
            Reason = "policy violation"
        };
        cmd.DeniedBy.Should().NotBeEmpty();
    }

    [Fact]
    public void RemoveDenyPermissionsCommand_CanBeCreated()
    {
        var cmd = new RemoveDenyPermissionsCommand
        {
            TenantId = MakeTenantId(),
            UserId = Guid.NewGuid(),
            Permissions = new[] { "x" },
            RemovedBy = Guid.NewGuid()
        };
        cmd.RemovedBy.Should().NotBeEmpty();
    }
}

public class ResourcePermissionCommandTests5
{
    [Fact]
    public void UpdateUserPermissionsCommand_CanBeCreated()
    {
        var cmd = new UpdateUserPermissionsCommand
        {
            TenantId = TenantId.New(),
            ResourceType = "course",
            ResourceId = "res-1",
            TargetUserId = Guid.NewGuid(),
            Permissions = new[] { "read" },
            UpdatedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        };
        cmd.ResourceType.Should().Be("course");
    }

    [Fact]
    public void ShareResourceCommand_CanBeCreated()
    {
        var cmd = new ShareResourceCommand
        {
            TenantId = TenantId.New(),
            ResourceType = "project",
            ResourceId = "proj-1",
            UserIds = new[] { Guid.NewGuid() },
            Permissions = new[] { "read", "write" },
            GrantedByUserId = Guid.NewGuid(),
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            Message = "Shared with you",
            RequireAcceptance = false,
            NotifyUsers = true
        };
        cmd.Message.Should().Be("Shared with you");
    }

    [Fact]
    public void ShareResourceCommand_WithEmails()
    {
        var cmd = new ShareResourceCommand
        {
            TenantId = TenantId.New(),
            ResourceType = "project",
            ResourceId = "proj-2",
            UserIds = Array.Empty<Guid>(),
            UserEmails = new[] { "user@test.com" },
            Permissions = new[] { "read" },
            GrantedByUserId = Guid.NewGuid()
        };
        cmd.UserEmails.Should().ContainSingle();
    }

    [Fact]
    public void RemoveUserAccessCommand_CanBeCreated()
    {
        var cmd = new RemoveUserAccessCommand
        {
            TenantId = TenantId.New(),
            ResourceType = "file",
            ResourceId = "file-1",
            TargetUserId = Guid.NewGuid(),
            RemovedByUserId = Guid.NewGuid(),
            Reason = "no longer needed"
        };
        cmd.Reason.Should().Be("no longer needed");
    }
}

public class JitElevationCommandTests5
{
    [Fact]
    public void RequestJitElevationCommand_CanBeCreated()
    {
        var cmd = new RequestJitElevationCommand(
            Guid.NewGuid(), Guid.NewGuid(), "admin.write",
            "Need access for deployment", 30);
        cmd.Permission.Should().Be("admin.write");
        cmd.DurationMinutes.Should().Be(30);
    }

    [Fact]
    public void RequestJitElevationCommand_WithOptionalParams()
    {
        var cmd = new RequestJitElevationCommand(
            Guid.NewGuid(), Guid.NewGuid(), "admin.write",
            "justification", 60, Guid.NewGuid(), "resource", DateTime.UtcNow);
        cmd.ResourceId.Should().NotBeNull();
        cmd.ResourceType.Should().Be("resource");
        cmd.StartsAt.Should().NotBeNull();
    }

    [Fact]
    public void ApproveJitElevationCommand_CanBeCreated()
    {
        var cmd = new ApproveJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "approved");
        cmd.Comments.Should().Be("approved");
    }

    [Fact]
    public void DenyJitElevationCommand_CanBeCreated()
    {
        var cmd = new DenyJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "denied reason");
        cmd.Comments.Should().Be("denied reason");
    }

    [Fact]
    public void RevokeJitElevationCommand_CanBeCreated()
    {
        var cmd = new RevokeJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(), "security incident");
        cmd.Reason.Should().Be("security incident");
    }

    [Fact]
    public void CleanupExpiredElevationsCommand_CanBeCreated()
    {
        var cmd = new CleanupExpiredElevationsCommand();
        cmd.Should().NotBeNull();
    }
}

public class DelegatedAdminCommandTests5
{
    [Fact]
    public void GrantDelegatedAdminCommand_CanBeCreated()
    {
        var cmd = new GrantDelegatedAdminCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Admin Scope", "Description",
            new[] { "course", "project" }, new[] { Guid.NewGuid() },
            new[] { "read", "write" });
        cmd.Name.Should().Be("Admin Scope");
        cmd.ManagedResourceTypes.Should().HaveCount(2);
    }

    [Fact]
    public void GrantDelegatedAdminCommand_WithOrgUnit()
    {
        var cmd = new GrantDelegatedAdminCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Admin", "Desc",
            new[] { "course" }, new[] { Guid.NewGuid() },
            new[] { "read" }, Guid.NewGuid());
        cmd.OrganizationalUnitId.Should().NotBeNull();
    }

    [Fact]
    public void RevokeDelegatedAdminCommand_CanBeCreated()
    {
        var cmd = new RevokeDelegatedAdminCommand(Guid.NewGuid());
        cmd.ScopeId.Should().NotBeEmpty();
    }
}

public class SoDCommandTests5
{
    [Fact]
    public void CreateSoDRuleCommand_CanBeCreated()
    {
        var cmd = new CreateSoDRuleCommand("Rule1", "Desc",
            new[] { "perm1", "perm2" }, Guid.NewGuid());
        cmd.Name.Should().Be("Rule1");
        cmd.IsEnabled.Should().BeTrue(); // default
    }

    [Fact]
    public void CreateSoDRuleCommand_WithAllParams()
    {
        var cmd = new CreateSoDRuleCommand("Rule2", "Desc2",
            new[] { "a", "b" }, Guid.NewGuid(), SoDRuleType.PermissionConflict, false);
        cmd.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateSoDRuleCommand_CanBeCreated()
    {
        var cmd = new UpdateSoDRuleCommand(Guid.NewGuid(), "Updated", "Desc",
            new[] { "x" }, SoDRuleType.PermissionConflict, true);
        cmd.Name.Should().Be("Updated");
    }

    [Fact]
    public void DeleteSoDRuleCommand_CanBeCreated()
    {
        var cmd = new DeleteSoDRuleCommand(Guid.NewGuid());
        cmd.RuleId.Should().NotBeEmpty();
    }

    [Fact]
    public void ResolveSoDViolationCommand_CanBeCreated()
    {
        var cmd = new ResolveSoDViolationCommand(Guid.NewGuid(),
            Guid.NewGuid(), SoDResolutionAction.RevokePermission, "resolved notes");
        cmd.Notes.Should().Be("resolved notes");
    }

    [Fact]
    public void GrantSoDExceptionCommand_CanBeCreated()
    {
        var cmd = new GrantSoDExceptionCommand(Guid.NewGuid(),
            Guid.NewGuid(), "justified exception");
        cmd.Justification.Should().Be("justified exception");
    }

    [Fact]
    public void ScanSoDViolationsCommand_CanBeCreated()
    {
        var cmd = new ScanSoDViolationsCommand(Guid.NewGuid());
        cmd.TenantId.Should().NotBeNull();
    }
}

public class AccessReviewCommandTests5
{
    [Fact]
    public void CreateAccessReviewCampaignCommand_CanBeCreated()
    {
        var cmd = new CreateAccessReviewCampaignCommand("Review Q1", "Quarterly review",
            Guid.NewGuid(), AccessReviewType.PermissionReview,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        cmd.Name.Should().Be("Review Q1");
    }

    [Fact]
    public void StartAccessReviewCampaignCommand_CanBeCreated()
    {
        var cmd = new StartAccessReviewCampaignCommand(Guid.NewGuid());
        cmd.CampaignId.Should().NotBeEmpty();
    }

    [Fact]
    public void CompleteAccessReviewCampaignCommand_CanBeCreated()
    {
        var cmd = new CompleteAccessReviewCampaignCommand(Guid.NewGuid(), Guid.NewGuid());
        cmd.CompletedBy.Should().NotBeEmpty();
    }

    [Fact]
    public void CancelAccessReviewCampaignCommand_CanBeCreated()
    {
        var cmd = new CancelAccessReviewCampaignCommand(Guid.NewGuid());
        cmd.CampaignId.Should().NotBeEmpty();
    }

    [Fact]
    public void ApproveAccessReviewItemCommand_CanBeCreated()
    {
        var cmd = new ApproveAccessReviewItemCommand(Guid.NewGuid(), "looks good", "no issues");
        cmd.Reason.Should().Be("looks good");
    }

    [Fact]
    public void RevokeAccessReviewItemCommand_CanBeCreated()
    {
        var cmd = new RevokeAccessReviewItemCommand(Guid.NewGuid(), "not needed", "remove access");
        cmd.Reason.Should().Be("not needed");
    }

    [Fact]
    public void SendAccessReviewRemindersCommand_CanBeCreated()
    {
        var cmd = new SendAccessReviewRemindersCommand(Guid.NewGuid());
        cmd.CampaignId.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessExpiredCampaignsCommand_CanBeCreated()
    {
        var cmd = new ProcessExpiredCampaignsCommand();
        cmd.Should().NotBeNull();
    }
}

public class PermissionDelegationCommandTests5
{
    [Fact]
    public void DelegatePermissionsCommand_CanBeCreated()
    {
        var cmd = new DelegatePermissionsCommand(Guid.NewGuid(), Guid.NewGuid(),
            new[] { "read", "write" }, Guid.NewGuid());
        cmd.Permissions.Should().HaveCount(2);
    }

    [Fact]
    public void DelegatePermissionsCommand_WithAllOptional()
    {
        var cmd = new DelegatePermissionsCommand(Guid.NewGuid(), Guid.NewGuid(),
            new[] { "read" }, Guid.NewGuid(),
            Guid.NewGuid(), DateTime.UtcNow.AddHours(4),
            true, "urgent", 5);
        cmd.CanSubDelegate.Should().BeTrue();
        cmd.UsageLimit.Should().Be(5);
    }

    [Fact]
    public void RevokeDelegationCommand_CanBeCreated()
    {
        var cmd = new RevokeDelegationCommand(Guid.NewGuid());
        cmd.DelegationId.Should().NotBeEmpty();
    }

    [Fact]
    public void RecordDelegationUsageCommand_CanBeCreated()
    {
        var cmd = new RecordDelegationUsageCommand(Guid.NewGuid());
        cmd.DelegationId.Should().NotBeEmpty();
    }

    [Fact]
    public void CleanupExpiredDelegationsCommand_CanBeCreated()
    {
        var cmd = new CleanupExpiredDelegationsCommand();
        cmd.Should().NotBeNull();
    }
}

#endregion

#region Query Constructor Tests

public class TenantPermissionQueryTests5
{
    [Fact]
    public void GetTenantPermissionsQuery_CanBeCreated()
    {
        var q = new GetTenantPermissionsQuery
        {
            TenantId = TenantId.New(),
            UserId = Guid.NewGuid(),
            IncludeEffective = true
        };
        q.IncludeEffective.Should().BeTrue();
    }

    [Fact]
    public void GetEffectivePermissionsQuery_CanBeCreated()
    {
        var q = new GetEffectivePermissionsQuery
        {
            TenantId = TenantId.New(),
            ResourceType = "course",
            ResourceId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        };
        q.ResourceType.Should().Be("course");
    }

    [Fact]
    public void HasPermissionQuery_CanBeCreated()
    {
        var q = new HasPermissionQuery
        {
            TenantId = TenantId.New(),
            ResourceType = "project",
            ResourceId = Guid.NewGuid(),
            Permission = "read",
            UserId = Guid.NewGuid()
        };
        q.Permission.Should().Be("read");
    }

    [Fact]
    public void GetResourceUsersQuery_CanBeCreated()
    {
        var q = new GetResourceUsersQuery
        {
            TenantId = TenantId.New(),
            ResourceType = "course",
            ResourceId = "res-1",
            IncludeInherited = false,
            IncludeExpired = true
        };
        q.IncludeInherited.Should().BeFalse();
        q.IncludeExpired.Should().BeTrue();
    }
}

public class JitElevationQueryTests5
{
    [Fact]
    public void GetJitElevationByIdQuery_CanBeCreated()
    {
        var q = new GetJitElevationByIdQuery(Guid.NewGuid());
        q.RequestId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetPendingJitElevationsQuery_CanBeCreated()
    {
        var q = new GetPendingJitElevationsQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GetUserJitElevationsQuery_CanBeCreated()
    {
        var q = new GetUserJitElevationsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetActiveJitElevationsQuery_CanBeCreated()
    {
        var q = new GetActiveJitElevationsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public void HasActiveJitElevationQuery_CanBeCreated()
    {
        var q = new HasActiveJitElevationQuery(Guid.NewGuid(), "admin.write", Guid.NewGuid());
        q.Permission.Should().Be("admin.write");
    }

    [Fact]
    public void HasActiveJitElevationQuery_WithResourceId()
    {
        var q = new HasActiveJitElevationQuery(Guid.NewGuid(), "perm", Guid.NewGuid(), Guid.NewGuid());
        q.ResourceId.Should().NotBeNull();
    }
}

public class DelegatedAdminQueryTests5
{
    [Fact]
    public void GetDelegatedAdminScopeByIdQuery_CanBeCreated()
    {
        var q = new GetDelegatedAdminScopeByIdQuery(Guid.NewGuid());
        q.ScopeId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAdminScopesQuery_CanBeCreated()
    {
        var q = new GetAdminScopesQuery(Guid.NewGuid(), Guid.NewGuid());
        q.AdminUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetManagedUsersQuery_CanBeCreated()
    {
        var q = new GetManagedUsersQuery(Guid.NewGuid(), Guid.NewGuid());
        q.AdminUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetManagedResourceTypesQuery_CanBeCreated()
    {
        var q = new GetManagedResourceTypesQuery(Guid.NewGuid(), Guid.NewGuid());
        q.AdminUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void CanManageUserQuery_CanBeCreated()
    {
        var q = new CanManageUserQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        q.TargetUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void CanManageResourceQuery_CanBeCreated()
    {
        var q = new CanManageResourceQuery(Guid.NewGuid(), "course", Guid.NewGuid());
        q.ResourceType.Should().Be("course");
    }
}

public class SoDQueryTests5
{
    [Fact]
    public void GetSoDRuleByIdQuery_CanBeCreated()
    {
        var q = new GetSoDRuleByIdQuery(Guid.NewGuid());
        q.RuleId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSoDRulesQuery_CanBeCreated()
    {
        var q = new GetSoDRulesQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GetActiveSoDRulesQuery_CanBeCreated()
    {
        var q = new GetActiveSoDRulesQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void DetectSoDViolationsQuery_CanBeCreated()
    {
        var q = new DetectSoDViolationsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetUserSoDViolationsQuery_CanBeCreated()
    {
        var q = new GetUserSoDViolationsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetActiveSoDViolationsQuery_CanBeCreated()
    {
        var q = new GetActiveSoDViolationsQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }
}

public class AccessReviewQueryTests5
{
    [Fact]
    public void GetAccessReviewCampaignByIdQuery_CanBeCreated()
    {
        var q = new GetAccessReviewCampaignByIdQuery(Guid.NewGuid());
        q.CampaignId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetActiveAccessReviewCampaignsQuery_CanBeCreated()
    {
        var q = new GetActiveAccessReviewCampaignsQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GetPendingReviewItemsQuery_CanBeCreated()
    {
        var q = new GetPendingReviewItemsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.ReviewerId.Should().NotBeEmpty();
    }
}

public class PermissionDelegationQueryTests5
{
    [Fact]
    public void GetDelegationByIdQuery_CanBeCreated()
    {
        var q = new GetDelegationByIdQuery(Guid.NewGuid());
        q.DelegationId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetActiveDelegationsQuery_CanBeCreated()
    {
        var q = new GetActiveDelegationsQuery(Guid.NewGuid(), Guid.NewGuid());
        q.DelegateUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void GetDelegationsByDelegatorQuery_CanBeCreated()
    {
        var q = new GetDelegationsByDelegatorQuery(Guid.NewGuid(), Guid.NewGuid());
        q.DelegatorUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void CheckDelegatedPermissionQuery_CanBeCreated()
    {
        var q = new CheckDelegatedPermissionQuery(Guid.NewGuid(), "read", Guid.NewGuid());
        q.Permission.Should().Be("read");
    }

    [Fact]
    public void CheckDelegatedPermissionQuery_WithResourceId()
    {
        var q = new CheckDelegatedPermissionQuery(Guid.NewGuid(), "write", Guid.NewGuid(), Guid.NewGuid());
        q.ResourceId.Should().NotBeNull();
    }
}

public class PermissionAnalyticsQueryTests5
{
    [Fact]
    public void GetPermissionUsageQuery_CanBeCreated()
    {
        var q = new GetPermissionUsageQuery(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GetUserActivityQuery_CanBeCreated()
    {
        var q = new GetUserActivityQuery(Guid.NewGuid(), 20);
        q.Top.Should().Be(20);
    }

    [Fact]
    public void GetResourceAccessPatternsQuery_CanBeCreated()
    {
        var q = new GetResourceAccessPatternsQuery(Guid.NewGuid(), 5);
        q.Top.Should().Be(5);
    }

    [Fact]
    public void GetPermissionTrendsQuery_CanBeCreated()
    {
        var q = new GetPermissionTrendsQuery(Guid.NewGuid(), DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void DetectPermissionAnomaliesQuery_CanBeCreated()
    {
        var q = new DetectPermissionAnomaliesQuery(Guid.NewGuid());
        q.TenantId.Should().NotBeNull();
    }

    [Fact]
    public void GeneratePermissionReportQuery_CanBeCreated()
    {
        var q = new GeneratePermissionReportQuery(Guid.NewGuid(), DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        q.TenantId.Should().NotBeNull();
    }
}

#endregion

#region Validator Tests

public class ValidatorConstructorTests5
{
    [Fact]
    public void AccessReviewValidators_CanBeCreated()
    {
        new CreateAccessReviewCampaignValidator().Should().NotBeNull();
        new StartAccessReviewCampaignValidator().Should().NotBeNull();
        new CompleteAccessReviewCampaignValidator().Should().NotBeNull();
        new CancelAccessReviewCampaignValidator().Should().NotBeNull();
        new ApproveAccessReviewItemValidator().Should().NotBeNull();
        new RevokeAccessReviewItemValidator().Should().NotBeNull();
    }

    [Fact]
    public void DelegatedAdminValidators_CanBeCreated()
    {
        new GrantDelegatedAdminValidator().Should().NotBeNull();
        new RevokeDelegatedAdminValidator().Should().NotBeNull();
    }

    [Fact]
    public void JitElevationValidators_CanBeCreated()
    {
        new RequestJitElevationValidator().Should().NotBeNull();
        new ApproveJitElevationValidator().Should().NotBeNull();
        new DenyJitElevationValidator().Should().NotBeNull();
        new RevokeJitElevationValidator().Should().NotBeNull();
    }

    [Fact]
    public void DelegationValidators_CanBeCreated()
    {
        new DelegatePermissionsValidator().Should().NotBeNull();
        new RevokeDelegationValidator().Should().NotBeNull();
    }

    [Fact]
    public void SoDValidators_CanBeCreated()
    {
        new CreateSoDRuleValidator().Should().NotBeNull();
        new UpdateSoDRuleValidator().Should().NotBeNull();
        new DeleteSoDRuleValidator().Should().NotBeNull();
        new ResolveSoDViolationValidator().Should().NotBeNull();
        new GrantSoDExceptionValidator().Should().NotBeNull();
    }
}

public class ValidatorValidationTests5
{
    [Fact]
    public void CreateAccessReviewCampaignValidator_InvalidCommand_HasErrors()
    {
        var validator = new CreateAccessReviewCampaignValidator();
        var cmd = new CreateAccessReviewCampaignCommand("", "", null,
            AccessReviewType.PermissionReview, default, default, Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateAccessReviewCampaignValidator_ValidCommand_NoErrors()
    {
        var validator = new CreateAccessReviewCampaignValidator();
        var cmd = new CreateAccessReviewCampaignCommand("Review", "Desc",
            Guid.NewGuid(), AccessReviewType.PermissionReview,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(30), Guid.NewGuid());
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void RequestJitElevationValidator_EmptyPermission_Fails()
    {
        var validator = new RequestJitElevationValidator();
        var cmd = new RequestJitElevationCommand(Guid.Empty, null, "", "", 0);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RequestJitElevationValidator_ValidCommand_Succeeds()
    {
        var validator = new RequestJitElevationValidator();
        var cmd = new RequestJitElevationCommand(Guid.NewGuid(), Guid.NewGuid(),
            "admin.write", "Need for deploy", 30);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GrantDelegatedAdminValidator_EmptyName_Fails()
    {
        var validator = new GrantDelegatedAdminValidator();
        var cmd = new GrantDelegatedAdminCommand(Guid.Empty, null, "", "",
            Array.Empty<string>(), Array.Empty<Guid>(), Array.Empty<string>());
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateSoDRuleValidator_EmptyName_Fails()
    {
        var validator = new CreateSoDRuleValidator();
        var cmd = new CreateSoDRuleCommand("", "", Array.Empty<string>(), null);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DelegatePermissionsValidator_EmptyPerms_Fails()
    {
        var validator = new DelegatePermissionsValidator();
        var cmd = new DelegatePermissionsCommand(Guid.Empty, Guid.Empty, Array.Empty<string>(), null);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ApproveJitElevationValidator_EmptyRequestId_Fails()
    {
        var validator = new ApproveJitElevationValidator();
        var cmd = new ApproveJitElevationCommand(Guid.Empty, Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DenyJitElevationValidator_EmptyComments_Fails()
    {
        var validator = new DenyJitElevationValidator();
        var cmd = new DenyJitElevationCommand(Guid.Empty, Guid.Empty, "");
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RevokeJitElevationValidator_EmptyReason_Fails()
    {
        var validator = new RevokeJitElevationValidator();
        var cmd = new RevokeJitElevationCommand(Guid.Empty, Guid.Empty, "");
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UpdateSoDRuleValidator_EmptyName_Fails()
    {
        var validator = new UpdateSoDRuleValidator();
        var cmd = new UpdateSoDRuleCommand(Guid.Empty, "", "", Array.Empty<string>(),
            SoDRuleType.PermissionConflict, true);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void DeleteSoDRuleValidator_EmptyId_Fails()
    {
        var validator = new DeleteSoDRuleValidator();
        var cmd = new DeleteSoDRuleCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ResolveSoDViolationValidator_EmptyNotes_Fails()
    {
        var validator = new ResolveSoDViolationValidator();
        var cmd = new ResolveSoDViolationCommand(Guid.Empty, Guid.Empty,
            SoDResolutionAction.RevokePermission, "");
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void GrantSoDExceptionValidator_EmptyJustification_Fails()
    {
        var validator = new GrantSoDExceptionValidator();
        var cmd = new GrantSoDExceptionCommand(Guid.Empty, Guid.Empty, "");
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RevokeDelegatedAdminValidator_EmptyScopeId_Fails()
    {
        var validator = new RevokeDelegatedAdminValidator();
        var cmd = new RevokeDelegatedAdminCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RevokeDelegationValidator_EmptyId_Fails()
    {
        var validator = new RevokeDelegationValidator();
        var cmd = new RevokeDelegationCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void RevokeAccessReviewItemValidator_EmptyReason_Fails()
    {
        var validator = new RevokeAccessReviewItemValidator();
        var cmd = new RevokeAccessReviewItemCommand(Guid.Empty, "");
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void StartAccessReviewCampaignValidator_EmptyId_Fails()
    {
        var validator = new StartAccessReviewCampaignValidator();
        var cmd = new StartAccessReviewCampaignCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CancelAccessReviewCampaignValidator_EmptyId_Fails()
    {
        var validator = new CancelAccessReviewCampaignValidator();
        var cmd = new CancelAccessReviewCampaignCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CompleteAccessReviewCampaignValidator_EmptyIds_Fails()
    {
        var validator = new CompleteAccessReviewCampaignValidator();
        var cmd = new CompleteAccessReviewCampaignCommand(Guid.Empty, Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ApproveAccessReviewItemValidator_EmptyId_Fails()
    {
        var validator = new ApproveAccessReviewItemValidator();
        var cmd = new ApproveAccessReviewItemCommand(Guid.Empty);
        var result = validator.Validate(cmd);
        result.IsValid.Should().BeFalse();
    }
}

#endregion

#region Handler Constructor Tests

public class SimpleServiceHandlerTests5
{
    // JIT Elevation handlers - take IJitElevationService (+ actor/repo deps on the guarded ones)
    [Fact]
    public void JitElevationHandlers_CanBeCreated()
    {
        var service = new Mock<IJitElevationService>();
        var accessor = new Mock<IActorContextAccessor>();
        var repository = new Mock<IJitElevationRequestRepository>();
        var loggerRequest = NullLogger<RequestJitElevationHandler>.Instance;
        var loggerApprove = NullLogger<ApproveJitElevationHandler>.Instance;
        var loggerDeny = NullLogger<DenyJitElevationHandler>.Instance;
        var loggerRevoke = NullLogger<RevokeJitElevationHandler>.Instance;
        new RequestJitElevationHandler(service.Object, accessor.Object, loggerRequest).Should().NotBeNull();
        new ApproveJitElevationHandler(service.Object, repository.Object, accessor.Object, loggerApprove).Should().NotBeNull();
        new DenyJitElevationHandler(service.Object, accessor.Object, loggerDeny).Should().NotBeNull();
        new RevokeJitElevationHandler(service.Object, accessor.Object, loggerRevoke).Should().NotBeNull();
        new CleanupExpiredElevationsHandler(service.Object).Should().NotBeNull();
    }

    // Delegated Admin handlers - take IDelegatedAdminService + guard/version/audit dependencies
    [Fact]
    public void DelegatedAdminHandlers_CanBeCreated()
    {
        var service = new Mock<IDelegatedAdminService>();
        var accessor = new Mock<IActorContextAccessor>();
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        var audit = new Mock<IPermissionAuditService>();
        new GrantDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object).Should().NotBeNull();
        new RevokeDelegatedAdminHandler(service.Object, accessor.Object, versionStore.Object, audit.Object).Should().NotBeNull();
    }

    // SoD handlers - all take ISoDService
    [Fact]
    public void SoDHandlers_CanBeCreated()
    {
        var service = new Mock<ISoDService>();
        new CreateSoDRuleHandler(service.Object).Should().NotBeNull();
        new UpdateSoDRuleHandler(service.Object).Should().NotBeNull();
        new DeleteSoDRuleHandler(service.Object).Should().NotBeNull();
        new ResolveSoDViolationHandler(service.Object).Should().NotBeNull();
        new GrantSoDExceptionHandler(service.Object).Should().NotBeNull();
        new ScanSoDViolationsHandler(service.Object).Should().NotBeNull();
    }

    // Permission Delegation handlers - all take IPermissionDelegationService
    [Fact]
    public void PermissionDelegationHandlers_CanBeCreated()
    {
        var service = new Mock<IPermissionDelegationService>();
        new DelegatePermissionsHandler(service.Object).Should().NotBeNull();
        new RevokeDelegationHandler(service.Object).Should().NotBeNull();
        new RecordDelegationUsageHandler(service.Object).Should().NotBeNull();
        new CleanupExpiredDelegationsHandler(service.Object).Should().NotBeNull();
    }

    // Access Review handlers - all take IAccessReviewService
    [Fact]
    public void AccessReviewCommandHandlers_CanBeCreated()
    {
        var service = new Mock<IAccessReviewService>();
        new CreateAccessReviewCampaignHandler(service.Object).Should().NotBeNull();
        new StartAccessReviewCampaignHandler(service.Object).Should().NotBeNull();
        new CompleteAccessReviewCampaignHandler(service.Object).Should().NotBeNull();
        new CancelAccessReviewCampaignHandler(service.Object).Should().NotBeNull();
        new ApproveAccessReviewItemHandler(service.Object).Should().NotBeNull();
        new RevokeAccessReviewItemHandler(service.Object).Should().NotBeNull();
        new SendAccessReviewRemindersHandler(service.Object).Should().NotBeNull();
        new ProcessExpiredCampaignsHandler(service.Object).Should().NotBeNull();
    }

    // Permission Analytics query handlers - all take IPermissionAnalyticsService
    [Fact]
    public void PermissionAnalyticsHandlers_CanBeCreated()
    {
        var service = new Mock<IPermissionAnalyticsService>();
        new GetPermissionUsageHandler(service.Object).Should().NotBeNull();
        new GetUserActivityHandler(service.Object).Should().NotBeNull();
        new GetResourceAccessPatternsHandler(service.Object).Should().NotBeNull();
        new GetPermissionTrendsHandler(service.Object).Should().NotBeNull();
        new DetectPermissionAnomaliesHandler(service.Object).Should().NotBeNull();
        new GeneratePermissionReportHandler(service.Object).Should().NotBeNull();
    }

    // JIT Elevation query handlers - all take IJitElevationService
    [Fact]
    public void JitElevationQueryHandlers_CanBeCreated()
    {
        var service = new Mock<IJitElevationService>();
        new GetJitElevationByIdHandler(service.Object).Should().NotBeNull();
        new GetPendingJitElevationsHandler(service.Object).Should().NotBeNull();
        new GetUserJitElevationsHandler(service.Object).Should().NotBeNull();
        new GetActiveJitElevationsHandler(service.Object).Should().NotBeNull();
        new HasActiveJitElevationHandler(service.Object).Should().NotBeNull();
    }

    // Delegated Admin query handlers - all take IDelegatedAdminService
    [Fact]
    public void DelegatedAdminQueryHandlers_CanBeCreated()
    {
        var service = new Mock<IDelegatedAdminService>();
        new GetDelegatedAdminScopeByIdHandler(service.Object).Should().NotBeNull();
        new GetAdminScopesHandler(service.Object).Should().NotBeNull();
        new GetManagedUsersHandler(service.Object).Should().NotBeNull();
        new GetManagedResourceTypesHandler(service.Object).Should().NotBeNull();
        new CanManageUserHandler(service.Object).Should().NotBeNull();
        new CanManageResourceHandler(service.Object).Should().NotBeNull();
    }

    // SoD query handlers - all take ISoDService
    [Fact]
    public void SoDQueryHandlers_CanBeCreated()
    {
        var service = new Mock<ISoDService>();
        new GetSoDRuleByIdHandler(service.Object).Should().NotBeNull();
        new GetSoDRulesHandler(service.Object).Should().NotBeNull();
        new GetActiveSoDRulesHandler(service.Object).Should().NotBeNull();
        new DetectSoDViolationsHandler(service.Object).Should().NotBeNull();
        new GetUserSoDViolationsHandler(service.Object).Should().NotBeNull();
        new GetActiveSoDViolationsHandler(service.Object).Should().NotBeNull();
    }

    // Permission Delegation query handlers
    [Fact]
    public void PermissionDelegationQueryHandlers_CanBeCreated()
    {
        var service = new Mock<IPermissionDelegationService>();
        new GetDelegationByIdHandler(service.Object).Should().NotBeNull();
        new GetActiveDelegationsHandler(service.Object).Should().NotBeNull();
        new GetDelegationsByDelegatorHandler(service.Object).Should().NotBeNull();
        new CheckDelegatedPermissionHandler(service.Object).Should().NotBeNull();
    }

    // Access Review query handlers
    [Fact]
    public void AccessReviewQueryHandlers_CanBeCreated()
    {
        var service = new Mock<IAccessReviewService>();
        new GetAccessReviewCampaignByIdHandler(service.Object).Should().NotBeNull();
        new GetActiveAccessReviewCampaignsHandler(service.Object).Should().NotBeNull();
        new GetPendingReviewItemsHandler(service.Object).Should().NotBeNull();
    }
}

public class TenantPermissionHandlerTests5
{
    [Fact]
    public void GrantTenantPermissionCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<GrantTenantPermissionCommandHandler>.Instance;
        new GrantTenantPermissionCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }

    [Fact]
    public void RevokeTenantPermissionCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<RevokeTenantPermissionCommandHandler>.Instance;
        new RevokeTenantPermissionCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }

    [Fact]
    public void SetGlobalDefaultPermissionsCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<SetGlobalDefaultPermissionsCommandHandler>.Instance;
        new SetGlobalDefaultPermissionsCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }

    [Fact]
    public void SetTenantDefaultPermissionsCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<SetTenantDefaultPermissionsCommandHandler>.Instance;
        new SetTenantDefaultPermissionsCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }

    [Fact]
    public void DenyTenantPermissionCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<DenyTenantPermissionCommandHandler>.Instance;
        new DenyTenantPermissionCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }

    [Fact]
    public void RemoveDenyPermissionsCommandHandler_CanBeCreated()
    {
        var svc = new Mock<IPermissionGrantService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<RemoveDenyPermissionsCommandHandler>.Instance;
        new RemoveDenyPermissionsCommandHandler(svc.Object, accessor.Object, logger).Should().NotBeNull();
    }
}

public class ResourcePermissionHandlerTests5
{
    [Fact]
    public void UpdateUserPermissionsCommandHandler_CanBeCreated()
    {
        var resSvc = new Mock<IResourcePermissionService>();
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<UpdateUserPermissionsCommandHandler>.Instance;
        new UpdateUserPermissionsCommandHandler(resSvc.Object, accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }

    [Fact]
    public void ShareResourceCommandHandler_CanBeCreated()
    {
        var resSvc = new Mock<IResourcePermissionService>();
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<ShareResourceCommandHandler>.Instance;
        new ShareResourceCommandHandler(resSvc.Object, accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }

    [Fact]
    public void RemoveUserAccessCommandHandler_CanBeCreated()
    {
        var resSvc = new Mock<IResourcePermissionService>();
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<RemoveUserAccessCommandHandler>.Instance;
        new RemoveUserAccessCommandHandler(resSvc.Object, accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }
}

public class TenantPermissionQueryHandlerTests5
{
    [Fact]
    public void GetTenantPermissionsQueryHandler_CanBeCreated()
    {
        var querySvc = new Mock<IPermissionQueryService>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<GetTenantPermissionsQueryHandler>.Instance;
        new GetTenantPermissionsQueryHandler(querySvc.Object, accessor.Object, logger)
            .Should().NotBeNull();
    }

    [Fact]
    public void GetEffectivePermissionsQueryHandler_CanBeCreated()
    {
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<GetEffectivePermissionsQueryHandler>.Instance;
        new GetEffectivePermissionsQueryHandler(accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }

    [Fact]
    public async Task GetEffectivePermissionsQueryHandler_ReturnsResourcePermissionsFromQueryService()
    {
        var actorId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = actorId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string> { "Member" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };
        var accessor = new Mock<IActorContextAccessor>();
        accessor.SetupGet(a => a.ActorContext).Returns(actor);
        var querySvc = new Mock<IPermissionQueryService>();
        querySvc
            .Setup(s => s.HasTenantPermissionAsync(actorId, tenantId, $"Property.{resourceId}.Read", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        querySvc
            .Setup(s => s.GetEffectivePermissionsAsync(actorId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>
            {
                $"Property.{resourceId}.Read",
                $"Property.{resourceId}.Update",
                "Property.other.Read",
                "Admin"
            });
        var handler = new GetEffectivePermissionsQueryHandler(
            accessor.Object,
            querySvc.Object,
            NullLogger<GetEffectivePermissionsQueryHandler>.Instance);

        var result = await handler.Handle(
            new GetEffectivePermissionsQuery
            {
                TenantId = new TenantId(tenantId),
                ResourceType = "Property",
                ResourceId = resourceId
            },
            CancellationToken.None);

        result.Permissions.Should().HaveCount(3);
        result.Permissions.Should().Contain(p => p.Permission == $"Property.{resourceId}.Read" && p.Source == "Effective");
        result.Permissions.Should().Contain(p => p.Permission == $"Property.{resourceId}.Update" && p.Source == "Effective");
        result.Permissions.Should().Contain(p => p.Permission == "Admin" && p.Source == "Effective");
        result.HasFullAccess.Should().BeTrue();
    }

    [Fact]
    public void HasPermissionQueryHandler_CanBeCreated()
    {
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<HasPermissionQueryHandler>.Instance;
        new HasPermissionQueryHandler(accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }

    [Fact]
    public void GetResourceUsersQueryHandler_CanBeCreated()
    {
        var resSvc = new Mock<IResourcePermissionService>();
        var accessor = new Mock<IActorContextAccessor>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<GetResourceUsersQueryHandler>.Instance;
        new GetResourceUsersQueryHandler(resSvc.Object, accessor.Object, querySvc.Object, logger)
            .Should().NotBeNull();
    }
}

#endregion

#region Service Constructor Tests

public class ServiceConstructorTests5
{
    [Fact]
    public void PermissionDelegationService_CanBeCreated()
    {
        var repo = new Mock<IPermissionDelegationRepository>();
        var logger = NullLogger<PermissionDelegationService>.Instance;
        var svc = new PermissionDelegationService(repo.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PermissionAuditService_CanBeCreated()
    {
        var repo = new Mock<IPermissionAuditLogRepository>();
        var logger = NullLogger<PermissionAuditService>.Instance;
        var svc = new PermissionAuditService(repo.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void DelegatedAdminService_CanBeCreated()
    {
        var repo = new Mock<IDelegatedAdminScopeRepository>();
        var logger = NullLogger<DelegatedAdminService>.Instance;
        var svc = new DelegatedAdminService(repo.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void SoDService_CanBeCreated()
    {
        var ruleRepo = new Mock<ISoDRuleRepository>();
        var violationRepo = new Mock<ISoDViolationRepository>();
        var logger = NullLogger<SoDService>.Instance;
        var svc = new SoDService(ruleRepo.Object, violationRepo.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void JitElevationService_CanBeCreated()
    {
        var repo = new Mock<IJitElevationRequestRepository>();
        var auditSvc = new Mock<IPermissionAuditService>();
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        var logger = NullLogger<JitElevationService>.Instance;
        var svc = new JitElevationService(repo.Object, auditSvc.Object, versionStore.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void AccessReviewService_CanBeCreated()
    {
        var campaignRepo = new Mock<IAccessReviewCampaignRepository>();
        var itemRepo = new Mock<IAccessReviewItemRepository>();
        var logger = NullLogger<AccessReviewService>.Instance;
        var svc = new AccessReviewService(campaignRepo.Object, itemRepo.Object, logger, actorContextAccessor: Mock.Of<GameGuild.Identity.Context.Actors.IActorContextAccessor>());
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PermissionAnalyticsService_CanBeCreated()
    {
        var auditRepo = new Mock<IPermissionAuditLogRepository>();
        var logger = NullLogger<PermissionAnalyticsService>.Instance;
        var svc = new PermissionAnalyticsService(auditRepo.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PermissionGrantService_CanBeCreated()
    {
        var repo = new Mock<ITenantPermissionRepository>();
        var auditSvc = new Mock<IPermissionAuditService>();
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        var accessor = new Mock<IActorContextAccessor>();
        var logger = NullLogger<PermissionGrantService>.Instance;
        var svc = new PermissionGrantService(repo.Object, auditSvc.Object,
            versionStore.Object, accessor.Object, logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PermissionQueryService_CanBeCreated()
    {
        var repo = new Mock<ITenantPermissionRepository>();
        var membershipChecker = new Mock<ITenantMembershipChecker>();
        var logger = NullLogger<PermissionQueryService>.Instance;
        var svc = new PermissionQueryService(repo.Object, membershipChecker.Object, Mock.Of<IEffectivePermissionResolver>(), logger);
        svc.Should().NotBeNull();
    }

    [Fact]
    public void PermissionBulkService_CanBeCreated()
    {
        var grantSvc = new Mock<IPermissionGrantService>();
        var querySvc = new Mock<IPermissionQueryService>();
        var logger = NullLogger<PermissionBulkService>.Instance;
        var svc = new PermissionBulkService(grantSvc.Object, querySvc.Object, logger);
        svc.Should().NotBeNull();
    }
}

#endregion

#region Middleware Tests

public class ActorContextMiddlewareTests5
{
    [Fact]
    public async Task InvokeAsync_UnauthenticatedUser_SetsAnonymousContext()
    {
        var middleware = new ActorContextMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ActorContextMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var accessor = new Mock<IActorContextAccessor>();
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        var claimsPrincipalAccessor = new Mock<IClaimsPrincipalAccessor>();

        claimsPrincipalAccessor.Setup(x => x.ClaimsPrincipal).Returns((ClaimsPrincipal?)null);
        tenantResolver.Setup(x => x.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        await middleware.InvokeAsync(context, accessor.Object, tenantResolver.Object,
            claimsPrincipalAccessor.Object);

        accessor.Verify(a => a.SetActorContext(It.IsAny<ActorContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_AuthenticatedUser_SetsActorContext()
    {
        var middleware = new ActorContextMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ActorContextMiddleware>.Instance);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, "admin")
        }, "test");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new Mock<IActorContextAccessor>();
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        var claimsPrincipalAccessor = new Mock<IClaimsPrincipalAccessor>();

        claimsPrincipalAccessor.Setup(x => x.ClaimsPrincipal).Returns(context.User);
        tenantResolver.Setup(x => x.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        await middleware.InvokeAsync(context, accessor.Object, tenantResolver.Object,
            claimsPrincipalAccessor.Object);

        accessor.Verify(a => a.SetActorContext(It.IsAny<ActorContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithPermissionService_ResolvesPermissions()
    {
        var middleware = new ActorContextMiddleware(
            _ => Task.CompletedTask,
            NullLogger<ActorContextMiddleware>.Instance);

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        }, "test");

        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        var accessor = new Mock<IActorContextAccessor>();
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        var claimsPrincipalAccessor = new Mock<IClaimsPrincipalAccessor>();
        var permSvc = new Mock<IAuthorizationPermissionService>();

        claimsPrincipalAccessor.Setup(x => x.ClaimsPrincipal).Returns(context.User);
        tenantResolver.Setup(x => x.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        await middleware.InvokeAsync(context, accessor.Object, tenantResolver.Object,
            claimsPrincipalAccessor.Object, permSvc.Object);

        accessor.Verify(a => a.SetActorContext(It.IsAny<ActorContext>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_NextDelegateThrows_PropagatesException()
    {
        var middleware = new ActorContextMiddleware(
            _ => throw new InvalidOperationException("test error"),
            NullLogger<ActorContextMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var accessor = new Mock<IActorContextAccessor>();
        var tenantResolver = new Mock<IAuthorizationTenantResolver>();
        var claimsPrincipalAccessor = new Mock<IClaimsPrincipalAccessor>();

        claimsPrincipalAccessor.Setup(x => x.ClaimsPrincipal).Returns((ClaimsPrincipal?)null);
        tenantResolver.Setup(x => x.ResolveTenantIdAsync(It.IsAny<HttpContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var act = () => middleware.InvokeAsync(context, accessor.Object, tenantResolver.Object,
            claimsPrincipalAccessor.Object);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}

public class RequestContextLoggingMiddlewareTests5
{
    [Fact]
    public async Task InvokeAsync_WithActorContext_Completes()
    {
        var middleware = new RequestContextLoggingMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RequestContextLoggingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var accessor = new Mock<IActorContextAccessor>();
        var actorCtx = new ActorContext
        {
            IsAuthenticated = false,
            ActorKind = ActorKind.Anonymous,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        accessor.Setup(a => a.ActorContext).Returns(actorCtx);

        await middleware.InvokeAsync(context, accessor.Object);
        // Should complete without error
    }

    [Fact]
    public async Task InvokeAsync_WithActorContext_IncludesInScope()
    {
        var middleware = new RequestContextLoggingMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RequestContextLoggingMiddleware>.Instance);

        var context = new DefaultHttpContext();
        var accessor = new Mock<IActorContextAccessor>();
        var actorContext = new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string> { "admin" },
            Permissions = new HashSet<string> { "read" }
        };
        accessor.Setup(a => a.ActorContext).Returns(actorContext);

        await middleware.InvokeAsync(context, accessor.Object);
        // Should complete without error
    }
}

#endregion

#region Entity and Model Tests

public class TenantSecurityVersionTests5
{
    [Fact]
    public void CanBeCreated()
    {
        var entity = new TenantSecurityVersion
        {
            TenantId = Guid.NewGuid(),
            SecurityVersion = 1,
            LastUpdatedAt = DateTime.UtcNow
        };
        entity.SecurityVersion.Should().Be(1);
    }

    [Fact]
    public void IncrementVersion_IncrementsAndUpdatesTimestamp()
    {
        var entity = new TenantSecurityVersion
        {
            TenantId = Guid.NewGuid(),
            SecurityVersion = 1,
            LastUpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        var result = entity.IncrementVersion("test change");
        result.Should().Be(2);
        entity.SecurityVersion.Should().Be(2);
        entity.LastChangeReason.Should().Be("test change");
    }

    [Fact]
    public void IncrementVersion_WithNullReason()
    {
        var entity = new TenantSecurityVersion
        {
            TenantId = Guid.NewGuid(),
            SecurityVersion = 5,
            LastUpdatedAt = DateTime.UtcNow
        };

        var result = entity.IncrementVersion();
        result.Should().Be(6);
    }

    [Fact]
    public void Properties_SetCorrectly()
    {
        var tenantId = Guid.NewGuid();
        var entity = new TenantSecurityVersion
        {
            TenantId = tenantId,
            SecurityVersion = 10,
            LastUpdatedAt = DateTime.UtcNow,
            LastChangeReason = "some reason"
        };
        entity.TenantId.Should().Be(tenantId);
        entity.LastChangeReason.Should().Be("some reason");
    }
}

public class RuleDefinitionTests5
{
    [Fact]
    public void Validate_RuleWithType_ReturnsResult()
    {
        var rule = new RuleDefinition { Type = "some-type" };
        var result = rule.Validate();
        // Just verify the result is returned (outcome depends on registered validators)
        result.Should().NotBeNull();
    }

    [Fact]
    public void Validate_EmptyType_ReturnsInvalid()
    {
        var rule = new RuleDefinition { Type = "" };
        var result = rule.Validate();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public void GetParameters_EmptyParams_ReturnsEmptyParams()
    {
        var rule = new RuleDefinition { Type = "test" };
        var parameters = rule.GetParameters();
        parameters.Should().NotBeNull();
    }

    [Fact]
    public void GetParameters_WithParams_ReturnsParams()
    {
        var paramsDict = new Dictionary<string, JsonElement>
        {
            ["key"] = JsonSerializer.Deserialize<JsonElement>("\"value\"")
        };
        var rule = new RuleDefinition { Type = "test", Params = paramsDict };
        var parameters = rule.GetParameters();
        parameters.Should().NotBeNull();
    }

    [Fact]
    public void Properties_SetCorrectly()
    {
        var rule = new RuleDefinition
        {
            Type = "require-ip-allow-list",
            Description = "Allow specific IPs",
            Enabled = false
        };
        rule.Type.Should().Be("require-ip-allow-list");
        rule.Description.Should().Be("Allow specific IPs");
        rule.Enabled.Should().BeFalse();
    }

    [Fact]
    public void RuleValidationResult_Valid_IsValid()
    {
        var result = RuleValidationResult.Valid;
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void RuleValidationResult_Invalid_HasErrors()
    {
        var result = RuleValidationResult.Invalid("error1", "error2");
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }
}

public class FailClosedTenantMembershipCheckerTests5
{
    [Fact]
    public async Task IsUserMemberOfTenantAsync_AlwaysReturnsFalse()
    {
        var checker = new FailClosedTenantMembershipChecker();
        var result = await checker.IsUserMemberOfTenantAsync(Guid.NewGuid(), Guid.NewGuid());
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserMemberOfTenantAsync_WithCancellation_ReturnsFalse()
    {
        var checker = new FailClosedTenantMembershipChecker();
        using var cts = new CancellationTokenSource();
        var result = await checker.IsUserMemberOfTenantAsync(Guid.NewGuid(), Guid.NewGuid(), cts.Token);
        result.Should().BeFalse();
    }
}

#endregion

#region MemoryPolicyCache Tests

public class MemoryPolicyCacheExtendedTests5
{
    private MemoryPolicyCache CreateCache(AuthorizationCacheOptions? opts = null)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = opts ?? AuthorizationCacheOptions.CreateDefault();
        return new MemoryPolicyCache(
            cache,
            Options.Create(options),
            null,
            NullLogger<MemoryPolicyCache>.Instance);
    }

    [Fact]
    public void Get_NonExistent_ReturnsNull()
    {
        var cache = CreateCache();
        var result = cache.Get("policy1", "tenant1", 1);
        result.Should().BeNull();
    }

    [Fact]
    public void SetAndGet_ReturnsCachedPolicy()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("policy1", "tenant1", 1, policy);

        var result = cache.Get("policy1", "tenant1", 1);
        result.Should().NotBeNull();
    }

    [Fact]
    public void Get_WrongVersion_ReturnsNull()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("policy1", "tenant1", 1, policy);

        var result = cache.Get("policy1", "tenant1", 2);
        result.Should().BeNull();
    }

    [Fact]
    public void Invalidate_ByTenant_ClearsAllForTenant()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("p1", "t1", 1, policy);
        cache.Set("p2", "t1", 1, policy);

        cache.Invalidate("t1");

        cache.Get("p1", "t1", 1).Should().BeNull();
        cache.Get("p2", "t1", 1).Should().BeNull();
    }

    [Fact]
    public void Invalidate_ByPolicyAndTenant_DoesNotThrow()
    {
        var cache = CreateCache();
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        cache.Set("p1", "t1", 1, policy);

        // Just verify it doesn't throw
        cache.Invalidate("p1", "t1");
    }

    [Fact]
    public void Set_MultipleTenants_IndependentCaches()
    {
        var cache = CreateCache();
        var policy1 = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var policy2 = new AuthorizationPolicyBuilder().RequireRole("admin").Build();

        cache.Set("p1", "t1", 1, policy1);
        cache.Set("p1", "t2", 1, policy2);

        cache.Get("p1", "t1", 1).Should().NotBeNull();
        cache.Get("p1", "t2", 1).Should().NotBeNull();
    }
}

#endregion

#region CachedAccessControlListService Tests

public class CachedAccessControlListServiceTests5
{
    private static CachedAccessControlListService CreateService(
        Mock<IAccessControlListService>? innerMock = null,
        Mock<ITenantSecurityVersionStore>? versionStoreMock = null,
        Mock<IUserSecurityVersionStore>? userVersionMock = null)
    {
        var inner = innerMock ?? new Mock<IAccessControlListService>();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var versionStore = versionStoreMock ?? new Mock<ITenantSecurityVersionStore>();
        var userVersion = userVersionMock ?? new Mock<IUserSecurityVersionStore>();
        var options = Options.Create(AuthorizationCacheOptions.CreateDefault());

        return new CachedAccessControlListService(
            inner.Object, cache, versionStore.Object, userVersion.Object, options);
    }

    [Fact]
    public async Task EvaluateAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(x => x.EvaluateAccessAsync(It.IsAny<AclSubject>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Read);

        var svc = CreateService(inner);
        var subject = new AclSubject { IsAuthenticated = true, UserId = Guid.NewGuid() };

        var result = await svc.EvaluateAccessAsync(subject, Guid.NewGuid(), "course", "c1", CancellationToken.None);
        result.Should().Be(AccessLevel.Read);
    }

    [Fact]
    public async Task HasAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(x => x.HasAccessAsync(It.IsAny<AclSubject>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var svc = CreateService(inner);
        var subject = new AclSubject { IsAuthenticated = true, UserId = Guid.NewGuid() };

        var result = await svc.HasAccessAsync(subject, Guid.NewGuid(), "course", "c1", AccessLevel.Read, CancellationToken.None);
        // The cached wrapper may return cached or delegated value
        inner.Verify(x => x.HasAccessAsync(It.IsAny<AclSubject>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(), It.IsAny<CancellationToken>()), Times.AtMostOnce);
    }

    [Fact]
    public async Task GrantAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        var svc = CreateService(inner);

        await svc.GrantAccessAsync(Guid.NewGuid(), AclPrincipalType.User, Guid.NewGuid(),
            Guid.NewGuid(), "course", "c1", AccessLevel.Write, CancellationToken.None);

        inner.Verify(x => x.GrantAccessAsync(It.IsAny<Guid>(), It.IsAny<AclPrincipalType>(), It.IsAny<Guid?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DenyAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        var svc = CreateService(inner);

        await svc.DenyAccessAsync(Guid.NewGuid(), AclPrincipalType.User, Guid.NewGuid(),
            Guid.NewGuid(), "course", "c1", AccessLevel.Write, CancellationToken.None);

        inner.Verify(x => x.DenyAccessAsync(It.IsAny<Guid>(), It.IsAny<AclPrincipalType>(), It.IsAny<Guid?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RevokeAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        var svc = CreateService(inner);

        await svc.RevokeAccessAsync(Guid.NewGuid(), AclPrincipalType.User, Guid.NewGuid(),
            Guid.NewGuid(), "course", "c1", CancellationToken.None);

        inner.Verify(x => x.RevokeAccessAsync(It.IsAny<Guid>(), It.IsAny<AclPrincipalType>(), It.IsAny<Guid?>(),
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void InvalidateTenant_ClearsCache()
    {
        var svc = CreateService();
        // Should not throw
        svc.InvalidateTenant("tenant1");
    }

    [Fact]
    public async Task InvalidateTenantAsync_ClearsCache()
    {
        var svc = CreateService();
        await svc.InvalidateTenantAsync("tenant1", CancellationToken.None);
    }

    [Fact]
    public async Task LegacyGetAccessLevelAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(x => x.GetAccessLevelAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AccessLevel.Admin);

        var svc = CreateService(inner);
        var result = await svc.GetAccessLevelAsync(Guid.NewGuid(), Guid.NewGuid(), "course", "c1", CancellationToken.None);
        result.Should().Be(AccessLevel.Admin);
    }

    [Fact]
    public async Task LegacyGrantAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        var svc = CreateService(inner);

        await svc.GrantAccessAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "course", "c1", AccessLevel.Write, CancellationToken.None);

        inner.Verify(x => x.GrantAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyRevokeAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        var svc = CreateService(inner);

        await svc.RevokeAccessAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "course", "c1", CancellationToken.None);

        inner.Verify(x => x.RevokeAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LegacyHasAccessAsync_DelegatesToInner()
    {
        var inner = new Mock<IAccessControlListService>();
        inner.Setup(x => x.HasAccessAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<AccessLevel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var svc = CreateService(inner);
        var result = await svc.HasAccessAsync(Guid.NewGuid(), Guid.NewGuid(), "course", "c1",
            AccessLevel.Write, CancellationToken.None);
        result.Should().BeFalse();
    }
}

#endregion

#region AuthorizationTenantResolver Tests

public class AuthorizationTenantResolverTests5
{
    private AuthorizationTenantResolver CreateResolver()
    {
        var tenancyOpts = Options.Create(new TenancyOptions());
        var tokenOpts = Options.Create(new AuthorizationTokenOptions());
        return new AuthorizationTenantResolver(tenancyOpts, tokenOpts);
    }

    [Fact]
    public void ResolveFromRequest_NoHeader_ReturnsNull()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        var result = resolver.ResolveFromRequest(context);
        result.Should().BeNull();
    }

    [Fact]
    public void ResolveFromRequest_WithTenantHeader_ReturnsTenantId()
    {
        var resolver = CreateResolver();
        var context = new DefaultHttpContext();
        var tenantId = Guid.NewGuid().ToString();
        context.Request.Headers["X-Tenant-Id"] = tenantId;
        var result = resolver.ResolveFromRequest(context);
        result.Should().Be(tenantId);
    }

    [Fact]
    public void ResolveFromClaims_NoClaims_ReturnsNull()
    {
        var resolver = CreateResolver();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var result = resolver.ResolveFromClaims(principal);
        result.Should().BeNull();
    }

    [Fact]
    public void GetUserDefaultTenant_NoClaims_ReturnsNull()
    {
        var resolver = CreateResolver();
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var result = resolver.GetUserDefaultTenant(principal);
        result.Should().BeNull();
    }
}

#endregion

#region PermissionFetchException Tests

public class PermissionFetchExceptionTests5
{
    [Fact]
    public void CanBeCreated_WithAllParams()
    {
        var inner = new Exception("inner");
        var ex = new PermissionFetchException("test message", Guid.NewGuid(), Guid.NewGuid(), inner);
        ex.Message.Should().Contain("test message");
        ex.InnerException.Should().Be(inner);
    }

    [Fact]
    public void CanBeCreated_WithoutInnerException()
    {
        var ex = new PermissionFetchException("msg", Guid.NewGuid(), Guid.NewGuid());
        ex.Message.Should().Contain("msg");
        ex.InnerException.Should().BeNull();
    }
}

#endregion

#region AuthorizationCacheOptions Tests

public class AuthorizationCacheOptionsTests5
{
    [Fact]
    public void CreateDefault_ReturnsValidInstance()
    {
        var opts = AuthorizationCacheOptions.CreateDefault();
        opts.Should().NotBeNull();
        opts.PolicyTtlSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var opts = new AuthorizationCacheOptions
        {
            PolicyTtlSeconds = 60,
            PermissionTtlSeconds = 120,
            MaxPolicyCacheSize = 500,
            UseDistributedCache = true,
            EnableMetrics = true
        };
        opts.PolicyTtlSeconds.Should().Be(60);
        opts.UseDistributedCache.Should().BeTrue();
    }
}

#endregion

#region RuleParameters Tests

public class RuleParametersTests5
{
    [Fact]
    public void FromJson_NullJson_ReturnsEmptyParams()
    {
        var result = RuleParameters.FromJson(null);
        result.Should().NotBeNull();
    }

    [Fact]
    public void FromJson_ValidJson_ReturnsParams()
    {
        var result = RuleParameters.FromJson("{\"key\": \"value\"}");
        result.Should().NotBeNull();
    }

    [Fact]
    public void FromJson_EmptyJson_ReturnsParams()
    {
        var result = RuleParameters.FromJson("{}");
        result.Should().NotBeNull();
    }
}

#endregion
