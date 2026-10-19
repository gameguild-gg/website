using FluentAssertions;
using Xunit;

namespace GameGuild.SharedKernel.UnitTests;

public class LogRedactionTests
{
    [Theory]
    [InlineData("alice@private-organization.example", "a***@p***.example")]
    [InlineData("alice@example.com", "a***@e***.com")]
    [InlineData("Bob.Example@MailServer.org", "b***@m***.org")]
    [InlineData("a@example.com", "a***@e***.com")]
    public void MaskEmail_ShouldFingerprintTheWholeAddressWithoutKeepingTheLegacyMask(string email, string legacyMask)
    {
        LogRedaction.MaskEmail(email).Should().MatchRegex("^email:[0-9a-f]{64}$")
            .And.NotBe(legacyMask).And.NotContain(email).And.NotContain("@");
    }

    [Theory]
    [InlineData("alice@example.com\r\nFORGED EVENT")]
    [InlineData("alice@example.com\u0085FORGED EVENT")]
    [InlineData("alice@example.com\u2028FORGED EVENT")]
    public void MaskEmail_ShouldNotCarryControlCharactersIntoOutput(string email)
    {
        var masked = LogRedaction.MaskEmail(email);
        masked.Should().NotContainAny("\r", "\n", "\u0085", "\u2028");
    }

    [Fact]
    public void MaskEmail_ShouldBeDeterministic()
    {
        LogRedaction.MaskEmail("alice@example.com").Should().Be(LogRedaction.MaskEmail("alice@example.com"));
    }

    [Fact]
    public void MaskEmail_ShouldDistinguishDifferentAddresses()
    {
        LogRedaction.MaskEmail("alice@example.com").Should().NotBe(LogRedaction.MaskEmail("bob@example.com"));
        LogRedaction.MaskEmail("alice@example.com").Should().NotBe(LogRedaction.MaskEmail("alice@otherdomain.com"));
    }

    [Fact]
    public void MaskEmail_ShouldNotExposeAnyOtherPartOfTheAddress()
    {
        var masked = LogRedaction.MaskEmail("alice.secret@example.com");
        masked.Should().NotContain("secret");
        masked.Should().NotContain("xample");
    }

    [Fact]
    public void MaskEmail_NullOrEmpty_ShouldReturnNone()
    {
        LogRedaction.MaskEmail(null).Should().Be("none");
        LogRedaction.MaskEmail("").Should().Be("none");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("alice@")]
    public void MaskEmail_MalformedOrUnsafeLeadingChars_ShouldReturnInvalid(string email)
    {
        LogRedaction.MaskEmail(email).Should().Be("invalid");
    }

    [Fact]
    public void MaskEmail_DomainWithoutSuffix_ShouldStillProduceAnOpaqueFingerprint()
    {
        LogRedaction.MaskEmail("alice@localhost").Should().MatchRegex("^email:[0-9a-f]{64}$")
            .And.NotContain("localhost").And.NotBe("a***@l***");
    }

    [Fact]
    public void RedactSecret_ShouldNotExposeADictionaryCheckableFingerprint()
    {
        LogRedaction.RedactSecret("password").Should().Be("secret:redacted");
        LogRedaction.RedactSecret("different-password").Should().Be("secret:redacted");
    }

    [Fact]
    public void Sanitize_ShouldReplaceUnicodeLineSeparatorsAndC1Controls()
    {
        LogRedaction.Sanitize("a\u007Fb\u0085c\u009Fd\u2028e\u2029f").Should().Be("a␀b␀c␀d␀e␀f");
    }

    [Fact]
    public void Sanitize_ShouldReplaceEveryC0AndC1ControlAndUnicodeLineSeparator()
    {
        var controls = Enumerable.Range(0, 32).Concat(Enumerable.Range(127, 33)).Concat([0x2028, 0x2029]);
        var input = string.Concat(controls.Select(codePoint => (char)codePoint));

        var sanitized = LogRedaction.Sanitize(input);

        sanitized.Should().Be(new string('␀', 67));
        input.Should().HaveLength(67);
        LogRedaction.Sanitize(sanitized).Should().Be(sanitized);
    }

    [Fact]
    public void RedactId_Guid_ShouldReturnPrefixedHash()
    {
        var id = Guid.NewGuid();
        var result = LogRedaction.RedactId(id);

        result.Should().StartWith("tid:");
        result.Should().HaveLength(12); // "tid:" + 8 hex chars
    }

    [Fact]
    public void RedactId_Guid_ShouldBeDeterministic()
    {
        var id = Guid.NewGuid();
        var result1 = LogRedaction.RedactId(id);
        var result2 = LogRedaction.RedactId(id);

        result1.Should().Be(result2);
    }

    [Fact]
    public void RedactId_Guid_Null_ShouldReturnNone()
    {
        LogRedaction.RedactId((Guid?)null).Should().Be("none");
    }

