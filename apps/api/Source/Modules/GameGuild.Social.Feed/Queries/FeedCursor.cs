using System.Text.Json;

namespace GameGuild.Social.Feed;

public readonly record struct FeedCursor(DateTime SortAt, Guid Id)
{
    private sealed record Payload(int Version, long Ticks, Guid Id);

    public string Encode()
    {
        var utc = SortAt.Kind == DateTimeKind.Utc
            ? SortAt
            : SortAt.ToUniversalTime();
        var json = JsonSerializer.SerializeToUtf8Bytes(new Payload(1, utc.Ticks, Id));
        return Convert.ToBase64String(json)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static bool TryDecode(string? value, out FeedCursor cursor)
    {
        cursor = default;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
            return false;

        try
        {
            var encoded = value.Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            var payload = JsonSerializer.Deserialize<Payload>(Convert.FromBase64String(encoded));

            if (payload is null || payload.Version != 1 || payload.Id == Guid.Empty)
                return false;

            var timestamp = new DateTime(payload.Ticks, DateTimeKind.Utc);
            if (timestamp < DateTime.UnixEpoch || timestamp >= new DateTime(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                return false;

            cursor = new FeedCursor(timestamp, payload.Id);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            return false;
        }
    }
}
