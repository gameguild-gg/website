using GameGuild.CQRS;
using GameGuild.Identity.Provisioning.Scim;

namespace GameGuild.Identity.Provisioning;

public sealed record ScimBulkCommand(ScimBulkRequest Request) : ICommand<Result<ScimBulkResponse>>;

public sealed class ScimBulkHandler(IScimBulkProcessor bulkProcessor)
    : ICommandHandler<ScimBulkCommand, Result<ScimBulkResponse>>
{
    public async Task<Result<ScimBulkResponse>> Handle(ScimBulkCommand request, CancellationToken cancellationToken)
    {
        return Result.Success(await bulkProcessor.ProcessAsync(request.Request, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>Service seam consumed by the bulk command handler.</summary>
public interface IScimBulkProcessor
{
    Task<ScimBulkResponse> ProcessAsync(ScimBulkRequest request, CancellationToken cancellationToken);
}
