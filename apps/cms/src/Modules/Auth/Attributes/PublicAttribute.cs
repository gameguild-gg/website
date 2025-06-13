namespace GameGuild.Modules.Auth.Attributes
{
    /// <summary>
    /// Attribute to mark endpoints as public (bypasses authentication)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PublicAttribute(bool isPublic = true) : Attribute
    {
        public bool IsPublic
        {
            get;
        } = isPublic;
    }
}
