using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record StartContentInteractionEndpointCommand(Guid ProgramUserId, Guid ContentId) : ICommand<ContentInteraction>;
public sealed record UpdateContentInteractionProgressEndpointCommand(Guid InteractionId, decimal CompletionPercentage) : ICommand<ContentInteraction>;
public sealed record SubmitContentInteractionEndpointCommand(Guid InteractionId, string SubmissionData) : ICommand<ContentInteraction>;
public sealed record CompleteContentInteractionEndpointCommand(Guid InteractionId) : ICommand<ContentInteraction>;
public sealed record UpdateContentInteractionTimeEndpointCommand(Guid InteractionId, int AdditionalMinutes) : ICommand<ContentInteraction>;

public sealed class ContentInteractionEndpointCommandHandler(IContentInteractionService interactionService) :
    ICommandHandler<StartContentInteractionEndpointCommand, ContentInteraction>,
    ICommandHandler<UpdateContentInteractionProgressEndpointCommand, ContentInteraction>,
    ICommandHandler<SubmitContentInteractionEndpointCommand, ContentInteraction>,
    ICommandHandler<CompleteContentInteractionEndpointCommand, ContentInteraction>,
    ICommandHandler<UpdateContentInteractionTimeEndpointCommand, ContentInteraction>
{
    public Task<ContentInteraction> Handle(StartContentInteractionEndpointCommand request, CancellationToken cancellationToken) =>
        interactionService.StartContentAsync(request.ProgramUserId, request.ContentId);

    public Task<ContentInteraction> Handle(UpdateContentInteractionProgressEndpointCommand request, CancellationToken cancellationToken) =>
        interactionService.UpdateProgressAsync(request.InteractionId, request.CompletionPercentage);

    public Task<ContentInteraction> Handle(SubmitContentInteractionEndpointCommand request, CancellationToken cancellationToken) =>
        interactionService.SubmitContentAsync(request.InteractionId, request.SubmissionData);

    public Task<ContentInteraction> Handle(CompleteContentInteractionEndpointCommand request, CancellationToken cancellationToken) =>
        interactionService.CompleteContentAsync(request.InteractionId);

    public Task<ContentInteraction> Handle(UpdateContentInteractionTimeEndpointCommand request, CancellationToken cancellationToken) =>
        interactionService.UpdateTimeSpentAsync(request.InteractionId, request.AdditionalMinutes);
}
