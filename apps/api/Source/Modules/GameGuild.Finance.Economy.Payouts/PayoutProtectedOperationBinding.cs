using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Payouts;

public static class PayoutProtectedOperationBinding
{
    public static string Reservation(Guid requestId)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Payout request ID is required.", nameof(requestId));
        return Hash("reservation", requestId.ToString("N"));
    }

    public static string Dispatch(Guid operationId, long expectedVersion)
    {
        if (operationId == Guid.Empty)
            throw new ArgumentException("Payout operation ID is required.", nameof(operationId));
        if (expectedVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        return Hash(
            "dispatch",
            operationId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture));
    }

    private static string Hash(params string[] fields)
    {
        var canonical = new StringBuilder("economy-payout-protected-operation-v1");
        foreach (var field in fields)
            canonical.Append('|').Append(Encoding.UTF8.GetByteCount(field)).Append(':').Append(field);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
