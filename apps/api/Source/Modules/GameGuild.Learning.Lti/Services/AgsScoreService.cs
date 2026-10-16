using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GameGuild.Learning.Assessments;
using GameGuild.Learning.Assessments.Grading.Contracts;
using GameGuild.Learning.Grading.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.Learning.Lti;

/// <summary>
/// AGS score passback for a released grade. Technical failures propagate to the
/// academic outbox consumer; absence of an assessment or user mapping is a valid no-op.
/// </summary>
public sealed class AgsScoreService(
    IApplicationDbContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<AgsScoreService> logger) : ILtiScorePassback
{
    private const string AgsScoreScope = "https://purl.imsglobal.org/spec/lti-ags/scope/score";

    public async Task<LtiScorePassbackOutcome> PostScoreIfMappedAsync(
        Guid assessmentId,
        Guid userId,
        ScoreValue score,
        ScoreValue maxScore,
        CancellationToken cancellationToken = default)
    {
        var mapping = await context.Set<LtiLineItemMapping>()
            .FirstOrDefaultAsync(m => m.AssessmentId == assessmentId, cancellationToken)
            .ConfigureAwait(false);
        if (mapping is null)
        {
            return LtiScorePassbackOutcome.NotMapped;
        }

        var deployment = await context.Set<LtiDeployment>()
            .FirstOrDefaultAsync(d => d.Id == mapping.DeploymentId && d.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
        if (deployment is null)
        {
            throw new InvalidOperationException(
                $"LTI line item mapping {mapping.Id} references missing deployment {mapping.DeploymentId}.");
        }

        var userMapping = await context.Set<LtiUserMapping>()
            .FirstOrDefaultAsync(
                u => u.DeploymentId == mapping.DeploymentId && u.UserId == userId,
                cancellationToken)
            .ConfigureAwait(false);
        if (userMapping is null)
        {
            logger.LogInformation(
                "LTI AGS: user {UserId} has no platform sub for deployment {DeploymentId}; skipping score passback",
                userId,
                mapping.DeploymentId);
            return LtiScorePassbackOutcome.UserNotMapped;
        }

        using var client = httpClientFactory.CreateClient(LtiModule.HttpClientName);
        var accessToken = await GetPlatformAccessTokenAsync(client, deployment, cancellationToken).ConfigureAwait(false);
        await PostScoreAsync(
                client,
                mapping,
                userMapping.Sub,
                score,
                maxScore,
                accessToken,
                cancellationToken)
            .ConfigureAwait(false);
        return LtiScorePassbackOutcome.Delivered;
    }

    private async Task<string> GetPlatformAccessTokenAsync(
        HttpClient client,
        LtiDeployment deployment,
        CancellationToken cancellationToken)
    {
        // private_key_jwt client assertion signed with the tool key (RS256, kid = deployment.KeyId).
        var now = DateTime.UtcNow;
        var assertion = new JwtSecurityTokenHandler().CreateEncodedJwt(
            issuer: deployment.ClientId,
            audience: deployment.AuthTokenUrl,
            subject: new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, deployment.ClientId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            ]),
            notBefore: now.AddSeconds(-30),
            expires: now.AddMinutes(5),
            issuedAt: now,
            signingCredentials: new SigningCredentials(LoadToolKey(deployment), SecurityAlgorithms.RsaSha256));

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_assertion_type"] = "urn:ietf:params:oauth:client-assertion-type:jwt-bearer",
            ["client_assertion"] = assertion,
            ["scope"] = AgsScoreScope
        });

        using var response = await client.PostAsync(deployment.AuthTokenUrl, form, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LTI AGS token request returned {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var token) && !string.IsNullOrWhiteSpace(token.GetString())
            ? token.GetString()!
            : throw new JsonException("LTI AGS token response did not contain an access token.");
    }

    private async Task PostScoreAsync(
        HttpClient client,
        LtiLineItemMapping mapping,
        string sub,
        ScoreValue score,
        ScoreValue maxScore,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var scoreUrl = mapping.LineItemUrl.TrimEnd('/') + "/scores";
        using var payloadStream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(payloadStream))
        {
            writer.WriteStartObject();
            writer.WriteString("userId", sub);
            writer.WritePropertyName("scoreGiven");
            writer.WriteRawValue(ToJsonNumber(score));
            writer.WritePropertyName("scoreMaximum");
            writer.WriteRawValue(ToJsonNumber(maxScore));
            writer.WriteString("activityProgress", "Completed");
            writer.WriteString("gradingProgress", "FullyGraded");
            writer.WriteEndObject();
        }
        var payload = Encoding.UTF8.GetString(payloadStream.ToArray());

        using var request = new HttpRequestMessage(HttpMethod.Post, scoreUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"LTI AGS score request returned {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }
    }

    private static string ToJsonNumber(ScoreValue value)
        => value.ToPointsString();

    private static RsaSecurityKey LoadToolKey(LtiDeployment deployment)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(deployment.PrivateKeyPem);
        // Export parameters so the key owns its material (no dependence on rsa's lifetime).
        return new RsaSecurityKey(rsa.ExportParameters(true)) { KeyId = deployment.KeyId };
    }
}
