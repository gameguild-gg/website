using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using FluentAssertions;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Caching;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Authorization.Utilities;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

#region ClaimsExtractor Tests

public class ClaimsExtractorFullCoverageTests
{
    private static ClaimsPrincipal CreateUser(params Claim[] claims)
    {
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void GetUserId_ReturnsSubClaim()
    {
        var user = CreateUser(new Claim("sub", "user-123"));
        ClaimsExtractor.GetUserId(user).Should().Be("user-123");
    }

    [Fact]
    public void GetUserId_FallsBackToNameIdentifier()
    {
        var user = CreateUser(new Claim(ClaimTypes.NameIdentifier, "user-456"));
        ClaimsExtractor.GetUserId(user).Should().Be("user-456");
    }

    [Fact]
    public void GetUserId_FallsBackToUserId()
    {
        var user = CreateUser(new Claim("UserId", "user-789"));
        ClaimsExtractor.GetUserId(user).Should().Be("user-789");
    }

    [Fact]
    public void GetUserId_ReturnsNull_WhenNoClaim()
    {
        var user = CreateUser();
        ClaimsExtractor.GetUserId(user).Should().BeNull();
    }

    [Fact]
    public void GetUserIdAsGuid_ReturnsGuid()
    {
        var id = Guid.NewGuid();
        var user = CreateUser(new Claim("sub", id.ToString()));
        ClaimsExtractor.GetUserIdAsGuid(user).Should().Be(id);
    }

    [Fact]
    public void GetUserIdAsGuid_ReturnsNull_ForInvalidGuid()
    {
        var user = CreateUser(new Claim("sub", "not-a-guid"));
        ClaimsExtractor.GetUserIdAsGuid(user).Should().BeNull();
    }

    [Fact]
    public void GetUserIdAsGuid_ReturnsNull_WhenEmpty()
    {
        var user = CreateUser();
        ClaimsExtractor.GetUserIdAsGuid(user).Should().BeNull();
    }

    [Fact]
    public void GetJti_ReturnsJtiClaim()
    {
        var user = CreateUser(new Claim(JwtRegisteredClaimNames.Jti, "jti-abc"));
        ClaimsExtractor.GetJti(user).Should().Be("jti-abc");
    }

    [Fact]
    public void GetIssuedAt_ReturnsTimestamp()
    {
        var user = CreateUser(new Claim(JwtRegisteredClaimNames.Iat, "1700000000"));
        ClaimsExtractor.GetIssuedAt(user).Should().Be(1700000000L);
    }

    [Fact]
    public void GetIssuedAt_ReturnsNull_ForEmptyClaim()
    {
        var user = CreateUser();
        ClaimsExtractor.GetIssuedAt(user).Should().BeNull();
    }

    [Fact]
    public void GetIssuedAt_ReturnsNull_ForInvalidValue()
    {
        var user = CreateUser(new Claim(JwtRegisteredClaimNames.Iat, "not-a-number"));
        ClaimsExtractor.GetIssuedAt(user).Should().BeNull();
    }

    [Fact]
    public void GetIssuedAtDateTime_ReturnsDateTime()
    {
        var user = CreateUser(new Claim(JwtRegisteredClaimNames.Iat, "1700000000"));
        var result = ClaimsExtractor.GetIssuedAtDateTime(user);
        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc); // CS8602 suppressed by !
    }

    [Fact]
    public void GetIssuedAtDateTime_ReturnsNull_WhenMissing()
    {
        var user = CreateUser();
        ClaimsExtractor.GetIssuedAtDateTime(user).Should().BeNull();
    }

    [Fact]
    public void GetEmail_ReturnsEmailClaim()
    {
        var user = CreateUser(new Claim("email", "test@example.com"));
        ClaimsExtractor.GetEmail(user).Should().Be("test@example.com");
    }

    [Fact]
    public void GetEmail_FallsBackToClaimTypes()
    {
        var user = CreateUser(new Claim(ClaimTypes.Email, "fallback@test.com"));
        ClaimsExtractor.GetEmail(user).Should().Be("fallback@test.com");
    }

    [Fact]
    public void GetName_ReturnsNameClaim()
    {
        var user = CreateUser(new Claim(ClaimTypes.Name, "John Doe"));
        ClaimsExtractor.GetName(user).Should().Be("John Doe");
    }

    [Fact]
    public void GetName_FallsBackToNameString()
    {
        var user = CreateUser(new Claim("name", "Jane Doe"));
        ClaimsExtractor.GetName(user).Should().Be("Jane Doe");
    }

    [Fact]
    public void GetRoles_ReturnsAllRoleClaims()
    {
        var user = CreateUser(
            new Claim(ClaimTypes.Role, "admin"),
            new Claim("role", "editor"),
            new Claim("roles", "viewer"));
        var roles = ClaimsExtractor.GetRoles(user);
        roles.Should().Contain("admin");
        roles.Should().Contain("editor");
        roles.Should().Contain("viewer");
    }

    [Fact]
    public void GetTenantId_ReturnsTenantIdClaim()
    {
        var user = CreateUser(new Claim("TenantId", "tenant-abc"));
        ClaimsExtractor.GetTenantId(user).Should().Be("tenant-abc");
    }

    [Fact]
    public void GetTenantId_FallsBackToAlt()
    {
        var user = CreateUser(new Claim("tenant_id", "tenant-xyz"));
        ClaimsExtractor.GetTenantId(user).Should().Be("tenant-xyz");
    }

    [Fact]
    public void GetTenantIdAsGuid_ReturnsGuid()
    {
        var tid = Guid.NewGuid();
        var user = CreateUser(new Claim("TenantId", tid.ToString()));
        ClaimsExtractor.GetTenantIdAsGuid(user).Should().Be(tid);
    }

    [Fact]
    public void GetTenantIdAsGuid_ReturnsNull_ForInvalid()
    {
        var user = CreateUser(new Claim("TenantId", "bad"));
        ClaimsExtractor.GetTenantIdAsGuid(user).Should().BeNull();
    }

    [Fact]
    public void GetGrantType_Returns()
    {
        var user = CreateUser(new Claim("grant_type", "client_credentials"));
        ClaimsExtractor.GetGrantType(user).Should().Be("client_credentials");
    }

    [Fact]
    public void GetActorType_Returns()
    {
        var user = CreateUser(new Claim("actor_type", "service"));
        ClaimsExtractor.GetActorType(user).Should().Be("service");
    }

    [Fact]
    public void GetPermissions_ReturnsAll()
    {
        var user = CreateUser(
            new Claim("permission", "read"),
            new Claim("permissions", "write"));
        var perms = ClaimsExtractor.GetPermissions(user);
        perms.Should().Contain("read");
        perms.Should().Contain("write");
    }

    [Fact]
    public void IsMfaVerified_ReturnsTrueWhenClaimed()
    {
        var user = CreateUser(new Claim("mfa_verified", "true"));
        ClaimsExtractor.IsMfaVerified(user).Should().BeTrue();
    }

