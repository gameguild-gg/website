using System;

namespace cms.Modules.Auth.Dtos
{
    public class OAuthSignInRequestDto
    {
        public string Code
        {
            get;
            set;
        } = string.Empty;

        public string State
        {
            get;
            set;
        } = string.Empty;

        public string RedirectUri
        {
            get;
            set;
        } = string.Empty;
    }

    public class GitHubUserDto
    {
        public long Id
        {
            get;
            set;
        }

        public string Login
        {
            get;
            set;
        } = string.Empty;

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string Email
        {
            get;
            set;
        } = string.Empty;

        public string AvatarUrl
        {
            get;
            set;
        } = string.Empty;
    }

    public class GoogleUserDto
    {
        public string Id
        {
            get;
            set;
        } = string.Empty;

        public string Email
        {
            get;
            set;
        } = string.Empty;

        public string Name
        {
            get;
            set;
        } = string.Empty;

        public string Picture
        {
            get;
            set;
        } = string.Empty;

        public bool EmailVerified
        {
            get;
            set;
        }
    }
}
