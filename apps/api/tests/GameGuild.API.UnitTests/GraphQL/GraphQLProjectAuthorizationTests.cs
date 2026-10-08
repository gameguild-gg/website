using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Core.Security;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Authorization;
using GameGuild.Projects;
using HotChocolate;
using HotChocolate.Execution;
using HotChocolate.Types;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace GameGuild.API.UnitTests.GraphQL;

public sealed class GraphQLProjectAuthorizationTests
{
    [Fact]
    public async Task PermissionDirective_DeniesWithoutDisclosingResourceDetailsOrInvokingResolver()
    {
        var authorization = new GraphQLProjectAuthorizationFake();
        var counters = new GraphQLProjectAuthorizationCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(authorization, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest("{ guarded(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("guarded").ValueKind
            .Should().Be(JsonValueKind.Null);
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("message").GetString().Should().Be("Not authorized to access this resource.");
        error.GetProperty("extensions").GetProperty("code").GetString().Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        authorization.CheckCount.Should().Be(1);
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.UserId == Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6") &&
                record.TenantId == Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85") &&
                record.ResourceType == "Project" &&
                record.ResourceId == "b659b7bf-6281-42e6-a7ef-23d296cff5dd" &&
                record.RequiredPermissions.SequenceEqual(new[] { PermissionType.Read.ToString() }) &&
                record.Outcome == PermissionEvaluationOutcome.Deny &&
                record.Source == "graphql" &&
                record.Operation == "guarded" &&
                record.Reason == "permission_denied" &&
                record.EvaluatedAtUtc != default);
    }

    [Fact]
    public async Task PermissionDirective_RecordsAllowedEvaluationsWithTheSameActorTenantResourceContext()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake((projectId, PermissionType.Read));
        var counters = new GraphQLProjectAuthorizationCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(authorization, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ guarded(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.UserId == Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6") &&
                record.TenantId == Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85") &&
                record.ResourceType == "Project" &&
                record.ResourceId == projectId.ToString("D") &&
                record.RequiredPermissions.SequenceEqual(new[] { PermissionType.Read.ToString() }) &&
                record.Outcome == PermissionEvaluationOutcome.Allow &&
                record.Source == "graphql" &&
                record.Operation == "guarded" &&
                record.Reason == null);
        counters.ResolverCount.Should().Be(1);
    }

    [Fact]
    public async Task PermissionDirective_StillDeniesWhenEvaluationLoggingFails()
    {
        var evaluationLog = new GraphQLPermissionEvaluationLogFake
        {
            ErrorToThrow = new InvalidOperationException("sensitive audit storage details"),
        };
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(
            new GraphQLProjectAuthorizationFake(),
            counters,
            evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ guarded(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("guarded").ValueKind
            .Should().Be(JsonValueKind.Null);
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("extensions").GetProperty("code").GetString().Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("sensitive audit storage details");
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task PermissionDirective_MasksUnauthorizedFieldsAsNullWithoutInvokingTheResolver()
    {
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(
            new GraphQLProjectAuthorizationFake(),
            counters,
            evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ masked(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("masked").ValueKind
            .Should().Be(JsonValueKind.Null);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        body.Should().NotContain("allowed");
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Reason.Should().Be("permission_denied");
    }

    [Fact]
    public async Task AuditSink_MapsActorTenantProjectAndPolicyDetailsToTheDurableAuditRecord()
    {
        CreateAuditLogRequest? recordedRequest = null;
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.TryLogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => recordedRequest = request)
            .ReturnsAsync(true);
        var sink = new AuditPermissionEvaluationLogSink(auditService.Object);

        var persisted = await sink.TryRecordAsync(
            new PermissionEvaluationRecord(
                Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6"),
                Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85"),
                "Project",
                "b659b7bf-6281-42e6-a7ef-23d296cff5dd",
                [PermissionType.Read.ToString(), PermissionType.Edit.ToString()],
                PermissionEvaluationOutcome.Deny,
                "graphql",
                "guarded",
                "permission_denied"),
            CancellationToken.None);

        persisted.Should().BeTrue();
        recordedRequest.Should().NotBeNull();
        recordedRequest!.ActionType.Should().Be(AuditActionTypes.PermissionDenied);
        recordedRequest.ResourceType.Should().Be("Project");
        recordedRequest.ResourceId.Should().Be("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        recordedRequest.UserId.Should().Be(Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6"));
        recordedRequest.TenantId.Should().Be(Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85"));
        recordedRequest.Success.Should().BeFalse();
        recordedRequest.RiskLevel.Should().Be(AuditRiskLevel.High);
        recordedRequest.Category.Should().Be(AuditCategory.Permission);
        using var metadataDocument = JsonDocument.Parse(JsonSerializer.Serialize(recordedRequest.Metadata));
        var metadata = metadataDocument.RootElement;
        metadata.GetProperty("Outcome").GetString().Should().Be("Deny");
        metadata.GetProperty("Operation").GetString().Should().Be("guarded");
        metadata.GetProperty("Source").GetString().Should().Be("graphql");
        metadata.GetProperty("RequiredPermissions").EnumerateArray().Select(item => item.GetString())
            .Should().Equal("Read", "Edit");
        metadata.GetProperty("Reason").GetString().Should().Be("permission_denied");
    }

    [Fact]
    public async Task AuditSink_MapsAllowedEvaluationsAsGrantedAndSurfacesDeliveryFailure()
    {
        CreateAuditLogRequest? recordedRequest = null;
        var auditService = new Mock<IAuditService>();
        auditService
            .Setup(service => service.TryLogAsync(It.IsAny<CreateAuditLogRequest>()))
            .Callback<CreateAuditLogRequest>(request => recordedRequest = request)
            .ReturnsAsync(false);
        var sink = new AuditPermissionEvaluationLogSink(auditService.Object);

        var persisted = await sink.TryRecordAsync(
            new PermissionEvaluationRecord(
                Guid.Parse("4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6"),
                Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85"),
                "Project",
                "b659b7bf-6281-42e6-a7ef-23d296cff5dd",
                [PermissionType.Read.ToString()],
                PermissionEvaluationOutcome.Allow,
                "graphql"),
            CancellationToken.None);

        persisted.Should().BeFalse("the durable audit write failed and the failure must stay observable");
        recordedRequest!.ActionType.Should().Be(AuditActionTypes.PermissionGranted);
        recordedRequest.Success.Should().BeTrue();
        recordedRequest.RiskLevel.Should().Be(AuditRiskLevel.Medium);
        recordedRequest.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task PermissionDirective_SanitizesGraphQlExceptionsFromAuthorizationService()
    {
        var authorization = new GraphQLProjectAuthorizationFake
        {
            ErrorToThrow = new GraphQLException(
                ErrorBuilder.New().SetMessage("sensitive database and tenant details").Build()),
        };
        var counters = new GraphQLProjectAuthorizationCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(authorization, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ guarded(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var error = document.RootElement.GetProperty("errors").EnumerateArray().Single();
        error.GetProperty("message").GetString().Should().Be("Authorization could not be evaluated.");
        error.GetProperty("extensions").GetProperty("code").GetString().Should().Be("AUTHORIZATION_UNAVAILABLE");
        body.Should().NotContain("sensitive database and tenant details");
        body.Should().NotContain("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        counters.ResolverCount.Should().Be(0);
        evaluationLog.Records.Should().ContainSingle().Which.Should().Match<PermissionEvaluationRecord>(
            record =>
                record.Outcome == PermissionEvaluationOutcome.Error &&
                record.Reason == "authorization_evaluation_failed");
    }

    [Fact]
    public async Task PermissionDirective_CachesRepeatedChecksWithinOneRequest()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake((projectId, PermissionType.Read));
        var counters = new GraphQLProjectAuthorizationCounters();
        var evaluationLog = new GraphQLPermissionEvaluationLogFake();
        await using var app = await CreateApplicationAsync(authorization, counters, evaluationLog);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ first(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") second(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.TryGetProperty("errors", out _).Should().BeFalse();
        document.RootElement.GetProperty("data").GetProperty("first").GetString().Should().Be("allowed");
        document.RootElement.GetProperty("data").GetProperty("second").GetString().Should().Be("allowed");
        authorization.CheckCount.Should().Be(1);
        counters.ResolverCount.Should().Be(2);
        evaluationLog.Records.Should().OnlyContain(record => record.Outcome == PermissionEvaluationOutcome.Allow);
        evaluationLog.Records.Should().HaveCount(2);
    }

    [Fact]
    public async Task PermissionDirective_EnforcesAnyAndAllAndReadsNestedInputResourceIds()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake((projectId, PermissionType.Edit));
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(authorization, counters);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ any(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") all(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") nested(input: { projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\" }) }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("any").GetString().Should().Be("allowed");
        document.RootElement.GetProperty("data").GetProperty("all").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("data").GetProperty("nested").GetString().Should().Be("allowed");
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("message").GetString().Should().Be("Not authorized to access this resource.");
        authorization.CheckCount.Should().Be(2);
        counters.ResolverCount.Should().Be(2);
    }

    [Fact]
    public async Task PermissionDirective_AuthorizesNestedFieldsFromTheirParentResource()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake();
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(authorization, counters);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ resource(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") { guardedDetails } }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("resource")
            .GetProperty("guardedDetails").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        body.Should().NotContain("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        authorization.CheckCount.Should().Be(1);
        counters.ResolverCount.Should().Be(0);
    }

    [Fact]
    public async Task PermissionDirective_SelectsFallbackPermissionFromRuntimeArgument()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake((projectId, PermissionType.Delete));
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(authorization, counters);
        using var client = app.GetTestClient();

        using var request = CreateRequest(
            "{ softDelete: delete(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\", softDelete: true) hardDelete: delete(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\", softDelete: false) }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("softDelete").GetString().Should().Be("allowed");
        document.RootElement.GetProperty("data").GetProperty("hardDelete").ValueKind.Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("message").GetString().Should().Be("Not authorized to access this resource.");
        authorization.CheckCount.Should().Be(2);
        counters.ResolverCount.Should().Be(1);
    }

    [Fact]
    public async Task PermissionDirective_CombinesRepeatedPoliciesAndPublishesEachDirective()
    {
        var projectId = Guid.Parse("b659b7bf-6281-42e6-a7ef-23d296cff5dd");
        var authorization = new GraphQLProjectAuthorizationFake((projectId, PermissionType.Read));
        var counters = new GraphQLProjectAuthorizationCounters();
        await using var app = await CreateApplicationAsync(authorization, counters);

        var executor = await app.Services.GetRequiredService<IRequestExecutorResolver>()
            .GetRequestExecutorAsync();
        var repeatedField = executor.Schema.GetType<ObjectType>(executor.Schema.QueryType.Name).Fields["repeated"];
        repeatedField.Directives.Count(directive => directive.Type.Name == "projectAuthorize").Should().Be(2);

        using var client = app.GetTestClient();
        using var request = CreateRequest(
            "{ repeated(projectId: \"b659b7bf-6281-42e6-a7ef-23d296cff5dd\") }");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("data").GetProperty("repeated").ValueKind
            .Should().Be(JsonValueKind.Null);
        document.RootElement.GetProperty("errors").EnumerateArray().Single()
            .GetProperty("extensions").GetProperty("code").GetString()
            .Should().Be("AUTHORIZATION_DENIED");
        authorization.CheckCount.Should().Be(2);
        counters.ResolverCount.Should().Be(0);
    }

    private static HttpRequestMessage CreateRequest(string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query }),
        };
        request.Headers.Add("Authorization", "Bearer user");
        return request;
    }

    private static async Task<WebApplication> CreateApplicationAsync(
        GraphQLProjectAuthorizationFake authorization,
        GraphQLProjectAuthorizationCounters counters,
        GraphQLPermissionEvaluationLogFake? evaluationLog = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, GraphQLTestAuthenticationHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton<IProjectAuthorizationService>(authorization);
        builder.Services.AddSingleton(counters);
        builder.Services.AddSingleton<IActorContextAccessor>(new GraphQLTestActorContextAccessor());
        // Route through the real log service so captured records get the same
        // stamping and sink fan-out behavior as production; the fake only acts
        // as the capture sink.
        if (evaluationLog is not null)
        {
            builder.Services.AddSingleton<IPermissionEvaluationLogSink>(evaluationLog);
        }
        builder.Services.AddSingleton<IPermissionEvaluationLogService, PermissionEvaluationLogService>();
        builder.Services.AddGraphQLServer()
            .AddAuthorization()
            .AddDirectiveType<ProjectAuthorizationDirectiveType>()
            .AddQueryType<GraphQLProjectAuthorizationProbe>()
            .AddTypeExtension<GraphQLProjectAuthorizationNestedResourceResolvers>();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGraphQL("/graphql").RequireAuthorization();
        await app.StartAsync();
        return app;
    }

    private sealed class GraphQLTestActorContextAccessor : IActorContextAccessor
    {
        private ActorContext _actorContext = GameGuild.Identity.Context.Actors.ActorContext.Anonymous with
        {
            SubjectId = "4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6",
            TenantId = Guid.Parse("7b37d70c-6ecd-4eb2-9f21-c08fc9563e85"),
        };

        public ActorContext ActorContext => _actorContext;

        public void SetActorContext(ActorContext context) => _actorContext = context;

        public void ClearActorContext() => _actorContext = GameGuild.Identity.Context.Actors.ActorContext.Anonymous;
    }

    private sealed class GraphQLPermissionEvaluationLogFake : IPermissionEvaluationLogSink
    {
        private readonly ConcurrentQueue<PermissionEvaluationRecord> _records = new();

        public Exception? ErrorToThrow { get; init; }

        public IReadOnlyCollection<PermissionEvaluationRecord> Records => _records.ToArray();

        public Task<bool> TryRecordAsync(
            PermissionEvaluationRecord record,
            CancellationToken cancellationToken = default)
        {
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<bool>(exception);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<bool>(cancellationToken);
            }

            _records.Enqueue(record);
            return Task.FromResult(true);
        }
    }

    private sealed class GraphQLTestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Authorization", out _))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "4b50fdd6-2e85-42bb-a9fa-27f6cb7e97c6")],
                Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private sealed class GraphQLProjectAuthorizationFake(params (Guid ProjectId, PermissionType Permission)[] allowed)
        : IProjectAuthorizationService
    {
        private readonly HashSet<(Guid ProjectId, PermissionType Permission)> _allowed = allowed.ToHashSet();
        private int _checkCount;

        public Exception? ErrorToThrow { get; init; }

        public int CheckCount => Volatile.Read(ref _checkCount);

        public Task<bool> IsActorActiveTenantMemberAsync(CancellationToken cancellationToken = default)
        {
            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<bool>(cancellationToken)
                : Task.FromResult(true);
        }

        public Task<bool> HasPermissionAsync(
            Guid projectId,
            PermissionType permission,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _checkCount);
            if (ErrorToThrow is { } exception)
            {
                return Task.FromException<bool>(exception);
            }

            return cancellationToken.IsCancellationRequested
                ? Task.FromCanceled<bool>(cancellationToken)
                : Task.FromResult(_allowed.Contains((projectId, permission)));
        }
    }

    public sealed class GraphQLProjectAuthorizationCounters
    {
        private int _resolverCount;

        public int ResolverCount => Volatile.Read(ref _resolverCount);

        public string? Resolve()
        {
            Interlocked.Increment(ref _resolverCount);
            return "allowed";
        }
    }

    public sealed class GraphQLProjectAuthorizationProbe(GraphQLProjectAuthorizationCounters counters)
    {
        [RequireGraphQLProjectPermission(PermissionType.Read, ResourceIdArgumentName = "projectId")]
        public string? Guarded(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(
            PermissionType.Read,
            ResourceIdArgumentName = "projectId",
            MaskUnauthorized = true)]
        public string? Masked(Guid projectId) => counters.Resolve();

        public GraphQLProjectAuthorizationNestedResource Resource(Guid projectId) => new(projectId);

        [RequireGraphQLProjectPermission(PermissionType.Read, ResourceIdArgumentName = "projectId")]
        public string? First(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(PermissionType.Read, ResourceIdArgumentName = "projectId")]
        public string? Second(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(
            PermissionType.Edit,
            PermissionType.Delete,
            ResourceIdArgumentName = "projectId",
            Mode = ProjectPermissionEvaluationMode.Any)]
        public string? Any(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(
            PermissionType.Edit,
            PermissionType.Delete,
            ResourceIdArgumentName = "projectId",
            Mode = ProjectPermissionEvaluationMode.All)]
        public string? All(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(PermissionType.Edit, ResourceIdArgumentName = "input.projectId")]
        public string? Nested(GraphQLProjectAuthorizationInput input) => counters.Resolve();

        [RequireGraphQLProjectPermission(PermissionType.Read, ResourceIdArgumentName = "projectId")]
        [RequireGraphQLProjectPermission(PermissionType.Edit, ResourceIdArgumentName = "projectId")]
        public string? Repeated(Guid projectId) => counters.Resolve();

        [RequireGraphQLProjectPermission(
            PermissionType.Delete,
            "softDelete",
            PermissionType.HardDelete,
            ResourceIdArgumentName = "projectId")]
        public string? Delete(Guid projectId, bool softDelete) => counters.Resolve();
    }

    public sealed record GraphQLProjectAuthorizationInput(Guid ProjectId);

    public sealed record GraphQLProjectAuthorizationNestedResource(Guid ProjectId);

    [ExtendObjectType(typeof(GraphQLProjectAuthorizationNestedResource))]
    public sealed class GraphQLProjectAuthorizationNestedResourceResolvers(
        GraphQLProjectAuthorizationCounters counters)
    {
        [RequireGraphQLProjectPermission(
            PermissionType.Read,
            ResourceIdParentPropertyName = "ProjectId")]
        public string? GuardedDetails([Parent] GraphQLProjectAuthorizationNestedResource resource) => counters.Resolve();
    }
}
