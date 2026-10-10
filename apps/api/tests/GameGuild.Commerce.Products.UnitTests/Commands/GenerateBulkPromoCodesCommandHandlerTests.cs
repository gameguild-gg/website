using FluentAssertions;
using GameGuild.Commerce.Products;
using Moq;
using Xunit;

namespace GameGuild.Tests.Commerce.Products.Unit.Commands;

/// <summary>
/// Bulk generation: uniqueness within the batch, prefix + suffix format,
/// count bounds, and campaign stamping.
/// </summary>
public class GenerateBulkPromoCodesCommandHandlerTests
{
    private readonly Mock<IPromoCodeRepository> _repository = new();
    private readonly Mock<IPromoCodeAuditLogger> _audit = new();

    private GenerateBulkPromoCodesCommandHandler CreateHandler() =>
        new(_repository.Object, _audit.Object);

    private GenerateBulkPromoCodesCommand CreateCommand(int count = 50, string prefix = "LAUNCH") => new(
        Count: count,
        Prefix: prefix,
        Name: "Launch campaign",
        Type: PromoCodeType.PercentageOff,
        DiscountPercentage: 20m,
        MaxUsesPerUser: 1,
        CampaignId: "launch-2026");

    public GenerateBulkPromoCodesCommandHandlerTests()
    {
        _repository.Setup(r => r.GetExistingCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        _repository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PromoCode>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Handle_GeneratesRequestedCount_AllUnique_WithPrefixAndSpecFormat()
    {
        var result = await CreateHandler().Handle(CreateCommand(50), CancellationToken.None);

        result.Should().HaveCount(50);
        var codes = result.Select(d => d.Code).ToList();
        codes.Should().OnlyHaveUniqueItems("batch uniqueness is guaranteed");
        codes.Should().OnlyContain(c => c.StartsWith("LAUNCH-", StringComparison.Ordinal));
        codes.Should().OnlyContain(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^[A-Z0-9-]{3,50}$"));
        // Suffix length: prefix (6) + hyphen (1) + 8 = 15
        codes.Should().OnlyContain(c => c.Length == "LAUNCH-".Length + 8);
    }

    [Fact]
    public async Task Handle_RegeneratesOnCollision_WithExistingCodes()
    {
        var captured = new List<IEnumerable<string>>();
        var call = 0;
        _repository.Setup(r => r.GetExistingCodesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<string>, CancellationToken>((codes, _) => captured.Add(codes.ToList()))
            .ReturnsAsync(() =>
            {
                call++;
                if (call == 1)
                {
                    // Pretend the entire first candidate batch already exists.
                    return captured[0].ToHashSet(StringComparer.OrdinalIgnoreCase);
                }

                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            });

        var result = await CreateHandler().Handle(CreateCommand(10), CancellationToken.None);

        result.Should().HaveCount(10);
        var secondBatch = captured[1].ToList();
        var firstBatch = captured[0].ToList();
        secondBatch.Should().NotIntersectWith(firstBatch, "the regenerated batch avoids known collisions");
        result.Select(d => d.Code).Should().BeEquivalentTo(secondBatch);
    }

    [Fact]
    public async Task Handle_StampsCampaignAndDiscountConfiguration()
    {
        var added = new List<PromoCode>();
        _repository.Setup(r => r.AddRangeAsync(It.IsAny<IEnumerable<PromoCode>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<PromoCode>, CancellationToken>((codes, _) => added.AddRange(codes))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(CreateCommand(5), CancellationToken.None);

        added.Should().HaveCount(5);
        added.Should().OnlyContain(p => p.CampaignId == "launch-2026");
        added.Should().OnlyContain(p => p.MaxUsesPerUser == 1);
        added.Should().OnlyContain(p => p.DiscountPercentage == 20m);
        added.Should().OnlyContain(p => p.IsActive);
        result.Should().OnlyContain(d => d.CampaignId == "launch-2026");
    }

    [Fact]
    public async Task Handle_AuditsEachCreatedCode()
    {
        await CreateHandler().Handle(CreateCommand(3), CancellationToken.None);

        _audit.Verify(a => a.LogMutationAsync(
            It.IsAny<Guid?>(),
            "Created",
            It.IsAny<Guid>(),
            It.IsAny<string>(),
            It.IsAny<object?>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public async Task Handle_RejectsCountsOutsideBounds(int count)
    {
        var act = () => CreateHandler().Handle(CreateCommand(count), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task Handle_NormalizesPrefixToUppercase()
    {
        var result = await CreateHandler().Handle(CreateCommand(5, "launch"), CancellationToken.None);

        result.Should().OnlyContain(d => d.Code.StartsWith("LAUNCH-", StringComparison.Ordinal));
    }
}
