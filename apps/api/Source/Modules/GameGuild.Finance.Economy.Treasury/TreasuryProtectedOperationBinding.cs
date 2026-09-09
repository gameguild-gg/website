using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Treasury;

public static class TreasuryProtectedOperationBinding
{
    public static string Proposal(
        DateOnly periodStart,
        long amountUnits,
        string destinationHash,
        string idempotencyKey)
    {
        if (periodStart.Day != 1)
            throw new ArgumentException("Withdrawal period must start on the first day of a month.", nameof(periodStart));
        if (amountUnits <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountUnits));
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
        return Hash(
            "proposal",
            periodStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            amountUnits.ToString(CultureInfo.InvariantCulture),
            destinationHash.Trim().ToLowerInvariant(),
            idempotencyKey.Trim());
    }

    public static string Dispatch(Guid runId, long expectedVersion)
    {
        if (runId == Guid.Empty)
            throw new ArgumentException("Treasury withdrawal run ID is required.", nameof(runId));
        if (expectedVersion <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        return Hash(
            "dispatch",
            runId.ToString("N"),
            expectedVersion.ToString(CultureInfo.InvariantCulture));
    }

    private static string Hash(params string[] fields)
    {
        var canonical = new StringBuilder("economy-treasury-protected-operation-v1");
        foreach (var field in fields)
            canonical.Append('|').Append(Encoding.UTF8.GetByteCount(field)).Append(':').Append(field);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }
}
