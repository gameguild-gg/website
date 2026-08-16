using GameGuild.API.Database;

namespace GameGuild.API.Setup;

/// <summary>Validates external dependencies required by protected environments.</summary>
public static class OperationalStartupConfiguration
{
    public static IReadOnlyList<string> Validate(IConfiguration configuration, string environmentName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (AllowsReducedConfiguration(environmentName))
            return [];

        var failures = new List<string>();
        failures.AddRange(DatabaseStartupConfiguration.Validate(configuration, environmentName));

        RequireStrongSecret(
            configuration,
            failures,
            "A JWT secret is required.",
            "The JWT secret must contain at least 32 characters and must not use a placeholder value.",
            "Jwt:SecretKey",
            "Authentication:JwtSecretKey");
        RequireAny(configuration, failures, "A JWT issuer is required.", "Jwt:Issuer", "Authentication:JwtIssuer");
        RequireAny(configuration, failures, "A JWT audience is required.", "Jwt:Audience", "Authentication:JwtAudience");
        RequireStrongSecret(
            configuration,
            failures,
            "An encryption key is required.",
            "The encryption key must contain at least 32 characters and must not use a placeholder value.",
            "Encryption:EncryptionKey",
            "Encryption:Key");

        if (configuration.GetValue<bool?>("Redis:Enabled") != true)
            failures.Add("Redis must be enabled.");
        Require(configuration, failures, "Redis:ConnectionString", "A Redis connection string is required.");

        ValidateEmail(configuration, failures);
        ValidateStorage(configuration, failures);

        return failures;
    }

    public static void ThrowIfInvalid(IConfiguration configuration, string environmentName)
    {
        var failures = Validate(configuration, environmentName);
        if (failures.Count != 0)
            throw new InvalidOperationException($"Unsafe operational startup configuration: {string.Join(" ", failures)}");
    }

    private static void ValidateEmail(IConfiguration configuration, ICollection<string> failures)
    {
        if (configuration.GetValue<bool?>("EmailDelivery:Enabled") != true)
            failures.Add("Email delivery must be enabled.");

        Require(configuration, failures, "EmailDelivery:FromEmail", "An email delivery sender address is required.");

        var provider = configuration["EmailDelivery:Provider"];
        if (string.Equals(provider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            Require(configuration, failures, "EmailDelivery:SmtpHost", "An SMTP host is required.");
            if (configuration.GetValue<int?>("EmailDelivery:SmtpPort") is not > 0)
                failures.Add("A positive SMTP port is required.");
        }
        else if (string.Equals(provider, "SendGrid", StringComparison.OrdinalIgnoreCase))
        {
            Require(configuration, failures, "EmailDelivery:SendGridApiKey", "A SendGrid API key is required.");
        }
        else if (string.IsNullOrWhiteSpace(provider))
        {
            failures.Add("An email delivery provider is required.");
        }
        else
        {
            failures.Add("EmailDelivery:Provider must use a supported provider: Smtp or SendGrid.");
        }
    }

    private static void ValidateStorage(IConfiguration configuration, ICollection<string> failures)
    {
        Require(configuration, failures, "Assets:Storage:ServiceUrl", "An object storage service URL is required.");
        Require(configuration, failures, "Assets:Storage:AccessKey", "An object storage access key is required.");
        Require(configuration, failures, "Assets:Storage:SecretKey", "An object storage secret key is required.");
        Require(configuration, failures, "Assets:Storage:BucketName", "An object storage bucket is required.");
        RequireBase64Secret(
            configuration,
            failures,
            "Assets:Token:SecretKey",
            "An asset token signing key is required.",
            "The asset token signing key must be valid Base64 containing at least 32 bytes.");
    }

    private static void RequireBase64Secret(
        IConfiguration configuration,
        ICollection<string> failures,
        string key,
        string missingFailure,
        string unsafeFailure)
    {
        var secret = configuration[key];
        if (string.IsNullOrWhiteSpace(secret))
        {
            failures.Add(missingFailure);
            return;
        }

        try
        {
            if (Convert.FromBase64String(secret).Length < 32)
                failures.Add(unsafeFailure);
        }
        catch (FormatException)
        {
            failures.Add(unsafeFailure);
        }
    }

    private static void RequireAny(
        IConfiguration configuration,
        ICollection<string> failures,
        string failure,
        params string[] keys)
    {
        if (!keys.Any(key => !string.IsNullOrWhiteSpace(configuration[key])))
            failures.Add(failure);
    }

    private static void Require(
        IConfiguration configuration,
        ICollection<string> failures,
        string key,
        string failure)
    {
        if (string.IsNullOrWhiteSpace(configuration[key]))
            failures.Add(failure);
    }

    private static void RequireStrongSecret(
        IConfiguration configuration,
        ICollection<string> failures,
        string missingFailure,
        string unsafeFailure,
        params string[] keys)
    {
        var secret = keys
            .Select(key => configuration[key])
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (string.IsNullOrWhiteSpace(secret))
        {
            failures.Add(missingFailure);
            return;
        }

        if (secret.Length < 32 || secret.StartsWith("CHANGE_THIS", StringComparison.OrdinalIgnoreCase))
            failures.Add(unsafeFailure);
    }

    private static bool AllowsReducedConfiguration(string environmentName) =>
        string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Test", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(environmentName, "Testing", StringComparison.OrdinalIgnoreCase);
}
