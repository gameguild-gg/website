using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace GameGuild.Finance.Economy.UnitTests.Operations;

public sealed class PostgreSqlEconomyWalletProvisionerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ProvisionAsync_IsTenantOwnerIdempotentAndCreatesCompleteAccountSet()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_wallet_provision");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var provisioner = new PostgreSqlEconomyWalletProvisioner(context);
        var tenantId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();

        var first = await provisioner.ProvisionAsync(tenantId, ownerId, Now);
        var replay = await provisioner.ProvisionAsync(tenantId, ownerId, Now.AddMinutes(1));

        replay.Should().Be(first);
        first.TenantId.Should().Be(tenantId);
        first.OwnerId.Should().Be(ownerId);
        first.State.Should().Be(WalletLifecycleState.Active);
        (await context.Set<EconomyWalletRow>().CountAsync()).Should().Be(1);
        (await context.Set<EconomyAccountRow>().CountAsync(row => row.WalletId == first.WalletId.Value))
            .Should().Be(12);
        (await context.Set<EconomyAccountRow>().CountAsync(row => row.WalletId == null))
            .Should().Be(1);
        (await context.Set<EconomyWalletBalanceProjectionRow>().CountAsync(row => row.WalletId == first.WalletId.Value))
            .Should().Be(1);
    }

    [Fact]
    public async Task ProvisionAsync_RejectsInvalidIdentityAndNonRelationalContext()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("legacy_wallet_provision_validation");
        await using var context = CreateContext(database.ConnectionString);
        var provisioner = new PostgreSqlEconomyWalletProvisioner(context);

        await FluentActions.Invoking(() => provisioner.ProvisionAsync(Guid.Empty, Guid.NewGuid(), Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => provisioner.ProvisionAsync(Guid.NewGuid(), Guid.Empty, Now).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyWalletProvisioner(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new PostgreSqlEconomyWalletProvisioner(new StubApplicationDbContext()))
            .Should().Throw<InvalidOperationException>();

        await using var offline = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=1")
                .Options);
        var unavailable = new PostgreSqlEconomyWalletProvisioner(offline);
        await FluentActions.Awaiting(() => unavailable.ProvisionAsync(
                Guid.NewGuid(), Guid.NewGuid(), Now).AsTask())
            .Should().ThrowAsync<EconomyWalletUnavailableException>()
            .WithMessage("*provisioner rejected*");
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
