using System.Text.Json;

namespace GameGuild.Finance.Economy.Risk;

public enum EconomyRiskLimitSubject
{
    SourceWallet = 1,
    DestinationWallet = 2,
    IdentityCluster = 3,
    SourceRoot = 4,
    Destination = 5,
    Provider = 6,
    Tenant = 7,
    CounterpartyPair = 8
}

public sealed record EconomyProtectedRiskLimitRule(
    RiskLimitDimension Dimension,
    EconomyRiskLimitSubject Subject,
    long CounterVersion,
    long MaximumUnits,
    TimeSpan Window);

public sealed record EconomyProtectedRiskPolicy(
    TimeSpan DecisionLifetime,
    int RequiredReviewApprovals,
    TimeSpan ComplianceHoldDuration,
    long CounterVersion,
    IReadOnlyList<EconomyProtectedRiskLimitRule> Limits)
{
    public static EconomyProtectedRiskPolicy Parse(string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        try
        {
            using var document = JsonDocument.Parse(canonicalPayload);
            var root = document.RootElement;
            var decisionLifetimeSeconds = root.GetProperty("riskDecisionLifetimeSeconds").GetInt32();
            var requiredReviewApprovals = root.GetProperty("riskReviewRequiredApprovals").GetInt32();
            var complianceHoldSeconds = root.GetProperty("complianceHoldSeconds").GetInt32();
            if (decisionLifetimeSeconds is < 30 or > 300)
                throw Invalid("Risk decision lifetime must be between 30 and 300 seconds.");
            if (requiredReviewApprovals is < 1 or > 2)
                throw Invalid("Risk review approvals must be one or two.");
            if (complianceHoldSeconds is < 60 or > 2_592_000)
                throw Invalid("Compliance hold duration must be between one minute and 30 days.");

            var limitsElement = root.GetProperty("riskLimits");
            if (limitsElement.ValueKind != JsonValueKind.Array || limitsElement.GetArrayLength() == 0)
                throw Invalid("At least one explicit aggregate risk limit is required.");
            var limits = limitsElement.EnumerateArray().Select(ParseLimit).ToArray();
            if (limits.Select(limit => (limit.Dimension, limit.Subject)).Distinct().Count() != limits.Length)
                throw Invalid("Aggregate risk limit dimensions and subjects must be unique.");
            var counterVersions = limits.Select(limit => limit.CounterVersion).Distinct().ToArray();
            if (counterVersions.Length != 1)
                throw Invalid("All aggregate risk limits must use one counter version.");
            return new EconomyProtectedRiskPolicy(
                TimeSpan.FromSeconds(decisionLifetimeSeconds),
                requiredReviewApprovals,
                TimeSpan.FromSeconds(complianceHoldSeconds),
                counterVersions[0],
                Array.AsReadOnly(limits));
        }
        catch (EconomyProtectedRiskPolicyException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
                                           FormatException or OverflowException or KeyNotFoundException)
        {
            throw Invalid("The signed protected-operation risk policy is invalid.", exception);
        }
    }

    private static EconomyProtectedRiskLimitRule ParseLimit(JsonElement element)
    {
        var dimensionText = element.GetProperty("dimension").GetString();
        var subjectText = element.GetProperty("subject").GetString();
        if (!Enum.TryParse<RiskLimitDimension>(dimensionText, ignoreCase: false, out var dimension) ||
            !Enum.IsDefined(dimension) ||
            !Enum.TryParse<EconomyRiskLimitSubject>(subjectText, ignoreCase: false, out var subject) ||
            !Enum.IsDefined(subject))
            throw Invalid("Aggregate risk limit dimension or subject is invalid.");
        var counterVersion = element.GetProperty("counterVersion").GetInt64();
        var maximumUnits = element.GetProperty("maximumUnits").GetInt64();
        var windowSeconds = element.GetProperty("windowSeconds").GetInt64();
        if (counterVersion <= 0 || maximumUnits <= 0 || windowSeconds is < 60 or > 31_536_000)
            throw Invalid("Aggregate risk limit values are outside their safe ranges.");
        return new EconomyProtectedRiskLimitRule(
            dimension,
            subject,
            counterVersion,
            maximumUnits,
            TimeSpan.FromSeconds(windowSeconds));
    }

    private static EconomyProtectedRiskPolicyException Invalid(
        string message,
        Exception? innerException = null) => new(message, innerException);
}

public sealed class EconomyProtectedRiskPolicyException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
