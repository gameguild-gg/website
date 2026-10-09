using System.Text.Json;
using GameGuild.CQRS;

namespace GameGuild.Identity.Tenants;

public sealed class GetTenantSettingsQueryHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<GetTenantSettingsQuery, TenantSettingsDto?>
{
    public async Task<TenantSettingsDto?> Handle(GetTenantSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false)
            ?? TenantSettings.CreateDefault(request.TenantId);

        return TenantSettingsMapper.ToDto(settings);
    }
}

public sealed class UpdateTenantSettingsCommandHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<UpdateTenantSettingsCommand>
{
    public async Task<Unit> Handle(UpdateTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsMapper.GetOrCreateSettingsAsync(tenantSettingsRepository, request.TenantId, cancellationToken).ConfigureAwait(false);
        TenantSettingsMapper.ApplyPartialUpdate(settings, request.Request);
        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class ReplaceTenantSettingsCommandHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<ReplaceTenantSettingsCommand>
{
    public async Task<Unit> Handle(ReplaceTenantSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsMapper.GetOrCreateSettingsAsync(tenantSettingsRepository, request.TenantId, cancellationToken).ConfigureAwait(false);
        TenantSettingsMapper.ApplyReplacement(settings, request.Request);
        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class GetTenantFeatureFlagsQueryHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<GetTenantFeatureFlagsQuery, Dictionary<string, bool>?>
{
    public async Task<Dictionary<string, bool>?> Handle(GetTenantFeatureFlagsQuery request, CancellationToken cancellationToken)
    {
        var settings = await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false)
            ?? TenantSettings.CreateDefault(request.TenantId);

        return TenantSettingsMapper.GetExtras(settings).FeatureFlags;
    }
}

public sealed class UpdateTenantFeatureFlagsCommandHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<UpdateTenantFeatureFlagsCommand>
{
    public async Task<Unit> Handle(UpdateTenantFeatureFlagsCommand request, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsMapper.GetOrCreateSettingsAsync(tenantSettingsRepository, request.TenantId, cancellationToken).ConfigureAwait(false);
        var extras = TenantSettingsMapper.GetExtras(settings);

        foreach (var flag in request.Request.FeatureFlags)
        {
            extras.FeatureFlags[flag.Key] = flag.Value;
        }

        TenantSettingsMapper.SaveExtras(settings, extras);
        settings.Touch();
        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class GetTenantSystemLimitsQueryHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<GetTenantSystemLimitsQuery, TenantSystemLimitsDto?>
{
    public async Task<TenantSystemLimitsDto?> Handle(GetTenantSystemLimitsQuery request, CancellationToken cancellationToken)
    {
        var settings = await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false)
            ?? TenantSettings.CreateDefault(request.TenantId);

        return TenantSettingsMapper.ToSystemLimitsDto(settings, TenantSettingsMapper.GetExtras(settings));
    }
}

public sealed class UpdateTenantSystemLimitsCommandHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<UpdateTenantSystemLimitsCommand>
{
    public async Task<Unit> Handle(UpdateTenantSystemLimitsCommand request, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsMapper.GetOrCreateSettingsAsync(tenantSettingsRepository, request.TenantId, cancellationToken).ConfigureAwait(false);
        var extras = TenantSettingsMapper.GetExtras(settings);
        TenantSettingsMapper.ApplySystemLimits(settings, extras, request.Request);
        TenantSettingsMapper.SaveExtras(settings, extras);
        settings.Touch();
        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class GetTenantIntegrationSettingsQueryHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<GetTenantIntegrationSettingsQuery, TenantIntegrationSettingsDto?>
{
    public async Task<TenantIntegrationSettingsDto?> Handle(GetTenantIntegrationSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await tenantSettingsRepository.GetByTenantIdAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
        return TenantIntegrationSettingsSerializer.Deserialize(settings?.IntegrationSettingsJson);
    }
}

public sealed class UpdateTenantIntegrationSettingsCommandHandler(ITenantSettingsRepository tenantSettingsRepository)
    : IRequestHandler<UpdateTenantIntegrationSettingsCommand>
{
    public async Task<Unit> Handle(UpdateTenantIntegrationSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await TenantSettingsMapper.GetOrCreateSettingsAsync(tenantSettingsRepository, request.TenantId, cancellationToken).ConfigureAwait(false);
        var current = TenantIntegrationSettingsSerializer.Deserialize(settings.IntegrationSettingsJson);
        settings.IntegrationSettingsJson = TenantIntegrationSettingsSerializer.Serialize(TenantIntegrationSettingsSerializer.Merge(current, request.Request));
        settings.Touch();
        await tenantSettingsRepository.UpdateAsync(settings, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

internal static class TenantSettingsMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<TenantSettings> GetOrCreateSettingsAsync(ITenantSettingsRepository tenantSettingsRepository, Guid tenantId, CancellationToken ct)
    {
        var settings = await tenantSettingsRepository.GetByTenantIdAsync(tenantId, ct).ConfigureAwait(false);
        if (settings is not null)
        {
            return settings;
        }

        settings = TenantSettings.CreateDefault(tenantId);
        return await tenantSettingsRepository.CreateAsync(settings, ct).ConfigureAwait(false);
    }

    public static TenantSettingsDto ToDto(TenantSettings settings)
    {
        var extras = GetExtras(settings);

        return new TenantSettingsDto(
            settings.TenantId,
            ToSystemConfigurationDto(settings, extras),
            extras.FeatureFlags,
            extras.BusinessRules,
            ReadJson(settings.BrandingSettings, DefaultUiSettings()),
            ReadJson(settings.SecuritySettings, DefaultSecuritySettings(settings)),
            TenantIntegrationSettingsSerializer.Deserialize(settings.IntegrationSettingsJson),
            ToSystemLimitsDto(settings, extras),
            ToOffset(settings.CreatedAt),
            ToOffset(settings.UpdatedAt)
        );
    }

    public static void ApplyPartialUpdate(TenantSettings settings, UpdateTenantSettingsRequest request)
    {
        var extras = GetExtras(settings);

        if (request.SystemConfiguration is not null)
        {
            ApplySystemConfiguration(settings, extras, request.SystemConfiguration);
        }

        if (request.FeatureFlags is not null)
        {
            foreach (var flag in request.FeatureFlags)
            {
                extras.FeatureFlags[flag.Key] = flag.Value;
            }
        }

        if (request.BusinessRules is not null)
        {
            extras.BusinessRules = MergeBusinessRules(extras.BusinessRules, request.BusinessRules);
        }

        if (request.UserInterfaceSettings is not null)
        {
            settings.BrandingSettings = WriteJson(MergeUiSettings(ReadJson(settings.BrandingSettings, DefaultUiSettings()), request.UserInterfaceSettings));
        }

        if (request.SecuritySettings is not null)
        {
            settings.SecuritySettings = WriteJson(MergeSecuritySettings(ReadJson(settings.SecuritySettings, DefaultSecuritySettings(settings)), request.SecuritySettings));
        }

        if (request.IntegrationSettings is not null)
        {
            var current = TenantIntegrationSettingsSerializer.Deserialize(settings.IntegrationSettingsJson);
            settings.IntegrationSettingsJson = TenantIntegrationSettingsSerializer.Serialize(TenantIntegrationSettingsSerializer.Merge(current, request.IntegrationSettings));
        }

        if (request.SystemLimits is not null)
        {
            ApplySystemLimits(settings, extras, request.SystemLimits);
        }

        SaveExtras(settings, extras);
        settings.Touch();
    }

    public static void ApplyReplacement(TenantSettings settings, ReplaceTenantSettingsRequest request)
    {
        var extras = new TenantSettingsExtras
        {
            FeatureFlags = new Dictionary<string, bool>(request.FeatureFlags),
            BusinessRules = new TenantBusinessRulesDto(
                request.BusinessRules.WorkflowRules ?? new Dictionary<string, object?>(),
                request.BusinessRules.ValidationRules ?? new Dictionary<string, object?>(),
                request.BusinessRules.ApprovalRules ?? new Dictionary<string, object?>(),
                request.BusinessRules.NotificationRules ?? new Dictionary<string, object?>()
            )
        };

        ApplySystemConfiguration(settings, extras, request.SystemConfiguration);
        ApplySystemLimits(settings, extras, request.SystemLimits);
        settings.BrandingSettings = WriteJson(MergeUiSettings(DefaultUiSettings(), request.UserInterfaceSettings));
        settings.SecuritySettings = WriteJson(MergeSecuritySettings(DefaultSecuritySettings(settings), request.SecuritySettings));
        settings.IntegrationSettingsJson = TenantIntegrationSettingsSerializer.Serialize(TenantIntegrationSettingsSerializer.Merge(TenantIntegrationSettingsSerializer.Empty(), request.IntegrationSettings));
        SaveExtras(settings, extras);
        settings.Touch();
    }

    public static void ApplySystemLimits(TenantSettings settings, TenantSettingsExtras extras, UpdateTenantSystemLimitsRequest request)
    {
        if (request.MaxUsers.HasValue)
        {
            settings.MaxUsers = request.MaxUsers;
        }

        if (request.MaxStorage.HasValue)
        {
            settings.StorageQuota = request.MaxStorage;
        }

        extras.SystemLimits = extras.SystemLimits with
        {
            MaxApiCalls = request.MaxApiCalls ?? extras.SystemLimits.MaxApiCalls,
            MaxProjects = request.MaxProjects ?? extras.SystemLimits.MaxProjects,
            CustomLimits = Merge(extras.SystemLimits.CustomLimits, request.CustomLimits)
        };
    }

    public static TenantSystemLimitsDto ToSystemLimitsDto(TenantSettings settings, TenantSettingsExtras extras)
        => new(
            settings.MaxUsers ?? 0,
            settings.StorageQuota ?? 0L,
            extras.SystemLimits.MaxApiCalls,
            extras.SystemLimits.MaxProjects,
            extras.SystemLimits.CustomLimits
        );

    public static TenantSettingsExtras GetExtras(TenantSettings settings)
        => ReadJson(settings.NotificationSettings, new TenantSettingsExtras());

    public static void SaveExtras(TenantSettings settings, TenantSettingsExtras extras)
        => settings.NotificationSettings = WriteJson(extras);

    private static void ApplySystemConfiguration(TenantSettings settings, TenantSettingsExtras extras, UpdateTenantSystemConfigurationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Locale))
        {
            settings.DefaultLanguage = request.Locale;
        }

        if (!string.IsNullOrWhiteSpace(request.TimeZone))
        {
            settings.DefaultTimezone = request.TimeZone;
        }

        if (!string.IsNullOrWhiteSpace(request.CurrencySettings?.DefaultCurrency))
        {
            settings.DefaultCurrency = request.CurrencySettings.DefaultCurrency;
        }

        extras.SystemConfiguration = extras.SystemConfiguration with
        {
            DateFormat = request.DateFormat ?? extras.SystemConfiguration.DateFormat,
            NumberFormat = request.NumberFormat ?? extras.SystemConfiguration.NumberFormat,
            CurrencyDisplayFormat = request.CurrencySettings?.DisplayFormat ?? extras.SystemConfiguration.CurrencyDisplayFormat,
            CurrencyDecimalPlaces = request.CurrencySettings?.DecimalPlaces ?? extras.SystemConfiguration.CurrencyDecimalPlaces,
            CustomConfiguration = Merge(extras.SystemConfiguration.CustomConfiguration, request.CustomConfiguration)
        };
    }

    private static TenantSystemConfigurationDto ToSystemConfigurationDto(TenantSettings settings, TenantSettingsExtras extras)
        => new(
            settings.DefaultTimezone,
            settings.DefaultLanguage,
            extras.SystemConfiguration.DateFormat,
            extras.SystemConfiguration.NumberFormat,
            new TenantCurrencySettingsDto(settings.DefaultCurrency, extras.SystemConfiguration.CurrencyDisplayFormat, extras.SystemConfiguration.CurrencyDecimalPlaces),
            extras.SystemConfiguration.CustomConfiguration
        );

    private static TenantBusinessRulesDto MergeBusinessRules(TenantBusinessRulesDto current, UpdateTenantBusinessRulesRequest update)
        => new(
            Merge(current.WorkflowRules, update.WorkflowRules),
            Merge(current.ValidationRules, update.ValidationRules),
            Merge(current.ApprovalRules, update.ApprovalRules),
            Merge(current.NotificationRules, update.NotificationRules)
        );

    private static TenantUiSettingsDto MergeUiSettings(TenantUiSettingsDto current, UpdateTenantUiSettingsRequest update)
        => new(
            update.Theme ?? current.Theme,
            Merge(current.Layout, update.Layout),
            update.Branding is null
                ? current.Branding
                : new TenantBrandingDto(
                    update.Branding.LogoUrl ?? current.Branding.LogoUrl,
                    update.Branding.FaviconUrl ?? current.Branding.FaviconUrl,
                    update.Branding.PrimaryColor ?? current.Branding.PrimaryColor,
                    update.Branding.SecondaryColor ?? current.Branding.SecondaryColor,
                    update.Branding.CompanyName ?? current.Branding.CompanyName
                ),
            update.CustomCss ?? current.CustomCss,
            Merge(current.ComponentSettings, update.ComponentSettings)
        );

    private static TenantSecuritySettingsDto MergeSecuritySettings(TenantSecuritySettingsDto current, UpdateTenantSecuritySettingsRequest update)
        => new(
            Merge(current.PasswordPolicy, update.PasswordPolicy),
            update.SessionTimeout ?? current.SessionTimeout,
            update.TwoFactorRequired ?? current.TwoFactorRequired,
            update.IpWhitelist ?? current.IpWhitelist,
            Merge(current.ApiRateLimits, update.ApiRateLimits)
        );

    private static Dictionary<TKey, TValue> Merge<TKey, TValue>(Dictionary<TKey, TValue> current, Dictionary<TKey, TValue>? update)
        where TKey : notnull
    {
        var merged = new Dictionary<TKey, TValue>(current);
        if (update is null)
        {
            return merged;
        }

        foreach (var item in update)
        {
            merged[item.Key] = item.Value;
        }

        return merged;
    }

    private static TenantUiSettingsDto DefaultUiSettings()
        => new("default", new Dictionary<string, object?>(), new TenantBrandingDto(null, null, null, null, null), null, new Dictionary<string, object?>());

    private static TenantSecuritySettingsDto DefaultSecuritySettings(TenantSettings settings)
        => new(new Dictionary<string, object?>(), 3600, settings.RequireTwoFactorAuth, new List<string>(), new Dictionary<string, int>());

    private static T ReadJson<T>(string? json, T fallback)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return fallback;
        }

        try { return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? fallback; }
        catch (JsonException) { return fallback; }
    }

    private static string WriteJson<T>(T value)
        => JsonSerializer.Serialize(value, JsonOptions);

    private static DateTimeOffset ToOffset(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return new DateTimeOffset(utc);
    }
}

public sealed record TenantSettingsExtras
{
    public Dictionary<string, bool> FeatureFlags { get; set; } = new();
    public TenantBusinessRulesDto BusinessRules { get; set; } = new(new Dictionary<string, object?>(), new Dictionary<string, object?>(), new Dictionary<string, object?>(), new Dictionary<string, object?>());
    public TenantSystemConfigurationExtras SystemConfiguration { get; set; } = new();
    public TenantSystemLimitsExtras SystemLimits { get; set; } = new();
}

public sealed record TenantSystemConfigurationExtras
{
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public string NumberFormat { get; init; } = "N2";
    public string CurrencyDisplayFormat { get; init; } = "{0:C}";
    public int CurrencyDecimalPlaces { get; init; } = 2;
    public Dictionary<string, object?> CustomConfiguration { get; init; } = new();
}

public sealed record TenantSystemLimitsExtras
{
    public int MaxApiCalls { get; init; } = 10000;
    public int MaxProjects { get; init; } = 50;
    public Dictionary<string, int> CustomLimits { get; init; } = new();
}
