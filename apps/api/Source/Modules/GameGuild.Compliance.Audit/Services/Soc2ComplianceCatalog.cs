using System.Text;

namespace GameGuild.Compliance.Audit;

/// <summary>Criterion identifiers and evidence adapters; assessment remains the auditor's responsibility.</summary>
internal static class Soc2ComplianceCatalog
{
    internal const string CriteriaSource = "https://www.aicpa-cima.com/resources/download/2017-trust-services-criteria-with-revised-points-of-focus-2022";
    internal const string DescriptionSource = "https://www.aicpa-cima.com/resources/download/get-description-criteria-for-your-organizations-soc-2-r-report";
    internal const string PublicCriteriaSource = "https://assets.ctfassets.net/rb9cdnjh59cm/72xv4p67HVXKp6CjWmjkPk/1cdbfa19f6307e2720396b66a6194dc9/trust-services-criteria-updated-copyright.pdf";
    private static readonly string[] OptionalCategories = ["Availability", "ProcessingIntegrity", "Confidentiality", "Privacy"];
    private static readonly string[] Slugs = ["availability", "processing-integrity", "confidentiality", "privacy"];
    private static readonly Profile[] AllProfiles = Enumerable.Range(1, 2).SelectMany(type =>
        Enumerable.Range(0, 16).Select(mask => CreateProfile(type, mask))).ToArray();

    internal sealed record Profile(int ReportType, string[] Categories, ComplianceFrameworkTemplate Template);
    internal static Profile? Find(string id) => AllProfiles.SingleOrDefault(item => item.Template.Id == id);
    internal static IReadOnlyList<ComplianceFrameworkTemplate> Templates() => AllProfiles.Select(item => item.Template).ToArray();

    private static Profile CreateProfile(int type, int mask)
    {
        var categories = new List<string> { "Security" };
        var slug = new StringBuilder("security");
        var ids = new List<string>();
        foreach (var (family, count) in new[] { (1, 5), (2, 3), (3, 4), (4, 2), (5, 3), (6, 8), (7, 5), (8, 1), (9, 2) })
        { ids.AddRange(Enumerable.Range(1, count).Select(number => $"CC{family}.{number}")); }
        for (var index = 0; index < OptionalCategories.Length; index++)
        {
            if ((mask & (1 << index)) == 0) { continue; }
            categories.Add(OptionalCategories[index]); slug.Append('-').Append(Slugs[index]);
            ids.AddRange(index switch
            {
                0 => ["A1.1", "A1.2", "A1.3"],
                1 => ["PI1.1", "PI1.2", "PI1.3", "PI1.4", "PI1.5"],
                2 => ["C1.1", "C1.2"],
                _ => ["P1.1", "P2.1", "P3.1", "P3.2", "P4.1", "P4.2", "P4.3", "P5.1", "P5.2",
                    "P6.1", "P6.2", "P6.3", "P6.4", "P6.5", "P6.6", "P6.7", "P7.1", "P8.1"]
            });
        }
        var documents = new[]
        {
            Requirement("soc2-system-description", "periodStartUtc", "periodEndUtc", "systemName",
                "boundaries", "services", "serviceCommitments", "systemRequirements", "components", "systemIncidents",
                "systemChanges", "guidanceReview"),
            Requirement("soc2-management-assertion", "periodStartUtc", "periodEndUtc", "signatory", "signedAtUtc"),
            Requirement("soc2-control-matrix", "criteriaMappings", "controls", "guidanceReview"),
            Requirement("soc2-supporting-evidence", "evidenceDescription")
        };
        var controls = ids.Select(id => new ComplianceControlTemplate(id, CriteriaSource, type == 1 ? [] :
            id.StartsWith("CC6.", StringComparison.Ordinal) ? [ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Authentication, ComplianceEvidenceKind.Authorization] :
            id.StartsWith("CC7.", StringComparison.Ordinal) ? [ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Integrity, ComplianceEvidenceKind.Incidents] :
            [ComplianceEvidenceKind.Operations], ["soc2-system-description", "soc2-management-assertion", "soc2-control-matrix"])).ToArray();
        var template = new ComplianceFrameworkTemplate($"soc2-tsc2017-type{type}-{slug}-evidence-v1",
            type == 1 ? ComplianceFramework.SOC2Type1 : ComplianceFramework.SOC2Type2,
            $"SOC2/TSC2017/PoF2022/Type{type}/scope-{mask:X1}",
            type == 1 ? ComplianceEvidencePeriodMode.PointInTime : ComplianceEvidencePeriodMode.Period,
            [CriteriaSource, DescriptionSource, PublicCriteriaSource], controls, documents);
        return new(type, categories.ToArray(), template);
    }

    private static ComplianceDocumentRequirement Requirement(string type, params string[] fields) =>
        new(type, ["frameworkVersion", "assessmentStatus", "owner", .. fields], true);
}
