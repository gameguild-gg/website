using FluentAssertions;
using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Posting;

namespace GameGuild.Finance.Economy.UnitTests.Posting;

public sealed class PostingMatrixTests
{
    public static TheoryData<PostingTemplateKind> SupportedTemplates => new()
    {
        PostingTemplateKind.ConfirmedTopUpMint,
        PostingTemplateKind.ProviderReversalFull,
        PostingTemplateKind.ProviderReversalPartial,
        PostingTemplateKind.ProviderConvertedSoftReversal,
        PostingTemplateKind.ProviderReversalDebt,
        PostingTemplateKind.ProviderReversalLoss,
        PostingTemplateKind.Spend,
        PostingTemplateKind.HardToSoftConversion,
        PostingTemplateKind.HardToSoftConversionFee,
        PostingTemplateKind.SystemBackedGrant,
        PostingTemplateKind.AdRewardIssuance,
        PostingTemplateKind.MarketplaceSettlement,
        PostingTemplateKind.MarketplaceRefund,
        PostingTemplateKind.Burn,
        PostingTemplateKind.Escrow,
        PostingTemplateKind.BountyEscrow,
        PostingTemplateKind.BountyClaim,
        PostingTemplateKind.BountyReclaim,
        PostingTemplateKind.Reclaim,
        PostingTemplateKind.Refund,
        PostingTemplateKind.PayoutReservation,
        PostingTemplateKind.PayoutSuccess,
        PostingTemplateKind.PayoutFailure,
        PostingTemplateKind.AdminWithdrawalReservation,
        PostingTemplateKind.AdminWithdrawalSuccess,
        PostingTemplateKind.AdminWithdrawalFailure
    };

    [Fact]
    public void MatrixCoversEveryRegisteredTemplateAndHasNoPreConfirmationMint()
    {
        var registered = Enum.GetValues<PostingTemplateKind>();

        registered.Should().HaveCount(26);
        registered.Select(kind => kind.ToString()).Should().NotContain(name =>
            name.Contains("Observed", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("FailedMint", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [MemberData(nameof(SupportedTemplates))]
    public void SupportedTemplate_AcceptsItsExactPostingShape(PostingTemplateKind kind)
    {
        var result = PostingMatrix.Validate(PostingFixture.Valid(kind));

        result.IsValid.Should().BeTrue(string.Join(Environment.NewLine, result.Errors));
        result.Errors.Should().BeEmpty();
        FluentActions.Invoking(() => PostingMatrix.EnsureValid(PostingFixture.Valid(kind))).Should().NotThrow();
    }

    [Fact]
    public void NullOrUnregisteredRequests_FailClosed()
    {
        FluentActions.Invoking(() => PostingMatrix.Validate(null!)).Should().Throw<ArgumentNullException>();
        var request = PostingFixture.Valid(PostingTemplateKind.Spend) with { Template = default };

        PostingMatrix.Validate(request).Errors.Should().ContainSingle(error => error.Code == PostingErrorCode.UnsupportedTemplate);
    }

    [Fact]
    public void UnknownTemplateVersion_IsRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend) with
        {
            Template = new PostingTemplate(PostingTemplateKind.Spend, 2)
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.UnsupportedTemplateVersion);
        FluentActions.Invoking(() => PostingMatrix.EnsureValid(request))
            .Should().Throw<PostingValidationException>()
            .Which.Errors.Should().Contain(error =>
                error.Code == PostingErrorCode.UnsupportedTemplateVersion);
    }

    [Fact]
    public void WrongAuthority_IsRejectedEvenWhenPostingBalances()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint) with
        {
            Authority = PostingAuthority.WalletOwner
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.UnauthorizedAuthority);
    }

    [Theory]
    [MemberData(nameof(SupportedTemplates))]
    public void EveryRegisteredTemplateRejectsAnAuthorityDifferentFromItsCatalogRegistration(
        PostingTemplateKind kind)
    {
        var valid = PostingFixture.Valid(kind);
        var wrongAuthority = valid.Authority == PostingAuthority.WalletOwner
            ? PostingAuthority.ProviderConfirmation
            : PostingAuthority.WalletOwner;

        var result = PostingMatrix.Validate(valid with { Authority = wrongAuthority });

        result.Errors.Should().ContainSingle(error => error.Code == PostingErrorCode.UnauthorizedAuthority,
            $"{kind} must enforce its registered posting authority");
    }

