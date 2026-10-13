using FluentAssertions;
using GameGuild.Identity.Authentication;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

/// <summary>
///     Server-side user-agent parsing for session device metadata (issue #278). The parser is
///     dependency-free and must degrade instead of throwing for any input.
/// </summary>
public class UserAgentParserTests
{
    private readonly UserAgentParser _sut = UserAgentParser.Shared;

    [Fact]
    public void Parse_NullAgent_DegradesToUnknownDevice()
    {
        var result = _sut.Parse(null);

        result.DeviceType.Should().Be("Unknown");
        result.DeviceName.Should().Be("Unknown device");
        result.IsBot.Should().BeFalse();
    }

    [Fact]
    public void Parse_EmptyAgent_DegradesToUnknownDevice()
    {
        var result = _sut.Parse("   ");

        result.DeviceType.Should().Be("Unknown");
    }

    [Fact]
    public void Parse_ChromeOnWindows_IsDesktopWithBrowserAndOsVersions()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.6099.130 Safari/537.36");

        result.DeviceType.Should().Be("Desktop");
        result.IsMobile.Should().BeFalse();
        result.Browser.Should().Be("Chrome");
        result.BrowserVersion.Should().Be("120.0.6099.130");
        result.OperatingSystem.Should().Be("Windows");
        result.OsVersion.Should().Be("10/11");
        result.DeviceName.Should().Be("Chrome 120 on Windows");
    }

    [Fact]
    public void Parse_EdgeOnWindows_DetectsEdgeBeforeChrome()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 Edg/119.0.2151.97");

        result.Browser.Should().Be("Edge");
        result.BrowserVersion.Should().Be("119.0.2151.97");
        result.OperatingSystem.Should().Be("Windows");
    }

    [Fact]
    public void Parse_SafariOnIPhone_IsMobileWithIOS()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (iPhone; CPU iPhone OS 17_1 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.1 Mobile/15E148 Safari/604.1");

        result.DeviceType.Should().Be("Mobile");
        result.IsMobile.Should().BeTrue();
        result.Browser.Should().Be("Safari");
        result.OperatingSystem.Should().Be("iOS");
        result.OsVersion.Should().Be("17.1");
    }

    [Fact]
    public void Parse_IPad_IsTablet()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (iPad; CPU OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1");

        result.DeviceType.Should().Be("Tablet");
        result.OperatingSystem.Should().Be("iPadOS");
        result.IsMobile.Should().BeTrue();
    }

    [Fact]
    public void Parse_FirefoxOnAndroid_IsMobileWithAndroidVersion()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (Android 14; Mobile; rv:121.0) Gecko/121.0 Firefox/121.0");

        result.DeviceType.Should().Be("Mobile");
        result.Browser.Should().Be("Firefox");
        result.OperatingSystem.Should().Be("Android");
        result.OsVersion.Should().Be("14");
    }

    [Fact]
    public void Parse_FirefoxOnMacOS_DetectsMacOsVersion()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_2) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

        result.OperatingSystem.Should().Be("macOS");
        result.OsVersion.Should().Be("14.2");
        result.DeviceType.Should().Be("Desktop");
    }

    [Fact]
    public void Parse_Opera_IsDetectedBeforeChrome()
    {
        var result = _sut.Parse(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36 OPR/105.0.0.0");

        result.Browser.Should().Be("Opera");
    }

    [Fact]
    public void Parse_BotAgent_IsClassifiedAsBot()
    {
        var result = _sut.Parse("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)");

        result.IsBot.Should().BeTrue();
        result.DeviceType.Should().Be("Bot");
        result.DeviceName.Should().Be("Automated client");
    }

    [Fact]
    public void Parse_HttpClientAgent_IsClassifiedAsBot()
    {
        var result = _sut.Parse("Mozilla/5.0 (Windows NT 10.0; Win64; x64) HttpClient/8.0");

        result.IsBot.Should().BeTrue();
    }

    [Fact]
    public void Parse_UnknownAgent_DoesNotThrowAndHasNoBrowser()
    {
        var result = _sut.Parse("some-internal-client/1.2");

        result.Browser.Should().BeNullOrEmpty();
        result.DeviceType.Should().Be("Desktop");
        result.DeviceName.Should().Contain("Unknown browser");
    }
}
