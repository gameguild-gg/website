using System.Net;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Rule that requires request IP to be within allowed CIDR ranges.
///     Parameters:
///     - cidrs: string[] - List of allowed IP ranges in CIDR notation (e.g., ["10.0.0.0/8", "192.168.1.0/24"])
///     - checkForwardedFor: bool (optional, default: true) - Whether to check X-Forwarded-For header.
///       SECURITY WARNING: Set to false if not behind a trusted reverse proxy to prevent IP spoofing.
/// </summary>
public sealed class RequireIpAllowListRuleEvaluator : IRuleEvaluator
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public RequireIpAllowListRuleEvaluator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string RuleType => RuleTypes.RequireIpAllowList;

    public Task<RuleEvaluationResult> EvaluateAsync(
        AuthorizationHandlerContext context,
        RuleParameters parameters,
        CancellationToken cancellationToken = default)
    {
        var allowedCidrs = parameters.GetStringArray("cidrs");

        if (allowedCidrs.Count == 0)
        {
            // No IP restrictions - pass
            return Task.FromResult(RuleEvaluationResult.Success());
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            return Task.FromResult(RuleEvaluationResult.Fail("No HTTP context available for IP check"));
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress;
        if (remoteIp is null)
        {
            return Task.FromResult(RuleEvaluationResult.Fail("Could not determine remote IP address"));
        }

        // Check X-Forwarded-For header if behind proxy
        if (parameters.GetBool("checkForwardedFor", true))
        {
            var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                if (IPAddress.TryParse(firstIp, out var parsedIp))
                {
                    remoteIp = parsedIp;
                }
            }
        }

        // Normalize IPv6-mapped IPv4
        if (remoteIp.IsIPv4MappedToIPv6)
        {
            remoteIp = remoteIp.MapToIPv4();
        }

        foreach (var cidr in allowedCidrs)
        {
            if (IsIpInCidr(remoteIp, cidr))
            {
                return Task.FromResult(RuleEvaluationResult.Success());
            }
        }

        return Task.FromResult(RuleEvaluationResult.Fail(
            $"IP address '{remoteIp}' is not in the allowed ranges"));
    }

    private static bool IsIpInCidr(IPAddress ip, string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out var networkAddress))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }

        // Convert both addresses to byte arrays
        var ipBytes = ip.GetAddressBytes();
        var networkBytes = networkAddress.GetAddressBytes();

        if (ipBytes.Length != networkBytes.Length)
        {
            return false;
        }

        if (prefixLength < 0 || prefixLength > ipBytes.Length * 8)
        {
            return false;
        }

        // Calculate the mask
        var maskBytes = new byte[ipBytes.Length];
        var remainingBits = prefixLength;

        for (var i = 0; i < maskBytes.Length; i++)
        {
            if (remainingBits >= 8)
            {
                maskBytes[i] = 0xFF;
                remainingBits -= 8;
            }
            else if (remainingBits > 0)
            {
                maskBytes[i] = (byte)(0xFF << (8 - remainingBits));
                remainingBits = 0;
            }
            else
            {
                maskBytes[i] = 0x00;
            }
        }

        // Apply mask and compare
        for (var i = 0; i < ipBytes.Length; i++)
        {
            if ((ipBytes[i] & maskBytes[i]) != (networkBytes[i] & maskBytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
