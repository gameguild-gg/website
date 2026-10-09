using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;

namespace GameGuild;

/// <summary>
///     Transforms route parameters to kebab-case.
/// </summary>
public sealed partial class KebabCaseParameterTransformer : IOutboundParameterTransformer
{
    public string? TransformOutbound(object? value)
    {
        if (value == null)
        {
            return null;
        }

        var stringValue = value.ToString();

        return string.IsNullOrEmpty(stringValue) 
            ? stringValue 
            : KebabCaseRegex().Replace(stringValue, "$1-$2").ToLower();
    }

    [GeneratedRegex("([a-z0-9])([A-Z])")]
    private static partial Regex KebabCaseRegex();
}
