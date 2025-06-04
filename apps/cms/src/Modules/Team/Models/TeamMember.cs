using cms.Common.Entities;

namespace Cms.Models
{
    public class TeamMember : BaseEntity
    {
        public int TeamId { get; set; }
        public string UserId { get; set; } = string.Empty; // External user reference
        public MemberStatus Status { get; set; }
        public TeamRole Role { get; set; }

        public Team? Team { get; set; }
    }
}
