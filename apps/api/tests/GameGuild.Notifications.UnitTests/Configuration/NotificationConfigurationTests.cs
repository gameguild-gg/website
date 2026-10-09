using GameGuild.Notifications.UnitTests.Infrastructure;

namespace GameGuild.Notifications.UnitTests.Configuration;

public class NotificationConfigurationTests
{
    [Fact]
    public void Model_Should_Contain_Notification_Entity_With_Query_Filter_And_Template_Relationship()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(Notification));

        entityType.Should().NotBeNull();
        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
        entityType.FindProperty(nameof(Notification.Title))!.GetMaxLength().Should().Be(200);
        entityType.FindProperty(nameof(Notification.Type))!.GetMaxLength().Should().Be(50);
        entityType.FindProperty(nameof(Notification.Channel))!.GetMaxLength().Should().Be(50);
        entityType.FindProperty(nameof(Notification.Priority))!.GetMaxLength().Should().Be(20);
        entityType.FindProperty(nameof(Notification.Metadata))!.GetMaxLength().Should().BeNull();
        entityType.FindProperty(nameof(Notification.Metadata))!.FindAnnotation("Relational:ColumnType")!.Value.Should().Be("text");
        entityType.FindNavigation(nameof(Notification.Template)).Should().NotBeNull();
    }

    [Fact]
    public void Model_Should_Contain_NotificationTemplate_Entity_With_Query_Filter()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(NotificationTemplate));

        entityType.Should().NotBeNull();
        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
        entityType.FindProperty(nameof(NotificationTemplate.Code))!.GetMaxLength().Should().Be(100);
        entityType.FindProperty(nameof(NotificationTemplate.Name))!.GetMaxLength().Should().Be(200);
        entityType.FindProperty(nameof(NotificationTemplate.MessageTemplate))!.GetMaxLength().Should().Be(4000);
    }

    [Fact]
    public void Model_Should_Contain_NotificationPreference_Entity_With_Query_Filter()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(NotificationPreference));

        entityType.Should().NotBeNull();
        entityType!.GetDeclaredQueryFilters().Should().NotBeEmpty();
        entityType.FindProperty(nameof(NotificationPreference.Timezone))!.GetMaxLength().Should().Be(50);
        entityType.FindProperty(nameof(NotificationPreference.MutedTypes))!.GetMaxLength().Should().Be(500);
    }

    private static NotificationsTestDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotificationsTestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new NotificationsTestDbContext(options);
    }
}
