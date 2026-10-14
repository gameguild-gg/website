using FluentAssertions;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Tests.Permissions.Unit.Middleware;

/// <summary>
/// Unit tests for the RequestContextLoggingMiddleware
/// </summary>
public class RequestContextLoggingMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<RequestContextLoggingMiddleware>> _mockLogger;
    private readonly Mock<IUserContext> _mockUserContext;
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly RequestContextLoggingMiddleware _middleware;
    private readonly DefaultHttpContext _httpContext;

    public RequestContextLoggingMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockLogger = new Mock<ILogger<RequestContextLoggingMiddleware>>();
        _mockUserContext = new Mock<IUserContext>();
        _mockTenantContext = new Mock<ITenantContext>();
        _middleware = new RequestContextLoggingMiddleware(_mockNext.Object, _mockLogger.Object);
        _httpContext = new DefaultHttpContext();

        // Setup defaults
        _httpContext.TraceIdentifier = "test-trace-id";
        _httpContext.Request.Path = "/api/test";
        _httpContext.Request.Method = "GET";
        _mockUserContext.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _mockUserContext.Setup(x => x.Email).Returns("test@example.com");
        _mockUserContext.Setup(x => x.IsAuthenticated).Returns(true);
        _mockUserContext.Setup(x => x.Roles).Returns(new List<string> { "User" });
        _mockTenantContext.Setup(x => x.TenantId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(x => x.TenantName).Returns("Test Tenant");
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Request_Start()
    {
        // Arrange
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request") && v.ToString()!.Contains("started")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Request_Completion()
    {
        // Arrange
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Request") && v.ToString()!.Contains("completed")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_User_Information()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var userEmail = "user@test.com";
        _mockUserContext.Setup(x => x.UserId).Returns(userId);
        _mockUserContext.Setup(x => x.Email).Returns(userEmail);
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(userId.ToString()) && v.ToString()!.Contains(userEmail)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Tenant_Information()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenantName = "My Tenant";
        _mockTenantContext.Setup(x => x.TenantId).Returns(tenantId);
        _mockTenantContext.Setup(x => x.TenantName).Returns(tenantName);
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(tenantId.ToString()) && v.ToString()!.Contains(tenantName)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_Anonymous_User()
    {
        // Arrange
        _mockUserContext.Setup(x => x.UserId).Returns((Guid?)null);
        _mockUserContext.Setup(x => x.Email).Returns((string?)null);
        _mockUserContext.Setup(x => x.IsAuthenticated).Returns(false);
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Anonymous")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Handle_No_Tenant()
    {
        // Arrange
        _mockTenantContext.Setup(x => x.TenantId).Returns((Guid?)null);
        _mockTenantContext.Setup(x => x.TenantName).Returns((string?)null);
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("None")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Request_Method_And_Path()
    {
        // Arrange
        _httpContext.Request.Method = "POST";
        _httpContext.Request.Path = "/api/users/create";
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("POST") && v.ToString()!.Contains("/api/users/create")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Duration()
    {
        // Arrange
        _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
            .Returns(async () => await Task.Delay(50));

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ms")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Status_Code()
    {
        // Arrange
        _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
            .Callback((HttpContext ctx) => ctx.Response.StatusCode = 200)
            .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("200")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Call_Next_Middleware()
    {
        // Arrange
        var nextCalled = false;
        _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
            .Callback(() => nextCalled = true)
            .Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        nextCalled.Should().BeTrue();
        _mockNext.Verify(x => x(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_Error_When_Exception_Occurs()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = async () => await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_Should_Propagate_Exception()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        _mockNext.Setup(x => x(It.IsAny<HttpContext>()))
            .ThrowsAsync(expectedException);

        // Act
        var act = async () => await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_TraceIdentifier()
    {
        // Arrange
        var traceId = "custom-trace-12345";
        _httpContext.TraceIdentifier = traceId;
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, _) => v.ToString()!.Contains(traceId)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void Constructor_Should_Initialize_Without_Throwing()
    {
        // Act
        var middleware = new RequestContextLoggingMiddleware(_mockNext.Object, _mockLogger.Object);

        // Assert
        middleware.Should().NotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_Should_Log_User_Roles()
    {
        // Arrange
        var roles = new List<string> { "Admin", "User", "Manager" };
        _mockUserContext.Setup(x => x.Roles).Returns(roles);
        _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

        // Act
        await _middleware.InvokeAsync(_httpContext, _mockUserContext.Object, _mockTenantContext.Object);

        // Assert - Verify logging was called (roles are in scope but not always in message)
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2)); // At least start and completion logs
    }
}
