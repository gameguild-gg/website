using GameGuild.CQRS;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using Microsoft.Extensions.DependencyInjection;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Dependency injection configuration for Authentication Application layer
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    ///     Registers all Authentication application services including command handlers and validation
    /// </summary>
    public static IServiceCollection AddAuthenticationApplication(this IServiceCollection services)
    {
        services.AddScoped<SendWelcomeEmailHandler>();
        services.AddScoped<LogAnalyticsEventHandler>();
        services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>>(provider =>
            provider.GetRequiredService<SendWelcomeEmailHandler>());
        services.AddScoped<IIntegrationEventHandler<UserCreatedEvent>>(provider =>
            provider.GetRequiredService<LogAnalyticsEventHandler>());

        // Register Command Handlers
        services.AddScoped<IRequestHandler<LocalSignUpCommand, SignInResponse>, LocalSignUpHandler>();
        services.AddScoped<IRequestHandler<LocalSignInCommand, SignInResponse>, LocalSignInHandler>();
        services.AddScoped<IRequestHandler<RefreshTokenCommand, SignInResponse>, RefreshTokenHandler>();
        services.AddScoped<IRequestHandler<GoogleIdTokenSignInCommand, SignInResponse>, GoogleIdTokenSignInHandler>();
        services.AddScoped<IRequestHandler<SendEmailVerificationCommand, EmailVerificationResponse>, SendEmailVerificationCommandHandler>();
        services.AddScoped<IRequestHandler<VerifyEmailCommand, EmailVerificationResult>, VerifyEmailCommandHandler>();
        services.AddScoped<IRequestHandler<RequestPasswordResetCommand, PasswordResetRequestResult>, RequestPasswordResetCommandHandler>();
        services.AddScoped<IRequestHandler<ResetPasswordCommand, PasswordResetResult>, ResetPasswordCommandHandler>();
        services.AddScoped<IRequestHandler<ChangePasswordCommand, PasswordChangeResult>, ChangePasswordCommandHandler>();

        // Register Permission Template Handlers
        services.AddScoped<IQueryHandler<GetPermissionTemplatesQuery, IEnumerable<PermissionTemplateDto>>, GetPermissionTemplatesQueryHandler>();
        services.AddScoped<ICommandHandler<ApplyPermissionTemplateCommand, ApplyPermissionTemplateResult>, ApplyPermissionTemplateCommandHandler>();

        // Register validators
        services.AddScoped<FluentValidation.IValidator<LocalSignUpCommand>, LocalSignUpCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<LocalSignInCommand>, LocalSignInCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<RefreshTokenCommand>, RefreshTokenCommandValidator>();
        services.AddScoped<FluentValidation.IValidator<GoogleIdTokenSignInCommand>, GoogleIdTokenSignInCommandValidator>();

        // Email renderers for identity notifications (consumed by the Notifications email dispatcher registry).
        services.AddScoped<IEmailRenderer, WelcomeEmailRenderer>();
        services.AddScoped<IEmailRenderer, EmailVerificationRenderer>();
        services.AddScoped<IEmailRenderer, PasswordResetRenderer>();
        services.AddScoped<IEmailRenderer, MagicLinkRenderer>();

        return services;
    }
}
