using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using GameGuild.Identity.Tenants;
using Microsoft.EntityFrameworkCore;

namespace GameGuild.Identity.Users;

/// <summary>
///     Represents a unified user in the system combining identity and authentication.
///     Inherits from EntityBase to provide GUID IDs, version control, timestamps, and soft delete functionality.
/// </summary>
/// <remarks>
///     <para>
///         <b>Unified Entity:</b> This entity combines identity and authentication concerns.
///         PasswordHash is nullable to support OAuth-only users.
///     </para>
///     <para>
///         <b>Related Entities (Same Module):</b> User has 1:1 relationships with:
///         <list type="bullet">
///             <item><see cref="UserProfile"/> - Extended profile information (bio, avatar, social links)</item>
///             <item><see cref="UserMetadata"/> - Custom fields, tags, external references</item>
///             <item><see cref="UserPreferences"/> - Notification, privacy, localization settings</item>
///         </list>
///         And 1:many with <see cref="UserNotification"/> for notification history.
///     </para>
///     <para>
///         <b>Cross-Module Relationship:</b> Users can belong to multiple tenants via the
///         <see cref="TenantMemberships"/> navigation property linking to <see cref="TenantMember"/>.
///     </para>
/// </remarks>
[Table("Users")]
[Index(nameof(Email), IsUnique = true)]
[Index(nameof(Username), IsUnique = true)]
public class User : EntityBase, IUser
{
    private string? _username;
    private bool _usernameWasGenerated;
    /// <summary>
    ///     Default constructor
    /// </summary>
    public User() { }

    /// <summary>
    ///     Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user data</param>
    public User(object partial) : base(partial) { }

    // ========================
    // IDENTITY FIELDS
    // ========================

    /// <summary>
    ///     Email address of the user (unique)
    /// </summary>
    [Required]
    [MaxLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    ///     Unique handle. Factories assign a canonical handle to new users; legacy null handles remain readable.
    /// </summary>
    [MaxLength(256)]
    public string? Username
    {
        get => _username;
        set
        {
            _username = value;
            _usernameWasGenerated = false;
        }
    }

    internal bool HasGeneratedUsername => _usernameWasGenerated;

    internal void AssignGeneratedUsername(string username)
    {
        _username = username;
        _usernameWasGenerated = true;
    }

    /// <summary>
    ///     Full name of the user
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    // ========================
    // AUTHENTICATION FIELDS
    // ========================

    /// <summary>
    ///     BCrypt password hash. Null for OAuth-only users.
    /// </summary>
    [JsonIgnore]
    [MaxLength(512)]
    public string? PasswordHash { get; set; }

    /// <summary>
    ///     Newline-delimited BCrypt hashes of the five most recently replaced passwords.
    ///     The hashes are never included in serialized user data.
    /// </summary>
    [JsonIgnore]
    [MaxLength(2600)]
    public string? PasswordHistoryHashes { get; private set; }

    /// <summary>
    ///     Whether the user's email has been verified
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    ///     Date and time of the user's last login
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    // ========================
    // STATUS FIELDS
    // ========================

    /// <summary>
    ///     Whether this user is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    ///     Whether this user is suspended
    /// </summary>
    public bool IsSuspended { get; set; }

    /// <summary>
    ///     Token version for immediate session invalidation.
    ///     Increment this when: user changes password, signs out all sessions,
    ///     or admin forces logout. JWT tokens with older versions are rejected.
    /// </summary>
    public int TokenVersion { get; set; } = 1;

    /// <summary>
    ///     Gets the current user status as a value object for rich status operations.
    ///     Not mapped to database - computed from IsActive and IsSuspended.
    /// </summary>
    [NotMapped]
    public UserStatus Status => new(IsActive, IsSuspended);

    // ========================
    // PROFILE FIELDS
    // ========================

    /// <summary>
    ///     Optional phone number
    /// </summary>
    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>
    ///     Date and time when the user was last seen/logged in
    /// </summary>
    public DateTime? LastSeenAt { get; set; }

    // ========================
    // NAVIGATION PROPERTIES
    // ========================

    /// <summary>
    ///     Extended profile information (bio, avatar, social links).
    ///     Lazy loaded, nullable if profile not yet created.
    /// </summary>
    public virtual UserProfile? Profile { get; set; }

    /// <summary>
    ///     Custom metadata (tags, external references, custom fields).
    ///     Lazy loaded, nullable if metadata not yet created.
    /// </summary>
    public virtual UserMetadata? Metadata { get; set; }

    /// <summary>
    ///     User preferences (notifications, privacy, localization).
    ///     Lazy loaded, nullable if preferences not yet created.
    /// </summary>
    public virtual UserPreferences? Preferences { get; set; }

    /// <summary>
    ///     Collection of notifications for this user.
    /// </summary>
    public virtual ICollection<UserNotification> Notifications { get; set; } = new List<UserNotification>();

    /// <summary>
    ///     Collection of tenant memberships for this user.
    ///     Provides direct navigation to all tenants the user belongs to.
    /// </summary>
    public virtual ICollection<TenantMember> TenantMemberships { get; set; } = new List<TenantMember>();

    // ========================
    // AUTHENTICATION METHODS
    // ========================

    /// <summary>
    ///     Set the password hash for the user
    /// </summary>
    /// <param name="passwordHash">BCrypt hash of the password</param>
    public void SetPasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var recentHashes = new List<string>(capacity: 5);
        if (!string.IsNullOrWhiteSpace(PasswordHash))
        {
            recentHashes.Add(PasswordHash);
        }

        recentHashes.AddRange(GetPasswordHistoryHashes().Take(4));
        PasswordHistoryHashes = recentHashes.Count == 0 ? null : string.Join('\n', recentHashes);

        PasswordHash = passwordHash;
        // Invalidate all existing tokens when password changes
        IncrementTokenVersion();
        Touch();
    }

