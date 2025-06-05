using cms.Modules.Auth.Dtos;

namespace cms.Modules.Auth.Dtos
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
