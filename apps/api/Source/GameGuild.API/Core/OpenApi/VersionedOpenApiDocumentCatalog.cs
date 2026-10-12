using System.Globalization;
using Asp.Versioning.ApiExplorer;

namespace GameGuild.API.Core.OpenApi;

/// <summary>Provides unique version documents and compatible controller-group aliases.</summary>
public sealed class VersionedOpenApiDocumentCatalog
{
    /// <summary>The default API explorer version group format.</summary>
    public static string DefaultGroupNameFormat => "'v'VVV";

    private readonly Dictionary<string, ApiVersionDescription> documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> groupScopes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Builds stable document names from discovered versions and controller groups.</summary>
    public VersionedOpenApiDocumentCatalog(IEnumerable<ApiVersionDescription> descriptions)
        : this(descriptions, DefaultGroupNameFormat)
    {
        // The default constructor preserves the standard explorer naming format.
    }

    /// <summary>Builds document names using a configured version format.</summary>
    public VersionedOpenApiDocumentCatalog(IEnumerable<ApiVersionDescription> descriptions, string groupNameFormat)
    {
        ArgumentNullException.ThrowIfNull(descriptions);
        var source = descriptions.ToArray();
        foreach (var version in source.GroupBy(description => description.ApiVersion)
                     .OrderBy(group => group.Key))
        {
            var name = version.Key.ToString(groupNameFormat, CultureInfo.InvariantCulture);
            if (documents.TryGetValue(name, out var collision) && !collision.ApiVersion.Equals(version.Key))
            {
                throw new ArgumentException(
                    $"The version group format gives different API versions the same document name '{name}'.",
                    nameof(groupNameFormat));
            }
            documents[name] = new ApiVersionDescription(version.Key, name,
                version.All(description => description.IsDeprecated), version.First().SunsetPolicy);
        }

        var groups = source.GroupBy(description => description.GroupName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
        var pendingAliases = new List<(ApiVersionDescription Description, string Scope)>();
        foreach (var group in groups)
        {
            var distinct = group.DistinctBy(description => description.ApiVersion).ToArray();
            foreach (var description in distinct)
            {
                var canonicalName = description.ApiVersion.ToString(groupNameFormat, CultureInfo.InvariantCulture);
                if (string.Equals(description.GroupName, canonicalName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Reserve existing unambiguous names before allocating new qualified aliases.
                if (distinct.Length == 1 && !documents.ContainsKey(description.GroupName))
                {
                    documents.Add(description.GroupName, description);
                    groupScopes.Add(description.GroupName, description.GroupName);
                }
                else
                {
                    pendingAliases.Add((description, description.GroupName));
                }
            }
        }
        foreach (var (description, scope) in pendingAliases)
        {
            var canonicalName = description.ApiVersion.ToString(groupNameFormat, CultureInfo.InvariantCulture);
            var preferred = scope + "." + canonicalName;
            var name = preferred;
            var suffix = 2;
            while (documents.ContainsKey(name))
            {
                name = preferred + "." + (suffix++).ToString(CultureInfo.InvariantCulture);
            }
            documents.Add(name, new ApiVersionDescription(description.ApiVersion, name,
                description.IsDeprecated, description.SunsetPolicy));
            groupScopes.Add(name, scope);
        }
        Descriptions = documents.Values.OrderBy(description => description.GroupName, StringComparer.Ordinal).ToArray();
    }

    /// <summary>Gets the exact base documents used by both Swagger generation and its UI.</summary>
    public IReadOnlyList<ApiVersionDescription> Descriptions { get; }

    /// <summary>Finds a registered base document.</summary>
    public bool TryGetDescription(string name, out ApiVersionDescription? description) =>
        documents.TryGetValue(name, out description);

    /// <summary>Finds the controller group restricting a document alias, if present.</summary>
    public bool TryGetGroupScope(string name, out string? groupName) =>
        groupScopes.TryGetValue(name, out groupName);
}
