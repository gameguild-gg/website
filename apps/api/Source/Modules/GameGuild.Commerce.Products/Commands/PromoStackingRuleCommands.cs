using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Commerce.Products;

/// <summary>
/// Command to create a promo stacking rule
/// </summary>
/// <param name="Name">Rule name (unique)</param>
/// <param name="Description">Optional description</param>
/// <param name="IsActive">Whether the rule is active</param>
/// <param name="Priority">Priority 1-100 (higher wins when several rules are active)</param>
/// <param name="MaxStackableCount">Maximum codes that may stack (>= 1)</param>
/// <param name="AllowExclusiveStacking">Whether exclusive codes may stack</param>
/// <param name="MaxTotalDiscountPercentage">Cap on combined discount as a percentage of the order</param>
/// <param name="MaxTotalDiscountAmount">Cap on combined discount as an absolute amount</param>
/// <param name="ConflictStrategy">Conflict resolution strategy</param>
public sealed record CreatePromoStackingRuleCommand(
    string Name,
    string? Description = null,
    bool IsActive = true,
    int Priority = 1,
    int MaxStackableCount = 3,
    bool AllowExclusiveStacking = false,
    decimal? MaxTotalDiscountPercentage = null,
    decimal? MaxTotalDiscountAmount = null,
    ConflictResolutionStrategy ConflictStrategy = ConflictResolutionStrategy.Sequential
) : ICommand<PromoStackingRuleDto>;

/// <summary>
/// Handler for <see cref="CreatePromoStackingRuleCommand"/>
/// </summary>
public sealed class CreatePromoStackingRuleCommandHandler(
    IPromoStackingRuleRepository repository,
    IPromoCodeAuditLogger auditLogger)
    : ICommandHandler<CreatePromoStackingRuleCommand, PromoStackingRuleDto>
{
    /// <inheritdoc />
    public async Task<PromoStackingRuleDto> Handle(CreatePromoStackingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await repository.NameExistsAsync(request.Name, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Stacking rule '{request.Name}' already exists");
        }

        var rule = new PromoStackingRule
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            IsActive = request.IsActive,
            Priority = request.Priority,
            MaxStackableCount = request.MaxStackableCount,
            AllowExclusiveStacking = request.AllowExclusiveStacking,
            MaxTotalDiscountPercentage = request.MaxTotalDiscountPercentage,
            MaxTotalDiscountAmount = request.MaxTotalDiscountAmount,
            ConflictStrategy = request.ConflictStrategy
        };

        await repository.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(null, "StackingRuleCreated", rule.Id, rule.Name, new { rule.Priority, rule.ConflictStrategy }, cancellationToken)
            .ConfigureAwait(false);

        return rule.ToDto();
    }
}

/// <summary>
/// Command to update a promo stacking rule
/// </summary>
/// <param name="Id">Rule ID</param>
/// <param name="Description">Optional description</param>
/// <param name="IsActive">Whether the rule is active</param>
/// <param name="Priority">Priority 1-100</param>
/// <param name="MaxStackableCount">Maximum codes that may stack</param>
/// <param name="AllowExclusiveStacking">Whether exclusive codes may stack</param>
/// <param name="MaxTotalDiscountPercentage">Combined discount percentage cap</param>
/// <param name="MaxTotalDiscountAmount">Combined discount amount cap</param>
/// <param name="ConflictStrategy">Conflict resolution strategy</param>
public sealed record UpdatePromoStackingRuleCommand(
    Guid Id,
    string? Description = null,
    bool? IsActive = null,
    int? Priority = null,
    int? MaxStackableCount = null,
    bool? AllowExclusiveStacking = null,
    decimal? MaxTotalDiscountPercentage = null,
    decimal? MaxTotalDiscountAmount = null,
    ConflictResolutionStrategy? ConflictStrategy = null
) : ICommand<PromoStackingRuleDto>;

