using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using GameGuild.Configuration;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;
using GameGuild.Configuration.PresentationLayer.OpenAPI;
using GameGuild.Learning.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using System.Text;
using Swashbuckle.AspNetCore.SwaggerGen;
using ApiVersioningOptions = GameGuild.Configuration.PresentationLayer.ApiVersioning.ApiVersioningOptions;

namespace GameGuild.API;

/// <summary>
///     Extension methods for configuring OpenAPI/Swagger services.
/// </summary>
public static class OpenApiExtensions
{
    internal const string AllowAnonymousExtensionName = "x-gameguild-allow-anonymous";

    /// <summary>
    ///     Sets up OpenAPI/Swagger with configurable options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="options">OpenAPI options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection SetupOpenApi(this IServiceCollection services, IConfiguration configuration,
        OpenApiOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "OpenApi", OpenApiOptions.CreateDefault);
        options.Validate();

        // Add native .NET 9 OpenAPI support
        // JSON serialization options are configured globally in Program.cs
        services.AddOpenApi(openApiOptions =>
        {
            // Custom document transformer for any OpenAPI customizations
            openApiOptions.AddDocumentTransformer<OpenApiDocumentTransformer>();
        });

        // Register custom document transformer
        services.AddSingleton<OpenApiDocumentTransformer>();

        // Add Swashbuckle for Swagger UI
        services.AddSwaggerGen(c =>
            {
                // If API Versioning is enabled, register a Swagger document per discovered API version
                using var providerScope = services.BuildServiceProvider();
                var provider = providerScope.GetService<IApiVersionDescriptionProvider>();

                if (provider is not null)
                {
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        c.SwaggerDoc(
                            description.GroupName,
                            new OpenApiInfo
                            {
                                Title = options.Title,
                                Version = description.ApiVersion.ToString(),
                                Description = options.Description,
                                Contact = new OpenApiContact
                                {
                                    Name = options.ContactName,
                                    Email = options.ContactEmail,
                                    Url = !string.IsNullOrEmpty(options.ContactUrl) ? new Uri(options.ContactUrl) : null
                                }
                            }
                        );
                    }

                    // Ensure only endpoints from the corresponding API version are included in each document
                    // Check API version instead of GroupName to allow custom ApiExplorerSettings GroupName
                    c.DocInclusionPredicate((docName, apiDesc) =>
                        {
                            if (apiDesc.ActionDescriptor is not ControllerActionDescriptor cad)
                                return string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase);

                            if (cad.ControllerTypeInfo.GetCustomAttributes(typeof(ApiVersionAttribute), false)
                                    .FirstOrDefault() is not ApiVersionAttribute apiVersionAttr)
                                return string.Equals(apiDesc.GroupName, docName, StringComparison.OrdinalIgnoreCase);
                            var version = apiVersionAttr.Versions.FirstOrDefault();

                            return version != null && docName.Equals($"v{version.MajorVersion}",
                                StringComparison.OrdinalIgnoreCase);
                        }
                    );
                }
                else
                {
                    // Fallback to single document when versioning is not configured
                    c.SwaggerDoc(
                        options.Version,
                        new OpenApiInfo
                        {
                            Title = options.Title,
                            Version = options.Version,
                            Description = options.Description,
                            Contact = new OpenApiContact
                            {
                                Name = options.ContactName,
                                Email = options.ContactEmail,
                                Url = !string.IsNullOrEmpty(options.ContactUrl) ? new Uri(options.ContactUrl) : null
                            }
                        }
                    );
                }

                // Configure schema ID generator to use full module path for guaranteed uniqueness
                // e.g., "GameGuild.Identity.Tenants.TenantSettingsDto" -> "Identity_Tenants_TenantSettingsDto"
                // e.g., "GameGuild.Identity.Users.UserDto" -> "Identity_Users_UserDto"
                // e.g., "GameGuild.Identity.Authentication.UserDto" -> "Identity_Authentication_UserDto"
                c.CustomSchemaIds(type =>
                {
                    var fullName = type.FullName ?? type.Name;
                    var parts = fullName.Split('.');

                    if (type.IsGenericType)
                    {
                        // For generic types, include module path + generic type + args
                        var genericTypeName = type.Name.Split('`')[0];
                        var genericArgs = string.Join("", type.GetGenericArguments().Select(t => t.Name));

                        // Find module path (everything between "GameGuild" and the type name)
                        var gameGuildIndex = Array.IndexOf(parts, "GameGuild");
                        if (gameGuildIndex >= 0 && parts.Length > gameGuildIndex + 2)
                        {
                            // Join all parts between GameGuild and the type name with underscores
                            var modulePath = string.Join("_", parts.Skip(gameGuildIndex + 1).Take(parts.Length - gameGuildIndex - 2));
                            return $"{modulePath}_{genericTypeName}{genericArgs}";
                        }

                        return $"{genericTypeName}{genericArgs}";
                    }

                    // For GameGuild types, use full Module_Submodule_TypeName pattern
                    // e.g., GameGuild.Identity.Users.UserDto -> Identity_Users_UserDto
                    // e.g., GameGuild.Commerce.Products.ProductDto -> Commerce_Products_ProductDto
                    if (parts.Length >= 4 && parts[0] == "GameGuild")
                    {
                        // Join all module segments (everything between GameGuild and the type name)
                        var modulePath = string.Join("_", parts.Skip(1).Take(parts.Length - 2));
                        var typeName = parts[^1];  // Last part is the type name
                        return $"{modulePath}_{typeName}";
                    }

                    // For shorter GameGuild types (e.g., GameGuild.SomeType)
                    if (parts.Length >= 2 && parts[0] == "GameGuild")
                    {
                        return string.Join("_", parts.Skip(1));
                    }

                    // For non-GameGuild types (external libraries, etc.)
                    if (parts.Length >= 2)
                    {
                        return $"{parts[^2]}_{parts[^1]}";
                    }

                    return type.Name;
                });

                // Normalize controller tags into a consistent module/controller path.
                c.OperationFilter<ModuleControllerTagOperationFilter>();
                c.OperationFilter<AllowAnonymousOperationFilter>();
                c.SchemaFilter<FlagsEnumSchemaFilter>();
                c.SchemaFilter<LegacyProgramContentTypeSchemaFilter>();

                // Add security definition for JWT Bearer token
                c.AddSecurityDefinition(
                    "Bearer",
                    new OpenApiSecurityScheme
                    {
                        Description =
                            "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                        Name = "Authorization",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "Bearer"
                    }
                );

                c.AddSecurityRequirement(
                    new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme
                            {
                                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                                Scheme = "oauth2", Name = "Bearer", In = ParameterLocation.Header
                            },
                            new List<string>()
                        }
                    }
                );
            }
        );

        return services;
    }

    /// <summary>
    ///     Helper function to normalize schema names.
    ///     Note: We intentionally keep the "Dto" suffix to avoid schema ID conflicts
    ///     between entities (e.g., TenantSettings) and their DTOs (e.g., TenantSettingsDto).
    /// </summary>
    private static string NormalizeSchemaName(string name)
    {
        // DO NOT strip "Dto" suffix - keeping it prevents conflicts between
        // entities and DTOs with similar names (e.g., TenantSettings vs TenantSettingsDto)

        // Convert "Result" suffix to "Response" for consistency
        if (name.EndsWith("Result", StringComparison.Ordinal))
            name = name[..^6] + "Response";

        return name;
    }

    /// <summary>
    ///     Sets up API versioning with configurable options.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="options">API versioning options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection SetupApiVersioning(this IServiceCollection services, IConfiguration configuration,
        ApiVersioningOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "ApiVersioning",
            ApiVersioningOptions.CreateDefault);
        options.Validate();

        services.AddApiVersioning(setup =>
                {
                    setup.AssumeDefaultVersionWhenUnspecified = options.AssumeDefaultVersionWhenUnspecified;
                    // Parse DefaultVersion (e.g., "1.0") into ApiVersion
                    var versionParts = options.DefaultVersion.Split('.');
                    var major = int.TryParse(versionParts.ElementAtOrDefault(0) ?? "1", out var mj) ? mj : 1;
                    var minor = int.TryParse(versionParts.ElementAtOrDefault(1) ?? "0", out var mn) ? mn : 0;
                    setup.DefaultApiVersion = new ApiVersion(major, minor);
                    setup.ApiVersionReader = ApiVersioningOptionsBuilder.CreateReader(options.ReadingStrategy, options);
                }
            )
            .AddApiExplorer(setup =>
                {
                    setup.GroupNameFormat = options.GroupNameFormat;
                    setup.SubstituteApiVersionInUrl = options.SubstituteApiVersionInUrl;
                }
            );

        return services;
    }

    /// <summary>
    ///     Sets up API Explorer.
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configuration">The application configuration</param>
    /// <param name="options">API versioning options</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection SetupApiExplorer(this IServiceCollection services, IConfiguration configuration,
        ApiVersioningOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "ApiVersioning",
            ApiVersioningOptions.CreateDefault);
        options.Validate();

        // API Explorer is now configured as part of API Versioning setup
        services.AddEndpointsApiExplorer();

        return services;
    }
}

