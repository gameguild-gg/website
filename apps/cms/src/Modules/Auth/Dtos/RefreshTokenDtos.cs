using System;

namespace cms.Modules.Auth.Dtos
{
    public class RefreshTokenRequestDto
    {
        public string RefreshToken
        {
            get;
            set;
        } = string.Empty;
    }

    public class RefreshTokenResponseDto
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

        public DateTime ExpiresAt
        {
            get;
            set;
        }
    }
}
