using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Capabilities;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Grading.Contracts;

namespace GameGuild.Learning.Assessments.Grading.Code;
public static class CodeAssessmentContracts
{
    public static string ContentType { get; } = "coding-assignment";
    public static string AdapterKey { get; } = "code-assessment-type";
    public static string HandlerKey { get; } = "code-wasm-review";
    public static string Version { get; } = "1";
    public static string PayloadSchema { get; } = "code-files/v1";
    public static string ItemId { get; } = "code";
    public static int MaxResponseBytes { get; } = 2_000_000;
    public static int MaxFiles { get; } = 100;
    public static int MaxTests { get; } = 100;
    public static JsonSerializerOptions ContentJson { get; } = new()
    {
        AllowOutOfOrderMetadataProperties = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}

public sealed class CodeAssessmentTypeAdapter(ICodeAssessmentExecutor executor) : IAssessmentTypeAdapter
{
    public string Key => CodeAssessmentContracts.AdapterKey;
    public string Version => CodeAssessmentContracts.Version;
    public string ContentType => CodeAssessmentContracts.ContentType;
    public bool IsCurrentForAuthoring => true;
    public ProgramContentType ProgramContentType => ProgramContentType.Code;
    public AssessmentType AssessmentType => AssessmentType.Assignment;
    public SubmissionModality SubmissionModalities => SubmissionModality.Code;
    public IReadOnlySet<ReviewExecutionContext> Contexts { get; } = new HashSet<ReviewExecutionContext>
    {
        ReviewExecutionContext.AuthorTest,
        ReviewExecutionContext.OfficialSubmission,
    };
    public IReadOnlyDictionary<ReviewMethod, AssessmentReviewHandlerBinding> ReviewHandlers { get; } = new Dictionary<ReviewMethod, AssessmentReviewHandlerBinding>
    {
        [ReviewMethod.AutomatedReview] = new(CodeAssessmentContracts.HandlerKey, CodeAssessmentContracts.Version),
    };

    public AssessmentAuthoringProjectionV1 ProjectAuthoring(JsonElement authoringDocument)
    {
        if (Encoding.UTF8.GetByteCount(authoringDocument.GetRawText()) > 10_000_000)
        {
            throw new JsonException("Code definition exceeds its size budget.");
        }

        var definition = ReadDefinition(authoringDocument);
        var validation = new CodingAssignmentContentValidator().Validate(definition);
        if (!validation.IsValid)
        {
            throw new JsonException(string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));
        }

        if (definition.Data.Files.Count > CodeAssessmentContracts.MaxFiles)
        {
            throw new JsonException("Code definition has too many files.");
        }

        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var(path, file)in definition.Data.Files)
        {
            if (!paths.Add(CanonicalFilePath(path)))
            {
                throw new JsonException("Code definition has aliased file paths.");
            }

            if (file.Encoding is not ("text" or "base64"))
            {
                throw new JsonException("Code file encoding is unsupported.");
            }

            if (file.Encoding == "base64")
            {
                try
                {
                    _ = Convert.FromBase64String(file.Content);
                }
                catch (FormatException)
                {
                    throw new JsonException("Code file is not valid base64.");
                }
            }
        }

        var tests = definition.Tests.Public.Concat(definition.Tests.Private).ToArray();
        if (tests.Length == 0 || tests.Length > CodeAssessmentContracts.MaxTests || tests.Any(test => !double.IsFinite(test.Weight)) || tests.Sum(test => test.Weight) <= 0 || !double.IsFinite(tests.Sum(test => test.Weight)))
        {
            throw new JsonException("Code grading requires a bounded, nonempty test plan with positive finite total weight.");
        }

