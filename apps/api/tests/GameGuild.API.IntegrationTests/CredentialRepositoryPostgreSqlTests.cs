using System.Security.Cryptography;
using GameGuild.API.Database;
using GameGuild.API.IntegrationTests.Infrastructure;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Tenants;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.API.IntegrationTests;

/// <summary>Actual registered repository ports and migrated PostgreSQL persistence.
/// This qualifies storage contracts, not external OAuth or WebAuthn cryptography.</summary>
[Collection(ApiPostgreSqlCollection.Name)]
public sealed class CredentialRepositoryPostgreSqlTests(ApiPostgreSqlFixture fixture)
{
    [Fact]
    public async Task LocalUserRepositoryPersistsCredentialAndRejectsStalePasswordExpectation()
    {
        var owner = await CreateOwner();
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            Assert.False(await repository.UpdatePasswordHashAsync(owner.Id, "synthetic-replacement", "stale-expectation"));
            Assert.True(await repository.UpdatePasswordHashAsync(owner.Id, "synthetic-replacement", "synthetic-initial"));
        }
        await using var read = fixture.Factory.Services.CreateAsyncScope();
        var users = read.ServiceProvider.GetRequiredService<IUserRepository>();
        Assert.Equal(owner.Id, (await users.GetByEmailAsync(owner.Email))!.Id);
        Assert.Equal(owner.Id, (await users.GetByUsernameAsync(owner.Username!))!.Id);
        Assert.Equal("synthetic-replacement", (await users.GetByIdAsync(owner.Id))!.PasswordHash);
    }

    [Fact]
    public async Task ExternalLoginRepositoryPersistsProviderIdentityAndConsentAcrossScopes()
    {
        var owner = await CreateOwner();
        var marker = Guid.NewGuid().ToString("N");
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IExternalLoginRepository>();
            await repository.AddAsync(new ExternalLogin { UserId = owner.Id, Provider = "SyntheticProvider", ProviderKey = marker });
            await repository.RecordConsentAsync("SyntheticProvider", owner.Id, ["profile", "email"]);
        }
        await using var read = fixture.Factory.Services.CreateAsyncScope();
        var external = read.ServiceProvider.GetRequiredService<IExternalLoginRepository>();
        var persisted = await external.GetByProviderKeyAsync("SyntheticProvider", marker);
        Assert.NotNull(persisted);
        Assert.Equal(owner.Id, persisted.UserId);
        Assert.Equal(new[] { "profile", "email" }, ExternalLoginGrants.Deserialize(persisted.GrantedScopes));
        Assert.NotNull(persisted.ConsentedAt);
        Assert.Equal(OAuthConsentVersions.Current, persisted.ConsentVersion);
        Assert.Empty(await external.GetByUserIdAsync(Guid.NewGuid()));
        Assert.False(await external.DeleteAsync("SyntheticProvider", Guid.NewGuid()));
        Assert.NotNull(await external.GetByProviderKeyAsync("SyntheticProvider", marker));
    }

    [Fact]
    public async Task WebAuthnRepositoryPersistsCounterAndTerminalRevocation()
    {
        var owner = await CreateOwner();
        var credential = new UserWebAuthnCredential { Id = Guid.NewGuid(), UserId = owner.Id,
            CredentialId = Guid.NewGuid().ToString("N"), PublicKey = Convert.ToBase64String([1, 2, 3]),
            FriendlyName = "Synthetic storage fixture" };
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IWebAuthnCredentialRepository>();
            await repository.CreateAsync(credential);
            await repository.UpdateSignatureCounterAsync(credential.Id, 7);
        }
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IWebAuthnCredentialRepository>();
            Assert.Equal(7u, (await repository.GetByCredentialIdAsync(credential.CredentialId))!.SignatureCounter);
            Assert.Empty(await repository.GetByUserIdAsync(Guid.NewGuid()));
            Assert.True(await repository.DeactivateAsync(credential.Id));
            Assert.False(await repository.HasActiveCredentialsAsync(owner.Id));
            Assert.True(await repository.ReactivateAsync(credential.Id));
            Assert.True(await repository.RevokeAsync(credential.Id));
            Assert.False(await repository.ReactivateAsync(credential.Id));
        }
        await using var read = fixture.Factory.Services.CreateAsyncScope();
        var persisted = await read.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Set<UserWebAuthnCredential>().AsNoTracking().SingleAsync(row => row.Id == credential.Id);
        Assert.False(persisted.IsActive);
        Assert.NotNull(persisted.RevokedAt);
        Assert.NotNull(persisted.LastUsedAt);
        Assert.Equal(7u, persisted.SignatureCounter);
    }

    [Fact]
    public async Task RefreshRepositoryPersistsHashedCredentialAndRevocation()
    {
        var owner = await CreateOwner();
        var token = new RefreshToken { UserId = owner.Id, Token = Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(32))),
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddHours(1), CreatedByIp = "192.0.2.10" };
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>().CreateAsync(token);
        }
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
            var persisted = await repository.GetByTokenAsync(token.Token);
            Assert.NotNull(persisted);
            Assert.Equal(owner.Id, persisted.UserId);
            Assert.Single(await repository.GetActiveByUserIdAsync(owner.Id));
            Assert.Empty(await repository.GetByUserIdAsync(Guid.NewGuid()));
            persisted.IsRevoked = true;
            persisted.RevokedAt = DateTime.UtcNow;
            await repository.UpdateAsync(persisted);
        }
        await using var read = fixture.Factory.Services.CreateAsyncScope();
        var refresh = read.ServiceProvider.GetRequiredService<IRefreshTokenRepository>();
        Assert.Empty(await refresh.GetActiveByUserIdAsync(owner.Id));
        Assert.True((await refresh.GetByTokenAsync(token.Token))!.IsRevoked);
    }

    [Fact]
    public async Task ApiKeyRepositoryPersistsUsageAndOwnedReplacementWithoutPlaintext()
    {
        var owner = await CreateOwner();
        var marker = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var (key, plaintext) = ApiKey.Create(owner.Id, tenantId, "Synthetic repository credential", [ApiKeyScopes.ManageApiKeys]);
        var (replacement, _) = ApiKey.Create(owner.Id, tenantId, "Synthetic replacement", [ApiKeyScopes.ManageApiKeys]);
        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Add(new Tenant { Id = tenantId, Name = "Synthetic credential tenant " + marker,
                Slug = "repository-" + marker, AdminEmail = owner.Email, IsActive = true });
            await db.SaveChangesAsync();
            var repository = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
            await repository.AddAsync(key);
            Assert.Null(await repository.GetByIdForUserAsync(key.Id, Guid.NewGuid()));
            Assert.Null(await repository.RevokeAsync(key.Id, Guid.NewGuid(), "Synthetic foreign owner"));
            await repository.RecordUsageAsync(key);
            await repository.RotateAsync(key, replacement, graceEndsAt: null);
        }
        await using var read = fixture.Factory.Services.CreateAsyncScope();
        var keys = read.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        var stored = await keys.GetByIdForUserAsync(key.Id, owner.Id);
        var current = await keys.GetByIdForUserAsync(replacement.Id, owner.Id);
        Assert.NotNull(stored);
        Assert.NotNull(current);
        Assert.NotEqual(plaintext, stored.KeyHash);
        Assert.Equal(1, stored.UsageCount);
        Assert.NotNull(stored.RevokedAt);
        Assert.Equal(key.Id, current.ReplacesKeyId);
        Assert.Equal(replacement.Id, Assert.Single(await keys.GetActiveByUserIdAsync(owner.Id)).Id);
    }

    private async Task<User> CreateOwner()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var marker = Guid.NewGuid().ToString("N");
        var owner = User.Create(marker + "@example.test", "Synthetic repository owner");
        owner.Username = marker;
        owner.SetPasswordHash("synthetic-initial");
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        await users.AddAsync(owner);
        await users.SaveChangesAsync();
        return owner;
    }
}
