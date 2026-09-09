using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Courses;

/// <summary> Content completion statistics </summary>
public class ContentCompletionStats {
    public int TotalContentItems { get; set; }

    public int CompletedContentItems { get; set; }

    public int InProgressContentItems { get; set; }

    public int NotStartedContentItems { get; set; }

    public PercentValue AverageCompletionRate { get; set; }

    public ScoreValue AverageScore { get; set; }

    public int TotalTimeSpentHours { get; set; }

    public Dictionary<string, int> CompletionByContentType { get; set; } = new Dictionary<string, int>();
}
