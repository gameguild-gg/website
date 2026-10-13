using System.Reflection;
using GameGuild.Assets.BackgroundServices;
using GameGuild.Assets.Commands;
using GameGuild.Assets.Controllers;
using GameGuild.Assets.Security;
using GameGuild.Assets.Transformation;
using GameGuild.Assets.VirusScan;
using GameGuild.CQRS;
using GameGuild.Features;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GameGuild.Assets.UnitTests;

public sealed class AssetMutationCommandCoverageTests
{
    [Fact]
    public async Task Handler_ForwardsEveryLibraryAndChunkedUploadCommand()
    {
        var library = new Mock<IAssetLibraryService>(MockBehavior.Strict);
        var uploads = new Mock<IAssetUploadService>(MockBehavior.Strict);
        var contentRepository = new Mock<IAssetContentRepository>(MockBehavior.Strict);
        var handler = new AssetMutationCommandHandler(library.Object, uploads.Object, contentRepository.Object);
        var cancellationToken = new CancellationTokenSource().Token;
        var resourceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var folderId = Guid.NewGuid();
        var referenceId = Guid.NewGuid();
        var revisionId = Guid.NewGuid();

        var folder = AssetFolder.Create(tenantId, "Project", resourceId, null, "Screenshots");
        var folderResult = AssetLibraryResult<AssetFolder>.Success(folder);
        library.Setup(service => service.CreateFolderAsync(
                "Project", resourceId, userId, tenantId, "Screenshots", null, cancellationToken))
            .ReturnsAsync(folderResult);
        var create = new CreateAssetFolderCommand("Project", resourceId, userId, tenantId, "Screenshots", null);
        (await handler.Handle(create, cancellationToken)).Should().BeSameAs(folderResult);

        var teamIds = new[] { Guid.NewGuid() };
        var authorities = new[] { "Tester" };
        library.Setup(service => service.RestrictFolderAsync(
                folderId, userId, tenantId, AssetFolderRestrictionMode.SelectedTeams,
                teamIds, authorities, cancellationToken))
            .ReturnsAsync(folderResult);
        var restrict = new RestrictAssetFolderCommand(
            folderId, userId, tenantId, AssetFolderRestrictionMode.SelectedTeams, teamIds, authorities);
        (await handler.Handle(restrict, cancellationToken)).Should().BeSameAs(folderResult);

        var reference = new AssetReference(Guid.NewGuid(), userId, "Copy", AssetAccessPolicy.Private, "Project", resourceId);
        var referenceResult = AssetLibraryResult<AssetReference>.Success(reference);
        library.Setup(service => service.CopyAsync(referenceId, userId, tenantId, "Copy", folderId, cancellationToken))
            .ReturnsAsync(referenceResult);
        var copy = new CopyAssetReferenceCommand(referenceId, userId, tenantId, "Copy", folderId);
        (await handler.Handle(copy, cancellationToken)).Should().BeSameAs(referenceResult);

        var revisionOwner = new AssetReference(Guid.NewGuid(), userId, "Original", AssetAccessPolicy.Private, "Project", resourceId)
        {
            Id = referenceId,
            TenantId = tenantId
        };
        var revision = revisionOwner.CreateInitialRevision(userId);
        var revisionResult = AssetLibraryResult<AssetReferenceRevision>.Success(revision);
        library.Setup(service => service.RestoreRevisionAsync(referenceId, revisionId, userId, tenantId, cancellationToken))
            .ReturnsAsync(revisionResult);
        var restore = new RestoreAssetRevisionCommand(referenceId, revisionId, userId, tenantId);
        (await handler.Handle(restore, cancellationToken)).Should().BeSameAs(revisionResult);

        var expiresAt = DateTime.UtcNow.AddHours(1);
        var session = new ChunkedUploadSession("upload-1", "object-key", userId, "video.mp4", "video/mp4", 512, 2, expiresAt);
        uploads.Setup(service => service.InitiateChunkedUploadAsync(
                "video.mp4", "video/mp4", 512, userId, cancellationToken))
            .ReturnsAsync(session);
        var initiate = new InitiateChunkedAssetUploadCommand("video.mp4", "video/mp4", 512, userId);
        (await handler.Handle(initiate, cancellationToken)).Should().BeSameAs(session);

        await using var chunk = new MemoryStream([1, 2, 3]);
        uploads.Setup(service => service.UploadChunkAsync("upload-1", 1, chunk, cancellationToken)).ReturnsAsync(true);
        var uploadChunk = new UploadAssetChunkCommand("upload-1", 1, chunk);
        (await handler.Handle(uploadChunk, cancellationToken)).Should().BeTrue();

        var options = new UploadAssetOptions(DisplayName: "Video", TenantId: tenantId);
        var completed = new AssetUploadResult(true, referenceId, Guid.NewGuid(), null);
        uploads.Setup(service => service.CompleteChunkedUploadAsync("upload-1", options, cancellationToken))
            .ReturnsAsync(completed);
        var complete = new CompleteChunkedAssetUploadCommand("upload-1", options);
        (await handler.Handle(complete, cancellationToken)).Should().BeSameAs(completed);

        uploads.Setup(service => service.AbortChunkedUploadAsync("upload-1", cancellationToken)).Returns(Task.CompletedTask);
        await handler.Handle(new AbortChunkedAssetUploadCommand("upload-1"), cancellationToken);

        library.VerifyAll();
        uploads.VerifyAll();
    }

