using System.Text.Json;
using FluentAssertions;
using GameGuild.API.Core.Logging;
using GameGuild.API.Database;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authentication;
using GameGuild.Identity.Context.Actors;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.Services;

// =====================================================================================
// PII redaction & tokenization pipeline (issue #192): rule matching matrix, redact vs
// tokenize, fail-closed behavior, audit-sink integration (EF interceptor), admin CRUD
// authorization/versioning/auditing, and the structured-log sink.
// =====================================================================================

#region Engine: rule matching matrix

public class PiiRedactionEngineMatchingTests
{
    private static CompiledPiiRule PresetRule(PiiPresetPattern preset, PiiRuleAction action = PiiRuleAction.Redact, PiiRuleScope scope = PiiRuleScope.Global, string replacement = "[PII-REDACTED]", int priority = 100, string[]? fields = null) =>
        new()
        {
            RuleId = Guid.NewGuid(),
            RuleName = preset.ToString(),
            Scope = scope,
            Action = action,
            Priority = priority,
            Preset = preset,
            Replacement = replacement,
            TargetFields = fields ?? [],
            Matcher = PiiRedactionEngine.GetPresetPattern(preset)
        };

    private static CompiledPiiRule RegexRule(string pattern, PiiRuleScope scope = PiiRuleScope.Global, string[]? fields = null, int priority = 100, PiiRuleAction action = PiiRuleAction.Redact) =>
        new()
        {
            RuleId = Guid.NewGuid(),
            RuleName = "custom",
            Scope = scope,
            Action = action,
            Priority = priority,
            Replacement = "[CUSTOM]",
            TargetFields = fields ?? [],
            Matcher = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.Compiled)
        };

    [Theory]
    [InlineData("contact me at john.doe@example.com please", PiiPresetPattern.Email, "john.doe@example.com")]
    [InlineData("ssn on file: 123-45-6789", PiiPresetPattern.NationalId, "123-45-6789")]
    [InlineData("call +1 (555) 123-4567 now", PiiPresetPattern.Phone, "+1 (555) 123-4567")]
    [InlineData("card 4111 1111 1111 1111 charged", PiiPresetPattern.CreditCard, "4111 1111 1111 1111")]
    public void ApplyToValue_PresetRules_ReplaceMatchedPii(string input, PiiPresetPattern preset, string matched)
    {
        var outcome = PiiRedactionEngine.ApplyToValue(input, "AnyField", [PresetRule(preset)], new FixedTokens());

        outcome.Changed.Should().BeTrue();
        outcome.Value!.Should().NotContain(matched);
        outcome.Value.Should().Contain("[PII-REDACTED]");
        outcome.AppliedRuleId.Should().NotBeNull();
    }

    [Fact]
    public void ApplyToValue_EmailRule_LeavesNonEmailTextIntact()
    {
        var outcome = PiiRedactionEngine.ApplyToValue("user performed action on resource 42", "Description", [PresetRule(PiiPresetPattern.Email)], new FixedTokens());

        outcome.Changed.Should().BeFalse();
    }

    [Fact]
    public void ApplyToValue_CreditCard_LuhnInvalidNumberIsNotMatched()
    {
        // Same shape as 4111 1111 1111 1111 but failing the Luhn checksum.
        var outcome = PiiRedactionEngine.ApplyToValue("card 4111 1111 1111 1112 charged", "AnyField", [PresetRule(PiiPresetPattern.CreditCard)], new FixedTokens());

        outcome.Changed.Should().BeFalse();
    }

    [Fact]
    public void ApplyToValue_Phone_TooFewDigitsIsNotMatched()
    {
        var outcome = PiiRedactionEngine.ApplyToValue("reference 1234567 done", "AnyField", [PresetRule(PiiPresetPattern.Phone)], new FixedTokens());

        outcome.Changed.Should().BeFalse();
    }

    [Fact]
    public void ApplyToValue_FieldScopeRule_OnlyAppliesToTargetedFields()
    {
        var rule = PresetRule(PiiPresetPattern.Email, fields: ["Metadata"], scope: PiiRuleScope.FieldName);

        var targeted = PiiRedactionEngine.ApplyToValue("mail john@example.com", "Metadata", [rule], new FixedTokens());
        var other = PiiRedactionEngine.ApplyToValue("mail john@example.com", "Description", [rule], new FixedTokens());

        targeted.Changed.Should().BeTrue();
        other.Changed.Should().BeFalse();
    }

    [Fact]
    public void ApplyToValue_FieldRule_WinsOverGlobalRuleRegardlessOfPriority()
    {
        var globalLowPriority = PresetRule(PiiPresetPattern.Email, scope: PiiRuleScope.Global, priority: 0, replacement: "[GLOBAL]");
        var fieldHighPriority = PresetRule(PiiPresetPattern.Email, scope: PiiRuleScope.FieldName, priority: 900, replacement: "[FIELD]", fields: ["Contact"]);

        var outcome = PiiRedactionEngine.ApplyToValue("john@example.com", "Contact", [globalLowPriority, fieldHighPriority], new FixedTokens());

        outcome.Value.Should().Contain("[FIELD]");
        outcome.Value.Should().NotContain("[GLOBAL]");
    }

    [Fact]
    public void ApplyToValue_WithinSameScope_LowerPriorityWins()
    {
        var first = PresetRule(PiiPresetPattern.Email, priority: 10, replacement: "[FIRST]");
        var second = PresetRule(PiiPresetPattern.Email, priority: 20, replacement: "[SECOND]");

        var outcome = PiiRedactionEngine.ApplyToValue("john@example.com", "Any", [second, first], new FixedTokens());

        outcome.Value.Should().Contain("[FIRST]");
    }

    [Fact]
    public void ApplyToValue_CustomRegex_MatchesOrganizationPattern()
    {
        var rule = RegexRule(@"EMP-\d{6}");

        var outcome = PiiRedactionEngine.ApplyToValue("employee EMP-123456 signed", "Description", [rule], new FixedTokens());

        outcome.Value.Should().Be("employee [CUSTOM] signed");
    }

    [Fact]
    public void ApplyToValue_RedactVsTokenize_RuleActionDeterminesReplacement()
    {
        var redact = PresetRule(PiiPresetPattern.Email, action: PiiRuleAction.Redact);
        var tokenize = PresetRule(PiiPresetPattern.Email, action: PiiRuleAction.Tokenize);

        var redacted = PiiRedactionEngine.ApplyToValue("john@example.com", "Any", [redact], new FixedTokens());
        var tokenized = PiiRedactionEngine.ApplyToValue("john@example.com", "Any", [tokenize], new FixedTokens());

        redacted.Value.Should().Be("[PII-REDACTED]");
        tokenized.Value.Should().Be("pii_tok_v1_fixedtoken0000000000");
        tokenized.Action.Should().Be(PiiRuleAction.Tokenize);
    }

    private sealed class FixedTokens : IPiiTokenSource
    {
        public string Tokenize(string value) => "pii_tok_v1_fixedtoken0000000000";
    }
}

