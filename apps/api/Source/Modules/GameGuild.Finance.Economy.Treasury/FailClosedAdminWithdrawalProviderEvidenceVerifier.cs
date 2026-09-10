namespace GameGuild.Finance.Economy.Treasury;

public sealed class FailClosedAdminWithdrawalProviderEvidenceVerifier : IAdminWithdrawalProviderEvidenceVerifier
{
    public bool Verify(AdminWithdrawalProviderReceipt receipt) => false;

    public bool Verify(AdminWithdrawalProviderEvent providerEvent) => false;
}
