using GameGuild.Identity.Users;

namespace GameGuild.TestingLab;

[Table("testing_committee_members")]
public sealed class TestingCommitteeMember : EntityBase
{
    public Guid EventId { get; private set; }

    public TestingEvent Event { get; private set; } = null!;

    public Guid UserId { get; private set; }

    public User User { get; private set; } = null!;

    public bool IsChair { get; private set; }

    public bool IsActive { get; private set; } = true;

    private TestingCommitteeMember()
    {
    }

    public static TestingCommitteeMember Create(Guid eventId, Guid userId, bool isChair, Guid? tenantId)
    {
        if (eventId == Guid.Empty || userId == Guid.Empty)
        {
            throw new ArgumentException("Event and reviewer are required.");
        }

        return new TestingCommitteeMember
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            IsChair = isChair,
            TenantId = tenantId,
        };
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Touch();
    }
}
