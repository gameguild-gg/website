namespace cms.Modules.Auth.Dtos
{
    public class UserDto
    {
        public Guid Id
        {
            get;
            set;
        }

        public string Username
        {
            get;
            set;
        } = string.Empty;

        public string Email
        {
            get;
            set;
        } = string.Empty;
        // Add other user fields as needed
    }
}
