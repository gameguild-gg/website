using System.Security.Cryptography;
using System.Text;

namespace GameGuild.Finance.Economy.Risk;

public enum RiskEntityType
{
    Account = 1,
    Tenant = 2,
    KycIdentity = 3,
    PaymentInstrument = 4,
    BankAccount = 5,
    PayoutDestination = 6,
    DeviceRiskToken = 7,
    IpAddress = 8,
    IpPrefix = 9,
    AutonomousSystem = 10,
    Referral = 11,
    Project = 12,
    Product = 13,
    MarketplaceCounterparty = 14,
    ProviderObject = 15,
    Session = 16
}

public readonly record struct RiskEntityNode
{
    public RiskEntityNode(RiskEntityType type, string identifierHash)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        ArgumentException.ThrowIfNullOrWhiteSpace(identifierHash);
        Type = type;
        IdentifierHash = identifierHash.Trim();
    }

    public RiskEntityType Type { get; }
    public string IdentifierHash { get; }

    public static RiskEntityNode FromHmac(RiskEntityType type, string identifier, byte[] secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0) throw new ArgumentException("HMAC secret cannot be empty.", nameof(secret));
        var digest = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(identifier));
        return new RiskEntityNode(type, Convert.ToHexString(digest));
    }
}

public sealed record EntityRiskCluster(
    string Id,
    long Version,
    string EvidenceHash,
    IReadOnlyList<RiskEntityNode> Nodes);

public sealed class EntityRiskGraph
{
    private readonly object _gate = new();
    private readonly Dictionary<RiskEntityNode, HashSet<RiskEntityNode>> _edges = [];
    private readonly List<string> _evidence = [];
    private long _version;

    public void Link(RiskEntityNode left, RiskEntityNode right, string evidenceHash, DateTimeOffset observedAt)
    {
        if (left == right) throw new ArgumentException("A risk entity cannot link to itself.", nameof(right));
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceHash);
        lock (_gate)
        {
            AddEdge(left, right);
            AddEdge(right, left);
            _version++;
            _evidence.Add($"{evidenceHash.Trim()}:{observedAt:O}");
        }
    }

    public EntityRiskCluster ClusterFor(RiskEntityNode seed)
    {
        lock (_gate)
        {
            var visited = new HashSet<RiskEntityNode> { seed };
            var pending = new Queue<RiskEntityNode>();
            pending.Enqueue(seed);
            while (pending.TryDequeue(out var current))
            {
                if (!_edges.TryGetValue(current, out var neighbors)) continue;
                foreach (var neighbor in neighbors)
                    if (visited.Add(neighbor)) pending.Enqueue(neighbor);
            }

            var nodes = visited.OrderBy(node => node.Type).ThenBy(node => node.IdentifierHash, StringComparer.Ordinal).ToArray();
            var identity = string.Join('|', nodes.Select(node => $"{(int)node.Type}:{node.IdentifierHash}"));
            var id = Hash(identity);
            var evidence = _evidence.Count == 0 ? Hash(identity) : Hash(string.Join('|', _evidence));
            return new EntityRiskCluster(id, _version, evidence, nodes);
        }
    }

    private void AddEdge(RiskEntityNode from, RiskEntityNode to)
    {
        if (!_edges.TryGetValue(from, out var edges))
        {
            edges = [];
            _edges.Add(from, edges);
        }

        edges.Add(to);
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