    [Fact]
    public void BalancedButWrongAccountShape_IsRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint);
        request = request with
        {
            Lines =
            [
                request.Lines[0] with { Account = EconomyAccountCode.PlatformHardTreasury },
                request.Lines[1]
            ]
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
    }

    [Fact]
    public void UnbalancedPosting_IsRejectedPerCurrency()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        request = request with { Lines = [request.Lines[0], request.Lines[1] with { Amount = new CoinAmount(CurrencyCode.HardCoin, 9) }] };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.UnbalancedCurrency);
    }

    [Fact]
    public void DuplicateOrNonContiguousSequences_AreRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var duplicate = request with { Lines = [request.Lines[0], request.Lines[1] with { Sequence = 1 }] };
        var gap = request with { Lines = [request.Lines[0], request.Lines[1] with { Sequence = 3 }] };

        PostingMatrix.Validate(duplicate).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSequence);
        PostingMatrix.Validate(gap).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSequence);
    }

    [Fact]
    public void Mint_RequiresConfirmedProviderEvidence()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint) with
        {
            Source = PostingFixture.Source(SourceConfirmationState.Observed)
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSourceState);

        PostingMatrix.Validate(request with { Source = null }).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSourceState);
    }

    [Fact]
    public void ProviderReversal_RequiresReversedProviderEvidence()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ProviderReversalPartial) with
        {
            Source = PostingFixture.Source(SourceConfirmationState.Confirmed)
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSourceState);
    }

    [Theory]
    [InlineData(PostingTemplateKind.ProviderReversalPartial)]
    [InlineData(PostingTemplateKind.ProviderConvertedSoftReversal)]
    [InlineData(PostingTemplateKind.ProviderReversalDebt)]
    [InlineData(PostingTemplateKind.ProviderReversalLoss)]
    public void PartialProviderReversalTemplates_AcceptDisputedAndRejectConfirmedEvidence(
        PostingTemplateKind kind)
    {
        var request = PostingFixture.Valid(kind);

        PostingMatrix.Validate(request with { Source = PostingFixture.Source(SourceConfirmationState.Disputed) })
            .Errors.Should().NotContain(error => error.Code == PostingErrorCode.InvalidSourceState);
        PostingMatrix.Validate(request with { Source = PostingFixture.Source(SourceConfirmationState.Reversed) })
            .Errors.Should().NotContain(error => error.Code == PostingErrorCode.InvalidSourceState);
        PostingMatrix.Validate(request with { Source = PostingFixture.Source(SourceConfirmationState.Confirmed) })
            .Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSourceState);
        PostingMatrix.Validate(request with { Source = null })
            .Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidSourceState);
    }

    [Fact]
    public void Conversion_RequiresExactPrincipalRatioAndSeparateFeePosting()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.HardToSoftConversion);
        var wrongRatio = request with
        {
            Lines =
            [
                request.Lines[0], request.Lines[1], request.Lines[2] with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 9_999) },
                request.Lines[3] with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 9_999) }
            ]
        };
        var embeddedFee = request with
        {
            Lines = [.. request.Lines, PostingFixture.Line(5, EntrySide.Credit, EconomyAccountCode.FeeRevenueHard, CurrencyCode.HardCoin, 1)]
        };

        PostingMatrix.Validate(wrongRatio).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidParity);
        PostingMatrix.Validate(embeddedFee).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidLineCount);
    }

    [Fact]
    public void SoftSpend_UsesSoftLiabilityAndRemainsBalanced()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        request = request with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 15, WalletId.New(), ProvenanceKind.AdRewardSoft),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 15, WalletId.New(), ProvenanceKind.AdRewardSoft)
            ]
        };

        PostingMatrix.Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void ZeroLineAndMissingWalletShapes_AreRejected()
    {
        FluentActions.Invoking(() => PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 0))
            .Should().Throw<ArgumentOutOfRangeException>();

        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        request = request with { Lines = [request.Lines[0] with { WalletId = null }, request.Lines[1]] };
        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.MissingWallet);
    }

    [Fact]
    public void DeserializedZeroAmountAndOverflowingTotals_AreRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var zero = request with { Lines = [request.Lines[0] with { Amount = default }, request.Lines[1]] };
        var overflow = request with
        {
            Lines =
            [
                request.Lines[0] with { Amount = new CoinAmount(CurrencyCode.HardCoin, long.MaxValue) },
                request.Lines[0] with { Sequence = 2, Amount = new CoinAmount(CurrencyCode.HardCoin, 1) },
                request.Lines[1] with { Sequence = 3, Amount = new CoinAmount(CurrencyCode.HardCoin, long.MaxValue) }
            ]
        };

        PostingMatrix.Validate(zero).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAmount);
        PostingMatrix.Validate(overflow).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAmount);
    }

    [Fact]
    public void MalformedLiabilityTransfer_IsRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.Spend);
        var malformed = request with
        {
            Lines =
            [
                request.Lines[0] with { Side = EntrySide.Credit, Account = EconomyAccountCode.HardCoinReserve },
                request.Lines[1] with { Account = EconomyAccountCode.SoftCoinLiability, Amount = new CoinAmount(CurrencyCode.SoftCoin, 10) }
            ]
        };

        var errors = PostingMatrix.Validate(malformed).Errors;
        errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
        errors.Should().Contain(error => error.Code == PostingErrorCode.UnbalancedCurrency);

        var refund = PostingFixture.Valid(PostingTemplateKind.Refund);
        refund = refund with { Lines = [refund.Lines[0], refund.Lines[1] with { Provenance = ProvenanceKind.PurchasedHard }] };
        PostingMatrix.Validate(refund).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidProvenance);
    }

    [Fact]
    public void SoftBurnEscrowAndReclaim_UseSoftSystemAccounts()
    {
        var wallet = WalletId.New();
        var softBurn = PostingFixture.Valid(PostingTemplateKind.Burn) with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 20, wallet, ProvenanceKind.AdRewardSoft),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, 20)
            ]
        };
        var softEscrow = PostingFixture.Valid(PostingTemplateKind.Escrow) with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 20, wallet, ProvenanceKind.AdRewardSoft),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinEscrow, CurrencyCode.SoftCoin, 20)
            ]
        };
        var softReclaim = PostingFixture.Valid(PostingTemplateKind.Reclaim) with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinEscrow, CurrencyCode.SoftCoin, 20),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 20, wallet, ProvenanceKind.EscrowReturn)
            ]
        };

        PostingMatrix.Validate(softBurn).IsValid.Should().BeTrue();
        PostingMatrix.Validate(softEscrow).IsValid.Should().BeTrue();
        PostingMatrix.Validate(softReclaim).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BountyEscrow_PreservesEachHardCoinProvenanceAccount()
    {
        var wallet = WalletId.New();
        var request = PostingFixture.Valid(PostingTemplateKind.BountyEscrow) with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                    CurrencyCode.HardCoin, 6, wallet, ProvenanceKind.PurchasedHard),
                PostingFixture.Line(2, EntrySide.Debit, EconomyAccountCode.EarnedHardLiability,
                    CurrencyCode.HardCoin, 4, wallet, ProvenanceKind.EarnedHard),
                PostingFixture.Line(3, EntrySide.Credit, EconomyAccountCode.HardCoinEscrow,
                    CurrencyCode.HardCoin, 10)
            ]
        };

        PostingMatrix.Validate(request).IsValid.Should().BeTrue();

        var malformed = request with
        {
            Lines =
            [
                request.Lines[0],
                request.Lines[1] with { Account = EconomyAccountCode.PurchasedHardLiability },
                request.Lines[2]
            ]
        };

        PostingMatrix.Validate(malformed).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);
    }

    [Fact]
    public void BountyTemplates_ReportUnsupportedCurrencyAndInvalidEscrowProvenance()
    {
        var wallet = WalletId.New();
        var hardClaim = PostingFixture.Valid(PostingTemplateKind.BountyClaim);
        var unsupportedClaim = hardClaim with
        {
            Lines =
            [
                hardClaim.Lines[0] with { Amount = default },
                hardClaim.Lines[1] with { Amount = default }
            ]
        };
        var softClaim = hardClaim with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinEscrow,
                    CurrencyCode.SoftCoin, 10),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 10, wallet, ProvenanceKind.EscrowReturn)
            ]
        };

        var hardEscrow = PostingFixture.Valid(PostingTemplateKind.BountyEscrow);
        var unsupportedEscrow = hardEscrow with
        {
            Lines =
            [
                hardEscrow.Lines[0] with { Amount = default },
                hardEscrow.Lines[1] with { Amount = default }
            ]
        };
        var undersizedEscrow = hardEscrow with { Lines = [hardEscrow.Lines[0]] };
        var unsupportedVersionEscrow = hardEscrow with
        {
            Template = new PostingTemplate(
                PostingTemplateKind.BountyEscrow, PostingTemplate.CurrentVersion + 1)
        };
        var mixedCurrencyEscrow = hardEscrow with
        {
            Lines =
            [
                hardEscrow.Lines[0] with
                {
                    Amount = new CoinAmount(CurrencyCode.SoftCoin, hardEscrow.Lines[0].Amount.Units)
                },
                hardEscrow.Lines[1]
            ]
        };
        var missingProvenanceEscrow = hardEscrow with
        {
            Lines =
            [
                hardEscrow.Lines[0] with { Provenance = null },
                hardEscrow.Lines[1]
            ]
        };

        PostingRequest SoftEscrow(ProvenanceKind provenance) => hardEscrow with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 10, wallet, provenance),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinEscrow,
                    CurrencyCode.SoftCoin, 10)
            ]
        };

        PostingMatrix.Validate(softClaim).IsValid.Should().BeTrue();
        PostingMatrix.Validate(unsupportedClaim).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(unsupportedEscrow).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(undersizedEscrow).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidLineCount);
        PostingMatrix.Validate(unsupportedVersionEscrow).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.UnsupportedTemplateVersion);
        PostingMatrix.Validate(mixedCurrencyEscrow).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(missingProvenanceEscrow).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidProvenance);
        PostingMatrix.Validate(SoftEscrow(ProvenanceKind.PurchasedHard)).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidProvenance);
        PostingMatrix.Validate(SoftEscrow(ProvenanceKind.EarnedHard)).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidProvenance);
        PostingMatrix.Validate(SoftEscrow(ProvenanceKind.AdRewardSoft)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void BountyReclaim_ValidatesReturnAndFeePairs()
    {
        var valid = PostingFixture.Valid(PostingTemplateKind.BountyReclaim);
        var wallet = valid.Lines[1].WalletId!.Value;
        var unsupportedCurrency = valid with
        {
            Lines =
            [
                valid.Lines[0] with { Amount = default },
                valid.Lines[1] with { Amount = default }
            ]
        };
        var oddPairCount = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1],
                valid.Lines[0] with { Sequence = 3 }
            ]
        };
        var mismatchedAmount = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with { Amount = new CoinAmount(CurrencyCode.HardCoin, 9) }
            ]
        };
        var mismatchedCurrency = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with { Amount = new CoinAmount(CurrencyCode.SoftCoin, 10) }
            ]
        };
        var hardFee = valid with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow,
                    CurrencyCode.HardCoin, 10),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.FeeRevenueHard,
                    CurrencyCode.HardCoin, 10)
            ]
        };
        var softFee = valid with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinEscrow,
                    CurrencyCode.SoftCoin, 10),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinReserve,
                    CurrencyCode.SoftCoin, 10)
            ]
        };
        var misplacedFee = valid with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow,
                    CurrencyCode.HardCoin, 2),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.FeeRevenueHard,
                    CurrencyCode.HardCoin, 2),
                PostingFixture.Line(3, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow,
                    CurrencyCode.HardCoin, 8),
                PostingFixture.Line(4, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability,
                    CurrencyCode.HardCoin, 8, wallet, ProvenanceKind.PurchasedHard)
            ]
        };
        var missingProvenance = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with { Provenance = null }
            ]
        };
        var hardAccountMismatch = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with { Provenance = ProvenanceKind.EarnedHard }
            ]
        };
        var purchasedAccountMismatch = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with
                {
                    Account = EconomyAccountCode.EarnedHardLiability,
                    Provenance = ProvenanceKind.PurchasedHard
                }
            ]
        };
        var validEarnedReturn = valid with
        {
            Lines =
            [
                valid.Lines[0],
                valid.Lines[1] with
                {
                    Account = EconomyAccountCode.EarnedHardLiability,
                    Provenance = ProvenanceKind.EarnedHard
                }
            ]
        };
        var softHardProvenance = valid with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinEscrow,
                    CurrencyCode.SoftCoin, 10),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 10, wallet, ProvenanceKind.PurchasedHard)
            ]
        };
        var validSoftReturn = valid with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinEscrow,
                    CurrencyCode.SoftCoin, 10),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 10, wallet, ProvenanceKind.AdRewardSoft)
            ]
        };

        PostingMatrix.Validate(valid).IsValid.Should().BeTrue();
        PostingMatrix.Validate(hardFee).IsValid.Should().BeTrue();
        PostingMatrix.Validate(softFee).IsValid.Should().BeTrue();
        PostingMatrix.Validate(validSoftReturn).IsValid.Should().BeTrue();
        PostingMatrix.Validate(validEarnedReturn).IsValid.Should().BeTrue();
        PostingMatrix.Validate(unsupportedCurrency).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(oddPairCount).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidLineCount);
        PostingMatrix.Validate(mismatchedAmount).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAmount);
        PostingMatrix.Validate(mismatchedCurrency).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAmount);
        PostingMatrix.Validate(misplacedFee).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);
        PostingMatrix.Validate(missingProvenance).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidProvenance);
        PostingMatrix.Validate(hardAccountMismatch).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);
        PostingMatrix.Validate(purchasedAccountMismatch).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);
        PostingMatrix.Validate(softHardProvenance).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidProvenance);
    }

    [Fact]
    public void InvalidCurrencyFromDeserialization_IsReportedWithoutEscapingValidator()
    {
        var burn = PostingFixture.Valid(PostingTemplateKind.Burn);
        burn = burn with { Lines = [burn.Lines[0] with { Amount = default }, burn.Lines[1]] };
        var reclaim = PostingFixture.Valid(PostingTemplateKind.Reclaim);
        reclaim = reclaim with { Lines = [reclaim.Lines[0], reclaim.Lines[1] with { Amount = default }] };

        PostingMatrix.Validate(burn).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(reclaim).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidCurrency);
    }

    [Fact]
    public void ExactShapeMatcher_ReportsEveryMaterialMismatch()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.ConfirmedTopUpMint);
        request = request with
        {
            Lines =
            [
                request.Lines[0] with
                {
                    Side = EntrySide.Credit,
                    Account = EconomyAccountCode.PlatformHardTreasury,
                    Amount = new CoinAmount(CurrencyCode.SoftCoin, 10),
                    WalletId = WalletId.New(),
                    Provenance = ProvenanceKind.AdRewardSoft
                },
                request.Lines[1] with { WalletId = null, Provenance = ProvenanceKind.EarnedHard }
            ]
        };

        var errors = PostingMatrix.Validate(request).Errors;
        errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
        errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidCurrency);
        errors.Should().Contain(error => error.Code == PostingErrorCode.MissingWallet);
        errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidProvenance);
    }

    [Fact]
    public void ConversionParityOverflow_IsRejected()
    {
        var request = PostingFixture.Valid(PostingTemplateKind.HardToSoftConversion);
        request = request with
        {
            Lines =
            [
                request.Lines[0] with { Amount = new CoinAmount(CurrencyCode.HardCoin, long.MaxValue) },
                request.Lines[1] with { Amount = new CoinAmount(CurrencyCode.HardCoin, long.MaxValue) },
                request.Lines[2] with { Amount = new CoinAmount(CurrencyCode.SoftCoin, long.MaxValue) },
                request.Lines[3] with { Amount = new CoinAmount(CurrencyCode.SoftCoin, long.MaxValue) }
            ]
        };

        PostingMatrix.Validate(request).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidParity);
    }

    [Fact]
    public void MarketplaceRefund_AllowsExplicitSoftReceivableButRejectsAnUnrelatedSystemDebit()
    {
        var wallet = WalletId.New();
        var valid = PostingFixture.Valid(PostingTemplateKind.MarketplaceRefund) with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.RecoveryReceivableSoft,
                    CurrencyCode.SoftCoin, 25),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 25, wallet, ProvenanceKind.MarketplaceSoft)
            ]
        };

        PostingMatrix.Validate(valid).IsValid.Should().BeTrue();
        PostingMatrix.Validate(valid with
        {
            Lines =
            [
                valid.Lines[0] with { Account = EconomyAccountCode.SoftCoinReserve },
                valid.Lines[1]
            ]
        }).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
    }

    [Fact]
    public void MarketplaceTemplatesRejectMissingSidesAndSettlementCoversEveryCurrencyBranch()
    {
        var settlement = PostingFixture.Valid(PostingTemplateKind.MarketplaceSettlement);
        var onlyCredits = settlement with
        {
            Lines = settlement.Lines.Select(line => line with { Side = EntrySide.Credit }).ToArray()
        };
        var onlyDebits = settlement with
        {
            Lines = settlement.Lines.Select(line => line with { Side = EntrySide.Debit }).ToArray()
        };
        PostingMatrix.Validate(onlyCredits).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);
        PostingMatrix.Validate(onlyDebits).Errors.Should().Contain(error =>
            error.Code == PostingErrorCode.InvalidAccountShape);

        var wallet = WalletId.New();
        var soft = settlement with
        {
            Lines =
            [
                PostingFixture.Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 10, wallet, ProvenanceKind.MarketplaceSoft),
                PostingFixture.Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 9, WalletId.New(), ProvenanceKind.MarketplaceSoft),
                PostingFixture.Line(3, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, 1, WalletId.New(), ProvenanceKind.MarketplaceSoft)
            ]
        };
        PostingMatrix.Validate(soft).Errors.Should().NotContain(error =>
            error.Code == PostingErrorCode.InvalidCurrency);
        PostingMatrix.Validate(soft with
        {
            Lines = [soft.Lines[0], soft.Lines[1] with { Amount = default }, soft.Lines[2]]
        }).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidCurrency);

        var refund = PostingFixture.Valid(PostingTemplateKind.MarketplaceRefund);
        PostingMatrix.Validate(refund with
        {
            Lines = refund.Lines.Select(line => line with { Side = EntrySide.Credit }).ToArray()
        }).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
        PostingMatrix.Validate(refund with
        {
            Lines = refund.Lines.Select(line => line with { Side = EntrySide.Debit }).ToArray()
        }).Errors.Should().Contain(error => error.Code == PostingErrorCode.InvalidAccountShape);
    }
}

