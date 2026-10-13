using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>
/// Gates a verified first factor before credential issuance. Pending challenges carry no ordinary credentials.
/// The command owner must roll back exceptions and ordinary denials, including provider proof consumption.
/// </summary>
public sealed class SignInMfaService(
    ISignInMfaSubjectReader subjects,
    IMfaSubjectRequirementPolicy policy,
    ISignInMfaChallengeStore challenges,
    IMfaService mfa,
    IAuthenticatedSessionIssuer issuer,
    IAuthenticationAuditEventSink audit,
    ISessionMfaEvidenceStore sessionEvidence,
    ISignInMfaEnrollmentPort enrollment,
    TimeProvider? timeProvider = null) : ISignInMfaService
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<SignInResponse> BeginAsync(Guid verifiedSubjectId, int verifiedTokenVersion, Guid? requestedTenantId,
        DeviceInfo deviceInfo, SignInFirstFactor firstFactor, bool requiresRiskStepUp, CancellationToken cancellationToken)
    {
        var prepared = await PrepareAsync(verifiedSubjectId, verifiedTokenVersion, requestedTenantId, deviceInfo,
            firstFactor, requiresRiskStepUp, cancellationToken).ConfigureAwait(false);
        if (prepared.Outcome is { } outcome) { return outcome; }
        if (!prepared.MayIssueOrdinaryCredentials || prepared.Subject is not { } subject || prepared.Decision is not { } decision)
        {
            throw new InvalidOperationException("Current subject policy did not permit ordinary credentials.");
        }
        return await IssueAsync(subject, decision.TenantId, deviceInfo, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SignInMfaPreparation> PrepareAsync(Guid verifiedSubjectId, int verifiedTokenVersion, Guid? requestedTenantId,
        DeviceInfo deviceInfo, SignInFirstFactor firstFactor, bool requiresRiskStepUp, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(deviceInfo);
        if (verifiedSubjectId == Guid.Empty || verifiedTokenVersion < 1 || !Enum.IsDefined(firstFactor))
        {
            throw new ArgumentException("Verified first-factor evidence is required.");
        }
        var current = await subjects.ReadCurrentAsync(verifiedSubjectId, cancellationToken).ConfigureAwait(false);
        RequireAccount(current, verifiedSubjectId, verifiedTokenVersion);
        if (IsLocked(current!)) { return SignInMfaPreparation.WithOutcome(Denied()); }
        var decision = await policy.EvaluateAsync(verifiedSubjectId, requestedTenantId, cancellationToken).ConfigureAwait(false);
        RequireDecision(decision, verifiedSubjectId, requestedTenantId);
        if (!decision.RequiresMfa && !current!.HasEnrolledMfa && !requiresRiskStepUp)
        {
            return SignInMfaPreparation.PermitWithoutMfa(current.User, decision);
        }

        var token = SignInMfaChallengeToken.Create();
        if (!SignInMfaChallengeToken.TryHash(token, out var hash)) { throw new InvalidOperationException("Challenge creation failed."); }
        var now = _time.GetUtcNow();
        var challenge = new SignInMfaChallenge
        {
            Id = Guid.NewGuid(), SubjectId = verifiedSubjectId, SubjectTokenVersion = verifiedTokenVersion,
            TenantId = decision.TenantId, PolicyFingerprint = decision.PolicyFingerprint, TokenHash = hash,
            FirstFactor = firstFactor, Purpose = current!.HasEnrolledMfa ? SignInMfaPurpose.VerifyFactor : SignInMfaPurpose.EnrollFactor,
            CreatedAt = now, ExpiresAt = now.AddMinutes(5)
        };
        await challenges.AddAsync(challenge, cancellationToken).ConfigureAwait(false);
        await RecordAsync("Authentication.MfaSignInRequired", challenge, false, firstFactor.ToString(), deviceInfo,
            null, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        // This marker is created only after challenge persistence and audit both succeed.
        return SignInMfaPreparation.WithOutcome(new SignInMfaPendingResponse(token, challenge.ExpiresAt, challenge.Purpose,
            requiresRiskStepUp, current.User, decision.TenantId));
    }

    public async Task<MfaSignInEnrollmentResponse> StartEnrollmentAsync(string bearer, DeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(deviceInfo);
        if (!SignInMfaChallengeToken.TryHash(bearer, out var hash)) { return new(); }
        var challenge = await challenges.FindActiveAsync(hash, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (challenge is null || challenge.Purpose != SignInMfaPurpose.EnrollFactor) { return new(); }
        await enrollment.AcquireSubjectLockAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
        challenge = await challenges.FindActiveAsync(hash, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (challenge is null || challenge.Purpose != SignInMfaPurpose.EnrollFactor) { return new(); }
        var current = await ReadBoundSubjectAsync(challenge, cancellationToken, enrolled: false).ConfigureAwait(false);
        if (current is null || IsLocked(current)) { return new(); }
        var setup = await enrollment.StartOrResumeAsync(challenge, current.User.Email, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (setup is null) { return new(); }
        if (!challenge.EnrollmentConfigurationId.HasValue)
        {
            var now = _time.GetUtcNow();
            var binding = new SignInMfaChallengeBinding(current.User.Id, challenge.TenantId, current.User.TokenVersion,
                challenge.PolicyFingerprint, SignInMfaPurpose.EnrollFactor);
            if (!await challenges.TryBindEnrollmentAsync(hash, binding, setup.ConfigurationId, setup.SecretFingerprint, now,
                cancellationToken).ConfigureAwait(false)) { return new(); }
            challenge.EnrollmentConfigurationId = setup.ConfigurationId;
            challenge.EnrollmentSecretFingerprint = setup.SecretFingerprint;
            challenge.EnrollmentInitializedAt = now;
        }
        current = await ReadBoundSubjectAsync(challenge, cancellationToken, enrolled: false).ConfigureAwait(false);
        if (current is null || IsLocked(current) || !await enrollment.MatchesAsync(challenge, false, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false)) { return new(); }
        await RecordAsync("Authentication.MfaSignInEnrollmentStarted", challenge, false, MfaMethod.Totp.ToString(), deviceInfo,
            null, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return new() { Success = true, SecretKey = setup.SecretKey, QrCodeUri = setup.QrCodeUri, ExpiresAt = setup.ExpiresAt };
    }

    public async Task<SignInResponse> CompleteCodeAsync(string bearer, string code, MfaMethod method,
        DeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(deviceInfo);
        if (method is not (MfaMethod.Totp or MfaMethod.BackupCode) || string.IsNullOrWhiteSpace(code) || code.Length > 64 ||
            !SignInMfaChallengeToken.TryHash(bearer, out var hash)) { return Denied(); }
        var challenge = await challenges.FindActiveAsync(hash, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (challenge is null) { return Denied(); }
        var completingEnrollment = challenge.Purpose == SignInMfaPurpose.EnrollFactor;
        if (completingEnrollment)
        {
            if (method != MfaMethod.Totp || challenge.EnrollmentConfigurationId is null ||
                !SignInMfaChallengeToken.IsDigest(challenge.EnrollmentSecretFingerprint ?? string.Empty)) { return Denied(); }
            await enrollment.AcquireSubjectLockAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
            challenge = await challenges.FindActiveAsync(hash, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (challenge is null || challenge.Purpose != SignInMfaPurpose.EnrollFactor ||
                !await enrollment.MatchesAsync(challenge, false, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false)) { return Denied(); }
        }
        else if (challenge.Purpose != SignInMfaPurpose.VerifyFactor) { return Denied(); }
        var current = await ReadBoundSubjectAsync(challenge, cancellationToken, enrolled: !completingEnrollment).ConfigureAwait(false);
        if (current is null || IsLocked(current)) { return Denied(); }

        var verification = await mfa.VerifyMfaAsync(challenge.SubjectId, code, method, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!verification.IsSuccess)
        {
            await RecordAsync("Authentication.MfaSignInDenied", challenge, false, method.ToString(), deviceInfo,
                null, cancellationToken).ConfigureAwait(false);
            // Keep durable failed-attempt accounting only after the trusted audit write succeeds.
            return new SignInMfaCommittedDenial();
        }
        if (verification.RequiresAdditionalVerification) { return Denied(); }

        // Provider verification can await I/O; refresh account/version/enrollment/policy after it.
        current = await ReadBoundSubjectAsync(challenge, cancellationToken).ConfigureAwait(false);
        if (current is null || IsLocked(current) || completingEnrollment &&
            !await enrollment.MatchesAsync(challenge, true, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false)) { return Denied(); }
        var binding = new SignInMfaChallengeBinding(current.User.Id, challenge.TenantId, current.User.TokenVersion,
            challenge.PolicyFingerprint, challenge.Purpose);
        if (!await challenges.TryConsumeAsync(hash, binding, method, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false))
        {
            return Denied();
        }
        var proof = SignInMfaProof.FromVerifiedChallenge(challenge, method, _time.GetUtcNow());
        var result = await IssueAsync(current.User, challenge.TenantId, deviceInfo, cancellationToken, proof).ConfigureAwait(false);
        if (completingEnrollment)
        {
            var backupCodes = await mfa.GenerateBackupCodesAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
            if (backupCodes is not { Length: > 0 } || backupCodes.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidOperationException("Enrollment recovery codes could not be generated.");
            }
            result.MfaEnrollmentBackupCodes = backupCodes;
        }
        await RecordAsync("Authentication.MfaSignInVerified", challenge, true, method.ToString(), deviceInfo,
            result.SessionId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private async Task<SignInMfaSubjectState?> ReadBoundSubjectAsync(SignInMfaChallenge challenge, CancellationToken cancellationToken, bool enrolled = true)
    {
        var current = await subjects.ReadCurrentAsync(challenge.SubjectId, cancellationToken).ConfigureAwait(false);
        if (!AccountMatches(current, challenge.SubjectId, challenge.SubjectTokenVersion) || current!.HasEnrolledMfa != enrolled) { return null; }
        var decision = await policy.EvaluateAsync(challenge.SubjectId, challenge.TenantId, cancellationToken).ConfigureAwait(false);
        RequireDecision(decision, challenge.SubjectId, challenge.TenantId);
        return string.Equals(decision.PolicyFingerprint, challenge.PolicyFingerprint, StringComparison.Ordinal) ? current : null;
    }

    public async Task<SignInMfaProof?> ReadSessionProofAsync(Guid subjectId, int tokenVersion, Guid tenantId, Guid sessionId,
        DateTimeOffset authenticatedAt, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = await subjects.ReadCurrentAsync(subjectId, cancellationToken).ConfigureAwait(false);
        RequireAccount(current, subjectId, tokenVersion);
        var decision = await policy.EvaluateAsync(subjectId, tenantId, cancellationToken).ConfigureAwait(false);
        RequireDecision(decision, subjectId, tenantId);
        var stored = await sessionEvidence.FindAsync(sessionId, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (stored is null)
        {
            if (decision.RequiresMfa || current!.HasEnrolledMfa)
            {
                throw new AuthenticationRequiredException("MFA verification is required before refreshing this session.");
            }
            return null;
        }
        if (!current!.HasEnrolledMfa || IsLocked(current) || stored.SessionId != sessionId ||
            !string.Equals(stored.PolicyFingerprint, decision.PolicyFingerprint, StringComparison.Ordinal))
        {
            throw new AuthenticationRequiredException("MFA verification evidence is no longer valid.");
        }
        var proof = stored.ToProof();
        proof.RequireBinding(subjectId, tenantId, tokenVersion, authenticatedAt, _time.GetUtcNow());
        return proof;
    }

    private async Task<SignInResponse> IssueAsync(User user, Guid tenantId, DeviceInfo device, CancellationToken cancellationToken,
        SignInMfaProof? proof = null)
    {
        var result = proof is null
            ? await issuer.IssueAsync(user, tenantId, device, cancellationToken).ConfigureAwait(false)
            : await issuer.IssueMfaAsync(user, tenantId, device, proof, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (result is null || !result.Success || result.RequiresMfa || result.RequiresStepUp || result.UserId != user.Id ||
            result.TenantId != tenantId || result.SessionId == Guid.Empty || string.IsNullOrWhiteSpace(result.AccessToken) ||
            string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            throw new InvalidOperationException("Authenticated credentials could not be issued for the verified binding.");
        }
        return result;
    }

    private Task RecordAsync(string action, SignInMfaChallenge challenge, bool success, string method,
        DeviceInfo device, Guid? sessionId, CancellationToken cancellationToken) =>
        audit.RecordAsync(new AuthenticationAuditEvent(action, challenge.SubjectId, success, method,
            device.IpAddress, device.UserAgent, sessionId, challenge.TenantId,
            Metadata: new { ChallengeId = challenge.Id, challenge.Purpose }), cancellationToken);

    private bool IsLocked(SignInMfaSubjectState current) => current.LockedOutUntil is { } until && until > _time.GetUtcNow().UtcDateTime;

    private static bool AccountMatches(SignInMfaSubjectState? current, Guid subjectId, int version) =>
        current is not null && current.User.Id == subjectId && version > 0 && !current.User.IsDeleted &&
        current.User.ValidateForAuthentication(version).IsSuccess;

    private static void RequireAccount(SignInMfaSubjectState? current, Guid subjectId, int version)
    {
        if (!AccountMatches(current, subjectId, version)) { throw new AuthenticationRequiredException("Verified first-factor evidence is no longer valid."); }
    }

    private static void RequireDecision(MfaRequirementDecision decision, Guid subjectId, Guid? requestedTenantId)
    {
        if (decision is null || decision.SubjectId != subjectId || decision.TenantId == Guid.Empty ||
            requestedTenantId.HasValue && decision.TenantId != requestedTenantId.Value ||
            !SignInMfaChallengeToken.IsDigest(decision.PolicyFingerprint))
        {
            throw new InvalidOperationException("Current subject policy could not establish an authorized tenant binding.");
        }
    }

    private static SignInResponse Denied() => new() { Success = false, Message = "Additional verification failed" };
}

/// <summary>Server-only transaction outcome; the bearer is not an ordinary access or refresh credential.</summary>
internal sealed class SignInMfaPendingResponse : SignInResponse, ICommitOnFailureOutcome
{
    public SignInMfaPendingResponse(string bearer, DateTimeOffset expiresAt, SignInMfaPurpose purpose, bool requiresRiskStepUp,
        User verifiedSubject, Guid tenantId)
    {
        ArgumentNullException.ThrowIfNull(verifiedSubject);
        if (verifiedSubject.Id == Guid.Empty || tenantId == Guid.Empty)
        {
            throw new ArgumentException("A pending MFA response requires a verified subject and resolved tenant.");
        }
        Success = false;
        Message = purpose == SignInMfaPurpose.EnrollFactor ? "MFA enrollment required" : "Additional verification required";
        RequiresMfa = true;
        MfaToken = bearer;
        RequiresStepUp = requiresRiskStepUp;
        StepUpToken = requiresRiskStepUp ? bearer : null;
        StepUpExpiresAt = expiresAt.UtcDateTime;
        AvailableMethods = purpose == SignInMfaPurpose.VerifyFactor ? ["TOTP", "BackupCode"] : ["TOTP"];
        UserId = verifiedSubject.Id;
        Email = verifiedSubject.Email;
        TenantId = tenantId;
        // Preserve the first-factor response contract without granting a session or disclosing the stored phone.
        User = AuthenticationMappings.CreateUserProfile(verifiedSubject, verifiedSubject.Id, verifiedSubject.Email,
            authenticationComplete: false, fallbackCreatedAt: verifiedSubject.CreatedAt);
    }
}

internal sealed class SignInMfaCommittedDenial : SignInResponse, ICommitOnFailureOutcome
{
    public SignInMfaCommittedDenial()
    {
        Success = false;
        Message = "Additional verification failed";
    }
}
