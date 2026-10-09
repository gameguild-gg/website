using System.Text.Json.Serialization;

namespace GameGuild.Compliance.Audit;

/// <summary>
/// Sensitivity-based retention floor for one <see cref="SensitivityLevel"/> within a policy template.
/// Data classified at a higher sensitivity never receives a shorter retention period.
/// </summary>
public sealed record AuditRetentionPolicySensitivityRule
{
    /// <summary>Data classification this rule applies to.</summary>
    [property: JsonConverter(typeof(JsonStringEnumConverter<SensitivityLevel>))]
    public required SensitivityLevel Sensitivity { get; init; }

    /// <summary>Minimum retention, in days, enforced for data at this classification.</summary>
    public required int MinimumRetentionDays { get; init; }

    /// <summary>Optional maximum retention, in days, after which data at this classification may be purged.</summary>
    public required int? MaximumRetentionDays { get; init; }

    /// <summary>Why this classification receives this retention period.</summary>
    public required string Rationale { get; init; }
}

/// <summary>
/// Pre-built retention policy template for a common regulatory framework. Templates form an inheritance
/// chain rooted at the platform baseline; tenants materialize a template into a
/// <see cref="ConfigureAuditRetentionRequest"/> and then adjust it through versioned configuration updates.
/// </summary>
public sealed record AuditRetentionPolicyTemplate
{
    /// <summary>Stable template identifier referenced by <see cref="BaseTemplateId"/>.</summary>
    public required string Id { get; init; }

    /// <summary>Human-readable template name.</summary>
    public required string DisplayName { get; init; }

    /// <summary>What the template presets and why.</summary>
    public required string Description { get; init; }

    /// <summary>Regulatory framework or policy family this template targets.</summary>
    public required string Framework { get; init; }

    /// <summary>True only for the platform baseline every other template inherits from.</summary>
    public required bool IsBaseline { get; init; }

    /// <summary>Template this one inherits obligations and sensitivity floors from; null for the baseline.</summary>
    public required string? BaseTemplateId { get; init; }

    /// <summary>Publication timestamp of this template revision (UTC).</summary>
    public required DateTime PublishedAtUtc { get; init; }

    /// <summary>Baseline retention scenario preset by the template.</summary>
    public required AuditRetentionScenario BaselineScenario { get; init; }

    /// <summary>Storage tier price assumptions preset by the template.</summary>
    public required IReadOnlyList<AuditStorageTierPrice> TierPrices { get; init; }

    /// <summary>Retention obligations with the administrator-provided regulatory source for each.</summary>
    public required IReadOnlyList<AuditRetentionObligation> Obligations { get; init; }

    /// <summary>Sensitivity classification floors; retention grows with classification.</summary>
    public required IReadOnlyList<AuditRetentionPolicySensitivityRule> SensitivityRules { get; init; }

    /// <summary>
    /// Materializes the template into a configuration request that passes
    /// <see cref="AuditRetentionInputValidation"/>. Obligations inherited from the base template are
    /// merged in so derived templates never weaken the baseline floors.
    /// </summary>
    public ConfigureAuditRetentionRequest ToConfigurationRequest()
    {
        var inherited = new List<AuditRetentionObligation>();
        var current = BaseTemplateId is null ? null : AuditRetentionPolicyTemplates.Find(BaseTemplateId);
        while (current is not null)
        {
            inherited.AddRange(current.Obligations);
            current = current.BaseTemplateId is null ? null : AuditRetentionPolicyTemplates.Find(current.BaseTemplateId);
        }

        var obligations = Obligations
            .Concat(inherited)
            .GroupBy(obligation => obligation.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(obligation => obligation.MinimumRetentionDays).Last())
            .OrderBy(obligation => obligation.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ConfigureAuditRetentionRequest
        {
            ExpectedRevision = 0,
            Currency = "USD",
            StorageOverheadMultiplier = 1,
            MaximumReadLatencyMilliseconds = 1000,
            Baseline = BaselineScenario,
            TierPrices = TierPrices.OrderBy(price => price.Tier).ToList(),
            Obligations = obligations.ToList()
        };
    }
}

/// <summary>
/// Catalog of pre-built retention policy templates. Exactly one template is the platform baseline;
/// every framework template inherits from it. <see cref="ValidateCatalog"/> runs at module startup so an
/// invalid template can never reach configuration or simulation.
/// </summary>
public static class AuditRetentionPolicyTemplates
{
    /// <summary>Identifier of the platform baseline template inherited by every framework template.</summary>
    public const string BaselineId = "gameguild-platform-baseline";

