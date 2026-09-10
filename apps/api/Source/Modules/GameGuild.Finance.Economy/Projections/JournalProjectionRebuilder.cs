using GameGuild.Finance.Economy.Contracts;
using GameGuild.Finance.Economy.Ledger;

namespace GameGuild.Finance.Economy.Projections;

public sealed record JournalWalletProjection(
    long PurchasedHard,
    long EarnedHard,
    long RestrictedHard,
    long Soft);

public sealed record JournalProjectionDifference(string Component, long JournalUnits, long LotUnits);

public sealed record JournalProjectionComparison(IReadOnlyList<JournalProjectionDifference> Differences)
{
    public bool IsMatch => Differences.Count == 0;
}

public static class JournalProjectionRebuilder
{
    public static JournalWalletProjection Rebuild(WalletId walletId, IReadOnlyList<JournalEntry> journal)
    {
        ArgumentNullException.ThrowIfNull(journal);

        var purchasedHard = 0L;
        var earnedHard = 0L;
        var restrictedHard = 0L;
        var soft = 0L;
        foreach (var line in journal.SelectMany(entry => entry.Lines).Where(line => line.WalletId == walletId))
        {
            var units = line.Side == EntrySide.Credit ? line.Amount.Units : -line.Amount.Units;
            if (line.Amount.Currency == CurrencyCode.SoftCoin)
            {
                soft = Add(soft, units);
                continue;
            }

            switch (line.Provenance)
            {
                case ProvenanceKind.PurchasedHard:
                    purchasedHard = Add(purchasedHard, units);
                    break;
                case ProvenanceKind.EarnedHard:
                    earnedHard = Add(earnedHard, units);
                    break;
                default:
                    restrictedHard = Add(restrictedHard, units);
                    break;
            }
        }

        if (purchasedHard < 0 || earnedHard < 0 || restrictedHard < 0 || soft < 0)
            throw new ProjectionCorruptionException("Journal recomputation produced a negative wallet component.");
        return new JournalWalletProjection(purchasedHard, earnedHard, restrictedHard, soft);
    }

    public static JournalProjectionComparison Compare(
        JournalWalletProjection journal,
        WalletBalanceProjection lots)
    {
        ArgumentNullException.ThrowIfNull(journal);
        ArgumentNullException.ThrowIfNull(lots);

        var differences = new List<JournalProjectionDifference>();
        AddDifference(differences, "PurchasedHard", journal.PurchasedHard, lots.PurchasedHard);
        AddDifference(differences, "EarnedHard", journal.EarnedHard, lots.EarnedHard);
        AddDifference(differences, "RestrictedHard", journal.RestrictedHard, lots.RestrictedHard);
        AddDifference(differences, "Soft", journal.Soft, lots.Soft);
        return new JournalProjectionComparison(differences);
    }

    private static void AddDifference(
        ICollection<JournalProjectionDifference> differences,
        string component,
        long journalUnits,
        long lotUnits)
    {
        if (journalUnits != lotUnits)
            differences.Add(new JournalProjectionDifference(component, journalUnits, lotUnits));
    }

    private static long Add(long left, long right)
    {
        try
        {
            return checked(left + right);
        }
        catch (OverflowException exception)
        {
            throw new ProjectionCorruptionException($"Journal projection arithmetic overflowed: {exception.Message}");
        }
    }
}
