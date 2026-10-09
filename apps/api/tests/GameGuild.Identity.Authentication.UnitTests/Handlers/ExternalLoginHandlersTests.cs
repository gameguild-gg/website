using System.Threading;
using FluentAssertions;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Handlers;

public class ExternalLoginHandlersTests
{
    private readonly Mock<IExternalLoginRepository> _externalLoginRepoMock = new();
    private readonly Mock<IGoogleIdTokenVerifier> _googleVerifierMock = new();
    private readonly Mock<IOAuthService> _oauthServiceMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();

    public ExternalLoginHandlersTests()
    {
        _googleVerifierMock
            .Setup(x => x.VerifyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VerifiedGoogleUser
            {
                Sub = "google-sub-1",
                Email = "user@example.com",
                EmailVerified = true,
                Name = "Test User"
            });

        // Server-resolved authorization scopes (issue #250): Google and Discord defaults.
        _oauthServiceMock
            .Setup(x => x.ResolveAuthorizationScopes("google", It.IsAny<string[]?>()))
            .Returns(new[] { "openid", "email", "profile" });
        _oauthServiceMock
            .Setup(x => x.ResolveAuthorizationScopes("google"))
            .Returns(new[] { "openid", "email", "profile" });
        _oauthServiceMock
            .Setup(x => x.ResolveAuthorizationScopes("discord"))
            .Returns(new[] { "identify", "email" });

        _externalLoginRepoMock
            .Setup(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin dto, CancellationToken _) => dto);

