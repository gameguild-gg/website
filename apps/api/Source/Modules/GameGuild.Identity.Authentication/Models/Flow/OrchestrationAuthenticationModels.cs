namespace GameGuild.Identity.Authentication;

/// <summary>
///     Concrete <see cref="AuthenticationFlowState"/> returned by the authentication orchestration service.
/// </summary>
public sealed class OrchestrationAuthenticationFlowState : AuthenticationFlowState;

/// <summary>
///     Concrete <see cref="AuthenticationResult"/> returned by the authentication orchestration service.
/// </summary>
public sealed class OrchestrationAuthenticationResult : AuthenticationResult;

/// <summary>
///     Concrete <see cref="InitiateAuthenticationRequest"/> for a local (email + password)
///     primary credential carried into an orchestrated authentication flow.
/// </summary>
public sealed class LocalAuthenticationInitiationRequest : InitiateAuthenticationRequest;
