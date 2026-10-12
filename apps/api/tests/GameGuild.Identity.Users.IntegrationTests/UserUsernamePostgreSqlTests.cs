using GameGuild.API.Database;
using GameGuild.API.Eventing;
using GameGuild.CQRS;
using GameGuild.Identity.Users;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Xunit;

namespace GameGuild.Tests.Users.Integration;

public sealed class UserUsernamePostgreSqlTests(UsernamePostgreSqlFixture fixture) : IClassFixture<UsernamePostgreSqlFixture>
{
    [Fact]
    public async Task GeneratedHandlesPersistForOAuthAdministrativeAndBulkCreation()
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var first = User.CreateOAuthUser($"oauth-{marker}@example.test", $"MátHeus Martíns {marker}", false);
        var second = User.Create($"admin-{marker}@example.test", $"MátHeus Martíns {marker}");
        var third = User.Create($"bulk-{marker}@example.test", $"MátHeus Martíns {marker}");
        await repository.AddRangeAsync([first, second, third]);
        await repository.SaveChangesAsync();

        await using var verification = fixture.CreateContext();
        var persisted = await verification.Set<User>().Where(user => new[] { first.Id, second.Id, third.Id }.Contains(user.Id)).ToListAsync();
        Assert.Equal(3, persisted.Count);
        Assert.Equal($"matheus-martins-{marker}", persisted.Single(user => user.Id == first.Id).Username);
        Assert.Equal($"matheus-martins-{marker}-{second.Id:N}", persisted.Single(user => user.Id == second.Id).Username);
        Assert.Equal($"matheus-martins-{marker}-{third.Id:N}", persisted.Single(user => user.Id == third.Id).Username);
        Assert.All(persisted, user => Assert.Equal($"MátHeus Martíns {marker}", user.Name));
        Assert.False(persisted.Single(user => user.Id == first.Id).IsEmailVerified);
        Assert.All(persisted, user => Assert.Equal(1, user.Version));
    }

    [Fact]
    public async Task ConcurrentGeneratedHandleRetryPreservesEntityVersionsAndExactlyOneOutboxEvent()
    {
        var marker = Guid.NewGuid().ToString("N");
        var related = User.Create($"related-{marker}@example.test", $"Related {marker}");
        await using (var setup = fixture.CreateContext())
        {
            setup.Set<User>().Add(related);
            await setup.SaveChangesAsync();
        }

        await using var loserContext = fixture.CreateContext();
        await using var winnerContext = fixture.CreateContext();
        var loserRepository = new UserRepository(loserContext);
        var winnerRepository = new UserRepository(winnerContext);
        var loser = User.CreateOAuthUser($"loser-{marker}@example.test", $"Same {marker}");
        var winner = User.CreateOAuthUser($"winner-{marker}@example.test", $"Same {marker}");
        var eventId = AttachCreatedEvent(loser);
        var trackedRelated = await loserContext.Set<User>().SingleAsync(user => user.Id == related.Id);
        trackedRelated.UpdateName($"Updated {marker}");

        await loserRepository.AddAsync(loser);
        await winnerRepository.AddAsync(winner);
        Assert.Equal(loser.Username, winner.Username); // Both preflight reads precede the winning INSERT.
        await winnerRepository.SaveChangesAsync();
        await loserRepository.SaveChangesAsync(); // Real 23505 on the original candidate, then one retry.

        await using var verification = fixture.CreateContext();
        var storedLoser = await verification.Set<User>().SingleAsync(user => user.Id == loser.Id);
        Assert.Equal($"same-{marker}-{loser.Id:N}", storedLoser.Username);
        Assert.Equal(1, storedLoser.Version);
        Assert.Equal(2, (await verification.Set<User>().SingleAsync(user => user.Id == related.Id)).Version);
        Assert.Equal(1, await verification.Set<OutboxMessage>().CountAsync(message => message.EventId == eventId));
        Assert.Empty(loser.IntegrationEvents);
        Assert.Equal(2, await verification.Set<User>().CountAsync(user => user.Id == loser.Id || user.Id == winner.Id));
    }

    [Fact]
    public async Task GeneratedHandleRetryWorksInsideAmbientTransactionAndCommitsOneEvent()
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var loserContext = fixture.CreateContext();
        await using var transaction = await loserContext.Database.BeginTransactionAsync();
        var loserRepository = new UserRepository(loserContext);
        var loser = User.Create($"transaction-loser-{marker}@example.test", $"Transaction {marker}");
        var eventId = AttachCreatedEvent(loser);
        await loserRepository.AddAsync(loser);
        await using (var winnerContext = fixture.CreateContext())
        {
            var winnerRepository = new UserRepository(winnerContext);
            await winnerRepository.AddAsync(User.Create($"transaction-winner-{marker}@example.test", $"Transaction {marker}"));
            await winnerRepository.SaveChangesAsync();
        }

        await loserRepository.SaveChangesAsync();
        var additional = User.Create($"additional-{marker}@example.test", $"Additional {marker}");
        await loserRepository.AddAsync(additional);
        await loserRepository.SaveChangesAsync();
        await transaction.CommitAsync();

        await using var verification = fixture.CreateContext();
        Assert.Equal($"transaction-{marker}-{loser.Id:N}", (await verification.Set<User>().SingleAsync(user => user.Id == loser.Id)).Username);
        Assert.Equal(1, await verification.Set<OutboxMessage>().CountAsync(message => message.EventId == eventId));
        Assert.True(await verification.Set<User>().AnyAsync(user => user.Id == additional.Id));
    }

    [Fact]
    public async Task ConcurrentExplicitUsernameIsRejectedWithoutRenamingOrPersistingTheLoser()
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var loserContext = fixture.CreateContext();
        await using var winnerContext = fixture.CreateContext();
        var loserRepository = new UserRepository(loserContext);
        var winnerRepository = new UserRepository(winnerContext);
        var loser = User.CreateWithPassword($"explicit-loser-{marker}@example.test", "Original Display", "hash", $"Chosen {marker}");
        var winner = User.CreateWithPassword($"explicit-winner-{marker}@example.test", "Winner", "hash", $"CHOSEN {marker}");
        var eventId = AttachCreatedEvent(loser);
        await loserRepository.AddAsync(loser);
        await winnerRepository.AddAsync(winner);
        await winnerRepository.SaveChangesAsync();

        var failure = await Assert.ThrowsAsync<RequestValidationException>(() => loserRepository.SaveChangesAsync());
        Assert.Equal(nameof(User.Username), Assert.Single(failure.Errors).PropertyName);
        Assert.Equal($"chosen-{marker}", loser.Username);
        Assert.Equal("Original Display", loser.Name);
        Assert.Equal(0, loser.Version);
        Assert.Single(loser.IntegrationEvents);
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.Set<User>().AnyAsync(user => user.Id == loser.Id));
        Assert.False(await verification.Set<OutboxMessage>().AnyAsync(message => message.EventId == eventId));
    }

    [Fact]
    public async Task DeletedLegacyCaseVariantStillReservesTheGeneratedHandle()
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var context = fixture.CreateContext();
        var legacy = new User { Email = $"legacy-{marker}@example.test", Name = "Legacy", Username = $"LEGACY-{marker}", DeletedAt = DateTime.UtcNow };
        context.Set<User>().Add(legacy);
        await context.SaveChangesAsync();
        var generated = User.Create($"generated-{marker}@example.test", $"Legacy {marker}");
        var repository = new UserRepository(context);
        await repository.AddAsync(generated);
        await repository.SaveChangesAsync();
        Assert.Equal($"legacy-{marker}-{generated.Id:N}", generated.Username);
        Assert.Equal($"LEGACY-{marker}", legacy.Username);
    }

    [Fact]
    public async Task EmailConstraintFailureIsPropagatedAndDoesNotRenameUser()
    {
        var marker = Guid.NewGuid().ToString("N");
        await using var context = fixture.CreateContext();
        var repository = new UserRepository(context);
        var first = User.Create($"same-email-{marker}@example.test", $"First {marker}");
        await repository.AddAsync(first);
        await repository.SaveChangesAsync();
        var second = User.Create(first.Email, $"Second {marker}");
        var originalUsername = second.Username;
        await repository.AddAsync(second);
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => repository.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal("IX_Users_Email", postgres.ConstraintName);
        Assert.Equal(originalUsername, second.Username);
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.Set<User>().AnyAsync(user => user.Id == second.Id));
    }

    [Fact]
    public async Task GeneratedRetrySkipsAnAlreadyReservedDisambiguator()
    {
        var marker = Guid.NewGuid().ToString("N");
        var loser = User.Create($"suffix-loser-{marker}@example.test", $"Suffix {marker}");
        await using var loserContext = fixture.CreateContext();
        var repository = new UserRepository(loserContext);
        await repository.AddAsync(loser);
        await using (var winnerContext = fixture.CreateContext())
        {
            winnerContext.Set<User>().AddRange(
                User.Create($"suffix-winner-{marker}@example.test", $"Suffix {marker}"),
                User.CreateWithPassword($"suffix-reserved-{marker}@example.test", "Reserved", "hash", $"suffix-{marker}-{loser.Id:N}"));
            await winnerContext.SaveChangesAsync();
        }

        await repository.SaveChangesAsync();
        Assert.Equal($"suffix-{marker}-{loser.Id:N}-2", loser.Username);
        Assert.Equal(1, loser.Version);
    }

    [Fact]
    public async Task SecondConcurrentCollisionStopsAfterOneRetryWithoutWritingOutbox()
    {
        var marker = Guid.NewGuid().ToString("N");
        var loser = User.Create($"bounded-loser-{marker}@example.test", $"Bounded {marker}");
        var interceptor = new CallbackSaveInterceptor(async attempt =>
        {
            if (attempt != 2)
            {
                return;
            }
            await using var contender = fixture.CreateContext();
            contender.Set<User>().Add(new User { Email = $"bounded-contender-{marker}@example.test", Name = "Contender", Username = loser.Username });
            await contender.SaveChangesAsync();
        });
        await using var context = fixture.CreateContext(interceptor);
        var repository = new UserRepository(context);
        var eventId = AttachCreatedEvent(loser);
        await repository.AddAsync(loser);
        await using (var winner = fixture.CreateContext())
        {
            winner.Set<User>().Add(User.Create($"bounded-winner-{marker}@example.test", $"Bounded {marker}"));
            await winner.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<RequestValidationException>(() => repository.SaveChangesAsync());
        Assert.Equal(2, interceptor.Attempts);
        Assert.Single(loser.IntegrationEvents);
        await using var verification = fixture.CreateContext();
        Assert.False(await verification.Set<User>().AnyAsync(user => user.Id == loser.Id));
        Assert.False(await verification.Set<OutboxMessage>().AnyAsync(message => message.EventId == eventId));
    }

    private static Guid AttachCreatedEvent(User user)
    {
        var created = new UserCreatedEvent(user.Id)
        {
            TenantId = DurableIntegrationEventTenants.Platform, ActorId = user.Id,
            AggregateType = nameof(User), AggregateId = user.Id.ToString(), CorrelationId = Guid.NewGuid(),
            OccurredAt = DateTime.UtcNow
        };
        user.AddIntegrationEvent(created);
        return created.EventId;
    }

    private sealed class CallbackSaveInterceptor(Func<int, Task> callback) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            _ = eventData;
            cancellationToken.ThrowIfCancellationRequested();
            await callback(++Attempts);
            return result;
        }
    }
}

public sealed class UsernamePostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("username_slugification");
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public ApplicationDbContext CreateContext() => CreateContext(null);

    public ApplicationDbContext CreateContext(SaveChangesInterceptor? interceptor)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(_database!.ConnectionString);
        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }
        return new ApplicationDbContext(options.Options);
    }

    public async Task DisposeAsync()
    {
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }
}
