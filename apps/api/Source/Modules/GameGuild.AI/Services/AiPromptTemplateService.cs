using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.AI;

internal sealed class AiPromptTemplateService(IApplicationDbContext db) : IAiPromptTemplateService
{
    private static readonly Regex KeyCleanupRegex = new("[^a-z0-9._-]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*(?<name>[A-Za-z0-9_.-]+)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<Result<IReadOnlyList<AiPromptTemplateDto>>> ListAsync(
        Guid tenantId,
        string? category = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = QueryForTenant(tenantId).AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(template => template.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = category.Trim();
            query = query.Where(template => template.Category == normalizedCategory);
        }

        var templates = await query
            .OrderByDescending(template => template.IsSystemTemplate)
            .ThenBy(template => template.Category)
            .ThenBy(template => template.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<AiPromptTemplateDto>>(templates.Select(ToDto).ToList());
    }

    public async Task<Result<AiPromptTemplateDto>> GetAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var template = await QueryForTenant(tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return template is null
            ? Result.Failure<AiPromptTemplateDto>(Error.NotFound("AI.PromptTemplateNotFound", "AI prompt template was not found."))
            : Result.Success(ToDto(template));
    }

    public async Task<Result<AiPromptTemplateDto>> CreateAsync(
        Guid tenantId,
        Guid? userId,
        CreateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateRequired(request.Key, request.Name, request.Prompt);
        if (validation.IsFailure)
        {
            return Result.Failure<AiPromptTemplateDto>(validation.Error);
        }

        var key = NormalizeKey(request.Key);
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure<AiPromptTemplateDto>(Error.Validation("AI.PromptTemplateKeyInvalid", "Prompt template key must contain at least one letter or number."));
        }

        if (await TenantTemplateKeyExistsAsync(tenantId, key, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<AiPromptTemplateDto>(Error.Conflict(
                "AI.PromptTemplateKeyConflict",
                $"A prompt template with key '{key}' already exists for this tenant."));
        }

        var template = new AiPromptTemplate
        {
            TenantId = tenantId,
            Key = key,
            Name = request.Name.Trim(),
            Description = NormalizeOptional(request.Description),
            Category = NormalizeCategory(request.Category),
            SystemPrompt = NormalizeOptional(request.SystemPrompt),
            Prompt = request.Prompt.Trim(),
            IsActive = request.IsActive ?? true,
            IsSystemTemplate = false,
            CreatedByUserId = userId,
            UpdatedByUserId = userId
        };

        await db.Set<AiPromptTemplate>().AddAsync(template, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ToDto(template));
    }

    public async Task<Result<AiPromptTemplateDto>> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        UpdateAiPromptTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = await EditableTemplateAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        if (template.IsFailure)
        {
            return Result.Failure<AiPromptTemplateDto>(template.Error);
        }

        var current = template.Value;

        if (request.Name is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Result.Failure<AiPromptTemplateDto>(Error.Validation("AI.PromptTemplateNameRequired", "Prompt template name is required."));
            }

            current.Name = request.Name.Trim();
        }

        if (request.Prompt is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
            {
                return Result.Failure<AiPromptTemplateDto>(Error.Validation("AI.PromptTemplatePromptRequired", "Prompt template text is required."));
            }

            current.Prompt = request.Prompt.Trim();
        }

        if (request.Description is not null)
        {
            current.Description = NormalizeOptional(request.Description);
        }

        if (request.Category is not null)
        {
            current.Category = NormalizeCategory(request.Category);
        }

        if (request.SystemPrompt is not null)
        {
            current.SystemPrompt = NormalizeOptional(request.SystemPrompt);
        }

        if (request.IsActive.HasValue)
        {
            current.IsActive = request.IsActive.Value;
        }

