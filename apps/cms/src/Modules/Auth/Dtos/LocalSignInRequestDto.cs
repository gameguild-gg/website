using System.ComponentModel.DataAnnotations;

namespace cms.Modules.Auth.Dtos
{
    public class LocalSignInRequestDto
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
