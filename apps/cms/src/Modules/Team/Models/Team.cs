namespace Cms.Models
{
    public class Team
    {
        public int Id
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string? Description
        {
            get;
            set;
        }

        public ICollection<TeamMember> Members
        {
            get;
            set;
        } = new List<TeamMember>();
    }
}
