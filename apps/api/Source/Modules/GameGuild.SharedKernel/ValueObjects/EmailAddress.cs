using System.Net.Mail;

namespace GameGuild;

/// <summary>
///     Represents an email address value object with validation
/// </summary>
public record EmailAddress
{
    public EmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email address cannot be null or empty.", nameof(email));
        }

        // Trim first, then validate on the trimmed value to avoid rejecting valid emails with surrounding whitespace
        var trimmed = email.Trim();

        if (!IsValidEmail(trimmed))
        {
            throw new ArgumentException("Invalid email address format.", nameof(email));
        }

        Value = trimmed.ToLowerInvariant();
    }

    public string Value { get; }

    public static implicit operator string(EmailAddress email) { return email.Value; }

    public static explicit operator EmailAddress(string email) { return new EmailAddress(email); }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new MailAddress(email);

            return string.Equals(addr.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException) { return false; }
    }

    public override string ToString() { return Value; }
}
