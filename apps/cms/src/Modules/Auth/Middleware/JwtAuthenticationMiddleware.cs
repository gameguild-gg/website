using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace cms.Modules.Auth.Middleware
{
    /// <summary>
    /// JWT Authentication middleware
    /// </summary>
    public class JwtAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly IConfiguration _configuration;

        public JwtAuthenticationMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string? token = ExtractTokenFromHeader(context.Request);

            if (!string.IsNullOrEmpty(token))
            {
                try
                {
                    ClaimsPrincipal principal = ValidateToken(token);
                    context.User = principal;
                }
                catch (Exception)
                {
                    // Token validation failed, continue without setting user
                }
            }

            await _next(context);
        }

        private string? ExtractTokenFromHeader(HttpRequest request)
        {
            string? authHeader = request.Headers["Authorization"].FirstOrDefault();
            if (authHeader != null && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            return null;
        }

        private ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            byte[] key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"] ?? "dev-key");

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            ClaimsPrincipal? principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            return principal;
        }
    }

    /// <summary>
    /// Extension method to register JWT middleware
    /// </summary>
    public static class JwtAuthenticationMiddlewareExtensions
    {
        public static IApplicationBuilder UseJwtAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<JwtAuthenticationMiddleware>();
        }
    }
}
