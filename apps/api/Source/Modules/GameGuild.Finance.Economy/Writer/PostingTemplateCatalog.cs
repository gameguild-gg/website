using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Writer;

public sealed record PostingTemplateRegistration(
    PostingTemplateKind Kind,
    int Version,
    PostingAuthority Authority,
    int LineCount,
    SourceConfirmationState? RequiredSourceState,
    bool RequiresReserveAuthorization,
    int? MaximumLineCount = null)
{
    public bool AllowsLineCount(int count) =>
        count >= LineCount && (!MaximumLineCount.HasValue || count <= MaximumLineCount.Value);
}

public static class PostingTemplateCatalog
{
    private static readonly IReadOnlyList<PostingTemplateRegistration> Registrations =
    [
        Register(PostingTemplateKind.ConfirmedTopUpMint, PostingAuthority.ProviderConfirmation, 2, SourceConfirmationState.Confirmed),
        Register(PostingTemplateKind.ProviderReversalFull, PostingAuthority.ProviderConfirmation, 2, SourceConfirmationState.Reversed),
        Register(PostingTemplateKind.ProviderReversalPartial, PostingAuthority.ProviderConfirmation, 2),
        Register(PostingTemplateKind.ProviderConvertedSoftReversal, PostingAuthority.ProviderConfirmation, 4),
        Register(PostingTemplateKind.ProviderReversalDebt, PostingAuthority.ProviderConfirmation, 2),
        Register(PostingTemplateKind.ProviderReversalLoss, PostingAuthority.ProviderConfirmation, 2),
        Register(PostingTemplateKind.Spend, PostingAuthority.WalletOwner, 2),
        Register(PostingTemplateKind.HardToSoftConversion, PostingAuthority.WalletOwner, 4),
        Register(PostingTemplateKind.HardToSoftConversionFee, PostingAuthority.WalletOwner, 2),
        Register(PostingTemplateKind.SystemBackedGrant, PostingAuthority.PlatformSystem, 4),
        Register(PostingTemplateKind.AdRewardIssuance, PostingAuthority.PlatformSystem, 2),
        Register(PostingTemplateKind.Burn, PostingAuthority.WalletOwner, 2),
        Register(PostingTemplateKind.Escrow, PostingAuthority.WalletOwner, 2),
        RegisterVariable(PostingTemplateKind.BountyEscrow, PostingAuthority.WalletOwner, 2),
        Register(PostingTemplateKind.BountyClaim, PostingAuthority.EscrowCoordinator, 2),
        RegisterVariable(PostingTemplateKind.BountyReclaim, PostingAuthority.EscrowCoordinator, 2),
        RegisterVariable(PostingTemplateKind.MarketplaceSettlement, PostingAuthority.MarketplaceCoordinator, 3),
        RegisterVariable(PostingTemplateKind.MarketplaceRefund, PostingAuthority.MarketplaceCoordinator, 2),
        Register(PostingTemplateKind.Reclaim, PostingAuthority.EscrowCoordinator, 2),
        Register(PostingTemplateKind.Refund, PostingAuthority.EscrowCoordinator, 2),
        Register(PostingTemplateKind.PayoutReservation, PostingAuthority.PayoutCoordinator, 2),
        Register(PostingTemplateKind.PayoutSuccess, PostingAuthority.PayoutCoordinator, 2),
        Register(PostingTemplateKind.PayoutFailure, PostingAuthority.PayoutCoordinator, 2),
        Register(PostingTemplateKind.AdminWithdrawalReservation, PostingAuthority.Administrator, 2),
        Register(PostingTemplateKind.AdminWithdrawalSuccess, PostingAuthority.Administrator, 2),
        Register(PostingTemplateKind.AdminWithdrawalFailure, PostingAuthority.Administrator, 2)
    ];

    public static IReadOnlyList<PostingTemplateRegistration> All => Registrations;

    public static PostingTemplateRegistration? Find(PostingTemplateKind kind, int version) =>
        Registrations.SingleOrDefault(item => item.Kind == kind && item.Version == version);

    private static PostingTemplateRegistration Register(
        PostingTemplateKind kind,
        PostingAuthority authority,
        int lineCount,
        SourceConfirmationState? sourceState = null) =>
        new(kind, PostingTemplate.CurrentVersion, authority, lineCount, sourceState, true);

    private static PostingTemplateRegistration RegisterVariable(
        PostingTemplateKind kind,
        PostingAuthority authority,
        int minimumLineCount,
        SourceConfirmationState? sourceState = null) =>
        new(kind, PostingTemplate.CurrentVersion, authority, minimumLineCount, sourceState, true, null);
}
