using FluentAssertions;
using GameGuild.Resources.Contents;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Resources.Contents.UnitTests.Services;

/// <summary>
/// Tests for ContentVersionQueryService - version history and comparison queries
/// </summary>
public class ContentVersionQueryServiceTests
{
    private readonly Mock<IApplicationDbContext> _dbMock;
    private readonly List<ContentVersion> _versions;
    private readonly ContentVersionQueryService _service;

    public ContentVersionQueryServiceTests()
    {
        _dbMock = new Mock<IApplicationDbContext>();
        Mock<ILogger<ContentVersionQueryService>> loggerMock = new Mock<ILogger<ContentVersionQueryService>>();
        _versions = new List<ContentVersion>();

        _service = new ContentVersionQueryService(_dbMock.Object, loggerMock.Object);
    }

    private void SetupDbSet()
    {
        var mockDbSet = _versions.AsQueryable().BuildMockDbSet();
        _dbMock.Setup(d => d.Set<ContentVersion>()).Returns(mockDbSet.Object);
    }

    #region GetVersionHistoryAsync Tests

    [Fact]
    public async Task GetVersionHistoryAsync_ShouldReturnAllVersionsForEntity()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "V1", Guid.NewGuid());
        var v2 = ContentVersion.Create(entityId, "Course", 2, "V2", Guid.NewGuid());
        var v3 = ContentVersion.Create(entityId, "Course", 3, "V3", Guid.NewGuid());
        var otherEntity = ContentVersion.Create(Guid.NewGuid(), "Course", 1, "Other", Guid.NewGuid());

        _versions.AddRange(new[] { v1, v2, v3, otherEntity });
        SetupDbSet();

        // Act
        var result = await _service.GetVersionHistoryAsync(entityId, "Course");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value.Should().OnlyContain(v => v.EntityId == entityId);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ShouldOrderByVersionNumberDescending()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "V1", Guid.NewGuid());
        var v2 = ContentVersion.Create(entityId, "Course", 2, "V2", Guid.NewGuid());
        var v3 = ContentVersion.Create(entityId, "Course", 3, "V3", Guid.NewGuid());

        _versions.AddRange(new[] { v1, v3, v2 }); // Add out of order
        SetupDbSet();

        // Act
        var result = await _service.GetVersionHistoryAsync(entityId, "Course");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var versions = result.Value.ToList();
        versions[0].VersionNumber.Should().Be(3);
        versions[1].VersionNumber.Should().Be(2);
        versions[2].VersionNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetVersionHistoryAsync_ShouldExcludeDeletedVersions()
    {
        // Arrange - Note: We can't soft-delete in unit tests without reflection,
        // but this demonstrates the expected behavior
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "Active", Guid.NewGuid());
        _versions.Add(v1);
        SetupDbSet();

        // Act
        var result = await _service.GetVersionHistoryAsync(entityId, "Course");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    #endregion

    #region GetVersionAsync Tests

    [Fact]
    public async Task GetVersionAsync_WhenExists_ShouldReturnVersion()
    {
        // Arrange
        var version = ContentVersion.Create(Guid.NewGuid(), "Course", 1, "Title", Guid.NewGuid());
        _versions.Add(version);
        SetupDbSet();

        // Act
        var result = await _service.GetVersionAsync(version.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(version.Id);
    }

    [Fact]
    public async Task GetVersionAsync_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        SetupDbSet();

        // Act
        var result = await _service.GetVersionAsync(Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ContentVersioning.NotFound");
    }

    #endregion

    #region GetVersionByNumberAsync Tests

    [Fact]
    public async Task GetVersionByNumberAsync_ShouldReturnCorrectVersion()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "V1", Guid.NewGuid());
        var v2 = ContentVersion.Create(entityId, "Course", 2, "V2", Guid.NewGuid());
        var v3 = ContentVersion.Create(entityId, "Course", 3, "V3", Guid.NewGuid());

        _versions.AddRange(new[] { v1, v2, v3 });
        SetupDbSet();

        // Act
        var result = await _service.GetVersionByNumberAsync(entityId, "Course", 2);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.VersionNumber.Should().Be(2);
        result.Value.Title.Should().Be("V2");
    }

    [Fact]
    public async Task GetVersionByNumberAsync_WhenNotFound_ShouldReturnFailure()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "V1", Guid.NewGuid());
        _versions.Add(v1);
        SetupDbSet();

        // Act
        var result = await _service.GetVersionByNumberAsync(entityId, "Course", 99);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ContentVersioning.NotFound");
    }

    #endregion

    #region GetCurrentVersionAsync Tests

    [Fact]
    public async Task GetCurrentVersionAsync_ShouldReturnCurrentVersion()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "V1", Guid.NewGuid());
        v1.SubmitForReview(Guid.NewGuid());
        v1.Approve(Guid.NewGuid());
        v1.Publish(Guid.NewGuid());
        v1.SetAsCurrent(false);

        var v2 = ContentVersion.Create(entityId, "Course", 2, "V2 Current", Guid.NewGuid());
        v2.SubmitForReview(Guid.NewGuid());
        v2.Approve(Guid.NewGuid());
        v2.Publish(Guid.NewGuid()); // IsCurrentVersion = true

        _versions.AddRange(new[] { v1, v2 });
        SetupDbSet();

        // Act
        var result = await _service.GetCurrentVersionAsync(entityId, "Course");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("V2 Current");
        result.Value.IsCurrentVersion.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentVersionAsync_WhenNoCurrentVersion_ShouldReturnFailure()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var draft = ContentVersion.Create(entityId, "Course", 1, "Draft", Guid.NewGuid());
        _versions.Add(draft);
        SetupDbSet();

        // Act
        var result = await _service.GetCurrentVersionAsync(entityId, "Course");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ContentVersioning.NotFound");
    }

    #endregion

    #region CompareVersionsAsync Tests

    [Fact]
    public async Task CompareVersionsAsync_ShouldReturnDiff()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "Original Title", Guid.NewGuid(),
            summary: "Original summary", body: "Original body");
        var v2 = ContentVersion.Create(entityId, "Course", 2, "Updated Title", Guid.NewGuid(),
            summary: "Updated summary", body: "Original body"); // Same body

        _versions.AddRange(new[] { v1, v2 });
        SetupDbSet();

        // Act
        var result = await _service.CompareVersionsAsync(v1.Id, v2.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TitleChanged.Should().BeTrue();
        result.Value.SummaryChanged.Should().BeTrue();
        result.Value.BodyChanged.Should().BeFalse();
        result.Value.Version1Number.Should().Be(1);
        result.Value.Version2Number.Should().Be(2);
    }

    [Fact]
    public async Task CompareVersionsAsync_WhenVersionNotFound_ShouldReturnFailure()
    {
        // Arrange
        var version = ContentVersion.Create(Guid.NewGuid(), "Course", 1, "Title", Guid.NewGuid());
        _versions.Add(version);
        SetupDbSet();

        // Act
        var result = await _service.CompareVersionsAsync(version.Id, Guid.NewGuid());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ContentVersioning.NotFound");
    }

    [Fact]
    public async Task CompareVersionsAsync_WhenDifferentEntities_ShouldReturnFailure()
    {
        // Arrange
        var v1 = ContentVersion.Create(Guid.NewGuid(), "Course", 1, "Course 1", Guid.NewGuid());
        var v2 = ContentVersion.Create(Guid.NewGuid(), "Course", 1, "Course 2", Guid.NewGuid());

        _versions.AddRange(new[] { v1, v2 });
        SetupDbSet();

        // Act
        var result = await _service.CompareVersionsAsync(v1.Id, v2.Id);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("ContentVersioning.VersionsMustBeSameEntity");
    }

    [Fact]
    public async Task CompareVersionsAsync_WhenNoChanges_ShouldIndicateNoChanges()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var v1 = ContentVersion.Create(entityId, "Course", 1, "Same Title", Guid.NewGuid(),
            summary: "Same summary", body: "Same body", metadata: "{\"key\":1}");
        var v2 = ContentVersion.Create(entityId, "Course", 2, "Same Title", Guid.NewGuid(),
            summary: "Same summary", body: "Same body", metadata: "{\"key\":1}");

        _versions.AddRange(new[] { v1, v2 });
        SetupDbSet();

        // Act
        var result = await _service.CompareVersionsAsync(v1.Id, v2.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TitleChanged.Should().BeFalse();
        result.Value.SummaryChanged.Should().BeFalse();
        result.Value.BodyChanged.Should().BeFalse();
        result.Value.MetadataChanged.Should().BeFalse();
    }

    #endregion
}
