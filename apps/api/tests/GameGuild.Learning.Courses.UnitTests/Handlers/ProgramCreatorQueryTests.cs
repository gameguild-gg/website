using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests.Handlers;

public sealed class ProgramCreatorQueryTests
{
    private static readonly Guid CreatorId = Guid.Parse("7d964847-f4de-4539-a1f0-b48f1fb10cce");
    private static readonly Guid OtherCreatorId = Guid.Parse("4eeb7c73-fb2f-4d9c-98fb-79cb9e01d664");
    private static readonly Guid TenantId = Guid.Parse("f4a370be-de34-4429-a1d6-39087f44a597");
    private static readonly Guid OtherTenantId = Guid.Parse("538bb5cd-c54e-4102-b4a0-e60a1f7c9b39");

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    public async Task ReadService_FiltersCreatorBeforePagination(int skip, int take)
    {
        await using var context = await CreateContext();
        var service = new ProgramReadService(context);

        var programs = await service.GetProgramsByCreatorAsync(CreatorId, skip, take);

        programs.Select(program => program.Title).Should().Equal(
            new[] { "Own draft", "Own private", "Own public" }.Skip(skip).Take(take));
        programs.Should().NotContain(program => program.CreatorId != CreatorId ||
            program.DeletedAt != null || program.TenantId != TenantId);
    }

    [Fact]
    public async Task ReadService_UnknownCreator_ReturnsNoPrograms()
    {
        await using var context = await CreateContext();

        var programs = await new ProgramReadService(context).GetProgramsByCreatorAsync(Guid.NewGuid());

        programs.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatorQuery_PreservesPublicationAndTenantScope(bool onlyPublished)
    {
        await using var context = await CreateContext();

        var programs = await Handler(context).Handle(
            new GetProgramsByCreatorQuery(CreatorId.ToString("D"), OnlyPublished: onlyPublished),
            CancellationToken.None);

        programs.Select(program => program.Title).Should().Equal(onlyPublished
            ? new[] { "Own public" }
            : new[] { "Own draft", "Own private", "Own public" });
        programs.Should().OnlyContain(program => program.CreatorId == CreatorId &&
            program.DeletedAt == null && program.TenantId == TenantId);
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(null)]
    public async Task CreatorQuery_InvalidCreator_ReturnsNoPrograms(string? creatorId)
    {
        await using var context = await CreateContext();

        var programs = await Handler(context).Handle(
            new GetProgramsByCreatorQuery(creatorId!), CancellationToken.None);

        programs.Should().BeEmpty();
    }

    [Theory]
    [InlineData("D")]
    [InlineData("N")]
    [InlineData("B")]
    public async Task CreatorQueries_AcceptGuidFormats_AndPageAfterFiltering(string format)
    {
        await using var context = await CreateContext();
        var creatorId = CreatorId.ToString(format).ToUpperInvariant();

        var creatorPrograms = await Handler(context).Handle(
            new GetProgramsByCreatorQuery(creatorId, Skip: 1, Take: 1), CancellationToken.None);
        var allPrograms = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: creatorId, Skip: 1, Take: 1), CancellationToken.None);

        creatorPrograms.Select(program => program.Title).Should().Equal("Own private");
        allPrograms.Select(program => program.Title).Should().Equal("Own private");
    }

    [Theory]
    [InlineData("not-a-guid")]
    [InlineData(" ")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task AllProgramsQuery_InvalidExplicitCreator_DoesNotBroadenResults(string creatorId)
    {
        await using var context = await CreateContext();

        var programs = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: creatorId), CancellationToken.None);

        programs.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task AllProgramsQuery_OmittedCreator_PreservesOtherFilters(string? creatorId)
    {
        await using var context = await CreateContext();

        var programs = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: creatorId, Status: ContentStatus.Published,
                Visibility: ContentVisibility.Public, SortBy: "Title", SortDescending: false),
            CancellationToken.None);

        programs.Select(program => program.Title).Should().Equal(
            "Other newest", "Other second", "Other third", "Own public", "Unowned");
    }

    [Fact]
    public async Task AllProgramsQuery_CreatorFilter_ComposesWithPublicationAndSorting()
    {
        await using var context = await CreateContext();

        var programs = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: CreatorId.ToString("D"),
                Status: ContentStatus.Published, Visibility: ContentVisibility.Public,
                SortBy: "Title", SortDescending: false), CancellationToken.None);

        programs.Select(program => program.Title).Should().Equal("Own public");
    }

    [Fact]
    public async Task AllProgramsQuery_CreatorFilter_DoesNotIncludeArchivedByDefault()
    {
        await using var context = await CreateContext();
        var archived = Course("Own archived", CreatorId, 10);
        archived.Status = ContentStatus.Archived;
        context.Add(archived);
        await context.SaveChangesAsync();

        var activePrograms = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: CreatorId.ToString("D")), CancellationToken.None);
        var withArchived = await Handler(context).Handle(
            new GetAllProgramsQuery(CreatorId: CreatorId.ToString("D"), IncludeArchived: true),
            CancellationToken.None);

        activePrograms.Select(program => program.Title).Should().Equal("Own draft", "Own private", "Own public");
        withArchived.Select(program => program.Title).Should().Equal(
            "Own archived", "Own draft", "Own private", "Own public");
    }

    private static ProgramBasicQueryHandlers Handler(CreatorQueryContext context) =>
        new(context, NullLogger<ProgramBasicQueryHandlers>.Instance);

    private static async Task<CreatorQueryContext> CreateContext()
    {
        var options = new DbContextOptionsBuilder<CreatorQueryContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new CreatorQueryContext(options);
        var draft = Course("Own draft", CreatorId, 8);
        draft.Status = ContentStatus.Draft;
        var privateCourse = Course("Own private", CreatorId, 6);
        privateCourse.Visibility = ContentVisibility.Private;
        var deleted = Course("Own deleted", CreatorId, 12);
        deleted.DeletedAt = DateTime.UtcNow;
        var otherTenant = Course("Own in other tenant", CreatorId, 11);
        otherTenant.TenantId = OtherTenantId;
        context.AddRange(
            deleted, otherTenant, Course("Other newest", OtherCreatorId, 9), draft,
            Course("Other second", OtherCreatorId, 7), privateCourse,
            Course("Other third", OtherCreatorId, 5), Course("Own public", CreatorId, 4),
            Course("Unowned", null, 3));
        await context.SaveChangesAsync();
        return context;
    }

    private static Program Course(string title, Guid? creatorId, int order) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Slug = title.Replace(' ', '-').ToLowerInvariant(),
        CreatorId = creatorId,
        TenantId = TenantId,
        Status = ContentStatus.Published,
        Visibility = ContentVisibility.Public,
        CreatedAt = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc).AddHours(order),
        Version = 1
    };

    // This focused EF model retains a tenant query filter. It does not certify the host's
    // tenant middleware, authorization pipeline, migrations, or database provider.
    private sealed class CreatorQueryContext(DbContextOptions<CreatorQueryContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public Guid CurrentTenantId => TenantId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Program>(entity =>
            {
                entity.Ignore(program => program.ProgramContents);
                entity.Ignore(program => program.ProgramUsers);
                entity.Ignore(program => program.ProgramRatings);
                entity.Ignore(program => program.ProgramWishlists);
                entity.HasQueryFilter(program => program.TenantId == CurrentTenantId);
            });
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Creator queries do not require transactions.");
    }
}
