using System.Globalization;
using GameGuild.Configuration.PresentationLayer.ApiVersioning;

namespace GameGuild.SharedKernel.UnitTests.Configuration;

public sealed class SemanticApiVersionFormattingTests
{
    [Theory]
    [InlineData(null, "1.2.3-alpha.1+build.7")]
    [InlineData("", "1.2.3-alpha.1+build.7")]
    [InlineData("'Version-'VVV", "Version-1.2.3-alpha.1+build.7")]
    [InlineData("\"Version-\"VVV", "Version-1.2.3-alpha.1+build.7")]
    [InlineData("'v'GVVV", "v1.2.3-alpha.1+build.7")]
    [InlineData("'v'GGGGVVV", "v1.2.3-alpha.1+build.7")]
    [InlineData("'all'", "all")]
    [InlineData("'VVV' VVV", "VVV 1.2.3-alpha.1+build.7")]
    [InlineData("VVV' / 'VVV", "1.2.3-alpha.1+build.7 / 1.2.3-alpha.1+build.7")]
    [InlineData("F", "1.2.3-alpha.1+build.7")]
    [InlineData("FF", "1.2.3-alpha.1+build.7")]
    [InlineData("V", "1")]
    [InlineData("VV", "1.2")]
    [InlineData("VVV", "1.2.3-alpha.1+build.7")]
    [InlineData("VVVV", "1.2.3-alpha.1+build.7")]
    [InlineData("v", "2")]
    [InlineData("S", "alpha.1")]
    [InlineData("P3'.'p3", "001.002")]
    [InlineData("PP", "01.02")]
    [InlineData("PPP", "01.02-alpha.1")]
    [InlineData("' ('S')'", " (alpha.1)")]
    [InlineData("G", "")]
    public void Formatting_obeys_literals_and_native_component_tokens_without_exposing_internal_status(
        string? format, string expected)
    {
        var version = new SemanticApiVersion(1, 2, 3, "alpha.1", "build.7");

        Assert.Equal(expected, version.ToString(format));
        Assert.Equal(expected, version.ToString(format, CultureInfo.InvariantCulture));
        Assert.Equal(expected, ((IFormattable)version).ToString(format, CultureInfo.GetCultureInfo("pt-BR")));

        var buffer = new char[256];
        Array.Fill(buffer, '#');
        Assert.True(((ISpanFormattable)version).TryFormat(buffer, out var written, format.AsSpan(), CultureInfo.InvariantCulture));
        Assert.Equal(expected, new string(buffer, 0, written));
        Assert.Equal('#', buffer[written]);
        if (expected.Length > 0)
        {
            var small = new char[expected.Length - 1];
            Array.Fill(small, '#');
            Assert.False(version.TryFormat(small, out var insufficientWritten, format.AsSpan(), CultureInfo.InvariantCulture));
            Assert.Equal(0, insufficientWritten);
            Assert.All(small, character => Assert.Equal('#', character));
        }
    }

    [Theory]
    [InlineData("VVV", "1.2.0")]
    [InlineData("F", "1.2.0")]
    [InlineData("'Version-'GVVV", "Version-1.2.0")]
    [InlineData("S", "")]
    public void Patch_zero_and_absent_prerelease_keep_semantic_identity(string format, string expected)
    {
        Assert.Equal(expected, new SemanticApiVersion(1, 2, 0).ToString(format));
    }

    [Theory]
    [InlineData("1.2.0-1", "1")]
    [InlineData("1.2.0-rc-1", "rc-1")]
    [InlineData("1.2.3-1", "1")]
    [InlineData("1.2.3-rc-1", "rc-1")]
    public void Legal_numeric_and_hyphenated_prerelease_labels_survive_component_projections(string text, string prerelease)
    {
        var version = SemanticApiVersionParser.Instance.Parse(text.AsSpan());

        Assert.Equal("Version-" + text, version.ToString("'Version-'GVVV"));
        Assert.Equal("1.2", version.ToString("VV"));
        Assert.Equal(prerelease, version.ToString("S"));
        Assert.Equal("01.02-" + prerelease, version.ToString("PPP"));
    }

    [Fact]
    public void Interpolated_strings_use_the_complete_semantic_version_and_quoted_literals()
    {
        var version = new SemanticApiVersion(1, 2, 3, "alpha.1", "build.7");

        Assert.Equal("Version-1.2.3-alpha.1+build.7",
            string.Create(CultureInfo.InvariantCulture, $"{version:'Version-'GVVV}"));
        Assert.Equal("1.2.3-alpha.1+build.7",
            string.Create(CultureInfo.InvariantCulture, $"{version}"));
    }
}
