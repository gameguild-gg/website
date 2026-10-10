using FluentAssertions;
using GameGuild.Identity.Users;
using Xunit;

namespace GameGuild.Identity.Provisioning.UnitTests;

/// <summary>
///     Provisioning-token lifecycle: issuance format, hash-only storage, scope
/// semantics, revocation, and the rotation grace window.
/// </summary>
public sealed class ScimProvisioningTokenTests
{
    [Fact]
    public void Create_ProducesGgScimPrefixedPlaintext_AndHashesIt()
    {
        var (token, plaintext) = ScimProvisioningToken.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Okta", ["scim:read", "scim:write"]);

        plaintext.Should().StartWith("gg_scim_");
        plaintext.Length.Should().BeGreaterThan(ScimProvisioningToken.TokenPrefix.Length + 16);
        token.KeyPrefix.Should().Be("gg_scim_");
        token.KeyHash.Should().HaveLength(64).And.NotContain(plaintext, "only the SHA-256 hash is stored");
        token.IsValid().Should().BeTrue();
    }

    [Fact]
    public void Create_GeneratesUniqueTokens()
    {
        var tenant = Guid.NewGuid();

        var (first, firstPlaintext) = ScimProvisioningToken.Create(tenant, Guid.NewGuid(), "a", ScimScopes.All);
        var (second, secondPlaintext) = ScimProvisioningToken.Create(tenant, Guid.NewGuid(), "a", ScimScopes.All);

        firstPlaintext.Should().NotBe(secondPlaintext);
        first.KeyHash.Should().NotBe(second.KeyHash);
    }

    [Fact]
    public void HasScope_MatchesExactlyOrThroughWildcard()
    {
        var (token, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ["scim:read"]);

        token.HasScope("scim:read").Should().BeTrue();
        token.HasScope("scim:read ").Should().BeFalse();
        token.HasScope("scim:write").Should().BeFalse();

        var (wildcard, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ["*"]);
        wildcard.HasScope("anything").Should().BeTrue();
    }

    [Fact]
    public void ExpiredToken_IsInvalid()
    {
        var (token, _) = ScimProvisioningToken.Create(
            Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All,
            expiresAt: SystemClock.UtcNow.AddMinutes(-1));

        token.IsValid().Should().BeFalse();
    }

    [Fact]
    public void RevokedToken_IsInvalid()
    {
        var (token, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);
        token.Revoke("leaked");

        token.IsValid().Should().BeFalse();
        token.RevokedAt.Should().NotBeNull();
        token.RevocationReason.Should().Be("leaked");
    }

    [Fact]
    public void RotationGrace_KeepsOldTokenValid_UntilTheWindowCloses()
    {
        var (token, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);

        token.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(5));
        token.IsValid().Should().BeTrue("the old token overlaps its replacement during the grace window");

        token.BeginRotationGrace(SystemClock.UtcNow.AddMinutes(-1));
        token.IsValid().Should().BeFalse();

        token.FinalizeRotationRevocation().Should().BeTrue();
        token.RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public void FinalizeRotationRevocation_IsIdempotent()
    {
        var (token, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);
        token.Revoke("rotated");

        token.FinalizeRotationRevocation().Should().BeFalse("already revoked; nothing to finalize");
    }

    [Fact]
    public void RecordUsage_UpdatesCountersAndTimestamp()
    {
        var (token, _) = ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), "t", ScimScopes.All);

        token.RecordUsage();

        token.UsageCount.Should().Be(1);
        token.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public void UserMapping_FactorySetsKeyFields()
    {
        var tenant = Guid.NewGuid();
        var user = Guid.NewGuid();

        var mapping = ScimUserMapping.Create(tenant, "ext-1", user);

        mapping.TenantId.Should().Be(tenant);
        mapping.ExternalId.Should().Be("ext-1");
        mapping.UserId.Should().Be(user);

        var group = ScimGroupMapping.Create(tenant, "grp-1", Guid.NewGuid());
        group.TenantId.Should().Be(tenant);
        group.ExternalId.Should().Be("grp-1");
    }

    [Fact]
    public void Create_WithBlankName_Throws()
    {
        var act = () => ScimProvisioningToken.Create(Guid.NewGuid(), Guid.NewGuid(), " ", ScimScopes.All);

        act.Should().Throw<ArgumentException>();
    }
}

/// <summary>SCIM user resource mapping round-trips.</summary>
public sealed class ScimUserMapperTests
{
    [Fact]
    public void ToResource_MapsEveryImplementedAttribute()
    {
        var user = User.CreateOAuthUser("bjensen@example.com", "Barbara Jensen", emailVerified: true);
        user.PhoneNumber = "+15551234";

        var resource = Scim.ScimUserMapper.ToResource(user, "ext-42");

        resource.Id.Should().Be(user.Id.ToString());
        resource.ExternalId.Should().Be("ext-42");
        resource.UserName.Should().NotBeNullOrEmpty();
        resource.DisplayName.Should().Be("Barbara Jensen");
        resource.Name!.GivenName.Should().Be("Barbara");
        resource.Name.FamilyName.Should().Be("Jensen");
        resource.Emails.Should().ContainSingle().Which.Value.Should().Be("bjensen@example.com");
        resource.Active.Should().BeTrue();
        resource.Meta.ResourceType.Should().Be("User");
        resource.Meta.Location.Should().Be($"/scim/v2/Users/{user.Id}");
    }

    [Fact]
    public void ToResource_SoftDeletedUser_IsInactive()
    {
        var user = User.CreateOAuthUser("x@example.com", "X Y", emailVerified: false);
        // SoftDelete refuses unpersisted entities (Version == 0); simulate a stored row
        // the same way the sibling Users.UnitTests suites do.
        user.Version = 1;
        user.MarkDeleted();

        var resource = Scim.ScimUserMapper.ToResource(user, null);

        resource.Active.Should().BeFalse();
        resource.ExternalId.Should().BeNull();
    }
}