        var maxScore = ScoreValue.FromPoints(definition.Grading.MaxScore.ToString(CultureInfo.InvariantCulture));
        var content = JsonSerializer.SerializeToElement(definition, CodeAssessmentContracts.ContentJson);
        var projection = JsonSerializer.SerializeToElement(new { schemaVersion = 1, itemId = CodeAssessmentContracts.ItemId, itemType = "code", maxScore, source = new { contentType = ContentType, itemId = CodeAssessmentContracts.ItemId }, definition = content, toolchain = CodeToolchainIdentity.Version1, }, GradingJson.Options);
        return new AssessmentAuthoringProjectionV1(ContentType, content, new ContentGradingDefinitionV2(2, new Dictionary<string, GradingItemAuthoringV2> { [CodeAssessmentContracts.ItemId] = new(), }), [new AssessmentAuthoringItemProjectionV1(CodeAssessmentContracts.ItemId, "code", maxScore, projection, Key, Version)]);
    }

    public JsonElement GenerateDelivery(JsonElement projectedItem)
    {
        var definition = ReadDefinition(projectedItem.GetProperty("definition"));
        var publicDefinition = definition with
        {
            Data = definition.Data with
            {
                Files = definition.Data.Files.Where(pair => pair.Value.Visibility == "Public").ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            },
            Tests = definition.Tests with
            {
                Private = []
            },
        };
        return JsonSerializer.SerializeToElement(new { itemId = CodeAssessmentContracts.ItemId, definition = JsonSerializer.SerializeToElement(publicDefinition, CodeAssessmentContracts.ContentJson), });
    }

    public JsonElement DecodeResponse(AssessmentResponseEnvelopeV1 envelope, IReadOnlyList<JsonElement> projectedItems)
    {
        if (envelope.SchemaVersion != 1 || envelope.ContentType != ContentType || envelope.PayloadSchema != CodeAssessmentContracts.PayloadSchema || projectedItems.Count != 1)
        {
            throw new JsonException("Code response contract does not match its revision.");
        }

        var payload = envelope.Payload;
        RequireProperties(payload, "files");
        var files = payload.GetProperty("files");
        if (files.ValueKind != JsonValueKind.Object || files.EnumerateObject().Count() > CodeAssessmentContracts.MaxFiles || Encoding.UTF8.GetByteCount(payload.GetRawText()) > CodeAssessmentContracts.MaxResponseBytes)
        {
            throw new JsonException("Code response exceeds its file or byte budget.");
        }

        var definition = ReadDefinition(projectedItems[0].GetProperty("definition"));
        var authoredFiles = definition.Data.Files.ToDictionary(pair => CanonicalFilePath(pair.Key), pair => pair.Value, StringComparer.Ordinal);
        var paths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.EnumerateObject())
        {
            var path = CanonicalFilePath(file.Name);
            if (!paths.Add(path))
            {
                throw new JsonException("Code response has aliased file paths.");
            }

            RequireProperties(file.Value, "content", "encoding");
            if (file.Value.GetProperty("encoding").ValueKind != JsonValueKind.String || file.Value.GetProperty("encoding").GetString() != "text" || file.Value.GetProperty("content").ValueKind != JsonValueKind.String)
            {
                throw new JsonException("Code responses contain text files only.");
            }

            if (authoredFiles.TryGetValue(path, out var authored))
            {
                if (authored.Visibility != "Public" || !authored.Modifiable || authored.Encoding != "text")
                {
                    throw new JsonException("Code response cannot replace a private or immutable file.");
                }
            }
            else if (!definition.Environment.AllowStudentCreateFiles)
            {
                throw new JsonException("This code assessment does not allow student-created files.");
            }
        }

        return payload.Clone();
    }

    public bool CanEvaluateDeterministically(JsonElement projectedItem) => true;
    public async ValueTask<GradeResultV1> EvaluateDeterministicAsync(DeterministicReviewRequest request, CancellationToken cancellationToken)
    {
        if (request.HandlerKey != CodeAssessmentContracts.HandlerKey || request.HandlerVersion != Version || request.ProjectedItems.Count != 1)
        {
            throw new InvalidOperationException("Code review handler binding is invalid.");
        }

        var definition = ReadDefinition(request.ProjectedItems[0].GetProperty("definition"));
        var files = request.NormalizedResponse.GetProperty("files");
        var toolchain = JsonSerializer.Deserialize<CodeToolchainIdentity>(request.ProjectedItems[0].GetProperty("toolchain"), GradingJson.Options);
        if (toolchain != CodeToolchainIdentity.Version1)
        {
            throw new InvalidOperationException("Code review requires its frozen version 1 toolchain.");
        }

        var result = await executor.ExecuteAsync(definition, files, toolchain, cancellationToken).ConfigureAwait(false);
        var tests = definition.Tests.Public.Concat(definition.Tests.Private).ToArray();
        if (result.Passed.Count != tests.Length || result.ReceiptHash.Length != 64 || result.ReceiptHash.Any(character => !char.IsAsciiHexDigitLower(character)))
        {
            throw new InvalidOperationException("Code executor returned an incomplete test receipt.");
        }

        var totalWeight = tests.Sum(test => test.Weight);
        var passedWeight = tests.Where((_, index) => result.Passed[index]).Sum(test => test.Weight);
        var maxUnits = request.ProjectedItems[0].GetProperty("maxScore").GetInt32();
        var score = ScoreValue.FromUnits(checked((int)Math.Round(passedWeight / totalWeight * maxUnits, MidpointRounding.AwayFromZero)));
        var maxScore = ScoreValue.FromUnits(maxUnits);
        // Private names, expected values and execution diagnostics never enter learner feedback.
        var feedback = "Code tests completed by the trusted grading worker.";
        var evidence = $"code-wasm:{Version}:{result.ReceiptHash}";
        var item = new GradeItemResultV1(CodeAssessmentContracts.ItemId, GradeItemState.Graded, score, maxScore, [evidence], ReviewMethod.AutomatedReview, request.HandlerKey, request.HandlerVersion, feedback);
        return new GradeResultV1(1, "final", score, maxScore, [item], [evidence], feedback);
    }

    public static CodingAssignmentContent ReadDefinition(JsonElement element)
    {
        var definition = JsonSerializer.Deserialize<CodingAssignmentContent>(element, CodeAssessmentContracts.ContentJson) ?? throw new JsonException("Code definition is required.");
        if (definition.Environment is null || definition.Data?.Files is null || definition.Tests?.Public is null || definition.Tests.Private is null || definition.Grading is null || definition.Data.Files.Values.Any(file => file?.Content is null) || definition.Tests.Public.Concat(definition.Tests.Private).Any(test => test is null))
        {
            throw new JsonException("Code definition is incomplete.");
        }

        return definition;
    }

    public static string CanonicalFilePath(string path)
    {
        var relative = path.StartsWith("/home/user/", StringComparison.Ordinal) ? path[11..] : path.StartsWith("/user/", StringComparison.Ordinal) ? path[6..] : path;
        var segments = relative.Split('/');
        if (relative.Length is 0 or > 240 || relative.StartsWith('/') || relative.Contains('\\') || relative.Contains(':') || relative.Any(char.IsControl) || segments.Any(segment => segment is "" or "." or "..") || segments.Any(segment => segment.StartsWith("functional_", StringComparison.Ordinal) && segment.EndsWith("_test.cpp", StringComparison.Ordinal)))
        {
            throw new JsonException("Code workspace path is invalid or reserved.");
        }

        return "/home/user/" + relative;
    }

    private static void RequireProperties(JsonElement element, params string[] properties)
    {
        if (element.ValueKind != JsonValueKind.Object || element.EnumerateObject().Count() != properties.Length || !element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal).SetEquals(properties))
        {
            throw new JsonException("Code response has unknown, duplicate or missing properties.");
        }
    }
}

