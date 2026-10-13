namespace GameGuild.Identity.Authentication;

/// <summary>
/// The first-factor verifier supplies the account version observed at verification.
/// These operations and their provider/store/issuer/audit collaborators must share the command transaction.
/// Completion accepts an opaque bearer and proof; it never accepts a client-selected account or tenant.
/// </summary>
public interface ISignInMfaService
{
    Task<SignInMfaPreparation> PrepareAsync(Guid verifiedSubjectId, int verifiedTokenVersion, Guid? requestedTenantId,
        DeviceInfo deviceInfo, SignInFirstFactor firstFactor, bool requiresRiskStepUp, CancellationToken cancellationToken);

    Task<SignInResponse> BeginAsync(Guid verifiedSubjectId, int verifiedTokenVersion, Guid? requestedTenantId,
        DeviceInfo deviceInfo, SignInFirstFactor firstFactor, bool requiresRiskStepUp, CancellationToken cancellationToken);

    Task<SignInResponse> CompleteCodeAsync(string bearer, string code, MfaMethod method,
        DeviceInfo deviceInfo, CancellationToken cancellationToken);

    Task<SignInMfaProof?> ReadSessionProofAsync(Guid subjectId, int tokenVersion, Guid tenantId, Guid sessionId,
        DateTimeOffset authenticatedAt, CancellationToken cancellationToken);
}
