namespace GameGuild.Identity.Authentication;

/// <summary>Optional verification capability; opaque legacy providers are never assumed to have done costly work.</summary>
public interface IPasswordVerificationWork
{
    PasswordVerificationResult VerifyPasswordWithWork(string hashedPassword, string providedPassword);
}
