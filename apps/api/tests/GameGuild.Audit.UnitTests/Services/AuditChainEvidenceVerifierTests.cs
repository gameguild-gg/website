using System.Security.Cryptography;
using System.Text.Json;
using GameGuild.Compliance.Audit;
using Microsoft.Extensions.Options;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

public sealed class AuditChainEvidenceVerifierTests
{
    [Fact]
    public void Recovers_only_the_original_signed_submicrosecond_of_a_legacy_timestamp()
    {
        var (entry, signer, timestamp) = LegacyEntry();
        var verifier = new AuditChainEvidenceVerifier(signer);
        var (verified, recovered) = verifier.VerifyEntryAndTimestamp(entry);
        Assert.True(verified);
        Assert.Equal(timestamp, recovered);
        typeof(TamperEvidentAuditLog).GetProperty(nameof(TamperEvidentAuditLog.Timestamp))!.SetValue(entry, entry.Timestamp.AddTicks(10));
        Assert.False(verifier.VerifyEntry(entry));
    }

    [Fact]
    public void Legacy_recovery_still_rejects_content_changes_and_unknown_signing_keys()
    {
        var (entry, signer, _) = LegacyEntry();
        typeof(TamperEvidentAuditLog).GetProperty(nameof(TamperEvidentAuditLog.Changes))!.SetValue(entry, "changed");
        Assert.False(new AuditChainEvidenceVerifier(signer).VerifyEntry(entry));
        var (original, _, _) = LegacyEntry();
        Assert.False(new AuditChainEvidenceVerifier(new EcdsaCryptographicSigningService(Options.Create(new AuditSigningOptions()))).VerifyEntry(original));
    }

    [Fact]
    public void New_audit_entries_use_storage_precision_before_they_are_signed()
    {
        var precise = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc).AddTicks(7);
        SystemClock.SetProvider(new FixedTime(precise));
        try
        {
            var entry = Create();
            Assert.Equal(0, entry.Timestamp.Ticks % 10);
            Assert.Equal(precise.AddTicks(-7), entry.Timestamp);
        }
        finally { SystemClock.Reset(); }
    }

    private static (TamperEvidentAuditLog Entry, ICryptographicSigningService Signer, DateTime OriginalTimestamp) LegacyEntry()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var options = new AuditSigningOptions { ActiveKeyId = "test" };
        options.Keys["test"] = new AuditSigningKeyOptions { PrivateKeyPem = key.ExportECPrivateKeyPem() };
        var signer = new EcdsaCryptographicSigningService(Options.Create(options));
        var entry = Create();
        var originalTimestamp = entry.Timestamp.AddTicks(7);
        // Independently reproduce the legacy wire contract, including its original 100 ns timestamp.
        var content = JsonSerializer.Serialize(new
        {
            entry.Id, entry.TenantId, entry.UserId, entry.SessionId, entry.CorrelationId, entry.Action,
            entry.EntityType, entry.EntityId, entry.BeforeSnapshot, entry.AfterSnapshot, entry.Changes,
            entry.RiskLevel, entry.IpAddress, entry.UserAgent, entry.Country, entry.Region, entry.City,
            Timestamp = originalTimestamp
        });
        var hash = signer.ComputeContentHash(content);
        var chain = signer.ComputeChainHash(hash, entry.PreviousHash, entry.SequenceNumber);
        entry.SetCryptographicHashes(hash, chain);
        entry.Sign(signer.SignData(chain, "test"), "test");
        return (entry, signer, originalTimestamp);
    }

    private static TamperEvidentAuditLog Create() => TamperEvidentAuditLog.Create(Guid.NewGuid(), Guid.NewGuid(), "AuditTest",
        "Test", null, null, null, "{}", "High", "192.0.2.1", "test", null, null, null, string.Empty, 1);
    private sealed class FixedTime(DateTime instant) : TimeProvider { public override DateTimeOffset GetUtcNow() => instant; }
}
