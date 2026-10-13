using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Command to create or replace a product's inventory configuration (issue #401).
///     Setting stock on a product that never tracked inventory switches tracking on
///     explicitly via <paramref name="TrackInventory"/> (default keeps current behavior
///     of untracked/unlimited for digital goods).
/// </summary>
public sealed record SetProductInventoryCommand(
    Guid ProductId,
    int StockQuantity,
    bool TrackInventory,
    int LowStockThreshold = 5,
    Guid? ActorUserId = null
) : ICommand<ProductInventoryDto>;

/// <summary>Validator for <see cref="SetProductInventoryCommand"/>.</summary>
public sealed class SetProductInventoryCommandValidator : AbstractValidator<SetProductInventoryCommand>
{
    public SetProductInventoryCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.StockQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.LowStockThreshold).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
///     Handler for <see cref="SetProductInventoryCommand"/>: creates or reconfigures the
///     product's inventory row, preserving outstanding reservations, and audits the change.
/// </summary>
public sealed class SetProductInventoryCommandHandler(
    IProductRepository productRepository,
    IProductInventoryRepository inventoryRepository,
    IProductCatalogAuditLogger auditLogger)
    : ICommandHandler<SetProductInventoryCommand, ProductInventoryDto>
{
    public async Task<ProductInventoryDto> Handle(SetProductInventoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProductNotFoundException(request.ProductId);

        var inventory = await inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (inventory is null)
        {
            inventory = ProductInventory.Create(
                request.ProductId,
                request.StockQuantity,
                request.TrackInventory,
                request.LowStockThreshold,
                product.TenantId);
            await inventoryRepository.AddAsync(inventory, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            inventory.Configure(request.StockQuantity, request.TrackInventory, request.LowStockThreshold);
            await inventoryRepository.UpdateAsync(inventory, cancellationToken).ConfigureAwait(false);
        }

        await inventoryRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(
            request.ActorUserId,
            "ConfigureInventory",
            "Inventory",
            inventory.Id,
            new { request.ProductId, request.StockQuantity, request.TrackInventory, request.LowStockThreshold },
            cancellationToken).ConfigureAwait(false);

        return new ProductInventoryDto(
            inventory.ProductId,
            inventory.TrackInventory,
            inventory.StockQuantity,
            inventory.ReservedQuantity,
            inventory.AvailableQuantity,
            inventory.LowStockThreshold);
    }
}

/// <summary>
///     Query for a product's real-time availability (issue #401). A product without an
///     inventory row (or with tracking disabled) is reported as always in stock, matching
///     the pre-existing digital-goods behavior.
/// </summary>
public sealed record GetProductAvailabilityQuery(Guid ProductId) : IQuery<ProductAvailabilityDto?>;

/// <summary>Handler for <see cref="GetProductAvailabilityQuery"/>.</summary>
public sealed class GetProductAvailabilityQueryHandler(
    IProductRepository productRepository,
    IProductInventoryRepository inventoryRepository)
    : IQueryHandler<GetProductAvailabilityQuery, ProductAvailabilityDto?>
{
    public async Task<ProductAvailabilityDto?> Handle(GetProductAvailabilityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await productRepository.GetByIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (product is null)
            return null;

        var inventory = await inventoryRepository.GetByProductIdAsync(request.ProductId, cancellationToken).ConfigureAwait(false);
        if (inventory is null || !inventory.TrackInventory)
        {
            return new ProductAvailabilityDto(
                request.ProductId,
                TrackInventory: false,
                StockQuantity: null,
                ReservedQuantity: null,
                AvailableQuantity: null,
                IsInStock: true,
                IsLowStock: false,
                LowStockThreshold: inventory?.LowStockThreshold ?? 0);
        }

        return new ProductAvailabilityDto(
            request.ProductId,
            inventory.TrackInventory,
            inventory.StockQuantity,
            inventory.ReservedQuantity,
            inventory.AvailableQuantity,
            inventory.IsInStock(),
            inventory.IsLowStock,
            inventory.LowStockThreshold);
    }
}
