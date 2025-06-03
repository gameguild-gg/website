using System.ComponentModel.DataAnnotations;

namespace cms.Modules.User.GraphQL;

public class CreateUserInput
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [EmailAddress]
    [StringLength(255)]
    public string Email { get; set; } = string.Empty;
    
    public bool IsActive { get; set; } = true;
}

public class UpdateUserInput
{
    [Required]
    public Guid Id { get; set; }
    
    [StringLength(100)]
    public string? Name { get; set; }
    
    [EmailAddress]
    [StringLength(255)]
    public string? Email { get; set; }
    
    public bool? IsActive { get; set; }
}
