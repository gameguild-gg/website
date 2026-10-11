namespace GameGuild.API.Security;

/// <summary>
///     HOST-side registry of endpoints that are intentionally reachable without
///     authentication. This file is per-repo content: the entries and their justifications
///     describe this product's public surface.
/// </summary>
/// <remarks>
///     <para>
///         This registry is the reviewed allowlist consumed by the controller-authorization
///         architecture tests. An endpoint may be anonymous only when it appears here with a
///         justification a reviewer has accepted; the architecture tests fail on any
///         <c>[AllowAnonymous]</c> endpoint that is not registered, and on any registered
///         entry whose endpoint no longer exists.
///     </para>
///     <para>
///         Keys are <c>Controller.Action</c> for action-level anonymous endpoints and
///         <c>Controller.*</c> for controllers whose class is annotated
///         <c>[AllowAnonymous]</c> (every endpoint of such a controller is anonymous).
///     </para>
///     <para>
///         Anonymous is not unguarded: most entries here delegate the real authorization
///         decision to a signed/expiring token, a provider signature, or a status filter
///         that hides non-published content from unauthenticated callers.
///     </para>
/// </remarks>
public static class AnonymousEndpointRegistry
{
    /// <summary>
    ///     Key suffix marking a whole controller as intentionally anonymous.
    /// </summary>
    public const string ControllerScopeSuffix = ".*";

    /// <summary>
    ///     Reviewed anonymous endpoints (keyed <c>Controller.Action</c> or
    ///     <c>Controller.*</c>) with a one-line justification per entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Entries =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── Infrastructure probes ──
            ["HealthController.*"] = "Infrastructure liveness/readiness probes for load balancers and orchestrators.",
            ["MetricsController.*"] = "Prometheus scrape endpoint for the monitoring stack.",
            ["VersioningController.GetCurrentVersion"] = "Build/version probe used by load balancers; exposes only the version.",

            // ── Authentication entry points (necessarily anonymous) ──
            ["AuthController.LocalSignUp"] = "Sign-up entry point; creates the account it then authenticates.",
            ["AuthController.LocalSignIn"] = "Sign-in entry point; verifies credentials and issues tokens.",
            ["AuthController.StartMfaSignInEnrollment"] = "Provisions an unenrolled account only through a current first-factor challenge; supplies no authenticated session or recovery codes.",
            ["AuthController.CompleteMfaSignIn"] = "Consumes an expiring first-factor challenge bound to the server-side subject, tenant and policy; issues credentials only after enrolled MFA proof.",
            ["AuthController.PolymorphicSignIn"] = "Password sign-in entry point; resolves a unique identifier and applies the local authentication controls.",
            ["AuthController.RefreshToken"] = "Refresh-token rotation; the rotating refresh token itself is the credential.",
            ["AuthController.RequestMagicLink"] = "Requests an emailed sign-in link; only schedules an email.",
            ["AuthController.ConsumeMagicLink"] = "Redeems a single-use, expiring magic-link token.",
            ["AuthController.RequestEmailCode"] = "Requests an emailed one-time sign-in code; only schedules an email.",
            ["AuthController.ConsumeEmailCode"] = "Redeems a single-use, expiring, attempt-limited email sign-in code.",
            ["AuthController.RequestPasswordReset"] = "Password-reset request; only schedules an email to the given address.",
            ["AuthController.ResetPassword"] = "Redeems a single-use, expiring reset token.",
            ["AuthController.SendEmailVerification"] = "Triggers a verification email; the emailed token is the proof.",
            ["AuthController.VerifyEmail"] = "Redeems a single-use emailed verification token.",
            ["AuthController.GitHubSignIn"] = "OAuth redirect entry to the external identity provider.",
            ["AuthController.GitHubCallback"] = "OAuth callback; the state parameter and provider code complete the flow.",
            ["AuthController.DiscordAuthorize"] = "OAuth redirect entry to the external identity provider.",
            ["AuthController.DiscordCallback"] = "OAuth callback; the state parameter and provider code complete the flow.",
            ["AuthController.OidcSignIn"] = "Federated OIDC redirect entry to the enterprise identity provider.",
            ["AuthController.OidcCallback"] = "Federated OIDC callback; CSRF state plus the provider's signed ID token complete the flow.",
            ["AuthController.OidcDiscoverProvider"] = "Login-page email-domain routing; reveals only configured provider slugs, no account state.",
            ["AuthController.GoogleIdTokenSignIn"] = "Exchanges a provider-issued ID token for platform tokens.",
            ["AuthController.GenerateWeb3Challenge"] = "Issues a nonce challenge for wallet-signature sign-in.",
            ["AuthController.VerifyWeb3Signature"] = "Verifies the wallet signature of the issued nonce.",
            ["MfaController.VerifyMfa"] = "Completes the MFA ceremony with the pending sign-in session's code.",
            ["WebAuthnController.BeginAuthentication"] = "First half of the passkey ceremony; returns only a challenge.",
            ["WebAuthnController.CompleteAuthentication"] = "Second half of the passkey ceremony; issues tokens on proof of possession.",
            ["ServiceAccountTokenController.Token"] = "OAuth-style client-credential token issuance for service accounts.",

