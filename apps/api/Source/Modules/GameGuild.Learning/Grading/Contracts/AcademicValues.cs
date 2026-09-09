using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameGuild.Learning.Grading.Contracts;

[JsonConverter(typeof(ScoreValueJsonConverter))]
public readonly record struct ScoreValue : IComparable<ScoreValue>
{
    public const int Scale = 100;
    public const int MaximumUnits = int.MaxValue;

    private static readonly Regex PointsPattern = new(
        "^(0|[1-9]\\d*)(?:\\.(\\d{1,2}))?$",
        RegexOptions.CultureInvariant);

    private ScoreValue(int units) => Units = units;

    public int Units { get; }

    public static ScoreValue Zero { get; } = new(0);

    public static ScoreValue FromUnits(int units)
    {
        if (units < 0) throw new ArgumentOutOfRangeException(nameof(units), "ScoreValue cannot be negative.");
        return new ScoreValue(units);
    }

    public static ScoreValue FromPoints(string value) =>
        FromUnits(ParseHumanUnits(value, MaximumUnits, "ScoreValue"));

    public static ScoreValue Sum(IEnumerable<ScoreValue> values)
    {
        long total = 0;
        foreach (var value in values)
        {
            total = checked(total + value.Units);
        }

        return FromWideUnits(total, nameof(values));
    }

    public static ScoreValue Average(IEnumerable<ScoreValue> values)
    {
        var materialized = values.ToArray();
        if (materialized.Length == 0) return Zero;
        var total = materialized.Aggregate(0L, (sum, value) => checked(sum + value.Units));
        return FromWideUnits(DivideRoundHalfUp(total, materialized.Length), nameof(values));
    }

    public static ScoreValue ByRatio(ScoreValue maximum, BigInteger earnedUnits, BigInteger totalUnits)
    {
        if (earnedUnits < 0 || totalUnits <= 0 || earnedUnits > totalUnits)
        {
            throw new ArgumentOutOfRangeException(nameof(earnedUnits), "The ratio must satisfy 0 <= earned <= total.");
        }

        var rounded = DivideRoundHalfUp((BigInteger)maximum.Units * earnedUnits, totalUnits);
        return FromWideUnits(rounded, nameof(earnedUnits));
    }

    public int CompareTo(ScoreValue other) => Units.CompareTo(other.Units);

    public string ToPointsString() => FormatHumanUnits(Units);

    public override string ToString() => ToPointsString();

    internal static int ParseHumanUnits(string value, int maximumUnits, string label)
    {
        ArgumentNullException.ThrowIfNull(value);
        var match = PointsPattern.Match(value.Trim());
        if (!match.Success)
        {
            throw new FormatException($"{label} must be a non-negative decimal with at most two fractional digits.");
        }

        var whole = BigInteger.Parse(match.Groups[1].Value);
        var fractionText = match.Groups[2].Success ? match.Groups[2].Value.PadRight(2, '0') : "00";
        var units = whole * Scale + BigInteger.Parse(fractionText);
        return CheckedUnits(units, maximumUnits, label);
    }

    internal static string FormatHumanUnits(int units)
    {
        var whole = units / Scale;
        var fraction = units % Scale;
        return fraction switch
        {
            0 => whole.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ when fraction % 10 == 0 => $"{whole}.{fraction / 10}",
            _ => $"{whole}.{fraction:00}",
        };
    }

    internal static int CheckedUnits(BigInteger units, int maximumUnits, string label)
    {
        if (units < 0 || units > maximumUnits)
        {
            throw new ArgumentOutOfRangeException(label, $"{label} is outside the supported range.");
        }

        return (int)units;
    }

    private static ScoreValue FromWideUnits(long units, string label)
    {
        if (units < 0 || units > MaximumUnits)
        {
            throw new OverflowException($"{label} exceeds the ScoreValue range.");
        }

        return new ScoreValue((int)units);
    }

    private static ScoreValue FromWideUnits(BigInteger units, string label) =>
        new(CheckedUnits(units, MaximumUnits, label));

    private static long DivideRoundHalfUp(long numerator, long denominator) =>
        checked((numerator + denominator / 2) / denominator);

    internal static BigInteger DivideRoundHalfUp(BigInteger numerator, BigInteger denominator) =>
        (numerator + denominator / 2) / denominator;
}

[JsonConverter(typeof(PercentValueJsonConverter))]
public readonly record struct PercentValue : IComparable<PercentValue>
{
    public const int Scale = ScoreValue.Scale;
    public const int MaximumUnits = 100 * Scale;

    private PercentValue(int units) => Units = units;

    public int Units { get; }

    public static PercentValue Zero { get; } = new(0);
    public static PercentValue Hundred { get; } = new(MaximumUnits);

    public static PercentValue FromUnits(int units)
    {
        if (units is < 0 or > MaximumUnits)
        {
            throw new ArgumentOutOfRangeException(nameof(units), "PercentValue must be between 0 and 10000 units.");
        }

        return new PercentValue(units);
    }

    public static PercentValue FromPercentage(string value) =>
        FromUnits(ScoreValue.ParseHumanUnits(value, MaximumUnits, "PercentValue"));

    public static PercentValue FromRatio(BigInteger earnedUnits, BigInteger totalUnits)
    {
        if (earnedUnits < 0 || totalUnits <= 0 || earnedUnits > totalUnits)
        {
            throw new ArgumentOutOfRangeException(nameof(earnedUnits), "The ratio must satisfy 0 <= earned <= total.");
        }

        var units = ScoreValue.DivideRoundHalfUp(earnedUnits * MaximumUnits, totalUnits);
        return FromUnits(ScoreValue.CheckedUnits(units, MaximumUnits, nameof(earnedUnits)));
    }

    public static PercentValue FromScores(ScoreValue earned, ScoreValue possible)
    {
        if (possible.Units <= 0 || earned.Units > possible.Units)
        {
            throw new ArgumentOutOfRangeException(nameof(possible), "Scores must satisfy 0 <= earned <= possible and possible must be positive.");
        }

        return FromRatio(earned.Units, possible.Units);
    }

    public static PercentValue Average(IEnumerable<PercentValue> values)
    {
        var materialized = values.ToArray();
        if (materialized.Length == 0) return Zero;
        var total = materialized.Aggregate(0L, (sum, value) => checked(sum + value.Units));
        var rounded = checked((total + materialized.Length / 2) / materialized.Length);
        return FromUnits(checked((int)rounded));
    }

    public int CompareTo(PercentValue other) => Units.CompareTo(other.Units);

    public string ToPercentageString() => ScoreValue.FormatHumanUnits(Units);

    public override string ToString() => ToPercentageString();
}

public sealed class ScoreValueJsonConverter : JsonConverter<ScoreValue>
{
    public override ScoreValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var units)
            ? ScoreValue.FromUnits(units)
            : throw new JsonException("ScoreValue must be a non-negative JSON integer.");

    public override void Write(Utf8JsonWriter writer, ScoreValue value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Units);
}

public sealed class PercentValueJsonConverter : JsonConverter<PercentValue>
{
    public override PercentValue Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var units)
            ? PercentValue.FromUnits(units)
            : throw new JsonException("PercentValue must be a JSON integer between 0 and 10000.");

    public override void Write(Utf8JsonWriter writer, PercentValue value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value.Units);
}
