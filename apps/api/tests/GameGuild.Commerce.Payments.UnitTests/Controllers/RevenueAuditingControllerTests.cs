using FluentAssertions;
using GameGuild.Commerce.Payments.Commands.AcknowledgeRevenueAnomalyAlert;
using GameGuild.Commerce.Payments.Commands.RunRevenueAnomalyDetection;
using GameGuild.Commerce.Payments.Commands.RunRevenueReconciliation;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Payments.UnitTests.Controllers;

public sealed class RevenueAuditingControllerTests
{
    [Fact]
    public async Task RunReconciliation_ShouldRejectNonAdminActors()
    {
        var (controller, sender) = CreateController(actorIsAdmin: false);

        var result = await controller.RunReconciliation(
            new RevenueAuditingController.RunRevenueReconciliationRequest(
                TenantId: null,
                Source: "erp",
                PeriodStartUtc: DateTimeOffset.UtcNow.AddDays(-1),
                PeriodEndUtc: DateTimeOffset.UtcNow,
                Lines: []),
            CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(
                It.IsAny<RunRevenueReconciliationCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task RunReconciliation_ShouldDispatchCommandAndReturnCreated_ForAdmins()
    {
        var run = new RevenueReconciliationRun { Status = RevenueReconciliationStatus.Completed };
        var (controller, sender) = CreateController(actorIsAdmin: true);
        sender
            .Setup(service => service.Send(
                It.Is<RunRevenueReconciliationCommand>(command =>
                    command.Source == "erp" && command.Lines!.Count == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var result = await controller.RunReconciliation(
            new RevenueAuditingController.RunRevenueReconciliationRequest(
                TenantId: null,
                Source: "erp",
                PeriodStartUtc: DateTimeOffset.UtcNow.AddDays(-1),
                PeriodEndUtc: DateTimeOffset.UtcNow,
                Lines:
                [
                    new RevenueAuditingController.StatementLineRequest(
                        "ref-1",
                        10m,
                        "USD",
                        DateTimeOffset.UtcNow)
                ]),
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedAtRouteResult>().Subject;
        created.StatusCode.Should().Be(StatusCodes.Status201Created);
        created.Value.Should().BeSameAs(run);
        sender.VerifyAll();
    }

    [Fact]
    public async Task GetRunById_ShouldTranslateMissingRunTo404()
    {
        var (controller, sender) = CreateController(actorIsAdmin: true);
        sender
            .Setup(service => service.Send(It.IsAny<Queries.RevenueAuditing.GetRevenueReconciliationRunByIdQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("missing"));

        var result = await controller.GetRunById(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DetectAnomalies_ShouldRejectNonAdminActors()
    {
        var (controller, sender) = CreateController(actorIsAdmin: false);

        var result = await controller.DetectAnomalies(null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(
                It.IsAny<RunRevenueAnomalyDetectionCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DetectAnomalies_ShouldReturnCreatedAlertCount()
    {
        var (controller, sender) = CreateController(actorIsAdmin: true);
        sender
            .Setup(service => service.Send(It.IsAny<RunRevenueAnomalyDetectionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var result = await controller.DetectAnomalies(new RevenueAuditingController.DetectRevenueAnomaliesRequest(null, null), CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<RevenueAuditingController.AnomalyDetectionResult>()
            .Which.AlertsCreated.Should().Be(4);
    }

    [Fact]
    public async Task AcknowledgeAlert_ShouldRejectNonAdminActors()
    {
        var (controller, sender) = CreateController(actorIsAdmin: false);

        var result = await controller.AcknowledgeAlert(Guid.NewGuid(), null, CancellationToken.None);

        result.Should().BeOfType<ObjectResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        sender.Verify(
            service => service.Send(
                It.IsAny<AcknowledgeRevenueAnomalyAlertCommand>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AcknowledgeAlert_ShouldTranslateMissingAlertTo404()
    {
        var (controller, sender) = CreateController(actorIsAdmin: true);
        sender
            .Setup(service => service.Send(It.IsAny<AcknowledgeRevenueAnomalyAlertCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new KeyNotFoundException("missing"));

        var result = await controller.AcknowledgeAlert(Guid.NewGuid(), null, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRuns_ShouldNormalizePagingAndDispatchQuery()
    {
        var (controller, sender) = CreateController(actorIsAdmin: false);
        sender
            .Setup(service => service.Send(
                It.Is<Queries.RevenueAuditing.GetRevenueReconciliationRunsQuery>(query => query.Skip == 0 && query.Take == 100),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<RevenueReconciliationRun>([], 0, 0, 20));

        var result = await controller.GetRuns(null, skip: -5, take: 500, ct: CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        sender.VerifyAll();
    }

    [Fact]
    public async Task ExportReport_ShouldReturnFile_WithExportedPayload()
    {
        var (controller, sender) = CreateController(actorIsAdmin: true);
        sender
            .Setup(service => service.Send(It.IsAny<Queries.RevenueAuditingReports.ExportRevenueReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevenueReportExport(
                "revenue-audit.csv",
                "text/csv; charset=utf-8",
                "section,key,count,total"));

        var result = await controller.ExportReport(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow,
            "csv",
            null,
            CancellationToken.None);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("text/csv; charset=utf-8");
        file.FileDownloadName.Should().Be("revenue-audit.csv");
    }

    private static (RevenueAuditingController Controller, Mock<ISender> Sender) CreateController(bool actorIsAdmin, Guid? tenantId = null)
    {
        var sender = new Mock<ISender>();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(actorIsAdmin ? ["SystemAdmin"] : []),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Test",
            IsAuthenticated = true
        };
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);

        var controller = new RevenueAuditingController(sender.Object, actorAccessor.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    RequestServices = new ServiceCollection().BuildServiceProvider()
                }
            }
        };
        return (controller, sender);
    }
}
