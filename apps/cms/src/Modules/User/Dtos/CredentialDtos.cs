using System.ComponentModel.DataAnnotations;

namespace cms.Modules.User.Dtos;

/// <summary>
/// DTO for creating a new credential
/// </summary>
public class CreateCredentialDto
{
    /// <summary>
    /// Foreign key to the User entity
    /// </summary>
    [Required]
    public Guid UserId
    {
        get;
        set;
    }

    /// <summary>
    /// Type of credential (e.g., "password", "api_key", "oauth_token", "2fa_secret")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The credential value (hashed password, encrypted token, etc.)
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Value
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Additional metadata for the credential (JSON format)
    /// </summary>
    [MaxLength(2000)]
    public string? Metadata
    {
        get;
        set;
    }

    /// <summary>
    /// When this credential expires (optional)
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this credential is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for updating an existing credential
/// </summary>
public class UpdateCredentialDto
{
    /// <summary>
    /// Type of credential (e.g., "password", "api_key", "oauth_token", "2fa_secret")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Type
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The credential value (hashed password, encrypted token, etc.)
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string Value
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Additional metadata for the credential (JSON format)
    /// </summary>
    [MaxLength(2000)]
    public string? Metadata
    {
        get;
        set;
    }

    /// <summary>
    /// When this credential expires (optional)
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this credential is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    } = true;
}

/// <summary>
/// DTO for credential response
/// </summary>
public class CredentialResponseDto
{
    /// <summary>
    /// Unique identifier for the credential
    /// </summary>
    public Guid Id
    {
        get;
        set;
    }

    /// <summary>
    /// Foreign key to the User entity
    /// </summary>
    public Guid UserId
    {
        get;
        set;
    }

    /// <summary>
    /// Type of credential
    /// </summary>
    public string Type
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// The credential value (should be masked/redacted in responses)
    /// </summary>
    public string Value
    {
        get;
        set;
    } = string.Empty;

    /// <summary>
    /// Additional metadata for the credential
    /// </summary>
    public string? Metadata
    {
        get;
        set;
    }

    /// <summary>
    /// When this credential expires
    /// </summary>
    public DateTime? ExpiresAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether this credential is currently active
    /// </summary>
    public bool IsActive
    {
        get;
        set;
    }

    /// <summary>
    /// When this credential was last used
    /// </summary>
    public DateTime? LastUsedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Check if the credential is expired
    /// </summary>
    public bool IsExpired
    {
        get => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
    }

    /// <summary>
    /// Check if the credential is valid (active and not expired)
    /// </summary>
    public bool IsValid
    {
        get => IsActive && !IsExpired && !IsDeleted;
    }

    /// <summary>
    /// Version number for optimistic concurrency control
    /// </summary>
    public int Version
    {
        get;
        set;
    }

    /// <summary>
    /// When the credential was created
    /// </summary>
    public DateTime CreatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the credential was last updated
    /// </summary>
    public DateTime UpdatedAt
    {
        get;
        set;
    }

    /// <summary>
    /// When the credential was soft deleted (null if not deleted)
    /// </summary>
    public DateTime? DeletedAt
    {
        get;
        set;
    }

    /// <summary>
    /// Whether the credential is soft deleted
    /// </summary>
    public bool IsDeleted
    {
        get => DeletedAt.HasValue;
    }

    /// <summary>
    /// Associated user information
    /// </summary>
    public UserResponseDto? User
    {
        get;
        set;
    }
}
