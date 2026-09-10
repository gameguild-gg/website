using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameGuild.Finance.Economy.Integrations.AI;

internal sealed class AiProviderCostFactEntity
{
    public Guid Id { get; set; }
    public Guid AuthorizationId { get; set; }
    public Guid RequestId { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public AiProvider Provider { get; set; }
    public string Model { get; set; } = string.Empty;
    public string ProviderUsageId { get; set; } = string.Empty;
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public long InputCostUsdNanos { get; set; }
    public long OutputCostUsdNanos { get; set; }
    public long ExactProviderCostUsdNanos { get; set; }
    public long ChargedSoftUnits { get; set; }
    public string RateCardVersion { get; set; } = string.Empty;
    public DateTimeOffset CompletedAt { get; set; }

    internal static AiProviderCostFactEntity FromDomain(AiProviderCostFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        return new AiProviderCostFactEntity
        {
            Id = fact.Id,
            AuthorizationId = fact.AuthorizationId,
            RequestId = fact.RequestId,
            TenantId = fact.TenantId,
            ActorId = fact.ActorId,
            ServiceCode = fact.ServiceCode,
            Provider = fact.Provider,
            Model = fact.Model,
            ProviderUsageId = fact.ProviderUsageId,
            InputTokens = fact.InputTokens,
            OutputTokens = fact.OutputTokens,
            TotalTokens = fact.TotalTokens,
            InputCostUsdNanos = fact.InputCostUsdNanos,
            OutputCostUsdNanos = fact.OutputCostUsdNanos,
            ExactProviderCostUsdNanos = fact.ExactProviderCostUsdNanos,
            ChargedSoftUnits = fact.ChargedSoftUnits,
            RateCardVersion = fact.RateCardVersion,
            CompletedAt = fact.CompletedAt
        };
    }

    internal AiProviderCostFact ToDomain() => new(
        Id, AuthorizationId, RequestId, TenantId, ActorId, ServiceCode, Provider, Model,
        ProviderUsageId, InputTokens, OutputTokens, TotalTokens, InputCostUsdNanos,
        OutputCostUsdNanos, ExactProviderCostUsdNanos, ChargedSoftUnits, RateCardVersion, CompletedAt);
}

internal sealed class AiProviderCostFactEntityConfiguration : IEntityTypeConfiguration<AiProviderCostFactEntity>
{
    public void Configure(EntityTypeBuilder<AiProviderCostFactEntity> builder)
    {
        builder.ToTable("ai_provider_cost_facts", table =>
        {
            table.HasCheckConstraint(
                "ck_ai_provider_cost_facts_token_conservation",
                "\"InputTokens\" >= 0 AND \"OutputTokens\" >= 0 AND \"TotalTokens\" = \"InputTokens\" + \"OutputTokens\"");
            table.HasCheckConstraint(
                "ck_ai_provider_cost_facts_cost_conservation",
                "\"InputCostUsdNanos\" >= 0 AND \"OutputCostUsdNanos\" >= 0 AND \"ExactProviderCostUsdNanos\" = \"InputCostUsdNanos\" + \"OutputCostUsdNanos\"");
            table.HasCheckConstraint(
                "ck_ai_provider_cost_facts_charge_positive",
                "\"ChargedSoftUnits\" > 0");
        });
        builder.HasKey(fact => fact.Id);
        builder.Property(fact => fact.Id).ValueGeneratedNever();
        builder.Property(fact => fact.ServiceCode).HasMaxLength(128).IsRequired();
        builder.Property(fact => fact.Provider).HasConversion<int>().IsRequired();
        builder.Property(fact => fact.Model).HasMaxLength(256).IsRequired();
        builder.Property(fact => fact.ProviderUsageId).HasMaxLength(256).IsRequired();
        builder.Property(fact => fact.RateCardVersion).HasMaxLength(128).IsRequired();
        builder.HasIndex(fact => fact.AuthorizationId).IsUnique();
        builder.HasIndex(fact => new { fact.Provider, fact.ProviderUsageId }).IsUnique();
        builder.HasIndex(fact => new { fact.TenantId, fact.CompletedAt });
        builder.HasIndex(fact => new { fact.ServiceCode, fact.CompletedAt });
    }
}

internal sealed class EfAiProviderCostFactStore : IAiProviderCostFactStore
{
    private readonly IApplicationDbContext _db;

    public EfAiProviderCostFactStore(IApplicationDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public void Save(AiProviderCostFact fact)
    {
        ArgumentNullException.ThrowIfNull(fact);
        _db.Set<AiProviderCostFactEntity>().Add(AiProviderCostFactEntity.FromDomain(fact));
        _db.SaveChangesAsync().ConfigureAwait(false).GetAwaiter().GetResult();
    }
}
