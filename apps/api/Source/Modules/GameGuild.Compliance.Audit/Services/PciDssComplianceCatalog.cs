namespace GameGuild.Compliance.Audit;

/// <summary>
/// Principal PCI DSS v4.0.1 requirements for a SAQ A posture: the platform stores provider-issued payment
/// tokens only, and cardholder-data functions are executed by PCI DSS validated third-party payment
/// providers. Requirements that such a posture does not own are catalogued as not-applicable WITH a
/// mandatory reviewed rationale in the SAQ A eligibility declaration - an unmapped or unjustified
/// requirement is always reported as a gap. This is an evidence organization, not a ROC/QSA opinion.
/// </summary>
internal static class PciDssComplianceCatalog
{
    internal const string Id = "pci-dss-v4.0.1-saqa-evidence-v1";
    internal const string Version = "PCI DSS v4.0.1 / SAQ A";

    private const string Standard = "https://www.pcisecuritystandards.org/standards/pci-dss/";

    internal static ComplianceFrameworkTemplate? Find(string id) => id == Id ? Template() : null;
    internal static IReadOnlyList<ComplianceFrameworkTemplate> Templates() => [Template()];

    /// <summary>Requirements resolved by the reviewed SAQ A eligibility rationale rather than by an owned control.</summary>
    internal static readonly IReadOnlySet<string> SaqEligibilityIds = new HashSet<string>(StringComparer.Ordinal)
    { "PCI.1", "PCI.2", "PCI.5", "PCI.9" };

    internal static ComplianceFrameworkTemplate Template() => new(Id, ComplianceFramework.PCI_DSS, Version,
        ComplianceEvidencePeriodMode.Period,
        [
            Standard,
            "https://www.pcisecuritystandards.org/document_library/",
            "https://listings.pcisecuritystandards.org/"
        ],
        [
            // Not applicable through the reviewed SAQ A eligibility declaration: network controls, system
            // configurations, anti-malware and physical controls for cardholder-data systems sit with the
            // validated payment providers; this platform operates no cardholder-data environment.
            new("PCI.1", Standard, [], ["pci-saqa-eligibility"]),
            new("PCI.2", Standard, [], ["pci-saqa-eligibility"]),
            // Stored account data: only provider tokens (not account data) are stored; storage protection,
            // PAN prohibition and disposal are declared in the reviewed tokenized-storage assessment.
            new("PCI.3", Standard, [], ["pci-saqa-eligibility", "pci-tokenized-storage"]),
            new("PCI.4", Standard, [], ["pci-saqa-eligibility", "pci-tokenized-storage"]),
            new("PCI.5", Standard, [], ["pci-saqa-eligibility"]),
            // Secure development: the platform's static analysis (CodeQL), dependency scanning and patch
            // cadence evidence maps here.
            new("PCI.6", Standard, [], ["pci-vulnerability-management"]),
            new("PCI.7", Standard, [ComplianceEvidenceKind.Authorization], ["pci-tokenized-storage"]),
            new("PCI.8", Standard, [ComplianceEvidenceKind.Authentication], ["pci-policy"]),
            new("PCI.9", Standard, [], ["pci-saqa-eligibility"]),
            new("PCI.10", Standard, [ComplianceEvidenceKind.Operations, ComplianceEvidenceKind.Integrity], ["pci-policy"]),
            // Regular security testing: the software-testing portion is owned (vulnerability management);
            // CDE scanning/penetration testing belongs to the validated providers.
            new("PCI.11", Standard, [], ["pci-vulnerability-management", "pci-saqa-eligibility"]),
            new("PCI.12", Standard, [], ["pci-policy"])
        ],
        [
            // saqType, tokenizationOnly and panStoragePosition are validated natively as the SAQ A posture;
            // requirementRationales must carry a nonempty reviewed rationale for every mapped requirement.
            Requirement("pci-saqa-eligibility", "saqType", "merchantChannelDescription", "panStoragePosition",
                "validatedProviders", "eligibilityConfirmations", "requirementRationales",
                "providerAttestationReferences", "reviewedAtUtc"),
            Requirement("pci-tokenized-storage", "storagePolicy", "tokenizationScheme", "providerVaultReference",
                "panProhibition", "accessRestrictions", "retentionAndDisposal"),
            Requirement("pci-vulnerability-management", "staticAnalysisTools", "dependencyScanningPolicy",
                "patchManagementCadence", "remediationTracking", "securityTestingScope", "scanReviewEvidence"),
            Requirement("pci-policy", "informationSecurityPolicy", "policyReviewSchedule", "rolesAndResponsibilities",
                "loggingAndMonitoringPolicy", "distributionAcknowledgement")
        ]);

    private static ComplianceDocumentRequirement Requirement(string type, params string[] fields) =>
        new(type, ["frameworkVersion", "assessmentStatus", "owner", .. fields], true);
}
