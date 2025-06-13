using Microsoft.AspNetCore.Mvc.Filters;
using GameGuild.Modules.Auth.Filters;

namespace GameGuild.Tests.Helpers
{
    /// <summary>
    /// A test implementation of JwtAuthenticationFilter that bypasses authentication for tests
    /// </summary>
    public class TestJwtAuthenticationFilter : JwtAuthenticationFilter
    {
        public TestJwtAuthenticationFilter(IConfiguration configuration) : base(configuration)
        {
        }

        public override void OnAuthorization(AuthorizationFilterContext context)
        {
            // Do nothing - bypass all authorization checks for tests
        }
    }
}
