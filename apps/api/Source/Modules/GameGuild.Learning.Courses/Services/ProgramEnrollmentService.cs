using GameGuild.Learning.Abstractions;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GameGuild.Learning.Courses;

/// <summary> Service implementation for program enrollment management </summary>
public class ProgramEnrollmentService : IProgramEnrollmentService {
  private readonly IApplicationDbContext _context;
  private readonly ILogger<ProgramEnrollmentService> _logger;
  private readonly ICertificateIssuanceService? _certificateIssuanceService;
  private readonly IProductProgramProvider? _productProgramProvider;

  public ProgramEnrollmentService(
      IApplicationDbContext context, 
      ILogger<ProgramEnrollmentService> logger,
      ICertificateIssuanceService? certificateIssuanceService = null,
      IProductProgramProvider? productProgramProvider = null) 
  { 
    _context = context; 
    _logger = logger;
    _certificateIssuanceService = certificateIssuanceService;
    _productProgramProvider = productProgramProvider;
  }

  /// <summary> Enroll a user in a program </summary>
  public async Task<ProgramEnrollment> EnrollUserAsync(Guid userId, Guid programId, EnrollmentSource source = EnrollmentSource.Manual) {
    // Check if user is already enrolled
    var existingEnrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.UserId == userId && pe.ProgramId == programId);

    if (existingEnrollment != null) {
      // Reactivate if cancelled or expired
      if (existingEnrollment.EnrollmentStatus == EnrollmentStatus.Cancelled || existingEnrollment.EnrollmentStatus == EnrollmentStatus.Expired) {
        existingEnrollment.EnrollmentStatus = EnrollmentStatus.Active;
        existingEnrollment.EnrolledAt = SystemClock.UtcNow;
        existingEnrollment.EnrollmentSource = source;
        existingEnrollment.Touch();
        await _context.SaveChangesAsync().ConfigureAwait(false);
      }

      return existingEnrollment;
    }

    // Verify program exists and is available for enrollment
    var program = await _context.Set<Program>().FirstOrDefaultAsync(p => p.Id == programId);

    if (program == null) throw new ArgumentException("Program not found", nameof(programId));

    if (program.EnrollmentStatus != EnrollmentStatus.Open) throw new InvalidOperationException("Program is not available for enrollment");

    // Create new enrollment
    var enrollment = new ProgramEnrollment { UserId = userId, ProgramId = programId, EnrollmentSource = source, EnrolledAt = SystemClock.UtcNow, StartDate = SystemClock.UtcNow };

    _context.Set<ProgramEnrollment>().Add(enrollment);
    await _context.SaveChangesAsync().ConfigureAwait(false);

