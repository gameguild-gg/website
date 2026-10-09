using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Teams;

[Table("project_collaboration_teams")]
public sealed class Team : EntityBase
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public TeamVisibility Visibility { get; set; } = TeamVisibility.Private;

    public TeamStatus Status { get; set; } = TeamStatus.Active;

    public bool IsPersonal { get; set; }

    // Kept during the data transition so existing rows and queries remain compatible.
    public bool IsActive { get; set; } = true;

    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();

    public ICollection<TeamInvitation> Invitations { get; set; } = new List<TeamInvitation>();

    public static Team Create(Guid tenantId, string name, string slug, Guid ownerUserId, bool isPersonal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (ownerUserId == Guid.Empty)
        {
            throw new ArgumentException("An owner is required.", nameof(ownerUserId));
        }

        var team = new Team
        {
            TenantId = tenantId,
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            IsPersonal = isPersonal
        };
        team.AddMember(ownerUserId, TeamMemberAuthority.Owner);
        return team;
    }

    public TeamMember AddMember(Guid userId, TeamMemberAuthority authority, string? professionalTitle = null)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A user is required.", nameof(userId));
        }

        var current = Members.SingleOrDefault(member => member.UserId == userId && member.DeletedAt == null);
        if (current != null)
        {
            current.Activate(authority, professionalTitle);
            return current;
        }

        var member = TeamMember.Create(TenantId!.Value, Id, userId, authority, professionalTitle);
        Members.Add(member);
        return member;
    }

    public void ChangeAuthority(Guid userId, TeamMemberAuthority authority)
    {
        var member = GetActiveMember(userId);
        if (member.Authority == TeamMemberAuthority.Owner && authority != TeamMemberAuthority.Owner)
        {
            EnsureAnotherActiveOwner(member.UserId);
        }

        member.ChangeAuthority(authority);
    }

    public void RemoveMember(Guid userId)
    {
        var member = GetActiveMember(userId);
        if (member.Authority == TeamMemberAuthority.Owner)
        {
            EnsureAnotherActiveOwner(member.UserId);
        }

        member.Deactivate();
    }

    public void Archive()
    {
        Status = TeamStatus.Archived;
        IsActive = false;
        Touch();
    }

    public new void Restore()
    {
        if (Status != TeamStatus.Archived)
        {
            throw new InvalidOperationException("Only an archived Team can be restored.");
        }

        Status = TeamStatus.Active;
        IsActive = true;
        Touch();
    }

    private TeamMember GetActiveMember(Guid userId) => Members.SingleOrDefault(member =>
        member.UserId == userId && member.IsActive && member.DeletedAt == null)
        ?? throw new InvalidOperationException("The user is not an active team member.");

    private void EnsureAnotherActiveOwner(Guid excludedUserId)
    {
        if (!Members.Any(member =>
                member.UserId != excludedUserId &&
                member.Authority == TeamMemberAuthority.Owner &&
                member.IsActive &&
                member.DeletedAt == null))
        {
            throw new InvalidOperationException("A team cannot lose its last active owner.");
        }
    }
}