#endregion

#region Tokenizer: deterministic HMAC tokens

public class PiiHmacTokenizerTests
{
    private static PiiHmacTokenizer CreateTokenizer() =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!"
            }).Build(),
            NullLogger<PiiHmacTokenizer>.Instance);

    [Fact]
    public void Tokenize_IsDeterministicForIdenticalValues()
    {
        var tokenizer = CreateTokenizer();

        var first = tokenizer.Tokenize("john@example.com");
        var second = tokenizer.Tokenize("john@example.com");

        first.Should().Be(second);
        first.Should().StartWith(PiiHmacTokenizer.TokenPrefix);
    }

    [Fact]
    public void Tokenize_DistinguishesDifferentValues()
    {
        var tokenizer = CreateTokenizer();

        tokenizer.Tokenize("john@example.com").Should().NotBe(tokenizer.Tokenize("jane@example.com"));
    }

    [Fact]
    public void Tokenize_IsStableAcrossInstancesDerivedFromTheSameKey()
    {
        var first = CreateTokenizer().Tokenize("john@example.com");
        var second = CreateTokenizer().Tokenize("john@example.com");

        first.Should().Be(second);
    }

    [Fact]
    public void Tokenize_FailsClosedWithoutConfiguredKey()
    {
        var tokenizer = new PiiHmacTokenizer(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build(),
            NullLogger<PiiHmacTokenizer>.Instance);

        var act = () => tokenizer.Tokenize("john@example.com");

        act.Should().Throw<InvalidOperationException>().WithMessage("*fails closed*");
    }
}

