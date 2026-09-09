using GameGuild.API.Authorization;
using GameGuild.Compliance.FinancialCrime;
using GameGuild.Compliance.KYC;
using GameGuild.CQRS;
using GameGuild.Economy.Risk;
using GameGuild.TrustSafety;

namespace GameGuild.API.Controllers;

public sealed record AssignFinancialCrimeCaseEndpointCommand(
    Guid TenantId, Guid CaseId, Guid ActorId, long ExpectedVersion, DateTimeOffset Now)
    : ICommand<FinancialCrimeCase>;
public sealed record DecideFinancialCrimeCaseEndpointCommand(
    FinancialCrimeCaseDecision Decision, long ExpectedCaseVersion)
    : ICommand<FinancialCrimeCaseDecision>;
public sealed record RecordRegulatoryReferenceEndpointCommand(
    Guid TenantId, Guid CaseId, string Kind, string JurisdictionCode, string ReferenceHash, Guid ActorId, DateTimeOffset Now)
    : ICommand;
public sealed record AssignTrustSafetyAppealEndpointCommand(
    Guid TenantId, Guid AppealId, Guid ActorId, long ExpectedVersion, DateTimeOffset Now)
    : ICommand<TrustSafetyAppeal>;
public sealed record DecideTrustSafetyAppealEndpointCommand(
    Guid TenantId, Guid AppealId, Guid ActorId, long ExpectedVersion, bool Overturn, string ReasonCode, string EvidenceHash, DateTimeOffset Now)
    : ICommand<TrustSafetyAppeal>;

public sealed record ProposeComplianceHoldReleaseEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid TenantId, Guid HoldId, Guid ActorId, DateTimeOffset Now)
    : ICommand<ComplianceHoldAdministrationState>;
public sealed record ApproveComplianceHoldReleaseEndpointCommand(
    EconomyStepUpOperation Operation, string StepUpReceipt, Guid TenantId, Guid HoldId, Guid ActorId, DateTimeOffset Now)
    : ICommand<ComplianceHoldAdministrationState>;

public sealed record ApproveEconomyRiskReviewEndpointCommand(
    Guid TenantId, Guid ReviewId, Guid ActorId, RiskManualDecisionCode DecisionCode, string Resolution, DateTimeOffset Now)
    : ICommand<RiskReviewCase>;
public sealed record RejectEconomyRiskReviewEndpointCommand(
    Guid TenantId, Guid ReviewId, Guid ActorId, RiskManualDecisionCode DecisionCode, string Resolution, DateTimeOffset Now)
    : ICommand<RiskReviewCase>;

public sealed record StartKycOnboardingEndpointCommand(StartKycAmlRequest Request) : ICommand<KycAmlOnboarding>;
public sealed record CreateKycAccessTokenEndpointCommand(
    Guid TenantId, string Subject, int LifetimeSeconds)
    : ICommand<KycAmlAccessToken>;
public sealed record IngestSumSubWebhookEndpointCommand(
    byte[] Payload, string Digest, string Algorithm, DateTimeOffset IssuedAt, DateTimeOffset Now)
    : ICommand<SumSubWebhookIngestionResult>;