    return enrollment;
  }

  /// <summary> Auto-enroll user in all programs included in a product </summary>
  public async Task<IEnumerable<ProgramEnrollment>> AutoEnrollInProductProgramsAsync(Guid userId, Guid productId) {
    // Use IProductProgramProvider if available, otherwise fall back to direct query
    // This abstraction allows the Learning module to remain loosely coupled to Commerce
    IReadOnlyList<Guid> programIds;
    
    if (_productProgramProvider != null) {
      programIds = await _productProgramProvider.GetProgramIdsForProductAsync(productId).ConfigureAwait(false);
    } else {
      // Fallback for backward compatibility - direct query
      _logger.LogWarning("IProductProgramProvider not configured, using direct ProductProgram query");
      programIds = await _context.Set<ProductProgram>()
          .Where(pp => pp.ProductId == productId)
          .OrderBy(pp => pp.SortOrder)
          .Select(pp => pp.ProgramId)
          .ToListAsync().ConfigureAwait(false);
    }

    var enrollments = new List<ProgramEnrollment>();

    foreach (var programId in programIds) {
      try {
        var enrollment = await EnrollUserAsync(userId, programId, EnrollmentSource.ProductPurchase).ConfigureAwait(false);
        enrollments.Add(enrollment);
        _logger.LogInformation(
            "Successfully enrolled user {UserId} in program {ProgramId} from product {ProductId}",
            userId, programId, productId);
      }
      catch (Exception ex) {
        _logger.LogWarning(ex,
            "Failed to enroll user {UserId} in program {ProgramId} from product {ProductId}",
            userId, programId, productId);
          throw;
      }
    }

    return enrollments;
  }

  /// <summary> Get user's enrollment in a program </summary>
  public async Task<ProgramEnrollment?> GetEnrollmentAsync(Guid userId, Guid programId) {
    return await _context.Set<ProgramEnrollment>().Include(pe => pe.Program).Include(pe => pe.User).FirstOrDefaultAsync(pe => pe.UserId == userId && pe.ProgramId == programId);
  }

  /// <summary> Get all user's enrollments </summary>
  public async Task<IEnumerable<ProgramEnrollment>> GetUserEnrollmentsAsync(Guid userId, EnrollmentStatus? status = null) {
    var query = _context.Set<ProgramEnrollment>().Include(pe => pe.Program).Where(pe => pe.UserId == userId);

    if (status.HasValue) { query = query.Where(pe => pe.EnrollmentStatus == status.Value); }

    return await query.OrderByDescending(pe => pe.EnrolledAt).ToListAsync();
  }

  /// <summary> Update enrollment progress </summary>
  public async Task<ProgramEnrollment> UpdateProgressAsync(Guid enrollmentId, PercentValue progressPercentage) {
    var enrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.Id == enrollmentId);

    if (enrollment == null) throw new ArgumentException("Enrollment not found", nameof(enrollmentId));

    enrollment.ProgressPercentage = progressPercentage;

    // Update completion status based on progress
    if (enrollment.ProgressPercentage.CompareTo(PercentValue.Hundred) == 0 && enrollment.CompletionStatus != CompletionStatus.Completed) {
      enrollment.CompletionStatus = CompletionStatus.Completed;
      enrollment.CompletedAt = SystemClock.UtcNow;
    }
    else if (enrollment.ProgressPercentage.CompareTo(PercentValue.Zero) > 0 && enrollment.CompletionStatus == CompletionStatus.NotStarted) { enrollment.CompletionStatus = CompletionStatus.InProgress; }

    enrollment.Touch();
    await _context.SaveChangesAsync().ConfigureAwait(false);

    return enrollment;
  }

  /// <summary> Mark enrollment as completed </summary>
  public async Task<ProgramEnrollment> CompleteEnrollmentAsync(Guid enrollmentId, PercentValue? finalGrade = null) {
    var enrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.Id == enrollmentId);

    if (enrollment == null) throw new ArgumentException("Enrollment not found", nameof(enrollmentId));

    enrollment.MarkAsCompleted(finalGrade);
    await _context.SaveChangesAsync().ConfigureAwait(false);

    return enrollment;
  }

  /// <summary> Cancel enrollment </summary>
  public async Task<bool> CancelEnrollmentAsync(Guid enrollmentId) {
    var enrollment = await _context.Set<ProgramEnrollment>().FirstOrDefaultAsync(pe => pe.Id == enrollmentId);

    if (enrollment == null) return false;

    enrollment.EnrollmentStatus = EnrollmentStatus.Cancelled;
    enrollment.Touch();
    await _context.SaveChangesAsync().ConfigureAwait(false);

    return true;
  }

  /// <summary> Check if user is enrolled in program </summary>
  public async Task<bool> IsUserEnrolledAsync(Guid userId, Guid programId) { return await _context.Set<ProgramEnrollment>().AnyAsync(pe => pe.UserId == userId && pe.ProgramId == programId && pe.EnrollmentStatus == EnrollmentStatus.Active); }

  /// <summary> Get enrollment statistics for a program </summary>
  public async Task<ProgramEnrollmentStats> GetEnrollmentStatsAsync(Guid programId) {
    var enrollments = await _context.Set<ProgramEnrollment>().Where(pe => pe.ProgramId == programId).ToListAsync();

    var totalEnrollments = enrollments.Count;
    var activeEnrollments = enrollments.Count(e => e.EnrollmentStatus == EnrollmentStatus.Active);
    var completedEnrollments = enrollments.Count(e => e.CompletionStatus == CompletionStatus.Completed);
    var cancelledEnrollments = enrollments.Count(e => e.EnrollmentStatus == EnrollmentStatus.Cancelled);

    return new ProgramEnrollmentStats {
      TotalEnrollments = totalEnrollments,
      ActiveEnrollments = activeEnrollments,
      CompletedEnrollments = completedEnrollments,
      CancelledEnrollments = cancelledEnrollments,
      AverageProgressPercentage = PercentValue.Average(enrollments.Select(e => e.ProgressPercentage)),
      CompletionRate = totalEnrollments > 0
        ? PercentValue.FromRatio(completedEnrollments, totalEnrollments)
        : PercentValue.Zero,
      AverageFinalGrade = enrollments.Any(e => e.FinalGrade.HasValue)
        ? PercentValue.Average(enrollments.Where(e => e.FinalGrade.HasValue).Select(e => e.FinalGrade!.Value))
        : null,
      CertificatesIssued = enrollments.Count(e => e.CertificateIssued),
    };
  }

  /// <summary> Issue certificate for completed enrollment </summary>
  public async Task<bool> IssueCertificateAsync(Guid enrollmentId) {
    var enrollment = await _context.Set<ProgramEnrollment>().Include(pe => pe.Program).Include(pe => pe.User).FirstOrDefaultAsync(pe => pe.Id == enrollmentId);

    if (enrollment == null || enrollment.CompletionStatus != CompletionStatus.Completed) {
      _logger.LogWarning(
          "Cannot issue certificate for enrollment {EnrollmentId}: enrollment not found or not completed",
          enrollmentId);
      return false;
    }

    if (enrollment.CertificateIssued) {
      _logger.LogInformation("Certificate already issued for enrollment {EnrollmentId}", enrollmentId);
      return true; // Already issued
    }

    // Try to issue via certificate issuance service if available (DIP: depends on abstraction)
    if (_certificateIssuanceService != null) {
      var result = await _certificateIssuanceService.IssueCertificateForEnrollmentAsync(
          enrollmentId,
          enrollment.UserId,
          enrollment.ProgramId,
          enrollment.TenantId).ConfigureAwait(false);

      if (result.IsSuccess) {
        _logger.LogInformation(
            "Certificate issued successfully for enrollment {EnrollmentId}, certificate ID: {CertificateId}",
            enrollmentId, result.Value);
      } else {
        _logger.LogWarning(
            "Failed to issue certificate for enrollment {EnrollmentId}: {Error}",
            enrollmentId, result.Error.Description);
      }
    } else {
      _logger.LogDebug(
          "No certificate issuance service configured, skipping certificate generation for enrollment {EnrollmentId}",
          enrollmentId);
    }

    // Mark enrollment as having certificate issued
    enrollment.CertificateIssued = true;
    enrollment.CertificateIssuedAt = SystemClock.UtcNow;
    enrollment.CompletionStatus = CompletionStatus.CompletedWithCertificate;
    enrollment.Touch();

    await _context.SaveChangesAsync().ConfigureAwait(false);

    _logger.LogInformation(
        "Certificate issuance completed for enrollment {EnrollmentId}",
        enrollmentId);

    return true;
  }
}
