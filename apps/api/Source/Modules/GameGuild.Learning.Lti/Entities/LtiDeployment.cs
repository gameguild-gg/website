namespace GameGuild.Learning.Lti;

/// <summary>
/// A registered LTI 1.3 platform (e.g. a Canvas installation) this tool can be launched from.
/// The (Issuer, ClientId, DeploymentId) trio identifies one tool deployment on one platform.
/// </summary>
public class LtiDeployment : EntityBase
{
    public string Issuer { get; private set; } = string.Empty;
    public string ClientId { get; private set; } = string.Empty;
    public string DeploymentId { get; private set; } = string.Empty;

    /// <summary>Platform OAuth2 token endpoint used for AGS client_credentials + private_key_jwt.</summary>
    public string AuthTokenUrl { get; private set; } = string.Empty;

    /// <summary>Platform JWKS endpoint used to validate launch id_tokens.</summary>
    public string PlatformJwksUrl { get; private set; } = string.Empty;

    /// <summary>Platform OIDC authorization endpoint the login redirect targets (admin-configured, never token-supplied).</summary>
    public string AuthorizationUrl { get; private set; } = string.Empty;

    public string KeyId { get; private set; } = string.Empty;

    /// <summary>Tool private key (PKCS#8 PEM) used to sign the tool JWKS and AGS client assertions. Secret — never return or log.</summary>
    public string PrivateKeyPem { get; private set; } = string.Empty;

    public bool Active { get; private set; }

    private LtiDeployment() { } // EF Core

    public static LtiDeployment Create(
        string issuer,
        string clientId,
        string deploymentId,
        string authTokenUrl,
        string platformJwksUrl,
        string authorizationUrl,
        string keyId,
        string privateKeyPem,
        bool active = true)
    {
        if (string.IsNullOrWhiteSpace(issuer)) throw new ArgumentException("Issuer is required.", nameof(issuer));
        if (string.IsNullOrWhiteSpace(clientId)) throw new ArgumentException("ClientId is required.", nameof(clientId));
        if (string.IsNullOrWhiteSpace(deploymentId)) throw new ArgumentException("DeploymentId is required.", nameof(deploymentId));
        if (string.IsNullOrWhiteSpace(authTokenUrl)) throw new ArgumentException("AuthTokenUrl is required.", nameof(authTokenUrl));
        if (string.IsNullOrWhiteSpace(platformJwksUrl)) throw new ArgumentException("PlatformJwksUrl is required.", nameof(platformJwksUrl));
        if (string.IsNullOrWhiteSpace(authorizationUrl)) throw new ArgumentException("AuthorizationUrl is required.", nameof(authorizationUrl));
        if (string.IsNullOrWhiteSpace(keyId)) throw new ArgumentException("KeyId is required.", nameof(keyId));
        if (string.IsNullOrWhiteSpace(privateKeyPem)) throw new ArgumentException("PrivateKeyPem is required.", nameof(privateKeyPem));

        return new LtiDeployment
        {
            Id = Guid.NewGuid(),
            Issuer = issuer.Trim(),
            ClientId = clientId.Trim(),
            DeploymentId = deploymentId.Trim(),
            AuthTokenUrl = authTokenUrl.Trim(),
            PlatformJwksUrl = platformJwksUrl.Trim(),
            AuthorizationUrl = authorizationUrl.Trim(),
            KeyId = keyId.Trim(),
            PrivateKeyPem = privateKeyPem,
            Active = active
        };
    }
}
