using System.Text.Json;
using FluentAssertions;
using GameGuild.Learning.Assessments.Grading.Abstractions;
using GameGuild.Learning.Assessments.Grading.Code;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Assessments.Grading.Runtime;
using GameGuild.Learning.Grading.Contracts;
using GameGuild.Learning.Courses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Learning.Assessments.Tests;
public sealed class CodeRubricSnapshotTests
{
    [Fact]
    public async Task ContentDeletion_SoftDeletesOnlyTheLinkedActiveCodeAssessmentWithoutCommittingEarly()
    {
        await using var context = Context();
        var content = new ProgramContent
        {
            Id = Guid.NewGuid(),
            Type = ProgramContentType.Code
        };
        var linked = Assessment.Create(Guid.NewGuid(), "Linked", AssessmentType.Assignment, ScoreValue.FromUnits(100), false, contentId: content.Id);
        var other = Assessment.Create(Guid.NewGuid(), "Other", AssessmentType.Assignment, ScoreValue.FromUnits(100), false, contentId: Guid.NewGuid());
        // The production persistence interceptor advances entity versions on
        // insertion. This lightweight InMemory context has no interceptor.
        linked.Version = 1;
        other.Version = 1;
        context.AddRange(linked, other);
        await context.SaveChangesAsync();
        var participant = new CodeProgramContentDeleteParticipant(context);
        participant.CanHandle(content).Should().BeTrue();
        await participant.PrepareDeleteAsync(content);
        linked.DeletedAt.Should().NotBeNull();
        other.DeletedAt.Should().BeNull();
        context.Entry(linked).State.Should().Be(EntityState.Modified);
        (await context.Set<Assessment>().AsNoTracking().SingleAsync(value => value.Id == linked.Id)).DeletedAt.Should().BeNull();
        await context.SaveChangesAsync();
        (await context.Set<Assessment>().AsNoTracking().SingleAsync(value => value.Id == linked.Id)).DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Materialization_IncludesConcreteRubricBytesInBothFrozenHashes()
    {
        await using var context = Context();
        var rubric = AssessmentRubric.Create("Original rubric");
        var criterion = RubricCriterion.Create(rubric.Id, "Correctness", ScoreValue.FromUnits(10000), 0);
        context.AddRange(rubric, criterion);
        await context.SaveChangesAsync();
        var assessment = Assessment.Create(Guid.NewGuid(), "Code", AssessmentType.Assignment, ScoreValue.FromUnits(10000), false);
        assessment.AssignRubric(rubric.Id);
        var projection = Projection();
        var first = await CodeRubricSnapshot.MaterializeAsync(context, assessment, projection, CancellationToken.None);
        var oldSource = CanonicalJson.Serialize(first.Content);
        var oldPrivate = CanonicalJson.Serialize(first.Items.Single().PrivateProjection);
        rubric.Replace("Revised rubric");
        await context.SaveChangesAsync();
        var next = await CodeRubricSnapshot.MaterializeAsync(context, assessment, projection, CancellationToken.None);
        CanonicalJson.Serialize(next.Content).Should().NotBe(oldSource);
        CanonicalJson.Serialize(next.Items.Single().PrivateProjection).Should().NotBe(oldPrivate);
        first.Items.Single().PrivateProjection.GetProperty("rubric").GetProperty("title").GetString().Should().Be("Original rubric");
        first.Content.GetRawText().Should().Contain("Original rubric").And.NotContain("Revised rubric");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("over-max")]
    [InlineData("wrong-total")]
    [InlineData("wrong-item")]
    public async Task Resolution_RejectsIncompleteForgedOrInconsistentFrozenRubricScores(string mutation)
    {
        var(snapshot, criterionId) = await SnapshotAsync();
        var scores = new Dictionary<Guid, ScoreValue>
        {
            [criterionId] = ScoreValue.FromUnits(7500)
        };
        var itemId = "code";
        var total = 7500;
        if (mutation == "missing")
        {
            scores.Clear();
        }

        if (mutation == "unknown")
        {
            scores = new()
            {
                [Guid.NewGuid()] = ScoreValue.FromUnits(7500)
            };
        }

        if (mutation == "over-max")
        {
            scores[criterionId] = ScoreValue.FromUnits(10001);
        }

        if (mutation == "wrong-total")
        {
            total = 5000;
        }

        if (mutation == "wrong-item")
        {
            itemId = "other";
        }

        Action resolve = () => CodeRubricSnapshot.ValidateResolution(snapshot, new InstructorReviewResolutionV1(1, [new(itemId, ScoreValue.FromUnits(total))], RubricScores: scores));
        resolve.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task Resolution_PersistsOnlyValidatedScoresForTheFrozenCriterionIds()
    {
        var(snapshot, id) = await SnapshotAsync();
        var result = CodeRubricSnapshot.ValidateResolution(snapshot, new InstructorReviewResolutionV1(1, [new("code", ScoreValue.FromUnits(7500))], RubricScores: new Dictionary<Guid, ScoreValue> { [id] = ScoreValue.FromUnits(7500) }));
        using var receipt = JsonDocument.Parse(result!);
        receipt.RootElement.EnumerateObject().Should().ContainSingle();
        receipt.RootElement.GetProperty(id.ToString()).GetInt32().Should().Be(7500);
    }

    private static async Task<(AssessmentExecutionSnapshotV1, Guid)> SnapshotAsync()
    {
        await using var context = Context();
        var rubric = AssessmentRubric.Create("Frozen rubric");
        var criterion = RubricCriterion.Create(rubric.Id, "Correctness", ScoreValue.FromUnits(10000), 0);
        context.AddRange(rubric, criterion);
        await context.SaveChangesAsync();
        var assessment = Assessment.Create(Guid.NewGuid(), "Code", AssessmentType.Assignment, ScoreValue.FromUnits(10000), false);
        assessment.AssignRubric(rubric.Id);
        var projection = await CodeRubricSnapshot.MaterializeAsync(context, assessment, Projection(), CancellationToken.None);
        // This validator reads only frozen content and criterion scores. The
        // complete execution/policy bindings are covered by runtime tests.
        var snapshot = new AssessmentExecutionSnapshotV1(1, new AssessmentAuthoringSourceV1(1, CodeAssessmentContracts.ContentType, projection.Content, projection.Grading!, null!), new AssessmentExecutionManifestV1(1, [], [], []), new Dictionary<string, JsonElement> { ["code"] = projection.Items.Single().PrivateProjection });
        return (snapshot, criterion.Id);
    }

    private static AssessmentAuthoringProjectionV1 Projection() => new(CodeAssessmentContracts.ContentType, JsonSerializer.SerializeToElement(new { definition = "source" }), new ContentGradingDefinitionV2(2, new Dictionary<string, GradingItemAuthoringV2> { ["code"] = new() }), [new("code", "code", ScoreValue.FromUnits(10000), JsonSerializer.SerializeToElement(new { definition = "source" }), CodeAssessmentContracts.AdapterKey, "1")]);
    private static RubricContext Context() => new(new DbContextOptionsBuilder<RubricContext>().UseInMemoryDatabase($"FrozenCodeRubric_{Guid.NewGuid()}").Options);
    private sealed class RubricContext(DbContextOptions<RubricContext> options) : DbContext(options), IApplicationDbContext
    {
        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) => Database.BeginTransactionAsync(cancellationToken);
        protected override void OnModelCreating(ModelBuilder modelBuilder) => new AssessmentsModelConfiguration().Configure(modelBuilder);
    }
}
