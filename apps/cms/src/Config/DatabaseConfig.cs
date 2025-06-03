namespace cms.Config;

public class DatabaseConfig
{
    public string ConnectionString
    {
        get;
        set;
    } = string.Empty;

    public string Provider
    {
        get;
        set;
    } = "PostgreSQL";

    public bool EnableSensitiveDataLogging
    {
        get;
        set;
    } = false;

    public bool EnableDetailedErrors
    {
        get;
        set;
    } = false;
}
