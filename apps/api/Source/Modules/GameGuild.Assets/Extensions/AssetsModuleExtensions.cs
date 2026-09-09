using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GameGuild.Assets.Configuration;
using GameGuild.Assets.Commands;
using GameGuild.Assets.Queries;
using GameGuild.Assets.Security;
using FluentValidation;

namespace GameGuild.Assets.Extensions;

/// <summary>
/// Module registration for Assets module.
/// </summary>
public static class AssetsModuleExtensions
{
    /// <summary>
    /// Adds Assets module services to the DI container.
    /// </summary>
    public static IServiceCollection AddAssetsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.Configure<AssetTokenOptions>(
            configuration.GetSection(AssetTokenOptions.SectionName));
        services.Configure<AssetStorageOptions>(
            configuration.GetSection(AssetStorageOptions.SectionName));
        services.Configure<AssetUploadConfiguration>(
            configuration.GetSection(AssetUploadConfiguration.SectionName));
        services.Configure<AssetAccessOptions>(
            configuration.GetSection(AssetAccessOptions.SectionName));
        services.Configure<AssetTextExtractionOptions>(
            configuration.GetSection(AssetTextExtractionOptions.SectionName));

        // Security Options (Threat Mitigations)
        services.Configure<AssetRateLimitOptions>(
            configuration.GetSection(AssetRateLimitOptions.SectionName));
        services.Configure<TransformationLimitsOptions>(
            configuration.GetSection(TransformationLimitsOptions.SectionName));
        services.Configure<VirusScanOptions>(
            configuration.GetSection(VirusScanOptions.SectionName));
        services.Configure<AssetGarbageCollectionOptions>(
            configuration.GetSection(AssetGarbageCollectionOptions.SectionName));
        services.Configure<TenantIsolationOptions>(
            configuration.GetSection(TenantIsolationOptions.SectionName));
        services.Configure<DownloadWindowOptions>(
            configuration.GetSection(DownloadWindowOptions.SectionName));

        // S3 Client
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var options = configuration.GetSection(AssetStorageOptions.SectionName)
                .Get<AssetStorageOptions>() ?? new AssetStorageOptions();

