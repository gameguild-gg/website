using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Risk;

/// <summary>
/// Derives the opaque, tenant-bound subject key shared by Economy and its
/// compliance evidence providers. Raw actor identifiers must never be used as
/// evidence lookup keys.
/// </summary>
public static class EconomySubjectReference
{
    public static string ForUser(Guid tenantId, Guid actorId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A tenant is required.", nameof(tenantId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("An actor is required.", nameof(actorId));

        var canonical = $"economy-subject-v1|{tenantId:N}|{actorId:N}";
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
