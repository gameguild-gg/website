using System.Text.RegularExpressions;

namespace GameGuild.Identity.Authentication;

/// <summary>
///     Dependency-free, server-side user-agent parser. Produces structured device metadata
///     (browser, operating system, device type, bot classification) from the raw
///     <c>User-Agent</c> header so sessions can record what actually connected. The parser is
///     intentionally conservative: unknown agents degrade to an "Unknown" device and parsing
///     never throws — enrichment must not be able to fail a sign-in.
/// </summary>
public sealed partial class UserAgentParser : IUserAgentParser
{
    /// <summary>Shared stateless instance (the parser is immutable and thread-safe).</summary>
    public static readonly UserAgentParser Shared = new();

    /// <inheritdoc />
    public DeviceInfo Parse(string? userAgent)
    {
        var agent = userAgent?.Trim() ?? string.Empty;
        if (agent.Length == 0)
        {
            return new DeviceInfo { DeviceName = "Unknown device", DeviceType = "Unknown" };
        }

        var isBot = IsBotAgent(agent);
        var (browser, browserVersion) = ParseBrowser(agent);
        var (operatingSystem, osVersion) = ParseOperatingSystem(agent);
        var deviceType = ParseDeviceType(agent, isBot);
        var isMobile = deviceType is DeviceTypes.Mobile or DeviceTypes.Tablet;

        return new DeviceInfo
        {
            DeviceName = BuildDeviceLabel(browser, browserVersion, operatingSystem, isBot),
            DeviceType = deviceType,
            OperatingSystem = operatingSystem,
            OsVersion = string.IsNullOrEmpty(osVersion) ? null : osVersion,
            Browser = browser,
            BrowserVersion = browserVersion,
            UserAgent = agent,
            IsMobile = isMobile,
            IsBot = isBot
        };
    }

    private static bool IsBotAgent(string agent)
    {
        return AgentContains(agent, "bot") || AgentContains(agent, "crawl") || AgentContains(agent, "spider") ||
               AgentContains(agent, "slurp") || AgentContains(agent, "curl/") || AgentContains(agent, "wget") ||
               AgentContains(agent, "httpclient") || AgentContains(agent, "python-requests") ||
               AgentContains(agent, "headless") || AgentContains(agent, "monitor");
    }

    private static (string Browser, string Version) ParseBrowser(string agent)
    {
        // Detection order matters: branded Chromium forks must be matched before generic Chrome.
        var edge = EdgeVersionRegex().Match(agent);
        if (edge.Success)
        {
            return ("Edge", edge.Groups[1].Value);
        }

        var opera = OperaVersionRegex().Match(agent);
        if (opera.Success)
        {
            return ("Opera", opera.Groups[1].Value);
        }

        if (AgentContains(agent, "opera"))
        {
            return ("Opera", string.Empty);
        }

        var samsung = SamsungVersionRegex().Match(agent);
        if (samsung.Success)
        {
            return ("Samsung Internet", samsung.Groups[1].Value);
        }

        var chrome = ChromeVersionRegex().Match(agent);
        if (chrome.Success)
        {
            return ("Chrome", chrome.Groups[1].Value);
        }

        var firefox = FirefoxVersionRegex().Match(agent);
        if (firefox.Success)
        {
            return ("Firefox", firefox.Groups[1].Value);
        }

        var safari = SafariVersionRegex().Match(agent);
        if (safari.Success)
        {
            return ("Safari", safari.Groups[1].Value);
        }

        if (AgentContains(agent, "safari"))
        {
            return ("Safari", string.Empty);
        }

        return (string.Empty, string.Empty);
    }

