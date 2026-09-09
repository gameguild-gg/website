using System.Security.Claims;
using FluentAssertions;
using FluentValidation;
using GameGuild.Identity.Authorization;
using GameGuild.Learning.Assessments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Learning.Courses.UnitTests;

public sealed class CodingAssignmentEndpointsTests
{
    // ── (a) GET student view as enrolled → Public tests + Public files only ──────

    [Fact]
    public async Task GetPublicAsync_StripsPrivateTestsAndPrivateFiles()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = PersistCodingContent(fixture, programId, BuildFullContent());

        var result = await fixture.Service.GetPublicAsync(programId, content.Id, Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Tests.Public.Should().HaveCount(2);
        result.Tests.Private.Should().BeEmpty();
        result.Data.Files.Should().NotContainKey("secret.cpp");
        result.Data.Files.Should().ContainKey("main.cpp");
    }

    // ── (c) GET full view returns everything ──────────────────────────────────────

    [Fact]
    public async Task GetFullAsync_ReturnsPrivateTestsAndFiles()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = PersistCodingContent(fixture, programId, BuildFullContent());

        var result = await fixture.Service.GetFullAsync(programId, content.Id);

        result.Should().NotBeNull();
        result!.Tests.Public.Should().HaveCount(2);
        result.Tests.Private.Should().HaveCount(1);
        result.Data.Files.Should().ContainKey("secret.cpp");
    }

    // ── (e) PUT valid payload on null JsonBody → creates ─────────────────────────

    [Fact]
    public async Task UpsertAsync_OnNullJsonBody_CreatesAndPersists()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = new ProgramContent
        {
            ProgramId = programId,
            Title = "Coding",
            Type = ProgramContentType.Code,
            JsonBody = null,
            Version = 1,
        };
        fixture.Context.Set<ProgramContent>().Add(content);
        await fixture.Context.SaveChangesAsync();

        var body = BuildFullContent() with { Grading = new GradingConfig { MaxScore = 80 } };
        var result = await fixture.Service.UpsertAsync(programId, content.Id, body, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fixture.Context.Entry(content).ReloadAsync();
        content.JsonBody.Should().NotBeNull();
        var persisted = System.Text.Json.JsonSerializer.Deserialize<CodingAssignmentContent>(content.JsonBody!);
        persisted!.Grading.MaxScore.Should().Be(80);
    }

    // ── (f) PUT valid payload on existing JsonBody → replaces ────────────────────

    [Fact]
    public async Task UpsertAsync_OnExistingJsonBody_Replaces()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = PersistCodingContent(fixture, programId, BuildFullContent());

        var replacement = BuildFullContent() with
        {
            Grading = new GradingConfig { MaxScore = 50 }
        };
        var result = await fixture.Service.UpsertAsync(programId, content.Id, replacement, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fixture.Context.Entry(content).ReloadAsync();
        var persisted = System.Text.Json.JsonSerializer.Deserialize<CodingAssignmentContent>(content.JsonBody!);
        persisted!.Grading.MaxScore.Should().Be(50);
    }

    // ── (g) PUT with empty tests (open-ended assignment) → succeeds ──────────────

    [Fact]
    public async Task UpsertAsync_WithEmptyTests_Succeeds()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = PersistCodingContent(fixture, programId, BuildFullContent());

        var openEnded = BuildFullContent() with
        {
            Tests = new TestSuite { Public = new List<Test>(), Private = new List<Test>() }
        };

        var result = await fixture.Service.UpsertAsync(programId, content.Id, openEnded, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        await fixture.Context.Entry(content).ReloadAsync();
        var persisted = System.Text.Json.JsonSerializer.Deserialize<CodingAssignmentContent>(content.JsonBody!);
        persisted!.Tests.Public.Should().BeEmpty();
        persisted.Tests.Private.Should().BeEmpty();
    }

    // ── (h) PUT syncs Assessment.MaxScore via IAssessmentGradingSync ─

    [Fact]
    public async Task UpsertAsync_InvokesAssessmentGradingSync()
    {
        await using var fixture = new ServiceFixture();
        var programId = Guid.NewGuid();
        var content = PersistCodingContent(fixture, programId, BuildFullContent());

        var body = BuildFullContent() with
        {
            Grading = new GradingConfig { MaxScore = 90 }
        };

        await fixture.Service.UpsertAsync(programId, content.Id, body, Guid.NewGuid());

        fixture.GradingSyncMock.Verify(
            s => s.SyncAsync(content.Id, 90, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── (b) Student denied when not enrolled → controller returns Forbid ────────

    [Fact]
    public async Task GetCodingAssignmentPublic_NotEnrolled_ReturnsForbid()
    {
        var controller = await Task.FromResult(BuildController(
            out var programMock, out var codingMock,
            enrollProgress: false));
        programMock.Setup(s => s.GetUserProgressDtoAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync((UserProgressDto?)null);

        var result = await controller.GetCodingAssignmentPublic(Guid.NewGuid(), Guid.NewGuid());

        result.Result.Should().BeOfType<ForbidResult>();
        codingMock.Verify(s => s.GetPublicAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── (d) Full view as non-instructor → controller returns Forbid ─────────────

    [Fact]
    public async Task GetCodingAssignmentFull_WithoutManagementAccess_ReturnsForbid()
    {
        var controller = BuildController(
            out var programMock, out var codingMock,
            hasManagementAccess: false);
        programMock.Setup(s => s.GetUserProgressDtoAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
            .ReturnsAsync(new UserProgressDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PercentValue.Zero, null, DateTime.UtcNow, null, Enumerable.Empty<ContentProgressDto>()));

        var result = await controller.GetCodingAssignmentFull(Guid.NewGuid(), Guid.NewGuid());

        result.Result.Should().BeOfType<ForbidResult>();
        codingMock.Verify(s => s.GetFullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── fixtures & helpers ─────────────────────────────────────────────────────

    private static CodingAssignmentContent BuildFullContent() => new()
    {
        Environment = new CodingEnvironment { Language = "cpp", Tools = "clang" },
        Data = new WorkspaceData
        {
            Files = new()
            {
                ["main.cpp"] = new() { Content = "int main(){}", Visibility = "Public", Modifiable = true },
                ["secret.cpp"] = new() { Content = "private", Visibility = "Private", Modifiable = false },
            }
        },
        Tests = new TestSuite
        {
            Public = new() { new StandardTest { Stdout = "ok" }, new StandardTest { Stdout = "ok2" } },
            Private = new() { new StandardTest { Stdout = "hidden" } }
        },
        Grading = new GradingConfig { MaxScore = 100 }
    };

    private static ProgramContent PersistCodingContent(ServiceFixture fixture, Guid programId, CodingAssignmentContent body)
    {
        var content = new ProgramContent
        {
            ProgramId = programId,
            Title = "Coding",
            Type = ProgramContentType.Code,
            JsonBody = System.Text.Json.JsonSerializer.Serialize(body),
            Version = 1,
        };
        fixture.Context.Set<ProgramContent>().Add(content);
        fixture.Context.SaveChanges();
        return content;
    }

    private static ProgramContentController BuildController(
        out Mock<IProgramCrudService> programMock,
        out Mock<ICodingAssignmentContentService> codingMock,
        bool enrollProgress = true,
        bool hasManagementAccess = false)
    {
        programMock = new Mock<IProgramCrudService>();
        codingMock = new Mock<ICodingAssignmentContentService>();
        var authorizationMock = new Mock<IAuthorizationService>();
        var contentMock = new Mock<IProgramContentService>();

        authorizationMock
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                Policies.CourseContentLearner))
            .ReturnsAsync(enrollProgress ? AuthorizationResult.Success() : AuthorizationResult.Failed());
        authorizationMock
            .Setup(service => service.AuthorizeAsync(
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<object>(),
                Policies.CourseContentManage))
            .ReturnsAsync(hasManagementAccess ? AuthorizationResult.Success() : AuthorizationResult.Failed());

        var controller = new ProgramContentController(
            contentMock.Object,
            programMock.Object,
            codingMock.Object,
            authorizationMock.Object,
            [],
            [],
            Mock.Of<ILogger<ProgramContentController>>());

        var userId = Guid.NewGuid();
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("sub", userId.ToString()),
        }, "Test");

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity),
            }
        };

        return controller;
    }

    private sealed class ServiceFixture : IAsyncDisposable
    {
        public CodingAssignmentTestContext Context { get; }
        public CodingAssignmentContentService Service { get; }
        public Mock<IAssessmentGradingSync> GradingSyncMock { get; } = new();

        public ServiceFixture()
        {
            var options = new DbContextOptionsBuilder<CodingAssignmentTestContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Context = new CodingAssignmentTestContext(options);
            Service = new CodingAssignmentContentService(
                Context,
                new CodingAssignmentContentValidator(),
                GradingSyncMock.Object);
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    private sealed class CodingAssignmentTestContext(DbContextOptions<CodingAssignmentTestContext> options)
        : DbContext(options), IApplicationDbContext
    {
        public DbSet<ProgramContent> ProgramContents => Set<ProgramContent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ProgramContent>(entity =>
            {
                entity.Ignore(c => c.Program);
                entity.Ignore(c => c.Parent);
                entity.Ignore(c => c.Children);
                entity.Ignore(c => c.ContentInteractions);
                entity.HasKey(c => c.Id);
            });
            base.OnModelCreating(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
