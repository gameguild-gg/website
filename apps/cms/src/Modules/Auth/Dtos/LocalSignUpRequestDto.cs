using System.ComponentModel.DataAnnotations;

namespace GameGuild.Modules.Auth.Dtos
{
    public class LocalSignUpRequestDto
    {
        public string? Username
        {
            get;
            set;
        }

        [Required, EmailAddress]
        public string Email
        {
            get;
            set;
        } = string.Empty;

        [Required]
        public string Password
        {
            get;
            set;
        } = string.Empty;
    }
}