/// <summary>
///     Custom OpenAPI document transformer that ensures proper serialization
/// </summary>
internal sealed class FlagsEnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum || !context.Type.IsDefined(typeof(FlagsAttribute), inherit: false))
            return;

        schema.Type = "string";
        schema.Format = null;
        schema.Enum?.Clear();
        schema.Description = "A comma-separated combination of the declared flag names.";
    }
}

/// <summary>
/// Keeps historical database enum values readable while preventing new API clients from
/// authoring obsolete content types. The write/mapping layer still accepts and normalizes
/// Page and Challenge records created before the migration.
/// </summary>
internal sealed class LegacyProgramContentTypeSchemaFilter : ISchemaFilter
{
    private static readonly HashSet<string> LegacyValues =
    [
        nameof(ProgramContentType.Page),
        nameof(ProgramContentType.Challenge),
    ];

    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(ProgramContentType) || schema.Enum is null)
            return;

        schema.Enum = schema.Enum
            .Where(value => value is not OpenApiString text || !LegacyValues.Contains(text.Value))
            .ToList();
        schema.Description = $"{schema.Description} Legacy values Page and Challenge are normalized on read and are not valid for new content.".Trim();
    }
}
internal sealed class OpenApiDocumentTransformer : Microsoft.AspNetCore.OpenApi.IOpenApiDocumentTransformer
{
    public Task TransformAsync(Microsoft.OpenApi.Models.OpenApiDocument document, Microsoft.AspNetCore.OpenApi.OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        // Document transformation is currently handled by the default pipeline.
        return Task.CompletedTask;
    }
}

