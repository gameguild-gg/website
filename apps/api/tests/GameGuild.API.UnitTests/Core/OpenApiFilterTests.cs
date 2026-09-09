using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Moq;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GameGuild.API.UnitTests.Core;

public sealed class OpenApiFilterTests
{
    [Flags]
    private enum FlagValues { None = 0, Read = 1, Write = 2 }
    private enum PlainValues { None = 0, One = 1 }

    private sealed class PlainController
    {
        public void Secured() { }

        [AllowAnonymous]
        public void Anonymous() { }
    }

    [AllowAnonymous]
    [Tags("", "Tenant Users")]
    private sealed class AnonymousController
    {
        public void Inherited() { }

        [Authorize]
        public void Authorized() { }
    }

    [Fact]
    public void FlagsEnumSchemaFilter_OnlyRewritesFlagsEnums()
    {
        var filter = new FlagsEnumSchemaFilter();
        var generator = Mock.Of<ISchemaGenerator>();
        var repository = new SchemaRepository();
        var nonEnumSchema = new OpenApiSchema { Type = "string" };
        var plainSchema = new OpenApiSchema { Type = "integer", Format = "int32", Enum = [new OpenApiInteger(1)] };
        var flagsSchema = new OpenApiSchema { Type = "integer", Format = "int32", Enum = [new OpenApiInteger(1)] };
        var flagsSchemaWithoutEnum = new OpenApiSchema { Type = "integer", Format = "int32", Enum = null! };

        filter.Apply(nonEnumSchema, new SchemaFilterContext(typeof(string), generator, repository));
        filter.Apply(plainSchema, new SchemaFilterContext(typeof(PlainValues), generator, repository));
        filter.Apply(flagsSchema, new SchemaFilterContext(typeof(FlagValues), generator, repository));
        filter.Apply(flagsSchemaWithoutEnum, new SchemaFilterContext(typeof(FlagValues), generator, repository));

        nonEnumSchema.Type.Should().Be("string");
        plainSchema.Type.Should().Be("integer");
        plainSchema.Enum.Should().ContainSingle();
        flagsSchema.Type.Should().Be("string");
        flagsSchema.Format.Should().BeNull();
        flagsSchema.Enum.Should().BeEmpty();
        flagsSchema.Description.Should().Contain("comma-separated");
        flagsSchemaWithoutEnum.Type.Should().Be("string");
    }

    [Fact]
    public async Task OpenApiDocumentTransformer_CompletesWithoutMutation()
    {
        var document = new OpenApiDocument();

        await new OpenApiDocumentTransformer().TransformAsync(document, null!, CancellationToken.None);

        document.Paths.Should().BeNull();
    }

    [Fact]
    public void ModuleControllerTagOperationFilter_HandlesControllerAndNonControllerActions()
    {
        var filter = new ModuleControllerTagOperationFilter();
        var untouched = new OpenApiOperation();
        var nonControllerContext = CreateContext(new ActionDescriptor(), typeof(PlainController).GetMethod(nameof(PlainController.Secured))!);

        filter.Apply(untouched, nonControllerContext);

        untouched.Tags.Should().BeNullOrEmpty();

        var operation = new OpenApiOperation();
        var descriptor = CreateControllerDescriptor(
            typeof(AnonymousController),
            nameof(AnonymousController.Inherited),
            "TenantSettings");
        filter.Apply(operation, CreateContext(descriptor, descriptor.MethodInfo));

        operation.Tags.Should().ContainSingle().Which.Name.Should().Be("api/unit-tests/core/tenant-users");

        var blankOperation = new OpenApiOperation();
        var blankDescriptor = CreateControllerDescriptor(
            typeof(OpenApiNoNamespaceController),
            nameof(OpenApiNoNamespaceController.Execute),
            " ");
        filter.Apply(blankOperation, CreateContext(blankDescriptor, blankDescriptor.MethodInfo));
        blankOperation.Tags.Should().BeNullOrEmpty();
    }

