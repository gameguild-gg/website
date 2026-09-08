using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GameGuild.Identity.Authorization;
using GameGuild.Identity.Context.Actors;
using GameGuild.Identity.Tenants;
using GameGuild.Resources;
using Moq;
using Xunit;

namespace GameGuild.AI.UnitTests;

public sealed class AiCoverageCompletionTests
{
    [Fact]
    public void AiModuleAndServiceCollection_ShouldRegisterAiServices()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Enabled"] = "true",
                ["AI:DefaultProvider"] = "OpenAi"
            })
            .Build();
        var module = new AiModule();
        var endpoints = Mock.Of<IEndpointRouteBuilder>();

        module.Name.Should().Be("AI");
        module.Order.Should().Be(120);
        module.MapEndpoints(endpoints).Should().BeSameAs(endpoints);
        module.ConfigureServices(services, configuration).Should().BeSameAs(services);

        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiOrchestrator) && descriptor.ImplementationType == typeof(AiOrchestrator));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiPromptTemplateService) && descriptor.ImplementationType == typeof(AiPromptTemplateService));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiConversationHistoryReader) && descriptor.ImplementationType == typeof(AiConversationHistoryRepository));
        services.Should().Contain(descriptor => descriptor.ServiceType == typeof(IAiConversationHistoryRepository) && descriptor.ImplementationType == typeof(AiConversationHistoryRepository));
        services.Where(descriptor => descriptor.ServiceType == typeof(IAiProviderAdapter))
            .Select(descriptor => descriptor.ImplementationType)
            .Should().BeEquivalentTo(new[] { typeof(OpenAiAdapter), typeof(AnthropicAdapter), typeof(GoogleAiAdapter) });
    }

    [Fact]
    public void AiContractsAndProviderParser_ShouldCoverAliasesDefaultsAndRecords()
    {
        var chat = new AiChatRequest("openai", "gpt", "system", [new AiChatMessage("user", "hello")], 0.2, 32);
        var generated = new AiGeneratedContentRequest(AiGeneratedContentKind.Report, "subject", "context", "audience", "tone", "openai", "gpt", 100);
        var quota = new AiQuotaStatusDto("AiTokens", 10, 20, 30, 20, 33.3, "Monthly", true, DateTime.UtcNow, DateTime.UtcNow.AddDays(1));
        var quotaResponse = new AiQuotaStatusResponse(Guid.NewGuid(), [quota], DateTime.UtcNow);
        var history = new AiConversationHistoryEntryDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Generate",
            "OpenAi",
            "gpt",
            "request",
            "system",
            "response",
            "Completed",
            "OK",
            "reason",
            "stop",
            new AiUsageDto(1, 2, 3),
            DateTime.UtcNow);
        var update = new UpdateAiPromptTemplateRequest("Name", "Prompt", "Description", "Category", "System", true);
        var render = new AiPromptTemplateRenderRequest(new Dictionary<string, string?> { ["name"] = "value" });
        var generate = new AiPromptTemplateGenerateRequest(new Dictionary<string, string?> { ["name"] = "value" }, "openai", "gpt", 0.3, 50);

        chat.Messages.Should().ContainSingle();
        generated.Kind.Should().Be(AiGeneratedContentKind.Report);
        quotaResponse.Quotas.Should().ContainSingle().Which.Should().Be(quota);
        history.Usage.TotalTokens.Should().Be(3);
        update.IsActive.Should().BeTrue();
        render.Variables.Should().ContainKey("name");
        generate.MaxTokens.Should().Be(50);

        foreach (var alias in new[] { "openai", "open-ai", "gpt", "anthropic", "claude", "google", "gemini", "googleai" })
        {
            AiProviderParser.TryParse(alias, out _).Should().BeTrue();
        }

        AiProviderParser.TryParse(null, out var nullProvider).Should().BeFalse();
        nullProvider.Should().Be(default);
        AiProviderParser.TryParse("unknown", out _).Should().BeFalse();
        AiProviderParser.ToResponseValue(AiProvider.OpenAi).Should().Be("OpenAi");
        AiProviderParser.ToResponseValue(AiProvider.Anthropic).Should().Be("Anthropic");
        AiProviderParser.ToResponseValue(AiProvider.Google).Should().Be("Google");
        AiProviderParser.ToResponseValue((AiProvider)999).Should().Be("999");
        AiProviderParser.ToApiKeySetting(AiProvider.OpenAi).Should().Be("ai:openai");
        AiProviderParser.ToApiKeySetting(AiProvider.Anthropic).Should().Be("ai:anthropic");
        AiProviderParser.ToApiKeySetting(AiProvider.Google).Should().Be("ai:google");
        FluentActions.Invoking(() => AiProviderParser.ToApiKeySetting((AiProvider)999)).Should().Throw<ArgumentOutOfRangeException>();
        AiProviderParser.GetDefaultBaseUrl(AiProvider.OpenAi).Should().Contain("openai");
        AiProviderParser.GetDefaultBaseUrl(AiProvider.Anthropic).Should().Contain("anthropic");
        AiProviderParser.GetDefaultBaseUrl(AiProvider.Google).Should().Contain("googleapis");
        FluentActions.Invoking(() => AiProviderParser.GetDefaultBaseUrl((AiProvider)999)).Should().Throw<ArgumentOutOfRangeException>();

        AiProviderParser.ToJsonElement(null).Should().BeNull();
        using var document = JsonDocument.Parse("""{"provider":"OpenAi"}""");
        AiProviderParser.ToJsonElement(document.RootElement)!.Value.GetProperty("provider").GetString().Should().Be("OpenAi");
        AiProviderParser.ToJsonElement(new { provider = "Google" })!.Value.GetProperty("provider").GetString().Should().Be("Google");
    }

    [Fact]
    public void AiJsonHelpers_ShouldCoverFallbacksAndPartExtraction()
    {
        using var document = JsonDocument.Parse("""
        {
          "text": "value",
          "flag": true,
          "badFlag": "true",
          "intValue": 7,
          "badInt": 999999999999,
          "strings": [" a ", "A", null, 5, "b"],
          "notArray": "x",
          "parts": [{"text":"first"}, {"text":" "}, {"other":"x"}, 5, {"text":"second"}]
        }
        """);
        var root = document.RootElement;

        AiJsonHelpers.TryGetString(root, "text", out var text).Should().BeTrue();
        text.Should().Be("value");
        AiJsonHelpers.TryGetString(root, "missing", out _).Should().BeFalse();
        AiJsonHelpers.TryGetBoolean(root, "flag", out var flag).Should().BeTrue();
        flag.Should().BeTrue();
        AiJsonHelpers.TryGetBoolean(root, "badFlag", out _).Should().BeFalse();
        AiJsonHelpers.TryGetInt(root, "intValue").Should().Be(7);
        AiJsonHelpers.TryGetInt(root, "badInt").Should().BeNull();
        AiJsonHelpers.TryGetInt(default, "intValue").Should().BeNull();
        AiJsonHelpers.TryGetStringArray(root, "strings").Should().Equal("a", "b");
        AiJsonHelpers.TryGetStringArray(root, "notArray").Should().BeEmpty();
        AiJsonHelpers.TryGetStringArray(default, "strings").Should().BeEmpty();
        AiJsonHelpers.ExtractTextFromParts(root.GetProperty("parts")).Should().Be("first\nsecond");
        AiJsonHelpers.ExtractTextFromParts(root.GetProperty("text")).Should().BeNull();
    }

    [Fact]
    public void AiProviderErrorMapper_ShouldMapStatusesAndBodyShapes()
    {
        AiProviderErrorMapper.Map("OpenAI", HttpStatusCode.Unauthorized, "body").Code.Should().Be("AI.ProviderUnauthorized");
        AiProviderErrorMapper.Map("OpenAI", HttpStatusCode.Forbidden, "body").Code.Should().Be("AI.ProviderForbidden");
        AiProviderErrorMapper.Map("OpenAI", (HttpStatusCode)429, "body").Code.Should().Be("AI.ProviderRateLimited");
        AiProviderErrorMapper.Map("OpenAI", HttpStatusCode.BadRequest, "bad").Code.Should().Be("AI.ProviderBadRequest");
        AiProviderErrorMapper.Map("OpenAI", HttpStatusCode.NotFound, "missing").Code.Should().Be("AI.ProviderEndpointNotFound");
        AiProviderErrorMapper.Map("OpenAI", HttpStatusCode.InternalServerError, new string('x', 401)).Code.Should().Be("AI.ProviderFailure");

        var emptyBody = AiProviderErrorMapper.Map("OpenAI", (HttpStatusCode)418, " ");
        emptyBody.Code.Should().Be("AI.ProviderRequestFailed");
        emptyBody.Description.Should().Be("OpenAI returned 418.");
    }

    [Fact]
    public void AiControllerPrivateHelpers_ShouldCoverGeneratedContentProviderAndCsvBranches()
    {
        var controller = CreateAiController(aiOptions: new AiOptions
        {
            Enabled = true,
            DefaultProvider = "OpenAi",
            AllowTenantOverrides = true,
            Providers = new Dictionary<string, AiProviderOptions>
            {
                ["OpenAi"] = new() { ApiKey = "openai-key", DefaultModel = "gpt", BaseUrl = " https://openai.test " },
                ["claude"] = new() { ApiKey = "claude-key", DefaultModel = "claude" },
                ["unknown-provider"] = new() { ApiKey = "ignored" }
            }
        });

        InvokePrivateInstance<AiProviderOptions?>(controller, "ResolvePlatformProviderOptions", AiProvider.OpenAi)!.DefaultModel.Should().Be("gpt");
        InvokePrivateInstance<AiProviderOptions?>(controller, "ResolvePlatformProviderOptions", AiProvider.Anthropic)!.DefaultModel.Should().Be("claude");
        InvokePrivateInstance<AiProviderOptions?>(controller, "ResolvePlatformProviderOptions", AiProvider.Google).Should().BeNull();

        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentSystemPrompt", AiGeneratedContentKind.Email).Should().Contain("emails");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentSystemPrompt", AiGeneratedContentKind.Report).Should().Contain("reports");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentSystemPrompt", AiGeneratedContentKind.ListingDescription).Should().Contain("listing");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentSystemPrompt", (AiGeneratedContentKind)999).Should().Contain("business content");

        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentPrompt", AiGeneratedContentKind.Email, " Subject ", " Context ", null, " ")
            .Should().Contain("Create an email.");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentPrompt", AiGeneratedContentKind.Report, "Subject", "Context", " executives ", " concise ")
            .Should().Contain("Create a report.");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentPrompt", AiGeneratedContentKind.ListingDescription, "Subject", "Context", "buyers", "polished")
            .Should().Contain("Create a listing description.");
        InvokePrivateStatic<string>(typeof(AiController), "BuildGeneratedContentPrompt", (AiGeneratedContentKind)999, "Subject", "Context", null, null)
            .Should().Contain("Create business content.");

        InvokePrivateStatic<int>(typeof(AiController), "DefaultMaxTokens", AiGeneratedContentKind.Email).Should().Be(700);
        InvokePrivateStatic<int>(typeof(AiController), "DefaultMaxTokens", AiGeneratedContentKind.Report).Should().Be(1400);
        InvokePrivateStatic<int>(typeof(AiController), "DefaultMaxTokens", AiGeneratedContentKind.ListingDescription).Should().Be(800);
        InvokePrivateStatic<int>(typeof(AiController), "DefaultMaxTokens", (AiGeneratedContentKind)999).Should().Be(900);

        var csv = InvokePrivateStatic<string>(typeof(AiController), "BuildHistoryCsv", (object)CreateHistoryEntries());
        csv.Should().Contain("\"chat,kind\"");
        csv.Should().Contain("\"Open\"\"Ai\"");
        csv.Should().Contain("\"line\nmodel\"");
        csv.Should().Contain("\"carriage\ronly\"");
        csv.Should().Contain("OK");

        InvokePrivateStatic<string>(typeof(AiController), "BuildHistoryCsv", (object)Array.Empty<AiConversationHistoryEntryDto>())
            .Should().StartWith("id,userId,requestKind");
    }

    [Fact]
    public async Task AiControllerEndpoints_ShouldExportHistoryAndBuildQuotas()
    {
        var tenantId = Guid.NewGuid();
        var historyReader = new Mock<IAiConversationHistoryReader>();
        historyReader.Setup(reader => reader.GetRecentAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHistoryEntries());
        var quotaReader = new Mock<IResourceQuotaReader>();
        quotaReader.Setup(reader => reader.GetQuotaAsync(tenantId, ResourceUsageType.AiRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResourceQuota
            {
                TenantId = tenantId,
                Type = ResourceUsageType.AiRequests,
                CurrentUsage = 25,
                SoftLimit = 80,
                HardLimit = 100,
                Period = ResourceQuotaPeriod.Monthly,
                LastReset = DateTime.UtcNow
            });
        quotaReader.Setup(reader => reader.GetQuotaAsync(tenantId, ResourceUsageType.AiTokens, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceQuota?)null);
        quotaReader.Setup(reader => reader.GetCurrentUsageAsync(tenantId, ResourceUsageType.AiTokens, It.IsAny<CancellationToken>()))
            .ReturnsAsync(33);
        var controller = CreateAiController(
            historyReader: historyReader.Object,
            quotaReader: quotaReader.Object,
            currentTenantId: tenantId);

        var history = await controller.History(600, CancellationToken.None);
        var csv = await controller.ExportHistory("csv", 600, CancellationToken.None);
        var json = await controller.ExportHistory("json", 0, CancellationToken.None);
        var quotas = await controller.Quotas(CancellationToken.None);
        var forbidden = await CreateAiController(hasTenantContext: false).ExportHistory("csv", 100, CancellationToken.None);

        history.Result.Should().BeOfType<OkObjectResult>();
        csv.Should().BeOfType<FileContentResult>().Which.ContentType.Should().Be("text/csv");
        json.Should().BeOfType<FileContentResult>().Which.ContentType.Should().Be("application/json");
        quotas.Result.Should().BeOfType<OkObjectResult>();
        forbidden.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public void TenantAiControllerPrivateHelpers_ShouldCoverCsvEscaping()
    {
        var controller = CreateTenantAiController(ActorContext.Anonymous);

        var csv = InvokePrivateStatic<string>(typeof(TenantAiController), "BuildHistoryCsv", (object)CreateHistoryEntries());

        csv.Should().Contain("\"chat,kind\"");
        csv.Should().Contain("\"Open\"\"Ai\"");
        csv.Should().Contain("\"line\nmodel\"");
        csv.Should().Contain("\"carriage\ronly\"");
        InvokePrivateStatic<string>(typeof(TenantAiController), "BuildHistoryCsv", (object)Array.Empty<AiConversationHistoryEntryDto>())
            .Should().StartWith("id,userId,requestKind");
        controller.Should().NotBeNull();
    }

    [Fact]
    public async Task TenantAiControllerEndpoints_ShouldValidateMembershipAndReturnExports()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var membershipChecker = new Mock<ITenantMembershipChecker>();
        membershipChecker.Setup(checker => checker.IsUserMemberOfTenantAsync(userId, tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var historyReader = new Mock<IAiConversationHistoryReader>();
        historyReader.Setup(reader => reader.GetRecentAsync(tenantId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateHistoryEntries());
        var quotaReader = new Mock<IResourceQuotaReader>();
        quotaReader.Setup(reader => reader.GetQuotaAsync(tenantId, It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ResourceQuota?)null);
        quotaReader.Setup(reader => reader.GetCurrentUsageAsync(tenantId, It.IsAny<ResourceUsageType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);
        var memberController = CreateTenantAiController(
            CreateActor(userId, null, "Member"),
            historyReader.Object,
            quotaReader.Object,
            membershipChecker.Object);
        var adminController = CreateTenantAiController(
            CreateActor(Guid.NewGuid(), null, "SystemAdmin"),
            historyReader.Object,
            quotaReader.Object,
            membershipChecker.Object);
        var sameTenantController = CreateTenantAiController(
            CreateActor(Guid.NewGuid(), tenantId, "Member"),
            historyReader.Object,
            quotaReader.Object,
            membershipChecker.Object);

        var forbidden = await CreateTenantAiController(ActorContext.Anonymous).GetHistory(tenantId, 20, CancellationToken.None);
        var memberHistory = await memberController.GetHistory(tenantId, 0, CancellationToken.None);
        var adminJson = await adminController.ExportHistory(tenantId, "json", 2000, CancellationToken.None);
        var sameTenantCsv = await sameTenantController.ExportHistory(tenantId, "csv", 1, CancellationToken.None);
        var quotas = await memberController.GetQuotas(tenantId, CancellationToken.None);

        forbidden.Result.Should().BeOfType<ForbidResult>();
        memberHistory.Result.Should().BeOfType<OkObjectResult>();
        adminJson.Should().BeOfType<FileContentResult>().Which.ContentType.Should().Be("application/json");
        sameTenantCsv.Should().BeOfType<FileContentResult>().Which.ContentType.Should().Be("text/csv");
        quotas.Result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void AiPromptTemplatesController_ShouldConstruct()
    {
        var controller = new AiPromptTemplatesController(
            new CommandHandlerSender(Mock.Of<IAiPromptTemplateService>(), Mock.Of<IAiOrchestrator>()),
            Mock.Of<IAiPromptTemplateService>(),
            Mock.Of<IRequestContextAccessor>());

        controller.Should().NotBeNull();
    }

    [Fact]
    public async Task AiPromptTemplateService_ShouldCoverValidationAndRenderingFallbacks()
    {
        await using var db = CreateDbContext();
        var service = new AiPromptTemplateService(db);
        var tenantId = Guid.NewGuid();

        (await service.CreateAsync(tenantId, null, new CreateAiPromptTemplateRequest(" ", "Name", "Prompt")))
            .Error.Code.Should().Be("AI.PromptTemplateKeyRequired");
        (await service.CreateAsync(tenantId, null, new CreateAiPromptTemplateRequest("Key", " ", "Prompt")))
            .Error.Code.Should().Be("AI.PromptTemplateNameRequired");
        (await service.CreateAsync(tenantId, null, new CreateAiPromptTemplateRequest("Key", "Name", " ")))
            .Error.Code.Should().Be("AI.PromptTemplatePromptRequired");
        (await service.CreateAsync(tenantId, null, new CreateAiPromptTemplateRequest("!!!", "Name", "Prompt")))
            .Error.Code.Should().Be("AI.PromptTemplateKeyInvalid");

        var longKey = new string('A', 150);
        var created = await service.CreateAsync(
            tenantId,
            null,
            new CreateAiPromptTemplateRequest(longKey, "Name", "Hello {{missing}} {{name}}", Category: " ", SystemPrompt: " "),
            CancellationToken.None);
        var rendered = await service.RenderAsync(tenantId, created.Value.Id, null, CancellationToken.None);
        var missing = await service.RenderAsync(tenantId, Guid.NewGuid(), null, CancellationToken.None);
        var inactive = new AiPromptTemplate
        {
            TenantId = tenantId,
            Key = "inactive",
            Name = "Inactive",
            Category = "General",
            Prompt = "Prompt",
            IsActive = false
        };
        db.Set<AiPromptTemplate>().Add(inactive);
        await db.SaveChangesAsync();
        var inactiveResult = await service.RenderAsync(tenantId, inactive.Id, null, CancellationToken.None);
        var updateName = await service.UpdateAsync(tenantId, created.Value.Id, null, new UpdateAiPromptTemplateRequest(Name: " "), CancellationToken.None);
        var updatePrompt = await service.UpdateAsync(tenantId, created.Value.Id, null, new UpdateAiPromptTemplateRequest(Prompt: " "), CancellationToken.None);

        created.Value.Key.Length.Should().BeLessThanOrEqualTo(128);
        created.Value.Category.Should().Be("General");
        rendered.Value.SystemPrompt.Should().BeNull();
        rendered.Value.Prompt.Should().Be("Hello {{missing}} {{name}}");
        missing.Error.Code.Should().Be("AI.PromptTemplateNotFound");
        inactiveResult.Error.Code.Should().Be("AI.PromptTemplateInactive");
        updateName.Error.Code.Should().Be("AI.PromptTemplateNameRequired");
        updatePrompt.Error.Code.Should().Be("AI.PromptTemplatePromptRequired");
    }

    [Fact]
    public async Task AiConversationHistoryRepository_ShouldPersistAndProjectEntries()
    {
        await using var db = CreateDbContext();
        var tenantId = Guid.NewGuid();
        var repository = new AiConversationHistoryRepository(db);

        await repository.AddAsync(new AiConversationLog
        {
            TenantId = tenantId,
            UserId = Guid.NewGuid(),
            RequestKind = "Generate",
            Provider = "OpenAi",
            Model = "gpt",
            RequestText = "request",
            SystemPrompt = "system",
            ResponseText = "response",
            Outcome = "Completed",
            OutcomeCode = "OK",
            OutcomeReason = "reason",
            FinishReason = "stop",
            InputTokens = 1,
            OutputTokens = 2,
            TotalTokens = 3,
            OccurredAt = DateTime.UtcNow
        });

        var entries = await repository.GetRecentAsync(tenantId, 500, CancellationToken.None);

        entries.Should().ContainSingle();
        entries[0].Usage.TotalTokens.Should().Be(3);
    }

    [Fact]
    public async Task ProviderAdapters_ShouldNormalizeSuccessfulResponses()
    {
        var openAi = new OpenAiAdapter(
            new SequenceHttpClientFactory(
                JsonResponse((HttpStatusCode)429, """{"error":"retry"}"""),
                JsonResponse(HttpStatusCode.OK, """
                {
                  "model": "gpt-response",
                  "choices": [
                    {
                      "message": { "content": [ { "text": "open" }, { "text": "ai" } ] },
                      "finish_reason": "stop"
                    }
                  ],
                  "usage": { "prompt_tokens": 3, "completion_tokens": 4, "total_tokens": 7 }
                }
                """)),
            NullLogger<OpenAiAdapter>.Instance);
        var anthropicWithUsage = new AnthropicAdapter(
            new SequenceHttpClientFactory(JsonResponse(HttpStatusCode.OK, """
            {
              "model": "claude-response",
              "content": [{ "text": "anthropic" }],
              "stop_reason": "end",
              "usage": { "input_tokens": 5, "output_tokens": 6 }
            }
            """)),
            NullLogger<AnthropicAdapter>.Instance);
        var anthropicWithoutUsage = new AnthropicAdapter(
            new SequenceHttpClientFactory(JsonResponse(HttpStatusCode.OK, """
            {
              "model": "claude-response",
              "content": [{ "type": "text", "text": "anthropic fallback" }],
              "usage": {}
            }
            """)),
            NullLogger<AnthropicAdapter>.Instance);
        var google = new GoogleAiAdapter(
            new SequenceHttpClientFactory(JsonResponse(HttpStatusCode.OK, """
            {
              "candidates": [
                {
                  "content": { "parts": [{ "text": "google" }] },
                  "finishReason": "STOP"
                }
              ],
              "usageMetadata": { "promptTokenCount": 1, "candidatesTokenCount": 2, "totalTokenCount": 3 }
            }
            """)),
            NullLogger<GoogleAiAdapter>.Instance);

        var openResult = await openAi.CompleteAsync(CreateResolvedRequest(systemPrompt: "system", baseUrl: "https://openai.test/"));
        var anthropicResult = await anthropicWithUsage.CompleteAsync(CreateResolvedRequest(AiProvider.Anthropic, baseUrl: "https://anthropic.test/"));
        var anthropicNoUsageResult = await anthropicWithoutUsage.CompleteAsync(CreateResolvedRequest(AiProvider.Anthropic, baseUrl: "https://anthropic.test/"));
        var googleResult = await google.CompleteAsync(CreateResolvedRequest(AiProvider.Google, model: "gemini model", systemPrompt: "system", baseUrl: "https://google.test/"));

        openAi.Provider.Should().Be(AiProvider.OpenAi);
        anthropicWithUsage.Provider.Should().Be(AiProvider.Anthropic);
        google.Provider.Should().Be(AiProvider.Google);
        openResult.Value.Text.Should().Be("open\nai");
        openResult.Value.TotalTokens.Should().Be(7);
        anthropicResult.Value.TotalTokens.Should().Be(11);
        anthropicNoUsageResult.Value.TotalTokens.Should().BeNull();
        googleResult.Value.Text.Should().Be("google");
        googleResult.Value.TotalTokens.Should().Be(3);
    }

    [Fact]
    public void AiOrchestratorPrivateHelpers_ShouldCoverModerationProviderModelAndTenantConfigBranches()
    {
        var orchestrator = CreateOrchestrator(new AiOptions
        {
            Enabled = true,
            DefaultProvider = "bad-provider",
            AllowTenantOverrides = false,
            Providers = new Dictionary<string, AiProviderOptions>
            {
                ["OpenAi"] = new() { ApiKey = "platform-openai", DefaultModel = "platform-chat", BaseUrl = " https://platform.openai " },
                ["gemini"] = new() { ApiKey = "platform-google", DefaultModel = "platform-google" },
                ["unknown-provider"] = new() { ApiKey = "ignored" }
            }
        });
        var enabledModeration = new AiModerationConfiguration(true, [" ", "blocked"], 10, 10);
        var blockedTermModeration = new AiModerationConfiguration(true, [" ", "blocked"], 100, 100);
        var noMatchModeration = new AiModerationConfiguration(true, [" ", "missing"], 100, 100);

        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateInput", CreateResolvedRequest(moderation: new AiModerationConfiguration(false, [], null, null))).IsSuccess.Should().BeTrue();
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateInput", CreateResolvedRequest(systemPrompt: "system", messages: [new AiChatMessage("user", "this prompt is too long")], moderation: enabledModeration))
            .Error.Code.Should().Be("AI.PromptTooLong");
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateInput", CreateResolvedRequest(messages: [new AiChatMessage("user", "contains blocked")], moderation: blockedTermModeration))
            .Error.Code.Should().Be("AI.ModerationBlockedTerm");
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateInput", CreateResolvedRequest(messages: [new AiChatMessage("user", "clean")], moderation: noMatchModeration))
            .IsSuccess.Should().BeTrue();

        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateOutput", CreateResolvedRequest(moderation: new AiModerationConfiguration(false, [], null, null)), new AiProviderExecutionResult("m", "text", null, null, null, null))
            .IsSuccess.Should().BeTrue();
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateOutput", CreateResolvedRequest(moderation: enabledModeration), new AiProviderExecutionResult("m", "this response is too long", null, null, null, null))
            .Error.Code.Should().Be("AI.ResponseTooLong");
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateOutput", CreateResolvedRequest(moderation: blockedTermModeration), new AiProviderExecutionResult("m", "blocked", null, null, null, null))
            .Error.Code.Should().Be("AI.ModerationBlockedTerm");
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateOutput", CreateResolvedRequest(moderation: noMatchModeration), new AiProviderExecutionResult("m", "clean", null, null, null, null))
            .IsSuccess.Should().BeTrue();
        InvokePrivateStatic<Result<bool>>(typeof(AiOrchestrator), "ModerateOutput", CreateResolvedRequest(moderation: new AiModerationConfiguration(true, [], null, null)), new AiProviderExecutionResult("m", "clean", null, null, null, null))
            .IsSuccess.Should().BeTrue();

        InvokePrivateStatic<string>(typeof(AiOrchestrator), "BuildRequestText", new[] { new AiChatMessage("user", " hello "), new AiChatMessage("assistant", " ") }, null)
            .Should().Be("user: hello");

        var tenantConfiguration = new AiTenantConfiguration(
            true,
            true,
            AiProvider.Anthropic,
            "tenant-chat",
            "tenant-generate",
            new Dictionary<AiProvider, AiTenantProviderConfiguration> { [AiProvider.OpenAi] = new(true, "tenant-provider") },
            new Dictionary<AiProvider, string> { [AiProvider.OpenAi] = "tenant-key" },
            new AiModerationConfiguration(false, [], null, null),
            new AiHistoryConfiguration(true));

        InvokePrivateInstance<Result<AiProvider>>(orchestrator, "ResolveProvider", "bad", tenantConfiguration).Error.Code.Should().Be("AI.ProviderInvalid");
        InvokePrivateInstance<Result<AiProvider>>(orchestrator, "ResolveProvider", "OpenAi", tenantConfiguration).Value.Should().Be(AiProvider.OpenAi);
        InvokePrivateInstance<Result<AiProvider>>(orchestrator, "ResolveProvider", null, tenantConfiguration).Value.Should().Be(AiProvider.Anthropic);
        InvokePrivateInstance<Result<AiProvider>>(orchestrator, "ResolveProvider", null, tenantConfiguration with { DefaultProvider = null }).Error.Code.Should().Be("AI.ProviderNotConfigured");
        InvokePrivateInstance<Result<AiProvider>>(CreateOrchestrator(new AiOptions { Enabled = true, DefaultProvider = "Google" }), "ResolveProvider", null, tenantConfiguration with { DefaultProvider = null }).Value.Should().Be(AiProvider.Google);
        InvokePrivateInstance<AiProviderOptions?>(orchestrator, "ResolvePlatformProviderOptions", AiProvider.OpenAi)!.DefaultModel.Should().Be("platform-chat");
        InvokePrivateInstance<AiProviderOptions?>(orchestrator, "ResolvePlatformProviderOptions", AiProvider.Google)!.DefaultModel.Should().Be("platform-google");
        InvokePrivateInstance<AiProviderOptions?>(orchestrator, "ResolvePlatformProviderOptions", AiProvider.Anthropic).Should().BeNull();

        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", " requested ", AiRequestKind.Chat, AiProvider.OpenAi, tenantConfiguration, null).Should().Be(" requested ");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", null, AiRequestKind.Chat, AiProvider.OpenAi, tenantConfiguration, null).Should().Be("tenant-chat");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", null, AiRequestKind.Generate, AiProvider.OpenAi, tenantConfiguration, null).Should().Be("tenant-generate");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", null, AiRequestKind.Chat, AiProvider.OpenAi, tenantConfiguration with { DefaultChatModel = null }, null).Should().Be("tenant-provider");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", null, AiRequestKind.Chat, AiProvider.Google, tenantConfiguration with { DefaultChatModel = null }, new AiProviderOptions { DefaultModel = "platform" }).Should().Be("platform");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveModel", null, AiRequestKind.Chat, AiProvider.Google, tenantConfiguration with { DefaultChatModel = null }, null).Should().BeNull();

        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveApiKey", AiProvider.OpenAi, tenantConfiguration, new AiProviderOptions { ApiKey = "platform" }).Should().Be("tenant-key");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveApiKey", AiProvider.Google, tenantConfiguration with { ApiKeys = new Dictionary<AiProvider, string> { [AiProvider.Google] = " " } }, new AiProviderOptions { ApiKey = "platform" }).Should().Be("platform");
        InvokePrivateStatic<string?>(typeof(AiOrchestrator), "ResolveApiKey", AiProvider.Anthropic, tenantConfiguration, null).Should().BeNull();

        var emptySettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", TenantIntegrationSettingsSerializer.Empty());
        var scalarSettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", new TenantIntegrationSettingsDto(
            new Dictionary<string, object?> { ["ai"] = "enabled" },
            new Dictionary<string, object?>(),
            new Dictionary<string, string>(),
            new Dictionary<string, object?>()));
        var fullSettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", CreateTenantIntegrationSettings());
        var nullApiKeySettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", new TenantIntegrationSettingsDto(
            new Dictionary<string, object?>(),
            new Dictionary<string, object?>(),
            null!,
            new Dictionary<string, object?>()));
        var nullExternalServicesSettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", new TenantIntegrationSettingsDto(
            null!,
            new Dictionary<string, object?>(),
            new Dictionary<string, string>(),
            new Dictionary<string, object?>()));
        var nullAiSettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", new TenantIntegrationSettingsDto(
            new Dictionary<string, object?> { ["ai"] = null },
            new Dictionary<string, object?>(),
            new Dictionary<string, string>(),
            new Dictionary<string, object?>()));
        var nonObjectProviderSettings = InvokePrivateInstance<AiTenantConfiguration>(orchestrator, "ParseTenantConfiguration", new TenantIntegrationSettingsDto(
            new Dictionary<string, object?>
            {
                ["ai"] = new Dictionary<string, object?>
                {
                    ["providers"] = "not-an-object"
                }
            },
            new Dictionary<string, object?>(),
            new Dictionary<string, string>(),
            new Dictionary<string, object?>()));

        emptySettings.Enabled.Should().BeTrue();
        scalarSettings.History.Enabled.Should().BeTrue();
        nullApiKeySettings.ApiKeys.Should().BeEmpty();
        nullExternalServicesSettings.Enabled.Should().BeTrue();
        nullAiSettings.Enabled.Should().BeTrue();
        nonObjectProviderSettings.Providers.Should().BeEmpty();
        fullSettings.Enabled.Should().BeFalse();
        fullSettings.AllowRequestOverride.Should().BeFalse();
        fullSettings.Providers[AiProvider.OpenAi].Enabled.Should().BeFalse();
        fullSettings.Providers[AiProvider.OpenAi].DefaultModel.Should().Be("tenant-openai");
        fullSettings.ApiKeys[AiProvider.OpenAi].Should().Be("tenant-openai-key");
        fullSettings.Moderation.Enabled.Should().BeTrue();
        fullSettings.Moderation.BlockedTerms.Should().Equal("blocked", "other");
        fullSettings.Moderation.MaxPromptCharacters.Should().Be(50);
        fullSettings.Moderation.MaxResponseCharacters.Should().Be(60);
        fullSettings.History.Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task AiOrchestratorChatAsync_ShouldCoverRoleValidationAndStaticInitializer()
    {
        var orchestrator = CreateOrchestrator(new AiOptions { Enabled = false });

        var result = await orchestrator.ChatAsync(new AiChatRequest(
            null,
            null,
            null,
            [new AiChatMessage("system", "hello")],
            null,
            null));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("AI.InvalidMessageRole");
    }

    private static AiController CreateAiController(
        IAiOrchestrator? orchestrator = null,
        IAiConversationHistoryReader? historyReader = null,
        IResourceQuotaReader? quotaReader = null,
        AiOptions? aiOptions = null,
        Guid? currentTenantId = null,
        bool hasTenantContext = true)
    {
        var requestContextAccessor = new Mock<IRequestContextAccessor>();
        requestContextAccessor.SetupGet(accessor => accessor.CurrentTenantId).Returns(hasTenantContext ? currentTenantId ?? Guid.NewGuid() : null);
        requestContextAccessor.SetupGet(accessor => accessor.CurrentUserId).Returns(Guid.NewGuid());

        return new AiController(
            new CommandHandlerSender(orchestrator ?? Mock.Of<IAiOrchestrator>(), Mock.Of<IAiPromptTemplateService>()),
            historyReader ?? Mock.Of<IAiConversationHistoryReader>(),
            requestContextAccessor.Object,
            quotaReader ?? Mock.Of<IResourceQuotaReader>(),
            Options.Create(aiOptions ?? new AiOptions { Enabled = true }));
    }

    private static TenantAiController CreateTenantAiController(
        ActorContext actorContext,
        IAiConversationHistoryReader? historyReader = null,
        IResourceQuotaReader? quotaReader = null,
        ITenantMembershipChecker? membershipChecker = null)
    {
        var actorAccessor = new Mock<IActorContextAccessor>();
        actorAccessor.SetupGet(accessor => accessor.ActorContext).Returns(actorContext);

        return new TenantAiController(
            historyReader ?? Mock.Of<IAiConversationHistoryReader>(),
            quotaReader ?? Mock.Of<IResourceQuotaReader>(),
            actorAccessor.Object,
            membershipChecker ?? Mock.Of<ITenantMembershipChecker>());
    }

    private static ActorContext CreateActor(Guid userId, Guid? tenantId, string role)
        => new()
        {
            ActorKind = ActorKind.User,
            SubjectId = userId.ToString(),
            TenantId = tenantId,
            Roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { role },
            Permissions = new HashSet<string>(),
            IsAuthenticated = true
        };

    private static IReadOnlyList<AiConversationHistoryEntryDto> CreateHistoryEntries()
        =>
        [
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "chat,kind",
                "Open\"Ai",
                "line\nmodel",
                "plain request",
                "system",
                null,
                "carriage\ronly",
                null,
                null,
                "stop",
                new AiUsageDto(1, 2, 3),
                DateTime.UtcNow),
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "Generate",
                "Google",
                "gemini",
                "request",
                null,
                "response",
                "Completed",
                "OK",
                "reason",
                null,
                new AiUsageDto(null, null, null),
                DateTime.UtcNow)
        ];

    private static AiResolvedRequest CreateResolvedRequest(
        AiProvider provider = AiProvider.OpenAi,
        string model = "model",
        string? systemPrompt = null,
        IReadOnlyList<AiChatMessage>? messages = null,
        string apiKey = "api-key",
        string baseUrl = "https://provider.test",
        AiModerationConfiguration? moderation = null,
        AiHistoryConfiguration? history = null)
        => new(
            Guid.NewGuid(),
            AiRequestKind.Generate,
            provider,
            model,
            systemPrompt,
            messages ?? [new AiChatMessage("user", "hello")],
            0.1,
            100,
            apiKey,
            baseUrl,
            moderation ?? new AiModerationConfiguration(false, [], null, null),
            history ?? new AiHistoryConfiguration(true));

    private static AiOrchestrator CreateOrchestrator(AiOptions options)
    {
        var requestContextAccessor = new Mock<IRequestContextAccessor>();
        requestContextAccessor.SetupGet(accessor => accessor.CurrentTenantId).Returns(Guid.NewGuid());
        requestContextAccessor.SetupGet(accessor => accessor.CurrentUserId).Returns(Guid.NewGuid());

        return new AiOrchestrator(
            Array.Empty<IAiProviderAdapter>(),
            requestContextAccessor.Object,
            Mock.Of<ITenantSettingsRepository>(),
            Mock.Of<IResourceQuotaEnforcer>(),
            Mock.Of<IAiConversationHistoryRepository>(),
            Options.Create(options),
            NullLogger<AiOrchestrator>.Instance);
    }

    private static TenantIntegrationSettingsDto CreateTenantIntegrationSettings()
        => new(
            new Dictionary<string, object?>
            {
                ["ai"] = new Dictionary<string, object?>
                {
                    ["enabled"] = false,
                    ["allowRequestOverride"] = false,
                    ["defaultProvider"] = "unknown",
                    ["defaultChatModel"] = "tenant-chat",
                    ["defaultGenerateModel"] = "tenant-generate",
                    ["providers"] = new Dictionary<string, object?>
                    {
                        ["gpt"] = new Dictionary<string, object?>
                        {
                            ["enabled"] = false,
                            ["defaultModel"] = "tenant-openai"
                        },
                        ["unknown"] = new Dictionary<string, object?>(),
                        ["google"] = "not-an-object"
                    },
                    ["moderation"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = true,
                        ["blockedTerms"] = new[] { " blocked ", "BLOCKED", "other" },
                        ["maxPromptCharacters"] = 50,
                        ["maxResponseCharacters"] = 60
                    },
                    ["history"] = new Dictionary<string, object?>
                    {
                        ["enabled"] = false
                    }
                }
            },
            new Dictionary<string, object?>(),
            new Dictionary<string, string>
            {
                ["ai:openai"] = "tenant-openai-key",
                ["ai:google"] = " "
            },
            new Dictionary<string, object?>());

    private static TestAiDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<TestAiDbContext>()
            .UseInMemoryDatabase($"ai-coverage-{Guid.NewGuid()}")
            .Options;

        return new TestAiDbContext(options);
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] arguments)
        => (T)type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments)!;

    private static T InvokePrivateInstance<T>(object instance, string methodName, params object?[] arguments)
        => (T)instance.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(instance, arguments)!;

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string body)
        => new(statusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

    private sealed class SequenceHttpClientFactory(params HttpResponseMessage[] responses) : IHttpClientFactory
    {
        private readonly SequenceHttpHandler _handler = new(responses);

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }

    private sealed class SequenceHttpHandler(IEnumerable<HttpResponseMessage> responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responses.Dequeue());
    }

    private sealed class TestAiDbContext(DbContextOptions<TestAiDbContext> options)
        : DbContext(options), IApplicationDbContext
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            new AiModelConfiguration().Configure(modelBuilder);
        }

        public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
