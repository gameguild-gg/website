using FluentAssertions;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Services;

/// <summary>
///     Issue #416: "Plan slugs are automatically generated and validated for uniqueness".
/// </summary>
public class SubscriptionPlanSlugGeneratorTests
{
    private readonly SubscriptionPlanSlugGenerator _generator = new();

    [Theory]
    [InlineData("Professional", "professional")]
    [InlineData("Pro Plan", "pro-plan")]
    [InlineData("  Pro   Plan  ", "pro-plan")] // collapses runs of separators and trims
    [InlineData("Pro---Plan!!!", "pro-plan")] // collapses consecutive hyphens
    [InlineData("Café Premium", "cafe-premium")] // diacritics stripped via FormD
    [InlineData("Ágil Pro", "agil-pro")]
    [InlineData("Plan  v2.0 (2026)", "plan-v2-0-2026")]
    [InlineData("Pro123", "pro123")]
    public void Slugify_ProducesUrlSafeLowercaseAscii(string name, string expectedSlug)
    {
        _generator.Slugify(name).Should().Be(expectedSlug);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("---")]
    [InlineData("!!!")]
    [InlineData("(")]
    public void Slugify_ReturnsNull_WhenNameHasNoUsableCharacters(string? name)
    {
        _generator.Slugify(name).Should().BeNull();
    }

    [Fact]
    public void Slugify_TruncatesToFiftyCharacters_WithoutTrailingHyphen()
    {
        var slug = _generator.Slugify(new string('a', 80));

        slug.Should().NotBeNull();
        slug!.Length.Should().Be(50);
        slug.Should().Be(new string('a', 50));
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_ReturnsBaseSlug_WhenNotTaken()
    {
        var slug = await _generator.GenerateUniqueSlugAsync(
            "Professional",
            (_, _) => Task.FromResult(false),
            CancellationToken.None);

        slug.Should().Be("professional");
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_AppendsNumericSuffix_OnCollision()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "professional" };

        var slug = await _generator.GenerateUniqueSlugAsync(
            "Professional",
            (candidate, _) => Task.FromResult(taken.Contains(candidate)),
            CancellationToken.None);

        slug.Should().Be("professional-2");
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_IncrementsSuffix_UntilFree()
    {
        var taken = new HashSet<string>(StringComparer.Ordinal) { "professional", "professional-2", "professional-3" };

        var slug = await _generator.GenerateUniqueSlugAsync(
            "Professional",
            (candidate, _) => Task.FromResult(taken.Contains(candidate)),
            CancellationToken.None);

        slug.Should().Be("professional-4");
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_TruncatesBaseSlug_BeforeAppendingSuffix()
    {
        var longName = new string('a', 80);
        var taken = new HashSet<string>(StringComparer.Ordinal) { new string('a', 50) };

        var slug = await _generator.GenerateUniqueSlugAsync(
            longName,
            (candidate, _) => Task.FromResult(taken.Contains(candidate)),
            CancellationToken.None);

        slug.Should().NotBeNull();
        slug!.Length.Should().BeLessThanOrEqualTo(50);
        slug.Should().Be($"{new string('a', 48)}-2");
    }

    [Fact]
    public async Task GenerateUniqueSlugAsync_ReturnsNull_WhenNameIsUnusable()
    {
        var slug = await _generator.GenerateUniqueSlugAsync(
            "!!!",
            (_, _) => Task.FromResult(false),
            CancellationToken.None);

        slug.Should().BeNull();
    }
}
