using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.TestingLab;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionnaireQuestionType
{
    FreeText,
    SingleChoice,
    MultipleChoice
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuestionnaireConditionOperator
{
    Equals,
    NotEquals,
    Includes
}

public sealed record QuestionnaireOption(string Id, string Label);

public sealed record QuestionnaireCondition(
    string QuestionId,
    QuestionnaireConditionOperator Operator,
    string Value);

public sealed record QuestionnaireQuestion(
    string Id,
    string Prompt,
    QuestionnaireQuestionType Type,
    bool Required,
    IReadOnlyList<QuestionnaireOption>? Options = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    QuestionnaireCondition? Condition = null);

public sealed record QuestionnaireSchema(
    string Title,
    IReadOnlyList<QuestionnaireQuestion> Questions)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Questions == null)
        {
            errors.Add("Questions are required.");
            return errors;
        }
        if (Questions.Count > 100)
        {
            errors.Add("A questionnaire cannot contain more than 100 questions.");
        }

        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var question in Questions)
        {
            if (string.IsNullOrWhiteSpace(question.Id))
            {
                errors.Add("Every question requires a stable identifier.");
            }
            else if (!identifiers.Add(question.Id.Trim()))
            {
                errors.Add($"Question identifier '{question.Id}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(question.Prompt))
            {
                errors.Add($"Question '{question.Id}' requires a prompt.");
            }

            var options = question.Options ?? [];
            if (question.Type is QuestionnaireQuestionType.SingleChoice or QuestionnaireQuestionType.MultipleChoice)
            {
                if (options.Count < 2)
                {
                    errors.Add($"Question '{question.Id}' requires at least two options.");
                }

                var optionIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in options)
                {
                    if (string.IsNullOrWhiteSpace(option.Id) || string.IsNullOrWhiteSpace(option.Label))
                    {
                        errors.Add($"Question '{question.Id}' has an invalid option.");
                    }
                    else if (!optionIds.Add(option.Id.Trim()))
                    {
                        errors.Add($"Question '{question.Id}' has a duplicated option identifier '{option.Id}'.");
                    }
                }
            }
            else if (options.Count > 0)
            {
                errors.Add($"Free-text question '{question.Id}' cannot define options.");
            }

            if (question.Condition is { } condition)
            {
                if (!identifiers.Contains(condition.QuestionId))
                {
                    errors.Add($"Question '{question.Id}' condition must reference an earlier question.");
                }

                if (string.IsNullOrWhiteSpace(condition.Value))
                {
                    errors.Add($"Question '{question.Id}' condition requires a value.");
                }
            }
        }
        return errors;
    }

    public void EnsureValid()
    {
        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join(" ", errors));
        }
    }

    public string ToJson(bool ensureValid = true)
    {
        if (ensureValid)
        {
            EnsureValid();
        }

        return JsonSerializer.Serialize(this, SerializerOptions);
    }

    public static QuestionnaireSchema FromJson(string json, bool ensureValid = true)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Questionnaire schema is required.", nameof(json));
        }

        try
        {
            var schema = JsonSerializer.Deserialize<QuestionnaireSchema>(json, SerializerOptions)
                ?? throw new ArgumentException("Questionnaire schema is invalid.", nameof(json));
            if (ensureValid)
            {
                schema.EnsureValid();
            }

            return schema;
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Questionnaire schema is invalid.", nameof(json), exception);
        }
    }
}
