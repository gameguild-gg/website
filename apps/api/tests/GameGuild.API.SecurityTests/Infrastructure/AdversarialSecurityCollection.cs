using GameGuild.API.Database;

namespace GameGuild.API.SecurityTests.Infrastructure;

/// <summary>
///     xUnit collection for the adversarial deny-by-default validation suite (issue #327).
///     Tests run sequentially against one shared PostgreSQL-backed host, mirroring
///     <c>ApiPostgreSqlCollection</c> in the API integration suite.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AdversarialSecurityCollection : ICollectionFixture<AdversarialSecurityFixture>
{
    public const string Name = "API adversarial security";
}
