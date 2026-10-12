using FluentAssertions;
using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;
using GameGuild.Learning.Assessments.Grading.Code;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;

public sealed class CodeGradingAdapterContractTests
{
    [Fact]
    public void Module_ResolvesVersionedCodeAdapterForOfficialSubmissions()
    {
        var services = new ServiceCollection();
        services.AddAssessmentsModule();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IAssessmentTypeAdapterResolver>();

        var adapter = resolver.ResolveForAuthoring(ProgramContentType.Code);

        adapter.ContentType.Should().Be("coding-assignment");
        adapter.SubmissionModalities.Should().Be(SubmissionModality.Code);
        resolver.Resolve(adapter.ContentType, adapter.Key, adapter.Version,
            ReviewExecutionContext.OfficialSubmission).Should().BeSameAs(adapter);
    }

    [Fact]
    public void Delivery_RedactsPrivateTestsAndFilesFromFrozenProjection()
    {
        var adapter = Adapter();
        var source = Definition();
        var projection = adapter.ProjectAuthoring(source);
        var delivery = adapter.GenerateDelivery(projection.Items.Single().PrivateProjection);

        projection.Content.GetRawText().Should().Contain("secret-result");
        projection.Items.Single().MaxScore.Should().Be(ScoreValue.FromUnits(10000));
        delivery.GetRawText().Should().NotContain("secret-result").And.NotContain("hidden.h");
        delivery.GetProperty("definition").GetProperty("Tests").GetProperty("Private").GetArrayLength().Should().Be(0);
        delivery.GetProperty("definition").GetProperty("Data").GetProperty("Files")
            .GetProperty("main.cpp").GetProperty("Content").GetString().Should().Be("int main(){return 0;}");
    }

    [Fact]
    public void Projection_FreezesTheExecutableToolchainWithoutExposingItAsLearnerContent()
    {
        var adapter = Adapter();
        var projection = adapter.ProjectAuthoring(Definition()).Items.Single().PrivateProjection;
        JsonSerializer.Deserialize<CodeToolchainIdentity>(projection.GetProperty("toolchain"), GradingJson.Options)
            .Should().Be(CodeToolchainIdentity.Version1);
        adapter.GenerateDelivery(projection).TryGetProperty("toolchain", out _).Should().BeFalse();
    }

    [Fact]
    public async Task WorkerOutput_IsBoundedInUtf8BytesAndCancelsTheOwnedRequestImmediately()
    {
        using var deadline = new CancellationTokenSource();
        using var output = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(new string('é', 11)));
        Func<Task> read = async () => await CodeGradingWorker.ReadBoundedAsync(output, 20, deadline);
        await read.Should().ThrowAsync<InvalidOperationException>().WithMessage("*byte budget*");
        deadline.IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task WorkerOutput_ReturnsValidUtf8WithinItsBudget()
    {
        using var deadline = new CancellationTokenSource();
        using var output = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("frozen éxecution"));
        (await CodeGradingWorker.ReadBoundedAsync(output, 30, deadline)).Should().Be("frozen éxecution");
        deadline.IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void Authoring_RejectsNullFilesAndNullTestsAsInvalidContent()
    {
        var invalidFile = System.Text.Json.Nodes.JsonNode.Parse(Definition().GetRawText())!;
        invalidFile["Data"]!["Files"]!["main.cpp"] = null;
        Action file = () => Adapter().ProjectAuthoring(JsonSerializer.SerializeToElement(invalidFile));
        file.Should().Throw<JsonException>();
        var invalidTest = System.Text.Json.Nodes.JsonNode.Parse(Definition().GetRawText())!;
        invalidTest["Tests"]!["Public"]![0] = null;
        Action test = () => Adapter().ProjectAuthoring(JsonSerializer.SerializeToElement(invalidTest));
        test.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("../hidden.h")]
    [InlineData("/etc/passwd")]
    [InlineData("C:/secret")]
    [InlineData("/home/user/../hidden.h")]
    [InlineData("main\\other.cpp")]
    [InlineData("functional_0_test.cpp")]
    public void Decoder_RejectsUnsafeOrHarnessPaths(string path)
    {
        Action action = () => Decode(new Dictionary<string, object> { [path] = File("x") });
        action.Should().Throw<JsonException>();
    }

    [Theory]
    [InlineData("hidden.h")]
    [InlineData("/home/user/hidden.h")]
    [InlineData("/user/hidden.h")]
    [InlineData("readonly.h")]
    [InlineData("new.cpp")]
    public void Decoder_RejectsPrivateImmutableAndUnapprovedNewFiles(string path)
    {
        Action action = () => Decode(new Dictionary<string, object> { [path] = File("override") });
        action.Should().Throw<JsonException>();
    }

    [Fact]
    public void Decoder_AcceptsTextDeltaAndRejectsClientScoresAndAliasedFiles()
    {
        Decode(new Dictionary<string, object> { ["/user/main.cpp"] = File("return 5;") })
            .GetProperty("files").GetProperty("/user/main.cpp").GetProperty("content").GetString().Should().Be("return 5;");
        Action aliases = () => Decode(new Dictionary<string, object>
        {
            ["main.cpp"] = File("one"), ["/home/user/main.cpp"] = File("two"),
        });
        aliases.Should().Throw<JsonException>();
        var projection = Adapter().ProjectAuthoring(Definition()).Items.Select(item => item.PrivateProjection).ToArray();
        Action forged = () => Adapter().DecodeResponse(Envelope(new { files = new { }, score = 100 }), projection);
        forged.Should().Throw<JsonException>();
    }

