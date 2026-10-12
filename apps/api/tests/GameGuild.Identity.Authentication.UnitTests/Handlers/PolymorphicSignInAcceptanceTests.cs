using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class PolymorphicSignInAcceptanceTests
{
    [Theory]
    [InlineData(CredentialType.Email, false)]
    [InlineData(CredentialType.Email, true)]
    [InlineData(CredentialType.Username, false)]
    [InlineData(CredentialType.Username, true)]
    [InlineData(CredentialType.Phone, false)]
    [InlineData(CredentialType.Phone, true)]
    public async Task UniqueResolutionForwardsCanonicalAccountPasswordTenantDeviceAndCancellation(CredentialType type, bool explicitType)
    {
        using var cancellation = new CancellationTokenSource();
        var user = Account();
        var identifier = Identifier(user, type);
        var lookup = Lookup(type);
        var tenant = Guid.NewGuid();
        var fingerprint = Guid.NewGuid().ToString("N");
        var password = SyntheticPassword();
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(value => value.FindSignInCandidatesAsync(identifier, lookup, cancellation.Token)).ReturnsAsync((IReadOnlyList<User>)[user]);
        repository.Setup(value => value.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(user);
        var source = new SignInResponse { Success = true, UserId = user.Id, Email = user.Email, TenantId = tenant, AccessToken = "synthetic-access", SessionId = Guid.NewGuid() };
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service.Setup(value => value.LocalSignInAsync(It.Is<LocalSignInRequest>(request => request.Email == user.Email && request.Password == password && request.TenantId == tenant && request.DeviceFingerprint == fingerprint), cancellation.Token)).ReturnsAsync(source);
        var result = await Handler(service, repository).Handle(new PolymorphicSignInCommand
        {
            Credential = " " + identifier + " ", CredentialType = explicitType ? type : null,
            Password = password, TenantId = tenant, DeviceFingerprint = fingerprint
        }, cancellation.Token);
        Assert.True(result.Success);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(source.SessionId, result.SessionId);
        Assert.Equal(source.AccessToken, result.AccessToken);
        Assert.Equal(user.PhoneNumber, result.User.PhoneNumber);
        repository.VerifyAll();
        service.VerifyAll();
    }

    [Theory]
    [InlineData("", CredentialType.Email)]
    [InlineData("bad-email", CredentialType.Email)]
    [InlineData("a\n@b.test", CredentialType.Email)]
    [InlineData("user name", CredentialType.Username)]
    [InlineData("user@name", CredentialType.Username)]
    [InlineData("1234567890", CredentialType.Phone)]
    [InlineData("+0123456", CredentialType.Phone)]
    [InlineData("+1 5551234567", CredentialType.Phone)]
    [InlineData("+1234567890123456", CredentialType.Phone)]
    [InlineData("+١٢٣٤٥٦٧٨٩", CredentialType.Phone)]
    [InlineData("wallet", CredentialType.WalletAddress)]
    [InlineData("user", (CredentialType)999)]
    public async Task InvalidOrUnsupportedIdentifierRunsTheExistingGenericDenialWithoutLookup(string identifier, CredentialType type)
    {
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var service = GenericDenial();
        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(service, repository).Handle(new PolymorphicSignInCommand
        {
            Credential = identifier, CredentialType = type, Password = SyntheticPassword()
        }, CancellationToken.None));
        Assert.Equal("Synthetic generic authentication failure", exception.Message);
        repository.VerifyNoOtherCalls();
        service.Verify(value => value.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(CredentialType.Email, false)]
    [InlineData(CredentialType.Email, true)]
    [InlineData(CredentialType.Username, false)]
    [InlineData(CredentialType.Username, true)]
    [InlineData(CredentialType.Phone, false)]
    [InlineData(CredentialType.Phone, true)]
    public async Task MissingAndAmbiguousAccountsRunTheSameLocalDenial(CredentialType type, bool ambiguous)
    {
        var user = Account();
        var identifier = Identifier(user, type);
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(value => value.FindSignInCandidatesAsync(identifier, Lookup(type), CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<User>)(ambiguous ? new[] { user, Account() } : Array.Empty<User>()));
        var service = GenericDenial();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Handler(service, repository).Handle(new PolymorphicSignInCommand
        {
            Credential = identifier, Password = SyntheticPassword(), CredentialType = type
        }, CancellationToken.None));
        repository.VerifyAll();
        service.Verify(value => value.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task CancellationBeforeResolutionDoesNotInvokeRepositoriesOrAuthentication()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        await Assert.ThrowsAsync<OperationCanceledException>(() => Handler(service, repository).Handle(new PolymorphicSignInCommand(), cancellation.Token));
        repository.VerifyNoOtherCalls();
        service.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task ControllerForwardsTheCompleteCredentialCommandAndReturnsTheCommonContract(CredentialType type)
    {
        using var cancellation = new CancellationTokenSource();
        var body = new PolymorphicSignInRequest { Credential = Identifier(Account(), type), CredentialType = type, Password = SyntheticPassword(), TenantId = Guid.NewGuid(), DeviceFingerprint = Guid.NewGuid().ToString("N") };
        var response = new SignInResponse { Success = true };
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.Is<PolymorphicSignInCommand>(command => command.Credential == body.Credential && command.CredentialType == type && command.Password == body.Password && command.TenantId == body.TenantId && command.DeviceFingerprint == body.DeviceFingerprint), cancellation.Token)).ReturnsAsync(response);
        var result = Assert.IsType<OkObjectResult>(await new AuthController(sender.Object).PolymorphicSignIn(body, cancellation.Token));
        Assert.Same(response, result.Value);
        sender.VerifyAll();
    }

    [Fact]
    public async Task ControllerUsesTheSameUnauthorizedProblemContract()
    {
        var sender = new Mock<ISender>(MockBehavior.Strict);
        sender.Setup(value => value.Send(It.IsAny<PolymorphicSignInCommand>(), CancellationToken.None)).ThrowsAsync(new UnauthorizedAccessException("Synthetic generic authentication failure"));
        var result = Assert.IsType<UnauthorizedObjectResult>(await new AuthController(sender.Object).PolymorphicSignIn(new PolymorphicSignInRequest(), CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(401, problem.Status);
        Assert.Equal("Synthetic generic authentication failure", problem.Detail);
    }

    [Fact]
    public void ServerAccountResolutionIsNotAJsonContract()
    {
        var json = JsonSerializer.Serialize(new LocalSignInRequest { Email = "synthetic@example.test" });
        Assert.DoesNotContain("ResolvedUserId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CredentialResolutionFailed", json, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task AdmissionReceivesCanonicalAccountOriginAndCancellationAndHoldsLeaseThroughAuthentication(CredentialType type)
    {
        using var cancellation = new CancellationTokenSource();
        var account = Account();
        var identifier = Identifier(account, type);
        var admission = new PasswordSignInAdmissionStub();
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(value => value.FindSignInCandidatesAsync(identifier, Lookup(type), cancellation.Token))
            .ReturnsAsync((IReadOnlyList<User>)[account]);
        repository.Setup(value => value.GetByIdAsync(account.Id, cancellation.Token))
            .Callback(() => Assert.False(admission.LeaseDisposed))
            .ReturnsAsync(account);
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        auth.Setup(value => value.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), cancellation.Token))
            .Callback((LocalSignInRequest request, CancellationToken _) =>
            {
                Assert.Equal(account.Email, Assert.Single(admission.Identifiers));
                Assert.Same(request.TimingOrigin, admission.Origin);
                Assert.False(admission.LeaseDisposed);
            })
            .ReturnsAsync(new SignInResponse { Success = true, UserId = account.Id });
        var handler = new PolymorphicSignInHandler(auth.Object, repository.Object,
            NullLogger<PolymorphicSignInHandler>.Instance, admission);

        var result = await handler.Handle(new PolymorphicSignInCommand
        {
            Credential = identifier, CredentialType = type, Password = SyntheticPassword()
        }, cancellation.Token);

        Assert.True(result.Success);
        Assert.True(admission.LeaseDisposed);
        Assert.Equal(cancellation.Token, admission.Cancellation);
        repository.VerifyAll();
        auth.VerifyAll();
    }

    [Theory]
    [InlineData(CredentialType.Email)]
    [InlineData(CredentialType.Username)]
    [InlineData(CredentialType.Phone)]
    public async Task DeniedAdmissionCannotInvokePasswordAuthenticationOrIssueAResponse(CredentialType type)
    {
        var account = Account();
        var identifier = Identifier(account, type);
        var denial = new UnauthorizedAccessException("Synthetic generic authentication failure");
        var admission = new PasswordSignInAdmissionStub { Denial = denial };
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(value => value.FindSignInCandidatesAsync(identifier, Lookup(type), CancellationToken.None))
            .ReturnsAsync((IReadOnlyList<User>)[account]);
        var auth = new Mock<IAuthService>(MockBehavior.Strict);
        var handler = new PolymorphicSignInHandler(auth.Object, repository.Object,
            NullLogger<PolymorphicSignInHandler>.Instance, admission);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new PolymorphicSignInCommand { Credential = identifier, CredentialType = type, Password = SyntheticPassword() },
            CancellationToken.None));

        Assert.Same(denial, exception);
        Assert.Equal(account.Email, Assert.Single(admission.Identifiers));
        auth.VerifyNoOtherCalls();
        repository.VerifyAll();
    }

    [Fact]
    public async Task FailedPasswordAuthenticationReleasesTheAdmissionLease()
    {
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        var admission = new PasswordSignInAdmissionStub();
        var auth = GenericDenial();
        var handler = new PolymorphicSignInHandler(auth.Object, repository.Object,
            NullLogger<PolymorphicSignInHandler>.Instance, admission);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => handler.Handle(
            new PolymorphicSignInCommand { Credential = "", CredentialType = CredentialType.Email, Password = SyntheticPassword() },
            CancellationToken.None));
        Assert.True(admission.LeaseDisposed);
        Assert.Equal("", Assert.Single(admission.Identifiers));
        repository.VerifyNoOtherCalls();
        auth.VerifyAll();
    }

    private static PolymorphicSignInHandler Handler(Mock<IAuthService> service, Mock<IUserRepository> repository) => new(service.Object, repository.Object, NullLogger<PolymorphicSignInHandler>.Instance, new PasswordSignInAdmissionStub());
    private static Mock<IAuthService> GenericDenial()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service.Setup(value => value.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new UnauthorizedAccessException("Synthetic generic authentication failure"));
        return service;
    }
    private static User Account() => new() { Id = Guid.NewGuid(), Email = $"poly-{Guid.NewGuid():N}@example.test", Username = $"poly-{Guid.NewGuid():N}", Name = "Synthetic account", PhoneNumber = "+15551234567" };
    private static string Identifier(User user, CredentialType type) => type switch { CredentialType.Email => user.Email, CredentialType.Phone => user.PhoneNumber!, _ => user.Username! };
    private static SignInIdentifierType Lookup(CredentialType type) => type switch { CredentialType.Email => SignInIdentifierType.Email, CredentialType.Phone => SignInIdentifierType.Phone, _ => SignInIdentifierType.Username };
    private static string SyntheticPassword() => "aA7!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(20));
}
