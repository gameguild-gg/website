using GameGuild.Identity.Tenants;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.TestingLab;

public sealed class TestingLabModelConfiguration : IModelConfiguration
{
    public void Configure(ModelBuilder modelBuilder)
    {
        ConfigureTestingEventTemplates(modelBuilder);
        ConfigureTestingEvent(modelBuilder);
        ConfigureTestingEventSlot(modelBuilder);
        ConfigureTestingSlotRegistration(modelBuilder);
        ConfigureTestingProjectApplication(modelBuilder);
        ConfigureTestingQuestionnaireRevision(modelBuilder);
        ConfigureTestingCommitteeMember(modelBuilder);
        ConfigureTestingApplicationVote(modelBuilder);
        ConfigureTestingFeedbackObligation(modelBuilder);
        ConfigureTestingRequest(modelBuilder);
        ConfigureTestingSession(modelBuilder);
        ConfigureTestingLocation(modelBuilder);
        ConfigureTestingParticipant(modelBuilder);
        ConfigureTestingFeedback(modelBuilder);
        ConfigureTestingFeedbackForm(modelBuilder);
        ConfigureFeedbackQualityRating(modelBuilder);
        ConfigureSessionRegistration(modelBuilder);
        ConfigureSessionWaitlist(modelBuilder);
        ConfigureSessionProject(modelBuilder);
        ConfigureTestingLabSettings(modelBuilder);
    }

