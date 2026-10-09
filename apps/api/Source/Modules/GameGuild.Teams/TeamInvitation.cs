using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Teams;

[Table("team_invitations")]
public sealed class TeamInvitation : EntityBase
{
    public Guid TeamId { get; set; }
    public Team? Team { get; set; }
    public Guid? InvitedUserId { get; set; }

    [MaxLength(255)]
    public string? InvitedEmail { get; set; }

    [Required, MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public TeamMemberAuthority Authority { get; set; }
    public Guid InvitedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public Guid? AcceptedByUserId { get; set; }

    public static TeamInvitation Create(
        Guid tenantId,
        Guid teamId,
        Guid invitedByUserId,
        string? invitedEmail,
        TeamMemberAuthority authority,
        string token,
        DateTime expiresAt,
        Guid? invitedUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (invitedUserId == null && string.IsNullOrWhiteSpace(invitedEmail))
        {
            throw new ArgumentException("An invited user or email is required.", nameof(invitedEmail));
        }

        return new TeamInvitation
        {
            TenantId = tenantId,
            TeamId = teamId,
            InvitedByUserId = invitedByUserId,
            InvitedUserId = invitedUserId,
            InvitedEmail = invitedEmail?.Trim().ToLowerInvariant(),
            Authority = authority,
            TokenHash = HashToken(token),
            ExpiresAt = expiresAt
        };
    }

    public bool Accept(string token, Guid acceptedByUserId, DateTime now)
    {
        if (UsedAt.HasValue || RevokedAt.HasValue || ExpiresAt <= now || !Matches(token))
        {
            return false;
        }

        MarkAccepted(acceptedByUserId, now);
        return true;
    }

    public bool AcceptAuthenticated(Guid acceptedByUserId, DateTime now)
    {
        if (UsedAt.HasValue || RevokedAt.HasValue || ExpiresAt <= now)
        {
            return false;
        }

        MarkAccepted(acceptedByUserId, now);
        return true;
    }

    private void MarkAccepted(Guid acceptedByUserId, DateTime now)
    {
        UsedAt = now;
        AcceptedByUserId = acceptedByUserId;
        Touch();
    }

    public void Revoke(DateTime now)
    {
        if (!UsedAt.HasValue)
        {
            RevokedAt = now;
        }

        Touch();
    }

    private bool Matches(string token)
    {
        var actual = Convert.FromHexString(HashToken(token));
        var expected = Convert.FromHexString(TokenHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static string HashToken(string token) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
