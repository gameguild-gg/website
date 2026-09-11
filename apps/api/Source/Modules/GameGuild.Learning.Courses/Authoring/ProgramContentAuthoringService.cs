using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Learning.Courses;

public interface IProgramContentAuthoringService
{
    Task<AuthoringDraftDto> GetOrCreateDraft(Guid programId, Guid contentId, Guid actorId, CancellationToken cancellationToken);
    Task<AuthoringDraftDto> SaveDraft(Guid programId, Guid contentId, int expectedRevision, AuthoringContentPayload payload, Guid actorId, CancellationToken cancellationToken);
    Task<PublishAuthoringResult> Publish(Guid programId, Guid contentId, int expectedRevision, Guid actorId, CancellationToken cancellationToken);
}

public sealed class ProgramContentPublicationAudit : EntityBase
{
    private ProgramContentPublicationAudit() { }

    public Guid ProgramId { get; private set; }
    public Guid ContentId { get; private set; }
    public Guid DraftId { get; private set; }
    public int DraftRevision { get; private set; }
    public int PreviousPublishedVersion { get; private set; }
    public int PublishedVersion { get; private set; }
    public Guid PublishedBy { get; private set; }
    public DateTimeOffset PublishedAt { get; private set; }

    public static ProgramContentPublicationAudit Create(
        ProgramContent content,
        ProgramContentDraft draft,
        Guid actorId,
        int nextPublishedVersion,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            TenantId = content.TenantId,
            ProgramId = content.ProgramId,
            ContentId = content.Id,
            DraftId = draft.Id,
            DraftRevision = draft.Revision,
            PreviousPublishedVersion = content.Version,
            PublishedVersion = nextPublishedVersion,
            PublishedBy = actorId,
            PublishedAt = now,
            CreatedAt = now.UtcDateTime,
            UpdatedAt = now.UtcDateTime,
        };
}