public sealed record CodeExecutionReceipt(IReadOnlyList<bool> Passed, string ReceiptHash);
/// <summary>
/// Immutable executable identity retained in version 1 snapshots. A future
/// toolchain requires another adapter version, leaving existing attempts bound
/// to these concrete artifacts rather than to whichever release is newest.
/// </summary>
public sealed record CodeToolchainIdentity(string ArtifactVersion, string RuntimeAbi, string ToolchainLockHash)
{
    public static CodeToolchainIdentity Version1 { get; } = new("4.4.0", "emception-browser-v1", "eb7e07aacb067ae32910fbebcb3762902da756b1a7ce64746f14f77a25057720");
}

public interface ICodeAssessmentExecutor
{
    Task<CodeExecutionReceipt> ExecuteAsync(CodingAssignmentContent definition, JsonElement files, CodeToolchainIdentity toolchain, CancellationToken cancellationToken);
}

public sealed class UnconfiguredCodeAssessmentExecutor : ICodeAssessmentExecutor
{
    public Task<CodeExecutionReceipt> ExecuteAsync(CodingAssignmentContent definition, JsonElement files, CodeToolchainIdentity toolchain, CancellationToken cancellationToken) => throw new InvalidOperationException("The trusted Code grading worker is not configured.");
}

public sealed class CodeAutomatedReviewStageHandler(CodeAssessmentTypeAdapter adapter) : IReviewStageHandler
{
    public ReviewMethod Method => ReviewMethod.AutomatedReview;
    public string Key => CodeAssessmentContracts.HandlerKey;
    public string Version => CodeAssessmentContracts.Version;
    public string? ProviderKey => null;
    public string? ProviderPolicyVersion => null;
    public IReadOnlySet<ReviewExecutionContext> Contexts => adapter.Contexts;

    public ValueTask<GradeResultV1> ExecuteAsync(ReviewStageRequest request, CancellationToken cancellationToken)
    {
        GradingContractValidator.ValidateBindings(request.ExecutionSnapshotHash, request.Snapshot, request.Delivery, request.Response);
        if (request.Snapshot.Manifest.Items.Any(item => item.AdapterKey != adapter.Key || item.AdapterVersion != Version))
        {
            throw new InvalidOperationException("Code review received another adapter's item.");
        }

        var projections = request.Snapshot.Manifest.Items.Select(item => request.Snapshot.ItemProjections[item.ItemId]).ToArray();
        var response = adapter.DecodeResponse(request.Response, projections);
        return adapter.EvaluateDeterministicAsync(new DeterministicReviewRequest(projections, request.Delivery, response, Key, Version), cancellationToken);
    }
}

public sealed class CodeCapabilityRegistration : IReviewCapabilityRegistration
{
    public void Register(IReviewCapabilityRegistry registry)
    {
        IReadOnlySet<ReviewExecutionContext> contexts = new HashSet<ReviewExecutionContext>
        {
            ReviewExecutionContext.AuthorTest,
            ReviewExecutionContext.OfficialSubmission,
        };
        registry.Register(new ExecutableComponentDescriptor(ExecutableComponentKind.AssessmentTypeAdapter, CodeAssessmentContracts.AdapterKey, CodeAssessmentContracts.Version, contexts));
        registry.Register(new ReviewCapabilityDescriptor(ReviewMethod.AutomatedReview, CodeAssessmentContracts.HandlerKey, CodeAssessmentContracts.Version, contexts));
    }
}
