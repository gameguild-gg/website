namespace GameGuild;

public sealed record UserCreatedEvent(
    [property: NonPersonalEventData] Guid UserId = default) : DurableIntegrationEventBase
{
    public override string EventName => "identity.user.created.v1";
    public override string SourceModule => "Identity.Users";
}

public sealed record UserDeletedEvent(
    [property: NonPersonalEventData] Guid UserId = default) : DurableIntegrationEventBase
{
    public override string EventName => "identity.user.deleted.v1";
    public override string SourceModule => "Identity.Users";
}

public sealed record PropertyCreatedEvent(
    [property: NonPersonalEventData] Guid PropertyId = default) : DurableIntegrationEventBase
{
    public override string EventName => "real-estate.property.created.v1";
    public override string SourceModule => "RealEstate";
}

public sealed record PropertyDeletedEvent(
    [property: NonPersonalEventData] Guid PropertyId = default) : DurableIntegrationEventBase
{
    public override string EventName => "real-estate.property.deleted.v1";
    public override string SourceModule => "RealEstate";
}

public sealed record PropertyMediaAttachedEvent(
    [property: NonPersonalEventData] Guid PropertyId = default,
    [property: NonPersonalEventData] Guid MediaId = default,
    [property: NonPersonalEventData] string MediaKind = "unknown") : DurableIntegrationEventBase
{
    public override string EventName => "real-estate.property-media.attached.v1";
    public override string SourceModule => "RealEstate";
}

public sealed record PropertyMediaRemovedEvent(
    [property: NonPersonalEventData] Guid PropertyId = default,
    [property: NonPersonalEventData] Guid MediaId = default,
    [property: NonPersonalEventData] string MediaKind = "unknown") : DurableIntegrationEventBase
{
    public override string EventName => "real-estate.property-media.removed.v1";
    public override string SourceModule => "RealEstate";
}

public sealed record AssetReferenceCreatedEvent(
    [property: NonPersonalEventData] Guid AssetReferenceId = default,
    [property: NonPersonalEventData] Guid AssetContentId = default,
    [property: NonPersonalEventData] string ContentHash = "unknown",
    [property: NonPersonalEventData] long ByteCount = 0) : DurableIntegrationEventBase
{
    public override string EventName => "assets.reference.created.v1";
    public override string SourceModule => "Assets";
}

public sealed record AssetReferenceRemovedEvent(
    [property: NonPersonalEventData] Guid AssetReferenceId = default,
    [property: NonPersonalEventData] Guid AssetContentId = default) : DurableIntegrationEventBase
{
    public override string EventName => "assets.reference.removed.v1";
    public override string SourceModule => "Assets";
}

public sealed record AssetObjectStoredEvent(
    [property: NonPersonalEventData] Guid AssetContentId = default,
    [property: NonPersonalEventData] string ContentHash = "unknown",
    [property: NonPersonalEventData] long ByteCount = 0,
    [property: NonPersonalEventData] string StorageUnit = "unknown") : DurableIntegrationEventBase
{
    public override string EventName => "assets.object.stored.v1";
    public override string SourceModule => "Assets";
}

public sealed record AssetObjectDeletedEvent(
    [property: NonPersonalEventData] Guid AssetContentId = default,
    [property: NonPersonalEventData] string ContentHash = "unknown",
    [property: NonPersonalEventData] long ByteCount = 0,
    [property: NonPersonalEventData] string StorageUnit = "unknown") : DurableIntegrationEventBase
{
    public override string EventName => "assets.object.deleted.v1";
    public override string SourceModule => "Assets";
}

public sealed record AssetTransformedEvent(
    [property: NonPersonalEventData] Guid SourceAssetContentId = default,
    [property: NonPersonalEventData] Guid TransformedAssetId = default,
    [property: NonPersonalEventData] string TransformDescriptor = "unknown",
    [property: NonPersonalEventData] long ByteCount = 0) : DurableIntegrationEventBase
{
    public override string EventName => "assets.transformed.v1";
    public override string SourceModule => "Assets";
}

public sealed record AssetServedEvent(
    [property: NonPersonalEventData] Guid AssetReferenceId = default,
    [property: NonPersonalEventData] Guid AssetContentId = default,
    [property: NonPersonalEventData] long ByteCount = 0,
    [property: NonPersonalEventData] string DeliveryUnit = "download") : DurableIntegrationEventBase
{
    public override string EventName => "assets.served.v1";
    public override string SourceModule => "Assets";
}

public sealed record ApiRequestMeasuredEventV1(
    [property: NonPersonalEventData] string Method = "UNKNOWN",
    [property: NonPersonalEventData] string RouteTemplate = "unknown",
    [property: NonPersonalEventData] int StatusCode = 0,
    [property: NonPersonalEventData] decimal DurationMilliseconds = 0,
    [property: NonPersonalEventData] decimal DatabaseMilliseconds = 0,
    [property: NonPersonalEventData] long CacheOperations = 0,
    [property: NonPersonalEventData] long TransferredBytes = 0) : DurableIntegrationEventBase
{
    public override string EventName => "platform.api-request.measured.v1";
    public override string SourceModule => "Platform.Http";
}
