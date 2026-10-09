using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace GameGuild.Localization;

/// <summary>
/// Default implementation of ILocalizedErrorService.
/// Uses in-memory fallback dictionary with database lookup for tenant-specific overrides.
/// </summary>
public class LocalizedErrorService : ILocalizedErrorService
{
    private readonly ILocalizationContext _localizationContext;
    private readonly ILanguageRepository _languageRepository;
    private readonly ILocalizationService? _localizationService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LocalizedErrorService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);

    // Fallback messages in English (always available)
    private static readonly ConcurrentDictionary<string, string> FallbackMessages = new(
        new Dictionary<string, string>
        {
            // Validation
            [ErrorMessageKeys.Validation.Required] = "The {0} field is required.",
            [ErrorMessageKeys.Validation.MinLength] = "The {0} field must be at least {1} characters.",
            [ErrorMessageKeys.Validation.MaxLength] = "The {0} field must not exceed {1} characters.",
            [ErrorMessageKeys.Validation.Email] = "The {0} field must be a valid email address.",
            [ErrorMessageKeys.Validation.Range] = "The {0} field must be between {1} and {2}.",
            [ErrorMessageKeys.Validation.Regex] = "The {0} field format is invalid.",
            [ErrorMessageKeys.Validation.Comparison] = "The {0} and {1} fields must match.",
            [ErrorMessageKeys.Validation.Unique] = "A {0} with this value already exists.",

            // Auth
            [ErrorMessageKeys.Auth.Unauthorized] = "Authentication is required to access this resource.",
            [ErrorMessageKeys.Auth.Forbidden] = "You do not have permission to access this resource.",
            [ErrorMessageKeys.Auth.TokenExpired] = "Your session has expired. Please sign in again.",
            [ErrorMessageKeys.Auth.InvalidCredentials] = "Invalid email or password.",
            [ErrorMessageKeys.Auth.AccountLocked] = "Your account has been locked. Please contact support.",
            [ErrorMessageKeys.Auth.AccountDisabled] = "Your account has been disabled.",
            [ErrorMessageKeys.Auth.SessionExpired] = "Your session has expired.",

            // Resource
            [ErrorMessageKeys.Resource.NotFound] = "The requested {0} was not found.",
            [ErrorMessageKeys.Resource.AlreadyExists] = "A {0} with this identifier already exists.",
            [ErrorMessageKeys.Resource.Conflict] = "The {0} has been modified by another user. Please refresh and try again.",
            [ErrorMessageKeys.Resource.Deleted] = "This {0} has been deleted.",

            // Quota
            [ErrorMessageKeys.Quota.Exceeded] = "You have exceeded your {0} quota limit.",
            [ErrorMessageKeys.Quota.NearLimit] = "You are approaching your {0} quota limit ({1}% used).",
            [ErrorMessageKeys.Quota.StorageFull] = "Your storage quota is full. Please upgrade or delete some files.",

            // System
            [ErrorMessageKeys.System.InternalError] = "An unexpected error occurred. Please try again later.",
            [ErrorMessageKeys.System.ServiceUnavailable] = "The service is temporarily unavailable. Please try again later.",
            [ErrorMessageKeys.System.MaintenanceMode] = "The system is currently under maintenance. Please try again later.",
            [ErrorMessageKeys.System.RateLimited] = "Too many requests. Please wait before trying again.",

            // Asset
            [ErrorMessageKeys.Asset.VirusDetected] = "The uploaded file contains malware and has been rejected.",
            [ErrorMessageKeys.Asset.ModerationRejected] = "The content was rejected due to policy violations: {0}.",
            [ErrorMessageKeys.Asset.ContentWarning] = "This content may contain: {0}.",
            [ErrorMessageKeys.Asset.TokenExpired] = "The access link has expired. Please request a new one.",
            [ErrorMessageKeys.Asset.TokenInvalid] = "The access link is invalid.",
            [ErrorMessageKeys.Asset.DownloadLimitExceeded] = "Download limit exceeded. Please try again later."
        });

    /// <summary>
    /// Well-known resource ID used to store error message overrides in the ResourceLocalization table.
    /// Error keys are stored as FieldName values under this resource ID with ResourceType "ErrorMessage".
    /// </summary>
    private static readonly Guid ErrorMessageResourceId = new("A0000000-0000-0000-0000-E00000000001");

    public LocalizedErrorService(
        ILocalizationContext localizationContext,
        ILanguageRepository languageRepository,
        IMemoryCache cache,
        ILogger<LocalizedErrorService> logger,
        ILocalizationService? localizationService = null)
    {
        _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
        _languageRepository = languageRepository ?? throw new ArgumentNullException(nameof(languageRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _localizationService = localizationService;
    }

    public string GetErrorMessage(string errorKey, params object[] args)
    {
        return GetErrorMessage(errorKey, _localizationContext.CurrentUiCulture, args);
    }

    public string GetErrorMessage(string errorKey, CultureInfo culture, params object[] args)
    {
        var message = GetLocalizedString(errorKey, culture);
        
        if (args.Length > 0)
        {
            try
            {
                return string.Format(culture, message, args);
            }
            catch (FormatException ex)
            {
                _logger.LogWarning(ex, "Failed to format error message {ErrorKey} with {ArgCount} arguments", 
                    LogRedaction.Sanitize(errorKey), args.Length);
                return message;
            }
        }

        return message;
    }

    public string GetValidationMessage(string validationKey, string fieldName, params object[] args)
    {
        var fullKey = validationKey.StartsWith("validation.") 
            ? validationKey 
            : $"validation.{validationKey}";

        var allArgs = new object[args.Length + 1];
        allArgs[0] = fieldName;
        Array.Copy(args, 0, allArgs, 1, args.Length);

        return GetErrorMessage(fullKey, allArgs);
    }

    public string GetSystemMessage(string messageKey, params object[] args)
    {
        return GetErrorMessage(messageKey, args);
    }

    public bool HasTranslation(string key)
    {
        return FallbackMessages.ContainsKey(key) || HasDatabaseTranslation(key);
    }

    private string GetLocalizedString(string key, CultureInfo culture)
    {
        var cacheKey = $"error_msg:{culture.Name}:{key}";

        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = _cacheExpiration;
            
            // Attempt DB lookup for tenant-specific overrides via ResourceLocalization.
            // Convention: error messages are stored under a well-known ResourceId with the
            // error key as the FieldName and ResourceType "ErrorMessage".
            var dbMessage = TryGetDatabaseTranslation(key, culture);
            if (dbMessage is not null)
            {
                return dbMessage;
            }
            
            if (FallbackMessages.TryGetValue(key, out var message))
            {
                return message;
            }

            _logger.LogWarning("Missing translation for key {Key} in culture {Culture}", LogRedaction.Sanitize(key), culture.Name);
            return key; // Return the key itself as fallback
        })!;
    }

    private string? TryGetDatabaseTranslation(string key, CultureInfo culture)
    {
        if (_localizationService is null)
        {
            return null;
        }

        try
        {
            var language = _languageRepository.GetByCodeAsync(culture.Name).GetAwaiter().GetResult();
            if (language is null)
            {
                return null;
            }

            var localizations = _localizationService
                .GetLocalizationsForFieldAsync(ErrorMessageResourceId, key)
                .GetAwaiter().GetResult();

            var match = localizations.FirstOrDefault(l => l.LanguageId == language.Id);
            return match?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Database lookup failed for error key {Key} in culture {Culture}, using fallback", 
                LogRedaction.Sanitize(key), culture.Name);
            return null;
        }
    }

    private bool HasDatabaseTranslation(string key)
    {
        if (_localizationService is null)
        {
            return false;
        }

        try
        {
            var localizations = _localizationService
                .GetLocalizationsForFieldAsync(ErrorMessageResourceId, key)
                .GetAwaiter().GetResult();
            return localizations.Count > 0;
        }
        catch
        {
            return false;
        }
    }
}