            // ── Public catalog/marketing surfaces (published content only) ──
            ["PageController.GetPageBySlug"] = "Public marketing pages by slug; anonymous callers receive published pages only.",
            ["PageController.GetSitemap"] = "SEO sitemap of published pages (slug + last-modified only).",
            ["ContentResourceController.List"] = "Public resource catalog; anonymous callers see published resources only.",
            ["ContentResourceController.GetBySlug"] = "Public resource URLs; anonymous callers see published resources only.",
            ["OpenGraphController.*"] = "Public OpenGraph/SEO metadata for crawlers and social previews.",
            ["ProductsController.GetProducts"] = "Public product catalog listing.",
            ["ProductsController.GetProduct"] = "Public product detail.",
            ["ProductsController.ProductExists"] = "Public existence check used by the storefront.",
            ["ProductsController.GetProductPricing"] = "Public product pricing.",
            ["SubscriptionPlansCrudController.GetSubscriptionPlans"] = "Public pricing plans for the marketing site.",
            ["ProgramCrudController.GetPublicPrograms"] = "Public course catalog; only published programs are returned.",
            ["ProgramCrudController.GetProgramBySlug"] = "Public course detail; only published programs are returned.",
            ["ProgramCrudController.GetLinkedProducts"] = "Public storefront products linked to a published course.",
            ["ProgramContentController.GetProgramContent"] = "Public course content outline; published content only.",
            ["ProgramContentController.GetContent"] = "Public course content by id; published content only.",
            ["RecommendationsController.GetPopularCourses"] = "Non-personalized popular-course discovery over published courses.",
            ["RecommendationsController.GetTrendingCourses"] = "Non-personalized trending-course discovery over published courses.",
            ["RecommendationsController.GetSimilarCourses"] = "Similar-course suggestions over published courses only.",
            ["LaunchPadEventsController.GetPublicEvents"] = "Public launch-event listing for the marketing site.",
            ["LaunchPadEventsController.GetPublicEvent"] = "Public launch-event detail.",
            ["TestingEventsController.GetPublicEvents"] = "Public game-testing event listing.",
            ["TestingEventsController.GetPublicEvent"] = "Public game-testing event detail.",
            ["TestingSessionsController.GetPublicTestingSessions"] = "Public testing-session listing.",
            ["CertificatesController.VerifyCertificate"] = "Public certificate verification by verification code.",
            ["ProjectsController.GetProjects"] = "Public project catalog with status/visibility filters applied server-side.",
            ["ProjectsController.GetProject"] = "Public project detail; unpublished projects are hidden from anonymous callers.",
            ["ProjectsController.GetProjectBySlug"] = "Public project detail by slug; published projects only.",
            ["ProjectsController.SearchProjects"] = "Public project search over published projects.",
            ["ProjectsController.GetPopularProjects"] = "Public popularity-sorted project listing.",
            ["ProjectsController.GetRecentProjects"] = "Public recency-sorted project listing.",
            ["ProjectsController.GetFeaturedProjects"] = "Public featured-project listing.",
            ["ProjectsController.GetProjectStatistics"] = "Aggregate public project statistics.",
            ["ProjectsController.GetProjectsByCategory"] = "Public category-filtered project listing.",
            ["ProjectsController.GetProjectsByCreator"] = "Public creator's published projects.",
            ["ProjectsController.GetProjectRoleTemplates"] = "Public role-template catalog used when forming teams.",
            ["ProjectsController.GetRolePermissions"] = "Public role-permission matrix for project roles.",
            ["ProjectStoreProductsController.ListPublicProductProjects"] = "Public listing of projects linked to storefront products.",

