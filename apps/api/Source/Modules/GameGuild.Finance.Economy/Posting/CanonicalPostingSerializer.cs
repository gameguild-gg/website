using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GameGuild.Finance.Economy.Contracts;

namespace GameGuild.Finance.Economy.Posting;

public static class CanonicalPostingSerializer
{
    public static byte[] Serialize(PostingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var builder = new StringBuilder(512);
        Append(builder, request.Id.Value.ToString("N"));
        Append(builder, ((int)request.Template.Kind).ToString(CultureInfo.InvariantCulture));
        Append(builder, request.Template.Version.ToString(CultureInfo.InvariantCulture));
        Append(builder, request.IdempotencyKey.Value);
        Append(builder, ((int)request.Authority).ToString(CultureInfo.InvariantCulture));
        Append(builder, request.ReserveVersion.Value.ToString(CultureInfo.InvariantCulture));
        Append(builder, request.PolicyVersion.Value.ToString(CultureInfo.InvariantCulture));
        Append(builder, request.RequestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        AppendSource(builder, request.Source);

        foreach (var line in request.Lines.OrderBy(line => line.Sequence))
        {
            Append(builder, line.Sequence.ToString(CultureInfo.InvariantCulture));
            Append(builder, ((int)line.Side).ToString(CultureInfo.InvariantCulture));
            Append(builder, ((int)line.Account).ToString(CultureInfo.InvariantCulture));
            Append(builder, ((int)line.Amount.Currency).ToString(CultureInfo.InvariantCulture));
            Append(builder, line.Amount.Units.ToString(CultureInfo.InvariantCulture));
            Append(builder, line.WalletId?.Value.ToString("N") ?? string.Empty);
            Append(builder, line.LotId?.Value.ToString("N") ?? string.Empty);
            Append(builder, line.Provenance.HasValue ? ((int)line.Provenance.Value).ToString(CultureInfo.InvariantCulture) : string.Empty);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    public static string ComputeHash(PostingRequest request) => Convert.ToHexStringLower(SHA256.HashData(Serialize(request)));

    private static void AppendSource(StringBuilder builder, SourceStampContract? source)
    {
        if (source is null)
        {
            Append(builder, string.Empty);
            return;
        }

        Append(builder, source.Id.Value.ToString("N"));
        Append(builder, source.EvidenceHash);
        Append(builder, ((int)source.State).ToString(CultureInfo.InvariantCulture));
        Append(builder, source.ObservedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(builder, source.ConfirmedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        Append(builder, source.ProviderReference ?? string.Empty);
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
}
