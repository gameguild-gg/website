using System.ComponentModel.DataAnnotations;

namespace cms.Modules.User.Dtos;

public class CreateUserDto
{
    [Required]
    [StringLength(100)]
    public string Name
    {
        get;
        set;
    } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email
    {
        get;
        set;
    } = string.Empty;
}

public class UpdateUserDto
{
    [StringLength(100)]
    public string? Name
    {
        get;
        set;
    }

    [EmailAddress]
    [StringLength(255)]
    public string? Email
    {
        get;
        set;
    }
}

public class UserResponseDto
{
    public Guid Id
    {
        get;
        set;
    }

    public int Version
    {
        get;
        set;
    }

    public string Name
    {
        get;
        set;
    } = string.Empty;

    public string Email
    {
        get;
        set;
    } = string.Empty;

    public bool IsActive
    {
        get;
        set;
    }

    public DateTime CreatedAt
    {
        get;
        set;
    }

    public DateTime UpdatedAt
    {
        get;
        set;
    }

    public DateTime? DeletedAt
    {
        get;
        set;
    }

    public bool IsDeleted
    {
        get;
        set;
    }
}
