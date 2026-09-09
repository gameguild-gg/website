using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary>
/// Represents a user's enrollment in a learning program with detailed progress tracking
/// </summary>
[Table("program_enrollments")]
[Index(nameof(UserId), nameof(ProgramId), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(ProgramId))]
[Index(nameof(EnrollmentStatus))]
[Index(nameof(EnrolledAt))]
[Index(nameof(CompletedAt))]
[Index(nameof(TenantId))]
public class ProgramEnrollment : EntityBase
{
    /// <summary>
    /// User ID
    /// </summary>
    [Required]
    public Guid UserId { get; set; }

    /// <summary>
    /// Program ID
    /// </summary>
    [Required]
    public Guid ProgramId { get; set; }

    /// <summary>
    /// Source of the enrollment (manual, product purchase, etc.)
    /// </summary>
    public EnrollmentSource EnrollmentSource { get; set; } = EnrollmentSource.Manual;

    /// <summary>
    /// Current status of the enrollment
    /// </summary>
    public EnrollmentStatus EnrollmentStatus { get; set; } = EnrollmentStatus.Active;

    /// <summary>
    /// Current completion status
    /// </summary>
    public CompletionStatus CompletionStatus { get; set; } = CompletionStatus.NotStarted;

    /// <summary>
    /// Date when user enrolled in the program
    /// </summary>
    public DateTime EnrolledAt { get; set; } = SystemClock.UtcNow;

    /// <summary>
    /// Date when enrollment started (may differ from EnrolledAt)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Date when enrollment was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Current progress percentage (0-100)
    /// </summary>
    public PercentValue ProgressPercentage { get; set; } = PercentValue.Zero;

    /// <summary>
    /// Final grade for the program (0-100)
    /// </summary>
    public PercentValue? FinalGrade { get; set; }

    /// <summary>
    /// Whether a certificate has been issued for this enrollment
    /// </summary>
    public bool CertificateIssued { get; set; } = false;

    /// <summary>
    /// Date when certificate was issued
    /// </summary>
    public DateTime? CertificateIssuedAt { get; set; }

    /// <summary>
    /// Navigation property to Program
    /// </summary>
    public virtual Program? Program { get; set; }

    /// <summary>
    /// Navigation property to User
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User? User { get; set; }

    /// <summary>
    /// Mark enrollment as completed
    /// </summary>
    public void MarkAsCompleted(PercentValue? finalGrade = null)
    {
        CompletionStatus = CompletionStatus.Completed;
        CompletedAt = SystemClock.UtcNow;
        ProgressPercentage = PercentValue.Hundred;
        if (finalGrade.HasValue)
        {
            FinalGrade = finalGrade.Value;
        }
        Touch();
    }
}
