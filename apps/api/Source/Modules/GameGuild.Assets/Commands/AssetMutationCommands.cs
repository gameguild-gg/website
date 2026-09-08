using GameGuild.CQRS;

namespace GameGuild.Assets.Commands;

public sealed record CreateAssetFolderCommand(
    string ResourceType,
    Guid ResourceId,
    Guid UserId,
    Guid? TenantId,
    string Name,
    Guid? ParentFolderId) : ICommand<AssetLibraryResult<AssetFolder>>;
public sealed record RestrictAssetFolderCommand(
    Guid FolderId,
    Guid UserId,
    Guid? TenantId,
    AssetFolderRestrictionMode Mode,
    IReadOnlyCollection<Guid> TeamIds,
    IReadOnlyCollection<string> Authorities) : ICommand<AssetLibraryResult<AssetFolder>>;
public sealed record CopyAssetReferenceCommand(
    Guid ReferenceId,
    Guid UserId,
    Guid? TenantId,
    string? DisplayName,
    Guid? FolderId) : ICommand<AssetLibraryResult<AssetReference>>;
public sealed record RestoreAssetRevisionCommand(
    Guid ReferenceId,
    Guid RevisionId,
    Guid UserId,
    Guid? TenantId) : ICommand<AssetLibraryResult<AssetReferenceRevision>>;
public sealed record InitiateChunkedAssetUploadCommand(
    string FileName,
    string MimeType,
    long TotalSize,
    Guid UserId) : ICommand<ChunkedUploadSession>;
public sealed record UploadAssetChunkCommand(
    string UploadId,
    int ChunkIndex,
    Stream Content) : ICommand<bool>;
public sealed record CompleteChunkedAssetUploadCommand(
    string UploadId,
    UploadAssetOptions Options) : ICommand<AssetUploadResult>;
public sealed record AbortChunkedAssetUploadCommand(string UploadId) : ICommand;
public sealed record UpdateAssetVirusScanStatusCommand(
    Guid ContentId,
    VirusScanStatus Status,
    string? ScanResult) : ICommand<AssetContent?>;
public sealed record ReviewAssetContentModerationCommand(
    Guid ContentId,
    ModerationStatus Status,
    Guid ReviewedBy,
    string[]? Labels,
    string? Notes) : ICommand<AssetContent?>;

public sealed class AssetMutationCommandHandler(
    IAssetLibraryService libraryService,
    IAssetUploadService uploadService,
    IAssetContentRepository contentRepository) :
    ICommandHandler<CreateAssetFolderCommand, AssetLibraryResult<AssetFolder>>,
    ICommandHandler<RestrictAssetFolderCommand, AssetLibraryResult<AssetFolder>>,
    ICommandHandler<CopyAssetReferenceCommand, AssetLibraryResult<AssetReference>>,
    ICommandHandler<RestoreAssetRevisionCommand, AssetLibraryResult<AssetReferenceRevision>>,
    ICommandHandler<InitiateChunkedAssetUploadCommand, ChunkedUploadSession>,
    ICommandHandler<UploadAssetChunkCommand, bool>,
    ICommandHandler<CompleteChunkedAssetUploadCommand, AssetUploadResult>,
    ICommandHandler<AbortChunkedAssetUploadCommand>,
    ICommandHandler<UpdateAssetVirusScanStatusCommand, AssetContent?>,
    ICommandHandler<ReviewAssetContentModerationCommand, AssetContent?>
{
    public Task<AssetLibraryResult<AssetFolder>> Handle(
        CreateAssetFolderCommand command,
        CancellationToken cancellationToken) =>
        libraryService.CreateFolderAsync(
            command.ResourceType,
            command.ResourceId,
            command.UserId,
            command.TenantId,
            command.Name,
            command.ParentFolderId,
            cancellationToken);

    public Task<AssetLibraryResult<AssetFolder>> Handle(
        RestrictAssetFolderCommand command,
        CancellationToken cancellationToken) =>
        libraryService.RestrictFolderAsync(
            command.FolderId,
            command.UserId,
            command.TenantId,
            command.Mode,
            command.TeamIds,
            command.Authorities,
            cancellationToken);

    public Task<AssetLibraryResult<AssetReference>> Handle(
        CopyAssetReferenceCommand command,
        CancellationToken cancellationToken) =>
        libraryService.CopyAsync(
            command.ReferenceId,
            command.UserId,
            command.TenantId,
            command.DisplayName,
            command.FolderId,
            cancellationToken);

    public Task<AssetLibraryResult<AssetReferenceRevision>> Handle(
        RestoreAssetRevisionCommand command,
        CancellationToken cancellationToken) =>
        libraryService.RestoreRevisionAsync(
            command.ReferenceId,
            command.RevisionId,
            command.UserId,
            command.TenantId,
            cancellationToken);

    public Task<ChunkedUploadSession> Handle(
        InitiateChunkedAssetUploadCommand command,
        CancellationToken cancellationToken) =>
        uploadService.InitiateChunkedUploadAsync(
            command.FileName,
            command.MimeType,
            command.TotalSize,
            command.UserId,
            cancellationToken);

    public Task<bool> Handle(UploadAssetChunkCommand command, CancellationToken cancellationToken) =>
        uploadService.UploadChunkAsync(command.UploadId, command.ChunkIndex, command.Content, cancellationToken);

    public Task<AssetUploadResult> Handle(
        CompleteChunkedAssetUploadCommand command,
        CancellationToken cancellationToken) =>
        uploadService.CompleteChunkedUploadAsync(command.UploadId, command.Options, cancellationToken);

    public async Task<Unit> Handle(AbortChunkedAssetUploadCommand command, CancellationToken cancellationToken)
    {
        await uploadService.AbortChunkedUploadAsync(command.UploadId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }

    public async Task<AssetContent?> Handle(
        UpdateAssetVirusScanStatusCommand command,
        CancellationToken cancellationToken)
    {
        var content = await contentRepository.GetByIdAsync(command.ContentId, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return null;
        content.SetVirusScanStatus(command.Status, command.ScanResult);
        await contentRepository.UpdateAsync(content, cancellationToken).ConfigureAwait(false);
        return content;
    }

    public async Task<AssetContent?> Handle(
        ReviewAssetContentModerationCommand command,
        CancellationToken cancellationToken)
    {
        var content = await contentRepository.GetByIdAsync(command.ContentId, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return null;
        content.SetModerationStatus(command.Status, command.ReviewedBy, command.Labels, command.Notes);
        await contentRepository.UpdateAsync(content, cancellationToken).ConfigureAwait(false);
        return content;
    }
}
