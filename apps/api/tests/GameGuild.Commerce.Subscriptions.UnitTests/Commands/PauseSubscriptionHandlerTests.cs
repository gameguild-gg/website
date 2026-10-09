using FluentAssertions;

using GameGuild.CQRS;

using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

public class PauseSubscriptionHandlerTests
{
    private readonly Mock<IApplicationDbContext> _mockContext;
    private readonly PauseSubscriptionHandler _handler;

    public PauseSubscriptionHandlerTests()
    {
        _mockContext = new Mock<IApplicationDbContext>();
        Mock<DbSet<Subscription>> mockDbSet = new Mock<DbSet<Subscription>>();
        _mockContext.Setup(c => c.Set<Subscription>()).Returns(mockDbSet.Object);
        _handler = new PauseSubscriptionHandler(_mockContext.Object);
    }

    [Fact]
    public void Command_ShouldHaveCorrectProperties_WithAllParameters()
    {
        // Arrange & Act
        var subscriptionId = Guid.NewGuid();
        var pauseUntil = DateTime.UtcNow.AddDays(30);
        var command = new PauseSubscriptionCommand(subscriptionId, pauseUntil, "Going on vacation");

        // Assert
        command.SubscriptionId.Should().Be(subscriptionId);
        command.PauseUntil.Should().Be(pauseUntil);
        command.Reason.Should().Be("Going on vacation");
    }

    [Fact]
    public void Command_ShouldHaveDefaultNullValues()
    {
        // Arrange & Act
        var subscriptionId = Guid.NewGuid();
        var command = new PauseSubscriptionCommand(subscriptionId);

        // Assert
        command.SubscriptionId.Should().Be(subscriptionId);
        command.PauseUntil.Should().BeNull();
        command.Reason.Should().BeNull();
    }

    [Fact]
    public void Command_ShouldHaveCorrectProperties_WithOnlyPauseUntil()
    {
        // Arrange & Act
        var subscriptionId = Guid.NewGuid();
        var pauseUntil = DateTime.UtcNow.AddDays(14);
        var command = new PauseSubscriptionCommand(subscriptionId, pauseUntil);

        // Assert
        command.SubscriptionId.Should().Be(subscriptionId);
        command.PauseUntil.Should().Be(pauseUntil);
        command.Reason.Should().BeNull();
    }

    [Fact]
    public void Command_ShouldSupportRecordEquality()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var pauseUntil = DateTime.UtcNow.AddDays(30);
        var command1 = new PauseSubscriptionCommand(subscriptionId, pauseUntil, "Reason");
        var command2 = new PauseSubscriptionCommand(subscriptionId, pauseUntil, "Reason");

        // Act & Assert
        command1.Should().Be(command2);
        command1.GetHashCode().Should().Be(command2.GetHashCode());
    }
}
