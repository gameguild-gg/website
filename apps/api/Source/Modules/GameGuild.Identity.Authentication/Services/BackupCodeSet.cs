using System.Globalization;

namespace GameGuild.Identity.Authentication;

/// <summary>Stores issued-set metadata without guessing the original size of legacy sets.</summary>
internal sealed record BackupCodeSet(int? IssuedCount, List<string> Hashes)
{
    private const string Prefix = "set-v1$";

    public static BackupCodeSet Read(string? stored)
    {
        if (string.IsNullOrEmpty(stored)) { return new BackupCodeSet(null, []); }
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return new BackupCodeSet(null, stored.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList());
        }

        var delimiter = stored.IndexOf('$', Prefix.Length);
        if (delimiter < 0 || !int.TryParse(stored.AsSpan(Prefix.Length, delimiter - Prefix.Length),
                NumberStyles.None, CultureInfo.InvariantCulture, out var total) || total is < 1 or > 20)
        {
            throw new InvalidOperationException("Invalid backup-code set metadata.");
        }

        var hashes = stored[(delimiter + 1)..].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (hashes.Count > total) { throw new InvalidOperationException("Invalid backup-code set size."); }
        return new BackupCodeSet(total, hashes);
    }

    public string Serialize() => IssuedCount is { } total
        ? $"{Prefix}{total.ToString(CultureInfo.InvariantCulture)}${string.Join(',', Hashes)}"
        : string.Join(',', Hashes);
}
