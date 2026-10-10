namespace GameGuild.Notifications;

/// <summary>
/// Static classification of notification types used by the types catalog and the unsubscribe endpoint.
/// The four toggle categories (Marketing/Social/Learning/Achievements) mirror the category switch in
/// <see cref="Services.NotificationPreferenceService.DecideDeliveryAsync"/> (step 5) — keep both in sync when adding types.
/// Billing/Transactional/System are display-only buckets with no matching preference toggle.
/// </summary>
public static class NotificationCategories
{
    /// <summary>
    /// Account-critical types that are always delivered regardless of preferences and never carry unsubscribe links.
    /// </summary>
    public static readonly HashSet<NotificationType> Transactional =
    [
        NotificationType.EmailVerification,
        NotificationType.PasswordReset,
        NotificationType.MagicLink,
        NotificationType.EmailCode,
        NotificationType.TenantInvite
    ];

    /// <summary>
    /// Returns the catalog category for a notification type.
    /// </summary>
    public static string GetCategory(NotificationType type) => type switch
    {
        NotificationType.Marketing
            or NotificationType.FeatureAnnouncement
            or NotificationType.Recommendation
            or NotificationType.InactivityReminder => "Marketing",

        NotificationType.SocialInteraction
            or NotificationType.DirectMessage
            or NotificationType.CohortActivity => "Social",

        NotificationType.CourseEnrollment
            or NotificationType.CourseCompletion
            or NotificationType.NewContent
            or NotificationType.AssessmentReminder
            or NotificationType.AssessmentGraded => "Learning",

        NotificationType.AchievementUnlocked
            or NotificationType.ProgressMilestone
            or NotificationType.CertificateIssued => "Achievements",

        NotificationType.Billing
            or NotificationType.MonthlyStatement => "Billing",

        NotificationType.EmailVerification
            or NotificationType.PasswordReset
            or NotificationType.MagicLink
            or NotificationType.EmailCode
            or NotificationType.TenantInvite => "Transactional",

        _ => "System"
    };
}
