using System.Globalization;
using System.Security.Claims;
using System.Text;
using GameGuild.API.Core.Security;
using GameGuild.Configuration;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Configuration.PresentationLayer.Authentication;
using GameGuild.Configuration.PresentationLayer.CORS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Authorization.Utilities;
using IClaimsTransformation = Microsoft.AspNetCore.Authentication.IClaimsTransformation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using AuthorizationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationOptions;
using AuthorizationClaimTransformationOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationClaimTransformationOptions;
using AuthorizationClaimRequirementOptions = GameGuild.Configuration.PresentationLayer.Authorization.AuthorizationClaimRequirementOptions;
using AuthenticationBuilder = Microsoft.AspNetCore.Authentication.AuthenticationBuilder;
using ConfiguredAuthorizationPolicyOptions = GameGuild.Configuration.PresentationLayer.Authorization.ConfiguredAuthorizationPolicyOptions;

namespace GameGuild.API;

/// <summary>
///     Extension methods for configuring security-related services
///     (Authentication, Authorization, CORS).
/// </summary>
public static class SecurityServiceCollectionExtensions
{
    private static readonly MemoryCache JwtAuthenticationAuditCache = new(new MemoryCacheOptions { SizeLimit = 10_000 });
    private static readonly object JwtAuthenticationAuditCacheLock = new();

    public static IServiceCollection SetupAuthentication(this IServiceCollection services,
        IConfiguration configuration, AuthenticationOptions? options) =>
        SetupAuthentication(services, configuration, options, configureAdditionalSchemes: null);

