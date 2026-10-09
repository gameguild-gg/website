using GameGuild.CQRS;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GameGuild.Identity.Users;

/// <summary>
///     EntityBase Framework implementation of the User repository
/// </summary>
public class UserRepository(IApplicationDbContext context) : IUserRepository
{
    private readonly HashSet<User> _pendingUsers = [];
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().FirstOrDefaultAsync(u => u.Email == email && u.DeletedAt == null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken cancellationToken = default) { return await context.Set<User>().Where(u => u.DeletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false); }

    public async Task<(IEnumerable<User> Users, int TotalCount)> SearchAsync(string searchTerm, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.Set<User>().Where(u => u.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(searchTerm)) { query = query.Where(u => u.Name.Contains(searchTerm) || u.Email.Contains(searchTerm)); }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var users = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return (users, totalCount);
    }

    public Task AddAsync(User user) => AddAsync(user, CancellationToken.None);

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        if (user.Username is null)
        {
            user.AssignGeneratedUsername(UsernameSlug.Generate(user.Name));
        }
        else if (!user.HasGeneratedUsername)
        {
            user.Username = UsernameSlug.FromExplicit(user.Username);
        }

        await AllocateUsernameAsync(user, cancellationToken).ConfigureAwait(false);
        await context.Set<User>().AddAsync(user, cancellationToken).ConfigureAwait(false);
        _pendingUsers.Add(user);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Set<User>().Update(user);

