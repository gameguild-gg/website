using FluentAssertions;
using GameGuild.API.Database;
using GameGuild.CQRS;
using GameGuild.Identity.Tenants;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace GameGuild.API.UnitTests.Database;

public sealed class ApplicationDbContextDomainEventTests
{
    [Fact]
    public async Task SaveChangesAsync_DoesNotPublishLegacyDomainEventsAfterPersistence()
    {
        // Given
        var publisher = new Mock<IPublisher>(MockBehavior.Strict);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var context = new ApplicationDbContext(options, publisher.Object);
        var tenant = new Tenant
        {
            Name = "Domain event tenant",
            Slug = "domain-event-tenant",
            AdminEmail = "admin@example.com"
        };
        context.Add(tenant);
        tenant.Deactivate();

        // When
        await context.SaveChangesAsync();

        // Then
        publisher.VerifyNoOtherCalls();
        tenant.DomainEvents.Should().BeEmpty();
    }
}
