namespace GameGuild.Compliance.Audit;

/// <summary>
/// Security Rule (45 CFR Part 164 Subpart C) and Breach Notification Rule (Subpart D) safeguard references
/// mapped to the platform evidence sources. Collecting evidence does not establish compliance or a
/// regulatory determination; covered entity/business associate responsibilities remain the tenant's.
/// </summary>
internal static class HipaaComplianceCatalog
{
    internal const string Id = "hipaa-45cfr164-security-evidence-v1";
    internal const string Version = "45 CFR Part 164 Subparts C and D (HIPAA Security/Breach)";

    private const string SubpartC = "https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-C";
    private const string SubpartD = "https://www.ecfr.gov/current/title-45/subtitle-A/subchapter-C/part-164/subpart-D";

    /// <summary>Required standards and required implementation specifications (plus the Breach Notification Rule duties);
    /// addressable safeguards may instead be resolved through reviewed applicability evidence.</summary>
    internal static readonly IReadOnlySet<string> CoreRequiredIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "164.308(a)(1)(i)", "164.308(a)(1)(ii)(A)", "164.308(a)(1)(ii)(B)", "164.308(a)(1)(ii)(C)", "164.308(a)(1)(ii)(D)",
        "164.308(a)(2)", "164.308(a)(3)(i)", "164.308(a)(4)(i)", "164.308(a)(5)(i)", "164.308(a)(6)(i)", "164.308(a)(6)(ii)",
        "164.308(a)(7)(i)", "164.308(a)(7)(ii)(A)", "164.308(a)(7)(ii)(B)", "164.308(a)(7)(ii)(C)", "164.308(a)(8)",
        "164.308(b)(1)", "164.308(b)(2)",
        "164.310(a)(1)", "164.310(b)", "164.310(c)", "164.310(d)(1)",
        "164.312(a)(1)", "164.312(a)(2)(i)", "164.312(b)", "164.312(c)(1)", "164.312(d)", "164.312(e)(1)",
        "164.314(a)", "164.314(b)",
        "164.316(a)", "164.316(b)(1)", "164.316(b)(2)(i)", "164.316(b)(2)(ii)", "164.316(b)(2)(iii)",
        "164.402", "164.404", "164.406", "164.408", "164.410", "164.412", "164.414"
    };

    internal static ComplianceFrameworkTemplate? Find(string id) => id == Id ? Template() : null;
    internal static IReadOnlyList<ComplianceFrameworkTemplate> Templates() => [Template()];

    internal static ComplianceFrameworkTemplate Template()
    {
        var controls = new List<ComplianceControlTemplate>();
        // Administrative safeguards (§164.308). The healthcare-clearinghouse isolation specification
        // (§164.308(a)(4)(ii)(A)) is omitted: this platform is not a clearinghouse.
        Administrative("164.308(a)(1)(i)", ["hipaa-risk-analysis", "hipaa-security-management"], [Operations]);
        Administrative("164.308(a)(1)(ii)(A)", ["hipaa-risk-analysis"], []);
        Administrative("164.308(a)(1)(ii)(B)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(1)(ii)(C)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(1)(ii)(D)", ["hipaa-security-management"], [Operations, Integrity]);
        Administrative("164.308(a)(2)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(3)(i)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(3)(ii)(A)", ["hipaa-security-management"], [Authorization]);
        Administrative("164.308(a)(3)(ii)(B)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(3)(ii)(C)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(4)(i)", ["hipaa-security-management"], [Authorization]);
        Administrative("164.308(a)(4)(ii)(B)", ["hipaa-security-management"], [Authorization]);
        Administrative("164.308(a)(4)(ii)(C)", ["hipaa-security-management"], [Authorization]);
        Administrative("164.308(a)(5)(i)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(5)(ii)(A)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(5)(ii)(B)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(5)(ii)(C)", ["hipaa-security-management"], [Authentication, Operations]);
        Administrative("164.308(a)(5)(ii)(D)", ["hipaa-security-management"], [Authentication]);
        Administrative("164.308(a)(6)(i)", ["hipaa-breach-notification"], [Incidents]);
        Administrative("164.308(a)(6)(ii)", ["hipaa-breach-notification"], [Incidents]);
        Administrative("164.308(a)(7)(i)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(7)(ii)(A)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(7)(ii)(B)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(7)(ii)(C)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(7)(ii)(D)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(7)(ii)(E)", ["hipaa-security-management"], []);
        Administrative("164.308(a)(8)", ["hipaa-security-management", "hipaa-risk-analysis"], []);
        Administrative("164.308(b)(1)", ["hipaa-security-management"], []);
        Administrative("164.308(b)(2)", ["hipaa-security-management"], []);
        // Physical safeguards (§164.310): the platform runs in managed hosting, so facility and media
        // declarations document the split between the tenant, this platform and the hosting provider.
        foreach (var id in new[] { "164.310(a)(1)", "164.310(a)(2)(i)", "164.310(a)(2)(ii)", "164.310(a)(2)(iii)",
            "164.310(b)", "164.310(c)", "164.310(d)(1)", "164.310(d)(2)(i)", "164.310(d)(2)(ii)",
            "164.310(d)(2)(iii)", "164.310(d)(2)(iv)" })
        { controls.Add(new(id, SubpartC + "/section-164.310", [], ["hipaa-physical-safeguards"])); }
        // Technical safeguards (§164.312) map to the collected audit, access-control, MFA and
        // tamper-evidence sources plus the reviewed encryption declarations.
        controls.Add(new("164.312(a)(1)", SubpartC + "/section-164.312", [Authorization, Authentication], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(a)(2)(i)", SubpartC + "/section-164.312", [Authentication], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(a)(2)(ii)", SubpartC + "/section-164.312", [], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(a)(2)(iii)", SubpartC + "/section-164.312", [], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(a)(2)(iv)", SubpartC + "/section-164.312", [], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(b)", SubpartC + "/section-164.312", [Operations, Integrity], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(c)(1)", SubpartC + "/section-164.312", [Integrity], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(c)(2)", SubpartC + "/section-164.312", [Integrity], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(d)", SubpartC + "/section-164.312", [Authentication], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(e)(1)", SubpartC + "/section-164.312", [], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(e)(2)(i)", SubpartC + "/section-164.312", [Integrity], ["hipaa-technical-safeguards"]));
        controls.Add(new("164.312(e)(2)(ii)", SubpartC + "/section-164.312", [], ["hipaa-technical-safeguards"]));
        // Organizational requirements (§164.314) and documentation requirements (§164.316).
        foreach (var id in new[] { "164.314(a)", "164.314(b)", "164.316(a)", "164.316(b)(1)",
            "164.316(b)(2)(i)", "164.316(b)(2)(ii)", "164.316(b)(2)(iii)" })
        { controls.Add(new(id, SubpartC + "/section-" + id[..7], [], ["hipaa-security-management"])); }
        // Breach Notification Rule (§164.402 through §164.414).
        controls.Add(new("164.402", SubpartD + "/section-164.402", [], ["hipaa-breach-notification"]));
        controls.Add(new("164.404", SubpartD + "/section-164.404", [Incidents], ["hipaa-breach-notification"]));
        foreach (var id in new[] { "164.406", "164.408", "164.410", "164.412", "164.414" })
        { controls.Add(new(id, SubpartD + "/section-" + id, [], ["hipaa-breach-notification"])); }
        return new(Id, ComplianceFramework.HIPAA, Version, ComplianceEvidencePeriodMode.Period,
        [
            SubpartC, SubpartD,
            "https://www.hhs.gov/hipaa/for-professionals/security/index.html",
            "https://www.hhs.gov/hipaa/for-professionals/breach-notification/index.html"
        ], controls,
        [
            Requirement("hipaa-risk-analysis", "scope", "threatsIdentified", "vulnerabilitiesIdentified",
                "likelihoodAndImpact", "riskDecisions", "mitigationPlan", "reviewSchedule", "assessedAtUtc"),
            Requirement("hipaa-security-management", "securityOfficer", "workforceSecurityProcedures", "sanctionPolicy",
                "awarenessTrainingProgram", "contingencyPlan", "businessAssociateRegister",
                "documentationRetentionSchedule", "policyReviewSchedule"),
            Requirement("hipaa-physical-safeguards", "facilityAccessControls", "hostingProviderResponsibilities",
                "workstationUse", "workstationSecurity", "deviceAndMediaControls", "mediaDisposalAndReuse"),
            Requirement("hipaa-technical-safeguards", "accessControlMechanisms", "emergencyAccess", "automaticLogoff",
                "encryptionAtRest", "encryptionInTransit", "keyManagement", "auditControlsConfiguration",
                "authenticationMechanisms", "transmissionIntegrity"),
            // notificationWindowDays is validated natively as a bounded integer (1-60), not as a declared string field.
            Requirement("hipaa-breach-notification", "breachDeterminationProcess", "individualNotificationProcedure",
                "hhsNotificationProcedure", "mediaNotificationProcedure",
                "businessAssociateFlowdown", "lawEnforcementDelayProcedure", "breachRegisterMaintenance")
        ]);
        void Administrative(string id, string[] documents, ComplianceEvidenceKind[] kinds) =>
            controls.Add(new(id, SubpartC + "/section-164.308", kinds, documents));
    }

    private static ComplianceDocumentRequirement Requirement(string type, params string[] fields) =>
        new(type, ["frameworkVersion", "assessmentStatus", "owner", .. fields], true, true);

    private const ComplianceEvidenceKind Operations = ComplianceEvidenceKind.Operations;
    private const ComplianceEvidenceKind Authentication = ComplianceEvidenceKind.Authentication;
    private const ComplianceEvidenceKind Authorization = ComplianceEvidenceKind.Authorization;
    private const ComplianceEvidenceKind Incidents = ComplianceEvidenceKind.Incidents;
    private const ComplianceEvidenceKind Integrity = ComplianceEvidenceKind.Integrity;
}
