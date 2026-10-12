namespace GameGuild.Compliance.Audit;

public interface IAuditActionTypeSearchService
{
    Task<AuditActionTypeSearchResult> SearchAsync(AuditActionTypeSearchRequest request);

    Task<AuditActionTypeSearchResult> SearchAsync(
        AuditActionTypeSearchRequest request,
        CancellationToken cancellationToken);

    Task<AuditActionTypeExportResult> ExportAsync(AuditActionTypeSearchRequest request, int maximumRecords);

    Task<AuditActionTypeExportResult> ExportAsync(
        AuditActionTypeSearchRequest request,
        int maximumRecords,
        CancellationToken cancellationToken);
}
