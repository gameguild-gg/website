namespace GameGuild.Resources;

/// <summary>Product-owned authoritative counter; missing sources are never interpreted as zero.</summary>
public interface IQuotaAuthoritativeUsageSource
{
    ResourceUsageType ResourceType { get; }
    Task<long> CountAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
