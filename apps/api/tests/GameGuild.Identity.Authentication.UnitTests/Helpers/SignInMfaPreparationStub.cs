using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Moq;

// Existing password-service tests isolate the mandatory preparation dependency. Actual policy,
// persistence and transaction behavior require the native SQL and HTTP integration cases.
internal static class SignInMfaPreparationStub
{
    internal static ISignInMfaService Create() => CreateMock().Object;

    internal static Mock<ISignInMfaService> CreateMock()
    {
        var port = new Mock<ISignInMfaService>(MockBehavior.Strict);
        port.Setup(service => service.PrepareAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid?>(),
                It.IsAny<DeviceInfo>(), It.IsAny<SignInFirstFactor>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Returns((Guid subjectId, int tokenVersion, Guid? tenantId, DeviceInfo _, SignInFirstFactor _, bool risk, CancellationToken cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(risk
                    ? SignInMfaPreparation.WithOutcome(new SignInMfaPendingResponse(SignInMfaChallengeToken.Create(),
                        DateTimeOffset.UtcNow.AddMinutes(5), SignInMfaPurpose.VerifyFactor, true,
                        new User { Id = subjectId, TokenVersion = tokenVersion }, tenantId ?? Guid.NewGuid()))
                    : SignInMfaPreparation.PermitWithoutMfa(new User { Id = subjectId, TokenVersion = tokenVersion },
                        new MfaRequirementDecision(subjectId, tenantId ?? Guid.NewGuid(), false, [], new string('0', 64))));
            });
        port.Setup(service => service.ReadSessionProofAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<Guid>(),
                It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Returns((Guid _, int _, Guid _, Guid _, DateTimeOffset _, CancellationToken cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<SignInMfaProof?>(null);
            });
        return port;
    }
}
