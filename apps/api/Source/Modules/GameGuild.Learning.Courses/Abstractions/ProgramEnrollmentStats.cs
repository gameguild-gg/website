using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> Enrollment statistics for a program </summary>
public class ProgramEnrollmentStats {
    public int TotalEnrollments { get; set; }

    public int ActiveEnrollments { get; set; }

    public int CompletedEnrollments { get; set; }

    public int CancelledEnrollments { get; set; }

    public PercentValue AverageProgressPercentage { get; set; }

    public PercentValue CompletionRate { get; set; }

    public PercentValue? AverageFinalGrade { get; set; }

    public int CertificatesIssued { get; set; }
}
