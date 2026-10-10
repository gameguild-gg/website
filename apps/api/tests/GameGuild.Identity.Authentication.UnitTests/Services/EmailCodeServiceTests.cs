using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class EmailCodeServiceTests : IDisposable
{
    private const string Email = "code-owner@example.test";

    private readonly MutableTimeProvider _clock = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly EmailCodeService _service;

    public EmailCodeServiceTests()
    {
        SystemClock.SetProvider(_clock);
        _service = new EmailCodeService(NullLogger<EmailCodeService>.Instance, _cache);
    }

    public void Dispose()
    {
        SystemClock.Reset();
        _cache.Dispose();
    }

    [Fact]
    public async Task GenerateEmailCodeAsync_ReturnsSixDigitCode_AndStoresOnlyItsDigest()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, Email);

        code.Should().NotBeNull().And.MatchRegex("^[0-9]{6}$");

        var entry = _cache.Keys.OfType<string>()
            .Single(key => key.StartsWith("emailcode:entry:", StringComparison.Ordinal));
        _cache.TryGetValue(entry, out var value).Should().BeTrue();
        var info = (EmailCodeInfo)value!;

        entry.Should().NotContain(Email);
        info.CodeDigest.Should().Equal(SHA256.HashData(Encoding.UTF8.GetBytes(code!)));
        info.UserId.Should().Be(userId);
        info.Email.Should().Be(Email);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_CorrectCodeSucceeds_AndIsSingleUse()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, Email);

        var first = await _service.VerifyEmailCodeAsync(Email, code!);
        var second = await _service.VerifyEmailCodeAsync(Email, code!);

        first.Success.Should().BeTrue();
        first.UserId.Should().Be(userId);
        first.Email.Should().Be(Email);
        second.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_ExpiresAfterTenMinutes()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, Email);

        _clock.AdvanceBy(EmailCodeService.CodeLifetime.Add(TimeSpan.FromSeconds(1)));

        var result = await _service.VerifyEmailCodeAsync(Email, code!);
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Be("Expired code");
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_WrongCodesExhaustTheFiveAttemptLimit()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, Email);

        for (var attempt = 1; attempt <= EmailCodeService.MaxVerificationAttempts; attempt++)
        {
            var wrong = WrongCode(code!);
            (await _service.VerifyEmailCodeAsync(Email, wrong)).Success.Should().BeFalse();
        }

        // The attempt budget is spent: even the correct code no longer verifies.
        var result = await _service.VerifyEmailCodeAsync(Email, code!);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_CorrectCodeStillWorksBeforeAttemptLimit()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, Email);

        for (var attempt = 1; attempt < EmailCodeService.MaxVerificationAttempts; attempt++)
        {
            (await _service.VerifyEmailCodeAsync(Email, WrongCode(code!))).Success.Should().BeFalse();
        }

        var result = await _service.VerifyEmailCodeAsync(Email, code!);
        result.Success.Should().BeTrue();
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_UnknownEmailFailsWithoutThrowing()
    {
        var act = async () => await _service.VerifyEmailCodeAsync("nobody@example.test", "123456");

        await act.Should().NotThrowAsync();
        (await act()).Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_EmptyCodeFailsSafely()
    {
        await _service.GenerateEmailCodeAsync(Guid.NewGuid(), Email);

        var result = await _service.VerifyEmailCodeAsync(Email, string.Empty);
        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyEmailCodeAsync_NormalizesEmailCaseAndSurroundingWhitespace()
    {
        var userId = Guid.NewGuid();
        var code = await _service.GenerateEmailCodeAsync(userId, " Code-Owner@Example.TEST ");

        var result = await _service.VerifyEmailCodeAsync("code-owner@example.test", code!);
        result.Success.Should().BeTrue();
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GenerateEmailCodeAsync_ThrottlesResendsWithinSixtySeconds()
    {
        var userId = Guid.NewGuid();
        var first = await _service.GenerateEmailCodeAsync(userId, Email);
        var throttled = await _service.GenerateEmailCodeAsync(userId, Email);

        first.Should().NotBeNull();
        throttled.Should().BeNull();

        _clock.AdvanceBy(EmailCodeService.ResendThrottleWindow.Add(TimeSpan.FromSeconds(1)));

        var renewed = await _service.GenerateEmailCodeAsync(userId, Email);
        renewed.Should().NotBeNull().And.NotBe(first);
    }

    [Fact]
    public async Task GenerateEmailCodeAsync_RenewalReplacesThePreviousCode()
    {
        var userId = Guid.NewGuid();
        var first = (await _service.GenerateEmailCodeAsync(userId, Email))!;

        _clock.AdvanceBy(EmailCodeService.ResendThrottleWindow.Add(TimeSpan.FromSeconds(1)));
        var renewed = (await _service.GenerateEmailCodeAsync(userId, Email))!;

        (await _service.VerifyEmailCodeAsync(Email, first)).Success.Should().BeFalse();
        (await _service.VerifyEmailCodeAsync(Email, renewed)).Success.Should().BeTrue();
    }

    private static string WrongCode(string code)
    {
        var digit = code[0] == '0' ? '1' : '0';
        return new string(digit, 1) + code[1..];
    }

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

        public void AdvanceBy(TimeSpan delta) => _now = _now.Add(delta);

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
