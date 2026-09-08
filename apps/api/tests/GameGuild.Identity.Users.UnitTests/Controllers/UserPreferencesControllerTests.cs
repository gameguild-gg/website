using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using GameGuild.CQRS;
using Moq;
using System.Text.Json;
using Xunit;

namespace GameGuild.Identity.Users.UnitTests.Controllers;

public class UserPreferencesControllerTests
{
    private readonly Mock<ISender> _sender = new();
    private readonly UserPreferencesController _controller;

    public UserPreferencesControllerTests()
    {
        _controller = new UserPreferencesController(_sender.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    [Fact]
    public async Task PreferencesEndpoints_ShouldMapQueryAndCommands()
    {
        var userId = Guid.NewGuid();
        var preferences = CreatePreferencesDto(userId);
        var updateRequest = new UpdateUserPreferencesRequest(GeneralPreferences: JsonMap(new Dictionary<string, object?> { ["theme"] = "dark" }));
        var replaceRequest = new ReplaceUserPreferencesRequest(
            JsonMap(new Dictionary<string, object?> { ["theme"] = "light" }),
            JsonMap(new Dictionary<string, object?> { ["email"] = true }),
            JsonMap(new Dictionary<string, object?> { ["fontSize"] = "large" }),
            JsonMap(new Dictionary<string, object?> { ["profileVisible"] = false }));

        _sender.Setup(sender => sender.Send(It.Is<GetUserPreferencesQuery>(query => query.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateUserPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, updateRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(
                It.Is<ReplaceUserPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, replaceRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(It.Is<ResetUserPreferencesCommand>(command => command.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var getResult = await _controller.GetPreferences(userId, CancellationToken.None);
        var updateResult = await _controller.UpdatePreferences(userId, updateRequest, CancellationToken.None);
        var replaceResult = await _controller.ReplacePreferences(userId, replaceRequest, CancellationToken.None);
        var resetResult = await _controller.ResetPreferences(userId, CancellationToken.None);

        getResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(preferences);
        updateResult.Should().BeOfType<NoContentResult>();
        replaceResult.Should().BeOfType<NoContentResult>();
        resetResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task GetPreferences_WhenMissing_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        _sender.Setup(sender => sender.Send(It.IsAny<GetUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferencesDto?)null);

        var result = await _controller.GetPreferences(userId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task NotificationEndpoints_ShouldMapQueryAndCommands()
    {
        var userId = Guid.NewGuid();
        var notificationPreferences = JsonMap(new Dictionary<string, object?> { ["email"] = true, ["push"] = false });
        var preferences = CreatePreferencesDto(userId, notificationPreferences: notificationPreferences);
        var updateRequest = new UpdateUserNotificationPreferencesRequest(notificationPreferences);
        var replaceRequest = new ReplaceUserNotificationPreferencesRequest(notificationPreferences);

        _sender.Setup(sender => sender.Send(It.Is<GetUserPreferencesQuery>(query => query.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
        _sender.Setup(sender => sender.Send(
                It.Is<ReplaceUserNotificationPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, replaceRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateUserNotificationPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, updateRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(It.Is<ResetUserNotificationPreferencesCommand>(command => command.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var checkResult = await _controller.CheckNotificationPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetNotificationPreferences(userId, CancellationToken.None);
        var replaceResult = await _controller.ReplaceNotificationPreferences(userId, replaceRequest, CancellationToken.None);
        var updateResult = await _controller.UpdateNotificationPreferences(userId, updateRequest, CancellationToken.None);
        var resetResult = await _controller.ResetNotificationPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<OkResult>();
        getResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(notificationPreferences);
        replaceResult.Should().BeOfType<NoContentResult>();
        updateResult.Should().BeOfType<NoContentResult>();
        resetResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task NotificationEndpoints_WhenMissing_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        _sender.SetupSequence(sender => sender.Send(It.IsAny<GetUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferencesDto?)null)
            .ReturnsAsync(CreatePreferencesDtoWithNullNotification(userId));

        var checkResult = await _controller.CheckNotificationPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetNotificationPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<NotFoundResult>();
        getResult.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task AccessibilityEndpoints_ShouldMapQueryAndCommands()
    {
        var userId = Guid.NewGuid();
        var accessibilityPreferences = JsonMap(new Dictionary<string, object?> { ["highContrast"] = true, ["fontSize"] = 18 });
        var preferences = CreatePreferencesDto(userId, accessibilityPreferences: accessibilityPreferences);
        var updateRequest = new UpdateUserAccessibilityPreferencesRequest(accessibilityPreferences);
        var replaceRequest = new ReplaceUserAccessibilityPreferencesRequest(accessibilityPreferences);

        _sender.Setup(sender => sender.Send(It.Is<GetUserPreferencesQuery>(query => query.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
        _sender.Setup(sender => sender.Send(
                It.Is<ReplaceUserAccessibilityPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, replaceRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateUserAccessibilityPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, updateRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(It.Is<ResetUserAccessibilityPreferencesCommand>(command => command.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var checkResult = await _controller.CheckAccessibilityPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetAccessibilityPreferences(userId, CancellationToken.None);
        var replaceResult = await _controller.ReplaceAccessibilityPreferences(userId, replaceRequest, CancellationToken.None);
        var updateResult = await _controller.UpdateAccessibilityPreferences(userId, updateRequest, CancellationToken.None);
        var resetResult = await _controller.ResetAccessibilityPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<OkResult>();
        getResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(accessibilityPreferences);
        replaceResult.Should().BeOfType<NoContentResult>();
        updateResult.Should().BeOfType<NoContentResult>();
        resetResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AccessibilityEndpoints_WhenMissing_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        _sender.SetupSequence(sender => sender.Send(It.IsAny<GetUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferencesDto?)null)
            .ReturnsAsync(CreatePreferencesDtoWithNullAccessibility(userId));

        var checkResult = await _controller.CheckAccessibilityPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetAccessibilityPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<NotFoundResult>();
        getResult.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task PrivacyEndpoints_ShouldMapQueryAndCommands()
    {
        var userId = Guid.NewGuid();
        var privacyPreferences = JsonMap(new Dictionary<string, object?> { ["profileVisible"] = true, ["analytics"] = false });
        var preferences = CreatePreferencesDto(userId, privacyPreferences: privacyPreferences);
        var updateRequest = new UpdateUserPrivacyPreferencesRequest(privacyPreferences);
        var replaceRequest = new ReplaceUserPrivacyPreferencesRequest(privacyPreferences);

        _sender.Setup(sender => sender.Send(It.Is<GetUserPreferencesQuery>(query => query.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
        _sender.Setup(sender => sender.Send(
                It.Is<ReplaceUserPrivacyPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, replaceRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateUserPrivacyPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, updateRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(It.Is<ResetUserPrivacyPreferencesCommand>(command => command.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var checkResult = await _controller.CheckPrivacyPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetPrivacyPreferences(userId, CancellationToken.None);
        var replaceResult = await _controller.ReplacePrivacyPreferences(userId, replaceRequest, CancellationToken.None);
        var updateResult = await _controller.UpdatePrivacyPreferences(userId, updateRequest, CancellationToken.None);
        var resetResult = await _controller.ResetPrivacyPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<OkResult>();
        getResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(privacyPreferences);
        replaceResult.Should().BeOfType<NoContentResult>();
        updateResult.Should().BeOfType<NoContentResult>();
        resetResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task PrivacyEndpoints_WhenMissing_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        _sender.SetupSequence(sender => sender.Send(It.IsAny<GetUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferencesDto?)null)
            .ReturnsAsync(CreatePreferencesDtoWithNullPrivacy(userId));

        var checkResult = await _controller.CheckPrivacyPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetPrivacyPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<NotFoundResult>();
        getResult.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task LocalizationEndpoints_ShouldMapQueryCommandsAndDto()
    {
        var userId = Guid.NewGuid();
        var localizationPreferences = JsonMap(new Dictionary<string, object?>
        {
            ["Language"] = "pt-BR",
            ["Timezone"] = "America/Sao_Paulo",
            ["DateFormat"] = "dd/MM/yyyy",
            ["TimeFormat"] = "24h",
            ["Currency"] = "BRL",
            ["NumberFormat"] = new Dictionary<string, object?> { ["DecimalSeparator"] = "," },
            ["CustomSettings"] = new Dictionary<string, object?> { ["Calendar"] = "gregorian" }
        });
        var preferences = CreatePreferencesDto(userId, localizationPreferences: localizationPreferences);
        var updateRequest = new UpdateUserLocalizationPreferencesRequest(localizationPreferences);
        var replaceRequest = new ReplaceUserLocalizationPreferencesRequest(localizationPreferences);

        _sender.Setup(sender => sender.Send(It.Is<GetUserPreferencesQuery>(query => query.UserId == userId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(preferences);
        _sender.Setup(sender => sender.Send(
                It.Is<ReplaceUserLocalizationPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, replaceRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(
                It.Is<UpdateUserLocalizationPreferencesCommand>(command => command.UserId == userId && ReferenceEquals(command.Request, updateRequest)),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _sender.Setup(sender => sender.Send(It.Is<ResetUserLocalizationPreferencesCommand>(command => command.UserId == userId), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var checkResult = await _controller.CheckLocalizationPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetLocalizationPreferences(userId, CancellationToken.None);
        var replaceResult = await _controller.ReplaceLocalizationPreferences(userId, replaceRequest, CancellationToken.None);
        var updateResult = await _controller.UpdateLocalizationPreferences(userId, updateRequest, CancellationToken.None);
        var resetResult = await _controller.ResetLocalizationPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<OkResult>();
        var dto = getResult.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<UserLocalizationPreferencesDto>().Subject;
        dto.Language.Should().Be("pt-BR");
        dto.Timezone.Should().Be("America/Sao_Paulo");
        dto.DateFormat.Should().Be("dd/MM/yyyy");
        dto.TimeFormat.Should().Be("24h");
        dto.Currency.Should().Be("BRL");
        dto.NumberFormat.Should().ContainKey("DecimalSeparator");
        dto.CustomSettings.Should().ContainKey("Calendar");
        replaceResult.Should().BeOfType<NoContentResult>();
        updateResult.Should().BeOfType<NoContentResult>();
        resetResult.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task LocalizationEndpoints_WhenMissing_ShouldReturnNotFound()
    {
        var userId = Guid.NewGuid();
        _sender.SetupSequence(sender => sender.Send(It.IsAny<GetUserPreferencesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserPreferencesDto?)null)
            .ReturnsAsync((UserPreferencesDto?)null);

        var checkResult = await _controller.CheckLocalizationPreferences(userId, CancellationToken.None);
        var getResult = await _controller.GetLocalizationPreferences(userId, CancellationToken.None);

        checkResult.Should().BeOfType<NotFoundResult>();
        getResult.Should().BeOfType<NotFoundResult>();
    }

    private static UserPreferencesDto CreatePreferencesDto(
        Guid userId,
        Dictionary<string, JsonElement>? notificationPreferences = null,
        Dictionary<string, JsonElement>? accessibilityPreferences = null,
        Dictionary<string, JsonElement>? privacyPreferences = null,
        Dictionary<string, JsonElement>? localizationPreferences = null)
    {
        return new UserPreferencesDto(
            Guid.NewGuid(),
            userId,
            JsonMap(new Dictionary<string, object?> { ["theme"] = "dark" }),
            notificationPreferences ?? JsonMap(new Dictionary<string, object?> { ["email"] = true }),
            accessibilityPreferences ?? JsonMap(new Dictionary<string, object?> { ["highContrast"] = true }),
            privacyPreferences ?? JsonMap(new Dictionary<string, object?> { ["profileVisible"] = true }),
            localizationPreferences ?? JsonMap(new Dictionary<string, object?> { ["Language"] = "en-US" }),
            DateTimeOffset.UtcNow,
            null,
            new byte[] { 1 });
    }

    private static UserPreferencesDto CreatePreferencesDtoWithNullNotification(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            JsonMap(new Dictionary<string, object?> { ["theme"] = "dark" }),
            null!,
            JsonMap(new Dictionary<string, object?> { ["highContrast"] = true }),
            JsonMap(new Dictionary<string, object?> { ["profileVisible"] = true }),
            JsonMap(new Dictionary<string, object?> { ["Language"] = "en-US" }),
            DateTimeOffset.UtcNow,
            null,
            new byte[] { 1 });

    private static UserPreferencesDto CreatePreferencesDtoWithNullAccessibility(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            JsonMap(new Dictionary<string, object?> { ["theme"] = "dark" }),
            JsonMap(new Dictionary<string, object?> { ["email"] = true }),
            null!,
            JsonMap(new Dictionary<string, object?> { ["profileVisible"] = true }),
            JsonMap(new Dictionary<string, object?> { ["Language"] = "en-US" }),
            DateTimeOffset.UtcNow,
            null,
            new byte[] { 1 });

    private static UserPreferencesDto CreatePreferencesDtoWithNullPrivacy(Guid userId)
        => new(
            Guid.NewGuid(),
            userId,
            JsonMap(new Dictionary<string, object?> { ["theme"] = "dark" }),
            JsonMap(new Dictionary<string, object?> { ["email"] = true }),
            JsonMap(new Dictionary<string, object?> { ["highContrast"] = true }),
            null!,
            JsonMap(new Dictionary<string, object?> { ["Language"] = "en-US" }),
            DateTimeOffset.UtcNow,
            null,
            new byte[] { 1 });
}
