using GameGuild.CQRS;

namespace GameGuild.Compliance.Audit;

public sealed record UploadComplianceDocumentCommand(UploadComplianceDocumentRequest Request) : ICommand<ComplianceDocumentResponse?>;
public sealed record ReviewComplianceDocumentCommand(Guid DocumentId, ReviewComplianceDocumentRequest Request) : ICommand<ComplianceDocumentResponse?>;
public sealed record PrepareCompliancePackageCommand(CreateCompliancePackageRequest Request) : ICommand<CompliancePackageResponse?>;

public sealed class CompliancePackagingCommandHandler(IComplianceEvidencePackagingService service) :
    ICommandHandler<UploadComplianceDocumentCommand, ComplianceDocumentResponse?>,
    ICommandHandler<ReviewComplianceDocumentCommand, ComplianceDocumentResponse?>,
    ICommandHandler<PrepareCompliancePackageCommand, CompliancePackageResponse?>
{
    public Task<ComplianceDocumentResponse?> Handle(UploadComplianceDocumentCommand command, CancellationToken cancellationToken) =>
        service.UploadDocumentAsync(command.Request, cancellationToken);
    public Task<ComplianceDocumentResponse?> Handle(ReviewComplianceDocumentCommand command, CancellationToken cancellationToken) =>
        service.ReviewDocumentAsync(command.DocumentId, command.Request, cancellationToken);
    public Task<CompliancePackageResponse?> Handle(PrepareCompliancePackageCommand command, CancellationToken cancellationToken) =>
        service.CreatePackageAsync(command.Request, cancellationToken);
}
