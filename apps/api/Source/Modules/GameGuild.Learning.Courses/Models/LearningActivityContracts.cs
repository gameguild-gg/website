using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameGuild.Learning.Courses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(DiscussionActivitySettings), "discussion")]
[JsonDerivedType(typeof(ReflectionActivitySettings), "reflection")]
[JsonDerivedType(typeof(SurveyActivitySettings), "survey")]
public abstract record ActivitySettings;

public sealed record DiscussionActivitySettings(
    bool AllowReplies = true,
    bool RequireThreadRoot = false,
    int MinimumBodyLength = 1,
    int MaximumBodyLength = 10_000) : ActivitySettings;

public sealed record ReflectionActivitySettings(
    bool PrivateToInstructors = true,
    int MinimumBodyLength = 1,
    int MaximumBodyLength = 10_000) : ActivitySettings;

public enum SurveyResultsVisibility
{
    AfterSubmission = 0,
    AfterClose = 1,
    Never = 2,
}

public sealed record SurveyActivitySettings(
    bool IsAnonymous = false,
    bool AllowMultipleResponses = false,
    SurveyResultsVisibility ResultsVisibility = SurveyResultsVisibility.AfterSubmission) : ActivitySettings;

public abstract record ActivityResponse(string Kind);

public sealed record DiscussionActivityResponse(string Body, Guid? ThreadRootId = null)
    : ActivityResponse("discussion");

public sealed record ReflectionActivityResponse(string Body)
    : ActivityResponse("reflection");

public sealed record SurveyActivityResponse(IReadOnlyDictionary<string, JsonElement> Answers)
    : ActivityResponse("survey");

public static class LearningActivityContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool IsActivityType(ProgramContentType type) =>
        type is ProgramContentType.Discussion or ProgramContentType.Reflection or ProgramContentType.Survey;

    public static bool RequiresSurveyPolicyLock(ProgramContentType type) => type == ProgramContentType.Survey;

    public static bool RequiresSubmissionPolicyLock(ProgramContentType type) =>
        type is ProgramContentType.Survey or ProgramContentType.Discussion;

    public static async Task ValidateDiscussionThreadRootAsync(
        IApplicationDbContext context,
        Guid programId,
        Guid contentId,
        ActivityResponse response)
    {
        if (response is not DiscussionActivityResponse { ThreadRootId: { } rootId })
        {
            return;
        }

        var rootPayload = await context.Set<ContentInteraction>()
            .AsNoTracking()
            .Where(interaction =>
                interaction.Id == rootId &&
                interaction.ContentId == contentId &&
                interaction.SubmittedAt != null &&
                interaction.DeletedAt == null)
            .Join(
                context.Set<ProgramContent>().AsNoTracking(),
                interaction => interaction.ContentId,
                content => content.Id,
                (interaction, content) => new { interaction.SubmissionData, content.ProgramId, content.Type })
            .Where(root => root.ProgramId == programId && root.Type == ProgramContentType.Discussion)
            .Select(root => root.SubmissionData)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (rootPayload is null ||
            ActivityResponseContract.Parse(ProgramContentType.Discussion, rootPayload, null) is not DiscussionActivityResponse { ThreadRootId: null })
        {
            throw new InvalidOperationException("Discussion thread root is invalid.");
        }
    }

    public static ActivitySettings? GetSettings(ProgramContentType type, string? serializedSettings)
    {
        if (!IsActivityType(type))
        {
            return null;
        }

        var settings = string.IsNullOrWhiteSpace(serializedSettings)
            ? CreateDefaultSettings(type)
            : JsonSerializer.Deserialize<ActivitySettings>(serializedSettings, JsonOptions)
                ?? throw new InvalidOperationException("Activity settings are invalid.");
        ValidateSettings(type, settings);
        return settings;
    }

    public static string SerializeSettings(ProgramContentType type, ActivitySettings settings)
    {
        ValidateSettings(type, settings);
        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    public static ActivitySettings CreateDefaultSettings(ProgramContentType type) => type switch
    {
        ProgramContentType.Discussion => new DiscussionActivitySettings(),
        ProgramContentType.Reflection => new ReflectionActivitySettings(),
        ProgramContentType.Survey => new SurveyActivitySettings(),
        _ => throw new InvalidOperationException($"{type} does not support activity settings."),
    };

    public static void ValidateSettings(ProgramContentType type, ActivitySettings settings)
    {
        var matches = (type, settings) switch
        {
            (ProgramContentType.Discussion, DiscussionActivitySettings discussion) => ValidateDiscussion(discussion),
            (ProgramContentType.Reflection, ReflectionActivitySettings reflection) => ValidateReflection(reflection),
            (ProgramContentType.Survey, SurveyActivitySettings survey) => ValidateSurvey(survey),
            _ => false,
        };

        if (!matches)
        {
            throw new InvalidOperationException("Activity settings do not match the content type.");
        }
    }

    public static bool AllowsMultipleResponses(ProgramContent content)
    {
        if (content.Type != ProgramContentType.Survey)
        {
            return false;
        }

        return ((SurveyActivitySettings)content.GetActivitySettings()!).AllowMultipleResponses;
    }

    public static bool IsAnonymousSurvey(ProgramContent content)
    {
        if (content.Type != ProgramContentType.Survey)
        {
            return false;
        }

        return ((SurveyActivitySettings)content.GetActivitySettings()!).IsAnonymous;
    }

    private static bool ValidateDiscussion(DiscussionActivitySettings settings)
    {
        ValidateBodyPolicy(settings.MinimumBodyLength, settings.MaximumBodyLength);
        if (settings.RequireThreadRoot && !settings.AllowReplies)
        {
            throw new InvalidOperationException("Discussion thread roots require replies to be enabled.");
        }

        return true;
    }

    private static bool ValidateReflection(ReflectionActivitySettings settings)
    {
        ValidateBodyPolicy(settings.MinimumBodyLength, settings.MaximumBodyLength);
        return true;
    }

    private static bool ValidateSurvey(SurveyActivitySettings settings)
    {
        if (!Enum.IsDefined(settings.ResultsVisibility))
        {
            throw new InvalidOperationException("Survey results visibility is not supported.");
        }

        return true;
    }

    internal static void ValidateBodyPolicy(int minimumBodyLength, int maximumBodyLength)
    {
        if (minimumBodyLength < 1 || maximumBodyLength < minimumBodyLength)
        {
            throw new InvalidOperationException("Activity body length policy is invalid.");
        }
    }
}

