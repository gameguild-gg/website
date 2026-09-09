using System.Reflection;
using System.Security.Claims;
using GameGuild.Configuration.PresentationLayer.Authorization;
using GameGuild.Identity.Authorization;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.UnitTests.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using AuthorizationOptions = Microsoft.AspNetCore.Authorization.AuthorizationOptions;

namespace GameGuild.Notifications.UnitTests.Controllers;

public class EmailDeliveryAdminControllerTests
{
    private readonly NotificationsTestDbContext _context = CreateContext();
    private readonly EmailDeliveryAdminController _controller;

    public EmailDeliveryAdminControllerTests()
    {
        _controller = CreateController(_context);
    }

    // ── Authorization ────────────────────────────────────────────────────

    [Fact]
    public void Controller_And_Every_Action_Should_Require_The_Admin_Policy()
    {
        var controllerAttribute = typeof(EmailDeliveryAdminController)
            .GetCustomAttribute<AuthorizeAttribute>();
        controllerAttribute.Should().NotBeNull();
        controllerAttribute!.Policy.Should().Be(Policies.Admin);

        var actionMethods = typeof(EmailDeliveryAdminController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        actionMethods.Should().HaveCount(6);
        foreach (var method in actionMethods)
        {
            method.GetCustomAttribute<AllowAnonymousAttribute>().Should().BeNull(method.Name);
        }
    }

    [Fact]
    public async Task Admin_Policy_Should_Deny_Anonymous_And_Plain_Users_And_Allow_Admin_Roles()
    {
        var policyProvider = CreatePolicyProvider();
        using var serviceProvider = BuildAuthorizationServiceProvider(policyProvider);
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var plainUser = new ClaimsPrincipal(new ClaimsIdentity("Bearer", nameType: "sub", roleType: "role"));
        var admin = PrincipalWithRole("Admin");
        var systemAdmin = PrincipalWithRole("SystemAdmin");

        (await authorizationService.AuthorizeAsync(anonymous, null, Policies.Admin)).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(plainUser, null, Policies.Admin)).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(admin, null, Policies.Admin)).Succeeded.Should().BeTrue();
        (await authorizationService.AuthorizeAsync(systemAdmin, null, Policies.Admin)).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task Admin_Policy_Should_Deny_All_Users_When_The_Stored_Definition_Is_Missing()
    {
        var policyProvider = CreatePolicyProvider(includeAdminPolicy: false);
        using var serviceProvider = BuildAuthorizationServiceProvider(policyProvider);
        var authorizationService = serviceProvider.GetRequiredService<IAuthorizationService>();

        var anonymous = new ClaimsPrincipal(new ClaimsIdentity());
        var plainUser = new ClaimsPrincipal(new ClaimsIdentity("Bearer", nameType: "sub", roleType: "role"));
        var admin = PrincipalWithRole("Admin");
        var systemAdmin = PrincipalWithRole("SystemAdmin");

        (await authorizationService.AuthorizeAsync(anonymous, null, Policies.Admin)).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(plainUser, null, Policies.Admin)).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(admin, null, Policies.Admin)).Succeeded.Should().BeFalse();
        (await authorizationService.AuthorizeAsync(systemAdmin, null, Policies.Admin)).Succeeded.Should().BeFalse();
    }

    // ── Event feed ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetEmailEvents_Should_Return_Newest_First_With_Normalized_Email_Filter()
    {
        var early = EmailDeliveryEvent.Create("msg-1", "User@Example.com", EmailDeliveryEventType.Send, DateTime.UtcNow.AddHours(-2), "sns-1");
        var late = EmailDeliveryEvent.Create("msg-1", "user@example.com", EmailDeliveryEventType.Bounce, DateTime.UtcNow.AddHours(-1), "sns-2",
            bounceType: "Permanent", diagnosticCode: "5.1.1");
        var other = EmailDeliveryEvent.Create("msg-2", "other@example.com", EmailDeliveryEventType.Delivery, DateTime.UtcNow, "sns-3");
        _context.EmailDeliveryEvents.AddRange(early, late, other);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetEmailEvents(0, 20, email: "  USER@EXAMPLE.COM ");

        var page = actionResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailDeliveryEventDto>>().Subject;
        page.TotalCount.Should().Be(2);
        page.Items.Should().OnlyContain(e => e.RecipientEmail == "user@example.com");
        page.Items.First().EventType.Should().Be("Bounce");
        page.Items.First().BounceType.Should().Be("Permanent");
        page.Items.First().DiagnosticCode.Should().Be("5.1.1");
    }

    [Fact]
    public async Task GetEmailEvents_Should_Filter_By_EventType_And_ProviderMessageId()
    {
        _context.EmailDeliveryEvents.AddRange(
            EmailDeliveryEvent.Create("msg-a", "a@x.com", EmailDeliveryEventType.Open, DateTime.UtcNow, "sns-1"),
            EmailDeliveryEvent.Create("msg-b", "b@x.com", EmailDeliveryEventType.Complaint, DateTime.UtcNow, "sns-2"));
        await _context.SaveChangesAsync();

        var byType = await _controller.GetEmailEvents(0, 20, eventType: "complaint");
        var byMessageId = await _controller.GetEmailEvents(0, 20, providerMessageId: "msg-a");

        byType.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailDeliveryEventDto>>().Subject
            .Items.Single().ProviderMessageId.Should().Be("msg-b");
        byMessageId.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailDeliveryEventDto>>().Subject
            .Items.Single().EventType.Should().Be("Open");
    }

    [Fact]
    public async Task GetEmailEvents_Should_Return_400_For_Unknown_EventType()
    {
        var actionResult = await _controller.GetEmailEvents(0, 20, eventType: "Exploded");

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        objectResult.Value.Should().BeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task GetEmailEvents_Should_Cap_Payload_Preview_At_500_Chars()
    {
        _context.EmailDeliveryEvents.Add(EmailDeliveryEvent.Create(
            "msg-p", "p@x.com", EmailDeliveryEventType.Bounce, DateTime.UtcNow, "sns-p",
            payload: new string('x', 1200)));
        await _context.SaveChangesAsync();

        var page = (await _controller.GetEmailEvents(0, 20)).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailDeliveryEventDto>>().Subject;

        page.Items.Single().PayloadPreview.Should().HaveLength(500);
    }

    // ── Suppressions ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetSuppressions_Should_Return_Active_Only_By_Default_And_All_When_IncludeReleased()
    {
        var active = EmailSuppression.Create("active@x.com", EmailSuppressionReason.HardBounce, "Permanent");
        var released = EmailSuppression.Create("released@x.com", EmailSuppressionReason.Complaint);
        released.Release();
        _context.EmailSuppressions.AddRange(active, released);
        await _context.SaveChangesAsync();

        var activeOnly = (await _controller.GetSuppressions(0, 20)).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailSuppressionDto>>().Subject;
        var all = (await _controller.GetSuppressions(0, 20, includeReleased: true)).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<EmailSuppressionDto>>().Subject;

        activeOnly.TotalCount.Should().Be(1);
        activeOnly.Items.Single().EmailAddress.Should().Be("active@x.com");
        activeOnly.Items.Single().IsActive.Should().BeTrue();
        activeOnly.Items.Single().Reason.Should().Be("HardBounce");
        all.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task ReleaseSuppression_Should_Normalize_Input_And_Release()
    {
        _context.EmailSuppressions.Add(EmailSuppression.Create("blocked@example.com", EmailSuppressionReason.HardBounce));
        await _context.SaveChangesAsync();

        var actionResult = await _controller.ReleaseSuppression("  BLOCKED@Example.COM ");

        var response = actionResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<UnsuppressResponse>().Subject;
        response.WasActive.Should().BeTrue();
        response.EmailAddress.Should().Be("blocked@example.com");
        (await _context.EmailSuppressions.SingleAsync()).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ReleaseSuppression_Should_Be_Idempotent_When_Already_Released_Or_Unknown()
    {
        var suppression = EmailSuppression.Create("once@example.com", EmailSuppressionReason.Complaint);
        _context.EmailSuppressions.Add(suppression);
        await _context.SaveChangesAsync();

        var first = await _controller.ReleaseSuppression("once@example.com");
        var again = await _controller.ReleaseSuppression("once@example.com");
        var unknown = await _controller.ReleaseSuppression("never@example.com");

        first.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<UnsuppressResponse>().Subject.WasActive.Should().BeTrue();
        again.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<UnsuppressResponse>().Subject.WasActive.Should().BeFalse();
        unknown.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<UnsuppressResponse>().Subject.WasActive.Should().BeFalse();
    }

    // ── Dead letters ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDeadLetters_Should_Return_Only_DeadLettered_With_Filters()
    {
        var deadA = DeadLetteredNotification("Victim@X.com", NotificationType.Billing);
        var deadB = DeadLetteredNotification("other@x.com", NotificationType.System);
        var pending = Notification.Create(Guid.NewGuid(), NotificationType.Billing, NotificationChannel.Email, "P", "Pending", recipientEmail: "victim@x.com");
        _context.Notifications.AddRange(deadA, deadB, pending);
        await _context.SaveChangesAsync();

        var all = (await _controller.GetDeadLetters(0, 20)).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<DeadLetterDto>>().Subject;
        var byEmail = (await _controller.GetDeadLetters(0, 20, email: "VICTIM@x.com")).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<DeadLetterDto>>().Subject;
        var byType = (await _controller.GetDeadLetters(0, 20, type: "system")).Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeAssignableTo<PagedResult<DeadLetterDto>>().Subject;

        all.TotalCount.Should().Be(2);
        byEmail.TotalCount.Should().Be(1);
        byEmail.Items.Single().Id.Should().Be(deadA.Id);
        byEmail.Items.Single().LastError.Should().Be("suppressed: hard bounce");
        byType.Items.Single().Id.Should().Be(deadB.Id);
    }

    [Fact]
    public async Task GetDeadLetters_Should_Return_400_For_Unknown_Type()
    {
        var actionResult = await _controller.GetDeadLetters(0, 20, type: "NotAType");

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    // ── Requeue ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Requeue_Should_Move_Row_Back_To_Pending_Keeping_Audit()
    {
        var dead = DeadLetteredNotification("free@x.com", NotificationType.Billing);
        _context.Notifications.Add(dead);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Requeue(dead.Id);

        var response = actionResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<RequeueResponse>().Subject;
        response.DeliveryStatus.Should().Be("Pending");
        response.RequeueCount.Should().Be(1);

        var row = await _context.Notifications.SingleAsync(n => n.Id == dead.Id);
        row.DeliveryStatus.Should().Be(NotificationDeliveryStatus.Pending);
        row.RequeueCount.Should().Be(1);
        row.NextAttemptAt.Should().BeNull();
        row.LastError.Should().Be("suppressed: hard bounce");
        row.AttemptCount.Should().Be(2);
    }

    [Fact]
    public async Task Requeue_Should_Return_409_For_Non_DeadLettered_Row()
    {
        var pending = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.Email, "P", "M", recipientEmail: "a@x.com");
        _context.Notifications.Add(pending);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Requeue(pending.Id);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Notifications.Requeue.NotDeadLettered");
    }

    [Fact]
    public async Task Requeue_Should_Return_409_When_Suppression_Is_Active_For_Recipient()
    {
        var dead = DeadLetteredNotification("victim@x.com", NotificationType.Security);
        _context.Notifications.Add(dead);
        _context.EmailSuppressions.Add(EmailSuppression.Create("VICTIM@X.com", EmailSuppressionReason.Complaint));
        await _context.SaveChangesAsync();

        var actionResult = await _controller.Requeue(dead.Id);

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Title.Should().Be("Notifications.Requeue.Suppressed");

        var row = await _context.Notifications.SingleAsync(n => n.Id == dead.Id);
        row.DeliveryStatus.Should().Be(NotificationDeliveryStatus.DeadLettered);
        row.RequeueCount.Should().Be(0);
    }

    [Fact]
    public async Task Requeue_Should_Return_404_For_Unknown_Id()
    {
        var actionResult = await _controller.Requeue(Guid.NewGuid());

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // ── Timeline ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTimeline_Should_Return_Correlated_Events_In_Chronological_Order()
    {
        _context.EmailDeliveryEvents.AddRange(
            EmailDeliveryEvent.Create("corr-1", "u@x.com", EmailDeliveryEventType.Bounce, DateTime.UtcNow.AddHours(-1), "sns-b"),
            EmailDeliveryEvent.Create("corr-1", "u@x.com", EmailDeliveryEventType.Send, DateTime.UtcNow.AddHours(-2), "sns-s"),
            EmailDeliveryEvent.Create("corr-2", "u@x.com", EmailDeliveryEventType.Open, DateTime.UtcNow, "sns-o"));

        // T3 (dispatch integration) stores ProviderMessageId at send; seed it through EF's property API.
        var correlated = DeadLetteredNotification("u@x.com", NotificationType.System);
        _context.Notifications.Add(correlated);
        _context.Entry(correlated).Property(n => n.ProviderMessageId).CurrentValue = "corr-1";
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetTimeline(correlated.Id);

        var timeline = actionResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<NotificationTimelineDto>().Subject;
        timeline.ProviderMessageId.Should().Be("corr-1");
        timeline.Events.Select(e => e.EventType).Should().ContainInOrder("Send", "Bounce");
    }

    [Fact]
    public async Task GetTimeline_Should_Return_Empty_List_For_Null_Correlation()
    {
        var digestRow = Notification.Create(Guid.NewGuid(), NotificationType.System, NotificationChannel.Email, "Digest", "Bundle", recipientEmail: "u@x.com");
        _context.Notifications.Add(digestRow);
        await _context.SaveChangesAsync();

        var actionResult = await _controller.GetTimeline(digestRow.Id);

        var timeline = actionResult.Should().BeOfType<OkObjectResult>().Subject
            .Value.Should().BeOfType<NotificationTimelineDto>().Subject;
        timeline.ProviderMessageId.Should().BeNull();
        timeline.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTimeline_Should_Return_404_For_Unknown_Id()
    {
        var actionResult = await _controller.GetTimeline(Guid.NewGuid());

        var objectResult = actionResult.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static EmailDeliveryAdminController CreateController(NotificationsTestDbContext context)
    {
        // Passthrough resolver: the RecipientEmail column decides, matching RecipientEmailResolver's first branch.
        var resolver = new Mock<IRecipientEmailResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification n, CancellationToken _) => n.RecipientEmail);

        var service = new EmailDeliveryAdminService(
            new ApplicationDbContextAdapter(context),
            resolver.Object,
            NullLogger<EmailDeliveryAdminService>.Instance);
        var contextAdapter = new ApplicationDbContextAdapter(context);
        var handler = new EmailDeliveryMutationCommandHandler(
            service,
            Mock.Of<IEmailEventProcessor>(),
            contextAdapter);
        return new EmailDeliveryAdminController(service, new HandlerSender(handler));
    }

    private static Notification DeadLetteredNotification(string email, NotificationType type)
    {
        var notification = Notification.Create(null, type, NotificationChannel.Email, "Dead", "Letter", recipientEmail: email);
        notification.MarkDeliveryAttemptFailed("smtp 4.x.y", DateTime.UtcNow.AddHours(-3));
        notification.MarkDeliveryAttemptFailed("smtp 5.x.y", DateTime.UtcNow.AddHours(-2));
        notification.MarkDeadLettered("suppressed: hard bounce");
        return notification;
    }

    private static ClaimsPrincipal PrincipalWithRole(string role)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "Bearer"));

    private static DbAuthorizationPolicyProvider CreatePolicyProvider(bool includeAdminPolicy = true)
    {
        var versionStore = new Mock<ITenantSecurityVersionStore>();
        versionStore.Setup(v => v.GetVersionAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        var policyStore = new Mock<IPolicyDefinitionStore>();
        if (includeAdminPolicy)
        {
            policyStore.Setup(p => p.GetPolicyAsync(Policies.Admin, null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new PolicyDefinition
                {
                    PolicyName = Policies.Admin,
                    RequiredRoles = ["Admin", "SystemAdmin"]
                });
        }
        else
        {
            policyStore.Setup(p => p.GetPolicyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((PolicyDefinition?)null);
        }

        var serviceProvider = new Mock<System.IServiceProvider>();
        serviceProvider.Setup(sp => sp.GetService(typeof(ITenantSecurityVersionStore))).Returns(versionStore.Object);
        serviceProvider.Setup(sp => sp.GetService(typeof(IPolicyDefinitionStore))).Returns(policyStore.Object);

        var scope = new Mock<IServiceScope>();
        scope.SetupGet(s => s.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(f => f.CreateScope()).Returns(scope.Object);

        var policyCache = new Mock<IPolicyCache>();
        policyCache.Setup(c => c.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<long>())).Returns((AuthorizationPolicy?)null);

        return new DbAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()),
            policyCache.Object,
            new DefaultPolicyMerger(),
            scopeFactory.Object,
            Options.Create(new TenancyOptions()),
            NullLogger<DbAuthorizationPolicyProvider>.Instance);
    }

    private static ServiceProvider BuildAuthorizationServiceProvider(IAuthorizationPolicyProvider policyProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore();
        services.AddSingleton(policyProvider);
        return services.BuildServiceProvider();
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationsTestDbContext(options);
    }

    private sealed class ApplicationDbContextAdapter(NotificationsTestDbContext inner) : IApplicationDbContext
    {
        public DbSet<T> Set<T>() where T : class => inner.Set<T>();

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => inner.SaveChangesAsync(cancellationToken);

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Mock.Of<IDbContextTransaction>());
    }
}
