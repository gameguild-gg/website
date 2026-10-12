using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using System.Reflection;

namespace GameGuild.API.Core.OpenApi;

/// <summary>Matches discovered OpenAPI documents to their resolved API versions.</summary>
public sealed class VersionedOpenApiDocumentSelector
{
    private readonly VersionedOpenApiDocumentCatalog catalog;

    /// <summary>Creates a selector using the version explorer's actual document groups.</summary>
    public VersionedOpenApiDocumentSelector(IEnumerable<ApiVersionDescription> descriptions)
        : this(descriptions, VersionedOpenApiDocumentCatalog.DefaultGroupNameFormat)
    {
        // The default constructor selects the standard explorer naming format.
    }

    /// <summary>Creates a selector using the configured version group format.</summary>
    public VersionedOpenApiDocumentSelector(IEnumerable<ApiVersionDescription> descriptions, string groupNameFormat)
    {
        catalog = new VersionedOpenApiDocumentCatalog(descriptions, groupNameFormat);
    }

    /// <summary>Gets the documents to register for this selector.</summary>
    public IReadOnlyList<ApiVersionDescription> Descriptions => catalog.Descriptions;

    /// <summary>Checks whether an action belongs to the requested base document.</summary>
    public bool Includes(string documentName, ApiDescription description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (!catalog.TryGetDescription(documentName, out var document))
        {
            return false;
        }
        var targetVersion = document!.ApiVersion;

        // Explorer clones carry the action's specific version, including patch/date/status.
        var selectedVersion = description.GetApiVersion();
        if (selectedVersion is not null)
        {
            return targetVersion.Equals(selectedVersion) && MatchesScope(documentName, description);
        }

        var metadata = description.ActionDescriptor.GetApiVersionMetadata();
        if (metadata.IsApiVersionNeutral)
        {
            return IncludesNeutralAction(documentName, description.GroupName);
        }
        if (!ReferenceEquals(metadata, ApiVersionMetadata.Empty))
        {
            return metadata.IsMappedTo(targetVersion) && MatchesScope(documentName, description);
        }

        // Descriptions created outside the version explorer can still contain MVC declarations.
        if (description.ActionDescriptor is ControllerActionDescriptor controller)
        {
            if (IsNeutral(controller))
            {
                return IncludesNeutralAction(documentName, description.GroupName);
            }

            var mappedVersions = controller.MethodInfo.GetCustomAttributes<MapToApiVersionAttribute>(inherit: false)
                .SelectMany(attribute => attribute.Versions).ToArray();
            if (mappedVersions.Length > 0)
            {
                return mappedVersions.Any(targetVersion.Equals) && MatchesScope(documentName, description);
            }

            var declaredVersions = controller.ControllerTypeInfo.GetCustomAttributes<ApiVersionAttribute>(inherit: false)
                .SelectMany(attribute => attribute.Versions).ToArray();
            if (declaredVersions.Length > 0)
            {
                return declaredVersions.Any(targetVersion.Equals) && MatchesScope(documentName, description);
            }
        }
        return string.Equals(description.GroupName, documentName, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesScope(string documentName, ApiDescription description) =>
        !catalog.TryGetGroupScope(documentName, out var scope)
        || description.ActionDescriptor.GetApiVersionMetadata().IsApiVersionNeutral
        || description.ActionDescriptor is ControllerActionDescriptor controller && IsNeutral(controller)
        || string.Equals(description.GroupName, scope, StringComparison.OrdinalIgnoreCase);

    private bool IncludesNeutralAction(string documentName, string? groupName) =>
        groupName is null || !catalog.TryGetDescription(groupName, out _)
        || string.Equals(groupName, documentName, StringComparison.OrdinalIgnoreCase);

    private static bool IsNeutral(ControllerActionDescriptor controller) =>
        controller.ControllerTypeInfo.IsDefined(typeof(ApiVersionNeutralAttribute), inherit: false)
        || controller.MethodInfo.IsDefined(typeof(ApiVersionNeutralAttribute), inherit: false);
}
