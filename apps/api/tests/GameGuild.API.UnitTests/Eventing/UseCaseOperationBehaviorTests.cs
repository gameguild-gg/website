using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.CQRS;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;

namespace GameGuild.API.UnitTests.Eventing;

public sealed class UseCaseOperationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenCommandRuns_EstablishesAmbientOperationContext()
    {
        // Given
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out var tenantId, out var actorId);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);
        UseCaseOperationContext? observed = null;

        // When
        var response = await behavior.Handle(
            new TestMutationCommand(),
            async () =>
            {
                observed = operationAccessor.Current;
                context.Add(new Tenant
                {
                    Name = "Operation tenant",
                    Slug = $"operation-{Guid.NewGuid():N}",
                    AdminEmail = "admin@example.com"
                });
                await context.SaveChangesAsync();
                return true;
            },
            CancellationToken.None);

        // Then
        response.Should().BeTrue();
        observed.Should().NotBeNull();
        observed!.OperationCode.Should().Be("test-mutation");
        observed.CommandType.Should().Be(nameof(TestMutationCommand));
        observed.TenantId.Should().Be(tenantId);
        observed.ActorId.Should().Be(actorId);
        operationAccessor.Current.Should().BeNull();
        var operationEvent = await context.Set<OutboxMessage>().SingleAsync();
        operationEvent.EventName.Should().Be("platform.use-case-operation.occurred.v1");
        operationEvent.TenantId.Should().Be(tenantId);
        operationEvent.ActorId.Should().Be(actorId);
    }

    [Fact]
    public async Task Handle_WhenSuccessfulCommandDoesNotMutate_DoesNotInventAnOperationEvent()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var operationAccessor = new UseCaseOperationContextAccessor();
        var actorAccessor = NewActorAccessor(out _, out _);
        await using var context = new ApplicationDbContext(options, null, operationAccessor);
        var behavior = new UseCaseOperationBehavior<TestMutationCommand, bool>(
            context,
            actorAccessor,
            operationAccessor);

        var response = await behavior.Handle(
            new TestMutationCommand(),
            () => Task.FromResult(true),
            CancellationToken.None);

        response.Should().BeTrue();
        (await context.Set<OutboxMessage>().CountAsync()).Should().Be(0);
    }

    private static ActorContextAccessor NewActorAccessor(out Guid tenantId, out Guid actorId)
    {
        tenantId = Guid.NewGuid();
        actorId = Guid.NewGuid();
        var accessor = new ActorContextAccessor();
        accessor.SetActorContext(ActorContextBuilder.ForUser(actorId).WithTenantId(tenantId).Build());
        return accessor;
    }

    private sealed record TestMutationCommand : ICommand<bool>;
}
