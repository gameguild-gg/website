namespace cms.Common.Entities;

/// <summary>
/// Permissions for curating and organizing content
/// </summary>
[Flags]
public enum CurationPerm : short
{
	None = 0,
	Categorize = 1,          // Change content categories/tags
	Tag = 2,                 // Add/remove tags
	Collection = 4,          // Add to collections/playlists
	Series = 8,              // Organize into content series
	CrossReference = 16,     // Link related content
	Translate = 32,          // Manage translations
	Version = 64,            // Manage content versions
	Template = 128,          // Create/apply templates

	// Convenience combinations
	BasicCuration = Categorize | Tag | Collection,
	AdvancedCuration = Series | CrossReference | Version | Template,
	All = Categorize | Tag | Collection | Series | CrossReference | Translate | Version | Template
}

/// <summary>
/// Injects curation permissions into an entity.
/// This class should be inherited by entities that manages curation permissions.
/// </summary>
public class WithCurationPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private CurationPerm CurationPermission { get; set; } = CurationPerm.None;

	public bool HasCuratePermission(CurationPerm permission)
	{
		return CurationPermission.HasFlag(permission);
	}

	public void GrantCuratePermission(CurationPerm permission)
	{
		CurationPermission |= permission;
	}

	public void RevokeCuratePermission(CurationPerm permission)
	{
		CurationPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for user interactions with content
/// </summary>
[Flags]
public enum InteractionPerm : short
{
	None = 0,
	Comment = 1,
	Reply = 2,              // Reply to comments
	Vote = 4,
	Share = 8,
	Report = 16,
	Follow = 32,
	Bookmark = 64,
	React = 128,              // Like, love, laugh reactions
	Subscribe = 256,         // Subscribe to content updates
	Mention = 512,           // Mention other users
	Tag = 1024,               // Tag other users in content

	// Convenience combinations
	BasicInteraction = Comment | Vote | Share,
	SocialInteraction = React | Follow | Mention | Tag,
	All = Comment | Vote | Share | Report | Follow | Bookmark | React | Subscribe | Mention | Tag
}

/// <summary>
/// Injects interaction permissions into an entity.
/// This class should be inherited by entities that manages interaction permissions.
/// </summary>
public class WithInteractionPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private InteractionPerm InteractionPermission { get; set; } = InteractionPerm.None;

	public bool HasInteractionPermission(InteractionPerm permission)
	{
		return InteractionPermission.HasFlag(permission);
	}

	public void GrantInteractionPermission(InteractionPerm permission)
	{
		InteractionPermission |= permission;
	}

	public void RevokeInteractionPermission(InteractionPerm permission)
	{
		InteractionPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for managing content lifecycle states
/// </summary>
[Flags]
public enum LifecyclePerm : short
{
	None = 0,
	Draft = 1,               // Manage draft content
	Submit = 2,              // Submit for review
	Withdraw = 4,            // Withdraw from review
	Archive = 8,             // Archive content (remove from feeds, keep searchable)
	Restore = 16,            // Restore archived content
	SoftDelete = 32,         // Soft delete (hidden but recoverable, audit trail)
	HardDelete = 64,         // Permanently delete content
	Backup = 128,            // Create content backups
	Migrate = 256,           // Migrate content between systems
	Clone = 512,             // Clone/duplicate content

	// Convenience combinations
	BasicLifecycle = Draft | Submit | Withdraw,
	AdvancedLifecycle = Archive | Restore | Clone,
	DeletionControl = SoftDelete | HardDelete,
	All = Draft | Submit | Withdraw | Archive | Restore | SoftDelete | HardDelete | Backup | Migrate | Clone
}

/// <summary>
/// Injects lifecycle permissions into an entity.
/// This class should be inherited by entities that manages lifecycle permissions.
/// </summary>
public class WithLifecyclePermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private LifecyclePerm LifecyclePermission { get; set; } = LifecyclePerm.None;

	public bool HasLifecyclePermission(LifecyclePerm permission)
	{
		return LifecyclePermission.HasFlag(permission);
	}

	public void GrantLifecyclePermission(LifecyclePerm permission)
	{
		LifecyclePermission |= permission;
	}

	public void RevokeLifecyclePermission(LifecyclePerm permission)
	{
		LifecyclePermission &= ~permission;
	}
}

/// <summary>
/// Permissions for editorial oversight and content quality
/// </summary>
[Flags]
public enum EditorialPerm : short
{
	None = 0,
	Edit = 1,                // Edit content for quality
	Proofread = 2,           // Proofread for errors
	FactCheck = 4,           // Verify factual accuracy
	StyleGuide = 8,          // Enforce style guidelines
	Plagiarism = 16,         // Check for plagiarism
	SEO = 32,                // Optimize for search engines
	Accessibility = 64,      // Ensure accessibility compliance
	Legal = 128,             // Review for legal compliance
	Brand = 256,             // Ensure brand consistency
	Guidelines = 512,        // Enforce editorial guidelines

	// Convenience combinations
	BasicEditorial = Edit | Proofread | StyleGuide,
	QualityControl = FactCheck | Plagiarism | Guidelines,
	ComplianceReview = Legal | Accessibility | Brand,
	All = Edit | Proofread | FactCheck | StyleGuide | Plagiarism | SEO | Accessibility | Legal | Brand | Guidelines
}

/// <summary>
/// Injects editorial permissions into an entity.
/// This class should be inherited by entities that manages editorial permissions.
/// </summary>
public class WithEditorialPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private EditorialPerm EditorialPermission { get; set; } = EditorialPerm.None;

	public bool HasEditorialPermission(EditorialPerm permission)
	{
		return EditorialPermission.HasFlag(permission);
	}

	public void GrantEditorialPermission(EditorialPerm permission)
	{
		EditorialPermission |= permission;
	}

	public void RevokeEditorialPermission(EditorialPerm permission)
	{
		EditorialPermission &= ~permission;
	}
}


