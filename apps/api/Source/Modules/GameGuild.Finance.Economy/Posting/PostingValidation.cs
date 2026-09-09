using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Writer;

namespace GameGuild.Finance.Economy.Posting;

public enum PostingErrorCode
{
    UnsupportedTemplate = 1,
    UnsupportedTemplateVersion = 2,
    UnauthorizedAuthority = 3,
    InvalidLineCount = 4,
    InvalidSequence = 5,
    InvalidAccountShape = 6,
    InvalidCurrency = 7,
    UnbalancedCurrency = 8,
    MissingWallet = 9,
    InvalidProvenance = 10,
    InvalidSourceState = 11,
    InvalidParity = 12,
    InvalidAmount = 13
}

public sealed record PostingValidationError(PostingErrorCode Code, string Message);

public sealed record PostingValidationResult
{
    internal PostingValidationResult(IReadOnlyList<PostingValidationError> errors) => Errors = errors;

    public bool IsValid => Errors.Count == 0;
    public IReadOnlyList<PostingValidationError> Errors { get; }
}

public sealed class PostingValidationException : InvalidOperationException
{
    public PostingValidationException(IReadOnlyList<PostingValidationError> errors)
        : base(string.Join("; ", errors.Select(error => $"{error.Code}: {error.Message}"))) => Errors = errors;

    public IReadOnlyList<PostingValidationError> Errors { get; }
}

public static class PostingMatrix
{
    public static PostingValidationResult Validate(PostingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var errors = new List<PostingValidationError>();

        var registeredTemplate = PostingTemplateCatalog.Find(
            request.Template.Kind,
            PostingTemplate.CurrentVersion);
        if (!Enum.IsDefined(request.Template.Kind) || registeredTemplate is null)
            Add(errors, PostingErrorCode.UnsupportedTemplate, "Posting template is not registered.");

        if (request.Template.Version != PostingTemplate.CurrentVersion)
            Add(errors, PostingErrorCode.UnsupportedTemplateVersion, "Only the current immutable template version is accepted.");

        if (registeredTemplate is not null && request.Authority != registeredTemplate.Authority)
            Add(errors, PostingErrorCode.UnauthorizedAuthority, "Posting authority does not match the selected template.");

        ValidateSequences(request.Lines, errors);
        ValidateAmountsAndCurrencyBalance(request.Lines, errors);
        ValidateSource(request, errors);
        ValidateShape(request, errors);

        return new PostingValidationResult(errors);
    }

    public static void EnsureValid(PostingRequest request)
    {
        var result = Validate(request);
        if (!result.IsValid) throw new PostingValidationException(result.Errors);
    }

    private static void ValidateSequences(IReadOnlyList<PostingLine> lines, ICollection<PostingValidationError> errors)
    {
        var ordered = lines.Select(line => line.Sequence).Order().ToArray();
        if (ordered.Length == 0 || ordered.Distinct().Count() != ordered.Length || ordered.Where((sequence, index) => sequence != index + 1).Any())
            Add(errors, PostingErrorCode.InvalidSequence, "Line sequences must be unique and contiguous from one.");
    }

    private static void ValidateAmountsAndCurrencyBalance(IReadOnlyList<PostingLine> lines, ICollection<PostingValidationError> errors)
    {
        if (lines.Any(line => line.Amount.Units <= 0))
            Add(errors, PostingErrorCode.InvalidAmount, "Posting lines must carry positive integer units.");

        foreach (var currencyGroup in lines.GroupBy(line => line.Amount.Currency))
        {
            try
            {
                var debits = currencyGroup.Where(line => line.Side == EntrySide.Debit).Aggregate(0L, (total, line) => checked(total + line.Amount.Units));
                var credits = currencyGroup.Where(line => line.Side == EntrySide.Credit).Aggregate(0L, (total, line) => checked(total + line.Amount.Units));
                if (debits != credits)
                    Add(errors, PostingErrorCode.UnbalancedCurrency, $"{currencyGroup.Key} debits and credits must balance independently.");
            }
            catch (OverflowException)
            {
                Add(errors, PostingErrorCode.InvalidAmount, "Posting totals exceed the supported integer range.");
            }
        }
    }

