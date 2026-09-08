using GameGuild.CQRS;
using GameGuild.Notifications.Services;
using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.Notifications.Controllers;

public sealed record UnsubscribeNotificationCommand(string? Token) : ICommand<IActionResult>;

public sealed class UnsubscribeNotificationCommandHandler : ICommandHandler<UnsubscribeNotificationCommand, IActionResult>
{
    private const string ManagePath = "/workspace/settings/notifications";

    private readonly IUnsubscribeTokenService _tokenService;
    private readonly INotificationPreferenceService _preferenceService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationUnsubscribeController> _logger;

    public UnsubscribeNotificationCommandHandler(
        IUnsubscribeTokenService tokenService,
        INotificationPreferenceService preferenceService,
        IConfiguration configuration,
        ILogger<NotificationUnsubscribeController> logger)
    {
        _tokenService = tokenService;
        _preferenceService = preferenceService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<IActionResult> Handle(UnsubscribeNotificationCommand command, CancellationToken cancellationToken)
    {
        var token = command.Token;

        if (string.IsNullOrWhiteSpace(token))
        {
            return InvalidToken();
        }

        var result = _tokenService.Validate(token);
        if (!result.IsValid)
        {
            return InvalidToken();
        }

        return result.Scope switch
        {
            "type" => await UnsubscribeFromTypeAsync(result, cancellationToken).ConfigureAwait(false),
            "category" => await UnsubscribeFromCategoryAsync(result, cancellationToken).ConfigureAwait(false),
            "all" => await UnsubscribeFromAllAsync(result, cancellationToken).ConfigureAwait(false),
            _ => InvalidToken()
        };
    }

    private async Task<IActionResult> UnsubscribeFromTypeAsync(UnsubscribeTokenResult token, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<NotificationType>(token.Value, ignoreCase: true, out var type)
            || NotificationCategories.Transactional.Contains(type))
        {
            return Unprocessable("Cannot unsubscribe from transactional emails.");
        }

        var preferences = await _preferenceService.GetPreferencesAsync(token.UserId, cancellationToken).ConfigureAwait(false);
        if (!preferences.IsSuccess)
        {
            return InternalFailure();
        }

        var mutedNames = preferences.Value.GetMutedTypeNames().Append(type.ToString());
        var update = await _preferenceService.SetMutedTypesAsync(token.UserId, mutedNames, cancellationToken).ConfigureAwait(false);
        if (!update.IsSuccess)
        {
            return InternalFailure();
        }

        return Success(token, type.ToString());
    }

    private async Task<IActionResult> UnsubscribeFromCategoryAsync(UnsubscribeTokenResult token, CancellationToken cancellationToken)
    {
        Result<NotificationPreference>? update = token.Value?.ToLowerInvariant() switch
        {
            "marketing" => await _preferenceService.UpdatePreferencesAsync(token.UserId, marketingEnabled: false, cancellationToken: cancellationToken).ConfigureAwait(false),
            "social" => await _preferenceService.UpdatePreferencesAsync(token.UserId, socialEnabled: false, cancellationToken: cancellationToken).ConfigureAwait(false),
            "learning" => await _preferenceService.UpdatePreferencesAsync(token.UserId, learningEnabled: false, cancellationToken: cancellationToken).ConfigureAwait(false),
            "achievements" => await _preferenceService.UpdatePreferencesAsync(token.UserId, achievementsEnabled: false, cancellationToken: cancellationToken).ConfigureAwait(false),
            _ => null
        };

        if (update is null)
        {
            return Unprocessable("Unknown or non-suppressible category; cannot unsubscribe from transactional emails.");
        }

        if (!update.IsSuccess)
        {
            return InternalFailure();
        }

        return Success(token, token.Value!.ToLowerInvariant());
    }

    private async Task<IActionResult> UnsubscribeFromAllAsync(UnsubscribeTokenResult token, CancellationToken cancellationToken)
    {
        var update = await _preferenceService.UpdatePreferencesAsync(token.UserId, emailEnabled: false, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!update.IsSuccess)
        {
            return InternalFailure();
        }

        return Success(token, null);
    }

    private OkObjectResult Success(UnsubscribeTokenResult token, string? value)
    {
        _logger.LogInformation("Unsubscribe applied. UserId: {UserId}, Scope: {Scope}, Value: {Value}", token.UserId, token.Scope, value);
        return new OkObjectResult(new UnsubscribeResponse("unsubscribed", token.Scope, value, BuildManageUrl()));
    }

    // Generic message by design: the endpoint must not reveal whether a token/user ever existed.
    // Explicit ProblemDetails (not ControllerBase.Problem) so direct unit invocation works without ProblemDetailsFactory.
    private static ObjectResult InvalidToken()
        => ProblemResult(StatusCodes.Status400BadRequest, "InvalidToken", "Invalid or malformed unsubscribe token.");

    private static ObjectResult Unprocessable(string detail)
        => ProblemResult(StatusCodes.Status422UnprocessableEntity, "NotUnsubscribable", detail);

    private static ObjectResult InternalFailure()
        => ProblemResult(StatusCodes.Status500InternalServerError, "UnsubscribeFailed", "The unsubscribe request could not be processed.");

    private static ObjectResult ProblemResult(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };

    private string BuildManageUrl()
    {
        var baseUrl = _configuration["App:BaseUrl"] ?? "http://localhost:3000";
        return $"{baseUrl.TrimEnd('/')}{ManagePath}";
    }
}

