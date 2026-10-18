using FluentAssertions;
using Microsoft.Extensions.Logging;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authentication.UnitTests.Services;

public sealed class ContentTypeHierarchyLogProjectionTests
{
    [Theory]
    [InlineData("\r\n\t")]
    [InlineData("\u0085\u2028\u2029")]
    public async Task CycleWarning_ProjectsBothNamesWithoutChangingStoredKeys(string controls)
    {
        var tenantId = Guid.NewGuid();
        var child = $"Child{controls}Type";
        var parent = $"Parent{controls}Type";
        var rows = new[]
        {
            Registered(tenantId, child, parent),
            Registered(tenantId, parent, child)
        };
        var logger = new CaptureLogger();

        await CreateProvider(rows, logger).GetAncestorsAsync(child, tenantId);

        var warning = logger.Entries.Single(entry => entry.Level == LogLevel.Warning);
        warning.Properties["ContentType"].Should().Be(LogRedaction.Sanitize(parent));
        warning.Properties["Parent"].Should().Be(LogRedaction.Sanitize(child));
        foreach (var character in controls)
        {
            Assert.DoesNotContain(character, warning.Message);
            Assert.DoesNotContain(character, (string)warning.Properties["ContentType"]!);
            Assert.DoesNotContain(character, (string)warning.Properties["Parent"]!);
        }

        rows[0].ContentTypeName.Should().Be(child);
        rows[0].ParentContentTypeName.Should().Be(parent);
        rows[1].ContentTypeName.Should().Be(parent);
        rows[1].ParentContentTypeName.Should().Be(child);
    }

    [Theory]
    [InlineData("\r\n\t")]
    [InlineData("\u0085\u2028\u2029")]
    public async Task AcyclicLookup_PreservesExactPermissionNames(string controls)
    {
        var tenantId = Guid.NewGuid();
        var child = $"Child{controls}Type";
        var parent = $"Parent{controls}Type";
        var logger = new CaptureLogger();

        var ancestors = await CreateProvider(
            [Registered(tenantId, child, parent)], logger).GetAncestorsAsync(child, tenantId);

        ancestors.Should().Equal(parent);
        logger.Entries.Should().BeEmpty();
    }

    private static ContentTypePermission Registered(Guid tenantId, string name, string parent) =>
        new(null, tenantId, name)
        {
            IsRegistered = true,
            ParentContentTypeName = parent
        };

    private static ContentTypePermissionHierarchyProvider CreateProvider(
        IEnumerable<ContentTypePermission> rows,
        CaptureLogger logger)
    {
        var context = new Mock<IApplicationDbContext>();
        var permissions = rows.AsQueryable().BuildMockDbSet();
        context.Setup(value => value.Set<ContentTypePermission>()).Returns(permissions.Object);
        return new ContentTypePermissionHierarchyProvider(context.Object, logger);
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> Properties);

    private sealed class CaptureLogger : ILogger<ContentTypePermissionHierarchyProvider>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>();
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), properties));
        }
    }
}
