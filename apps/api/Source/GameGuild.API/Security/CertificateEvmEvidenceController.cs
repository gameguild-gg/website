using Asp.Versioning;
using GameGuild.API.Core.Integration;
using GameGuild.API.Database;
using GameGuild.Identity.Context.Actors;
using GameGuild.Learning.Certificates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.API.Security;

[ApiVersion("1.0")]
[Route("api/certificates")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[Microsoft.AspNetCore.Http.Tags("certificates")]
public sealed class CertificateEvmEvidenceController(ApplicationDbContext context, IActorContextAccessor actors,
    ICertificateEvmEvidenceReader reader) : BaseApiController
{
    /// <summary>Read asynchronous publication state. Cached state does not prove current chain validity.</summary>
    [HttpGet("{id:guid}/evm")]
    [ProducesResponseType<CertificateEvmEvidenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<CertificateEvmEvidenceResponse>> GetCertificateEvmEvidence(Guid id, CancellationToken cancellationToken) =>
        ReadAsync(id, false, cancellationToken);

    /// <summary>Independently verify the original recorded transaction against its configured canonical chain.</summary>
    [HttpGet("{id:guid}/evm/proof")]
    [ProducesResponseType<CertificateEvmEvidenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<ActionResult<CertificateEvmEvidenceResponse>> GetCertificateEvmProof(Guid id, CancellationToken cancellationToken) =>
        ReadAsync(id, true, cancellationToken);

    private async Task<ActionResult<CertificateEvmEvidenceResponse>> ReadAsync(Guid id, bool freshProof, CancellationToken cancellationToken)
    {
        var actor = actors.ActorContext;
        if (!actor.IsAuthenticated || actor.SubjectIdAsGuid is not { } actorId || actorId == Guid.Empty)
        {
            return Unauthorized();
        }
        var query = context.Set<Certificate>().IgnoreQueryFilters().AsNoTracking().Where(c => c.Id == id && c.DeletedAt == null);
        if (!actor.IsSystemAdmin)
        {
            query = query.Where(c => c.UserId == actorId && c.TenantId == actor.TenantId);
        }
        var document = await query.Select(c => new CertificateEvmDocument(c.Id, c.UserId, c.TenantId, c.Status, c.ExpiresAt))
            .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return NotFound();
        }
        return Ok(await reader.ReadAsync(document, freshProof, cancellationToken).ConfigureAwait(false));
    }
}
