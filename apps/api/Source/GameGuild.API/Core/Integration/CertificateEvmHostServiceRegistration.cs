using System.Globalization;
using System.Numerics;
using GameGuild.API.Database;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace GameGuild.API.Core.Integration;

/// <summary>One explicit immutable EVM profile per host. Registration never reads a key, contacts RPC or opens a database.</summary>
public static class CertificateEvmHostServiceRegistration
{
    public static IServiceCollection AddCertificateEvmHost(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configuration);
        var enabled = configuration[CertificateEvmHostOptions.SectionName + ":Enabled"];
        if (enabled is null)
        {
            return services;
        }
        if (!bool.TryParse(enabled, out var active))
        {
            throw InvalidConfiguration();
        }
        if (!active)
        {
            return services;
        }
        if (services.Any(s => s.ServiceType == typeof(EvmCertificatePublicationPolicy) ||
            s.ServiceType == typeof(EvmCertificateNetworkOptions) || s.ServiceType == typeof(IDbContextFactory<EvmCertificateJournalDbContext>)))
        {
            throw InvalidConfiguration();
        }

        CertificateEvmHostOptions options;
        EvmCertificatePublicationPolicy policy;
        EvmCertificateEnvironmentKeySource keys;
        string connection;
        try
        {
            options = new CertificateEvmHostOptions();
            configuration.GetSection(CertificateEvmHostOptions.SectionName).Bind(options, binder => binder.ErrorOnUnknownConfiguration = true);
            var legacyProvider = configuration["BlockchainCertificates:Provider"] ?? BlockchainCertificateOptions.ProviderNone;
            if (!string.Equals(legacyProvider.Trim(), BlockchainCertificateOptions.ProviderNone, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidConfiguration();
            }
            CertificateEvmEvidenceProducer.ValidateIssuer(options.CanonicalIssuer);
            var network = EvmCertificatePublicationNetwork.Capture(options.Profile, options.Network);
            var budget = EvmCertificateFeeBudget.Create(options.GasLimit, Wei(options.MaximumFeePerGasWei), Wei(options.MaximumTransactionFeeWei));
            policy = EvmCertificatePublicationPolicy.Create(network, options.SignerReference, budget,
                Wei(options.InitialFeePerGasWei), Wei(options.InitialPriorityFeePerGasWei), options.CheckDelay,
                options.LeaseDuration, options.ReplacementDelay, options.MaximumAttempts);
            keys = new EvmCertificateEnvironmentKeySource(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [options.SignerReference] = options.PrivateKeyEnvironmentVariable
            });
            connection = PostgresConnectionString.Resolve(configuration) ?? throw InvalidConfiguration();
            var database = new NpgsqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(database.Host) || string.IsNullOrWhiteSpace(database.Database) || string.IsNullOrWhiteSpace(database.Username))
            {
                throw InvalidConfiguration();
            }
        }
        catch (Exception)
        {
            // ConfigurationBinder and provider exceptions may embed a secret-bearing value. No inner exception is retained.
            throw InvalidConfiguration();
        }

        services.AddDbContextFactory<EvmCertificateJournalDbContext>(builder => builder.UseNpgsql(connection,
            postgres => postgres.MigrationsHistoryTable(EvmCertificateJournalDbContext.MigrationsHistoryTable, EvmCertificateJournalDbContext.SchemaName)));
        services.AddScoped<EvmCertificateJournal>();
        services.AddSingleton<IEvmCertificatePrivateKeySource>(keys);
        services.AddScoped<IEvmCertificateTransactionSigner, EvmCertificateSoftwareSigner>();
        services.AddEvmCertificatePublicationTransport(options.Network);
        services.AddCertificateEvmDurableEvidence(policy.Network, options.CanonicalIssuer);
        services.AddSingleton<IDatabaseMigrationPrerequisite, CertificateEvmJournalMigrationPrerequisite>();
        services.AddSingleton<IHostedService, CertificateEvmJournalReadiness>();
        services.AddEvmCertificatePublicationWorker(policy);
        return services;
    }

    private static BigInteger Wei(string value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 78 || value.Any(c => !char.IsAsciiDigit(c)) ||
            !BigInteger.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var wei))
        {
            throw InvalidConfiguration();
        }
        return wei;
    }

    private static InvalidOperationException InvalidConfiguration() => new(
        "Invalid EVM certificate host configuration. Require one explicit profile, HTTPS network identity, public issuer, signer environment reference, integer-wei budget, bounded worker policy and runtime database connection; legacy Provider must remain none.");
}
