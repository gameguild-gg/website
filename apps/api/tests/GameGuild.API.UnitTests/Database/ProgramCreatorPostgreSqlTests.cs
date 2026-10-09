using GameGuild.API.Database;
using GameGuild.Identity.Users;
using GameGuild.Learning.Courses;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Course = GameGuild.Learning.Courses.Program;

namespace GameGuild.API.UnitTests.Database;

public sealed class ProgramCreatorPostgreSqlTests(ProgramCreatorPostgreSqlFixture fixture)
    : IClassFixture<ProgramCreatorPostgreSqlFixture>
{
    [Theory]
    [InlineData("service", 0, 3)]
    [InlineData("service", 1, 1)]
    [InlineData("service", 3, 1)]
    [InlineData("all-query", 0, 3)]
    [InlineData("all-query", 1, 1)]
    [InlineData("all-query", 3, 1)]
    [InlineData("creator-query", 0, 3)]
    [InlineData("creator-query", 1, 1)]
    [InlineData("creator-query", 3, 1)]
    public async Task CreatorFilter_IsExecutedBeforePagination_OnPostgreSql(string operation, int skip, int take)
    {
        await using var context = fixture.CreateContext();
        var creatorId = await SeedCourses(context);

        var programs = await Read(context, operation, creatorId, skip, take);

        Assert.Equal(new[] { "Own draft", "Own private", "Own public" }.Skip(skip).Take(take),
            programs.Select(program => program.Title));
        Assert.All(programs, program =>
        {
            Assert.Equal(creatorId, program.CreatorId);
            Assert.Null(program.DeletedAt);
        });
    }

    [Theory]
    [InlineData("all-query")]
    [InlineData("creator-query")]
    public async Task PublicationFilter_ExcludesDraftAndPrivateCourses_OnPostgreSql(string operation)
    {
        await using var context = fixture.CreateContext();
        var creatorId = await SeedCourses(context);
        var handler = new ProgramBasicQueryHandlers(context, NullLogger<ProgramBasicQueryHandlers>.Instance);

        var programs = operation == "all-query"
            ? await handler.Handle(new GetAllProgramsQuery(CreatorId: creatorId.ToString("D"),
                Status: ContentStatus.Published, Visibility: ContentVisibility.Public), CancellationToken.None)
            : await handler.Handle(new GetProgramsByCreatorQuery(creatorId.ToString("D"), OnlyPublished: true),
                CancellationToken.None);

        Assert.Equal(new[] { "Own public" }, programs.Select(program => program.Title));
    }

    [Theory]
    [InlineData("service")]
    [InlineData("all-query")]
    [InlineData("creator-query")]
    public async Task UnknownCreator_DoesNotReturnOtherCreatorsCourses_OnPostgreSql(string operation)
    {
        await using var context = fixture.CreateContext();
        await SeedCourses(context);

        Assert.Empty(await Read(context, operation, Guid.NewGuid(), 0, 50));
    }

    [Theory]
    [InlineData("all-query")]
    [InlineData("creator-query")]
    public async Task InvalidExplicitCreator_DoesNotBroadenResults_OnPostgreSql(string operation)
    {
        await using var context = fixture.CreateContext();
        await SeedCourses(context);
        var handler = new ProgramBasicQueryHandlers(context, NullLogger<ProgramBasicQueryHandlers>.Instance);

        var programs = operation == "all-query"
            ? await handler.Handle(new GetAllProgramsQuery(CreatorId: "not-a-guid"), CancellationToken.None)
            : await handler.Handle(new GetProgramsByCreatorQuery("not-a-guid"), CancellationToken.None);

        Assert.Empty(programs);
    }

    private static Task<IEnumerable<Course>> Read(
        ApplicationDbContext context, string operation, Guid creatorId, int skip, int take)
    {
        var handler = new ProgramBasicQueryHandlers(context, NullLogger<ProgramBasicQueryHandlers>.Instance);
        return operation switch
        {
            "service" => new ProgramReadService(context).GetProgramsByCreatorAsync(creatorId, skip, take),
            "all-query" => handler.Handle(new GetAllProgramsQuery(Skip: skip, Take: take,
                CreatorId: creatorId.ToString("D")), CancellationToken.None),
            "creator-query" => handler.Handle(new GetProgramsByCreatorQuery(creatorId.ToString("D"), skip, take),
                CancellationToken.None),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown creator query.")
        };
    }

    private static async Task<Guid> SeedCourses(ApplicationDbContext context)
    {
        var marker = Guid.NewGuid().ToString("N");
        var creator = User.Create($"creator-{marker}@example.test", "Creator");
        var otherCreator = User.Create($"other-{marker}@example.test", "Other creator");
        creator.Username = $"creator-{marker}";
        otherCreator.Username = $"other-{marker}";
        context.AddRange(creator, otherCreator);
        await context.SaveChangesAsync();
        var draft = CourseEntity("Own draft", creator.Id, marker, 8);
        draft.Status = ContentStatus.Draft;
        var privateCourse = CourseEntity("Own private", creator.Id, marker, 6);
        privateCourse.Visibility = ContentVisibility.Private;
        var deleted = CourseEntity("Own deleted", creator.Id, marker, 12);
        deleted.DeletedAt = DateTime.UtcNow;
        context.AddRange(deleted, CourseEntity("Other newest", otherCreator.Id, marker, 9), draft,
            CourseEntity("Other second", otherCreator.Id, marker, 7), privateCourse,
            CourseEntity("Other third", otherCreator.Id, marker, 5),
            CourseEntity("Own public", creator.Id, marker, 4), CourseEntity("Unowned", null, marker, 3));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return creator.Id;
    }

    private static Course CourseEntity(string title, Guid? creatorId, string marker, int order) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Slug = $"{marker}-{order}",
        CreatorId = creatorId,
        Status = ContentStatus.Published,
        Visibility = ContentVisibility.Public,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(order),
        Version = 1
    };
}

// Reuse the repository's disposable PostgreSQL fixture and production EF model. These
// tests certify query execution, not the HTTP authorization or tenant middleware.
public sealed class ProgramCreatorPostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("program_creator_queries");
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_database!.ConnectionString).Options);

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
