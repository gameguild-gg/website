using System.ComponentModel.DataAnnotations;

namespace cms.Modules.Auth.Dtos
{
    /// <summary>
    /// Request DTO for sending email verification
    /// </summary>
    public class SendEmailVerificationRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email
        {
            get;
            set;
        } = string.Empty;
    }

    /// <summary>
    /// Request DTO for verifying email
    /// </summary>
    public class VerifyEmailRequestDto
    {
        [Required]
        public string Token
        {
            get;
            set;
        } = string.Empty;
    }

    /// <summary>
    /// Request DTO for sending password reset email
    /// </summary>
    public class ForgotPasswordRequestDto
    {
        [Required]
        [EmailAddress]
        public string Email
        {
            get;
            set;
        } = string.Empty;
    }

    /// <summary>
    /// Request DTO for resetting password
    /// </summary>
    public class ResetPasswordRequestDto
    {
        [Required]
        public string Token
        {
            get;
            set;
        } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword
        {
            get;
            set;
        } = string.Empty;
    }

    /// <summary>
    /// Request DTO for changing password (authenticated user)
    /// </summary>
    public class ChangePasswordRequestDto
    {
        [Required]
        public string CurrentPassword
        {
            get;
            set;
        } = string.Empty;

        [Required]
        [MinLength(8)]
        public string NewPassword
        {
            get;
            set;
        } = string.Empty;
    }

    /// <summary>
    /// Generic response DTO for email operations
    /// </summary>
    public class EmailOperationResponseDto
    {
        public bool Success
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        } = string.Empty;
    }
}
