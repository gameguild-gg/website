using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;
using GameGuild.Finance.Ledgers.Abstractions;
using GameGuild.Finance.Ledgers.Configuration;
using GameGuild.Finance.Ledgers.Repositories;
using GameGuild.Finance.Ledgers.Services;

namespace GameGuild.Finance.Ledgers;

/// <summary>
/// Extension methods for registering the Ledger module services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Ledger module services to the service collection.
    /// </summary>
    public static IServiceCollection AddLedgerModule(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<ILedgerClosureRepository, LedgerClosureRepository>();
        services.AddScoped<ILedgerEntryRepository, LedgerEntryRepository>();

        // Services
        services.AddScoped<ILedgerHierarchyService, LedgerHierarchyService>();
        services.AddScoped<ILedgerEntryService, LedgerEntryService>();
        services.AddScoped<ILedgerRollupService, LedgerRollupService>();
        services.AddScoped<IVirtualLedgerService, VirtualLedgerService>();

        // Validators (FluentValidation) - using a non-static type as marker
        services.AddValidatorsFromAssemblyContaining<LedgerHierarchyService>(ServiceLifetime.Scoped);

        // MediatR handlers are registered via assembly scanning in Program.cs

        return services;
    }

    /// <summary>
    /// Configures EF Core to use Ledger module entity configurations.
    /// </summary>
    public static ModelBuilder ApplyLedgerModuleConfigurations(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LedgerConfiguration());
        modelBuilder.ApplyConfiguration(new LedgerClosureConfiguration());
        modelBuilder.ApplyConfiguration(new LedgerEntryConfiguration());

        return modelBuilder;
    }

    /// <summary>
    /// Ensures the finance schema exists in the database (PostgreSQL).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Provider-specific PostgreSQL schema DDL; unit tests use EF InMemory.")]
    public static void EnsureFinanceSchemaCreated(this DbContext context)
    {
        context.Database.ExecuteSqlRaw("CREATE SCHEMA IF NOT EXISTS finance");
    }
}
