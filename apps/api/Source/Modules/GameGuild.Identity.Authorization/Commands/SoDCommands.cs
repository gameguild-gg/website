using FluentValidation;
using GameGuild.CQRS;

namespace GameGuild.Identity.Authorization.Commands;

// ============================================================================
// Separation of Duties (SoD) Commands
// ============================================================================

/// <summary>
///     Command to create a new SoD rule
/// </summary>
public sealed record CreateSoDRuleCommand(
    string Name,
    string Description,
    string[] ConflictingPermissions,
    Guid? TenantId,
    SoDRuleType RuleType = SoDRuleType.PermissionConflict,
    bool IsEnabled = true
) : ICommand<SoDRule>;

public sealed class CreateSoDRuleValidator : AbstractValidator<CreateSoDRuleCommand>
{
    public CreateSoDRuleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConflictingPermissions).NotEmpty()
            .Must(p => p.Length >= 2)
            .WithMessage("At least two conflicting permissions are required");
    }
}

public sealed class CreateSoDRuleHandler(ISoDService service)
    : ICommandHandler<CreateSoDRuleCommand, SoDRule>
{
    public async Task<SoDRule> Handle(
        CreateSoDRuleCommand request,
        CancellationToken cancellationToken
    )
    {
        var rule = new SoDRule
        {
            Name = request.Name,
            Description = request.Description,
            ConflictingPermissions = System.Text.Json.JsonSerializer.Serialize(request.ConflictingPermissions),
            TenantId = request.TenantId,
            RuleType = request.RuleType,
            IsEnabled = request.IsEnabled
        };

        return await service.CreateRuleAsync(rule, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to update an existing SoD rule
/// </summary>
public sealed record UpdateSoDRuleCommand(
    Guid RuleId,
    string Name,
    string Description,
    string[] ConflictingPermissions,
    SoDRuleType RuleType,
    bool IsEnabled
) : ICommand<SoDRule?>;

public sealed class UpdateSoDRuleValidator : AbstractValidator<UpdateSoDRuleCommand>
{
    public UpdateSoDRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ConflictingPermissions).NotEmpty()
            .Must(p => p.Length >= 2)
            .WithMessage("At least two conflicting permissions are required");
    }
}

public sealed class UpdateSoDRuleHandler(ISoDService service)
    : ICommandHandler<UpdateSoDRuleCommand, SoDRule?>
{
    public async Task<SoDRule?> Handle(
        UpdateSoDRuleCommand request,
        CancellationToken cancellationToken
    )
    {
        var existing = await service.GetRuleByIdAsync(request.RuleId, cancellationToken).ConfigureAwait(false);
        if (existing == null)
        {
            return null;
        }

        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.ConflictingPermissions = System.Text.Json.JsonSerializer.Serialize(request.ConflictingPermissions);
        existing.RuleType = request.RuleType;
        existing.IsEnabled = request.IsEnabled;

        return await service.UpdateRuleAsync(existing, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to delete a SoD rule
/// </summary>
public sealed record DeleteSoDRuleCommand(Guid RuleId) : ICommand<bool>;

public sealed class DeleteSoDRuleValidator : AbstractValidator<DeleteSoDRuleCommand>
{
    public DeleteSoDRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
    }
}

public sealed class DeleteSoDRuleHandler(ISoDService service)
    : ICommandHandler<DeleteSoDRuleCommand, bool>
{
    public async Task<bool> Handle(
        DeleteSoDRuleCommand request,
        CancellationToken cancellationToken
    )
    {
        return await service.DeleteRuleAsync(request.RuleId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to resolve a SoD violation
/// </summary>
public sealed record ResolveSoDViolationCommand(
    Guid ViolationId,
    Guid ResolvedBy,
    SoDResolutionAction Action,
    string Notes
) : ICommand<SoDViolation>;

public sealed class ResolveSoDViolationValidator : AbstractValidator<ResolveSoDViolationCommand>
{
    public ResolveSoDViolationValidator()
    {
        RuleFor(x => x.ViolationId).NotEmpty();
        RuleFor(x => x.ResolvedBy).NotEmpty();
        RuleFor(x => x.Action).IsInEnum();
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(2000);
    }
}

public sealed class ResolveSoDViolationHandler(ISoDService service)
    : ICommandHandler<ResolveSoDViolationCommand, SoDViolation>
{
    public async Task<SoDViolation> Handle(
        ResolveSoDViolationCommand request,
        CancellationToken cancellationToken
    )
    {
        return await service.ResolveViolationAsync(
            request.ViolationId,
            request.ResolvedBy,
            request.Action,
            request.Notes,
            cancellationToken
        );
    }
}

/// <summary>
///     Command to grant an exception for a SoD violation
/// </summary>
public sealed record GrantSoDExceptionCommand(
    Guid ViolationId,
    Guid ApprovedBy,
    string Justification
) : ICommand<SoDViolation>;

public sealed class GrantSoDExceptionValidator : AbstractValidator<GrantSoDExceptionCommand>
{
    public GrantSoDExceptionValidator()
    {
        RuleFor(x => x.ViolationId).NotEmpty();
        RuleFor(x => x.ApprovedBy).NotEmpty();
        RuleFor(x => x.Justification).NotEmpty().MaximumLength(2000);
    }
}

public sealed class GrantSoDExceptionHandler(ISoDService service)
    : ICommandHandler<GrantSoDExceptionCommand, SoDViolation>
{
    public async Task<SoDViolation> Handle(
        GrantSoDExceptionCommand request,
        CancellationToken cancellationToken
    )
    {
        return await service.GrantExceptionAsync(
            request.ViolationId,
            request.ApprovedBy,
            request.Justification,
            cancellationToken
        ).ConfigureAwait(false);
    }
}

/// <summary>
///     Command to scan for SoD violations
/// </summary>
public sealed record ScanSoDViolationsCommand(Guid? TenantId) : ICommand<int>;

public sealed class ScanSoDViolationsHandler(ISoDService service)
    : ICommandHandler<ScanSoDViolationsCommand, int>
{
    public async Task<int> Handle(
        ScanSoDViolationsCommand request,
        CancellationToken cancellationToken
    )
    {
        return await service.ScanForViolationsAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
    }
}