    private static readonly AuditRetentionPolicyTemplate PlatformBaseline = new()
    {
        Id = BaselineId,
        DisplayName = "GameGuild platform baseline",
        Description = "Platform default retention: one year online with a second year in colder tiers, security evidence held longer.",
        Framework = "Platform",
        IsBaseline = true,
        BaseTemplateId = null,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "platform-baseline", RetentionDays = 730, HotDays = 90, WarmUntilDays = 180, ColdUntilDays = 365
        },
        TierPrices =
        [
            new AuditStorageTierPrice { Tier = AuditStorageTier.Hot, MonthlyCostPerGiB = 10, RetrievalCostPerGiB = 0, ExpectedReadLatencyMilliseconds = 1 },
            new AuditStorageTierPrice { Tier = AuditStorageTier.Warm, MonthlyCostPerGiB = 5, RetrievalCostPerGiB = 0.01m, ExpectedReadLatencyMilliseconds = 10 },
            new AuditStorageTierPrice { Tier = AuditStorageTier.Cold, MonthlyCostPerGiB = 2, RetrievalCostPerGiB = 0.1m, ExpectedReadLatencyMilliseconds = 100 },
            new AuditStorageTierPrice { Tier = AuditStorageTier.Archive, MonthlyCostPerGiB = 1, RetrievalCostPerGiB = 1, ExpectedReadLatencyMilliseconds = 10000 }
        ],
        Obligations =
        [
            new AuditRetentionObligation { Name = "Audit trail", Source = "GameGuild platform policy: audit trails retained for at least 400 days", MinimumRetentionDays = 400 },
            new AuditRetentionObligation { Name = "Security investigation evidence", Source = "GameGuild platform policy: security evidence retained for two years", MinimumRetentionDays = 730 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, null, "Internal audit data is kept for at least six months."),
            (365, null, "Confidential audit data is kept for at least one year."),
            (730, null, "Restricted audit data is kept for at least two years."),
            (2190, null, "Highly restricted audit data is kept for at least six years."))
    };