    private static (string OperatingSystem, string Version) ParseOperatingSystem(string agent)
    {
        var windows = WindowsVersionRegex().Match(agent);
        if (windows.Success)
        {
            return ("Windows", MapWindowsVersion(windows.Groups[1].Value));
        }

        if (AgentContains(agent, "iphone"))
        {
            var iOS = IosVersionRegex().Match(agent);
            return ("iOS", iOS.Success ? iOS.Groups[1].Value.Replace('_', '.') : string.Empty);
        }

        if (AgentContains(agent, "ipad"))
        {
            var iPadOs = IosVersionRegex().Match(agent);
            return ("iPadOS", iPadOs.Success ? iPadOs.Groups[1].Value.Replace('_', '.') : string.Empty);
        }

        if (AgentContains(agent, "mac os x") || AgentContains(agent, "macintosh"))
        {
            var macOs = MacOsVersionRegex().Match(agent);
            return ("macOS", macOs.Success ? macOs.Groups[1].Value.Replace('_', '.') : string.Empty);
        }

        var android = AndroidVersionRegex().Match(agent);
        if (android.Success)
        {
            return ("Android", android.Groups[1].Value);
        }

        if (AgentContains(agent, "cros"))
        {
            return ("ChromeOS", string.Empty);
        }

        if (AgentContains(agent, "linux"))
        {
            return ("Linux", string.Empty);
        }

        return (string.Empty, string.Empty);
    }

    private static string ParseDeviceType(string agent, bool isBot)
    {
        if (isBot)
        {
            return DeviceTypes.Bot;
        }

        if (AgentContains(agent, "ipad") || AgentContains(agent, "tablet") || AgentContains(agent, "playstation"))
        {
            return DeviceTypes.Tablet;
        }

        // "Mobi" covers Android Chrome, Mobile Safari, and Edge mobile; desktop Safari on iPads
        // that do not advertise "Mobi" fall through to Desktop, which is the accepted UA hint.
        if (AgentContains(agent, "mobi") || AgentContains(agent, "iphone") || AgentContains(agent, "android"))
        {
            return DeviceTypes.Mobile;
        }

        return DeviceTypes.Desktop;
    }

    private static string BuildDeviceLabel(string browser, string browserVersion, string operatingSystem, bool isBot)
    {
        if (isBot)
        {
            return "Automated client";
        }

        var browserLabel = string.IsNullOrEmpty(browser)
            ? "Unknown browser"
            : string.IsNullOrEmpty(browserVersion) ? browser : $"{browser} {browserVersion.Split('.')[0]}";
        return string.IsNullOrEmpty(operatingSystem)
            ? browserLabel
            : $"{browserLabel} on {operatingSystem}";
    }

    private static string MapWindowsVersion(string ntVersion)
    {
        return ntVersion switch
        {
            "10.0" or "11.0" => "10/11",
            "6.3" => "8.1",
            "6.2" => "8",
            "6.1" => "7",
            _ => ntVersion
        };
    }

    private static bool AgentContains(string agent, string token)
    {
        return agent.Contains(token, StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?:Edg|Edge|EdgA|EdgiOS)/([\d.]+)")]
    private static partial Regex EdgeVersionRegex();

    [GeneratedRegex(@"(?:OPR|Opt)/([\d.]+)")]
    private static partial Regex OperaVersionRegex();

    [GeneratedRegex(@"SamsungBrowser/([\d.]+)")]
    private static partial Regex SamsungVersionRegex();

    [GeneratedRegex(@"(?:Chrome|CriOS|CrMo)/([\d.]+)")]
    private static partial Regex ChromeVersionRegex();

    [GeneratedRegex(@"(?:Firefox|FxiOS)/([\d.]+)")]
    private static partial Regex FirefoxVersionRegex();

    [GeneratedRegex(@"Version/([\d.]+).*Safari")]
    private static partial Regex SafariVersionRegex();

    [GeneratedRegex(@"Windows NT ([\d.]+)")]
    private static partial Regex WindowsVersionRegex();

    [GeneratedRegex(@"(?:iPhone OS|CPU OS) ([\d_]+)")]
    private static partial Regex IosVersionRegex();

    [GeneratedRegex(@"Mac OS X ([\d_]+)")]
    private static partial Regex MacOsVersionRegex();

    [GeneratedRegex(@"Android ([\d.]+)")]
    private static partial Regex AndroidVersionRegex();

    private static class DeviceTypes
    {
        public const string Desktop = "Desktop";
        public const string Mobile = "Mobile";
        public const string Tablet = "Tablet";
        public const string Bot = "Bot";
        public const string Unknown = "Unknown";
    }
}
