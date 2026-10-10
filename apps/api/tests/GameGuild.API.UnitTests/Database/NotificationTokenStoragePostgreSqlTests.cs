using Microsoft.AspNetCore.DataProtection;
using GameGuild.API.Database.Migrations;
using GameGuild.API.Database;
using GameGuild.CQRS;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Users;
using GameGuild.Notifications;
using GameGuild.Notifications.Services;
using GameGuild.Notifications.Services.Email;
using GameGuild.Notifications.Services.Email.Renderers;
using Microsoft.Extensions.Options;
using GameGuild.TestSupport.Finance.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.API.UnitTests.Database;

public sealed class NotificationTokenStoragePostgreSqlTests(NotificationTokenStoragePostgreSqlFixture fixture)
    : IClassFixture<NotificationTokenStoragePostgreSqlFixture>
{
    private static readonly NotificationMetadataProtector MetadataProtector = new(new EphemeralDataProtectionProvider());
    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task QueuedIdentityEmail_RawDatabaseMetadataDoesNotContainBearerToken(string kind)
    {
        await using var context = fixture.CreateContext();
        var (notification, token) = await Queue(context, kind);
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT \"Metadata\" FROM \"Notifications\" WHERE \"Id\" = @notificationId";
        var id = command.CreateParameter();
        id.ParameterName = "notificationId";
        id.Value = notification.Id;
        command.Parameters.Add(id);
        var stored = Assert.IsType<string>(await command.ExecuteScalarAsync());

        Assert.DoesNotContain(token, stored, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("verification")]
    [InlineData("reset")]
    [InlineData("magic")]
    public async Task QueuedIdentityEmail_RenderedDeliveryStillContainsOriginalToken(string kind)
    {
        await using var context = fixture.CreateContext();
        var (notification, token) = await Queue(context, kind);
        context.ChangeTracker.Clear();
        var stored = await context.Set<Notification>().SingleAsync(value => value.Id == notification.Id);
        var footer = Mock.Of<IEmailFooterService>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["App:BaseUrl"] = "https://notification.example.test"
        }).Build();
        IEmailRenderer renderer = kind switch
        {
            "verification" => new EmailVerificationRenderer(footer, config, MetadataProtector),
            "reset" => new PasswordResetRenderer(footer, config, MetadataProtector),
            "magic" => new MagicLinkRenderer(footer, config, MetadataProtector),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var message = Assert.IsType<GameGuild.Email.EmailMessage>(await renderer.RenderAsync(stored));

        Assert.Contains("?token=" + token, message.PlainTextContent, StringComparison.Ordinal);
        Assert.Contains("?token=" + token, message.HtmlContent!, StringComparison.Ordinal);
        Assert.Equal(NotificationChannel.Email, stored.Channel);
        Assert.False(stored.IsSent);
        Assert.Equal(NotificationDeliveryStatus.Pending, stored.DeliveryStatus);
    }

    [Fact]
    public async Task OrdinaryNotification_PreservesMetadataAndRecipient()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context);
        const string metadata = "{\"context\":\"ordinary native fixture\"}";
        var result = await CreateDelivery(context).SendAsync(user.Id, NotificationType.System,
            "Native fixture", "Ordinary message", metadata: metadata);
        Assert.True(result.IsSuccess);
        context.ChangeTracker.Clear();
        var stored = await context.Set<Notification>().SingleAsync(value => value.Id == result.Value.Id);
        Assert.Equal(metadata, stored.Metadata);
        Assert.Equal(user.Id, stored.RecipientId);
        Assert.True(stored.IsSent);
    }

    [Fact]
    public async Task ProtectedMetadata_UsesPersistedDatabaseKeyringAcrossProviderRestarts()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context);
        const string metadata = "{\"token\":\"restart-native-token\",\"email\":\"member@example.test\"}";
        var notification = Notification.Create(user.Id, NotificationType.PasswordReset, NotificationChannel.Email,
            "Reset", "Requested operation", metadata: metadata);
        using (var first = fixture.CreateDataProtectionProvider())
        {
            new NotificationMetadataProtector(first.GetRequiredService<IDataProtectionProvider>()).ProtectForStorage(notification);
        }
        context.Add(notification);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var stored = await context.Set<Notification>().SingleAsync(value => value.Id == notification.Id);
        var envelope = stored.Metadata;
        using var restarted = fixture.CreateDataProtectionProvider();
        var protector = new NotificationMetadataProtector(restarted.GetRequiredService<IDataProtectionProvider>());
        Assert.Equal(metadata, protector.GetForRendering(stored, stored.Type));
        Assert.Equal(envelope, stored.Metadata);
        Assert.DoesNotContain("restart-native-token", stored.Metadata!, StringComparison.Ordinal);
        Assert.True(await context.DataProtectionKeys.AnyAsync());
    }

    [Fact]
    public async Task MaximumUnicodeInput_PersistsWithoutTruncatingTheProtectedEnvelope()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context);
        var metadata = string.Concat(Enumerable.Repeat("\U0001F600", 2000));
        var result = await CreateDelivery(context).SendAsync(user.Id, NotificationType.MagicLink,
            "Sign in", "Requested operation", NotificationChannel.Email, metadata: metadata);
        Assert.True(result.IsSuccess);
        context.ChangeTracker.Clear();
        var stored = await context.Set<Notification>().SingleAsync(value => value.Id == result.Value.Id);
        Assert.True(stored.Metadata!.Length > 4000);
        Assert.Equal(metadata, MetadataProtector.GetForRendering(stored, stored.Type));
        var property = context.Model.FindEntityType(typeof(Notification))!.FindProperty(nameof(Notification.Metadata))!;
        Assert.Equal("text", property.GetColumnType());
        Assert.Null(property.GetMaxLength());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourceMigration_ExpandsMetadata_AndDownRefusesDataLoss(bool wide)
    {
        await using var context = fixture.CreateContext();
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        // PostgreSQL's temporary schema shadows the real queue on this connection only.
        await context.Database.ExecuteSqlRawAsync("CREATE TEMP TABLE \"Notifications\" (\"Id\" uuid PRIMARY KEY, \"Metadata\" character varying(4000))");
        var migration = new ProtectNotificationCredentialMetadata();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        foreach (var sql in generator.Generate(migration.UpOperations))
            await context.Database.ExecuteSqlRawAsync(sql.CommandText);
        var id = Guid.NewGuid();
        var value = new string('x', wide ? 5000 : 4000);
        await context.Database.ExecuteSqlRawAsync("INSERT INTO \"Notifications\" (\"Id\",\"Metadata\") VALUES ({0},{1})", id, value);
        await transaction.CreateSavepointAsync("before_down");
        async Task Down()
        {
            foreach (var sql in generator.Generate(migration.DownOperations))
                await context.Database.ExecuteSqlRawAsync(sql.CommandText);
        }
        if (wide)
        {
            var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(Down);
            Assert.Contains("cannot be narrowed without data loss", exception.MessageText, StringComparison.Ordinal);
            await transaction.RollbackToSavepointAsync("before_down");
        }
        else
        {
            await Down();
        }
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT \"Metadata\" FROM \"Notifications\"";
        Assert.Equal(value, Assert.IsType<string>(await command.ExecuteScalarAsync()));
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task LegacyBackfill_ProtectsHistoricalAndDeletedRowsThroughProductionPostgreSqlQuery()
    {
        await using var context = fixture.CreateContext();
        var user = await CreateUser(context);
        const string metadata = "{\"token\":\"historical-native-token\",\"email\":\"member@example.test\"}";
        var rows = Enum.GetValues<NotificationDeliveryStatus>().Select(status =>
        {
            var row = Notification.Create(user.Id, NotificationType.PasswordReset, NotificationChannel.InApp,
                "Historical request", "Requested operation", metadata: metadata);
            context.Entry(row).Property(value => value.DeliveryStatus).CurrentValue = status;
            if (status == NotificationDeliveryStatus.Sent) row.MarkAsSent();
            row.DeletedAt = SystemClock.UtcNow;
            return row;
        }).ToList();
        context.AddRange(rows);
        await context.SaveChangesAsync();
        // Compare persisted states: PostgreSQL timestamps have microsecond precision.
        // Reload before the sweep instead of comparing the factory's finer in-memory timestamps.
        var ids = rows.Select(row => row.Id).ToArray();
        context.ChangeTracker.Clear();
        var persisted = await context.Set<Notification>().IgnoreQueryFilters()
            .Where(row => ids.Contains(row.Id)).ToListAsync();
        var states = persisted.ToDictionary(row => row.Id, row =>
            (row.DeliveryStatus, row.IsSent, row.SentAt, row.DeletedAt, row.AttemptCount, row.NextAttemptAt, row.LastError));
        var sender = new Mock<GameGuild.Email.IEmailSender>(MockBehavior.Strict);
        var preferences = new Mock<INotificationPreferenceService>();
#pragma warning disable CS0618 // Exercise the production dispatcher's existing preference gate.
        preferences.Setup(p => p.ShouldSendNotificationAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<NotificationChannel>(), It.IsAny<GameGuild.Notifications.NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
#pragma warning restore CS0618
        var dispatcher = new EmailDispatcherService(context, new EmailRendererRegistry([]), Mock.Of<IRecipientEmailResolver>(),
            preferences.Object, sender.Object, Options.Create(new EmailDispatcherOptions { SweepBatchSize = 2 }),
            NullLogger<EmailDispatcherService>.Instance, MetadataProtector);
        // Repeated bounded passes cover the six lifecycle states without scheduling a second worker.
        for (var index = 0; index < rows.Count; index++) await dispatcher.SweepOnceAsync();
        context.ChangeTracker.Clear();
        foreach (var id in states.Keys)
        {
            var row = await context.Set<Notification>().IgnoreQueryFilters().SingleAsync(value => value.Id == id);
            Assert.StartsWith(NotificationMetadataProtector.ProtectedPrefix, row.Metadata!, StringComparison.Ordinal);
            Assert.DoesNotContain("historical-native-token", row.Metadata!, StringComparison.Ordinal);
            Assert.Equal(metadata, MetadataProtector.GetForRendering(row, row.Type));
            Assert.Equal(states[id], (row.DeliveryStatus, row.IsSent, row.SentAt, row.DeletedAt,
                row.AttemptCount, row.NextAttemptAt, row.LastError));
        }
        sender.VerifyNoOtherCalls();
    }

    private static async Task<(Notification Notification, string Token)> Queue(ApplicationDbContext context, string kind)
    {
        var user = await CreateUser(context);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var generator = new EmailVerificationService(NullLogger<EmailVerificationService>.Instance, cache, Mock.Of<IPublisher>());
        var token = kind switch
        {
            "verification" => await generator.GenerateVerificationTokenAsync(user.Id, user.Email),
            "reset" => await generator.GeneratePasswordResetTokenAsync(user.Id, user.Email),
            "magic" => await generator.GenerateMagicLinkTokenAsync(user.Id, user.Email),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var users = new Mock<IUserRepository>();
        users.Setup(value => value.GetByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var service = new NotificationService(CreateDelivery(context), Mock.Of<INotificationPreferenceService>(),
            Mock.Of<INotificationTemplateService>());
        switch (kind)
        {
            case "verification":
                await new SendEmailVerificationRequestedHandler(NullLogger<SendEmailVerificationRequestedHandler>.Instance, service, users.Object)
                    .Handle(new EmailVerificationRequestedNotification { Email = user.Email, Token = token, UserName = user.Name }, CancellationToken.None);
                break;
            case "reset":
                await new SendPasswordResetRequestedHandler(NullLogger<SendPasswordResetRequestedHandler>.Instance, service, users.Object)
                    .Handle(new PasswordResetRequestedNotification { Email = user.Email, Token = token, UserName = user.Name }, CancellationToken.None);
                break;
            case "magic":
                await new SendMagicLinkRequestedHandler(NullLogger<SendMagicLinkRequestedHandler>.Instance, service, users.Object)
                    .Handle(new MagicLinkRequestedNotification { Email = user.Email, Token = token, UserName = user.Name }, CancellationToken.None);
                break;
        }
        var notification = await context.Set<Notification>().SingleAsync(value => value.RecipientId == user.Id);
        return (notification, token);
    }

    private static NotificationDeliveryService CreateDelivery(ApplicationDbContext context)
    {
        var preferences = new Mock<INotificationPreferenceService>();
        preferences.Setup(value => value.DecideDeliveryAsync(It.IsAny<Guid>(), It.IsAny<NotificationType>(),
            It.IsAny<NotificationChannel>(), It.IsAny<GameGuild.Notifications.NotificationPriority>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(NotificationDeliveryDecision.Send());
        return new NotificationDeliveryService(context, preferences.Object, Mock.Of<INotificationTemplateService>(),
            NullLogger<NotificationDeliveryService>.Instance, MetadataProtector);
    }

    private static async Task<User> CreateUser(ApplicationDbContext context)
    {
        var marker = Guid.NewGuid().ToString("N");
        var user = User.Create("notification_" + marker + "@example.test", "Native " + marker);
        context.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}

public sealed class NotificationTokenStoragePostgreSqlFixture : IAsyncLifetime
{
    private EconomyPostgreSqlTestDatabase? _database;
    public async Task InitializeAsync()
    {
        _database = await EconomyPostgreSqlTestDatabase.CreateAsync("notification_token_storage");
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }
    public ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseNpgsql(_database!.ConnectionString).Options);
    public ServiceProvider CreateDataProtectionProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_database!.ConnectionString));
        services.AddDataProtection().SetApplicationName("GameGuild.NotificationNativeFixture")
            .PersistKeysToDbContext<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }
    public async Task DisposeAsync()
    {
        if (_database is not null) await _database.DisposeAsync();
    }
}
