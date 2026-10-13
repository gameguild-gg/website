using System.Reflection;
using GameGuild.Identity.Provisioning.Scim.Filtering;

namespace GameGuild.Identity.Provisioning.Scim;

/// <summary>
///     Filterable attribute maps for the SCIM projections. Keyed by lower-cased SCIM
///     attribute name; aliases (emails → primary e-mail, name → displayName) map to the
///     same underlying member.
/// </summary>
public static class ScimFilterableAttributes
{
    public static readonly IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> User =
        Build<ScimUserView>(new Dictionary<string, (string PropertyName, ScimFilterAttributeType Type, bool CaseSensitive)>
        {
            ["id"] = (nameof(ScimUserView.UserId), ScimFilterAttributeType.Identifier, CaseSensitive: true),
            ["externalid"] = (nameof(ScimUserView.ExternalId), ScimFilterAttributeType.String, CaseSensitive: false),
            ["username"] = (nameof(ScimUserView.UserName), ScimFilterAttributeType.String, CaseSensitive: false),
            ["displayname"] = (nameof(ScimUserView.DisplayName), ScimFilterAttributeType.String, CaseSensitive: false),
            ["name"] = (nameof(ScimUserView.DisplayName), ScimFilterAttributeType.String, CaseSensitive: false),
            ["emails"] = (nameof(ScimUserView.Email), ScimFilterAttributeType.String, CaseSensitive: false),
            ["emails.value"] = (nameof(ScimUserView.Email), ScimFilterAttributeType.String, CaseSensitive: false),
            ["phonenumbers"] = (nameof(ScimUserView.PhoneNumber), ScimFilterAttributeType.String, CaseSensitive: false),
            ["phonenumbers.value"] = (nameof(ScimUserView.PhoneNumber), ScimFilterAttributeType.String, CaseSensitive: false),
            ["active"] = (nameof(ScimUserView.Active), ScimFilterAttributeType.Boolean, CaseSensitive: false)
        });

    public static readonly IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> Group =
        Build<ScimGroupView>(new Dictionary<string, (string PropertyName, ScimFilterAttributeType Type, bool CaseSensitive)>
        {
            ["id"] = (nameof(ScimGroupView.RoleId), ScimFilterAttributeType.Identifier, CaseSensitive: true),
            ["externalid"] = (nameof(ScimGroupView.ExternalId), ScimFilterAttributeType.String, CaseSensitive: false),
            ["displayname"] = (nameof(ScimGroupView.DisplayName), ScimFilterAttributeType.String, CaseSensitive: false)
        });

    private static IReadOnlyDictionary<string, (PropertyInfo Property, ScimFilterAttribute Attribute)> Build<TView>(
        IReadOnlyDictionary<string, (string PropertyName, ScimFilterAttributeType Type, bool CaseSensitive)> definition)
    {
        var result = new Dictionary<string, (PropertyInfo, ScimFilterAttribute)>(StringComparer.Ordinal);
        foreach (var (attributeName, mapping) in definition)
        {
            var property = typeof(TView).GetProperty(mapping.PropertyName)
                           ?? throw new InvalidOperationException(
                               $"The SCIM projection {typeof(TView).Name} has no property '{mapping.PropertyName}'.");
            result[attributeName] = (property, new ScimFilterAttribute(attributeName, mapping.Type, mapping.CaseSensitive));
        }

        return result;
    }
}
