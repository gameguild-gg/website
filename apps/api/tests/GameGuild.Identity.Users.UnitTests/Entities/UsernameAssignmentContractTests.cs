using FluentAssertions;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Entities;

public sealed class UsernameAssignmentContractTests
{
    [Theory]
    [InlineData("Matheus Martins", "matheus-martins")]
    [InlineData("MÁTHeus Martíns", "matheus-martins")]
    [InlineData("Jose\u0301 da Silva", "jose-da-silva")]
    [InlineData("  Alpha   User  ", "alpha-user")]
    [InlineData("Alpha/User", "alpha-user")]
    [InlineData("Alpha@User", "alpha-user")]
    [InlineData("Alpha---User", "alpha-user")]
    [InlineData("User.Name_1", "user.name_1")]
    [InlineData("...Alpha___", "alpha")]
    [InlineData("123 User", "123-user")]
    [InlineData("東京", "user")]
    public void PasswordFactory_AutomaticallyAssignsCanonicalUsernameWithoutChangingName(string name, string expected)
    {
        var user = User.CreateWithPassword("factory@example.invalid", name, "synthetic-hash");

        user.Username.Should().Be(expected);
        user.Name.Should().Be(name);
        user.PasswordHash.Should().Be("synthetic-hash");
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("oauth")]
    [InlineData("unverified-oauth")]
    public void EveryCreationFactory_AssignsUsernameWithoutChangingIdentity(string factory)
    {
        var user = factory switch
        {
            "legacy" => User.Create("factory@example.invalid", "Alpha User", "+15550000000"),
            "oauth" => User.CreateOAuthUser("factory@example.invalid", "Alpha User"),
            _ => User.CreateOAuthUser("factory@example.invalid", "Alpha User", false)
        };

        user.Username.Should().Be("alpha-user");
        user.Name.Should().Be("Alpha User");
        user.Email.Should().Be("factory@example.invalid");
        user.IsEmailVerified.Should().Be(factory == "oauth");
    }

    [Fact]
    public void ExplicitUsername_NormalizesSeparatelyFromDisplayName()
    {
        var user = User.CreateWithPassword("factory@example.invalid", "Unchanged Display Name", "synthetic-hash", "User.Name_1");

        user.Username.Should().Be("user.name_1");
        user.Name.Should().Be("Unchanged Display Name");
    }

    [Theory]
    [InlineData("---")]
    [InlineData("...")]
    [InlineData("東京")]
    public void ExplicitUsername_RejectsInputsWithNoUsableSlug(string username)
    {
        Assert.Throws<ArgumentException>(() => User.CreateWithPassword("factory@example.invalid", "Display", "synthetic-hash", username));
    }

    [Fact]
    public void GeneratedUsername_RespectsStoredMaximumLength()
    {
        var user = User.Create("factory@example.invalid", new string('A', 300));

        user.Username.Should().HaveLength(256).And.MatchRegex("^[a-z0-9._-]+$");
    }

    [Fact]
    public void DisplayNameUpdates_KeepEstablishedUsername()
    {
        var user = User.Create("factory@example.invalid", "Original Name");
        user.Username = "Existing.Legacy_Handle";

        user.UpdateName("New Display Name");

        user.Username.Should().Be("Existing.Legacy_Handle");
        user.Name.Should().Be("New Display Name");
    }
}
