using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using GameGuild.Social.Posts.Services;
using Xunit;

namespace GameGuild.Social.Posts.UnitTests;

/// <summary>
/// Tests for PostAnnouncementService
/// </summary>
public class PostAnnouncementServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock = new();
    private readonly Mock<IPostService> _postServiceMock = new();
    private readonly Mock<ILogger<PostAnnouncementService>> _loggerMock = new();
    private readonly PostAnnouncementService _service;

    public PostAnnouncementServiceTests()
    {
        _service = new PostAnnouncementService(
            _contextMock.Object,
            _postServiceMock.Object,
            _loggerMock.Object);
    }

    private static Post CreateTestPost() => Post.Create(Guid.NewGuid(), "test");

    // --- CreateSystemAnnouncementAsync ---

    [Fact]
    public async Task CreateSystemAnnouncement_Normal_CreatesPostWithAnnouncementTag()
    {
        var tenantId = Guid.NewGuid();
        var authorId = Guid.NewGuid();
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                authorId, It.IsAny<string>(), PostVisibility.Public,
                null, null, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateSystemAnnouncementAsync(
            tenantId, authorId, "Test Title", "Test message", "normal");

        result.IsSuccess.Should().BeTrue();
        _postServiceMock.Verify(
            x => x.AddTagsToPostAsync(post.Id,
                It.Is<string[]>(tags => tags.Contains("announcement") && tags.Contains("system")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        // Normal priority should not pin
        _postServiceMock.Verify(
            x => x.TogglePostPinAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("low")]
    [InlineData("unknown")]
    public async Task CreateSystemAnnouncement_OtherPriorities_CreatesPost(string priority)
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateSystemAnnouncementAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Message", priority);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("high")]
    [InlineData("urgent")]
    public async Task CreateSystemAnnouncement_HighPriority_PinsPost(string priority)
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.TogglePostPinAsync(post.Id, post.AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _service.CreateSystemAnnouncementAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Alert", "Urgent msg", priority);

        _postServiceMock.Verify(
            x => x.TogglePostPinAsync(post.Id, post.AuthorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateSystemAnnouncement_Failure_ReturnsFailure()
    {
        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<PostVisibility>(),
                It.IsAny<string?>(), It.IsAny<MediaType?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Post>(Error.Failure("fail", "err")));

        var result = await _service.CreateSystemAnnouncementAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "Message");

        result.IsSuccess.Should().BeFalse();
    }

    // --- CreateMilestoneCelebrationAsync ---

    [Fact]
    public async Task CreateMilestoneCelebration_Success_TagsMilestone()
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateMilestoneCelebrationAsync(
            Guid.NewGuid(), Guid.NewGuid(), "First Course", "Completed!", DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        _postServiceMock.Verify(
            x => x.AddTagsToPostAsync(post.Id,
                It.Is<string[]>(tags => tags.Contains("milestone") && tags.Contains("celebration")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- CreateCommunityUpdateAsync ---

    [Fact]
    public async Task CreateCommunityUpdate_AllAudience_NoExtraTag()
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _service.CreateCommunityUpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Update", "Content", "all");

        _postServiceMock.Verify(
            x => x.AddTagsToPostAsync(post.Id,
                It.Is<string[]>(tags => tags.Length == 2 &&
                    tags.Contains("community") && tags.Contains("update")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateCommunityUpdate_SpecificAudience_AddsAudienceTag()
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        await _service.CreateCommunityUpdateAsync(
            Guid.NewGuid(), Guid.NewGuid(), "Update", "Content", "Instructors");

        _postServiceMock.Verify(
            x => x.AddTagsToPostAsync(post.Id,
                It.Is<string[]>(tags => tags.Length == 3 && tags.Contains("instructors")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- CreateWelcomePostAsync ---

    [Fact]
    public async Task CreateWelcomePost_Success_TagsWelcome()
    {
        var post = CreateTestPost();

        _postServiceMock
            .Setup(x => x.CreatePostAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), PostVisibility.Public,
                null, null, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(post));

        _postServiceMock
            .Setup(x => x.AddTagsToPostAsync(post.Id, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        var result = await _service.CreateWelcomePostAsync(
            Guid.NewGuid(), Guid.NewGuid(), "JohnDoe");

        result.IsSuccess.Should().BeTrue();
        _postServiceMock.Verify(
            x => x.AddTagsToPostAsync(post.Id,
                It.Is<string[]>(tags => tags.Contains("welcome") && tags.Contains("new-member")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
