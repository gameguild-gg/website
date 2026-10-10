using Asp.Versioning;
using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GameGuild.Identity.Provisioning;

/// <summary>
///     RFC 7644 §4 ServiceProviderConfig discovery endpoint. Advertises exactly the
///     capabilities implemented by this provider.
/// </summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimServiceProviderConfigController(
    IScimProvisioningContext provisioningContext,
    Microsoft.Extensions.Options.IOptions<ScimProvisioningOptions> options) : BaseApiController
{
    [HttpGet("scim/v2/ServiceProviderConfig")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        ScimCommandGuards.RequireRead(provisioningContext);
        return Ok(ScimDiscoveryDocuments.BuildServiceProviderConfig(options.Value));
    }
}

/// <summary>RFC 7644 §4 Schemas discovery endpoint.</summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimSchemasController(IScimProvisioningContext provisioningContext) : BaseApiController
{
    [HttpGet("scim/v2/Schemas")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult List()
    {
        ScimCommandGuards.RequireRead(provisioningContext);
        return Ok(ScimDiscoveryDocuments.BuildSchemas());
    }

    [HttpGet("scim/v2/Schemas/{schemaId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status404NotFound)]
    public IActionResult Get(string schemaId)
    {
        ScimCommandGuards.RequireRead(provisioningContext);
        var schema = ScimDiscoveryDocuments.BuildSchema(schemaId);
        return schema is null
            ? new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 404, null, $"Schema '{schemaId}' was not found.")) { StatusCode = 404 }
            : Ok(schema);
    }
}

/// <summary>RFC 7644 §4 ResourceTypes discovery endpoint.</summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimResourceTypesController(IScimProvisioningContext provisioningContext) : BaseApiController
{
    [HttpGet("scim/v2/ResourceTypes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult List()
    {
        ScimCommandGuards.RequireRead(provisioningContext);
        return Ok(ScimDiscoveryDocuments.BuildResourceTypes());
    }
}

/// <summary>RFC 7644 §3.7 Bulk endpoint.</summary>
[ApiVersionNeutral]
[Microsoft.AspNetCore.Http.Tags("scim")]
[Authorize(AuthenticationSchemes = ScimProvisioningAuthenticationOptions.SchemeName)]
[TypeFilter(typeof(ScimExceptionFilter))]
public sealed class ScimBulkController(ISender dispatcher) : BaseApiController
{
    [HttpPost("scim/v2/Bulk")]
    [ProducesResponseType(typeof(Scim.ScimBulkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ScimErrorBody), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Bulk([FromBody] ScimBulkRequest request, CancellationToken cancellationToken)
    {
        var result = await dispatcher.Send(new ScimBulkCommand(request), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? Ok(result.Value)
            : new ObjectResult(new ScimErrorBody([ScimConstants.ErrorSchema], 400, null, result.Error.Description)) { StatusCode = 400 };
    }
}