    public static IServiceCollection SetupAuthentication(this IServiceCollection services, IConfiguration configuration,
        AuthenticationOptions? options, Action<AuthenticationBuilder>? configureAdditionalSchemes)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "Authentication",
            AuthenticationOptions.CreateDefault);
        options.Validate();

        // The authentication module's OAuth service uses the same validated typed settings as the API schemes.
        services.AddSingleton(options);

        if (!options.EnableAuthentication) return services;

        var isDevelopmentOrTesting = IsDevelopmentOrTesting(configuration);
        IdentityModelEventSource.ShowPII = isDevelopmentOrTesting;

        var accessTokenExpirationMinutes = checked((int)Math.Ceiling(options.JwtExpiration.TotalMinutes));
        var jwtConfiguration = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = options.JwtSecretKey,
                ["Jwt:Issuer"] = options.JwtIssuer,
                ["Jwt:Audience"] = options.JwtAudience,
                ["Jwt:AccessTokenExpirationMinutes"] = accessTokenExpirationMinutes.ToString(CultureInfo.InvariantCulture),
                ["Jwt:RefreshTokenExpirationDays"] = options.RefreshTokenExpirationDays.ToString(CultureInfo.InvariantCulture)
            })
            .Build();
        var resolvedJwtOptions = JwtOptionsResolver.CreateValidated(jwtConfiguration);

        services.PostConfigure<JwtOptions>(jwtOptions =>
        {
            jwtOptions.SecretKey = resolvedJwtOptions.SecretKey;
            jwtOptions.Issuer = resolvedJwtOptions.Issuer;
            jwtOptions.Audience = resolvedJwtOptions.Audience;
            jwtOptions.AccessTokenExpirationMinutes = resolvedJwtOptions.AccessTokenExpirationMinutes;
            jwtOptions.RefreshTokenExpirationDays = resolvedJwtOptions.RefreshTokenExpirationDays;
        });

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(resolvedJwtOptions.SecretKey))
        { KeyId = "GameGuild-jwt-key" };

        var authenticationBuilder = services.AddAuthentication(authOptions =>
                {
                    authOptions.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    authOptions.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    authOptions.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
                }
            )
            .AddJwtBearer(jwtOptions =>
                {
                    jwtOptions.SaveToken = true;
                    jwtOptions.MapInboundClaims = false;
                    jwtOptions.RequireHttpsMetadata = !isDevelopmentOrTesting;

                    jwtOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = resolvedJwtOptions.ValidateIssuerSigningKey,
                        IssuerSigningKey = securityKey,
                        ValidateIssuer = resolvedJwtOptions.ValidateIssuer,
                        ValidIssuer = resolvedJwtOptions.Issuer,
                        ValidateAudience = resolvedJwtOptions.ValidateAudience,
                        ValidAudience = resolvedJwtOptions.Audience,
                        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                        ValidateLifetime = resolvedJwtOptions.ValidateLifetime,
                        ClockSkew = TimeSpan.FromSeconds(resolvedJwtOptions.ClockSkewSeconds),
                        NameClaimType = "sub",
                        RoleClaimType = "role",
                        TryAllIssuerSigningKeys = true
                    };

                    jwtOptions.Events = new JwtBearerEvents
                    {
                        OnAuthenticationFailed = async context =>
                        {
                            var failureType = context.Exception.GetType().Name;
                            context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                                .CreateLogger("GameGuild.API")
                                .LogWarning("JWT authentication failed: {FailureType}", failureType);

                            await RecordJwtAuthenticationAuditAsync(
                                context.HttpContext,
                                new AuthenticationAuditEvent(
                                    "Authentication.JwtValidationFailed",
                                    null,
                                    false,
                                    "JWT",
                                    ErrorMessage: failureType)).ConfigureAwait(false);
                        },
                        OnTokenValidated = async context =>
                        {
                            context.HttpContext.RequestServices.GetService<ILoggerFactory>()?
                                .CreateLogger("GameGuild.API")
                                .LogDebug("JWT token validated for subject {Subject}",
                                    context.Principal?.FindFirst("sub")?.Value);

                            var tokenId = context.Principal?.FindFirst("jti")?.Value
                                          ?? context.SecurityToken?.Id;

                            if (string.IsNullOrWhiteSpace(tokenId)
                                || !TryMarkJwtTokenAudited(tokenId, context.SecurityToken?.ValidTo))
                            {
                                return;
                            }

                            await RecordJwtAuthenticationAuditAsync(
                                context.HttpContext,
                                new AuthenticationAuditEvent(
                                    "Authentication.JwtTokenValidated",
                                    GetGuidClaim(context.Principal, "sub"),
                                    true,
                                    "JWT",
                                    SessionId: GetGuidClaim(context.Principal, JwtClaimTypes.SessionId),
                                    TenantId: GetGuidClaim(context.Principal, JwtClaimTypes.TenantId))).ConfigureAwait(false);
                        }
                    };
                }
            );

        if (options.EnableApiKeyAuthentication)
        {
            authenticationBuilder.AddApiKeyAuthentication(apiKeyOptions =>
            {
                if (options.ApiKeyHeaderName is not null)
                {
                    apiKeyOptions.HeaderName = options.ApiKeyHeaderName;
                }

                apiKeyOptions.AllowQueryString = options.AllowApiKeyInQueryString;

                if (options.ApiKeyQueryStringParameterName is not null)
                {
                    apiKeyOptions.QueryStringParameterName = options.ApiKeyQueryStringParameterName;
                }

                if (options.ApiKeyCustomKeyResolver is not null)
                {
                    apiKeyOptions.CustomKeyResolver = options.ApiKeyCustomKeyResolver;
                }
            });
        }

        if (options.EnableBasicAuthentication)
        {
            var basicSettings = options.Basic!;
            authenticationBuilder.AddBasicAuthentication(basicSettings.SchemeName, basicOptions =>
            {
                basicOptions.Realm = basicSettings.Realm;
            });
        }

        if (options.EnableClientCertificateAuthentication)
        {
            var certificateSettings = options.ClientCertificate!;
            authenticationBuilder.AddClientCertificateAuthentication(certificateSettings);
        }

        if (options.EnableCookieAuthentication)
        {
            var cookieSettings = options.Cookie!;
            authenticationBuilder.AddCookie(cookieSettings.SchemeName, cookieOptions =>
            {
                cookieOptions.Cookie.Name = cookieSettings.Name;
                cookieOptions.Cookie.Path = "/";
                cookieOptions.Cookie.HttpOnly = true;
                cookieOptions.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookieOptions.Cookie.SameSite = cookieSettings.SameSite;
                cookieOptions.ExpireTimeSpan = cookieSettings.Expiration;
                cookieOptions.SlidingExpiration = cookieSettings.SlidingExpiration;
                cookieOptions.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    return Task.CompletedTask;
                };
                cookieOptions.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;

                    return Task.CompletedTask;
                };
            });
        }

        configureAdditionalSchemes?.Invoke(authenticationBuilder);

        // Add authorization if enabled
        if (options.EnableAuthorization)
        {
            services.AddAuthorization();
        }

        return services;
    }

    public static IServiceCollection SetupCors(this IServiceCollection services, IConfiguration configuration,
        CorsOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "Cors", CorsOptions.CreateDefault);
        options.Validate();

        services.AddCors(corsOptions =>
            {
                corsOptions.AddDefaultPolicy(policyBuilder =>
                    {
                        if (options.AllowedOrigins.Length > 0)
                        {
                            policyBuilder.WithOrigins(options.AllowedOrigins);
                        }
                        else
                        {
                            policyBuilder.AllowAnyOrigin();
                        }

                        if (options.AllowedMethods.Length > 0)
                        {
                            policyBuilder.WithMethods(options.AllowedMethods);
                        }
                        else
                        {
                            policyBuilder.AllowAnyMethod();
                        }

                        if (options.AllowedHeaders.Length > 0)
                        {
                            policyBuilder.WithHeaders(options.AllowedHeaders);
                        }
                        else
                        {
                            policyBuilder.AllowAnyHeader();
                        }
                    }
                );
            }
        );

        return services;
    }

    public static IServiceCollection SetupAuthorization(this IServiceCollection services, IConfiguration configuration,
        AuthorizationOptions? options)
    {
        options ??= OptionBuilderUtilities.CreateAndBind(configuration, "Authorization",
            AuthorizationOptions.CreateDefault);
        options.Validate();

        // ===== Configuration Options =====
        services.AddAuthorizationOptions(configuration);
        services.PostConfigure<AuthorizationOptions>(configured => CopyAuthorizationOptions(options, configured));
        services.TryAddEnumerable(ServiceDescriptor.Transient<IClaimsTransformation, ConfiguredAuthorizationClaimsTransformation>());

        // ===== Presentation Layer (handlers, tenant context, policy provider) =====
        services.AddAuthorizationPresentation();

        // ===== Rule-Based Authorization (DB-driven, tenant-configurable policies) =====
        services.AddRuleBasedAuthorization();

        services.AddAuthorization(authzOptions =>
        {
            authzOptions.AddPolicy("RequireAdminRole", policy =>
                policy.RequireAssertion(context => IsAdministrator(context.User)));
            authzOptions.AddPolicy("RequireUserRole", policy =>
                policy.RequireAssertion(context => IsUser(context.User)));
            authzOptions.AddPolicy("RequireTenantAccess", policy =>
                policy.RequireAssertion(context => HasTenantClaim(context.User)));

            foreach (var (name, configuredPolicy) in options.Policies)
            {
                if (Policies.IsValid(name))
                {
                    throw new InvalidOperationException(
                        $"Authorization policy '{name}' is database-backed and cannot be replaced by static configuration.");
                }

                if (authzOptions.GetPolicy(name) is not null)
                {
                    throw new InvalidOperationException(
                        $"Authorization policy '{name}' is reserved and cannot be replaced by static configuration.");
                }

                authzOptions.AddPolicy(name, policy => ConfigurePolicy(policy, configuredPolicy, options.RoleHierarchy));
            }

            var configuredDefaultPolicy = authzOptions.GetPolicy(options.DefaultPolicy);
            var defaultPolicyBuilder = new AuthorizationPolicyBuilder();
            if (configuredDefaultPolicy is not null)
            {
                defaultPolicyBuilder.Combine(configuredDefaultPolicy);
            }

            if (options.RequireAuthenticatedUser &&
                configuredDefaultPolicy?.Requirements.OfType<DenyAnonymousAuthorizationRequirement>().Any() != true)
            {
                defaultPolicyBuilder.RequireAuthenticatedUser();
            }

            authzOptions.DefaultPolicy = defaultPolicyBuilder.Build();

            if (options.FallbackPolicyName is not null)
            {
                var fallbackPolicy = authzOptions.GetPolicy(options.FallbackPolicyName);
                if (fallbackPolicy is null)
                {
                    throw new InvalidOperationException(
                        $"Fallback authorization policy '{options.FallbackPolicyName}' is not registered. " +
                        "Configure it as a static policy or use a built-in policy.");
                }

                authzOptions.FallbackPolicy = fallbackPolicy;
            }
        });

        services.AddScoped<IAuthorizationMiddlewareResultHandler, AuditingAuthorizationMiddlewareResultHandler>();

        if (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(IAuthorizationPermissionService))?.ImplementationType ==
            typeof(AuthorizationPermissionServiceAdapter))
        {
            services.AddHttpContextAccessor();
            services.AddMemoryCache();
            services.AddScoped<AuthorizationPermissionServiceAdapter>();
            services.Replace(ServiceDescriptor.Scoped<IAuthorizationPermissionService, AuditingAuthorizationPermissionService>());
        }

        return services;
    }

    private static void ConfigurePolicy(
        AuthorizationPolicyBuilder policy,
        ConfiguredAuthorizationPolicyOptions configuredPolicy,
        IReadOnlyDictionary<string, List<string>> roleHierarchy)
    {
        if (configuredPolicy.RequireAuthenticatedUser)
        {
            policy.RequireAuthenticatedUser();
        }

        if (configuredPolicy.Roles.Count > 0)
        {
            policy.RequireRole(ExpandRoles(configuredPolicy.Roles, roleHierarchy));
        }

        foreach (var claim in configuredPolicy.Claims)
        {
            if (claim.AllowedValues.Count == 0)
            {
                policy.RequireClaim(claim.Type);
            }
            else
            {
                policy.RequireClaim(claim.Type, claim.AllowedValues);
            }
        }

        policy.AddAuthenticationSchemes(configuredPolicy.AuthenticationSchemes.ToArray());
    }

    private static string[] ExpandRoles(
        IEnumerable<string> requiredRoles,
        IReadOnlyDictionary<string, List<string>> roleHierarchy)
    {
        var acceptedRoles = new HashSet<string>(requiredRoles, StringComparer.OrdinalIgnoreCase);

        foreach (var candidateRole in roleHierarchy.Keys)
        {
            if (requiredRoles.Any(requiredRole => InheritsRole(candidateRole, requiredRole, roleHierarchy,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase))))
            {
                acceptedRoles.Add(candidateRole);
            }
        }

        return acceptedRoles.OrderBy(role => role, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static bool InheritsRole(
        string candidateRole,
        string requiredRole,
        IReadOnlyDictionary<string, List<string>> roleHierarchy,
        HashSet<string> visited)
    {
        if (string.Equals(candidateRole, requiredRole, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!visited.Add(candidateRole))
        {
            return false;
        }

        if (!roleHierarchy.TryGetValue(candidateRole, out var inheritedRoles))
        {
            return false;
        }

        return inheritedRoles.Any(inheritedRole =>
            InheritsRole(inheritedRole, requiredRole, roleHierarchy, visited));
    }

    private static void CopyAuthorizationOptions(
        AuthorizationOptions source,
        AuthorizationOptions destination)
    {
        destination.DefaultPolicy = source.DefaultPolicy;
        destination.FallbackPolicyName = source.FallbackPolicyName;
        destination.RequireAuthenticatedUser = source.RequireAuthenticatedUser;
        destination.SystemAccountId = source.SystemAccountId;
        destination.Policies = source.Policies.ToDictionary(
            entry => entry.Key,
            entry => new ConfiguredAuthorizationPolicyOptions
            {
                RequireAuthenticatedUser = entry.Value.RequireAuthenticatedUser,
                Roles = [.. entry.Value.Roles],
                Claims = entry.Value.Claims.Select(claim => new AuthorizationClaimRequirementOptions
                {
                    Type = claim.Type,
                    AllowedValues = [.. claim.AllowedValues]
                }).ToList(),
                AuthenticationSchemes = [.. entry.Value.AuthenticationSchemes]
            },
            StringComparer.OrdinalIgnoreCase);
        destination.RoleHierarchy = source.RoleHierarchy.ToDictionary(
            entry => entry.Key,
            entry => new List<string>(entry.Value),
            StringComparer.OrdinalIgnoreCase);
        destination.ClaimTransformations = source.ClaimTransformations.Select(transformation =>
            new AuthorizationClaimTransformationOptions
            {
                SourceClaimType = transformation.SourceClaimType,
                TargetClaimType = transformation.TargetClaimType,
                ValueMappings = new Dictionary<string, string>(transformation.ValueMappings, StringComparer.Ordinal)
            }).ToList();
    }

    private static bool HasTenantClaim(ClaimsPrincipal user) =>
        !string.IsNullOrWhiteSpace(ClaimsExtractor.GetTenantId(user));

    private static bool HasRole(ClaimsPrincipal user, string role) =>
        ClaimsExtractor.GetRoles(user).Contains(role);

    private static bool IsAdministrator(ClaimsPrincipal user) =>
        HasRole(user, "Admin") || HasRole(user, "SystemAdmin");

    private static bool IsTenantAdministrator(ClaimsPrincipal user) =>
        IsAdministrator(user) || HasRole(user, "TenantAdmin");

    private static bool IsUser(ClaimsPrincipal user) =>
        HasRole(user, "User") || IsTenantAdministrator(user);

    private static bool TryMarkJwtTokenAudited(string tokenId, DateTime? tokenValidTo)
    {
        var cacheKey = $"authentication-audit:jwt:{tokenId}";

        lock (JwtAuthenticationAuditCacheLock)
        {
            if (JwtAuthenticationAuditCache.TryGetValue(cacheKey, out _))
            {
                return false;
            }

            var remainingLifetime = tokenValidTo.HasValue
                ? tokenValidTo.Value - DateTime.UtcNow
                : TimeSpan.FromMinutes(15);
            if (remainingLifetime <= TimeSpan.Zero)
            {
                remainingLifetime = TimeSpan.FromMinutes(15);
            }
            else if (remainingLifetime > TimeSpan.FromDays(7))
            {
                remainingLifetime = TimeSpan.FromDays(7);
            }

            JwtAuthenticationAuditCache.Set(
                cacheKey,
                true,
                new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = remainingLifetime,
                    Size = 1
                });
        }

        return true;
    }

    private static Guid? GetGuidClaim(ClaimsPrincipal? principal, string claimType)
    {
        var value = principal?.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static async Task RecordJwtAuthenticationAuditAsync(HttpContext context, AuthenticationAuditEvent auditEvent)
    {
        var auditEventSink = context.RequestServices.GetService<IAuthenticationAuditEventSink>();
        if (auditEventSink is null)
        {
            return;
        }

        try
        {
            await auditEventSink.RecordAsync(auditEvent, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            context.RequestServices.GetService<ILoggerFactory>()?
                .CreateLogger("GameGuild.API")
                .LogError(exception, "Could not record JWT authentication audit event {ActionType}", auditEvent.ActionType);
        }
    }

    private static bool IsDevelopmentOrTesting(IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
                              ?? configuration["DOTNET_ENVIRONMENT"];

        return string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase)
               || string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
    }
}