public sealed class EconomyComplianceEndpointCommandHandler(
    IFinancialCrimeControlPlane financialCrime,
    ITrustSafetyControlPlane trustSafety,
    IComplianceHoldAdministrationStore holds,
    IEconomyStepUpExecutor stepUp,
    IRiskReviewStore riskReviews,
    IKycAmlOrchestrator kyc) :
    ICommandHandler<AssignFinancialCrimeCaseEndpointCommand, FinancialCrimeCase>,
    ICommandHandler<DecideFinancialCrimeCaseEndpointCommand, FinancialCrimeCaseDecision>,
    ICommandHandler<RecordRegulatoryReferenceEndpointCommand>,
    ICommandHandler<AssignTrustSafetyAppealEndpointCommand, TrustSafetyAppeal>,
    ICommandHandler<DecideTrustSafetyAppealEndpointCommand, TrustSafetyAppeal>,
    ICommandHandler<ProposeComplianceHoldReleaseEndpointCommand, ComplianceHoldAdministrationState>,
    ICommandHandler<ApproveComplianceHoldReleaseEndpointCommand, ComplianceHoldAdministrationState>,
    ICommandHandler<ApproveEconomyRiskReviewEndpointCommand, RiskReviewCase>,
    ICommandHandler<RejectEconomyRiskReviewEndpointCommand, RiskReviewCase>,
    ICommandHandler<StartKycOnboardingEndpointCommand, KycAmlOnboarding>,
    ICommandHandler<CreateKycAccessTokenEndpointCommand, KycAmlAccessToken>,
    ICommandHandler<IngestSumSubWebhookEndpointCommand, SumSubWebhookIngestionResult>
{
    public async Task<FinancialCrimeCase> Handle(AssignFinancialCrimeCaseEndpointCommand request, CancellationToken ct) =>
        await financialCrime.AssignCaseAsync(
            request.TenantId, request.CaseId, request.ActorId, request.ExpectedVersion, request.Now, ct).ConfigureAwait(false);

    public async Task<FinancialCrimeCaseDecision> Handle(DecideFinancialCrimeCaseEndpointCommand request, CancellationToken ct) =>
        await financialCrime.DecideCaseAsync(request.Decision, request.ExpectedCaseVersion, ct).ConfigureAwait(false);

    public async Task<Unit> Handle(RecordRegulatoryReferenceEndpointCommand request, CancellationToken ct)
    {
        await financialCrime.RecordRegulatoryReferenceAsync(
            request.TenantId, request.CaseId, request.Kind, request.JurisdictionCode,
            request.ReferenceHash, request.ActorId, request.Now, ct).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<TrustSafetyAppeal> Handle(AssignTrustSafetyAppealEndpointCommand request, CancellationToken ct) =>
        await trustSafety.AssignAppealAsync(
            request.TenantId, request.AppealId, request.ActorId, request.ExpectedVersion, request.Now, ct).ConfigureAwait(false);

    public async Task<TrustSafetyAppeal> Handle(DecideTrustSafetyAppealEndpointCommand request, CancellationToken ct) =>
        await trustSafety.DecideAppealAsync(
            request.TenantId, request.AppealId, request.ActorId, request.ExpectedVersion, request.Overturn,
            request.ReasonCode, request.EvidenceHash, request.Now, ct).ConfigureAwait(false);

    public async Task<ComplianceHoldAdministrationState> Handle(ProposeComplianceHoldReleaseEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => holds.ProposeReleaseAsync(
                request.TenantId, request.HoldId, request.ActorId, evidenceHash, request.Now, token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<ComplianceHoldAdministrationState> Handle(ApproveComplianceHoldReleaseEndpointCommand request, CancellationToken ct) =>
        await stepUp.ExecuteAsync(
            request.Operation,
            request.StepUpReceipt,
            (evidenceHash, token) => holds.ApproveReleaseAsync(
                request.TenantId, request.HoldId, request.ActorId, evidenceHash, request.Now, token).AsTask(),
            ct).ConfigureAwait(false);

    public async Task<RiskReviewCase> Handle(ApproveEconomyRiskReviewEndpointCommand request, CancellationToken ct) =>
        await riskReviews.ApproveAsync(
            request.TenantId, request.ReviewId, request.ActorId, request.DecisionCode, request.Resolution, request.Now, ct).ConfigureAwait(false);

    public async Task<RiskReviewCase> Handle(RejectEconomyRiskReviewEndpointCommand request, CancellationToken ct) =>
        await riskReviews.RejectAsync(
            request.TenantId, request.ReviewId, request.ActorId, request.DecisionCode, request.Resolution, request.Now, ct).ConfigureAwait(false);

    public async Task<KycAmlOnboarding> Handle(StartKycOnboardingEndpointCommand request, CancellationToken ct) =>
        await kyc.StartAsync(request.Request, ct).ConfigureAwait(false);

    public async Task<KycAmlAccessToken> Handle(CreateKycAccessTokenEndpointCommand request, CancellationToken ct) =>
        await kyc.CreateAccessTokenAsync(request.TenantId, request.Subject, request.LifetimeSeconds, ct).ConfigureAwait(false);

    public async Task<SumSubWebhookIngestionResult> Handle(IngestSumSubWebhookEndpointCommand request, CancellationToken ct) =>
        await kyc.IngestWebhookAsync(
            request.Payload, request.Digest, request.Algorithm, request.IssuedAt, request.Now, ct).ConfigureAwait(false);
}
