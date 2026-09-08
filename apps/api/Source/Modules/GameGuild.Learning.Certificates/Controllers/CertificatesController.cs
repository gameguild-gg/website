using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Certificates;

/// <summary>
/// Controller for certificate management and verification
/// </summary>
[Route("api/certificates")]
[Authorize]
public class CertificatesController : BaseApiController
{
    private readonly ICertificateService _certificateService;
    private readonly ICertificateTemplateService _templateService;
    private readonly IActorContextAccessor _actorContextAccessor;
    private readonly ILogger<CertificatesController> _logger;
    private readonly ISender _sender;

    public CertificatesController(
        ICertificateService certificateService,
        ICertificateTemplateService templateService,
        IActorContextAccessor actorContextAccessor,
        ILogger<CertificatesController> logger,
        ISender sender)
    {
        _certificateService = certificateService;
        _templateService = templateService;
        _actorContextAccessor = actorContextAccessor;
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Get certificates for the current user
    /// </summary>
    [HttpGet("my")]
    public async Task<ActionResult<IEnumerable<CertificateDto>>> GetMyCertificates()
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var certificates = await _certificateService.GetUserCertificatesAsync(actor.SubjectIdAsGuid.Value, actor.TenantId).ConfigureAwait(false);
        var dtos = certificates.Select(c => CertificateDto.FromEntity(c));

        return Ok(dtos);
    }

    /// <summary>
    /// Get a certificate by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CertificateDto>> GetCertificate(Guid id)
    {
        var certificate = await _certificateService.GetCertificateByIdAsync(id).ConfigureAwait(false);
        if (certificate == null)
        {
            return NotFound();
        }

        return Ok(CertificateDto.FromEntity(certificate));
    }

