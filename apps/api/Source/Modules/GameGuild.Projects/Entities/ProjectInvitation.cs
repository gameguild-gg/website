using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GameGuild.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Projects;

[Table("ProjectInvitations")]
[Index(nameof(Token), IsUnique = true, Name = "IX_ProjectInvitations_Token")]
[Index(nameof(ProjectId), nameof(Status), Name = "IX_ProjectInvitations_Project_Status")]
[Index(nameof(InvitedUserId), nameof(Status), Name = "IX_ProjectInvitations_User_Status")]
public sealed class ProjectInvitation : EntityBase<Guid>
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;

    public Guid? InvitedUserId { get; set; }
    public User? InvitedUser { get; set; }

    public Guid InvitedByUserId { get; set; }
    public User? InvitedByUser { get; set; }

    [Required]
    [MaxLength(64)]
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    [MaxLength(255)]
    public string? InvitedEmail { get; set; }

    [Required]
    [MaxLength(100)]
    public string Role { get; set; } = "Viewer";

    [Required]
    [MaxLength(500)]
    public string Permissions { get; set; } = "read";

    public ProjectInvitationStatus Status { get; set; } = ProjectInvitationStatus.Pending;

    public DateTime InvitedAt { get; set; } = SystemClock.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < SystemClock.UtcNow;
    public bool CanRespond => Status == ProjectInvitationStatus.Pending && !IsExpired;

    public void Accept()
    {
        if (!CanRespond)
        {
            throw new InvalidOperationException("Only pending, non-expired invitations can be accepted.");
        }

        Status = ProjectInvitationStatus.Accepted;
        RespondedAt = SystemClock.UtcNow;
    }

    public void Decline()
    {
        if (!CanRespond)
        {
            throw new InvalidOperationException("Only pending, non-expired invitations can be declined.");
        }

        Status = ProjectInvitationStatus.Declined;
        RespondedAt = SystemClock.UtcNow;
    }
}

public enum ProjectInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Revoked = 3,
    Expired = 4
}
