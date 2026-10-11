using System.Net;
using System.Reflection;
using FluentAssertions;
using GameGuild.API.Core.Filters;
using GameGuild.API.Database;
using GameGuild.Configuration.ApplicationLayer;
using GameGuild.Identity.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GameGuild.API.UnitTests.Core;

public sealed class AuthenticationLockoutActionFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_ShouldRejectPasswordSignInWhileConfiguredAccountLockoutIsActive()
    {
        var now = DateTime.UtcNow;
        await using var database = CreateDatabase();
        await SeedAttemptsAsync(database, CreateFailures("player@example.com", 5, now.AddMinutes(-5)));
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            AccountLockoutDurationMinutes = 30
        };
        var (filter, services) = CreateFilter(database, options);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "player@example.com");
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeFalse();
            context.Result.Should().BeOfType<UnauthorizedObjectResult>()
                .Which.Value.Should().BeOfType<ProblemDetails>()
                .Which.Detail.Should().Be("Invalid credentials. Please check your email and password.");
            context.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldAllowSignInAfterLockoutDurationExpires()
    {
        var now = DateTime.UtcNow;
        await using var database = CreateDatabase();
        await SeedAttemptsAsync(database, CreateFailures("player@example.com", 5, now.AddMinutes(-40)));
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            AccountLockoutDurationMinutes = 30
        };
        var (filter, services) = CreateFilter(database, options);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "player@example.com");
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeTrue();
            context.Result.Should().BeNull();
            context.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldCountOnlyFailuresForTheRequestedEmail()
    {
        var now = DateTime.UtcNow;
        await using var database = CreateDatabase();
        var attempts = CreateFailures("player@example.com", 4, now.AddMinutes(-4));
        attempts.AddRange(CreateFailures("another-player@example.com", 5, now.AddMinutes(-3)));
        attempts.Add(CreateAttempt("player@example.com", isSuccessful: true, now.AddMinutes(-2)));
        await SeedAttemptsAsync(database, attempts);
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            AccountLockoutDurationMinutes = 30
        };
        var (filter, services) = CreateFilter(database, options);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "PLAYER@example.com");
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldThrottleIpAcrossDifferentAccountsAtConfiguredThreshold()
    {
        var now = DateTime.UtcNow;
        const string sourceIp = "192.0.2.10";
        await using var database = CreateDatabase();
        var attempts = Enumerable.Range(0, 50)
            .Select(index => CreateAttempt($"account-{index}@example.com", isSuccessful: false, now.AddMinutes(-10 - index)))
            .ToList();
        await SeedAttemptsAsync(database, attempts);

        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            MaxAttemptsPerIpPerHour = 50,
            EnableIpThrottling = true
        };
        var (filter, services) = CreateFilter(database, options);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "target@example.com");
            context.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse(sourceIp);
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeFalse();
            context.Result.Should().BeOfType<UnauthorizedObjectResult>()
                .Which.Value.Should().BeOfType<ProblemDetails>()
                .Which.Detail.Should().Be("Invalid credentials. Please check your email and password.");
            context.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldAllowIpBelowThresholdAndWhenIpThrottlingIsDisabled()
    {
        var now = DateTime.UtcNow;
        await using var database = CreateDatabase();
        await SeedAttemptsAsync(database, CreateFailures("another-account@example.com", 49, now.AddMinutes(-10)));

        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            MaxAttemptsPerIpPerHour = 50,
            EnableIpThrottling = true
        };
        var (filter, services) = CreateFilter(database, options);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "target@example.com");
            context.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            var nextCalled = false;

            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeTrue();
        }

        var disabledOptions = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 5,
            MaxAttemptsPerIpPerHour = 50,
            EnableIpThrottling = false
        };
        var (disabledFilter, disabledServices) = CreateFilter(database, disabledOptions);
        using (disabledServices)
        {
            var (context, actionContext) = CreateContext(disabledServices, nameof(AuthController.LocalSignIn), "target@example.com");
            context.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
            var nextCalled = false;

            await disabledFilter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeTrue();
        }
    }

    [Fact]
    public async Task OnActionExecutionAsync_ShouldNotReadLockoutStateForOtherActions()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        using (services)
        {
            var (context, actionContext) = CreateContext(services, actionName: "LocalSignUp", email: "player@example.com");
            var nextCalled = false;

            await new AuthenticationLockoutActionFilter().OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });

            nextCalled.Should().BeTrue();
            context.Result.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedAdmissionCompletesRealCredentialWorkAndTheSameGenericFloor(bool deniedByIp)
    {
        await using var database = CreateDatabase();
        await SeedAttemptsAsync(database, CreateFailures("blocked@example.test", 1, DateTime.UtcNow.AddMinutes(-1)));
        var logger = new AdmissionWorkLogger();
        var options = new AuthenticationSecurityOptions
        {
            MaxFailedAttemptsPerHour = 1,
            MaxAttemptsPerIpPerHour = 1,
            EnableIpThrottling = deniedByIp,
            AccountLockoutDurationMinutes = 30
        };
        var (filter, services) = CreateFilter(database, options, logger);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn),
                deniedByIp ? "absent@example.test" : "blocked@example.test");
            if (deniedByIp) { context.HttpContext.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10"); }
            var nextCalled = false;
            await filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });
            nextCalled.Should().BeFalse();
            var result = context.Result.Should().BeOfType<UnauthorizedObjectResult>().Which;
            var problem = result.Value.Should().BeOfType<ProblemDetails>().Which;
            problem.Status.Should().Be(StatusCodes.Status401Unauthorized);
            problem.Title.Should().Be("Unauthorized");
            problem.Detail.Should().Be(services.GetRequiredService<IUserEnumerationProtectionService>().GetGenericErrorMessage("login"));
            logger.Costs.Should().Equal(10);
            AuthenticationTimingOrigin.GetOrStartForRequest(context.HttpContext).Elapsed.Should()
                .BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(400));
            context.HttpContext.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        }
    }

    [Fact]
    public async Task CancelledAdmissionDoesNotHashOrExecuteTheAction()
    {
        await using var database = CreateDatabase();
        var logger = new AdmissionWorkLogger();
        var (filter, services) = CreateFilter(database, new AuthenticationSecurityOptions(), logger);
        using (services)
        {
            var (context, actionContext) = CreateContext(services, nameof(AuthController.LocalSignIn), "synthetic@example.test");
            context.HttpContext.RequestAborted = new CancellationToken(true);
            var nextCalled = false;
            Func<Task> run = () => filter.OnActionExecutionAsync(context, () =>
            {
                nextCalled = true;
                return Task.FromResult(new ActionExecutedContext(actionContext, [], new object()));
            });
            await run.Should().ThrowAsync<OperationCanceledException>();
            nextCalled.Should().BeFalse();
            logger.Costs.Should().BeEmpty();
            context.Result.Should().BeNull();
        }
    }

    private sealed class AdmissionWorkLogger : ILogger<UserEnumerationProtectionService>
    {
        public List<int> Costs { get; } = [];
        IDisposable? ILogger.BeginScope<TState>(TState state)
        {
            _ = state;
            return null;
        }
        bool ILogger.IsEnabled(LogLevel logLevel)
        {
            _ = logLevel;
            return true;
        }
        void ILogger.Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            // The interface requires these arguments; this recorder observes only structured state.
            _ = logLevel;
            _ = eventId;
            _ = exception;
            _ = formatter;
            if (state is not IEnumerable<KeyValuePair<string, object?>> values) { return; }
            foreach (var value in values)
            {
                if (value.Key == "WorkFactor" && value.Value is int cost) { Costs.Add(cost); }
            }
        }
    }

    private static ApplicationDbContext CreateDatabase()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static async Task SeedAttemptsAsync(
        ApplicationDbContext database,
        IReadOnlyCollection<AuthenticationAttempt> attempts)
    {
        database.Set<AuthenticationAttempt>().AddRange(attempts);
        await database.SaveChangesAsync();
    }

    private static (AuthenticationLockoutActionFilter Filter, ServiceProvider Services) CreateFilter(
        ApplicationDbContext database,
        AuthenticationSecurityOptions options,
        ILogger<UserEnumerationProtectionService>? logger = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(database);
        services.AddSingleton<IApplicationDbContext>(database);
        services.AddSingleton(options);
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PasswordPolicy:BCryptWorkFactor"] = "10"
        }).Build());
        services.AddSingleton<IUserEnumerationProtectionService, UserEnumerationProtectionService>();
        services.AddSingleton<IPasswordSignInAdmissionService, PasswordSignInAdmissionService>();
        if (logger is not null) { services.AddSingleton(logger); }
        return (new AuthenticationLockoutActionFilter(), services.BuildServiceProvider());
    }

    private static (ActionExecutingContext Context, ActionContext ActionContext) CreateContext(
        IServiceProvider services,
        string actionName,
        string email)
    {
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var descriptor = new ControllerActionDescriptor
        {
            ActionName = actionName,
            ControllerName = nameof(AuthController),
            ControllerTypeInfo = typeof(AuthController).GetTypeInfo()
        };
        var actionContext = new ActionContext(httpContext, new RouteData(), descriptor, new ModelStateDictionary());
        var arguments = new Dictionary<string, object?>
        {
            ["body"] = new LocalSignInRequest { Email = email, Password = "unused" }
        };
        var executingContext = new ActionExecutingContext(actionContext, [], arguments, new object());

        return (executingContext, actionContext);
    }

    private static List<AuthenticationAttempt> CreateFailures(string email, int count, DateTime mostRecent)
    {
        return Enumerable.Range(0, count)
            .Select(index => CreateAttempt(email, isSuccessful: false, mostRecent.AddMinutes(-index)))
            .ToList();
    }

    private static AuthenticationAttempt CreateAttempt(string email, bool isSuccessful, DateTime attemptedAt)
    {
        return new AuthenticationAttempt
        {
            Id = Guid.NewGuid(),
            Email = email.ToLowerInvariant(),
            IpAddress = "192.0.2.10",
            IsSuccessful = isSuccessful,
            AttemptedAt = attemptedAt,
            ProcessingTime = TimeSpan.FromMilliseconds(100),
            CreatedAt = attemptedAt,
            UpdatedAt = attemptedAt
        };
    }
}
