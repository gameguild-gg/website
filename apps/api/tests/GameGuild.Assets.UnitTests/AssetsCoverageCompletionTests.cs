#pragma warning disable CS8600, CS8602, CS8604, CS8625

using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Amazon.S3;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Assets.Configuration;
using GameGuild.Assets.Controllers;
using GameGuild.Assets.Extensions;
using GameGuild.Assets.Security;
using GameGuild.Assets.Services;
using GameGuild.Assets.Storage;
using GameGuild.Assets.Transformation;
using GameGuild.Assets.VirusScan;
using GameGuild.CQRS;
using GameGuild.Features;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Models;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using Moq;
using CommerceOrderStatus = GameGuild.Commerce.Orders.OrderStatus;

namespace GameGuild.Assets.UnitTests;

public class AssetsCoverageCompletionTests
{
    private static readonly string[] KnownMimeTypes =
    [
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
        "image/svg+xml",
        "video/mp4",
        "video/webm",
        "audio/mpeg",
        "audio/wav",
        "audio/ogg",
        "application/pdf",
        "application/zip",
        "text/plain",
        "application/json",
        "application/octet-stream"
    ];

    [Fact]
    public void Options_Getters_And_NullJsonBranches_AreCovered()
    {
        var extractionOptions = new AssetTextExtractionOptions { EnableOcr = true };
        extractionOptions.EnableOcr.Should().BeTrue();

        var storageOptions = new AssetStorageOptions
        {
            BucketName = "assets",
            TransformedBucketName = "derived",
            QuarantineBucketName = "quarantine"
        };
        storageOptions.GetTransformedBucketName().Should().Be("derived");
        storageOptions.GetQuarantineBucketName().Should().Be("quarantine");

        new AssetStorageOptions { BucketName = "assets" }.GetQuarantineBucketName().Should().Be("assets");

        var content = new AssetContent("bucket", "key", "hash", "image/png", 1, null, null)
        {
            ModerationLabels = "null"
        };
        content.ModerationLabelsList.Should().BeEmpty();

        var reference = new AssetReference(content.Id, Guid.NewGuid(), "asset", AssetAccessPolicy.Private, null, null)
        {
            Tags = "null"
        };
        reference.TagsList.Should().BeEmpty();
    }

    [Fact]
    public void Ef_Constructors_And_RecordDtos_AreCovered()
    {
        Activator.CreateInstance(typeof(AssetContent), nonPublic: true).Should().BeOfType<AssetContent>();
        Activator.CreateInstance(typeof(AssetReference), nonPublic: true).Should().BeOfType<AssetReference>();
        Activator.CreateInstance(typeof(AssetReport), nonPublic: true).Should().BeOfType<AssetReport>();

        var extracted = new ExtractedAssetTextResponse(
            Guid.NewGuid(),
            "text/plain",
            "body",
            "text",
            false,
            false,
            Array.Empty<string>());
        extracted.Text.Should().Be("body");

        var assetExtracted = new AssetExtractedTextResponse(
            Guid.NewGuid(),
            "application/pdf",
            "ready",
            "pdf-text",
            "text",
            null,
            false,
            false);
        assetExtracted.Status.Should().Be("ready");
    }

    [Fact]
    public void Module_Registration_And_Model_Configuration_AreCovered()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        new AssetsModule().ConfigureServices(services, configuration).Should().BeSameAs(services);

        var modelBuilder = new ModelBuilder(new ConventionSet());
        modelBuilder.ConfigureAssetsEntities().Should().BeSameAs(modelBuilder);