#endregion

#region Pipeline service: audit record application, vault, fail-closed

public class PiiRedactionServiceTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly TestApplicationDbContext _context;

    private static readonly Guid Tenant = Guid.NewGuid();

    public PiiRedactionServiceTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PiiDb_{Guid.NewGuid()}", databaseRoot)
            .Options;
        _context = new TestApplicationDbContext(options);
        var collection = new ServiceCollection();
        collection.AddScoped<IApplicationDbContext>(_ => _context);
        _serviceProvider = collection.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }

    private static PiiRedactionService CreateService(IPiiRedactionRuleProvider provider, IEncryptionService? encryption = null) =>
        new(
            provider,
            CreateTokenizer(),
            encryption ?? CreateEncryption(),
            NullLogger<PiiRedactionService>.Instance);

    private static PiiHmacTokenizer CreateTokenizer() =>
        new(
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!"
            }).Build(),
            NullLogger<PiiHmacTokenizer>.Instance);

    private static EncryptionService CreateEncryption() =>
        new(
            NullLogger<EncryptionService>.Instance,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!"
            }).Build());

    private static PiiRedactionRule MakeRule(
        PiiPresetPattern preset,
        PiiRuleAction action = PiiRuleAction.Redact,
        PiiRuleScope scope = PiiRuleScope.Global,
        string[]? fields = null) =>
        PiiRedactionRule.Create(Tenant, $"rule-{preset}-{action}", PiiRulePatternType.Preset, null, preset, action, scope, fields ?? [], 100, null, null);

    [Fact]
    public async Task ApplyToAuditLogAsync_RedactsAllProtectableFieldsIncludingMetadataJson()
    {
        var rules = new FakeRuleProvider(MakeRule(PiiPresetPattern.Email));
        var service = CreateService(rules);
        var entry = new AuditLog
        {
            ActionType = "Update",
            ResourceType = "Profile",
            ResourceId = "john@example.com",
            Description = "changed email to jane@example.com",
            ErrorMessage = "failed for john@example.com",
            UserAgent = "agent john@example.com",
            Metadata = JsonSerializer.Serialize(new { nested = new { email = "deep@example.com" } })
        };

        var summary = await service.ApplyToAuditLogAsync(entry, _context, CancellationToken.None);

        summary.Changed.Should().BeTrue();
        summary.FailClosed.Should().BeFalse();
        entry.Description.Should().NotContain("jane@example.com");
        entry.ErrorMessage.Should().NotContain("john@example.com");
        entry.UserAgent.Should().NotContain("john@example.com");
        entry.ResourceId.Should().NotContain("john@example.com");
        entry.Metadata.Should().NotContain("deep@example.com");
        entry.Metadata.Should().Contain("[PII-REDACTED]");
    }

    [Fact]
    public async Task ApplyToAuditLogAsync_Tokenize_WritesEncryptedVaultEntryAndOperationTrail()
    {
        var rule = MakeRule(PiiPresetPattern.Email, PiiRuleAction.Tokenize);
        var service = CreateService(new FakeRuleProvider(rule));
        var encryption = CreateEncryption();
        var entry = new AuditLog
        {
            ActionType = "Export",
            ResourceType = "Report",
            Description = "exported for john@example.com"
        };

        var summary = await service.ApplyToAuditLogAsync(entry, _context, CancellationToken.None);

        summary.TokenizedValueCount.Should().BeGreaterThan(0);
        entry.Description.Should().Contain(PiiHmacTokenizer.TokenPrefix);
        entry.Description.Should().NotContain("john@example.com");

        var vaultEntry = await _context.Set<PiiTokenVaultEntry>().SingleAsync();
        vaultEntry.EncryptedValue.Should().NotBe("john@example.com");
        vaultEntry.SourceRuleId.Should().Be(rule.Id);
        encryption.Decrypt(vaultEntry.EncryptedValue).Should().Be("john@example.com");

        var operation = await _context.Set<PiiRedactionOperation>().SingleAsync();
        operation.Source.Should().Be(PiiRedactionSource.AuditSink);
        operation.FailClosed.Should().BeFalse();
        operation.AppliedRuleIds.Should().Contain(rule.Id);
    }

    [Fact]
    public async Task ApplyToAuditLogAsync_NoRules_NoOperationTrailAndUnchangedRecord()
    {
        var service = CreateService(new FakeRuleProvider());
        var entry = new AuditLog { ActionType = "Read", ResourceType = "Doc", Description = "plain text" };

        var summary = await service.ApplyToAuditLogAsync(entry, _context, CancellationToken.None);

        summary.Changed.Should().BeFalse();
        (await _context.Set<PiiRedactionOperation>().CountAsync()).Should().Be(0);
        entry.Description.Should().Be("plain text");
    }

    [Fact]
    public async Task ApplyToAuditLogAsync_FailsClosedWhenRulesAreUnavailable()
    {
        var provider = new FakeRuleProvider();
        provider.ThrowOnLoad = true;
        var service = CreateService(provider);
        var entry = new AuditLog
        {
            ActionType = "Sensitive",
            ResourceType = "Profile",
            ResourceId = "john@example.com",
            Description = "contains john@example.com",
            ErrorMessage = "error john@example.com",
            UserAgent = "ua john@example.com",
            Metadata = "{\"email\":\"john@example.com\"}"
        };

        var summary = await service.ApplyToAuditLogAsync(entry, _context, CancellationToken.None);

        summary.FailClosed.Should().BeTrue();
        summary.Changed.Should().BeTrue();
        entry.Description.Should().Be(PiiRedactionFailClosed.Marker);
        entry.ErrorMessage.Should().Be(PiiRedactionFailClosed.Marker);
        entry.UserAgent.Should().Be(PiiRedactionFailClosed.Marker);
        entry.ResourceId.Should().Be(PiiRedactionFailClosed.Marker);
        entry.Metadata.Should().NotContain("john@example.com");

        var operation = await _context.Set<PiiRedactionOperation>().SingleAsync();
        operation.FailClosed.Should().BeTrue();
        operation.FailureReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RedactRequestForDurableFallbackAsync_SanitizesProtectableFields()
    {
        var service = CreateService(new FakeRuleProvider(MakeRule(PiiPresetPattern.Email)));
        var request = new CreateAuditLogRequest
        {
            ActionType = "Failed",
            ResourceType = "Payment",
            ResourceId = "john@example.com",
            Description = "payer john@example.com",
            ErrorMessage = "error",
            Metadata = new { Payer = "john@example.com" }
        };

        var sanitized = await service.RedactRequestForDurableFallbackAsync(request, CancellationToken.None);

        sanitized.ResourceId.Should().NotContain("@");
        sanitized.Description.Should().NotContain("john@example.com");
        JsonSerializer.Serialize(sanitized.Metadata).Should().NotContain("john@example.com");
        // The original request object must not be mutated.
        request.ResourceId.Should().Be("john@example.com");
    }

    private sealed class FakeRuleProvider(params PiiRedactionRule[] rules) : IPiiRedactionRuleProvider
    {
        public bool ThrowOnLoad { get; set; }

        public Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken) => Load();

        public Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken) => Load();

        public PiiRuleSet? TryGetGlobalSnapshot() => ThrowOnLoad ? null : PiiRedactionRuleProvider.Compile(rules);

        public PiiRedactionCacheStatus GetCacheStatus() => new(false, null, 0, 0, null, null);

        public void Invalidate(Guid? tenantId) { }

        private Task<PiiRuleSet> Load() =>
            ThrowOnLoad
                ? Task.FromException<PiiRuleSet>(new PiiRedactionUnavailableException(new InvalidOperationException("rule store down")))
                : Task.FromResult(PiiRedactionRuleProvider.Compile(rules));
    }
}