/// <summary>
/// Permissions for content moderation activities
/// </summary>
[Flags]
public enum ModerationPerm : short
{
	None = 0,
	Review = 1,              // Review submitted content
	Approve = 2,             // Approve pending content
	Reject = 4,              // Reject content submissions
	Hide = 8,                // Hide content from public view
	Quarantine = 16,         // Restrict content pending review
	Flag = 32,               // Flag content for attention
	Warning = 64,            // Issue warnings to users
	Suspend = 128,           // Suspend user accounts
	Ban = 256,               // Ban users permanently
	Escalate = 512,          // Escalate to higher authorities

	// Convenience combinations
	BasicModeration = Review | Approve | Reject | Flag,
	ContentControl = Hide | Quarantine | Warning,
	UserControl = Suspend | Ban | Escalate,
	All = Review | Approve | Reject | Hide | Quarantine | Flag | Warning | Suspend | Ban | Escalate
}

/// <summary>
/// Injects moderation permissions into an entity.
/// This class should be inherited by entities that manages moderation permissions.
/// </summary>
public class WithModerationPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private ModerationPerm ModerationPermission { get; set; } = ModerationPerm.None;

	public bool HasModerationPermission(ModerationPerm permission)
	{
		return ModerationPermission.HasFlag(permission);
	}

	public void GrantModerationPermission(ModerationPerm permission)
	{
		ModerationPermission |= permission;
	}

	public void RevokeModerationPermission(ModerationPerm permission)
	{
		ModerationPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for monetization and business operations
/// </summary>
[Flags]
public enum MonetizationPerm : short
{
	None = 0,
	Monetize = 1,            // Enable monetization features
	Paywall = 2,             // Add content behind paywall
	Subscription = 4,        // Manage subscription content
	Advertisement = 8,       // Add advertisements
	Sponsorship = 16,        // Mark as sponsored content
	Affiliate = 32,          // Add affiliate links
	Commission = 64,         // Manage commission structures
	License = 128,           // Manage content licensing
	Pricing = 256,           // Set content pricing
	Revenue = 512,           // View revenue analytics

	// Convenience combinations
	BasicMonetization = Monetize | Paywall | Advertisement,
	AdvancedMonetization = Subscription | Sponsorship | License,
	BusinessIntelligence = Revenue | Commission | Pricing,
	All = Monetize | Paywall | Subscription | Advertisement | Sponsorship | Affiliate | Commission | License | Pricing | Revenue
}

/// <summary>
/// Injects monetization permissions into an entity.
/// This class should be inherited by entities that manages monetization permissions.
/// </summary>
public class WithMonetizationPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private MonetizationPerm MonetizationPermission { get; set; } = MonetizationPerm.None;

	public bool HasMonetizationPermission(MonetizationPerm permission)
	{
		return MonetizationPermission.HasFlag(permission);
	}

	public void GrantMonetizationPermission(MonetizationPerm permission)
	{
		MonetizationPermission |= permission;
	}

	public void RevokeMonetizationPermission(MonetizationPerm permission)
	{
		MonetizationPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for promoting and featuring content
/// </summary>
[Flags]
public enum PromotionPerm : short
{
	None = 0,
	Feature = 1,             // Feature on homepage
	Pin = 2,                 // Pin to top of categories
	Trending = 4,            // Add to trending sections
	Recommend = 8,           // Add to recommendations
	Spotlight = 16,          // Highlight in spotlight
	Banner = 32,             // Display as banner content
	Carousel = 64,           // Add to carousel displays
	Widget = 128,            // Feature in widgets
	Email = 256,             // Include in email campaigns
	Push = 512,              // Send push notifications

	// Convenience combinations
	BasicPromotion = Feature | Pin | Recommend,
	VisualPromotion = Banner | Carousel | Spotlight,
	ActivePromotion = Email | Push | Trending,
	All = Feature | Pin | Trending | Recommend | Spotlight | Banner | Carousel | Widget | Email | Push
}

/// <summary>
/// Injects promotion permissions into an entity.
/// This class should be inherited by entities that manages promotion permissions.
/// </summary>
public class WithPromotionPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private PromotionPerm PromotionPermission { get; set; } = PromotionPerm.None;

	public bool HasPromotionPermission(PromotionPerm permission)
	{
		return PromotionPermission.HasFlag(permission);
	}

	public void GrantPromotionPermission(PromotionPerm permission)
	{
		PromotionPermission |= permission;
	}

	public void RevokePromotionPermission(PromotionPerm permission)
	{
		PromotionPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for publishing and distributing content
/// </summary>
[Flags]
public enum PublishingPerm : short
{
	None = 0,
	Publish = 1,             // Make content publicly visible
	Unpublish = 2,           // Remove from public view
	Schedule = 4,            // Schedule future publication
	Reschedule = 8,          // Modify scheduled publication
	Distribute = 16,         // Push to external channels
	Syndicate = 32,          // Syndicate to other platforms
	RSS = 64,                // Include in RSS feeds
	Newsletter = 128,        // Include in newsletters
	SocialMedia = 256,       // Post to social media
	API = 512,               // Make available via API

	// Convenience combinations
	BasicPublishing = Publish | Unpublish | Schedule,
	ExternalDistribution = Distribute | Syndicate | SocialMedia,
	AutomatedDistribution = RSS | Newsletter | API,
	All = Publish | Unpublish | Schedule | Reschedule | Distribute | Syndicate | RSS | Newsletter | SocialMedia | API
}

/// <summary>
/// Injects publishing permissions into an entity.
/// This class should be inherited by entities that manages publishing permissions.
/// </summary>
public class WithPublishingPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private PublishingPerm PublishingPermission { get; set; } = PublishingPerm.None;

	public bool HasPublishingPermission(PublishingPerm permission)
	{
		return PublishingPermission.HasFlag(permission);
	}

	public void GrantPublishingPermission(PublishingPerm permission)
	{
		PublishingPermission |= permission;
	}

	public void RevokePublishingPermission(PublishingPerm permission)
	{
		PublishingPermission &= ~permission;
	}
}

/// <summary>
/// Permissions for quality control and content assessment
/// </summary>
[Flags]
public enum QualityControlPerm : short
{
    None = 0,
    Score = 1,               // Assign quality scores
    Rate = 2,                // Rate content quality
    Benchmark = 4,           // Set quality benchmarks
    Metrics = 8,             // Access quality metrics
    Analytics = 16,          // View detailed analytics
    Performance = 32,        // Monitor content performance
    Feedback = 64,           // Collect quality feedback
    Audit = 128,             // Conduct quality audits
    Standards = 256,         // Define quality standards
    Improvement = 512,       // Suggest improvements
    
    // Convenience combinations
    BasicQuality = Score | Rate | Feedback,
    QualityAnalytics = Metrics | Analytics | Performance,
    QualityManagement = Benchmark | Standards | Audit | Improvement,
    All = Score | Rate | Benchmark | Metrics | Analytics | Performance | Feedback | Audit | Standards | Improvement
}

/// <summary>
/// Injects quality control permissions into an entity.
/// This class should be inherited by entities that manages quality control permissions.
/// </summary>
public class WithQualityControlPermission
{
	// this field should map to a column in the database on the entity which this class is inherited
	private QualityControlPerm QualityControlPermission { get; set; } = QualityControlPerm.None;

	public bool HasQualityControlPermission(QualityControlPerm permission)
	{
		return QualityControlPermission.HasFlag(permission);
	}

	public void GrantQualityControlPermission(QualityControlPerm permission)
	{
		QualityControlPermission |= permission;
	}

	public void RevokeQualityControlPermission(QualityControlPerm permission)
	{
		QualityControlPermission &= ~permission;
	}
}