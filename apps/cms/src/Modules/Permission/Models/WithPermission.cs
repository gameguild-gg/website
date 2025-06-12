
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq; // do not remove this, it's needed for IQueryable extensions

namespace cms.Common.Entities;

public enum PermissionType
{
	#region Interaction Permissions
      Read = 1,
      Comment = 2,
	  Reply = 3,
	  Vote = 4,
	  Share = 5,
	  Report = 6,
	  Follow = 7,
	  Bookmark = 8,
	  React = 9,
	  Subscribe = 10,
	  Mention = 11,
	  Tag = 12,
	#endregion
	#region Curation Permissions
	  Categorize = 13,
	  Collection = 14,
	  Series = 15,
	  CrossReference = 16,
	  Translate = 17,
	  Version = 18,
	  Template = 19,
	#endregion
	#region Lifecycle Permissions
	  Draft = 20,
	  Submit = 21,
	  Withdraw = 22,
	  Archive = 23,
	  Restore = 24,
	  Delete = 25, // Delete is an alias for SoftDelete, so they share the same value
	  SoftDelete = 25, // Only the owners of a resource can soft delete it at resource level, it still can be deleted by admins at tenant or content type level
	  HardDelete = 26,
	  Backup = 27,
	  Migrate = 28,
	  Clone = 29,
	#endregion
	#region Editorial Permissions
	  Edit = 30,
	  Proofread = 31,
	  FactCheck = 32,
	  StyleGuide = 33,
	  Plagiarism = 34,
	  SEO = 35,
	  Accessibility = 36,
	  Legal = 37,
	  Brand = 38,
	  Guidelines = 39,
	#endregion
	#region Moderation Permissions
	  Review = 40,
	  Approve = 41,
	  Reject = 42,
	  Hide = 43,
	  Quarantine = 44,
	  Flag = 45,
	  Warning = 46,
	  Suspend = 47,
	  Ban = 48,
	  Escalate = 49,
	#endregion
	#region Monetization Permissions
	  Monetize = 50,
	  Paywall = 51,
	  Subscription = 52,
	  Advertisement = 53,
	  Sponsorship = 54,
	  Affiliate = 55,
	  Commission = 56,
	  License = 57,
	  Pricing = 58,
	  Revenue = 59,
	#endregion
	#region Promotion Permissions
	  Feature = 60,
	  Pin = 61,
	  Trending = 62,
	  Recommend = 63,
	  Spotlight = 64,
	  Banner = 65,
	  Carousel = 66,
	  Widget = 67,
	  Email = 68,
	  Push = 69,
	  SMS = 70,
	#endregion
	#region Publishing Permissions
	  Publish = 71,
	  Unpublish = 72,
	  Schedule = 73,
	  Reschedule = 74,
	  Distribute = 75,
	  Syndicate = 76,
	  RSS = 77,
	  Newsletter = 78,
	  SocialMedia = 79,
	  API = 80,
	#endregion
	#region Quality Control Permissions
	  Score = 81,
	  Rate = 82,
	  Benchmark = 83,
	  Metrics = 84,
	  Analytics = 85,
	  Performance = 86,
	  Feedback = 87,
	  Audit = 88,
	  Standards = 89,
	  Improvement = 90
	#endregion
}

// this is a base permission model that can be used to store permissions for any entity
public class WithPermissions: BaseEntity
{
	[Column(TypeName = "bigint")]
	public ulong PermissionFlags1 { get; set; } = 0; // bits 0-63
	
	[Column(TypeName = "bigint")]
	public ulong PermissionFlags2 { get; set; } = 0; // bits 64-127
	
	/// <summary>
	/// User relationship - NULL means default permissions
	/// </summary>
	public Guid? UserId { get; set; }
	
	/// <summary>
	/// Navigation property to the User entity
	/// </summary>
	[ForeignKey(nameof(UserId))]
	public virtual Modules.User.Models.User? User { get; set; }
	
	/// <summary>
	/// Tenant relationship - NULL means global defaults
	/// </summary>
	public Guid? TenantId { get; set; }
	
	/// <summary>
	/// Navigation property to the Tenant entity
	/// </summary>
	[ForeignKey(nameof(TenantId))]
	public virtual new Modules.Tenant.Models.Tenant? Tenant { get; set; }
	
	/// <summary>
	/// Optional expiration date for this permission
	/// If null, permission never expires
	/// If date has passed, permission is expired
	/// </summary>
	public DateTime? ExpiresAt { get; set; }
	
	// Computed properties
	
	/// <summary>
	/// Check if the permission is expired based on ExpiresAt date
	/// </summary>
	public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.UtcNow;
	
	/// <summary>
	/// Check if the permission is valid (not deleted and not expired)
	/// </summary>
	public bool IsValid => !IsDeleted && !IsExpired;
	