    [Fact]
    public void ModerationCommandRecords_ExposeTheirCompletePayload()
    {
        var contentId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var virus = new UpdateAssetVirusScanStatusCommand(contentId, VirusScanStatus.Infected, "EICAR");
        var moderation = new ReviewAssetContentModerationCommand(
            contentId, ModerationStatus.Rejected, reviewerId, ["malware"], "Rejected by review");

        virus.Should().BeEquivalentTo(new
        {
            ContentId = contentId,
            Status = VirusScanStatus.Infected,
            ScanResult = "EICAR"
        });
        moderation.Should().BeEquivalentTo(new
        {
            ContentId = contentId,
            Status = ModerationStatus.Rejected,
            ReviewedBy = reviewerId,
            Labels = new[] { "malware" },
            Notes = "Rejected by review"
        });
    }
}

public sealed class AssetLibraryDomainCoverageTests
{
    [Fact]
    public async Task InterfaceDefaults_DenyFolderAndParentManagement()
    {
        IAssetFolderAuthorizationService folders = new MinimalFolderAuthorizationService();
        IAssetParentAuthorizationResolver parents = new MinimalParentAuthorizationResolver();
        var folder = AssetFolder.Create(Guid.NewGuid(), "Project", Guid.NewGuid(), null, "Root");

        (await folders.CanReadFolderAsync(folder, Guid.NewGuid(), folder.TenantId)).Should().BeFalse();
        (await parents.CanManageAsync(Guid.NewGuid(), Guid.NewGuid(), null)).Should().BeFalse();
    }

    [Fact]
    public void Folder_NormalizesResourcesRestrictionsAndEmptySerializedValues()
    {
        var resourceId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var folder = AssetFolder.Create(Guid.NewGuid(), " projects ", resourceId, null, " Root ");

        folder.Name.Should().Be("Root");
        folder.BelongsTo("Project", resourceId).Should().BeTrue();
        folder.BelongsTo("projects", Guid.NewGuid()).Should().BeFalse();
        folder.BelongsTo("Team", resourceId).Should().BeFalse();

        folder.SetRestriction(
            AssetFolderRestrictionMode.TeamAuthorities,
            [teamId, teamId],
            [" Tester ", "tester", " "]);
        folder.AllowedTeamIds.Should().Equal(teamId);
        folder.AllowedAuthorities.Should().Equal("Tester");

        folder.SetRestriction(AssetFolderRestrictionMode.None);
        folder.AllowedTeamIds.Should().BeEmpty();
        folder.AllowedAuthorities.Should().BeEmpty();

        SetPrivateProperty(folder, nameof(AssetFolder.AllowedTeamIdsJson), "null");
        SetPrivateProperty(folder, nameof(AssetFolder.AllowedAuthoritiesJson), "null");
        folder.AllowedTeamIds.Should().BeEmpty();
        folder.AllowedAuthorities.Should().BeEmpty();

        var teamFolder = AssetFolder.Create(Guid.NewGuid(), "teams", resourceId, null, "Team assets");
        teamFolder.BelongsTo("Team", resourceId).Should().BeTrue();
        var customFolder = AssetFolder.Create(Guid.NewGuid(), "Course", resourceId, null, "Course assets");
        customFolder.BelongsTo("Course", resourceId).Should().BeTrue();
    }

