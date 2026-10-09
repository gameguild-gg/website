using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Learning.Courses;

public sealed class CodingAssignmentContentService(
    IApplicationDbContext context,
    IValidator<CodingAssignmentContent> validator,
    IAssessmentGradingSync assessmentGradingSync) : ICodingAssignmentContentService
{
    private static readonly JsonSerializerOptions s_jsonOptions = new();

    public async Task<CodingAssignmentContent?> GetPublicAsync(
        Guid programId,
        Guid contentId,
        Guid userId,
        CancellationToken ct = default)
    {
        var content = await GetContentAsync(programId, contentId, ct).ConfigureAwait(false);
        if (content == null)
        {
            return null;
        }

        var parsed = Parse(content.JsonBody);
        if (parsed == null)
        {
            return null;
        }

        return StripPrivate(parsed);
    }

    public async Task<CodingAssignmentContent?> GetFullAsync(
        Guid programId,
        Guid contentId,
        CancellationToken ct = default)
    {
        var content = await GetContentAsync(programId, contentId, ct).ConfigureAwait(false);
        return content == null ? null : Parse(content.JsonBody);
    }

    public async Task<Result<CodingAssignmentContent>> UpsertAsync(
        Guid programId,
        Guid contentId,
        CodingAssignmentContent body,
        Guid actorUserId,
        CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(body, ct).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            var code = validation.Errors[0].ErrorCode;
            var message = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<CodingAssignmentContent>(
                Error.Validation(string.IsNullOrEmpty(code) ? "CodingAssignmentContent.Invalid" : code, message));
        }

        var content = await GetContentAsync(programId, contentId, ct).ConfigureAwait(false);
        if (content == null)
        {
            return Result.Failure<CodingAssignmentContent>(
                Error.NotFound("ProgramContent", "Program content not found for this program."));
        }

        content.JsonBody = JsonSerializer.Serialize(body, s_jsonOptions);
        content.Touch();
        context.Set<ProgramContent>().Update(content);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        await assessmentGradingSync.SyncAsync(contentId, body.Grading.MaxScore, ct).ConfigureAwait(false);

        return Result.Success(body);
    }

    private async Task<ProgramContent?> GetContentAsync(Guid programId, Guid contentId, CancellationToken ct)
    {
        return await context.Set<ProgramContent>()
            .AsTracking()
            .FirstOrDefaultAsync(pc => pc.Id == contentId
                                        && pc.ProgramId == programId
                                        && pc.DeletedAt == null, ct)
            .ConfigureAwait(false);
    }

    private static CodingAssignmentContent? Parse(string? jsonBody)
    {
        if (string.IsNullOrWhiteSpace(jsonBody))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CodingAssignmentContent>(
                NormalizeTestDiscriminatorOrder(jsonBody), s_jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Postgres jsonb reorders object keys (by length, then bytes), which can move the polymorphic
    /// <c>kind</c> discriminator behind other properties. System.Text.Json requires the discriminator
    /// to precede derived-type properties, so hoist <c>kind</c> back to the front of every test object
    /// before deserializing. The write path is unaffected.
    /// </summary>
    internal static string NormalizeTestDiscriminatorOrder(string jsonBody)
    {
        using var doc = JsonDocument.Parse(jsonBody);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("Tests", out var tests)
            || tests.ValueKind != JsonValueKind.Object
            || !TestsNeedNormalization(tests))
        {
            return jsonBody;
        }

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name != "Tests")
                {
                    prop.WriteTo(writer);
                    continue;
                }

                writer.WritePropertyName("Tests");
                writer.WriteStartObject();
                foreach (var suite in tests.EnumerateObject())
                {
                    writer.WritePropertyName(suite.Name);
                    if (suite.Value.ValueKind != JsonValueKind.Array)
                    {
                        suite.Value.WriteTo(writer);
                        continue;
                    }

                    writer.WriteStartArray();
                    foreach (var test in suite.Value.EnumerateArray())
                    {
                        WriteKindFirst(writer, test);
                    }
                    writer.WriteEndArray();
                }
                writer.WriteEndObject();
            }
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static bool TestsNeedNormalization(JsonElement tests)
    {
        foreach (var suite in tests.EnumerateObject())
        {
            if (suite.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var test in suite.Value.EnumerateArray())
            {
                if (test.ValueKind == JsonValueKind.Object
                    && test.TryGetProperty("kind", out _)
                    && test.EnumerateObject().First().Name != "kind")
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static void WriteKindFirst(Utf8JsonWriter writer, JsonElement test)
    {
        if (test.ValueKind != JsonValueKind.Object
            || !test.TryGetProperty("kind", out var kind)
            || test.EnumerateObject().First().Name == "kind")
        {
            test.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        writer.WritePropertyName("kind");
        kind.WriteTo(writer);
        foreach (var prop in test.EnumerateObject())
        {
            if (prop.Name != "kind")
            {
                prop.WriteTo(writer);
            }
        }
        writer.WriteEndObject();
    }

    private static CodingAssignmentContent StripPrivate(CodingAssignmentContent source)
    {
        var publicFiles = source.Data.Files
            .Where(kv => kv.Value.Visibility == "Public")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        return source with
        {
            Data = source.Data with { Files = publicFiles },
            Tests = source.Tests with { Private = new List<Test>() }
        };
    }
}
