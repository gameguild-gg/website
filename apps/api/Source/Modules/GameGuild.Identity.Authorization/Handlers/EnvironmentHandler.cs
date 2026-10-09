using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace GameGuild.Identity.Authorization;

/// <summary>
///     Handles environment requirements using ABAC (Attribute-Based Access Control).
///     Validates IP ranges, time windows (with timezone support), device types, and connection security.
/// </summary>
public sealed class EnvironmentHandler : AuthorizationHandler<EnvironmentRequirement>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EnvironmentHandler> _logger;

    /// <summary>
    ///     Initializes a new instance of <see cref="EnvironmentHandler"/>.
    /// </summary>
    public EnvironmentHandler(
        IHttpContextAccessor httpContextAccessor,
        TimeProvider timeProvider,
        ILogger<EnvironmentHandler> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EnvironmentRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("No HTTP context available for environment check");
            context.Fail(new AuthorizationFailureReason(this, "No HTTP context"));
            return Task.CompletedTask;
        }

        var constraints = requirement.Constraints;

        // Check secure connection requirement
        if (constraints.RequireSecureConnection && !httpContext.Request.IsHttps)
        {
            _logger.LogWarning("Secure connection required but request is not HTTPS");
            context.Fail(new AuthorizationFailureReason(this, "HTTPS required"));
            return Task.CompletedTask;
        }

        // Check IP restrictions
        if (constraints.AllowedIpRanges.Count > 0)
        {
            var clientIp = httpContext.Connection.RemoteIpAddress;
            if (clientIp is null || !IsIpAllowed(clientIp, constraints.AllowedIpRanges))
            {
                _logger.LogWarning("Client IP {ClientIp} not in allowed ranges", clientIp);
                context.Fail(new AuthorizationFailureReason(this, "IP address not allowed"));
                return Task.CompletedTask;
            }
        }

        // Check time window restrictions (with timezone support)
        if (constraints.AllowedTimeWindows.Count > 0)
        {
            var currentTime = _timeProvider.GetUtcNow();
            if (!IsWithinTimeWindow(currentTime, constraints.AllowedTimeWindows))
            {
                _logger.LogWarning("Request outside allowed time window");
                context.Fail(new AuthorizationFailureReason(this, "Outside allowed time window"));
                return Task.CompletedTask;
            }
        }

        // Check device type restrictions
        if (constraints.RequiredDeviceTypes.Count > 0)
        {
            var userAgent = httpContext.Request.Headers.UserAgent.ToString();
            if (!IsDeviceTypeAllowed(userAgent, constraints.RequiredDeviceTypes))
            {
                _logger.LogWarning("Device type not allowed");
                context.Fail(new AuthorizationFailureReason(this, "Device type not allowed"));
                return Task.CompletedTask;
            }
        }

        _logger.LogDebug("Environment constraints satisfied");
        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static bool IsIpAllowed(IPAddress clientIp, IReadOnlyList<string> allowedRanges)
    {
        foreach (var range in allowedRanges)
        {
            if (TryParseIpRange(range, out var network, out var prefixLength))
            {
                if (IsInRange(clientIp, network, prefixLength))
                {
                    return true;
                }
            }
            else if (IPAddress.TryParse(range, out var singleIp))
            {
                if (clientIp.Equals(singleIp))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryParseIpRange(string cidr, out IPAddress network, out int prefixLength)
    {
        network = IPAddress.None;
        prefixLength = 0;

        var parts = cidr.Split('/');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!IPAddress.TryParse(parts[0], out network!))
        {
            return false;
        }

        if (!int.TryParse(parts[1], out prefixLength))
        {
            return false;
        }

        return true;
    }

    private static bool IsInRange(IPAddress address, IPAddress network, int prefixLength)
    {
        var addressBytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();

        if (addressBytes.Length != networkBytes.Length)
        {
            return false;
        }

        var bytesToCheck = prefixLength / 8;
        var bitsToCheck = prefixLength % 8;

        for (var i = 0; i < bytesToCheck; i++)
        {
            if (addressBytes[i] != networkBytes[i])
            {
                return false;
            }
        }

        if (bitsToCheck > 0 && bytesToCheck < addressBytes.Length)
        {
            var mask = (byte)(0xFF << (8 - bitsToCheck));
            if ((addressBytes[bytesToCheck] & mask) != (networkBytes[bytesToCheck] & mask))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinTimeWindow(DateTimeOffset currentTime, IReadOnlyList<TimeWindow> timeWindows)
    {
        foreach (var window in timeWindows)
        {
            if (window.Contains(currentTime))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDeviceTypeAllowed(string userAgent, IReadOnlyList<string> allowedTypes)
    {
        var isMobile = userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
                       userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                       userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase);

        var isTablet = userAgent.Contains("Tablet", StringComparison.OrdinalIgnoreCase) ||
                       userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase);

        var isDesktop = !isMobile && !isTablet;

        foreach (var type in allowedTypes)
        {
            if (type.Equals("mobile", StringComparison.OrdinalIgnoreCase) && isMobile)
            {
                return true;
            }

            if (type.Equals("tablet", StringComparison.OrdinalIgnoreCase) && isTablet)
            {
                return true;
            }

            if (type.Equals("desktop", StringComparison.OrdinalIgnoreCase) && isDesktop)
            {
                return true;
            }
        }

        return false;
    }
}