    [Fact]
    public void Reference_RejectsDuplicateInitialAndForeignRevisionAndImportsLegacyContent()
    {
        var ownerId = Guid.NewGuid();
        var reference = new AssetReference(Guid.NewGuid(), ownerId, "Original", AssetAccessPolicy.Private, "Course", Guid.NewGuid())
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        };

        var initial = reference.CreateInitialRevision(ownerId);
        var duplicate = () => reference.CreateInitialRevision(ownerId);
        duplicate.Should().Throw<InvalidOperationException>().WithMessage("*already exists*");
        reference.ReplaceContent(Guid.NewGuid(), ownerId).RevisionNumber.Should().Be(2);

        var generatedId = new AssetReference(
            Guid.NewGuid(), ownerId, "Generated id", AssetAccessPolicy.Private, "Course", Guid.NewGuid());
        generatedId.CreateInitialRevision(ownerId);
        generatedId.Id.Should().NotBeEmpty();

        var copied = reference.CopyTo(ownerId, null, null);
        copied.DisplayName.Should().Be("Original");
        reference.CopyTo(ownerId, "Explicit copy", Guid.NewGuid()).DisplayName.Should().Be("Explicit copy");

        var legacy = new AssetReference(Guid.NewGuid(), ownerId, "Legacy", AssetAccessPolicy.Private, "Course", Guid.NewGuid());
        var replacement = legacy.ReplaceContent(Guid.NewGuid(), ownerId, " ");
        legacy.Revisions.Should().HaveCount(2);
        replacement.Note.Should().BeNull();

