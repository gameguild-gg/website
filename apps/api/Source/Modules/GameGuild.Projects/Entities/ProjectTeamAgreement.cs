using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GameGuild.Projects;

[Table("project_team_agreements")]
public sealed class ProjectTeamAgreement : EntityBase
{
    public Guid ProjectId { get; set; }
    public Guid ProposingTeamId { get; set; }
    public Guid ReceivingTeamId { get; set; }
    public Guid ProposedByUserId { get; set; }
    public Guid? AcceptedByUserId { get; set; }
    public ProjectTeamAgreementStatus Status { get; set; } = ProjectTeamAgreementStatus.Proposed;

    [Required, MaxLength(1000)]
    public string Scope { get; set; } = string.Empty;

    [Required, MaxLength(2000)]
    public string Deliverables { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public int Revision { get; set; } = 1;
    public DateTime? AcceptedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public static ProjectTeamAgreement Create(
        Guid projectId,
        Guid proposingTeamId,
        Guid receivingTeamId,
        Guid proposedByUserId,
        string scope,
        string deliverables,
        DateTime startsAt,
        DateTime endsAt)
    {
        if (proposingTeamId == receivingTeamId)
        {
            throw new ArgumentException("An agreement requires two different teams.", nameof(receivingTeamId));
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Agreement end must be after its start.", nameof(endsAt));
        }

        return new ProjectTeamAgreement
        {
            ProjectId = projectId,
            ProposingTeamId = proposingTeamId,
            ReceivingTeamId = receivingTeamId,
            ProposedByUserId = proposedByUserId,
            Scope = scope.Trim(),
            Deliverables = deliverables.Trim(),
            StartsAt = startsAt,
            EndsAt = endsAt
        };
    }

    public void CounterPropose(Guid actorId, string scope, string deliverables, DateTime startsAt, DateTime endsAt)
    {
        if (Status is ProjectTeamAgreementStatus.Accepted or ProjectTeamAgreementStatus.Cancelled or ProjectTeamAgreementStatus.Completed)
        {
            throw new InvalidOperationException("This agreement can no longer be revised.");
        }

        if (endsAt <= startsAt)
        {
            throw new ArgumentException("Agreement end must be after its start.", nameof(endsAt));
        }

        ProposedByUserId = actorId;
        Scope = scope.Trim();
        Deliverables = deliverables.Trim();
        StartsAt = startsAt;
        EndsAt = endsAt;
        Revision++;
        Status = ProjectTeamAgreementStatus.CounterProposed;
        Touch();
    }

    public void Accept(Guid actorId)
    {
        if (actorId == ProposedByUserId)
        {
            throw new InvalidOperationException("Agreement acceptance requires a distinct actor.");
        }

        if (Status is not (ProjectTeamAgreementStatus.Proposed or ProjectTeamAgreementStatus.CounterProposed))
        {
            throw new InvalidOperationException("This agreement cannot be accepted in its current state.");
        }

        AcceptedByUserId = actorId;
        AcceptedAt = SystemClock.UtcNow;
        Status = ProjectTeamAgreementStatus.Accepted;
        Touch();
    }

    public void Cancel()
    {
        if (Status == ProjectTeamAgreementStatus.Completed)
        {
            throw new InvalidOperationException("A completed agreement cannot be cancelled.");
        }

        Status = ProjectTeamAgreementStatus.Cancelled;
        CancelledAt = SystemClock.UtcNow;
        Touch();
    }

    public void Complete()
    {
        if (Status != ProjectTeamAgreementStatus.Accepted)
        {
            throw new InvalidOperationException("Only an accepted agreement can be completed.");
        }

        Status = ProjectTeamAgreementStatus.Completed;
        CompletedAt = SystemClock.UtcNow;
        Touch();
    }
}
