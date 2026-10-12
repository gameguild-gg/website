using FluentValidation;
using GameGuild.Identity.Users;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Validator for LocalSignUpCommand following CQRS and DRY principles
/// </summary>
public sealed class LocalSignUpCommandValidator : AbstractValidator<LocalSignUpCommand>
{
    public LocalSignUpCommandValidator(IPasswordHasher passwordHasher)
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email is required")
            .NotNull()
            .WithMessage("Email cannot be null")
            .EmailAddress()
            .WithMessage("Email must be a valid email address")
            .MaximumLength(254)
            .WithMessage("Email is too long"); // RFC 5321 limit

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Password is required")
            .NotNull()
            .WithMessage("Password cannot be null")
            .Custom((password, context) =>
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    return;
                }

                var result = passwordHasher.ValidatePasswordStrength(password);
                foreach (var failure in result.ValidationFailures)
                {
                    context.AddFailure(new FluentValidation.Results.ValidationFailure(nameof(LocalSignUpCommand.Password), failure)
                    {
                        AttemptedValue = null
                    });
                }
            });

        RuleFor(x => x.Username)
            .NotEmpty()
            .WithMessage("Username is required")
            .NotNull()
            .WithMessage("Username cannot be null")
            .MinimumLength(3)
            .WithMessage("Username must be at least 3 characters long")
            .MaximumLength(50)
            .WithMessage("Username is too long")
            .Must(username => UsernameSlug.Normalize(username) is { Length: >= 3 and <= 50 })
            .WithMessage("Username must produce a handle of 3 to 50 letters, numbers, dots, hyphens, or underscores");

        RuleFor(x => x.TenantId).Must(tenantId => !tenantId.HasValue || tenantId.Value != Guid.Empty).WithMessage("Tenant ID must be a valid GUID when provided");
    }
}
