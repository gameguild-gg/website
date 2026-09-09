using FluentAssertions;
using FluentValidation;
using GameGuild.CQRS;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class LessonInteractionTrackingTests
{
    [Fact]
    public void AddTimeSpentSeconds_ShouldRetainExactTimeAndMaintainMinuteCompatibility()
    {
        var interaction = CreateInteraction();

        interaction.AddTimeSpentSeconds(45);
        interaction.AddTimeSpentSeconds(75);

        interaction.TimeSpentSeconds.Should().Be(120);
        interaction.TimeSpentMinutes.Should().Be(2);
    }

    [Fact]
    public void AddTimeSpent_ShouldAlsoUpdateExactSeconds()
    {
        var interaction = CreateInteraction();

        interaction.AddTimeSpent(3);

        interaction.TimeSpentSeconds.Should().Be(180);
        interaction.TimeSpentMinutes.Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddTimeSpentSeconds_WhenDurationIsNotPositive_ShouldRejectIt(int seconds)
    {
        var interaction = CreateInteraction();

        var action = () => interaction.AddTimeSpentSeconds(seconds);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Reset_ShouldClearSecondAndMinuteCounters()
    {
        var interaction = CreateInteraction();
        interaction.AddTimeSpentSeconds(125);

        interaction.Reset();

        interaction.TimeSpentSeconds.Should().Be(0);
        interaction.TimeSpentMinutes.Should().Be(0);
    }

    [Fact]
    public void CreateEvent_WhenPayloadIsNotJson_ShouldRejectIt()
    {
        var action = () => ContentInteractionEvent.Create(
            Guid.NewGuid(),
            ContentInteractionEventType.QuizAnswered,
            payload: "not-json");

        action.Should().Throw<ArgumentException>()
            .WithMessage("Event payload must be valid JSON.*");
    }

    [Fact]
    public void CreateEvent_WhenTypeIsUnknown_ShouldRejectIt()
    {
        var action = () => ContentInteractionEvent.Create(
            Guid.NewGuid(),
            (ContentInteractionEventType)999);

        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("type");
    }

    [Fact]
    public void DatabaseContract_ShouldRestrictPersistedInteractionEventTypes()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var entity = modelBuilder.Entity<ContentInteractionEvent>();

        new ContentInteractionEventConfiguration().Configure(entity);

        var constraint = entity.Metadata.GetCheckConstraints()
            .Single(item => item.Name == "CK_content_interaction_events_Type_Valid");
        constraint.Sql.Should().Be("\"Type\" BETWEEN 0 AND 8");
    }

    [Fact]
    public void RecordEventCommandValidator_WhenTypeIsUnknown_ShouldRejectIt()
    {
        var validatorType = typeof(RecordContentInteractionEventCommand).Assembly
            .GetTypes()
            .SingleOrDefault(type =>
                !type.IsAbstract &&
                typeof(IValidator<RecordContentInteractionEventCommand>).IsAssignableFrom(type));
        validatorType.Should().NotBeNull();
        var validator = (IValidator<RecordContentInteractionEventCommand>)Activator.CreateInstance(validatorType!)!;
        var command = new RecordContentInteractionEventCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (ContentInteractionEventType)999);

        var result = validator.Validate(command);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error => error.PropertyName == nameof(command.Type));
    }

    [Fact]
    public void RecordEventCommandValidator_ShouldEnforcePositionPrecisionAndAcceptCanonicalProgress()
    {
        var validator = new RecordContentInteractionEventCommandValidator();
        var baseline = new RecordContentInteractionEventCommand(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ContentInteractionEventType.Paused);

        validator.Validate(baseline with
        {
            PositionSeconds = 999999999.999m,
            ProgressPercentage = Percent("99.99"),
        }).IsValid.Should().BeTrue();

        var positionOverflow = validator.Validate(baseline with { PositionSeconds = 1000000000m });
        positionOverflow.Errors.Should().Contain(error => error.PropertyName == nameof(baseline.PositionSeconds));

        var positionScale = validator.Validate(baseline with { PositionSeconds = 1.2345m });
        positionScale.Errors.Should().Contain(error => error.PropertyName == nameof(baseline.PositionSeconds));

        var progress = validator.Validate(baseline with { ProgressPercentage = Percent("99.99") });
        progress.Errors.Should().NotContain(error => error.PropertyName == nameof(baseline.ProgressPercentage));
    }

    [Fact]
    public void CreateEvent_ShouldEnforcePositionPrecisionAndPercentValueShouldRejectExcessScale()
    {
        var positionOverflow = () => ContentInteractionEvent.Create(
            Guid.NewGuid(),
            ContentInteractionEventType.Paused,
            positionSeconds: 1000000000m);
        var progressScale = () => Percent("99.999");

        positionOverflow.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("positionSeconds");
        progressScale.Should().Throw<FormatException>()
            .WithMessage("*at most two fractional digits*");
    }

    [Fact]
    public void Complete_ShouldSetCompletedStatusAndPreventProgressRegression()
    {
        var interaction = CreateInteraction();
        interaction.Status = ProgressStatus.InProgress;

        interaction.Complete();
        interaction.UpdateProgress(Percent("25"));

        interaction.IsCompleted.Should().BeTrue();
        interaction.Status.Should().Be(ProgressStatus.Completed);
        interaction.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public void ContentInteractionModel_ShouldAllowHistoryButOnlyOneActiveAttempt()
    {
        var modelBuilder = new ModelBuilder(new ConventionSet());
        var entity = modelBuilder.Entity<ContentInteraction>();
        new ContentInteractionConfiguration().Configure(entity);

        var attemptIndex = entity.Metadata.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                new[] { nameof(ContentInteraction.UserId), nameof(ContentInteraction.ContentId) }));

        attemptIndex.IsUnique.Should().BeTrue();
        attemptIndex.GetFilter().Should().Be("\"SubmittedAt\" IS NULL AND \"DeletedAt\" IS NULL");
    }

    [Fact]
    public void InteractionEventHandlers_ShouldDependOnAuthenticatedRequestContext()
    {
        var recordParameters = typeof(RecordContentInteractionEventCommandHandler)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);
        var queryParameters = typeof(GetContentInteractionEventsQueryHandler)
            .GetConstructors().Single().GetParameters().Select(parameter => parameter.ParameterType);

        recordParameters.Should().Contain(typeof(IRequestContextAccessor));
        queryParameters.Should().Contain(typeof(IRequestContextAccessor));
    }

    [Fact]
    public void RecordEndpoint_ShouldRequireReadPermissionForSelfTracking()
    {
        var method = typeof(LessonInteractionEventsController).GetMethod(
            nameof(LessonInteractionEventsController.Record));

        var permission = method!.GetCustomAttributes(
                typeof(RequireResourcePermissionAttribute<PermissionType, Program>),
                inherit: true)
            .Cast<RequireResourcePermissionAttribute<PermissionType, Program>>()
            .Single();
        permission.RequiredPermission.Should().Be(PermissionType.Read);
    }

    [Fact]
    public async Task RecordEventHandler_WhenHeartbeatIsRetried_ShouldBeIdempotent()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            Title = "Interactive lesson",
            Type = ProgramContentType.Lesson,
            LessonFormat = LessonContentFormat.Video,
        };
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));
        var command = new RecordContentInteractionEventCommand(
            lesson.ProgramId,
            interaction.Id,
            ContentInteractionEventType.Heartbeat,
            DurationSeconds: 45,
            PositionSeconds: 90.5m,
            ProgressPercentage: Percent("25"),
            Payload: """{"player":"html5"}""",
            IdempotencyKey: "heartbeat-0001");

        var first = await handler.Handle(command, CancellationToken.None);
        var retried = await handler.Handle(command, CancellationToken.None);

        first.Id.Should().Be(retried.Id);
        (await context.Set<ContentInteractionEvent>().CountAsync()).Should().Be(1);
        var persistedInteraction = await context.Set<ContentInteraction>().SingleAsync();
        persistedInteraction.TimeSpentSeconds.Should().Be(45);
        persistedInteraction.ProgressPercentage.Should().Be(Percent("25"));
        persistedInteraction.BookmarkPosition.Should().Be("video:90.5");

        var crossCourseRetry = () => handler.Handle(
            command with { ProgramId = Guid.NewGuid() },
            CancellationToken.None);
        await crossCourseRetry.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Content interaction was not found in this course.");
    }

    [Fact]
    public async Task RecordEventHandler_WhenIdempotencyKeyIsReusedForDifferentPayload_ShouldRejectIt()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));
        var first = new RecordContentInteractionEventCommand(
            lesson.ProgramId,
            interaction.Id,
            ContentInteractionEventType.Heartbeat,
            DurationSeconds: 30,
            IdempotencyKey: "heartbeat-conflict");
        await handler.Handle(first, CancellationToken.None);

        var action = () => handler.Handle(
            first with { DurationSeconds = 60 },
            CancellationToken.None);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Idempotency key was already used for a different interaction event.");
    }

    [Fact]
    public async Task RecordEventHandler_WhenConcurrentRetryWins_ShouldReturnThePersistedEvent()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = TrackingTestDbContext.Create(databaseName, databaseRoot);
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var command = new RecordContentInteractionEventCommand(
            lesson.ProgramId,
            interaction.Id,
            ContentInteractionEventType.Heartbeat,
            DurationSeconds: 30,
            IdempotencyKey: "heartbeat-race");
        var winningEvent = ContentInteractionEvent.Create(
            interaction.Id,
            command.Type,
            command.DurationSeconds,
            idempotencyKey: command.IdempotencyKey);
        context.BeforeEventSaveAsync = async cancellationToken =>
        {
            await using var winningContext = TrackingTestDbContext.Create(databaseName, databaseRoot);
            var winningInteraction = await winningContext.Set<ContentInteraction>()
                .SingleAsync(item => item.Id == interaction.Id, cancellationToken);
            winningInteraction.AddTimeSpentSeconds(command.DurationSeconds!.Value);
            winningContext.Set<ContentInteractionEvent>().Add(winningEvent);
            await winningContext.SaveChangesAsync(cancellationToken);
        };
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));

        var result = await handler.Handle(command, CancellationToken.None);

        result.Id.Should().Be(winningEvent.Id);
        await using var verificationContext = TrackingTestDbContext.Create(databaseName, databaseRoot);
        (await verificationContext.Set<ContentInteractionEvent>().CountAsync()).Should().Be(1);
        (await verificationContext.Set<ContentInteraction>().SingleAsync()).TimeSpentSeconds.Should().Be(30);
    }

    [Fact]
    public async Task RecordEventHandler_WhenConcurrentRetryUsesDifferentPayload_ShouldRejectIt()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = TrackingTestDbContext.Create(databaseName, databaseRoot);
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var incoming = new RecordContentInteractionEventCommand(
            lesson.ProgramId,
            interaction.Id,
            ContentInteractionEventType.Heartbeat,
            DurationSeconds: 60,
            IdempotencyKey: "heartbeat-race-conflict");
        var winningEvent = ContentInteractionEvent.Create(
            interaction.Id,
            incoming.Type,
            durationSeconds: 30,
            idempotencyKey: incoming.IdempotencyKey);
        context.BeforeEventSaveAsync = async cancellationToken =>
        {
            await using var winningContext = TrackingTestDbContext.Create(databaseName, databaseRoot);
            var winningInteraction = await winningContext.Set<ContentInteraction>()
                .SingleAsync(item => item.Id == interaction.Id, cancellationToken);
            winningInteraction.AddTimeSpentSeconds(30);
            winningContext.Set<ContentInteractionEvent>().Add(winningEvent);
            await winningContext.SaveChangesAsync(cancellationToken);
        };
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));

        var action = () => handler.Handle(incoming, CancellationToken.None);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Idempotency key was already used for a different interaction event.");
    }

    [Fact]
    public async Task RecordEventHandler_WhenCompletedLessonIsOpenedAgain_ShouldPreserveCompletion()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        interaction.Complete();
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));

        await handler.Handle(
            new RecordContentInteractionEventCommand(
                lesson.ProgramId,
                interaction.Id,
                ContentInteractionEventType.Opened),
            CancellationToken.None);

        interaction.IsCompleted.Should().BeTrue();
        interaction.Status.Should().Be(ProgressStatus.Completed);
        interaction.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public async Task RecordEventHandler_WhenInteractionBelongsToAnotherLearner_ShouldRejectIt()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var handler = new RecordContentInteractionEventCommandHandler(
            context,
            new TestRequestContextAccessor(Guid.NewGuid()));

        var action = () => handler.Handle(
            new RecordContentInteractionEventCommand(
                lesson.ProgramId,
                interaction.Id,
                ContentInteractionEventType.Opened),
            CancellationToken.None);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Content interaction was not found in this course.");
    }

    [Fact]
    public async Task EventsController_ShouldDispatchRecordThroughCqrs()
    {
        var programId = Guid.NewGuid();
        var interactionId = Guid.NewGuid();
        var expected = new ContentInteractionEventDto(
            Guid.NewGuid(),
            interactionId,
            ContentInteractionEventType.Opened,
            SystemClock.UtcNow,
            null,
            null,
            null,
            null,
            "open-1");
        var sender = new Mock<ISender>();
        sender.Setup(service => service.Send(
                It.Is<RecordContentInteractionEventCommand>(command =>
                    command.ProgramId == programId &&
                    command.InteractionId == interactionId &&
                    command.Type == ContentInteractionEventType.Opened &&
                    command.IdempotencyKey == "open-1"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = new LessonInteractionEventsController(sender.Object);

        var response = await controller.Record(
            programId,
            interactionId,
            new RecordContentInteractionEventRequest(
                ContentInteractionEventType.Opened,
                IdempotencyKey: "open-1"),
            CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(expected);
        sender.VerifyAll();
    }

    [Fact]
    public async Task GetEventsHandler_ShouldReturnTheInteractionTimelineInOccurrenceOrder()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            Title = "Lesson",
            Type = ProgramContentType.Lesson,
        };
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        var later = ContentInteractionEvent.Create(
            interaction.Id,
            ContentInteractionEventType.Paused,
            positionSeconds: 20,
            occurredAt: new DateTime(2026, 7, 15, 12, 1, 0, DateTimeKind.Utc));
        var earlier = ContentInteractionEvent.Create(
            interaction.Id,
            ContentInteractionEventType.Opened,
            occurredAt: new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        context.Set<ContentInteractionEvent>().AddRange(later, earlier);
        await context.SaveChangesAsync();
        var handler = new GetContentInteractionEventsQueryHandler(
            context,
            new TestRequestContextAccessor(interaction.UserId));

        var result = await handler.Handle(
            new GetContentInteractionEventsQuery(lesson.ProgramId, interaction.Id),
            CancellationToken.None);

        result.Select(item => item.Type).Should().Equal(
            ContentInteractionEventType.Opened,
            ContentInteractionEventType.Paused);
    }

    [Fact]
    public async Task GetEventsHandler_WhenInteractionBelongsToAnotherLearner_ShouldRejectIt()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var interaction = CreateInteraction();
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var handler = new GetContentInteractionEventsQueryHandler(
            context,
            new TestRequestContextAccessor(Guid.NewGuid()));

        var action = () => handler.Handle(
            new GetContentInteractionEventsQuery(lesson.ProgramId, interaction.Id),
            CancellationToken.None);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Content interaction was not found in this course.");
    }

    [Fact]
    public async Task StartContent_WhenCreatingInteraction_ShouldCopyTheEnrollmentUserId()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(enrollment.UserId));

        var interaction = await service.StartContentAsync(enrollment.Id, lesson.Id);

        interaction.UserId.Should().Be(enrollment.UserId);
    }

    [Fact]
    public async Task StartContent_WhenPreviousInteractionWasSubmitted_ShouldRepairOwnershipOnTheNewAttempt()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        var previousInteraction = CreateInteraction();
        previousInteraction.ProgramUserId = enrollment.Id;
        previousInteraction.ContentId = lesson.Id;
        previousInteraction.Content = lesson;
        previousInteraction.UserId = Guid.Empty;
        previousInteraction.SubmittedAt = SystemClock.UtcNow;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        context.Set<ContentInteraction>().Add(previousInteraction);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(enrollment.UserId));

        var newAttempt = await service.StartContentAsync(enrollment.Id, lesson.Id);
        var resumedAttempt = await service.StartContentAsync(enrollment.Id, lesson.Id);

        newAttempt.Id.Should().NotBe(previousInteraction.Id);
        newAttempt.UserId.Should().Be(enrollment.UserId);
        resumedAttempt.Id.Should().Be(newAttempt.Id);
    }

    [Fact]
    public async Task StartContent_WhenEnrollmentBelongsToAnotherLearner_ShouldRejectIt()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(Guid.NewGuid()));

        var action = () => service.StartContentAsync(enrollment.Id, lesson.Id);

        await action.Should().ThrowAsync<RequestValidationException>()
            .WithMessage("Active course enrollment was not found.");
    }

    [Fact]
    public async Task StartContent_WhenCompletedLessonIsReopened_ShouldPreserveCompletion()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        var interaction = CreateInteraction();
        interaction.ProgramUserId = enrollment.Id;
        interaction.UserId = enrollment.UserId;
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        interaction.Complete();
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(enrollment.UserId));

        var reopened = await service.StartContentAsync(enrollment.Id, lesson.Id);

        reopened.Id.Should().Be(interaction.Id);
        reopened.IsCompleted.Should().BeTrue();
        reopened.Status.Should().Be(ProgressStatus.Completed);
        reopened.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public async Task CompleteContent_WhenReopened_ShouldPreserveCompletion()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        var interaction = CreateInteraction();
        interaction.ProgramUserId = enrollment.Id;
        interaction.UserId = enrollment.UserId;
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(enrollment.UserId));

        var completed = await service.CompleteContentAsync(interaction.Id);
        var reopened = await service.StartContentAsync(enrollment.Id, lesson.Id);

        completed.IsCompleted.Should().BeTrue();
        reopened.IsCompleted.Should().BeTrue();
        reopened.Status.Should().Be(ProgressStatus.Completed);
        reopened.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public async Task SubmitContent_ShouldSetTheDomainCompletionFlag()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        var interaction = CreateInteraction();
        interaction.ProgramUserId = enrollment.Id;
        interaction.UserId = enrollment.UserId;
        interaction.ContentId = lesson.Id;
        interaction.Content = lesson;
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        context.Set<ContentInteraction>().Add(interaction);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(interaction.UserId));

        var submitted = await service.SubmitContentAsync(interaction.Id, "submission");

        submitted.IsCompleted.Should().BeTrue();
        submitted.Status.Should().Be(ProgressStatus.Completed);
        submitted.ProgressPercentage.Should().Be(PercentValue.Hundred);
    }

    [Fact]
    public async Task SubmitContent_WhenSurveyPayloadIsMalformed_ShouldReject()
    {
        await using var context = TrackingTestDbContext.Create();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Survey", Type = ProgramContentType.Survey };
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = survey.ProgramId, UserId = Guid.NewGuid(), JoinedAt = SystemClock.UtcNow };
        var interaction = new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = enrollment.Id, UserId = enrollment.UserId, ContentId = survey.Id, Content = survey };
        context.AddRange(survey, enrollment, interaction);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(enrollment.UserId));

        Func<Task> action = () => service.SubmitContentAsync(interaction.Id, """{"kind":"survey","answers":[]}""");

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SubmitContent_WhenNonSurvey_ShouldPreserveUnrelatedTrackedChanges()
    {
        await using var context = TrackingTestDbContext.Create();
        var lesson = CreateLesson();
        var unrelated = CreateLesson();
        unrelated.ProgramId = lesson.ProgramId;
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = lesson.ProgramId, UserId = Guid.NewGuid(), JoinedAt = SystemClock.UtcNow };
        var interaction = new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = enrollment.Id, UserId = enrollment.UserId, ContentId = lesson.Id, Content = lesson };
        context.AddRange(lesson, unrelated, enrollment, interaction);
        await context.SaveChangesAsync();
        unrelated.Title = "Pending unrelated change";
        context.Entry(unrelated).State.Should().Be(EntityState.Modified);
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(enrollment.UserId));

        await service.SubmitContentAsync(interaction.Id, "legacy lesson submission");

        context.Entry(unrelated).State.Should().Be(EntityState.Modified);
        await context.SaveChangesAsync();
        (await context.Set<ProgramContent>().SingleAsync(item => item.Id == unrelated.Id)).Title.Should().Be("Pending unrelated change");
    }

    [Fact]
    public async Task StartContent_WhenSurveyAllowsOnlyOneResponse_ShouldRejectAnotherAttempt()
    {
        await using var context = TrackingTestDbContext.Create();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(AllowMultipleResponses: false));
        var enrollment = new ProgramUser { Id = Guid.NewGuid(), ProgramId = survey.ProgramId, UserId = Guid.NewGuid(), JoinedAt = SystemClock.UtcNow };
        var response = new ContentInteraction { Id = Guid.NewGuid(), ProgramUserId = enrollment.Id, UserId = enrollment.UserId, ContentId = survey.Id, Content = survey, SubmittedAt = SystemClock.UtcNow };
        context.AddRange(survey, enrollment, response);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(enrollment.UserId));

        Func<Task> action = () => service.StartContentAsync(enrollment.Id, survey.Id);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("This survey accepts only one response.");
    }

    [Fact]
    public async Task GetSurveyResponses_ShouldReturnIdentityFreeResultsForAnonymousSurvey()
    {
        await using var context = TrackingTestDbContext.Create();
        var tenantId = Guid.NewGuid();
        var program = new Program { Id = Guid.NewGuid(), Title = "Course", Slug = "course" , TenantId = tenantId };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        survey.SetActivitySettings(new SurveyActivitySettings(IsAnonymous: true, AllowMultipleResponses: true));
        var learner = new ProgramUser { Id = Guid.NewGuid(), ProgramId = survey.ProgramId, UserId = Guid.NewGuid(), JoinedAt = SystemClock.UtcNow };
        var response = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = learner.Id,
            UserId = learner.UserId,
            ContentId = survey.Id,
            Content = survey,
            SubmittedAt = SystemClock.UtcNow,
            SubmissionData = """{"kind":"survey","answers":{"anonymous":true}}""",
        };
        context.AddRange(program, survey, learner, response);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(Guid.NewGuid(), tenantId),
            CreatePermissions(PermissionType.Review));

        var results = await service.GetSurveyResponsesAsync(survey.ProgramId, survey.Id);

        var result = results.Should().ContainSingle().Subject;
        result.ResponseId.Should().Be(response.Id);
        result.Answers["anonymous"].GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task GetSurveyResponses_WhenProgramDoesNotMatch_ShouldReject()
    {
        await using var context = TrackingTestDbContext.Create();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Survey", Type = ProgramContentType.Survey };
        context.Add(survey);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(Guid.NewGuid(), Guid.NewGuid()), CreateManagerPermissions());

        Func<Task> action = () => service.GetSurveyResponsesAsync(Guid.NewGuid(), survey.Id);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task GetSurveyResponses_WhenActorIsNotManager_ShouldReject()
    {
        await using var context = TrackingTestDbContext.Create();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Survey", Type = ProgramContentType.Survey };
        context.Add(survey);
        await context.SaveChangesAsync();
        var service = new ContentInteractionService(context, new TestRequestContextAccessor(Guid.NewGuid(), Guid.NewGuid()), new Mock<IPermissionQueryService>().Object);

        Func<Task> action = () => service.GetSurveyResponsesAsync(survey.ProgramId, survey.Id);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Theory]
    [InlineData(PermissionType.Create)]
    [InlineData(PermissionType.Delete)]
    [InlineData(PermissionType.Edit)]
    public async Task GetSurveyResponses_WhenActorLacksProgramReviewPermission_ShouldReject(PermissionType grantedPermission)
    {
        await using var context = TrackingTestDbContext.Create();
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = Guid.NewGuid(), Title = "Survey", Type = ProgramContentType.Survey };
        context.Add(survey);
        await context.SaveChangesAsync();
        var managerId = Guid.NewGuid();
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(managerId, Guid.NewGuid()),
            CreatePermissions(grantedPermission));

        Func<Task> action = () => service.GetSurveyResponsesAsync(survey.ProgramId, survey.Id);

        await action.Should().ThrowAsync<RequestValidationException>();
    }

    [Fact]
    public async Task StartContent_WhenConcurrentRequestCreatesActiveAttempt_ShouldReturnWinner()
    {
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        await using var context = TrackingTestDbContext.Create(databaseName, databaseRoot);
        var lesson = CreateLesson();
        var enrollment = new ProgramUser
        {
            Id = Guid.NewGuid(),
            ProgramId = lesson.ProgramId,
            UserId = Guid.NewGuid(),
            JoinedAt = SystemClock.UtcNow,
        };
        context.Set<ProgramContent>().Add(lesson);
        context.Set<ProgramUser>().Add(enrollment);
        await context.SaveChangesAsync();
        var winner = new ContentInteraction
        {
            Id = Guid.NewGuid(),
            ProgramUserId = enrollment.Id,
            UserId = enrollment.UserId,
            ContentId = lesson.Id,
            Status = ProgressStatus.InProgress,
        };
        context.BeforeInteractionSaveAsync = async cancellationToken =>
        {
            await using var winningContext = TrackingTestDbContext.Create(databaseName, databaseRoot);
            winningContext.Set<ContentInteraction>().Add(winner);
            await winningContext.SaveChangesAsync(cancellationToken);
        };
        var service = new ContentInteractionService(
            context,
            new TestRequestContextAccessor(enrollment.UserId));

        var result = await service.StartContentAsync(enrollment.Id, lesson.Id);

        result.Id.Should().Be(winner.Id);
        await using var verificationContext = TrackingTestDbContext.Create(databaseName, databaseRoot);
        (await verificationContext.Set<ContentInteraction>().CountAsync()).Should().Be(1);
    }

    private static ProgramContent CreateLesson() =>
        new()
        {
            Id = Guid.NewGuid(),
            ProgramId = Guid.NewGuid(),
            Title = "Lesson",
            Type = ProgramContentType.Lesson,
        };

    private static ContentInteraction CreateInteraction() =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ContentId = Guid.NewGuid(),
            ProgramUserId = Guid.NewGuid(),
        };

    private static IPermissionQueryService CreateManagerPermissions()
    {
        var permissions = new Mock<IPermissionQueryService>();
        permissions.Setup(service => service.HasTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        return permissions.Object;
    }

    private static IPermissionQueryService CreatePermissions(PermissionType grantedPermission)
    {
        var permissions = new Mock<IPermissionQueryService>();
        permissions.Setup(service => service.HasTenantPermissionAsync(
                It.IsAny<Guid?>(), It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid? _, Guid? _, string permission, CancellationToken _) =>
                permission.EndsWith($".{grantedPermission}", StringComparison.Ordinal));
        return permissions.Object;
    }

    private sealed class TestRequestContextAccessor(Guid? currentUserId, Guid? currentTenantId = null) : IRequestContextAccessor
    {
        public Guid? CurrentUserId { get; } = currentUserId;

        public Guid? CurrentTenantId { get; } = currentTenantId;

        public bool IsAuthenticated => CurrentUserId.HasValue;

        public bool HasTenantContext => CurrentTenantId.HasValue;

        public Task<UserInfo?> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<UserInfo?>(CurrentUserId.HasValue
                ? new UserInfo(CurrentUserId.Value, "learner@example.com", "Learner", true)
                : null);

        public Task<TenantInfo?> GetCurrentTenantAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<TenantInfo?>(null);
    }

    private sealed class TrackingTestDbContext(DbContextOptions<TrackingTestDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Func<CancellationToken, Task>? BeforeEventSaveAsync { get; set; }

        public Func<CancellationToken, Task>? BeforeInteractionSaveAsync { get; set; }

        public static TrackingTestDbContext Create(
            string? databaseName = null,
            InMemoryDatabaseRoot? databaseRoot = null)
        {
            var builder = new DbContextOptionsBuilder<TrackingTestDbContext>();
            var resolvedName = databaseName ?? Guid.NewGuid().ToString();
            if (databaseRoot is null)
            {
                builder.UseInMemoryDatabase(resolvedName);
            }
            else
            {
                builder.UseInMemoryDatabase(resolvedName, databaseRoot);
            }

            var options = builder.Options;
            return new TrackingTestDbContext(options);
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var beforeInteractionSave = BeforeInteractionSaveAsync;
            if (beforeInteractionSave is not null &&
                ChangeTracker.Entries<ContentInteraction>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                BeforeInteractionSaveAsync = null;
                await beforeInteractionSave(cancellationToken);
                throw new DbUpdateException("Simulated concurrent active-attempt conflict.");
            }

            var beforeSave = BeforeEventSaveAsync;
            if (beforeSave is not null &&
                ChangeTracker.Entries<ContentInteractionEvent>()
                    .Any(entry => entry.State == EntityState.Added))
            {
                BeforeEventSaveAsync = null;
                await beforeSave(cancellationToken);
                throw new DbUpdateException("Simulated concurrent idempotency conflict.");
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.HasKey(program => program.Id);
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.HasKey(content => content.Id);
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
            });
            modelBuilder.Entity<ContentInteraction>(entity =>
            {
                entity.HasKey(interaction => interaction.Id);
                entity.Ignore(interaction => interaction.User);
                entity.HasOne(interaction => interaction.Content)
                    .WithMany()
                    .HasForeignKey(interaction => interaction.ContentId);
                entity.HasOne(interaction => interaction.ProgramUser)
                    .WithMany()
                    .HasForeignKey(interaction => interaction.ProgramUserId);
                entity.HasMany(interaction => interaction.ActivityGrades)
                    .WithOne(grade => grade.ContentInteraction)
                    .HasForeignKey(grade => grade.ContentInteractionId);
            });
            modelBuilder.Entity<ActivityGrade>(entity =>
            {
                entity.HasKey(grade => grade.Id);
                entity.Ignore(grade => grade.Student);
                entity.Ignore(grade => grade.Grader);
                entity.Ignore(grade => grade.ProgramUser);
                entity.Ignore(grade => grade.GraderProgramUser);
            });
            modelBuilder.Entity<ContentInteractionEvent>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.HasOne(item => item.Interaction)
                    .WithMany(interaction => interaction.Events)
                    .HasForeignKey(item => item.InteractionId);
            });
            modelBuilder.Entity<ProgramUser>(entity =>
            {
                entity.HasKey(item => item.Id);
                entity.Ignore(item => item.User);
                entity.Ignore(item => item.Program);
                entity.Ignore(item => item.ContentInteractions);
                entity.Ignore(item => item.ReceivedGrades);
                entity.Ignore(item => item.GivenGrades);
                entity.Ignore(item => item.ProgramRatings);
            });
        }
    }
}