    /// <summary>
    /// Verify a certificate by its number (public endpoint)
    /// </summary>
    [HttpGet("verify/{certificateNumber}")]
    [AllowAnonymous]
    public async Task<ActionResult<CertificateVerificationResult>> VerifyCertificate(string certificateNumber)
    {
        var result = await _certificateService.VerifyCertificateAsync(certificateNumber).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Issue a certificate for an enrollment
    /// </summary>
    [HttpPost("issue")]
    [RequireResourcePermission<PermissionType, Certificate>(PermissionType.Create)]
    public async Task<ActionResult<CertificateDto>> IssueCertificate([FromBody] IssueCertificateRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        if (actor.SubjectIdAsGuid == null)
        {
            return Unauthorized();
        }

        var result = await _sender.Send(new IssueCertificateEndpointCommand(
            request.TemplateId,
            request.EnrollmentId,
            request.UserId,
            request.CourseId,
            actor.TenantId)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(nameof(GetCertificate), new { id = result.Value.Id }, CertificateDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Revoke a certificate
    /// </summary>
    [HttpPost("{id:guid}/revoke")]
    [RequireResourcePermission<PermissionType, Certificate>(PermissionType.Delete)]
    public async Task<ActionResult> RevokeCertificate(Guid id, [FromBody] RevokeCertificateRequest request)
    {
        var result = await _sender.Send(new RevokeCertificateEndpointCommand(id, request.Reason)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Get certificates for a specific course
    /// </summary>
    [HttpGet("course/{courseId:guid}")]
    public async Task<ActionResult<IEnumerable<CertificateDto>>> GetCourseCertificates(Guid courseId)
    {
        var actor = _actorContextAccessor.ActorContext;

        var certificates = await _certificateService.GetCourseCertificatesAsync(courseId, actor.TenantId).ConfigureAwait(false);
        var dtos = certificates.Select(c => CertificateDto.FromEntity(c));

        return Ok(dtos);
    }

    /// <summary>
    /// Get certificate templates for a specific course
    /// </summary>
    [HttpGet("templates/course/{courseId:guid}")]
    [RequireContentTypePermission<CertificateTemplate>(PermissionType.Read)]
    public async Task<ActionResult<IEnumerable<CertificateTemplateDto>>> GetCourseCertificateTemplates(Guid courseId)
    {
        var templates = await _templateService.GetTemplatesByCourseAsync(courseId).ConfigureAwait(false);
        return Ok(templates.Select(CertificateTemplateDto.FromEntity));
    }

    /// <summary>
    /// Get a certificate template by ID
    /// </summary>
    [HttpGet("templates/{templateId:guid}")]
    [RequireContentTypePermission<CertificateTemplate>(PermissionType.Read)]
    public async Task<ActionResult<CertificateTemplateDetailDto>> GetCertificateTemplate(Guid templateId)
    {
        var template = await _templateService.GetTemplateByIdAsync(templateId).ConfigureAwait(false);
        if (template is null)
        {
            return NotFound();
        }

        return Ok(CertificateTemplateDetailDto.FromEntity(template));
    }

    /// <summary>
    /// Create a certificate template for a course
    /// </summary>
    [HttpPost("templates")]
    [RequireContentTypePermission<CertificateTemplate>(PermissionType.Create)]
    public async Task<ActionResult<CertificateTemplateDetailDto>> CreateCertificateTemplate([FromBody] CreateCertificateTemplateRequest request)
    {
        var actor = _actorContextAccessor.ActorContext;
        var result = await _sender.Send(new CreateCertificateTemplateEndpointCommand(
            request.CourseId,
            request.Name,
            request.TemplateHtml,
            actor.TenantId)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return BadRequest(result.Error);
        }

        return CreatedAtAction(
            nameof(GetCertificateTemplate),
            new { templateId = result.Value.Id },
            CertificateTemplateDetailDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Update a certificate template and its course-default state
    /// </summary>
    [HttpPut("templates/{templateId:guid}")]
    [RequireContentTypePermission<CertificateTemplate>(PermissionType.Edit)]
    public async Task<ActionResult<CertificateTemplateDetailDto>> UpdateCertificateTemplate(
        Guid templateId,
        [FromBody] UpdateCertificateTemplateRequest request)
    {
        var result = await _sender.Send(new UpdateCertificateTemplateEndpointCommand(
            templateId,
            request.Name,
            request.Description,
            request.TemplateHtml,
            request.TemplateStyles,
            request.IsDefault,
            request.IsActive)).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return Ok(CertificateTemplateDetailDto.FromEntity(result.Value));
    }

    /// <summary>
    /// Delete a certificate template
    /// </summary>
    [HttpDelete("templates/{templateId:guid}")]
    [RequireContentTypePermission<CertificateTemplate>(PermissionType.Delete)]
    public async Task<ActionResult> DeleteCertificateTemplate(Guid templateId)
    {
        var result = await _sender.Send(new DeleteCertificateTemplateEndpointCommand(templateId)).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            return result.Error.Type == ErrorType.NotFound ? NotFound(result.Error) : BadRequest(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Get certificates expiring within the specified days
    /// </summary>
    [HttpGet("expiring")]
    [RequireContentTypePermission<Certificate>(PermissionType.Read)]
    public async Task<ActionResult<IEnumerable<CertificateDto>>> GetExpiringCertificates([FromQuery] int days = 30)
    {
        var certificates = await _certificateService.GetExpiringCertificatesAsync(days).ConfigureAwait(false);
        var dtos = certificates.Select(c => CertificateDto.FromEntity(c));

        return Ok(dtos);
    }
}

/// <summary>
/// DTO for certificate display
/// </summary>
public sealed record CertificateDto(
    Guid Id,
    Guid TemplateId,
    Guid EnrollmentId,
    Guid UserId,
    Guid CourseId,
    string CertificateNumber,
    string RecipientName,
    string CourseName,
    DateTime IssuedAt,
    DateTime? ExpiresAt,
    CertificateStatus Status)
{
    public static CertificateDto FromEntity(Certificate c) => new(
        c.Id,
        c.TemplateId,
        c.EnrollmentId,
        c.UserId,
        c.CourseId,
        c.CertificateNumber,
        c.RecipientName,
        c.CourseName,
        c.IssuedAt,
        c.ExpiresAt,
        c.Status);
}

/// <summary>
/// DTO for certificate template list display
/// </summary>
public sealed record CertificateTemplateDto(
    Guid Id,
    Guid CourseId,
    Guid? TenantId,
    string Name,
    string? Description,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CertificateTemplateDto FromEntity(CertificateTemplate template) => new(
        template.Id,
        template.CourseId,
        template.TenantId,
        template.Name,
        template.Description,
        template.IsDefault,
        template.IsActive,
        template.CreatedAt,
        template.UpdatedAt);
}

/// <summary>
/// DTO for certificate template editing and preview
/// </summary>
public sealed record CertificateTemplateDetailDto(
    Guid Id,
    Guid CourseId,
    Guid? TenantId,
    string Name,
    string? Description,
    string TemplateHtml,
    string? TemplateStyles,
    bool IsDefault,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static CertificateTemplateDetailDto FromEntity(CertificateTemplate template) => new(
        template.Id,
        template.CourseId,
        template.TenantId,
        template.Name,
        template.Description,
        template.TemplateHtml,
        template.TemplateStyles,
        template.IsDefault,
        template.IsActive,
        template.CreatedAt,
        template.UpdatedAt);
}

/// <summary>
/// Request to create a certificate template
/// </summary>
public sealed record CreateCertificateTemplateRequest(Guid CourseId, string Name, string TemplateHtml);

/// <summary>
/// Request to update a certificate template
/// </summary>
public sealed record UpdateCertificateTemplateRequest(
    string Name,
    string? Description,
    string TemplateHtml,
    string? TemplateStyles,
    bool IsDefault,
    bool IsActive);

/// <summary>
/// Request to issue a certificate
/// </summary>
public sealed record IssueCertificateRequest(
    Guid TemplateId,
    Guid EnrollmentId,
    Guid UserId,
    Guid CourseId);

/// <summary>
/// Request to revoke a certificate
/// </summary>
public sealed record RevokeCertificateRequest(string Reason);
