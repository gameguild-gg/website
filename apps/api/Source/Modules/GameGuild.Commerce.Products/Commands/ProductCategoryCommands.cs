using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
///     Exception thrown when a product category is not found (issue #401).
/// </summary>
public class ProductCategoryNotFoundException : Exception
{
    public ProductCategoryNotFoundException(Guid categoryId)
        : base($"Product category with ID '{categoryId}' was not found.")
    {
        CategoryId = categoryId;
    }

    public Guid CategoryId { get; }
}

/// <summary>
///     Exception thrown when a category mutation violates a hierarchy invariant
///     (cycle, depth, unknown parent) or uniqueness (duplicate slug/name).
/// </summary>
public class ProductCategoryValidationException : Exception
{
    public ProductCategoryValidationException(string message, CategoryValidationError error)
        : base(message)
    {
        Error = error;
    }

    public CategoryValidationError Error { get; }
}

/// <summary>
///     Command to create a product category (issue #401). The acting user comes from
///     the controller's actor context, never from the request body.
/// </summary>
public sealed record CreateProductCategoryCommand(
    string Name,
    string Slug,
    Guid? ParentCategoryId = null,
    string? Description = null,
    string? AttributesJson = null,
    int DisplayOrder = 0,
    Guid? TenantId = null,
    Guid? ActorUserId = null
) : ICommand<ProductCategoryDto>;

/// <summary>Validator for <see cref="CreateProductCategoryCommand"/>.</summary>
public sealed class CreateProductCategoryCommandValidator : AbstractValidator<CreateProductCategoryCommand>
{
    public CreateProductCategoryCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(150)
            .Matches("^[a-zA-Z0-9\\- ]+$").WithMessage("Slug may only contain letters, digits, hyphens and spaces.");
        RuleFor(x => x.Description).MaximumLength(1000);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

/// <summary>
///     Handler for <see cref="CreateProductCategoryCommand"/>: hierarchy validation
///     (parent exists, no cycle, depth cap), tenant-unique slug, audited.
/// </summary>
public sealed class CreateProductCategoryCommandHandler(
    IProductCategoryRepository categoryRepository,
    IProductCategoryValidator categoryValidator,
    IProductCatalogAuditLogger auditLogger)
    : ICommandHandler<CreateProductCategoryCommand, ProductCategoryDto>
{
    public async Task<ProductCategoryDto> Handle(CreateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await categoryRepository.GetBySlugAsync(request.Slug, request.TenantId, cancellationToken).ConfigureAwait(false) is not null)
        {
            throw new ProductCategoryValidationException(
                $"A category with slug '{ProductCategory.NormalizeSlug(request.Slug)}' already exists in this catalog",
                CategoryValidationError.None);
        }

        var categories = await categoryRepository.ListAsync(request.TenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var parentMap = categories.ToDictionary(c => c.Id, c => c.ParentCategoryId);
        if (request.ParentCategoryId.HasValue)
        {
            var validation = categoryValidator.ValidateParent(Guid.Empty, request.ParentCategoryId, parentMap);
            if (!validation.IsValid)
                throw new ProductCategoryValidationException(validation.ErrorMessage!, validation.Error!.Value);
        }

        var category = ProductCategory.Create(
            request.Name,
            request.Slug,
            request.ParentCategoryId,
            request.Description,
            request.AttributesJson,
            request.DisplayOrder,
            request.TenantId);

        await categoryRepository.AddAsync(category, cancellationToken).ConfigureAwait(false);
        await categoryRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(
            request.ActorUserId,
            "Create",
            "Category",
            category.Id,
            new { category.Name, category.Slug, category.ParentCategoryId },
            cancellationToken).ConfigureAwait(false);

        var categoriesById = categories.ToDictionary(c => c.Id);
        categoriesById[category.Id] = category;

        return category.ToDto(categoriesById);
    }
}

/// <summary>
///     Command to update a product category (rename, re-slug, reparent, attributes).
///     Guarded by optimistic concurrency via <see cref="ExpectedVersion"/>.
/// </summary>
public sealed record UpdateProductCategoryCommand(
    Guid CategoryId,
    string? Name = null,
    string? Slug = null,
    Guid? ParentCategoryId = null,
    bool Reparent = false,
    string? Description = null,
    string? AttributesJson = null,
    int? DisplayOrder = null,
    long? ExpectedVersion = null,
    Guid? ActorUserId = null
) : ICommand<ProductCategoryDto>;

/// <summary>Validator for <see cref="UpdateProductCategoryCommand"/>.</summary>
public sealed class UpdateProductCategoryCommandValidator : AbstractValidator<UpdateProductCategoryCommand>
{
    public UpdateProductCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).MaximumLength(150).When(x => x.Name is not null);
        RuleFor(x => x.Slug).MaximumLength(150)
            .Matches("^[a-zA-Z0-9\\- ]+$").WithMessage("Slug may only contain letters, digits, hyphens and spaces.")
            .When(x => x.Slug is not null);
        RuleFor(x => x.Description).MaximumLength(1000).When(x => x.Description is not null);
        RuleFor(x => x.DisplayOrder).GreaterThanOrEqualTo(0).When(x => x.DisplayOrder.HasValue);
    }
}

