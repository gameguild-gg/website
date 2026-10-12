namespace GameGuild.Compliance.Audit;

public interface IComplianceFrameworkCatalog
{
    IReadOnlyList<ComplianceFrameworkTemplate> GetTemplates();
    ComplianceFrameworkTemplate? Find(string id);
}

/// <summary>Versioned evidence mappings. Reviewed assessments establish applicability and effectiveness.</summary>
public sealed class ComplianceFrameworkCatalog : IComplianceFrameworkCatalog
{
    private static readonly ComplianceFrameworkTemplate Iso27001 = BuildIso27001();
    private static readonly ComplianceFrameworkTemplate IsoIsms = BuildIsoIsms();
    private static readonly ComplianceFrameworkTemplate Gdpr = BuildGdpr();
    private static readonly IReadOnlyList<ComplianceFrameworkTemplate> FedRamp = FedRampComplianceCatalog.Templates();
    private static readonly IReadOnlyList<ComplianceFrameworkTemplate> Soc2 = Soc2ComplianceCatalog.Templates();
    public IReadOnlyList<ComplianceFrameworkTemplate> GetTemplates() => new[] { IsoIsms, Gdpr, Iso27001 }.Concat(FedRamp).Concat(Soc2).Select(Clone).ToArray();
    public ComplianceFrameworkTemplate? Find(string id)
    {
        var template = new[] { IsoIsms, Gdpr, Iso27001 }.Concat(FedRamp).Concat(Soc2).SingleOrDefault(item => item.Id == id);
        return template is null ? null : Clone(template);
    }

    internal const string IsoIsmsId = "iso27001-2022-isms-evidence-v2";
    internal const string GdprId = "gdpr-2016-679-evidence-v1";

    private static ComplianceFrameworkTemplate BuildIsoIsms()
    {
        const string source = "https://www.iso.org/standard/27001";
        const string soaSource = "https://committee.iso.org/files/live/sites/jtc1sc27/files/resources/ISO-IECJTC1-SC27-WG1_N3298_Auditing%20Practices%20Note%20-%20SoA.pdf";
        var documents = new List<ComplianceDocumentRequirement>
        {
            Requirement("control-assessment", true, "controlAssessments"),
            Requirement("iso-context", false, "scope", "interestedParties", "climateRelevanceAssessment"),
            Requirement("iso-leadership", false, "policy", "rolesResponsibilities", "managementCommitment"),
            Requirement("iso-planning", false, "riskMethodology", "riskRegister", "riskTreatmentPlan", "objectives", "changePlanning"),
            Requirement("iso-support", false, "resources", "competence", "awareness", "communication", "documentControls"),
            Requirement("iso-operation", false, "operationalControls", "outsourcedProcesses", "riskReassessments", "treatmentImplementation"),
            Requirement("iso-performance", false, "measurementResults", "internalAuditProgramme", "internalAuditResults", "managementReview"),
            Requirement("iso-improvement", false, "continualImprovement", "correctiveActions", "effectivenessVerification"),
            Requirement("iso-soa", false, "scope", "soaRevision", "necessaryControls", "annexADecisions")
        };
        var types = new[] { "iso-context", "iso-leadership", "iso-planning", "iso-support", "iso-operation", "iso-performance", "iso-improvement" };
        var controls = types.Select((type, index) => new ComplianceControlTemplate($"ISMS.{index + 4}", source, [],
            index == 2 ? ["control-assessment", type, "iso-soa"] : ["control-assessment", type])).ToList();
        foreach (var control in Iso27001.Controls)
        {
            var evidence = control.Id is "A.5.15" or "A.5.16" or "A.5.17" or "A.5.18" or "A.8.2" or "A.8.3" or "A.8.4" or "A.8.5"
                ? new[] { ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Authentication, ComplianceEvidenceKind.Authorization }
                : control.Id is "A.8.15" or "A.8.16"
                    ? new[] { ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Integrity }
                    : control.Id is "A.5.24" or "A.5.25" or "A.5.26" or "A.5.27" or "A.5.28"
                        ? new[] { ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Incidents }
                        : new[] { ComplianceEvidenceKind.Operations };
            controls.Add(control with { AutomaticEvidence = evidence, RequiredDocumentTypes = ["control-assessment", "iso-soa"] });
        }
        return new(IsoIsmsId, ComplianceFramework.ISO27001, "ISO/IEC27001:2022+Amd1:2024", ComplianceEvidencePeriodMode.Period,
            [.. Iso27001.Sources, "https://www.iso.org/standard/88435.html", soaSource], controls, documents);
    }

