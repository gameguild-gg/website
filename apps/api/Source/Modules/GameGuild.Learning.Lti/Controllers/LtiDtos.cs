namespace GameGuild.Learning.Lti;

/// <summary>Admin request to register a platform deployment. PrivateKeyPem is accepted on write and never returned.</summary>
public sealed record CreateLtiDeploymentRequest(
    string Issuer,
    string ClientId,
    string DeploymentId,
    string AuthTokenUrl,
    string PlatformJwksUrl,
    string AuthorizationUrl,
    string KeyId,
    string PrivateKeyPem,
    bool Active = true);

/// <summary>Deployment response — deliberately excludes PrivateKeyPem.</summary>
public sealed record LtiDeploymentDto(
    Guid Id,
    string Issuer,
    string ClientId,
    string DeploymentId,
    string AuthTokenUrl,
    string PlatformJwksUrl,
    string AuthorizationUrl,
    string KeyId,
    bool Active)
{
    public static LtiDeploymentDto FromEntity(LtiDeployment d) =>
        new(d.Id, d.Issuer, d.ClientId, d.DeploymentId, d.AuthTokenUrl, d.PlatformJwksUrl, d.AuthorizationUrl, d.KeyId, d.Active);
}

/// <summary>Admin request to link an assessment to a platform AGS line item.</summary>
public sealed record CreateLtiLineItemRequest(
    Guid AssessmentId,
    string LineItemId,
    string LineItemUrl,
    int MaxScore);

public sealed record LtiLineItemMappingDto(
    Guid Id,
    Guid AssessmentId,
    Guid DeploymentId,
    string LineItemId,
    string LineItemUrl,
    int MaxScore)
{
    public static LtiLineItemMappingDto FromEntity(LtiLineItemMapping m) =>
        new(m.Id, m.AssessmentId, m.DeploymentId, m.LineItemId, m.LineItemUrl, m.MaxScore);
}
