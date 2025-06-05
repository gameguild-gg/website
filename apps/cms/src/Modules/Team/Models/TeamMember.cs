using cms.Common.Entities;

namespace Cms.Models
{
    public class TeamMember : BaseEntity
    {
        public Guid TeamId
        {
            get;
            set;
        }

        public string UserId
        {
            get;
            set;
        } = string.Empty; // External user reference

        public TeamRole Role
        {
            get;
            set;
        }

        public string? InvitedBy
        {
            get;
            set;
        } // user_id who sent invitation (nullable)

        public MemberStatus Status
        {
            get;
            set;
        } = MemberStatus.Pending;

        public Team? Team
        {
            get;
            set;
        }
    }
}
