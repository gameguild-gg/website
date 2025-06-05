using System;

namespace cms.Modules.Auth.Attributes
{
    /// <summary>
    /// Attribute to mark endpoints as public (bypasses authentication)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class PublicAttribute : Attribute
    {
        public bool IsPublic
        {
            get;
        }

        public PublicAttribute(bool isPublic = true)
        {
            IsPublic = isPublic;
        }
    }
}
