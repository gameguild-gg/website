using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using cms.Common.Entities;
using cms.Common.Enums;

namespace cms.Modules.Product.Models;

/// <summary>
/// Junction entity representing the relationship between a User and a Product
/// Inherits from BaseEntity to provide UUID IDs, version control, timestamps, and soft delete functionality
/// </summary>
[Table("user_products")]
[Index(nameof(UserId), nameof(ProductId), IsUnique = true)]
[Index(nameof(UserId))]
[Index(nameof(ProductId))]
[Index(nameof(AccessStatus))]
[Index(nameof(AcquisitionType))]
[Index(nameof(AccessEndDate))]
[Index(nameof(SubscriptionId))]
public class UserProduct : BaseEntity
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
    /// Navigation property to the User entity
    /// </summary>
    [ForeignKey(nameof(UserId))]
    public virtual User.Models.User User
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key to the Product entity
    /// </summary>
    [Required]
    public Guid ProductId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the Product entity
    /// </summary>
    [ForeignKey(nameof(ProductId))]
    public virtual Product Product
    {
        get;
        set;
    } = null!;

    /// <summary>
    /// Foreign key to the Subscription entity (optional)
    /// </summary>
    public Guid? SubscriptionId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the Subscription entity
    /// </summary>
    [ForeignKey(nameof(SubscriptionId))]
    public virtual Subscription.Models.UserSubscription? Subscription
    {
        get;
        set;
    }
    
    /// <summary>
    /// How the user acquired this product
    /// </summary>
    public ProductAcquisitionType AcquisitionType
    {
        get;
        set;
    }

    /// <summary>
    /// Current access status for this product
    /// </summary>
    public ProductAccessStatus AccessStatus
    {
        get;
        set;
    } = ProductAccessStatus.Active;
    
    /// <summary>
    /// Amount the user paid for this product
    /// </summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal PricePaid
    {
        get;
        set;
    }
    
    /// <summary>
    /// Currency code for the price paid
    /// </summary>
    [MaxLength(3)]
    public string Currency
    {
        get;
        set;
    } = "USD";

    /// <summary>
    /// When the user's access to this product starts
    /// </summary>
    public DateTime? AccessStartDate
    {
        get;
        set;
    }

    /// <summary>
    /// When the user's access to this product ends
    /// </summary>
    public DateTime? AccessEndDate
    {
        get;
        set;
    }
    
    /// <summary>
    /// User who gifted this product (if acquisition type is Gift)
    /// </summary>
    public Guid? GiftedByUserId
    {
        get;
        set;
    }

    /// <summary>
    /// Navigation property to the user who gifted this product
    /// </summary>
    [ForeignKey(nameof(GiftedByUserId))]
    public virtual User.Models.User? GiftedByUser
    {
        get;
        set;
    }

    /// <summary>
    /// Default constructor
    /// </summary>
    public UserProduct() { }

    /// <summary>
    /// Constructor for partial initialization
    /// </summary>
    /// <param name="partial">Partial user product data</param>
    public UserProduct(object partial) : base(partial) { }

    /// <summary>
    /// Check if the user currently has active access to this product
    /// </summary>
    public bool HasActiveAccess()
    {
        if (AccessStatus != ProductAccessStatus.Active)
            return false;

        var now = DateTime.UtcNow;
        return (AccessStartDate == null || AccessStartDate <= now) &&
               (AccessEndDate == null || AccessEndDate > now);
    }

    /// <summary>
    /// Grant access to the product
    /// </summary>
    public void GrantAccess(DateTime? startDate = null, DateTime? endDate = null)
    {
        AccessStatus = ProductAccessStatus.Active;
        AccessStartDate = startDate ?? DateTime.UtcNow;
        AccessEndDate = endDate;
        Touch();
    }

    /// <summary>
    /// Revoke access to the product
    /// </summary>
    public void RevokeAccess()
    {
        AccessStatus = ProductAccessStatus.Revoked;
        AccessEndDate = DateTime.UtcNow;
        Touch();
    }
}

/// <summary>
/// Entity Framework configuration for UserProduct entity
/// </summary>
public class UserProductConfiguration : IEntityTypeConfiguration<UserProduct>
{
    public void Configure(EntityTypeBuilder<UserProduct> builder)
    {
        // Configure relationship with User (can't be done with annotations)
        builder.HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure relationship with Product (can't be done with annotations)
        builder.HasOne(up => up.Product)
            .WithMany()
            .HasForeignKey(up => up.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure relationship with Subscription (can't be done with annotations)
        builder.HasOne(up => up.Subscription)
            .WithMany()
            .HasForeignKey(up => up.SubscriptionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure relationship with GiftedByUser (can't be done with annotations)
        builder.HasOne(up => up.GiftedByUser)
            .WithMany()
            .HasForeignKey(up => up.GiftedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