#endregion

#region Audit sink integration: EF interceptor over the central audit sink

public class AuditLogPiiRedactionInterceptorTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly TestApplicationDbContext _context;

    public AuditLogPiiRedactionInterceptorTests()
    {
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PiiInterceptorDb_{Guid.NewGuid()}", databaseRoot)
            .Options;
        _context = new TestApplicationDbContext(options);
        var collection = new ServiceCollection();
        collection.AddScoped<IApplicationDbContext>(_ => _context);
        _serviceProvider = collection.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _context.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_AppliesPipelineToAuditLogEntities_CentralSinkIntegration()
    {
        var rule = PiiRedactionRule.Create(null, "global-email", PiiRulePatternType.Preset, null, PiiPresetPattern.Email, PiiRuleAction.Redact, PiiRuleScope.Global, [], 100, null, null);
        var provider = new SnapshotProvider(PiiRedactionRuleProvider.Compile([rule]));
        var service = new PiiRedactionService(
            provider,
            new PiiHmacTokenizer(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!" }).Build(),
                NullLogger<PiiHmacTokenizer>.Instance),
            new EncryptionService(
                NullLogger<EncryptionService>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!" }).Build()),
            NullLogger<PiiRedactionService>.Instance);

        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PiiInterceptorSinkDb_{Guid.NewGuid()}", databaseRoot)
            .AddInterceptors(new AuditLogPiiRedactionInterceptor(service))
            .Options;
        await using var context = new TestApplicationDbContext(options);

        context.Set<AuditLog>().Add(new AuditLog
        {
            ActionType = "SignIn",
            ResourceType = "Session",
            Description = "sign-in for john@example.com",
            Metadata = JsonSerializer.Serialize(new { Email = "john@example.com" })
        });

        await context.SaveChangesAsync();

        var persisted = await context.Set<AuditLog>().SingleAsync();
        persisted.Description.Should().NotContain("john@example.com");
        persisted.Metadata.Should().NotContain("john@example.com");
        (await context.Set<PiiRedactionOperation>().CountAsync()).Should().Be(1);
    }

    private sealed class SnapshotProvider(PiiRuleSet snapshot) : IPiiRedactionRuleProvider
    {
        public Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken) => Task.FromResult(snapshot);

        public Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken) => Task.FromResult(snapshot);

        public PiiRuleSet? TryGetGlobalSnapshot() => snapshot;

        public PiiRedactionCacheStatus GetCacheStatus() => new(true, snapshot.LoadedAt, 1, snapshot.Rules.Count, null, null);

        public void Invalidate(Guid? tenantId) { }
    }
}

