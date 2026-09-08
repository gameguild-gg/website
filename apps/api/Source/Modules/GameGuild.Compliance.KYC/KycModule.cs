using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GameGuild.Compliance.KYC;

public sealed class KycModule : ModuleBase
{
    public override string Name => "Compliance.KYC";
    public override bool EnabledByDefault => true;

    public override IServiceCollection ConfigureServices(
        IServiceCollection services,
        IConfiguration configuration) => services.AddKycComposition(configuration);
}

public static class KycCompositionExtensions
{
    public static IServiceCollection AddKycComposition(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddOptions<SumSubKycAmlOptions>()
            .Bind(configuration.GetSection(SumSubKycAmlOptions.SectionName));
        services.AddOptions<KycPolicyOptions>()
            .Bind(configuration.GetSection(KycPolicyOptions.SectionName));
        services.AddOptions<ComplianceRawObjectStoreOptions>()
            .Bind(configuration.GetSection(ComplianceRawObjectStoreOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IKycRepository, KycRepository>();
        services.TryAddScoped<IKycService, KycService>();
        services.TryAddScoped<IKycEvidenceStore, UnavailableKycEvidenceStore>();
        services.AddHttpClient<IKycAmlProvider, SumSubKycAmlProvider>();
        if (configuration.GetValue<bool>($"{ComplianceRawObjectStoreOptions.SectionName}:Enabled"))
        {
            services.TryAddSingleton<IAmazonS3>(_ => new AmazonS3Client());
            services.TryAddSingleton<IComplianceRawObjectStore, S3ComplianceRawObjectStore>();
        }
        else
        {
            services.TryAddSingleton<IComplianceRawObjectStore, UnavailableComplianceRawObjectStore>();
        }
        services.TryAddScoped<IKycAmlOrchestrator, SumSubKycAmlOrchestrator>();
        return services;
    }
}
