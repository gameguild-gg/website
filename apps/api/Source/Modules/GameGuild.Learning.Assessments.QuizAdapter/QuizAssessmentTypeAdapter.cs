using System.Text.Json;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Courses;

namespace GameGuild.Learning.Assessments.QuizAdapter;

public sealed class QuizAssessmentTypeAdapter(
    QuizAuthoringAdapter authoring,
    QuizDeliveryGenerator delivery,
    QuizAnswerDecoder decoder,
    QuizDeterministicReviewAlgorithm deterministicReview) : IAssessmentTypeAdapter
{
    private static readonly IReadOnlySet<ReviewExecutionContext> SupportedContexts =
        new HashSet<ReviewExecutionContext> { ReviewExecutionContext.AuthorTest };

    public string Key => QuizAdapterContracts.AdapterKey;
    public string Version => QuizAdapterContracts.Version;
    public string ContentType => QuizAdapterContracts.ContentType;
    public bool IsCurrentForAuthoring => true;
    public ProgramContentType ProgramContentType => ProgramContentType.Questionnaire;
    public AssessmentType AssessmentType => AssessmentType.Quiz;
    public SubmissionModality SubmissionModalities => SubmissionModality.StructuredAnswer;
    public IReadOnlySet<ReviewExecutionContext> Contexts => SupportedContexts;

    public AssessmentAuthoringProjectionV1 ProjectAuthoring(JsonElement authoringDocument) =>
        authoring.Project(authoringDocument);

    public JsonElement GenerateDelivery(JsonElement projectedItem) => delivery.Generate(projectedItem);

    public JsonElement DecodeResponse(AssessmentResponseEnvelopeV1 envelope) => decoder.Decode(envelope);

    public ValueTask<GradeResultV1> EvaluateDeterministicAsync(
        DeterministicReviewRequest request,
        CancellationToken cancellationToken) =>
        deterministicReview.EvaluateAsync(request, cancellationToken);
}
