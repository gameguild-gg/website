using FluentAssertions;
using GameGuild.Finance.Economy.Integrations.AI;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Risk;
using Xunit;

namespace GameGuild.Finance.Economy.UnitTests;

public sealed class AiCostAccountingValidationTests
{
    private static readonly DateTimeOffset Now = AiCostAccountingCoordinatorTests.Harness.Now;

    [Fact]
    public void Coordinator_RejectsNullDependencies()
    {
        var harness = new AiCostAccountingCoordinatorTests.Harness();

        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                null!, harness.RiskGate, harness.Fences, harness.Gateway, harness.Store, harness.Publisher))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                harness.Catalog, null!, harness.Fences, harness.Gateway, harness.Store, harness.Publisher))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                harness.Catalog, harness.RiskGate, null!, harness.Gateway, harness.Store, harness.Publisher))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                harness.Catalog, harness.RiskGate, harness.Fences, null!, harness.Store, harness.Publisher))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                harness.Catalog, harness.RiskGate, harness.Fences, harness.Gateway, null!, harness.Publisher))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostAccountingCoordinator(
                harness.Catalog, harness.RiskGate, harness.Fences, harness.Gateway, harness.Store, null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FundingFragment_RequiresAnExactlyBoundParentLot()
    {
        var harness = new AiCostAccountingCoordinatorTests.Harness();
        var lot = harness.Command().AvailableFundingLots.Single();
        var selection = FifoFragmentSelector.Select(
            [lot], new CoinAmount(CurrencyCode.SoftCoin, 100_000)).Selections.Single();

        var fragment = new AiFundingFragment(lot, selection);
        fragment.ParentLot.Should().BeSameAs(lot);
        fragment.Selection.Should().BeSameAs(selection);

        FluentActions.Invoking(() => new AiFundingFragment(null!, selection))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiFundingFragment(lot, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiFundingFragment(
                lot, selection with { ParentLotId = CreditLotId.New() }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AiFundingFragment(
                lot, selection with { Amount = new CoinAmount(CurrencyCode.HardCoin, selection.Amount.Units) }))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new AiFundingFragment(
                lot, selection with { TraceUnitsPerCoinUnit = 2 }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Authorize_ValidatesRequiredCommandFields()
    {
        var invalidCommands = new Func<AuthorizeAiServiceCommand, AuthorizeAiServiceCommand>[]
        {
            command => command with { RequestId = Guid.Empty },
            command => command with { TenantId = Guid.Empty },
            command => command with { ActorId = Guid.Empty },
            command => command with { ServiceCode = " " },
            command => command with { Provider = (AiProvider)999 },
            command => command with { Model = " " },
            command => command with { AvailableFundingLots = [] }
        };

        foreach (var mutation in invalidCommands)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            FluentActions.Invoking(() => harness.Coordinator.Authorize(mutation(harness.Command())))
                .Should().Throw<ArgumentException>();
        }

        var nullHarness = new AiCostAccountingCoordinatorTests.Harness();
        FluentActions.Invoking(() => nullHarness.Coordinator.Authorize(null!))
            .Should().Throw<ArgumentNullException>();
        var command = nullHarness.Command();
        FluentActions.Invoking(() => nullHarness.Coordinator.Authorize(command with { AvailableFundingLots = null! }))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => nullHarness.Coordinator.Authorize(command with { Risk = null! }))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Authorize_RejectsDuplicateRequestAndFundingLotIdentities()
    {
        var harness = new AiCostAccountingCoordinatorTests.Harness();
        var command = harness.Command();
        harness.Coordinator.Authorize(command);

        FluentActions.Invoking(() => harness.Coordinator.Authorize(
                command with { IdempotencyKey = new IdempotencyKey("same-request-new-key") }))
            .Should().Throw<AiCostAccountingIdempotencyException>();

        var duplicateHarness = new AiCostAccountingCoordinatorTests.Harness();
        var duplicateCommand = duplicateHarness.Command();
        FluentActions.Invoking(() => duplicateHarness.Coordinator.Authorize(
                duplicateCommand with
                {
                    AvailableFundingLots =
                        [duplicateCommand.AvailableFundingLots[0], duplicateCommand.AvailableFundingLots[0]]
                }))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Authorize_OnlySelectsConfirmedActiveSoftLotsFromTheSourceWallet()
    {
        var mutations = new Func<CreditLot, CreditLot>[]
        {
            lot => CopyLot(lot, walletId: WalletId.New()),
            lot => CopyLot(lot, amount: new CoinAmount(CurrencyCode.HardCoin, lot.Amount.Units)),
            lot => CopyLot(lot, state: CreditLotState.Held),
            lot => CopyLot(lot, confirmedAt: Now.AddMinutes(1), maturesAt: Now.AddMinutes(1))
        };

        foreach (var mutation in mutations)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            var command = harness.Command();
            FluentActions.Invoking(() => harness.Coordinator.Authorize(
                    command with { AvailableFundingLots = [mutation(command.AvailableFundingLots.Single())] }))
                .Should().Throw<InsufficientFragmentsException>();
        }
    }

    [Fact]
    public void Authorize_RejectsReservationReceiptsNotBoundToTheCharge()
    {
        var factories = new Func<AiSoftChargeReservationRequest, AiSoftChargeReservation>[]
        {
            _ => null!,
            request => new AiSoftChargeReservation(
                Guid.Empty, request.AuthorizationId, request.Amount, request.FundingFragments, request.ReservedAt),
            request => new AiSoftChargeReservation(
                Guid.NewGuid(), Guid.NewGuid(), request.Amount, request.FundingFragments, request.ReservedAt),
            request => new AiSoftChargeReservation(
                Guid.NewGuid(), request.AuthorizationId,
                new CoinAmount(CurrencyCode.SoftCoin, request.Amount.Units + 1),
                request.FundingFragments, request.ReservedAt),
            request => new AiSoftChargeReservation(
                Guid.NewGuid(), request.AuthorizationId, request.Amount, [], request.ReservedAt)
        };

        foreach (var factory in factories)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            harness.Gateway.ReservationFactory = factory;
            FluentActions.Invoking(() => harness.Coordinator.Authorize(harness.Command()))
                .Should().Throw<InvalidOperationException>();
        }
    }

    [Fact]
    public void RiskGate_RejectsNullDependenciesAndArguments()
    {
        FluentActions.Invoking(() => new AiCostRiskGate(null!, new AggregateRiskCounterStore()))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new AiCostRiskGate(new RiskDecisionAuthorizer(), null!))
            .Should().Throw<ArgumentNullException>();

        var harness = new AiCostAccountingCoordinatorTests.Harness();
        var command = harness.Command();
        var price = harness.Catalog.Resolve(command.ServiceCode, command.Provider, command.Model, Now);
        var funding = harness.Funding(command);
        FluentActions.Invoking(() => harness.RiskGate.Authorize(null!, price, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(command, null!, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(command, price, null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(command with { Risk = null! }, price, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(
                command with { Risk = command.Risk with { Decision = null! } }, price, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(
                command with { Risk = command.Risk with { Context = null! } }, price, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(
                command with { Risk = command.Risk with { EntityCluster = null! } }, price, funding))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.RiskGate.Authorize(
                command with { Risk = command.Risk with { Limits = null! } }, price, funding))
            .Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RiskGate_RejectsEveryUnboundOperationContextField()
    {
        var mutations = new Func<ProtectedOperationContext, ProtectedOperationContext>[]
        {
            context => context with { ActorId = Guid.NewGuid() },
            context => context with { Operation = PostingTemplateKind.Spend },
            context => context with { SourceWalletId = WalletId.New() },
            context => context with { DestinationWalletId = WalletId.New() },
            context => context with { Amount = new CoinAmount(CurrencyCode.SoftCoin, context.Amount.Units + 1) },
            context => context with { CurrencyLegs = [] },
            context => context with { SourceRoots = [] },
            context => context with { ProviderReferenceHash = "wrong" },
            context => context with { EntityGraphVersion = context.EntityGraphVersion + 1 },
            context => context with { EntityGraphEvidenceHash = "wrong" }
        };

        foreach (var mutation in mutations)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            var command = harness.Command();
            var price = harness.Catalog.Resolve(command.ServiceCode, command.Provider, command.Model, Now);
            var funding = harness.Funding(command);
            FluentActions.Invoking(() => harness.RiskGate.Authorize(
                    command with { Risk = command.Risk with { Context = mutation(command.Risk.Context) } },
                    price,
                    funding))
                .Should().Throw<AiCostRiskExposureException>();
        }
    }

    [Fact]
    public void RiskGate_RequiresEveryEntityLimitDimensionAndMaterialSubject()
    {
        var requiredEntityTypes = AiCostRiskGate.RequiredEntityTypes;
        AiCostRiskGate.RequiredLimitDimensions.Should().HaveCount(6);

        foreach (var entityType in requiredEntityTypes)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            var command = harness.Command();
            var cluster = command.Risk.EntityCluster with
            {
                Nodes = command.Risk.EntityCluster.Nodes.Where(node => node.Type != entityType).ToArray()
            };
            AssertRiskRejected(harness, command with { Risk = command.Risk with { EntityCluster = cluster } });
        }

        foreach (var dimension in AiCostRiskGate.RequiredLimitDimensions)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            var command = harness.Command();
            AssertRiskRejected(harness, command with
            {
                Risk = command.Risk with
                {
                    Limits = command.Risk.Limits.Where(limit => limit.Key.Dimension != dimension).ToArray()
                }
            });
        }

        foreach (var dimension in new[]
                 {
                     RiskLimitDimension.Wallet,
                     RiskLimitDimension.IdentityCluster,
                     RiskLimitDimension.Tenant,
                     RiskLimitDimension.ProviderAccount,
                     RiskLimitDimension.GlobalLossBudget
                 })
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            var command = harness.Command();
            var limits = command.Risk.Limits.Select(limit => limit.Key.Dimension == dimension
                ? new AggregateRiskLimit(new RiskLimitKey(dimension, "wrong"), 1, limit.MaxUnits, limit.Window)
                : limit).ToArray();
            AssertRiskRejected(harness, command with { Risk = command.Risk with { Limits = limits } });
        }

        var counterHarness = new AiCostAccountingCoordinatorTests.Harness();
        var counterCommand = counterHarness.Command();
        AssertRiskRejected(counterHarness, counterCommand with
        {
            Risk = counterCommand.Risk with { CounterReservationId = Guid.Empty }
        });
    }

    [Fact]
    public void RiskGate_StaticIdentifiersValidateAndNormalizeInputs()
    {
        AiCostRiskGate.ProviderAccount(AiProvider.OpenAi, " model ").Should().Be("OpenAi:model");
        AiCostRiskGate.ProviderReference(Guid.Parse("11111111-1111-1111-1111-111111111111"),
                " service ", AiProvider.OpenAi, " model ")
            .Should().Be("ai:11111111111111111111111111111111:service:OpenAi:model");

        FluentActions.Invoking(() => AiCostRiskGate.ProviderAccount((AiProvider)999, "model"))
            .Should().Throw<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => AiCostRiskGate.ProviderAccount(AiProvider.OpenAi, " "))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AiCostRiskGate.ProviderReference(
                Guid.Empty, "service", AiProvider.OpenAi, "model"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AiCostRiskGate.ProviderReference(
                Guid.NewGuid(), " ", AiProvider.OpenAi, "model"))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => AiCostRiskGate.GlobalLossBudget(Guid.Empty))
            .Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RiskGate_ReturnsTheAuthorizedDecisionAndCounterReservation()
    {
        var harness = new AiCostAccountingCoordinatorTests.Harness();
        var command = harness.Command();
        var price = harness.Catalog.Resolve(command.ServiceCode, command.Provider, command.Model, Now);

        var authorization = harness.RiskGate.Authorize(command, price, harness.Funding(command));

        authorization.Decision.DecisionId.Should().Be(command.Risk.Decision.Id);
        authorization.Decision.IdempotencyKey.Should().Be(command.IdempotencyKey);
        authorization.CounterReservation.Id.Should().Be(command.Risk.CounterReservationId);
    }

    [Fact]
    public void Complete_ValidatesInputsBindingStateAndEnvelope()
    {
        var invalidCommands = new Func<CompleteAiServiceCommand, CompleteAiServiceCommand>[]
        {
            command => command with { AuthorizationId = Guid.Empty },
            command => command with { ProviderUsageId = " " },
            command => command with { Provider = (AiProvider)999 },
            command => command with { Model = " " },
            command => command with { InputTokens = -1 },
            command => command with { OutputTokens = -1 },
            command => command with { TotalTokens = -1 }
        };
        foreach (var mutation in invalidCommands)
        {
            var harness = new AiCostAccountingCoordinatorTests.Harness();
            FluentActions.Invoking(() => harness.Coordinator.Complete(mutation(harness.Completion(Guid.NewGuid()))))
                .Should().Throw<ArgumentException>();
        }

        var nullHarness = new AiCostAccountingCoordinatorTests.Harness();
        FluentActions.Invoking(() => nullHarness.Coordinator.Complete(null!))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => nullHarness.Coordinator.Complete(nullHarness.Completion(Guid.NewGuid())))
            .Should().Throw<KeyNotFoundException>();

        var providerHarness = new AiCostAccountingCoordinatorTests.Harness();
        var providerAuthorization = providerHarness.Coordinator.Authorize(providerHarness.Command());
        FluentActions.Invoking(() => providerHarness.Coordinator.Complete(
                providerHarness.Completion(providerAuthorization.Id) with { Provider = AiProvider.Anthropic }))
            .Should().Throw<AiProviderUsageReplayException>();

        var modelHarness = new AiCostAccountingCoordinatorTests.Harness();
        var modelAuthorization = modelHarness.Coordinator.Authorize(modelHarness.Command());
        FluentActions.Invoking(() => modelHarness.Coordinator.Complete(
                modelHarness.Completion(modelAuthorization.Id) with { Model = "other" }))
            .Should().Throw<AiProviderUsageReplayException>();

        var outputHarness = new AiCostAccountingCoordinatorTests.Harness();
        var outputAuthorization = outputHarness.Coordinator.Authorize(outputHarness.Command());
        FluentActions.Invoking(() => outputHarness.Coordinator.Complete(
                outputHarness.Completion(outputAuthorization.Id) with { OutputTokens = 1_000_001 }))
            .Should().Throw<AiProviderUsageExceededReservationException>();

        var totalHarness = new AiCostAccountingCoordinatorTests.Harness();
        var totalAuthorization = totalHarness.Coordinator.Authorize(totalHarness.Command());
        FluentActions.Invoking(() => totalHarness.Coordinator.Complete(
                totalHarness.Completion(totalAuthorization.Id) with { TotalTokens = 1 }))
            .Should().Throw<ArgumentException>();

        var stateHarness = new AiCostAccountingCoordinatorTests.Harness();
        var completed = stateHarness.Coordinator.Authorize(stateHarness.Command());
        stateHarness.Coordinator.Complete(stateHarness.Completion(completed.Id));
        FluentActions.Invoking(() => stateHarness.Coordinator.Complete(
                stateHarness.Completion(completed.Id) with
                {
                    ProviderUsageId = "usage-2",
                    IdempotencyKey = new IdempotencyKey("complete-2")
                }))
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailAndGet_ValidateStateIdentityAndIdempotency()
    {
        var harness = new AiCostAccountingCoordinatorTests.Harness();
        FluentActions.Invoking(() => harness.Coordinator.Fail(null!)).Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => harness.Coordinator.Fail(new FailAiServiceCommand(
                Guid.Empty, "failure", Now, new IdempotencyKey("empty"))))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Fail(new FailAiServiceCommand(
                Guid.NewGuid(), " ", Now, new IdempotencyKey("blank"))))
            .Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Fail(new FailAiServiceCommand(
                Guid.NewGuid(), "failure", Now, new IdempotencyKey("missing"))))
            .Should().Throw<KeyNotFoundException>();
        FluentActions.Invoking(() => harness.Coordinator.Get(Guid.Empty)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => harness.Coordinator.Get(Guid.NewGuid())).Should().Throw<KeyNotFoundException>();

        var first = harness.Coordinator.Authorize(harness.Command());
        harness.Coordinator.Get(first.Id).Should().BeSameAs(first);
        harness.Coordinator.Fail(new FailAiServiceCommand(
            first.Id, "failure", Now, new IdempotencyKey("shared-fail")));
        FluentActions.Invoking(() => harness.Coordinator.Fail(new FailAiServiceCommand(
                first.Id, "again", Now, new IdempotencyKey("different-fail"))))
            .Should().Throw<InvalidOperationException>();

        var second = harness.Coordinator.Authorize(harness.Command(requestId: Guid.NewGuid()));
        FluentActions.Invoking(() => harness.Coordinator.Fail(new FailAiServiceCommand(
                second.Id, "failure", Now, new IdempotencyKey("shared-fail"))))
            .Should().Throw<AiCostAccountingIdempotencyException>();
    }

    [Fact]
    public void TreasuryObservation_RejectsNegativeReservedUnits()
    {
        var fact = new AiTreasuryServiceCostFact(
            Guid.NewGuid(), "ai.grade", 100_000, 1, 2, 3, Now, Now.AddMinutes(1));

        FluentActions.Invoking(() => fact.ToReserveObservation(-1))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void AssertRiskRejected(
        AiCostAccountingCoordinatorTests.Harness harness,
        AuthorizeAiServiceCommand command)
    {
        var price = harness.Catalog.Resolve(command.ServiceCode, command.Provider, command.Model, Now);
        FluentActions.Invoking(() => harness.RiskGate.Authorize(command, price, harness.Funding(command)))
            .Should().Throw<AiCostRiskExposureException>();
    }

    private static CreditLot CopyLot(
        CreditLot lot,
        WalletId? walletId = null,
        CoinAmount? amount = null,
        CreditLotState? state = null,
        DateTimeOffset? confirmedAt = null,
        DateTimeOffset? maturesAt = null) => new(
        lot.Id,
        walletId ?? lot.WalletId,
        amount ?? lot.Amount,
        lot.Provenance,
        confirmedAt ?? lot.ConfirmedAt,
        maturesAt ?? lot.OriginalMaturesAt,
        lot.JournalSequence,
        state ?? lot.State,
        lot.Ranges,
        lot.TraceUnitsPerCoinUnit);
}
