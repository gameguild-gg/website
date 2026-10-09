using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameGuild.API.Database;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GameGuild.API.Endpoints;

/// <summary>
///     Authentication endpoints for sign-up, sign-in, and token refresh
/// </summary>
public static class AuthenticationEndpoint
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var authGroup = app.MapGroup("/auth").WithTags("Authentication").WithOpenApi();

        authGroup.MapPost("/sign-up", SignUp)
            .WithName("SignUp")
            .Produces<SignInResponseDto>(StatusCodes.Status201Created)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status409Conflict);

        authGroup.MapPost("/sign-in", SignIn).WithName("SignIn").Produces<SignInResponseDto>().Produces<ProblemDetails>(StatusCodes.Status400BadRequest).Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/refresh", RefreshToken).WithName("RefreshToken").Produces<RefreshTokenResponseDto>().Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        authGroup.MapPost("/google", GoogleSignIn).WithName("GoogleSignIn").Produces<SignInResponseDto>().Produces<ProblemDetails>(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> SignUp(SignUpRequest request, IAuthService authService, IConfiguration configuration, ILogger<Program> logger, CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate request
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            {
                return Results.BadRequest(new ProblemDetails { Title = "Validation Error", Detail = "Invalid email address", Status = StatusCodes.Status400BadRequest });
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return Results.BadRequest(new ProblemDetails { Title = "Validation Error", Detail = "Password must be at least 6 characters long", Status = StatusCodes.Status400BadRequest });
            }

            // Create LocalSignUpRequest for the Authentication module
            var signUpRequest = new LocalSignUpRequest
            {
                Email = request.Email,
                Password = request.Password,
                Username = request.Username ?? request.Email.Split('@')[0]
            };

            // Call the Authentication module's service
            var response = await authService.LocalSignUpAsync(signUpRequest, cancellationToken).ConfigureAwait(false);

            logger.LogInformation("User signed up successfully: {UserId}", LogRedaction.RedactId(response.UserId, "uid"));

            // Map to the API's response DTO
            return Results.Created(
                $"/users/{response.UserId}",
                new SignInResponseDto
                {
                    AccessToken = response.AccessToken,
                    RefreshToken = response.RefreshToken,
                    AccessTokenExpiresAt = response.ExpiresAt,
                    RefreshTokenExpiresAt = response.ExpiresAt.AddDays(RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent: false)),
                    ExpiresAt = response.ExpiresAt,
                    User = new AuthUserDto
                    {
                        Id = response.UserId,
                        Email = response.Email,
                        Username = request.Username ?? response.Email.Split('@')[0]
                    }
                }
            );
        }
        catch (RequestValidationException ex)
        {
            var errors = ex.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());

            return Results.ValidationProblem(errors);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during sign-up");

            return Results.Problem("An error occurred during sign-up");
        }
    }

    private static async Task<IResult> SignIn(SignInRequest request, ApplicationDbContext dbContext, IConfiguration configuration, ILogger<Program> logger)
    {
        try
        {
            // Find user by email
            var user = await dbContext.Set<User>().FirstOrDefaultAsync(u => u.Email == request.Email).ConfigureAwait(false);

            if (user == null || !user.HasPassword) { return Results.Unauthorized(); }

            // Verify password using BCrypt directly (User entity uses BCrypt)
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash)) { return Results.Unauthorized(); }

            // Record login
            user.RecordLogin();
            await dbContext.SaveChangesAsync().ConfigureAwait(false);

            // Generate tokens
            var tokens = GenerateTokens(user, configuration, request.RememberMe == true);

            logger.LogInformation("User signed in successfully: {UserId}", LogRedaction.RedactId(user.Id, "uid"));

            return Results.Ok(
                new SignInResponseDto
                {
                    AccessToken = tokens.AccessToken,
                    RefreshToken = tokens.RefreshToken,
                    AccessTokenExpiresAt = tokens.AccessTokenExpiresAt,
                    RefreshTokenExpiresAt = tokens.RefreshTokenExpiresAt,
                    ExpiresAt = tokens.AccessTokenExpiresAt,
                    User = new AuthUserDto { Id = user.Id, Email = user.Email, Username = user.Username }
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during sign-in");

            return Results.Problem("An error occurred during sign-in");
        }
    }

    private static Task<IResult> RefreshToken(RefreshTokenRequest request, IConfiguration configuration, ILogger<Program> logger)
    {
        logger.LogWarning("Deprecated /auth/refresh endpoint called — use POST /v1/auth/tokens:refresh instead");

        return Task.FromResult(
            Results.Problem(
                detail: "Use POST /v1/auth/tokens:refresh instead",
                title: "Deprecated Endpoint",
                statusCode: StatusCodes.Status410Gone
            )
        );
    }

    private static Task<IResult> GoogleSignIn(GoogleSignInRequest request, ILogger<Program> logger)
    {
        logger.LogWarning("Deprecated /auth/google endpoint called without OAuth provider wiring");

        return Task.FromResult(
            Results.Problem(
                detail: "Use POST /v1/auth/google instead.",
                title: "Deprecated Endpoint",
                statusCode: StatusCodes.Status410Gone
            )
        );
    }

    private static TokenResponse GenerateTokens(User user, IConfiguration configuration, bool persistent)
    {
        var jwtSecret = configuration["Jwt:Secret"]
            ?? configuration["Jwt:SecretKey"]
            ?? configuration["JwtSettings:SecretKey"]
            ?? configuration["Authentication:JwtSecretKey"]
            ?? throw new InvalidOperationException("JWT secret is not configured. Set 'Jwt:Secret' in configuration.");
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "GameGuild";
        var jwtAudience = configuration["Jwt:Audience"] ?? "GameGuild";
        var expirationMinutes = int.Parse(configuration["Jwt:ExpirationMinutes"] ?? "60");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)) { KeyId = "GameGuild-jwt-key" };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var accessTokenExpiry = SystemClock.UtcNow.AddMinutes(expirationMinutes);
        var refreshTokenExpiry = SystemClock.UtcNow.AddDays(RefreshTokenLifetimeResolver.ResolveExpirationDays(null, configuration, persistent));

        var claims = new[ ]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("username", user.Username ?? user.Email)
        };

        var token = new JwtSecurityToken(jwtIssuer, jwtAudience, claims, expires : accessTokenExpiry, signingCredentials : credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        return new TokenResponse { AccessToken = accessToken, RefreshToken = refreshToken, AccessTokenExpiresAt = accessTokenExpiry, RefreshTokenExpiresAt = refreshTokenExpiry };
    }
}

// Request/Response DTOs
public sealed record SignUpRequest(string Email, string Password, string? Username);

public sealed record SignInRequest(string Email, string Password, bool? RememberMe = null);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record GoogleSignInRequest(string IdToken);

public sealed record SignInResponseDto
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTime ExpiresAt { get; init; }

    public required DateTime AccessTokenExpiresAt { get; init; }

    public required DateTime RefreshTokenExpiresAt { get; init; }

    public required AuthUserDto User { get; init; }

    public Guid? TenantId { get; init; }
}

public sealed record RefreshTokenResponseDto
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTime AccessTokenExpiresAt { get; init; }

    public required DateTime RefreshTokenExpiresAt { get; init; }
}

public sealed record AuthUserDto
{
    public required Guid Id { get; init; }

    public required string Email { get; init; }

    public string? Username { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }
}

public sealed record TokenResponse
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    public required DateTime AccessTokenExpiresAt { get; init; }

    public required DateTime RefreshTokenExpiresAt { get; init; }
}