#endregion

#region Administration: guard, versioning, auditing

public class PiiRedactionAdminServiceTests : IDisposable
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    private readonly TestApplicationDbContext _context;
    private readonly ActorContextAccessor _actorAccessor = new();
    private readonly Mock<IAuditService> _audit = new();

    public PiiRedactionAdminServiceTests()
    {
        _audit.Setup(audit => audit.LogAsync(It.IsAny<CreateAuditLogRequest>())).Returns(Task.CompletedTask);
        _audit.Setup(audit => audit.LogPermissionDenyAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid?>()))
            .Returns(Task.CompletedTask);
        var databaseRoot = new InMemoryDatabaseRoot();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"PiiAdminDb_{Guid.NewGuid()}", databaseRoot)
            .Options;
        _context = new TestApplicationDbContext(options);
    }

    public void Dispose() => _context.Dispose();

    private IPiiRedactionAdminService CreateService() =>
        new PiiRedactionAdminService(
            _context,
            new SnapshotProvider(),
            new PiiHmacTokenizer(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!" }).Build(),
                NullLogger<PiiHmacTokenizer>.Instance),
            new EncryptionService(
                NullLogger<EncryptionService>.Instance,
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!" }).Build()),
            _actorAccessor,
            _audit.Object,
            NullLogger<PiiRedactionAdminService>.Instance);

    private void SignInAsTenantAdmin() =>
        _actorAccessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Admin.ToString(),
            TenantId = Tenant,
            Roles = new HashSet<string> { "TenantAdmin" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });

    private void SignInAsMember() =>
        _actorAccessor.SetActorContext(new ActorContext
        {
            ActorKind = ActorKind.User,
            SubjectId = Guid.NewGuid().ToString(),
            TenantId = Tenant,
            Roles = new HashSet<string> { "Member" },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        });

    private static UpsertPiiRedactionRuleRequest EmailRuleRequest(PiiRuleAction action = PiiRuleAction.Redact) => new()
    {
        RuleName = "Email protection",
        PatternType = PiiRulePatternType.Preset,
        Preset = PiiPresetPattern.Email,
        Action = action,
        Scope = PiiRuleScope.Global,
        TargetFields = [],
        Priority = 100,
        IsEnabled = true
    };

    [Fact]
    public async Task CreateRule_NonAdministrator_IsDeniedAndPermissionDenyIsAudited()
    {
        SignInAsMember();
        var service = CreateService();

        var act = () => service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PiiRedactionUnauthorizedException>();
        _audit.Verify(audit => audit.LogPermissionDenyAsync(
            It.IsAny<Guid?>(),
            "audit:pii-redaction-pipeline",
            "PiiRedactionRule",
            It.IsAny<string?>(),
            It.IsAny<string>(),
            It.IsAny<Guid?>()), Times.Once);
    }

    [Fact]
    public async Task CreateRule_AnonymousActor_IsDenied()
    {
        _actorAccessor.SetActorContext(ActorContext.Anonymous);
        var service = CreateService();

        var act = () => service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PiiRedactionUnauthorizedException>();
    }

    [Fact]
    public async Task CreateRule_TenantAdministrator_PersistsRuleAndAuditsMutation()
    {
        SignInAsTenantAdmin();
        var service = CreateService();

        var created = await service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        created.TenantId.Should().Be(Tenant);
        created.IsEnabled.Should().BeTrue();
        created.Revision.Should().Be(0);
        (await _context.Set<PiiRedactionRule>().CountAsync(rule => rule.Id == created.Id)).Should().Be(1);
        _audit.Verify(audit => audit.LogAsync(
            It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.PiiRedactionRuleCreated
                && request.ResourceId == created.Id.ToString()
                && request.TenantId == Tenant
                && request.UserId == Admin)), Times.Once);
    }

    [Fact]
    public async Task UpdateRule_StaleRevision_IsRejectedWithConcurrencyConflict()
    {
        SignInAsTenantAdmin();
        var service = CreateService();
        var created = await service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        var act = () => service.UpdateRuleAsync(created.Id, expectedRevision: created.Revision + 5, EmailRuleRequest(), CancellationToken.None);

        await act.Should().ThrowAsync<PiiRedactionConcurrencyException>();
    }

    [Fact]
    public async Task UpdateRule_CurrentRevision_AppliesChangeAndBumpsRevision()
    {
        SignInAsTenantAdmin();
        var service = CreateService();
        var created = await service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        var update = EmailRuleRequest(PiiRuleAction.Tokenize) with { RuleName = "Email tokenization", IsEnabled = false };
        var updated = await service.UpdateRuleAsync(created.Id, created.Revision, update, CancellationToken.None);

        updated.RuleName.Should().Be("Email tokenization");
        updated.Action.Should().Be(PiiRuleAction.Tokenize);
        updated.IsEnabled.Should().BeFalse();
        updated.Revision.Should().Be(created.Revision + 1);
        _audit.Verify(audit => audit.LogAsync(
            It.Is<CreateAuditLogRequest>(request => request.ActionType == AuditActionTypes.PiiRedactionRuleUpdated)), Times.Once);
    }

    [Fact]
    public async Task DeleteRule_CurrentRevision_RemovesRuleAndAuditsHighRisk()
    {
        SignInAsTenantAdmin();
        var service = CreateService();
        var created = await service.CreateRuleAsync(EmailRuleRequest(), CancellationToken.None);

        await service.DeleteRuleAsync(created.Id, created.Revision, CancellationToken.None);

        (await _context.Set<PiiRedactionRule>().CountAsync(rule => rule.Id == created.Id)).Should().Be(0);
        _audit.Verify(audit => audit.LogAsync(
            It.Is<CreateAuditLogRequest>(request =>
                request.ActionType == AuditActionTypes.PiiRedactionRuleDeleted
                && request.RiskLevel == AuditRiskLevel.High)), Times.Once);
    }

    [Theory]
    [InlineData(PiiRulePatternType.Regex, null, null, "regexPattern")]
    [InlineData(PiiRulePatternType.Preset, null, null, "preset")]
    [InlineData(PiiRulePatternType.Preset, "(valid", null, "regexPattern")]
    public async Task CreateRule_InvalidPayload_IsRejected(PiiRulePatternType type, string? regex, PiiPresetPattern? preset, string expectedErrorKey)
    {
        SignInAsTenantAdmin();
        var service = CreateService();
        var request = new UpsertPiiRedactionRuleRequest
        {
            RuleName = "bad",
            PatternType = type,
            RegexPattern = regex,
            Preset = preset,
            Action = PiiRuleAction.Redact,
            Scope = PiiRuleScope.Global,
            TargetFields = []
        };

        var act = () => service.CreateRuleAsync(request, CancellationToken.None);

        (await act.Should().ThrowAsync<PiiRedactionValidationException>())
            .Which.Errors.Should().ContainKey(expectedErrorKey);
    }

    [Fact]
    public async Task CreateRule_FieldScopeWithoutTargetFields_IsRejected()
    {
        SignInAsTenantAdmin();
        var service = CreateService();
        var request = EmailRuleRequest() with { Scope = PiiRuleScope.FieldName, TargetFields = [] };

        var act = () => service.CreateRuleAsync(request, CancellationToken.None);

        (await act.Should().ThrowAsync<PiiRedactionValidationException>())
            .Which.Errors.Should().ContainKey("targetFields");
    }

    [Fact]
    public async Task ResolveToken_RequiresInvestigationReason()
    {
        SignInAsTenantAdmin();
        var service = CreateService();

        var act = () => service.ResolveTokenAsync("pii_tok_v1_abc", "short", CancellationToken.None);

        (await act.Should().ThrowAsync<PiiRedactionValidationException>())
            .Which.Errors.Should().ContainKey("reason");
    }

    [Fact]
    public async Task ResolveToken_UnknownToken_FailsClosedWithNotFound()
    {
        SignInAsTenantAdmin();
        var service = CreateService();

        var act = () => service.ResolveTokenAsync("pii_tok_v1_abc", "investigating incident INC-42", CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    private sealed class SnapshotProvider : IPiiRedactionRuleProvider
    {
        public Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(PiiRedactionRuleProvider.Compile([]));

        public Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(PiiRedactionRuleProvider.Compile([]));

        public PiiRuleSet? TryGetGlobalSnapshot() => PiiRedactionRuleProvider.Compile([]);

        public PiiRedactionCacheStatus GetCacheStatus() => new(true, SystemClock.UtcNow, 1, 0, null, null);

        public void Invalidate(Guid? tenantId) { }
    }
}

#endregion

#region Structured-log sink

public class PiiRedactionLogSinkTests
{
    private static LogEvent MakeEvent(Action<Dictionary<string, LogEventPropertyValue>>? configure = null)
    {
        var properties = new Dictionary<string, LogEventPropertyValue>
        {
            ["Application"] = new ScalarValue("GameGuild.API"),
            ["ContactEmail"] = new ScalarValue("john@example.com"),
            ["Note"] = new ScalarValue("plain note"),
            ["Card"] = new ScalarValue("4111111111111111")
        };
        configure?.Invoke(properties);
        return new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            exception: null,
            messageTemplate: new MessageTemplate(string.Empty, []),
            properties: properties.Select(pair => new LogEventProperty(pair.Key, pair.Value)));
    }

    private static PiiRedactionLogSink CreateSink(IPiiRedactionRuleProvider provider, CapturingSink capture) =>
        new(
            capture,
            provider,
            new PiiHmacTokenizer(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Encryption:EncryptionKey"] = "test-key-material-for-pii-tokenization-v1!!" }).Build(),
                NullLogger<PiiHmacTokenizer>.Instance),
            NullLogger<PiiRedactionLogSink>.Instance);

    [Fact]
    public void Emit_WithGlobalRules_RewritesStringPropertiesAndForwards()
    {
        var rule = PiiRedactionRule.Create(null, "email", PiiRulePatternType.Preset, null, PiiPresetPattern.Email, PiiRuleAction.Redact, PiiRuleScope.Global, [], 100, null, null);
        var provider = new SnapshotProvider(PiiRedactionRuleProvider.Compile([rule]));
        using var capture = new CapturingSink();
        var sink = CreateSink(provider, capture);

        sink.Emit(MakeEvent());

        var forwarded = capture.Events.Single();
        ((ScalarValue)forwarded.Properties["ContactEmail"]).Value.Should().Be("[PII-REDACTED]");
        ((ScalarValue)forwarded.Properties["Note"]).Value.Should().Be("plain note");
        ((ScalarValue)forwarded.Properties["Application"]).Value.Should().Be("GameGuild.API");
        ((ScalarValue)forwarded.Properties["PiiRedaction"]).Value.Should().Be("applied");
    }

    [Fact]
    public void Emit_WithoutSnapshot_FailsClosedOnNonStructuralStrings()
    {
        var provider = new SnapshotProvider(null);
        using var capture = new CapturingSink();
        var sink = CreateSink(provider, capture);

        sink.Emit(MakeEvent());

        var forwarded = capture.Events.Single();
        ((ScalarValue)forwarded.Properties["ContactEmail"]).Value.Should().Be(PiiRedactionFailClosed.Marker);
        ((ScalarValue)forwarded.Properties["Note"]).Value.Should().Be(PiiRedactionFailClosed.Marker);
        ((ScalarValue)forwarded.Properties["Application"]).Value.Should().Be("GameGuild.API");
        ((ScalarValue)forwarded.Properties["PiiRedaction"]).Value.Should().Be("fail-closed");
    }

    private sealed class SnapshotProvider(PiiRuleSet? snapshot) : IPiiRedactionRuleProvider
    {
        public Task<PiiRuleSet> GetTenantRuleSetAsync(Guid? tenantId, CancellationToken cancellationToken) =>
            snapshot is null ? throw new PiiRedactionUnavailableException(new InvalidOperationException()) : Task.FromResult(snapshot);

        public Task<PiiRuleSet> GetGlobalRuleSetAsync(CancellationToken cancellationToken) =>
            snapshot is null ? throw new PiiRedactionUnavailableException(new InvalidOperationException()) : Task.FromResult(snapshot);

        public PiiRuleSet? TryGetGlobalSnapshot() => snapshot;

        public PiiRedactionCacheStatus GetCacheStatus() => new(snapshot is not null, snapshot?.LoadedAt, 1, snapshot?.Rules.Count ?? 0, null, null);

        public void Invalidate(Guid? tenantId) { }
    }

    private sealed class CapturingSink : ILogEventSink, IDisposable
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);

        public void Dispose() { }
    }
}

#endregion
