using System.Text.Json;

namespace GameGuild.Compliance.Audit;

/// <summary>Checks evidence quality and coverage. It does not decide whether an organization complies with a standard.</summary>
public sealed class ComplianceEvidenceValidationEngine
{
    public static int MaximumDocumentBytes { get; } = 1048576;
    public static int MaximumDatasetBytes { get; } = 4194304;
    public static int MaximumContentBytes { get; } = 33554432;

    public CompliancePackageValidationReport Inspect(
        ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyList<ComplianceEvidenceDataset> datasets,
        DateTime capturedAtUtc) => Inspect(template, request, documents, datasets, capturedAtUtc, CancellationToken.None);

    public CompliancePackageValidationReport Inspect(
        ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyList<ComplianceEvidenceDataset> datasets,
        DateTime capturedAtUtc, CancellationToken cancellationToken)
    {
        ValidateInputs(template, request, documents, datasets, capturedAtUtc);
        // Parse each captured source and document once, even when a framework maps hundreds of controls to it.
        var sourceGaps = new Dictionary<ComplianceEvidenceKind, IReadOnlyList<ComplianceEvidenceGap>>();
        foreach (var dataset in datasets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gaps = new List<ComplianceEvidenceGap>();
            ValidateDataset(string.Empty, dataset, request, gaps);
            sourceGaps.Add(dataset.Kind, gaps);
        }
        var documentGaps = new Dictionary<Guid, IReadOnlyList<ComplianceEvidenceGap>>();
        var nativeDocuments = new Dictionary<Guid, JsonElement>();
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var requirement = document.Type == "applicability"
                ? new ComplianceDocumentRequirement("applicability", ["frameworkVersion", "assessmentStatus", "rationale"], true)
                : template.Documents.Single(item => item.Type == document.Type);
            var gaps = new List<ComplianceEvidenceGap>();
            ValidateDocument(template, string.Empty, document, requirement, request, gaps, out var root);
            if (ComplianceNativeEvidence.IsNative(template))
            {
                ComplianceNativeEvidence.ValidateDocument(template, document, root, capturedAtUtc, gaps);
                nativeDocuments.Add(document.Id, root);
            }
            documentGaps.Add(document.Id, gaps);
        }
        var nativeGaps = ComplianceNativeEvidence.InspectScope(template, request, documents, nativeDocuments, capturedAtUtc, cancellationToken);
        var results = new List<ComplianceControlEvidenceResult>();
        var allGaps = new List<ComplianceEvidenceGap>();
        foreach (var control in template.Controls)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var gaps = new List<ComplianceEvidenceGap>();
            var paths = new List<string>();
            var usedDocuments = new HashSet<Guid>();
            var exclusion = request.Exclusions.SingleOrDefault(item => item.ControlId == control.Id);
            if (exclusion is not null)
            {
                var applicability = documents.SingleOrDefault(item => item.Id == exclusion.ApplicabilityDocumentId);
                if (applicability is null || (applicability.Type != "applicability" &&
                    !(template.Id == ComplianceFrameworkCatalog.IsoIsmsId && applicability.Type == "iso-soa")) ||
                    !applicability.ControlIds.Contains(control.Id, StringComparer.Ordinal))
                {
                    gaps.Add(new(control.Id, "InvalidScopeExclusion", "An exclusion requires a mapped, reviewed applicability document.", exclusion.ApplicabilityDocumentId));
                }
                else
                {
                    usedDocuments.Add(applicability.Id);
                    gaps.AddRange(documentGaps[applicability.Id].Where(gap => gap.ControlId.Length == 0 || gap.ControlId == control.Id).Select(gap => gap with { ControlId = control.Id }));
                }
            }
            else
            {
                foreach (var kind in control.AutomaticEvidence)
                {
                    var dataset = datasets.SingleOrDefault(item => item.Kind == kind);
                    if (dataset is null)
                    {
                        gaps.Add(new(control.Id, "MissingAutomaticEvidence", $"The {kind} dataset was not collected.", null));
                        continue;
                    }
                    paths.Add(EvidencePath(kind));
                    gaps.AddRange(sourceGaps[dataset.Kind].Select(gap => gap with { ControlId = control.Id }));
                }
                foreach (var type in control.RequiredDocumentTypes)
                {
                    var candidates = documents.Where(item => item.Type == type && item.ControlIds.Contains(control.Id, StringComparer.Ordinal)).ToArray();
                    if (candidates.Length == 0)
                    {
                        gaps.Add(new(control.Id, "MissingDocument", $"A mapped {type} document is required.", null));
                        continue;
                    }
                    foreach (var document in candidates)
                    {
                        usedDocuments.Add(document.Id);
                        gaps.AddRange(documentGaps[document.Id].Where(gap => gap.ControlId.Length == 0 || gap.ControlId == control.Id).Select(gap => gap with { ControlId = control.Id }));
                    }
                }
            }
            // Extra mapped documents are still captured and must not silently bypass quality validation.
            foreach (var document in documents.Where(item => item.ControlIds.Contains(control.Id, StringComparer.Ordinal) && !usedDocuments.Contains(item.Id)))
            {
                usedDocuments.Add(document.Id);
                gaps.AddRange(documentGaps[document.Id].Where(gap => gap.ControlId.Length == 0 || gap.ControlId == control.Id).Select(gap => gap with { ControlId = control.Id }));
            }
            gaps.AddRange(nativeGaps.Where(gap => gap.ControlId == control.Id));
            if (ComplianceNativeEvidence.IsNative(template))
            {
                gaps.AddRange(documentGaps.Values.SelectMany(items => items).Where(gap => gap.ControlId == control.Id && !gaps.Contains(gap)));
            }
            foreach (var id in gaps.Where(gap => gap.DocumentId.HasValue).Select(gap => gap.DocumentId!.Value).Where(id => documents.Any(document => document.Id == id)))
            {
                usedDocuments.Add(id);
            }
            paths.AddRange(usedDocuments.Order().Select(id => $"documents/{id:D}/metadata.json"));
            allGaps.AddRange(gaps);
            results.Add(new(control.Id, gaps.Count != 0 ? "EvidenceGap" : exclusion is not null ? "ReviewedExclusion" : "EvidenceCollected",
                paths, usedDocuments.Order().ToArray(), gaps));
        }
        return new(allGaps.Count == 0, results, allGaps,
        [
            "Readiness describes collected evidence quality, not regulatory compliance or certification. An auditor must assess control effectiveness.",
            "Continuous event streams require at least one observation on each UTC date in the requested period; missing dates are reported conservatively, not assumed covered.",
            "PDF and text document fields are reviewer declarations. JSON fields are validated against the actual uploaded contents.",
            "Zero observed incidents does not establish that every incident was detected. Source collection errors and truncation prevent readiness."
        ]);
    }

    internal static string EvidencePath(ComplianceEvidenceKind kind) => $"evidence/{kind.ToString().ToLowerInvariant()}.json";

    internal IReadOnlyList<ComplianceEvidenceGap> InspectDocumentQuality(ComplianceFrameworkTemplate template,
        CreateCompliancePackageRequest request, ComplianceDocumentSnapshot document, DateTime capturedAtUtc,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateInputs(template, request, [document], [], capturedAtUtc);
        var requirement = document.Type == "applicability"
            ? new ComplianceDocumentRequirement("applicability", ["frameworkVersion", "assessmentStatus", "rationale"], true)
            : template.Documents.Single(item => item.Type == document.Type);
        var gaps = new List<ComplianceEvidenceGap>();
        ValidateDocument(template, string.Empty, document, requirement, request, gaps, out var root);
        ComplianceNativeEvidence.ValidateDocument(template, document, root, capturedAtUtc, gaps);
        return gaps;
    }

    private static void ValidateDocument(ComplianceFrameworkTemplate template, string controlId,
        ComplianceDocumentSnapshot document, ComplianceDocumentRequirement requirement,
        CreateCompliancePackageRequest request, List<ComplianceEvidenceGap> gaps, out JsonElement root)
    {
        root = default;
        void Gap(string code, string detail) => gaps.Add(new(controlId, code, detail, document.Id));
        if (CompliancePackagingEncoding.Hash(document.Content) != document.ContentSha256)
        {
            Gap("DocumentHashMismatch", "The stored content hash does not match the captured document bytes.");
        }
        if (document.Review != ComplianceDocumentReview.Approved || document.ReviewedByUserId is null || document.ReviewedByUserId == Guid.Empty)
        {
            Gap("DocumentNotApproved", "The document requires an authenticated approval.");
        }
        if (document.ReviewedAtUtc is null || document.ReviewedAtUtc.Value.Kind != DateTimeKind.Utc)
        {
            Gap("DocumentReviewMissing", "The document has no valid UTC review timestamp.");
        }
        if (requirement.RequiresPeriodCoverage &&
            (document.ValidFromUtc > request.PeriodStartUtc.UtcDateTime || document.ValidUntilUtc < request.PeriodEndUtc.UtcDateTime))
        {
            Gap("DocumentPeriodGap", "The reviewed document does not cover the complete requested period.");
        }
        IReadOnlyDictionary<string, string> fields = document.ValidationFields;
        if (document.MediaType == "application/json")
        {
            try
            {
                using var json = CompliancePackagingEncoding.Parse(document.Content);
                if (json.RootElement.ValueKind != JsonValueKind.Object) { throw new JsonException("An assessment must be a JSON object."); }
                if (ComplianceNativeEvidence.IsNative(template)) { root = json.RootElement.Clone(); }
                fields = json.RootElement.EnumerateObject().ToDictionary(property => property.Name,
                    property => property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                        JsonValueKind.Array or JsonValueKind.Object => property.Value.GetRawText(),
                        _ => string.Empty
                    }, StringComparer.Ordinal);
            }
            catch (JsonException)
            {
                Gap("InvalidDocumentJson", "The document is not an unambiguous JSON object within the supported depth.");
                fields = new Dictionary<string, string>();
            }
        }
        foreach (var field in requirement.RequiredFields)
        {
            if (!fields.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value) || value is "null" or "[]" or "{}")
            {
                Gap("DocumentFieldMissing", $"The document requires a nonempty {field} field.");
            }
        }
        if (!fields.TryGetValue("frameworkVersion", out var version) || version != template.Version)
        {
            Gap("FrameworkVersionMismatch", "The assessment framework version does not match the selected template.");
        }
        if (!fields.TryGetValue("assessmentStatus", out var status) || status != "satisfactory")
        {
            Gap("DeclaredControlDeficiency", "The assessment must explicitly record a satisfactory review; deficient, unknown and pending assessments remain gaps.");
        }
        if (requirement.RequiresControlAssessments) { gaps.AddRange(ValidateControlAssessments(document, fields)); }
    }

    private static IReadOnlyList<ComplianceEvidenceGap> ValidateControlAssessments(ComplianceDocumentSnapshot document,
        IReadOnlyDictionary<string, string> fields)
    {
        var gaps = new List<ComplianceEvidenceGap>();
        void Gap(string controlId, string code, string message) => gaps.Add(new(controlId, code, message, document.Id));
        JsonDocument? mapping = null;
        try
        {
            if (fields.TryGetValue("controlAssessments", out var content))
            {
                mapping = CompliancePackagingEncoding.Parse(System.Text.Encoding.UTF8.GetBytes(content));
                if (mapping.RootElement.ValueKind != JsonValueKind.Object) { mapping.Dispose(); mapping = null; }
            }
        }
        catch (JsonException) { mapping?.Dispose(); mapping = null; }
        using (mapping)
        {
            foreach (var id in document.ControlIds)
            {
                if (mapping is null || !mapping.RootElement.TryGetProperty(id, out var assessment) || assessment.ValueKind != JsonValueKind.Object)
                {
                    Gap(id, "ControlAssessmentMissing", $"The document requires a structured assessment for {id}.");
                    continue;
                }
                foreach (var field in new[] { "owner", "controlImplementation", "effectivenessEvidence" })
                {
                    if (!assessment.TryGetProperty(field, out var value) || value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        Gap(id, "ControlAssessmentIncomplete", $"Assessment {id} requires nonempty {field} evidence.");
                    }
                }
                if (!assessment.TryGetProperty("assessmentStatus", out var state) || state.ValueKind != JsonValueKind.String || state.GetString() != "satisfactory")
                {
                    Gap(id, "DeclaredControlDeficiency", $"The assessment for {id} is not satisfactory.");
                }
            }
        }
        return gaps;
    }

    private static void ValidateDataset(string controlId, ComplianceEvidenceDataset dataset,
        CreateCompliancePackageRequest request, List<ComplianceEvidenceGap> gaps)
    {
        void Gap(string code, string detail) => gaps.Add(new(controlId, code, detail, null));
        foreach (var error in dataset.ValidationErrors) { Gap("SourceValidationFailed", error); }
        try
        {
            using var json = CompliancePackagingEncoding.Parse(dataset.Content);
            if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() != dataset.RecordCount)
            {
                Gap("SourceCountMismatch", "The captured JSON array does not match the reported row count.");
            }
            else
            {
                var observed = new List<DateTime>();
                foreach (var item in json.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("observedAtUtc", out var timestamp) ||
                        timestamp.ValueKind != JsonValueKind.String || !timestamp.TryGetDateTime(out var time) || time.Kind != DateTimeKind.Utc)
                    {
                        Gap("SourceTimestampInvalid", "Every captured row must contain an observedAtUtc timestamp in UTC.");
                        break;
                    }
                    observed.Add(time);
                }
                if (observed.Count == dataset.RecordCount)
                {
                    var first = observed.Count == 0 ? (DateTime?)null : observed.Min();
                    var last = observed.Count == 0 ? (DateTime?)null : observed.Max();
                    var dates = observed.Select(DateOnly.FromDateTime).ToHashSet();
                    if (first != dataset.FirstObservedUtc || last != dataset.LastObservedUtc || !dates.SetEquals(dataset.ObservedDatesUtc))
                    {
                        Gap("SourceTimelineMismatch", "The declared observation range differs from the captured rows.");
                    }
                    if (dataset.Kind != ComplianceEvidenceKind.Retention &&
                        observed.Any(time => time < request.PeriodStartUtc.UtcDateTime || time > request.PeriodEndUtc.UtcDateTime))
                    {
                        Gap("SourceOutsidePeriod", "Captured event timestamps fall outside the requested audit period.");
                    }
                }
            }
        }
        catch (JsonException) { Gap("InvalidSourceJson", "The captured dataset is not valid, unambiguous JSON."); }
        if (dataset.RecordCount == 0 && dataset.Kind != ComplianceEvidenceKind.Incidents)
        {
            Gap("NoObservedEvidence", $"No rows were captured for {dataset.Kind}.");
        }
        if (dataset.Kind is ComplianceEvidenceKind.Operations or ComplianceEvidenceKind.Authentication or ComplianceEvidenceKind.Authorization or ComplianceEvidenceKind.Integrity)
        {
            var dates = dataset.ObservedDatesUtc.ToHashSet();
            var start = DateOnly.FromDateTime(request.PeriodStartUtc.UtcDateTime);
            var end = DateOnly.FromDateTime(request.PeriodEndUtc.UtcDateTime);
            var missing = 0;
            for (var date = start; date <= end; date = date.AddDays(1)) { if (!dates.Contains(date)) { missing++; } }
            if (missing != 0) { Gap("TimelineGap", $"{dataset.Kind} has no observed evidence on {missing} UTC date(s) in the requested period."); }
        }
    }

    internal static void ValidateInputs(ComplianceFrameworkTemplate template, CreateCompliancePackageRequest request,
        IReadOnlyList<ComplianceDocumentSnapshot> documents, IReadOnlyList<ComplianceEvidenceDataset> datasets, DateTime capturedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(datasets);
        void Require(bool condition, string field, string detail)
        {
            if (!condition) { throw new CompliancePackagingValidationException(new Dictionary<string, string[]> { [field] = [detail] }); }
        }
        Require(capturedAtUtc.Kind == DateTimeKind.Utc, "CapturedAtUtc", "Capture time must be UTC.");
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 200 && request.TemplateId == template.Id,
            "Request", "A name and matching template identifier are required.");
        Require(request.PeriodStartUtc.Offset == TimeSpan.Zero && request.PeriodEndUtc.Offset == TimeSpan.Zero &&
            request.PeriodStartUtc.Year >= 1980 && request.PeriodEndUtc.Year <= 2107 &&
            request.PeriodStartUtc <= request.PeriodEndUtc && request.PeriodEndUtc.UtcDateTime <= capturedAtUtc &&
            (request.PeriodEndUtc - request.PeriodStartUtc).TotalDays <= 1827,
            "Period", "Use an ordered UTC period of at most five years, ending no later than capture time.");
        Require(Enum.IsDefined(template.Framework) && Enum.IsDefined(template.PeriodMode) &&
            !string.IsNullOrWhiteSpace(template.Version) && template.Version.Length <= 100 &&
            template.Controls.Count is > 0 and <= 1500 && template.Controls.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count() == template.Controls.Count &&
            template.Documents.Count <= 100 && template.Documents.Select(item => item.Type).Distinct(StringComparer.Ordinal).Count() == template.Documents.Count,
            "Template", "Template identifiers, controls and document types must be bounded and unique.");
        Require(template.PeriodMode != ComplianceEvidencePeriodMode.PointInTime || request.PeriodStartUtc == request.PeriodEndUtc,
            "Period", "A point-in-time template requires matching start and end instants.");
        Require(template.Sources.Count is > 0 and <= 20 && template.Sources.All(IsHttpsSource) && template.Controls.All(control =>
            !string.IsNullOrWhiteSpace(control.Id) && control.Id.Length <= 100 && IsHttpsSource(control.SourceUri) &&
            control.AutomaticEvidence.Count <= 6 && control.AutomaticEvidence.All(Enum.IsDefined) &&
            control.AutomaticEvidence.Distinct().Count() == control.AutomaticEvidence.Count &&
            control.RequiredDocumentTypes.All(type => template.Documents.Any(item => item.Type == type)) &&
            (control.RequiredDocumentTypes.Count > 0 || control.AutomaticEvidence.Count > 0)),
            "Template", "Every control requires source provenance and a valid evidence or document mapping.");
        Require(template.Documents.All(item => !string.IsNullOrWhiteSpace(item.Type) && item.Type.Length <= 80 &&
            item.RequiredFields.Count <= 50 && item.RequiredFields.All(field => !string.IsNullOrWhiteSpace(field) && field.Length <= 80)),
            "Template", "Document requirements must contain bounded field names.");
        var controls = template.Controls.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        Require(request.DocumentIds.Count <= 100 && documents.Count <= 100 &&
            request.DocumentIds.Distinct().Count() == request.DocumentIds.Count && documents.Select(item => item.Id).Distinct().Count() == documents.Count &&
            request.DocumentIds.ToHashSet().SetEquals(documents.Select(item => item.Id)),
            "Documents", "Capture exactly the requested, unique document revisions (at most 100).");
        Require(FedRampComplianceCatalog.Find(template.Id) is null || documents.Count <= 60,
            "Documents", "FedRAMP profiles support at most 60 captured documents, including native artifact exports, within the bounded ZIP entry budget.");
        Require(Soc2ComplianceCatalog.Find(template.Id) is null || documents.Count <= 90,
            "Documents", "SOC2 profiles support at most 90 captured documents within the bounded ZIP entry budget.");
        Require(documents.All(item => item.Id != Guid.Empty && item.UploadedByUserId != Guid.Empty && item.Revision >= 1 &&
            !string.IsNullOrWhiteSpace(item.Name) && item.Name.Length <= 200 && IsHttpsSource(item.SourceUri) &&
            item.Content.Length > 0 && item.Content.Length <= MaximumDocumentBytes && item.ContentSha256.Length == 64 &&
            (item.TemplateId is null || item.TemplateId == template.Id) &&
            item.ValidFromUtc.Kind == DateTimeKind.Utc && item.ValidUntilUtc.Kind == DateTimeKind.Utc && item.ValidFromUtc <= item.ValidUntilUtc &&
            item.ControlIds.Count is > 0 and <= 1500 && item.ControlIds.All(controls.Contains) &&
            item.ControlIds.Distinct(StringComparer.Ordinal).Count() == item.ControlIds.Count &&
            Enum.IsDefined(item.Review) && (item.ReviewedAtUtc is null || item.ReviewedAtUtc <= capturedAtUtc) &&
            item.ValidationFields.Count <= 50 && item.ValidationFields.All(field => field.Key.Length <= 80 && field.Value.Length <= 4000) &&
            item.MediaType is "application/json" or "application/pdf" or "text/plain" &&
            (item.Type == "applicability" || template.Documents.Any(requirement => requirement.Type == item.Type))),
            "Documents", "Document identity, provenance, content, UTC coverage and control mapping are invalid.");
        Require(!ComplianceNativeEvidence.IsNative(template) || documents.Where(item => item.Type is not ("control-assessment" or "applicability" or "gdpr-dpia" or "gdpr-prior-consultation" or "fedramp-artifact" or "fedramp-supporting-evidence" or "soc2-supporting-evidence"))
            .GroupBy(item => item.Type, StringComparer.Ordinal).All(group => group.Count() == 1),
            "Documents", "Capture one unambiguous native scope, SoA or register revision per document type; individual DPIAs and consultations may be separate documents.");
        Require(request.Exclusions.Count <= template.Controls.Count && request.Exclusions.All(item => controls.Contains(item.ControlId) &&
            !string.IsNullOrWhiteSpace(item.Rationale) && item.Rationale.Length <= 2000 && item.ApplicabilityDocumentId != Guid.Empty) &&
            request.Exclusions.Select(item => item.ControlId).Distinct(StringComparer.Ordinal).Count() == request.Exclusions.Count,
            "Exclusions", "Exclusions must reference unique known controls and justified applicability evidence.");
        Require(template.Id != ComplianceFrameworkCatalog.IsoIsmsId || request.Exclusions.All(item => !item.ControlId.StartsWith("ISMS.", StringComparison.Ordinal)),
            "Exclusions", "ISO management requirements in clauses 4 through 10 cannot be excluded.");
        Require(template.Id != ComplianceFrameworkCatalog.GdprId || request.Exclusions.All(item => item.ControlId is not ("GDPR.Art.3" or "GDPR.Art.35" or "GDPR.Art.36")),
            "Exclusions", "GDPR scope and conditional DPIA requirements must be resolved through reviewed scope and screening evidence.");
        Require(FedRampComplianceCatalog.Find(template.Id) is null || request.Exclusions.Count == 0,
            "Exclusions", "FedRAMP applicability is resolved in reviewed SDR decisions; generic exclusions cannot bypass baseline evidence.");
        Require(Soc2ComplianceCatalog.Find(template.Id) is null || request.Exclusions.Count == 0,
            "Exclusions", "Select a SOC2 category scope template and resolve applicability in reviewed mappings; selected criteria cannot be excluded.");
        Require(datasets.Count <= 6 && datasets.Select(item => item.Kind).Distinct().Count() == datasets.Count && datasets.All(item =>
            Enum.IsDefined(item.Kind) && !string.IsNullOrWhiteSpace(item.Source) && item.Source.Length <= 200 &&
            item.Content.Length > 0 && item.Content.Length <= MaximumDatasetBytes && item.RecordCount is >= 0 and <= 50000 &&
            item.ObservedDatesUtc.Count <= 1828 && item.ValidationErrors.Count <= 100 && item.ValidationErrors.All(error => error.Length <= 2000)),
            "Datasets", "Collected sources must be unique, bounded and valid.");
        Require(documents.Sum(item => (long)item.Content.Length) + datasets.Sum(item => (long)item.Content.Length) <= MaximumContentBytes,
            "Content", "The evidence exceeds the 32 MiB package content limit.");
    }

    private static bool IsHttpsSource(string source) => source.Length <= 2048 && Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
}