	/// <summary>
	/// Check if this is a default permission for a specific tenant
	/// </summary>
	public bool IsDefaultPermission => UserId == null && TenantId != null;
	
	/// <summary>
	/// Check if this is a global default permission
	/// </summary>
	public bool IsGlobalDefaultPermission => UserId == null && TenantId == null;
	
	/// <summary>
	/// Check if this is a user-specific permission
	/// </summary>
	public bool IsUserPermission => UserId != null;

	public bool HasPermission(PermissionType permission)
	{
		var bitPos = (int)permission;
		
		if (bitPos < 64)
			return (PermissionFlags1 & (1UL << bitPos)) != 0;
		else if (bitPos < 128)
			return (PermissionFlags2 & (1UL << (bitPos - 64))) != 0;
		
		return false;
	}

	public bool HasAllPermissions(Collection<PermissionType> permissions)
	{
		return permissions.All(HasPermission);
	}

	public bool HasAnyPermission(Collection<PermissionType> permissions)
	{
		return permissions.Any(HasPermission);
	}

	public void AddPermission(PermissionType permission)
	{
		SetPermission(permission, true);
	}

	public void RemovePermission(PermissionType permission)
	{
		SetPermission(permission, false);
	}

	public void RemovePermissions(Collection<PermissionType> permissions)
	{
		foreach (var permission in permissions)
			SetPermission(permission, false);
	}

	private void SetPermission(PermissionType permission, bool value)
	{
		var bitPos = (int)permission;
		
		if (bitPos < 64)
		{
			var mask = 1UL << bitPos;
			PermissionFlags1 = value ? (PermissionFlags1 | mask) : (PermissionFlags1 & ~mask);
		}
		else if (bitPos < 128)
		{
			var mask = 1UL << (bitPos - 64);
			PermissionFlags2 = value ? (PermissionFlags2 | mask) : (PermissionFlags2 & ~mask);
		}
	}
}

/// <summary>
/// Helper methods for permission queries without fluent API
/// </summary>
public static class PermissionQueryHelper
{
	public static IQueryable<T> GetWithPermission<T>(
		IQueryable<T> query, 
		PermissionType permission) where T : WithPermissions
	{
		var bitPos = (int)permission;
		
		if (bitPos < 64)
		{
			var mask = 1UL << bitPos;
			return query.Where(x => (x.PermissionFlags1 & mask) == mask);
		}
		else if (bitPos < 128)
		{
			var mask = 1UL << (bitPos - 64);
			return query.Where(x => (x.PermissionFlags2 & mask) == mask);
		}
		
		return query.Where(x => false);
	}
	
	public static IQueryable<T> GetWithAllPermissions<T>(
		IQueryable<T> query, 
		params PermissionType[] permissions) where T : WithPermissions
	{
		var mask1 = 0UL;
		var mask2 = 0UL;
		
		foreach (var permission in permissions)
		{
			var bitPos = (int)permission;
			if (bitPos < 64)
				mask1 |= 1UL << bitPos;
			else if (bitPos < 128)
				mask2 |= 1UL << (bitPos - 64);
		}
		
		var result = query;
		
		if (mask1 > 0)
			result = result.Where(x => (x.PermissionFlags1 & mask1) == mask1);
			
		if (mask2 > 0)
			result = result.Where(x => (x.PermissionFlags2 & mask2) == mask2);
			
		return result;
	}
	
	public static IQueryable<T> GetWithAnyPermission<T>(
		IQueryable<T> query, 
		params PermissionType[] permissions) where T : WithPermissions
	{
		var mask1 = 0UL;
		var mask2 = 0UL;
		
		foreach (var permission in permissions)
		{
			var bitPos = (int)permission;
			if (bitPos < 64)
				mask1 |= 1UL << bitPos;
			else if (bitPos < 128)
				mask2 |= 1UL << (bitPos - 64);
		}
		
		var result = query.Where(x => false); // Start with empty result
		
		if (mask1 > 0)
			result = result.Union(query.Where(x => (x.PermissionFlags1 & mask1) != 0));
			
		if (mask2 > 0)
			result = result.Union(query.Where(x => (x.PermissionFlags2 & mask2) != 0));
			
		return result;
	}
	
	/// <summary>
	/// Calculate permission masks for manual queries
	/// </summary>
	public static (ulong mask1, ulong mask2) CalculatePermissionMasks(params PermissionType[] permissions)
	{
		var mask1 = 0UL;
		var mask2 = 0UL;
		
		foreach (var permission in permissions)
		{
			var bitPos = (int)permission;
			if (bitPos < 64)
				mask1 |= 1UL << bitPos;
			else if (bitPos < 128)
				mask2 |= 1UL << (bitPos - 64);
		}
		
		return (mask1, mask2);
	}
}