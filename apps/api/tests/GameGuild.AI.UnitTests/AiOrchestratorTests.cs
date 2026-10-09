using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using GameGuild.Identity.Tenants;
using GameGuild.Resources;
using Xunit;

namespace GameGuild.AI.UnitTests;

public class AiOrchestratorTests
{
    [Fact]
    public async Task GenerateAsync_RecordsTerminalExecutionForProductBillingAttribution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, userId);
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "history": { "enabled": true }
                    }
                  }
                }
                """
            });
        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();
        var openAiAdapter = CreateAdapter(
            AiProvider.OpenAi,
            expectedApiKey: "platform-openai-key",
            responseModel: "gpt-4.1-mini");
        var billingRecorder = new SpyAiExecutionBillingRecorder();
        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            },
            billingRecorder);

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "Write a title", null, null));

        result.IsSuccess.Should().BeTrue();
        billingRecorder.Records.Should().ContainSingle();
        var record = billingRecorder.Records[0];
        record.TenantId.Should().Be(tenantId);
        record.ActorId.Should().Be(userId);
        record.Provider.Should().Be("OpenAi");
        record.Model.Should().Be("gpt-4.1-mini");
        record.Outcome.Should().Be("Completed");
        record.TotalTokens.Should().Be(15);
    }

    [Fact]
    public async Task GenerateAsync_RecordsBlockedExecutionForProductBillingAttribution()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, userId);
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "moderation": { "enabled": true, "blockedTerms": ["forbidden"] },
                      "history": { "enabled": true }
                    }
                  }
                }
                """
            });
        var billingRecorder = new SpyAiExecutionBillingRecorder();
        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            CreateQuotaEnforcer().Object,
            CreateHistoryRepository().Object,
            new[] { CreateAdapter(AiProvider.OpenAi, failIfCalled: true).Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            },
            billingRecorder);

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "forbidden topic", null, null));

        result.IsFailure.Should().BeTrue();
        billingRecorder.Records.Should().ContainSingle();
        billingRecorder.Records[0].Outcome.Should().Be("ModerationBlocked");
        billingRecorder.Records[0].OutcomeCode.Should().Be("AI.ModerationBlockedTerm");
    }

    [Fact]
    public async Task GenerateAsync_UsesTenantDefaultProviderAndTenantApiKey_WhenAvailable()
    {
        var tenantId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, Guid.NewGuid());
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "Anthropic"
                    }
                  },
                  "apiKeys": {
                    "ai:anthropic": "tenant-claude-key"
                  }
                }
                """
            });

        var openAiAdapter = CreateAdapter(AiProvider.OpenAi, failIfCalled: true);
        var anthropicAdapter = CreateAdapter(
            AiProvider.Anthropic,
            expectedApiKey: "tenant-claude-key",
            responseModel: "claude-3-5-sonnet-latest");
        var googleAdapter = CreateAdapter(AiProvider.Google, failIfCalled: true);
        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();

        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object, anthropicAdapter.Object, googleAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                AllowTenantOverrides = true,
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai", DefaultModel = "gpt-4.1-mini" },
                    ["Anthropic"] = new() { ApiKey = "platform-claude", DefaultModel = "claude-3-5-sonnet-latest" },
                    ["Google"] = new() { ApiKey = "platform-google", DefaultModel = "gemini-2.0-flash" },
                }
            });

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "Summarize this", null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be("Anthropic");
        anthropicAdapter.Verify(adapter => adapter.CompleteAsync(It.IsAny<AiResolvedRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_RejectsProviderOverride_WhenTenantDisallowsOverride()
    {
        var tenantId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, Guid.NewGuid());
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "allowRequestOverride": false
                    }
                  }
                }
                """
            });

        var openAiAdapter = CreateAdapter(AiProvider.OpenAi, failIfCalled: true);
        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();
        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                AllowTenantOverrides = true,
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai", DefaultModel = "gpt-4.1-mini" },
                }
            });

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest("Google", null, null, "Summarize this", null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI.ProviderOverrideForbidden");
        openAiAdapter.Verify(adapter => adapter.CompleteAsync(It.IsAny<AiResolvedRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_FallsBackToPlatformApiKey_WhenTenantKeyIsMissing()
    {
        var tenantId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, Guid.NewGuid());
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "providers": {
                        "OpenAi": {
                          "enabled": true,
                          "defaultModel": "gpt-4.1-mini"
                        }
                      }
                    }
                  }
                }
                """
            });

        var openAiAdapter = CreateAdapter(
            AiProvider.OpenAi,
            expectedApiKey: "platform-openai-key",
            responseModel: "gpt-4.1-mini");
        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();

        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                AllowTenantOverrides = true,
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            });

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "Write a title", null, null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Provider.Should().Be("OpenAi");
        openAiAdapter.Verify(adapter => adapter.CompleteAsync(It.IsAny<AiResolvedRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_BlocksPrompt_WhenTenantModerationContainsBlockedTerm()
    {
        var tenantId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, Guid.NewGuid());
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "moderation": {
                        "enabled": true,
                        "blockedTerms": ["forbidden phrase"]
                      }
                    }
                  }
                }
                """
            });

        var openAiAdapter = CreateAdapter(AiProvider.OpenAi, failIfCalled: true);
        var historyRepository = CreateHistoryRepository();

        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            CreateQuotaEnforcer().Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            });

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "This contains a forbidden phrase.", null, null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI.ModerationBlockedTerm");
        historyRepository.Verify(repository => repository.AddAsync(
            It.Is<AiConversationLog>(entry => entry.Outcome == "ModerationBlocked"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ConsumesQuotasAndPersistsHistory_WhenSuccessful()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, userId);
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "history": {
                        "enabled": true
                      }
                    }
                  }
                }
                """
            });

        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();
        var openAiAdapter = CreateAdapter(
            AiProvider.OpenAi,
            expectedApiKey: "platform-openai-key",
            responseModel: "gpt-4.1-mini");

        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            });

        var result = await orchestrator.GenerateAsync(new AiGenerateRequest(null, null, null, "Write a title", null, null));

        result.IsSuccess.Should().BeTrue();
        quotaEnforcer.Verify(service => service.TryAtomicConsumeAsync(tenantId, ResourceUsageType.AiRequests, 1, It.IsAny<CancellationToken>()), Times.Once);
        quotaEnforcer.Verify(service => service.TryAtomicConsumeAsync(tenantId, ResourceUsageType.AiTokens, 15, It.IsAny<CancellationToken>()), Times.Once);
        historyRepository.Verify(repository => repository.AddAsync(
            It.Is<AiConversationLog>(entry => entry.UserId == userId && entry.Outcome == "Completed" && entry.TotalTokens == 15),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateForActorStreamingWithReservedQuota_DoesNotConsumeQuotaTwice()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var requestContextAccessor = CreateRequestContextAccessor(tenantId, userId);
        var tenantSettingsRepository = new Mock<ITenantSettingsRepository>();
        tenantSettingsRepository
            .Setup(repository => repository.GetByTenantIdAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantSettings
            {
                TenantId = tenantId,
                IntegrationSettingsJson = """
                {
                  "externalServices": {
                    "ai": {
                      "enabled": true,
                      "defaultProvider": "OpenAi",
                      "history": { "enabled": true }
                    }
                  }
                }
                """
            });
        var quotaEnforcer = CreateQuotaEnforcer();
        var historyRepository = CreateHistoryRepository();
        var openAiAdapter = CreateAdapter(
            AiProvider.OpenAi,
            expectedApiKey: "platform-openai-key",
            responseModel: "gpt-4.1-mini");
        var orchestrator = CreateOrchestrator(
            requestContextAccessor,
            tenantSettingsRepository.Object,
            quotaEnforcer.Object,
            historyRepository.Object,
            new[] { openAiAdapter.Object },
            new AiOptions
            {
                Enabled = true,
                DefaultProvider = "OpenAi",
                Providers = new Dictionary<string, AiProviderOptions>
                {
                    ["OpenAi"] = new() { ApiKey = "platform-openai-key", DefaultModel = "gpt-4.1-mini" },
                }
            });

        var deltas = new List<string>();
        var result = await orchestrator.GenerateForActorStreamingWithReservedQuotaAsync(
            new AiExecutionActor(tenantId, userId),
            new AiGenerateRequest(null, null, null, "Write a title", null, null),
            (delta, _) =>
            {
                deltas.Add(delta);
                return ValueTask.CompletedTask;
            });

        result.IsSuccess.Should().BeTrue();
        deltas.Should().NotBeEmpty();
        quotaEnforcer.Verify(service => service.TryAtomicConsumeAsync(
            It.IsAny<Guid>(),
            It.IsAny<ResourceUsageType>(),
            It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
        historyRepository.Verify(repository => repository.AddAsync(
            It.Is<AiConversationLog>(entry => entry.UserId == userId && entry.Outcome == "Completed"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static AiOrchestrator CreateOrchestrator(
        IRequestContextAccessor requestContextAccessor,
        ITenantSettingsRepository tenantSettingsRepository,
        IResourceQuotaEnforcer quotaEnforcer,
        IAiConversationHistoryRepository historyRepository,
        IEnumerable<IAiProviderAdapter> adapters,
        AiOptions options,
        IAiExecutionBillingRecorder? billingRecorder = null)
    {
        return new AiOrchestrator(
            adapters,
            requestContextAccessor,
            tenantSettingsRepository,
            quotaEnforcer,
            historyRepository,
            Options.Create(options),
            NullLogger<AiOrchestrator>.Instance,
            billingRecorder);
    }

    private static Mock<IResourceQuotaEnforcer> CreateQuotaEnforcer()
    {
        var quotaEnforcer = new Mock<IResourceQuotaEnforcer>();
        quotaEnforcer
            .Setup(service => service.TryAtomicConsumeAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((true, 0L, 1000L));
        quotaEnforcer
            .Setup(service => service.DecrementUsageAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<long>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        quotaEnforcer
            .Setup(service => service.TryConsumeResourceAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<long>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceLimitCheckResponse { CanProceed = true });
        quotaEnforcer
            .Setup(service => service.CheckLimitsAsync(It.IsAny<Guid>(), It.IsAny<ResourceUsageType>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceLimitCheckResponse { CanProceed = true });
        quotaEnforcer
            .Setup(service => service.CheckMultipleLimitsAsync(It.IsAny<Guid>(), It.IsAny<Dictionary<ResourceUsageType, long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<ResourceUsageType, ResourceLimitCheckResponse>());
        return quotaEnforcer;
    }

    private static Mock<IAiConversationHistoryRepository> CreateHistoryRepository()
    {
        var historyRepository = new Mock<IAiConversationHistoryRepository>();
        historyRepository
            .Setup(repository => repository.AddAsync(It.IsAny<AiConversationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        historyRepository
            .Setup(repository => repository.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<AiConversationHistoryEntryDto>());
        return historyRepository;
    }

    private sealed class SpyAiExecutionBillingRecorder : IAiExecutionBillingRecorder
    {
        public List<AiExecutionBillingRecord> Records { get; } = [];

        public ValueTask RecordExecutionAsync(AiExecutionBillingRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return ValueTask.CompletedTask;
        }
    }

    private static IRequestContextAccessor CreateRequestContextAccessor(Guid tenantId, Guid userId)
    {
        var requestContextAccessor = new Mock<IRequestContextAccessor>();
        requestContextAccessor.SetupGet(accessor => accessor.CurrentTenantId).Returns(tenantId);
        requestContextAccessor.SetupGet(accessor => accessor.CurrentUserId).Returns(userId);
        requestContextAccessor.SetupGet(accessor => accessor.IsAuthenticated).Returns(true);
        requestContextAccessor.SetupGet(accessor => accessor.HasTenantContext).Returns(true);
        return requestContextAccessor.Object;
    }

    private static Mock<IAiProviderAdapter> CreateAdapter(
        AiProvider provider,
        string? expectedApiKey = null,
        string? responseModel = null,
        bool failIfCalled = false)
    {
        var adapter = new Mock<IAiProviderAdapter>();
        adapter.SetupGet(instance => instance.Provider).Returns(provider);

        if (failIfCalled)
        {
            adapter
                .Setup(instance => instance.CompleteAsync(It.IsAny<AiResolvedRequest>(), It.IsAny<CancellationToken>()))
                .Throws(new Xunit.Sdk.XunitException($"Provider {provider} should not be called."));

            return adapter;
        }

        adapter
            .Setup(instance => instance.CompleteAsync(It.IsAny<AiResolvedRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AiResolvedRequest request, CancellationToken _) =>
            {
                if (expectedApiKey is not null)
                {
                    request.ApiKey.Should().Be(expectedApiKey);
                }

                return Result.Success(new AiProviderExecutionResult(
                    responseModel ?? request.Model,
                    "ok",
                    "stop",
                    10,
                    5,
                    15));
            });
        adapter
            .Setup(instance => instance.CompleteStreamingAsync(
                It.IsAny<AiResolvedRequest>(),
                It.IsAny<Func<string, CancellationToken, ValueTask>>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (AiResolvedRequest request, Func<string, CancellationToken, ValueTask> onDelta, CancellationToken token) =>
            {
                if (expectedApiKey is not null)
                {
                    request.ApiKey.Should().Be(expectedApiKey);
                }

                await onDelta("ok", token);
                return Result.Success(new AiProviderExecutionResult(
                    responseModel ?? request.Model,
                    "ok",
                    "stop",
                    10,
                    5,
                    15));
            });

        return adapter;
    }
}
