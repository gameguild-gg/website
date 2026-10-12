using GameGuild.Compliance.Audit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class RequiredSecurityEventCommandTests
{
    [Fact]
    public async Task RequiredCaptureUsesOnlyTheOwningContextAndToken()
    {
        using var cancellation = new CancellationTokenSource();
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var rows = new Mock<DbSet<AuditLog>>();
        AuditLog? captured = null;
        rows.Setup(set => set.Add(It.IsAny<AuditLog>())).Callback<AuditLog>(row => captured = row);
        context.Setup(port => port.Set<AuditLog>()).Returns(rows.Object);
        context.Setup(port => port.SaveChangesAsync(cancellation.Token)).ReturnsAsync(1);
        var fixture = new CaptureFixture();
        fixture.Evaluator.Setup(port => port.EvaluateAsync(context.Object, It.IsAny<AuditLog>(),
            It.IsAny<ClassifiedSecurityEvent>(), cancellation.Token)).Returns(Task.CompletedTask);
        var request = Request();

        var result = await fixture.Logger.RecordInCommandAsync(context.Object, request, cancellation.Token);

        Assert.Equal(SecurityEventCaptureOutcome.PersistedToDatabase, result.Outcome);
        Assert.NotNull(captured);
        Assert.Equal(result.EventId, captured.Id);
        Assert.Equal(request.TenantId, captured.TenantId);
        Assert.Equal(request.UserId, captured.UserId);
        Assert.Equal(request.ActionType, captured.ActionType);
        context.Verify(port => port.SaveChangesAsync(cancellation.Token), Times.Once);
        fixture.Evaluator.Verify(port => port.EvaluateAsync(context.Object, captured,
            It.IsAny<ClassifiedSecurityEvent>(), cancellation.Token), Times.Once);
        fixture.AssertNoIndependentTransport();
    }

    [Fact]
    public async Task PersistenceFailurePropagatesWithoutRetrySpoolOrAnIndependentContext()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        context.Setup(port => port.Set<AuditLog>()).Returns(Mock.Of<DbSet<AuditLog>>());
        var failure = new InvalidOperationException("Synthetic owning-command audit failure.");
        context.Setup(port => port.SaveChangesAsync(CancellationToken.None)).ThrowsAsync(failure);
        var fixture = new CaptureFixture();
        fixture.Evaluator.Setup(port => port.EvaluateAsync(context.Object, It.IsAny<AuditLog>(),
            It.IsAny<ClassifiedSecurityEvent>(), CancellationToken.None)).Returns(Task.CompletedTask);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Logger.RecordInCommandAsync(context.Object, Request(), CancellationToken.None));

        Assert.Same(failure, actual);
        context.Verify(port => port.SaveChangesAsync(CancellationToken.None), Times.Once);
        fixture.AssertNoIndependentTransport();
    }

    [Fact]
    public async Task CancellationPreventsAnyCapture()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var fixture = new CaptureFixture();
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            fixture.Logger.RecordInCommandAsync(context.Object, Request(), new CancellationToken(canceled: true)));
        context.VerifyNoOtherCalls();
        fixture.Evaluator.VerifyNoOtherCalls();
        fixture.AssertNoIndependentTransport();
    }

    [Fact]
    public async Task NonSecurityRequestsCannotClaimRequiredCommandCapture()
    {
        var context = new Mock<IApplicationDbContext>(MockBehavior.Strict);
        var fixture = new CaptureFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Logger.RecordInCommandAsync(
            context.Object, new CreateAuditLogRequest { ActionType = "Synthetic.Unclassified", Category = AuditCategory.General }, CancellationToken.None));
        context.VerifyNoOtherCalls();
        fixture.Evaluator.VerifyNoOtherCalls();
        fixture.AssertNoIndependentTransport();
    }

    private static CreateAuditLogRequest Request() => new()
    {
        ActionType = "Authentication.MfaSignInVerified", Category = AuditCategory.Authentication,
        TenantId = Guid.NewGuid(), UserId = Guid.NewGuid(), Success = true
    };

    private sealed class CaptureFixture
    {
        private readonly Mock<IServiceScopeFactory> scopeFactory = new(MockBehavior.Strict);
        private readonly Mock<ISecurityEventSpool> spool = new(MockBehavior.Strict);
        private readonly Mock<IAuditService> fallback = new(MockBehavior.Strict);
        public Mock<ISecurityAlertRuleEvaluator> Evaluator { get; } = new(MockBehavior.Strict);
        public SecurityEventLogger Logger { get; }

        public CaptureFixture() => Logger = new SecurityEventLogger(scopeFactory.Object, new HttpContextAccessor(),
            spool.Object, Evaluator.Object, Options.Create(new SecurityEventPipelineOptions()), fallback.Object,
            NullLogger<SecurityEventLogger>.Instance);

        public void AssertNoIndependentTransport()
        {
            scopeFactory.VerifyNoOtherCalls();
            spool.VerifyNoOtherCalls();
            fallback.VerifyNoOtherCalls();
        }
    }
}
