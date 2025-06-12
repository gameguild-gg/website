using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;
using System.Text.Json;

namespace cms.Modules.Program.Models;

[Table("programs")]
[Index(nameof(Visibility))]
[Index(nameof(Status))]
[Index(nameof(Slug))]
public class Program : Content, ITenantable
{
    [MaxLength(500)]
    public string? Thumbnail
    {
        get;
        set;
    }

    // Navigation properties
    public virtual ICollection<ProgramContent> ProgramContents
    {
        get;
        set;
    } = new List<ProgramContent>();

    public virtual ICollection<ProgramUser> ProgramUsers
    {
        get;
        set;
    } = new List<ProgramUser>();

    public virtual ICollection<Product.Models.ProductProgram> ProductPrograms
    {
        get;
        set;
    } = new List<Product.Models.ProductProgram>();

    public virtual ICollection<Certificate.Models.Certificate> Certificates
    {
        get;
        set;
    } = new List<Certificate.Models.Certificate>();

    public virtual ICollection<Feedback.Models.ProgramFeedbackSubmission> FeedbackSubmissions
    {
        get;
        set;
    } = new List<Feedback.Models.ProgramFeedbackSubmission>();

    public virtual ICollection<Feedback.Models.ProgramRating> ProgramRatings
    {
        get;
        set;
    } = new List<Feedback.Models.ProgramRating>();

    // Helper methods for JSON metadata
    public T? GetMetadata<T>(string key) where T : class
    {
        if (Metadata?.AdditionalData == null) return null;

        try
        {
            var metadataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata.AdditionalData);
            if (metadataDict != null && metadataDict.TryGetValue(key, out var value))
            {
                return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
            }
        }
        catch
        {
            // Handle JSON parsing errors gracefully
        }

        return null;
    }

    public void SetMetadata<T>(string key, T value)
    {
        if (Metadata == null)
        {
            Metadata = new ResourceMetadata
            {
                ResourceType = nameof(Program), AdditionalData = "{}"
            };
        }

        var metadataDict = string.IsNullOrEmpty(Metadata.AdditionalData)
            ? new Dictionary<string, object>()
            : JsonSerializer.Deserialize<Dictionary<string, object>>(Metadata.AdditionalData) ?? new Dictionary<string, object>();

        metadataDict[key] = value!;
        Metadata.AdditionalData = JsonSerializer.Serialize(metadataDict);
    }
}

/// <summary>
/// Entity Framework configuration for Program entity
/// </summary>
public class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        // Additional configuration can be added here if needed
    }
}
