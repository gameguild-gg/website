using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GameGuild.Finance.Economy.Ledger;

public enum ChainAnchorKind
{
    Periodic = 1,
    OnDemand = 2
}

public sealed class ChainAnchor
{
    public ChainAnchor(
        Guid id,
        ChainAnchorKind kind,
        long headSequence,
        string headHash,
        string? dispatchSnapshotHash,
        string credentialId,
        string canonicalPayload,
        string signature,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty) throw new ArgumentException("Anchor ID cannot be empty.", nameof(id));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(headSequence);
        ArgumentException.ThrowIfNullOrWhiteSpace(headHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        Id = id;
        Kind = kind;
        HeadSequence = headSequence;
        HeadHash = headHash;
        DispatchSnapshotHash = dispatchSnapshotHash;
        CredentialId = credentialId;
        CanonicalPayload = canonicalPayload;
        Signature = signature;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public ChainAnchorKind Kind { get; }
    public long HeadSequence { get; }
    public string HeadHash { get; }
    public string? DispatchSnapshotHash { get; }
    public string CredentialId { get; }
    public string CanonicalPayload { get; }
    public string Signature { get; }
    public DateTimeOffset CreatedAt { get; }
}

public interface IChainHeadSigner
{
    string CredentialId { get; }
    string Sign(string canonicalPayload);
}

public sealed class HmacChainHeadSigner : IChainHeadSigner
{
    private readonly byte[] _key;

    public HmacChainHeadSigner(string credentialId, byte[] key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(credentialId);
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32) throw new ArgumentException("Anchor signing keys must be at least 256 bits.", nameof(key));
        CredentialId = credentialId.Trim();
        _key = key.ToArray();
    }

    public string CredentialId { get; }

    public string Sign(string canonicalPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPayload);
        using var hmac = new HMACSHA256(_key);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalPayload)));
    }

    public bool Verify(string canonicalPayload, string signature)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        var expected = Convert.FromBase64String(Sign(canonicalPayload));
        var supplied = Convert.FromBase64String(signature);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}

public interface IChainAnchorOutboxFactory
{
    ImmutableOutboxMessage Persist(ChainAnchor anchor);
}

public sealed class ChainAnchorOutboxFactory : IChainAnchorOutboxFactory
{
    public ImmutableOutboxMessage Persist(ChainAnchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        var payload = JsonSerializer.Serialize(new AnchorPersistencePayload(
            anchor.Id,
            anchor.Kind,
            anchor.HeadSequence,
            anchor.HeadHash,
            anchor.DispatchSnapshotHash,
            anchor.CredentialId,
            anchor.CanonicalPayload,
            anchor.Signature,
            anchor.CreatedAt));
        return new ImmutableOutboxMessage(Guid.NewGuid(), "economy.chain-anchor.persist.v1", payload, anchor.CreatedAt);
    }

    private sealed record AnchorPersistencePayload(
        Guid Id,
        ChainAnchorKind Kind,
        long HeadSequence,
        string HeadHash,
        string? DispatchSnapshotHash,
        string CredentialId,
        string CanonicalPayload,
        string Signature,
        DateTimeOffset CreatedAt);
}

public sealed class ChainAnchorService
{
    private readonly InMemoryLedgerKernelStore _store;
    private readonly IChainHeadSigner _signer;
    private readonly IChainAnchorOutboxFactory _outboxFactory;

    public ChainAnchorService(
        InMemoryLedgerKernelStore store,
        IChainHeadSigner signer,
        IChainAnchorOutboxFactory? outboxFactory = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        _outboxFactory = outboxFactory ?? new ChainAnchorOutboxFactory();
    }

    public ChainAnchor CreatePeriodic(DateTimeOffset createdAt) =>
        Create(ChainAnchorKind.Periodic, null, createdAt);

    public ChainAnchor CreateOnDemand(string dispatchSnapshotHash, DateTimeOffset createdAt) =>
        Create(ChainAnchorKind.OnDemand, NormalizeHash(dispatchSnapshotHash), createdAt);

    private ChainAnchor Create(
        ChainAnchorKind kind,
        string? dispatchSnapshotHash,
        DateTimeOffset createdAt) =>
        _store.Execute(transaction =>
        {
            var head = transaction.JournalHead
                ?? throw new InvalidOperationException("A journal head is required before creating an anchor.");
            var canonical = CanonicalPayload(
                kind,
                head.Sequence,
                head.Hash,
                dispatchSnapshotHash,
                _signer.CredentialId,
                createdAt);
            var anchor = new ChainAnchor(
                Guid.NewGuid(),
                kind,
                head.Sequence,
                head.Hash,
                dispatchSnapshotHash,
                _signer.CredentialId,
                canonical,
                _signer.Sign(canonical),
                createdAt);
            transaction.AddAnchor(anchor);
            transaction.AddOutbox(_outboxFactory.Persist(anchor));
            return anchor;
        });

    private static string CanonicalPayload(
        ChainAnchorKind kind,
        long headSequence,
        string headHash,
        string? dispatchSnapshotHash,
        string credentialId,
        DateTimeOffset createdAt) =>
        string.Join(
            '|',
            "economy-chain-anchor-v1",
            ((int)kind).ToString(CultureInfo.InvariantCulture),
            headSequence.ToString(CultureInfo.InvariantCulture),
            headHash,
            dispatchSnapshotHash ?? string.Empty,
            credentialId,
            createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));

    private static string NormalizeHash(string hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hash);
        hash = hash.Trim();
        if (hash.Length != 64 || hash.Any(character => !Uri.IsHexDigit(character)))
            throw new ArgumentException("Dispatch snapshot hash must be a SHA-256 hexadecimal value.", nameof(hash));
        return hash.ToLowerInvariant();
    }
}