    [Fact]
    public void ModuleControllerTagOperationFilter_CoversCanonicalControllerNames()
    {
        var cases = new Dictionary<string, string>
        {
            ["ApiKey"] = "auth/api-keys",
            ["Auth"] = "auth",
            ["Mfa"] = "auth/multi-factor",
            ["Session"] = "auth/sessions",
            ["KeyRotation"] = "auth/signing-keys",
            ["TrustedDevices"] = "auth/trusted-devices",
            ["WebAuthn"] = "auth/webauthn",
            ["Roles"] = "auth/roles",
            ["PermissionAdmin"] = "auth/permissions/admin",
            ["PermissionEvaluation"] = "auth/permissions/evaluation",
            ["PermissionGrants"] = "auth/permissions/grants",
            ["ServiceAccountCrud"] = "auth/service-accounts",
            ["ServiceAccountOperations"] = "auth/service-accounts",
            ["ServiceAccountToken"] = "auth/service-accounts/tokens",
            ["AccessReviewAnalytics"] = "access-control/access-reviews/analytics",
            ["AccessReviewCampaign"] = "access-control/access-reviews/campaigns",
            ["AccessReviewItem"] = "access-control/access-reviews/items",
            ["AbacPolicy"] = "access-control/abac-policies",
            ["ConditionalPolicyCrud"] = "access-control/conditional-policies",
            ["ConditionalPolicyEvaluation"] = "access-control/conditional-policies/evaluations"
        };

        foreach (var (controller, expected) in cases)
        {
            BuildTag("GameGuild.Custom", controller, null).Should().Be(expected);
        }
    }

    [Fact]
    public void ModuleControllerTagOperationFilter_CoversNamespacePrefixesAndLeafNormalization()
    {
        var cases = new (string? Namespace, string Controller, string Expected)[]
        {
            (null, "WidgetController", "widget"),
            ("GameGuild.API.Controllers", "WidgetController", "widget"),
            ("GameGuild.Identity.Authentication.Controllers", "WidgetController", "auth/widget"),
            ("GameGuild.Identity.Authorization.Controllers", "WidgetController", "access-control/widget"),
            ("GameGuild.Identity.Users.Controllers", "UserCrud", "users/user"),
            ("GameGuild.Identity.Tenants.Controllers", "UserInvitations", "users/invitations"),
            ("GameGuild.Identity.Tenants.Controllers", "TenantSettings", "tenants/settings"),
            ("GameGuild.Resources.Contents.Controllers", "ContentOperations", "resources/contents/content"),
            ("GameGuild.Resources.Controllers", "UserQuota", "users/resources/user-quota"),
            ("GameGuild.Resources.Controllers", "TenantQuota", "tenants/resources/tenant-quota"),
            ("GameGuild.Resources.Controllers", "Quota", "resources/quota"),
            ("GameGuild.Commerce.Products.Controllers", "Catalog", "commerce/products/catalog"),
            ("GameGuild.Compliance.Audit.Controllers", "Events", "compliance/audit/events"),
            ("GameGuild.Content.Pages.Controllers", "Page", "content/pages/page"),
            ("GameGuild.Learning.Courses.Controllers", "Course", "learning/courses/course"),
            ("GameGuild.Social.Groups.Controllers", "Group", "social/groups/group"),
            ("GameGuild.Monitoring.SLA.Controllers", "Report", "monitoring/sla/report"),
            ("GameGuild.Gamification.Achievements.Controllers", "Award", "gamification/achievements/award"),
            ("GameGuild.Analytics.Controllers", "Report", "analytics/report"),
            ("GameGuild.Assets.Controllers", "File", "assets/file"),
            ("GameGuild.Features.Controllers", "Flag", "features/flag"),
            ("GameGuild.Notifications.Controllers", "Template", "notifications/template"),
            ("GameGuild.Projects.Controllers", "Project", "projects/project"),
            ("GameGuild.TestingLab.Controllers", "Request", "testing-lab/request"),
            ("GameGuild.Custom.Module.Controllers", "Widget", "custom/module/widget")
        };

        foreach (var (controllerNamespace, controller, expected) in cases)
        {
            BuildTag(controllerNamespace, controller, null).Should().Be(expected);
        }
    }

