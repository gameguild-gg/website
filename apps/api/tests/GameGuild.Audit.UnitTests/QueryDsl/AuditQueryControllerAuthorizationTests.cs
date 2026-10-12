using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.QueryDsl;

/// <summary>
///     Authorization matrix for the audit query DSL surface: every endpoint is deny-by-default
///     and gated on the same SystemAdmin policy as the sibling Compliance.Audit controllers,
///     with no anonymous access anywhere.
/// </summary>
public sealed class AuditQueryControllerAuthorizationTests
{
    private const string AnonymousEndpointRegistryPath = "apps/api/Source/GameGuild.API/Security/AnonymousEndpointRegistry.cs";

    [Fact]
    public void Controller_RequiresSystemAdminPolicy()
    {
        var attribute = typeof(AuditQueryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull("the audit query controller must be deny-by-default");
        attribute!.Policy.Should().Be(Policies.SystemAdmin, "audit queries expose tenant-wide audit data and follow the sibling audit controllers");
    }

    [Fact]
    public void EveryAction_IsAuthorized_NoAnonymousEndpoints()
    {
        var routeAttributeTypes = new[]
        {
            typeof(Microsoft.AspNetCore.Mvc.HttpGetAttribute),
            typeof(Microsoft.AspNetCore.Mvc.HttpPostAttribute),
            typeof(Microsoft.AspNetCore.Mvc.HttpPutAttribute),
            typeof(Microsoft.AspNetCore.Mvc.HttpDeleteAttribute)
        };

        var actions = typeof(AuditQueryController)
            .GetMethods()
            .Where(method => method.DeclaringType == typeof(AuditQueryController))
            .Where(method => method.IsPublic)
            .Where(method => routeAttributeTypes.Any(attributeType => method.GetCustomAttributes(attributeType, false).Length > 0))
            .ToArray();

        actions.Should().NotBeEmpty();
        foreach (var action in actions)
        {
            var allowAnonymous = action.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Any();
            allowAnonymous.Should().BeFalse($"audit query action {action.Name} must never be anonymous");
        }
    }

    [Fact]
    public void AnonymousEndpointRegistry_DoesNotContainAuditQueryEndpoints()
    {
        var registry = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), AnonymousEndpointRegistryPath));

        registry.Should().NotContain("audit-queries", "no audit query endpoint is registered for anonymous access");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("repository root must be discoverable from the test output directory");
        return directory!.FullName;
    }
}

public sealed class AuditQueryTemplateCatalogTests
{
    [Fact]
    public void Templates_AreValidDslQueries_WithUniqueIds()
    {
        var templates = AuditQueryTemplateCatalog.Templates;

        templates.Should().NotBeEmpty();
        templates.Select(template => template.TemplateId).Should().OnlyHaveUniqueItems();
        templates.Select(template => template.QueryJson).Should().OnlyHaveUniqueItems();

        var parser = new AuditQueryDslParser();
        foreach (var template in templates)
        {
            var parsed = parser.ParseJson(template.QueryJson);
            parsed.IsSuccess.Should().BeTrue($"template '{template.TemplateId}' must parse: {parsed.Error.Description}");
            template.Name.Should().NotBeNullOrWhiteSpace();
            template.IntendedRole.Should().NotBeNullOrWhiteSpace();
        }
    }
}
