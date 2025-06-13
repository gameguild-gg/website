using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GameGuild.Modules.Auth.Filters;
using GameGuild.Modules.Auth.Middleware;
using GameGuild.Modules.Auth.Services;
using Microsoft.Extensions.Primitives;

namespace GameGuild.Modules.Auth.Configuration
{
    /// <summary>
    /// Extension methods for configuring authentication services
    /// </summary>
    public static class AuthConfiguration
    {
        /// <summary>
        /// Add authentication services to the service collection
        /// </summary>
        public static IServiceCollection AddAuthModule(this IServiceCollection services, IConfiguration configuration)
        {
            // Register auth services
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IJwtTokenService, JwtTokenService>();
            services.AddScoped<IOAuthService, OAuthService>();
            services.AddScoped<IWeb3Service, Web3Service>();
            services.AddScoped<IEmailVerificationService, EmailVerificationService>();

            // Register HTTP client for OAuth services
            services.AddHttpClient<OAuthService>();

            // Register authentication filters
            services.AddScoped<JwtAuthenticationFilter>();
            // RoleAuthorizationFilter removed - using new three-layer DAC system

            // Configure JWT authentication
            IConfigurationSection jwtSettings = configuration.GetSection("Jwt");
            string secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey is required");
            string issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer is required");
            string audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience is required");

            services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                    }
                )
                .AddJwtBearer(options =>
                    {
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuer = true,
                            ValidateAudience = true,
                            ValidateLifetime = true,
                            ValidateIssuerSigningKey = true,
                            ValidIssuer = issuer,
                            ValidAudience = audience,
                            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                            ClockSkew = TimeSpan.Zero // Remove default 5-minute clock skew
                        };

                        options.Events = new JwtBearerEvents
                        {
                            OnMessageReceived = context =>
                            {
                                // Allow token in query string for SignalR connections
                                StringValues accessToken = context.Request.Query["access_token"];
                                PathString path = context.HttpContext.Request.Path;

                                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                                {
                                    context.Token = accessToken;
                                }

                                return Task.CompletedTask;
                            },
                            OnAuthenticationFailed = context =>
                            {
                                // Log authentication failures
                                var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                                ILogger logger = loggerFactory.CreateLogger("AuthConfiguration");
                                logger.LogWarning("JWT authentication failed: {Exception}", context.Exception.Message);

                                return Task.CompletedTask;
                            }
                        };
                    }
                );

            // Add authorization
            services.AddAuthorization();

            return services;
        }

        /// <summary>
        /// Configure the authentication middleware pipeline
        /// </summary>
        public static IApplicationBuilder UseAuthModule(this IApplicationBuilder app)
        {
            // Add authentication middleware
            app.UseAuthentication();
            app.UseAuthorization();

            // Add custom JWT middleware
            app.UseMiddleware<JwtAuthenticationMiddleware>();

            return app;
        }

        /// <summary>
        /// Add authentication filters to MVC configuration
        /// </summary>
        public static IMvcBuilder AddAuthFilters(this IMvcBuilder builder)
        {
            builder.AddMvcOptions(options =>
                {
                    options.Filters.Add<JwtAuthenticationFilter>();
                    // RoleAuthorizationFilter removed - using new three-layer DAC system
                }
            );

            return builder;
        }
    }
}
