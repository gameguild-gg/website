using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;
using GameGuild.Finance.Economy.Persistence;
using GameGuild.Finance.Economy.Risk;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Reflection;

namespace GameGuild.Finance.Economy.UnitTests.Ledger;

public sealed class PostgreSqlProviderEvidencePostingAuthorityIssuerTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 25, 4, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task IssueAsync_PersistsOneUseProviderEvidenceAuthorityAndReplaysExactly()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_evidence_authority");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var walletId = WalletId.New();
        var capabilityId = Guid.NewGuid();
        await SeedAsync(context, tenantId, actorId, walletId, capabilityId, "[15,16]");
        var request = CreateRequest(tenantId, actorId, walletId);
        var issuer = new PostgreSqlProviderEvidencePostingAuthorityIssuer(context);

        var first = await issuer.IssueAsync(request);
        var replay = await issuer.IssueAsync(request);

        replay.Should().Be(first);
        first.CapabilityId.Should().Be(capabilityId);
        first.TenantId.Should().Be(tenantId);
        first.ActorId.Should().Be(actorId);
        first.ExpectedCounterVersion.Should().Be(1);
        var decision = await context.Set<EconomyRiskDecisionRow>().AsNoTracking().SingleAsync();
        decision.TemplateKind.Should().Be(PostingTemplateKind.AdminWithdrawalSuccess);
        decision.OperationFingerprint.Should().Be(request.OperationFingerprint);
        decision.PolicyVersion.Should().Be(request.PolicyVersion.Value);
        decision.ReserveVersion.Should().Be(request.ReserveVersion.Value);
        decision.ReserveAuthorizationEpoch.Should().Be(request.ReserveAuthorizationEpoch);
        decision.KillSwitchEpoch.Should().Be(request.KillSwitchEpoch);
        (await context.Set<EconomyRiskCounterRow>().AsNoTracking().SingleAsync()).UsedUnits
            .Should().Be(request.Amount.Units);
        (await context.Set<EconomyRiskCounterReservationRow>().AsNoTracking().SingleAsync()).RiskDecisionId
            .Should().Be(decision.Id);
        (await context.Set<EconomyRiskAuditEvidenceRow>().AsNoTracking().SingleAsync()).EvidenceHash
            .Should().Be(request.EvidenceHash);

        await issuer.ConsumeAsync(first, Now.AddMinutes(1));
        var consumed = await context.Set<EconomyRiskCounterReservationRow>().AsNoTracking().SingleAsync();
        consumed.Status.Should().Be(RiskCounterReservationStatus.Consumed);
        consumed.ConsumedAt.Should().Be(Now.AddMinutes(1));
        await FluentActions.Invoking(() => issuer.ConsumeAsync(first, Now.AddMinutes(2)).AsTask())
            .Should().ThrowAsync<RiskDecisionReuseException>();
    }

    [Fact]
    public async Task IssueAsync_FailsClosedForMismatchedReplayCapabilityOrTenantWallet()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_evidence_authority_fail_closed");
        await using var context = CreateContext(database.ConnectionString);
        await context.Database.MigrateAsync();
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var walletId = WalletId.New();
        await SeedAsync(context, tenantId, actorId, walletId, Guid.NewGuid(), "[15]");
        var issuer = new PostgreSqlProviderEvidencePostingAuthorityIssuer(context);
        var request = CreateRequest(tenantId, actorId, walletId);
        await issuer.IssueAsync(request);

        await FluentActions.Invoking(() => issuer.IssueAsync(request with
                { Amount = new CoinAmount(CurrencyCode.HardCoin, request.Amount.Units + 1) }).AsTask())
            .Should().ThrowAsync<RiskDecisionReuseException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(request with
                { CapabilityName = "missing-capability", OperationFingerprint = "different-fingerprint" }).AsTask())
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(request with
                { TenantId = Guid.NewGuid(), OperationFingerprint = "other-tenant-fingerprint" }).AsTask())
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>();
        var capability = await context.Set<EconomyRegisteredCapabilityRow>()
            .SingleAsync(row => row.Name == "test-admin-withdrawal-provider-terminal");
        capability.AllowedTemplateKinds = "null";
        await context.SaveChangesAsync();
        await FluentActions.Invoking(() => issuer.IssueAsync(request with
                { OperationFingerprint = "null-template-policy" }).AsTask())
            .Should().ThrowAsync<RegisteredPostingCapabilityUnavailableException>();
        var allows = typeof(PostgreSqlProviderEvidencePostingAuthorityIssuer)
            .GetMethod("Allows", BindingFlags.NonPublic | BindingFlags.Static)!;
        FluentActions.Invoking(() => allows.Invoke(null, ["{", request.TemplateKind]))
            .Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<RegisteredPostingCapabilityUnavailableException>()
            .Which.Message.Should().Contain("invalid template policy");
        (await context.Set<EconomyRiskDecisionRow>().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_RejectsUnsafeShapesBeforeDatabaseAccess()
    {
        await using var database = await EconomyPostgreSqlTestDatabase.CreateAsync("provider_evidence_authority_shapes");
        await using var context = CreateContext(database.ConnectionString);
        var issuer = new PostgreSqlProviderEvidencePostingAuthorityIssuer(context);
        var valid = CreateRequest(Guid.NewGuid(), Guid.NewGuid(), WalletId.New());

        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { CapabilityName = " " }).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { TenantId = Guid.Empty }).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { TemplateKind = PostingTemplateKind.Spend }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with
                { Amount = new CoinAmount(CurrencyCode.SoftCoin, 1) }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { KillSwitchEpoch = -1 }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { PolicyVersion = default }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { ReserveVersion = default }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { ReserveAuthorizationEpoch = 0 }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with { ExpiresAt = valid.IssuedAt }).AsTask())
            .Should().ThrowAsync<ArgumentException>();
        await FluentActions.Invoking(() => issuer.IssueAsync(valid with
                { EvidenceHash = new string('x', 129) }).AsTask())
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
        FluentActions.Invoking(() => new PostgreSqlProviderEvidencePostingAuthorityIssuer(null!))
            .Should().Throw<ArgumentNullException>();
        var proxy = DispatchProxy.Create<IApplicationDbContext, NonRelationalContextProxy>();
        FluentActions.Invoking(() => new PostgreSqlProviderEvidencePostingAuthorityIssuer(proxy))
            .Should().Throw<InvalidOperationException>();
    }

    private static ProviderEvidencePostingAuthorityRequest CreateRequest(
        Guid tenantId,
        Guid actorId,
        WalletId walletId) => new(
        "test-admin-withdrawal-provider-terminal",
        tenantId,
        actorId,
        walletId,
        PostingTemplateKind.AdminWithdrawalSuccess,
        new CoinAmount(CurrencyCode.HardCoin, 500),
        new PolicyVersion(3),
        new ReserveVersion(4),
        5,
        6,
        "0123456789abcdef",
        "provider-reference-hash",
        "evidence-hash",
        Now,
        Now.AddMinutes(5));

    private static async Task SeedAsync(
        ApplicationDbContext context,
        Guid tenantId,
        Guid actorId,
        WalletId walletId,
        Guid capabilityId,
        string allowedTemplates)
    {
        context.Set<EconomyWalletRow>().Add(new EconomyWalletRow
        {
            Id = walletId.Value,
            OwnerId = actorId,
            TenantId = tenantId,
            State = WalletLifecycleState.Active,
            CreatedAt = Now.AddDays(-1)
        });
        context.Set<EconomyRegisteredCapabilityRow>().Add(new EconomyRegisteredCapabilityRow
        {
            Id = capabilityId,
            Name = "test-admin-withdrawal-provider-terminal",
            AllowedTemplateKinds = allowedTemplates,
            IsEnabled = true,
            CreatedAt = Now.AddDays(-1)
        });
        await context.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext(string connectionString) => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options);

    private class NonRelationalContextProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException();
    }
}
