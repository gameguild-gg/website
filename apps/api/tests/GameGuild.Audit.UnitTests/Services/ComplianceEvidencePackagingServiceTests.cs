using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class ComplianceEvidencePackagingServiceTests
{
    private readonly Guid _tenant = Guid.NewGuid();
    private readonly Guid _user = Guid.NewGuid();
    private readonly Mock<IActorContextAccessor> _actors = new();
    private readonly Mock<ICompliancePackagingRepository> _repository = new();
    private readonly Mock<IComplianceFrameworkCatalog> _catalog = new();
    private readonly Mock<IComplianceEvidenceDataSource> _source = new();
    private readonly Mock<IAuditService> _audit = new();
    private static readonly DateTime Start = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddDays(1);
    private static readonly DateTime Now = new(2026, 10, 3, 20, 0, 0, DateTimeKind.Utc);

    public ComplianceEvidencePackagingServiceTests()
    {
        SetActor("TenantAdmin");
        _catalog.Setup(item => item.Find("test-v1")).Returns(Template());
        _catalog.Setup(item => item.GetTemplates()).Returns([Template()]);
    }

    private void SetActor(string role, bool authenticated = true, bool tenant = true) =>
        _actors.SetupGet(item => item.ActorContext).Returns(new ActorContext
        {
            ActorKind = ActorKind.User, SubjectId = _user.ToString(), TenantId = tenant ? _tenant : null,
            IsAuthenticated = authenticated, Roles = new HashSet<string> { role }, Permissions = new HashSet<string>()
        });

    private ComplianceEvidencePackagingService Service(bool signingAvailable = true)
    {
        var options = new AuditSigningOptions();
        if (signingAvailable)
        {
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            options.ActiveKeyId = "test-key";
            options.Keys["test-key"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        }
        var engine = new ComplianceEvidenceValidationEngine();
        return new(_actors.Object, _repository.Object, _catalog.Object, _source.Object, engine,
            new ComplianceArtifactBuilder(engine, new EcdsaCryptographicSigningService(Options.Create(options))), _audit.Object, new FixedTime());
    }

    [Theory]
    [InlineData("User", true, true)]
    [InlineData("TenantAdmin", false, true)]
    [InlineData("SystemAdmin", true, false)]
    public async Task All_operations_require_an_authenticated_tenant_administrator(string role, bool authenticated, bool tenant)
    {
        SetActor(role, authenticated, tenant);
        var service = Service();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetTemplatesAsync(default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadDocumentAsync(Upload(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetDocumentAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListDocumentsAsync(0, 10, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReviewDocumentAsync(Guid.NewGuid(), Review(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.CreatePackageAsync(Request(Document()), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetPackageAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ListPackagesAsync(0, 10, default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DownloadPackageAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.VerifyPackageAsync(Guid.NewGuid(), default));
        _repository.VerifyNoOtherCalls();
        _catalog.VerifyNoOtherCalls();
        _source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_binds_actor_and_tenant_and_returns_metadata_without_document_bytes()
    {
        ComplianceEvidenceDocument? saved = null;
        _repository.Setup(item => item.AddDocumentAsync(It.IsAny<ComplianceEvidenceDocument>(), default))
            .Callback<ComplianceEvidenceDocument, CancellationToken>((document, _) => saved = document).Returns(Task.CompletedTask);
        var response = await Service().UploadDocumentAsync(Upload(), default);
        Assert.NotNull(response);
        Assert.NotNull(saved);
        Assert.Equal(_tenant, saved.TenantId);
        Assert.Equal(_user, response.UploadedByUserId);
        Assert.Equal(ComplianceDocumentReview.Pending, response.Review);
        Assert.Equal(1, response.Revision);
        Assert.Equal(Now, saved.CreatedAt);
        Assert.DoesNotContain("ContentBase64", JsonSerializer.Serialize(response), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source-body-secret", JsonSerializer.Serialize(response), StringComparison.Ordinal);
        _audit.Verify(item => item.LogAsync(It.Is<CreateAuditLogRequest>(entry => entry.UserId == _user && entry.TenantId == _tenant)), Times.Once);
    }

    [Theory]
    [InlineData("not-base64", "application/json")]
    [InlineData("e30=", "image/png")]
    [InlineData("W10=", "application/json")]
    [InlineData("aW52YWxpZA==", "application/pdf")]
    public async Task Upload_rejects_invalid_encoding_media_or_assessment_structure(string body, string mediaType)
    {
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() =>
            Service().UploadDocumentAsync(Upload() with { ContentBase64 = body, MediaType = mediaType }, default));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Upload_rejects_unknown_control_mappings()
    {
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => Service().UploadDocumentAsync(Upload() with { ControlIds = ["UNKNOWN"] }, default));
        _repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Review_uses_actor_identity_revision_and_preserves_immutable_content()
    {
        var document = Document();
        var original = document.Content.ToArray();
        _repository.Setup(item => item.GetDocumentAsync(_tenant, document.Id, default)).ReturnsAsync(document);
        var result = await Service().ReviewDocumentAsync(document.Id, Review(), default);
        Assert.NotNull(result);
        Assert.Equal(_user, result.ReviewedByUserId);
        Assert.Equal(Now, result.ReviewedAtUtc);
        Assert.Equal(2, result.Revision);
        Assert.Equal(ComplianceDocumentReview.Approved, result.Review);
        Assert.Equal(original, document.Content);
        _repository.Verify(item => item.SaveDocumentReviewAsync(document, default), Times.Once);
    }

    [Fact]
    public async Task Review_rejects_stale_revision_and_deficient_or_forged_assessments()
    {
        var document = Document();
        _repository.Setup(item => item.GetDocumentAsync(_tenant, document.Id, default)).ReturnsAsync(document);
        await Assert.ThrowsAsync<CompliancePackagingConcurrencyException>(() => Service().ReviewDocumentAsync(document.Id, Review() with { ExpectedRevision = 2 }, default));
        document.Content = Encoding.UTF8.GetBytes("{\"frameworkVersion\":\"v1\",\"assessmentStatus\":\"deficient\",\"owner\":\"Security\"}");
        document.ContentSha256 = Hash(document.Content);
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => Service().ReviewDocumentAsync(document.Id, Review(), default));
        document.ContentSha256 = new string('0', 64);
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => Service().ReviewDocumentAsync(document.Id, Review(), default));
        Assert.Equal(ComplianceDocumentReview.Pending, document.Review);
        _repository.Verify(item => item.SaveDocumentReviewAsync(It.IsAny<ComplianceEvidenceDocument>(), default), Times.Never);
    }

    [Fact]
    public async Task Review_propagates_repository_concurrency_failure()
    {
        var document = Document();
        _repository.Setup(item => item.GetDocumentAsync(_tenant, document.Id, default)).ReturnsAsync(document);
        _repository.Setup(item => item.SaveDocumentReviewAsync(document, default)).ThrowsAsync(new CompliancePackagingConcurrencyException());
        await Assert.ThrowsAsync<CompliancePackagingConcurrencyException>(() => Service().ReviewDocumentAsync(document.Id, Review(), default));
        _audit.Verify(item => item.LogAsync(It.IsAny<CreateAuditLogRequest>()), Times.Never);
    }

    [Fact]
    public async Task Review_of_screening_does_not_require_other_package_documents_to_already_exist()
    {
        var scenario = ComplianceFrameworkEvidenceTests.Gdpr();
        var snapshot = scenario.Documents.Single(item => item.Type == "gdpr-dpia-screening");
        var document = Document();
        document.TemplateId = scenario.Template.Id;
        document.Type = snapshot.Type;
        document.Content = snapshot.Content;
        document.ContentSha256 = snapshot.ContentSha256;
        document.ControlIdsJson = JsonSerializer.Serialize(snapshot.ControlIds);
        _catalog.Setup(item => item.Find(scenario.Template.Id)).Returns(scenario.Template);
        _repository.Setup(item => item.GetDocumentAsync(_tenant, document.Id, default)).ReturnsAsync(document);
        var result = await Service().ReviewDocumentAsync(document.Id, Review(), default);
        Assert.Equal(ComplianceDocumentReview.Approved, result!.Review);
        _repository.Verify(item => item.GetDocumentsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Missing_or_other_tenant_documents_return_not_found_before_collection()
    {
        _repository.Setup(item => item.GetDocumentsAsync(_tenant, It.IsAny<IReadOnlyList<Guid>>(), default)).ReturnsAsync([]);
        Assert.Null(await Service().CreatePackageAsync(Request(Document()), default));
        _source.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Prepare_persists_a_signed_immutable_snapshot_and_audits_verified_downloads()
    {
        var service = Service();
        var document = Document();
        document.Review = ComplianceDocumentReview.Approved;
        document.ReviewedByUserId = _user;
        document.ReviewedAtUtc = Now;
        document.Revision = 2;
        _repository.Setup(item => item.GetDocumentsAsync(_tenant, It.IsAny<IReadOnlyList<Guid>>(), default)).ReturnsAsync([document]);
        _source.Setup(item => item.CaptureAsync(_tenant, Start, End, It.IsAny<IReadOnlyList<ComplianceEvidenceKind>>(), default)).ReturnsAsync([Dataset()]);
        ComplianceSealedPackage? saved = null;
        _repository.Setup(item => item.AddPackageAsync(It.IsAny<ComplianceSealedPackage>(), default))
            .Callback<ComplianceSealedPackage, CancellationToken>((package, _) => saved = package).Returns(Task.CompletedTask);
        var prepared = await service.CreatePackageAsync(Request(document), default);
        Assert.NotNull(prepared);
        Assert.NotNull(saved);
        Assert.True(prepared.Summary.ReadyForAuditorReview);
        Assert.Equal(_tenant, saved.TenantId);
        Assert.Equal(_user, saved.PreparedByUserId);
        _repository.Setup(item => item.GetPackageAsync(_tenant, saved.Id, default)).ReturnsAsync(saved);
        var artifactBefore = saved.ArtifactContent.ToArray();
        document.Review = ComplianceDocumentReview.Rejected;
        document.Content[0] ^= 1;
        Assert.True((await service.VerifyPackageAsync(saved.Id, default))!.IsValid);
        Assert.Equal(artifactBefore, (await service.DownloadPackageAsync(saved.Id, default))!.Content);
        var loaded = await service.GetPackageAsync(saved.Id, default);
        Assert.True(loaded!.Summary.ReadyForAuditorReview);
        Assert.Equal(2, prepared.Manifest.Entries.Count(item => item.Path.StartsWith("documents/", StringComparison.Ordinal)));
        _audit.Verify(item => item.LogAsync(It.Is<CreateAuditLogRequest>(entry => entry.ActionType == "ComplianceEvidencePackageDownloaded" && entry.UserId == _user && entry.TenantId == _tenant)), Times.Once);
        saved.ManifestJson = "{}";
        Assert.False((await service.VerifyPackageAsync(saved.Id, default))!.IsValid);
        await Assert.ThrowsAsync<CompliancePackagingIntegrityException>(() => service.DownloadPackageAsync(saved.Id, default));
    }

    [Fact]
    public async Task Missing_signing_keys_never_persist_an_unsigned_success()
    {
        var document = Document();
        _repository.Setup(item => item.GetDocumentsAsync(_tenant, It.IsAny<IReadOnlyList<Guid>>(), default)).ReturnsAsync([document]);
        _source.Setup(item => item.CaptureAsync(_tenant, Start, End, It.IsAny<IReadOnlyList<ComplianceEvidenceKind>>(), default)).ReturnsAsync([Dataset()]);
        await Assert.ThrowsAsync<CompliancePackagingSigningUnavailableException>(() => Service(false).CreatePackageAsync(Request(document), default));
        _repository.Verify(item => item.AddPackageAsync(It.IsAny<ComplianceSealedPackage>(), default), Times.Never);
    }

    [Fact]
    public async Task Invalid_pagination_and_future_periods_are_rejected()
    {
        var service = Service();
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => service.ListDocumentsAsync(-1, 10, default));
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => service.ListPackagesAsync(0, 101, default));
        _repository.Setup(item => item.GetDocumentsAsync(_tenant, It.IsAny<IReadOnlyList<Guid>>(), default)).ReturnsAsync([]);
        await Assert.ThrowsAsync<CompliancePackagingValidationException>(() => service.CreatePackageAsync(new CreateCompliancePackageRequest
        {
            Name = "Future", TemplateId = "test-v1", PeriodStartUtc = Now, PeriodEndUtc = Now.AddDays(1)
        }, default));
        _source.VerifyNoOtherCalls();
    }

    private ComplianceEvidenceDocument Document() => new()
    {
        TenantId = _tenant, Name = "Evidence", TemplateId = "test-v1", Type = "assessment", MediaType = "application/json",
        Content = Convert.FromBase64String(Upload().ContentBase64), ContentSha256 = Hash(Convert.FromBase64String(Upload().ContentBase64)),
        SourceUri = "https://example.com/evidence", ValidFromUtc = Start.AddDays(-1), ValidUntilUtc = Now.AddDays(30),
        UploadedByUserId = _user, ControlIdsJson = "[\"C1\"]", ValidationFieldsJson = "{}", Revision = 1
    };
    private static UploadComplianceDocumentRequest Upload() => new()
    {
        TemplateId = "test-v1", Name = "Evidence", Type = "assessment", MediaType = "application/json",
        SourceUri = "https://example.com/evidence", ValidFromUtc = Start.AddDays(-1), ValidUntilUtc = Now.AddDays(30), ControlIds = ["C1"],
        ContentBase64 = Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new
        {
            frameworkVersion = "v1", assessmentStatus = "satisfactory", owner = "Security", privateData = "source-body-secret"
        }))
    };
    private static ReviewComplianceDocumentRequest Review() => new() { ExpectedRevision = 1, Decision = ComplianceDocumentReview.Approved, Notes = "Reviewed actual source evidence." };
    private static CreateCompliancePackageRequest Request(ComplianceEvidenceDocument document) => new()
    {
        Name = "Audit package", TemplateId = "test-v1", PeriodStartUtc = Start, PeriodEndUtc = End, DocumentIds = [document.Id]
    };
    private static ComplianceFrameworkTemplate Template() => new("test-v1", ComplianceFramework.Custom, "v1", ComplianceEvidencePeriodMode.Period,
        ["https://example.com/standard"], [new("C1", "https://example.com/standard", [ComplianceEvidenceKind.Operations], ["assessment"])],
        [new("assessment", ["frameworkVersion", "assessmentStatus", "owner"], true)]);
    private static ComplianceEvidenceDataset Dataset() => new(ComplianceEvidenceKind.Operations, "AuditLogs",
        JsonSerializer.SerializeToUtf8Bytes(new[] { new { observedAtUtc = Start }, new { observedAtUtc = End } }), 2,
        Start, End, [DateOnly.FromDateTime(Start), DateOnly.FromDateTime(End)], []);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private sealed class FixedTime : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
}
