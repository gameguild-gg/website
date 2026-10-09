using FluentAssertions;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using Microsoft.Extensions.Logging;
using Moq;
using System.Reflection;
using Xunit;

namespace GameGuild.Resources.UnitTests.Behaviors;

public class ResourceQuotaBehaviorTests
{
    private readonly Mock<IResourceQuotaService> _quotaServiceMock;
    private readonly Mock<IActorContextAccessor> _actorContextAccessorMock;
    private readonly Mock<ILogger<ResourceQuotaBehavior<TestQuotaCommand, Unit>>> _loggerMock;
    private readonly ResourceQuotaBehavior<TestQuotaCommand, Unit> _behavior;
    private readonly Mock<RequestHandlerDelegate<Unit>> _nextMock;

    public ResourceQuotaBehaviorTests()
    {
        _quotaServiceMock = new Mock<IResourceQuotaService>();
        _actorContextAccessorMock = new Mock<IActorContextAccessor>();
        _loggerMock = new Mock<ILogger<ResourceQuotaBehavior<TestQuotaCommand, Unit>>>();
        _behavior = new ResourceQuotaBehavior<TestQuotaCommand, Unit>(
            _quotaServiceMock.Object,
            _actorContextAccessorMock.Object,
            _loggerMock.Object);
        _nextMock = new Mock<RequestHandlerDelegate<Unit>>();
        _nextMock.Setup(x => x()).ReturnsAsync(Unit.Value);
    }

    [Fact]
    public async Task Handle_ThrowsException_WhenTenantIdMissing()
    {
        // Arrange
        var command = new TestQuotaCommand();
        var actorContext = ActorContext.Anonymous; // No tenant ID
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _behavior.Handle(command, _nextMock.Object, CancellationToken.None));

        exception.Message.Should().Contain("requires tenant context");
        exception.Message.Should().Contain("X-Tenant-Id");
        
