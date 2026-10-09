using System.Text.RegularExpressions;

namespace GameGuild.Learning.Courses;

/// <summary>
/// String extension methods for the Programs module
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Converts a string to a URL-friendly slug format
    /// </summary>
    /// <param name="value">The string to convert</param>
    /// <returns>A URL-friendly slug string</returns>
    public static string ToSlugCase(this string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        // Convert to lowercase
        value = value.ToLowerInvariant();

        // Replace spaces and underscores with hyphens
        value = value.Replace(" ", "-")
                    .Replace("_", "-")
                    .Replace(".", "-");

        // Remove invalid characters (keep only alphanumeric and hyphens)
        value = Regex.Replace(value, @"[^a-z0-9\-]", "");

        // Remove multiple consecutive hyphens
        value = Regex.Replace(value, @"-+", "-");

        // Remove leading and trailing hyphens
        value = value.Trim('-');

        return value;
    }
}