    /// <summary>
    ///     Gets previously used password hashes, newest first.
    /// </summary>
    public IReadOnlyList<string> GetPasswordHistoryHashes()
    {
        if (string.IsNullOrWhiteSpace(PasswordHistoryHashes))
        {
            return Array.Empty<string>();
        }

        return PasswordHistoryHashes.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(5)
            .ToArray();
    }

    /// <summary>
    ///     Increments the token version to invalidate all existing JWT tokens.
    ///     Call this when: user changes password, signs out all sessions, or admin forces logout.
    /// </summary>
    public void IncrementTokenVersion()
    {
        TokenVersion++;
    }

    /// <summary>
    ///     Check if the user has a password set (vs OAuth-only)
    /// </summary>
    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    /// <summary>
    ///     Record a successful login
    /// </summary>
    public void RecordLogin()
    {
        LastLoginAt = SystemClock.UtcNow;
        LastSeenAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Mark email as verified
    /// </summary>
    public void VerifyEmail()
    {
        IsEmailVerified = true;
        Touch();
    }

    // ========================
    // STATUS METHODS
    // ========================

    /// <summary>
    ///     Activate the user
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    /// <summary>
    ///     Deactivate the user
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    /// <summary>
    ///     Suspend the user
    /// </summary>
    public void Suspend()
    {
        IsSuspended = true;
        Touch();
    }

    /// <summary>
    ///     Unsuspend the user
    /// </summary>
    public void Unsuspend()
    {
        IsSuspended = false;
        Touch();
    }

    // ========================
    // PROFILE METHODS
    // ========================

    /// <summary>
    ///     Update user information
    /// </summary>
    /// <param name="name">New name</param>
    /// <param name="phoneNumber">New phone number</param>
    public void UpdateInfo(string name, string? phoneNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        PhoneNumber = phoneNumber;
        Touch();
    }

    /// <summary>
    ///     Record user activity (last seen)
    /// </summary>
    public void RecordActivity()
    {
        LastSeenAt = SystemClock.UtcNow;
        Touch();
    }

    /// <summary>
    ///     Update the user's name
    /// </summary>
    /// <param name="name">New name</param>
    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Touch();
    }

    /// <summary>
    ///     Update the user's phone number
    /// </summary>
    /// <param name="phoneNumber">New phone number</param>
    public void UpdatePhoneNumber(string? phoneNumber)
    {
        PhoneNumber = phoneNumber;
        Touch();
    }

    // ========================
    // LIFECYCLE METHODS
    // ========================

    /// <summary>
    ///     Marks the user for deletion (soft delete).
    ///     User can be restored within the retention period.
    /// </summary>
    public void MarkDeleted()
    {
        SoftDelete();
        Deactivate();
    }

    /// <summary>
    ///     Restores a soft-deleted user.
    ///     Re-activates the user and clears deletion timestamp.
    /// </summary>
    public void RestoreUser()
    {
        Restore();
        Activate();
    }

    /// <summary>
    ///     Validates that the user can be permanently purged.
    ///     Throws if the user has active tenant memberships or other constraints.
    /// </summary>
    /// <exception cref="InvalidOperationException">If user cannot be purged due to constraints</exception>
    public void ValidatePurge()
    {
        if (!IsDeleted)
        {
            throw new InvalidOperationException("User must be soft-deleted before purging.");
        }

        if (TenantMemberships.Any(m => m.IsActive))
        {
            throw new InvalidOperationException("User has active tenant memberships. Remove memberships before purging.");
        }
    }

    /// <summary>
    ///     Checks if user can perform actions (active and not suspended)
    /// </summary>
    public bool CanPerformActions => Status.CanPerformActions;

    /// <summary>
    ///     Checks if user can sign in (active, even if suspended for warning display)
    /// </summary>
    public bool CanSignIn => Status.CanSignIn;

    // ========================
    // DOMAIN LOGIC METHODS
    // ========================

    /// <summary>
    ///     Validates that the user can authenticate with the provided token version.
    ///     Returns a result indicating success or the reason for failure.
    /// </summary>
    /// <param name="tokenVersion">The token version from the JWT.</param>
    /// <returns>Authentication validation result.</returns>
    public UserAuthenticationResult ValidateForAuthentication(int tokenVersion)
    {
        if (!IsActive)
        {
            return UserAuthenticationResult.Fail(UserAuthenticationFailure.Inactive);
        }

        if (IsSuspended)
        {
            return UserAuthenticationResult.Fail(UserAuthenticationFailure.Suspended);
        }

        if (TokenVersion != tokenVersion)
        {
            return UserAuthenticationResult.Fail(UserAuthenticationFailure.TokenRevoked);
        }

        return UserAuthenticationResult.Success();
    }

