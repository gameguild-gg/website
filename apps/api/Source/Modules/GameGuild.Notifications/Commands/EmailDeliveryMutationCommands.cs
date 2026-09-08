using GameGuild.CQRS;
using GameGuild.Notifications.Services.Email;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Notifications;

public sealed record ReleaseEmailSuppressionCommand(string Email) : ICommand<Result<bool>>;
public sealed record RequeueEmailNotificationCommand(Guid NotificationId) : ICommand<Result<Notification>>;
public sealed record IngestEmailDeliveryEventCommand(EmailDeliveryEvent DeliveryEvent) : ICommand<Result>;

public sealed class EmailDeliveryMutationCommandHandler(
    IEmailDeliveryAdminService adminService,
    IEmailEventProcessor eventProcessor,
    IApplicationDbContext context) :
    ICommandHandler<ReleaseEmailSuppressionCommand, Result<bool>>,
    ICommandHandler<RequeueEmailNotificationCommand, Result<Notification>>,
    ICommandHandler<IngestEmailDeliveryEventCommand, Result>
{
    public Task<Result<bool>> Handle(ReleaseEmailSuppressionCommand command, CancellationToken cancellationToken) =>
        adminService.ReleaseSuppressionAsync(command.Email, cancellationToken);

    public Task<Result<Notification>> Handle(RequeueEmailNotificationCommand command, CancellationToken cancellationToken) =>
        adminService.RequeueAsync(command.NotificationId, cancellationToken);

    public async Task<Result> Handle(IngestEmailDeliveryEventCommand command, CancellationToken cancellationToken)
    {
        var emailEvent = command.DeliveryEvent;
        var processorRan = false;

        try
        {
            var alreadyIngested = await context.Set<EmailDeliveryEvent>()
                .AnyAsync(e => e.SnsMessageId == emailEvent.SnsMessageId, cancellationToken)
                .ConfigureAwait(false);

            if (!alreadyIngested)
            {
                context.Set<EmailDeliveryEvent>().Add(emailEvent);
            }

            await eventProcessor.ProcessAsync(emailEvent, cancellationToken).ConfigureAwait(false);
            processorRan = true;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (DbUpdateException)
        {
            ResetContextState();
            var isDuplicate = await context.Set<EmailDeliveryEvent>()
                .AnyAsync(e => e.SnsMessageId == emailEvent.SnsMessageId, cancellationToken)
                .ConfigureAwait(false);
            if (!isDuplicate)
            {
                return IngestFailure();
            }

            if (!processorRan)
            {
                await eventProcessor.ProcessAsync(emailEvent, cancellationToken).ConfigureAwait(false);
            }

            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception)
        {
            return IngestFailure();
        }
    }

    private void ResetContextState()
    {
        if (context is not DbContext dbContext)
        {
            return;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries().ToList())
        {
            if (entry.State != EntityState.Unchanged)
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static Result IngestFailure() =>
        Result.Failure(Error.Failure("Notifications.EmailEvents.IngestFailed", "The event could not be ingested."));
}
