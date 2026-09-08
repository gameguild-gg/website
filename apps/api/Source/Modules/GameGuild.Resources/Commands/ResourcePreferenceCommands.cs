using GameGuild.CQRS;

namespace GameGuild.Resources;

public sealed record SetTenantResourceSettingCommand(Guid TenantId, string Key, SetResourceSettingsRequest Setting) : ICommand<ResourceSettings>;
public sealed record DeleteTenantResourceSettingCommand(Guid TenantId, string Key) : ICommand<bool>;
public sealed record SetUserResourceSettingCommand(Guid UserId, string Key, SetUserResourceSettingsRequest Setting) : ICommand<ResourceSettings>;
public sealed record SetTenantResourceMetadataCommand(Guid TenantId, string Key, SetResourceMetadataRequest Metadata) : ICommand<ResourceMetadata>;
public sealed record DeleteTenantResourceMetadataCommand(Guid TenantId, string Key) : ICommand<bool>;
public sealed record SetUserResourceMetadataCommand(Guid UserId, string Key, SetResourceMetadataRequest Metadata) : ICommand<ResourceMetadata>;

public sealed class SetTenantResourceSettingCommandHandler(IResourceSettingsRepository repository)
    : ICommandHandler<SetTenantResourceSettingCommand, ResourceSettings>
{
    public async Task<ResourceSettings> Handle(SetTenantResourceSettingCommand command, CancellationToken cancellationToken)
    {
        var body = command.Setting;
        var setting = await repository.GetByKeyAsync(command.TenantId, command.Key, cancellationToken)
            .ConfigureAwait(false);
        if (setting is not null)
        {
            setting.Value = body.Value;
            setting.DefaultValue = body.DefaultValue ?? setting.DefaultValue;
            setting.DataType = body.DataType ?? setting.DataType;
            setting.Description = body.Description ?? setting.Description;
            setting.Category = body.Category ?? setting.Category;
            setting.AllowUserOverride = body.AllowUserOverride ?? setting.AllowUserOverride;
            setting.DisplayOrder = body.DisplayOrder ?? setting.DisplayOrder;
            setting.ValidationRules = body.ValidationRules ?? setting.ValidationRules;
            setting.Touch();
            await repository.UpdateAsync(setting, cancellationToken).ConfigureAwait(false);
            return setting;
        }

        setting = new ResourceSettings
        {
            Key = command.Key,
            Value = body.Value,
            DefaultValue = body.DefaultValue,
            DataType = body.DataType ?? "String",
            Description = body.Description,
            Category = body.Category,
            AllowUserOverride = body.AllowUserOverride ?? true,
            DisplayOrder = body.DisplayOrder ?? 0,
            ValidationRules = body.ValidationRules,
            IsActive = true
        };
        SetTenantId(setting, command.TenantId);
        await repository.CreateAsync(setting, cancellationToken).ConfigureAwait(false);
        return setting;
    }

    internal static void SetTenantId(EntityBase entity, Guid tenantId)
    {
        var property = entity.GetType().GetProperty(nameof(EntityBase.TenantId));
        property?.GetSetMethod(nonPublic: true)?.Invoke(entity, [tenantId]);
    }
}

public sealed class DeleteTenantResourceSettingCommandHandler(IResourceSettingsRepository repository)
    : ICommandHandler<DeleteTenantResourceSettingCommand, bool>
{
    public Task<bool> Handle(DeleteTenantResourceSettingCommand command, CancellationToken cancellationToken) =>
        repository.DeleteByKeyAsync(command.TenantId, command.Key, cancellationToken);
}

public sealed class SetUserResourceSettingCommandHandler(IResourceSettingsRepository repository)
    : ICommandHandler<SetUserResourceSettingCommand, ResourceSettings>
{
    public async Task<ResourceSettings> Handle(SetUserResourceSettingCommand command, CancellationToken cancellationToken)
    {
        var setting = await repository.GetByUserKeyAsync(command.UserId, command.Key, cancellationToken)
            .ConfigureAwait(false);
        if (setting is not null)
        {
            setting.Value = command.Setting.Value;
            setting.Touch();
            await repository.UpdateAsync(setting, cancellationToken).ConfigureAwait(false);
            return setting;
        }

        setting = new ResourceSettings
        {
            UserId = command.UserId,
            Key = command.Key,
            Value = command.Setting.Value,
            IsActive = true
        };
        await repository.CreateAsync(setting, cancellationToken).ConfigureAwait(false);
        return setting;
    }
}

public sealed class SetTenantResourceMetadataCommandHandler(IResourceMetadataRepository repository)
    : ICommandHandler<SetTenantResourceMetadataCommand, ResourceMetadata>
{
    public async Task<ResourceMetadata> Handle(SetTenantResourceMetadataCommand command, CancellationToken cancellationToken)
    {
        var body = command.Metadata;
        var metadata = await repository.GetByKeyAsync(command.TenantId, command.Key, cancellationToken)
            .ConfigureAwait(false);
        if (metadata is not null)
        {
            Apply(metadata, body);
            await repository.UpdateAsync(metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }

        metadata = New(command.Key, body);
        SetTenantResourceSettingCommandHandler.SetTenantId(metadata, command.TenantId);
        await repository.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);
        return metadata;
    }

    internal static void Apply(ResourceMetadata metadata, SetResourceMetadataRequest body)
    {
        metadata.Value = body.Value;
        metadata.DataType = body.DataType ?? metadata.DataType;
        metadata.Description = body.Description ?? metadata.Description;
        metadata.Category = body.Category ?? metadata.Category;
        metadata.DisplayOrder = body.DisplayOrder ?? metadata.DisplayOrder;
        metadata.Touch();
    }

    internal static ResourceMetadata New(string key, SetResourceMetadataRequest body) => new()
    {
        Key = key,
        Value = body.Value,
        DataType = body.DataType ?? "String",
        Description = body.Description,
        Category = body.Category,
        DisplayOrder = body.DisplayOrder ?? 0,
        IsActive = true
    };
}

public sealed class DeleteTenantResourceMetadataCommandHandler(IResourceMetadataRepository repository)
    : ICommandHandler<DeleteTenantResourceMetadataCommand, bool>
{
    public Task<bool> Handle(DeleteTenantResourceMetadataCommand command, CancellationToken cancellationToken) =>
        repository.DeleteByKeyAsync(command.TenantId, command.Key, cancellationToken);
}

public sealed class SetUserResourceMetadataCommandHandler(IResourceMetadataRepository repository)
    : ICommandHandler<SetUserResourceMetadataCommand, ResourceMetadata>
{
    public async Task<ResourceMetadata> Handle(SetUserResourceMetadataCommand command, CancellationToken cancellationToken)
    {
        var metadata = await repository.GetByUserKeyAsync(command.UserId, command.Key, cancellationToken)
            .ConfigureAwait(false);
        if (metadata is not null)
        {
            SetTenantResourceMetadataCommandHandler.Apply(metadata, command.Metadata);
            await repository.UpdateAsync(metadata, cancellationToken).ConfigureAwait(false);
            return metadata;
        }

        metadata = SetTenantResourceMetadataCommandHandler.New(command.Key, command.Metadata);
        metadata.UserId = command.UserId;
        await repository.CreateAsync(metadata, cancellationToken).ConfigureAwait(false);
        return metadata;
    }
}
