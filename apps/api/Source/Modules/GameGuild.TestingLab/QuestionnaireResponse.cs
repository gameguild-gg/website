using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.TestingLab;

public sealed record QuestionnaireAnswer(
    string QuestionId,
    string? TextValue = null,
    IReadOnlyList<string>? SelectedOptionIds = null);

public sealed record QuestionnaireResponse(IReadOnlyList<QuestionnaireAnswer> Answers)
{
    internal static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);

    public static QuestionnaireResponse FromJson(string json) =>
        JsonSerializer.Deserialize<QuestionnaireResponse>(json, SerializerOptions)
        ?? throw new ArgumentException("Questionnaire response is invalid.", nameof(json));
}
public static class QuestionnaireResponseValidator
{
    public static IReadOnlyList<string> Validate(QuestionnaireSchema schema, QuestionnaireResponse response)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(response);
        var errors = schema.Validate().ToList();
        if (errors.Count > 0)
        {
            return errors;
        }

        var answers = (response.Answers ?? [])
            .Where(answer => !string.IsNullOrWhiteSpace(answer.QuestionId))
            .GroupBy(answer => answer.QuestionId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);
        var knownIds = schema.Questions.Select(question => question.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var unknown in answers.Keys.Where(id => !knownIds.Contains(id)))
        {
            errors.Add($"Answer references unknown question '{unknown}'.");
        }

        foreach (var question in schema.Questions)
        {
            if (!IsActive(question, answers))
            {
                continue;
            }

            answers.TryGetValue(question.Id, out var answer);
            var hasText = !string.IsNullOrWhiteSpace(answer?.TextValue);
            var selected = answer?.SelectedOptionIds?.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().ToArray() ?? [];
            if (question.Required && !hasText && selected.Length == 0)
            {
                errors.Add($"Question '{question.Id}' is required.");
            }

            if (question.Type == QuestionnaireQuestionType.FreeText)
            {
                if (selected.Length > 0)
                {
                    errors.Add($"Question '{question.Id}' accepts text only.");
                }

                continue;
            }
            if (hasText)
            {
                errors.Add($"Question '{question.Id}' accepts option identifiers only.");
            }

            if (question.Type == QuestionnaireQuestionType.SingleChoice && selected.Length > 1)
            {
                errors.Add($"Question '{question.Id}' accepts only one option.");
            }
            // Schema validation above guarantees options for every choice question.
            var allowed = question.Options!.Select(option => option.Id).ToHashSet(StringComparer.Ordinal);
            if (selected.Any(value => !allowed.Contains(value)))
            {
                errors.Add($"Question '{question.Id}' contains a value that is not an allowed option.");
            }
        }
        return errors;
    }

    public static void EnsureValid(QuestionnaireSchema schema, QuestionnaireResponse response)
    {
        var errors = Validate(schema, response);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    private static bool IsActive(
        QuestionnaireQuestion question,
        IReadOnlyDictionary<string, QuestionnaireAnswer> answers)
    {
        if (question.Condition == null)
        {
            return true;
        }

        if (!answers.TryGetValue(question.Condition.QuestionId, out var source))
        {
            return false;
        }

        var values = source.SelectedOptionIds?.ToArray() ??
            (string.IsNullOrWhiteSpace(source.TextValue) ? [] : [source.TextValue]);
        var contains = values.Contains(question.Condition.Value, StringComparer.Ordinal);
        return question.Condition.Operator switch
        {
            QuestionnaireConditionOperator.Equals => values.Length == 1 && contains,
            QuestionnaireConditionOperator.NotEquals => values.Length > 0 && !contains,
            QuestionnaireConditionOperator.Includes => contains,
            _ => false
        };
    }
}
