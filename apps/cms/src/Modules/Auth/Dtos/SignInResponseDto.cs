namespace GameGuild.Modules.Auth.Dtos
{
    public class SignInResponseDto
    {
        public string AccessToken
        {
            get;
            set;
        } = string.Empty;

        public string RefreshToken
        {
            get;
            set;
        } = string.Empty;

        public UserDto User
        {
            get;
            set;
        } = new UserDto();
    }
}
