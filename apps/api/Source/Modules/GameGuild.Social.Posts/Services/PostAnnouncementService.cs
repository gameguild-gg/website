using Microsoft.Extensions.Logging;

namespace GameGuild.Social.Posts.Services;

/// <summary>
/// Service for creating system-generated and automated posts
/// </summary>
public class PostAnnouncementService : IPostAnnouncementService
{
    private readonly IApplicationDbContext _context;
    private readonly IPostService _postService;
    private readonly ILogger<PostAnnouncementService> _logger;

    public PostAnnouncementService(
        IApplicationDbContext context,
        IPostService postService,
        ILogger<PostAnnouncementService> logger)
    {
        _context = context;
        _postService = postService;
        _logger = logger;
    }

    public async Task<Result<Post>> CreateSystemAnnouncementAsync(
        Guid tenantId,
        Guid authorId,
        string title,
        string message,
        string priority = "normal",
        CancellationToken cancellationToken = default)
    {
        var content = FormatSystemAnnouncement(title, message, priority);

        var result = await _postService.CreatePostAsync(
            authorId,
            content,
            PostVisibility.Public,
            tenantId: tenantId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            // Pin high-priority announcements
            if (priority == "high" || priority == "urgent")
            {
                await _postService.TogglePostPinAsync(result.Value.Id, result.Value.AuthorId, cancellationToken).ConfigureAwait(false);
            }

            // Add system tag
            await _postService.AddTagsToPostAsync(result.Value.Id, new[] { "announcement", "system" }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created system announcement post {PostId} for tenant {TenantId}", result.Value.Id, tenantId);
        }

        return result;
    }

    public async Task<Result<Post>> CreateMilestoneCelebrationAsync(
        Guid tenantId,
        Guid authorId,
        string milestoneName,
        string description,
        DateTime achievementDate,
        CancellationToken cancellationToken = default)
    {
        var content = FormatMilestoneCelebration(milestoneName, description, achievementDate);

        var result = await _postService.CreatePostAsync(
            authorId,
            content,
            PostVisibility.Public,
            tenantId: tenantId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            await _postService.AddTagsToPostAsync(result.Value.Id, new[] { "milestone", "celebration", "achievement" }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created milestone celebration post {PostId} for '{Milestone}'", result.Value.Id, milestoneName);
        }

        return result;
    }

    public async Task<Result<Post>> CreateCommunityUpdateAsync(
        Guid tenantId,
        Guid authorId,
        string title,
        string content,
        string targetAudience = "all",
        CancellationToken cancellationToken = default)
    {
        var formattedContent = FormatCommunityUpdate(title, content, targetAudience);

        var result = await _postService.CreatePostAsync(
            authorId,
            formattedContent,
            PostVisibility.Public,
            tenantId: tenantId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            var tags = new List<string> { "community", "update" };
            if (targetAudience != "all")
                tags.Add(targetAudience.ToLowerInvariant());

            await _postService.AddTagsToPostAsync(result.Value.Id, tags.ToArray(), cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created community update post {PostId} for audience '{Audience}'", result.Value.Id, targetAudience);
        }

        return result;
    }

    public async Task<Result<Post>> CreateWelcomePostAsync(
        Guid tenantId,
        Guid userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        var content = FormatWelcomePost(userName);

        var result = await _postService.CreatePostAsync(
            userId,
            content,
            PostVisibility.Public,
            tenantId: tenantId,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.IsSuccess && result.Value is not null)
        {
            await _postService.AddTagsToPostAsync(result.Value.Id, new[] { "welcome", "introduction", "new-member" }, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Created welcome post {PostId} for user {UserId}", result.Value.Id, userId);
        }

        return result;
    }

    #region Private Formatting Methods

    private static string FormatSystemAnnouncement(string title, string message, string priority)
    {
        var priorityEmoji = priority switch
        {
            "urgent" => "🚨",
            "high" => "⚠️",
            "normal" => "📢",
            "low" => "ℹ️",
            _ => "📢"
        };

        return $"{priorityEmoji} **{title}**\n\n{message}";
    }

    private static string FormatMilestoneCelebration(string milestoneName, string description, DateTime achievementDate)
    {
        return $"🎉 **Milestone Achieved: {milestoneName}**\n\n" +
               $"{description}\n\n" +
               $"📅 Achieved on {achievementDate:MMMM d, yyyy}";
    }

    private static string FormatCommunityUpdate(string title, string content, string targetAudience)
    {
        var audienceTag = targetAudience != "all" ? $"\n\n👥 For: {targetAudience}" : "";
        return $"📋 **{title}**\n\n{content}{audienceTag}";
    }

    private static string FormatWelcomePost(string userName)
    {
        return $"👋 Hi everyone! I'm {userName} and I just joined the community.\n\n" +
               "Looking forward to learning and collaborating with all of you! 🎮";
    }

    #endregion
}
