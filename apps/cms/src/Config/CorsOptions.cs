using System.ComponentModel.DataAnnotations;

namespace GameGuild.Config;

public class CorsOptions
{
    public const string SectionName = "Cors";

    [Required]
    public string[] AllowedOrigins { get; set; } = Array.Empty<string>();

    public bool AllowCredentials { get; set; } = true;

    public string[] AllowedMethods { get; set; } = { "GET", "POST", "PUT", "DELETE", "OPTIONS" };

    public string[] AllowedHeaders { get; set; } = { "*" };
}