    private static void ConfigureTestingEventTemplates(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingEventTemplate>(builder =>
        {
            builder.ToTable("testing_event_templates");
            builder.HasKey(template => template.Id);
            builder.Property(template => template.Name).IsRequired().HasMaxLength(255);
            builder.Property(template => template.Description).HasMaxLength(1000);
            builder.HasIndex(template => new { template.TenantId, template.Name });
            builder.HasMany(template => template.Revisions)
                .WithOne(revision => revision.Template)
                .HasForeignKey(revision => revision.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TestingEventTemplateRevision>(builder =>
        {
            builder.ToTable("testing_event_template_revisions");
            builder.HasKey(revision => revision.Id);
            builder.Property(revision => revision.GeneralRules).IsRequired().HasMaxLength(20000);
            builder.Property(revision => revision.CandidateInstructions).IsRequired().HasMaxLength(20000);
            builder.Property(revision => revision.TesterInstructions).IsRequired().HasMaxLength(20000);
            builder.Property(revision => revision.ProjectApplicationSchemaJson).IsRequired().HasColumnType("jsonb");
            builder.Property(revision => revision.TesterRegistrationSchemaJson).IsRequired().HasColumnType("jsonb");
            builder.Property(revision => revision.DefaultMode).HasConversion<string>().HasMaxLength(40);
            builder.Property(revision => revision.DefaultApprovalMode).HasConversion<string>().HasMaxLength(40);
            builder.Ignore(revision => revision.ProjectApplicationSchema);
            builder.Ignore(revision => revision.TesterRegistrationSchema);
            builder.HasOne(revision => revision.CreatedByUser)
                .WithMany()
                .HasForeignKey(revision => revision.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(revision => new { revision.TemplateId, revision.RevisionNumber }).IsUnique();
        });
    }

    private static void ConfigureTestingSlotRegistration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingSlotRegistration>(builder =>
        {
            builder.ToTable("testing_slot_registrations");
            builder.HasKey(registration => registration.Id);
            builder.Property(registration => registration.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(registration => registration.Notes).HasMaxLength(1000);
            builder.Property(registration => registration.RegistrationResponseJson).HasColumnType("jsonb");
            builder.Ignore(registration => registration.RegistrationResponse);
            builder.HasOne(registration => registration.Event)
                .WithMany()
                .HasForeignKey(registration => registration.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(registration => registration.Slot)
                .WithMany()
                .HasForeignKey(registration => registration.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(registration => registration.User)
                .WithMany()
                .HasForeignKey(registration => registration.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(registration => registration.TenantId);
            builder.HasIndex(registration => new { registration.SlotId, registration.Status });
            builder.HasIndex(registration => new { registration.SlotId, registration.UserId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"Status\" <> 'Cancelled'")
                .HasDatabaseName("IX_testing_slot_registrations_active_slot_user");
            builder.HasIndex(registration => new { registration.SlotId, registration.WaitlistPosition })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"Status\" = 'Waitlisted'")
                .HasDatabaseName("IX_testing_slot_registrations_waitlist_position");
        });
    }
    private static void ConfigureTestingRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingRequest>(builder =>
        {
            builder.ToTable("testing_requests");
            builder.HasKey(request => request.Id);
            builder.Property(request => request.Title).IsRequired().HasMaxLength(255);
            builder.Property(request => request.DownloadUrl).HasMaxLength(1000);
            builder.Property(request => request.InstructionsUrl).HasMaxLength(500);
            builder.Property(request => request.InstructionsType).HasConversion<string>().HasMaxLength(40);
            builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(request => request.Priority).HasConversion<string>().HasMaxLength(40);
            builder.Property(request => request.Mode).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(request => request.ProjectVersion)
                .WithMany()
                .HasForeignKey(request => request.ProjectVersionId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(request => request.CreatedBy)
                .WithMany()
                .HasForeignKey(request => request.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTestingSession(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingSession>(builder =>
        {
            builder.ToTable("testing_sessions");
            builder.HasKey(session => session.Id);
            builder.Property(session => session.SessionName).IsRequired().HasMaxLength(255);
            builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(session => session.EventSlot)
                .WithMany()
                .HasForeignKey(session => session.EventSlotId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(session => session.TestingRequest)
                .WithMany(request => request.Sessions)
                .HasForeignKey(session => session.TestingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(session => session.Location)
                .WithMany(location => location.Sessions)
                .HasForeignKey(session => session.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(session => session.Manager)
                .WithMany()
                .HasForeignKey(session => session.ManagerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(session => session.CreatedBy)
                .WithMany()
                .HasForeignKey(session => session.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTestingLocation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingLocation>(builder =>
        {
            builder.ToTable("testing_locations");
            builder.HasKey(location => location.Id);
            builder.Property(location => location.Name).IsRequired().HasMaxLength(200);
            builder.Property(location => location.Address).HasMaxLength(500);
            builder.Property(location => location.City).HasMaxLength(100);
            builder.Property(location => location.State).HasMaxLength(100);
            builder.Property(location => location.PostalCode).HasMaxLength(20);
            builder.Property(location => location.Country).HasMaxLength(100);
            builder.Property(location => location.VirtualUrl).HasMaxLength(500);
            builder.Property(location => location.ContactEmail).HasMaxLength(255);
            builder.Property(location => location.ContactPhone).HasMaxLength(50);
            builder.Property(location => location.Status).HasConversion<string>().HasMaxLength(40);
            builder.Ignore(location => location.MaxTestersCapacity);
            builder.Ignore(location => location.EquipmentAvailable);
        });
    }

    private static void ConfigureTestingParticipant(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingParticipant>(builder =>
        {
            builder.ToTable("testing_participants");
            builder.HasKey(participant => participant.Id);
            builder.Property(participant => participant.Status).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(participant => participant.TestingRequest)
                .WithMany(request => request.Participants)
                .HasForeignKey(participant => participant.TestingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(participant => participant.User)
                .WithMany()
                .HasForeignKey(participant => participant.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureTestingFeedback(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingFeedback>(builder =>
        {
            builder.ToTable("testing_feedback");
            builder.HasKey(feedback => feedback.Id);
            builder.Property(feedback => feedback.TestingContext).HasConversion<string>().HasMaxLength(40);
            builder.Property(feedback => feedback.QualityRating).HasConversion<string>().HasMaxLength(40);
            builder.Property(feedback => feedback.ReportReason).HasMaxLength(500);
            builder.Property(feedback => feedback.StructuredResponsesJson).HasColumnType("jsonb");
            builder.Ignore(feedback => feedback.StructuredResponses);
            builder.HasOne(feedback => feedback.TestingRequest)
                .WithMany(request => request.Feedback)
                .HasForeignKey(feedback => feedback.TestingRequestId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(feedback => feedback.FeedbackForm)
                .WithMany(form => form.Feedback)
                .HasForeignKey(feedback => feedback.FeedbackFormId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(feedback => feedback.Event)
                .WithMany()
                .HasForeignKey(feedback => feedback.EventId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(feedback => feedback.Application)
                .WithMany()
                .HasForeignKey(feedback => feedback.ApplicationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TestingQuestionnaireRevision>()
                .WithMany()
                .HasForeignKey(feedback => feedback.QuestionnaireRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(feedback => new { feedback.EventId, feedback.ApplicationId, feedback.UserId });
            builder.HasOne(feedback => feedback.User)
                .WithMany()
                .HasForeignKey(feedback => feedback.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(feedback => feedback.Session)
                .WithMany(session => session.Feedback)
                .HasForeignKey(feedback => feedback.SessionId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(feedback => feedback.ReportedBy)
                .WithMany()
                .HasForeignKey(feedback => feedback.ReportedById)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Ignore(feedback => feedback.ReportedByUserId);
        });
    }

    private static void ConfigureTestingFeedbackForm(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingFeedbackForm>(builder =>
        {
            builder.ToTable("testing_feedback_forms");
            builder.HasKey(form => form.Id);
            builder.Property(form => form.Name).IsRequired().HasMaxLength(200);
            builder.Property(form => form.FormData).IsRequired();
            builder.Property(form => form.FormType).HasConversion<string>().HasMaxLength(40);
            builder.Property(form => form.Tags).HasMaxLength(500);
            builder.HasOne<TestingRequest>()
                .WithMany(request => request.FeedbackForms)
                .HasForeignKey(form => form.TestingRequestId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.Ignore(form => form.FormSchema);
        });
    }

    private static void ConfigureFeedbackQualityRating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeedbackQualityRating>(builder =>
        {
            builder.ToTable("feedback_quality_ratings");
            builder.HasKey(rating => rating.Id);
            builder.Property(rating => rating.Reason).HasMaxLength(500);
            builder.HasOne(rating => rating.Feedback)
                .WithMany(feedback => feedback.QualityRatings)
                .HasForeignKey(rating => rating.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(rating => rating.RatedBy)
                .WithMany()
                .HasForeignKey(rating => rating.RatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSessionRegistration(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionRegistration>(builder =>
        {
            builder.ToTable("session_registrations");
            builder.HasKey(registration => registration.Id);
            builder.Property(registration => registration.RegistrationType).HasConversion<string>().HasMaxLength(40);
            builder.Property(registration => registration.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(registration => registration.AttendanceStatus).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(registration => registration.Session)
                .WithMany(session => session.Registrations)
                .HasForeignKey(registration => registration.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(registration => registration.User)
                .WithMany()
                .HasForeignKey(registration => registration.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Ignore(registration => registration.RegistrationNotes);
            builder.Ignore(registration => registration.AttendedAt);
        });
    }

    private static void ConfigureSessionWaitlist(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionWaitlist>(builder =>
        {
            builder.ToTable("session_waitlist");
            builder.HasKey(waitlist => waitlist.Id);
            builder.Property(waitlist => waitlist.RegistrationType).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(waitlist => waitlist.Session)
                .WithMany()
                .HasForeignKey(waitlist => waitlist.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(waitlist => waitlist.User)
                .WithMany()
                .HasForeignKey(waitlist => waitlist.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSessionProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SessionProject>(builder =>
        {
            builder.ToTable("session_projects");
            builder.HasKey(project => project.Id);
            builder.HasOne(project => project.Session)
                .WithMany()
                .HasForeignKey(project => project.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(project => project.Project)
                .WithMany()
                .HasForeignKey(project => project.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(project => project.ProjectVersion)
                .WithMany()
                .HasForeignKey(project => project.ProjectVersionId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(project => project.RegisteredBy)
                .WithMany()
                .HasForeignKey(project => project.RegisteredById)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(project => new { project.SessionId, project.ProjectId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"IsActive\" = TRUE")
                .HasDatabaseName("IX_session_projects_active_pair");
            builder.HasIndex(project => project.TenantId);
        });
    }

    private static void ConfigureTestingLabSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingLabSettings>(builder =>
        {
            builder.ToTable("testing_lab_settings");
            builder.HasKey(settings => settings.Id);
            builder.Property(settings => settings.LabName).IsRequired().HasMaxLength(255);
            builder.Property(settings => settings.Description).HasMaxLength(1000);
            builder.Property(settings => settings.Timezone).IsRequired().HasMaxLength(50);
            builder.Property(settings => settings.VersionSubmissionPolicy).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(settings => settings.Tenant)
                .WithMany()
                .HasForeignKey(settings => settings.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTestingEvent(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingEvent>(builder =>
        {
            builder.ToTable("testing_events");
            builder.HasKey(testingEvent => testingEvent.Id);
            builder.Property(testingEvent => testingEvent.Name).IsRequired().HasMaxLength(255);
            builder.Property(testingEvent => testingEvent.Description).HasMaxLength(2000);
            builder.Property(testingEvent => testingEvent.TimeZoneId).IsRequired().HasMaxLength(100);
            builder.Property(testingEvent => testingEvent.Mode).HasConversion<string>().HasMaxLength(40);
            builder.Property(testingEvent => testingEvent.ApprovalMode).HasConversion<string>().HasMaxLength(40);
            builder.Property(testingEvent => testingEvent.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(testingEvent => testingEvent.LearningCompletionRequirement).HasConversion<string>().HasMaxLength(100);
            builder.Property(testingEvent => testingEvent.RecurrenceFrequency).HasConversion<string>().HasMaxLength(20);
            builder.Property(testingEvent => testingEvent.RecurrenceDaysOfWeek).HasMaxLength(64);
            builder.Property(testingEvent => testingEvent.GeneralRules).HasMaxLength(20000);
            builder.Property(testingEvent => testingEvent.CandidateInstructions).HasMaxLength(20000);
            builder.Property(testingEvent => testingEvent.TesterInstructions).HasMaxLength(20000);
            builder.Property(testingEvent => testingEvent.ProjectApplicationSchemaJson).HasColumnType("jsonb");
            builder.Property(testingEvent => testingEvent.TesterRegistrationSchemaJson).HasColumnType("jsonb");
            builder.Ignore(testingEvent => testingEvent.ProjectApplicationSchema);
            builder.Ignore(testingEvent => testingEvent.TesterRegistrationSchema);
            builder.HasIndex(testingEvent => testingEvent.SourceTemplateRevisionId);
            builder.HasOne(testingEvent => testingEvent.Manager)
                .WithMany()
                .HasForeignKey(testingEvent => testingEvent.ManagerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(testingEvent => testingEvent.Slots)
                .WithOne(slot => slot.Event)
                .HasForeignKey(slot => slot.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(testingEvent => testingEvent.Applications)
                .WithOne(application => application.Event)
                .HasForeignKey(application => application.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(testingEvent => testingEvent.CommitteeMembers)
                .WithOne(member => member.Event)
                .HasForeignKey(member => member.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(testingEvent => testingEvent.TenantId);
            builder.HasIndex(testingEvent => new { testingEvent.TenantId, testingEvent.Status, testingEvent.StartsAt });
            builder.HasIndex(testingEvent => new { testingEvent.RecurrenceSeriesId, testingEvent.RecurrenceOccurrence })
                .HasDatabaseName("IX_testing_events_recurrence_series_occurrence");
        });
    }

    private static void ConfigureTestingEventSlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingEventSlot>(builder =>
        {
            builder.ToTable("testing_event_slots");
            builder.HasKey(slot => slot.Id);
            builder.Property(slot => slot.Mode).HasConversion<string>().HasMaxLength(40);
            builder.Property(slot => slot.CampusName).HasMaxLength(200);
            builder.Property(slot => slot.RoomName).HasMaxLength(200);
            builder.Property(slot => slot.MeetingUrl).HasMaxLength(1000);
            builder.HasOne(slot => slot.Location)
                .WithMany()
                .HasForeignKey(slot => slot.LocationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(slot => slot.TenantId);
            builder.HasIndex(slot => new { slot.EventId, slot.StartsAt });
        });
    }

    private static void ConfigureTestingProjectApplication(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingProjectApplication>(builder =>
        {
            builder.ToTable("testing_project_applications");
            builder.HasKey(application => application.Id);
            builder.Property(application => application.PreferredAvailability).HasMaxLength(1000);
            builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(40);
            builder.Property(application => application.SubmissionVersionPolicy).HasConversion<string>().HasMaxLength(40);
            builder.Property(application => application.DecisionRationale).HasMaxLength(2000);
            builder.Property(application => application.SubmittedAssetReferenceIdsJson).HasMaxLength(10000);
            builder.Property(application => application.BriefJson).HasColumnType("jsonb");
            builder.Property(application => application.EventApplicationResponseJson).HasColumnType("jsonb");
            builder.Ignore(application => application.SubmittedAssetReferenceIds);
            builder.Ignore(application => application.Brief);
            builder.Ignore(application => application.EventApplicationResponse);
            builder.HasOne(application => application.Project)
                .WithMany()
                .HasForeignKey(application => application.ProjectId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(application => application.ProjectVersion)
                .WithMany()
                .HasForeignKey(application => application.ProjectVersionId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(application => application.SubmittedBy)
                .WithMany()
                .HasForeignKey(application => application.SubmittedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(application => application.AssignedSlot)
                .WithMany()
                .HasForeignKey(application => application.AssignedSlotId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(application => application.DecidedBy)
                .WithMany()
                .HasForeignKey(application => application.DecidedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasMany(application => application.Votes)
                .WithOne(vote => vote.Application)
                .HasForeignKey(vote => vote.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(application => application.QuestionnaireRevisions)
                .WithOne()
                .HasForeignKey(revision => revision.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(application => application.TenantId);
            builder.HasIndex(application => new { application.EventId, application.Status });
            builder.HasIndex(application => application.CurrentQuestionnaireRevisionId);
            builder.HasIndex(application => new { application.EventId, application.ProjectId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"Status\" NOT IN ('Rejected', 'Withdrawn')")
                .HasDatabaseName("IX_testing_project_applications_active_event_project");
        });
    }

    private static void ConfigureTestingQuestionnaireRevision(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingQuestionnaireRevision>(builder =>
        {
            builder.ToTable("testing_questionnaire_revisions");
            builder.HasKey(revision => revision.Id);
            builder.Property(revision => revision.SchemaJson).IsRequired().HasColumnType("jsonb");
            builder.Ignore(revision => revision.Schema);
            builder.HasOne<GameGuild.Identity.Users.User>()
                .WithMany()
                .HasForeignKey(revision => revision.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(revision => revision.TenantId);
            builder.HasIndex(revision => new { revision.ApplicationId, revision.RevisionNumber })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL");
        });
    }

    private static void ConfigureTestingCommitteeMember(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingCommitteeMember>(builder =>
        {
            builder.ToTable("testing_committee_members");
            builder.HasKey(member => member.Id);
            builder.HasOne(member => member.User)
                .WithMany()
                .HasForeignKey(member => member.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(member => member.TenantId);
            builder.HasIndex(member => new { member.EventId, member.UserId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL AND \"IsActive\" = TRUE")
                .HasDatabaseName("IX_testing_committee_members_active_event_user");
        });
    }

    private static void ConfigureTestingApplicationVote(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingApplicationVote>(builder =>
        {
            builder.ToTable("testing_application_votes");
            builder.HasKey(vote => vote.Id);
            builder.Property(vote => vote.Decision).HasConversion<string>().HasMaxLength(40);
            builder.Property(vote => vote.Comments).HasMaxLength(2000);
            builder.HasOne(vote => vote.Reviewer)
                .WithMany()
                .HasForeignKey(vote => vote.ReviewerId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(vote => vote.TenantId);
            builder.HasIndex(vote => new { vote.ApplicationId, vote.ReviewerId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_testing_application_votes_active_application_reviewer");
        });
    }

    private static void ConfigureTestingFeedbackObligation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestingFeedbackObligation>(builder =>
        {
            builder.ToTable("testing_feedback_obligations");
            builder.HasKey(obligation => obligation.Id);
            builder.Property(obligation => obligation.Status).HasConversion<string>().HasMaxLength(40);
            builder.HasOne<TestingEvent>()
                .WithMany()
                .HasForeignKey(obligation => obligation.EventId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<TestingEventSlot>()
                .WithMany()
                .HasForeignKey(obligation => obligation.SlotId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<TestingProjectApplication>()
                .WithMany()
                .HasForeignKey(obligation => obligation.ApplicationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<GameGuild.Identity.Users.User>()
                .WithMany()
                .HasForeignKey(obligation => obligation.TesterUserId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<TestingFeedback>()
                .WithMany()
                .HasForeignKey(obligation => obligation.FeedbackId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<TestingQuestionnaireRevision>()
                .WithMany()
                .HasForeignKey(obligation => obligation.QuestionnaireRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(obligation => obligation.TenantId);
            builder.HasIndex(obligation => obligation.Status);
            builder.HasIndex(obligation => new { obligation.SlotId, obligation.ApplicationId, obligation.TesterUserId })
                .IsUnique()
                .HasFilter("\"DeletedAt\" IS NULL")
                .HasDatabaseName("IX_testing_feedback_obligations_active_assignment");
        });
    }}
