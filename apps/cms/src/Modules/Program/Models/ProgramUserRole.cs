using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Program.Models;

/// <summary>
/// Junction entity representing role assignments for users within programs
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
[Table("program_user_roles")]
public class ProgramUserRole : BaseEntity
{
    /// <summary>
    /// Foreign key to the Program entity
    /// </summary>
    [Required]
    public Guid ProgramId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the Program entity
    /// </summary>
    [ForeignKey(nameof(ProgramId))]
    public virtual Program Program
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key to the ProgramUser entity
    /// </summary>
    [Required]
    public Guid ProgramUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the ProgramUser entity
    /// </summary>
    [ForeignKey(nameof(ProgramUserId))]
    public virtual ProgramUser ProgramUser
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// The role assigned to the user within this program
    /// </summary>
    public ProgramRoleType Role
    {
        get;
        set;
    }

    /// <summary>
    /// Date when this role becomes active (null = immediately active)
    /// </summary>
    public DateTime? ActiveFrom
    {
        get;
        set;
    }

    /// <summary>
    /// Date when this role expires (null = no expiration)
    /// </summary>
    public DateTime? ActiveUntil
    {
        get;
        set;
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public ProgramUserRole() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial program user role data</param>
    public ProgramUserRole(object partial) : base(partial) { }

    /// <summary>
    /// Check if the role is currently active based on date constraints
    /// </summary>
    public bool IsCurrentlyActive()
    {
        var now = DateTime.UtcNow;
        return (ActiveFrom == null || ActiveFrom <= now) &&
               (ActiveUntil == null || ActiveUntil > now);
    }

    /// <summary>
    /// Set the role as active from now
    /// </summary>
    public void ActivateNow()
    {
        ActiveFrom = DateTime.UtcNow;
        Touch();
    }

    /// <summary>
    /// Set the role to expire at a specific date
    /// </summary>
    public void ExpireAt(DateTime expireDate)
    {
        ActiveUntil = expireDate;
        Touch();
    }
}

/// <summary>
/// Entity Framework configuration for ProgramUserRole entity
/// </summary>
public class ProgramUserRoleConfiguration : IEntityTypeConfiguration<ProgramUserRole>
{
    public void Configure(EntityTypeBuilder<ProgramUserRole> builder)
    {
        // Configure relationship with Program (can't be done with annotations)
        builder.HasOne(pur => pur.Program)
            .WithMany()
            .HasForeignKey(pur => pur.ProgramId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure relationship with ProgramUser (can't be done with annotations)
        builder.HasOne(pur => pur.ProgramUser)
            .WithMany(pu => pu.ProgramUserRoles)
            .HasForeignKey(pur => pur.ProgramUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
