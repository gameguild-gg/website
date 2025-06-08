using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Slugify;

/// <summary>
/// Provides utilities for converting strings to URL-friendly slug format.
/// Wrapper around Slugify.Core for consistent API.
/// </summary>
public static class SlugCase
{
    private static readonly SlugHelper _slugHelper;
    private static IMemoryCache _cache;
    
    static SlugCase()
    {
        var config = new SlugHelperConfiguration
        {
            ForceLowerCase = true,
            CollapseDashes = true,
            TrimWhitespace = true,
            // Add custom replacements if needed
            // Use Unidecode if we need a more comprehensive transliteration
            StringReplacements = new Dictionary<string, string>
            {
                {"&", "and"},
                {"+", "plus"}
            }
        };
        
        _slugHelper = new SlugHelper(config);
        
        // Initialize cache with the same configuration as SnakeCase
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 1000, // Maximum 1000 entries
            CompactionPercentage = 0.25 // Remove 25% of entries when limit is reached
        };
        _cache = new MemoryCache(cacheOptions);
    }

    /// <summary>
    /// Converts a string to a URL-friendly slug format using Slugify.Core.
    /// </summary>
    /// <param name="text">The text to convert to a slug.</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A URL-friendly slug string.</returns>
    /// <exception cref="ArgumentException">Thrown when text is null or empty.</exception>
    public static string Convert(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Text cannot be null or empty.", nameof(text));
        }

        if (maxLength <= 0)
        {
            throw new ArgumentException("Max length must be greater than zero.", nameof(maxLength));
        }

        var cacheKey = $"slugify:{text}:{maxLength}";
        return _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.Size = 1; // Each entry counts as 1 towards the size limit
            entry.SlidingExpiration = TimeSpan.FromMinutes(30); // Expire after 30 minutes of inactivity
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2); // Absolute expiration after 2 hours
            
            var slug = _slugHelper.GenerateSlug(text);
            
            // Truncate if necessary
            if (slug.Length > maxLength)
            {
                slug = slug.Substring(0, maxLength).TrimEnd('-');
            }
            
            return slug;
        }) ?? string.Empty;
    }

    /// <summary>
    /// Converts a string to a slug with a specific separator.
    /// </summary>
    /// <param name="text">The text to convert to a slug.</param>
    /// <param name="separator">The separator to use (default: "-").</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A URL-friendly slug string with custom separator.</returns>
    public static string Convert(string text, string separator, int maxLength = 100)
    {
        var slug = Convert(text, maxLength);
        
        if (!string.IsNullOrEmpty(separator) && separator != "-")
        {
            slug = slug.Replace("-", separator);
        }
        
        return slug;
    }

    /// <summary>
    /// Converts multiple strings to slugs.
    /// </summary>
    /// <param name="texts">The strings to convert.</param>
    /// <returns>An array of slug strings.</returns>
    public static string[] ConvertMany(params string[] texts)
    {
        if (texts == null || texts.Length == 0)
        {
            return Array.Empty<string>();
        }

        var result = new string[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            result[i] = Convert(texts[i]);
        }
        return result;
    }

    /// <summary>
    /// Generates a unique slug by appending a number if the base slug already exists.
    /// </summary>
    /// <param name="text">The text to convert to a slug.</param>
    /// <param name="existingSlugs">Collection of existing slugs to check against.</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A unique slug string.</returns>
    public static string GenerateUnique(string text, IEnumerable<string> existingSlugs, int maxLength = 100)
    {
        var baseSlug = Convert(text, maxLength);
        
        if (existingSlugs == null || !existingSlugs.Contains(baseSlug))
        {
            return baseSlug;
        }

        var existingSet = new HashSet<string>(existingSlugs, StringComparer.OrdinalIgnoreCase);
        var counter = 1;
        string uniqueSlug;
        
        do
        {
            var suffix = $"-{counter}";
            var availableLength = maxLength - suffix.Length;
            
            if (availableLength <= 0)
            {
                uniqueSlug = counter.ToString();
            }
            else
            {
                var truncatedBase = baseSlug.Length > availableLength 
                    ? baseSlug.Substring(0, availableLength).TrimEnd('-')
                    : baseSlug;
                uniqueSlug = truncatedBase + suffix;
            }
            
            counter++;
        } 
        while (existingSet.Contains(uniqueSlug));
        
        return uniqueSlug;
    }

    /// <summary>
    /// Validates if a string is already a valid slug.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>True if the text is a valid slug, false otherwise.</returns>
    public static bool IsValidSlug(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // Check if it matches slug pattern: lowercase letters, numbers, and dashes
        // No leading/trailing dashes, no consecutive dashes
        return Regex.IsMatch(text, @"^[a-z0-9]+(?:-[a-z0-9]+)*$");
    }

    /// <summary>
    /// Creates a slug from a type name.
    /// </summary>
    /// <typeparam name="T">The type whose name to convert.</typeparam>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A slug based on the type name.</returns>
    public static string FromType<T>(int maxLength = 100)
    {
        return Convert(typeof(T).Name, maxLength);
    }

    /// <summary>
    /// Creates a slug from a type name.
    /// </summary>
    /// <param name="type">The type whose name to convert.</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A slug based on the type name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when type is null.</exception>
    public static string FromType(Type type, int maxLength = 100)
    {
        if (type == null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        return Convert(type.Name, maxLength);
    }

    /// <summary>
    /// Converts a string to a slug without caching (useful for one-time conversions).
    /// </summary>
    /// <param name="text">The text to convert to a slug.</param>
    /// <param name="maxLength">Maximum length of the resulting slug (default: 100).</param>
    /// <returns>A URL-friendly slug string.</returns>
    public static string ConvertUncached(string text, int maxLength = 100)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (maxLength <= 0)
        {
            throw new ArgumentException("Max length must be greater than zero.", nameof(maxLength));
        }

        var slug = _slugHelper.GenerateSlug(text);
        
        // Truncate if necessary
        if (slug.Length > maxLength)
        {
            slug = slug.Substring(0, maxLength).TrimEnd('-');
        }
        
        return slug;
    }

    /// <summary>
    /// Clears the internal cache by disposing and recreating it. Useful for memory management in long-running applications.
    /// </summary>
    public static void ClearCache()
    {
        // IMemoryCache doesn't have a Clear method, so we need to dispose and recreate
        _cache.Dispose();
        var cacheOptions = new MemoryCacheOptions
        {
            SizeLimit = 1000, // Maximum 1000 entries
            CompactionPercentage = 0.25 // Remove 25% of entries when limit is reached
        };
        _cache = new MemoryCache(cacheOptions);
    }

    /// <summary>
    /// Disposes the memory cache. Call this method when the application is shutting down.
    /// </summary>
    public static void Dispose()
    {
        _cache?.Dispose();
    }
}
