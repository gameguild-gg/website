using FluentAssertions;
using GameGuild.Identity.Context.Actors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace GameGuild.Identity.Authorization.UnitTests;

public sealed class RequestContextLoggingSecurityTests
{
    public static IEnumerable<object[]> ControlCharacters =>
        Enumerable.Range(0, 32).Concat(Enumerable.Range(127, 33))
            .Concat([0x2028, 0x2029]).Select(value => new object[] { ((char)value).ToString() });

    [Theory]
    [MemberData(nameof(ControlCharacters))]
    public async Task InvokeAsync_SanitizesEveryRenderedAndStructuredRequestValue(string control)
    {
        var logger = new CapturingRequestLogger();
        var subject = "private.subject@example.invalid";
        var tenant = Guid.NewGuid();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = $"trace{control}forged"
        };
        context.Request.Method = $"GET{control}forged";
        context.Request.Path = $"/api/{control}forged";
        var accessor = CreateAccessor(subject, tenant, $"role{control}forged");
        var middleware = new RequestContextLoggingMiddleware(
            http => { http.Response.StatusCode = StatusCodes.Status204NoContent; return Task.CompletedTask; },
            logger);

        await middleware.InvokeAsync(context, accessor);

        logger.Entries.Should().HaveCount(2);
        logger.Entries.Should().OnlyContain(entry => entry.Level == LogLevel.Information && entry.EventId.Id == 0);
        logger.Scopes.Should().ContainSingle();
        logger.ScopeDisposed.Should().BeTrue();
        var values = logger.Entries.SelectMany(entry => entry.Properties.Values)
            .Concat(logger.Scopes.Single().Values).OfType<string>()
            .Concat(logger.Entries.Select(entry => entry.Rendered));
        foreach (var value in values)
        {
            value.Should().NotContain(control);
            value.Should().NotContain(subject);
            value.Should().NotContain(tenant.ToString());
        }

        logger.Scopes.Single()["RequestId"].Should().Be(LogRedaction.Sanitize(context.TraceIdentifier));
        logger.Scopes.Single()["Roles"].Should().Be(LogRedaction.Sanitize($"role{control}forged"));
        logger.Scopes.Single()["UserId"].Should().Be(LogRedaction.RedactId(subject, "uid"));
        logger.Scopes.Single()["TenantId"].Should().Be(LogRedaction.RedactId(tenant, "tid"));
        context.Response.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task InvokeAsync_LogsFailureTypeWithoutSensitiveExceptionPayloadAndRethrowsOriginal()
    {
        const string sensitivePayload = "private.exception@example.invalid secret-token-value\r\nforged";
        var original = new InvalidOperationException(sensitivePayload);
        var logger = new CapturingRequestLogger();
        var context = new DefaultHttpContext { TraceIdentifier = "trace\r\nforged" };
        var middleware = new RequestContextLoggingMiddleware(_ => Task.FromException(original), logger);

        var act = () => middleware.InvokeAsync(context, CreateAccessor("subject", Guid.NewGuid(), "role"));
        var failure = await act.Should().ThrowAsync<InvalidOperationException>();

        failure.Which.Should().BeSameAs(original);
        logger.ScopeDisposed.Should().BeTrue();
        var error = logger.Entries.Single(entry => entry.Level == LogLevel.Error);
        error.Exception.Should().BeNull();
        error.Rendered.Should().NotContain(sensitivePayload).And.NotContain("secret-token-value");
        error.Properties.Should().NotContainKey("ErrorMessage");
        error.Properties["ErrorType"].Should().Be(typeof(InvalidOperationException).FullName);
        error.Properties["RequestId"].Should().Be(LogRedaction.Sanitize(context.TraceIdentifier));
    }

    private static IActorContextAccessor CreateAccessor(string subject, Guid tenant, string role)
    {
        var accessor = new Mock<IActorContextAccessor>();
        accessor.Setup(value => value.ActorContext).Returns(new ActorContext
        {
            IsAuthenticated = true,
            ActorKind = ActorKind.User,
            SubjectId = subject,
            TenantId = tenant,
            Roles = new HashSet<string> { role },
            Permissions = new HashSet<string>()
        });
        return accessor.Object;
    }

    private sealed class CapturingRequestLogger : ILogger<RequestContextLoggingMiddleware>
    {
        public List<LogEntry> Entries { get; } = [];
        public List<Dictionary<string, object?>> Scopes { get; } = [];
        public bool ScopeDisposed { get; private set; }

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(CaptureProperties(state));
            return new CaptureScope(() => ScopeDisposed = true);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), CaptureProperties(state), exception));
            }
        }

        private static Dictionary<string, object?> CaptureProperties<TState>(TState state) =>
            state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(value => value.Key, value => value.Value)
                : throw new InvalidOperationException("Expected structured logging state.");
    }

    private sealed class CaptureScope(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }

    private sealed record LogEntry(LogLevel Level, EventId EventId, string Rendered,
        Dictionary<string, object?> Properties, Exception? Exception);
}
