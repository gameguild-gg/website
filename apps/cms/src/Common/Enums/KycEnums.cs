using System.ComponentModel;

namespace GameGuild.Common.Enums;

public enum KycProvider
{
    [Description("SumSub KYC verification provider")]
    Sumsub,

    [Description("Shufti Pro KYC verification provider")]
    Shufti,

    [Description("Onfido KYC verification provider")]
    Onfido,

    [Description("Jumio KYC verification provider")]
    Jumio,

    [Description("Custom or internal KYC verification process")]
    Custom
}

public enum KycVerificationStatus
{
    [Description("Verification request has been submitted but not completed")]
    Pending,

    [Description("Provider is processing the verification")]
    InProgress,

    [Description("KYC verification passed")]
    Approved,

    [Description("KYC verification failed")]
    Rejected,

    [Description("Verification was approved but later suspended")]
    Suspended,

    [Description("Verification has expired and needs renewal")]
    Expired
}
