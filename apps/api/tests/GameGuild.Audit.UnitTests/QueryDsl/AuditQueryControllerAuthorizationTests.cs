using FluentAssertions;
using GameGuild.Compliance.Audit;
using GameGuild.Identity.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace GameGuild.Tests.Audit.Unit.QueryDsl;

/// <summary>
///     Authorization matrix for the audit query DSL surface: every endpoint is deny-by-default
///     and gated on the Audit.Read permission tier (issue #294 - the tenant-administered
///     self-service audit tier; the controller additionally clamps non-system callers to
///     their own tenant's rows), with no anonymous access anywhere.
/// </summary>
public sealed class AuditQueryControllerAuthorizationTests
{
    private const string AnonymousEndpointRegistryPath = "apps/api/Source/GameGuild.API/Security/AnonymousEndpointRegistry.cs";

    [Fact]
    public void Controller_RequiresAuditReadPolicy()
    {
        var attribute = typeof(AuditQueryController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .OfType<AuthorizeAttribute>()
            .SingleOrDefault();

        attribute.Should().NotBeNull("the audit query controller must be deny-by-default");
        attribute!.Policy.Should().Be(Policies.AuditRead, "audit queries expose tenant-wide audit data and follow the sibling audit controllers' Audit.Read tier (#294)");
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

    [Fact]
    public void QueryOperationActions_CarryTheIssue375QueryOperationGates()
    {
        // Issue #375: the list/search/query actions additionally require the
        // query-operations permission tier (AND-composed with the controller-level
        // Audit.Read policy; fail-closed when the key is not granted).
        var expectedGates = new (string Action, string Permission)[]
        {
            (nameof(AuditQueryController.ExecuteQuery), "queries:query"),
            (nameof(AuditQueryController.GetQueryResults), "queries:search"),
            (nameof(AuditQueryController.ValidateQuery), "queries:query"),
            (nameof(AuditQueryController.GetTemplates), "queries:list"),
            (nameof(AuditQueryController.ListSavedQueries), "queries:list"),
            (nameof(AuditQueryController.ExecuteSavedQuery), "queries:query")
        };

        foreach (var (action, permission) in expectedGates)
        {
            var method = typeof(AuditQueryController).GetMethod(action);
            method.Should().NotBeNull($"audit query action {action} must exist");

            var gates = method!.GetCustomAttributes(typeof(RequireDacPermissionAttribute), inherit: false)
                .OfType<RequireDacPermissionAttribute>()
                .ToArray();
            gates.Should().NotBeEmpty($"audit query action {action} must carry a query-operations gate (#375)");
            gates.Should().Contain(gate => gate.RequiredPermissions.Contains(permission),
                $"audit query action {action} must require {permission} (#375)");
        }
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
