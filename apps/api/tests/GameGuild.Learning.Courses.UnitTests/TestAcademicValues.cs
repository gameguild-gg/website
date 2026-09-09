global using GameGuild.Learning.Grading.Contracts;
global using static GameGuild.Learning.Courses.UnitTests.TestAcademicValues;

namespace GameGuild.Learning.Courses.UnitTests;

internal static class TestAcademicValues
{
    public static ScoreValue Score(string value) => ScoreValue.FromPoints(value);

    public static PercentValue Percent(string value) => PercentValue.FromPercentage(value);
}
