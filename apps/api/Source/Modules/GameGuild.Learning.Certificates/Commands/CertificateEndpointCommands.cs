using GameGuild.CQRS;

namespace GameGuild.Learning.Certificates;

public sealed record IssueCertificateEndpointCommand(
    Guid TemplateId,
    Guid EnrollmentId,
    Guid UserId,
    Guid CourseId,
    Guid? TenantId) : ICommand<Result<Certificate>>;

public sealed record RevokeCertificateEndpointCommand(
    Guid CertificateId,
    string Reason) : ICommand<Result>;

public sealed record CreateCertificateTemplateEndpointCommand(
    Guid CourseId,
    string Name,
    string TemplateHtml,
    Guid? TenantId) : ICommand<Result<CertificateTemplate>>;

public sealed record UpdateCertificateTemplateEndpointCommand(
    Guid TemplateId,
    string Name,
    string? Description,
    string TemplateHtml,
    string? TemplateStyles,
    bool IsDefault,
    bool IsActive) : ICommand<Result<CertificateTemplate>>;

public sealed record DeleteCertificateTemplateEndpointCommand(
    Guid TemplateId) : ICommand<Result>;

public sealed class CertificateEndpointCommandHandler(
    ICertificateService certificateService,
    ICertificateTemplateService templateService) :
    ICommandHandler<IssueCertificateEndpointCommand, Result<Certificate>>,
    ICommandHandler<RevokeCertificateEndpointCommand, Result>,
    ICommandHandler<CreateCertificateTemplateEndpointCommand, Result<CertificateTemplate>>,
    ICommandHandler<UpdateCertificateTemplateEndpointCommand, Result<CertificateTemplate>>,
    ICommandHandler<DeleteCertificateTemplateEndpointCommand, Result>
{
    public Task<Result<Certificate>> Handle(
        IssueCertificateEndpointCommand request,
        CancellationToken cancellationToken) =>
        certificateService.IssueCertificateAsync(
            request.TemplateId,
            request.EnrollmentId,
            request.UserId,
            request.CourseId,
            request.TenantId);

    public Task<Result> Handle(
        RevokeCertificateEndpointCommand request,
        CancellationToken cancellationToken) =>
        certificateService.RevokeCertificateAsync(request.CertificateId, request.Reason);

    public Task<Result<CertificateTemplate>> Handle(
        CreateCertificateTemplateEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var template = CertificateTemplate.Create(
            request.CourseId,
            request.Name,
            request.TemplateHtml,
            request.TenantId);
        return templateService.CreateTemplateAsync(template, request.TenantId);
    }

    public async Task<Result<CertificateTemplate>> Handle(
        UpdateCertificateTemplateEndpointCommand request,
        CancellationToken cancellationToken)
    {
        var template = await templateService.GetTemplateByIdAsync(request.TemplateId).ConfigureAwait(false);
        if (template is null)
        {
            return Result.Failure<CertificateTemplate>(
                Error.NotFound("CertificateTemplate", "Certificate template not found"));
        }

        template.Update(
            request.Name,
            request.Description,
            request.TemplateHtml,
            request.TemplateStyles,
            request.IsActive);

        if (request.IsDefault)
        {
            template.SetDefault(true);
            return await templateService
                .SetDefaultTemplateAsync(template.CourseId, template.Id)
                .ConfigureAwait(false);
        }

        template.SetDefault(false);
        return await templateService.UpdateTemplateAsync(template).ConfigureAwait(false);
    }

    public Task<Result> Handle(
        DeleteCertificateTemplateEndpointCommand request,
        CancellationToken cancellationToken) =>
        templateService.DeleteTemplateAsync(request.TemplateId);
}