    /// <summary>
    ///     Validates that the user can be registered.
    ///     This method performs domain-level validation only; uniqueness is enforced at persistence level.
    /// </summary>
    /// <returns>Registration validation result.</returns>
    public UserRegistrationResult ValidateForRegistration()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Email))
        {
            errors.Add("Email is required.");
        }

        if (!Email.Contains('@'))
        {
            errors.Add("Email format is invalid.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("Name is required.");
        }

        if (Name.Length < 2)
        {
            errors.Add("Name must be at least 2 characters.");
        }

        if (errors.Count > 0)
        {
            return UserRegistrationResult.Failure(errors);
        }

        return UserRegistrationResult.Success();
    }

    /// <summary>
    ///     Validates that the user can join a new tenant.
    /// </summary>
    /// <param name="tenantId">The tenant to join.</param>
    /// <returns>True if the user can join, false with reason if not.</returns>
    public UserTenantJoinResult ValidateForTenantJoin(Guid tenantId)
    {
        if (!IsActive)
        {
            return UserTenantJoinResult.Failure("User account is inactive.");
        }

        if (IsSuspended)
        {
            return UserTenantJoinResult.Failure("User account is suspended.");
        }

        if (IsMemberOfTenant(tenantId))
        {
            return UserTenantJoinResult.Failure("User is already a member of this tenant.");
        }

        return UserTenantJoinResult.Success();
    }

    /// <summary>
    ///     Checks if the user requires email verification before performing an action.
    /// </summary>
    /// <param name="requiresVerification">Whether the action requires email verification.</param>
    /// <returns>True if verification is required but not completed.</returns>
    public bool RequiresEmailVerification(bool requiresVerification = true)
    {
        return requiresVerification && !IsEmailVerified;
    }

    // ========================
    // TENANT MEMBERSHIP METHODS
    // ========================

    /// <summary>
    ///     Gets the user's role in a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant to check</param>
    /// <returns>Role string if member, null otherwise</returns>
    public string? GetRoleInTenant(Guid tenantId)
    {
        return TenantMemberships
            .FirstOrDefault(m => m.TenantId == tenantId && m.IsActive)?
            .Role;
    }

    /// <summary>
    ///     Checks if user is a member of a specific tenant
    /// </summary>
    /// <param name="tenantId">The tenant to check</param>
    /// <returns>True if active member</returns>
    public bool IsMemberOfTenant(Guid tenantId)
    {
        return TenantMemberships.Any(m => m.TenantId == tenantId && m.IsActive);
    }

    /// <summary>
    ///     Gets all active tenant IDs for this user
    /// </summary>
    public IEnumerable<Guid> GetActiveTenantIds()
    {
        return TenantMemberships
            .Where(m => m.IsActive)
            .Select(m => m.TenantId);
    }

    // ========================
    // FACTORY METHODS
    // ========================

    /// <summary>
    ///     Static factory method to create a new user with password authentication
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="name">User's full name</param>
    /// <param name="passwordHash">BCrypt password hash</param>
    /// <param name="username">Optional username</param>
    /// <returns>New User instance</returns>
    public static User CreateWithPassword(string email, string name, string passwordHash, string? username = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            Name = name,
            PasswordHash = passwordHash,
            IsActive = true
        };
        if (username is null)
        {
            user.AssignGeneratedUsername(UsernameSlug.Generate(name));
        }
        else
        {
            user.Username = UsernameSlug.FromExplicit(username);
        }

        return user;
    }

    /// <summary>
    ///     Static factory method to create a new OAuth-only user
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="name">User's full name</param>
    /// <returns>New User instance without password</returns>
    public static User CreateOAuthUser(string email, string name) => CreateOAuthUser(email, name, emailVerified: true);

    /// <summary>
    ///     Static factory method to create an OAuth-only user and preserve the provider's email verification status
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="name">User's full name</param>
    /// <param name="emailVerified">Whether the external provider verified the email address</param>
    /// <returns>New User instance without password</returns>
    public static User CreateOAuthUser(string email, string name, bool emailVerified)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var user = new User
        {
            Email = email.ToLowerInvariant(),
            Name = name,
            PasswordHash = null, // OAuth-only user
            IsActive = true,
            IsEmailVerified = emailVerified
        };
        user.AssignGeneratedUsername(UsernameSlug.Generate(name));
        return user;
    }

    /// <summary>
    ///     Static factory method to create a new user (legacy compatibility)
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="name">User's full name</param>
    /// <param name="phoneNumber">Optional phone number</param>
    /// <returns>New User instance</returns>
    public static User Create(string email, string name, string? phoneNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var user = new User { Email = email.ToLowerInvariant(), Name = name, PhoneNumber = phoneNumber, IsActive = true };
        user.AssignGeneratedUsername(UsernameSlug.Generate(name));
        return user;
    }
}