internal static class PostingFixture
{
    private static readonly DateTimeOffset Time = DateTimeOffset.Parse("2026-07-18T12:00:00Z");

    internal static PostingRequest Valid(PostingTemplateKind kind)
    {
        var (authority, lines, source) = kind switch
        {
            PostingTemplateKind.ConfirmedTopUpMint => (PostingAuthority.ProviderConfirmation,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard)
                }, Source(SourceConfirmationState.Confirmed)),
            PostingTemplateKind.ProviderReversalFull or PostingTemplateKind.ProviderReversalPartial => (PostingAuthority.ProviderConfirmation,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, Source(SourceConfirmationState.Reversed)),
            PostingTemplateKind.ProviderConvertedSoftReversal => (PostingAuthority.ProviderConfirmation,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 10_000, WalletId.New(), ProvenanceKind.ConvertedSoft),
                    Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, 10_000),
                    Line(3, EntrySide.Debit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, 10),
                    Line(4, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, Source(SourceConfirmationState.Disputed)),
            PostingTemplateKind.ProviderReversalDebt => (PostingAuthority.ProviderConfirmation,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.RecoveryReceivableHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, Source(SourceConfirmationState.Disputed)),
            PostingTemplateKind.ProviderReversalLoss => (PostingAuthority.ProviderConfirmation,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.ProviderLossHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, Source(SourceConfirmationState.Disputed)),
            PostingTemplateKind.Spend => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard)
                }, null),
            PostingTemplateKind.HardToSoftConversion => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, 10),
                    Line(3, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, 10_000),
                    Line(4, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 10_000, WalletId.New(), ProvenanceKind.ConvertedSoft)
                }, null),
            PostingTemplateKind.HardToSoftConversionFee => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.FeeRevenueHard, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.SystemBackedGrant => (PostingAuthority.PlatformSystem,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, 10),
                    Line(3, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, 10_000),
                    Line(4, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 10_000, WalletId.New(), ProvenanceKind.SystemGrantSoft)
                }, null),
            PostingTemplateKind.AdRewardIssuance => (PostingAuthority.PlatformSystem,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, 10, WalletId.New(), ProvenanceKind.AdRewardSoft)
                }, null),
            PostingTemplateKind.MarketplaceSettlement => (PostingAuthority.MarketplaceCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability,
                        CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability,
                        CurrencyCode.HardCoin, 9, WalletId.New(), ProvenanceKind.EarnedHard),
                    Line(3, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability,
                        CurrencyCode.HardCoin, 1, WalletId.New(), ProvenanceKind.EarnedHard)
                }, null),
            PostingTemplateKind.MarketplaceRefund => (PostingAuthority.MarketplaceCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.EarnedHardLiability,
                        CurrencyCode.HardCoin, 8, WalletId.New(), ProvenanceKind.EarnedHard),
                    Line(2, EntrySide.Debit, EconomyAccountCode.RecoveryReceivableHard,
                        CurrencyCode.HardCoin, 2),
                    Line(3, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability,
                        CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard)
                }, null),
            PostingTemplateKind.Burn => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.Escrow => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.HardCoinEscrow, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.BountyEscrow => (PostingAuthority.WalletOwner,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.HardCoinEscrow, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.BountyClaim => (PostingAuthority.EscrowCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.EarnedHard)
                }, null),
            PostingTemplateKind.BountyReclaim => (PostingAuthority.EscrowCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard)
                }, null),
            PostingTemplateKind.Reclaim => (PostingAuthority.EscrowCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.HardCoinEscrow, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.EscrowReturn)
                }, null),
            PostingTemplateKind.Refund => (PostingAuthority.EscrowCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.PurchasedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.RefundRestoration)
                }, null),
            PostingTemplateKind.PayoutReservation => (PostingAuthority.PayoutCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.EarnedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.EarnedHard),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.PayoutSuccess => (PostingAuthority.PayoutCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.PayoutFailure => (PostingAuthority.PayoutCoordinator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability, CurrencyCode.HardCoin, 10, WalletId.New(), ProvenanceKind.EarnedHard)
                }, null),
            PostingTemplateKind.AdminWithdrawalReservation => (PostingAuthority.Administrator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.AdminWithdrawalSuccess => (PostingAuthority.Administrator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, 10)
                }, null),
            PostingTemplateKind.AdminWithdrawalFailure => (PostingAuthority.Administrator,
                new[]
                {
                    Line(1, EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, 10),
                    Line(2, EntrySide.Credit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, 10)
                }, null),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

        return new PostingRequest(
            PostingId.New(),
            new PostingTemplate(kind, PostingTemplate.CurrentVersion),
            new IdempotencyKey($"test-{kind}"),
            authority,
            new ReserveVersion(7),
            new PolicyVersion(4),
            source,
            Time,
            lines);
    }

    internal static PostingLine Line(
        int sequence,
        EntrySide side,
        EconomyAccountCode account,
        CurrencyCode currency,
        long units,
        WalletId? walletId = null,
        ProvenanceKind? provenance = null) =>
        new(sequence, side, account, new CoinAmount(currency, units), walletId, null, provenance);

    internal static SourceStampContract Source(SourceConfirmationState state)
    {
        DateTimeOffset? confirmedAt = state is SourceConfirmationState.Confirmed or
            SourceConfirmationState.Disputed or SourceConfirmationState.Reversed
            ? Time.AddMinutes(1)
            : null;
        return new SourceStampContract(SourceStampId.New(), "sha256-source", state, Time, confirmedAt, "pi_test");
    }
}
