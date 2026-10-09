using Microsoft.Extensions.Configuration;

namespace GameGuild.Configuration.ApplicationLayer;

public static class JwtOptionsResolver
{
    public static JwtOptions CreateValidated(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new JwtOptions
        {
            Issuer = FirstConfigured(configuration, "Jwt:Issuer", "JwtSettings:Issuer") ?? new JwtOptions().Issuer,
            Audience = FirstConfigured(configuration, "Jwt:Audience", "JwtSettings:Audience") ?? new JwtOptions().Audience,
            SecretKey = FirstConfigured(configuration, "Jwt:Secret", "Jwt:SecretKey", "JwtSettings:SecretKey", "Authentication:JwtSecretKey") ?? string.Empty,
            AccessTokenExpirationMinutes = GetInt(configuration, new JwtOptions().AccessTokenExpirationMinutes, "Jwt:AccessTokenExpirationMinutes", "JwtSettings:AccessTokenExpirationMinutes"),
            RefreshTokenExpirationDays = GetInt(configuration, new JwtOptions().RefreshTokenExpirationDays, "Jwt:RefreshTokenExpirationDays", "JwtSettings:RefreshTokenExpirationDays"),
            RefreshTokenSlidingExpiration = GetBool(configuration, new JwtOptions().RefreshTokenSlidingExpiration, "Jwt:RefreshTokenSlidingExpiration", "JwtSettings:RefreshTokenSlidingExpiration"),
            RefreshTokenReplayContainmentScope = ResolveReplayScope(configuration),
            ClockSkewSeconds = GetInt(configuration, new JwtOptions().ClockSkewSeconds, "Jwt:ClockSkewSeconds", "JwtSettings:ClockSkewSeconds"),
            ValidateIssuer = GetBool(configuration, new JwtOptions().ValidateIssuer, "Jwt:ValidateIssuer", "JwtSettings:ValidateIssuer"),
            ValidateAudience = GetBool(configuration, new JwtOptions().ValidateAudience, "Jwt:ValidateAudience", "JwtSettings:ValidateAudience"),
            ValidateLifetime = GetBool(configuration, new JwtOptions().ValidateLifetime, "Jwt:ValidateLifetime", "JwtSettings:ValidateLifetime"),
            ValidateIssuerSigningKey = GetBool(configuration, new JwtOptions().ValidateIssuerSigningKey, "Jwt:ValidateIssuerSigningKey", "JwtSettings:ValidateIssuerSigningKey")
        };

        var errors = options.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"JWT configuration is invalid: {string.Join("; ", errors)}");
        }

        return options;
    }

    private static string? FirstConfigured(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>Resolve the server policy consistently for typed and legacy consumers.</summary>
    public static RefreshTokenReplayScope ResolveReplayScope(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var value = FirstConfigured(configuration, "Jwt:RefreshTokenReplayContainmentScope", "JwtSettings:RefreshTokenReplayContainmentScope");
        if (value is null) { return RefreshTokenReplayScope.Family; }
        if (!Enum.TryParse<RefreshTokenReplayScope>(value, ignoreCase: true, out var scope) || !Enum.IsDefined(scope))
        {
            throw new InvalidOperationException("JWT RefreshTokenReplayContainmentScope must be Family or Account");
        }
        return scope;
    }

    private static int GetInt(IConfiguration configuration, int defaultValue, params string[] keys)
    {
        var value = FirstConfigured(configuration, keys);
        return int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool GetBool(IConfiguration configuration, bool defaultValue, params string[] keys)
    {
        var value = FirstConfigured(configuration, keys);
        return bool.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