        current.UpdatedByUserId = userId;
        current.Touch();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success(ToDto(current));
    }

    public async Task<Result> DeleteAsync(
        Guid tenantId,
        Guid id,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var template = await EditableTemplateAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        if (template.IsFailure)
        {
            return Result.Failure(template.Error);
        }

        var current = template.Value;
        current.UpdatedByUserId = userId;
        current.DeletedAt = SystemClock.UtcNow;
        current.Touch();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    public async Task<Result<AiPromptTemplateRenderResponse>> RenderAsync(
        Guid tenantId,
        Guid id,
        IReadOnlyDictionary<string, string?>? variables,
        CancellationToken cancellationToken = default)
    {
        var template = await QueryForTenant(tenantId)
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return Result.Failure<AiPromptTemplateRenderResponse>(Error.NotFound("AI.PromptTemplateNotFound", "AI prompt template was not found."));
        }

        if (!template.IsActive)
        {
            return Result.Failure<AiPromptTemplateRenderResponse>(Error.Forbidden("AI.PromptTemplateInactive", "AI prompt template is inactive."));
        }

        var normalizedVariables = NormalizeVariables(variables);

        return Result.Success(new AiPromptTemplateRenderResponse(
            template.Id,
            template.Key,
            Render(template.SystemPrompt, normalizedVariables),
            Render(template.Prompt, normalizedVariables) ?? string.Empty,
            normalizedVariables));
    }

    private IQueryable<AiPromptTemplate> QueryForTenant(Guid tenantId)
        => db.Set<AiPromptTemplate>()
            .Where(template => template.TenantId == null || template.TenantId == tenantId);

    private async Task<Result<AiPromptTemplate>> EditableTemplateAsync(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await db.Set<AiPromptTemplate>()
            .FirstOrDefaultAsync(current => current.Id == id && current.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (template is null)
        {
            return Result.Failure<AiPromptTemplate>(Error.NotFound("AI.PromptTemplateNotFound", "AI prompt template was not found."));
        }

        if (template.IsSystemTemplate)
        {
            return Result.Failure<AiPromptTemplate>(Error.Forbidden("AI.SystemPromptTemplateReadOnly", "System prompt templates cannot be modified from this endpoint."));
        }

        return Result.Success(template);
    }

    private async Task<bool> TenantTemplateKeyExistsAsync(
        Guid tenantId,
        string key,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var query = db.Set<AiPromptTemplate>()
            .AsNoTracking()
            .Where(template => template.TenantId == tenantId && template.Key == key);

        if (exceptId.HasValue)
        {
            query = query.Where(template => template.Id != exceptId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Result ValidateRequired(string key, string name, string prompt)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Result.Failure(Error.Validation("AI.PromptTemplateKeyRequired", "Prompt template key is required."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("AI.PromptTemplateNameRequired", "Prompt template name is required."));
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Result.Failure(Error.Validation("AI.PromptTemplatePromptRequired", "Prompt template text is required."));
        }

        return Result.Success();
    }

    private static string NormalizeKey(string key)
    {
        var normalized = KeyCleanupRegex.Replace(key.Trim().ToLowerInvariant(), "-").Trim('-', '.', '_');
        return normalized.Length <= 128 ? normalized : normalized[..128].Trim('-', '.', '_');
    }

    private static string NormalizeCategory(string? category)
        => string.IsNullOrWhiteSpace(category) ? "General" : category.Trim();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyDictionary<string, string?> NormalizeVariables(IReadOnlyDictionary<string, string?>? variables)
        => variables is null
            ? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string?>(variables, StringComparer.OrdinalIgnoreCase);

    private static string? Render(string? template, IReadOnlyDictionary<string, string?> variables)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return null;
        }

        return PlaceholderRegex.Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            return variables.TryGetValue(name, out var value)
                ? value ?? string.Empty
                : match.Value;
        });
    }

    private static AiPromptTemplateDto ToDto(AiPromptTemplate template)
        => new(
            template.Id,
            template.TenantId,
            template.Key,
            template.Name,
            template.Description,
            template.Category,
            template.SystemPrompt,
            template.Prompt,
            template.IsActive,
            template.IsSystemTemplate,
            template.CreatedByUserId,
            template.UpdatedByUserId,
            template.CreatedAt,
            template.UpdatedAt);
}