/// <summary>
///     Handler for <see cref="UpdateProductCategoryCommand"/>. Reparenting runs through
///     the hierarchy validator (cycle + depth) before any write.
/// </summary>
public sealed class UpdateProductCategoryCommandHandler(
    IProductCategoryRepository categoryRepository,
    IProductCategoryValidator categoryValidator,
    IProductCatalogAuditLogger auditLogger)
    : ICommandHandler<UpdateProductCategoryCommand, ProductCategoryDto>
{
    public async Task<ProductCategoryDto> Handle(UpdateProductCategoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProductCategoryNotFoundException(request.CategoryId);

        if (request.ExpectedVersion.HasValue && category.Version != request.ExpectedVersion.Value)
        {
            throw new ConcurrencyException(
                $"Category {request.CategoryId} has been modified by another user.");
        }

        var categories = await categoryRepository.ListAsync(category.TenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        var parentMap = categories.ToDictionary(c => c.Id, c => c.ParentCategoryId);
        var categoriesById = categories.ToDictionary(c => c.Id);
        categoriesById[category.Id] = category;

        if (request.Reparent && request.ParentCategoryId != category.ParentCategoryId)
        {
            var validation = categoryValidator.ValidateParent(category.Id, request.ParentCategoryId, parentMap);
            if (!validation.IsValid)
                throw new ProductCategoryValidationException(validation.ErrorMessage!, validation.Error!.Value);

            category.ParentCategoryId = request.ParentCategoryId;
            category.Touch();
        }

        if (!string.IsNullOrWhiteSpace(request.Name) || request.Description is not null)
            category.UpdateDetails(request.Name, request.Description);

        if (!string.IsNullOrWhiteSpace(request.Slug))
        {
            var normalized = ProductCategory.NormalizeSlug(request.Slug);
            if (normalized != category.Slug &&
                await categoryRepository.GetBySlugAsync(normalized, category.TenantId, cancellationToken).ConfigureAwait(false) is not null)
            {
                throw new ProductCategoryValidationException(
                    $"A category with slug '{normalized}' already exists in this catalog",
                    CategoryValidationError.None);
            }

            category.SetSlug(request.Slug);
        }

        // Passing a new JSON object replaces the attributes; an empty object ("{}") clears them.
        if (request.AttributesJson is not null)
        {
            category.SetAttributes(request.AttributesJson);
        }

        if (request.DisplayOrder.HasValue)
        {
            category.DisplayOrder = request.DisplayOrder.Value;
            category.Touch();
        }

        await categoryRepository.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
        await categoryRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(
            request.ActorUserId,
            "Update",
            "Category",
            category.Id,
            new { request.Name, request.Slug, request.ParentCategoryId, request.Reparent },
            cancellationToken).ConfigureAwait(false);

        return category.ToDto(categoriesById);
    }
}

/// <summary>
///     Command to soft-delete a product category. Rejected while the category still has
///     child categories or assigned products (use an explicit move first).
/// </summary>
public sealed record DeleteProductCategoryCommand(
    Guid CategoryId,
    Guid? ActorUserId = null
) : ICommand;

/// <summary>Validator for <see cref="DeleteProductCategoryCommand"/>.</summary>
public sealed class DeleteProductCategoryCommandValidator : AbstractValidator<DeleteProductCategoryCommand>
{
    public DeleteProductCategoryCommandValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
    }
}

/// <summary>Handler for <see cref="DeleteProductCategoryCommand"/>.</summary>
public sealed class DeleteProductCategoryCommandHandler(
    IProductCategoryRepository categoryRepository,
    IProductCatalogAuditLogger auditLogger)
    : ICommandHandler<DeleteProductCategoryCommand>
{
    public async Task Handle(DeleteProductCategoryCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var category = await categoryRepository.GetByIdAsync(request.CategoryId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProductCategoryNotFoundException(request.CategoryId);

        if (await categoryRepository.HasChildrenAsync(request.CategoryId, cancellationToken).ConfigureAwait(false))
        {
            throw new ProductCategoryValidationException(
                $"Category '{category.Name}' still has child categories; move or delete them first",
                CategoryValidationError.None);
        }

        var productCount = await categoryRepository.CountProductsInCategoryAsync(request.CategoryId, cancellationToken).ConfigureAwait(false);
        if (productCount > 0)
        {
            throw new ProductCategoryValidationException(
                $"Category '{category.Name}' still has {productCount} assigned products; reassign them first",
                CategoryValidationError.None);
        }

        category.SoftDelete();
        await categoryRepository.UpdateAsync(category, cancellationToken).ConfigureAwait(false);
        await categoryRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(
            request.ActorUserId,
            "Delete",
            "Category",
            category.Id,
            new { category.Name, category.Slug },
            cancellationToken).ConfigureAwait(false);
    }
}
