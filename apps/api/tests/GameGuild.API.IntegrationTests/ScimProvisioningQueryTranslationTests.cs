using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Provisioning;
using GameGuild.Identity.Provisioning.Scim;
using GameGuild.Identity.Provisioning.Scim.Filtering;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.IntegrationTests;

/// <summary>
///     Guards that the SCIM read-side projections translate to SQL on the PostgreSQL
///     provider. These tests never connect: <see cref="EntityFrameworkQueryableExtensions"/>
///     <c>ToQueryString</c> runs the full EF translation pipeline and throws the same
///     "could not be translated" error the HTTP surface would surface as 500. The
///     PostgreSQL HTTP suite only runs in CI, so translation regressions otherwise hide
///     behind request failures (PR #773: every /scim list/create returned 500 because
///     composed Where/OrderBy could not bind through the projection).
/// </summary>
public sealed class ScimProvisioningQueryTranslationTests
{
    private static ApplicationDbContext CreateContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=gameguild")
            .Options);

    [Fact]
    public void TranslationContext_RequiresNoCredentialsAndKeepsConnectionClosed()
    {
        using var context = CreateContext();
        var connection = context.Database.GetDbConnection();
        context.Database.GetConnectionString().Should().Be("Host=localhost;Port=5432;Database=gameguild");
        connection.State.Should().Be(System.Data.ConnectionState.Closed);

        new ScimUserMappingRepository(context).QueryTenantUsers(Guid.NewGuid())
            .Take(1).ToQueryString().Should().Contain("SELECT");

        connection.State.Should().Be(System.Data.ConnectionState.Closed);
    }

    [Fact]
    public void UserProjection_TranslatesOrderingPaginationAndLookup()
    {
        using var context = CreateContext();
        var query = new ScimUserMappingRepository(context).QueryTenantUsers(Guid.NewGuid());

        var pagedSql = query
            .OrderBy(view => view.UserId)
            .Skip(1)
            .Take(2)
            .ToQueryString();
        pagedSql.Should().Contain("SELECT", "the paged user list must translate to SQL");

        var userId = Guid.NewGuid();
        query.Where(view => view.UserId == userId).ToQueryString()
            .Should().Contain("SELECT", "the single-user lookup must translate to SQL");
    }

    [Fact]
    public void UserProjection_TranslatesScimFilterPredicates()
    {
        using var context = CreateContext();
        var query = new ScimUserMappingRepository(context).QueryTenantUsers(Guid.NewGuid());

        foreach (var filter in new[]
                 {
                     "userName sw \"scim.page\"",
                     "userName co \"scim\"",
                     "userName eq \"bjensen\"",
                     "externalId eq \"00u5pljx8TkcQBZo2PxB\"",
                     "active eq \"false\"",
                     "userName pr"
                 })
        {
            var predicate = ScimFilterEvaluator.BuildPredicate<ScimUserView>(
                ScimFilterParser.Parse(filter), ScimFilterableAttributes.User);
            query.Where(predicate).OrderBy(view => view.UserId).ToQueryString()
                .Should().Contain("SELECT", $"the filter '{filter}' must translate to SQL");
        }
    }

    [Fact]
    public void GroupProjection_TranslatesOrderingPaginationAndLookup()
    {
        using var context = CreateContext();
        var query = new ScimGroupMappingRepository(context).QueryTenantGroups(Guid.NewGuid());

        query.OrderBy(view => view.RoleId).Skip(1).Take(2).ToQueryString()
            .Should().Contain("SELECT", "the paged group list must translate to SQL");

        var roleId = Guid.NewGuid();
        query.Where(view => view.RoleId == roleId).ToQueryString()
            .Should().Contain("SELECT", "the single-group lookup must translate to SQL");

        var predicate = ScimFilterEvaluator.BuildPredicate<ScimGroupView>(
            ScimFilterParser.Parse("displayName sw \"SCIM Eng\""), ScimFilterableAttributes.Group);
        query.Where(predicate).OrderBy(view => view.RoleId).ToQueryString()
            .Should().Contain("SELECT", "the group filter must translate to SQL");
    }
}
