using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class SurveyGradingProtectionTests
{
    [Fact]
    public async Task GradeActivityAsync_WhenInteractionBelongsToSurvey_ShouldRejectWithoutCreatingGrade()
    {
        await using var context = CreateContext();
        var program = new Program { Id = Guid.NewGuid(), Title = "Course", Slug = "course" };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        var learner = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var grader = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var interaction = new ContentInteraction { Id = Guid.NewGuid(), ContentId = survey.Id, Content = survey, ProgramUserId = learner.Id, UserId = learner.UserId, SubmittedAt = SystemClock.UtcNow };
        context.AddRange(program, survey, learner, grader, interaction);
        await context.SaveChangesAsync();
        var service = new ActivityGradeService(context);

        Func<Task> action = () => service.GradeActivityAsync(interaction.Id, grader.Id, Score("90"), Score("100"));

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Surveys cannot be graded.");
        (await context.Set<ActivityGrade>().CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GradeReadPaths_WhenSurveyGradeExists_ShouldExcludeIt()
    {
        await using var context = CreateContext();
        var program = new Program { Id = Guid.NewGuid(), Title = "Course", Slug = "course" };
        var survey = new ProgramContent { Id = Guid.NewGuid(), ProgramId = program.Id, Title = "Survey", Type = ProgramContentType.Survey };
        var learner = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var grader = new ProgramUser { Id = Guid.NewGuid(), ProgramId = program.Id, UserId = Guid.NewGuid(), IsActive = true };
        var interaction = new ContentInteraction { Id = Guid.NewGuid(), ContentId = survey.Id, ProgramUserId = learner.Id, UserId = learner.UserId, SubmittedAt = SystemClock.UtcNow };
        var grade = new ActivityGrade
        {
            Id = Guid.NewGuid(),
            ContentInteractionId = interaction.Id,
            GraderProgramUserId = grader.Id,
            Points = Score("90"),
            MaxPoints = Score("100"),
        };
        context.AddRange(program, survey, learner, grader, interaction, grade);
        await context.SaveChangesAsync();
        var service = new ActivityGradeService(context);

        (await service.GetGradeStatisticsAsync(program.Id)).TotalGrades.Should().Be(0);
        var dtoAction = () => grade.ToDto();
        dtoAction.Should().Throw<InvalidOperationException>();
    }

    private static GradeTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GradeTestDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class GradeTestDbContext(DbContextOptions<GradeTestDbContext> options) : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
            });
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(content => content.Parent);
                entity.Ignore(content => content.Children);
                entity.Ignore(content => content.ContentInteractions);
                entity.HasOne(content => content.Program).WithMany().HasForeignKey(content => content.ProgramId);
            });
            modelBuilder.Entity<ProgramUser>(entity =>
            {
                entity.Ignore(enrollment => enrollment.User);
                entity.Ignore(enrollment => enrollment.Program);
                entity.Ignore(enrollment => enrollment.ContentInteractions);
                entity.Ignore(enrollment => enrollment.ReceivedGrades);
                entity.Ignore(enrollment => enrollment.GivenGrades);
                entity.Ignore(enrollment => enrollment.ProgramRatings);
            });
            modelBuilder.Entity<ContentInteraction>(entity =>
            {
                entity.Ignore(interaction => interaction.User);
                entity.Ignore(interaction => interaction.ActivityGrades);
                entity.Ignore(interaction => interaction.Events);
                entity.HasOne(interaction => interaction.Content).WithMany().HasForeignKey(interaction => interaction.ContentId);
                entity.HasOne(interaction => interaction.ProgramUser).WithMany().HasForeignKey(interaction => interaction.ProgramUserId);
            });
            modelBuilder.Entity<ActivityGrade>(entity =>
            {
                entity.Ignore(grade => grade.Student);
                entity.Ignore(grade => grade.Grader);
                entity.Ignore(grade => grade.ProgramUser);
                entity.Ignore(grade => grade.GraderProgramUser);
                entity.HasOne(grade => grade.ContentInteraction).WithMany().HasForeignKey(grade => grade.ContentInteractionId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
