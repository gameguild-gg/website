using GameGuild.CQRS;

namespace GameGuild.Analytics;

public sealed record RunAnalyticsWarehouseCommand(
    AnalyticsWarehouseRunRequest Request) : ICommand<AnalyticsWarehouseRunResponse>;

public sealed class RunAnalyticsWarehouseCommandHandler(IAnalyticsDataWarehouseService warehouseService)
    : ICommandHandler<RunAnalyticsWarehouseCommand, AnalyticsWarehouseRunResponse>
{
    public Task<AnalyticsWarehouseRunResponse> Handle(
        RunAnalyticsWarehouseCommand command,
        CancellationToken cancellationToken) =>
        warehouseService.MaterializeAsync(command.Request, cancellationToken);
}