        var other = new AssetReference(Guid.NewGuid(), ownerId, "Other", AssetAccessPolicy.Private, "Course", Guid.NewGuid())
        {
            Id = Guid.NewGuid()
        };
        var foreignRevision = other.CreateInitialRevision(ownerId);
        var restoreForeign = () => reference.RestoreRevision(foreignRevision, ownerId);
        restoreForeign.Should().Throw<InvalidOperationException>().WithMessage("*another asset reference*");
        initial.AssetReferenceId.Should().Be(reference.Id);
    }

    [Fact]
    public void Reference_RevisionCreationPreservesExistingIdsAndGeneratesMissingIds()
    {
        var userId = Guid.NewGuid();
        var expectedInitialId = Guid.NewGuid();
        var initialized = new AssetReference(
            Guid.NewGuid(), userId, "Initialized", AssetAccessPolicy.Private, "Course", Guid.NewGuid())
        {
            Id = expectedInitialId
        };
        var uninitialized = new AssetReference(
            Guid.NewGuid(), userId, "Uninitialized", AssetAccessPolicy.Private, "Course", Guid.NewGuid());

        initialized.CreateInitialRevision(userId);
        uninitialized.CreateInitialRevision(userId);

        initialized.Id.Should().Be(expectedInitialId);
        uninitialized.Id.Should().NotBeEmpty();

        var expectedReplacementId = Guid.NewGuid();
        var initializedReplacement = new AssetReference(
            Guid.NewGuid(), userId, "Initialized replacement", AssetAccessPolicy.Private, "Course", Guid.NewGuid())
        {
            Id = expectedReplacementId
        };
        var uninitializedReplacement = new AssetReference(
            Guid.NewGuid(), userId, "Uninitialized replacement", AssetAccessPolicy.Private, "Course", Guid.NewGuid());

        initializedReplacement.ReplaceContent(Guid.NewGuid(), userId);
        uninitializedReplacement.ReplaceContent(Guid.NewGuid(), userId);

        initializedReplacement.Id.Should().Be(expectedReplacementId);
        uninitializedReplacement.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ScopedGrant_ValidatesExpiryAndScopeAndTracksEveryInactiveReason()
    {
        var tenantId = Guid.NewGuid();
        var expired = () => AssetScopedAccessGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), tenantId, "Session", Guid.NewGuid(), DateTime.UtcNow.AddMinutes(-1), Guid.NewGuid());
        expired.Should().Throw<ArgumentException>().WithParameterName("expiresAt");

        var blankScope = () => AssetScopedAccessGrant.Create(
            Guid.NewGuid(), Guid.NewGuid(), tenantId, " ", Guid.NewGuid(), DateTime.UtcNow.AddHours(1), Guid.NewGuid());
        blankScope.Should().Throw<ArgumentException>().WithParameterName("scopeType");

        var active = CreateGrant(tenantId);
        active.ScopeType.Should().Be("Session");
        active.IsActive.Should().BeTrue();
        active.Revoke();
        active.IsActive.Should().BeFalse();
        var firstRevocation = active.RevokedAt;
        active.Revoke();
        active.RevokedAt.Should().Be(firstRevocation);

        var expiredGrant = CreateGrant(tenantId);
        SetPrivateProperty(expiredGrant, nameof(AssetScopedAccessGrant.ExpiresAt), DateTime.UtcNow.AddMinutes(-1));
        expiredGrant.IsActive.Should().BeFalse();

        var deletedGrant = CreateGrant(tenantId);
        deletedGrant.Version = 1;
        deletedGrant.SoftDelete();
        deletedGrant.IsActive.Should().BeFalse();

        Activator.CreateInstance(typeof(AssetScopedAccessGrant), nonPublic: true)
            .Should().BeOfType<AssetScopedAccessGrant>();
    }

    [Fact]
    public void LibraryView_RecordExposesBothCollections()
    {
        IReadOnlyList<AssetFolder> folders = [AssetFolder.Create(Guid.NewGuid(), "Project", Guid.NewGuid(), null, "Root")];
        IReadOnlyList<AssetReference> assets =
        [
            new AssetReference(Guid.NewGuid(), Guid.NewGuid(), "Asset", AssetAccessPolicy.Private, "Project", Guid.NewGuid())
        ];

        var view = new AssetLibraryView(folders, assets);

        view.Folders.Should().BeSameAs(folders);
        view.Assets.Should().BeSameAs(assets);
    }

    private static AssetScopedAccessGrant CreateGrant(Guid tenantId) => AssetScopedAccessGrant.Create(
        Guid.NewGuid(), Guid.NewGuid(), tenantId, " Session ", Guid.NewGuid(), DateTime.UtcNow.AddHours(1), Guid.NewGuid());

    private static void SetPrivateProperty<T>(object target, string name, T value) =>
        target.GetType().GetProperty(name)!.SetValue(target, value);

    private sealed class MinimalFolderAuthorizationService : IAssetFolderAuthorizationService
    {
        public Task<bool> CanReadAsync(
            AssetReference reference,
            Guid userId,
            Guid? tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class MinimalParentAuthorizationResolver : IAssetParentAuthorizationResolver
    {
        public bool Supports(string resourceType) => true;

        public Task<bool> CanReadAsync(
            Guid parentResourceId,
            Guid userId,
            Guid? tenantId,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}

public sealed class AssetAuthorizationAndScopedAccessCoverageTests : IDisposable
{
    private readonly AssetsFinalTestContext _context = new(
        new DbContextOptionsBuilder<AssetsFinalTestContext>()
            .UseInMemoryDatabase($"asset-final-{Guid.NewGuid():N}")
            .Options);

    [Fact]
    public async Task LibraryService_WhenNoParentResolverExists_DeniesReadAndManagement()
    {
        var service = new AssetLibraryService(
            _context,
            [],
            Mock.Of<IAssetAccessService>(),
            Mock.Of<IAssetFolderAuthorizationService>(),
            Mock.Of<IAssetContentRepository>());

        var read = await service.GetAsync("Unknown", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var create = await service.CreateFolderAsync(
            "Unknown", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Folder", null);

        read.Error.Should().Be("NotFound");
        create.Error.Should().Be("Forbidden");
    }

    [Fact]
    public async Task LibraryService_WhenResolverReturnsNoTask_FallsBackToDenied()
    {
        var resolver = new Mock<IAssetParentAuthorizationResolver>();
        resolver.Setup(candidate => candidate.Supports("Project")).Returns(true);
        resolver.Setup(candidate => candidate.CanReadAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Returns((Task<bool>)null!);
        var service = new AssetLibraryService(
            _context,
            [resolver.Object],
            Mock.Of<IAssetAccessService>(),
            Mock.Of<IAssetFolderAuthorizationService>(),
            Mock.Of<IAssetContentRepository>());

        var result = await service.GetAsync("Project", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Error.Should().Be("NotFound");
    }

    [Fact]
    public async Task ScopedAccessService_RejectsMissingTenantAndQueriesNonNullTenant()
    {
        var service = new AssetScopedAccessService(_context);

        (await service.HasActiveGrantAsync(Guid.NewGuid(), Guid.NewGuid(), null)).Should().BeFalse();
        (await service.HasActiveGrantAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())).Should().BeFalse();
    }

    [Fact]
    public async Task FolderAuthorization_EvaluatesUnrestrictedMissingAndMatchingResolvers()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var resourceId = Guid.NewGuid();
        var unrestricted = AssetFolder.Create(tenantId, "Project", resourceId, null, "Public");
        unrestricted.Id = Guid.NewGuid();
        var restricted = AssetFolder.Create(tenantId, "Project", resourceId, null, "Restricted");
        restricted.Id = Guid.NewGuid();
        restricted.SetRestriction(AssetFolderRestrictionMode.SelectedTeams, [Guid.NewGuid()]);
        _context.Set<AssetFolder>().AddRange(unrestricted, restricted);
        await _context.SaveChangesAsync();

        var withoutResolver = new AssetFolderAuthorizationService(_context, []);
        (await withoutResolver.CanReadFolderAsync(unrestricted, userId, tenantId)).Should().BeTrue();
        (await withoutResolver.CanReadFolderAsync(restricted, userId, tenantId)).Should().BeFalse();

        var resolver = new Mock<IAssetFolderRestrictionAuthorizationResolver>(MockBehavior.Strict);
        resolver.Setup(candidate => candidate.Supports(AssetFolderRestrictionMode.SelectedTeams, "Project"))
            .Returns(true);
        resolver.Setup(candidate => candidate.IsAuthorizedAsync(
                It.Is<AssetFolder>(folder => folder.Id == restricted.Id), userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var withResolver = new AssetFolderAuthorizationService(_context, [resolver.Object]);

        (await withResolver.CanReadFolderAsync(restricted, userId, tenantId)).Should().BeTrue();
        resolver.VerifyAll();
    }

    public void Dispose() => _context.Dispose();

    private sealed class AssetsFinalTestContext(DbContextOptions<AssetsFinalTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AssetFolder>();
            modelBuilder.Entity<AssetScopedAccessGrant>();
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}

public sealed class AssetControllerAndEventCoverageTests
{
    [Fact]
    public async Task AssetLibrariesController_ExposesActorAndMapsEveryResult()
    {
        var actor = new ActorContext
        {
            ActorKind = ActorKind.User,
            IsAuthenticated = true,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };
        var actors = new Mock<IActorContextAccessor>();
        actors.SetupGet(accessor => accessor.ActorContext).Returns(actor);
        var libraries = new Mock<IAssetLibraryService>();
        var controller = new AssetLibrariesController(libraries.Object, actors.Object, Mock.Of<ISender>());

        typeof(AssetLibrariesController).GetProperty("Actor", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(controller).Should().BeSameAs(actor);

        Map(controller, AssetLibraryResult<string>.Success("asset")).Should().BeOfType<OkObjectResult>();
        Map(controller, AssetLibraryResult<string>.Failure("NotFound")).Should().BeOfType<NotFoundResult>();
        Map(controller, AssetLibraryResult<string>.Failure("RevisionNotFound")).Should().BeOfType<NotFoundResult>();
        Map(controller, AssetLibraryResult<string>.Failure("Forbidden")).Should().BeOfType<StatusCodeResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        Map(controller, AssetLibraryResult<string>.Failure("Validation")).Should().BeOfType<UnprocessableEntityObjectResult>();
        Map(controller, AssetLibraryResult<string>.Failure("InvalidFolder")).Should().BeOfType<UnprocessableEntityObjectResult>();
        Map(controller, AssetLibraryResult<string>.Failure("InvalidParentFolder")).Should().BeOfType<UnprocessableEntityObjectResult>();
        Map(controller, AssetLibraryResult<string>.Failure("Unexpected")).Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task CdnDelivery_RecordsEventsWhenProducerExistsAndCompletesWhenItDoesNot()
    {
        var reference = new AssetReference(
            Guid.NewGuid(), Guid.NewGuid(), "Asset", AssetAccessPolicy.Public, "Course", Guid.NewGuid())
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        };
        var producer = new Mock<IDurableEventProducer>(MockBehavior.Strict);
        producer.Setup(candidate => candidate.RecordAsync(
                It.Is<AssetServedEvent>(message =>
                    message.AssetReferenceId == reference.Id && message.TenantId == reference.TenantId),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var withProducer = CreateCdnController(producer.Object);

        await InvokeRecordServedAsync(withProducer, reference);

        reference.TenantId = null;
        await InvokeRecordServedAsync(CreateCdnController(null), reference);

        var nullTaskProducer = new Mock<IDurableEventProducer>();
        nullTaskProducer.Setup(candidate => candidate.RecordAsync(
                It.IsAny<IDurableIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns((Task)null!);
        await InvokeRecordServedAsync(CreateCdnController(nullTaskProducer.Object), reference);
        producer.VerifyAll();
    }

    [Fact]
    public void SecureDelivery_ValidatesAbsentAcceptedAndRejectedTransformations()
    {
        var validator = new Mock<ITransformationValidator>(MockBehavior.Strict);
        var sanitized = new TransformationSpec { Width = 320 };
        validator.Setup(candidate => candidate.Validate(
                It.Is<TransformationSpec>(spec => spec.Width == 320), AssetKind.Image))
            .Returns(new TransformationValidationResult(true, SanitizedSpec: sanitized));
        validator.Setup(candidate => candidate.Validate(
                It.Is<TransformationSpec>(spec => spec.Width == 9999), AssetKind.Image))
            .Returns(new TransformationValidationResult(false, "Width exceeds limit"));
        var controller = CreateSecureController(validator.Object);

        var absent = InvokeValidateTransformation(controller, null, AssetKind.Image);
        absent.Spec.Should().BeNull();
        absent.Error.Should().BeNull();

        var accepted = InvokeValidateTransformation(controller, "w=320", AssetKind.Image);
        accepted.Spec.Should().BeSameAs(sanitized);
        accepted.Error.Should().BeNull();

        var rejected = InvokeValidateTransformation(controller, "w=9999", AssetKind.Image);
        rejected.Spec.Should().BeNull();
        rejected.Error.Should().BeOfType<BadRequestObjectResult>();
        validator.VerifyAll();
    }

    [Fact]
    public void GarbageCollectionEvent_UsesTenantOrPlatformFallbackAndVirusScannerConstructs()
    {
        var content = new AssetContent("assets", "key", new string('a', 64), "image/png", 128, 16, 16)
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid()
        };

        var tenantEvent = RunDeletedEvent(content);
        tenantEvent.TenantId.Should().Be(content.TenantId!.Value);
        tenantEvent.Should().BeOfType<AssetObjectDeletedEvent>();

        content.TenantId = null;
        RunDeletedEvent(content).TenantId.Should().Be(DurableIntegrationEventTenants.Platform);

        var scanner = new VirusScanBackgroundService(
            Mock.Of<IServiceScopeFactory>(),
            Options.Create(new VirusScanOptions { Enabled = false }),
            NullLogger<VirusScanBackgroundService>.Instance);
        scanner.Should().NotBeNull();
    }

    private static IDurableIntegrationEvent RunDeletedEvent(AssetContent content)
    {
        var repository = new Mock<IAssetContentRepository>();
        repository
            .Setup(repo => repo.GetMarkedForDeletionAsync(
                It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([content]);
        repository
            .Setup(repo => repo.GetCurrentReferenceCountAsync(content.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        var service = new GameGuild.Assets.Security.AssetGarbageCollectionService(
            repository.Object,
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetGarbageCollectionOptions { GracePeriodDays = 1 }),
            NullLogger<GameGuild.Assets.Security.AssetGarbageCollectionService>.Instance);

        content.MarkedForDeletionAt = DateTime.UtcNow.AddDays(-2);
        content.ClearIntegrationEvents();
        service.RunGarbageCollectionAsync().GetAwaiter().GetResult();

        return content.IntegrationEvents.Should().ContainSingle().Which;
    }

    [Fact]
    public void UploadEvents_UseExplicitTenantAndFallbackOperationMetadata()
    {
        var service = new AssetUploadService(
            Mock.Of<IAssetContentRepository>(),
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetUploadConfiguration()),
            NullLogger<AssetUploadService>.Instance);
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var content = new AssetContent("assets", "key", new string('a', 64), "image/png", 128, 16, 16)
        {
            Id = Guid.NewGuid()
        };
        var reference = new AssetReference(content.Id, actorId, "Asset", AssetAccessPolicy.Private, "Course", Guid.NewGuid())
        {
            Id = Guid.NewGuid()
        };

        var objectEvent = InvokePrivate<AssetObjectStoredEvent>(
            service, "CreateObjectStoredEvent", content, actorId, tenantId);
        var referenceEvent = InvokePrivate<AssetReferenceCreatedEvent>(
            service, "CreateReferenceCreatedEvent", reference, content, actorId, tenantId);

        objectEvent.TenantId.Should().Be(tenantId);
        objectEvent.CorrelationId.Should().NotBeEmpty();
        referenceEvent.TenantId.Should().Be(tenantId);
        referenceEvent.CorrelationId.Should().NotBeEmpty();

        var idleContextService = new AssetUploadService(
            Mock.Of<IAssetContentRepository>(),
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetUploadConfiguration()),
            NullLogger<AssetUploadService>.Instance,
            new UseCaseOperationContextAccessor());
        InvokePrivate<AssetObjectStoredEvent>(
            idleContextService, "CreateObjectStoredEvent", content, actorId, null).CorrelationId.Should().NotBeEmpty();
        InvokePrivate<AssetReferenceCreatedEvent>(
            idleContextService, "CreateReferenceCreatedEvent", reference, content, actorId, null).CorrelationId.Should().NotBeEmpty();
    }

    private static IActionResult Map(AssetLibrariesController controller, AssetLibraryResult<string> result)
    {
        var method = typeof(AssetLibrariesController).GetMethod("Map", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(string));
        return (IActionResult)method.Invoke(controller, [result])!;
    }

    private static AssetsCdnController CreateCdnController(IDurableEventProducer? producer) => new(
        Mock.Of<IAssetAccessService>(),
        Mock.Of<IAssetStorageService>(),
        Mock.Of<IAssetContentRepository>(),
        Mock.Of<IAssetReferenceRepository>(),
        producer);

    private static async Task InvokeRecordServedAsync(AssetsCdnController controller, AssetReference reference)
    {
        var method = typeof(AssetsCdnController).GetMethod("RecordServedAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var task = (Task)method.Invoke(controller, [reference, Guid.NewGuid(), 128L, "cdn", CancellationToken.None])!;
        await task;
    }

    private static SecureAssetDeliveryController CreateSecureController(ITransformationValidator validator) => new(
        Mock.Of<IAssetAccessService>(),
        Mock.Of<IAssetRateLimitService>(),
        Mock.Of<ITenantAssetValidationService>(),
        validator,
        Mock.Of<IDownloadWindowService>(),
        Mock.Of<IAssetContentRepository>(),
        Mock.Of<IAssetReferenceRepository>(),
        Mock.Of<IActorContextAccessor>(),
        Mock.Of<IAssetStorageService>(),
        Options.Create(new AssetAccessOptions()),
        NullLogger<SecureAssetDeliveryController>.Instance);

    private static (TransformationSpec? Spec, ObjectResult? Error) InvokeValidateTransformation(
        SecureAssetDeliveryController controller,
        string? transform,
        AssetKind kind)
    {
        object?[] arguments = [transform, kind, null];
        var method = typeof(SecureAssetDeliveryController)
            .GetMethod("ValidateTransformation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var spec = (TransformationSpec?)method.Invoke(controller, arguments);
        return (spec, (ObjectResult?)arguments[2]);
    }

    private static AssetObjectDeletedEvent CreateDeletedEvent(AssetContent content) =>
        new(content.Id, content.ContentHash, content.SizeBytes, "object-storage")
        {
            TenantId = content.TenantId ?? DurableIntegrationEventTenants.Platform,
            ActorId = DurableIntegrationEventActors.System,
            AggregateType = nameof(AssetContent),
            AggregateId = content.Id.ToString(),
            CorrelationId = Guid.NewGuid()
        };

    private static T InvokePrivate<T>(object target, string name, params object?[] arguments) =>
        (T)target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(target, arguments)!;
}

public sealed class AssetPolicyCoverageTests
{
    [Fact]
    public void AccessService_TransformationTokenOverloadReturnsUnderlyingValidationResult()
    {
        var referenceId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var transformation = new TransformationSpec { Width = 640 };
        var tokens = new Mock<IAssetTokenService>(MockBehavior.Strict);
        tokens.Setup(service => service.ValidateToken("valid", referenceId, tenantId, transformation))
            .Returns(new AssetTokenPayload(referenceId, 1, DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds(),
                AssetAccessPolicy.Private, transformation.ToCanonicalString(), tenantId));
        tokens.Setup(service => service.ValidateToken("invalid", referenceId, tenantId, transformation))
            .Returns((AssetTokenPayload?)null);
        tokens.Setup(service => service.ValidateToken("invalid", referenceId, Guid.Empty, transformation))
            .Returns((AssetTokenPayload?)null);
        var service = CreateAccessService(tokens.Object);

        service.ValidateToken("valid", referenceId, tenantId, transformation).Should().BeTrue();
        service.ValidateToken("invalid", referenceId, tenantId, transformation).Should().BeFalse();
        service.ValidateToken("invalid", referenceId, null, transformation).Should().BeFalse();

        tokens.VerifyAll();
    }

    private static AssetAccessService CreateAccessService(IAssetTokenService tokens) => new(
        Mock.Of<IAssetReferenceRepository>(),
        Mock.Of<ITransformedAssetRepository>(),
        Mock.Of<IAssetStorageService>(),
        tokens,
        Mock.Of<ITenantMemberRepository>(),
        Mock.Of<IFeatureFlagEvaluationService>(),
        [],
        Mock.Of<IAssetFolderAuthorizationService>(),
        Mock.Of<IAssetScopedAccessService>(),
        Options.Create(new AssetAccessOptions()),
        NullLogger<AssetAccessService>.Instance);
}