            // ── Public community reads (published/aggregate content only) ──
            ["PostsCrudController.GetPosts"] = "Public community feed; anonymous callers see published posts only.",
            ["PostsCrudController.GetPost"] = "Public post detail; published posts only.",
            ["PostsCrudController.GetTrending"] = "Public trending-post listing.",
            ["PostsCrudController.GetByAuthor"] = "Public author's published posts.",
            ["PostsCrudController.Search"] = "Public post search over published posts.",
            ["PostCommentsController.GetComments"] = "Public comment thread under a published post.",
            ["PostCommentsController.GetPopularTags"] = "Public tag aggregation for discovery.",
            ["PostCommentsController.GetPostTags"] = "Public tags attached to a published post.",
            ["PostCommentsController.SearchByTags"] = "Public tag-based post search.",
            ["PostInteractionsController.Share"] = "Records an anonymous share count increment for a public post.",
            ["PostInteractionsController.RecordView"] = "Records an anonymous view count increment for a public post.",
            ["PostInteractionsController.GetStatistics"] = "Aggregate public interaction counts for a post.",
            ["SocialProfilesController.GetByHandle"] = "Public profile lookup by handle.",
            ["SocialProfilesController.Search"] = "Public profile search.",
            ["FollowersController.GetFollowers"] = "Public follower list of a profile.",
            ["FollowersController.GetFollowerCount"] = "Public follower count of a profile.",
            ["FollowersController.GetFollowerCountsBatch"] = "Batched public follower counts.",
            ["ReviewsController.GetReview"] = "Public course-review detail.",
            ["ReviewsController.GetCourseReviews"] = "Public reviews under a published course.",
            ["ReviewsController.GetCourseRatingStats"] = "Aggregate public rating statistics for a course.",
            ["DiscussionsController.GetDiscussion"] = "Public course discussion detail.",
            ["DiscussionsController.GetCourseDiscussions"] = "Public discussions under a published course.",
            ["DiscussionsController.GetContentDiscussions"] = "Public discussions under published content.",
            ["RepliesController.GetDiscussionReplies"] = "Public replies under a public discussion.",
            ["LikesController.GetCourseLikeCount"] = "Aggregate public like count for a course.",

            // ── Public blog reading surface (published content only) ──
            ["BlogPublicController.GetAuthorPosts"] = "Public blog reading surface — published content only.",
            ["BlogPublicController.GetPostDetail"] = "Public blog reading surface — published content only.",
            ["BlogPublicController.ResolveRoute"] = "Public blog reading surface — published content only.",
            ["BlogPublicController.GetIndex"] = "Public blog reading surface — published content only.",
            ["BlogPublicController.RecordView"] = "Public blog reading surface — published content only; atomic view counter with PerIp rate limiting.",
            ["BlogPublicController.GetComments"] = "Public blog reading surface — published content only.",

            // ── Public intake forms ──
            ["MarketingLeadController.CreateLead"] = "Public contact form; rate limited and validated.",

            // ── Asset delivery (per-request token + access-policy authorization) ──
            ["AssetsController.GetAsset"] = "Metadata fetch; access policy is enforced inside the asset access service.",
            ["AssetsController.GetContent"] = "Content streaming; authorized by signed expiring token and access policy.",
            ["AssetsController.GenerateAccessUrl"] = "URL generation; access-policy validation denies protected assets to anonymous callers.",
            ["AssetsController.GetExtractedText"] = "Text extraction; same token + access-policy path as content streaming.",
            ["SecureAssetDeliveryController.GetContent"] = "Hardened delivery; signed expiring token plus rate limiting and access-policy validation.",
            ["SecureAssetDeliveryController.GetAccessUrl"] = "Hardened URL generation; per-request access-policy validation.",
            ["AssetsCdnController.*"] = "CDN asset routes authorized per request by embedded path tokens.",

            // ── Provider webhooks (signature/secret verified inside the handler) ──
            ["BillingWebhooksController.HandleGooglePayWebhook"] = "Provider billing webhook; bearer-JWT signature verified with the configured Google Pay keys inside the handler.",
            ["BillingWebhooksController.HandleApplePayWebhook"] = "Provider billing webhook; receipt/signature verified inside the handler.",
            ["BillingWebhooksController.HandleStripeWebhook"] = "Provider billing webhook; Stripe signature verified with the webhook secret inside the handler.",
            ["BillingWebhooksController.HandlePayPalWebhook"] = "Provider billing webhook; PayPal transmission signature verified inside the handler.",
            ["EmailEventsController.*"] = "Email delivery-events webhook; SNS signature verified with a pinned signing-cert host.",
            ["EconomySumSubWebhookController.Ingest"] = "KYC provider webhook; request signature is validated against the provider secret.",
            ["EconomyStripeConnectWebhookController.Ingest"] = "Payout provider webhook; Stripe signature is validated against the configured secret.",

            // ── Standards-protocol entry points (LTI 1.3 platform side) ──
            ["LtiController.Jwks"] = "Public JWKS endpoint so external tools can verify platform-issued tokens.",
            ["LtiController.Login"] = "LTI third-party-initiated login entry; the OIDC state binds the flow.",
            ["LtiController.Launch"] = "LTI tool launch; the signed platform-issued JWT is the credential.",

            // ── Self-service with signed tokens ──
            ["NotificationUnsubscribeController.*"] = "One-click unsubscribe via a DataProtection-signed token; no user id in cleartext.",
        };
}
