using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace cms.Common.Entities;

/// <summary>
/// Entity representing supported languages for localization
/// Mirrors the TypeScript LanguageEntity from the API
/// </summary>
[Table("Languages")]
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(Name))]
public class Language : BaseEntity
{
    /// <summary>
    /// Language code (e.g., 'en-US', 'pt-BR', 'es-ES')
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Code
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Display name of the language
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Whether this language is currently active/supported
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;

    /// <summary>
    /// Collection of resource localizations in this language
    /// </summary>
    public virtual ICollection<ResourceLocalization> ResourceLocalizations
    {
        get;
        set;
    } = new List<ResourceLocalization>();
}
