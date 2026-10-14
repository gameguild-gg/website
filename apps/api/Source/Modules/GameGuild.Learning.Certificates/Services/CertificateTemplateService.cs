using GameGuild.Learning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Certificates;

/// <summary>
/// Service implementation for certificate template management
/// </summary>
public class CertificateTemplateService(
    IApplicationDbContext context,
    ILogger<CertificateTemplateService> logger,
    ILearningAuditEventSink auditSink,
    IRequestContextAccessor? requestContextAccessor = null) : ICertificateTemplateService
{
    private readonly IApplicationDbContext _context = context;
    private readonly ILogger<CertificateTemplateService> _logger = logger;
    private readonly ILearningAuditEventSink _auditSink = auditSink ?? throw new ArgumentNullException(nameof(auditSink));
    private Guid? ActingUserId => requestContextAccessor?.CurrentUserId;

    public async Task<Result<CertificateTemplate>> CreateTemplateAsync(CertificateTemplate template, Guid? tenantId = null)
    {
        try
        {
            _context.Set<CertificateTemplate>().Add(template);
            // Template mutations follow the same fail-closed contract as certificates: the row
            // and its audit event commit together or not at all.
            var transaction = await _context.BeginAuditedMutationTransactionAsync().ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await _auditSink.RecordAsync(new LearningAuditEvent(
                    "Learning.Certificates.TemplateCreated", "CertificateTemplate", template.Id.ToString(),
                    ActingUserId, template.TenantId ?? tenantId,
                    $"Certificate template '{template.Name}' created",
                    Metadata: new { template.CourseId, template.IsActive, template.IsDefault }), CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAuditedMutationAsync().ConfigureAwait(false);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Certificate template created: {TemplateId} for course {CourseId}", template.Id, template.CourseId);

            return Result.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating certificate template for course {CourseId}", template.CourseId);
            return Result.Failure<CertificateTemplate>(Error.Failure("CreateTemplate", "Failed to create certificate template"));
        }
    }

    public async Task<CertificateTemplate?> GetTemplateByIdAsync(Guid id)
    {
        return await _context.Set<CertificateTemplate>()
            .FirstOrDefaultAsync(t => t.Id == id).ConfigureAwait(false);
    }

    public async Task<IEnumerable<CertificateTemplate>> GetTemplatesByCourseAsync(Guid courseId)
    {
        return await _context.Set<CertificateTemplate>()
            .Where(t => t.CourseId == courseId)
            .ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result<CertificateTemplate>> UpdateTemplateAsync(CertificateTemplate template)
    {
        try
        {
            _context.Set<CertificateTemplate>().Update(template);
            var transaction = await _context.BeginAuditedMutationTransactionAsync().ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await _auditSink.RecordAsync(new LearningAuditEvent(
                    "Learning.Certificates.TemplateUpdated", "CertificateTemplate", template.Id.ToString(),
                    ActingUserId, template.TenantId,
                    $"Certificate template '{template.Name}' updated",
                    Metadata: new { template.CourseId, template.IsActive, template.IsDefault }), CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAuditedMutationAsync().ConfigureAwait(false);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Certificate template updated: {TemplateId}", template.Id);

            return Result.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating certificate template {TemplateId}", template.Id);
            return Result.Failure<CertificateTemplate>(Error.Failure("UpdateTemplate", "Failed to update certificate template"));
        }
    }

    public async Task<Result> DeleteTemplateAsync(Guid id)
    {
        try
        {
            var template = await GetTemplateByIdAsync(id).ConfigureAwait(false);
            if (template == null)
            {
                return Result.Failure(Error.NotFound("Template", "Certificate template not found"));
            }

            _context.Set<CertificateTemplate>().Remove(template);
            var transaction = await _context.BeginAuditedMutationTransactionAsync().ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await _auditSink.RecordAsync(new LearningAuditEvent(
                    "Learning.Certificates.TemplateDeleted", "CertificateTemplate", template.Id.ToString(),
                    ActingUserId, template.TenantId,
                    $"Certificate template '{template.Name}' deleted",
                    Metadata: new { template.CourseId }), CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAuditedMutationAsync().ConfigureAwait(false);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Certificate template deleted: {TemplateId}", id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting certificate template {TemplateId}", id);
            return Result.Failure(Error.Failure("DeleteTemplate", "Failed to delete certificate template"));
        }
    }

    public async Task<IEnumerable<CertificateTemplate>> GetActiveTemplatesAsync(Guid? tenantId = null)
    {
        var query = _context.Set<CertificateTemplate>()
            .Where(t => t.IsActive);

        if (tenantId.HasValue)
        {
            query = query.Where(t => t.TenantId == tenantId.Value);
        }

        return await query.ToListAsync().ConfigureAwait(false);
    }

    public async Task<Result<CertificateTemplate>> SetDefaultTemplateAsync(Guid courseId, Guid templateId)
    {
        try
        {
            // Reset all templates for this course to non-default
            var templates = await _context.Set<CertificateTemplate>()
                .Where(t => t.CourseId == courseId)
                .ToListAsync().ConfigureAwait(false);

            var template = templates.FirstOrDefault(t => t.Id == templateId);
            if (template == null)
            {
                return Result.Failure<CertificateTemplate>(Error.NotFound("Template", "Certificate template not found"));
            }

            foreach (var courseTemplate in templates)
            {
                courseTemplate.SetDefault(courseTemplate.Id == templateId);
            }

            var transaction = await _context.BeginAuditedMutationTransactionAsync().ConfigureAwait(false);
            try
            {
                await _context.SaveChangesAsync().ConfigureAwait(false);
                await _auditSink.RecordAsync(new LearningAuditEvent(
                    "Learning.Certificates.TemplateDefaultChanged", "CertificateTemplate", templateId.ToString(),
                    ActingUserId, template.TenantId,
                    $"Certificate template '{template.Name}' set as default for course {courseId}",
                    Metadata: new { CourseId = courseId, TemplateId = templateId }), CancellationToken.None).ConfigureAwait(false);
                await transaction.CommitAuditedMutationAsync().ConfigureAwait(false);
            }
            finally
            {
                if (transaction is not null)
                {
                    await transaction.DisposeAsync().ConfigureAwait(false);
                }
            }

            _logger.LogInformation("Set default certificate template: {TemplateId} for course {CourseId}", templateId, courseId);

            return Result.Success(template);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default template {TemplateId} for course {CourseId}", templateId, courseId);
            return Result.Failure<CertificateTemplate>(Error.Failure("SetDefaultTemplate", "Failed to set default template"));
        }
    }
}
