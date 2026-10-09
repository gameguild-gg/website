using GameGuild.Notifications.Services.Email;
using Microsoft.AspNetCore.DataProtection;
using FluentAssertions;
using GameGuild.Notifications.Configuration;
using GameGuild.Notifications.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GameGuild.Notifications.Tests;

/// <summary>
/// Tests for EF Core configurations and service constructors.
/// </summary>
public class ConfigurationAndInfrastructureTests
{
    private static ModelBuilder CreateModelBuilder() => new(new ConventionSet());

    [Fact]
    public void NotificationConfiguration_ShouldConfigureEntity()
    {
        var mb = CreateModelBuilder();
        new NotificationConfiguration().Configure(mb.Entity<Notification>());
        mb.Model.FindEntityType(typeof(Notification)).Should().NotBeNull();
    }

    [Fact]
    public void NotificationTemplateConfiguration_ShouldConfigureEntity()
    {
        var mb = CreateModelBuilder();
        new NotificationTemplateConfiguration().Configure(mb.Entity<NotificationTemplate>());
        mb.Model.FindEntityType(typeof(NotificationTemplate)).Should().NotBeNull();
    }

    [Fact]
    public void NotificationPreferenceConfiguration_ShouldConfigureEntity()
    {
        var mb = CreateModelBuilder();
        new NotificationPreferenceConfiguration().Configure(mb.Entity<NotificationPreference>());
        mb.Model.FindEntityType(typeof(NotificationPreference)).Should().NotBeNull();
    }

    [Fact]
    public void NotificationsModelConfiguration_ShouldRegisterCanonicalTenantColumns()
    {
        var options = new DbContextOptionsBuilder<NotificationsModuleDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new NotificationsModuleDbContext(options);
        var notification = context.Model.FindEntityType(typeof(Notification));
        var template = context.Model.FindEntityType(typeof(NotificationTemplate));
        context.Model.FindEntityType(typeof(NotificationPreference)).Should().NotBeNull();
        notification.Should().NotBeNull();
        template.Should().NotBeNull();
        notification!.FindProperty(nameof(Notification.TenantId)).Should().NotBeNull();
        notification.FindProperty(nameof(Notification.NotificationTenantId)).Should().BeNull();
        template!.FindProperty(nameof(NotificationTemplate.TenantId)).Should().NotBeNull();
        template.FindProperty(nameof(NotificationTemplate.TemplateTenantId)).Should().BeNull();
        notification.FindProperty("TenantId1").Should().BeNull();
        template.FindProperty("TenantId1").Should().BeNull();
    }

    [Fact]
    public void NotificationDeliveryService_CanBeInstantiated()
    {
        var service = new NotificationDeliveryService(
            Mock.Of<IApplicationDbContext>(),
            Mock.Of<INotificationPreferenceService>(),
            Mock.Of<INotificationTemplateService>(),
            NullLogger<NotificationDeliveryService>.Instance,
            new NotificationMetadataProtector(new EphemeralDataProtectionProvider()));

        service.Should().NotBeNull();
    }

    private sealed class NotificationsModuleDbContext(DbContextOptions<NotificationsModuleDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new NotificationsModelConfiguration().Configure(modelBuilder);
            base.OnModelCreating(modelBuilder);
        }
    }
}