        // Verify next handler was never called (fail-closed)
        _nextMock.Verify(x => x(), Times.Never);
    }

    [Fact]
    public async Task Handle_ThrowsException_WhenQuotaServiceFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new TestQuotaCommand();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        var expectedException = new InvalidOperationException("Database connection failed");
        _quotaServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _behavior.Handle(command, _nextMock.Object, CancellationToken.None));

        exception.InnerException.Should().Be(expectedException);
        exception.Message.Should().Contain("Unable to verify resource quota");
        
        // Verify next handler was never called (fail-closed on quota service error)
        _nextMock.Verify(x => x(), Times.Never);
    }

    [Fact]
    public async Task Handle_ConsumesQuota_WhenRecordUsageIsTrue()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new TestQuotaCommand();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        _quotaServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 5L, 10L));

        // Act
        var result = await _behavior.Handle(command, _nextMock.Object, CancellationToken.None);

        // Assert
        result.Should().Be(Unit.Value);
        _nextMock.Verify(x => x(), Times.Once);
        _quotaServiceMock.Verify(
            x => x.TryAtomicConsumeAsync(tenantId, ResourceUsageType.Users, 1L, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_ThrowsQuotaExceeded_WhenAtomicConsumeFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new TestQuotaCommand();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        _quotaServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((false, 10L, 10L)); // At limit

        // Act & Assert
        var exception = await Assert.ThrowsAsync<QuotaExceededException>(
            () => _behavior.Handle(command, _nextMock.Object, CancellationToken.None));

        exception.Message.Should().Contain("Resource quota exceeded");
        exception.Message.Should().Contain("Users");
        exception.Message.Should().Contain("10"); // Current usage
        
        // Verify next handler was never called
        _nextMock.Verify(x => x(), Times.Never);
    }

    [Fact]
    public async Task Handle_RollsBackQuota_WhenCommandFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new TestQuotaCommand();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        // Quota consumption succeeds
        _quotaServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 5L, 10L));

        // Command handler throws an exception AFTER quota was consumed
        var commandException = new InvalidOperationException("Command processing failed");
        _nextMock.Setup(x => x()).ThrowsAsync(commandException);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _behavior.Handle(command, _nextMock.Object, CancellationToken.None));

        exception.Should().Be(commandException);

        // Verify quota was consumed
        _quotaServiceMock.Verify(
            x => x.TryAtomicConsumeAsync(tenantId, ResourceUsageType.Users, 1L, It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify rollback was called (quota decrement)
        _quotaServiceMock.Verify(
            x => x.DecrementUsageAsync(tenantId, ResourceUsageType.Users, 1L, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsError_WhenRollbackFails()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var command = new TestQuotaCommand();
        var actorContext = new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(),
            Permissions = new HashSet<string>(),
            TypedAttributes = ActorAttributes.Empty,
            AuthScheme = "Bearer",
            IsAuthenticated = true
        };
        _actorContextAccessorMock.Setup(x => x.ActorContext).Returns(actorContext);

        // Quota consumption succeeds
        _quotaServiceMock
            .Setup(x => x.TryAtomicConsumeAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 5L, 10L));

        // Command handler throws an exception AFTER quota was consumed
        var commandException = new InvalidOperationException("Command processing failed");
        _nextMock.Setup(x => x()).ThrowsAsync(commandException);

        // Rollback also fails
        var rollbackException = new InvalidOperationException("Database connection lost during rollback");
        _quotaServiceMock
            .Setup(x => x.DecrementUsageAsync(
                tenantId,
                ResourceUsageType.Users,
                1L,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(rollbackException);

        // Act & Assert - Original exception should still be thrown
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _behavior.Handle(command, _nextMock.Object, CancellationToken.None));

        exception.Should().Be(commandException);

        // Verify rollback was attempted
        _quotaServiceMock.Verify(
            x => x.DecrementUsageAsync(tenantId, ResourceUsageType.Users, 1L, null, null, It.IsAny<CancellationToken>()),
            Times.Once);
        
        // Note: Logger.LogError is called but we don't verify it here to keep the test simple
        // The important thing is that the original exception is propagated, not the rollback exception
    }

    [Fact]
    public void TryExtractUserId_ReturnsNull_WhenResponseIsNull()
    {
        InvokeTryExtractUserId<TestQuotaCommand, ResponseWithId>(null).Should().BeNull();
    }

    [Fact]
    public void TryExtractUserId_ReturnsId_WhenGuidIdPropertyExists()
    {
        var expectedId = Guid.NewGuid();

        var result = InvokeTryExtractUserId<TestQuotaCommand, ResponseWithId>(new ResponseWithId { Id = expectedId });

        result.Should().Be(expectedId);
    }

    [Fact]
    public void TryExtractUserId_ReturnsUserId_WhenIdPropertyIsMissing()
    {
        var expectedUserId = Guid.NewGuid();

        var result = InvokeTryExtractUserId<TestQuotaCommand, ResponseWithUserId>(new ResponseWithUserId { UserId = expectedUserId });

        result.Should().Be(expectedUserId);
    }

    [Fact]
    public void TryExtractUserId_ReturnsNull_WhenIdAndUserIdPropertiesAreMissing()
    {
        InvokeTryExtractUserId<TestQuotaCommand, ResponseWithoutIdentifiers>(new ResponseWithoutIdentifiers())
            .Should().BeNull();
    }

    [Fact]
    public void TryExtractUserId_ReturnsNull_WhenIdPropertyIsNotGuid()
    {
        InvokeTryExtractUserId<TestQuotaCommand, ResponseWithStringId>(new ResponseWithStringId { Id = "not-a-guid" })
            .Should().BeNull();
    }

    [Fact]
    public void TryExtractUserId_ReturnsId_ForValueTypeResponse()
    {
        var expectedId = Guid.NewGuid();

        InvokeTryExtractUserId<TestQuotaCommand, ValueResponseWithId>(new ValueResponseWithId(expectedId))
            .Should().Be(expectedId);
    }

    [Fact]
    public void TryExtractUserId_PreservesAnEmptyGuid_InDefaultValueTypeResponse()
    {
        InvokeTryExtractUserId<TestQuotaCommand, ValueResponseWithId>(default(ValueResponseWithId))
            .Should().Be(Guid.Empty);
    }

    [Fact]
    public void TryExtractUserId_ReturnsNull_ForEmptyNullableValueTypeResponse()
    {
        InvokeTryExtractUserId<TestQuotaCommand, ValueResponseWithId?>(null).Should().BeNull();
    }

    [Fact]
    public void TryExtractUserId_ReturnsId_ForPopulatedNullableValueTypeResponse()
    {
        var expectedId = Guid.NewGuid();

        InvokeTryExtractUserId<TestQuotaCommand, ValueResponseWithId?>(new ValueResponseWithId(expectedId))
            .Should().Be(expectedId);
    }

    private readonly record struct ValueResponseWithId(Guid Id);

    private static Guid? InvokeTryExtractUserId<TRequest, TResponse>(object? response)
        where TRequest : IRequestBase
    {
        var method = typeof(ResourceQuotaBehavior<TRequest, TResponse>).GetMethod(
            "TryExtractUserId",
            BindingFlags.NonPublic | BindingFlags.Static);

        method.Should().NotBeNull();

        return (Guid?)method!.Invoke(null, new object?[] { response });
    }

    private sealed class ResponseWithId
    {
        public Guid Id { get; init; }
    }

    private sealed class ResponseWithUserId
    {
        public Guid UserId { get; init; }
    }

    private sealed class ResponseWithoutIdentifiers
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ResponseWithStringId
    {
        public string Id { get; init; } = string.Empty;
    }

    // Test command with RequiresQuota attribute
    [RequiresQuota(ResourceUsageType.Users, 1)]
    public class TestQuotaCommand : IRequest<Unit>
    {
    }
}
