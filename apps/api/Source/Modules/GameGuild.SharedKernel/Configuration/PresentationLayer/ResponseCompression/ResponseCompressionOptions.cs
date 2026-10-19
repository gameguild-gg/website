namespace GameGuild.Configuration.PresentationLayer.ResponseCompression;

public sealed class ResponseCompressionOptions : BaseOptions
{
    /// <summary>
    ///     The configuration section name for this options type.
    /// </summary>
    public const string SectionName = "ResponseCompression";

    public bool EnableCompression { get; set; } = false;

    public string[ ] MimeTypes { get; set; } = ["text/plain", "application/json", "application/xml"];

    public static ResponseCompressionOptions CreateDefault() { return new ResponseCompressionOptions(); }
}
