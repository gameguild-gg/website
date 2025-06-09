
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace cms.Common.Entities;

public enum PermissionType
{
	#region Interaction Permissions
	  Comment = 1,
	  Reply = 2,
	  Vote = 3,
	  Share = 4,
	  Report = 5,
	  Follow = 6,
	  Bookmark = 7,
	  React = 8,
	  Subscribe = 9,
	  Mention = 10,
	  Tag = 11,
	#endregion
	#region Curation Permissions
	  Categorize = 12,
	  Collection = 13,
	  Series = 14,
	  CrossReference = 15,
	  Translate = 16,
	  Version = 17,
	  Template = 18,
	#endregion
	#region Lifecycle Permissions
	Draft = 19,
	  Submit = 20,
	  Withdraw = 21,
	  Archive = 22,
	  Restore = 23,
	  SoftDelete = 24,
	  HardDelete = 25,
	  Backup = 26,
	  Migrate = 27,
	  Clone = 28,
	#endregion
	#region Editorial Permissions
	  Edit = 29,
	  Proofread = 30,
	  FactCheck = 31,
	  StyleGuide = 32,
	  Plagiarism = 33,
	  SEO = 34,
	  Accessibility = 35,
	  Legal = 36,
	  Brand = 37,
	  Guidelines = 38,
	#endregion
	#region Moderation Permissions
	  Review = 39,
	  Approve = 40,
	  Reject = 41,
	  Hide = 42,
	  Quarantine = 43,
	  Flag = 44,
	  Warning = 45,
	  Suspend = 46,
	  Ban = 47,
	  Escalate = 48,
	#endregion
	#region Monetization Permissions
	  Monetize = 59,
	  Paywall = 60,
	  Subscription = 61,
	  Advertisement = 62,
	  Sponsorship = 63,
	  Affiliate = 64,
	  Commission = 65,
	  License = 66,
	  Pricing = 67,
	  Revenue = 68,
	#endregion
	#region Promotion Permissions
	  Feature = 69,
	  Pin = 70,
	  Trending = 71,
	  Recommend = 72,
	  Spotlight = 73,
	  Banner = 74,
	  Carousel = 75,
	  Widget = 76,
	  Email = 77,
	  Push = 78,
	  SMS = 79,
	#endregion
	#region Publishing Permissions
	  Publish = 80,
	  Unpublish = 81,
	  Schedule = 82,
	  Reschedule = 83,
	  Distribute = 84,
	  Syndicate = 85,
	  RSS = 86,
	  Newsletter = 87,
	  SocialMedia = 88,
	  API = 89,
	#endregion
	#region Quality Control Permissions
	  Score = 90,
	  Rate = 91,
	  Benchmark = 92,
	  Metrics = 93,
	  Analytics = 94,
	  Performance = 95,
	  Feedback = 96,
	  Audit = 97,
	  Standards = 98,
	  Improvement = 99
	#endregion
}

// this is a base permission model that can be used to store permissions for any entity
public class WithPermissions
{
	[Column(TypeName = "bigint")]
	public ulong PermissionFlags1 { get; set; } = 0; // bits 0-63
	
	[Column(TypeName = "bigint")]
	public ulong PermissionFlags2 { get; set; } = 0; // bits 64-127

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