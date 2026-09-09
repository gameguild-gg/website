using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class PostgreSqlEconomyWalletDirectoryTests
{
    [Fact]
    public async Task ResolvesOnlyActiveWalletsInsideTheRequestedTenant()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("wallet_directory");
        await using var context = CreateContext(database.ConnectionString);
        var tenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var owner = Guid.NewGuid();
        var activeWallet = WalletId.New();
        var suspendedWallet = WalletId.New();
        context.Set<EconomyWalletRow>().AddRange(
            Wallet(activeWallet, owner, tenant, WalletLifecycleState.Active),
            Wallet(suspendedWallet, Guid.NewGuid(), tenant, WalletLifecycleState.Frozen),
            Wallet(WalletId.New(), owner, otherTenant, WalletLifecycleState.Active));
        await context.SaveChangesAsync();
        var directory = new PostgreSqlEconomyWalletDirectory(context);

        var byOwner = await directory.GetOwnerWalletAsync(tenant, owner);
        var byWallet = await directory.GetWalletAsync(tenant, activeWallet);

        byOwner.Should().Be(new EconomyWalletIdentity(activeWallet, tenant, owner, WalletLifecycleState.Active));
        byWallet.Should().Be(byOwner);
        await FluentActions.Awaiting(() => directory.GetOwnerWalletAsync(otherTenant, Guid.NewGuid()).AsTask())
            .Should().ThrowAsync<EconomyWalletUnavailableException>();
        await FluentActions.Awaiting(() => directory.GetWalletAsync(tenant, suspendedWallet).AsTask())
            .Should().ThrowAsync<EconomyWalletUnavailableException>();
        await FluentActions.Awaiting(() => directory.GetWalletAsync(otherTenant, activeWallet).AsTask())
            .Should().ThrowAsync<EconomyWalletUnavailableException>();
    }

    [Fact]
    public async Task RejectsInvalidArgumentsAndNonRelationalContexts()
    {
        Action nullContext = () => new PostgreSqlEconomyWalletDirectory(null!);
        Action nonRelational = () => new PostgreSqlEconomyWalletDirectory(new StubApplicationDbContext());
        nullContext.Should().Throw<ArgumentNullException>();
        nonRelational.Should().Throw<InvalidOperationException>().WithMessage("*relational DbContext*");

        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("wallet_directory_validation");
        await using var context = CreateContext(database.ConnectionString);
        var directory = new PostgreSqlEconomyWalletDirectory(context);
        await FluentActions.Awaiting(() => directory.GetOwnerWalletAsync(Guid.Empty, Guid.NewGuid()).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => directory.GetOwnerWalletAsync(Guid.NewGuid(), Guid.Empty).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => directory.GetWalletAsync(Guid.Empty, WalletId.New()).AsTask())
            .Should().ThrowAsync<ArgumentException>().WithParameterName("tenantId");

        var inner = new InvalidOperationException("inner");
        var exception = new EconomyWalletUnavailableException("unavailable", inner);
        exception.Message.Should().Be("unavailable");
        exception.InnerException.Should().BeSameAs(inner);
    }

    private static EconomyWalletRow Wallet(
        WalletId id,
        Guid owner,
        Guid tenant,
        WalletLifecycleState state) => new()
    {
        Id = id.Value,
        OwnerId = owner,
        TenantId = tenant,
        State = state,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);

    private sealed class StubApplicationDbContext : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => throw new NotSupportedException();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTransactionAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
