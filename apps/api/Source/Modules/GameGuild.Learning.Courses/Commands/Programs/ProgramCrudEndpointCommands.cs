using GameGuild.CQRS;

namespace GameGuild.Learning.Courses;

public sealed record CreateProgramEndpointCommand(CreateProgramDto Program) : ICommand<Program>;
public sealed record UpdateProgramEndpointCommand(Guid ProgramId, UpdateProgramDto Update) : ICommand<Program?>;
public sealed record DeleteProgramEndpointCommand(Guid ProgramId) : ICommand;
public sealed record CloneProgramEndpointCommand(Guid ProgramId, string NewTitle) : ICommand<Program>;
public sealed record AddUserToProgramEndpointCommand(Guid ProgramId, Guid UserId) : ICommand<UserProgressDto?>;
public sealed record RemoveUserFromProgramEndpointCommand(Guid ProgramId, Guid UserId) : ICommand<bool>;
public sealed record UpdateUserProgressEndpointCommand(Guid ProgramId, Guid UserId, UpdateProgressDto Progress) : ICommand<UserProgressDto?>;
public sealed record MarkProgramContentCompletedEndpointCommand(Guid ProgramId, Guid UserId, Guid ContentId) : ICommand<bool>;
public sealed record ResetUserProgressEndpointCommand(Guid ProgramId, Guid UserId) : ICommand<bool>;
public sealed record EnableProgramMonetizationEndpointCommand(Guid ProgramId, MonetizationDto Monetization) : ICommand<Program?>;
public sealed record DisableProgramMonetizationEndpointCommand(Guid ProgramId) : ICommand<Program?>;
public sealed record UpdateProgramPricingEndpointCommand(Guid ProgramId, UpdatePricingDto Pricing) : ICommand<PricingDto?>;
public sealed record CreateProductFromProgramEndpointCommand(Guid ProgramId, CreateProductFromProgramDto Product) : ICommand<Guid?>;
public sealed record LinkProgramToProductEndpointCommand(Guid ProgramId, Guid ProductId) : ICommand<bool>;
public sealed record UnlinkProgramFromProductEndpointCommand(Guid ProgramId, Guid ProductId) : ICommand<bool>;

public sealed class ProgramCrudEndpointCommandHandler(IProgramCrudService programService) :
    ICommandHandler<CreateProgramEndpointCommand, Program>,
    ICommandHandler<UpdateProgramEndpointCommand, Program?>,
    ICommandHandler<DeleteProgramEndpointCommand>,
    ICommandHandler<CloneProgramEndpointCommand, Program>,
    ICommandHandler<AddUserToProgramEndpointCommand, UserProgressDto?>,
    ICommandHandler<RemoveUserFromProgramEndpointCommand, bool>,
    ICommandHandler<UpdateUserProgressEndpointCommand, UserProgressDto?>,
    ICommandHandler<MarkProgramContentCompletedEndpointCommand, bool>,
    ICommandHandler<ResetUserProgressEndpointCommand, bool>,
    ICommandHandler<EnableProgramMonetizationEndpointCommand, Program?>,
    ICommandHandler<DisableProgramMonetizationEndpointCommand, Program?>,
    ICommandHandler<UpdateProgramPricingEndpointCommand, PricingDto?>,
    ICommandHandler<CreateProductFromProgramEndpointCommand, Guid?>,
    ICommandHandler<LinkProgramToProductEndpointCommand, bool>,
    ICommandHandler<UnlinkProgramFromProductEndpointCommand, bool>
{
    public Task<Program> Handle(CreateProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.CreateProgramAsync(request.Program);

    public Task<Program?> Handle(UpdateProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.UpdateProgramAsync(request.ProgramId, request.Update);

    public async Task<Unit> Handle(DeleteProgramEndpointCommand request, CancellationToken cancellationToken)
    {
        await programService.DeleteProgramAsync(request.ProgramId).ConfigureAwait(false);
        return Unit.Value;
    }

    public Task<Program> Handle(CloneProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.CloneProgramAsync(request.ProgramId, request.NewTitle);

    public Task<UserProgressDto?> Handle(AddUserToProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.AddUserToProgramAsync(request.ProgramId, request.UserId);

    public Task<bool> Handle(RemoveUserFromProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.RemoveUserFromProgramAsync(request.ProgramId, request.UserId);

    public Task<UserProgressDto?> Handle(UpdateUserProgressEndpointCommand request, CancellationToken cancellationToken) =>
        programService.UpdateUserProgressAsync(request.ProgramId, request.UserId, request.Progress);

    public Task<bool> Handle(MarkProgramContentCompletedEndpointCommand request, CancellationToken cancellationToken) =>
        programService.MarkContentCompletedAsync(request.ProgramId, request.UserId, request.ContentId);

    public Task<bool> Handle(ResetUserProgressEndpointCommand request, CancellationToken cancellationToken) =>
        programService.ResetUserProgressAsync(request.ProgramId, request.UserId);

    public Task<Program?> Handle(EnableProgramMonetizationEndpointCommand request, CancellationToken cancellationToken) =>
        programService.EnableMonetizationAsync(request.ProgramId, request.Monetization);

    public Task<Program?> Handle(DisableProgramMonetizationEndpointCommand request, CancellationToken cancellationToken) =>
        programService.DisableMonetizationAsync(request.ProgramId);

    public Task<PricingDto?> Handle(UpdateProgramPricingEndpointCommand request, CancellationToken cancellationToken) =>
        programService.UpdateProgramPricingAsync(request.ProgramId, request.Pricing);

    public Task<Guid?> Handle(CreateProductFromProgramEndpointCommand request, CancellationToken cancellationToken) =>
        programService.CreateProductFromProgramAsync(request.ProgramId, request.Product);

    public Task<bool> Handle(LinkProgramToProductEndpointCommand request, CancellationToken cancellationToken) =>
        programService.LinkProgramToProductAsync(request.ProgramId, request.ProductId);

    public Task<bool> Handle(UnlinkProgramFromProductEndpointCommand request, CancellationToken cancellationToken) =>
        programService.UnlinkProgramFromProductAsync(request.ProgramId, request.ProductId);
}
