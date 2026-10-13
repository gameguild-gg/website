using System.Text.Json;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Subscriptions;

/// <summary>
///     Response for <see cref="CompareSubscriptionPlansQuery" /> (issue #416: "Plan
///     comparison API returns feature matrix for multiple plans").
/// </summary>
/// <param name="BasePlan">The reference plan the comparison is anchored on.</param>
/// <param name="Plans">Base plan followed by each comparison plan, in request order.</param>
/// <param name="FeatureMatrix">
///     One row per distinct feature/limit key; values are keyed by plan slug (missing key
///     on a plan maps to <c>null</c>).
/// </param>
public sealed record SubscriptionPlanComparisonResponse(
    SubscriptionPlanComparisonPlan BasePlan,
    IReadOnlyList<SubscriptionPlanComparisonPlan> Plans,
    IReadOnlyList<SubscriptionPlanComparisonRow> FeatureMatrix);

/// <summary>Trimmed plan projection used by the comparison endpoint.</summary>
public sealed record SubscriptionPlanComparisonPlan(
    Guid Id,
    string Name,
    string Slug,
    long MonthlyPriceInCents,
    long? AnnualPriceInCents,
    string Currency,
    IReadOnlyDictionary<string, object?> Features,
    IReadOnlyDictionary<string, long?> Limits);

/// <summary>One feature/limit row across every compared plan.</summary>
public sealed record SubscriptionPlanComparisonRow(
    string Feature,
    IReadOnlyDictionary<string, object?> Values);

/// <summary>
///     Handler for <see cref="CompareSubscriptionPlansQuery" /> — previously unimplemented,
///     the <c>POST v1/subscription-plans:compare</c> endpoint failed at runtime with
///     "handler not registered" (issue #416).
/// </summary>
/// <remarks>
///     Builds a feature matrix over the union of the plans' <c>Features</c> JSON keys plus
///     the well-known limit columns (<c>maxUsers</c>, <c>maxStorageMb</c>,
///     <c>maxApiCallsPerMonth</c>). Reading is lenient: legacy payloads that predate the
///     features schema validation are skipped rather than failing the comparison.
/// </remarks>
public sealed class CompareSubscriptionPlansQueryHandler(ISubscriptionPlanRepository planRepository)
    : IQueryHandler<CompareSubscriptionPlansQuery, object>
{
    private static readonly string[] LimitKeys = ["maxUsers", "maxStorageMb", "maxApiCallsPerMonth"];

    public async Task<object> Handle(CompareSubscriptionPlansQuery request, CancellationToken cancellationToken)
    {
        var basePlan = await planRepository.GetByIdAsync(request.PlanId, cancellationToken).ConfigureAwait(false)
            ?? throw new EntityNotFoundException("SubscriptionPlan", request.PlanId);

        // Load comparison plans in request order, de-duplicated, skipping unknown ids.
        var comparisonPlans = new List<SubscriptionPlan>();
        foreach (var planId in request.ComparisonPlanIds.Distinct())
        {
            if (planId == basePlan.Id)
            {
                continue;
            }

            var plan = await planRepository.GetByIdAsync(planId, cancellationToken).ConfigureAwait(false);
            if (plan is not null)
            {
                comparisonPlans.Add(plan);
            }
        }

        var allPlans = new[] { basePlan }.Concat(comparisonPlans).ToList();
        var projections = allPlans.Select(ToComparisonPlan).ToList();

        var featureMatrix = BuildFeatureMatrix(allPlans);

        return new SubscriptionPlanComparisonResponse(projections[0], projections, featureMatrix);
    }

    private static SubscriptionPlanComparisonPlan ToComparisonPlan(SubscriptionPlan plan)
    {
        return new SubscriptionPlanComparisonPlan(
            plan.Id,
            plan.Name,
            plan.Slug,
            plan.MonthlyPriceInCents,
            plan.AnnualPriceInCents,
            plan.Currency,
            ParseFeatures(plan.Features),
            new Dictionary<string, long?>
            {
                ["maxUsers"] = plan.MaxUsers,
                ["maxStorageMb"] = plan.MaxStorageMb,
                ["maxApiCallsPerMonth"] = plan.MaxApiCallsPerMonth
            });
    }

    private static IReadOnlyList<SubscriptionPlanComparisonRow> BuildFeatureMatrix(IReadOnlyList<SubscriptionPlan> plans)
    {
        var parsedFeatures = plans.Select(p => ParseFeatures(p.Features)).ToList();

        // Union of feature keys (matrix order: first-seen across plans), then limit columns.
        var featureKeys = new List<string>();
        var seenKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var features in parsedFeatures)
        {
            foreach (var key in features.Keys)
            {
                if (seenKeys.Add(key))
                {
                    featureKeys.Add(key);
                }
            }
        }

        var rows = new List<SubscriptionPlanComparisonRow>();
        foreach (var key in featureKeys)
        {
            rows.Add(BuildRow(plans, parsedFeatures, key, p => null));
        }

        foreach (var limitKey in LimitKeys)
        {
            rows.Add(BuildRow(plans, parsedFeatures, limitKey, plan => LimitValue(plan, limitKey)));
        }

        return rows;
    }

    private static SubscriptionPlanComparisonRow BuildRow(
        IReadOnlyList<SubscriptionPlan> plans,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> parsedFeatures,
        string key,
        Func<SubscriptionPlan, object?> fallback)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < plans.Count; i++)
        {
            values[plans[i].Slug] = parsedFeatures[i].TryGetValue(key, out var value)
                ? value
                : fallback(plans[i]);
        }

        return new SubscriptionPlanComparisonRow(key, values);
    }

    private static object? LimitValue(SubscriptionPlan plan, string limitKey) => limitKey switch
    {
        "maxUsers" => plan.MaxUsers is null ? null : (long?)plan.MaxUsers.Value,
        "maxStorageMb" => plan.MaxStorageMb,
        "maxApiCallsPerMonth" => plan.MaxApiCallsPerMonth,
        _ => null
    };

    private static IReadOnlyDictionary<string, object?> ParseFeatures(string? features)
    {
        if (string.IsNullOrWhiteSpace(features))
        {
            return new Dictionary<string, object?>();
        }

        try
        {
            using var document = JsonDocument.Parse(features);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object?>();
            }

            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number when property.Value.TryGetInt64(out var integer) => integer,
                    JsonValueKind.Number => property.Value.GetDouble(),
                    JsonValueKind.String => property.Value.GetString(),
                    _ => null
                };
            }

            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?>();
        }
    }
}
