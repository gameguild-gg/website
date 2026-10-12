using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record SearchAuditActionTypesQuery(AuditActionTypeSearchRequest Request)
    : IQuery<AuditActionTypeSearchResult>;

public sealed record ExportAuditActionTypesQuery(AuditActionTypeSearchRequest Request, int MaximumRecords)
    : IQuery<AuditActionTypeExportResult>;

public sealed class AuditActionTypeQueryHandler(IAuditActionTypeSearchService service) :
    IQueryHandler<SearchAuditActionTypesQuery, AuditActionTypeSearchResult>,
    IQueryHandler<ExportAuditActionTypesQuery, AuditActionTypeExportResult>
{
    public Task<AuditActionTypeSearchResult> Handle(
        SearchAuditActionTypesQuery query,
        CancellationToken cancellationToken) => service.SearchAsync(query.Request, cancellationToken);

    public Task<AuditActionTypeExportResult> Handle(
        ExportAuditActionTypesQuery query,
        CancellationToken cancellationToken) => service.ExportAsync(query.Request, query.MaximumRecords, cancellationToken);
}
