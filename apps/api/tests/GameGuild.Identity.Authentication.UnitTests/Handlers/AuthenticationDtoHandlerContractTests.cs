using FluentValidation;
using FluentValidation.Results;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public sealed class AuthenticationDtoHandlerContractTests
{
    [Theory]
    [InlineData("signup")]
    [InlineData("signin")]
    [InlineData("refresh")]
    [InlineData("polymorphic")]
    [InlineData("social")]
    [InlineData("google-id")]
    [InlineData("discord")]
    public async Task CurrentHandlersApplyProfileConversionAndKeepServerTokens(string flow)
    {
        using var cancellation = new CancellationTokenSource();
        var user = User.Create("handler@example.test", "Ana Maria Silva", "+15550001000");
        var source = new SignInResponse
        {
            Success = true, UserId = user.Id, Email = user.Email, SessionId = Guid.NewGuid(), TenantId = Guid.NewGuid(),
            AccessToken = "synthetic-access", RefreshToken = "synthetic-refresh", ExpiresIn = 600,
            ExpiresAt = new DateTime(2030, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            AccessTokenExpiresAt = new DateTime(2030, 1, 1, 0, 10, 0, DateTimeKind.Utc),
            RefreshTokenExpiresAt = new DateTime(2030, 1, 8, 0, 0, 0, DateTimeKind.Utc)
        };
        var repository = new Mock<IUserRepository>(MockBehavior.Strict);
        repository.Setup(repo => repo.GetByIdAsync(user.Id, cancellation.Token)).ReturnsAsync(user);
        if (flow == "polymorphic")
        {
            repository.Setup(repo => repo.FindSignInCandidatesAsync(user.Email, SignInIdentifierType.Email, cancellation.Token))
                .ReturnsAsync((IReadOnlyList<User>)[user]);
        }
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        var oauth = new Mock<IOAuthAuthService>(MockBehavior.Strict);

        service.Setup(auth => auth.LocalSignUpAsync(It.Is<LocalSignUpRequest>(request => request.Email == user.Email && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);
        service.Setup(auth => auth.LocalSignInAsync(It.Is<LocalSignInRequest>(request => request.Email == user.Email && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);
        service.Setup(auth => auth.RefreshTokenAsync(It.Is<RefreshTokenRequest>(request => request.RefreshToken == "synthetic-input-refresh" && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);
        service.Setup(auth => auth.GoogleSignInAsync(It.Is<OAuthSignInRequest>(request => request.AccessToken == "synthetic-input-oauth" && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);
        service.Setup(auth => auth.GoogleIdTokenSignInAsync(It.Is<GoogleIdTokenRequest>(request => request.IdToken == "synthetic-input-id" && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);
        oauth.Setup(auth => auth.DiscordSignInAsync(It.Is<DiscordSignInRequest>(request => request.Code == "synthetic-input-code" && request.TenantId == source.TenantId), cancellation.Token)).ReturnsAsync(source);

        var mapped = flow switch
        {
            "signup" => await new LocalSignUpHandler(service.Object, repository.Object, Mock.Of<GameGuild.CQRS.ISender>(), NullLogger<LocalSignUpHandler>.Instance)
                .Handle(new LocalSignUpCommand { Email = user.Email, Username = "handler", TenantId = source.TenantId }, cancellation.Token),
            "signin" => await new LocalSignInHandler(service.Object, repository.Object, new HttpContextAccessor(), NullLogger<LocalSignInHandler>.Instance, Validator<LocalSignInCommand>())
                .Handle(new LocalSignInCommand { Email = user.Email, TenantId = source.TenantId }, cancellation.Token),
            "refresh" => await new RefreshTokenHandler(service.Object, repository.Object, NullLogger<RefreshTokenHandler>.Instance, Validator<RefreshTokenCommand>())
                .Handle(new RefreshTokenCommand { RefreshToken = "synthetic-input-refresh", TenantId = source.TenantId }, cancellation.Token),
            "polymorphic" => await new PolymorphicSignInHandler(service.Object, repository.Object, NullLogger<PolymorphicSignInHandler>.Instance, Validator<PolymorphicSignInCommand>())
                .Handle(new PolymorphicSignInCommand { Credential = user.Email, TenantId = source.TenantId }, cancellation.Token),
            "social" => await new SocialSignInHandler(service.Object, repository.Object, NullLogger<SocialSignInHandler>.Instance, Validator<SocialSignInCommand>())
                .Handle(new SocialSignInCommand { Provider = SocialProvider.Google, Token = "synthetic-input-oauth", TenantId = source.TenantId }, cancellation.Token),
            "google-id" => await new GoogleIdTokenSignInHandler(service.Object, repository.Object, NullLogger<GoogleIdTokenSignInHandler>.Instance, Validator<GoogleIdTokenSignInCommand>())
                .Handle(new GoogleIdTokenSignInCommand { IdToken = "synthetic-input-id", TenantId = source.TenantId }, cancellation.Token),
            "discord" => await new DiscordCallbackCommandHandler(oauth.Object, repository.Object, NullLogger<DiscordCallbackCommandHandler>.Instance, Validator<DiscordCallbackCommand>())
                .Handle(new DiscordCallbackCommand { Code = "synthetic-input-code", TenantId = source.TenantId }, cancellation.Token),
            _ => throw new InvalidOperationException("Unknown handler contract case.")
        };

        Assert.Equal(user.Id, mapped.User.Id);
        Assert.Equal("Ana", mapped.User.FirstName);
        Assert.Equal("Maria Silva", mapped.User.LastName);
        Assert.Equal(user.PhoneNumber, mapped.User.PhoneNumber);
        Assert.Equal(source.UserId, mapped.UserId);
        Assert.Equal(source.TenantId, mapped.TenantId);
        Assert.Equal(source.SessionId, mapped.SessionId);
        Assert.Equal(source.AccessToken, mapped.AccessToken);
        Assert.Equal(source.RefreshToken, mapped.RefreshToken);
        Assert.Equal(source.ExpiresIn, mapped.ExpiresIn);
        Assert.Equal(source.ExpiresAt, mapped.ExpiresAt);
        Assert.Equal(source.AccessTokenExpiresAt, mapped.AccessTokenExpiresAt);
        Assert.Equal(source.RefreshTokenExpiresAt, mapped.RefreshTokenExpiresAt);
        repository.Verify(repo => repo.GetByIdAsync(user.Id, cancellation.Token), Times.Once);
        if (flow == "polymorphic")
        {
            repository.Verify(repo => repo.FindSignInCandidatesAsync(user.Email, SignInIdentifierType.Email, cancellation.Token), Times.Once);
        }
        repository.VerifyNoOtherCalls();
        Assert.Equal("Ana Maria Silva", user.Name);
    }

    private static IValidator<T> Validator<T>()
    {
        var validator = new Mock<IValidator<T>>();
        validator.Setup(value => value.ValidateAsync(It.IsAny<T>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ValidationResult());
        return validator.Object;
    }
}