    [Fact]
    public void ModuleControllerTagOperationFilter_CoversExplicitTagNormalization()
    {
        BuildTag("GameGuild.API.Controllers", "Widget", "Team Members").Should().Be("team-members");
        BuildTag("GameGuild.Identity.Authentication.Controllers", "Widget", "social/groups").Should().Be("social/groups");
        BuildTag("GameGuild.Identity.Authentication.Controllers", "Widget", "authentication").Should().Be("auth");
        BuildTag("GameGuild.Identity.Authentication.Controllers", "Widget", "authentication/Sessions").Should().Be("auth/sessions");
        BuildTag("GameGuild.Identity.Tenants.Controllers", "Widget", "tenants").Should().Be("tenants");
        BuildTag("GameGuild.Identity.Tenants.Controllers", "Widget", "Tenant Settings").Should().Be("tenants/settings");
        BuildTag("GameGuild.Identity.Tenants.Controllers", "Widget", "Billing/History").Should().Be("tenants/billing/history");
        BuildTag("GameGuild.Identity.Tenants.Controllers", "", null).Should().Be("tenants");
        InvokePrivate<string>("NormalizeExplicitTag", "tenants", " ").Should().Be("tenants");
        InvokePrivate<List<string>>("NormalizePath", " ").Should().BeEmpty();
        InvokePrivate<string>("CollapseDuplicatePrefixToken", " ", "tenants").Should().BeEmpty();
        InvokePrivate<string>("CollapseDuplicatePrefixToken", "widget", " ").Should().Be("widget");
        InvokePrivate<string>("CollapseDuplicatePrefixToken", "tenants", "tenants").Should().Be("tenants");
        InvokePrivate<bool>("IsAliasOfPrefix", "tenants", "tenants").Should().BeTrue();
        InvokePrivate<bool>("IsAliasOfPrefix", "widget", "tenants").Should().BeFalse();
        InvokePrivate<string>("NormalizeLeaf", "", "Widget").Should().Be("widget");
        InvokePrivate<string>("NormalizeLeaf", "tenants", "Widget").Should().Be("widget");
        InvokePrivate<string>("NormalizeLeaf", "tenants", "Tenants").Should().BeEmpty();
        InvokePrivate<string>("NormalizeExplicitTag", "tenants", "tenant-tenants").Should().Be("tenants");
        InvokePrivate<bool>("IsFullyQualified", new List<string>()).Should().BeFalse();
        InvokePrivate<string>("ToKebabCase", " ").Should().BeEmpty();
        InvokePrivate<string>("ToKebabCase", "__API").Should().Be("api");
        InvokePrivate<string>("ToKebabCase", "API_Key Value").Should().Be("api-key-value");
        InvokePrivate<string>("ToKebabCase", "alpha1").Should().Be("alpha1");
    }

    [Fact]
    public void AllowAnonymousOperationFilter_RespectsActionAndControllerAuthorization()
    {
        var filter = new AllowAnonymousOperationFilter();
        var nonController = new OpenApiOperation();
        filter.Apply(nonController, CreateContext(new ActionDescriptor(), typeof(PlainController).GetMethod(nameof(PlainController.Secured))!));
        nonController.Extensions.Should().NotContainKey(OpenApiExtensions.AllowAnonymousExtensionName);

        var secured = ApplyAnonymousFilter(filter, typeof(PlainController), nameof(PlainController.Secured));
        secured.Extensions.Should().NotContainKey(OpenApiExtensions.AllowAnonymousExtensionName);

        var actionAnonymous = ApplyAnonymousFilter(filter, typeof(PlainController), nameof(PlainController.Anonymous));
        actionAnonymous.Extensions[OpenApiExtensions.AllowAnonymousExtensionName].Should().BeOfType<OpenApiBoolean>();
        actionAnonymous.Security.Should().BeEmpty();

        var controllerAnonymous = ApplyAnonymousFilter(filter, typeof(AnonymousController), nameof(AnonymousController.Inherited));
        controllerAnonymous.Extensions.Should().ContainKey(OpenApiExtensions.AllowAnonymousExtensionName);

        var actionAuthorized = ApplyAnonymousFilter(filter, typeof(AnonymousController), nameof(AnonymousController.Authorized));
        actionAuthorized.Extensions.Should().NotContainKey(OpenApiExtensions.AllowAnonymousExtensionName);
    }

    private static OpenApiOperation ApplyAnonymousFilter(AllowAnonymousOperationFilter filter, Type controllerType, string actionName)
    {
        var descriptor = CreateControllerDescriptor(controllerType, actionName, controllerType.Name.Replace("Controller", ""));
        var operation = new OpenApiOperation();
        filter.Apply(operation, CreateContext(descriptor, descriptor.MethodInfo));
        return operation;
    }

    private static ControllerActionDescriptor CreateControllerDescriptor(Type controllerType, string actionName, string controllerName)
    {
        return new ControllerActionDescriptor
        {
            ControllerName = controllerName,
            ControllerTypeInfo = controllerType.GetTypeInfo(),
            MethodInfo = controllerType.GetMethod(actionName)!,
            ActionName = actionName
        };
    }

    private static OperationFilterContext CreateContext(ActionDescriptor descriptor, MethodInfo method)
    {
        var description = new ApiDescription { ActionDescriptor = descriptor };
        return new OperationFilterContext(description, Mock.Of<ISchemaGenerator>(), new SchemaRepository(), method);
    }

    private static string BuildTag(string? controllerNamespace, string controllerName, string? explicitTag)
    {
        return InvokePrivate<string>("BuildTag", controllerNamespace, controllerName, explicitTag);
    }

    private static T InvokePrivate<T>(string methodName, params object?[] arguments)
    {
        var method = typeof(ModuleControllerTagOperationFilter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();
        return method!.Invoke(null, arguments).Should().BeAssignableTo<T>().Subject;
    }
}