    private static void ValidateSource(PostingRequest request, ICollection<PostingValidationError> errors)
    {
        var requiredState = request.Template.Kind switch
        {
            PostingTemplateKind.ConfirmedTopUpMint => SourceConfirmationState.Confirmed,
            PostingTemplateKind.ProviderReversalFull => SourceConfirmationState.Reversed,
            _ => (SourceConfirmationState?)null
        };

        if (requiredState.HasValue && request.Source?.State != requiredState)
            Add(errors, PostingErrorCode.InvalidSourceState, $"Template requires {requiredState} source evidence.");
        if (request.Template.Kind is PostingTemplateKind.ProviderReversalPartial or
            PostingTemplateKind.ProviderConvertedSoftReversal or
            PostingTemplateKind.ProviderReversalDebt or
            PostingTemplateKind.ProviderReversalLoss &&
            request.Source?.State is not (SourceConfirmationState.Disputed or SourceConfirmationState.Reversed))
            Add(errors, PostingErrorCode.InvalidSourceState, "Template requires disputed or reversed source evidence.");
    }

    private static void ValidateShape(PostingRequest request, ICollection<PostingValidationError> errors)
    {
        var registeredTemplate = PostingTemplateCatalog.Find(
            request.Template.Kind,
            request.Template.Version);
        var expectedCount = request.Template.Kind is PostingTemplateKind.HardToSoftConversion or
            PostingTemplateKind.SystemBackedGrant or
            PostingTemplateKind.ProviderConvertedSoftReversal ? 4 : 2;
        var isVariable = request.Template.Kind is PostingTemplateKind.BountyEscrow or
            PostingTemplateKind.BountyReclaim or PostingTemplateKind.MarketplaceSettlement or
            PostingTemplateKind.MarketplaceRefund;
        var hasValidLineCount = isVariable
            ? registeredTemplate?.AllowsLineCount(request.Lines.Count) == true
            : request.Lines.Count == expectedCount;
        if (!hasValidLineCount)
        {
            var requirement = isVariable
                ? "at least two lines"
                : $"exactly {expectedCount} lines";
            Add(errors, PostingErrorCode.InvalidLineCount, $"Template requires {requirement}.");
            return;
        }

        var lines = request.Lines.OrderBy(line => line.Sequence).ToArray();
        switch (request.Template.Kind)
        {
            case PostingTemplateKind.ConfirmedTopUpMint:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.PurchasedHard, errors);
                break;
            case PostingTemplateKind.ProviderReversalFull:
            case PostingTemplateKind.ProviderReversalPartial:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.PurchasedHard, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.ProviderConvertedSoftReversal:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, true, ProvenanceKind.ConvertedSoft, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, false, null, errors);
                Match(lines[2], EntrySide.Debit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[3], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                ValidateParity(lines[2].Amount.Units, lines[0].Amount.Units, errors);
                break;
            case PostingTemplateKind.ProviderReversalDebt:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.RecoveryReceivableHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.ProviderReversalLoss:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.ProviderLossHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.Spend:
                ValidateSameLiabilityTransfer(lines, null, errors);
                break;
            case PostingTemplateKind.HardToSoftConversion:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.PurchasedHard, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[2], EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, false, null, errors);
                Match(lines[3], EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, true, ProvenanceKind.ConvertedSoft, errors);
                ValidateParity(lines[0].Amount.Units, lines[3].Amount.Units, errors);
                break;
            case PostingTemplateKind.HardToSoftConversionFee:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PurchasedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.PurchasedHard, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.FeeRevenueHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.SystemBackedGrant:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.HardCoinReserve, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[2], EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, false, null, errors);
                Match(lines[3], EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, true, ProvenanceKind.SystemGrantSoft, errors);
                ValidateParity(lines[0].Amount.Units, lines[3].Amount.Units, errors);
                break;
            case PostingTemplateKind.AdRewardIssuance:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.SoftCoinReserve, CurrencyCode.SoftCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.SoftCoinLiability, CurrencyCode.SoftCoin, true, ProvenanceKind.AdRewardSoft, errors);
                break;
            case PostingTemplateKind.Burn:
                ValidateLiabilityAndSystemAccount(lines, EntrySide.Debit, ReserveFor(lines[0].Amount.Currency), errors);
                break;
            case PostingTemplateKind.Escrow:
                ValidateLiabilityAndSystemAccount(lines, EntrySide.Debit, EscrowFor(lines[0].Amount.Currency), errors);
                break;
            case PostingTemplateKind.BountyEscrow:
                ValidateBountyEscrow(lines, errors);
                break;
            case PostingTemplateKind.BountyClaim:
                var claimEscrowAccount = EscrowFor(lines[1].Amount.Currency);
                if (claimEscrowAccount.HasValue)
                    Match(lines[0], EntrySide.Debit, claimEscrowAccount.Value, lines[1].Amount.Currency, false, null, errors);
                else
                    Add(errors, PostingErrorCode.InvalidCurrency, "Bounty claim requires a supported coin currency.");
                if (lines[1].Amount.Currency == CurrencyCode.HardCoin)
                    ValidateLiability(lines[1], EntrySide.Credit, ProvenanceKind.EarnedHard, errors);
                else
                    ValidateLiability(lines[1], EntrySide.Credit, ProvenanceKind.EscrowReturn, errors);
                break;
            case PostingTemplateKind.BountyReclaim:
                ValidateBountyReclaim(lines, errors);
                break;
            case PostingTemplateKind.MarketplaceSettlement:
                ValidateMarketplaceSettlement(lines, errors);
                break;
            case PostingTemplateKind.MarketplaceRefund:
                ValidateMarketplaceRefund(lines, errors);
                break;
            case PostingTemplateKind.Reclaim:
                var escrowAccount = EscrowFor(lines[1].Amount.Currency);
                if (escrowAccount.HasValue)
                    Match(lines[0], EntrySide.Debit, escrowAccount.Value, lines[1].Amount.Currency, false, null, errors);
                else
                    Add(errors, PostingErrorCode.InvalidCurrency, "Reclaim requires a supported coin currency.");
                ValidateLiability(lines[1], EntrySide.Credit, ProvenanceKind.EscrowReturn, errors);
                break;
            case PostingTemplateKind.Refund:
                ValidateSameLiabilityTransfer(lines, ProvenanceKind.RefundRestoration, errors);
                break;
            case PostingTemplateKind.PayoutReservation:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.EarnedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.EarnedHard, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.PayoutSuccess:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.PayoutFailure:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PayoutPayableHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.EarnedHardLiability, CurrencyCode.HardCoin, true, ProvenanceKind.EarnedHard, errors);
                break;
            case PostingTemplateKind.AdminWithdrawalReservation:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.AdminWithdrawalSuccess:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.ExternalClearingHard, CurrencyCode.HardCoin, false, null, errors);
                break;
            case PostingTemplateKind.AdminWithdrawalFailure:
                Match(lines[0], EntrySide.Debit, EconomyAccountCode.AdminWithdrawalPayableHard, CurrencyCode.HardCoin, false, null, errors);
                Match(lines[1], EntrySide.Credit, EconomyAccountCode.PlatformHardTreasury, CurrencyCode.HardCoin, false, null, errors);
                break;
            default:
                break;
        }
    }

    private static void ValidateMarketplaceSettlement(
        PostingLine[] lines,
        ICollection<PostingValidationError> errors)
    {
        if (lines.All(line => line.Side != EntrySide.Debit) ||
            lines.All(line => line.Side != EntrySide.Credit))
            Add(errors, PostingErrorCode.InvalidAccountShape,
                "Marketplace settlement requires buyer debits and seller/platform credits.");
        foreach (var line in lines)
        {
            if (line.Side == EntrySide.Debit)
            {
                ValidateLiability(line, EntrySide.Debit, line.Provenance, errors);
                continue;
            }
            if (line.Amount.Currency == CurrencyCode.HardCoin)
                Match(line, EntrySide.Credit, EconomyAccountCode.EarnedHardLiability,
                    CurrencyCode.HardCoin, true, ProvenanceKind.EarnedHard, errors);
            else if (line.Amount.Currency == CurrencyCode.SoftCoin)
                Match(line, EntrySide.Credit, EconomyAccountCode.SoftCoinLiability,
                    CurrencyCode.SoftCoin, true, ProvenanceKind.MarketplaceSoft, errors);
            else
                Add(errors, PostingErrorCode.InvalidCurrency,
                    "Marketplace settlement requires a supported coin currency.");
        }
    }

    private static void ValidateMarketplaceRefund(
        PostingLine[] lines,
        ICollection<PostingValidationError> errors)
    {
        if (lines.All(line => line.Side != EntrySide.Debit) ||
            lines.All(line => line.Side != EntrySide.Credit))
            Add(errors, PostingErrorCode.InvalidAccountShape,
                "Marketplace refund requires proceeds debits and buyer credits.");
        foreach (var line in lines)
        {
            if (line.Side == EntrySide.Credit)
            {
                ValidateLiability(line, EntrySide.Credit, line.Provenance, errors);
                continue;
            }

            if (line.Account == EconomyAccountCode.RecoveryReceivableHard)
            {
                Match(line, EntrySide.Debit, EconomyAccountCode.RecoveryReceivableHard,
                    CurrencyCode.HardCoin, false, null, errors);
                continue;
            }

            if (line.Account == EconomyAccountCode.RecoveryReceivableSoft)
            {
                Match(line, EntrySide.Debit, EconomyAccountCode.RecoveryReceivableSoft,
                    CurrencyCode.SoftCoin, false, null, errors);
                continue;
            }

            ValidateLiability(line, EntrySide.Debit, line.Provenance, errors);
        }
    }

    private static void ValidateSameLiabilityTransfer(PostingLine[] lines, ProvenanceKind? creditProvenance, ICollection<PostingValidationError> errors)
    {
        ValidateLiability(lines[0], EntrySide.Debit, null, errors);
        ValidateLiability(lines[1], EntrySide.Credit, creditProvenance, errors);
        if (lines[0].Account != lines[1].Account || lines[0].Amount.Currency != lines[1].Amount.Currency)
            Add(errors, PostingErrorCode.InvalidAccountShape, "Transfer legs must use the same currency liability account.");
    }

    private static void ValidateBountyEscrow(PostingLine[] lines, ICollection<PostingValidationError> errors)
    {
        var escrow = lines[^1];
        var currency = escrow.Amount.Currency;
        var expectedEscrow = EscrowFor(currency);
        if (!expectedEscrow.HasValue)
        {
            Add(errors, PostingErrorCode.InvalidCurrency, "Bounty escrow requires a supported coin currency.");
            return;
        }

        Match(escrow, EntrySide.Credit, expectedEscrow.Value, currency, false, null, errors);
        foreach (var line in lines[..^1])
        {
            ValidateLiability(line, EntrySide.Debit, line.Provenance, errors);
            if (line.Amount.Currency != currency)
                Add(errors, PostingErrorCode.InvalidCurrency, "Bounty escrow legs must use one currency.");
            if (line.Provenance is null)
                Add(errors, PostingErrorCode.InvalidProvenance, "Bounty escrow debit legs require their immutable provenance.");
            else if (currency == CurrencyCode.HardCoin &&
                     ((line.Provenance == ProvenanceKind.EarnedHard && line.Account != EconomyAccountCode.EarnedHardLiability) ||
                      (line.Provenance != ProvenanceKind.EarnedHard && line.Account != EconomyAccountCode.PurchasedHardLiability)))
                Add(errors, PostingErrorCode.InvalidAccountShape,
                    "HardCoin bounty escrow legs must retain the liability account that matches their provenance.");
            else if (currency == CurrencyCode.SoftCoin &&
                     line.Provenance is ProvenanceKind.PurchasedHard or ProvenanceKind.EarnedHard)
                Add(errors, PostingErrorCode.InvalidProvenance,
                    "SoftCoin bounty escrow legs cannot carry HardCoin provenance.");
        }
    }

    private static void ValidateBountyReclaim(PostingLine[] lines, ICollection<PostingValidationError> errors)
    {
        var currency = lines[0].Amount.Currency;
        var escrow = EscrowFor(currency);
        if (!escrow.HasValue)
        {
            Add(errors, PostingErrorCode.InvalidCurrency, "Bounty reclaim requires a supported coin currency.");
            return;
        }

        var feeOrBurnPairSeen = false;
        for (var index = 0; index < lines.Length; index += 2)
        {
            var debit = lines[index];
            if (index == lines.Length - 1)
            {
                Add(errors, PostingErrorCode.InvalidLineCount,
                    "Bounty reclaim requires a credit line for every debit line.");
                return;
            }

            var credit = lines[index + 1];
            Match(debit, EntrySide.Debit, escrow.Value, currency, false, null, errors);
            if (credit.Amount.Currency != currency || credit.Amount.Units != debit.Amount.Units)
                Add(errors, PostingErrorCode.InvalidAmount,
                    "Bounty reclaim debit and credit pairs must use the same currency and amount.");

            if (credit.WalletId is null)
            {
                feeOrBurnPairSeen = true;
                var expectedFeeAccount = currency == CurrencyCode.HardCoin
                    ? EconomyAccountCode.FeeRevenueHard
                    : EconomyAccountCode.SoftCoinReserve;
                Match(credit, EntrySide.Credit, expectedFeeAccount, currency, false, null, errors);
                continue;
            }

            if (feeOrBurnPairSeen)
            {
                Add(errors, PostingErrorCode.InvalidAccountShape,
                    "Bounty reclaim return pairs cannot follow a fee or burn pair.");
            }
            ValidateLiability(credit, EntrySide.Credit, credit.Provenance, errors);
            if (credit.Provenance is null)
                Add(errors, PostingErrorCode.InvalidProvenance,
                    "Bounty reclaim return credits must preserve immutable provenance.");
            else if (currency == CurrencyCode.HardCoin &&
                     ((credit.Provenance == ProvenanceKind.EarnedHard && credit.Account != EconomyAccountCode.EarnedHardLiability) ||
                      (credit.Provenance != ProvenanceKind.EarnedHard && credit.Account != EconomyAccountCode.PurchasedHardLiability)))
                Add(errors, PostingErrorCode.InvalidAccountShape,
                    "HardCoin bounty reclaim credits must retain the liability account matching their provenance.");
            else if (currency == CurrencyCode.SoftCoin &&
                     credit.Provenance is ProvenanceKind.PurchasedHard or ProvenanceKind.EarnedHard)
                Add(errors, PostingErrorCode.InvalidProvenance,
                    "SoftCoin bounty reclaim credits cannot carry HardCoin provenance.");
        }
    }

    private static void ValidateLiabilityAndSystemAccount(PostingLine[] lines, EntrySide liabilitySide, EconomyAccountCode? systemAccount, ICollection<PostingValidationError> errors)
    {
        ValidateLiability(lines[0], liabilitySide, null, errors);
        if (systemAccount.HasValue)
            Match(lines[1], EntrySide.Credit, systemAccount.Value, lines[0].Amount.Currency, false, null, errors);
        else
            Add(errors, PostingErrorCode.InvalidCurrency, "Template requires a supported coin currency.");
    }

    private static void ValidateLiability(
        PostingLine line,
        EntrySide side,
        ProvenanceKind? requiredProvenance,
        ICollection<PostingValidationError> errors)
    {
        var validAccount = line.Amount.Currency switch
        {
            CurrencyCode.HardCoin => line.Account is EconomyAccountCode.PurchasedHardLiability or EconomyAccountCode.EarnedHardLiability,
            CurrencyCode.SoftCoin => line.Account == EconomyAccountCode.SoftCoinLiability,
            _ => false
        };
        if (!validAccount) Add(errors, PostingErrorCode.InvalidAccountShape, "Line must use the liability account for its currency.");
        if (line.Side != side) Add(errors, PostingErrorCode.InvalidAccountShape, "Liability line has the wrong entry side.");
        if (line.WalletId is null) Add(errors, PostingErrorCode.MissingWallet, "Liability line requires a wallet.");
        if (requiredProvenance.HasValue && line.Provenance != requiredProvenance)
            Add(errors, PostingErrorCode.InvalidProvenance, "Liability line has the wrong provenance.");
    }

    private static EconomyAccountCode? ReserveFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => EconomyAccountCode.HardCoinReserve,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinReserve,
        _ => null
    };

    private static EconomyAccountCode? EscrowFor(CurrencyCode currency) => currency switch
    {
        CurrencyCode.HardCoin => EconomyAccountCode.HardCoinEscrow,
        CurrencyCode.SoftCoin => EconomyAccountCode.SoftCoinEscrow,
        _ => null
    };

    private static void Match(
        PostingLine line,
        EntrySide side,
        EconomyAccountCode account,
        CurrencyCode currency,
        bool walletRequired,
        ProvenanceKind? provenance,
        ICollection<PostingValidationError> errors)
    {
        if (line.Side != side || line.Account != account)
            Add(errors, PostingErrorCode.InvalidAccountShape, $"Line {line.Sequence} does not match its registered account shape.");
        if (line.Amount.Currency != currency)
            Add(errors, PostingErrorCode.InvalidCurrency, $"Line {line.Sequence} uses the wrong currency.");
        if (walletRequired && line.WalletId is null)
            Add(errors, PostingErrorCode.MissingWallet, $"Line {line.Sequence} requires a wallet.");
        if (!walletRequired && line.WalletId is not null)
            Add(errors, PostingErrorCode.InvalidAccountShape, $"Line {line.Sequence} cannot target a wallet.");
        if (line.Provenance != provenance)
            Add(errors, PostingErrorCode.InvalidProvenance, $"Line {line.Sequence} uses the wrong provenance.");
    }

    private static void ValidateParity(long hardUnits, long softUnits, ICollection<PostingValidationError> errors)
    {
        try
        {
            if (checked(hardUnits * Money.FixedParity.SoftCoinsPerHardCoin) != softUnits)
                Add(errors, PostingErrorCode.InvalidParity, "Principal conversion must use exactly 1 HC = 1,000 SC.");
        }
        catch (OverflowException)
        {
            Add(errors, PostingErrorCode.InvalidParity, "Principal conversion exceeds the supported integer range.");
        }
    }

    private static void Add(ICollection<PostingValidationError> errors, PostingErrorCode code, string message) =>
        errors.Add(new PostingValidationError(code, message));
}