    private static ComplianceFrameworkTemplate BuildGdpr()
    {
        const string source = "https://eur-lex.europa.eu/eli/reg/2016/679/ojv";
        var documents = new[]
        {
            Requirement("gdpr-accountability", true, "processingScope", "legalBases", "nationalLawAssessment", "controlAssessments"),
            Requirement("gdpr-processing-records", false, "processingActivities"),
            Requirement("gdpr-rights", false, "notices", "rightsHandling", "automatedDecisionReview", "complaintsProcess"),
            Requirement("gdpr-security", false, "securityMeasures", "restorationTests", "effectivenessTests", "breachRegister", "notificationDecisions"),
            Requirement("gdpr-dpia-screening", false, "screenings"),
            Requirement("gdpr-dpia", false, "activityId", "processingDescription", "processingPurposes", "necessityProportionality", "individualRightsRisks",
                "mitigations", "residualRiskDecision", "consultation", "assessmentTiming", "reviewTriggers"),
            Requirement("gdpr-prior-consultation", false, "activityId", "authority", "consultationStatus", "submissionReference"),
            Requirement("gdpr-transfers", false, "transferInventory", "transferBasisReview", "safeguards", "derogationReview")
        };
        // Organisation-facing processing duties, including territorial scope and research safeguards.
        // Authority/institutional chapters are not presented as tenant processing controls.
        var articles = new[] { 3 }.Concat(Enumerable.Range(5, 35)).Concat(Enumerable.Range(44, 6)).Append(89);
        var controls = articles.Select(article =>
        {
            var required = new List<string> { "gdpr-accountability" };
            var evidence = new List<ComplianceEvidenceKind> { ComplianceEvidenceKind.Operations };
            if (article is >= 12 and <= 23) { required.Add("gdpr-rights"); }
            if (article == 30) { required.Add("gdpr-processing-records"); }
            if (article is >= 32 and <= 34)
            {
                required.Add("gdpr-security");
                evidence.AddRange(article == 32
                    ? [ComplianceEvidenceKind.Authentication, ComplianceEvidenceKind.Authorization, ComplianceEvidenceKind.Integrity]
                    : new[] { ComplianceEvidenceKind.Incidents });
            }
            if (article is 35 or 36) { required.Add("gdpr-dpia-screening"); }
            if (article is >= 44 and <= 49) { required.Add("gdpr-transfers"); }
            return new ComplianceControlTemplate($"GDPR.Art.{article}", source, evidence, required);
        }).ToArray();
        return new(GdprId, ComplianceFramework.GDPR, "Regulation(EU)2016/679", ComplianceEvidencePeriodMode.Period,
            [source, "https://commission.europa.eu/law/law-topic/data-protection/information-business-and-organisations/obligations_en"], controls, documents);
    }

    private static ComplianceDocumentRequirement Requirement(string type, bool controlAssessments, params string[] fields) =>
        new(type, ["frameworkVersion", "assessmentStatus", "owner", .. fields], true, controlAssessments);

    private static ComplianceFrameworkTemplate BuildIso27001()
    {
        const string source = "https://www.iso.org/standard/27001";
        var controls = new List<ComplianceControlTemplate>();
        foreach (var (family, count) in new[] { (5, 37), (6, 8), (7, 14), (8, 34) })
        {
            for (var number = 1; number <= count; number++)
            {
                // These logs are supplemental operational evidence. A reviewed control assessment remains mandatory.
                var evidence = family == 8
                    ? new[] { ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Authentication, ComplianceEvidenceKind.Authorization, ComplianceEvidenceKind.Integrity }
                    : new[] { ComplianceEvidenceKind.Operations };
                controls.Add(new($"A.{family}.{number}", source, evidence, ["control-assessment"]));
            }
        }
        return new("iso27001-2022-evidence-v1", ComplianceFramework.ISO27001, "ISO/IEC27001:2022",
            ComplianceEvidencePeriodMode.Period,
            [source, "https://committee.iso.org/files/live/sites/jtc1sc27/files/resources/ISO-IECJTC1-SC27_N22394_SC%2027%20Journal%20Volume%202%2C%20Issue%202%20-%20Special%20issue%20on%20ISO-IEC%2027002.pdf"],
            controls,
            [new("control-assessment", ["frameworkVersion", "assessmentStatus", "owner", "controlAssessments"], true, true)]);
    }

    private static ComplianceFrameworkTemplate Clone(ComplianceFrameworkTemplate template) => template with
    {
        Sources = template.Sources.ToArray(),
        Controls = template.Controls.Select(item => item with { AutomaticEvidence = item.AutomaticEvidence.ToArray(), RequiredDocumentTypes = item.RequiredDocumentTypes.ToArray() }).ToArray(),
        Documents = template.Documents.Select(item => item with { RequiredFields = item.RequiredFields.ToArray() }).ToArray()
    };
}
