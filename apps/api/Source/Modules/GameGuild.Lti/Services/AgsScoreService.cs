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

namespace GameGuild.Lti;

/// <summary>
/// AGS score passback: on instructor grade release, posts the score to the platform
/// line item mapped to the assessment. Fire-and-log — failures never propagate to grading.
/// </summary>
public sealed class AgsScoreService(
    IApplicationDbContext context,
    IHttpClientFactory httpClientFactory,
    ILogger<AgsScoreService> logger) : ILtiScorePassback
{
    private const string AgsScoreScope = "https://purl.imsglobal.org/spec/lti-ags/scope/score";

    public async Task PostScoreIfMappedAsync(Guid assessmentId, Guid userId, ScoreValue score, ScoreValue maxScore)
    {
        try
        {
            var mapping = await context.Set<LtiLineItemMapping>()
                .FirstOrDefaultAsync(m => m.AssessmentId == assessmentId)
                .ConfigureAwait(false);
            if (mapping is null)
            {
                return; // assessment not linked to a line item — nothing to pass back
            }

            var deployment = await context.Set<LtiDeployment>()
                .FirstOrDefaultAsync(d => d.Id == mapping.DeploymentId && d.DeletedAt == null)
                .ConfigureAwait(false);
            if (deployment is null)
            {
                logger.LogWarning("LTI AGS: line item mapping {MappingId} references missing deployment {DeploymentId}", mapping.Id, mapping.DeploymentId);
                return;
            }

            var userMapping = await context.Set<LtiUserMapping>()
                .FirstOrDefaultAsync(u => u.DeploymentId == mapping.DeploymentId && u.UserId == userId)
                .ConfigureAwait(false);
            if (userMapping is null)
            {
                logger.LogInformation("LTI AGS: user {UserId} has no platform sub for deployment {DeploymentId}; skipping score passback", userId, mapping.DeploymentId);
                return;
            }

            using var client = httpClientFactory.CreateClient(LtiModule.HttpClientName);
            var accessToken = await GetPlatformAccessTokenAsync(client, deployment).ConfigureAwait(false);
            if (accessToken is null)
            {
                return;
            }

            await PostScoreAsync(client, mapping, userMapping.Sub, score, maxScore, accessToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Never throw to the grading caller — a slow/broken platform must not fail the grade.
            logger.LogError(ex, "LTI AGS: score passback failed for assessment {AssessmentId} user {UserId}", assessmentId, userId);
        }
    }

    private async Task<string?> GetPlatformAccessTokenAsync(HttpClient client, LtiDeployment deployment)
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

        using var response = await client.PostAsync(deployment.AuthTokenUrl, form).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("LTI AGS: token request to {TokenUrl} returned {StatusCode}", deployment.AuthTokenUrl, (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var token) ? token.GetString() : null;
    }

    private async Task PostScoreAsync(
        HttpClient client,
        LtiLineItemMapping mapping,
        string sub,
        ScoreValue score,
        ScoreValue maxScore,
        string accessToken)
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

        using var response = await client.SendAsync(request).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("LTI AGS: score POST to {ScoreUrl} returned {StatusCode}", scoreUrl, (int)response.StatusCode);
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
