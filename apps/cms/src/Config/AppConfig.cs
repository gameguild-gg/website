namespace GameGuild.Config;

public class AppConfig
{
    public string Host
    {
        get;
        set;
    } = "localhost";

    public int Port
    {
        get;
        set;
    } = 5000;

    public bool IsDevelopmentEnvironment
    {
        get;
        set;
    }

    public bool IsProductionEnvironment
    {
        get;
        set;
    }

    public bool IsDocumentationEnabled
    {
        get;
        set;
    }

    public string Environment
    {
        get;
        set;
    } = "Development";
}
