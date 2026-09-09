global using static GameGuild.Learning.Assessments.Tests.TestAcademicValues;
global using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Tests;

internal static class TestAcademicValues
{
    internal static ScoreValue Score(string value) => ScoreValue.FromPoints(value);

    internal static ScoreValue Score(int value) => Score(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static ScoreValue Score(decimal value) => Score(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static PercentValue Percent(string value) => PercentValue.FromPercentage(value);

    internal static PercentValue Percent(int value) => Percent(value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    internal static PercentValue Percent(decimal value) => Percent(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
