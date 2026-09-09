using System.Data;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace GameGuild.Finance.Economy.UnitTests.Persistence;

public sealed class PostgreSqlTransactionExecutorTests
{
    [Fact]
    public async Task ContractContextCommitsSuccessAndRollsBackFailure()
    {
        var transaction = new Mock<IDbContextTransaction>(MockBehavior.Strict);
        transaction.Setup(item => item.CommitAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        transaction.Setup(item => item.RollbackAsync(CancellationToken.None)).Returns(Task.CompletedTask);
        transaction.Setup(item => item.DisposeAsync()).Returns(ValueTask.CompletedTask);
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        context.Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);

        var result = await PostgreSqlTransactionExecutor.ExecuteAsync(
            context.Object, IsolationLevel.Serializable, _ => Task.FromResult(42), CancellationToken.None);

        result.Should().Be(42);
        transaction.Verify(item => item.CommitAsync(CancellationToken.None), Times.Once);

        var failure = new InvalidOperationException("operation failed");
        await FluentActions.Awaiting(() => PostgreSqlTransactionExecutor.ExecuteAsync<int>(
                context.Object, IsolationLevel.Serializable, _ => Task.FromException<int>(failure),
                CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().Where(item => ReferenceEquals(item, failure));
        transaction.Verify(item => item.RollbackAsync(CancellationToken.None), Times.Once);
        transaction.Verify(item => item.DisposeAsync(), Times.Exactly(2));
    }

    [Fact]
    public async Task RelationalContextOwnsTransactionUnlessOneAlreadyExists()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transaction_executor");
        await using var context = CreateContext(database.ConnectionString);
        var observedTransaction = false;

        var result = await PostgreSqlTransactionExecutor.ExecuteAsync(
            (DbContext)context, IsolationLevel.Serializable, _ =>
            {
                observedTransaction = context.Database.CurrentTransaction is not null;
                return Task.FromResult("committed");
            }, CancellationToken.None);

        result.Should().Be("committed");
        observedTransaction.Should().BeTrue();
        context.Database.CurrentTransaction.Should().BeNull();

        await using var existing = await context.Database.BeginTransactionAsync(CancellationToken.None);
        var sameTransaction = await PostgreSqlTransactionExecutor.ExecuteAsync(
            (DbContext)context, IsolationLevel.ReadCommitted,
            _ => Task.FromResult(ReferenceEquals(existing, context.Database.CurrentTransaction)),
            CancellationToken.None);
        sameTransaction.Should().BeTrue();
        context.Database.CurrentTransaction.Should().BeSameAs(existing);
        await existing.RollbackAsync();

        var routedThroughContractOverload = await PostgreSqlTransactionExecutor.ExecuteAsync(
            (IApplicationDbContext)context, IsolationLevel.ReadCommitted,
            _ => Task.FromResult(true), CancellationToken.None);
        routedThroughContractOverload.Should().BeTrue();

        var protectedTransactionObserved = false;
        var protectedResult = await new EconomyProtectedOperationTransaction(context).ExecuteAsync(
            _ =>
            {
                protectedTransactionObserved = context.Database.CurrentTransaction is not null;
                return Task.FromResult("protected");
            },
            CancellationToken.None);
        protectedResult.Should().Be("protected");
        protectedTransactionObserved.Should().BeTrue();
    }

    [Fact]
    public async Task NonGenericOverloadExecutesOperationAndAllOverloadsValidateArguments()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("transaction_executor_void");
        await using var context = CreateContext(database.ConnectionString);
        var executed = false;

        await PostgreSqlTransactionExecutor.ExecuteAsync(
            context, IsolationLevel.ReadCommitted,
            _ =>
            {
                executed = true;
                return Task.CompletedTask;
            }, CancellationToken.None);

        executed.Should().BeTrue();
        Func<Task<int>> nullContract = () => PostgreSqlTransactionExecutor.ExecuteAsync<int>(
            (IApplicationDbContext)null!, IsolationLevel.Serializable, _ => Task.FromResult(1),
            CancellationToken.None);
        Func<Task<int>> nullContractOperation = () => PostgreSqlTransactionExecutor.ExecuteAsync<int>(
            (IApplicationDbContext)context, IsolationLevel.Serializable, null!, CancellationToken.None);
        Func<Task<int>> nullRelational = () => PostgreSqlTransactionExecutor.ExecuteAsync<int>(
            (DbContext)null!, IsolationLevel.Serializable, _ => Task.FromResult(1), CancellationToken.None);
        Func<Task<int>> nullRelationalOperation = () => PostgreSqlTransactionExecutor.ExecuteAsync<int>(
            (DbContext)context, IsolationLevel.Serializable, null!, CancellationToken.None);
        await nullContract.Should().ThrowAsync<ArgumentNullException>();
        await nullContractOperation.Should().ThrowAsync<ArgumentNullException>();
        await nullRelational.Should().ThrowAsync<ArgumentNullException>();
        await nullRelationalOperation.Should().ThrowAsync<ArgumentNullException>();
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
}
