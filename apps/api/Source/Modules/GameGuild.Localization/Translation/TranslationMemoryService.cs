using Microsoft.Extensions.Logging;

namespace GameGuild.Localization;

/// <summary>
/// Translation memory service interface.
/// </summary>
public interface ITranslationMemoryService
{
    Task<TranslationMemoryEntry?> FindMatchAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        double minimumSimilarity = 0.8,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TranslationMemoryEntry>> SearchAsync(
        string searchText,
        string? sourceLanguage = null,
        string? targetLanguage = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    Task<TranslationMemoryEntry> AddEntryAsync(
        string sourceText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        string? context = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default);

    Task UpdateQualityScoreAsync(
        Guid entryId,
        double qualityScore,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<TranslationMemoryEntry>> GetRecentEntriesAsync(
        int count = 100,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Translation memory service implementation.
/// </summary>
public sealed class TranslationMemoryService : ITranslationMemoryService
{
    private readonly ILogger<TranslationMemoryService> _logger;
    private readonly Dictionary<Guid, TranslationMemoryEntry> _entries;
    private readonly Dictionary<string, List<Guid>> _sourceIndex;

    public TranslationMemoryService(ILogger<TranslationMemoryService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _entries = new Dictionary<Guid, TranslationMemoryEntry>();
        _sourceIndex = new Dictionary<string, List<Guid>>();
    }

    public Task<TranslationMemoryEntry?> FindMatchAsync(
        string sourceText,
        string sourceLanguage,
        string targetLanguage,
        double minimumSimilarity = 0.8,
        CancellationToken cancellationToken = default)
    {
        var normalizedSource = NormalizeText(sourceText);
        var key = $"{sourceLanguage}:{normalizedSource}";

        if (_sourceIndex.TryGetValue(key, out var entryIds))
        {
            var matches = entryIds
                .Select(id => _entries[id])
                .Where(e => e.TargetLanguage == targetLanguage)
                .Select(e => new
                {
                    Entry = e,
                    Similarity = CalculateSimilarity(sourceText, e.SourceText)
                })
                .Where(x => x.Similarity >= minimumSimilarity)
                .OrderByDescending(x => x.Similarity)
                .ThenByDescending(x => x.Entry.QualityScore)
                .FirstOrDefault();

            if (matches != null)
            {
                _logger.LogInformation("Found translation memory match with {Similarity}% similarity",
                    matches.Similarity * 100);
                return Task.FromResult<TranslationMemoryEntry?>(matches.Entry);
            }
        }

        _logger.LogDebug("No translation memory match found for '{SourceText}'", sourceText);
        return Task.FromResult<TranslationMemoryEntry?>(null);
    }

    public Task<IEnumerable<TranslationMemoryEntry>> SearchAsync(
        string searchText,
        string? sourceLanguage = null,
        string? targetLanguage = null,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        var normalizedSearch = NormalizeText(searchText);

        var results = _entries.Values
            .Where(e => string.IsNullOrEmpty(sourceLanguage) || e.SourceLanguage == sourceLanguage)
            .Where(e => string.IsNullOrEmpty(targetLanguage) || e.TargetLanguage == targetLanguage)
            .Select(e => new
            {
                Entry = e,
                Similarity = CalculateSimilarity(normalizedSearch, e.SourceText)
            })
            .Where(x => x.Similarity > 0.5)
            .OrderByDescending(x => x.Similarity)
            .ThenByDescending(x => x.Entry.QualityScore)
            .Take(maxResults)
            .Select(x => x.Entry);

        return Task.FromResult<IEnumerable<TranslationMemoryEntry>>(results.ToList());
    }

    public Task<TranslationMemoryEntry> AddEntryAsync(
        string sourceText,
        string translatedText,
        string sourceLanguage,
        string targetLanguage,
        string? context = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new TranslationMemoryEntry
        {
            Id = Guid.NewGuid(),
            SourceText = sourceText,
            TranslatedText = translatedText,
            SourceLanguage = sourceLanguage,
            TargetLanguage = targetLanguage,
            Context = context,
            Metadata = metadata ?? new Dictionary<string, string>(),
            QualityScore = 1.0,
            CreatedAt = SystemClock.UtcNow,
            UsageCount = 0
        };

        _entries[entry.Id] = entry;

        var normalizedSource = NormalizeText(sourceText);
        var key = $"{sourceLanguage}:{normalizedSource}";

        if (!_sourceIndex.ContainsKey(key))
        {
            _sourceIndex[key] = new List<Guid>();
        }
        _sourceIndex[key].Add(entry.Id);

        _logger.LogInformation("Added translation memory entry {EntryId} for {SourceLanguage} -> {TargetLanguage}",
            entry.Id, sourceLanguage, targetLanguage);

        return Task.FromResult(entry);
    }

    public Task UpdateQualityScoreAsync(
        Guid entryId,
        double qualityScore,
        CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(entryId, out var entry))
        {
            throw new InvalidOperationException($"Entry {entryId} not found");
        }

        entry.QualityScore = Math.Clamp(qualityScore, 0.0, 1.0);
        entry.LastUpdatedAt = SystemClock.UtcNow;

        _logger.LogInformation("Updated quality score for entry {EntryId} to {QualityScore}",
            entryId, qualityScore);

        return Task.CompletedTask;
    }

    public Task<IEnumerable<TranslationMemoryEntry>> GetRecentEntriesAsync(
        int count = 100,
        CancellationToken cancellationToken = default)
    {
        var entries = _entries.Values
            .OrderByDescending(e => e.CreatedAt)
            .Take(count);

        return Task.FromResult<IEnumerable<TranslationMemoryEntry>>(entries.ToList());
    }

    private static string NormalizeText(string text)
    {
        return text.Trim().ToLowerInvariant();
    }

    private static double CalculateSimilarity(string text1, string text2)
    {
        // Simple similarity calculation using Levenshtein distance
        var distance = LevenshteinDistance(text1, text2);
        var maxLength = Math.Max(text1.Length, text2.Length);

        if (maxLength == 0)
        {
            return 1.0;
        }

        return 1.0 - ((double)distance / maxLength);
    }

    private static int LevenshteinDistance(string s1, string s2)
    {
        var n = s1.Length;
        var m = s2.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        for (var i = 0; i <= n; i++)
        {
            d[i, 0] = i;
        }

        for (var j = 0; j <= m; j++)
        {
            d[0, j] = j;
        }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s2[j - 1] == s1[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}

/// <summary>
/// Translation memory entry entity.
/// </summary>
public sealed class TranslationMemoryEntry
{
    public required Guid Id { get; init; }
    public required string SourceText { get; init; }
    public required string TranslatedText { get; init; }
    public required string SourceLanguage { get; init; }
    public required string TargetLanguage { get; init; }
    public string? Context { get; init; }
    public required Dictionary<string, string> Metadata { get; init; }
    public required double QualityScore { get; set; }
    public required DateTime CreatedAt { get; init; }
    public DateTime? LastUpdatedAt { get; set; }
    public required int UsageCount { get; set; }
}