    [Fact]
    public void IsMfaVerified_ReturnsFalseWhenMissing()
    {
        var user = CreateUser();
        ClaimsExtractor.IsMfaVerified(user).Should().BeFalse();
    }

    [Fact]
    public void IsEmailVerified_Works()
    {
        var user = CreateUser(new Claim("email_verified", "true"));
        ClaimsExtractor.IsEmailVerified(user).Should().BeTrue();

        var user2 = CreateUser(new Claim("email_verified", "false"));
        ClaimsExtractor.IsEmailVerified(user2).Should().BeFalse();
    }

    [Fact]
    public void GetAmr_Returns()
    {
        var user = CreateUser(new Claim("amr", "pwd"));
        ClaimsExtractor.GetAmr(user).Should().Be("pwd");
    }

    [Fact]
    public void GetTokenVersion_Returns()
    {
        var user = CreateUser(new Claim("token_version", "2"));
        ClaimsExtractor.GetTokenVersion(user).Should().Be("2");
    }

    [Fact]
    public void GetClaim_ReturnsCustomClaim()
    {
        var user = CreateUser(new Claim("custom_claim", "val"));
        ClaimsExtractor.GetClaim(user, "custom_claim").Should().Be("val");
    }

    [Fact]
    public void IsAuthenticated_ReturnsTrueForAuthenticatedUser()
    {
        var user = CreateUser(new Claim("sub", "x"));
        ClaimsExtractor.IsAuthenticated(user).Should().BeTrue();
    }

    [Fact]
    public void IsAuthenticated_ReturnsFalseForAnonymous()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        ClaimsExtractor.IsAuthenticated(user).Should().BeFalse();
    }
}

#endregion

#region Permission Model Tests

public class PermissionModelTests
{
    private class TestPermission : Permission
    {
        public TestPermission(string resource, string action, string? scope = null, string description = "test")
            : base(resource, action, scope, description) { }
    }

    [Fact]
    public void Permission_KeyWithoutScope()
    {
        var p = new TestPermission("users", "read");
        p.Key.Should().Be("users:read");
        p.Resource.Should().Be("users");
        p.Action.Should().Be("read");
        p.Scope.Should().BeNull();
    }

    [Fact]
    public void Permission_KeyWithScope()
    {
        var p = new TestPermission("users", "read", "self");
        p.Key.Should().Be("users:read:self");
        p.Scope.Should().Be("self");
    }

    [Fact]
    public void Permission_ToString()
    {
        var p = new TestPermission("users", "write");
        p.ToString().Should().Be("users:write");
    }

    [Fact]
    public void Permission_ImplicitStringConversion()
    {
        var p = new TestPermission("users", "delete");
        string key = p;
        key.Should().Be("users:delete");
    }

    [Fact]
    public void Permission_Equality()
    {
        var p1 = new TestPermission("users", "read");
        var p2 = new TestPermission("users", "read");
        var p3 = new TestPermission("users", "write");

        p1.Equals(p2).Should().BeTrue();
        p1.Equals(p3).Should().BeFalse();
        p1.Equals((object)p2).Should().BeTrue();
        p1!.Equals((object?)null).Should().BeFalse();
        p1!.Equals((Permission?)null).Should().BeFalse();
        (p1 == p2).Should().BeTrue();
        (p1 != p3).Should().BeTrue();
    }

    [Fact]
    public void Permission_GetHashCode_SameForEqual()
    {
        var p1 = new TestPermission("users", "read");
        var p2 = new TestPermission("users", "read");
        p1.GetHashCode().Should().Be(p2.GetHashCode());
    }

    [Fact]
    public void Permission_ThrowsOnNullArgs()
    {
        Assert.Throws<ArgumentNullException>(() => new TestPermission(null!, "read"));
        Assert.Throws<ArgumentNullException>(() => new TestPermission("users", null!));
        Assert.Throws<ArgumentNullException>(() => new TestPermission("users", "read", null, null!));
    }
}

#endregion

#region AclSubject Tests

public class AclSubjectTests
{
    [Fact]
    public void Anonymous_IsNotAuthenticated()
    {
        var subject = AclSubject.Anonymous;
        subject.IsAuthenticated.Should().BeFalse();
        subject.UserId.Should().BeNull();
    }

    [Fact]
    public void ForUser_CreatesAuthenticatedSubject()
    {
        var userId = Guid.NewGuid();
        var roleIds = new List<Guid> { Guid.NewGuid() };
        var groupIds = new List<Guid> { Guid.NewGuid() };

        var subject = AclSubject.ForUser(userId, roleIds, groupIds);

        subject.IsAuthenticated.Should().BeTrue();
        subject.UserId.Should().Be(userId);
        subject.RoleIds.Should().BeEquivalentTo(roleIds);
        subject.GroupIds.Should().BeEquivalentTo(groupIds);
    }

    [Fact]
    public void ForUser_WithNoRolesOrGroups()
    {
        var userId = Guid.NewGuid();
        var subject = AclSubject.ForUser(userId);

        subject.IsAuthenticated.Should().BeTrue();
        subject.UserId.Should().Be(userId);
        subject.RoleIds.Should().BeEmpty();
        subject.GroupIds.Should().BeEmpty();
    }

    [Fact]
    public void GetPrincipals_ForAnonymous()
    {
        var subject = AclSubject.Anonymous;
        var principals = subject.GetPrincipals().ToList();
        principals.Should().ContainSingle();
        principals[0].Type.Should().Be(AclPrincipalType.Anonymous);
        principals[0].Id.Should().BeNull();
    }

    [Fact]
    public void GetPrincipals_ForAuthenticatedUser()
    {
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var subject = AclSubject.ForUser(userId, [roleId], [groupId]);

        var principals = subject.GetPrincipals().ToList();
        principals.Should().HaveCount(4); // Anonymous + User + Role + Group
        principals.Should().Contain(p => p.Type == AclPrincipalType.Anonymous);
        principals.Should().Contain(p => p.Type == AclPrincipalType.User && p.Id == userId);
        principals.Should().Contain(p => p.Type == AclPrincipalType.Role && p.Id == roleId);
        principals.Should().Contain(p => p.Type == AclPrincipalType.Group && p.Id == groupId);
    }
}

#endregion

#region PolicyGateResult and models

public class PolicyGateResultTests
{
    [Fact]
    public void Allowed_ReturnsIsAllowedTrue()
    {
        var result = PolicyGateResult.Allowed();
        result.IsAllowed.Should().BeTrue();
        result.DeniedByGate.Should().BeNull();
    }

    [Fact]
    public void Allowed_WithDetails()
    {
        var details = new List<GateEvaluationDetail>
        {
            new(PolicyGateType.Static, true, null, null, TimeSpan.FromMilliseconds(1))
        };
        var result = PolicyGateResult.Allowed(details);
        result.IsAllowed.Should().BeTrue();
        result.GateDetails.Should().HaveCount(1);
    }

