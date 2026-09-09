using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlRegisteredPostingCapabilityResolverTests
{
    [Fact]
    public async Task ResolveAsync_AuthorizesOnlyAnEnabledCapabilityWithTheRequestedTemplate()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("registered_capability_resolver");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var enabledId = Guid.NewGuid();

        await using (var connection = new NpgsqlConnection(database.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"""
                INSERT INTO public.economy_registered_capabilities
                    ("Id", "Name", "AllowedTemplateKinds", "IsEnabled", "CreatedAt", "RevokedAt")
                VALUES
                    ('{enabledId}', 'test-ad-reward-issuance', '[{(int)PostingTemplateKind.AdRewardIssuance}]'::jsonb, true, now(), NULL),
                    ('{Guid.NewGuid()}', 'revoked-capability', '[{(int)PostingTemplateKind.AdRewardIssuance}]'::jsonb, false, now(), now()),
                    ('{Guid.NewGuid()}', 'invalid-policy', jsonb_build_object(), true, now(), NULL);
                """, connection);
            await command.ExecuteNonQueryAsync();
        }

        var resolver = new PostgreSqlRegisteredPostingCapabilityResolver(context);

        var capability = await resolver.ResolveAsync(" test-ad-reward-issuance ", PostingTemplateKind.AdRewardIssuance);

        capability.Should().Be(new RegisteredPostingCapability(
            enabledId,
            "test-ad-reward-issuance",
            PostingTemplateKind.AdRewardIssuance));
        await FluentActions.Awaiting(() => resolver.ResolveAsync("missing", PostingTemplateKind.AdRewardIssuance))
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>()
            .WithMessage("*unavailable*");
        await FluentActions.Awaiting(() => resolver.ResolveAsync("revoked-capability", PostingTemplateKind.AdRewardIssuance))
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>()
            .WithMessage("*unavailable*");
        await FluentActions.Awaiting(() => resolver.ResolveAsync("test-ad-reward-issuance", PostingTemplateKind.Spend))
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>()
            .WithMessage("*does not authorize*");
        await FluentActions.Awaiting(() => resolver.ResolveAsync("invalid-policy", PostingTemplateKind.AdRewardIssuance))
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>()
            .WithMessage("*invalid template policy*");
    }

    [Fact]
    public async Task ResolveAsync_RejectsInvalidArgumentsAndNonRelationalContexts()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("registered_capability_args");
        await using var context = CreateContext(database.ConnectionString);
        var resolver = new PostgreSqlRegisteredPostingCapabilityResolver(context);

        await FluentActions.Awaiting(() => resolver.ResolveAsync(" ", PostingTemplateKind.AdRewardIssuance))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => resolver.ResolveAsync("valid", (PostingTemplateKind)int.MaxValue))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostgreSqlRegisteredPostingCapabilityResolver(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlRegisteredPostingCapabilityResolver(
                new Mock<IApplicationDbContext>().Object))
            .Should().Throw<InvalidOperationException>();
    }

    private static ApplicationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);
}