    private static readonly AuditRetentionPolicyTemplate Soc2Type2 = new()
    {
        Id = "soc2-type2",
        DisplayName = "SOC 2 Type II",
        Description = "Monitoring and change-management evidence retention aligned to a one-year SOC 2 Type II audit window.",
        Framework = "SOC 2",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "soc2-type2", RetentionDays = 730, HotDays = 90, WarmUntilDays = 180, ColdUntilDays = 365
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "SOC 2 monitoring evidence", Source = "SOC 2 Trust Services Criteria CC7.2: monitoring evidence retained for the audit period plus follow-up", MinimumRetentionDays = 400 },
            new AuditRetentionObligation { Name = "SOC 2 change management evidence", Source = "SOC 2 Trust Services Criteria CC8.1: change records retained for the audit period", MinimumRetentionDays = 365 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, null, "Internal audit data is kept for at least six months."),
            (400, null, "Confidential control evidence covers a full audit year."),
            (730, null, "Restricted control evidence is kept for two years."),
            (2190, null, "Highly restricted evidence is kept for six years."))
    };

    private static readonly AuditRetentionPolicyTemplate Iso27001 = new()
    {
        Id = "iso-27001",
        DisplayName = "ISO/IEC 27001:2022",
        Description = "Event logging and forensic evidence retention aligned to the ISO 27001 ISMS controls.",
        Framework = "ISO 27001",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "iso-27001", RetentionDays = 730, HotDays = 90, WarmUntilDays = 180, ColdUntilDays = 365
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "ISMS event logs", Source = "ISO/IEC 27001:2022 control A.8.15: logging retained per organization-defined policy, preset to one year", MinimumRetentionDays = 365 },
            new AuditRetentionObligation { Name = "Forensic evidence", Source = "ISO/IEC 27001:2022 control A.5.28: evidence preserved for forensics and legal purposes, preset to two years", MinimumRetentionDays = 730 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, null, "Internal audit data is kept for at least six months."),
            (365, null, "Confidential logs cover the organization-defined logging period."),
            (730, null, "Restricted forensic evidence is kept for two years."),
            (2190, null, "Highly restricted forensic evidence is kept for six years."))
    };

    private static readonly AuditRetentionPolicyTemplate Gdpr = new()
    {
        Id = "gdpr",
        DisplayName = "GDPR accountability",
        Description = "Retention floors for audit records that document processing of personal data.",
        Framework = "GDPR",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "gdpr", RetentionDays = 730, HotDays = 90, WarmUntilDays = 180, ColdUntilDays = 365
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "Records of processing evidence", Source = "GDPR Art. 30: records of processing activities retained per controller policy, preset to one year", MinimumRetentionDays = 365 },
            new AuditRetentionObligation { Name = "Lawful basis evidence", Source = "GDPR Art. 5(2) accountability: processing evidence retained while accountability applies, preset to two years", MinimumRetentionDays = 730 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, 730, "Internal audit data is minimized after six months."),
            (365, 1095, "Confidential personal-data evidence is capped at three years."),
            (730, 1825, "Restricted personal-data evidence is capped at five years."),
            (1460, 2190, "Highly restricted special-category evidence is capped at six years."))
    };

    private static readonly AuditRetentionPolicyTemplate Hipaa = new()
    {
        Id = "hipaa",
        DisplayName = "HIPAA security rule",
        Description = "Documentation and audit-control retention aligned to the HIPAA security rule.",
        Framework = "HIPAA",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "hipaa", RetentionDays = 2190, HotDays = 90, WarmUntilDays = 365, ColdUntilDays = 1095
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "HIPAA documentation", Source = "HIPAA 45 CFR 164.316(b)(2): required documentation retained for six years from creation or last effective date", MinimumRetentionDays = 2190 },
            new AuditRetentionObligation { Name = "HIPAA audit controls", Source = "HIPAA 45 CFR 164.312(b): audit control records retained with documentation, preset to six years", MinimumRetentionDays = 2190 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Non-PHI public audit data ages out after one year."),
            (180, null, "Internal audit data is kept for at least six months."),
            (365, null, "Confidential audit data is kept for at least one year."),
            (2190, null, "Restricted PHI audit data is kept for six years."),
            (2190, null, "Highly restricted PHI audit data is kept for six years."))
    };

    private static readonly AuditRetentionPolicyTemplate PciDss = new()
    {
        Id = "pci-dss",
        DisplayName = "PCI DSS v4",
        Description = "Audit trail retention aligned to the payment card industry data security standard.",
        Framework = "PCI DSS",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "pci-dss", RetentionDays = 730, HotDays = 90, WarmUntilDays = 180, ColdUntilDays = 365
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "PCI DSS audit trail", Source = "PCI DSS v4 requirement 10.5.1: audit trail history retained for at least 12 months, three months immediately available", MinimumRetentionDays = 365 },
            new AuditRetentionObligation { Name = "PCI DSS payment evidence", Source = "PCI DSS v4 requirement 10.2: payment event records retained per organization policy, preset to two years", MinimumRetentionDays = 730 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, null, "Internal audit data is kept for at least six months."),
            (365, null, "Confidential cardholder-data evidence covers the required trail year."),
            (730, null, "Restricted payment evidence is kept for two years."),
            (2190, null, "Highly restricted payment evidence is kept for six years."))
    };

    private static readonly AuditRetentionPolicyTemplate FedRamp = new()
    {
        Id = "fedramp-moderate",
        DisplayName = "FedRAMP Moderate (NIST SP 800-53)",
        Description = "Audit record retention aligned to FedRAMP Moderate baseline logging expectations.",
        Framework = "FedRAMP",
        IsBaseline = false,
        BaseTemplateId = BaselineId,
        PublishedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        BaselineScenario = new AuditRetentionScenario
        {
            Name = "fedramp-moderate", RetentionDays = 1095, HotDays = 90, WarmUntilDays = 365, ColdUntilDays = 730
        },
        TierPrices = PlatformBaseline.TierPrices,
        Obligations =
        [
            new AuditRetentionObligation { Name = "FedRAMP audit records", Source = "NIST SP 800-53 Rev. 5 control AU-11: audit record retention per organization-defined duration, FedRAMP baseline preset to one year", MinimumRetentionDays = 365 },
            new AuditRetentionObligation { Name = "FedRAMP incident evidence", Source = "FedRAMP incident response guidelines: incident evidence retained for the reporting lifecycle, preset to three years", MinimumRetentionDays = 1095 }
        ],
        SensitivityRules = CreateSensitivityRules(
            (90, 365, "Public audit data ages out after one year at most."),
            (180, null, "Internal audit data is kept for at least six months."),
            (365, null, "Confidential audit data covers the AU-11 retention period."),
            (1095, null, "Restricted incident evidence is kept for three years."),
            (2190, null, "Highly restricted incident evidence is kept for six years."))
    };

    /// <summary>All templates, baseline first, then framework templates in catalog order.</summary>
    public static IReadOnlyList<AuditRetentionPolicyTemplate> All { get; } =
    [
        PlatformBaseline,
        Soc2Type2,
        Iso27001,
        Gdpr,
        Hipaa,
        PciDss,
        FedRamp
    ];

    /// <summary>The platform baseline template every framework template inherits from.</summary>
    public static AuditRetentionPolicyTemplate Baseline { get; } = PlatformBaseline;

    /// <summary>Returns the template with the given identifier, or null.</summary>
    public static AuditRetentionPolicyTemplate? Find(string id) =>
        All.FirstOrDefault(template => string.Equals(template.Id, id, StringComparison.Ordinal));

    /// <summary>
    /// Validates catalog integrity: unique identifiers, exactly one baseline, resolvable inheritance that
    /// reaches the baseline without cycles, monotonic sensitivity floors, and configuration requests that
    /// pass <see cref="AuditRetentionInputValidation"/>. Called at module startup.
    /// </summary>
    public static void ValidateCatalog()
    {
        if (All.Count == 0) { throw new InvalidOperationException("The retention policy template catalog is empty."); }
        if (All.Select(template => template.Id).Distinct(StringComparer.Ordinal).Count() != All.Count)
        {
            throw new InvalidOperationException("Retention policy template identifiers must be unique.");
        }
        if (All.Count(template => template.IsBaseline) != 1 || Baseline.Id != BaselineId || Baseline.BaseTemplateId is not null)
        {
            throw new InvalidOperationException("The catalog must contain exactly one root baseline template.");
        }
        foreach (var template in All)
        {
            if (template.IsBaseline) { continue; }
            if (template.BaseTemplateId is null)
            {
                throw new InvalidOperationException($"Template '{template.Id}' must declare the baseline it inherits from.");
            }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var current = template;
            while (current.BaseTemplateId is not null)
            {
                if (!visited.Add(current.Id))
                {
                    throw new InvalidOperationException($"Template inheritance for '{template.Id}' contains a cycle.");
                }
                current = Find(current.BaseTemplateId)
                    ?? throw new InvalidOperationException($"Template '{template.Id}' inherits from unknown template '{current.BaseTemplateId}'.");
            }
            if (!current.IsBaseline)
            {
                throw new InvalidOperationException($"Template inheritance for '{template.Id}' must terminate at the platform baseline.");
            }
        }
        foreach (var template in All)
        {
            if (template.TierPrices.Select(price => price.Tier).Distinct().Count() != 4)
            {
                throw new InvalidOperationException($"Template '{template.Id}' must price hot, warm, cold and archive tiers exactly once.");
            }
            if (template.Obligations.Count == 0)
            {
                throw new InvalidOperationException($"Template '{template.Id}' must declare at least one retention obligation.");
            }
            ValidateSensitivityRules(template);
            AuditRetentionInputValidation.Validate(template.ToConfigurationRequest());
        }
    }

    private static void ValidateSensitivityRules(AuditRetentionPolicyTemplate template)
    {
        if (template.SensitivityRules.Count == 0)
        {
            throw new InvalidOperationException($"Template '{template.Id}' must declare sensitivity retention rules.");
        }
        var ordered = template.SensitivityRules.OrderBy(rule => (int)rule.Sensitivity).ToArray();
        var previousMinimum = 0;
        foreach (var rule in ordered)
        {
            if (rule.MinimumRetentionDays is < 1 or > 36500)
            {
                throw new InvalidOperationException($"Template '{template.Id}' has a sensitivity floor outside 1..36500 days.");
            }
            if (rule.MaximumRetentionDays.HasValue && rule.MaximumRetentionDays.Value < rule.MinimumRetentionDays)
            {
                throw new InvalidOperationException($"Template '{template.Id}' has a sensitivity cap below its floor.");
            }
            if (rule.MinimumRetentionDays < previousMinimum)
            {
                throw new InvalidOperationException($"Template '{template.Id}' must not lower retention for more sensitive classifications.");
            }
            previousMinimum = rule.MinimumRetentionDays;
        }
    }

    private static AuditRetentionPolicySensitivityRule[] CreateSensitivityRules(
        (int Minimum, int? Maximum, string Rationale) publicRule,
        (int Minimum, int? Maximum, string Rationale) internalRule,
        (int Minimum, int? Maximum, string Rationale) confidentialRule,
        (int Minimum, int? Maximum, string Rationale) restrictedRule,
        (int Minimum, int? Maximum, string Rationale) highlyRestrictedRule) =>
    [
        new() { Sensitivity = SensitivityLevel.Public, MinimumRetentionDays = publicRule.Minimum, MaximumRetentionDays = publicRule.Maximum, Rationale = publicRule.Rationale },
        new() { Sensitivity = SensitivityLevel.Internal, MinimumRetentionDays = internalRule.Minimum, MaximumRetentionDays = internalRule.Maximum, Rationale = internalRule.Rationale },
        new() { Sensitivity = SensitivityLevel.Confidential, MinimumRetentionDays = confidentialRule.Minimum, MaximumRetentionDays = confidentialRule.Maximum, Rationale = confidentialRule.Rationale },
        new() { Sensitivity = SensitivityLevel.Restricted, MinimumRetentionDays = restrictedRule.Minimum, MaximumRetentionDays = restrictedRule.Maximum, Rationale = restrictedRule.Rationale },
        new() { Sensitivity = SensitivityLevel.HighlyRestricted, MinimumRetentionDays = highlyRestrictedRule.Minimum, MaximumRetentionDays = highlyRestrictedRule.Maximum, Rationale = highlyRestrictedRule.Rationale }
    ];
}