internal sealed class ModuleControllerTagOperationFilter : IOperationFilter
{
    private static readonly HashSet<string> CanonicalRoots =
    [
        "access-control",
        "analytics",
        "assets",
        "auth",
        "commerce",
        "compliance",
        "content",
        "features",
        "gamification",
        "health",
        "learning",
        "monitoring",
        "notifications",
        "projects",
        "resources",
        "social",
        "tenants",
        "testing-lab",
        "users"
    ];

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controllerAction)
            return;

        var explicitTag = GetExplicitControllerTag(controllerAction);
        var tag = BuildTag(controllerAction.ControllerTypeInfo.Namespace, controllerAction.ControllerName, explicitTag);
        if (string.IsNullOrWhiteSpace(tag))
            return;

        operation.Tags = new List<OpenApiTag> { new() { Name = tag } };
    }

    private static string? GetExplicitControllerTag(ControllerActionDescriptor controllerAction)
    {
        return controllerAction.ControllerTypeInfo
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Http.TagsAttribute), inherit: false)
            .OfType<Microsoft.AspNetCore.Http.TagsAttribute>()
            .SelectMany(attribute => attribute.Tags ?? Array.Empty<string>())
            .FirstOrDefault(tag => !string.IsNullOrWhiteSpace(tag));
    }

    private static string BuildTag(string? controllerNamespace, string controllerName, string? explicitTag)
    {
        if (TryGetCanonicalControllerTag(controllerName, out var canonicalTag))
            return canonicalTag;

        var prefix = GetCanonicalPrefix(controllerNamespace, controllerName);

        if (!string.IsNullOrWhiteSpace(explicitTag))
            return NormalizeExplicitTag(prefix, explicitTag);

        var leaf = NormalizeLeaf(prefix, controllerName);
        if (string.IsNullOrWhiteSpace(prefix))
            return leaf;

        if (string.IsNullOrWhiteSpace(leaf))
            return prefix;

        return $"{prefix}/{leaf}";
    }

    private static bool TryGetCanonicalControllerTag(string controllerName, out string tag)
    {
        tag = ToKebabCase(controllerName) switch
        {
            "api-key" => "auth/api-keys",
            "auth" => "auth",
            "mfa" => "auth/multi-factor",
            "session" => "auth/sessions",
            "key-rotation" => "auth/signing-keys",
            "trusted-devices" => "auth/trusted-devices",
            "web-authn" => "auth/webauthn",
            "roles" => "auth/roles",
            "permission-admin" => "auth/permissions/admin",
            "permission-evaluation" => "auth/permissions/evaluation",
            "permission-grants" => "auth/permissions/grants",
            "service-account-crud" => "auth/service-accounts",
            "service-account-operations" => "auth/service-accounts",
            "service-account-token" => "auth/service-accounts/tokens",
            "access-review-analytics" => "access-control/access-reviews/analytics",
            "access-review-campaign" => "access-control/access-reviews/campaigns",
            "access-review-item" => "access-control/access-reviews/items",
            "abac-policy" => "access-control/abac-policies",
            "conditional-policy-crud" => "access-control/conditional-policies",
            "conditional-policy-evaluation" => "access-control/conditional-policies/evaluations",
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(tag);
    }

    private static string GetCanonicalPrefix(string? controllerNamespace, string controllerName)
    {
        if (string.IsNullOrWhiteSpace(controllerNamespace))
            return string.Empty;

        if (string.Equals(controllerNamespace, "GameGuild.API.Controllers", StringComparison.Ordinal))
            return string.Empty;

        return controllerNamespace switch
        {
            var ns when ns.StartsWith("GameGuild.Identity.Authentication", StringComparison.Ordinal) => "auth",
            var ns when ns.StartsWith("GameGuild.Identity.Authorization", StringComparison.Ordinal) => "access-control",
            var ns when ns.StartsWith("GameGuild.Identity.Users", StringComparison.Ordinal) => "users",
            var ns when ns.StartsWith("GameGuild.Identity.Tenants", StringComparison.Ordinal) =>
                controllerName.StartsWith("User", StringComparison.Ordinal) ? "users" : "tenants",
            var ns when ns.StartsWith("GameGuild.Resources.Contents", StringComparison.Ordinal) => "resources/contents",
            var ns when ns.StartsWith("GameGuild.Resources", StringComparison.Ordinal) =>
                controllerName.StartsWith("User", StringComparison.Ordinal)
                    ? "users/resources"
                    : controllerName.StartsWith("Tenant", StringComparison.Ordinal)
                        ? "tenants/resources"
                        : "resources",
            var ns when ns.StartsWith("GameGuild.Commerce.", StringComparison.Ordinal) =>
                $"commerce/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Compliance.", StringComparison.Ordinal) =>
                $"compliance/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Content.", StringComparison.Ordinal) =>
                $"content/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Learning.", StringComparison.Ordinal) =>
                $"learning/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Social.", StringComparison.Ordinal) =>
                $"social/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Monitoring.", StringComparison.Ordinal) =>
                $"monitoring/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Gamification.", StringComparison.Ordinal) =>
                $"gamification/{string.Join("/", GetRemainderSegments(ns, 2))}",
            var ns when ns.StartsWith("GameGuild.Analytics", StringComparison.Ordinal) => "analytics",
            var ns when ns.StartsWith("GameGuild.Assets", StringComparison.Ordinal) => "assets",
            var ns when ns.StartsWith("GameGuild.Features", StringComparison.Ordinal) => "features",
            var ns when ns.StartsWith("GameGuild.Notifications", StringComparison.Ordinal) => "notifications",
            var ns when ns.StartsWith("GameGuild.Projects", StringComparison.Ordinal) => "projects",
            var ns when ns.StartsWith("GameGuild.TestingLab", StringComparison.Ordinal) => "testing-lab",
            _ => string.Join("/", GetRemainderSegments(controllerNamespace, 1))
        };
    }

    private static string NormalizeExplicitTag(string prefix, string explicitTag)
    {
        var explicitSegments = NormalizePath(explicitTag);
        if (explicitSegments.Count == 0)
            return prefix;

        if (IsFullyQualified(explicitSegments))
            return string.Join("/", explicitSegments);

        var prefixSegments = NormalizePath(prefix);
        if (prefixSegments.Count == 0)
            return string.Join("/", explicitSegments);

        var lastPrefixSegment = prefixSegments[^1];

        if (IsAliasOfPrefix(explicitSegments[0], lastPrefixSegment))
        {
            var tail = explicitSegments.Skip(1).ToList();
            return tail.Count == 0 ? prefix : string.Join("/", prefixSegments.Concat(tail));
        }

        if (explicitSegments.Count == 1)
        {
            var collapsed = CollapseDuplicatePrefixToken(explicitSegments[0], lastPrefixSegment);
            if (string.IsNullOrWhiteSpace(collapsed))
                return prefix;

            return collapsed == lastPrefixSegment
                ? prefix
                : string.Join("/", prefixSegments.Append(collapsed));
        }

        return string.Join("/", prefixSegments.Concat(explicitSegments));
    }

    private static string NormalizeLeaf(string prefix, string controllerName)
    {
        var leaf = ToKebabCase(controllerName);
        foreach (var suffix in new[] { "-crud", "-operations", "-operation", "-controller" })
        {
            if (leaf.EndsWith(suffix, StringComparison.Ordinal))
            {
                leaf = leaf[..^suffix.Length];
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(prefix))
            return leaf;

        var prefixSegments = NormalizePath(prefix);
        var lastPrefixSegment = prefixSegments[^1];
        var collapsed = CollapseDuplicatePrefixToken(leaf, lastPrefixSegment);

        return collapsed == lastPrefixSegment ? string.Empty : collapsed;
    }

    private static bool IsFullyQualified(IReadOnlyList<string> segments)
    {
        return segments.Count > 0 && CanonicalRoots.Contains(segments[0]);
    }

    private static bool IsAliasOfPrefix(string segment, string lastPrefixSegment)
    {
        if (segment == lastPrefixSegment)
            return true;

        return lastPrefixSegment switch
        {
            "auth" => segment == "authentication",
            _ => false
        };
    }

    private static string CollapseDuplicatePrefixToken(string token, string lastPrefixSegment)
    {
        if (string.IsNullOrWhiteSpace(token))
            return string.Empty;

        if (token == lastPrefixSegment)
            return token;

        foreach (var candidate in new[] { lastPrefixSegment, Singularize(lastPrefixSegment) })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            var prefix = $"{candidate}-";
            if (token.StartsWith(prefix, StringComparison.Ordinal))
                return token[prefix.Length..];
        }

        return token;
    }

    private static string Singularize(string value)
    {
        return value.EndsWith("s", StringComparison.Ordinal) ? value[..^1] : value;
    }

    private static List<string> NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new List<string>();

        return value
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(ToKebabCase)
            .ToList();
    }

    private static IEnumerable<string> GetRemainderSegments(string controllerNamespace, int skipCount)
    {
        return controllerNamespace
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Skip(skipCount)
            .Where(segment => !string.Equals(segment, "Controllers", StringComparison.Ordinal))
            .Select(ToKebabCase)
            .Where(segment => !string.IsNullOrWhiteSpace(segment));
    }

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length + 8);

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (char.IsUpper(character))
            {
                if (index > 0 && builder[^1] != '-')
                {
                    var previous = value[index - 1];
                    var nextIsLower = index + 1 < value.Length && char.IsLower(value[index + 1]);

                    if (char.IsLower(previous) || char.IsDigit(previous) || (char.IsUpper(previous) && nextIsLower))
                        builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
            }
            else if (character == '_' || character == ' ')
            {
                if (builder.Length > 0 && builder[^1] != '-')
                    builder.Append('-');
            }
            else
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }
}

internal sealed class AllowAnonymousOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.ApiDescription.ActionDescriptor is not ControllerActionDescriptor controllerAction)
            return;

        var actionAllowsAnonymous = controllerAction.MethodInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
        var controllerAllowsAnonymous = controllerAction.ControllerTypeInfo.IsDefined(typeof(AllowAnonymousAttribute), inherit: true);
        var actionRequiresAuthorization = controllerAction.MethodInfo
            .GetCustomAttributes(inherit: true)
            .OfType<IAuthorizeData>()
            .Any();

        if (!actionAllowsAnonymous && !(controllerAllowsAnonymous && !actionRequiresAuthorization))
            return;

        operation.Extensions[OpenApiExtensions.AllowAnonymousExtensionName] = new OpenApiBoolean(true);
        operation.Security = new List<OpenApiSecurityRequirement>();
    }
}
