using System;

namespace cms.Modules.Auth.Attributes
{
    /// <summary>
    /// Attribute to specify required roles for authorization
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class RequireRolesAttribute : Attribute
    {
        public string[] Roles
        {
            get;
        }

        public RequireRolesAttribute(params string[] roles)
        {
            Roles = roles ?? throw new ArgumentNullException(nameof(roles));
        }
    }
}