        _userRepoMock
            .Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.CreateWithPassword("user@example.com", "User", "password-hash"));
    }

    // ── List ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetExternalLogins_ReturnsDtos_InRepositoryOrder_NewestFirst()
    {
        var userId = Guid.NewGuid();
        var older = new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1", CreatedAt = DateTime.UtcNow.AddHours(-2) };
        var newer = new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "snow-1", CreatedAt = DateTime.UtcNow };
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([newer, older]);

        var result = await new GetExternalLoginsQueryHandler(_externalLoginRepoMock.Object)
            .Handle(new GetExternalLoginsQuery { UserId = userId }, CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].Provider.Should().Be("discord");
        result[0].CreatedAt.Should().Be(newer.CreatedAt);
        result[1].Provider.Should().Be("google");
        result[1].CreatedAt.Should().Be(older.CreatedAt);
    }

    // ── Google link ─────────────────────────────────────────────────────

    [Fact]
    public async Task LinkGoogle_ValidToken_NoExistingLink_WritesRow()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);

        var handler = new LinkGoogleAccountCommandHandler(_googleVerifierMock.Object, _externalLoginRepoMock.Object, _oauthServiceMock.Object, NullLogger<LinkGoogleAccountCommandHandler>.Instance);
        await handler.Handle(new LinkGoogleAccountCommand { UserId = userId, IdToken = "valid-id-token" }, CancellationToken.None);

        _googleVerifierMock.Verify(x => x.VerifyAsync("valid-id-token", It.IsAny<CancellationToken>()), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.AddAsync(
                It.Is<ExternalLogin>(e => e.UserId == userId && e.Provider == "google" && e.ProviderKey == "google-sub-1"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkGoogle_AlreadyLinkedToSameUser_IsIdempotent_NoSecondWrite()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "google-sub-1" });

        var handler = new LinkGoogleAccountCommandHandler(_googleVerifierMock.Object, _externalLoginRepoMock.Object, _oauthServiceMock.Object, NullLogger<LinkGoogleAccountCommandHandler>.Instance);
        await handler.Handle(new LinkGoogleAccountCommand { UserId = userId, IdToken = "valid-id-token" }, CancellationToken.None);

        _externalLoginRepoMock.Verify(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkGoogle_LinkedToAnotherUser_ThrowsConflict_NeverUpserts()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { UserId = Guid.NewGuid(), Provider = "google", ProviderKey = "google-sub-1" });

        var handler = new LinkGoogleAccountCommandHandler(_googleVerifierMock.Object, _externalLoginRepoMock.Object, _oauthServiceMock.Object, NullLogger<LinkGoogleAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkGoogleAccountCommand { UserId = userId, IdToken = "valid-id-token" }, CancellationToken.None);

        await act.Should().ThrowAsync<ExternalLoginConflictException>()
            .WithMessage("Social account already linked to another user");
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkGoogle_InvalidToken_ThrowsUnauthorized_NoRepoAccess()
    {
        _googleVerifierMock
            .Setup(x => x.VerifyAsync("forged-token", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Google ID token is invalid"));

        var handler = new LinkGoogleAccountCommandHandler(_googleVerifierMock.Object, _externalLoginRepoMock.Object, _oauthServiceMock.Object, NullLogger<LinkGoogleAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkGoogleAccountCommand { UserId = Guid.NewGuid(), IdToken = "forged-token" }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>().WithMessage("Google ID token is invalid");
        _externalLoginRepoMock.Verify(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Discord link-authorize ──────────────────────────────────────────

    [Fact]
    public async Task DiscordLinkAuthorize_ReturnsAuthUrl_AndPerRequestState()
    {
        string? capturedState = null;
        _oauthServiceMock
            .Setup(x => x.GetAuthorizationUrlAsync("discord", "https://app/callback", It.IsAny<string>(), null))
            .Callback((string _, string _, string state, string[]? _) => capturedState = state)
            .ReturnsAsync("https://discord.com/oauth2/authorize?client_id=abc");

        var handler = new DiscordLinkAuthorizeCommandHandler(_oauthServiceMock.Object, NullLogger<DiscordLinkAuthorizeCommandHandler>.Instance);
        var result = await handler.Handle(
            new DiscordLinkAuthorizeCommand { RedirectUri = "https://app/callback" }, CancellationToken.None);

        result.AuthUrl.Should().Be("https://discord.com/oauth2/authorize?client_id=abc");
        result.State.Should().MatchRegex("^[0-9a-f]{32}$");
        result.State.Should().Be(capturedState);
    }

    // ── Discord link-callback ───────────────────────────────────────────

    [Fact]
    public async Task LinkDiscord_CallbackExchangesCodeForProfile_AndWritesRow()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", "auth-code", "state-1", "https://app/callback"))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord", Email = "d@example.com", EmailVerified = true });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _externalLoginRepoMock.Verify(
            x => x.AddAsync(
                It.Is<ExternalLogin>(e => e.UserId == userId && e.Provider == "discord" && e.ProviderKey == "2516582401"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    ///     F2 defect pin (identity-transfer race): when the pre-check misses but another user's
    ///     row commits concurrently, the link MUST go through the insert-only path. The old code
    ///     called UpsertAsync here — its internal read found the winner and silently reassigned
    ///     ownership via the update branch. Insert-only AddAsync cannot reassign: it either
    ///     inserts or throws DbUpdateException on the unique index.
    /// </summary>
    [Fact]
    public async Task LinkDiscord_PreCheckMiss_InsertOnlyPath_NeverCallsUpsert()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _externalLoginRepoMock.Verify(
            x => x.AddAsync(
                It.Is<ExternalLogin>(e => e.UserId == userId && e.Provider == "discord" && e.ProviderKey == "2516582401"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkDiscord_Race_DbUpdateExceptionFromAddThenForeignRow_ThrowsConflict()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .SetupSequence(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null)
            .ReturnsAsync(new ExternalLogin { UserId = Guid.NewGuid(), Provider = "discord", ProviderKey = "2516582401" });
        _externalLoginRepoMock
            .Setup(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key value violates unique constraint"));

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ExternalLoginConflictException>()
            .WithMessage("Social account already linked to another user");
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkDiscord_Race_DbUpdateExceptionFromAddThenSameUserRow_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .SetupSequence(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null)
            .ReturnsAsync(new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "2516582401" });
        _externalLoginRepoMock
            .Setup(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key value violates unique constraint"));

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        await act.Should().NotThrowAsync();
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkDiscord_Race_DbUpdateExceptionFromAddThenRefetchNull_Rethrows()
    {
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .SetupSequence(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null)
            .ReturnsAsync((ExternalLogin?)null);
        _externalLoginRepoMock
            .Setup(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("duplicate key value violates unique constraint"));

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = Guid.NewGuid(),
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task LinkDiscord_PreCheckForeignOwner_ThrowsConflict_NeverUpserts()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { UserId = Guid.NewGuid(), Provider = "discord", ProviderKey = "2516582401" });

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        var act = () => handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ExternalLoginConflictException>();
        _externalLoginRepoMock.Verify(x => x.UpsertAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Unlink ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Unlink_LinkedWithPasswordAndSecondProvider_DeletesRow()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "snow-1" },
                new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" }
            ]);
        _externalLoginRepoMock
            .Setup(x => x.DeleteAsync("discord", userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new UnlinkExternalLoginCommandHandler(_externalLoginRepoMock.Object, _userRepoMock.Object);
        await handler.Handle(new UnlinkExternalLoginCommand { UserId = userId, Provider = "discord" }, CancellationToken.None);

        _externalLoginRepoMock.Verify(x => x.DeleteAsync("discord", userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unlink_LastMethodWithNoPassword_ThrowsGuard_NeverDeletes()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" }]);
        _userRepoMock
            .Setup(x => x.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(User.CreateOAuthUser("user@example.com", "OAuth User"));

        var handler = new UnlinkExternalLoginCommandHandler(_externalLoginRepoMock.Object, _userRepoMock.Object);
        var act = () => handler.Handle(new UnlinkExternalLoginCommand { UserId = userId, Provider = "google" }, CancellationToken.None);

        await act.Should().ThrowAsync<LastSignInMethodException>()
            .WithMessage("Cannot remove the last sign-in method");
        _externalLoginRepoMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Unlink_LastMethodButPasswordSet_DeletesRow()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1" }]);

        var handler = new UnlinkExternalLoginCommandHandler(_externalLoginRepoMock.Object, _userRepoMock.Object);
        await handler.Handle(new UnlinkExternalLoginCommand { UserId = userId, Provider = "google" }, CancellationToken.None);

        _externalLoginRepoMock.Verify(x => x.DeleteAsync("google", userId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unlink_NotLinked_ThrowsNotFound_NeverDeletes()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new UnlinkExternalLoginCommandHandler(_externalLoginRepoMock.Object, _userRepoMock.Object);
        var act = () => handler.Handle(new UnlinkExternalLoginCommand { UserId = userId, Provider = "google" }, CancellationToken.None);

        await act.Should().ThrowAsync<ExternalLoginNotFoundException>();
        _externalLoginRepoMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _userRepoMock.Verify(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Scope recording (issue #250) ────────────────────────────────────

    [Fact]
    public async Task LinkGoogle_NewLink_RecordsResolvedScopesWithConsentStamp()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("google", "google-sub-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);

        var handler = new LinkGoogleAccountCommandHandler(_googleVerifierMock.Object, _externalLoginRepoMock.Object, _oauthServiceMock.Object, NullLogger<LinkGoogleAccountCommandHandler>.Instance);
        await handler.Handle(new LinkGoogleAccountCommand { UserId = userId, IdToken = "valid-id-token" }, CancellationToken.None);

        _oauthServiceMock.Verify(x => x.ResolveAuthorizationScopes("google"), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.AddAsync(
                It.Is<ExternalLogin>(e =>
                    e.UserId == userId &&
                    ExternalLoginGrants.Deserialize(e.GrantedScopes).SequenceEqual(new[] { "openid", "email", "profile" }) &&
                    e.ConsentedAt != null &&
                    e.ConsentVersion == OAuthConsentVersions.Current),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LinkDiscord_Callback_RecordsResolvedScopesWithConsentStamp()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", "auth-code", "state-1", "https://app/callback"))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord", Email = "d@example.com", EmailVerified = true });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalLogin?)null);

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _oauthServiceMock.Verify(x => x.ResolveAuthorizationScopes("discord"), Times.Once);
        _externalLoginRepoMock.Verify(
            x => x.AddAsync(
                It.Is<ExternalLogin>(e =>
                    ExternalLoginGrants.Deserialize(e.GrantedScopes).SequenceEqual(new[] { "identify", "email" }) &&
                    e.ConsentedAt != null &&
                    e.ConsentVersion == OAuthConsentVersions.Current),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LinkDiscord_AlreadyLinkedToSameUser_SameScopeSet_KeepsFirstConsentStamp()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin
            {
                UserId = userId,
                Provider = "discord",
                ProviderKey = "2516582401",
                GrantedScopes = ExternalLoginGrants.Serialize(new[] { "identify", "email" }),
                ConsentedAt = DateTime.UtcNow.AddDays(-7),
                ConsentVersion = OAuthConsentVersions.Current
            });

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _externalLoginRepoMock.Verify(x => x.AddAsync(It.IsAny<ExternalLogin>(), It.IsAny<CancellationToken>()), Times.Never);
        _externalLoginRepoMock.Verify(x => x.RecordConsentAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LinkDiscord_AlreadyLinkedToSameUser_ChangedScopeSet_ReRecordsConsent()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin
            {
                UserId = userId,
                Provider = "discord",
                ProviderKey = "2516582401",
                GrantedScopes = ExternalLoginGrants.Serialize(new[] { "identify" }),
                ConsentedAt = DateTime.UtcNow.AddDays(-7),
                ConsentVersion = OAuthConsentVersions.Current
            });

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _externalLoginRepoMock.Verify(
            x => x.RecordConsentAsync("discord", userId, It.Is<IReadOnlyList<string>>(s => s.SequenceEqual(new[] { "identify", "email" })), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task LinkDiscord_LegacyRowWithoutConsent_BackfillsConsentOnRelink()
    {
        var userId = Guid.NewGuid();
        _oauthServiceMock
            .Setup(x => x.HandleCallbackAsync("discord", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(new OAuthUserProfile { ProviderId = "2516582401", Provider = "Discord" });
        _externalLoginRepoMock
            .Setup(x => x.GetByProviderKeyAsync("discord", "2516582401", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "2516582401" });

        var handler = new LinkDiscordAccountCommandHandler(_oauthServiceMock.Object, _externalLoginRepoMock.Object, NullLogger<LinkDiscordAccountCommandHandler>.Instance);
        await handler.Handle(new LinkDiscordAccountCommand
        {
            UserId = userId,
            Code = "auth-code",
            State = "state-1",
            RedirectUri = "https://app/callback"
        }, CancellationToken.None);

        _externalLoginRepoMock.Verify(
            x => x.RecordConsentAsync("discord", userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── List shape (issue #250) ─────────────────────────────────────────

    [Fact]
    public async Task GetExternalLogins_MapsGrantFields_AllDefaultsForLegacyRows()
    {
        var userId = Guid.NewGuid();
        var consentedAt = DateTime.UtcNow.AddDays(-3);
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ExternalLogin
                {
                    UserId = userId,
                    Provider = "google",
                    ProviderKey = "sub-1",
                    CreatedAt = DateTime.UtcNow,
                    GrantedScopes = ExternalLoginGrants.Serialize(new[] { "openid", "email", "profile" }),
                    ConsentedAt = consentedAt,
                    ConsentVersion = OAuthConsentVersions.Current
                },
                new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "snow-1", CreatedAt = DateTime.UtcNow.AddHours(-1) }
            ]);

        var result = await new GetExternalLoginsQueryHandler(_externalLoginRepoMock.Object)
            .Handle(new GetExternalLoginsQuery { UserId = userId }, CancellationToken.None);

        var google = result.Single(r => r.Provider == "google");
        google.GrantedScopes.Should().Equal("openid", "email", "profile");
        google.ConsentedAt.Should().Be(consentedAt);
        google.ConsentVersion.Should().Be(OAuthConsentVersions.Current);

        var discord = result.Single(r => r.Provider == "discord");
        discord.GrantedScopes.Should().BeEmpty();
        discord.ConsentedAt.Should().BeNull();
        discord.ConsentVersion.Should().Be(0);
    }

    // ── Link preview (issue #250 consent screen) ────────────────────────

    [Fact]
    public async Task LinkPreview_ReturnsResolvedScopes_ForSupportedProvider()
    {
        var handler = new GetExternalLoginLinkPreviewQueryHandler(_oauthServiceMock.Object);
        var result = await handler.Handle(new GetExternalLoginLinkPreviewQuery { Provider = "Discord" }, CancellationToken.None);

        result.Provider.Should().Be("discord");
        result.RequestedScopes.Should().Equal("identify", "email");
    }

    [Fact]
    public async Task LinkPreview_UnsupportedProvider_Throws()
    {
        var handler = new GetExternalLoginLinkPreviewQueryHandler(_oauthServiceMock.Object);
        var act = () => handler.Handle(new GetExternalLoginLinkPreviewQuery { Provider = "steam" }, CancellationToken.None);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // ── Per-scope revocation (issue #250) ────────────────────────────────

    private RevokeExternalLoginScopesCommandHandler CreateRevokeHandler() =>
        new(_externalLoginRepoMock.Object, NullLogger<RevokeExternalLoginScopesCommandHandler>.Instance);

    [Fact]
    public async Task RevokeScopes_RemovesOnlyRequestedScopes_AndReturnsRemainingState()
    {
        var userId = Guid.NewGuid();
        var consentedAt = DateTime.UtcNow.AddDays(-5);
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExternalLogin
                {
                    UserId = userId,
                    Provider = "google",
                    ProviderKey = "sub-1",
                    GrantedScopes = ExternalLoginGrants.Serialize(new[] { "openid", "email", "profile" }),
                    ConsentedAt = consentedAt,
                    ConsentVersion = OAuthConsentVersions.Current
                }
            ]);
        _externalLoginRepoMock
            .Setup(x => x.UpdateGrantedScopesAsync("google", userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid _, IReadOnlyList<string> scopes, CancellationToken _) =>
                new ExternalLogin { UserId = userId, Provider = "google", ProviderKey = "sub-1", GrantedScopes = ExternalLoginGrants.Serialize(scopes), ConsentedAt = consentedAt });

        var result = await CreateRevokeHandler().Handle(
            new RevokeExternalLoginScopesCommand { UserId = userId, Provider = "google", Scopes = ["email"] },
            CancellationToken.None);

        result.Provider.Should().Be("google");
        result.GrantedScopes.Should().Equal("openid", "profile");
        result.ConsentedAt.Should().Be(consentedAt);
        result.ConsentVersion.Should().Be(OAuthConsentVersions.Current);
        _externalLoginRepoMock.Verify(
            x => x.UpdateGrantedScopesAsync("google", userId, It.Is<IReadOnlyList<string>>(s => s.SequenceEqual(new[] { "openid", "profile" })), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RevokeScopes_UnknownScope_IsIdempotent_NoWrite()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExternalLogin
                {
                    UserId = userId,
                    Provider = "discord",
                    ProviderKey = "snow-1",
                    GrantedScopes = ExternalLoginGrants.Serialize(new[] { "identify", "email" }),
                    ConsentedAt = DateTime.UtcNow
                }
            ]);

        var result = await CreateRevokeHandler().Handle(
            new RevokeExternalLoginScopesCommand { UserId = userId, Provider = "discord", Scopes = ["guilds"] },
            CancellationToken.None);

        result.GrantedScopes.Should().Equal("identify", "email");
        _externalLoginRepoMock.Verify(x => x.UpdateGrantedScopesAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RevokeScopes_AllGrantedScopes_LeavesLinkWithEmptyGrantList()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new ExternalLogin
                {
                    UserId = userId,
                    Provider = "discord",
                    ProviderKey = "snow-1",
                    GrantedScopes = ExternalLoginGrants.Serialize(new[] { "identify", "email" }),
                    ConsentedAt = DateTime.UtcNow
                }
            ]);
        _externalLoginRepoMock
            .Setup(x => x.UpdateGrantedScopesAsync("discord", userId, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, Guid _, IReadOnlyList<string> scopes, CancellationToken _) =>
                new ExternalLogin { UserId = userId, Provider = "discord", ProviderKey = "snow-1", GrantedScopes = ExternalLoginGrants.Serialize(scopes) });

        var result = await CreateRevokeHandler().Handle(
            new RevokeExternalLoginScopesCommand { UserId = userId, Provider = "discord", Scopes = ["identify", "EMAIL"] },
            CancellationToken.None);

        result.GrantedScopes.Should().BeEmpty();
        _externalLoginRepoMock.Verify(x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
            "revoking every scope must NOT unlink the provider — that is the unlink endpoint's job");
    }

    [Fact]
    public async Task RevokeScopes_ProviderNotLinked_ThrowsNotFound()
    {
        var userId = Guid.NewGuid();
        _externalLoginRepoMock
            .Setup(x => x.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var act = () => CreateRevokeHandler().Handle(
            new RevokeExternalLoginScopesCommand { UserId = userId, Provider = "google", Scopes = ["email"] },
            CancellationToken.None);

        await act.Should().ThrowAsync<ExternalLoginNotFoundException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("bad scope")]
    [InlineData("bad\nscope")]
    public async Task RevokeScopes_InvalidScopeToken_ThrowsBeforeAnyLookup(string invalidScope)
    {
        var act = () => CreateRevokeHandler().Handle(
            new RevokeExternalLoginScopesCommand { UserId = Guid.NewGuid(), Provider = "google", Scopes = [invalidScope] },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOAuthScopeException>();
        _externalLoginRepoMock.Verify(x => x.GetByUserIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
