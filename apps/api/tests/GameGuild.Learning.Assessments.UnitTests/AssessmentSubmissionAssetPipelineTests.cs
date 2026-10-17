using FluentAssertions;
using GameGuild.Assets;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

/// <summary>
/// T10: submission media routes through the assets pipeline — the usage guard keeps
/// referenced assets alive (FilePayload asset ids, asset:// URIs, question payloads),
/// and the submit gate blocks acceptance of unsafe managed media (mirror of Blog T9).
/// </summary>
public sealed class AssessmentSubmissionAssetPipelineTests : IDisposable
{
    private readonly GuardDbContext _db = CreateContext();

    // ---- grader safety gate ----

    [Fact]
    public async Task Submit_Blocked_WhenManagedFileAssetRejected_ErrorNamesAssetId()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        var asset = AddAsset(ModerationStatus.Rejected);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: asset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Contain(asset.Id.ToString());
        Reload(submission.Id).Status.Should().Be(SubmissionStatus.InProgress,
            "an unsafe managed asset must not be accepted");
    }

    [Fact]
    public async Task Submit_Blocked_WhenManagedFileAssetVirusInfected_ErrorNamesAssetId()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        var asset = AddAsset(ModerationStatus.Approved, VirusScanStatus.Infected);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: asset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Contain(asset.Id.ToString());
    }

    [Fact]
    public async Task Submit_Blocked_WhenManagedFileAssetMissing_ErrorNamesAssetId()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        var ghostId = Guid.NewGuid();
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: ghostId.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Contain(ghostId.ToString(),
            "a FilePayload naming a managed asset id that does not resolve fails closed");
    }

    [Fact]
    public async Task Submit_Allowed_WhenManagedFileAssetSafe()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        var asset = AddAsset(ModerationStatus.Approved);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: asset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeTrue();
        Reload(submission.Id).Status.Should().Be(SubmissionStatus.Submitted);
    }

    [Fact]
    public async Task Submit_LegacyTextSubmission_PassesThroughUnchanged()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.Text);
        submission.SetPayload(
            new SubmitAssessmentRequest(TextPayload: "essay without any media"), SubmissionModality.Text);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeTrue();
        Reload(submission.Id).TextPayload.Should().Be("essay without any media");
    }

    [Fact]
    public async Task Submit_LegacyUrlStringSubmission_PassesThroughUnchanged()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.Url);
        submission.SetPayload(
            new SubmitAssessmentRequest(UrlPayload: "https://example.com/legacy"), SubmissionModality.Url);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeTrue();
        Reload(submission.Id).UrlPayload.Should().Be("https://example.com/legacy");
    }

    [Fact]
    public async Task Submit_Blocked_WhenMediaPayloadEmbedsRejectedAssetId()
    {
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.Media);
        var asset = AddAsset(ModerationStatus.Rejected);
        // MediaPayload requires an http(s) URL; a managed asset id embedded anywhere
        // in a payload string (e.g. proxy URL) is still gated.
        submission.SetPayload(
            new SubmitAssessmentRequest(MediaPayload: $"https://cdn.example.com/proxy?u={asset.Id}"),
            SubmissionModality.Media);
        await _db.SaveChangesAsync();

        var result = await CreateService().SubmitAsync(submission.Id);

        result.IsSuccess.Should().BeFalse();
        result.Error.Description.Should().Contain(asset.Id.ToString());
    }

    // ---- usage guard ----

    [Fact]
    public async Task Guard_InUse_WhenSubmissionFilePayloadCarriesManagedAssetId()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: asset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_NotInUse_AfterAssetRemovedFromPayload()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        var otherAsset = AddAsset(ModerationStatus.Approved);
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: otherAsset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Guard_InUse_WhenSubmissionPayloadCarriesAssetUri()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.Text);
        submission.SetPayload(
            new SubmitAssessmentRequest(TextPayload: $"![proof](asset://{asset.Id})"), SubmissionModality.Text);
        await _db.SaveChangesAsync();

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task Guard_IgnoresDeletedSubmission()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        var (_, submission) = await SeedSubmissionAsync(SubmissionModality.File);
        submission.SetPayload(
            new SubmitAssessmentRequest(FilePayload: asset.Id.ToString()), SubmissionModality.File);
        await _db.SaveChangesAsync();
        submission.Version = 1;
        submission.SoftDelete();
        await _db.SaveChangesAsync();

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task Guard_InUse_WhenQuestionPayloadCarriesAssetUri()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        var program = new Program { Id = Guid.NewGuid(), TenantId = null };
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            ProgramId = program.Id,
            Title = "quiz",
            Slug = $"quiz-{Guid.NewGuid():N}",
            JsonBody = $"{{\"questions\":[{{\"prompt\":\"<img src=\\\"asset://{asset.Id}\\\" />\"}}]}}",
        };
        var assessment = Assessment.Create(
            program.Id, "Quiz", AssessmentType.Quiz, Score(100), contentId: content.Id);
        _db.AddRange(program, content, assessment);
        await _db.SaveChangesAsync();

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeTrue(
            "author-side quiz question payloads embed asset:// URIs in ProgramContent.JsonBody");
    }

    [Fact]
    public async Task Guard_NotInUse_WhenNoReferenceExists()
    {
        var asset = AddAsset(ModerationStatus.Approved);
        await SeedSubmissionAsync(SubmissionModality.Text);

        var guard = new AssessmentSubmissionAssetUsageGuard(_db);
        (await guard.IsInUseAsync(asset.Id)).Should().BeFalse();
    }

    // ---- fixture ----

    private AssessmentSubmission Reload(Guid id) =>
        _db.Set<AssessmentSubmission>().AsNoTracking().Single(s => s.Id == id);

    private AssessmentService CreateService() => new(
        _db,
        Mock.Of<IProgramContentService>(),
        new RubricService(_db, NullLogger<RubricService>.Instance),
        NullLogger<AssessmentService>.Instance);

    /// <summary>Generic-path assessment (no content link, so SubmitAsync is reachable).</summary>
    private async Task<(Assessment Assessment, AssessmentSubmission Submission)> SeedSubmissionAsync(
        SubmissionModality modality)
    {
        var program = new Program { Id = Guid.NewGuid(), TenantId = null };
        var assessment = Assessment.Create(program.Id, "Upload proof", AssessmentType.Assignment, Score(100));
        assessment.SetDeliveryContract(
            SubmissionModality.Text | SubmissionModality.File | SubmissionModality.Url | SubmissionModality.Media,
            AssessmentPresentationMode.SingleStep);
        var submission = AssessmentSubmission.Start(assessment.Id, Guid.NewGuid(), Guid.NewGuid(), 1);
        _db.AddRange(program, assessment, submission);
        await _db.SaveChangesAsync();
        return (assessment, submission);
    }

    private AssetReference AddAsset(
        ModerationStatus moderation = ModerationStatus.Approved,
        VirusScanStatus virus = VirusScanStatus.Clean)
    {
        var content = new AssetContent("bucket", $"key-{Guid.NewGuid():N}", new string('a', 64), "image/png", 4, null, null)
        {
            VirusScanStatus = virus,
            ModerationStatus = moderation,
        };
        var reference = new AssetReference(
            content.Id, Guid.NewGuid(), "proof.png", AssetAccessPolicy.Private,
            nameof(AssessmentSubmission), Guid.NewGuid())
        {
            Content = content,
        };
        _db.AddRange(content, reference);
        _db.SaveChanges();
        return reference;
    }

    private static ScoreValue Score(int value) =>
        ScoreValue.FromPoints(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static GuardDbContext CreateContext() => new(
        new DbContextOptionsBuilder<GuardDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public void Dispose() => _db.Dispose();

    private sealed class GuardDbContext(DbContextOptions<GuardDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AssessmentsModelConfiguration().Configure(modelBuilder);
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<Assessment>(entity =>
            {
                entity.Ignore(item => item.InteractiveVideoCues);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.HasKey(content => content.Id);
                entity.Ignore(content => content.Program);
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
            });
            modelBuilder.Entity<AssetContent>(builder =>
            {
                builder.HasKey(content => content.Id);
                builder.Ignore(content => content.References);
                builder.Ignore(content => content.TransformedVersions);
            });
modelBuilder.Entity<AssetReference>(builder =>
            {
                builder.HasKey(reference => reference.Id);
                builder.Ignore(reference => reference.Reports);
                builder.Ignore(reference => reference.Localizations);
                builder.Ignore(reference => reference.Revisions);
                builder
                    .HasOne(reference => reference.Content)
                    .WithMany()
                    .HasForeignKey(reference => reference.AssetContentId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