            var hasServiceUrl = !string.IsNullOrWhiteSpace(options.ServiceUrl);
            var useHttp = Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var serviceUri)
                && string.Equals(serviceUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase);

            var config = new AmazonS3Config
            {
                ForcePathStyle = options.ForcePathStyle,
                UseHttp = useHttp
            };

            if (hasServiceUrl)
            {
                config.ServiceURL = options.ServiceUrl;
                config.AuthenticationRegion = options.Region;
            }
            else
            {
                config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(options.Region);
            }

            if (!string.IsNullOrEmpty(options.AccessKey) && !string.IsNullOrEmpty(options.SecretKey))
            {
                return new AmazonS3Client(options.AccessKey, options.SecretKey, config);
            }

            return new AmazonS3Client(config);
        });

        // Repositories
        services.AddScoped<IAssetContentRepository, AssetContentRepository>();
        services.AddScoped<IAssetReferenceRepository, AssetReferenceRepository>();
        services.AddScoped<ITransformedAssetRepository, TransformedAssetRepository>();
        services.AddScoped<IAssetReportRepository, AssetReportRepository>();

        // Services
        services.AddScoped<IAssetTokenService, AssetTokenService>();
        services.AddScoped<IAssetStorageService, AssetStorageService>();
        services.AddScoped<IAssetUploadService, AssetUploadService>();
        services.AddScoped<IAssetAccessService, AssetAccessService>();
        services.AddScoped<IAssetUploadAuthorizationService, AssetUploadAuthorizationService>();
        services.AddScoped<IAssetFolderAuthorizationService, AssetFolderAuthorizationService>();
        services.AddScoped<IAssetLibraryService, AssetLibraryService>();
        services.AddScoped<IAssetScopedAccessService, AssetScopedAccessService>();
        services.AddScoped<IAssetModerationService, AssetModerationService>();
        services.AddScoped<IAssetTextExtractionService, AssetTextExtractionService>();

        // Security Services (Threat Mitigations)
        services.AddScoped<IAssetRateLimitService, AssetRateLimitService>();
        services.AddScoped<ITransformationValidator, TransformationValidator>();
        services.AddScoped<IVirusScanService, VirusScanService>();
        services.AddScoped<BackgroundServices.IAssetVirusScanProcessor, BackgroundServices.AssetVirusScanProcessor>();
        services.AddHostedService<BackgroundServices.VirusScanBackgroundService>();
        services.AddScoped<IAssetGarbageCollectionService, AssetGarbageCollectionService>();
        services.AddScoped<ITenantAssetValidationService, TenantAssetValidationService>();
        services.AddScoped<IDownloadWindowService, DownloadWindowService>();
        services.AddScoped<IOrderValidationService, CommerceOrderValidationService>();
        services.AddScoped<ISecureUploadService, SecureUploadService>();

        // Validators
        services.AddValidatorsFromAssemblyContaining<UploadAssetValidator>(ServiceLifetime.Scoped);

        // Command/Query Handlers
        services.AddScoped<IRequestHandler<UploadAssetCommand, UploadAssetResponse>, UploadAssetHandler>();
        services.AddScoped<IRequestHandler<GenerateAccessUrlCommand, GenerateAccessUrlResponse?>, GenerateAccessUrlHandler>();
        services.AddScoped<IRequestHandler<UpdateAssetCommand, UpdateAssetResponse?>, UpdateAssetHandler>();
        services.AddScoped<IRequestHandler<DeleteAssetCommand, DeleteAssetResponse>, DeleteAssetHandler>();
        services.AddScoped<IRequestHandler<BulkDeleteAssetsCommand, BulkDeleteAssetsResponse>, BulkDeleteAssetsHandler>();
        services.AddScoped<IRequestHandler<BulkUploadAssetsCommand, BulkUploadAssetsResponse>, BulkUploadAssetsHandler>();
        services.AddScoped<IRequestHandler<ReportAssetCommand, ReportAssetResponse?>, ReportAssetHandler>();
        services.AddScoped<IRequestHandler<ReviewReportCommand, ReviewReportResponse?>, ReviewReportHandler>();
        services.AddScoped<IRequestHandler<RunAssetRetentionCommand, AssetRetentionRunResponse>, RunAssetRetentionHandler>();
        services.AddScoped<IRequestHandler<SetAssetLegalHoldCommand, AssetLegalHoldResponse?>, SetAssetLegalHoldHandler>();

        services.AddScoped<IRequestHandler<GetAssetQuery, AssetDto?>, GetAssetHandler>();
        services.AddScoped<IRequestHandler<GetAssetPreviewQuery, AssetPreviewResponse?>, GetAssetPreviewHandler>();
        services.AddScoped<IRequestHandler<GetAssetsByParentQuery, IReadOnlyList<AssetDto>>, GetAssetsByParentHandler>();
        services.AddScoped<IRequestHandler<GetUserAssetsQuery, IReadOnlyList<AssetDto>>, GetUserAssetsHandler>();
        services.AddScoped<IRequestHandler<GetModerationQueueQuery, IReadOnlyList<ReportDto>>, GetModerationQueueHandler>();
        services.AddScoped<IRequestHandler<GetAssetReportsQuery, IReadOnlyList<ReportDto>>, GetAssetReportsHandler>();
        services.AddScoped<IRequestHandler<BulkGenerateAssetAccessUrlsQuery, BulkAssetAccessUrlsResponse>, BulkGenerateAssetAccessUrlsHandler>();
        services.AddScoped<IRequestHandler<SearchAssetsQuery, AssetSearchResponse>, SearchAssetsHandler>();
        services.AddScoped<IRequestHandler<GetAssetRetentionReportQuery, AssetRetentionReportResponse>, GetAssetRetentionReportHandler>();
        services.AddScoped<IRequestHandler<GetAssetStatisticsQuery, AssetStatisticsResponse>, GetAssetStatisticsHandler>();
        services.AddScoped<IRequestHandler<ExportAssetStatisticsQuery, AssetStatisticsExportResponse>, ExportAssetStatisticsHandler>();

        return services;
    }

    /// <summary>
    /// Configures EF Core model for Assets entities.
    /// </summary>
    public static ModelBuilder ConfigureAssetsEntities(this ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("assets");

        modelBuilder.ApplyConfiguration(new AssetContentConfiguration());
        modelBuilder.ApplyConfiguration(new AssetReferenceConfiguration());
        modelBuilder.ApplyConfiguration(new TransformedAssetConfiguration());
        modelBuilder.ApplyConfiguration(new AssetReportConfiguration());
        modelBuilder.ApplyConfiguration(new AssetFolderConfiguration());
        modelBuilder.ApplyConfiguration(new AssetReferenceRevisionConfiguration());
        modelBuilder.ApplyConfiguration(new AssetScopedAccessGrantConfiguration());

        return modelBuilder;
    }
}

/// <summary>
/// Module implementation for Assets.
/// </summary>
public class AssetsModule : ModuleBase
{
    public override string Name => "Assets";

    public override IServiceCollection ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAssetsModule(configuration);
        return services;
    }
}
