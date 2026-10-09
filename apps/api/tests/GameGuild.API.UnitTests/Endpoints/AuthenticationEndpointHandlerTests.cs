using System.Reflection;
using FluentAssertions;
using GameGuild.API.Endpoints;
using GameGuild.CQRS;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using IAuthService = GameGuild.Identity.Authentication.IAuthService;
using LocalSignUpRequest = GameGuild.Identity.Authentication.LocalSignUpRequest;

namespace GameGuild.API.UnitTests.Endpoints;

public sealed class AuthenticationEndpointHandlerTests
{
    [Fact]
    public async Task GoogleSignIn_LegacyShell_ReturnsGoneInsteadOfMockTokens()
    {
        var method = typeof(AuthenticationEndpoint).GetMethod("GoogleSignIn", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var task = (Task<IResult>)method!.Invoke(
            null,
            [new GoogleSignInRequest("id-token"), Mock.Of<ILogger<Program>>()])!;

        var result = await task;

        result.Should().BeAssignableTo<IStatusCodeHttpResult>();
        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status410Gone);
        result.Should().NotBeOfType<SignInResponseDto>();
    }

    [Fact]
    public async Task SignUp_PasswordPolicyFailure_ReturnsValidationProblemWithoutPassword()
    {
        var method = typeof(AuthenticationEndpoint).GetMethod("SignUp", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        var password = new string('x', 16);
        var authService = new Mock<IAuthService>();
        authService
            .Setup(service => service.LocalSignUpAsync(It.IsAny<LocalSignUpRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestValidationException([new ValidationError("Password", "Password requirements were not met.")]));

        var task = (Task<IResult>)method!.Invoke(
            null,
            [new SignUpRequest("user@example.com", password, "user"), authService.Object, Mock.Of<ILogger<Program>>(), CancellationToken.None])!;

        var result = await task;

        result.Should().BeAssignableTo<IStatusCodeHttpResult>();
        ((IStatusCodeHttpResult)result).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        var problemResult = result.Should().BeOfType<ProblemHttpResult>().Subject;
        var problem = problemResult.ProblemDetails.Should().BeOfType<HttpValidationProblemDetails>().Subject;
        problem!.Errors["Password"].Should().Contain("Password requirements were not met.");
        problem.Errors.Values.SelectMany(messages => messages).Should().NotContain(password);
    }
}