        var modelBuilderFromConfiguration = new ModelBuilder(new ConventionSet());
        new AssetsModelConfiguration().Configure(modelBuilderFromConfiguration);
        modelBuilderFromConfiguration.Model.GetEntityTypes().Should().NotBeEmpty();
    }

    [Fact]
    public void Controllers_Constructors_And_PrivateActorProperties_AreCovered()
    {
        var actor = CreateActor(Guid.NewGuid(), Guid.NewGuid());
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actor);

        var controller = new AssetsController(
            Mock.Of<ISender>(),
            actorAccessor.Object,
            Mock.Of<IAssetUploadAuthorizationService>(),
            Mock.Of<IAssetTextExtractionService>());

        GetPrivateProperty<ActorContext>(controller, "Actor").Should().BeSameAs(actor);

        var adminController = new AssetsAdminController(
            Mock.Of<ISender>(),
            actorAccessor.Object);

        GetPrivateProperty<ActorContext>(adminController, "Actor").Should().BeSameAs(actor);
    }

    [Fact]
    public void SecureDelivery_PrivateHelpers_Cover_ClientIp_And_MimeRisk_Branches()
    {
        var controller = CreateSecureDeliveryController();
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "203.0.113.1, 10.0.0.1";
        controller.ControllerContext = new ControllerContext { HttpContext = context };

        InvokeInstance<string>(controller, "GetClientIp").Should().Be("203.0.113.1");

        context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("198.51.100.7");
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        InvokeInstance<string>(controller, "GetClientIp").Should().Be("198.51.100.7");

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        InvokeInstance<string>(controller, "GetClientIp").Should().Be("unknown");

        InvokeStatic<bool>(typeof(SecureAssetDeliveryController), "IsHighRiskMimeType", "image/png").Should().BeTrue();
        InvokeStatic<bool>(typeof(SecureAssetDeliveryController), "IsHighRiskMimeType", "video/mp4").Should().BeTrue();
        InvokeStatic<bool>(typeof(SecureAssetDeliveryController), "IsHighRiskMimeType", "application/pdf").Should().BeTrue();
        InvokeStatic<bool>(typeof(SecureAssetDeliveryController), "IsHighRiskMimeType", "text/plain").Should().BeFalse();
    }

    [Fact]
    public void TenantValidation_FailOpen_And_CrossTenant_Branches_AreCovered()
    {
        var failOpen = new TenantAssetValidationService(
            Options.Create(new TenantIsolationOptions { FailClosedOnMissingTenant = false }),
            NullLogger<TenantAssetValidationService>.Instance);

        failOpen.ValidateTenantAccess(null, Guid.NewGuid(), null!).Error.Should().Be("No actor context available");
        failOpen.ValidateTenantAccess(null, Guid.Empty, CreateActor(Guid.NewGuid(), null)).IsValid.Should().BeTrue();
        failOpen.ValidateTokenTenant(Guid.Empty, null).IsValid.Should().BeTrue();

        var strict = new TenantAssetValidationService(
            Options.Create(new TenantIsolationOptions { AllowCrossTenantForAdmins = false }),
            NullLogger<TenantAssetValidationService>.Instance);

        var actor = CreateActor(Guid.NewGuid(), Guid.NewGuid());
        strict.ResolveEffectiveTenant(Guid.NewGuid(), actor)
            .Should()
            .Match<GameGuild.Assets.Security.TenantValidationResult>(
                result => !result.IsValid && result.Error == "Cannot access resources in different tenant");

        strict.ResolveEffectiveTenant(Guid.NewGuid(), CreateActor(Guid.NewGuid(), null))
            .IsValid
            .Should()
            .BeTrue();

        var globalTenant = Guid.NewGuid();
        var globalAllowed = new TenantAssetValidationService(
            Options.Create(new TenantIsolationOptions { GlobalAccessTenants = [globalTenant] }),
            NullLogger<TenantAssetValidationService>.Instance);
        globalAllowed.ResolveEffectiveTenant(Guid.NewGuid(), CreateActor(Guid.NewGuid(), globalTenant))
            .IsValid
            .Should()
            .BeTrue();

        var adminAllowed = new TenantAssetValidationService(
            Options.Create(new TenantIsolationOptions { AllowCrossTenantForAdmins = true }),
            NullLogger<TenantAssetValidationService>.Instance);
        var adminActor = CreateActor(Guid.NewGuid(), Guid.NewGuid()) with
        {
            Roles = new HashSet<string> { "SystemAdmin" }
        };
        adminAllowed.ResolveEffectiveTenant(Guid.NewGuid(), adminActor)
            .IsValid
            .Should()
            .BeTrue();
    }

    [Fact]
    public void AssetAuthorization_PermissionMapping_Covers_All_Cases()
    {
        InvokeMap(AssetsPermission.Read).Should().Be(AccessLevel.Read);
        InvokeMap(AssetsPermission.Create).Should().Be(AccessLevel.Write);
        InvokeMap(AssetsPermission.Update).Should().Be(AccessLevel.Write);
        InvokeMap(AssetsPermission.Delete).Should().Be(AccessLevel.Admin);
        InvokeMap(AssetsPermission.Admin).Should().Be(AccessLevel.Admin);
        InvokeMap(AssetsPermission.Moderate).Should().Be(AccessLevel.Admin);
        InvokeMap(AssetsPermission.Transform).Should().Be(AccessLevel.Read);
        InvokeMap(AssetsPermission.GenerateUrl).Should().Be(AccessLevel.Read);
        InvokeMap(AssetsPermission.Report).Should().Be(AccessLevel.Read);
        InvokeMap(new UnknownPermission()).Should().Be(AccessLevel.None);

        static AccessLevel InvokeMap(Permission permission)
            => InvokeStatic<AccessLevel>(typeof(AssetAuthorizationHandler), "MapPermissionToAccessLevel", permission);
    }

    [Fact]
    public void AssetModeration_PrivateSignatureHelpers_Cover_All_Image_And_Binary_Branches()
    {
        InvokeStatic<IReadOnlyList<string>>(typeof(AssetModerationService), "GetBlockedTextMarkers")
            .Should()
            .Contain(["malware", "phishing", "credential theft", "hate speech", "explicit threat"]);

        InvokeStatic<bool>(typeof(AssetModerationService), "HasExecutableSignature", ReadOnlyMemory<byte>.Empty).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExecutableSignature", new ReadOnlyMemory<byte>([(byte)'M'])).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExecutableSignature", new ReadOnlyMemory<byte>([(byte)'M', (byte)'Z'])).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExecutableSignature", new ReadOnlyMemory<byte>([0x7F, (byte)'E', (byte)'L', (byte)'F'])).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExecutableSignature", new ReadOnlyMemory<byte>([0x7F, (byte)'E', (byte)'L', (byte)'X'])).Should().BeFalse();

        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/png", new ReadOnlyMemory<byte>([0x89, (byte)'P', (byte)'N', (byte)'G'])).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/png", new ReadOnlyMemory<byte>([(byte)'P', (byte)'N', (byte)'G'])).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/jpeg", new ReadOnlyMemory<byte>([0xFF, 0xD8, 0xFF])).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/jpg", new ReadOnlyMemory<byte>([0xFF, 0xD8, 0x00])).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/gif", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("GIF87a"))).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/gif", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("GIF89a"))).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/gif", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("GIF00a"))).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/webp", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("RIFFxxxxWEBP"))).Should().BeTrue();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/webp", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("RIFFxxxxWEPB"))).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/webp", new ReadOnlyMemory<byte>(Encoding.ASCII.GetBytes("RIFF"))).Should().BeFalse();
        InvokeStatic<bool>(typeof(AssetModerationService), "HasExpectedImageSignature", "image/svg+xml", ReadOnlyMemory<byte>.Empty).Should().BeTrue();
    }

    [Fact]
    public void AssetAccessService_BuildAccessUrl_Covers_Transformation_Branch()
    {
        var service = new AssetAccessService(
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<ITransformedAssetRepository>(),
            Mock.Of<IAssetStorageService>(),
            Mock.Of<IAssetTokenService>(),
            Mock.Of<ITenantMemberRepository>(),
            Mock.Of<IFeatureFlagEvaluationService>(),
            Array.Empty<IAssetParentAuthorizationResolver>(),
            Mock.Of<IAssetFolderAuthorizationService>(),
            Mock.Of<IAssetScopedAccessService>(),
            Options.Create(new AssetAccessOptions { BaseUrl = "https://cdn.example.test/" }),
            NullLogger<AssetAccessService>.Instance);

        var url = InvokeInstance<string>(
            service,
            "BuildAccessUrl",
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "token-value",
            new TransformationSpec { Width = 320, Format = ImageFormat.Webp });

        url.Should().StartWith("https://cdn.example.test/api/assets/");
        url.Should().Contain("token=token-value");
        url.Should().Contain("transform=");
    }

    [Fact]
    public void Storage_PrivateObjectKeySwitches_Cover_All_MimeTypes_And_InvalidHashes()
    {
        foreach (var serviceType in new[]
                 {
                     typeof(AssetStorageService),
                     typeof(S3StorageService),
                     typeof(LocalFileSystemStorageService)
                 })
        {
            foreach (var mimeType in KnownMimeTypes)
            {
                var key = InvokeStatic<string>(
                    serviceType,
                    "GenerateObjectKey",
                    "abcdef0123456789",
                    mimeType,
                    mimeType == "image/png");

                key.Should().Contain("abcdef0123456789");
            }

            var invalid = () => InvokeStatic<string>(
                serviceType,
                "GenerateObjectKey",
                "abc",
                "text/plain",
                false);

            if (serviceType == typeof(LocalFileSystemStorageService))
            {
                invalid.Should().Throw<ArgumentException>()
                    .WithMessage("*at least four*");
            }
            else
            {
                invalid.Should().Throw<ArgumentOutOfRangeException>();
            }
        }
    }

    [Fact]
    public async Task LocalFileSystemStorage_Covers_Url_Multipart_Metadata_And_Guard_Branches()
    {
        var root = Path.Combine(Path.GetTempPath(), $"gameguild-assets-coverage-{Guid.NewGuid():N}");
        try
        {
            var serviceWithPrefix = new LocalFileSystemStorageService(
                new LocalFileSystemConfiguration { BasePath = root, ServeUrlPrefix = "https://static.example.test/" },
                "assets",
                "assets-transformed",
                "assets-quarantine");

            var prefixedUrl = await serviceWithPrefix.GeneratePresignedUrlAsync(
                "assets",
                "content/file.txt",
                TimeSpan.FromMinutes(5),
                isDownload: false);
            prefixedUrl.Should().Be("https://static.example.test/assets/content%2Ffile.txt");

            var service = new LocalFileSystemStorageService(
                new LocalFileSystemConfiguration { BasePath = root },
                "assets",
                "assets-transformed",
                "assets-quarantine");

            var localUrl = await service.GeneratePresignedUrlAsync(
                "assets",
                "content/file.txt",
                TimeSpan.FromMinutes(5),
                isDownload: false);
            localUrl.Should().StartWith("local://assets/content/file.txt?expires=");
            localUrl.Should().EndWith("&download=false");

            Func<Task> emptyBucket = () => service.ExistsAsync("", "key");
            Func<Task> emptyKey = () => service.ExistsAsync("assets", "");
            Func<Task> emptyUploadId = () => service.AbortMultipartUploadAsync("", "key");

            await emptyBucket.Should().ThrowAsync<ArgumentException>();
            await emptyKey.Should().ThrowAsync<ArgumentException>();
            await emptyUploadId.Should().ThrowAsync<ArgumentException>();

            var uploadId = await service.InitiateMultipartUploadAsync("text/plain");
            await using (var part = new MemoryStream(Encoding.UTF8.GetBytes("part-one")))
            {
                (await service.UploadPartAsync(uploadId, "assembled.bin", 1, part)).Should().NotBeNullOrWhiteSpace();
            }

            await service.AbortMultipartUploadAsync(uploadId, "assembled.bin");
            await service.AbortMultipartUploadAsync(Guid.NewGuid().ToString("N"), "assembled.bin");

            var orphanPath = Path.Combine(root, "assets", "folder", "orphan.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(orphanPath)!);
            await File.WriteAllTextAsync(orphanPath, "orphan");
            var metadata = await service.GetMetadataAsync("assets", "folder/orphan.bin");
            metadata.Should().NotBeNull();
            metadata!.MimeType.Should().Be("application/octet-stream");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void StorageFactory_Covers_S3_R2_B2_And_Default_Branches()
    {
        var factory = CreateStorageFactory();

        factory.CreateFromConfiguration(
                new S3CompatibleConfiguration
                {
                    AccessKeyId = "key",
                    SecretAccessKey = "secret",
                    Region = "us-east-1"
                },
                "assets")
            .Should()
            .BeOfType<S3StorageService>();

        factory.CreateFromConfiguration(
                new S3CompatibleConfiguration
                {
                    AccessKeyId = "key",
                    SecretAccessKey = "secret",
                    SessionToken = "session",
                    ServiceUrl = "http://localhost:9000",
                    Region = "us-east-1",
                    UseHttp = true,
                    ForcePathStyle = true
                },
                "assets")
            .Should()
            .BeOfType<S3StorageService>();

        factory.CreateFromConfiguration(
                new CloudflareR2Configuration
                {
                    AccountId = "account",
                    AccessKeyId = "key",
                    SecretAccessKey = "secret",
                    Jurisdiction = "eu"
                },
                "assets")
            .Should()
            .BeOfType<S3StorageService>();

        factory.CreateFromConfiguration(
                new BackblazeB2Configuration
                {
                    ApplicationKeyId = "key",
                    ApplicationKey = "secret",
                    Endpoint = "s3.us-west-004.backblazeb2.com",
                    Region = "us-west-004"
                },
                "assets")
            .Should()
            .BeOfType<S3StorageService>();

        var unsupported = () => factory.CreateFromConfiguration(new UnsupportedStorageConfiguration(), "assets");
        unsupported.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void AssetTextExtraction_PrivateHelpers_Cover_Ocr_Fallbacks_PdfEscapes_And_Normalization()
    {
        var service = CreateTextExtractionService();

        var success = InvokeInstance<object>(
            service,
            "ExtractOcrTextFromBody",
            JsonSerializer.Serialize(new
            {
                readResults = new[]
                {
                    new
                    {
                        lines = new[]
                        {
                            new { content = "Scanned lease text" }
                        }
                    }
                }
            }));

        GetPublicProperty<bool>(success, "Success").Should().BeTrue();
        GetPublicProperty<string>(success, "Text").Should().Be("Scanned lease text");

        var failure = InvokeInstance<object>(service, "ExtractOcrTextFromBody", "{}");
        GetPublicProperty<bool>(failure, "Success").Should().BeFalse();

        var escapedPdf = Encoding.Latin1.GetBytes("(!!!) (Line\\nCarriage\\rTab\\tBack\\bForm\\fParen\\)Slash\\\\Z)");
        var pdfText = InvokeStatic<string>(typeof(AssetTextExtractionService), "ExtractReadablePdfText", escapedPdf);
        pdfText.Should().Contain("Line");
        pdfText.Should().Contain("Paren)");

        InvokeStatic<string>(typeof(AssetTextExtractionService), "NormalizeExtractedText", "   ")
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void AssetTokenService_Covers_Cache_Expiry_FullCache_And_InvalidEphemeral_Branches()
    {
        var service = CreateTokenService();
        var assetId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        var token = service.GenerateToken(assetId, tenantId, AssetAccessPolicy.Public);
        var payload = service.ValidateToken(token, assetId, tenantId);
        payload.Should().NotBeNull();

        var cache = GetTokenCache(service);
        var cacheKey = $"{token}:{assetId}:{tenantId}";
        cache[cacheKey] = (payload!, DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds());
        service.ValidateToken(token, assetId, tenantId).Should().NotBeNull();

        var expiredToken = service.GenerateToken(assetId, tenantId, AssetAccessPolicy.Public, customExpiry: TimeSpan.FromSeconds(-5));
        service.ValidateToken(expiredToken, assetId, tenantId).Should().BeNull();

        cache.Clear();
        var expiredTimestamp = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeSeconds();
        for (var i = 0; i < 10_000; i++)
        {
            cache[$"expired-{i}"] = (
                new AssetTokenPayload(Guid.NewGuid(), 1, expiredTimestamp, AssetAccessPolicy.Public, string.Empty, tenantId),
                expiredTimestamp);
        }

        var fullCacheToken = service.GenerateToken(assetId, tenantId, AssetAccessPolicy.Public);
        service.ValidateToken(fullCacheToken, assetId, tenantId).Should().NotBeNull();
        cache.Count.Should().Be(1);

        InvokeInstance<object?>(service, "EvictExpiredEntries").Should().BeNull();

        service.ValidateEphemeralToken(Base64UrlEncode(new byte[10])).Should().BeNull();

        var shortUserToken = new byte[37];
        shortUserToken[20] = 1;
        service.ValidateEphemeralToken(Base64UrlEncode(shortUserToken)).Should().BeNull();
    }

    [Fact]
    public async Task VirusScan_And_OrderStatus_Private_Branches_AreCovered()
    {
        var disabledScanner = new VirusScanService(
            Options.Create(new VirusScanOptions { Enabled = false }),
            NullLogger<VirusScanService>.Instance);

        (await disabledScanner.ScanStoredAsync("assets", "clean.txt")).IsClean.Should().BeTrue();

        var scanner = new VirusScanService(
            Options.Create(new VirusScanOptions { BlockedExtensions = [".blocked"] }),
            NullLogger<VirusScanService>.Instance);

        var blocked = await scanner.ScanStoredAsync("assets", "malware.blocked");
        blocked.IsClean.Should().BeFalse();
        blocked.ThreatName.Should().Be("BLOCKED_EXTENSION");

        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Paid)
            .Should()
            .Be(OrderStatus.Paid);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Fulfilled)
            .Should()
            .Be(OrderStatus.Fulfilled);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Completed)
            .Should()
            .Be(OrderStatus.Fulfilled);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Refunded)
            .Should()
            .Be(OrderStatus.Refunded);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.PartiallyRefunded)
            .Should()
            .Be(OrderStatus.Refunded);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Cancelled)
            .Should()
            .Be(OrderStatus.Cancelled);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Disputed)
            .Should()
            .Be(OrderStatus.Disputed);
        InvokeStatic<OrderStatus>(typeof(CommerceOrderValidationService), "MapStatus", CommerceOrderStatus.Pending)
            .Should()
            .Be(OrderStatus.Pending);
    }

    [Fact]
    public void Localization_PrivateFallbacks_AreCovered()
    {
        var service = new AssetLocalizationService();

        service.GetAccessDeniedMessage(AssetAccessPolicy.Private, "en-US")
            .Should()
            .Contain("private");

        InvokeStatic<string>(
                typeof(AssetLocalizationService),
                "GetMessage",
                "unknown.key",
                "zz-ZZ",
                null)
            .Should()
            .Be("unknown.key");
    }

    private static SecureAssetDeliveryController CreateSecureDeliveryController()
        => new(
            Mock.Of<IAssetAccessService>(),
            Mock.Of<IAssetRateLimitService>(),
            Mock.Of<ITenantAssetValidationService>(),
            Mock.Of<ITransformationValidator>(),
            Mock.Of<IDownloadWindowService>(),
            Mock.Of<IAssetContentRepository>(),
            Mock.Of<IAssetReferenceRepository>(),
            Mock.Of<IActorContextAccessor>(),
            Mock.Of<IAssetStorageService>(),
            Options.Create(new AssetAccessOptions()),
            NullLogger<SecureAssetDeliveryController>.Instance);

    private static StorageServiceFactory CreateStorageFactory()
        => new(
            Options.Create(new GlobalStorageOptions
            {
                TransformedBucketName = "assets-transformed",
                QuarantineBucketName = "assets-quarantine"
            }),
            Mock.Of<ITenantStorageConfigurationRepository>(),
            Mock.Of<IStorageConfigurationEncryption>(),
            Mock.Of<IStorageService>(),
            NullLogger<StorageServiceFactory>.Instance);

    private static AssetTextExtractionService CreateTextExtractionService()
        => new(
            Mock.Of<IAssetStorageService>(),
            new StubHttpClientFactory(),
            Options.Create(new AssetTextExtractionOptions()),
            NullLogger<AssetTextExtractionService>.Instance);

    private static AssetTokenService CreateTokenService()
        => new(Options.Create(new AssetTokenOptions
        {
            SecretKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("01234567890123456789012345678901")),
            DefaultExpiryHours = 1,
            TimeWindowHours = 24
        }));

    private static ConcurrentDictionary<string, (AssetTokenPayload Payload, long ExpiryTimestamp)> GetTokenCache(
        AssetTokenService service)
    {
        var field = typeof(AssetTokenService).GetField("_tokenCache", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return (ConcurrentDictionary<string, (AssetTokenPayload Payload, long ExpiryTimestamp)>)field!.GetValue(service)!;
    }

    private static ActorContext CreateActor(Guid userId, Guid? tenantId)
        => new()
        {
            ActorKind = ActorKind.User,
            IsAuthenticated = true,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>()
        };

    private static T GetPrivateProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.NonPublic);
        property.Should().NotBeNull();
        return (T)property!.GetValue(target)!;
    }

    private static T GetPublicProperty<T>(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        property.Should().NotBeNull();
        return (T)property!.GetValue(target)!;
    }

    private static T InvokeStatic<T>(Type type, string methodName, params object?[] args)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (T)Invoke(method!, null, args)!;
    }

    private static T InvokeInstance<T>(object target, string methodName, params object?[] args)
    {
        var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        method.Should().NotBeNull();
        return (T?)Invoke(method!, target, args) is { } result ? result : default!;
    }

    private static object? Invoke(MethodInfo method, object? target, object?[] args)
    {
        try
        {
            return method.Invoke(target, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private sealed class UnknownPermission()
        : Permission("assets", "unknown", null, "Unknown permission");

    private sealed class UnsupportedStorageConfiguration : StorageProviderConfiguration
    {
        public override StorageProviderType ProviderType => StorageProviderType.S3Compatible;

        public override Storage.ValidationResult Validate() => Storage.ValidationResult.Success();
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
