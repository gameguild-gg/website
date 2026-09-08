using GameGuild.CQRS;

namespace GameGuild.Content.Pages;

public sealed record CreateContentResourceCommand(CreateContentResourceDto Resource) : ICommand<ContentResource>;
public sealed record UpdateContentResourceCommand(Guid ResourceId, UpdateContentResourceDto Resource) : ICommand<ContentResource?>;
public sealed record DeleteContentResourceCommand(Guid ResourceId) : ICommand<bool>;
public sealed record CreatePageCommand(CreatePageDto Page) : ICommand<Page>;
public sealed record UpdatePageCommand(Guid PageId, UpdatePageDto Page) : ICommand<Page?>;
public sealed record DeletePageCommand(Guid PageId) : ICommand<bool>;
public sealed record UnpublishPageCommand(Guid PageId) : ICommand<Page?>;
public sealed record CreatePageSectionCommand(Guid PageId, CreatePageSectionDto Section) : ICommand<PageSection>;
public sealed record UpdatePageSectionCommand(Guid PageId, Guid SectionId, UpdatePageSectionDto Section) : ICommand<PageSection?>;
public sealed record DeletePageSectionCommand(Guid PageId, Guid SectionId) : ICommand<bool>;
public sealed record ReorderPageSectionsCommand(Guid PageId, IReadOnlyList<Guid> OrderedIds) : ICommand;
public sealed record CreateMarketingLeadCommand(CreateMarketingLeadDto Lead) : ICommand<MarketingLead>;

public sealed class CreateContentResourceCommandHandler(IContentResourceService service)
    : ICommandHandler<CreateContentResourceCommand, ContentResource>
{
    public Task<ContentResource> Handle(CreateContentResourceCommand command, CancellationToken cancellationToken) =>
        service.CreateAsync(command.Resource, cancellationToken);
}

public sealed class UpdateContentResourceCommandHandler(IContentResourceService service)
    : ICommandHandler<UpdateContentResourceCommand, ContentResource?>
{
    public Task<ContentResource?> Handle(UpdateContentResourceCommand command, CancellationToken cancellationToken) =>
        service.UpdateAsync(command.ResourceId, command.Resource, cancellationToken);
}

public sealed class DeleteContentResourceCommandHandler(IContentResourceService service)
    : ICommandHandler<DeleteContentResourceCommand, bool>
{
    public Task<bool> Handle(DeleteContentResourceCommand command, CancellationToken cancellationToken) =>
        service.DeleteAsync(command.ResourceId, cancellationToken);
}

public sealed class CreatePageCommandHandler(IPageService service) : ICommandHandler<CreatePageCommand, Page>
{
    public Task<Page> Handle(CreatePageCommand command, CancellationToken cancellationToken) =>
        service.CreateAsync(command.Page, cancellationToken);
}

public sealed class UpdatePageCommandHandler(IPageService service) : ICommandHandler<UpdatePageCommand, Page?>
{
    public Task<Page?> Handle(UpdatePageCommand command, CancellationToken cancellationToken) =>
        service.UpdateAsync(command.PageId, command.Page, cancellationToken);
}

public sealed class DeletePageCommandHandler(IPageService service) : ICommandHandler<DeletePageCommand, bool>
{
    public Task<bool> Handle(DeletePageCommand command, CancellationToken cancellationToken) =>
        service.DeleteAsync(command.PageId, cancellationToken);
}

public sealed class UnpublishPageCommandHandler(IPageService service) : ICommandHandler<UnpublishPageCommand, Page?>
{
    public Task<Page?> Handle(UnpublishPageCommand command, CancellationToken cancellationToken) =>
        service.UnpublishAsync(command.PageId, cancellationToken);
}

public sealed class CreatePageSectionCommandHandler(IPageService service)
    : ICommandHandler<CreatePageSectionCommand, PageSection>
{
    public Task<PageSection> Handle(CreatePageSectionCommand command, CancellationToken cancellationToken) =>
        service.CreateSectionAsync(command.PageId, command.Section, cancellationToken);
}

public sealed class UpdatePageSectionCommandHandler(IPageService service)
    : ICommandHandler<UpdatePageSectionCommand, PageSection?>
{
    public async Task<PageSection?> Handle(UpdatePageSectionCommand command, CancellationToken cancellationToken)
    {
        var section = await service.UpdateSectionAsync(command.SectionId, command.Section, cancellationToken)
            .ConfigureAwait(false);
        return section?.PageId == command.PageId ? section : null;
    }
}

public sealed class DeletePageSectionCommandHandler(IPageService service)
    : ICommandHandler<DeletePageSectionCommand, bool>
{
    public async Task<bool> Handle(DeletePageSectionCommand command, CancellationToken cancellationToken)
    {
        var section = await service.GetSectionByIdAsync(command.SectionId, cancellationToken).ConfigureAwait(false);
        return section?.PageId == command.PageId
               && await service.DeleteSectionAsync(command.SectionId, cancellationToken).ConfigureAwait(false);
    }
}

public sealed class ReorderPageSectionsCommandHandler(IPageService service)
    : ICommandHandler<ReorderPageSectionsCommand>
{
    public async Task<Unit> Handle(ReorderPageSectionsCommand command, CancellationToken cancellationToken)
    {
        await service.ReorderSectionsAsync(command.PageId, command.OrderedIds, cancellationToken)
            .ConfigureAwait(false);
        return Unit.Value;
    }
}

public sealed class CreateMarketingLeadCommandHandler(IMarketingLeadService service)
    : ICommandHandler<CreateMarketingLeadCommand, MarketingLead>
{
    public Task<MarketingLead> Handle(CreateMarketingLeadCommand command, CancellationToken cancellationToken) =>
        service.CreateAsync(command.Lead, cancellationToken);
}