public sealed class ProgramContentAuthoringService(IApplicationDbContext db) : IProgramContentAuthoringService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuthoringDraftDto> GetOrCreateDraft(
        Guid programId,
        Guid contentId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateActor(actorId);
        var existing = await db.Set<ProgramContentDraft>()
            .SingleOrDefaultAsync(candidate => candidate.ContentId == contentId, cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            EnsureProgram(existing.ProgramId, programId);
            return ToDto(existing);
        }

        var content = await FindContent(programId, contentId, cancellationToken).ConfigureAwait(false);
        var now = DateTimeOffset.UtcNow;
        var payload = AuthoringContentPayload.From(content);
        var draft = ProgramContentDraft.Create(
            Guid.NewGuid(),
            programId,
            contentId,
            actorId,
            content.Version,
            JsonSerializer.Serialize(payload, JsonOptions),
            now);
        draft.TenantId = content.TenantId;
        db.Set<ProgramContentDraft>().Add(draft);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return ToDto(draft);
        }
        catch (DbUpdateException)
        {
            // The draft is shared per lesson. Two first-time editors may race on the
            // unique ContentId constraint; the winner's draft is the canonical one.
            if (db is DbContext context)
                context.Entry(draft).State = EntityState.Detached;

            var winner = await db.Set<ProgramContentDraft>()
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.ContentId == contentId, cancellationToken)
                .ConfigureAwait(false);
            if (winner is null)
                throw;

            EnsureProgram(winner.ProgramId, programId);
            return ToDto(winner);
        }
    }

    public async Task<AuthoringDraftDto> SaveDraft(
        Guid programId,
        Guid contentId,
        int expectedRevision,
        AuthoringContentPayload payload,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateActor(actorId);
        ArgumentNullException.ThrowIfNull(payload);
        var draft = await FindDraft(programId, contentId, cancellationToken).ConfigureAwait(false);
        draft.Update(expectedRevision, JsonSerializer.Serialize(payload, JsonOptions), actorId, DateTimeOffset.UtcNow);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentRevision = await db.Set<ProgramContentDraft>()
                .AsNoTracking()
                .Where(candidate => candidate.ContentId == contentId)
                .Select(candidate => candidate.Revision)
                .SingleAsync(cancellationToken)
                .ConfigureAwait(false);
            throw new AuthoringRevisionConflictException(expectedRevision, currentRevision);
        }
        return ToDto(draft);
    }

    public async Task<PublishAuthoringResult> Publish(
        Guid programId,
        Guid contentId,
        int expectedRevision,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        ValidateActor(actorId);
        var draft = await FindDraft(programId, contentId, cancellationToken).ConfigureAwait(false);
        if (draft.Revision != expectedRevision)
            throw new AuthoringRevisionConflictException(expectedRevision, draft.Revision);

        var content = await FindContent(programId, contentId, cancellationToken).ConfigureAwait(false);
        if (draft.BasePublishedVersion != content.Version)
            throw new AuthoringPublishedVersionConflictException(draft.BasePublishedVersion, content.Version);

        var basePublishedVersion = draft.BasePublishedVersion;
        var payload = Deserialize(draft.PayloadJson);
        ApplyPayload(content, payload);
        var now = DateTimeOffset.UtcNow;
        var nextPublishedVersion = checked(content.Version + 1);
        var audit = ProgramContentPublicationAudit.Create(content, draft, actorId, nextPublishedVersion, now);
        db.Set<ProgramContentPublicationAudit>().Add(audit);
        draft.Rebase(nextPublishedVersion, actorId, draft.PayloadJson, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            var currentDraft = await db.Set<ProgramContentDraft>()
                .AsNoTracking()
                .Where(candidate => candidate.ContentId == contentId && candidate.DeletedAt == null)
                .Select(candidate => new { candidate.Revision, candidate.BasePublishedVersion })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (currentDraft is null)
                throw new KeyNotFoundException("Authoring draft was not found.");
            if (currentDraft.Revision != expectedRevision)
                throw new AuthoringRevisionConflictException(expectedRevision, currentDraft.Revision);

            var currentPublishedVersion = await db.Set<ProgramContent>()
                .AsNoTracking()
                .Where(candidate => candidate.Id == contentId && candidate.DeletedAt == null)
                .Select(candidate => (int?)candidate.Version)
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Course content was not found.");
            if (currentPublishedVersion != basePublishedVersion ||
                currentDraft.BasePublishedVersion != currentPublishedVersion)
                throw new AuthoringPublishedVersionConflictException(
                    basePublishedVersion,
                    currentPublishedVersion);

            throw;
        }

        return new PublishAuthoringResult(ToDto(draft), content.ToDto());
    }

    private async Task<ProgramContent> FindContent(Guid programId, Guid contentId, CancellationToken cancellationToken)
    {
        var content = await db.Set<ProgramContent>()
            .SingleOrDefaultAsync(candidate => candidate.Id == contentId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Course content was not found.");
        EnsureProgram(content.ProgramId, programId);
        return content;
    }

    private async Task<ProgramContentDraft> FindDraft(Guid programId, Guid contentId, CancellationToken cancellationToken)
    {
        var draft = await db.Set<ProgramContentDraft>()
            .SingleOrDefaultAsync(candidate => candidate.ContentId == contentId && candidate.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new KeyNotFoundException("Authoring draft was not found.");
        EnsureProgram(draft.ProgramId, programId);
        return draft;
    }

    private static AuthoringDraftDto ToDto(ProgramContentDraft draft) => new(
        draft.Id,
        draft.ProgramId,
        draft.ContentId,
        Deserialize(draft.PayloadJson),
        draft.BasePublishedVersion,
        draft.Revision,
        draft.ETag,
        draft.LastEditedBy,
        draft.LastEditedAt);

    private static AuthoringContentPayload Deserialize(string json) =>
        JsonSerializer.Deserialize<AuthoringContentPayload>(json, JsonOptions)
        ?? throw new ValidationException("Authoring draft payload is invalid.");

    private static void ApplyPayload(ProgramContent content, AuthoringContentPayload payload)
    {
        content.Title = payload.Title.Trim();
        content.Slug = string.IsNullOrWhiteSpace(payload.Slug) ? payload.Title.ToSlugCase() : payload.Slug.Trim();
        content.Description = payload.Description;
        content.Type = ProgramContentMappingExtensions.NormalizeProfessorFacingType(payload.Type);
        content.Body = payload.Body;
        content.JsonBody = payload.JsonBody?.GetRawText();
        content.LessonFormat = payload.LessonFormat;
        content.IsRequired = payload.IsRequired;
        content.EstimatedMinutes = payload.EstimatedMinutes;
        content.EstimatedMinutesSource = payload.EstimatedMinutesSource;
        content.Visibility = payload.Visibility;
        if (payload.ActivitySettings is not null)
            content.SetActivitySettings(payload.ActivitySettings);
        content.NormalizeLearningContract();
        content.Touch();
    }

    private static void ValidateActor(Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated author is required.");
    }

    private static void EnsureProgram(Guid actual, Guid expected)
    {
        if (actual != expected)
            throw new KeyNotFoundException("Course content was not found in this course.");
    }
}

public sealed class AuthoringPublishedVersionConflictException(int expectedVersion, int currentVersion)
    : InvalidOperationException($"Published version {expectedVersion} is stale; current version is {currentVersion}.")
{
    public int ExpectedVersion { get; } = expectedVersion;
    public int CurrentVersion { get; } = currentVersion;
}

internal sealed class ProgramContentDraftConfiguration : IEntityTypeConfiguration<ProgramContentDraft>
{
    public void Configure(EntityTypeBuilder<ProgramContentDraft> builder)
    {
        builder.ToTable("program_content_drafts");
        builder.Property(candidate => candidate.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(candidate => candidate.Revision).IsConcurrencyToken();
        builder.Ignore(candidate => candidate.ETag);
        builder.HasIndex(candidate => candidate.ContentId).IsUnique();
        builder.HasIndex(candidate => new { candidate.TenantId, candidate.ProgramId });
        builder.HasOne<ProgramContent>().WithOne().HasForeignKey<ProgramContentDraft>(candidate => candidate.ContentId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProgramContentPublicationAuditConfiguration : IEntityTypeConfiguration<ProgramContentPublicationAudit>
{
    public void Configure(EntityTypeBuilder<ProgramContentPublicationAudit> builder)
    {
        builder.ToTable("program_content_publication_audits");
        builder.HasIndex(candidate => new { candidate.ContentId, candidate.PublishedAt });
        builder.HasIndex(candidate => new { candidate.TenantId, candidate.PublishedBy });
    }
}

internal sealed class AiAuthoringProposalConfiguration : IEntityTypeConfiguration<AiAuthoringProposal>
{
    public void Configure(EntityTypeBuilder<AiAuthoringProposal> builder)
    {
        builder.ToTable("ai_authoring_proposals");
        builder.Property(candidate => candidate.OriginalContent).HasColumnType("text");
        builder.Property(candidate => candidate.ProposedContent).HasColumnType("text").IsRequired();
        builder.HasIndex(candidate => candidate.RunId).IsUnique();
        builder.HasIndex(candidate => new { candidate.ContentId, candidate.Status });
    }
}

internal sealed class AiAuthoringConversationConfiguration : IEntityTypeConfiguration<AiAuthoringConversation>
{
    public void Configure(EntityTypeBuilder<AiAuthoringConversation> builder)
    {
        builder.ToTable("ai_authoring_conversations");
        builder.HasIndex(candidate => new { candidate.TenantId, candidate.ContentId, candidate.AuthorId }).IsUnique();
        builder.HasIndex(candidate => new { candidate.AuthorId, candidate.LastMessageAt });
    }
}

internal sealed class AiAuthoringMessageConfiguration : IEntityTypeConfiguration<AiAuthoringMessage>
{
    public void Configure(EntityTypeBuilder<AiAuthoringMessage> builder)
    {
        builder.ToTable("ai_authoring_messages");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();
        builder.Property(candidate => candidate.Role).HasMaxLength(16).IsRequired();
        builder.Property(candidate => candidate.Content).HasColumnType("text").IsRequired();
        builder.HasIndex(candidate => new { candidate.ConversationId, candidate.CreatedAt });
        builder.HasIndex(candidate => candidate.RunId);
    }
}

internal sealed class AiAuthoringRunConfiguration : IEntityTypeConfiguration<AiAuthoringRun>
{
    public void Configure(EntityTypeBuilder<AiAuthoringRun> builder)
    {
        builder.ToTable("ai_authoring_runs");
        builder.Property(candidate => candidate.Instruction).HasColumnType("text").IsRequired();
        builder.Property(candidate => candidate.Selection).HasColumnType("text");
        builder.Property(candidate => candidate.ResponseText).HasColumnType("text");
        builder.Property(candidate => candidate.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(candidate => candidate.Provider).HasMaxLength(64);
        builder.Property(candidate => candidate.Model).HasMaxLength(256);
        builder.Property(candidate => candidate.ErrorCode).HasMaxLength(128);
        builder.HasIndex(candidate => new { candidate.TenantId, candidate.ActorId, candidate.IdempotencyKey }).IsUnique();
        builder.HasIndex(candidate => new { candidate.ContentId, candidate.ActorId, candidate.CreatedAt });
        builder.HasIndex(candidate => candidate.Status);
    }
}

internal sealed class AiAuthoringStreamEventConfiguration : IEntityTypeConfiguration<AiAuthoringStreamEvent>
{
    public void Configure(EntityTypeBuilder<AiAuthoringStreamEvent> builder)
    {
        builder.ToTable("ai_authoring_stream_events");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();
        builder.Property(candidate => candidate.Type).HasMaxLength(64).IsRequired();
        builder.Property(candidate => candidate.Status).HasMaxLength(32).IsRequired();
        builder.Property(candidate => candidate.Delta).HasColumnType("text");
        builder.Property(candidate => candidate.PayloadJson).HasColumnType("jsonb");
        builder.HasIndex(candidate => new { candidate.RunId, candidate.Sequence }).IsUnique();
    }
}
