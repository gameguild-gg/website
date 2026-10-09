using FluentAssertions;

using GameGuild.CQRS;

using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace GameGuild.Commerce.Subscriptions.UnitTests.Commands;

public class ResumeSubscriptionHandlerTests
{
    private readonly Mock<DbSet<Subscription>> _mockDbSet;
    private readonly ResumeSubscriptionHandler _handler;

    public ResumeSubscriptionHandlerTests()
    {
        Mock<IApplicationDbContext> mockContext = new Mock<IApplicationDbContext>();
        _mockDbSet = new Mock<DbSet<Subscription>>();
        mockContext.Setup(c => c.Set<Subscription>()).Returns(_mockDbSet.Object);
        _handler = new ResumeSubscriptionHandler(mockContext.Object);
    }

    [Fact]
    public void Command_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var subscriptionId = Guid.NewGuid();
        var command = new ResumeSubscriptionCommand(subscriptionId);

        // Assert
        command.SubscriptionId.Should().Be(subscriptionId);
    }

    [Fact]
    public void Command_ShouldSupportRecordEquality()
    {
        // Arrange
        var subscriptionId = Guid.NewGuid();
        var command1 = new ResumeSubscriptionCommand(subscriptionId);
        var command2 = new ResumeSubscriptionCommand(subscriptionId);

        // Act & Assert
        command1.Should().Be(command2);
        command1.GetHashCode().Should().Be(command2.GetHashCode());
    }

    [Fact]
    public void Command_ShouldNotBeEqualWithDifferentIds()
    {
        // Arrange
        var command1 = new ResumeSubscriptionCommand(Guid.NewGuid());
        var command2 = new ResumeSubscriptionCommand(Guid.NewGuid());

        // Act & Assert
        command1.Should().NotBe(command2);
    }
}
