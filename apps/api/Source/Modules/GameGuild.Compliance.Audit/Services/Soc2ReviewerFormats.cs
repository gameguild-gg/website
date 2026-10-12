using System.Text;
using System.Text.Json;
using static GameGuild.Compliance.Audit.ComplianceNativeEvidence;

namespace GameGuild.Compliance.Audit;

internal static class Soc2ReviewerFormats
{
    internal static IReadOnlyList<string> Paths { get; } = ["review/soc2/system-description.json", "review/soc2/system-description.txt",
        "review/soc2/management-assertion.json", "review/soc2/management-assertion.txt",
        "review/soc2/control-matrix.json", "review/soc2/control-matrix.txt",
        "review/soc2/criteria.csv", "review/soc2/control-tests.csv", "review/soc2/profile.json"];

    internal static void Build(ComplianceFrameworkTemplate template, IReadOnlyList<ComplianceDocumentSnapshot> documents,
        IReadOnlyDictionary<Guid, JsonElement> roots, CompliancePackageValidationReport report, Dictionary<string, byte[]> files)
    {
        var formatting = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true };
        void Export(string type, string name)
        {
            var document = documents.SingleOrDefault(item => item.Type == type);
            var content = document is null ? "{\"missingEvidence\":true}" :
                roots[document.Id].ValueKind == JsonValueKind.Undefined ? "{\"invalidStructuredEvidence\":true}" :
                JsonSerializer.Serialize(roots[document.Id], formatting);
            files.Add($"review/soc2/{name}.json", Encoding.UTF8.GetBytes(content));
            files.Add($"review/soc2/{name}.txt", Encoding.UTF8.GetBytes(
                $"{type}\nSource: {(document is null ? "missing" : Metadata(document))}\nReady for auditor review: {report.ReadyForAuditorReview}\n\n{content}\n"));
        }
        Export("soc2-system-description", "system-description");
        Export("soc2-management-assertion", "management-assertion");
        Export("soc2-control-matrix", "control-matrix");
        var profile = Soc2ComplianceCatalog.Find(template.Id)!;
        files.Add(Paths[8], JsonSerializer.SerializeToUtf8Bytes(new
        {
            template.Id, template.Version, profile.ReportType, profile.Categories, template.Sources,
            CriteriaIdentifierSource = Soc2ComplianceCatalog.PublicCriteriaSource,
            CriteriaIdentifierSourceEdition = "TSP Section 100 / 2017 TSC / March 2020",
            CriteriaIdentifierSourceSha256 = "b3eeb82c7e493bbd694d16b3f4ebc381183908ead2321f587f3531f7c4abe15b",
            GuidanceReview = "The 2022 points of focus and description guidance require recorded reviewer judgment; mappings do not reproduce the licensed guidance.",
            AuditOpinion = "This is a captured evidence package for review, not a service auditor's report or opinion."
        }, formatting));
        var matrix = documents.SingleOrDefault(item => item.Type == "soc2-control-matrix");
        var root = matrix is null ? default : roots[matrix.Id];
        var controls = Array(root, "controls", 200);
        var criteria = new StringBuilder("criterionId,controlId,mappingRationale,pointOfFocusReview,description,owner,designBasis,designConclusion,designEvidenceDocumentIds,sourceMetadata,gapCodes\r\n");
        foreach (var criterion in template.Controls)
        {
            var mapping = Property(Property(root, "criteriaMappings"), criterion.Id);
            foreach (var id in Strings(mapping, "controlIds", 20).DefaultIfEmpty(string.Empty))
            {
                var control = controls.FirstOrDefault(item => Text(item, "controlId") == id);
                Row(criteria, criterion.Id, id, Text(mapping, "mappingRationale"), Text(mapping, "pointOfFocusReview"),
                    Text(control, "description"), Text(control, "owner"), Text(control, "designBasis"), Text(control, "designConclusion"),
                    string.Join(';', Strings(control, "designEvidenceDocumentIds", 20)), matrix is null ? "" : Metadata(matrix),
                    string.Join(';', report.Gaps.Where(item => item.ControlId == criterion.Id).Select(item => item.Code).Distinct()));
            }
        }
        files.Add(Paths[6], Encoding.UTF8.GetBytes(criteria.ToString()));
        var tests = new StringBuilder("controlId,testProcedures,populationCount,sampleCount,deviationCount,operatingConclusion,testedFromUtc,testedUntilUtc,sampleId,observedAtUtc,result,evidenceDocumentId,sourceMetadata\r\n");
        foreach (var control in controls)
        {
            var samples = Array(control, "samples", 200);
            foreach (var sample in samples.Length == 0 ? new[] { default(JsonElement) } : samples)
            {
                Row(tests, Text(control, "controlId"), Text(control, "testProcedures"), Property(control, "populationCount").ToString(),
                    Property(control, "sampleCount").ToString(), Property(control, "deviationCount").ToString(), Text(control, "operatingConclusion"),
                    Text(control, "testedFromUtc"), Text(control, "testedUntilUtc"), Text(sample, "sampleId"), Text(sample, "observedAtUtc"),
                    Text(sample, "result"), Text(sample, "evidenceDocumentId"), matrix is null ? "" : Metadata(matrix));
            }
        }
        files.Add(Paths[7], Encoding.UTF8.GetBytes(tests.ToString()));
    }

    private static string Metadata(ComplianceDocumentSnapshot document) => $"documents/{document.Id:D}/metadata.json";
    private static void Row(StringBuilder output, params string[] cells) =>
        output.AppendJoin(',', cells.Select(ComplianceArtifactBuilder.CsvCell)).Append("\r\n");
}
