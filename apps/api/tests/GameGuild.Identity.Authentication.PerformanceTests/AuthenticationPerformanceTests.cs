using System.Diagnostics;
using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using FluentValidation;

namespace GameGuild.Tests.Authentication.Performance;

/// <summary>
/// Performance tests for Authentication module
/// Tests authentication operations under load and measures performance
/// </summary>
public class AuthenticationPerformanceTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestApplicationDbContext _context;
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
    private readonly Mock<ILogger<LocalSignInHandler>> _mockLogger;
    private readonly Mock<IValidator<LocalSignInCommand>> _mockValidator;
    private readonly LocalSignInHandler _handler;

    public AuthenticationPerformanceTests(ITestOutputHelper output)
    {
        _output = output;

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        _context = new TestApplicationDbContext(options);
        _mockAuthService = new Mock<IAuthService>();
        Mock<IUserRepository> mockUserRepository = new Mock<IUserRepository>();
        _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();
        _mockLogger = new Mock<ILogger<LocalSignInHandler>>();
        _mockValidator = new Mock<IValidator<LocalSignInCommand>>();

        _handler = new LocalSignInHandler(
            _mockAuthService.Object,
            mockUserRepository.Object,
            _mockHttpContextAccessor.Object,
            _mockLogger.Object,
            _mockValidator.Object
        );

        SetupMocks();
    }

    private void SetupMocks()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = "Performance Test User Agent";
        _mockHttpContextAccessor.Setup(x => x.HttpContext).Returns(httpContext);

        _mockValidator.Setup(x => x.ValidateAsync(It.IsAny<LocalSignInCommand>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _mockAuthService.Setup(x => x.LocalSignInAsync(It.IsAny<LocalSignInRequest>(), It.IsAny<CancellationToken>()))
                       .ReturnsAsync(new SignInResponse
                       {
                           Success = true,
                           User = new GameGuild.Identity.Authentication.UserDto { Id = Guid.NewGuid(), Email = "test@example.com" },
                           AccessToken = "jwt-token",
                           RefreshToken = "refresh-token"
                       });
    }

    [Fact]
    public async Task Single_SignIn_Should_Execute_Within_Performance_Threshold()
    {
        // Arrange
        var command = new LocalSignInCommand
        {
            Email = "test@example.com",
            Password = "password123"
        };

        await _handler.Handle(command, CancellationToken.None);
        var stopwatch = Stopwatch.StartNew();

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Single SignIn execution time: {stopwatch.ElapsedMilliseconds}ms");

        result.Success.Should().BeTrue();
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100); // Should complete within 100ms
    }

    [Fact]
    public async Task Concurrent_SignIn_Requests_Should_Handle_Load()
    {
        // Arrange
        const int concurrentRequests = 50;
        var tasks = new List<Task<SignInResponse>>();
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < concurrentRequests; i++)
        {
            var command = new LocalSignInCommand
            {
                Email = $"test{i}@example.com",
                Password = "password123"
            };

            tasks.Add(_handler.Handle(command, CancellationToken.None));
        }

        SignInResponse[] results = await Task.WhenAll(tasks);

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Concurrent SignIn ({concurrentRequests} requests) execution time: {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per request: {stopwatch.ElapsedMilliseconds / (double)concurrentRequests:F2}ms");

        results.Should().AllSatisfy(result => result.Success.Should().BeTrue());
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000); // Should complete within 5 seconds
    }

    [Fact]
    public async Task Validator_Performance_Should_Be_Efficient()
    {
        // Arrange
        var validator = new LocalSignInCommandValidator();
        var command = new LocalSignInCommand
        {
            Email = "test@example.com",
            Password = "password123"
        };

        const int iterations = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var result = await validator.ValidateAsync(command);
        }

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"Validator performance ({iterations} validations): {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per validation: {stopwatch.ElapsedMilliseconds / (double)iterations:F3}ms");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000); // Should complete within 1 second
    }

    [Fact]
    public async Task SignUp_Validator_Performance_Should_Be_Efficient()
    {
        // Arrange
        var passwordHasher = new Mock<IPasswordHasher>();
        passwordHasher.Setup(hasher => hasher.ValidatePasswordStrength(It.IsAny<string>()))
            .Returns(new PasswordStrengthResult { IsValid = true });
        var validator = new LocalSignUpCommandValidator(passwordHasher.Object);
        var command = new LocalSignUpCommand
        {
            Email = "test@example.com",
            Password = "ValidPassword123!",
            Username = "testuser"
        };

        const int iterations = 1000;
        var stopwatch = Stopwatch.StartNew();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var result = await validator.ValidateAsync(command);
        }

        // Assert
        stopwatch.Stop();
        _output.WriteLine($"SignUp Validator performance ({iterations} validations): {stopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Average per validation: {stopwatch.ElapsedMilliseconds / (double)iterations:F3}ms");

        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000); // Should complete within 2 seconds (more complex validation)
    }

    [Fact]
    public async Task Bulk_SignIn_Validation_Should_Scale_Linearly()
    {
        // Arrange
        var validator = new LocalSignInCommandValidator();
        var validCommands = new List<LocalSignInCommand>();
        var invalidCommands = new List<LocalSignInCommand>();

        for (int i = 0; i < 100; i++)
        {
            validCommands.Add(new LocalSignInCommand
            {
                Email = $"valid{i}@example.com",
                Password = "ValidPassword123!"
            });

            invalidCommands.Add(new LocalSignInCommand
            {
                Email = "invalid-email", // Invalid format
                Password = "" // Empty password
            });
        }

        // Act & Assert - Valid commands
        var stopwatch = Stopwatch.StartNew();
        var validResults = await Task.WhenAll(validCommands.Select(cmd => validator.ValidateAsync(cmd)));
        stopwatch.Stop();

        _output.WriteLine($"Bulk validation - Valid commands (100): {stopwatch.ElapsedMilliseconds}ms");
        validResults.Should().AllSatisfy(result => result.IsValid.Should().BeTrue());

        // Act & Assert - Invalid commands
        stopwatch.Restart();
        var invalidResults = await Task.WhenAll(invalidCommands.Select(cmd => validator.ValidateAsync(cmd)));
        stopwatch.Stop();

        _output.WriteLine($"Bulk validation - Invalid commands (100): {stopwatch.ElapsedMilliseconds}ms");
        invalidResults.Should().AllSatisfy(result => result.IsValid.Should().BeFalse());
    }

    [Fact]
    public async Task Memory_Usage_Should_Be_Reasonable_Under_Load()
    {
        // Arrange
        const int iterations = 1000;
        var initialMemory = GC.GetTotalMemory(true);

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var command = new LocalSignInCommand
            {
                Email = $"memorytest{i}@example.com",
                Password = "password123"
            };

            var result = await _handler.Handle(command, CancellationToken.None);
            result.Success.Should().BeTrue();

            // Force garbage collection every 100 iterations
            if (i % 100 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        // Force final garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        _output.WriteLine($"Memory usage after {iterations} iterations:");
        _output.WriteLine($"Initial: {initialMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Final: {finalMemory / 1024 / 1024:F2} MB");
        _output.WriteLine($"Increase: {memoryIncrease / 1024 / 1024:F2} MB");

        // Memory increase should be reasonable (less than 10MB for 1000 operations)
        memoryIncrease.Should().BeLessThan(10 * 1024 * 1024);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
