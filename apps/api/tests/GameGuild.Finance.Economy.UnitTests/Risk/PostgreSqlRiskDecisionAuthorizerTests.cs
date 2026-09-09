using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GameGuild.Finance.Economy.UnitTests.Risk;

public sealed class PostgreSqlRiskDecisionAuthorizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 10, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AllowsOnlyTheExactDurableAllowDecision()
    {
        await using var database = await CreateDatabaseAsync();
        var context = CreateContext(database.ConnectionString);
        await using (context)
        {
            await context.Database.MigrateAsync();
            var operation = CreateOperation();
            var decision = CreateDecision(operation);
            await PersistAsync(context, operation, decision);

            var authorization = new PostgreSqlRiskDecisionAuthorizer(context)
                .AuthorizeValueMovement(decision, operation, Now);

            authorization.DecisionId.Should().Be(decision.Id);
            authorization.OperationFingerprint.Should().Be(operation.Fingerprint());
            authorization.IdempotencyKey.Should().Be(operation.IdempotencyKey);
        }
    }

    [Fact]
    public async Task RejectsMissingOrDifferentDurableDecision()
    {
        await using var database = await CreateDatabaseAsync();
        var context = CreateContext(database.ConnectionString);
        await using (context)
        {
            await context.Database.MigrateAsync();
            var operation = CreateOperation();
            var decision = CreateDecision(operation);
            await PersistAsync(context, operation, decision);
            var authorizer = new PostgreSqlRiskDecisionAuthorizer(context);

            FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                    decision with { Id = Guid.NewGuid() }, operation, Now))
                .Should().Throw<RiskDecisionBindingException>();
            FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                    decision with { IssuedAt = decision.IssuedAt.AddSeconds(1) }, operation, Now))
                .Should().Throw<RiskDecisionBindingException>();
            FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                    decision with { ExpiresAt = decision.ExpiresAt.AddSeconds(1) }, operation, Now))
                .Should().Throw<RiskDecisionBindingException>();
            FluentActions.Invoking(() => authorizer.AuthorizeValueMovement(
                    decision with { Outcome = RiskOutcome.Deny }, operation, Now))
                .Should().Throw<RiskDecisionBindingException>();
        }
    }

    [Fact]
    public async Task RejectsAStoredDenyDecisionAfterVerifyingItsBinding()
    {
        await using var database = await CreateDatabaseAsync();
        var context = CreateContext(database.ConnectionString);
        await using (context)
        {
            await context.Database.MigrateAsync();
            var operation = CreateOperation();
            var decision = CreateDecision(operation) with { Outcome = RiskOutcome.Deny };
            await PersistAsync(context, operation, decision);

            FluentActions.Invoking(() => new PostgreSqlRiskDecisionAuthorizer(context)
                    .AuthorizeValueMovement(decision, operation, Now))
                .Should().Throw<RiskAuthorizationDeniedException>();
        }
    }

    private static Task<EconomyPostgreSqlTestDatabase> CreateDatabaseAsync() =>
        EconomyPostgreSqlTestDatabase.CreateAsync("risk_authorizer");

    private static ApplicationDbContext CreateContext(string connectionString) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private static ProtectedOperationContext CreateOperation()
    {
        return new ProtectedOperationContext(
            new IdempotencyKey("persisted-risk-decision"),
            Guid.NewGuid(),
            PostingTemplateKind.Spend,
            WalletId.New(),
            WalletId.New(),
            new CoinAmount(CurrencyCode.HardCoin, 100),
            [new RiskCurrencyLeg(CurrencyCode.HardCoin, 100)],
            [SourceStampId.New()],
            "provider-reference-hash",
            new PolicyVersion(1),
            new ReserveVersion(1),
            1,
            1,
            1,
            "entity-graph-hash");
    }

    private static RiskDecisionSnapshot CreateDecision(ProtectedOperationContext operation) =>
        RiskDecisionSnapshot.Create(
            Guid.NewGuid(),
            RiskOutcome.Allow,
            operation,
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            [RiskReasonCode.WithinLimits]);

    private static async Task PersistAsync(
        ApplicationDbContext context,
        ProtectedOperationContext operation,
        RiskDecisionSnapshot decision)
    {
        context.Set<EconomyWalletRow>().AddRange(
            new EconomyWalletRow
            {
                Id = operation.SourceWalletId.Value,
                OwnerId = operation.ActorId,
                TenantId = Guid.NewGuid(),
                State = WalletLifecycleState.Active,
                CreatedAt = Now.AddMinutes(-2)
            },
            new EconomyWalletRow
            {
                Id = operation.DestinationWalletId.Value,
                OwnerId = operation.ActorId,
                TenantId = Guid.NewGuid(),
                State = WalletLifecycleState.Active,
                CreatedAt = Now.AddMinutes(-2)
            });
        context.Set<EconomyRiskDecisionRow>().Add(new EconomyRiskDecisionRow
        {
            Id = decision.Id,
            Outcome = decision.Outcome,
            OperationFingerprint = decision.OperationFingerprint,
            ActorHash = "actor-hash",
            TemplateKind = operation.Operation,
            SourceWalletId = operation.SourceWalletId.Value,
            DestinationWalletId = operation.DestinationWalletId.Value,
            Currency = operation.Amount.Currency,
            AmountUnits = operation.Amount.Units,
            CurrencyLegs = "[]",
            SourceRoots = "[]",
            ProviderReferenceHash = operation.ProviderReferenceHash,
            PolicyVersion = operation.PolicyVersion.Value,
            ReserveVersion = operation.ReserveVersion.Value,
            ReserveAuthorizationEpoch = operation.ReserveAuthorizationEpoch,
            FeatureVersion = operation.FeatureVersion,
            KillSwitchEpoch = operation.KillSwitchEpoch,
            CounterVersion = operation.CounterVersion,
            EntityGraphVersion = operation.EntityGraphVersion,
            EntityGraphEvidenceHash = operation.EntityGraphEvidenceHash,
            ReasonCodes = "[1]",
            IssuedAt = decision.IssuedAt,
            ExpiresAt = decision.ExpiresAt
        });
        await context.SaveChangesAsync();
    }

}
