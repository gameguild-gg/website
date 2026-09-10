using FluentAssertions;
using GameGuild.Finance.Economy.Commands;
using GameGuild.Finance.Economy.Funding;
using GameGuild.Identity.Context.Actors;

namespace GameGuild.Finance.Economy.UnitTests.Commands;

public sealed class HardCoinTopUpCommandsTests
{
    private static readonly Guid TenantId = Guid.Parse("9b000000-0000-0000-0000-000000000001");
    private static readonly Guid ActorId = Guid.Parse("9b000000-0000-0000-0000-000000000002");
    private static readonly Guid TopUpId = Guid.Parse("9b000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task CreateHandler_ForwardsOnlyTheBusinessIntent()
    {
        var service = new StubService();
        var request = new CreateMyHardCoinTopUpRequest(250, "top-up-key");

        var result = await new CreateMyHardCoinTopUpCommandHandler(service)
            .Handle(new CreateMyHardCoinTopUpCommand(request), default);

        service.Request.Should().Be(new SelfServiceHardCoinTopUpRequest(250, "top-up-key"));
        result.TopUpId.Should().Be(TopUpId);
    }

    [Fact]
    public async Task QueryHandlers_DeriveTenantAndActorFromTheAuthenticatedContext()
    {
        var reader = new StubReader();
        var accessor = Accessor(authenticated: true);

        var item = await new GetMyHardCoinTopUpQueryHandler(reader, accessor)
            .Handle(new GetMyHardCoinTopUpQuery(TopUpId), default);
        var list = await new ListMyHardCoinTopUpsQueryHandler(reader, accessor)
            .Handle(new ListMyHardCoinTopUpsQuery(25), default);

        item.Should().NotBeNull();
        list.Should().ContainSingle();
        reader.GetAuthority.Should().Be((TenantId, ActorId, TopUpId));
        reader.ListAuthority.Should().Be((TenantId, ActorId, 25));
    }

    [Fact]
    public async Task QueryHandlers_RejectAnonymousOrInvalidRequests()
    {
        var reader = new StubReader();
        var anonymous = Accessor(authenticated: false);

        await FluentActions.Awaiting(() => new GetMyHardCoinTopUpQueryHandler(reader, anonymous)
                .Handle(new GetMyHardCoinTopUpQuery(TopUpId), default))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => new ListMyHardCoinTopUpsQueryHandler(reader, Accessor(true))
                .Handle(new ListMyHardCoinTopUpsQuery(0), default))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => new ListMyHardCoinTopUpsQueryHandler(reader, Accessor(true))
                .Handle(new ListMyHardCoinTopUpsQuery(101), default))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Awaiting(() => new GetMyHardCoinTopUpQueryHandler(reader, Accessor(true))
                .Handle(new GetMyHardCoinTopUpQuery(Guid.Empty), default))
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Awaiting(() => new GetMyHardCoinTopUpQueryHandler(reader, Accessor(true))
                .Handle(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => new ListMyHardCoinTopUpsQueryHandler(reader, Accessor(true))
                .Handle(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => new CreateMyHardCoinTopUpCommandHandler(new StubService())
                .Handle(null!, default))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => new CreateMyHardCoinTopUpCommandHandler(new StubService())
                .Handle(new CreateMyHardCoinTopUpCommand(null!), default))
            .Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task QueryHandlers_RejectEachIncompleteActorAuthority()
    {
        var reader = new StubReader();
        foreach (var actor in new[]
                 {
                     Actor(authenticated: true, tenantId: null, subjectId: ActorId.ToString()),
                     Actor(authenticated: true, tenantId: TenantId, subjectId: null),
                     Actor(authenticated: true, tenantId: TenantId, subjectId: "not-a-guid")
                 })
        {
            var accessor = new ActorContextAccessor();
            accessor.SetActorContext(actor);
            await FluentActions.Awaiting(() => new GetMyHardCoinTopUpQueryHandler(reader, accessor)
                    .Handle(new GetMyHardCoinTopUpQuery(TopUpId), default))
                .Should().ThrowAsync<UnauthorizedAccessException>();
        }
    }

    [Fact]
    public void ValidatorAndContractsCoverEveryPublicBusinessField()
    {
        var validator = new CreateMyHardCoinTopUpCommandValidator();
        validator.Validate(new CreateMyHardCoinTopUpCommand(
            new CreateMyHardCoinTopUpRequest(250, "key"))).IsValid.Should().BeTrue();
        validator.Validate(new CreateMyHardCoinTopUpCommand(null!)).IsValid.Should().BeFalse();
        validator.Validate(new CreateMyHardCoinTopUpCommand(
            new CreateMyHardCoinTopUpRequest(0, ""))).Errors.Should().HaveCount(2);
        validator.Validate(new CreateMyHardCoinTopUpCommand(
            new CreateMyHardCoinTopUpRequest(1, new string('k', 129)))).IsValid.Should().BeFalse();

        var receipt = new SelfServiceHardCoinTopUpReceipt(
            TopUpId, Guid.NewGuid(), 250, 250, "USD", EconomyTopUpProviderStatus.RequiresAction,
            "secret", "pk", false) { ProviderObjectId = "pi" };
        receipt.Should().BeEquivalentTo(new
        {
            receipt.TopUpId,
            receipt.PaymentId,
            receipt.HardCoinUnits,
            receipt.UsdMinorUnits,
            receipt.Currency,
            receipt.Status,
            receipt.ClientSecret,
            receipt.PublishableKey,
            receipt.IsDuplicate,
            receipt.ProviderObjectId
        });
        var status = Status();
        status.Should().BeEquivalentTo(new
        {
            status.TopUpId,
            status.HardCoinUnits,
            status.UsdMinorUnits,
            status.Currency,
            status.Status,
            status.ProviderObjectId,
            status.RequestedAt,
            status.ProviderBoundAt
        });
    }

    private static ActorContextAccessor Accessor(bool authenticated)
    {
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(Actor(authenticated, TenantId, ActorId.ToString()));
        return accessor;
    }

    private static ActorContext Actor(bool authenticated, Guid? tenantId, string? subjectId) => new()
    {
        ActorKind = ActorKind.User,
        SubjectId = subjectId,
        TenantId = tenantId,
        Roles = new HashSet<string>(),
        Permissions = new HashSet<string>(),
        IsAuthenticated = authenticated
    };

    private static EconomyTopUpStatusDto Status() => new(
        TopUpId, 250, 250, "USD", EconomyTopUpProviderStatus.RequiresAction,
        "pi_topup", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

    private sealed class StubService : ISelfServiceHardCoinTopUpService
    {
        public SelfServiceHardCoinTopUpRequest? Request { get; private set; }

        public Task<SelfServiceHardCoinTopUpReceipt> CreateAsync(
            SelfServiceHardCoinTopUpRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new SelfServiceHardCoinTopUpReceipt(
                TopUpId, Guid.NewGuid(), request.HardCoinUnits, request.HardCoinUnits,
                "USD", EconomyTopUpProviderStatus.RequiresAction, "secret", "pk", false));
        }
    }

    private sealed class StubReader : IEconomyTopUpReader
    {
        public (Guid TenantId, Guid ActorId, Guid TopUpId) GetAuthority { get; private set; }
        public (Guid TenantId, Guid ActorId, int Take) ListAuthority { get; private set; }

        public ValueTask<EconomyTopUpStatusDto?> GetAsync(
            Guid tenantId, Guid actorId, Guid topUpId, CancellationToken cancellationToken)
        {
            GetAuthority = (tenantId, actorId, topUpId);
            return ValueTask.FromResult<EconomyTopUpStatusDto?>(Status());
        }

        public ValueTask<IReadOnlyList<EconomyTopUpStatusDto>> ListAsync(
            Guid tenantId, Guid actorId, int take, CancellationToken cancellationToken)
        {
            ListAuthority = (tenantId, actorId, take);
            return ValueTask.FromResult<IReadOnlyList<EconomyTopUpStatusDto>>([Status()]);
        }
    }
}