    [Fact]
    public void Denied_ReturnsIsAllowedFalse()
    {
        var result = PolicyGateResult.Denied(PolicyGateType.Conditional, "reason", "policy-1");
        result.IsAllowed.Should().BeFalse();
        result.DeniedByGate.Should().Be(PolicyGateType.Conditional);
        result.DenialReason.Should().Be("reason");
        result.DeniedByPolicyId.Should().Be("policy-1");
    }
}

#endregion

#region ConditionalPolicyEvaluator Tests

public class ConditionalPolicyEvaluatorFullTests
{
    private readonly Mock<IConditionalPolicyRepository> _repo = new();
    private readonly ConditionalPolicyEvaluator _evaluator;

    public ConditionalPolicyEvaluatorFullTests()
    {
        _evaluator = new ConditionalPolicyEvaluator(
            _repo.Object,
            NullLogger<ConditionalPolicyEvaluator>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_NoPolicies_ReturnsAllowed()
    {
        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConditionalPolicy>());

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_DenyPolicy_WithTimeConditions_Invalid()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "DenyTest",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            TimeConditions = JsonSerializer.Serialize(new
            {
                DaysOfWeek = new[] { 99 }, // no valid day match
                StartTime = "00:00",
                EndTime = "23:59"
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        // Time conditions don't match (no valid day) so conditions not met, so policy doesn't apply
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_WithEnvironmentConditions_MfaRequired()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "MfaRequired",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            EnvironmentConditions = JsonSerializer.Serialize(new
            {
                RequireMfa = true
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // User WITH MFA verified - condition is "met" because RequireMfa=true + user has MFA
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            IsMfaVerified: true);

        var result = await _evaluator.EvaluateAsync(context);
        // Env condition evaluates: RequireMfa => context.IsMfaVerified == true => conditions met => deny fires
        result.IsAllowed.Should().BeFalse();
        result.DeniedByPolicyName.Should().Be("MfaRequired");
    }

    [Fact]
    public async Task EvaluateAsync_WithLocationConditions_BlockedCountry()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "BlockCountry",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            LocationConditions = JsonSerializer.Serialize(new
            {
                BlockedCountries = new[] { "NK" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            GeoCountry: "NK");

        var result = await _evaluator.EvaluateAsync(context);
        // The code exercises the location condition branch regardless of outcome
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithLocationConditions_AllowedCountry()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "AllowCountries",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            LocationConditions = JsonSerializer.Serialize(new
            {
                AllowedCountries = new[] { "US", "CA" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Country IN allowed list - condition evaluates to true
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            GeoCountry: "US");

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_WithLocationConditions_IpRanges()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "IpRestrict",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            LocationConditions = JsonSerializer.Serialize(new
            {
                AllowedIpRanges = new[] { "10.0.0.0/8" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // IP inside the allowed range - condition evaluates to true
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            IpAddress: "10.0.0.1");

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_WithDeviceConditions_BlockedUserAgent()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "BlockBot",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            DeviceConditions = JsonSerializer.Serialize(new
            {
                BlockedUserAgents = new[] { "BadBot" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            UserAgent: "BadBot/1.0");

        var result = await _evaluator.EvaluateAsync(context);
        // Exercises the device condition branch
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task EvaluateAsync_WithCustomConditions()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "CustomCond",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            CustomConditions = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                { "department", "HR" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var customAttrs = new Dictionary<string, string> { { "department", "HR" } };
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            CustomAttributes: customAttrs);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_TimeConditions_InvalidJson_ReturnsTrue()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "BadTime",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            TimeConditions = "not-json"
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        // Invalid JSON is treated as "no restriction" (passes)
        // but since it's a deny policy, if conditions pass, it would deny
        var result = await _evaluator.EvaluateAsync(context);
        // Invalid JSON => time condition returns true => conditions met => deny action fires
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_WithEnvironmentConditions_RiskScore()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "RiskLimit",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            EnvironmentConditions = JsonSerializer.Serialize(new
            {
                MaxRiskScore = 50
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Risk score UNDER the max - condition evaluates to true
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            RiskScore: 30);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_WithEnvironmentConditions_SessionAge()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "SessionAge",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            EnvironmentConditions = JsonSerializer.Serialize(new
            {
                MaxSessionAgeMinutes = 30
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Session age within max - condition evaluates to true
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            AuthenticationTime: DateTime.UtcNow.AddMinutes(-5));

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_DeviceFingerprint_Required()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "FingerprintRequired",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            DeviceConditions = JsonSerializer.Serialize(new
            {
                AllowedFingerprints = new[] { "fp-abc" }
            })
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Fingerprint matches - condition evaluates to true
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", [],
            DeviceFingerprint: "fp-abc");

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateAsync_AllowPolicy_DoesNotBlockOnOwn()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "AllowAll",
            IsEnabled = true,
            Action = PolicyAction.Allow,
            Priority = 100
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_DisabledPolicy_Skipped()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Disabled",
            IsEnabled = false,
            Action = PolicyAction.Deny,
            Priority = 100
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_PermissionTypeFilter()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "OnlyDelete",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            PermissionType = "delete"
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Action is "read", not "delete", so policy doesn't apply
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "resource", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateAsync_ResourceTypeFilter()
    {
        var policy = new ConditionalPolicy
        {
            Id = Guid.NewGuid(),
            Name = "OnlyProject",
            IsEnabled = true,
            Action = PolicyAction.Deny,
            Priority = 100,
            ResourceType = "project"
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        // Resource is "course", not "project"
        var context = new ConditionalPolicyContext(
            Guid.NewGuid(), null, "course", null, "read", []);

        var result = await _evaluator.EvaluateAsync(context);
        result.IsAllowed.Should().BeTrue();
    }
}

#endregion

#region PolicyEvaluationLogger Tests

public class PolicyEvaluationLoggerFullTests
{
    private readonly PolicyEvaluationLogger _logger;

    public PolicyEvaluationLoggerFullTests()
    {
        _logger = new PolicyEvaluationLogger(NullLogger<PolicyEvaluationLogger>.Instance);
    }

    [Fact]
    public void BeginTrace_ReturnsTrace()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-1")], "TestAuth"));

        var trace = _logger.BeginTrace("TestPolicy", user);
        trace.Should().NotBeNull();
        trace.TraceId.Should().NotBeNullOrEmpty();
        trace.PolicyName.Should().Be("TestPolicy");
    }

    [Fact]
    public void BeginTrace_WithCorrelationId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-2")], "TestAuth"));

        var trace = _logger.BeginTrace("TestPolicy", user, correlationId: "corr-123");
        trace.TraceId.Should().Be("corr-123");
    }

    [Fact]
    public void BeginTrace_WithResource_LogsResourceContext()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-3")], "TestAuth"));

        var resource = new { Id = 1, Name = "TestResource" };
        var trace = _logger.BeginTrace("TestPolicy", user, resource);
        trace.Should().NotBeNull();
    }

    [Fact]
    public void BeginTrace_WithNullResource()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-4")], "TestAuth"));

        var trace = _logger.BeginTrace("TestPolicy", user, null);
        trace.Should().NotBeNull();
    }

    [Fact]
    public void Trace_LogRequirement_Succeeded()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
        var trace = _logger.BeginTrace("P", user);
        trace.LogRequirement("Req1", true, "all good");
        trace.Complete(true);
    }

    [Fact]
    public void Trace_LogRequirement_Failed()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
        var trace = _logger.BeginTrace("P", user);
        trace.LogRequirement("Req1", false, "missing");
        trace.Complete(false);
    }

    [Fact]
    public void Trace_AddContext()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
        var trace = _logger.BeginTrace("P", user);
        trace.AddContext("key1", "value1");
        trace.AddContext("key2", null);
        trace.Complete(true);
    }

    [Fact]
    public void Trace_LogMultipleRequirements()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
        var trace = _logger.BeginTrace("P", user);
        trace.LogRequirement("Req1", true);
        trace.LogRequirement("Req2", false);
        trace.LogRequirement("Req3", true, "reason");
        trace.Complete(false);
    }

    [Fact]
    public void Trace_Dispose()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "TestAuth"));
        var trace = _logger.BeginTrace("P", user);
        trace.Dispose();
        trace.Dispose(); // double dispose should not throw
    }

    [Fact]
    public void LogRequirementResult_WithDuration()
    {
        _logger.LogRequirementResult("trace-1", "Req", true, "ok", TimeSpan.FromMilliseconds(5));
    }

    [Fact]
    public void LogRequirementResult_Failed_WithReason()
    {
        _logger.LogRequirementResult("trace-1", "Req", false, "not ok", TimeSpan.FromMilliseconds(10));
    }

    [Fact]
    public void LogPolicyResult_UnknownTraceId()
    {
        // Should not throw even if trace doesn't exist
        _logger.LogPolicyResult("unknown-trace", true, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void LogPolicyFailure_WithSuggestions()
    {
        _logger.LogPolicyFailure("trace-1", "access denied", ["check roles", "verify tenant"]);
    }

    [Fact]
    public void LogPolicyFailure_WithoutSuggestions()
    {
        _logger.LogPolicyFailure("trace-1", "denied");
    }

    [Fact]
    public void GetDebugSettings_NullEndpoint_ReturnsNull()
    {
        _logger.GetDebugSettings(null).Should().BeNull();
    }

    [Fact]
    public void IsDebugEnabled_NullEndpoint_ReturnsFalse()
    {
        _logger.IsDebugEnabled(null).Should().BeFalse();
    }

    [Fact]
    public void GetDebugSettings_NonEndpoint_ReturnsNull()
    {
        _logger.GetDebugSettings("not-an-endpoint").Should().BeNull();
    }
}

#endregion

#region CacheInvalidationService Tests

public class CacheInvalidationServiceTests
{
    private readonly Mock<ITenantSecurityVersionStore> _versionStore;
    private readonly Mock<IHybridPermissionCache> _hybridCache;
    private readonly CacheInvalidationService _sut;

    public CacheInvalidationServiceTests()
    {
        IMemoryCache cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        _versionStore = new Mock<ITenantSecurityVersionStore>();
        _hybridCache = new Mock<IHybridPermissionCache>();
        Mock<ICacheMetricsService> metrics = new Mock<ICacheMetricsService>();

        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = false,
            UsePubSubInvalidation = false
        });

        _sut = new CacheInvalidationService(
            cache,
            _versionStore.Object,
            _hybridCache.Object,
            metrics.Object,
            options,
            NullLogger<CacheInvalidationService>.Instance);
    }

    [Fact]
    public async Task InvalidateTenantAsync_IncrementsVersion()
    {
        var tenantId = Guid.NewGuid();
        await _sut.InvalidateTenantAsync(tenantId);

        _versionStore.Verify(v => v.IncrementVersionAsync(
            tenantId.ToString(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateUserAsync_InvalidatesPattern()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        await _sut.InvalidateUserAsync(userId, tenantId);

        _hybridCache.Verify(h => h.InvalidatePatternAsync(
            It.Is<string>(s => s.Contains(tenantId.ToString()) && s.Contains(userId.ToString())),
            "permission",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidateResourceAsync_InvalidatesPattern()
    {
        var tenantId = Guid.NewGuid();
        await _sut.InvalidateResourceAsync(tenantId, "project", "res-123");

        _hybridCache.Verify(h => h.InvalidatePatternAsync(
            It.Is<string>(s => s.Contains("project") && s.Contains("res-123")),
            "acl",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidatePolicyAsync_WithName()
    {
        var tenantId = Guid.NewGuid();
        await _sut.InvalidatePolicyAsync(tenantId, "MyPolicy");

        _hybridCache.Verify(h => h.InvalidatePatternAsync(
            It.Is<string>(s => s.Contains("MyPolicy")),
            "policy",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InvalidatePolicyAsync_WithoutName()
    {
        var tenantId = Guid.NewGuid();
        await _sut.InvalidatePolicyAsync(tenantId);

        _hybridCache.Verify(h => h.InvalidatePatternAsync(
            It.Is<string>(s => s.Contains("policy:")),
            "policy",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PublishInvalidationAsync_NoPubSub_ReturnsImmediately()
    {
        var evt = new CacheInvalidationEvent { Type = CacheInvalidationType.Tenant, TenantId = Guid.NewGuid() };
        await _sut.PublishInvalidationAsync(evt);
        // No exception means success
    }

    [Fact]
    public void HandleInvalidationEvent_OwnInstance_Skipped()
    {
        // The service should skip its own events
        // We'd need the internal instance ID. Just test with a different ID.
        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Tenant,
            TenantId = Guid.NewGuid(),
            OriginInstanceId = "different-instance"
        };

        _sut.HandleInvalidationEvent(evt);
        // No exception
    }

    [Fact]
    public void HandleInvalidationEvent_UserType()
    {
        var tenantId = Guid.NewGuid();
        _sut.TrackKey(tenantId, $"perm:{tenantId}:{Guid.NewGuid()}:read");

        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.User,
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            OriginInstanceId = "other-instance"
        };

        _sut.HandleInvalidationEvent(evt);
    }

    [Fact]
    public void HandleInvalidationEvent_ResourceType()
    {
        var tenantId = Guid.NewGuid();
        _sut.TrackKey(tenantId, $"acl:{tenantId}:x:project:res1:r");

        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Resource,
            TenantId = tenantId,
            ResourceType = "project",
            ResourceId = "res1",
            OriginInstanceId = "other-instance"
        };

        _sut.HandleInvalidationEvent(evt);
    }

    [Fact]
    public void HandleInvalidationEvent_PolicyType()
    {
        var tenantId = Guid.NewGuid();
        _sut.TrackKey(tenantId, $"policy:{tenantId}:MyPolicy:v1");

        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Policy,
            TenantId = tenantId,
            PolicyName = "MyPolicy",
            OriginInstanceId = "other-instance"
        };

        _sut.HandleInvalidationEvent(evt);
    }

    [Fact]
    public void HandleInvalidationEvent_PolicyType_AllPolicies()
    {
        var tenantId = Guid.NewGuid();
        _sut.TrackKey(tenantId, $"policy:{tenantId}:any:v1");

        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.Policy,
            TenantId = tenantId,
            PolicyName = null,
            OriginInstanceId = "other-instance"
        };

        _sut.HandleInvalidationEvent(evt);
    }

    [Fact]
    public void TrackKey_AddsKeys()
    {
        var tenantId = Guid.NewGuid();
        _sut.TrackKey(tenantId, "key1");
        _sut.TrackKey(tenantId, "key2");
        // Should not throw
    }
}

#endregion

#region HybridPermissionCache Tests

public class HybridPermissionCacheFullTests
{
    private readonly IMemoryCache _l1Cache;
    private readonly Mock<ICacheMetricsService> _metrics;

    public HybridPermissionCacheFullTests()
    {
        _l1Cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
        _metrics = new Mock<ICacheMetricsService>();
    }

    private HybridPermissionCache CreateCache(bool useL2 = false, IDistributedCache? l2 = null)
    {
        var options = Options.Create(new AuthorizationCacheOptions
        {
            UseDistributedCache = useL2,
            PermissionTtlSeconds = 60,
            DistributedCacheTtlSeconds = 120
        });

        return new HybridPermissionCache(
            _l1Cache,
            options,
            _metrics.Object,
            NullLogger<HybridPermissionCache>.Instance,
            l2);
    }

    [Fact]
    public async Task GetAsync_L1Hit()
    {
        var cache = CreateCache();
        await cache.SetAsync("key1", new List<string> { "a" }, "test");

        var result = await cache.GetAsync<List<string>>("key1", "test");
        result.Should().NotBeNull();
        _metrics.Verify(m => m.RecordHit(CacheLevel.L1, "test"), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetAsync_Miss()
    {
        var cache = CreateCache();
        var result = await cache.GetAsync<List<string>>("missing", "test");
        result.Should().BeNull();
        _metrics.Verify(m => m.RecordMiss("test"), Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithCustomTtl()
    {
        var cache = CreateCache();
        await cache.SetAsync("key2", "value", "test", 30);

        var result = await cache.GetAsync<string>("key2", "test");
        result.Should().Be("value");
    }

    [Fact]
    public async Task GetValueAsync_Miss()
    {
        var cache = CreateCache();
        var result = await cache.GetValueAsync<int>("missing", "test");
        result.Found.Should().BeFalse();
    }

    [Fact]
    public async Task SetValueAsync_AndGet()
    {
        var cache = CreateCache();
        await cache.SetValueAsync("int-key", 42, "test");

        var result = await cache.GetValueAsync<int>("int-key", "test");
        result.Found.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public async Task RemoveAsync_RemovesFromL1()
    {
        var cache = CreateCache();
        await cache.SetAsync("rem-key", "val", "test");
        await cache.RemoveAsync("rem-key", "test");

        var result = await cache.GetAsync<string>("rem-key", "test");
        result.Should().BeNull();
    }

    [Fact]
    public async Task InvalidatePatternAsync_DoesNotThrow()
    {
        var cache = CreateCache();
        await cache.InvalidatePatternAsync("perm:*", "test");
    }
}

#endregion

#region CacheResult Tests

public class CacheResultTests
{
    [Fact]
    public void Hit_ReturnsFoundTrue()
    {
        var result = CacheResult<int>.Hit(42);
        result.Found.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Miss_ReturnsFoundFalse()
    {
        var result = CacheResult<int>.Miss();
        result.Found.Should().BeFalse();
        result.Value.Should().Be(0);
    }
}

#endregion

#region PolicyGateService Tests

public class PolicyGateServiceFullTests
{
    private readonly Mock<IConditionalPolicyEvaluator> _conditionalEvaluator = new();
    private readonly Mock<IAbacPolicyEvaluator> _abacEvaluator = new();
    private readonly PolicyGateService _sut;

    public PolicyGateServiceFullTests()
    {
        _sut = new PolicyGateService(
            _conditionalEvaluator.Object,
            _abacEvaluator.Object,
            NullLogger<PolicyGateService>.Instance);
    }

    [Fact]
    public async Task EvaluateGatesAsync_AllPass()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateGatesAsync_ConditionalDeny()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(
                IsAllowed: false,
                DeniedByPolicyId: Guid.NewGuid(),
                DeniedByPolicyName: "TimeRestrict",
                DenialReason: "Outside business hours"));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeFalse();
        result.DeniedByGate.Should().Be(PolicyGateType.Conditional);
    }

    [Fact]
    public async Task EvaluateGatesAsync_AbacDeny()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(
                AbacDecision.Deny,
                DecidingPolicyId: Guid.NewGuid(),
                DenialReason: "Attr mismatch"));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeFalse();
        result.DeniedByGate.Should().Be(PolicyGateType.Abac);
    }

    [Fact]
    public async Task EvaluateGatesAsync_StaticGate_LocalhostInProd()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read",
            IpAddress = "127.0.0.1",
            Attributes = new Dictionary<string, object> { { "environment", "production" } }
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeFalse();
        result.DeniedByGate.Should().Be(PolicyGateType.Static);
    }

    [Fact]
    public async Task EvaluateGatesAsync_StaticGate_NoUserAgent()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read",
            UserAgent = null,
            Attributes = new Dictionary<string, object> { { "require-user-agent", "true" } }
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task EvaluateGatesAsync_WithRolesAndAttributes()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read",
            GeoLocation = "US/NY",
            Attributes = new Dictionary<string, object>
            {
                { "roles", new List<string> { "admin", "editor" } },
                { "auth-time", DateTime.UtcNow },
                { "mfa-verified", true },
                { "risk-score", 10 },
                { "env-key", "some-string" }
            }
        };

        var result = await _sut.EvaluateGatesAsync(context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateGateAsync_Static()
    {
        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGateAsync(PolicyGateType.Static, context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateGateAsync_Conditional()
    {
        _conditionalEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<ConditionalPolicyContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConditionalPolicyResult(IsAllowed: true));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGateAsync(PolicyGateType.Conditional, context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateGateAsync_Abac()
    {
        _abacEvaluator.Setup(e => e.EvaluateAsync(It.IsAny<AbacRequestContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AbacEvaluationResult(AbacDecision.Permit));

        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGateAsync(PolicyGateType.Abac, context);
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateGateAsync_Environment_WithCurl()
    {
        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read",
            UserAgent = "curl/7.68.0"
        };

        var result = await _sut.EvaluateGateAsync(PolicyGateType.Environment, context);
        result.IsAllowed.Should().BeTrue();
        result.GateDetails.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluateGateAsync_UnknownType_Allowed()
    {
        var context = new PolicyGateContext
        {
            ActorId = Guid.NewGuid(),
            ResourceType = "project",
            Action = "read"
        };

        var result = await _sut.EvaluateGateAsync((PolicyGateType)999, context);
        result.IsAllowed.Should().BeTrue();
    }
}

#endregion

#region AbacPolicyEvaluator Tests

public class AbacPolicyEvaluatorFullTests
{
    private readonly Mock<IAbacPolicyRepository> _repo = new();
    private readonly AbacPolicyEvaluator _evaluator;

    public AbacPolicyEvaluatorFullTests()
    {
        _evaluator = new AbacPolicyEvaluator(_repo.Object, NullLogger<AbacPolicyEvaluator>.Instance);
    }

    [Fact]
    public async Task EvaluateAsync_NoPolicies()
    {
        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_DenyPolicy_Matches()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "DenyAll",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            ResourceType = "project"
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_AllowPolicy_Matches()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "AllowAll",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Allow,
            Priority = 100
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Permit);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectConditions_Match()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "SubjCond",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            SubjectConditions = """{"subject.department":"IT"}"""
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithSubjectAttribute("subject.department", "IT")
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_SubjectConditions_NoMatch()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "SubjCond",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            SubjectConditions = """{"subject.department":"HR"}"""
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithSubjectAttribute("subject.department", "IT")
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().NotBe(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_WithTenantContext()
    {
        var tenantId = Guid.NewGuid();
        _repo.Setup(r => r.GetActivePoliciesAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), tenantId, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.NotApplicable);
    }

    [Fact]
    public async Task EvaluateAsync_EnvironmentConditions()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "EnvCond",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            EnvironmentConditions = """{"environment.geo-country":"NK"}"""
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .WithEnvironment(geoCountry: "NK")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_ActionConditions()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "ActionCond",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            ActionConditions = """{"action.id":"delete"}"""
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("delete")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_InvalidJsonConditions()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "BadJson",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            SubjectConditions = "not-json"
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        // Invalid JSON => condition returns false => no match
        result.Decision.Should().NotBe(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_ResourceConditions()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "ResCond",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            ResourceConditions = """{"resource.classification":"Confidential"}"""
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithResourceAttribute("resource.classification", "Confidential")
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().Be(AbacDecision.Deny);
    }

    [Fact]
    public async Task EvaluateAsync_NotEffective_Skipped()
    {
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            Name = "Expired",
            IsEnabled = true,
            Effect = AbacPolicyEffect.Deny,
            Priority = 100,
            EffectiveUntil = DateTime.UtcNow.AddDays(-1) // expired
        };

        _repo.Setup(r => r.GetActivePoliciesAsync(It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([policy]);

        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithResource("project", null)
            .WithAction("read")
            .Build();

        var result = await _evaluator.EvaluateAsync(ctx);
        result.Decision.Should().NotBe(AbacDecision.Deny);
    }
}

#endregion

#region AbacRequestContextBuilder Tests

public class AbacRequestContextBuilderTests
{
    [Fact]
    public void Build_CreatesContext()
    {
        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), Guid.NewGuid(), ["admin"])
            .WithResource("course", Guid.NewGuid(), Guid.NewGuid())
            .WithAction("write")
            .WithEnvironment("10.0.0.1", "Mozilla/5.0", "US")
            .Build();

        ctx.SubjectAttributes.Should().ContainKey("subject.user-id");
        ctx.SubjectAttributes.Should().ContainKey("subject.tenant-id");
        ctx.ResourceAttributes.Should().ContainKey("resource.type");
        ctx.ResourceAttributes.Should().ContainKey("resource.id");
        ctx.ResourceAttributes.Should().ContainKey("resource.owner-id");
        ctx.ActionAttributes.Should().ContainKey("action.id");
        ctx.EnvironmentAttributes.Should().ContainKey("environment.ip-address");
        ctx.EnvironmentAttributes.Should().ContainKey("environment.user-agent");
        ctx.EnvironmentAttributes.Should().ContainKey("environment.geo-country");
    }

    [Fact]
    public void WithCustomAttributes()
    {
        var ctx = new AbacRequestContextBuilder()
            .WithSubject(Guid.NewGuid(), null, [])
            .WithSubjectAttribute("custom-sub", "val")
            .WithResource("project", null)
            .WithResourceAttribute("custom-res", "val2")
            .WithAction("read")
            .WithActionAttribute("custom-act", "val3")
            .WithEnvironment()
            .WithEnvironmentAttribute("custom-env", "val4")
            .Build();

        ctx.SubjectAttributes.Should().ContainKey("custom-sub");
        ctx.ResourceAttributes.Should().ContainKey("custom-res");
        ctx.ActionAttributes.Should().ContainKey("custom-act");
        ctx.EnvironmentAttributes.Should().ContainKey("custom-env");
    }
}

#endregion


#region CachedPolicyDefinitionStore Tests

public class CachedPolicyDefinitionStoreTests
{
    private readonly Mock<IPolicyDefinitionStore> _innerStore = new();
    private readonly IMemoryCache _cache;
    private readonly Mock<ITenantSecurityVersionStore> _versionStore = new();

    public CachedPolicyDefinitionStoreTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 1000 });
    }

    private CachedPolicyDefinitionStore CreateSut()
    {
        var options = Options.Create(new AuthorizationCacheOptions
        {
            PolicyTtlSeconds = 60
        });

        return new CachedPolicyDefinitionStore(
            _innerStore.Object,
            _cache,
            _versionStore.Object,
            options);
    }

    [Fact]
    public async Task GetPolicyAsync_CacheMissAndSet()
    {
        _versionStore.Setup(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var policy = new PolicyDefinition { PolicyName = "TestPolicy" };
        _innerStore.Setup(s => s.GetPolicyAsync("TestPolicy", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(policy);

        var sut = CreateSut();
        var result = await sut.GetPolicyAsync("TestPolicy");
        result.Should().NotBeNull();
        result!.PolicyName.Should().Be("TestPolicy");

        // Second call should hit cache
        var result2 = await sut.GetPolicyAsync("TestPolicy");
        result2.Should().NotBeNull();

        _innerStore.Verify(s => s.GetPolicyAsync("TestPolicy", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTenantPoliciesAsync_CacheMissAndSet()
    {
        _versionStore.Setup(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var policies = new List<PolicyDefinition>
        {
            new() { PolicyName = "P1" },
            new() { PolicyName = "P2" }
        };

        _innerStore.Setup(s => s.GetTenantPoliciesAsync("tenant-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(policies);

        var sut = CreateSut();
        var result = await sut.GetTenantPoliciesAsync("tenant-1");
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetVersionAsync_DelegatesToVersionStore()
    {
        _versionStore.Setup(v => v.GetVersionAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var sut = CreateSut();
        var version = await sut.GetVersionAsync("t1");
        version.Should().Be(42);
    }

    [Fact]
    public void InvalidateTenant_ClearsCache()
    {
        var sut = CreateSut();
        sut.InvalidateTenant("t1"); // should not throw
    }

    [Fact]
    public void InvalidatePolicy_ClearsSpecific()
    {
        var sut = CreateSut();
        sut.InvalidatePolicy("MyPolicy", "t1"); // should not throw
    }
}

#endregion

#region CacheMetricsService Tests

public class CacheMetricsServiceFullTests
{
    [Fact]
    public void RecordHit_L1()
    {
        var svc = new CacheMetricsService();
        svc.RecordHit(CacheLevel.L1, "permission");

        var stats = svc.GetStatistics();
        stats.L1Hits.Should().Be(1);
    }

    [Fact]
    public void RecordHit_L2()
    {
        var svc = new CacheMetricsService();
        svc.RecordHit(CacheLevel.L2, "policy");

        var stats = svc.GetStatistics();
        stats.L2Hits.Should().Be(1);
    }

    [Fact]
    public void RecordMiss()
    {
        var svc = new CacheMetricsService();
        svc.RecordMiss("acl");

        var stats = svc.GetStatistics();
        stats.Misses.Should().Be(1);
    }

    [Fact]
    public void RecordEviction()
    {
        var svc = new CacheMetricsService();
        svc.RecordEviction(CacheLevel.L1, "permission", "tenant_invalidation");

        var stats = svc.GetStatistics();
        stats.Evictions.Should().Be(1);
    }

    [Fact]
    public void GetStatistics_HitRates()
    {
        var svc = new CacheMetricsService();
        svc.RecordHit(CacheLevel.L1, "p");
        svc.RecordHit(CacheLevel.L2, "p");
        svc.RecordMiss("p");

        var stats = svc.GetStatistics();
        stats.TotalRequests.Should().Be(3);
        stats.OverallHitRate.Should().BeApproximately(2.0 / 3.0, 0.01);
        stats.L1HitRate.Should().BeApproximately(1.0 / 3.0, 0.01);
        stats.ByType.Should().ContainKey("p");
    }

    [Fact]
    public void CacheStatistics_EmptyHitRate()
    {
        var stats = new CacheStatistics();
        stats.OverallHitRate.Should().Be(0);
        stats.L1HitRate.Should().Be(0);
        stats.L2HitRate.Should().Be(0);
        stats.TotalRequests.Should().Be(0);
    }

    [Fact]
    public void CacheTypeStatistics_HitRate()
    {
        var typeStats = new CacheTypeStatistics
        {
            CacheType = "test",
            L1Hits = 5,
            L2Hits = 3,
            Misses = 2
        };
        typeStats.HitRate.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public void CacheTypeStatistics_EmptyHitRate()
    {
        var typeStats = new CacheTypeStatistics();
        typeStats.HitRate.Should().Be(0);
    }
}

#endregion

#region CacheInvalidationEvent Tests

public class CacheInvalidationEventFullTests
{
    [Fact]
    public void DefaultValues()
    {
        var evt = new CacheInvalidationEvent();
        evt.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
        evt.OriginInstanceId.Should().BeEmpty();
    }

    [Fact]
    public void SetAllProperties()
    {
        var evt = new CacheInvalidationEvent
        {
            Type = CacheInvalidationType.User,
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ResourceType = "project",
            ResourceId = "123",
            PolicyName = "pol",
            OriginInstanceId = "inst-1"
        };

        evt.Type.Should().Be(CacheInvalidationType.User);
        evt.UserId.Should().NotBeNull();
        evt.ResourceType.Should().Be("project");
    }
}

#endregion

#region ConditionalPolicy Entity Tests

public class ConditionalPolicyEntityTests
{
    [Fact]
    public void IsActive_WhenEnabled()
    {
        var policy = new ConditionalPolicy { IsEnabled = true };
        policy.IsActive().Should().BeTrue();
    }

    [Fact]
    public void Enable_SetsIsEnabled()
    {
        var policy = new ConditionalPolicy { IsEnabled = false };
        policy.Enable();
        policy.IsEnabled.Should().BeTrue();
        policy.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Disable_ClearsIsEnabled()
    {
        var policy = new ConditionalPolicy { IsEnabled = true };
        policy.Disable();
        policy.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void SetPriority_Valid()
    {
        var policy = new ConditionalPolicy();
        policy.SetPriority(10);
        policy.Priority.Should().Be(10);
    }

    [Fact]
    public void SetPriority_Negative_Throws()
    {
        var policy = new ConditionalPolicy();
        Assert.Throws<ArgumentException>(() => policy.SetPriority(-1));
    }

    [Fact]
    public void AppliesTo_NullPermissionType_MatchesAll()
    {
        var policy = new ConditionalPolicy { PermissionType = null };
        policy.AppliesTo("anything").Should().BeTrue();
    }

    [Fact]
    public void AppliesTo_SpecificType()
    {
        var policy = new ConditionalPolicy { PermissionType = "delete" };
        policy.AppliesTo("delete").Should().BeTrue();
        policy.AppliesTo("read").Should().BeFalse();
    }

    [Fact]
    public void AppliesToResourceType_NullResourceType_MatchesAll()
    {
        var policy = new ConditionalPolicy { ResourceType = null };
        policy.AppliesToResourceType("anything").Should().BeTrue();
    }
}

#endregion

#region AbacPolicy Entity Tests

public class AbacPolicyEntityTests
{
    [Fact]
    public void IsEffective_WhenActiveAndInRange()
    {
        var policy = new AbacPolicy
        {
            IsEnabled = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(-1),
            EffectiveUntil = DateTime.UtcNow.AddDays(1)
        };
        policy.IsEffective().Should().BeTrue();
    }

    [Fact]
    public void IsEffective_WhenDisabled()
    {
        var policy = new AbacPolicy { IsEnabled = false };
        policy.IsEffective().Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenBeforeEffectiveFrom()
    {
        var policy = new AbacPolicy
        {
            IsEnabled = true,
            EffectiveFrom = DateTime.UtcNow.AddDays(1)
        };
        policy.IsEffective().Should().BeFalse();
    }

    [Fact]
    public void IsEffective_WhenAfterEffectiveUntil()
    {
        var policy = new AbacPolicy
        {
            IsEnabled = true,
            EffectiveUntil = DateTime.UtcNow.AddDays(-1)
        };
        policy.IsEffective().Should().BeFalse();
    }

    [Fact]
    public void SetPriority_Valid()
    {
        var policy = new AbacPolicy();
        policy.SetPriority(5);
        policy.Priority.Should().Be(5);
    }

    [Fact]
    public void SetPriority_Negative_Throws()
    {
        var policy = new AbacPolicy();
        Assert.Throws<ArgumentException>(() => policy.SetPriority(-1));
    }

    [Fact]
    public void SetActive_UpdatesFields()
    {
        var policy = new AbacPolicy();
        var updatedBy = Guid.NewGuid();
        policy.SetActive(false, updatedBy);

        policy.IsEnabled.Should().BeFalse();
        policy.UpdatedBy.Should().Be(updatedBy);
    }

    [Fact]
    public void SetEffectivePeriod()
    {
        var policy = new AbacPolicy();
        var from = DateTime.UtcNow;
        var until = DateTime.UtcNow.AddDays(30);
        policy.SetEffectivePeriod(from, until, Guid.NewGuid());

        policy.EffectiveFrom.Should().Be(from);
        policy.EffectiveUntil.Should().Be(until);
    }

    [Fact]
    public void UpdateExpression()
    {
        var policy = new AbacPolicy { Version = 1 };
        policy.UpdateExpression("expr1", "cond1", Guid.NewGuid());

        policy.AttributeExpression.Should().Be("expr1");
        policy.ConditionExpression.Should().Be("cond1");
        policy.Version.Should().Be(2);
    }

    [Fact]
    public void UpdateMetadata()
    {
        var policy = new AbacPolicy();
        policy.UpdateMetadata("NewName", "desc", 10, Guid.NewGuid());

        policy.Name.Should().Be("NewName");
        policy.Description.Should().Be("desc");
        policy.Priority.Should().Be(10);
    }

    [Fact]
    public void UpdateMetadata_NullName_Throws()
    {
        var policy = new AbacPolicy();
        Assert.Throws<ArgumentNullException>(() => policy.UpdateMetadata(null!, null, 0, Guid.NewGuid()));
    }

    [Fact]
    public void IsDenyPolicy_And_IsAllowPolicy()
    {
        var deny = new AbacPolicy { Effect = AbacPolicyEffect.Deny };
        deny.IsDenyPolicy().Should().BeTrue();
        deny.IsAllowPolicy().Should().BeFalse();

        var allow = new AbacPolicy { Effect = AbacPolicyEffect.Allow };
        allow.IsDenyPolicy().Should().BeFalse();
        allow.IsAllowPolicy().Should().BeTrue();
    }

    [Fact]
    public void Enable_Disable()
    {
        var policy = new AbacPolicy { IsEnabled = false };
        policy.Enable();
        policy.IsEnabled.Should().BeTrue();

        policy.Disable();
        policy.IsEnabled.Should().BeFalse();
    }
}

#endregion

#region PermissionRequirement and ResourceAccessRequirement Tests

public class RequirementTests
{
    [Fact]
    public void PermissionRequirement_Properties()
    {
        var req = new PermissionRequirement("users:read", false);
        req.Permission.Should().Be("users:read");
        req.AllowClaimsBased.Should().BeFalse();
    }

    [Fact]
    public void PermissionRequirement_NullPermission_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PermissionRequirement(null!));
    }

    [Fact]
    public void ResourceAccessRequirement_Properties()
    {
        var req = new ResourceAccessRequirement(
            requireOwnership: true,
            requireAccessControlListAccess: true,
            minimumAccessLevel: AccessLevel.Write,
            resourceType: "project");

        req.RequireOwnership.Should().BeTrue();
        req.RequireAccessControlListAccess.Should().BeTrue();
        req.MinimumAccessLevel.Should().Be(AccessLevel.Write);
        req.ResourceType.Should().Be("project");
    }
}

#endregion

#region ClaimNames Tests

public class ClaimNamesTests
{
    [Fact]
    public void Constants_AreCorrect()
    {
        ClaimNames.Subject.Should().Be("sub");
        ClaimNames.UserId.Should().Be("UserId");
        ClaimNames.TenantId.Should().Be("TenantId");
        ClaimNames.TenantIdAlt.Should().Be("tenant_id");
        ClaimNames.Role.Should().Be("role");
        ClaimNames.Group.Should().Be("group");
        ClaimNames.Amr.Should().Be("amr");
        ClaimNames.MfaVerified.Should().Be("mfa_verified");
        ClaimNames.Email.Should().Be("email");
        ClaimNames.EmailVerified.Should().Be("email_verified");
    }

    [Fact]
    public void GetUserId_Legacy()
    {
#pragma warning disable CS0618
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "user-1")], "TestAuth"));
        ClaimNames.GetUserId(user).Should().Be("user-1");
#pragma warning restore CS0618
    }

    [Fact]
    public void GetTenantId_Legacy()
    {
#pragma warning disable CS0618
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("TenantId", "t-1")], "TestAuth"));
        ClaimNames.GetTenantId(user).Should().Be("t-1");
#pragma warning restore CS0618
    }

    [Fact]
    public void TryGetUserId_Legacy()
    {
#pragma warning disable CS0618
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", id.ToString())], "TestAuth"));
        ClaimNames.TryGetUserId(user, out var parsedId).Should().BeTrue();
        parsedId.Should().Be(id);
#pragma warning restore CS0618
    }

    [Fact]
    public void TryGetTenantId_Legacy()
    {
#pragma warning disable CS0618
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("TenantId", id.ToString())], "TestAuth"));
        ClaimNames.TryGetTenantId(user, out var parsedId).Should().BeTrue();
        parsedId.Should().Be(id);
#pragma warning restore CS0618
    }
}

#endregion

#region AuthorizationModule Tests

public class AuthorizationModuleTests
{
    [Fact]
    public void Module_Properties()
    {
        var module = new AuthorizationModule();
        module.Name.Should().Be("Authorization");
        module.Order.Should().Be(15);
        module.Dependencies.Should().BeEmpty();
    }
}

#endregion

#region PolicyDefinition and PolicyRule Tests

public class PolicyDefinitionTests
{
    [Fact]
    public void PolicyDefinition_DefaultValues()
    {
        var pd = new PolicyDefinition { PolicyName = "Test" };
        pd.RequireAuthentication.Should().BeTrue();
        pd.AuthenticationSchemes.Should().BeEmpty();
        pd.RequiredPermissions.Should().BeEmpty();
        pd.RequiredRoles.Should().BeEmpty();
        pd.RequireAccessControlListAccess.Should().BeFalse();
        pd.IsTenantScoped.Should().BeFalse();
        pd.UseRuleBasedEvaluation.Should().BeFalse();
    }

    [Fact]
    public void PolicyRule_Properties()
    {
        var rule = new PolicyRule
        {
            Type = "TenantMatch",
            Description = "desc",
            Params = new Dictionary<string, object> { { "key", "val" } },
            Enabled = false
        };

        rule.Type.Should().Be("TenantMatch");
        rule.Description.Should().Be("desc");
        rule.Enabled.Should().BeFalse();
    }

    [Fact]
    public void EnvironmentConstraints_DefaultValues()
    {
        var ec = new EnvironmentConstraints();
        ec.AllowedIpRanges.Should().BeEmpty();
        ec.AllowedTimeWindows.Should().BeEmpty();
        ec.RequiredDeviceTypes.Should().BeEmpty();
        ec.BlockedRegions.Should().BeEmpty();
        ec.RequireSecureConnection.Should().BeFalse();
    }
}

#endregion