/// <summary>
/// Handler for <see cref="UpdatePromoStackingRuleCommand"/>
/// </summary>
public sealed class UpdatePromoStackingRuleCommandHandler(
    IPromoStackingRuleRepository repository,
    IPromoCodeAuditLogger auditLogger)
    : ICommandHandler<UpdatePromoStackingRuleCommand, PromoStackingRuleDto>
{
    /// <inheritdoc />
    public async Task<PromoStackingRuleDto> Handle(UpdatePromoStackingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new PromoStackingRuleNotFoundException(request.Id);

        if (request.Description != null) rule.Description = request.Description;
        if (request.IsActive.HasValue) rule.IsActive = request.IsActive.Value;
        if (request.Priority.HasValue) rule.Priority = request.Priority.Value;
        if (request.MaxStackableCount.HasValue) rule.MaxStackableCount = request.MaxStackableCount.Value;
        if (request.AllowExclusiveStacking.HasValue) rule.AllowExclusiveStacking = request.AllowExclusiveStacking.Value;
        if (request.MaxTotalDiscountPercentage.HasValue) rule.MaxTotalDiscountPercentage = request.MaxTotalDiscountPercentage.Value;
        if (request.MaxTotalDiscountAmount.HasValue) rule.MaxTotalDiscountAmount = request.MaxTotalDiscountAmount.Value;
        if (request.ConflictStrategy.HasValue) rule.ConflictStrategy = request.ConflictStrategy.Value;

        rule.Touch();
        await repository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(null, "StackingRuleUpdated", rule.Id, rule.Name, new { rule.IsActive, rule.Priority }, cancellationToken)
            .ConfigureAwait(false);

        return rule.ToDto();
    }
}

/// <summary>
/// Command to delete a promo stacking rule
/// </summary>
/// <param name="Id">Rule ID</param>
public sealed record DeletePromoStackingRuleCommand(Guid Id) : ICommand<bool>;

/// <summary>
/// Handler for <see cref="DeletePromoStackingRuleCommand"/>
/// </summary>
public sealed class DeletePromoStackingRuleCommandHandler(
    IPromoStackingRuleRepository repository,
    IPromoCodeAuditLogger auditLogger)
    : ICommandHandler<DeletePromoStackingRuleCommand, bool>
{
    /// <inheritdoc />
    public async Task<bool> Handle(DeletePromoStackingRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var rule = await repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);
        if (rule is null)
        {
            return false;
        }

        await repository.DeleteAsync(rule, cancellationToken).ConfigureAwait(false);
        await repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await auditLogger.LogMutationAsync(null, "StackingRuleDeleted", rule.Id, rule.Name, null, cancellationToken)
            .ConfigureAwait(false);

        return true;
    }
}

/// <summary>
/// Exception thrown when a promo stacking rule is not found
/// </summary>
public sealed class PromoStackingRuleNotFoundException(Guid ruleId)
    : Exception($"Promo stacking rule {ruleId} was not found");

/// <summary>
/// Validator for <see cref="CreatePromoStackingRuleCommand"/>
/// </summary>
public sealed class CreatePromoStackingRuleCommandValidator : AbstractValidator<CreatePromoStackingRuleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreatePromoStackingRuleCommandValidator"/> class.
    /// </summary>
    public CreatePromoStackingRuleCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Rule name is required.")
            .MaximumLength(200)
            .WithMessage("Rule name cannot exceed 200 characters.");

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 100)
            .WithMessage("Priority must be between 1 and 100.");

        RuleFor(x => x.MaxStackableCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Max stackable count must be at least 1.");

        RuleFor(x => x.MaxTotalDiscountPercentage)
            .InclusiveBetween(1m, 100m)
            .WithMessage("Max total discount percentage must be between 1 and 100.")
            .When(x => x.MaxTotalDiscountPercentage.HasValue);

        RuleFor(x => x.MaxTotalDiscountAmount)
            .GreaterThan(0)
            .WithMessage("Max total discount amount must be greater than 0.")
            .When(x => x.MaxTotalDiscountAmount.HasValue);
    }
}

/// <summary>
/// Validator for <see cref="UpdatePromoStackingRuleCommand"/>
/// </summary>
public sealed class UpdatePromoStackingRuleCommandValidator : AbstractValidator<UpdatePromoStackingRuleCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdatePromoStackingRuleCommandValidator"/> class.
    /// </summary>
    public UpdatePromoStackingRuleCommandValidator()
    {
        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 100)
            .WithMessage("Priority must be between 1 and 100.")
            .When(x => x.Priority.HasValue);

        RuleFor(x => x.MaxStackableCount)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Max stackable count must be at least 1.")
            .When(x => x.MaxStackableCount.HasValue);

        RuleFor(x => x.MaxTotalDiscountPercentage)
            .InclusiveBetween(1m, 100m)
            .WithMessage("Max total discount percentage must be between 1 and 100.")
            .When(x => x.MaxTotalDiscountPercentage.HasValue);

        RuleFor(x => x.MaxTotalDiscountAmount)
            .GreaterThan(0)
            .WithMessage("Max total discount amount must be greater than 0.")
            .When(x => x.MaxTotalDiscountAmount.HasValue);
    }
}
