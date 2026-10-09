namespace GameGuild.Identity.Authentication;

/// <summary>
///     Concrete <see cref="AuthenticationFlowState"/> returned by the authentication orchestration service.
/// </summary>
public sealed class OrchestrationAuthenticationFlowState : AuthenticationFlowState;

/// <summary>
///     Concrete <see cref="AuthenticationResult"/> returned by the authentication orchestration service.
/// </summary>
public sealed class OrchestrationAuthenticationResult : AuthenticationResult;