public static class ActivityResponseContract
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ActivityResponse Parse(ProgramContentType contentType, string payload, ActivitySettings? settings)
    {
        if (!LearningActivityContract.IsActivityType(contentType))
        {
            throw new InvalidOperationException("Content does not accept a typed activity response.");
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("kind", out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException("Activity response must include a kind.");
            }

            var kind = kindElement.GetString();
            return contentType switch
            {
                ProgramContentType.Discussion when kind == "discussion" => ParseDiscussion(root, settings as DiscussionActivitySettings),
                ProgramContentType.Reflection when kind == "reflection" => ParseReflection(root, settings as ReflectionActivitySettings),
                ProgramContentType.Survey when kind == "survey" => ParseSurvey(root),
                _ => throw new InvalidOperationException("Activity response kind does not match the content type."),
            };
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException("Activity response payload is malformed.", exception);
        }
    }

    public static string Serialize(ActivityResponse response) => JsonSerializer.Serialize(response, response.GetType(), JsonOptions);

    private static DiscussionActivityResponse ParseDiscussion(JsonElement root, DiscussionActivitySettings? settings)
    {
        var resolvedSettings = settings ?? new DiscussionActivitySettings();
        LearningActivityContract.ValidateSettings(ProgramContentType.Discussion, resolvedSettings);
        var body = GetBody(root, resolvedSettings.MinimumBodyLength, resolvedSettings.MaximumBodyLength);
        var threadRootId = GetOptionalGuid(root, "threadRootId");
        if (threadRootId.HasValue && !resolvedSettings.AllowReplies)
        {
            throw new InvalidOperationException("Discussion replies are disabled.");
        }
        if (resolvedSettings.RequireThreadRoot && !threadRootId.HasValue)
        {
            throw new InvalidOperationException("Discussion responses require a thread root.");
        }

        return new DiscussionActivityResponse(body, threadRootId);
    }

    private static ReflectionActivityResponse ParseReflection(JsonElement root, ReflectionActivitySettings? settings)
    {
        var resolvedSettings = settings ?? new ReflectionActivitySettings();
        LearningActivityContract.ValidateSettings(ProgramContentType.Reflection, resolvedSettings);
        return new ReflectionActivityResponse(GetBody(root, resolvedSettings.MinimumBodyLength, resolvedSettings.MaximumBodyLength));
    }

    private static SurveyActivityResponse ParseSurvey(JsonElement root)
    {
        if (!root.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Survey response must include an answers object.");
        }

        var values = answers.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.Ordinal);
        if (values.Count == 0)
        {
            throw new InvalidOperationException("Survey response must include at least one answer.");
        }

        return new SurveyActivityResponse(values);
    }

    private static string GetBody(JsonElement root, int minimumLength, int maximumLength)
    {
        if (!root.TryGetProperty("body", out var bodyElement) || bodyElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Activity response must include a body.");
        }

        var body = bodyElement.GetString()!.Trim();
        if (string.IsNullOrEmpty(body) || body.Length < minimumLength || body.Length > maximumLength)
        {
            throw new InvalidOperationException("Activity response body does not satisfy the configured length policy.");
        }

        return body;
    }

    private static Guid? GetOptionalGuid(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String || !Guid.TryParse(element.GetString(), out var value))
        {
            throw new InvalidOperationException($"Activity response {propertyName} must be a GUID.");
        }

        return value;
    }
}
