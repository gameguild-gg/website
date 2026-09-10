using FluentAssertions;
using GameGuild.Finance.Economy.Risk;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class EconomyProtectedRiskPolicyTests
{
    [Fact]
    public void Parse_RequiresExplicitSafeSettingsAndMaterializesLimits()
    {
        const string payload = """
            {
              "riskDecisionLifetimeSeconds": 300,
              "riskReviewRequiredApprovals": 2,
              "complianceHoldSeconds": 86400,
              "riskLimits": [
                { "dimension": "Wallet", "subject": "SourceWallet", "counterVersion": 7, "maximumUnits": 1000, "windowSeconds": 86400 },
                { "dimension": "IdentityCluster", "subject": "IdentityCluster", "counterVersion": 7, "maximumUnits": 2000, "windowSeconds": 86400 },
                { "dimension": "Tenant", "subject": "Tenant", "counterVersion": 7, "maximumUnits": 5000, "windowSeconds": 86400 }
              ]
            }
            """;

        var policy = EconomyProtectedRiskPolicy.Parse(payload);

        policy.DecisionLifetime.Should().Be(TimeSpan.FromMinutes(5));
        policy.RequiredReviewApprovals.Should().Be(2);
        policy.ComplianceHoldDuration.Should().Be(TimeSpan.FromDays(1));
        policy.CounterVersion.Should().Be(7);
        policy.Limits.Should().HaveCount(3);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":29,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":3,\"complianceHoldSeconds\":60,\"riskLimits\":[]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":59,\"riskLimits\":[]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":0,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60},{\"dimension\":\"Tenant\",\"subject\":\"Tenant\",\"counterVersion\":2,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":{}}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60},{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Unknown\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"0\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"Unknown\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"0\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":0,\"maximumUnits\":100,\"windowSeconds\":60}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":59}]}")]
    [InlineData("{\"riskDecisionLifetimeSeconds\":300,\"riskReviewRequiredApprovals\":1,\"complianceHoldSeconds\":60,\"riskLimits\":[{\"dimension\":\"Wallet\",\"subject\":\"SourceWallet\",\"counterVersion\":1,\"maximumUnits\":100,\"windowSeconds\":31536001}]}")]
    public void Parse_FailsClosedForMissingOrUnsafeSettings(string payload)
    {
        var action = () => EconomyProtectedRiskPolicy.Parse(payload);

        action.Should().Throw<EconomyProtectedRiskPolicyException>();
    }
}
