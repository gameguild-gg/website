using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Distributed WebAuthn challenge store tests (issue #234): user-keyed ceremony storage,
///     guest-slot assertion sessions, JSON round-trips of the Fido2 options and TTL expiry
///     against the domain clock.
/// </summary>
public sealed class DistributedCacheWebAuthnSessionStoreTests
{
    private readonly IServiceProvider _services = new ServiceCollection().AddDistributedMemoryCache().BuildServiceProvider();

    private IDistributedCache Cache => _services.GetRequiredService<IDistributedCache>();

    private DistributedCacheWebAuthnSessionStore CreateStore() =>
        new(Cache, NullLogger<DistributedCacheWebAuthnSessionStore>.Instance);

    private static Fido2NetLib.CredentialCreateOptions RegistrationOptions(Guid userId, byte challenge) =>
        Fido2NetLib.CredentialCreateOptions.FromJson($$"""
            {
                "challenge": "{{Convert.ToBase64String([challenge])}}",
                "rp": { "id": "localhost", "name": "GameGuild" },
                "user": { "id": "{{Convert.ToBase64String(userId.ToByteArray())}}", "name": "user@example.com", "displayName": "User" },
                "pubKeyCredParams": [ { "type": "public-key", "alg": -7 } ],
                "timeout": 60000,
                "attestation": "none"
            }
            """);

    private static Fido2NetLib.AssertionOptions AssertionOptions(byte challenge) =>
        Fido2NetLib.AssertionOptions.FromJson($$"""
            {
                "challenge": "{{Convert.ToBase64String([challenge])}}",
                "timeout": 30000,
                "allowCredentials": [],
                "userVerification": "preferred"
            }
            """);

    [Fact]
    public async Task RegistrationSession_RoundTrips_Options_And_Challenge()
    {
        var store = CreateStore();
        var userId = Guid.NewGuid();

        await store.StoreRegistrationSessionAsync(new WebAuthnSession
        {
            UserId = userId,
            Challenge = [1, 2, 3, 4],
            RegistrationOptions = RegistrationOptions(userId, 4)
        });

        var found = await store.FindRegistrationSessionAsync(userId);
        found.Should().NotBeNull();
        found!.UserId.Should().Be(userId);
        found.Challenge.Should().Equal(1, 2, 3, 4);
        found.RegistrationOptions.Should().NotBeNull();
        found.RegistrationOptions!.User.Name.Should().Be("user@example.com");
        found.RegistrationOptions.Challenge.Should().Equal(4);

        await store.RemoveRegistrationSessionAsync(userId);
        (await store.FindRegistrationSessionAsync(userId)).Should().BeNull();
    }

    [Fact]
    public async Task AssertionSession_IsIsolatedPerUser_AndGuestSlotIsSeparate()
    {
        var store = CreateStore();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        await store.StoreAssertionSessionAsync(new WebAuthnSession { UserId = user1, Challenge = [11], AssertionOptions = AssertionOptions(11) });
        await store.StoreAssertionSessionAsync(new WebAuthnSession { UserId = user2, Challenge = [22], AssertionOptions = AssertionOptions(22) });
        await store.StoreAssertionSessionAsync(new WebAuthnSession { UserId = null, Challenge = [33], AssertionOptions = AssertionOptions(33) });

        (await store.FindAssertionSessionAsync(user1))!.AssertionOptions!.Challenge.Should().Equal(11);
        (await store.FindAssertionSessionAsync(user2))!.AssertionOptions!.Challenge.Should().Equal(22);
        (await store.FindGuestAssertionSessionAsync())!.AssertionOptions!.Challenge.Should().Equal(33);

        // Per-user removal leaves the others intact (replaces the process-local FindFirst scan).
        await store.RemoveAssertionSessionAsync(user1);
        (await store.FindAssertionSessionAsync(user1)).Should().BeNull();
        (await store.FindAssertionSessionAsync(user2)).Should().NotBeNull();
        (await store.FindGuestAssertionSessionAsync()).Should().NotBeNull();
    }

    [Fact]
    public async Task ReissuedAssertionSession_ReplacesThePreviousOneForThatUser()
    {
        var store = CreateStore();
        var userId = Guid.NewGuid();

        await store.StoreAssertionSessionAsync(new WebAuthnSession { UserId = userId, Challenge = [1], AssertionOptions = AssertionOptions(1) });
        await store.StoreAssertionSessionAsync(new WebAuthnSession { UserId = userId, Challenge = [2], AssertionOptions = AssertionOptions(2) });

        var found = await store.FindAssertionSessionAsync(userId);
        found!.AssertionOptions!.Challenge.Should().Equal(2);
    }

    [Fact]
    public async Task ExpiredSession_FailsClosed_EvenWhenCacheEntryStillExists()
    {
        var store = CreateStore();
        var userId = Guid.NewGuid();

        await store.StoreRegistrationSessionAsync(new WebAuthnSession
        {
            UserId = userId,
            Challenge = [9],
            CreatedAt = DateTime.UtcNow.Subtract(DistributedCacheWebAuthnSessionStore.SessionLifetime.Add(TimeSpan.FromMinutes(1))),
            RegistrationOptions = RegistrationOptions(userId, 9)
        });

        (await store.FindRegistrationSessionAsync(userId)).Should().BeNull();
    }

    [Fact]
    public async Task RegistrationSessionWithoutUser_IsRejected()
    {
        var store = CreateStore();
        var act = () => store.StoreRegistrationSessionAsync(new WebAuthnSession
        {
            UserId = null,
            Challenge = [1],
            RegistrationOptions = RegistrationOptions(Guid.NewGuid(), 1)
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SessionWithoutOptions_IsRejected()
    {
        var store = CreateStore();
        var act = () => store.StoreRegistrationSessionAsync(new WebAuthnSession { UserId = Guid.NewGuid(), Challenge = [1] });

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CorruptPayload_IsTreatedAsExpired()
    {
        var cache = Cache;
        var userId = Guid.NewGuid();
        await cache.SetStringAsync("auth:webauthn:session:reg:" + userId.ToString("N"), "not-json{{");

        var store = CreateStore();
        (await store.FindRegistrationSessionAsync(userId)).Should().BeNull();
    }
}