        return Task.CompletedTask;
    }

    public Task DeleteAsync(User user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        user.SoftDelete(); // Soft delete
        context.Set<User>().Update(user);

        return Task.CompletedTask;
    }

    public Task SaveChangesAsync() => SaveChangesAsync(CancellationToken.None);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        // The application context increments entity versions and captures durable events before saving.
        // A failed username INSERT must not increment those versions twice when retried.
        var versions = (context as DbContext)?.ChangeTracker.Entries<EntityBase<Guid>>()
            .Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .ToDictionary(entry => entry.Entity, entry => entry.Entity.Version);
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUsernameConflict(exception))
        {
            if (versions is not null)
            {
                foreach (var (entity, version) in versions)
                {
                    entity.Version = version;
                }
            }

            var retry = false;
            foreach (var user in _pendingUsers)
            {
                if (!await UsernameIsReservedAsync(user.Username!, user, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (!user.HasGeneratedUsername)
                {
                    throw UsernameUnavailable();
                }

                await AllocateUsernameAsync(user, cancellationToken).ConfigureAwait(false);
                retry = true;
            }

            if (!retry)
            {
                throw UsernameUnavailable();
            }

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException retryException) when (IsUsernameConflict(retryException))
            {
                throw UsernameUnavailable();
            }
        }

        _pendingUsers.Clear();
    }

    private async Task AllocateUsernameAsync(User user, CancellationToken cancellationToken)
    {
        var slug = user.Username!;
        if (!await UsernameIsReservedAsync(slug, user, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        if (!user.HasGeneratedUsername)
        {
            throw UsernameUnavailable();
        }

        for (var attempt = 1; attempt <= 8; attempt++)
        {
            var candidate = UsernameSlug.WithDisambiguator(slug, user.Id, attempt);
            if (!await UsernameIsReservedAsync(candidate, user, cancellationToken).ConfigureAwait(false))
            {
                user.AssignGeneratedUsername(candidate);
                return;
            }
        }

        throw UsernameUnavailable();
    }

    private async Task<bool> UsernameIsReservedAsync(string username, User user, CancellationToken cancellationToken)
    {
        // Deleted users retain their handles under the existing unique index, and unsaved batch members
        // reserve handles too. Comparisons match the existing case-insensitive lookup contract.
        if (context.Set<User>().Local.Any(existing => existing.Id != user.Id
            && string.Equals(existing.Username, username, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var canonical = username.ToLowerInvariant();
        return await context.Set<User>().IgnoreQueryFilters()
            .AnyAsync(existing => existing.Id != user.Id && existing.Username != null
                && existing.Username.ToLower() == canonical, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsUsernameConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Users_Username"
        };

    private static RequestValidationException UsernameUnavailable() =>
        new([new ValidationError(nameof(User.Username), "Username is already in use. Choose a different username.")]);

    // Bulk operations
    public async Task<IEnumerable<User>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().Where(u => ids.Contains(u.Id) && u.DeletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<User>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().Where(u => emails.Contains(u.Email) && u.DeletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task AddRangeAsync(IEnumerable<User> users) => AddRangeAsync(users, CancellationToken.None);

    public async Task AddRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(users);
        foreach (var user in users)
        {
            await AddAsync(user, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UpdateRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        context.Set<User>().UpdateRange(users);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(users);

        foreach (var user in users)
        {
            user.SoftDelete(); // Assuming soft delete
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task<IEnumerable<User>> GetActiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().Where(u => u.IsActive && u.DeletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IEnumerable<User>> GetInactiveUsersAsync(CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().Where(u => !u.IsActive && u.DeletedAt == null).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(IEnumerable<User> Users, int TotalCount)> GetUsersPagedAsync(bool? isActive, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = context.Set<User>().Where(u => u.DeletedAt == null);

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var skip = (pageNumber - 1) * pageSize;
        var users = await query.Skip(skip).Take(pageSize).ToListAsync(cancellationToken).ConfigureAwait(false);

        return (users, totalCount);
    }

    public async Task<IDictionary<string, bool>> CheckEmailsExistAsync(IEnumerable<string> emails, CancellationToken cancellationToken = default)
    {
        var existingEmails = await context.Set<User>().Where(u => emails.Contains(u.Email) && u.DeletedAt == null).Select(u => u.Email).ToListAsync(cancellationToken).ConfigureAwait(false);

        return emails.ToDictionary(email => email, email => existingEmails.Contains(email));
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default) { return await context.Set<User>().AnyAsync(u => u.Id == id && u.DeletedAt == null, cancellationToken).ConfigureAwait(false); }

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>().AnyAsync(u => u.Email == email && u.DeletedAt == null, cancellationToken).ConfigureAwait(false);
    }

    public async Task PurgeAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Set<User>().Remove(user);
        await Task.CompletedTask;
    }

    public async Task PurgeRangeAsync(IEnumerable<User> users, CancellationToken cancellationToken = default)
    {
        context.Set<User>().RemoveRange(users);
        await Task.CompletedTask;
    }

    public IQueryable<User> GetQueryable()
    {
        return context.Set<User>().AsQueryable();
    }

    // ========================
    // AUTHENTICATION OPERATIONS (Merged from AuthUserRepository)
    // ========================

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return null;
        }

        var normalizedUsername = username.ToLowerInvariant();
        return await context.Set<User>()
            .FirstOrDefaultAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername && u.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<IReadOnlyList<User>> FindSignInCandidatesAsync(string identifier, SignInIdentifierType type) =>
        FindSignInCandidatesAsync(identifier, type, CancellationToken.None);

    public async Task<IReadOnlyList<User>> FindSignInCandidatesAsync(string identifier, SignInIdentifierType type, CancellationToken cancellationToken)
    {
        var normalized = identifier.ToLowerInvariant();
        var query = context.Set<User>().AsNoTracking().Where(user => user.DeletedAt == null);
        query = type switch
        {
            SignInIdentifierType.Email => query.Where(user => user.Email.ToLower() == normalized),
            SignInIdentifierType.Username => query.Where(user => user.Username != null && user.Username.ToLower() == normalized),
            SignInIdentifierType.Phone => query.Where(user => user.PhoneNumber == identifier),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return await query.Take(2).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        var normalizedUsername = username.ToLowerInvariant();
        return await context.Set<User>()
            .AnyAsync(u => u.Username != null && u.Username.ToLower() == normalizedUsername && u.DeletedAt == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> UpdatePasswordHashAsync(Guid userId, string passwordHash, string? expectedCurrentPasswordHash)
    {
        return UpdatePasswordHashAsync(userId, passwordHash, expectedCurrentPasswordHash, CancellationToken.None);
    }

    public async Task<bool> UpdatePasswordHashAsync(Guid userId, string passwordHash, string? expectedCurrentPasswordHash, CancellationToken cancellationToken)
    {
        var user = await GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || !string.Equals(user.PasswordHash, expectedCurrentPasswordHash, StringComparison.Ordinal))
        {
            return false;
        }

        user.SetPasswordHash(passwordHash);
        try
        {
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task RecordLoginAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user != null)
        {
            user.RecordLogin();
            await SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.Set<User>()
            .Where(u => u.Id == userId && u.DeletedAt == null)
            .Select(u => (int?)u.TokenVersion)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
