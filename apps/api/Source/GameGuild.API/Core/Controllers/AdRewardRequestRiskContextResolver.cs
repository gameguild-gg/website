using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace GameGuild.API.Controllers;

public sealed class AdRewardRequestRiskContextResolver(
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IAdRewardRequestRiskContextResolver
{
    public const string HmacKeyConfiguration = "Economy:AdRewards:RiskContextHmacKey";
    public const string VerifiedAsnItemKey = "Economy.VerifiedAsn";

    public ValueTask<AdRewardRequestRiskContext> ResolveAsync(
        Guid tenantId,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (tenantId == Guid.Empty || actorId == Guid.Empty)
        {
            throw new AdRewardRiskContextUnavailableException("Authenticated tenant and actor are required.");
        }

        var key = ReadKey();
        try
        {
            var context = httpContextAccessor.HttpContext
                ?? throw new AdRewardRiskContextUnavailableException("The trusted request context is unavailable.");
            var sessionId = context.User.FindFirst("sid")?.Value;
            var ipAddress = context.Connection.RemoteIpAddress?.ToString();
            var asn = context.Items[VerifiedAsnItemKey] as string;
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(ipAddress) ||
                string.IsNullOrWhiteSpace(asn))
            {
                throw new AdRewardRiskContextUnavailableException(
                "A signed session, resolved client address, and verified ASN are required.");
            }

            return ValueTask.FromResult(new AdRewardRequestRiskContext(
                Hash(key, $"device|{tenantId:N}|{actorId:N}|{sessionId}"),
                Hash(key, $"ip|{tenantId:N}|{ipAddress}"),
                Hash(key, $"asn|{tenantId:N}|{asn}")));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private byte[] ReadKey()
    {
        var encoded = configuration[HmacKeyConfiguration];
        try
        {
            var key = Convert.FromBase64String(encoded ?? string.Empty);
            if (key.Length < 32)
            {
                CryptographicOperations.ZeroMemory(key);
                throw new AdRewardRiskContextUnavailableException(
                    "The AdRewards risk-context signing key is unavailable.");
            }
            return key;
        }
        catch (FormatException exception)
        {
            throw new AdRewardRiskContextUnavailableException(
                "The AdRewards risk-context signing key is invalid.", exception);
        }
    }

    private static string Hash(byte[] key, string value) =>
        Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

public sealed class AdRewardRiskContextUnavailableException : InvalidOperationException
{
    public AdRewardRiskContextUnavailableException(string message) : base(message) { }

    public AdRewardRiskContextUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }
}