    [Fact]
    public void Decoder_RejectsOversizedResponsesAndNonTextEncoding()
    {
        Action large = () => Decode(new Dictionary<string, object> { ["main.cpp"] = File(new string('x', 2_000_001)) });
        large.Should().Throw<JsonException>();
        Action binary = () => Decode(new Dictionary<string, object> { ["main.cpp"] = new { content = "WA==", encoding = "base64" } });
        binary.Should().Throw<JsonException>();
    }

    [Fact]
    public void Decoder_RejectsNonStringEncodingAsAnInvalidResponse()
    {
        Action numeric = () => Decode(new Dictionary<string, object> { ["main.cpp"] = new { content = "source", encoding = 1 } });
        numeric.Should().Throw<JsonException>();
    }

    [Fact]
    public async Task Review_UsesFrozenWeightsAndTrustedReceiptAndRedactsPrivateFeedback()
    {
        var executor = new RecordingExecutor([true, false]);
        var adapter = new CodeAssessmentTypeAdapter(executor);
        var projection = adapter.ProjectAuthoring(Definition()).Items.Single().PrivateProjection;
        var result = await adapter.EvaluateDeterministicAsync(new DeterministicReviewRequest([projection],
            new AssessmentExecutionDeliveryV1(1, Guid.NewGuid(), new string('a', 64), [],
                new Dictionary<string, AssessmentExecutionDeliveryItemV1>()),
            Decode(new Dictionary<string, object> { ["main.cpp"] = File("source") }),
            CodeAssessmentContracts.HandlerKey, "1"), CancellationToken.None);

        result.Score.Should().Be(ScoreValue.FromUnits(2500));
        result.MaxScore.Should().Be(ScoreValue.FromUnits(10000));
        result.Items.Should().ContainSingle().Which.EvidenceRefs.Should().ContainSingle();
        JsonSerializer.Serialize(result).Should().NotContain("secret-result");
        executor.CapturedDefinition!.Tests.Private.Single().Weight.Should().Be(3);
        executor.Files.GetProperty("main.cpp").GetProperty("content").GetString().Should().Be("source");
    }

    [Fact]
    public async Task Review_RejectsPartialReceiptsAndHonorsCancellation()
    {
        var projection = Adapter().ProjectAuthoring(Definition()).Items.Single().PrivateProjection;
        var request = new DeterministicReviewRequest([projection],
            new AssessmentExecutionDeliveryV1(1, Guid.NewGuid(), new string('a', 64), [],
                new Dictionary<string, AssessmentExecutionDeliveryItemV1>()),
            Decode(new Dictionary<string, object>()), CodeAssessmentContracts.HandlerKey, "1");
        Func<Task> partial = async () => await new CodeAssessmentTypeAdapter(new RecordingExecutor([true]))
            .EvaluateDeterministicAsync(request, CancellationToken.None);
        await partial.Should().ThrowAsync<InvalidOperationException>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Func<Task> cancelled = async () => await new CodeAssessmentTypeAdapter(new RecordingExecutor([true, true]))
            .EvaluateDeterministicAsync(request, cancellation.Token);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();
    }

    private static CodeAssessmentTypeAdapter Adapter() => new(new UnconfiguredCodeAssessmentExecutor());
    private static object File(string content) => new { content, encoding = "text" };
    private static AssessmentResponseEnvelopeV1 Envelope(object payload) =>
        new(1, CodeAssessmentContracts.ContentType, CodeAssessmentContracts.PayloadSchema, JsonSerializer.SerializeToElement(payload));
    private static JsonElement Decode(Dictionary<string, object> files)
    {
        var adapter = Adapter();
        return adapter.DecodeResponse(Envelope(new { files }),
            adapter.ProjectAuthoring(Definition()).Items.Select(item => item.PrivateProjection).ToArray());
    }
    private static JsonElement Definition() => JsonSerializer.SerializeToElement(new CodingAssignmentContent
    {
        Environment = new CodingEnvironment { Language = "cpp", Tools = "clang" },
        Data = new WorkspaceData { Files = new Dictionary<string, BundleFileMeta>
        {
            ["main.cpp"] = new() { Content = "int main(){return 0;}" },
            ["hidden.h"] = new() { Content = "secret-result", Visibility = "Private", Modifiable = false },
            ["readonly.h"] = new() { Content = "immutable", Modifiable = false },
        } },
        Tests = new TestSuite
        {
            Public = [new StandardTest { Stdout = "public-result", Weight = 1 }],
            Private = [new StandardTest { Stdout = "secret-result", Weight = 3 }],
        },
        Grading = new GradingConfig { MaxScore = 100 },
    });

    private sealed class RecordingExecutor(IReadOnlyList<bool> passed) : ICodeAssessmentExecutor
    {
        public CodingAssignmentContent? CapturedDefinition { get; private set; }
        public JsonElement Files { get; private set; }
        public Task<CodeExecutionReceipt> ExecuteAsync(CodingAssignmentContent definition, JsonElement files, CodeToolchainIdentity toolchain,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedDefinition = definition;
            toolchain.Should().Be(CodeToolchainIdentity.Version1);
            Files = files.Clone();
            return Task.FromResult(new CodeExecutionReceipt(passed, new string('a', 64)));
        }
    }
}