    [Fact]
    public void RedactId_Guid_Empty_ShouldReturnNone()
    {
        LogRedaction.RedactId(Guid.Empty).Should().Be("none");
    }

    [Fact]
    public void RedactId_Guid_CustomPrefix_ShouldUseIt()
    {
        var id = Guid.NewGuid();
        var result = LogRedaction.RedactId(id, "usr");

        result.Should().StartWith("usr:");
    }

    [Fact]
    public void RedactId_String_ShouldReturnPrefixedHash()
    {
        var result = LogRedaction.RedactId("user-123");

        result.Should().StartWith("uid:");
        result.Should().HaveLength(12); // "uid:" + 8 hex chars
    }

    [Fact]
    public void RedactId_String_Null_ShouldReturnNone()
    {
        LogRedaction.RedactId((string?)null).Should().Be("none");
    }

    [Fact]
    public void RedactId_String_Empty_ShouldReturnNone()
    {
        LogRedaction.RedactId("").Should().Be("none");
    }

    [Fact]
    public void RedactId_String_CustomPrefix_ShouldUseIt()
    {
        var result = LogRedaction.RedactId("test", "sub");

        result.Should().StartWith("sub:");
    }

    [Fact]
    public void RedactId_DifferentGuids_ShouldProduceDifferentHashes()
    {
        var hash1 = LogRedaction.RedactId(Guid.NewGuid());
        var hash2 = LogRedaction.RedactId(Guid.NewGuid());

        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void RedactSecret_ShouldReturnConstantRedactionMarker()
    {
        var result = LogRedaction.RedactSecret("super-secret-token");

        result.Should().Be("secret:redacted");
    }

    [Fact]
    public void RedactSecret_ShouldBeDeterministic()
    {
        LogRedaction.RedactSecret("same-token").Should().Be(LogRedaction.RedactSecret("same-token"));
    }

    [Fact]
    public void RedactSecret_NullOrEmpty_ShouldReturnNone()
    {
        LogRedaction.RedactSecret(null).Should().Be("none");
        LogRedaction.RedactSecret("").Should().Be("none");
    }

    [Fact]
    public void RedactSecret_ShouldNotContainOriginalValue()
    {
        var result = LogRedaction.RedactSecret("hunter2-password");

        result.Should().NotContain("hunter2");
    }

    [Fact]
    public void Sanitize_ShouldReplaceNewlinesWithVisibleMarker()
    {
        LogRedaction.Sanitize("first line\r\nsecond line\nthird").Should().Be("first line␀␀second line␀third");
    }

    [Fact]
    public void Sanitize_ShouldReplaceAllControlCharacters()
    {
        LogRedaction.Sanitize("a\u0000b\u001Fc").Should().Be("a␀b␀c");
    }

    [Fact]
    public void Sanitize_NullOrEmpty_ShouldReturnEmpty()
    {
        LogRedaction.Sanitize(null).Should().BeEmpty();
        LogRedaction.Sanitize("").Should().BeEmpty();
    }

    [Fact]
    public void Sanitize_CleanText_ShouldRemainUnchanged()
    {
        LogRedaction.Sanitize("normal log text").Should().Be("normal log text");
    }

    [Theory]
    [InlineData("alice@example.com", "attacker@example.com")] // same domain, different local part
    public void MaskUsername_ShouldDistinguishDifferentNames(string first, string second)
    {
        LogRedaction.MaskUsername(first).Should().NotBe(LogRedaction.MaskUsername(second));
    }

    [Fact]
    public void MaskUsername_ShouldNotKeepTheFirstCharacterOrLengthMarker()
    {
        LogRedaction.MaskUsername("alice").Should().MatchRegex("^username:[0-9a-f]{64}$").And.NotBe("a***(5)");
        LogRedaction.MaskUsername("bobby-tables").Should().MatchRegex("^username:[0-9a-f]{64}$").And.NotBe("b***(12)");
        LogRedaction.MaskUsername("attacker").Should().MatchRegex("^username:[0-9a-f]{64}$").And.NotBe("a***(8)");
    }

    [Fact]
    public void MaskUsername_ShouldBeDeterministic()
    {
        LogRedaction.MaskUsername("carol").Should().Be(LogRedaction.MaskUsername("carol"));
        LogRedaction.MaskUsername("carol").Should().NotBe(LogRedaction.MaskUsername("cora"));
    }

    [Fact]
    public void MaskUsername_ShouldNotExposeTheRestOfTheName()
    {
        LogRedaction.MaskUsername("secret-handle").Should().NotContain("ecret");
        LogRedaction.MaskUsername("secret-handle").Should().NotContain("andle");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskUsername_NullOrEmpty_ShouldReturnNone(string? username)
    {
        LogRedaction.MaskUsername(username).Should().Be("none");
    }

    [Theory]
    [InlineData(".hidden")]
    [InlineData("-dashed")]
    public void MaskUsername_UnsafeLeadingChar_ShouldReturnInvalid(string username)
    {
        LogRedaction.MaskUsername(username).Should().Be("invalid");
    }

    [Fact]
    public void MaskIpAddress_ShouldFingerprintTheNetworkWithoutCopyingOctets()
    {
        LogRedaction.MaskIpAddress("10.20.0.30").Should().MatchRegex("^ip-network:[0-9a-f]{64}$").And.NotBe("10.x.0.x");
        LogRedaction.MaskIpAddress("192.168.1.1").Should().MatchRegex("^ip-network:[0-9a-f]{64}$").And.NotBe("192.x.1.x");
    }

    [Fact]
    public void MaskIpAddress_ShouldPreserveNetworkCorrelation()
    {
        LogRedaction.MaskIpAddress("10.20.0.30").Should().Be(LogRedaction.MaskIpAddress("10.99.0.77"));
        LogRedaction.MaskIpAddress("10.20.0.30").Should().NotBe(LogRedaction.MaskIpAddress("11.20.0.30"));
    }

    [Fact]
    public void MaskIpAddress_NonIpv4_ShouldFallBackToDeterministicHash()
    {
        var masked = LogRedaction.MaskIpAddress("2001:db8::1");
        masked.Should().MatchRegex("^ip:[0-9a-f]{64}$");
        masked.Should().Be(LogRedaction.MaskIpAddress("2001:db8::1"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskIpAddress_NullOrEmpty_ShouldReturnNone(string? ipAddress)
    {
        LogRedaction.MaskIpAddress(ipAddress).Should().Be("none");
    }

    [Theory]
    [InlineData("999.1.1.1")]
    [InlineData("10.1.1")]
    [InlineData("10.1.1.1.1")]
    [InlineData("10.a.1.1")]
    public void MaskIpAddress_MalformedIpv4_ShouldNotKeepOctets(string ipAddress)
    {
        LogRedaction.MaskIpAddress(ipAddress).Should().NotMatch("?*.x.?.x");
    }

    [Theory]
    [InlineData("alice@example.com", "a***@e***.com")]
    [InlineData("10.20.0.30", "10.x.0.x")]
    [InlineData("attacker", "a***(8)")]
    public void MaskIdentifier_ShouldDispatchByShapeWithoutKeepingTheLegacyMask(string identifier, string legacyMask)
    {
        var expected = identifier.Contains('@') ? LogRedaction.MaskEmail(identifier)
            : identifier.Contains('.') ? LogRedaction.MaskIpAddress(identifier)
            : LogRedaction.MaskUsername(identifier);
        LogRedaction.MaskIdentifier(identifier).Should().Be(expected)
            .And.MatchRegex("^(email|ip-network|username):[0-9a-f]{64}$").And.NotBe(legacyMask);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void MaskIdentifier_NullOrEmpty_ShouldReturnNone(string? identifier)
    {
        LogRedaction.MaskIdentifier(identifier).Should().Be("none");
    }

    [Fact]
    public void MaskIdentifier_ShouldBeDeterministic()
    {
        LogRedaction.MaskIdentifier("alice@example.com").Should().Be(LogRedaction.MaskIdentifier("alice@example.com"));
        LogRedaction.MaskIdentifier("attacker").Should().Be(LogRedaction.MaskIdentifier("attacker"));
    }

    [Theory]
    [InlineData("private-person@private-organization.secretxy")]
    [InlineData("private-person@private-organization.12345678")]
    [InlineData("private-person@private-organization.秘密情報")]
    [InlineData("private-person@private-organization.secretxy\r\nFORGED")]
    public void MaskEmail_ShouldNotCopyAnArbitraryPrivateSuffix(string email)
    {
        LogRedaction.MaskEmail(email).Should().MatchRegex("^email:[0-9a-f]{64}$")
            .And.NotContain(email).And.NotContain("@")
            .And.NotContainAny("private-person", "private-organization", "secretxy", "秘密情報", "FORGED", "\r", "\n");
    }

    [Fact]
    public void MaskUsername_ShouldDistinguishEqualLengthNamesWithTheSameFirstCharacter()
    {
        LogRedaction.MaskUsername("alice").Should().NotBe(LogRedaction.MaskUsername("amora"));
    }

    [Fact]
    public void Fingerprints_ShouldSeparateIdentifierCategoriesAndNormalizeEmailCase()
    {
        const string email = "alice@example.com";
        LogRedaction.MaskEmail(email).Should().Be(LogRedaction.MaskEmail(email.ToUpperInvariant()));
        LogRedaction.MaskEmail(email).Split(':')[1].Should().NotBe(LogRedaction.MaskUsername(email).Split(':')[1]);
    }

    [Fact]
    public async Task Fingerprints_ShouldRemainStableDuringConcurrentLogging()
    {
        var expected = LogRedaction.MaskEmail("private-person@example.test");
        var values = await Task.WhenAll(Enumerable.Range(0, 64)
            .Select(_ => Task.Run(() => LogRedaction.MaskEmail("private-person@example.test"))));
        values.Should().OnlyContain(value => value == expected);
    }
}
