using FluentAssertions;
using GameGuild.Compliance.Audit;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

/// <summary>
///     Unit tests for the security event taxonomy: classification, severity escalation,
/// declared-risk precedence, fallback classification, and security relevance.
/// </summary>
public sealed class SecurityEventTaxonomyTests
{
    [Fact]
    public void Classify_KnownAction_UsesItsDescriptor()
    {
        var classified = SecurityEventTaxonomy.Classify(
            AuditActionTypes.MfaDisabled, AuditCategory.Authentication, success: true);

        classified.Kind.Should().Be(SecurityEventKind.Authentication);
        classified.Severity.Should().Be(AuditRiskLevel.High);
        classified.IsSecurityRelevant.Should().BeTrue();
        classified.ActionType.Should().Be(AuditActionTypes.MfaDisabled);
        classified.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Classify_FailedOutcome_EscalatesSeverityForEscalatingActions()
    {
        var succeeded = SecurityEventTaxonomy.Classify(
            AuditActionTypes.Login, AuditCategory.Authentication, success: true);
        var failed = SecurityEventTaxonomy.Classify(
            AuditActionTypes.Login, AuditCategory.Authentication, success: false);

        succeeded.Severity.Should().Be(AuditRiskLevel.Low);
        failed.Severity.Should().Be(AuditRiskLevel.Medium);
    }

    [Fact]
    public void Classify_FailedOutcome_DoesNotEscalateNonEscalatingActions()
    {
        var failed = SecurityEventTaxonomy.Classify(
            AuditActionTypes.LoginFailed, AuditCategory.Authentication, success: false);

        failed.Severity.Should().Be(AuditRiskLevel.High);
    }

    [Fact]
    public void Classify_DeclaredRiskLevel_TakesPrecedenceWhenHigher()
    {
        var classified = SecurityEventTaxonomy.Classify(
            AuditActionTypes.Logout, AuditCategory.Authentication, success: true,
            declaredRiskLevel: AuditRiskLevel.Critical);

        classified.Severity.Should().Be(AuditRiskLevel.Critical);
    }

    [Theory]
    [InlineData(AuditCategory.Authentication, SecurityEventKind.Authentication, true)]
    [InlineData(AuditCategory.Authorization, SecurityEventKind.Authorization, true)]
    [InlineData(AuditCategory.Permission, SecurityEventKind.Authorization, true)]
    [InlineData(AuditCategory.Security, SecurityEventKind.ThreatDetection, true)]
    [InlineData(AuditCategory.Admin, SecurityEventKind.ConfigurationChange, true)]
    [InlineData(AuditCategory.Privacy, SecurityEventKind.DataProtection, false)]
    [InlineData(AuditCategory.Tenant, SecurityEventKind.ConfigurationChange, false)]
    [InlineData(AuditCategory.General, SecurityEventKind.Other, false)]
    public void Classify_UnknownAction_FallsBackToCategoryDerivedKind(
        AuditCategory category, SecurityEventKind expectedKind, bool expectedSecurityRelevant)
    {
        var classified = SecurityEventTaxonomy.Classify("BrandNewAction", category, success: true);

        classified.Kind.Should().Be(expectedKind);
        classified.ActionType.Should().Be("BrandNewAction");
        classified.IsSecurityRelevant.Should().Be(expectedSecurityRelevant);
    }

    [Fact]
    public void Classify_EmptyActionType_ClassifiesAsOther()
    {
        var classified = SecurityEventTaxonomy.Classify(string.Empty, AuditCategory.General, success: true);

        classified.Kind.Should().Be(SecurityEventKind.Other);
        classified.Severity.Should().Be(AuditRiskLevel.Low);
        classified.IsSecurityRelevant.Should().BeFalse();
    }

    [Fact]
    public void Classify_HighSeverityUnknownEvent_IsSecurityRelevant()
    {
        var classified = SecurityEventTaxonomy.Classify(
            "NovelDanger", AuditCategory.General, success: true, declaredRiskLevel: AuditRiskLevel.High);

        classified.IsSecurityRelevant.Should().BeTrue();
    }

    [Fact]
    public void GetTaxonomy_ReturnsEveryExplicitDescriptorWithStableFields()
    {
        var taxonomy = SecurityEventTaxonomy.GetTaxonomy();

        taxonomy.Should().NotBeEmpty();
        taxonomy.Should().OnlyHaveUniqueItems(entry => entry.ActionType);
        taxonomy.Should().OnlyContain(entry => !string.IsNullOrWhiteSpace(entry.Description));
        taxonomy.Should().OnlyContain(entry => Enum.IsDefined(entry.DefaultSeverity));

        var tenantIsolation = taxonomy.Single(entry => entry.ActionType == AuditActionTypes.TenantIsolationBypassed);
        tenantIsolation.Kind.Should().Be(SecurityEventKind.TenantIsolation);
        tenantIsolation.DefaultSeverity.Should().Be(AuditRiskLevel.Critical);
    }

    [Fact]
    public void IsSecurityKind_MatchesTheSecurityEventKinds()
    {
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.Authentication).Should().BeTrue();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.Authorization).Should().BeTrue();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.SessionManagement).Should().BeTrue();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.ThreatDetection).Should().BeTrue();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.TenantIsolation).Should().BeTrue();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.DataProtection).Should().BeFalse();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.ConfigurationChange).Should().BeFalse();
        SecurityEventTaxonomy.IsSecurityKind(SecurityEventKind.Other).Should().BeFalse();
    }
}
