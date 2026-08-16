/**
 * @game-guild/client - Generated Endpoint Definitions
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 *
 * Generated from: GameGuild API
 * API Version: 1.0
 */
import type * as Types from "./types.gen.js";

/* eslint-disable @typescript-eslint/no-explicit-any */
// Endpoint Definitions

export interface PostAccessReviewsCampaignsInput {
  body?: Types.IdentityAuthorizationCommandsCreateAccessReviewCampaignCommand;
}
export type PostAccessReviewsCampaignsOutput =
  Types.IdentityAuthorizationAccessReviewCampaign;
export const postAccessReviewsCampaignsEndpoint = {
  operationId: "postAccessReviewsCampaigns" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetAccessReviewsCampaignsInput {
  id: string;
}
export type GetAccessReviewsCampaignsOutput =
  Types.IdentityAuthorizationAccessReviewCampaign;
export const getAccessReviewsCampaignsEndpoint = {
  operationId: "getAccessReviewsCampaigns" as const,
  method: "GET" as const,
  path: "/v1/access-reviews/campaigns/{id}" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetAccessReviewsCampaignsActiveInput {
  query?: {
    tenantId?: string;
  };
}
export type GetAccessReviewsCampaignsActiveOutput =
  Array<Types.IdentityAuthorizationAccessReviewCampaign>;
export const getAccessReviewsCampaignsActiveEndpoint = {
  operationId: "getAccessReviewsCampaignsActive" as const,
  method: "GET" as const,
  path: "/v1/access-reviews/campaigns/active" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsCampaignsStartInput {
  id: string;
}
export type PostAccessReviewsCampaignsStartOutput = void;
export const postAccessReviewsCampaignsStartEndpoint = {
  operationId: "postAccessReviewsCampaignsStart" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns/{id}:start" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsCampaignsCompleteInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersCompleteCampaignInput;
}
export type PostAccessReviewsCampaignsCompleteOutput = void;
export const postAccessReviewsCampaignsCompleteEndpoint = {
  operationId: "postAccessReviewsCampaignsComplete" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns/{id}:complete" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsCampaignsCancelInput {
  id: string;
}
export type PostAccessReviewsCampaignsCancelOutput = void;
export const postAccessReviewsCampaignsCancelEndpoint = {
  operationId: "postAccessReviewsCampaignsCancel" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns/{id}:cancel" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsCampaignsSendRemindersInput {
  id: string;
}
export type PostAccessReviewsCampaignsSendRemindersOutput = number;
export const postAccessReviewsCampaignsSendRemindersEndpoint = {
  operationId: "postAccessReviewsCampaignsSendReminders" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns/{id}:send-reminders" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetAccessReviewsItemsPendingInput {
  query?: {
    reviewerId?: string;
    tenantId?: string;
  };
}
export type GetAccessReviewsItemsPendingOutput =
  Array<Types.IdentityAuthorizationAccessReviewItem>;
export const getAccessReviewsItemsPendingEndpoint = {
  operationId: "getAccessReviewsItemsPending" as const,
  method: "GET" as const,
  path: "/v1/access-reviews/items/pending" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsItemsApproveInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersApproveItemInput;
}
export type PostAccessReviewsItemsApproveOutput =
  Types.IdentityAuthorizationAccessReviewItem;
export const postAccessReviewsItemsApproveEndpoint = {
  operationId: "postAccessReviewsItemsApprove" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/items/{id}:approve" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostAccessReviewsItemsRevokeInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersRevokeItemInput;
}
export type PostAccessReviewsItemsRevokeOutput =
  Types.IdentityAuthorizationAccessReviewItem;
export const postAccessReviewsItemsRevokeEndpoint = {
  operationId: "postAccessReviewsItemsRevoke" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/items/{id}:revoke" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export type PostAccessReviewsCampaignsProcessExpiredInput = void;
export type PostAccessReviewsCampaignsProcessExpiredOutput = number;
export const postAccessReviewsCampaignsProcessExpiredEndpoint = {
  operationId: "postAccessReviewsCampaignsProcessExpired" as const,
  method: "POST" as const,
  path: "/v1/access-reviews/campaigns:process-expired" as const,
  tags: ["AccessControlAccessReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostDelegatedAdminInput {
  body?: Types.IdentityAuthorizationCommandsGrantDelegatedAdminCommand;
}
export type PostDelegatedAdminOutput =
  Types.IdentityAuthorizationDelegatedAdminScope;
export const postDelegatedAdminEndpoint = {
  operationId: "postDelegatedAdmin" as const,
  method: "POST" as const,
  path: "/v1/delegated-admin" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminInput {
  id: string;
}
export type GetDelegatedAdminOutput =
  Types.IdentityAuthorizationDelegatedAdminScope;
export const getDelegatedAdminEndpoint = {
  operationId: "getDelegatedAdmin" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/{id}" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface DeleteDelegatedAdminInput {
  id: string;
}
export type DeleteDelegatedAdminOutput = void;
export const deleteDelegatedAdminEndpoint = {
  operationId: "deleteDelegatedAdmin" as const,
  method: "DELETE" as const,
  path: "/v1/delegated-admin/{id}" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminUserScopesInput {
  adminUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetDelegatedAdminUserScopesOutput =
  Array<Types.IdentityAuthorizationDelegatedAdminScope>;
export const getDelegatedAdminUserScopesEndpoint = {
  operationId: "getDelegatedAdminUserScopes" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/user/{adminUserId}/scopes" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminUserManagedUsersInput {
  adminUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetDelegatedAdminUserManagedUsersOutput = Array<string>;
export const getDelegatedAdminUserManagedUsersEndpoint = {
  operationId: "getDelegatedAdminUserManagedUsers" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/user/{adminUserId}/managed-users" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminUserManagedResourcesInput {
  adminUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetDelegatedAdminUserManagedResourcesOutput = Array<string>;
export const getDelegatedAdminUserManagedResourcesEndpoint = {
  operationId: "getDelegatedAdminUserManagedResources" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/user/{adminUserId}/managed-resources" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminUserCanManageUserInput {
  adminUserId: string;
  targetUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetDelegatedAdminUserCanManageUserOutput = boolean;
export const getDelegatedAdminUserCanManageUserEndpoint = {
  operationId: "getDelegatedAdminUserCanManageUser" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/user/{adminUserId}/can-manage-user/{targetUserId}" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetDelegatedAdminUserCanManageResourceInput {
  adminUserId: string;
  query?: {
    resourceType?: string;
    tenantId?: string;
  };
}
export type GetDelegatedAdminUserCanManageResourceOutput = boolean;
export const getDelegatedAdminUserCanManageResourceEndpoint = {
  operationId: "getDelegatedAdminUserCanManageResource" as const,
  method: "GET" as const,
  path: "/v1/delegated-admin/user/{adminUserId}/can-manage-resource" as const,
  tags: ["AccessControlDelegatedAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostJitElevationsInput {
  body?: Types.IdentityAuthorizationCommandsRequestJitElevationCommand;
}
export type PostJitElevationsOutput =
  Types.IdentityAuthorizationJitElevationInput;
export const postJitElevationsEndpoint = {
  operationId: "postJitElevations" as const,
  method: "POST" as const,
  path: "/v1/jit-elevations" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface PostJitElevationsApproveInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersApproveElevationInput;
}
export type PostJitElevationsApproveOutput =
  Types.IdentityAuthorizationJitElevationInput;
export const postJitElevationsApproveEndpoint = {
  operationId: "postJitElevationsApprove" as const,
  method: "POST" as const,
  path: "/v1/jit-elevations/{id}:approve" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface PostJitElevationsDenyInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersDenyElevationInput;
}
export type PostJitElevationsDenyOutput =
  Types.IdentityAuthorizationJitElevationInput;
export const postJitElevationsDenyEndpoint = {
  operationId: "postJitElevationsDeny" as const,
  method: "POST" as const,
  path: "/v1/jit-elevations/{id}:deny" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface PostJitElevationsRevokeInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersRevokeElevationInput;
}
export type PostJitElevationsRevokeOutput = void;
export const postJitElevationsRevokeEndpoint = {
  operationId: "postJitElevationsRevoke" as const,
  method: "POST" as const,
  path: "/v1/jit-elevations/{id}:revoke" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetJitElevationsInput {
  id: string;
}
export type GetJitElevationsOutput =
  Types.IdentityAuthorizationJitElevationInput;
export const getJitElevationsEndpoint = {
  operationId: "getJitElevations" as const,
  method: "GET" as const,
  path: "/v1/jit-elevations/{id}" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetJitElevationsPendingInput {
  query?: {
    tenantId?: string;
  };
}
export type GetJitElevationsPendingOutput =
  Array<Types.IdentityAuthorizationJitElevationInput>;
export const getJitElevationsPendingEndpoint = {
  operationId: "getJitElevationsPending" as const,
  method: "GET" as const,
  path: "/v1/jit-elevations/pending" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetJitElevationsUserInput {
  userId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetJitElevationsUserOutput =
  Array<Types.IdentityAuthorizationJitElevationInput>;
export const getJitElevationsUserEndpoint = {
  operationId: "getJitElevationsUser" as const,
  method: "GET" as const,
  path: "/v1/jit-elevations/user/{userId}" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetJitElevationsUserActiveInput {
  userId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetJitElevationsUserActiveOutput =
  Array<Types.IdentityAuthorizationJitElevationInput>;
export const getJitElevationsUserActiveEndpoint = {
  operationId: "getJitElevationsUserActive" as const,
  method: "GET" as const,
  path: "/v1/jit-elevations/user/{userId}/active" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetJitElevationsUserCheckInput {
  userId: string;
  query?: {
    permission?: string;
    tenantId?: string;
    resourceId?: string;
  };
}
export type GetJitElevationsUserCheckOutput = boolean;
export const getJitElevationsUserCheckEndpoint = {
  operationId: "getJitElevationsUserCheck" as const,
  method: "GET" as const,
  path: "/v1/jit-elevations/user/{userId}/check" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export type PostJitElevationsCleanupInput = void;
export type PostJitElevationsCleanupOutput = number;
export const postJitElevationsCleanupEndpoint = {
  operationId: "postJitElevationsCleanup" as const,
  method: "POST" as const,
  path: "/v1/jit-elevations/:cleanup" as const,
  tags: ["AccessControlJitElevations"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsUsageInput {
  query?: {
    tenantId?: string;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetPermissionAnalyticsUsageOutput =
  Array<Types.IdentityAuthorizationPermissionUsageMetrics>;
export const getPermissionAnalyticsUsageEndpoint = {
  operationId: "getPermissionAnalyticsUsage" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/usage" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsUserActivityInput {
  query?: {
    tenantId?: string;
    top?: number;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetPermissionAnalyticsUserActivityOutput =
  Array<Types.IdentityAuthorizationUserActivitySummary>;
export const getPermissionAnalyticsUserActivityEndpoint = {
  operationId: "getPermissionAnalyticsUserActivity" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/user-activity" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsResourcePatternsInput {
  query?: {
    tenantId?: string;
    top?: number;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetPermissionAnalyticsResourcePatternsOutput =
  Array<Types.IdentityAuthorizationResourceAccessPattern>;
export const getPermissionAnalyticsResourcePatternsEndpoint = {
  operationId: "getPermissionAnalyticsResourcePatterns" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/resource-patterns" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsTrendsInput {
  query?: {
    tenantId?: string;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetPermissionAnalyticsTrendsOutput =
  Array<Types.IdentityAuthorizationPermissionTrend>;
export const getPermissionAnalyticsTrendsEndpoint = {
  operationId: "getPermissionAnalyticsTrends" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/trends" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsAnomaliesInput {
  query?: {
    tenantId?: string;
    fromDate?: string;
  };
}
export type GetPermissionAnalyticsAnomaliesOutput =
  Array<Types.IdentityAuthorizationPermissionAnomaly>;
export const getPermissionAnalyticsAnomaliesEndpoint = {
  operationId: "getPermissionAnalyticsAnomalies" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/anomalies" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionAnalyticsReportInput {
  query?: {
    tenantId?: string;
    periodStart?: string;
    periodEnd?: string;
  };
}
export type GetPermissionAnalyticsReportOutput =
  Types.IdentityAuthorizationPermissionAnalyticsReport;
export const getPermissionAnalyticsReportEndpoint = {
  operationId: "getPermissionAnalyticsReport" as const,
  method: "GET" as const,
  path: "/v1/permission-analytics/report" as const,
  tags: ["AccessControlPermissionAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface PostPermissionDelegationsInput {
  body?: Types.IdentityAuthorizationCommandsDelegatePermissionsCommand;
}
export type PostPermissionDelegationsOutput =
  Types.IdentityAuthorizationPermissionDelegation;
export const postPermissionDelegationsEndpoint = {
  operationId: "postPermissionDelegations" as const,
  method: "POST" as const,
  path: "/v1/permission-delegations" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionDelegationsInput {
  id: string;
}
export type GetPermissionDelegationsOutput =
  Types.IdentityAuthorizationPermissionDelegation;
export const getPermissionDelegationsEndpoint = {
  operationId: "getPermissionDelegations" as const,
  method: "GET" as const,
  path: "/v1/permission-delegations/{id}" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface DeletePermissionDelegationsInput {
  id: string;
}
export type DeletePermissionDelegationsOutput = void;
export const deletePermissionDelegationsEndpoint = {
  operationId: "deletePermissionDelegations" as const,
  method: "DELETE" as const,
  path: "/v1/permission-delegations/{id}" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionDelegationsDelegateInput {
  delegateUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetPermissionDelegationsDelegateOutput =
  Array<Types.IdentityAuthorizationPermissionDelegation>;
export const getPermissionDelegationsDelegateEndpoint = {
  operationId: "getPermissionDelegationsDelegate" as const,
  method: "GET" as const,
  path: "/v1/permission-delegations/delegate/{delegateUserId}" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionDelegationsDelegatorInput {
  delegatorUserId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetPermissionDelegationsDelegatorOutput =
  Array<Types.IdentityAuthorizationPermissionDelegation>;
export const getPermissionDelegationsDelegatorEndpoint = {
  operationId: "getPermissionDelegationsDelegator" as const,
  method: "GET" as const,
  path: "/v1/permission-delegations/delegator/{delegatorUserId}" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface GetPermissionDelegationsCheckInput {
  query?: {
    delegateUserId?: string;
    permission?: string;
    tenantId?: string;
    resourceId?: string;
  };
}
export type GetPermissionDelegationsCheckOutput = boolean;
export const getPermissionDelegationsCheckEndpoint = {
  operationId: "getPermissionDelegationsCheck" as const,
  method: "GET" as const,
  path: "/v1/permission-delegations/check" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export type PostPermissionDelegationsCleanupInput = void;
export type PostPermissionDelegationsCleanupOutput = number;
export const postPermissionDelegationsCleanupEndpoint = {
  operationId: "postPermissionDelegationsCleanup" as const,
  method: "POST" as const,
  path: "/v1/permission-delegations/:cleanup" as const,
  tags: ["AccessControlPermissionDelegations"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationResourcesInvitationsInput {
  invitationId: string;
}
export type GetAuthorizationResourcesInvitationsOutput =
  Types.IdentityAuthorizationGetResourceInvitationOutput;
export const getAuthorizationResourcesInvitationsEndpoint = {
  operationId: "getAuthorizationResourcesInvitations" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/resources/invitations/{invitationId}" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAuthorizationResourcesInvitationsInput {
  invitationId: string;
}
export type DeleteAuthorizationResourcesInvitationsOutput =
  Types.IdentityAuthorizationInvitationActionResult;
export const deleteAuthorizationResourcesInvitationsEndpoint = {
  operationId: "deleteAuthorizationResourcesInvitations" as const,
  method: "DELETE" as const,
  path: "/api/v1/authorization/resources/invitations/{invitationId}" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export type GetAuthorizationResourcesInvitationsPendingInput = void;
export type GetAuthorizationResourcesInvitationsPendingOutput =
  Types.IdentityAuthorizationGetPendingResourceInvitationsOutput;
export const getAuthorizationResourcesInvitationsPendingEndpoint = {
  operationId: "getAuthorizationResourcesInvitationsPending" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/resources/invitations/pending" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationResourcesInvitationsAcceptInput {
  invitationId: string;
}
export type PostAuthorizationResourcesInvitationsAcceptOutput =
  Types.IdentityAuthorizationInvitationActionResult;
export const postAuthorizationResourcesInvitationsAcceptEndpoint = {
  operationId: "postAuthorizationResourcesInvitationsAccept" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/resources/invitations/{invitationId}/accept" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationResourcesInvitationsDeclineInput {
  invitationId: string;
  body?: Types.IdentityAuthorizationDeclineInvitationInput;
}
export type PostAuthorizationResourcesInvitationsDeclineOutput =
  Types.IdentityAuthorizationInvitationActionResult;
export const postAuthorizationResourcesInvitationsDeclineEndpoint = {
  operationId: "postAuthorizationResourcesInvitationsDecline" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/resources/invitations/{invitationId}/decline" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationResourcesShareInput {
  body?: Types.IdentityAuthorizationShareResourceCommand;
}
export type PostAuthorizationResourcesShareOutput =
  Types.IdentityAuthorizationShareResult;
export const postAuthorizationResourcesShareEndpoint = {
  operationId: "postAuthorizationResourcesShare" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/resources/share" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface PutAuthorizationResourcesUsersPermissionsInput {
  body?: Types.IdentityAuthorizationUpdateUserPermissionsCommand;
}
export type PutAuthorizationResourcesUsersPermissionsOutput =
  Types.IdentityAuthorizationPermissionUpdateResult;
export const putAuthorizationResourcesUsersPermissionsEndpoint = {
  operationId: "putAuthorizationResourcesUsersPermissions" as const,
  method: "PUT" as const,
  path: "/api/v1/authorization/resources/users/permissions" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAuthorizationResourcesUsersAccessInput {
  body?: Types.IdentityAuthorizationRemoveUserAccessCommand;
}
export type DeleteAuthorizationResourcesUsersAccessOutput =
  Types.IdentityAuthorizationPermissionUpdateResult;
export const deleteAuthorizationResourcesUsersAccessEndpoint = {
  operationId: "deleteAuthorizationResourcesUsersAccess" as const,
  method: "DELETE" as const,
  path: "/api/v1/authorization/resources/users/access" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationResourcesPermissionsInput {
  resourceType: string;
  resourceId: string;
  query?: {
    tenantId?: string;
    userId?: string;
  };
}
export type GetAuthorizationResourcesPermissionsOutput =
  Types.IdentityAuthorizationEffectivePermissionsOutput;
export const getAuthorizationResourcesPermissionsEndpoint = {
  operationId: "getAuthorizationResourcesPermissions" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/resources/{resourceType}/{resourceId}/permissions" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationResourcesHasPermissionInput {
  resourceType: string;
  resourceId: string;
  query?: {
    tenantId?: string;
    permission?: string;
    userId?: string;
  };
}
export type GetAuthorizationResourcesHasPermissionOutput =
  Types.IdentityAuthorizationHasPermissionOutput;
export const getAuthorizationResourcesHasPermissionEndpoint = {
  operationId: "getAuthorizationResourcesHasPermission" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/resources/{resourceType}/{resourceId}/has-permission" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationResourcesUsersInput {
  resourceType: string;
  resourceId: string;
  query?: {
    tenantId?: string;
    includeInherited?: boolean;
    includeExpired?: boolean;
  };
}
export type GetAuthorizationResourcesUsersOutput =
  Types.IdentityAuthorizationGetResourceUsersOutput;
export const getAuthorizationResourcesUsersEndpoint = {
  operationId: "getAuthorizationResourcesUsers" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/resources/{resourceType}/{resourceId}/users" as const,
  tags: ["AccessControlResourcePermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetSodRulesForGetSodRulesInput {
  query?: {
    tenantId?: string;
    activeOnly?: boolean;
  };
}
export type GetSodRulesForGetSodRulesOutput =
  Array<Types.IdentityAuthorizationSoDRule>;
export const getSodRulesForGetSodRulesEndpoint = {
  operationId: "getSodRulesForGetSodRules" as const,
  method: "GET" as const,
  path: "/v1/sod/rules" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PostSodRulesInput {
  body?: Types.IdentityAuthorizationCommandsCreateSoDRuleCommand;
}
export type PostSodRulesOutput = Types.IdentityAuthorizationSoDRule;
export const postSodRulesEndpoint = {
  operationId: "postSodRules" as const,
  method: "POST" as const,
  path: "/v1/sod/rules" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface GetSodRulesForGetSodRulesByIdInput {
  id: string;
}
export type GetSodRulesForGetSodRulesByIdOutput =
  Types.IdentityAuthorizationSoDRule;
export const getSodRulesForGetSodRulesByIdEndpoint = {
  operationId: "getSodRulesForGetSodRulesById" as const,
  method: "GET" as const,
  path: "/v1/sod/rules/{id}" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PutSodRulesInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersUpdateSoDRuleInput;
}
export type PutSodRulesOutput = Types.IdentityAuthorizationSoDRule;
export const putSodRulesEndpoint = {
  operationId: "putSodRules" as const,
  method: "PUT" as const,
  path: "/v1/sod/rules/{id}" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface DeleteSodRulesInput {
  id: string;
}
export type DeleteSodRulesOutput = void;
export const deleteSodRulesEndpoint = {
  operationId: "deleteSodRules" as const,
  method: "DELETE" as const,
  path: "/v1/sod/rules/{id}" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface GetSodViolationsDetectInput {
  userId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetSodViolationsDetectOutput =
  Array<Types.IdentityAuthorizationSoDViolation>;
export const getSodViolationsDetectEndpoint = {
  operationId: "getSodViolationsDetect" as const,
  method: "GET" as const,
  path: "/v1/sod/violations/detect/{userId}" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface GetSodViolationsUserInput {
  userId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetSodViolationsUserOutput =
  Array<Types.IdentityAuthorizationSoDViolation>;
export const getSodViolationsUserEndpoint = {
  operationId: "getSodViolationsUser" as const,
  method: "GET" as const,
  path: "/v1/sod/violations/user/{userId}" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface GetSodViolationsActiveInput {
  query?: {
    tenantId?: string;
  };
}
export type GetSodViolationsActiveOutput =
  Array<Types.IdentityAuthorizationSoDViolation>;
export const getSodViolationsActiveEndpoint = {
  operationId: "getSodViolationsActive" as const,
  method: "GET" as const,
  path: "/v1/sod/violations/active" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PostSodViolationsResolveInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersResolveViolationInput;
}
export type PostSodViolationsResolveOutput =
  Types.IdentityAuthorizationSoDViolation;
export const postSodViolationsResolveEndpoint = {
  operationId: "postSodViolationsResolve" as const,
  method: "POST" as const,
  path: "/v1/sod/violations/{id}:resolve" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PostSodViolationsExceptionInput {
  id: string;
  body?: Types.IdentityAuthorizationControllersGrantExceptionInput;
}
export type PostSodViolationsExceptionOutput =
  Types.IdentityAuthorizationSoDViolation;
export const postSodViolationsExceptionEndpoint = {
  operationId: "postSodViolationsException" as const,
  method: "POST" as const,
  path: "/v1/sod/violations/{id}:exception" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PostSodViolationsScanInput {
  query?: {
    tenantId?: string;
  };
}
export type PostSodViolationsScanOutput = number;
export const postSodViolationsScanEndpoint = {
  operationId: "postSodViolationsScan" as const,
  method: "POST" as const,
  path: "/v1/sod/violations:scan" as const,
  tags: ["AccessControlSeparationOfDuties"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsGrantInput {
  body?: Types.IdentityAuthorizationGrantTenantPermissionCommand;
}
export type PostAuthorizationTenantsGrantOutput = string;
export const postAuthorizationTenantsGrantEndpoint = {
  operationId: "postAuthorizationTenantsGrant" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/grant" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsRevokeInput {
  body?: Types.IdentityAuthorizationRevokeTenantPermissionCommand;
}
export type PostAuthorizationTenantsRevokeOutput = boolean;
export const postAuthorizationTenantsRevokeEndpoint = {
  operationId: "postAuthorizationTenantsRevoke" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/revoke" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationTenantsPermissionsInput {
  tenantId: string;
  query?: {
    userId?: string;
    includeEffective?: boolean;
  };
}
export type GetAuthorizationTenantsPermissionsOutput =
  Types.IdentityAuthorizationGetTenantPermissionsOutput;
export const getAuthorizationTenantsPermissionsEndpoint = {
  operationId: "getAuthorizationTenantsPermissions" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/tenants/{tenantId}/permissions" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthorizationTenantsHasPermissionInput {
  tenantId: string;
  query?: {
    permission?: string;
    userId?: string;
  };
}
export type GetAuthorizationTenantsHasPermissionOutput = boolean;
export const getAuthorizationTenantsHasPermissionEndpoint = {
  operationId: "getAuthorizationTenantsHasPermission" as const,
  method: "GET" as const,
  path: "/api/v1/authorization/tenants/{tenantId}/has-permission" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsGlobalDefaultsInput {
  body?: Types.IdentityAuthorizationSetGlobalDefaultPermissionsCommand;
}
export type PostAuthorizationTenantsGlobalDefaultsOutput = boolean;
export const postAuthorizationTenantsGlobalDefaultsEndpoint = {
  operationId: "postAuthorizationTenantsGlobalDefaults" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/global/defaults" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsDefaultsInput {
  body?: Types.IdentityAuthorizationSetTenantDefaultPermissionsCommand;
}
export type PostAuthorizationTenantsDefaultsOutput = boolean;
export const postAuthorizationTenantsDefaultsEndpoint = {
  operationId: "postAuthorizationTenantsDefaults" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/defaults" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsDenyInput {
  body?: Types.IdentityAuthorizationDenyTenantPermissionCommand;
}
export type PostAuthorizationTenantsDenyOutput = string;
export const postAuthorizationTenantsDenyEndpoint = {
  operationId: "postAuthorizationTenantsDeny" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/deny" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthorizationTenantsDenyRemoveInput {
  body?: Types.IdentityAuthorizationRemoveDenyPermissionsCommand;
}
export type PostAuthorizationTenantsDenyRemoveOutput = boolean;
export const postAuthorizationTenantsDenyRemoveEndpoint = {
  operationId: "postAuthorizationTenantsDenyRemove" as const,
  method: "POST" as const,
  path: "/api/v1/authorization/tenants/deny/remove" as const,
  tags: ["AccessControlTenantPermissions"] as const,
  requiresAuth: true,
} as const;

export type GetAccessCapabilitiesInput = void;
export type GetAccessCapabilitiesOutput =
  Types.APIAccessAccessCapabilitiesOutput;
export const getAccessCapabilitiesEndpoint = {
  operationId: "getAccessCapabilities" as const,
  method: "GET" as const,
  path: "/v1/access/capabilities" as const,
  tags: ["ApiAccessCapabilities"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesActivityGradesInput {
  programId: string;
  body?: Types.LearningCoursesCreateActivityGrade;
}
export type PostCoursesActivityGradesOutput =
  Types.LearningCoursesActivityGrade;
export const postCoursesActivityGradesEndpoint = {
  operationId: "postCoursesActivityGrades" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/activity-grades" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesInteractionInput {
  programId: string;
  contentInteractionId: string;
}
export type GetCoursesActivityGradesInteractionOutput =
  Types.LearningCoursesActivityGrade;
export const getCoursesActivityGradesInteractionEndpoint = {
  operationId: "getCoursesActivityGradesInteraction" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/interaction/{contentInteractionId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesGraderInput {
  programId: string;
  graderProgramUserId: string;
}
export type GetCoursesActivityGradesGraderOutput =
  Array<Types.LearningCoursesActivityGrade>;
export const getCoursesActivityGradesGraderEndpoint = {
  operationId: "getCoursesActivityGradesGrader" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/grader/{graderProgramUserId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesStudentInput {
  programId: string;
  programUserId: string;
}
export type GetCoursesActivityGradesStudentOutput =
  Array<Types.LearningCoursesActivityGrade>;
export const getCoursesActivityGradesStudentEndpoint = {
  operationId: "getCoursesActivityGradesStudent" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/student/{programUserId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesActivityGradesInput {
  programId: string;
  gradeId: string;
  body?: Types.LearningCoursesUpdateActivityGrade;
}
export type PutCoursesActivityGradesOutput = Types.LearningCoursesActivityGrade;
export const putCoursesActivityGradesEndpoint = {
  operationId: "putCoursesActivityGrades" as const,
  method: "PUT" as const,
  path: "/v1/courses/{programId}/activity-grades/{gradeId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface DeleteCoursesActivityGradesInput {
  programId: string;
  gradeId: string;
}
export type DeleteCoursesActivityGradesOutput = void;
export const deleteCoursesActivityGradesEndpoint = {
  operationId: "deleteCoursesActivityGrades" as const,
  method: "DELETE" as const,
  path: "/v1/courses/{programId}/activity-grades/{gradeId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesPendingInput {
  programId: string;
}
export type GetCoursesActivityGradesPendingOutput =
  Array<Types.LearningCoursesContentInteraction>;
export const getCoursesActivityGradesPendingEndpoint = {
  operationId: "getCoursesActivityGradesPending" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/pending" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesStatisticsInput {
  programId: string;
}
export type GetCoursesActivityGradesStatisticsOutput =
  Types.LearningCoursesGradeStatistics;
export const getCoursesActivityGradesStatisticsEndpoint = {
  operationId: "getCoursesActivityGradesStatistics" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/statistics" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesActivityGradesContentInput {
  programId: string;
  contentId: string;
}
export type GetCoursesActivityGradesContentOutput =
  Array<Types.LearningCoursesActivityGrade>;
export const getCoursesActivityGradesContentEndpoint = {
  operationId: "getCoursesActivityGradesContent" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/activity-grades/content/{contentId}" as const,
  tags: ["LearningCoursesActivityGrade"] as const,
  requiresAuth: true,
} as const;

export type GetAiStatusInput = void;
export type GetAiStatusOutput = Types.AIAiStatusOutput;
export const getAiStatusEndpoint = {
  operationId: "getAiStatus" as const,
  method: "GET" as const,
  path: "/v1/ai/status" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiChatInput {
  body?: Types.AIAiChatInput;
}
export type PostAiChatOutput = Types.AIAiCompletionOutput;
export const postAiChatEndpoint = {
  operationId: "postAiChat" as const,
  method: "POST" as const,
  path: "/v1/ai/chat" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiGenerateContentInput {
  body?: Types.AIAiGeneratedContentInput;
}
export type PostAiGenerateContentOutput = Types.AIAiCompletionOutput;
export const postAiGenerateContentEndpoint = {
  operationId: "postAiGenerateContent" as const,
  method: "POST" as const,
  path: "/v1/ai/generate-content" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiGenerateContentEmailInput {
  body?: Types.AIAiGeneratedContentDraftInput;
}
export type PostAiGenerateContentEmailOutput = Types.AIAiCompletionOutput;
export const postAiGenerateContentEmailEndpoint = {
  operationId: "postAiGenerateContentEmail" as const,
  method: "POST" as const,
  path: "/v1/ai/generate-content/email" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiEmailInput {
  body?: Types.AIAiGeneratedContentDraftInput;
}
export type PostAiEmailOutput = Types.AIAiCompletionOutput;
export const postAiEmailEndpoint = {
  operationId: "postAiEmail" as const,
  method: "POST" as const,
  path: "/v1/ai/email" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiGenerateContentReportInput {
  body?: Types.AIAiGeneratedContentDraftInput;
}
export type PostAiGenerateContentReportOutput = Types.AIAiCompletionOutput;
export const postAiGenerateContentReportEndpoint = {
  operationId: "postAiGenerateContentReport" as const,
  method: "POST" as const,
  path: "/v1/ai/generate-content/report" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiReportInput {
  body?: Types.AIAiGeneratedContentDraftInput;
}
export type PostAiReportOutput = Types.AIAiCompletionOutput;
export const postAiReportEndpoint = {
  operationId: "postAiReport" as const,
  method: "POST" as const,
  path: "/v1/ai/report" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiGenerateContentListingDescriptionInput {
  body?: Types.AIAiGeneratedContentDraftInput;
}
export type PostAiGenerateContentListingDescriptionOutput =
  Types.AIAiCompletionOutput;
export const postAiGenerateContentListingDescriptionEndpoint = {
  operationId: "postAiGenerateContentListingDescription" as const,
  method: "POST" as const,
  path: "/v1/ai/generate-content/listing-description" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface PostAiGenerateInput {
  body?: Types.AIAiGenerateInput;
}
export type PostAiGenerateOutput = Types.AIAiCompletionOutput;
export const postAiGenerateEndpoint = {
  operationId: "postAiGenerate" as const,
  method: "POST" as const,
  path: "/v1/ai/generate" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface GetAiHistoryInput {
  query?: {
    take?: number;
  };
}
export type GetAiHistoryOutput = Array<Types.AIAiConversationHistoryEntry>;
export const getAiHistoryEndpoint = {
  operationId: "getAiHistory" as const,
  method: "GET" as const,
  path: "/v1/ai/history" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface GetAiHistoryExportInput {
  query?: {
    format?: string;
    take?: number;
  };
}
export type GetAiHistoryExportOutput = void;
export const getAiHistoryExportEndpoint = {
  operationId: "getAiHistoryExport" as const,
  method: "GET" as const,
  path: "/v1/ai/history/export" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export type GetAiQuotasInput = void;
export type GetAiQuotasOutput = Types.AIAiQuotaStatusOutput;
export const getAiQuotasEndpoint = {
  operationId: "getAiQuotas" as const,
  method: "GET" as const,
  path: "/v1/ai/quotas" as const,
  tags: ["Ai"] as const,
  requiresAuth: true,
} as const;

export interface GetAiPromptTemplatesForGetAiPromptTemplatesInput {
  query?: {
    category?: string;
    includeInactive?: boolean;
  };
}
export type GetAiPromptTemplatesForGetAiPromptTemplatesOutput =
  Array<Types.AIAiPromptTemplate>;
export const getAiPromptTemplatesForGetAiPromptTemplatesEndpoint = {
  operationId: "getAiPromptTemplatesForGetAiPromptTemplates" as const,
  method: "GET" as const,
  path: "/v1/ai/prompt-templates" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface PostAiPromptTemplatesInput {
  body?: Types.AICreateAiPromptTemplateInput;
}
export type PostAiPromptTemplatesOutput = Types.AIAiPromptTemplate;
export const postAiPromptTemplatesEndpoint = {
  operationId: "postAiPromptTemplates" as const,
  method: "POST" as const,
  path: "/v1/ai/prompt-templates" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface GetAiPromptTemplatesForGetAiPromptTemplatesByIdInput {
  id: string;
}
export type GetAiPromptTemplatesForGetAiPromptTemplatesByIdOutput =
  Types.AIAiPromptTemplate;
export const getAiPromptTemplatesForGetAiPromptTemplatesByIdEndpoint = {
  operationId: "getAiPromptTemplatesForGetAiPromptTemplatesById" as const,
  method: "GET" as const,
  path: "/v1/ai/prompt-templates/{id}" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface PutAiPromptTemplatesInput {
  id: string;
  body?: Types.AIUpdateAiPromptTemplateInput;
}
export type PutAiPromptTemplatesOutput = Types.AIAiPromptTemplate;
export const putAiPromptTemplatesEndpoint = {
  operationId: "putAiPromptTemplates" as const,
  method: "PUT" as const,
  path: "/v1/ai/prompt-templates/{id}" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAiPromptTemplatesInput {
  id: string;
}
export type DeleteAiPromptTemplatesOutput = void;
export const deleteAiPromptTemplatesEndpoint = {
  operationId: "deleteAiPromptTemplates" as const,
  method: "DELETE" as const,
  path: "/v1/ai/prompt-templates/{id}" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface PostAiPromptTemplatesRenderInput {
  id: string;
  body?: Types.AIAiPromptTemplateRenderInput;
}
export type PostAiPromptTemplatesRenderOutput =
  Types.AIAiPromptTemplateRenderOutput;
export const postAiPromptTemplatesRenderEndpoint = {
  operationId: "postAiPromptTemplatesRender" as const,
  method: "POST" as const,
  path: "/v1/ai/prompt-templates/{id}/render" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface PostAiPromptTemplatesGenerateInput {
  id: string;
  body?: Types.AIAiPromptTemplateGenerateInput;
}
export type PostAiPromptTemplatesGenerateOutput = Types.AIAiCompletionOutput;
export const postAiPromptTemplatesGenerateEndpoint = {
  operationId: "postAiPromptTemplatesGenerate" as const,
  method: "POST" as const,
  path: "/v1/ai/prompt-templates/{id}/generate" as const,
  tags: ["AiPromptTemplates"] as const,
  requiresAuth: true,
} as const;

export interface PostApiAnalyticsEventsInput {
  body?: Types.AnalyticsTrackAnalyticsEventCommand;
}
export type PostApiAnalyticsEventsOutput = void;
export const postApiAnalyticsEventsEndpoint = {
  operationId: "postApiAnalyticsEvents" as const,
  method: "POST" as const,
  path: "/api/analytics/events" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAnalyticsTimeseriesInput {
  query?: {
    eventName?: string;
    startDate?: string;
    endDate?: string;
    granularity?: Types.AnalyticsTimeSeriesGranularity;
    tenantId?: string;
  };
}
export type GetApiAnalyticsTimeseriesOutput = void;
export const getApiAnalyticsTimeseriesEndpoint = {
  operationId: "getApiAnalyticsTimeseries" as const,
  method: "GET" as const,
  path: "/api/analytics/timeseries" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAnalyticsKpiInput {
  kpiName: string;
  query?: {
    startDate?: string;
    endDate?: string;
    tenantId?: string;
  };
}
export type GetApiAnalyticsKpiOutput = void;
export const getApiAnalyticsKpiEndpoint = {
  operationId: "getApiAnalyticsKpi" as const,
  method: "GET" as const,
  path: "/api/analytics/kpi/{kpiName}" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface PostApiAnalyticsFunnelInput {
  body?: Types.AnalyticsAnalyzeFunnelQuery;
}
export type PostApiAnalyticsFunnelOutput = void;
export const postApiAnalyticsFunnelEndpoint = {
  operationId: "postApiAnalyticsFunnel" as const,
  method: "POST" as const,
  path: "/api/analytics/funnel" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface PostApiAnalyticsWarehouseRunInput {
  body?: Types.AnalyticsAnalyticsWarehouseRunInput;
}
export type PostApiAnalyticsWarehouseRunOutput =
  Types.AnalyticsAnalyticsWarehouseRunOutput;
export const postApiAnalyticsWarehouseRunEndpoint = {
  operationId: "postApiAnalyticsWarehouseRun" as const,
  method: "POST" as const,
  path: "/api/analytics/warehouse/run" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAnalyticsWarehouseFactsInput {
  query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    factName?: string;
    take?: number;
  };
}
export type GetApiAnalyticsWarehouseFactsOutput =
  Array<Types.AnalyticsAnalyticsWarehouseFact>;
export const getApiAnalyticsWarehouseFactsEndpoint = {
  operationId: "getApiAnalyticsWarehouseFacts" as const,
  method: "GET" as const,
  path: "/api/analytics/warehouse/facts" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAnalyticsWarehouseExportInput {
  query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    factName?: string;
    take?: number;
  };
}
export type GetApiAnalyticsWarehouseExportOutput = void;
export const getApiAnalyticsWarehouseExportEndpoint = {
  operationId: "getApiAnalyticsWarehouseExport" as const,
  method: "GET" as const,
  path: "/api/analytics/warehouse/export" as const,
  tags: ["Analytics"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAnalyticsDashboardsInput {
  query?: {
    tenantId?: string;
  };
}
export type GetApiAnalyticsDashboardsOutput = Array<Types.AnalyticsDashboard>;
export const getApiAnalyticsDashboardsEndpoint = {
  operationId: "getApiAnalyticsDashboards" as const,
  method: "GET" as const,
  path: "/api/analytics/dashboards" as const,
  tags: ["AnalyticsDashboards"] as const,
  requiresAuth: true,
} as const;

export interface PostApiAnalyticsDashboardsInput {
  body?: Types.AnalyticsCreateDashboardInput;
}
export type PostApiAnalyticsDashboardsOutput = Types.AnalyticsDashboard;
export const postApiAnalyticsDashboardsEndpoint = {
  operationId: "postApiAnalyticsDashboards" as const,
  method: "POST" as const,
  path: "/api/analytics/dashboards" as const,
  tags: ["AnalyticsDashboards"] as const,
  requiresAuth: true,
} as const;

export interface GetAnalyticsDashboardByIdInput {
  id: string;
}
export type GetAnalyticsDashboardByIdOutput = Types.AnalyticsDashboard;
export const getAnalyticsDashboardByIdEndpoint = {
  operationId: "getAnalyticsDashboardById" as const,
  method: "GET" as const,
  path: "/api/analytics/dashboards/{id}" as const,
  tags: ["AnalyticsDashboards"] as const,
  requiresAuth: true,
} as const;

export interface PutApiAnalyticsDashboardsInput {
  id: string;
  body?: Types.AnalyticsUpdateDashboardInput;
}
export type PutApiAnalyticsDashboardsOutput = Types.AnalyticsDashboard;
export const putApiAnalyticsDashboardsEndpoint = {
  operationId: "putApiAnalyticsDashboards" as const,
  method: "PUT" as const,
  path: "/api/analytics/dashboards/{id}" as const,
  tags: ["AnalyticsDashboards"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsInput {
  body?: Types.LearningAssessmentsCreateAssessmentInput;
}
export type PostAssessmentsOutput = Types.LearningAssessmentsAssessment;
export const postAssessmentsEndpoint = {
  operationId: "postAssessments" as const,
  method: "POST" as const,
  path: "/v1/assessments" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsInput {
  id: string;
}
export type GetAssessmentsOutput = Types.LearningAssessmentsAssessment;
export const getAssessmentsEndpoint = {
  operationId: "getAssessments" as const,
  method: "GET" as const,
  path: "/v1/assessments/{id}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PutAssessmentsInput {
  id: string;
  body?: Types.LearningAssessmentsUpdateAssessmentInput;
}
export type PutAssessmentsOutput = Types.LearningAssessmentsAssessment;
export const putAssessmentsEndpoint = {
  operationId: "putAssessments" as const,
  method: "PUT" as const,
  path: "/v1/assessments/{id}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAssessmentsInput {
  id: string;
}
export type DeleteAssessmentsOutput = void;
export const deleteAssessmentsEndpoint = {
  operationId: "deleteAssessments" as const,
  method: "DELETE" as const,
  path: "/v1/assessments/{id}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsDefinitionInput {
  id: string;
}
export type GetAssessmentsDefinitionOutput =
  Types.LearningAssessmentsAssessmentDefinition;
export const getAssessmentsDefinitionEndpoint = {
  operationId: "getAssessmentsDefinition" as const,
  method: "GET" as const,
  path: "/v1/assessments/{id}/definition" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsCourseInput {
  courseId: string;
}
export type GetAssessmentsCourseOutput =
  Array<Types.LearningAssessmentsAssessment>;
export const getAssessmentsCourseEndpoint = {
  operationId: "getAssessmentsCourse" as const,
  method: "GET" as const,
  path: "/v1/assessments/course/{courseId}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsCourseGroupsInput {
  courseId: string;
}
export type GetAssessmentsCourseGroupsOutput =
  Array<Types.LearningAssessmentsAssessmentGroup>;
export const getAssessmentsCourseGroupsEndpoint = {
  operationId: "getAssessmentsCourseGroups" as const,
  method: "GET" as const,
  path: "/v1/assessments/course/{courseId}/groups" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsCourseAnalyticsInput {
  courseId: string;
}
export type GetAssessmentsCourseAnalyticsOutput =
  Types.LearningAssessmentsCourseAssessmentAnalytics;
export const getAssessmentsCourseAnalyticsEndpoint = {
  operationId: "getAssessmentsCourseAnalytics" as const,
  method: "GET" as const,
  path: "/v1/assessments/course/{courseId}/analytics" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsGroupsInput {
  body?: Types.LearningAssessmentsCreateAssessmentGroupInput;
}
export type PostAssessmentsGroupsOutput =
  Types.LearningAssessmentsAssessmentGroup;
export const postAssessmentsGroupsEndpoint = {
  operationId: "postAssessmentsGroups" as const,
  method: "POST" as const,
  path: "/v1/assessments/groups" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PutAssessmentsGroupsInput {
  id: string;
  body?: Types.LearningAssessmentsUpdateAssessmentGroupInput;
}
export type PutAssessmentsGroupsOutput =
  Types.LearningAssessmentsAssessmentGroup;
export const putAssessmentsGroupsEndpoint = {
  operationId: "putAssessmentsGroups" as const,
  method: "PUT" as const,
  path: "/v1/assessments/groups/{id}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAssessmentsGroupsInput {
  id: string;
}
export type DeleteAssessmentsGroupsOutput = void;
export const deleteAssessmentsGroupsEndpoint = {
  operationId: "deleteAssessmentsGroups" as const,
  method: "DELETE" as const,
  path: "/v1/assessments/groups/{id}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PutAssessmentsGroupInput {
  id: string;
  body?: Types.LearningAssessmentsAssignAssessmentGroupInput;
}
export type PutAssessmentsGroupOutput = Types.LearningAssessmentsAssessment;
export const putAssessmentsGroupEndpoint = {
  operationId: "putAssessmentsGroup" as const,
  method: "PUT" as const,
  path: "/v1/assessments/{id}/group" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsInteractiveVideoCuesInput {
  id: string;
}
export type GetAssessmentsInteractiveVideoCuesOutput =
  Array<Types.LearningAssessmentsInteractiveVideoAssessmentCue>;
export const getAssessmentsInteractiveVideoCuesEndpoint = {
  operationId: "getAssessmentsInteractiveVideoCues" as const,
  method: "GET" as const,
  path: "/v1/assessments/{id}/interactive-video-cues" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsInteractiveVideoCuesInput {
  id: string;
  body?: Types.LearningAssessmentsLinkInteractiveVideoCueInput;
}
export type PostAssessmentsInteractiveVideoCuesOutput =
  Types.LearningAssessmentsInteractiveVideoAssessmentCue;
export const postAssessmentsInteractiveVideoCuesEndpoint = {
  operationId: "postAssessmentsInteractiveVideoCues" as const,
  method: "POST" as const,
  path: "/v1/assessments/{id}/interactive-video-cues" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAssessmentsInteractiveVideoCuesInput {
  id: string;
  cueId: string;
}
export type DeleteAssessmentsInteractiveVideoCuesOutput = void;
export const deleteAssessmentsInteractiveVideoCuesEndpoint = {
  operationId: "deleteAssessmentsInteractiveVideoCues" as const,
  method: "DELETE" as const,
  path: "/v1/assessments/{id}/interactive-video-cues/{cueId}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsInteractiveVideoCuesContentEnrollmentsInput {
  assessmentId: string;
  contentId: string;
  enrollmentId: string;
}
export type GetAssessmentsInteractiveVideoCuesContentEnrollmentsOutput =
  Array<Types.LearningAssessmentsLearnerInteractiveVideoAssessmentCue>;
export const getAssessmentsInteractiveVideoCuesContentEnrollmentsEndpoint = {
  operationId: "getAssessmentsInteractiveVideoCuesContentEnrollments" as const,
  method: "GET" as const,
  path: "/v1/assessments/{assessmentId}/interactive-video-cues/content/{contentId}/enrollments/{enrollmentId}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsRestoreInput {
  id: string;
}
export type PostAssessmentsRestoreOutput = void;
export const postAssessmentsRestoreEndpoint = {
  operationId: "postAssessmentsRestore" as const,
  method: "POST" as const,
  path: "/v1/assessments/{id}/restore" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsSubmissionsStartInput {
  assessmentId: string;
  body?: Types.LearningAssessmentsStartSubmissionInput;
}
export type PostAssessmentsSubmissionsStartOutput =
  Types.LearningAssessmentsLearnerAssessmentAttempt;
export const postAssessmentsSubmissionsStartEndpoint = {
  operationId: "postAssessmentsSubmissionsStart" as const,
  method: "POST" as const,
  path: "/v1/assessments/{assessmentId}/submissions/start" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsSubmissionsSubmitInput {
  submissionId: string;
  body?: Types.LearningAssessmentsSubmitAssessmentInput;
}
export type PostAssessmentsSubmissionsSubmitOutput =
  Types.LearningAssessmentsLearnerAssessmentSubmission;
export const postAssessmentsSubmissionsSubmitEndpoint = {
  operationId: "postAssessmentsSubmissionsSubmit" as const,
  method: "POST" as const,
  path: "/v1/assessments/submissions/{submissionId}/submit" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface PostAssessmentsSubmissionsGradeInput {
  submissionId: string;
  body?: Types.LearningAssessmentsGradeSubmissionInput;
}
export type PostAssessmentsSubmissionsGradeOutput =
  Types.LearningAssessmentsAssessmentSubmission;
export const postAssessmentsSubmissionsGradeEndpoint = {
  operationId: "postAssessmentsSubmissionsGrade" as const,
  method: "POST" as const,
  path: "/v1/assessments/submissions/{submissionId}/grade" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionIdInput {
  submissionId: string;
}
export type GetAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionIdOutput =
  void;
export const getAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionIdEndpoint =
  {
    operationId:
      "getAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionId" as const,
    method: "GET" as const,
    path: "/v1/assessments/submissions/{submissionId}" as const,
    tags: ["LearningAssessments"] as const,
    requiresAuth: true,
  } as const;

export interface GetAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissionsInput {
  assessmentId: string;
}
export type GetAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissionsOutput =
  Array<Types.LearningAssessmentsAssessmentSubmission>;
export const getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissionsEndpoint =
  {
    operationId:
      "getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions" as const,
    method: "GET" as const,
    path: "/v1/assessments/{assessmentId}/submissions" as const,
    tags: ["LearningAssessments"] as const,
    requiresAuth: true,
  } as const;

export interface GetAssessmentsMySubmissionsInput {
  enrollmentId: string;
}
export type GetAssessmentsMySubmissionsOutput =
  Array<Types.LearningAssessmentsLearnerAssessmentSubmission>;
export const getAssessmentsMySubmissionsEndpoint = {
  operationId: "getAssessmentsMySubmissions" as const,
  method: "GET" as const,
  path: "/v1/assessments/my-submissions/{enrollmentId}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssessmentsCanAttemptInput {
  assessmentId: string;
  enrollmentId: string;
}
export type GetAssessmentsCanAttemptOutput =
  Types.LearningAssessmentsCanAttemptOutput;
export const getAssessmentsCanAttemptEndpoint = {
  operationId: "getAssessmentsCanAttempt" as const,
  method: "GET" as const,
  path: "/v1/assessments/{assessmentId}/can-attempt/{enrollmentId}" as const,
  tags: ["LearningAssessments"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetLibrariesInput {
  resourceType: string;
  resourceId: string;
}
export type GetAssetLibrariesOutput = void;
export const getAssetLibrariesEndpoint = {
  operationId: "getAssetLibraries" as const,
  method: "GET" as const,
  path: "/v1/asset-libraries/{resourceType}/{resourceId}" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetLibrariesFoldersInput {
  resourceType: string;
  resourceId: string;
  body?: Types.AssetsControllersCreateAssetFolderInput;
}
export type PostAssetLibrariesFoldersOutput = void;
export const postAssetLibrariesFoldersEndpoint = {
  operationId: "postAssetLibrariesFolders" as const,
  method: "POST" as const,
  path: "/v1/asset-libraries/{resourceType}/{resourceId}/folders" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface PutAssetLibrariesFoldersRestrictionInput {
  folderId: string;
  body?: Types.AssetsControllersRestrictAssetFolderInput;
}
export type PutAssetLibrariesFoldersRestrictionOutput = void;
export const putAssetLibrariesFoldersRestrictionEndpoint = {
  operationId: "putAssetLibrariesFoldersRestriction" as const,
  method: "PUT" as const,
  path: "/v1/asset-libraries/folders/{folderId}/restriction" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetLibrariesAssetsCopyInput {
  referenceId: string;
  body?: Types.AssetsControllersCopyAssetReferenceInput;
}
export type PostAssetLibrariesAssetsCopyOutput = void;
export const postAssetLibrariesAssetsCopyEndpoint = {
  operationId: "postAssetLibrariesAssetsCopy" as const,
  method: "POST" as const,
  path: "/v1/asset-libraries/assets/{referenceId}/copy" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetLibrariesAssetsRevisionsInput {
  referenceId: string;
}
export type GetAssetLibrariesAssetsRevisionsOutput = void;
export const getAssetLibrariesAssetsRevisionsEndpoint = {
  operationId: "getAssetLibrariesAssetsRevisions" as const,
  method: "GET" as const,
  path: "/v1/asset-libraries/assets/{referenceId}/revisions" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetLibrariesAssetsRevisionsRestoreInput {
  referenceId: string;
  revisionId: string;
}
export type PostAssetLibrariesAssetsRevisionsRestoreOutput = void;
export const postAssetLibrariesAssetsRevisionsRestoreEndpoint = {
  operationId: "postAssetLibrariesAssetsRevisionsRestore" as const,
  method: "POST" as const,
  path: "/v1/asset-libraries/assets/{referenceId}/revisions/{revisionId}/restore" as const,
  tags: ["AssetsLibraries"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsForGetAssetsInput {
  query?: {
    owner?: string;
    parentType?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  };
}
export type GetAssetsForGetAssetsOutput = void;
export const getAssetsForGetAssetsEndpoint = {
  operationId: "getAssetsForGetAssets" as const,
  method: "GET" as const,
  path: "/v1/assets" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsInput {
  query?: {
    displayName?: string;
    accessPolicy?: Types.AssetsAssetAccessPolicy;
    parentResourceType?: string;
    parentResourceId?: string;
    folderId?: string;
  };
  body?: FormData;
}
export type PostAssetsOutput = void;
export const postAssetsEndpoint = {
  operationId: "postAssets" as const,
  method: "POST" as const,
  path: "/v1/assets" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsBulkUploadInput {
  query?: {
    accessPolicy?: Types.AssetsAssetAccessPolicy;
    parentResourceType?: string;
    parentResourceId?: string;
    folderId?: string;
  };
  body?: FormData;
}
export type PostAssetsBulkUploadOutput =
  Types.AssetsCommandsBulkUploadAssetsOutput;
export const postAssetsBulkUploadEndpoint = {
  operationId: "postAssetsBulkUpload" as const,
  method: "POST" as const,
  path: "/v1/assets/bulk-upload" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsChunkedUploadsInput {
  query?: {
    fileName?: string;
    mimeType?: string;
    totalSize?: number;
  };
}
export type PostAssetsChunkedUploadsOutput = Types.AssetsChunkedUploadSession;
export const postAssetsChunkedUploadsEndpoint = {
  operationId: "postAssetsChunkedUploads" as const,
  method: "POST" as const,
  path: "/v1/assets/chunked-uploads" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsChunkedUploadsPartsInput {
  uploadId: string;
  query?: {
    chunkIndex?: number;
  };
  body?: FormData;
}
export type PostAssetsChunkedUploadsPartsOutput = void;
export const postAssetsChunkedUploadsPartsEndpoint = {
  operationId: "postAssetsChunkedUploadsParts" as const,
  method: "POST" as const,
  path: "/v1/assets/chunked-uploads/{uploadId}/parts" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsChunkedUploadsCompleteInput {
  uploadId: string;
  query?: {
    displayName?: string;
    accessPolicy?: Types.AssetsAssetAccessPolicy;
    parentResourceType?: string;
    parentResourceId?: string;
    folderId?: string;
  };
}
export type PostAssetsChunkedUploadsCompleteOutput =
  Types.AssetsAssetUploadResult;
export const postAssetsChunkedUploadsCompleteEndpoint = {
  operationId: "postAssetsChunkedUploadsComplete" as const,
  method: "POST" as const,
  path: "/v1/assets/chunked-uploads/{uploadId}:complete" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAssetsChunkedUploadsInput {
  uploadId: string;
}
export type DeleteAssetsChunkedUploadsOutput = void;
export const deleteAssetsChunkedUploadsEndpoint = {
  operationId: "deleteAssetsChunkedUploads" as const,
  method: "DELETE" as const,
  path: "/v1/assets/chunked-uploads/{uploadId}" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsForGetAssetsByIdInput {
  id: string;
  query?: {
    includeContent?: boolean;
  };
}
export type GetAssetsForGetAssetsByIdOutput = void;
export const getAssetsForGetAssetsByIdEndpoint = {
  operationId: "getAssetsForGetAssetsById" as const,
  method: "GET" as const,
  path: "/v1/assets/{id}" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface DeleteAssetsInput {
  id: string;
}
export type DeleteAssetsOutput = void;
export const deleteAssetsEndpoint = {
  operationId: "deleteAssets" as const,
  method: "DELETE" as const,
  path: "/v1/assets/{id}" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PatchAssetsInput {
  id: string;
  body?: Types.AssetsControllersUpdateAssetInput;
}
export type PatchAssetsOutput = void;
export const patchAssetsEndpoint = {
  operationId: "patchAssets" as const,
  method: "PATCH" as const,
  path: "/v1/assets/{id}" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsPreviewInput {
  id: string;
  query?: {
    includeExtractedText?: boolean;
    thumbnailWidth?: number;
    thumbnailHeight?: number;
  };
}
export type GetAssetsPreviewOutput = Types.AssetsQueriesAssetPreviewOutput;
export const getAssetsPreviewEndpoint = {
  operationId: "getAssetsPreview" as const,
  method: "GET" as const,
  path: "/v1/assets/{id}/preview" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetExtractedTextInput {
  id: string;
}
export type GetAssetExtractedTextOutput =
  Types.AssetsControllersAssetExtractedTextOutput;
export const getAssetExtractedTextEndpoint = {
  operationId: "getAssetExtractedText" as const,
  method: "GET" as const,
  path: "/v1/assets/{id}/extracted-text" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsGenerateAccessUrlInput {
  id: string;
  query?: {
    width?: number;
    height?: number;
    fit?: Types.AssetsImageFit;
    format?: Types.AssetsImageFormat;
    quality?: number;
    direct?: boolean;
  };
}
export type PostAssetsGenerateAccessUrlOutput = void;
export const postAssetsGenerateAccessUrlEndpoint = {
  operationId: "postAssetsGenerateAccessUrl" as const,
  method: "POST" as const,
  path: "/v1/assets/{id}:generate-access-url" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsBulkDownloadInput {
  body?: Types.AssetsControllersBulkAssetAccessUrlInput;
}
export type PostAssetsBulkDownloadOutput =
  Types.AssetsQueriesBulkAssetAccessUrlsOutput;
export const postAssetsBulkDownloadEndpoint = {
  operationId: "postAssetsBulkDownload" as const,
  method: "POST" as const,
  path: "/v1/assets/bulk-download" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsContentInput {
  id: string;
  query?: {
    token?: string;
    transform?: string;
  };
}
export type GetAssetsContentOutput = void;
export const getAssetsContentEndpoint = {
  operationId: "getAssetsContent" as const,
  method: "GET" as const,
  path: "/v1/assets/{id}/content" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetSignedAssetExtractedTextInput {
  id: string;
  query?: {
    token?: string;
  };
}
export type GetSignedAssetExtractedTextOutput = void;
export const getSignedAssetExtractedTextEndpoint = {
  operationId: "getSignedAssetExtractedText" as const,
  method: "GET" as const,
  path: "/v1/assets/{id}:extracted-text" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsBulkDeleteInput {
  body?: Types.AssetsControllersBulkDeleteAssetsInput;
}
export type PostAssetsBulkDeleteOutput =
  Types.AssetsCommandsBulkDeleteAssetsOutput;
export const postAssetsBulkDeleteEndpoint = {
  operationId: "postAssetsBulkDelete" as const,
  method: "POST" as const,
  path: "/v1/assets/bulk-delete" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface PostAssetsReportInput {
  id: string;
  body?: Types.AssetsControllersReportAssetInput;
}
export type PostAssetsReportOutput = void;
export const postAssetsReportEndpoint = {
  operationId: "postAssetsReport" as const,
  method: "POST" as const,
  path: "/v1/assets/{id}:report" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsSearchInput {
  query?: {
    q?: string;
    kind?: Types.AssetsAssetKind;
    parentType?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  };
}
export type GetAssetsSearchOutput = Types.AssetsQueriesAssetSearchOutput;
export const getAssetsSearchEndpoint = {
  operationId: "getAssetsSearch" as const,
  method: "GET" as const,
  path: "/v1/assets/search" as const,
  tags: ["Assets"] as const,
  requiresAuth: true,
} as const;

export type GetAdminAssetsStatisticsInput = void;
export type GetAdminAssetsStatisticsOutput =
  Types.AssetsQueriesAssetStatisticsOutput;
export const getAdminAssetsStatisticsEndpoint = {
  operationId: "getAdminAssetsStatistics" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/statistics" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsStatisticsExportInput {
  query?: {
    format?: string;
  };
}
export type GetAdminAssetsStatisticsExportOutput = Blob;
export const getAdminAssetsStatisticsExportEndpoint = {
  operationId: "getAdminAssetsStatisticsExport" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/statistics:export" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsModerationQueueInput {
  query?: {
    limit?: number;
  };
}
export type GetAdminAssetsModerationQueueOutput = void;
export const getAdminAssetsModerationQueueEndpoint = {
  operationId: "getAdminAssetsModerationQueue" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/moderation-queue" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsReportsInput {
  id: string;
}
export type GetAdminAssetsReportsOutput = void;
export const getAdminAssetsReportsEndpoint = {
  operationId: "getAdminAssetsReports" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/{id}/reports" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsReportsReviewInput {
  reportId: string;
  body?: Types.AssetsControllersReviewReportInput;
}
export type PostAdminAssetsReportsReviewOutput = void;
export const postAdminAssetsReportsReviewEndpoint = {
  operationId: "postAdminAssetsReportsReview" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/reports/{reportId}:review" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsForceDeleteInput {
  id: string;
}
export type PostAdminAssetsForceDeleteOutput = void;
export const postAdminAssetsForceDeleteEndpoint = {
  operationId: "postAdminAssetsForceDelete" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/{id}:force-delete" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsInput {
  query?: {
    status?: string;
    limit?: number;
  };
}
export type GetAdminAssetsOutput = void;
export const getAdminAssetsEndpoint = {
  operationId: "getAdminAssets" as const,
  method: "GET" as const,
  path: "/v1/admin/assets" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsGcCandidatesInput {
  query?: {
    gracePeriodHours?: number;
    limit?: number;
  };
}
export type GetAdminAssetsGcCandidatesOutput = void;
export const getAdminAssetsGcCandidatesEndpoint = {
  operationId: "getAdminAssetsGcCandidates" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/gc-candidates" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsRunVirusScanInput {
  contentId: string;
  body?: Types.AssetsControllersUpdateVirusScanInput;
}
export type PostAdminAssetsRunVirusScanOutput = void;
export const postAdminAssetsRunVirusScanEndpoint = {
  operationId: "postAdminAssetsRunVirusScan" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/{contentId}:run-virus-scan" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAssetsRetentionInput {
  query?: {
    gracePeriodHours?: number;
    limit?: number;
  };
}
export type GetAdminAssetsRetentionOutput =
  Types.AssetsQueriesAssetRetentionReportOutput;
export const getAdminAssetsRetentionEndpoint = {
  operationId: "getAdminAssetsRetention" as const,
  method: "GET" as const,
  path: "/v1/admin/assets/retention" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsRunGcInput {
  query?: {
    gracePeriodHours?: number;
    limit?: number;
    dryRun?: boolean;
  };
}
export type PostAdminAssetsRunGcOutput = void;
export const postAdminAssetsRunGcEndpoint = {
  operationId: "postAdminAssetsRunGc" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/:run-gc" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsRetentionRunInput {
  query?: {
    gracePeriodHours?: number;
    limit?: number;
    dryRun?: boolean;
  };
}
export type PostAdminAssetsRetentionRunOutput = void;
export const postAdminAssetsRetentionRunEndpoint = {
  operationId: "postAdminAssetsRetentionRun" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/retention:run" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsMarkUndeletableInput {
  contentId: string;
  body?: Types.AssetsControllersMarkNonDeletableInput;
}
export type PostAdminAssetsMarkUndeletableOutput = void;
export const postAdminAssetsMarkUndeletableEndpoint = {
  operationId: "postAdminAssetsMarkUndeletable" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/{contentId}:mark-undeletable" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsUnmarkUndeletableInput {
  contentId: string;
}
export type PostAdminAssetsUnmarkUndeletableOutput = void;
export const postAdminAssetsUnmarkUndeletableEndpoint = {
  operationId: "postAdminAssetsUnmarkUndeletable" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/{contentId}:unmark-undeletable" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAssetsReviewModerationInput {
  contentId: string;
  body?: Types.AssetsControllersContentModerationInput;
}
export type PostAdminAssetsReviewModerationOutput = void;
export const postAdminAssetsReviewModerationEndpoint = {
  operationId: "postAdminAssetsReviewModeration" as const,
  method: "POST" as const,
  path: "/v1/admin/assets/{contentId}:review-moderation" as const,
  tags: ["AssetsAdmin"] as const,
  requiresAuth: true,
} as const;

export interface GetAssetsForGetAssetsByReferenceIdByTokenInput {
  referenceId: string;
  token: string;
}
export type GetAssetsForGetAssetsByReferenceIdByTokenOutput = void;
export const getAssetsForGetAssetsByReferenceIdByTokenEndpoint = {
  operationId: "getAssetsForGetAssetsByReferenceIdByToken" as const,
  method: "GET" as const,
  path: "/assets/{referenceId}/{token}" as const,
  tags: ["AssetsCdn"] as const,
  requiresAuth: true,
} as const;

export interface GetEInput {
  token: string;
}
export type GetEOutput = void;
export const getEEndpoint = {
  operationId: "getE" as const,
  method: "GET" as const,
  path: "/e/{token}" as const,
  tags: ["AssetsCdn"] as const,
  requiresAuth: true,
} as const;

export interface GetTInput {
  transformation: string;
  referenceId: string;
  token: string;
}
export type GetTOutput = void;
export const getTEndpoint = {
  operationId: "getT" as const,
  method: "GET" as const,
  path: "/t/{transformation}/{referenceId}/{token}" as const,
  tags: ["AssetsCdn"] as const,
  requiresAuth: true,
} as const;

export interface GetApiAssetsContentInput {
  assetId: string;
  query?: {
    token?: string;
    transform?: string;
  };
}
export type GetApiAssetsContentOutput = void;
export const getApiAssetsContentEndpoint = {
  operationId: "getApiAssetsContent" as const,
  method: "GET" as const,
  path: "/api/assets/{assetId}/content" as const,
  tags: ["AssetsSecureDelivery"] as const,
  requiresAuth: true,
} as const;

export interface PostApiAssetsAccessUrlInput {
  assetId: string;
  body?: Types.AssetsSecurityAccessUrlInput;
}
export type PostApiAssetsAccessUrlOutput = Types.AssetsAssetAccessUrl;
export const postApiAssetsAccessUrlEndpoint = {
  operationId: "postApiAssetsAccessUrl" as const,
  method: "POST" as const,
  path: "/api/assets/{assetId}/access-url" as const,
  tags: ["AssetsSecureDelivery"] as const,
  requiresAuth: true,
} as const;

/**
 * Register a new user
 *
 * Creates a new user account with email and password credentials, returning authentication tokens on success.
 */
export interface PostAuthSignUpInput {
  body?: Types.IdentityAuthenticationLocalSignUpInput;
}
export type PostAuthSignUpOutput = Types.IdentityAuthenticationSignInOutput;
export const postAuthSignUpEndpoint = {
  operationId: "postAuthSignUp" as const,
  method: "POST" as const,
  path: "/v1/auth/sign-up" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Sign in with email and password
 *
 * Authenticates a user with email and password credentials, returning access and refresh tokens.
 */
export interface PostAuthSignInInput {
  body?: Types.IdentityAuthenticationLocalSignInInput;
}
export type PostAuthSignInOutput = Types.IdentityAuthenticationSignInOutput;
export const postAuthSignInEndpoint = {
  operationId: "postAuthSignIn" as const,
  method: "POST" as const,
  path: "/v1/auth/sign-in" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Sign in with Google ID Token
 *
 * Authenticates a user using a Google ID Token (for NextAuth.js integration), returning access and refresh tokens. Account-linking counterpart: POST /v1/auth/external-logins/google.
 */
export interface PostAuthGoogleSignInInput {
  body?: Types.IdentityAuthenticationGoogleIdTokenInput;
}
export type PostAuthGoogleSignInOutput =
  Types.IdentityAuthenticationSignInOutput;
export const postAuthGoogleSignInEndpoint = {
  operationId: "postAuthGoogleSignIn" as const,
  method: "POST" as const,
  path: "/v1/auth/google:sign-in" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Request magic sign-in link
 *
 * Generates a short-lived one-time sign-in token and dispatches the magic-link notification. Always returns a generic success response to prevent user enumeration.
 */
export interface PostAuthMagicLinkRequestInput {
  body?: Types.IdentityAuthenticationRequestMagicLinkInput;
}
export type PostAuthMagicLinkRequestOutput =
  Types.IdentityAuthenticationMagicLinkRequestResult;
export const postAuthMagicLinkRequestEndpoint = {
  operationId: "postAuthMagicLinkRequest" as const,
  method: "POST" as const,
  path: "/v1/auth/magic-link:request" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Consume magic sign-in link
 *
 * Consumes a short-lived one-time magic-link token and returns access and refresh tokens.
 */
export interface PostAuthMagicLinkConsumeInput {
  body?: Types.IdentityAuthenticationConsumeMagicLinkInput;
}
export type PostAuthMagicLinkConsumeOutput =
  Types.IdentityAuthenticationSignInOutput;
export const postAuthMagicLinkConsumeEndpoint = {
  operationId: "postAuthMagicLinkConsume" as const,
  method: "POST" as const,
  path: "/v1/auth/magic-link:consume" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Initiate GitHub OAuth sign-in
 *
 * Initiates GitHub OAuth authentication flow and returns the authorization URL.
 */
export interface GetAuthGithubAuthorizeInput {
  query?: {
    redirectUri?: string;
  };
}
export type GetAuthGithubAuthorizeOutput =
  Types.IdentityAuthenticationGitHubSignInOutput;
export const getAuthGithubAuthorizeEndpoint = {
  operationId: "getAuthGithubAuthorize" as const,
  method: "GET" as const,
  path: "/v1/auth/github:authorize" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Initiate Discord OAuth sign-in
 *
 * Initiates the Discord OAuth authorization-code sign-in flow and returns the authorization URL with the CSRF state parameter. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-authorize.
 */
export interface PostAuthDiscordSignInAuthorizeInput {
  body?: Types.IdentityAuthenticationDiscordAuthorizeInput;
}
export type PostAuthDiscordSignInAuthorizeOutput =
  Types.IdentityAuthenticationDiscordSignInOutput;
export const postAuthDiscordSignInAuthorizeEndpoint = {
  operationId: "postAuthDiscordSignInAuthorize" as const,
  method: "POST" as const,
  path: "/v1/auth/discord:sign-in-authorize" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Discord OAuth sign-in callback
 *
 * Exchanges the Discord OAuth authorization code for access and refresh tokens, applying the same account matching and auto-link policy as Google sign-in. Account-linking counterpart: POST /v1/auth/external-logins/discord:link-callback.
 */
export interface PostAuthDiscordSignInCallbackInput {
  body?: Types.IdentityAuthenticationDiscordCallbackInput;
}
export type PostAuthDiscordSignInCallbackOutput =
  Types.IdentityAuthenticationSignInOutput;
export const postAuthDiscordSignInCallbackEndpoint = {
  operationId: "postAuthDiscordSignInCallback" as const,
  method: "POST" as const,
  path: "/v1/auth/discord:sign-in-callback" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Refresh access token
 *
 * Exchanges a valid refresh token for a new access token and refresh token pair.
 */
export interface PostAuthTokensRefreshInput {
  body?: Types.IdentityAuthenticationRefreshTokenInput;
}
export type PostAuthTokensRefreshOutput =
  Types.IdentityAuthenticationSignInOutput;
export const postAuthTokensRefreshEndpoint = {
  operationId: "postAuthTokensRefresh" as const,
  method: "POST" as const,
  path: "/v1/auth/tokens:refresh" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Revoke refresh token
 *
 * Invalidates a refresh token, preventing it from being used to obtain new access tokens.
 */
export interface PostAuthTokensRevokeInput {
  body?: Types.IdentityAuthenticationRevokeRefreshTokenInput;
}
export type PostAuthTokensRevokeOutput = void;
export const postAuthTokensRevokeEndpoint = {
  operationId: "postAuthTokensRevoke" as const,
  method: "POST" as const,
  path: "/v1/auth/tokens:revoke" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Generate Web3 authentication challenge
 *
 * Generates a cryptographic challenge that must be signed by the user's wallet to prove ownership.
 */
export interface PostAuthWeb3ChallengeInput {
  body?: Types.IdentityAuthenticationWeb3ChallengeInput;
}
export type PostAuthWeb3ChallengeOutput =
  Types.IdentityAuthenticationWeb3ChallengeOutput;
export const postAuthWeb3ChallengeEndpoint = {
  operationId: "postAuthWeb3Challenge" as const,
  method: "POST" as const,
  path: "/v1/auth/web3/challenge" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Send email verification
 *
 * Sends a verification email to the specified email address to confirm ownership.
 */
export interface PostAuthEmailSendVerificationInput {
  body?: Types.IdentityAuthenticationSendEmailVerificationInput;
}
export type PostAuthEmailSendVerificationOutput =
  Types.IdentityAuthenticationEmailVerificationOutput;
export const postAuthEmailSendVerificationEndpoint = {
  operationId: "postAuthEmailSendVerification" as const,
  method: "POST" as const,
  path: "/v1/auth/email:send-verification" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Verify email with token
 *
 * Verifies the user's email address using a token received via email.
 */
export interface PostAuthEmailVerifyInput {
  body?: Types.IdentityAuthenticationVerifyEmailInput;
}
export type PostAuthEmailVerifyOutput =
  Types.IdentityAuthenticationEmailVerificationResult;
export const postAuthEmailVerifyEndpoint = {
  operationId: "postAuthEmailVerify" as const,
  method: "POST" as const,
  path: "/v1/auth/email:verify" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Request password reset
 *
 * Sends a password reset link to the specified email address. Always returns success for security.
 */
export interface PostAuthPasswordResetRequestInput {
  body?: Types.IdentityAuthenticationRequestPasswordResetInput;
}
export type PostAuthPasswordResetRequestOutput =
  Types.IdentityAuthenticationPasswordResetRequestResult;
export const postAuthPasswordResetRequestEndpoint = {
  operationId: "postAuthPasswordResetRequest" as const,
  method: "POST" as const,
  path: "/v1/auth/password:reset-request" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Complete password reset
 *
 * Resets the user's password using a token received via email.
 */
export interface PostAuthPasswordResetInput {
  body?: Types.IdentityAuthenticationCompletePasswordResetInput;
}
export type PostAuthPasswordResetOutput =
  Types.IdentityAuthenticationPasswordResetResult;
export const postAuthPasswordResetEndpoint = {
  operationId: "postAuthPasswordReset" as const,
  method: "POST" as const,
  path: "/v1/auth/password:reset" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Change password
 *
 * Changes the password for the currently authenticated user.
 */
export interface PostAuthPasswordChangeInput {
  body?: Types.IdentityAuthenticationPasswordChangeInput;
}
export type PostAuthPasswordChangeOutput =
  Types.IdentityAuthenticationPasswordChangeResult;
export const postAuthPasswordChangeEndpoint = {
  operationId: "postAuthPasswordChange" as const,
  method: "POST" as const,
  path: "/v1/auth/password:change" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * GitHub OAuth callback
 *
 * Handles the GitHub OAuth callback, exchanging the authorization code for tokens.
 */
export interface GetAuthGithubCallbackInput {
  query?: {
    code?: string;
    state?: string;
  };
}
export type GetAuthGithubCallbackOutput =
  Types.IdentityAuthenticationSignInOutput;
export const getAuthGithubCallbackEndpoint = {
  operationId: "getAuthGithubCallback" as const,
  method: "GET" as const,
  path: "/v1/auth/github:callback" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Verify Web3 signature
 *
 * Verifies a Web3 wallet signature against a previously issued challenge and returns authentication tokens.
 */
export interface PostAuthWeb3VerifyInput {
  body?: Types.IdentityAuthenticationWeb3VerifyInput;
}
export type PostAuthWeb3VerifyOutput = Types.IdentityAuthenticationSignInOutput;
export const postAuthWeb3VerifyEndpoint = {
  operationId: "postAuthWeb3Verify" as const,
  method: "POST" as const,
  path: "/v1/auth/web3:verify" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * List linked external logins
 *
 * HEAD request per Google REST guidance: safe, metadata-only response with no body. Linked providers and their linked-at timestamps are conveyed in the X-Linked-Providers response header as comma-separated 'provider=iso8601-timestamp' pairs, newest first. The header is omitted when no providers are linked.
 */
export type HeadAuthExternalLoginsInput = void;
export type HeadAuthExternalLoginsOutput = void;
export const headAuthExternalLoginsEndpoint = {
  operationId: "headAuthExternalLogins" as const,
  method: "HEAD" as const,
  path: "/v1/auth/external-logins" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Link Google account
 *
 * Verifies a Google ID token and links the Google identity to the authenticated user. Idempotent when already linked to the same user.
 */
export interface PostAuthExternalLoginsGoogleInput {
  body?: Types.IdentityAuthenticationLinkGoogleAccountInput;
}
export type PostAuthExternalLoginsGoogleOutput = void;
export const postAuthExternalLoginsGoogleEndpoint = {
  operationId: "postAuthExternalLoginsGoogle" as const,
  method: "POST" as const,
  path: "/v1/auth/external-logins/google" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Start Discord account link
 *
 * Returns the Discord OAuth authorization URL plus the state parameter to validate at the callback.
 */
export interface PostAuthExternalLoginsDiscordLinkAuthorizeInput {
  body?: Types.IdentityAuthenticationDiscordLinkAuthorizeInput;
}
export type PostAuthExternalLoginsDiscordLinkAuthorizeOutput =
  Types.IdentityAuthenticationDiscordLinkAuthorizeOutput;
export const postAuthExternalLoginsDiscordLinkAuthorizeEndpoint = {
  operationId: "postAuthExternalLoginsDiscordLinkAuthorize" as const,
  method: "POST" as const,
  path: "/v1/auth/external-logins/discord:link-authorize" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Complete Discord account link
 *
 * Exchanges the Discord authorization code for the user profile and links the Discord identity to the authenticated user. Idempotent when already linked to the same user.
 */
export interface PostAuthExternalLoginsDiscordLinkCallbackInput {
  body?: Types.IdentityAuthenticationDiscordLinkCallbackInput;
}
export type PostAuthExternalLoginsDiscordLinkCallbackOutput = void;
export const postAuthExternalLoginsDiscordLinkCallbackEndpoint = {
  operationId: "postAuthExternalLoginsDiscordLinkCallback" as const,
  method: "POST" as const,
  path: "/v1/auth/external-logins/discord:link-callback" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * Unlink external login
 *
 * Removes the external login link for the given provider. Refused with 400 when it is the user's last sign-in method and no password is set.
 */
export interface DeleteAuthExternalLoginsInput {
  provider: string;
}
export type DeleteAuthExternalLoginsOutput = void;
export const deleteAuthExternalLoginsEndpoint = {
  operationId: "deleteAuthExternalLogins" as const,
  method: "DELETE" as const,
  path: "/v1/auth/external-logins/{provider}" as const,
  tags: ["Auth"] as const,
  requiresAuth: true,
} as const;

/**
 * List all API keys
 */
export type GetAuthApiKeysInput = void;
export type GetAuthApiKeysOutput = Array<Types.IdentityAuthenticationApiKey>;
export const getAuthApiKeysEndpoint = {
  operationId: "getAuthApiKeys" as const,
  method: "GET" as const,
  path: "/v1/auth/api-keys" as const,
  tags: ["AuthApiKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a new API key
 */
export interface PostAuthApiKeysInput {
  body?: Types.IdentityAuthenticationCreateApiKeyCommand;
}
export type PostAuthApiKeysOutput =
  Types.IdentityAuthenticationCreateApiKeyOutput;
export const postAuthApiKeysEndpoint = {
  operationId: "postAuthApiKeys" as const,
  method: "POST" as const,
  path: "/v1/auth/api-keys" as const,
  tags: ["AuthApiKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Revoke an API key
 */
export interface PostAuthApiKeysRevokeInput {
  keyId: string;
  body?: Types.IdentityAuthenticationRevokeApiKeyInput;
}
export type PostAuthApiKeysRevokeOutput = void;
export const postAuthApiKeysRevokeEndpoint = {
  operationId: "postAuthApiKeysRevoke" as const,
  method: "POST" as const,
  path: "/v1/auth/api-keys/{keyId}:revoke" as const,
  tags: ["AuthApiKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Get MFA configuration
 *
 * Retrieves the current user's multi-factor authentication configuration and enabled methods.
 */
export type GetAuthMfaInput = void;
export type GetAuthMfaOutput =
  Types.IdentityAuthenticationMfaConfigurationOutput;
export const getAuthMfaEndpoint = {
  operationId: "getAuthMfa" as const,
  method: "GET" as const,
  path: "/v1/auth/mfa" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Initiate TOTP setup
 *
 * Initiates Time-based One-Time Password (TOTP) setup, returning a secret key and QR code URI for authenticator apps.
 */
export type PostAuthMfaTotpSetupInput = void;
export type PostAuthMfaTotpSetupOutput =
  Types.IdentityAuthenticationMfaSetupOutput;
export const postAuthMfaTotpSetupEndpoint = {
  operationId: "postAuthMfaTotpSetup" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/totp:setup" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Complete TOTP setup
 *
 * Completes TOTP setup by verifying a code from the user's authenticator app.
 */
export interface PostAuthMfaTotpCompleteInput {
  body?: Types.IdentityAuthenticationCompleteMfaSetupInput;
}
export type PostAuthMfaTotpCompleteOutput =
  Types.IdentityAuthenticationMfaSuccessOutput;
export const postAuthMfaTotpCompleteEndpoint = {
  operationId: "postAuthMfaTotpComplete" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/totp:complete" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Verify MFA code
 *
 * Verifies an MFA code during the authentication flow. Used after initial sign-in when MFA is required.
 */
export interface PostAuthMfaVerifyInput {
  body?: Types.IdentityAuthenticationVerifyMfaInput;
}
export type PostAuthMfaVerifyOutput =
  Types.IdentityAuthenticationMfaVerificationOutput;
export const postAuthMfaVerifyEndpoint = {
  operationId: "postAuthMfaVerify" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/verify" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Get backup codes
 *
 * Retrieves the user's backup codes status. Codes are not returned for security; use regenerate to get new codes.
 */
export type GetAuthMfaBackupCodesInput = void;
export type GetAuthMfaBackupCodesOutput =
  Types.IdentityAuthenticationBackupCodesStatusOutput;
export const getAuthMfaBackupCodesEndpoint = {
  operationId: "getAuthMfaBackupCodes" as const,
  method: "GET" as const,
  path: "/v1/auth/mfa/backup-codes" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Regenerate backup codes
 *
 * Generates a new set of backup codes, invalidating any previously generated codes.
 */
export type PostAuthMfaBackupCodesRegenerateInput = void;
export type PostAuthMfaBackupCodesRegenerateOutput =
  Types.IdentityAuthenticationBackupCodesOutput;
export const postAuthMfaBackupCodesRegenerateEndpoint = {
  operationId: "postAuthMfaBackupCodesRegenerate" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/backup-codes:regenerate" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Setup SMS MFA
 *
 * Initiates SMS-based MFA setup by sending a verification code to the provided phone number.
 */
export interface PostAuthMfaSmsSetupInput {
  body?: Types.IdentityAuthenticationSmsMfaSetupInput;
}
export type PostAuthMfaSmsSetupOutput =
  Types.IdentityAuthenticationSmsMfaSetupOutput;
export const postAuthMfaSmsSetupEndpoint = {
  operationId: "postAuthMfaSmsSetup" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/sms:setup" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Complete SMS MFA setup
 *
 * Completes SMS MFA setup by verifying the code sent to the user's phone.
 */
export interface PostAuthMfaSmsCompleteInput {
  body?: Types.IdentityAuthenticationCompleteMfaSetupInput;
}
export type PostAuthMfaSmsCompleteOutput =
  Types.IdentityAuthenticationMfaSuccessOutput;
export const postAuthMfaSmsCompleteEndpoint = {
  operationId: "postAuthMfaSmsComplete" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa/sms:complete" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * List MFA methods
 *
 * Returns all available MFA methods and their configuration status for the current user.
 */
export type GetAuthMfaMethodsInput = void;
export type GetAuthMfaMethodsOutput =
  Types.IdentityAuthenticationMfaMethodsOutput;
export const getAuthMfaMethodsEndpoint = {
  operationId: "getAuthMfaMethods" as const,
  method: "GET" as const,
  path: "/v1/auth/mfa/methods" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

/**
 * Disable MFA
 *
 * Disables multi-factor authentication for the current user after password verification.
 */
export interface PostAuthMfaDisableInput {
  body?: Types.IdentityAuthenticationDisableMfaInput;
}
export type PostAuthMfaDisableOutput =
  Types.IdentityAuthenticationMfaSuccessOutput;
export const postAuthMfaDisableEndpoint = {
  operationId: "postAuthMfaDisable" as const,
  method: "POST" as const,
  path: "/v1/auth/mfa:disable" as const,
  tags: ["AuthMultiFactor"] as const,
  requiresAuth: true,
} as const;

export interface GetRolesForGetRolesInput {
  query?: {
    tenantId?: string;
    includeInactive?: boolean;
  };
}
export type GetRolesForGetRolesOutput = void;
export const getRolesForGetRolesEndpoint = {
  operationId: "getRolesForGetRoles" as const,
  method: "GET" as const,
  path: "/v1/roles" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface PostRolesInput {
  body?: Types.IdentityAuthenticationCreateRoleInput;
}
export type PostRolesOutput = void;
export const postRolesEndpoint = {
  operationId: "postRoles" as const,
  method: "POST" as const,
  path: "/v1/roles" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface GetRolesForGetRolesByRoleIdInput {
  roleId: string;
}
export type GetRolesForGetRolesByRoleIdOutput = void;
export const getRolesForGetRolesByRoleIdEndpoint = {
  operationId: "getRolesForGetRolesByRoleId" as const,
  method: "GET" as const,
  path: "/v1/roles/{roleId}" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface PutRolesInput {
  roleId: string;
  body?: Types.IdentityAuthenticationUpdateRoleInput;
}
export type PutRolesOutput = void;
export const putRolesEndpoint = {
  operationId: "putRoles" as const,
  method: "PUT" as const,
  path: "/v1/roles/{roleId}" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface DeleteRolesInput {
  roleId: string;
}
export type DeleteRolesOutput = void;
export const deleteRolesEndpoint = {
  operationId: "deleteRoles" as const,
  method: "DELETE" as const,
  path: "/v1/roles/{roleId}" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface GetRolesUserInput {
  userId: string;
  query?: {
    includeExpired?: boolean;
  };
}
export type GetRolesUserOutput = void;
export const getRolesUserEndpoint = {
  operationId: "getRolesUser" as const,
  method: "GET" as const,
  path: "/v1/roles/user/{userId}" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface PostRolesAssignInput {
  body?: Types.IdentityAuthenticationAssignRoleToUserInput;
}
export type PostRolesAssignOutput = void;
export const postRolesAssignEndpoint = {
  operationId: "postRolesAssign" as const,
  method: "POST" as const,
  path: "/v1/roles/:assign" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface PostRolesRemoveInput {
  body?: Types.IdentityAuthenticationRemoveRoleFromUserInput;
}
export type PostRolesRemoveOutput = void;
export const postRolesRemoveEndpoint = {
  operationId: "postRolesRemove" as const,
  method: "POST" as const,
  path: "/v1/roles/:remove" as const,
  tags: ["AuthRoles"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthServiceAccountsForGetAuthServiceAccountsInput {
  query?: {
    tenantId?: string;
  };
}
export type GetAuthServiceAccountsForGetAuthServiceAccountsOutput =
  Array<Types.IdentityAuthenticationServiceAccountOutput>;
export const getAuthServiceAccountsForGetAuthServiceAccountsEndpoint = {
  operationId: "getAuthServiceAccountsForGetAuthServiceAccounts" as const,
  method: "GET" as const,
  path: "/v1/auth/service-accounts" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthServiceAccountsInput {
  body?: Types.IdentityAuthenticationCreateServiceAccountInput;
}
export type PostAuthServiceAccountsOutput =
  Types.IdentityAuthenticationServiceAccountCreatedOutput;
export const postAuthServiceAccountsEndpoint = {
  operationId: "postAuthServiceAccounts" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountIdInput {
  serviceAccountId: string;
}
export type GetAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountIdOutput =
  Types.IdentityAuthenticationServiceAccountOutput;
export const getAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountIdEndpoint =
  {
    operationId:
      "getAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountId" as const,
    method: "GET" as const,
    path: "/v1/auth/service-accounts/{serviceAccountId}" as const,
    tags: ["AuthServiceAccounts"] as const,
    requiresAuth: true,
  } as const;

export interface DeleteAuthServiceAccountsInput {
  serviceAccountId: string;
}
export type DeleteAuthServiceAccountsOutput = void;
export const deleteAuthServiceAccountsEndpoint = {
  operationId: "deleteAuthServiceAccounts" as const,
  method: "DELETE" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update service account
 *
 * Updates specific fields of a service account. Only provided fields are updated.
 */
export interface PatchAuthServiceAccountsInput {
  serviceAccountId: string;
  body?: Types.IdentityAuthenticationPatchServiceAccountInput;
}
export type PatchAuthServiceAccountsOutput = void;
export const patchAuthServiceAccountsEndpoint = {
  operationId: "patchAuthServiceAccounts" as const,
  method: "PATCH" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if service account exists
 *
 * Checks if a service account exists without returning the body.
 */
export interface HeadAuthServiceAccountsInput {
  serviceAccountId: string;
}
export type HeadAuthServiceAccountsOutput = void;
export const headAuthServiceAccountsEndpoint = {
  operationId: "headAuthServiceAccounts" as const,
  method: "HEAD" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthServiceAccountsRotateSecretInput {
  serviceAccountId: string;
}
export type PostAuthServiceAccountsRotateSecretOutput =
  Types.IdentityAuthenticationSecretRotationOutput;
export const postAuthServiceAccountsRotateSecretEndpoint = {
  operationId: "postAuthServiceAccountsRotateSecret" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}:rotate-secret" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthServiceAccountsUnlockInput {
  serviceAccountId: string;
}
export type PostAuthServiceAccountsUnlockOutput = void;
export const postAuthServiceAccountsUnlockEndpoint = {
  operationId: "postAuthServiceAccountsUnlock" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}:unlock" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

/**
 * Lock service account
 *
 * Locks a service account to prevent it from authenticating.
 */
export interface PostAuthServiceAccountsLockInput {
  serviceAccountId: string;
  body?: Types.IdentityAuthenticationLockServiceAccountInput;
}
export type PostAuthServiceAccountsLockOutput = void;
export const postAuthServiceAccountsLockEndpoint = {
  operationId: "postAuthServiceAccountsLock" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}:lock" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

/**
 * Get service account audit log
 *
 * Retrieves the audit log of actions performed on or by a service account.
 */
export interface GetAuthServiceAccountsAuditLogInput {
  serviceAccountId: string;
  query?: {
    page?: number;
    pageSize?: number;
  };
}
export type GetAuthServiceAccountsAuditLogOutput =
  Types.IdentityAuthenticationServiceAccountAuditLogOutput;
export const getAuthServiceAccountsAuditLogEndpoint = {
  operationId: "getAuthServiceAccountsAuditLog" as const,
  method: "GET" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}/audit-log" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthServiceAccountsDeactivateInput {
  serviceAccountId: string;
}
export type PostAuthServiceAccountsDeactivateOutput = void;
export const postAuthServiceAccountsDeactivateEndpoint = {
  operationId: "postAuthServiceAccountsDeactivate" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}:deactivate" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthServiceAccountsReactivateInput {
  serviceAccountId: string;
}
export type PostAuthServiceAccountsReactivateOutput = void;
export const postAuthServiceAccountsReactivateEndpoint = {
  operationId: "postAuthServiceAccountsReactivate" as const,
  method: "POST" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}:reactivate" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PatchAuthServiceAccountsScopesInput {
  serviceAccountId: string;
  body?: Types.IdentityAuthenticationUpdateScopesInput;
}
export type PatchAuthServiceAccountsScopesOutput = void;
export const patchAuthServiceAccountsScopesEndpoint = {
  operationId: "patchAuthServiceAccountsScopes" as const,
  method: "PATCH" as const,
  path: "/v1/auth/service-accounts/{serviceAccountId}/scopes" as const,
  tags: ["AuthServiceAccounts"] as const,
  requiresAuth: true,
} as const;

export interface PostOauthTokenInput {
  body?: FormData;
}
export type PostOauthTokenOutput =
  Types.IdentityAuthenticationClientCredentialsTokenOutput;
export const postOauthTokenEndpoint = {
  operationId: "postOauthToken" as const,
  method: "POST" as const,
  path: "/v1/oauth/token" as const,
  tags: ["AuthServiceAccountsTokens"] as const,
  requiresAuth: true,
} as const;

/**
 * Get active sessions
 *
 * Retrieves a list of all active sessions for the current user, including device and location information.
 */
export type GetAuthSessionsInput = void;
export type GetAuthSessionsOutput =
  Array<Types.IdentityAuthenticationSessionOutput>;
export const getAuthSessionsEndpoint = {
  operationId: "getAuthSessions" as const,
  method: "GET" as const,
  path: "/v1/auth/sessions" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Analyze session security
 *
 * Analyzes the current session for security risks and provides recommendations.
 */
export type GetAuthSessionsAnalyzeSecurityInput = void;
export type GetAuthSessionsAnalyzeSecurityOutput =
  Types.IdentityAuthenticationSessionSecurityAnalysis;
export const getAuthSessionsAnalyzeSecurityEndpoint = {
  operationId: "getAuthSessionsAnalyzeSecurity" as const,
  method: "GET" as const,
  path: "/v1/auth/sessions:analyze-security" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Terminate a session
 *
 * Terminates a specific session by its identifier. The session must belong to the current user.
 */
export interface DeleteAuthSessionsInput {
  sessionId: string;
}
export type DeleteAuthSessionsOutput =
  Types.IdentityAuthenticationSessionSuccessOutput;
export const deleteAuthSessionsEndpoint = {
  operationId: "deleteAuthSessions" as const,
  method: "DELETE" as const,
  path: "/v1/auth/sessions/{sessionId}" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Terminate other sessions
 *
 * Terminates all active sessions except the current one.
 */
export type PostAuthSessionsTerminateOthersInput = void;
export type PostAuthSessionsTerminateOthersOutput =
  Types.IdentityAuthenticationSessionTerminationOutput;
export const postAuthSessionsTerminateOthersEndpoint = {
  operationId: "postAuthSessionsTerminateOthers" as const,
  method: "POST" as const,
  path: "/v1/auth/sessions:terminate-others" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Terminate all sessions
 *
 * Terminates all active sessions including the current one. User will need to sign in again.
 */
export type PostAuthSessionsTerminateAllInput = void;
export type PostAuthSessionsTerminateAllOutput =
  Types.IdentityAuthenticationSessionTerminationOutput;
export const postAuthSessionsTerminateAllEndpoint = {
  operationId: "postAuthSessionsTerminateAll" as const,
  method: "POST" as const,
  path: "/v1/auth/sessions:terminate-all" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Refresh current session
 *
 * Extends the current session's expiration time.
 */
export type PostAuthSessionsRefreshInput = void;
export type PostAuthSessionsRefreshOutput =
  Types.IdentityAuthenticationSessionSuccessOutput;
export const postAuthSessionsRefreshEndpoint = {
  operationId: "postAuthSessionsRefresh" as const,
  method: "POST" as const,
  path: "/v1/auth/sessions:refresh" as const,
  tags: ["AuthSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get signing keys
 *
 * Retrieves signing keys with optional status filtering. Use status=active for current signing key, status=valid for all keys usable for validation.
 */
export interface GetAuthSigningKeysInput {
  query?: {
    status?: string;
  };
}
export type GetAuthSigningKeysOutput =
  Array<Types.IdentityAuthenticationJwtKeyInfo>;
export const getAuthSigningKeysEndpoint = {
  operationId: "getAuthSigningKeys" as const,
  method: "GET" as const,
  path: "/v1/auth/signing-keys" as const,
  tags: ["AuthSigningKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Rotate signing key
 *
 * Manually rotates to a new signing key. Previous keys remain valid for token validation during grace period.
 */
export interface PostAuthSigningKeysRotateInput {
  body?: Types.IdentityAuthenticationRotateKeyInput;
}
export type PostAuthSigningKeysRotateOutput =
  Types.IdentityAuthenticationJwtKeyInfo;
export const postAuthSigningKeysRotateEndpoint = {
  operationId: "postAuthSigningKeysRotate" as const,
  method: "POST" as const,
  path: "/v1/auth/signing-keys:rotate" as const,
  tags: ["AuthSigningKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Cleanup expired keys
 *
 * Removes signing keys that have been expired beyond the retention period.
 */
export interface PostAuthSigningKeysCleanupInput {
  body?: Types.IdentityAuthenticationCleanupKeysInput;
}
export type PostAuthSigningKeysCleanupOutput =
  Types.IdentityAuthenticationCleanupResult;
export const postAuthSigningKeysCleanupEndpoint = {
  operationId: "postAuthSigningKeysCleanup" as const,
  method: "POST" as const,
  path: "/v1/auth/signing-keys:cleanup" as const,
  tags: ["AuthSigningKeys"] as const,
  requiresAuth: true,
} as const;

/**
 * Get trusted devices
 *
 * Retrieves a list of devices that have been marked as trusted for the current user.
 */
export type GetAuthTrustedDevicesInput = void;
export type GetAuthTrustedDevicesOutput =
  Array<Types.IdentityAuthenticationTrustedDeviceOutput>;
export const getAuthTrustedDevicesEndpoint = {
  operationId: "getAuthTrustedDevices" as const,
  method: "GET" as const,
  path: "/v1/auth/trusted-devices" as const,
  tags: ["AuthTrustedDevices"] as const,
  requiresAuth: true,
} as const;

/**
 * Trust current device
 *
 * Marks the current device as trusted, allowing faster authentication in the future.
 */
export interface PostAuthTrustedDevicesInput {
  body?: Types.IdentityAuthenticationTrustDeviceInput;
}
export type PostAuthTrustedDevicesOutput =
  Types.IdentityAuthenticationSessionSuccessOutput;
export const postAuthTrustedDevicesEndpoint = {
  operationId: "postAuthTrustedDevices" as const,
  method: "POST" as const,
  path: "/v1/auth/trusted-devices" as const,
  tags: ["AuthTrustedDevices"] as const,
  requiresAuth: true,
} as const;

/**
 * Revoke device trust
 *
 * Removes a device from the trusted devices list.
 */
export interface DeleteAuthTrustedDevicesInput {
  deviceId: string;
}
export type DeleteAuthTrustedDevicesOutput =
  Types.IdentityAuthenticationSessionSuccessOutput;
export const deleteAuthTrustedDevicesEndpoint = {
  operationId: "deleteAuthTrustedDevices" as const,
  method: "DELETE" as const,
  path: "/v1/auth/trusted-devices/{deviceId}" as const,
  tags: ["AuthTrustedDevices"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthWebauthnRegistrationBeginInput {
  body?: Types.IdentityAuthenticationBeginWebAuthnRegistrationInput;
}
export type PostAuthWebauthnRegistrationBeginOutput =
  Types.IdentityAuthenticationWebAuthnRegistrationOptionsResult;
export const postAuthWebauthnRegistrationBeginEndpoint = {
  operationId: "postAuthWebauthnRegistrationBegin" as const,
  method: "POST" as const,
  path: "/v1/auth/webauthn/registration:begin" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthWebauthnRegistrationCompleteInput {
  body?: Types.IdentityAuthenticationCompleteWebAuthnRegistrationInput;
}
export type PostAuthWebauthnRegistrationCompleteOutput =
  Types.IdentityAuthenticationWebAuthnRegistrationResult;
export const postAuthWebauthnRegistrationCompleteEndpoint = {
  operationId: "postAuthWebauthnRegistrationComplete" as const,
  method: "POST" as const,
  path: "/v1/auth/webauthn/registration:complete" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthWebauthnAuthenticationBeginInput {
  body?: Types.IdentityAuthenticationBeginWebAuthnAuthenticationInput;
}
export type PostAuthWebauthnAuthenticationBeginOutput =
  Types.IdentityAuthenticationWebAuthnAuthenticationOptionsResult;
export const postAuthWebauthnAuthenticationBeginEndpoint = {
  operationId: "postAuthWebauthnAuthenticationBegin" as const,
  method: "POST" as const,
  path: "/v1/auth/webauthn/authentication:begin" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthWebauthnAuthenticationCompleteInput {
  body?: Types.IdentityAuthenticationCompleteWebAuthnAuthenticationInput;
}
export type PostAuthWebauthnAuthenticationCompleteOutput =
  Types.IdentityAuthenticationWebAuthnAuthenticationResult;
export const postAuthWebauthnAuthenticationCompleteEndpoint = {
  operationId: "postAuthWebauthnAuthenticationComplete" as const,
  method: "POST" as const,
  path: "/v1/auth/webauthn/authentication:complete" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export type GetAuthWebauthnCredentialsForGetAuthWebauthnCredentialsInput = void;
export type GetAuthWebauthnCredentialsForGetAuthWebauthnCredentialsOutput =
  Array<Types.IdentityAuthenticationWebAuthnCredentialInfo>;
export const getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsEndpoint = {
  operationId:
    "getAuthWebauthnCredentialsForGetAuthWebauthnCredentials" as const,
  method: "GET" as const,
  path: "/v1/auth/webauthn/credentials" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface GetAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialIdInput {
  credentialId: string;
}
export type GetAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialIdOutput =
  Types.IdentityAuthenticationWebAuthnCredentialInfo;
export const getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialIdEndpoint =
  {
    operationId:
      "getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialId" as const,
    method: "GET" as const,
    path: "/v1/auth/webauthn/credentials/{credentialId}" as const,
    tags: ["AuthWebauthn"] as const,
    requiresAuth: true,
  } as const;

export interface DeleteAuthWebauthnCredentialsInput {
  credentialId: string;
}
export type DeleteAuthWebauthnCredentialsOutput = void;
export const deleteAuthWebauthnCredentialsEndpoint = {
  operationId: "deleteAuthWebauthnCredentials" as const,
  method: "DELETE" as const,
  path: "/v1/auth/webauthn/credentials/{credentialId}" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface PatchAuthWebauthnCredentialsInput {
  credentialId: string;
  body?: Types.IdentityAuthenticationUpdateCredentialNameInput;
}
export type PatchAuthWebauthnCredentialsOutput = void;
export const patchAuthWebauthnCredentialsEndpoint = {
  operationId: "patchAuthWebauthnCredentials" as const,
  method: "PATCH" as const,
  path: "/v1/auth/webauthn/credentials/{credentialId}" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface HeadAuthWebauthnCredentialsInput {
  credentialId: string;
}
export type HeadAuthWebauthnCredentialsOutput = void;
export const headAuthWebauthnCredentialsEndpoint = {
  operationId: "headAuthWebauthnCredentials" as const,
  method: "HEAD" as const,
  path: "/v1/auth/webauthn/credentials/{credentialId}" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export interface PostAuthWebauthnCredentialsVerifyInput {
  credentialId: string;
}
export type PostAuthWebauthnCredentialsVerifyOutput =
  Types.IdentityAuthenticationWebAuthnCredentialVerifyResult;
export const postAuthWebauthnCredentialsVerifyEndpoint = {
  operationId: "postAuthWebauthnCredentialsVerify" as const,
  method: "POST" as const,
  path: "/v1/auth/webauthn/credentials/{credentialId}:verify" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

export type GetAuthWebauthnInput = void;
export type GetAuthWebauthnOutput =
  Types.IdentityAuthenticationWebAuthnStatusOutput;
export const getAuthWebauthnEndpoint = {
  operationId: "getAuthWebauthn" as const,
  method: "GET" as const,
  path: "/v1/auth/webauthn" as const,
  tags: ["AuthWebauthn"] as const,
  requiresAuth: true,
} as const;

/**
 * List billing charges
 *
 * Compatibility billing endpoint backed by the persisted payment query model.
 */
export interface GetBillingChargesInput {
  query?: {
    tenantId?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  };
}
export type GetBillingChargesOutput =
  Array<Types.CommercePaymentsPaymentResult>;
export const getBillingChargesEndpoint = {
  operationId: "getBillingCharges" as const,
  method: "GET" as const,
  path: "/api/v1/billing/charges" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Create billing charge
 *
 * Processes a subscription charge through the configured payment command path.
 */
export interface PostBillingChargesInput {
  body?: Types.CommercePaymentsBillingChargesControllerCreateBillingChargeInput;
}
export type PostBillingChargesOutput = Types.CommercePaymentsPaymentResult;
export const postBillingChargesEndpoint = {
  operationId: "postBillingCharges" as const,
  method: "POST" as const,
  path: "/api/v1/billing/charges" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Get billing charge
 */
export interface GetBillingChargeByIdInput {
  chargeId: string;
}
export type GetBillingChargeByIdOutput = Types.CommercePaymentsPaymentResult;
export const getBillingChargeByIdEndpoint = {
  operationId: "getBillingChargeById" as const,
  method: "GET" as const,
  path: "/api/v1/billing/charges/{chargeId}" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Retry billing charge
 */
export interface PostBillingChargesRetryInput {
  chargeId: string;
}
export type PostBillingChargesRetryOutput =
  Types.CommercePaymentsPaymentRetryResult;
export const postBillingChargesRetryEndpoint = {
  operationId: "postBillingChargesRetry" as const,
  method: "POST" as const,
  path: "/api/v1/billing/charges/{chargeId}:retry" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Refund billing charge
 */
export interface PostBillingChargesRefundInput {
  chargeId: string;
  body?: Types.CommercePaymentsBillingChargesControllerRefundBillingChargeInput;
}
export type PostBillingChargesRefundOutput =
  Types.CommercePaymentsProcessRefundResult;
export const postBillingChargesRefundEndpoint = {
  operationId: "postBillingChargesRefund" as const,
  method: "POST" as const,
  path: "/api/v1/billing/charges/{chargeId}:refund" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel billing charge
 */
export interface PostBillingChargesCancelInput {
  chargeId: string;
  body?: Types.CommercePaymentsBillingChargesControllerCancelBillingChargeInput;
}
export type PostBillingChargesCancelOutput =
  Types.CommercePaymentsPaymentCancellationResult;
export const postBillingChargesCancelEndpoint = {
  operationId: "postBillingChargesCancel" as const,
  method: "POST" as const,
  path: "/api/v1/billing/charges/{chargeId}:cancel" as const,
  tags: ["CommercePaymentsBillingCharges"] as const,
  requiresAuth: true,
} as const;

/**
 * Retry invoice payment
 *
 * Accepts a local retry scheduling request for open or past-due invoices. External gateway capture requires configured payment-provider credentials.
 */
export interface PostBillingInvoicesRetryInput {
  invoiceId: string;
}
export type PostBillingInvoicesRetryOutput =
  Types.CommerceBillingInvoicePaymentRetryResult;
export const postBillingInvoicesRetryEndpoint = {
  operationId: "postBillingInvoicesRetry" as const,
  method: "POST" as const,
  path: "/api/v1/billing/invoices/{invoiceId}/retry" as const,
  tags: ["CommerceBillingInvoices"] as const,
  requiresAuth: true,
} as const;

/**
 * List billing subscriptions
 *
 * Compatibility billing endpoint backed by the subscription query model.
 */
export interface GetBillingSubscriptionsInput {
  query?: {
    tenantId?: string;
    status?: Types.CommerceSubscriptionsSubscriptionStatus;
    planId?: string;
    page?: number;
    pageSize?: number;
  };
}
export type GetBillingSubscriptionsOutput =
  Types.PagedResultOfCommerceSubscriptionsSubscription;
export const getBillingSubscriptionsEndpoint = {
  operationId: "getBillingSubscriptions" as const,
  method: "GET" as const,
  path: "/api/v1/billing/subscriptions" as const,
  tags: ["CommerceSubscriptionsBillingSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Create billing subscription
 */
export interface PostBillingSubscriptionsInput {
  body?: Types.CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput;
}
export type PostBillingSubscriptionsOutput = void;
export const postBillingSubscriptionsEndpoint = {
  operationId: "postBillingSubscriptions" as const,
  method: "POST" as const,
  path: "/api/v1/billing/subscriptions" as const,
  tags: ["CommerceSubscriptionsBillingSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get billing subscription
 */
export interface GetBillingSubscriptionByIdInput {
  subscriptionId: string;
}
export type GetBillingSubscriptionByIdOutput =
  Types.CommerceSubscriptionsSubscription;
export const getBillingSubscriptionByIdEndpoint = {
  operationId: "getBillingSubscriptionById" as const,
  method: "GET" as const,
  path: "/api/v1/billing/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptionsBillingSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Renew billing subscription
 */
export interface PostBillingSubscriptionsRenewInput {
  subscriptionId: string;
}
export type PostBillingSubscriptionsRenewOutput = void;
export const postBillingSubscriptionsRenewEndpoint = {
  operationId: "postBillingSubscriptionsRenew" as const,
  method: "POST" as const,
  path: "/api/v1/billing/subscriptions/{subscriptionId}:renew" as const,
  tags: ["CommerceSubscriptionsBillingSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel billing subscription
 */
export interface PostBillingSubscriptionsCancelInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput;
}
export type PostBillingSubscriptionsCancelOutput = void;
export const postBillingSubscriptionsCancelEndpoint = {
  operationId: "postBillingSubscriptionsCancel" as const,
  method: "POST" as const,
  path: "/api/v1/billing/subscriptions/{subscriptionId}:cancel" as const,
  tags: ["CommerceSubscriptionsBillingSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Handle Google Pay webhook events for transaction notifications
 *
 * Processes Google Pay webhook notifications for payment processing, subscription billing, and transaction status updates. Google Pay webhooks provide real-time notifications for payment completions, failures, refunds, and subscription lifecycle events.
 */
export type PostBillingWebhooksGooglePayInput = void;
export type PostBillingWebhooksGooglePayOutput = Record<string, unknown>;
export const postBillingWebhooksGooglePayEndpoint = {
  operationId: "postBillingWebhooksGooglePay" as const,
  method: "POST" as const,
  path: "/api/v1/billing/webhooks/google-pay" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

/**
 * Handle Apple Pay webhook events for transaction notifications
 *
 * Processes Apple Pay webhook notifications for payment completions and transaction status updates.
 */
export type PostBillingWebhooksApplePayInput = void;
export type PostBillingWebhooksApplePayOutput = Record<string, unknown>;
export const postBillingWebhooksApplePayEndpoint = {
  operationId: "postBillingWebhooksApplePay" as const,
  method: "POST" as const,
  path: "/api/v1/billing/webhooks/apple-pay" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

/**
 * Handle Stripe webhook events with signature verification
 *
 * Processes Stripe webhook notifications with enhanced security through signature verification. Handles subscription lifecycle events, payment confirmations, invoice updates, and customer changes. Stripe signatures are verified using the webhook signing secret to ensure event authenticity.
 */
export type PostBillingWebhooksStripeInput = void;
export type PostBillingWebhooksStripeOutput = Record<string, unknown>;
export const postBillingWebhooksStripeEndpoint = {
  operationId: "postBillingWebhooksStripe" as const,
  method: "POST" as const,
  path: "/api/v1/billing/webhooks/stripe" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

/**
 * Handle PayPal IPN (Instant Payment Notification) webhook events
 *
 * Processes PayPal Instant Payment Notification (IPN) webhook events for subscription billing, payment confirmations, and account updates. PayPal IPN provides real-time transaction status updates and subscription lifecycle management for PayPal-based billing integrations.
 */
export type PostBillingWebhooksPaypalInput = void;
export type PostBillingWebhooksPaypalOutput = Record<string, unknown>;
export const postBillingWebhooksPaypalEndpoint = {
  operationId: "postBillingWebhooksPaypal" as const,
  method: "POST" as const,
  path: "/api/v1/billing/webhooks/paypal" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

/**
 * Retrieve webhook event details by event ID
 *
 * Retrieves detailed information about a specific webhook event for debugging and monitoring purposes. Shows event payload, processing status, timestamps, and any error messages. Useful for troubleshooting webhook processing issues and verifying event delivery.
 */
export interface GetBillingWebhooksWebhookEventsInput {
  eventId: string;
}
export type GetBillingWebhooksWebhookEventsOutput = Record<string, unknown>;
export const getBillingWebhooksWebhookEventsEndpoint = {
  operationId: "getBillingWebhooksWebhookEvents" as const,
  method: "GET" as const,
  path: "/api/v1/billing/webhooks/webhook-events/{eventId}" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

/**
 * Retry failed webhook event processing
 *
 * Manually retries processing of a previously failed webhook event. Useful for handling temporary failures such as downstream service unavailability, network timeouts, or transient processing errors. The retry operation uses the original event payload and applies current business logic.
 */
export interface PostBillingWebhooksWebhookEventsRetryInput {
  eventId: string;
}
export type PostBillingWebhooksWebhookEventsRetryOutput = Record<
  string,
  unknown
>;
export const postBillingWebhooksWebhookEventsRetryEndpoint = {
  operationId: "postBillingWebhooksWebhookEventsRetry" as const,
  method: "POST" as const,
  path: "/api/v1/billing/webhooks/webhook-events/{eventId}:retry" as const,
  tags: ["CommerceBillingWebhooks"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialBlogForGetApiSocialBlogInput {
  query?: {
    authorId?: string;
    status?: Types.SocialBlogBlogPostStatus;
    featured?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialBlogForGetApiSocialBlogOutput =
  Array<Types.SocialBlogBlogPost>;
export const getApiSocialBlogForGetApiSocialBlogEndpoint = {
  operationId: "getApiSocialBlogForGetApiSocialBlog" as const,
  method: "GET" as const,
  path: "/api/social/blog" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialBlogInput {
  body?: Types.SocialBlogCreateBlogPostInput;
}
export type PostApiSocialBlogOutput = Types.SocialBlogBlogPost;
export const postApiSocialBlogEndpoint = {
  operationId: "postApiSocialBlog" as const,
  method: "POST" as const,
  path: "/api/social/blog" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialBlogForGetApiSocialBlogByIdInput {
  id: string;
}
export type GetApiSocialBlogForGetApiSocialBlogByIdOutput = void;
export const getApiSocialBlogForGetApiSocialBlogByIdEndpoint = {
  operationId: "getApiSocialBlogForGetApiSocialBlogById" as const,
  method: "GET" as const,
  path: "/api/social/blog/{id}" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialBlogPublishInput {
  id: string;
}
export type PostApiSocialBlogPublishOutput = void;
export const postApiSocialBlogPublishEndpoint = {
  operationId: "postApiSocialBlogPublish" as const,
  method: "POST" as const,
  path: "/api/social/blog/{id}/publish" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialBlogUnpublishInput {
  id: string;
}
export type PostApiSocialBlogUnpublishOutput = void;
export const postApiSocialBlogUnpublishEndpoint = {
  operationId: "postApiSocialBlogUnpublish" as const,
  method: "POST" as const,
  path: "/api/social/blog/{id}/unpublish" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialBlogFeatureInput {
  id: string;
  query?: {
    featured?: boolean;
  };
}
export type PostApiSocialBlogFeatureOutput = void;
export const postApiSocialBlogFeatureEndpoint = {
  operationId: "postApiSocialBlogFeature" as const,
  method: "POST" as const,
  path: "/api/social/blog/{id}/feature" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialBlogViewsInput {
  id: string;
}
export type PostApiSocialBlogViewsOutput = void;
export const postApiSocialBlogViewsEndpoint = {
  operationId: "postApiSocialBlogViews" as const,
  method: "POST" as const,
  path: "/api/social/blog/{id}/views" as const,
  tags: ["SocialBlogPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesInput {
  tenantId: string;
}
export type GetTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesOutput =
  Record<string, boolean>;
export const getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesEndpoint =
  {
    operationId:
      "getTenantsCapabilitiesForGetTenantsByTenantIdCapabilities" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/capabilities" as const,
    tags: ["FeaturesCapabilities"] as const,
    requiresAuth: true,
  } as const;

export interface PostTenantsCapabilitiesInput {
  tenantId: string;
  body?: Types.FeaturesSetCapabilityOverrideInput;
}
export type PostTenantsCapabilitiesOutput = void;
export const postTenantsCapabilitiesEndpoint = {
  operationId: "postTenantsCapabilities" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/capabilities" as const,
  tags: ["FeaturesCapabilities"] as const,
  requiresAuth: true,
} as const;

export interface GetTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapabilityInput {
  tenantId: string;
  capability: string;
}
export type GetTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapabilityOutput =
  Types.FeaturesCapabilityCheckOutput;
export const getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapabilityEndpoint =
  {
    operationId:
      "getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapability" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/capabilities/{capability}" as const,
    tags: ["FeaturesCapabilities"] as const,
    requiresAuth: true,
  } as const;

export interface DeleteTenantsCapabilitiesInput {
  tenantId: string;
  capability: string;
  query?: {
    reason?: string;
  };
}
export type DeleteTenantsCapabilitiesOutput = void;
export const deleteTenantsCapabilitiesEndpoint = {
  operationId: "deleteTenantsCapabilities" as const,
  method: "DELETE" as const,
  path: "/v1/tenants/{tenantId}/capabilities/{capability}" as const,
  tags: ["FeaturesCapabilities"] as const,
  requiresAuth: true,
} as const;

export interface PostTenantsCapabilitiesSyncInput {
  tenantId: string;
}
export type PostTenantsCapabilitiesSyncOutput = void;
export const postTenantsCapabilitiesSyncEndpoint = {
  operationId: "postTenantsCapabilitiesSync" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/capabilities/sync" as const,
  tags: ["FeaturesCapabilities"] as const,
  requiresAuth: true,
} as const;

export interface GetTenantsCapabilitiesAuditLogInput {
  tenantId: string;
  query?: {
    capability?: string;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetTenantsCapabilitiesAuditLogOutput =
  Array<Types.FeaturesCapabilityAuditLog>;
export const getTenantsCapabilitiesAuditLogEndpoint = {
  operationId: "getTenantsCapabilitiesAuditLog" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/capabilities/audit-log" as const,
  tags: ["FeaturesCapabilities"] as const,
  requiresAuth: true,
} as const;

export type GetApiCertificatesMyInput = void;
export type GetApiCertificatesMyOutput =
  Array<Types.LearningCertificatesCertificate>;
export const getApiCertificatesMyEndpoint = {
  operationId: "getApiCertificatesMy" as const,
  method: "GET" as const,
  path: "/api/certificates/my" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesInput {
  id: string;
}
export type GetApiCertificatesOutput = Types.LearningCertificatesCertificate;
export const getApiCertificatesEndpoint = {
  operationId: "getApiCertificates" as const,
  method: "GET" as const,
  path: "/api/certificates/{id}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesVerifyInput {
  certificateNumber: string;
}
export type GetApiCertificatesVerifyOutput =
  Types.LearningCertificatesCertificateVerificationResult;
export const getApiCertificatesVerifyEndpoint = {
  operationId: "getApiCertificatesVerify" as const,
  method: "GET" as const,
  path: "/api/certificates/verify/{certificateNumber}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCertificatesIssueInput {
  body?: Types.LearningCertificatesIssueCertificateInput;
}
export type PostApiCertificatesIssueOutput =
  Types.LearningCertificatesCertificate;
export const postApiCertificatesIssueEndpoint = {
  operationId: "postApiCertificatesIssue" as const,
  method: "POST" as const,
  path: "/api/certificates/issue" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCertificatesRevokeInput {
  id: string;
  body?: Types.LearningCertificatesRevokeCertificateInput;
}
export type PostApiCertificatesRevokeOutput = void;
export const postApiCertificatesRevokeEndpoint = {
  operationId: "postApiCertificatesRevoke" as const,
  method: "POST" as const,
  path: "/api/certificates/{id}/revoke" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesCourseInput {
  courseId: string;
}
export type GetApiCertificatesCourseOutput =
  Array<Types.LearningCertificatesCertificate>;
export const getApiCertificatesCourseEndpoint = {
  operationId: "getApiCertificatesCourse" as const,
  method: "GET" as const,
  path: "/api/certificates/course/{courseId}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesTemplatesCourseInput {
  courseId: string;
}
export type GetApiCertificatesTemplatesCourseOutput =
  Array<Types.LearningCertificatesCertificateTemplate>;
export const getApiCertificatesTemplatesCourseEndpoint = {
  operationId: "getApiCertificatesTemplatesCourse" as const,
  method: "GET" as const,
  path: "/api/certificates/templates/course/{courseId}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesTemplatesInput {
  templateId: string;
}
export type GetApiCertificatesTemplatesOutput =
  Types.LearningCertificatesCertificateTemplateDetail;
export const getApiCertificatesTemplatesEndpoint = {
  operationId: "getApiCertificatesTemplates" as const,
  method: "GET" as const,
  path: "/api/certificates/templates/{templateId}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface PutApiCertificatesTemplatesInput {
  templateId: string;
  body?: Types.LearningCertificatesUpdateCertificateTemplateInput;
}
export type PutApiCertificatesTemplatesOutput =
  Types.LearningCertificatesCertificateTemplateDetail;
export const putApiCertificatesTemplatesEndpoint = {
  operationId: "putApiCertificatesTemplates" as const,
  method: "PUT" as const,
  path: "/api/certificates/templates/{templateId}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiCertificatesTemplatesInput {
  templateId: string;
}
export type DeleteApiCertificatesTemplatesOutput = void;
export const deleteApiCertificatesTemplatesEndpoint = {
  operationId: "deleteApiCertificatesTemplates" as const,
  method: "DELETE" as const,
  path: "/api/certificates/templates/{templateId}" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCertificatesTemplatesInput {
  body?: Types.LearningCertificatesCreateCertificateTemplateInput;
}
export type PostApiCertificatesTemplatesOutput =
  Types.LearningCertificatesCertificateTemplateDetail;
export const postApiCertificatesTemplatesEndpoint = {
  operationId: "postApiCertificatesTemplates" as const,
  method: "POST" as const,
  path: "/api/certificates/templates" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCertificatesExpiringInput {
  query?: {
    days?: number;
  };
}
export type GetApiCertificatesExpiringOutput =
  Array<Types.LearningCertificatesCertificate>;
export const getApiCertificatesExpiringEndpoint = {
  operationId: "getApiCertificatesExpiring" as const,
  method: "GET" as const,
  path: "/api/certificates/expiring" as const,
  tags: ["LearningCertificates"] as const,
  requiresAuth: true,
} as const;

/**
 * List B2B client accounts
 *
 * Lists client accounts through the canonical tenant page query.
 */
export interface GetClientsInput {
  query?: {
    page?: number;
    pageSize?: number;
    status?: string;
    searchTerm?: string;
  };
}
export type GetClientsOutput = Types.PagedResultOfIdentityTenantsTenant;
export const getClientsEndpoint = {
  operationId: "getClients" as const,
  method: "GET" as const,
  path: "/v1/clients" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a B2B client account
 *
 * Creates a client account using the canonical tenant creation workflow.
 */
export interface PostClientsInput {
  body?: Types.CommerceSubscriptionsCreateClientInput;
}
export type PostClientsOutput = void;
export const postClientsEndpoint = {
  operationId: "postClients" as const,
  method: "POST" as const,
  path: "/v1/clients" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Get a B2B client account
 */
export interface GetClientByIdInput {
  clientId: string;
}
export type GetClientByIdOutput = Types.IdentityTenantsTenant;
export const getClientByIdEndpoint = {
  operationId: "getClientById" as const,
  method: "GET" as const,
  path: "/v1/clients/{clientId}" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Update a B2B client account
 */
export interface PutClientsInput {
  clientId: string;
  body?: Types.IdentityTenantsUpdateTenantInput;
}
export type PutClientsOutput = void;
export const putClientsEndpoint = {
  operationId: "putClients" as const,
  method: "PUT" as const,
  path: "/v1/clients/{clientId}" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Archive a B2B client account
 */
export interface DeleteClientsInput {
  clientId: string;
  body?: Types.IdentityTenantsArchiveInput;
}
export type DeleteClientsOutput = void;
export const deleteClientsEndpoint = {
  operationId: "deleteClients" as const,
  method: "DELETE" as const,
  path: "/v1/clients/{clientId}" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * List contracted modules for a B2B client
 *
 * Returns subscription-backed modules plus tenant feature flags for a client account.
 */
export interface GetClientsModulesInput {
  clientId: string;
  query?: {
    page?: number;
    pageSize?: number;
    status?: Types.CommerceSubscriptionsSubscriptionStatus;
  };
}
export type GetClientsModulesOutput =
  Types.CommerceSubscriptionsClientModulesOutput;
export const getClientsModulesEndpoint = {
  operationId: "getClientsModules" as const,
  method: "GET" as const,
  path: "/v1/clients/{clientId}/modules" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Update contracted module toggles for a B2B client
 */
export interface PutClientsModulesInput {
  clientId: string;
  body?: Types.IdentityTenantsUpdateTenantFeatureFlagsInput;
}
export type PutClientsModulesOutput = void;
export const putClientsModulesEndpoint = {
  operationId: "putClientsModules" as const,
  method: "PUT" as const,
  path: "/v1/clients/{clientId}/modules" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

/**
 * Update contracted module toggles for a B2B client
 */
export interface PatchClientsModulesInput {
  clientId: string;
  body?: Types.IdentityTenantsUpdateTenantFeatureFlagsInput;
}
export type PatchClientsModulesOutput = void;
export const patchClientsModulesEndpoint = {
  operationId: "patchClientsModules" as const,
  method: "PATCH" as const,
  path: "/v1/clients/{clientId}/modules" as const,
  tags: ["CommerceSubscriptionsClients"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCohortsInput {
  body?: Types.LearningCohortsCreateCohortInput;
}
export type PostApiCohortsOutput = Types.LearningCohortsCohort;
export const postApiCohortsEndpoint = {
  operationId: "postApiCohorts" as const,
  method: "POST" as const,
  path: "/api/cohorts" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCohortsInput {
  id: string;
}
export type GetApiCohortsOutput = Types.LearningCohortsCohort;
export const getApiCohortsEndpoint = {
  operationId: "getApiCohorts" as const,
  method: "GET" as const,
  path: "/api/cohorts/{id}" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface PutApiCohortsInput {
  id: string;
  body?: Types.LearningCohortsUpdateCohortInput;
}
export type PutApiCohortsOutput = Types.LearningCohortsCohort;
export const putApiCohortsEndpoint = {
  operationId: "putApiCohorts" as const,
  method: "PUT" as const,
  path: "/api/cohorts/{id}" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiCohortsInput {
  id: string;
}
export type DeleteApiCohortsOutput = void;
export const deleteApiCohortsEndpoint = {
  operationId: "deleteApiCohorts" as const,
  method: "DELETE" as const,
  path: "/api/cohorts/{id}" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCohortsCourseInput {
  courseId: string;
}
export type GetApiCohortsCourseOutput = Array<Types.LearningCohortsCohort>;
export const getApiCohortsCourseEndpoint = {
  operationId: "getApiCohortsCourse" as const,
  method: "GET" as const,
  path: "/api/cohorts/course/{courseId}" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCohortsCourseActiveInput {
  courseId: string;
}
export type GetApiCohortsCourseActiveOutput =
  Array<Types.LearningCohortsCohort>;
export const getApiCohortsCourseActiveEndpoint = {
  operationId: "getApiCohortsCourseActive" as const,
  method: "GET" as const,
  path: "/api/cohorts/course/{courseId}/active" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface GetApiCohortsCourseEnrollableInput {
  courseId: string;
}
export type GetApiCohortsCourseEnrollableOutput =
  Array<Types.LearningCohortsCohort>;
export const getApiCohortsCourseEnrollableEndpoint = {
  operationId: "getApiCohortsCourseEnrollable" as const,
  method: "GET" as const,
  path: "/api/cohorts/course/{courseId}/enrollable" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCohortsOpenInput {
  id: string;
}
export type PostApiCohortsOpenOutput = Types.LearningCohortsCohort;
export const postApiCohortsOpenEndpoint = {
  operationId: "postApiCohortsOpen" as const,
  method: "POST" as const,
  path: "/api/cohorts/{id}/open" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCohortsCloseInput {
  id: string;
}
export type PostApiCohortsCloseOutput = Types.LearningCohortsCohort;
export const postApiCohortsCloseEndpoint = {
  operationId: "postApiCohortsClose" as const,
  method: "POST" as const,
  path: "/api/cohorts/{id}/close" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCohortsCompleteInput {
  id: string;
}
export type PostApiCohortsCompleteOutput = Types.LearningCohortsCohort;
export const postApiCohortsCompleteEndpoint = {
  operationId: "postApiCohortsComplete" as const,
  method: "POST" as const,
  path: "/api/cohorts/{id}/complete" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiCohortsCancelInput {
  id: string;
}
export type PostApiCohortsCancelOutput = Types.LearningCohortsCohort;
export const postApiCohortsCancelEndpoint = {
  operationId: "postApiCohortsCancel" as const,
  method: "POST" as const,
  path: "/api/cohorts/{id}/cancel" as const,
  tags: ["LearningCohorts"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesCohortsScheduleAvailableContentInput {
  courseId: string;
  cohortId: string;
}
export type GetCoursesCohortsScheduleAvailableContentOutput =
  Array<Types.LearningCohortsAvailableCohortContent>;
export const getCoursesCohortsScheduleAvailableContentEndpoint = {
  operationId: "getCoursesCohortsScheduleAvailableContent" as const,
  method: "GET" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule/available-content" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesCohortsScheduleInput {
  courseId: string;
  cohortId: string;
}
export type GetCoursesCohortsScheduleOutput =
  Types.LearningCohortsCohortSchedule;
export const getCoursesCohortsScheduleEndpoint = {
  operationId: "getCoursesCohortsSchedule" as const,
  method: "GET" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesCohortsScheduleInput {
  courseId: string;
  cohortId: string;
  body?: Types.LearningCohortsApplyCohortScheduleInput;
}
export type PutCoursesCohortsScheduleOutput =
  Types.LearningCohortsCohortSchedule;
export const putCoursesCohortsScheduleEndpoint = {
  operationId: "putCoursesCohortsSchedule" as const,
  method: "PUT" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesCohortsSchedulePreviewInput {
  courseId: string;
  cohortId: string;
  body?: Types.LearningCohortsPreviewCohortScheduleInput;
}
export type PostCoursesCohortsSchedulePreviewOutput =
  Types.LearningCohortsCohortSchedulePreview;
export const postCoursesCohortsSchedulePreviewEndpoint = {
  operationId: "postCoursesCohortsSchedulePreview" as const,
  method: "POST" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule/preview" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface PatchCoursesCohortsScheduleItemsInput {
  courseId: string;
  cohortId: string;
  itemId: string;
  body?: Types.LearningCohortsUpdateCohortScheduleInput;
}
export type PatchCoursesCohortsScheduleItemsOutput =
  Types.LearningCohortsCohortSchedule;
export const patchCoursesCohortsScheduleItemsEndpoint = {
  operationId: "patchCoursesCohortsScheduleItems" as const,
  method: "PATCH" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule/items/{itemId}" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesCohortsScheduleItemsShiftInput {
  courseId: string;
  cohortId: string;
  itemId: string;
  body?: Types.LearningCohortsShiftCohortScheduleInput;
}
export type PostCoursesCohortsScheduleItemsShiftOutput =
  Types.LearningCohortsCohortSchedule;
export const postCoursesCohortsScheduleItemsShiftEndpoint = {
  operationId: "postCoursesCohortsScheduleItemsShift" as const,
  method: "POST" as const,
  path: "/v1/courses/{courseId}/cohorts/{cohortId}/schedule/items/{itemId}/shift" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesCohortsCalendarInput {
  courseId: string;
  query?: {
    cohortId?: string;
    from?: string;
    to?: string;
  };
}
export type GetCoursesCohortsCalendarOutput =
  Types.LearningCohortsCourseCohortCalendar;
export const getCoursesCohortsCalendarEndpoint = {
  operationId: "getCoursesCohortsCalendar" as const,
  method: "GET" as const,
  path: "/v1/courses/{courseId}/cohorts/calendar" as const,
  tags: ["LearningCohortsSchedules"] as const,
  requiresAuth: true,
} as const;

export interface PostOrdersInput {
  body?: Types.CommerceOrdersCreateOrderInput;
}
export type PostOrdersOutput = Types.CommerceOrdersOrder;
export const postOrdersEndpoint = {
  operationId: "postOrders" as const,
  method: "POST" as const,
  path: "/v1/orders" as const,
  tags: ["CommerceOrders"] as const,
  requiresAuth: true,
} as const;

export interface PostOrdersItemsInput {
  orderId: string;
  body?: Types.CommerceOrdersAddOrderItemInput;
}
export type PostOrdersItemsOutput = Types.CommerceOrdersOrder;
export const postOrdersItemsEndpoint = {
  operationId: "postOrdersItems" as const,
  method: "POST" as const,
  path: "/v1/orders/{orderId}/items" as const,
  tags: ["CommerceOrders"] as const,
  requiresAuth: true,
} as const;

export interface PostOrdersCompleteInput {
  orderId: string;
  body?: Types.CommerceOrdersCompleteOrderInput;
}
export type PostOrdersCompleteOutput = Types.CommerceOrdersOrder;
export const postOrdersCompleteEndpoint = {
  operationId: "postOrdersComplete" as const,
  method: "POST" as const,
  path: "/v1/orders/{orderId}:complete" as const,
  tags: ["CommerceOrders"] as const,
  requiresAuth: true,
} as const;

export interface GetOrdersInput {
  orderId: string;
}
export type GetOrdersOutput = Types.CommerceOrdersOrder;
export const getOrdersEndpoint = {
  operationId: "getOrders" as const,
  method: "GET" as const,
  path: "/v1/orders/{orderId}" as const,
  tags: ["CommerceOrders"] as const,
  requiresAuth: true,
} as const;

export interface PostOrdersCaptureInput {
  orderId: string;
  body?: Types.CommerceOrdersCaptureOrderInput;
}
export type PostOrdersCaptureOutput = Types.CommerceOrdersOrderCapture;
export const postOrdersCaptureEndpoint = {
  operationId: "postOrdersCapture" as const,
  method: "POST" as const,
  path: "/v1/orders/{orderId}:capture" as const,
  tags: ["CommerceOrders"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription metrics
 *
 * Retrieves subscription metrics and analytics.
 */
export type GetSubscriptionsGetMetricsInput = void;
export type GetSubscriptionsGetMetricsOutput = void;
export const getSubscriptionsGetMetricsEndpoint = {
  operationId: "getSubscriptionsGetMetrics" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions:get-metrics" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription invoices
 *
 * Retrieves the invoice history for a specific subscription.
 */
export interface GetSubscriptionsInvoicesInput {
  subscriptionId: string;
  query?: {
    page?: number;
    pageSize?: number;
  };
}
export type GetSubscriptionsInvoicesOutput = void;
export const getSubscriptionsInvoicesEndpoint = {
  operationId: "getSubscriptionsInvoices" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions/{subscriptionId}/invoices" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription usage and limits
 *
 * Retrieves usage information and limits for a specific subscription.
 */
export interface GetSubscriptionsUsageInput {
  subscriptionId: string;
}
export type GetSubscriptionsUsageOutput =
  Types.CommerceSubscriptionsSubscriptionUsage;
export const getSubscriptionsUsageEndpoint = {
  operationId: "getSubscriptionsUsage" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions/{subscriptionId}/usage" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription billing history
 *
 * Retrieves billing history for a specific subscription.
 */
export interface GetSubscriptionsBillingHistoryInput {
  subscriptionId: string;
}
export type GetSubscriptionsBillingHistoryOutput =
  Array<Types.CommerceSubscriptionsBillingHistory>;
export const getSubscriptionsBillingHistoryEndpoint = {
  operationId: "getSubscriptionsBillingHistory" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions/{subscriptionId}/billing-history" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Activate subscription
 *
 * Activates a subscription by ID.
 */
export interface PostSubscriptionsActivateInput {
  subscriptionId: string;
}
export type PostSubscriptionsActivateOutput = void;
export const postSubscriptionsActivateEndpoint = {
  operationId: "postSubscriptionsActivate" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:activate" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Start subscription trial
 *
 * Starts a trial period for a subscription.
 */
export interface PostSubscriptionsStartTrialInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput;
}
export type PostSubscriptionsStartTrialOutput = void;
export const postSubscriptionsStartTrialEndpoint = {
  operationId: "postSubscriptionsStartTrial" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:start-trial" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * End subscription trial
 *
 * Ends a trial period for a subscription.
 */
export interface PostSubscriptionsEndTrialInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput;
}
export type PostSubscriptionsEndTrialOutput = void;
export const postSubscriptionsEndTrialEndpoint = {
  operationId: "postSubscriptionsEndTrial" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:end-trial" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel subscription
 *
 * Cancels a subscription with specified reason and effective date.
 */
export interface PostSubscriptionsCancelInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput;
}
export type PostSubscriptionsCancelOutput = void;
export const postSubscriptionsCancelEndpoint = {
  operationId: "postSubscriptionsCancel" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:cancel" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Suspend subscription
 *
 * Suspends a subscription temporarily.
 */
export interface PostSubscriptionsSuspendInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput;
}
export type PostSubscriptionsSuspendOutput = void;
export const postSubscriptionsSuspendEndpoint = {
  operationId: "postSubscriptionsSuspend" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:suspend" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Pause subscription billing
 *
 * Pauses billing for a subscription while keeping the subscription active. Useful for temporary payment holds.
 */
export interface PostSubscriptionsPauseInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput;
}
export type PostSubscriptionsPauseOutput = void;
export const postSubscriptionsPauseEndpoint = {
  operationId: "postSubscriptionsPause" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:pause" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Resume subscription billing
 *
 * Resumes billing for a paused subscription.
 */
export interface PostSubscriptionsResumeInput {
  subscriptionId: string;
}
export type PostSubscriptionsResumeOutput = void;
export const postSubscriptionsResumeEndpoint = {
  operationId: "postSubscriptionsResume" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:resume" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Reactivate subscription
 *
 * Reactivates a suspended or cancelled subscription.
 */
export interface PostSubscriptionsReactivateInput {
  subscriptionId: string;
}
export type PostSubscriptionsReactivateOutput = void;
export const postSubscriptionsReactivateEndpoint = {
  operationId: "postSubscriptionsReactivate" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:reactivate" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Upgrade subscription plan
 *
 * Upgrades a subscription to a higher-tier plan.
 */
export interface PostSubscriptionsUpgradeInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput;
}
export type PostSubscriptionsUpgradeOutput =
  Types.CommerceSubscriptionsSubscriptionUpgradeResult;
export const postSubscriptionsUpgradeEndpoint = {
  operationId: "postSubscriptionsUpgrade" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:upgrade" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Downgrade subscription plan
 *
 * Downgrades a subscription to a lower-tier plan.
 */
export interface PostSubscriptionsDowngradeInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput;
}
export type PostSubscriptionsDowngradeOutput =
  Types.CommerceSubscriptionsSubscriptionDowngradeResult;
export const postSubscriptionsDowngradeEndpoint = {
  operationId: "postSubscriptionsDowngrade" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:downgrade" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Renew subscription
 *
 * Manually renews a subscription for another billing cycle.
 */
export interface PostSubscriptionsRenewInput {
  subscriptionId: string;
}
export type PostSubscriptionsRenewOutput = void;
export const postSubscriptionsRenewEndpoint = {
  operationId: "postSubscriptionsRenew" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:renew" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Set subscription auto-renew
 *
 * Enables or disables auto-renewal for a subscription.
 */
export interface PostSubscriptionsAutoRenewInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput;
}
export type PostSubscriptionsAutoRenewOutput = void;
export const postSubscriptionsAutoRenewEndpoint = {
  operationId: "postSubscriptionsAutoRenew" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:auto-renew" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Set subscription external IDs
 *
 * Sets external system IDs for subscription integration.
 */
export interface PostSubscriptionsExternalIdsInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput;
}
export type PostSubscriptionsExternalIdsOutput = void;
export const postSubscriptionsExternalIdsEndpoint = {
  operationId: "postSubscriptionsExternalIds" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions/{subscriptionId}:external-ids" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscriptions with pagination, search, and filtering
 *
 * Retrieves a paginated list of subscriptions with optional filtering. Use query parameters: status (active, trialing, cancelled, etc.), tenantId, planId, and expiring=true for expiring subscriptions.
 */
export interface GetSubscriptionsForGetSubscriptionsInput {
  query?: {
    page?: number;
    pageSize?: number;
    status?: Types.CommerceSubscriptionsSubscriptionStatus;
    tenantId?: string;
    planId?: string;
    expiring?: boolean;
    expiringDays?: number;
  };
}
export type GetSubscriptionsForGetSubscriptionsOutput = void;
export const getSubscriptionsForGetSubscriptionsEndpoint = {
  operationId: "getSubscriptionsForGetSubscriptions" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a new subscription
 *
 * Creates a new subscription with the provided information.
 */
export interface PostSubscriptionsInput {
  body?: Types.CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput;
}
export type PostSubscriptionsOutput = void;
export const postSubscriptionsEndpoint = {
  operationId: "postSubscriptions" as const,
  method: "POST" as const,
  path: "/api/v1/subscriptions" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription by ID
 *
 * Retrieves detailed information for a specific subscription.
 */
export interface GetSubscriptionsForGetSubscriptionsBySubscriptionIdInput {
  subscriptionId: string;
}
export type GetSubscriptionsForGetSubscriptionsBySubscriptionIdOutput = void;
export const getSubscriptionsForGetSubscriptionsBySubscriptionIdEndpoint = {
  operationId: "getSubscriptionsForGetSubscriptionsBySubscriptionId" as const,
  method: "GET" as const,
  path: "/api/v1/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Full update subscription
 *
 * Performs a full replacement of subscription data. All fields will be updated.
 */
export interface PutSubscriptionsInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput;
}
export type PutSubscriptionsOutput = void;
export const putSubscriptionsEndpoint = {
  operationId: "putSubscriptions" as const,
  method: "PUT" as const,
  path: "/api/v1/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete subscription
 *
 * Permanently deletes a subscription. Use cancel action for soft removal.
 */
export interface DeleteSubscriptionsInput {
  subscriptionId: string;
}
export type DeleteSubscriptionsOutput = void;
export const deleteSubscriptionsEndpoint = {
  operationId: "deleteSubscriptions" as const,
  method: "DELETE" as const,
  path: "/api/v1/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update subscription
 *
 * Updates specific fields of a subscription. Only provided fields are updated.
 */
export interface PatchSubscriptionsInput {
  subscriptionId: string;
  body?: Types.CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput;
}
export type PatchSubscriptionsOutput = void;
export const patchSubscriptionsEndpoint = {
  operationId: "patchSubscriptions" as const,
  method: "PATCH" as const,
  path: "/api/v1/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if subscription exists by ID
 *
 * Checks if a subscription exists by ID without returning the body.
 */
export interface HeadSubscriptionsInput {
  subscriptionId: string;
}
export type HeadSubscriptionsOutput = void;
export const headSubscriptionsEndpoint = {
  operationId: "headSubscriptions" as const,
  method: "HEAD" as const,
  path: "/api/v1/subscriptions/{subscriptionId}" as const,
  tags: ["CommerceSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription plan usage statistics
 *
 * Retrieves usage statistics for a specific subscription plan.
 */
export interface GetSubscriptionPlansUsageInput {
  planId: string;
}
export type GetSubscriptionPlansUsageOutput = void;
export const getSubscriptionPlansUsageEndpoint = {
  operationId: "getSubscriptionPlansUsage" as const,
  method: "GET" as const,
  path: "/api/v1/subscription-plans/{planId}/usage" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Get suggested plan upgrades
 *
 * Suggests upgrade plans based on current usage requirements.
 */
export interface GetSubscriptionPlansSuggestUpgradesInput {
  planId: string;
  query?: {
    users?: number;
    storageMb?: number;
    apiCalls?: number;
  };
}
export type GetSubscriptionPlansSuggestUpgradesOutput = void;
export const getSubscriptionPlansSuggestUpgradesEndpoint = {
  operationId: "getSubscriptionPlansSuggestUpgrades" as const,
  method: "GET" as const,
  path: "/api/v1/subscription-plans/{planId}/suggest-upgrades" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Calculate pricing for a subscription plan
 *
 * Calculates the total cost for a subscription plan including all applicable taxes, fees, and discounts.
 */
export interface GetSubscriptionPlansPricingInput {
  planId: string;
  query?: {
    tenantId?: string;
    discountCode?: string;
  };
}
export type GetSubscriptionPlansPricingOutput = void;
export const getSubscriptionPlansPricingEndpoint = {
  operationId: "getSubscriptionPlansPricing" as const,
  method: "GET" as const,
  path: "/api/v1/subscription-plans/{planId}/pricing" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Update subscription plan pricing
 *
 * Updates the pricing for a subscription plan.
 */
export interface PatchSubscriptionPlansPricingInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput;
}
export type PatchSubscriptionPlansPricingOutput = void;
export const patchSubscriptionPlansPricingEndpoint = {
  operationId: "patchSubscriptionPlansPricing" as const,
  method: "PATCH" as const,
  path: "/api/v1/subscription-plans/{planId}/pricing" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate subscription plan limits
 *
 * Validates whether the specified usage fits within the plan limits. Custom action per Google API guidelines.
 */
export interface PostSubscriptionPlansValidateLimitsInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput;
}
export type PostSubscriptionPlansValidateLimitsOutput = void;
export const postSubscriptionPlansValidateLimitsEndpoint = {
  operationId: "postSubscriptionPlansValidateLimits" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:validate-limits" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update subscription plan details
 *
 * Updates specific fields of a subscription plan's details.
 */
export interface PatchSubscriptionPlansDetailsInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput;
}
export type PatchSubscriptionPlansDetailsOutput = void;
export const patchSubscriptionPlansDetailsEndpoint = {
  operationId: "patchSubscriptionPlansDetails" as const,
  method: "PATCH" as const,
  path: "/api/v1/subscription-plans/{planId}/details" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Update subscription plan limits
 *
 * Updates the limits for a subscription plan.
 */
export interface PatchSubscriptionPlansLimitsInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput;
}
export type PatchSubscriptionPlansLimitsOutput = void;
export const patchSubscriptionPlansLimitsEndpoint = {
  operationId: "patchSubscriptionPlansLimits" as const,
  method: "PATCH" as const,
  path: "/api/v1/subscription-plans/{planId}/limits" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Update subscription plan features
 *
 * Updates the features for a subscription plan.
 */
export interface PatchSubscriptionPlansFeaturesInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput;
}
export type PatchSubscriptionPlansFeaturesOutput = void;
export const patchSubscriptionPlansFeaturesEndpoint = {
  operationId: "patchSubscriptionPlansFeatures" as const,
  method: "PATCH" as const,
  path: "/api/v1/subscription-plans/{planId}/features" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Activate subscription plan
 *
 * Activates a subscription plan by ID.
 */
export interface PostSubscriptionPlansActivateInput {
  planId: string;
}
export type PostSubscriptionPlansActivateOutput = void;
export const postSubscriptionPlansActivateEndpoint = {
  operationId: "postSubscriptionPlansActivate" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:activate" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Deactivate subscription plan
 *
 * Deactivates a subscription plan by ID.
 */
export interface PostSubscriptionPlansDeactivateInput {
  planId: string;
}
export type PostSubscriptionPlansDeactivateOutput = void;
export const postSubscriptionPlansDeactivateEndpoint = {
  operationId: "postSubscriptionPlansDeactivate" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:deactivate" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Archive subscription plan
 *
 * Archives a subscription plan, making it unavailable for new subscriptions while preserving existing subscriptions.
 */
export interface PostSubscriptionPlansArchiveInput {
  planId: string;
}
export type PostSubscriptionPlansArchiveOutput = void;
export const postSubscriptionPlansArchiveEndpoint = {
  operationId: "postSubscriptionPlansArchive" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:archive" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Clone subscription plan
 *
 * Creates a copy of an existing subscription plan with a new name and slug.
 */
export interface PostSubscriptionPlansCloneInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput;
}
export type PostSubscriptionPlansCloneOutput = void;
export const postSubscriptionPlansCloneEndpoint = {
  operationId: "postSubscriptionPlansClone" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:clone" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Set subscription plan featured status
 *
 * Sets whether a subscription plan is featured or not.
 */
export interface PostSubscriptionPlansFeaturedInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput;
}
export type PostSubscriptionPlansFeaturedOutput = void;
export const postSubscriptionPlansFeaturedEndpoint = {
  operationId: "postSubscriptionPlansFeatured" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:featured" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Set subscription plan external ID
 *
 * Sets the external system ID for subscription plan integration.
 */
export interface PostSubscriptionPlansExternalIdInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput;
}
export type PostSubscriptionPlansExternalIdOutput = void;
export const postSubscriptionPlansExternalIdEndpoint = {
  operationId: "postSubscriptionPlansExternalId" as const,
  method: "POST" as const,
  path: "/api/v1/subscription-plans/{planId}:external-id" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription plans with pagination and filtering
 *
 * Retrieves a paginated list of subscription plans with optional filtering. Use query parameters: featured=true for featured plans, q=searchTerm for search, slug=value for slug lookup, minPrice/maxPrice for price range.
 */
export interface GetSubscriptionPlansForGetSubscriptionPlansInput {
  query?: {
    page?: number;
    pageSize?: number;
    activeOnly?: boolean;
    isActive?: boolean;
    featured?: boolean;
    q?: string;
    slug?: string;
    minPrice?: number;
    maxPrice?: number;
  };
}
export type GetSubscriptionPlansForGetSubscriptionPlansOutput = void;
export const getSubscriptionPlansForGetSubscriptionPlansEndpoint = {
  operationId: "getSubscriptionPlansForGetSubscriptionPlans" as const,
  method: "GET" as const,
  path: "/v1/subscription-plans" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a new subscription plan
 *
 * Creates a new subscription plan with the provided information.
 */
export interface PostSubscriptionPlansInput {
  body?: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput;
}
export type PostSubscriptionPlansOutput = void;
export const postSubscriptionPlansEndpoint = {
  operationId: "postSubscriptionPlans" as const,
  method: "POST" as const,
  path: "/v1/subscription-plans" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Compare subscription plans
 *
 * Compares multiple subscription plans side by side. Custom action per Google API guidelines.
 */
export interface PostSubscriptionPlansCompareInput {
  body?: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput;
}
export type PostSubscriptionPlansCompareOutput = void;
export const postSubscriptionPlansCompareEndpoint = {
  operationId: "postSubscriptionPlansCompare" as const,
  method: "POST" as const,
  path: "/v1/subscription-plans:compare" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription plan by ID
 *
 * Retrieves detailed information for a specific subscription plan.
 */
export interface GetSubscriptionPlansForGetSubscriptionPlansByPlanIdInput {
  planId: string;
}
export type GetSubscriptionPlansForGetSubscriptionPlansByPlanIdOutput = void;
export const getSubscriptionPlansForGetSubscriptionPlansByPlanIdEndpoint = {
  operationId: "getSubscriptionPlansForGetSubscriptionPlansByPlanId" as const,
  method: "GET" as const,
  path: "/v1/subscription-plans/{planId}" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Full update subscription plan
 *
 * Performs a full replacement of subscription plan data. All fields will be updated.
 */
export interface PutSubscriptionPlansInput {
  planId: string;
  body?: Types.CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput;
}
export type PutSubscriptionPlansOutput = void;
export const putSubscriptionPlansEndpoint = {
  operationId: "putSubscriptionPlans" as const,
  method: "PUT" as const,
  path: "/v1/subscription-plans/{planId}" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete subscription plan
 *
 * Deletes a subscription plan by ID.
 */
export interface DeleteSubscriptionPlansInput {
  planId: string;
}
export type DeleteSubscriptionPlansOutput = void;
export const deleteSubscriptionPlansEndpoint = {
  operationId: "deleteSubscriptionPlans" as const,
  method: "DELETE" as const,
  path: "/v1/subscription-plans/{planId}" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if subscription plan exists by ID
 *
 * Checks if a subscription plan exists by ID without returning the body.
 */
export interface HeadSubscriptionPlansInput {
  planId: string;
}
export type HeadSubscriptionPlansOutput = void;
export const headSubscriptionPlansEndpoint = {
  operationId: "headSubscriptionPlans" as const,
  method: "HEAD" as const,
  path: "/v1/subscription-plans/{planId}" as const,
  tags: ["CommerceSubscriptionsPlans"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAuditLogsInput {
  query?: {
    UserId?: string;
    TenantId?: string;
    ActionType?: string;
    ResourceType?: string;
    Category?: Types.ComplianceAuditAuditCategory;
    RiskLevel?: Types.ComplianceAuditAuditRiskLevel;
    Success?: boolean;
    StartDate?: string;
    EndDate?: string;
    IpAddress?: string;
    Skip?: number;
    Take?: number;
  };
}
export type GetAdminAuditLogsOutput = Types.ComplianceAuditAuditLogOutput;
export const getAdminAuditLogsEndpoint = {
  operationId: "getAdminAuditLogs" as const,
  method: "GET" as const,
  path: "/v1/admin/audit-logs" as const,
  tags: ["ComplianceAudit"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminAuditLogsStatisticsInput {
  query?: {
    StartDate?: string;
    EndDate?: string;
  };
}
export type GetAdminAuditLogsStatisticsOutput =
  Types.ComplianceAuditAuditStatisticsOutput;
export const getAdminAuditLogsStatisticsEndpoint = {
  operationId: "getAdminAuditLogsStatistics" as const,
  method: "GET" as const,
  path: "/v1/admin/audit-logs/statistics" as const,
  tags: ["ComplianceAudit"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminAuditLogsExportInput {
  body?: Types.ComplianceAuditAuditExportInput;
}
export type PostAdminAuditLogsExportOutput = void;
export const postAdminAuditLogsExportEndpoint = {
  operationId: "postAdminAuditLogsExport" as const,
  method: "POST" as const,
  path: "/v1/admin/audit-logs/:export" as const,
  tags: ["ComplianceAudit"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminSecurityAuditInput {
  query?: {
    SourceType?: Types.ComplianceAuditSecurityAuditSourceType;
    UserId?: string;
    TenantId?: string;
    ActionType?: string;
    Success?: boolean;
    RiskLevel?: Types.ComplianceAuditAuditRiskLevel;
    StartDate?: string;
    EndDate?: string;
    IpAddress?: string;
    SearchText?: string;
    Skip?: number;
    Take?: number;
    SortBy?: string;
    SortDirection?: string;
  };
}
export type GetAdminSecurityAuditOutput =
  Types.ComplianceAuditUnifiedSecurityAuditOutput;
export const getAdminSecurityAuditEndpoint = {
  operationId: "getAdminSecurityAudit" as const,
  method: "GET" as const,
  path: "/v1/admin/security-audit" as const,
  tags: ["ComplianceAuditSecurity"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminSecurityAuditAuthenticationInput {
  query?: {
    UserId?: string;
    Email?: string;
    IpAddress?: string;
    Success?: boolean;
    FailureReason?: string;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
  };
}
export type GetAdminSecurityAuditAuthenticationOutput =
  Types.ComplianceAuditAuthenticationAuditOutput;
export const getAdminSecurityAuditAuthenticationEndpoint = {
  operationId: "getAdminSecurityAuditAuthentication" as const,
  method: "GET" as const,
  path: "/v1/admin/security-audit/authentication" as const,
  tags: ["ComplianceAuditSecurity"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminSecurityAuditPermissionsInput {
  query?: {
    UserId?: string;
    TenantId?: string;
    PermissionType?: string;
    OperationType?: string;
    ResourceType?: string;
    Success?: boolean;
    StartDate?: string;
    EndDate?: string;
    Skip?: number;
    Take?: number;
  };
}
export type GetAdminSecurityAuditPermissionsOutput =
  Types.ComplianceAuditPermissionAuditOutput;
export const getAdminSecurityAuditPermissionsEndpoint = {
  operationId: "getAdminSecurityAuditPermissions" as const,
  method: "GET" as const,
  path: "/v1/admin/security-audit/permissions" as const,
  tags: ["ComplianceAuditSecurity"] as const,
  requiresAuth: true,
} as const;

export interface GetAdminSecurityAuditDashboardInput {
  query?: {
    startDate?: string;
    endDate?: string;
    tenantId?: string;
  };
}
export type GetAdminSecurityAuditDashboardOutput =
  Types.ComplianceAuditSecurityAuditDashboard;
export const getAdminSecurityAuditDashboardEndpoint = {
  operationId: "getAdminSecurityAuditDashboard" as const,
  method: "GET" as const,
  path: "/v1/admin/security-audit/dashboard" as const,
  tags: ["ComplianceAuditSecurity"] as const,
  requiresAuth: true,
} as const;

export interface PostAdminSecurityAuditExportInput {
  body?: Types.ComplianceAuditUnifiedSecurityAuditInput;
}
export type PostAdminSecurityAuditExportOutput = Blob;
export const postAdminSecurityAuditExportEndpoint = {
  operationId: "postAdminSecurityAuditExport" as const,
  method: "POST" as const,
  path: "/v1/admin/security-audit/:export" as const,
  tags: ["ComplianceAuditSecurity"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceConsentPoliciesInput {
  query?: {
    tenantId?: string;
  };
}
export type GetApiComplianceConsentPoliciesOutput =
  Array<Types.ComplianceConsentConsentPolicy>;
export const getApiComplianceConsentPoliciesEndpoint = {
  operationId: "getApiComplianceConsentPolicies" as const,
  method: "GET" as const,
  path: "/api/compliance/consent/policies" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentPoliciesInput {
  body?: Types.ComplianceConsentCreateConsentPolicyCommand;
}
export type PostApiComplianceConsentPoliciesOutput = string;
export const postApiComplianceConsentPoliciesEndpoint = {
  operationId: "postApiComplianceConsentPolicies" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/policies" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentPoliciesVersionsInput {
  policyId: string;
  body?: Types.ComplianceConsentPublishVersionInput;
}
export type PostApiComplianceConsentPoliciesVersionsOutput =
  Types.ComplianceConsentPolicyVersion;
export const postApiComplianceConsentPoliciesVersionsEndpoint = {
  operationId: "postApiComplianceConsentPoliciesVersions" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/policies/{policyId}/versions" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceConsentUsersInput {
  userId: string;
}
export type GetApiComplianceConsentUsersOutput =
  Array<Types.ComplianceConsentUserConsent>;
export const getApiComplianceConsentUsersEndpoint = {
  operationId: "getApiComplianceConsentUsers" as const,
  method: "GET" as const,
  path: "/api/compliance/consent/users/{userId}" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentGrantInput {
  body?: Types.ComplianceConsentGrantConsentCommand;
}
export type PostApiComplianceConsentGrantOutput =
  Types.ComplianceConsentUserConsent;
export const postApiComplianceConsentGrantEndpoint = {
  operationId: "postApiComplianceConsentGrant" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/grant" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentRevokeInput {
  body?: Types.ComplianceConsentRevokeConsentCommand;
}
export type PostApiComplianceConsentRevokeOutput = void;
export const postApiComplianceConsentRevokeEndpoint = {
  operationId: "postApiComplianceConsentRevoke" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/revoke" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentDataSubjectRequestsInput {
  body?: Types.ComplianceConsentSubmitDataSubjectRequestCommand;
}
export type PostApiComplianceConsentDataSubjectRequestsOutput =
  Types.ComplianceConsentDataSubjectInput;
export const postApiComplianceConsentDataSubjectRequestsEndpoint = {
  operationId: "postApiComplianceConsentDataSubjectRequests" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/data-subject-requests" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceConsentDataSubjectRequestsProcessInput {
  requestId: string;
  body?: Types.ComplianceConsentProcessRequestBody;
}
export type PostApiComplianceConsentDataSubjectRequestsProcessOutput =
  Types.ComplianceConsentDataSubjectInput;
export const postApiComplianceConsentDataSubjectRequestsProcessEndpoint = {
  operationId: "postApiComplianceConsentDataSubjectRequestsProcess" as const,
  method: "POST" as const,
  path: "/api/compliance/consent/data-subject-requests/{requestId}/process" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export type GetApiComplianceConsentDataSubjectRequestsPendingInput = void;
export type GetApiComplianceConsentDataSubjectRequestsPendingOutput =
  Array<Types.ComplianceConsentDataSubjectInput>;
export const getApiComplianceConsentDataSubjectRequestsPendingEndpoint = {
  operationId: "getApiComplianceConsentDataSubjectRequestsPending" as const,
  method: "GET" as const,
  path: "/api/compliance/consent/data-subject-requests/pending" as const,
  tags: ["ComplianceConsent"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceFerpaStudentsRecordsInput {
  studentUserId: string;
}
export type GetApiComplianceFerpaStudentsRecordsOutput =
  Array<Types.ComplianceFERPAFerpaEducationRecord>;
export const getApiComplianceFerpaStudentsRecordsEndpoint = {
  operationId: "getApiComplianceFerpaStudentsRecords" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/students/{studentUserId}/records" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceFerpaStudentsDirectoryInformationInput {
  studentUserId: string;
}
export type GetApiComplianceFerpaStudentsDirectoryInformationOutput =
  Array<Types.ComplianceFERPAFerpaEducationRecord>;
export const getApiComplianceFerpaStudentsDirectoryInformationEndpoint = {
  operationId: "getApiComplianceFerpaStudentsDirectoryInformation" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/students/{studentUserId}/directory-information" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaRecordsInput {
  body?: Types.ComplianceFERPARegisterEducationRecordCommand;
}
export type PostApiComplianceFerpaRecordsOutput =
  Types.ComplianceFERPAFerpaEducationRecord;
export const postApiComplianceFerpaRecordsEndpoint = {
  operationId: "postApiComplianceFerpaRecords" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/records" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceFerpaDirectoryPolicyInput {
  query?: {
    tenantId?: string;
  };
}
export type GetApiComplianceFerpaDirectoryPolicyOutput =
  Types.ComplianceFERPAFerpaDirectoryInformationPolicy;
export const getApiComplianceFerpaDirectoryPolicyEndpoint = {
  operationId: "getApiComplianceFerpaDirectoryPolicy" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/directory-policy" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PutApiComplianceFerpaDirectoryPolicyInput {
  body?: Types.ComplianceFERPAUpsertDirectoryInformationPolicyCommand;
}
export type PutApiComplianceFerpaDirectoryPolicyOutput =
  Types.ComplianceFERPAFerpaDirectoryInformationPolicy;
export const putApiComplianceFerpaDirectoryPolicyEndpoint = {
  operationId: "putApiComplianceFerpaDirectoryPolicy" as const,
  method: "PUT" as const,
  path: "/api/compliance/ferpa/directory-policy" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceFerpaStudentsConsentsInput {
  studentUserId: string;
}
export type GetApiComplianceFerpaStudentsConsentsOutput =
  Array<Types.ComplianceFERPAFerpaDisclosureConsent>;
export const getApiComplianceFerpaStudentsConsentsEndpoint = {
  operationId: "getApiComplianceFerpaStudentsConsents" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/students/{studentUserId}/consents" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaConsentsInput {
  body?: Types.ComplianceFERPAGrantFerpaDisclosureConsentCommand;
}
export type PostApiComplianceFerpaConsentsOutput =
  Types.ComplianceFERPAFerpaDisclosureConsent;
export const postApiComplianceFerpaConsentsEndpoint = {
  operationId: "postApiComplianceFerpaConsents" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/consents" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaConsentsRevokeInput {
  consentId: string;
}
export type PostApiComplianceFerpaConsentsRevokeOutput = void;
export const postApiComplianceFerpaConsentsRevokeEndpoint = {
  operationId: "postApiComplianceFerpaConsentsRevoke" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/consents/{consentId}/revoke" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaDisclosuresInput {
  body?: Types.ComplianceFERPARecordFerpaDisclosureCommand;
}
export type PostApiComplianceFerpaDisclosuresOutput =
  Types.ComplianceFERPAFerpaDisclosureLog;
export const postApiComplianceFerpaDisclosuresEndpoint = {
  operationId: "postApiComplianceFerpaDisclosures" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/disclosures" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface GetApiComplianceFerpaStudentsDisclosuresInput {
  studentUserId: string;
}
export type GetApiComplianceFerpaStudentsDisclosuresOutput =
  Array<Types.ComplianceFERPAFerpaDisclosureLog>;
export const getApiComplianceFerpaStudentsDisclosuresEndpoint = {
  operationId: "getApiComplianceFerpaStudentsDisclosures" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/students/{studentUserId}/disclosures" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaInspectionRequestsInput {
  body?: Types.ComplianceFERPASubmitFerpaInspectionRequestCommand;
}
export type PostApiComplianceFerpaInspectionRequestsOutput =
  Types.ComplianceFERPAFerpaInspectionInput;
export const postApiComplianceFerpaInspectionRequestsEndpoint = {
  operationId: "postApiComplianceFerpaInspectionRequests" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/inspection-requests" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface PostApiComplianceFerpaInspectionRequestsCompleteInput {
  requestId: string;
  body?: Types.ComplianceFERPACompleteFerpaInspectionRequestBody;
}
export type PostApiComplianceFerpaInspectionRequestsCompleteOutput =
  Types.ComplianceFERPAFerpaInspectionInput;
export const postApiComplianceFerpaInspectionRequestsCompleteEndpoint = {
  operationId: "postApiComplianceFerpaInspectionRequestsComplete" as const,
  method: "POST" as const,
  path: "/api/compliance/ferpa/inspection-requests/{requestId}/complete" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export type GetApiComplianceFerpaInspectionRequestsPendingInput = void;
export type GetApiComplianceFerpaInspectionRequestsPendingOutput =
  Array<Types.ComplianceFERPAFerpaInspectionInput>;
export const getApiComplianceFerpaInspectionRequestsPendingEndpoint = {
  operationId: "getApiComplianceFerpaInspectionRequestsPending" as const,
  method: "GET" as const,
  path: "/api/compliance/ferpa/inspection-requests/pending" as const,
  tags: ["ComplianceFerpa"] as const,
  requiresAuth: true,
} as const;

export interface GetMarketingLeadsInput {
  query?: {
    source?: string;
    status?: string;
    topic?: string;
    search?: string;
    skip?: number;
    take?: number;
  };
}
export type GetMarketingLeadsOutput = Array<Types.ContentPagesMarketingLead>;
export const getMarketingLeadsEndpoint = {
  operationId: "getMarketingLeads" as const,
  method: "GET" as const,
  path: "/v1/marketing/leads" as const,
  tags: ["ContentMarketingLeads"] as const,
  requiresAuth: true,
} as const;

export interface PostMarketingLeadsInput {
  body?: Types.ContentPagesCreateMarketingLead;
}
export type PostMarketingLeadsOutput = Types.ContentPagesMarketingLead;
export const postMarketingLeadsEndpoint = {
  operationId: "postMarketingLeads" as const,
  method: "POST" as const,
  path: "/v1/marketing/leads" as const,
  tags: ["ContentMarketingLeads"] as const,
  requiresAuth: true,
} as const;

export interface GetMarketingLeadByIdInput {
  id: string;
}
export type GetMarketingLeadByIdOutput = Types.ContentPagesMarketingLead;
export const getMarketingLeadByIdEndpoint = {
  operationId: "getMarketingLeadById" as const,
  method: "GET" as const,
  path: "/v1/marketing/leads/{id}" as const,
  tags: ["ContentMarketingLeads"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesForGetPagesInput {
  query?: {
    type?: Types.ContentPagesPageType;
    status?: Types.ContentPagesPageStatus;
    locale?: string;
    parentId?: string;
    skip?: number;
    take?: number;
  };
}
export type GetPagesForGetPagesOutput = Array<Types.ContentPagesPage>;
export const getPagesForGetPagesEndpoint = {
  operationId: "getPagesForGetPages" as const,
  method: "GET" as const,
  path: "/v1/pages" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PostPagesInput {
  body?: Types.ContentPagesCreatePage;
}
export type PostPagesOutput = Types.ContentPagesPage;
export const postPagesEndpoint = {
  operationId: "postPages" as const,
  method: "POST" as const,
  path: "/v1/pages" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesForGetPagesByIdInput {
  id: string;
}
export type GetPagesForGetPagesByIdOutput = Types.ContentPagesPage;
export const getPagesForGetPagesByIdEndpoint = {
  operationId: "getPagesForGetPagesById" as const,
  method: "GET" as const,
  path: "/v1/pages/{id}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PutPagesInput {
  id: string;
  body?: Types.ContentPagesUpdatePage;
}
export type PutPagesOutput = Types.ContentPagesPage;
export const putPagesEndpoint = {
  operationId: "putPages" as const,
  method: "PUT" as const,
  path: "/v1/pages/{id}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface DeletePagesInput {
  id: string;
}
export type DeletePagesOutput = void;
export const deletePagesEndpoint = {
  operationId: "deletePages" as const,
  method: "DELETE" as const,
  path: "/v1/pages/{id}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesBySlugInput {
  slug: string;
}
export type GetPagesBySlugOutput = Types.ContentPagesPage;
export const getPagesBySlugEndpoint = {
  operationId: "getPagesBySlug" as const,
  method: "GET" as const,
  path: "/v1/pages/by-slug/{slug}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesSitemapInput {
  query?: {
    locale?: string;
  };
}
export type GetPagesSitemapOutput = Array<Types.ContentPagesSitemapEntry>;
export const getPagesSitemapEndpoint = {
  operationId: "getPagesSitemap" as const,
  method: "GET" as const,
  path: "/v1/pages/sitemap" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PostPagesPublishInput {
  id: string;
}
export type PostPagesPublishOutput = Types.ContentPagesPage;
export const postPagesPublishEndpoint = {
  operationId: "postPagesPublish" as const,
  method: "POST" as const,
  path: "/v1/pages/{id}/publish" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PostPagesUnpublishInput {
  id: string;
}
export type PostPagesUnpublishOutput = Types.ContentPagesPage;
export const postPagesUnpublishEndpoint = {
  operationId: "postPagesUnpublish" as const,
  method: "POST" as const,
  path: "/v1/pages/{id}/unpublish" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesSectionsForGetPagesByPageIdSectionsInput {
  pageId: string;
}
export type GetPagesSectionsForGetPagesByPageIdSectionsOutput =
  Array<Types.ContentPagesPageSection>;
export const getPagesSectionsForGetPagesByPageIdSectionsEndpoint = {
  operationId: "getPagesSectionsForGetPagesByPageIdSections" as const,
  method: "GET" as const,
  path: "/v1/pages/{pageId}/sections" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PostPagesSectionsInput {
  pageId: string;
  body?: Types.ContentPagesCreatePageSection;
}
export type PostPagesSectionsOutput = Types.ContentPagesPageSection;
export const postPagesSectionsEndpoint = {
  operationId: "postPagesSections" as const,
  method: "POST" as const,
  path: "/v1/pages/{pageId}/sections" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetPagesSectionsForGetPagesByPageIdSectionsBySectionIdInput {
  pageId: string;
  sectionId: string;
}
export type GetPagesSectionsForGetPagesByPageIdSectionsBySectionIdOutput =
  Types.ContentPagesPageSection;
export const getPagesSectionsForGetPagesByPageIdSectionsBySectionIdEndpoint = {
  operationId:
    "getPagesSectionsForGetPagesByPageIdSectionsBySectionId" as const,
  method: "GET" as const,
  path: "/v1/pages/{pageId}/sections/{sectionId}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PutPagesSectionsInput {
  pageId: string;
  sectionId: string;
  body?: Types.ContentPagesUpdatePageSection;
}
export type PutPagesSectionsOutput = Types.ContentPagesPageSection;
export const putPagesSectionsEndpoint = {
  operationId: "putPagesSections" as const,
  method: "PUT" as const,
  path: "/v1/pages/{pageId}/sections/{sectionId}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface DeletePagesSectionsInput {
  pageId: string;
  sectionId: string;
}
export type DeletePagesSectionsOutput = void;
export const deletePagesSectionsEndpoint = {
  operationId: "deletePagesSections" as const,
  method: "DELETE" as const,
  path: "/v1/pages/{pageId}/sections/{sectionId}" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface PostPagesSectionsReorderInput {
  pageId: string;
  body?: Array<string>;
}
export type PostPagesSectionsReorderOutput = void;
export const postPagesSectionsReorderEndpoint = {
  operationId: "postPagesSectionsReorder" as const,
  method: "POST" as const,
  path: "/v1/pages/{pageId}/sections/reorder" as const,
  tags: ["ContentPages"] as const,
  requiresAuth: true,
} as const;

export interface GetOgInput {
  slug: string;
}
export type GetOgOutput = Types.ContentPagesOpenGraphMetadata;
export const getOgEndpoint = {
  operationId: "getOg" as const,
  method: "GET" as const,
  path: "/v1/og/{slug}" as const,
  tags: ["ContentPagesOpenGraph"] as const,
  requiresAuth: true,
} as const;

export interface GetContentResourcesForGetContentResourcesInput {
  query?: {
    type?: Types.ContentPagesContentResourceType;
    status?: Types.ContentPagesContentResourceStatus;
    locale?: string;
    category?: string;
    featured?: boolean;
    q?: string;
    skip?: number;
    take?: number;
  };
}
export type GetContentResourcesForGetContentResourcesOutput =
  Array<Types.ContentPagesContentResource>;
export const getContentResourcesForGetContentResourcesEndpoint = {
  operationId: "getContentResourcesForGetContentResources" as const,
  method: "GET" as const,
  path: "/v1/content-resources" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface PostContentResourcesInput {
  body?: Types.ContentPagesCreateContentResource;
}
export type PostContentResourcesOutput = Types.ContentPagesContentResource;
export const postContentResourcesEndpoint = {
  operationId: "postContentResources" as const,
  method: "POST" as const,
  path: "/v1/content-resources" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface GetContentResourcesForGetContentResourcesByIdInput {
  id: string;
}
export type GetContentResourcesForGetContentResourcesByIdOutput =
  Types.ContentPagesContentResource;
export const getContentResourcesForGetContentResourcesByIdEndpoint = {
  operationId: "getContentResourcesForGetContentResourcesById" as const,
  method: "GET" as const,
  path: "/v1/content-resources/{id}" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface PutContentResourcesInput {
  id: string;
  body?: Types.ContentPagesUpdateContentResource;
}
export type PutContentResourcesOutput = Types.ContentPagesContentResource;
export const putContentResourcesEndpoint = {
  operationId: "putContentResources" as const,
  method: "PUT" as const,
  path: "/v1/content-resources/{id}" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface DeleteContentResourcesInput {
  id: string;
}
export type DeleteContentResourcesOutput = void;
export const deleteContentResourcesEndpoint = {
  operationId: "deleteContentResources" as const,
  method: "DELETE" as const,
  path: "/v1/content-resources/{id}" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface GetContentResourcesBySlugInput {
  slug: string;
}
export type GetContentResourcesBySlugOutput = Types.ContentPagesContentResource;
export const getContentResourcesBySlugEndpoint = {
  operationId: "getContentResourcesBySlug" as const,
  method: "GET" as const,
  path: "/v1/content-resources/by-slug/{slug}" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface PostContentResourcesPublishInput {
  id: string;
}
export type PostContentResourcesPublishOutput =
  Types.ContentPagesContentResource;
export const postContentResourcesPublishEndpoint = {
  operationId: "postContentResourcesPublish" as const,
  method: "POST" as const,
  path: "/v1/content-resources/{id}/publish" as const,
  tags: ["ContentPagesResources"] as const,
  requiresAuth: true,
} as const;

export interface PostCourseInteractionsInput {
  query?: {
    programId?: string;
  };
  body?: Types.LearningCoursesStartContentInput;
}
export type PostCourseInteractionsOutput =
  Types.LearningCoursesContentInteraction;
export const postCourseInteractionsEndpoint = {
  operationId: "postCourseInteractions" as const,
  method: "POST" as const,
  path: "/v1/course-interactions" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface PutCourseInteractionsProgressInput {
  interactionId: string;
  query?: {
    programId?: string;
  };
  body?: Types.LearningCoursesUpdateProgressInput;
}
export type PutCourseInteractionsProgressOutput =
  Types.LearningCoursesContentInteraction;
export const putCourseInteractionsProgressEndpoint = {
  operationId: "putCourseInteractionsProgress" as const,
  method: "PUT" as const,
  path: "/v1/course-interactions/{interactionId}/progress" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface PostCourseInteractionsSubmitInput {
  interactionId: string;
  query?: {
    programId?: string;
  };
  body?: Types.LearningCoursesSubmitContentInput;
}
export type PostCourseInteractionsSubmitOutput =
  Types.LearningCoursesContentInteraction;
export const postCourseInteractionsSubmitEndpoint = {
  operationId: "postCourseInteractionsSubmit" as const,
  method: "POST" as const,
  path: "/v1/course-interactions/{interactionId}/submit" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface PostCourseInteractionsCompleteInput {
  interactionId: string;
  query?: {
    programId?: string;
  };
  body?: Types.LearningCoursesCompleteContentInput;
}
export type PostCourseInteractionsCompleteOutput =
  Types.LearningCoursesContentInteraction;
export const postCourseInteractionsCompleteEndpoint = {
  operationId: "postCourseInteractionsComplete" as const,
  method: "POST" as const,
  path: "/v1/course-interactions/{interactionId}/complete" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsUserContentInput {
  programUserId: string;
  contentId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsUserContentOutput =
  Types.LearningCoursesContentInteraction;
export const getCourseInteractionsUserContentEndpoint = {
  operationId: "getCourseInteractionsUserContent" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/user/{programUserId}/content/{contentId}" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsUserInput {
  programUserId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsUserOutput =
  Array<Types.LearningCoursesContentInteraction>;
export const getCourseInteractionsUserEndpoint = {
  operationId: "getCourseInteractionsUser" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/user/{programUserId}" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsContentSurveyResultsInput {
  contentId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsContentSurveyResultsOutput =
  Array<Types.LearningCoursesSurveyResponseResult>;
export const getCourseInteractionsContentSurveyResultsEndpoint = {
  operationId: "getCourseInteractionsContentSurveyResults" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/content/{contentId}/survey-results" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsContentSurveyResultsVisibleInput {
  contentId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsContentSurveyResultsVisibleOutput =
  Array<Types.LearningCoursesSurveyResponseResult>;
export const getCourseInteractionsContentSurveyResultsVisibleEndpoint = {
  operationId: "getCourseInteractionsContentSurveyResultsVisible" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/content/{contentId}/survey-results/visible" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsContentReflectionResponsesInput {
  contentId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsContentReflectionResponsesOutput =
  Array<Types.LearningCoursesReflectionResponseResult>;
export const getCourseInteractionsContentReflectionResponsesEndpoint = {
  operationId: "getCourseInteractionsContentReflectionResponses" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/content/{contentId}/reflection-responses" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface GetCourseInteractionsContentReflectionResponsesVisibleInput {
  contentId: string;
  query?: {
    programId?: string;
  };
}
export type GetCourseInteractionsContentReflectionResponsesVisibleOutput =
  Array<Types.LearningCoursesReflectionResponseResult>;
export const getCourseInteractionsContentReflectionResponsesVisibleEndpoint = {
  operationId:
    "getCourseInteractionsContentReflectionResponsesVisible" as const,
  method: "GET" as const,
  path: "/v1/course-interactions/content/{contentId}/reflection-responses/visible" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface PutCourseInteractionsTimeSpentInput {
  interactionId: string;
  query?: {
    programId?: string;
  };
  body?: Types.LearningCoursesUpdateTimeSpentInput;
}
export type PutCourseInteractionsTimeSpentOutput =
  Types.LearningCoursesContentInteraction;
export const putCourseInteractionsTimeSpentEndpoint = {
  operationId: "putCourseInteractionsTimeSpent" as const,
  method: "PUT" as const,
  path: "/v1/course-interactions/{interactionId}/time-spent" as const,
  tags: ["LearningCoursesContentInteraction"] as const,
  requiresAuth: true,
} as const;

export interface PostVCoursesCheckoutCompleteInput {
  courseId: string;
  version: string;
  body?: Types.LearningCoursesCompleteCourseCheckoutInput;
}
export type PostVCoursesCheckoutCompleteOutput =
  Types.LearningCoursesCompleteCourseCheckoutOutput;
export const postVCoursesCheckoutCompleteEndpoint = {
  operationId: "postVCoursesCheckoutComplete" as const,
  method: "POST" as const,
  path: "/v{version}/courses/{courseId}/checkout/complete" as const,
  tags: ["LearningCoursesCheckout"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesStudentsMessageInput {
  courseId: string;
  body?: Types.LearningCoursesSendCourseStudentMessageInput;
}
export type PostCoursesStudentsMessageOutput =
  Types.LearningCoursesSendCourseStudentMessageOutput;
export const postCoursesStudentsMessageEndpoint = {
  operationId: "postCoursesStudentsMessage" as const,
  method: "POST" as const,
  path: "/v1/courses/{courseId}/students/message" as const,
  tags: ["LearningCoursesStudents"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsInput {
  courseId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsOutput =
  Types.PagedResultOfCommerceProductsSupportTicket;
export const getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsEndpoint =
  {
    operationId:
      "getCoursesSupportTicketsForGetCoursesByCourseIdSupportTickets" as const,
    method: "GET" as const,
    path: "/v1/courses/{courseId}/support/tickets" as const,
    tags: ["LearningCoursesSupportTickets"] as const,
    requiresAuth: true,
  } as const;

export interface GetCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketIdInput {
  courseId: string;
  ticketId: string;
}
export type GetCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketIdOutput =
  Types.CommerceProductsSupportTicket;
export const getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketIdEndpoint =
  {
    operationId:
      "getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketId" as const,
    method: "GET" as const,
    path: "/v1/courses/{courseId}/support/tickets/{ticketId}" as const,
    tags: ["LearningCoursesSupportTickets"] as const,
    requiresAuth: true,
  } as const;

export interface PostCoursesSupportTicketsMessagesInput {
  courseId: string;
  ticketId: string;
  body?: Types.LearningCoursesCourseSupportTicketMessageInput;
}
export type PostCoursesSupportTicketsMessagesOutput =
  Types.CommerceProductsSupportTicket;
export const postCoursesSupportTicketsMessagesEndpoint = {
  operationId: "postCoursesSupportTicketsMessages" as const,
  method: "POST" as const,
  path: "/v1/courses/{courseId}/support/tickets/{ticketId}/messages" as const,
  tags: ["LearningCoursesSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesSupportTicketsResolveInput {
  courseId: string;
  ticketId: string;
  body?: Types.LearningCoursesResolveCourseSupportTicketInput;
}
export type PostCoursesSupportTicketsResolveOutput =
  Types.CommerceProductsSupportTicket;
export const postCoursesSupportTicketsResolveEndpoint = {
  operationId: "postCoursesSupportTicketsResolve" as const,
  method: "POST" as const,
  path: "/v1/courses/{courseId}/support/tickets/{ticketId}:resolve" as const,
  tags: ["LearningCoursesSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryFeaturedForGetDiscoveryFeaturedInput {
  query?: {
    tenantId?: string;
    skip?: number;
    take?: number;
  };
}
export type GetDiscoveryFeaturedForGetDiscoveryFeaturedOutput =
  Array<Types.LearningExperienceDiscoveryFeaturedContent>;
export const getDiscoveryFeaturedForGetDiscoveryFeaturedEndpoint = {
  operationId: "getDiscoveryFeaturedForGetDiscoveryFeatured" as const,
  method: "GET" as const,
  path: "/v1/discovery/featured" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoveryFeaturedInput {
  query?: {
    tenantId?: string;
  };
  body?: Types.LearningExperienceDiscoveryCreateFeaturedContent;
}
export type PostDiscoveryFeaturedOutput =
  Types.LearningExperienceDiscoveryFeaturedContent;
export const postDiscoveryFeaturedEndpoint = {
  operationId: "postDiscoveryFeatured" as const,
  method: "POST" as const,
  path: "/v1/discovery/featured" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryFeaturedTypeInput {
  type: Types.LearningExperienceDiscoveryFeaturedContentType;
  query?: {
    tenantId?: string;
    skip?: number;
    take?: number;
  };
}
export type GetDiscoveryFeaturedTypeOutput =
  Array<Types.LearningExperienceDiscoveryFeaturedContent>;
export const getDiscoveryFeaturedTypeEndpoint = {
  operationId: "getDiscoveryFeaturedType" as const,
  method: "GET" as const,
  path: "/v1/discovery/featured/type/{type}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryFeaturedForGetDiscoveryFeaturedByIdInput {
  id: string;
}
export type GetDiscoveryFeaturedForGetDiscoveryFeaturedByIdOutput =
  Types.LearningExperienceDiscoveryFeaturedContent;
export const getDiscoveryFeaturedForGetDiscoveryFeaturedByIdEndpoint = {
  operationId: "getDiscoveryFeaturedForGetDiscoveryFeaturedById" as const,
  method: "GET" as const,
  path: "/v1/discovery/featured/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PutDiscoveryFeaturedInput {
  id: string;
  body?: Types.LearningExperienceDiscoveryUpdateFeaturedContent;
}
export type PutDiscoveryFeaturedOutput =
  Types.LearningExperienceDiscoveryFeaturedContent;
export const putDiscoveryFeaturedEndpoint = {
  operationId: "putDiscoveryFeatured" as const,
  method: "PUT" as const,
  path: "/v1/discovery/featured/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface DeleteDiscoveryFeaturedInput {
  id: string;
}
export type DeleteDiscoveryFeaturedOutput = void;
export const deleteDiscoveryFeaturedEndpoint = {
  operationId: "deleteDiscoveryFeatured" as const,
  method: "DELETE" as const,
  path: "/v1/discovery/featured/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PatchDiscoveryFeaturedToggleInput {
  id: string;
  query?: {
    isActive?: boolean;
  };
}
export type PatchDiscoveryFeaturedToggleOutput =
  Types.LearningExperienceDiscoveryFeaturedContent;
export const patchDiscoveryFeaturedToggleEndpoint = {
  operationId: "patchDiscoveryFeaturedToggle" as const,
  method: "PATCH" as const,
  path: "/v1/discovery/featured/{id}/toggle" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryCollectionsForGetDiscoveryCollectionsInput {
  query?: {
    tenantId?: string;
    type?: Types.LearningExperienceDiscoveryCollectionType;
    skip?: number;
    take?: number;
  };
}
export type GetDiscoveryCollectionsForGetDiscoveryCollectionsOutput =
  Array<Types.LearningExperienceDiscoveryCourseCollection>;
export const getDiscoveryCollectionsForGetDiscoveryCollectionsEndpoint = {
  operationId: "getDiscoveryCollectionsForGetDiscoveryCollections" as const,
  method: "GET" as const,
  path: "/v1/discovery/collections" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoveryCollectionsInput {
  query?: {
    curatorId?: string;
    tenantId?: string;
  };
  body?: Types.LearningExperienceDiscoveryCreateCourseCollection;
}
export type PostDiscoveryCollectionsOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const postDiscoveryCollectionsEndpoint = {
  operationId: "postDiscoveryCollections" as const,
  method: "POST" as const,
  path: "/v1/discovery/collections" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryCollectionsFeaturedInput {
  query?: {
    tenantId?: string;
    take?: number;
  };
}
export type GetDiscoveryCollectionsFeaturedOutput =
  Array<Types.LearningExperienceDiscoveryCourseCollection>;
export const getDiscoveryCollectionsFeaturedEndpoint = {
  operationId: "getDiscoveryCollectionsFeatured" as const,
  method: "GET" as const,
  path: "/v1/discovery/collections/featured" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryCollectionsSlugInput {
  slug: string;
  query?: {
    tenantId?: string;
  };
}
export type GetDiscoveryCollectionsSlugOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const getDiscoveryCollectionsSlugEndpoint = {
  operationId: "getDiscoveryCollectionsSlug" as const,
  method: "GET" as const,
  path: "/v1/discovery/collections/slug/{slug}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryCollectionsForGetDiscoveryCollectionsByIdInput {
  id: string;
}
export type GetDiscoveryCollectionsForGetDiscoveryCollectionsByIdOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const getDiscoveryCollectionsForGetDiscoveryCollectionsByIdEndpoint = {
  operationId: "getDiscoveryCollectionsForGetDiscoveryCollectionsById" as const,
  method: "GET" as const,
  path: "/v1/discovery/collections/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PutDiscoveryCollectionsInput {
  id: string;
  body?: Types.LearningExperienceDiscoveryUpdateCourseCollection;
}
export type PutDiscoveryCollectionsOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const putDiscoveryCollectionsEndpoint = {
  operationId: "putDiscoveryCollections" as const,
  method: "PUT" as const,
  path: "/v1/discovery/collections/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface DeleteDiscoveryCollectionsInput {
  id: string;
}
export type DeleteDiscoveryCollectionsOutput = void;
export const deleteDiscoveryCollectionsEndpoint = {
  operationId: "deleteDiscoveryCollections" as const,
  method: "DELETE" as const,
  path: "/v1/discovery/collections/{id}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoveryCollectionsCuratorInput {
  curatorId: string;
  query?: {
    includeUnpublished?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetDiscoveryCollectionsCuratorOutput =
  Array<Types.LearningExperienceDiscoveryCourseCollection>;
export const getDiscoveryCollectionsCuratorEndpoint = {
  operationId: "getDiscoveryCollectionsCurator" as const,
  method: "GET" as const,
  path: "/v1/discovery/collections/curator/{curatorId}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoveryCollectionsPublishInput {
  id: string;
}
export type PostDiscoveryCollectionsPublishOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const postDiscoveryCollectionsPublishEndpoint = {
  operationId: "postDiscoveryCollectionsPublish" as const,
  method: "POST" as const,
  path: "/v1/discovery/collections/{id}/publish" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoveryCollectionsUnpublishInput {
  id: string;
}
export type PostDiscoveryCollectionsUnpublishOutput =
  Types.LearningExperienceDiscoveryCourseCollection;
export const postDiscoveryCollectionsUnpublishEndpoint = {
  operationId: "postDiscoveryCollectionsUnpublish" as const,
  method: "POST" as const,
  path: "/v1/discovery/collections/{id}/unpublish" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoverySearchRecordInput {
  query?: {
    userId?: string;
  };
  body?: Types.LearningExperienceDiscoveryRecordSearch;
}
export type PostDiscoverySearchRecordOutput =
  Types.LearningExperienceDiscoverySearchHistory;
export const postDiscoverySearchRecordEndpoint = {
  operationId: "postDiscoverySearchRecord" as const,
  method: "POST" as const,
  path: "/v1/discovery/search/record" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostDiscoverySearchClickInput {
  searchId: string;
  body?: Types.LearningExperienceDiscoveryRecordSearchClick;
}
export type PostDiscoverySearchClickOutput = void;
export const postDiscoverySearchClickEndpoint = {
  operationId: "postDiscoverySearchClick" as const,
  method: "POST" as const,
  path: "/v1/discovery/search/{searchId}/click" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoverySearchHistoryInput {
  userId: string;
  query?: {
    take?: number;
  };
}
export type GetDiscoverySearchHistoryOutput =
  Array<Types.LearningExperienceDiscoverySearchHistory>;
export const getDiscoverySearchHistoryEndpoint = {
  operationId: "getDiscoverySearchHistory" as const,
  method: "GET" as const,
  path: "/v1/discovery/search/history/{userId}" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface GetDiscoverySearchPopularInput {
  query?: {
    daysBack?: number;
    take?: number;
  };
}
export type GetDiscoverySearchPopularOutput =
  Array<Types.LearningExperienceDiscoveryPopularSearchResult>;
export const getDiscoverySearchPopularEndpoint = {
  operationId: "getDiscoverySearchPopular" as const,
  method: "GET" as const,
  path: "/v1/discovery/search/popular" as const,
  tags: ["LearningExperienceDiscovery"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialDiscussionsInput {
  body?: Types.LearningExperienceSocialServicesCreateDiscussionInput;
}
export type PostApiSocialDiscussionsOutput =
  Types.LearningExperienceSocialServicesCourseDiscussion;
export const postApiSocialDiscussionsEndpoint = {
  operationId: "postApiSocialDiscussions" as const,
  method: "POST" as const,
  path: "/api/social/discussions" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialDiscussionsInput {
  id: string;
}
export type GetApiSocialDiscussionsOutput =
  Types.LearningExperienceSocialServicesCourseDiscussion;
export const getApiSocialDiscussionsEndpoint = {
  operationId: "getApiSocialDiscussions" as const,
  method: "GET" as const,
  path: "/api/social/discussions/{id}" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialDiscussionsInput {
  id: string;
}
export type DeleteApiSocialDiscussionsOutput = void;
export const deleteApiSocialDiscussionsEndpoint = {
  operationId: "deleteApiSocialDiscussions" as const,
  method: "DELETE" as const,
  path: "/api/social/discussions/{id}" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesDiscussionsInput {
  courseId: string;
  query?: {
    skip?: number;
    take?: number;
    pinnedFirst?: boolean;
  };
}
export type GetApiSocialCoursesDiscussionsOutput =
  Array<Types.LearningExperienceSocialServicesCourseDiscussion>;
export const getApiSocialCoursesDiscussionsEndpoint = {
  operationId: "getApiSocialCoursesDiscussions" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/discussions" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesContentDiscussionsInput {
  courseId: string;
  contentId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialCoursesContentDiscussionsOutput =
  Array<Types.LearningExperienceSocialServicesCourseDiscussion>;
export const getApiSocialCoursesContentDiscussionsEndpoint = {
  operationId: "getApiSocialCoursesContentDiscussions" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/content/{contentId}/discussions" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialDiscussionsPinInput {
  id: string;
}
export type PostApiSocialDiscussionsPinOutput =
  Types.LearningExperienceSocialServicesCourseDiscussion;
export const postApiSocialDiscussionsPinEndpoint = {
  operationId: "postApiSocialDiscussionsPin" as const,
  method: "POST" as const,
  path: "/api/social/discussions/{id}/pin" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialDiscussionsUnpinInput {
  id: string;
}
export type PostApiSocialDiscussionsUnpinOutput =
  Types.LearningExperienceSocialServicesCourseDiscussion;
export const postApiSocialDiscussionsUnpinEndpoint = {
  operationId: "postApiSocialDiscussionsUnpin" as const,
  method: "POST" as const,
  path: "/api/social/discussions/{id}/unpin" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialDiscussionsResolveInput {
  id: string;
}
export type PostApiSocialDiscussionsResolveOutput =
  Types.LearningExperienceSocialServicesCourseDiscussion;
export const postApiSocialDiscussionsResolveEndpoint = {
  operationId: "postApiSocialDiscussionsResolve" as const,
  method: "POST" as const,
  path: "/api/social/discussions/{id}/resolve" as const,
  tags: ["LearningExperienceSocialDiscussions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get my Economy wallet
 */
export type GetEconomyWalletInput = void;
export type GetEconomyWalletOutput = Types.EconomyContractsEconomyWalletSummary;
export const getEconomyWalletEndpoint = {
  operationId: "getEconomyWallet" as const,
  method: "GET" as const,
  path: "/api/v1/economy/wallet" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * List my Economy wallet transactions
 */
export interface GetEconomyWalletTransactionsInput {
  query?: {
    take?: number;
  };
}
export type GetEconomyWalletTransactionsOutput =
  Array<Types.EconomyContractsEconomyWalletTransaction>;
export const getEconomyWalletTransactionsEndpoint = {
  operationId: "getEconomyWalletTransactions" as const,
  method: "GET" as const,
  path: "/api/v1/economy/wallet/transactions" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * Convert my confirmed HardCoin balance into SoftCoin
 */
export interface PostEconomyConversionsHardToSoftInput {
  body?: Types.EconomyCommandsConvertMyHardToSoftInput;
}
export type PostEconomyConversionsHardToSoftOutput =
  Types.EconomyFundingSelfServiceHardToSoftConversionReceipt;
export const postEconomyConversionsHardToSoftEndpoint = {
  operationId: "postEconomyConversionsHardToSoft" as const,
  method: "POST" as const,
  path: "/api/v1/economy/conversions/hard-to-soft" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * Get my Economy capability readiness
 */
export type GetEconomyCapabilitiesInput = void;
export type GetEconomyCapabilitiesOutput =
  Array<Types.APIControllersEconomySelfServiceCapability>;
export const getEconomyCapabilitiesEndpoint = {
  operationId: "getEconomyCapabilities" as const,
  method: "GET" as const,
  path: "/api/v1/economy/capabilities" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * List my payout operations
 */
export interface GetEconomyPayoutsForGetEconomyPayoutsInput {
  query?: {
    take?: number;
  };
}
export type GetEconomyPayoutsForGetEconomyPayoutsOutput =
  Array<Types.EconomyPayoutsQueriesEconomyPayoutOperation>;
export const getEconomyPayoutsForGetEconomyPayoutsEndpoint = {
  operationId: "getEconomyPayoutsForGetEconomyPayouts" as const,
  method: "GET" as const,
  path: "/api/v1/economy/payouts" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * List my payout requests
 */
export interface GetEconomyPayoutRequestsInput {
  query?: {
    take?: number;
  };
}
export type GetEconomyPayoutRequestsOutput =
  Array<Types.EconomyPayoutsQueriesEconomyPayoutInput>;
export const getEconomyPayoutRequestsEndpoint = {
  operationId: "getEconomyPayoutRequests" as const,
  method: "GET" as const,
  path: "/api/v1/economy/payout-requests" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * Submit my payout request
 *
 * Records a withdrawal request only. It does not reserve or transfer value until KYC, risk, provider, and FIFO eligibility checks pass.
 */
export interface PostEconomyPayoutRequestsInput {
  body?: Types.EconomyPayoutsCommandsCreateMyPayoutRequestInput;
}
export type PostEconomyPayoutRequestsOutput =
  Types.EconomyPayoutsQueriesEconomyPayoutInput;
export const postEconomyPayoutRequestsEndpoint = {
  operationId: "postEconomyPayoutRequests" as const,
  method: "POST" as const,
  path: "/api/v1/economy/payout-requests" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel my pending payout request
 */
export interface PostEconomyPayoutRequestsCancelInput {
  requestId: string;
}
export type PostEconomyPayoutRequestsCancelOutput =
  Types.EconomyPayoutsQueriesEconomyPayoutInput;
export const postEconomyPayoutRequestsCancelEndpoint = {
  operationId: "postEconomyPayoutRequestsCancel" as const,
  method: "POST" as const,
  path: "/api/v1/economy/payout-requests/{requestId}/cancel" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

/**
 * Get my payout operation
 */
export interface GetEconomyPayoutsForGetEconomyPayoutsByOperationIdInput {
  operationId: string;
}
export type GetEconomyPayoutsForGetEconomyPayoutsByOperationIdOutput =
  Types.EconomyPayoutsQueriesEconomyPayoutOperation;
export const getEconomyPayoutsForGetEconomyPayoutsByOperationIdEndpoint = {
  operationId: "getEconomyPayoutsForGetEconomyPayoutsByOperationId" as const,
  method: "GET" as const,
  path: "/api/v1/economy/payouts/{operationId}" as const,
  tags: ["Economy"] as const,
  requiresAuth: true,
} as const;

export interface GetApiLearningEnrollmentsInput {
  id: string;
}
export type GetApiLearningEnrollmentsOutput = void;
export const getApiLearningEnrollmentsEndpoint = {
  operationId: "getApiLearningEnrollments" as const,
  method: "GET" as const,
  path: "/api/learning/enrollments/{id}" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface GetApiLearningEnrollmentsUsersInput {
  userId: string;
  query?: {
    status?: Types.LearningEnrollmentsEnrollmentStatus;
  };
}
export type GetApiLearningEnrollmentsUsersOutput =
  Array<Types.LearningEnrollmentsEnrollment>;
export const getApiLearningEnrollmentsUsersEndpoint = {
  operationId: "getApiLearningEnrollmentsUsers" as const,
  method: "GET" as const,
  path: "/api/learning/enrollments/users/{userId}" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface GetApiLearningEnrollmentsCoursesInput {
  courseId: string;
  query?: {
    status?: Types.LearningEnrollmentsEnrollmentStatus;
  };
}
export type GetApiLearningEnrollmentsCoursesOutput =
  Array<Types.LearningEnrollmentsEnrollment>;
export const getApiLearningEnrollmentsCoursesEndpoint = {
  operationId: "getApiLearningEnrollmentsCourses" as const,
  method: "GET" as const,
  path: "/api/learning/enrollments/courses/{courseId}" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface PostApiLearningEnrollmentsInput {
  body?: Types.LearningEnrollmentsEnrollUserInput;
}
export type PostApiLearningEnrollmentsOutput =
  Types.LearningEnrollmentsEnrollment;
export const postApiLearningEnrollmentsEndpoint = {
  operationId: "postApiLearningEnrollments" as const,
  method: "POST" as const,
  path: "/api/learning/enrollments" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface PatchApiLearningEnrollmentsProgressInput {
  id: string;
  body?: Types.LearningEnrollmentsUpdateEnrollmentProgressInput;
}
export type PatchApiLearningEnrollmentsProgressOutput = void;
export const patchApiLearningEnrollmentsProgressEndpoint = {
  operationId: "patchApiLearningEnrollmentsProgress" as const,
  method: "PATCH" as const,
  path: "/api/learning/enrollments/{id}/progress" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface PostApiLearningEnrollmentsStatusInput {
  id: string;
  status: Types.LearningEnrollmentsEnrollmentStatus;
}
export type PostApiLearningEnrollmentsStatusOutput = void;
export const postApiLearningEnrollmentsStatusEndpoint = {
  operationId: "postApiLearningEnrollmentsStatus" as const,
  method: "POST" as const,
  path: "/api/learning/enrollments/{id}/status/{status}" as const,
  tags: ["LearningEnrollments"] as const,
  requiresAuth: true,
} as const;

export interface GetEntitlementsInput {
  query?: {
    status?: string;
    days?: number;
  };
}
export type GetEntitlementsOutput =
  Array<Types.CommerceProductsEntitlementInfo>;
export const getEntitlementsEndpoint = {
  operationId: "getEntitlements" as const,
  method: "GET" as const,
  path: "/v1/entitlements" as const,
  tags: ["CommerceProductsEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface PostEntitlementsInput {
  body?: Types.CommerceProductsGrantEntitlementInput;
}
export type PostEntitlementsOutput = Types.CommerceProductsEntitlementInfo;
export const postEntitlementsEndpoint = {
  operationId: "postEntitlements" as const,
  method: "POST" as const,
  path: "/v1/entitlements" as const,
  tags: ["CommerceProductsEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface GetEntitlementsCheckInput {
  query?: {
    productId?: string;
  };
}
export type GetEntitlementsCheckOutput =
  Types.CommerceProductsEntitlementCheckResult;
export const getEntitlementsCheckEndpoint = {
  operationId: "getEntitlementsCheck" as const,
  method: "GET" as const,
  path: "/v1/entitlements/:check" as const,
  tags: ["CommerceProductsEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface PostEntitlementsCheckBatchInput {
  body?: Types.CommerceProductsCheckMultipleAccessInput;
}
export type PostEntitlementsCheckBatchOutput = Record<string, boolean>;
export const postEntitlementsCheckBatchEndpoint = {
  operationId: "postEntitlementsCheckBatch" as const,
  method: "POST" as const,
  path: "/v1/entitlements/:check-batch" as const,
  tags: ["CommerceProductsEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface PostEntitlementsRevokeInput {
  entitlementId: string;
  body?: Types.CommerceProductsRevokeEntitlementInput;
}
export type PostEntitlementsRevokeOutput = void;
export const postEntitlementsRevokeEndpoint = {
  operationId: "postEntitlementsRevoke" as const,
  method: "POST" as const,
  path: "/v1/entitlements/{entitlementId}:revoke" as const,
  tags: ["CommerceProductsEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesEvaluateInput {
  body?: Types.FeaturesFeatureEvaluationInput;
}
export type PostFeaturesEvaluateOutput = void;
export const postFeaturesEvaluateEndpoint = {
  operationId: "postFeaturesEvaluate" as const,
  method: "POST" as const,
  path: "/v1/features/:evaluate" as const,
  tags: ["FeaturesFlags"] as const,
  requiresAuth: true,
} as const;

export interface GetFeaturesValueInput {
  key: string;
  query?: {
    defaultValue?: boolean;
    userId?: string;
    tenantId?: string;
    environment?: string;
  };
}
export type GetFeaturesValueOutput = void;
export const getFeaturesValueEndpoint = {
  operationId: "getFeaturesValue" as const,
  method: "GET" as const,
  path: "/v1/features/{key}/value" as const,
  tags: ["FeaturesFlags"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesEvaluateBulkInput {
  body?: Types.FeaturesBulkEvaluationInput;
}
export type PostFeaturesEvaluateBulkOutput = void;
export const postFeaturesEvaluateBulkEndpoint = {
  operationId: "postFeaturesEvaluateBulk" as const,
  method: "POST" as const,
  path: "/v1/features/:evaluate-bulk" as const,
  tags: ["FeaturesFlags"] as const,
  requiresAuth: true,
} as const;

export interface GetFeaturesEnabledInput {
  query?: {
    userId?: string;
    tenantId?: string;
    environment?: string;
  };
}
export type GetFeaturesEnabledOutput = void;
export const getFeaturesEnabledEndpoint = {
  operationId: "getFeaturesEnabled" as const,
  method: "GET" as const,
  path: "/v1/features/enabled" as const,
  tags: ["FeaturesFlags"] as const,
  requiresAuth: true,
} as const;

export interface GetFeaturesInput {
  query?: {
    isEnabled?: boolean;
  };
}
export type GetFeaturesOutput = Array<Types.FeaturesFeatureFlag>;
export const getFeaturesEndpoint = {
  operationId: "getFeatures" as const,
  method: "GET" as const,
  path: "/v1/features" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesInput {
  body?: Types.FeaturesCreateFeatureInput;
}
export type PostFeaturesOutput = Record<string, unknown>;
export const postFeaturesEndpoint = {
  operationId: "postFeatures" as const,
  method: "POST" as const,
  path: "/v1/features" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface GetFeatureByKeyInput {
  key: string;
}
export type GetFeatureByKeyOutput = void;
export const getFeatureByKeyEndpoint = {
  operationId: "getFeatureByKey" as const,
  method: "GET" as const,
  path: "/v1/features/{key}" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface PutFeaturesInput {
  key: string;
  body?: Types.FeaturesUpdateFeatureInput;
}
export type PutFeaturesOutput = void;
export const putFeaturesEndpoint = {
  operationId: "putFeatures" as const,
  method: "PUT" as const,
  path: "/v1/features/{key}" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface DeleteFeaturesInput {
  key: string;
}
export type DeleteFeaturesOutput = void;
export const deleteFeaturesEndpoint = {
  operationId: "deleteFeatures" as const,
  method: "DELETE" as const,
  path: "/v1/features/{key}" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface GetFeaturesExistsInput {
  key: string;
  query?: {
    environment?: string;
  };
}
export type GetFeaturesExistsOutput = boolean;
export const getFeaturesExistsEndpoint = {
  operationId: "getFeaturesExists" as const,
  method: "GET" as const,
  path: "/v1/features/{key}/exists" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesEnableInput {
  id: string;
}
export type PostFeaturesEnableOutput = void;
export const postFeaturesEnableEndpoint = {
  operationId: "postFeaturesEnable" as const,
  method: "POST" as const,
  path: "/v1/features/{id}:enable" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesDisableInput {
  id: string;
}
export type PostFeaturesDisableOutput = void;
export const postFeaturesDisableEndpoint = {
  operationId: "postFeaturesDisable" as const,
  method: "POST" as const,
  path: "/v1/features/{id}:disable" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface PostFeaturesToggleInput {
  id: string;
  body?: Types.FeaturesToggleFeatureInput;
}
export type PostFeaturesToggleOutput = void;
export const postFeaturesToggleEndpoint = {
  operationId: "postFeaturesToggle" as const,
  method: "POST" as const,
  path: "/v1/features/{id}:toggle" as const,
  tags: ["Features"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialFeedMeInput {
  query?: {
    skip?: number;
    take?: number;
    filterByType?: Types.LearningExperienceSocialFeedItemType;
  };
}
export type GetApiSocialFeedMeOutput =
  Array<Types.LearningExperienceSocialServicesPersonalizedFeedItem>;
export const getApiSocialFeedMeEndpoint = {
  operationId: "getApiSocialFeedMe" as const,
  method: "GET" as const,
  path: "/api/social/feed/me" as const,
  tags: ["LearningExperienceSocialFeed"] as const,
  requiresAuth: true,
} as const;

export type PostApiSocialFeedMeGenerateInput = void;
export type PostApiSocialFeedMeGenerateOutput = number;
export const postApiSocialFeedMeGenerateEndpoint = {
  operationId: "postApiSocialFeedMeGenerate" as const,
  method: "POST" as const,
  path: "/api/social/feed/me/generate" as const,
  tags: ["LearningExperienceSocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialFeedViewedInput {
  id: string;
}
export type PostApiSocialFeedViewedOutput =
  Types.LearningExperienceSocialServicesPersonalizedFeedItem;
export const postApiSocialFeedViewedEndpoint = {
  operationId: "postApiSocialFeedViewed" as const,
  method: "POST" as const,
  path: "/api/social/feed/{id}/viewed" as const,
  tags: ["LearningExperienceSocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialFeedDismissInput {
  id: string;
}
export type PostApiSocialFeedDismissOutput =
  Types.LearningExperienceSocialServicesPersonalizedFeedItem;
export const postApiSocialFeedDismissEndpoint = {
  operationId: "postApiSocialFeedDismiss" as const,
  method: "POST" as const,
  path: "/api/social/feed/{id}/dismiss" as const,
  tags: ["LearningExperienceSocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialFeedUsersInput {
  userId: string;
  query?: {
    skip?: number;
    take?: number;
    includeRead?: boolean;
  };
}
export type GetApiSocialFeedUsersOutput = Array<Types.SocialFeedFeedItem>;
export const getApiSocialFeedUsersEndpoint = {
  operationId: "getApiSocialFeedUsers" as const,
  method: "GET" as const,
  path: "/api/social/feed/users/{userId}" as const,
  tags: ["SocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialFeedInput {
  body?: Types.SocialFeedAddFeedItemInput;
}
export type PostApiSocialFeedOutput = Types.SocialFeedFeedItem;
export const postApiSocialFeedEndpoint = {
  operationId: "postApiSocialFeed" as const,
  method: "POST" as const,
  path: "/api/social/feed" as const,
  tags: ["SocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialFeedReadInput {
  id: string;
}
export type PostApiSocialFeedReadOutput = void;
export const postApiSocialFeedReadEndpoint = {
  operationId: "postApiSocialFeedRead" as const,
  method: "POST" as const,
  path: "/api/social/feed/{id}/read" as const,
  tags: ["SocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialFeedHideInput {
  id: string;
}
export type PostApiSocialFeedHideOutput = void;
export const postApiSocialFeedHideEndpoint = {
  operationId: "postApiSocialFeedHide" as const,
  method: "POST" as const,
  path: "/api/social/feed/{id}/hide" as const,
  tags: ["SocialFeed"] as const,
  requiresAuth: true,
} as const;

export interface GetApiGameJamsForGetApiGameJamsInput {
  query?: {
    status?: Types.GameJamsJamStatus;
    skip?: number;
    take?: number;
  };
}
export type GetApiGameJamsForGetApiGameJamsOutput = Array<Types.GameJamsJamDto>;
export const getApiGameJamsForGetApiGameJamsEndpoint = {
  operationId: "getApiGameJamsForGetApiGameJams" as const,
  method: "GET" as const,
  path: "/api/game-jams" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface PostApiGameJamsInput {
  body?: Types.GameJamsCreateJamInput;
}
export type PostApiGameJamsOutput = Types.GameJamsJamDto;
export const postApiGameJamsEndpoint = {
  operationId: "postApiGameJams" as const,
  method: "POST" as const,
  path: "/api/game-jams" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface GetApiGameJamsForGetApiGameJamsByIdInput {
  id: string;
}
export type GetApiGameJamsForGetApiGameJamsByIdOutput = void;
export const getApiGameJamsForGetApiGameJamsByIdEndpoint = {
  operationId: "getApiGameJamsForGetApiGameJamsById" as const,
  method: "GET" as const,
  path: "/api/game-jams/{id}" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface PostApiGameJamsStatusInput {
  id: string;
  status: Types.GameJamsJamStatus;
}
export type PostApiGameJamsStatusOutput = void;
export const postApiGameJamsStatusEndpoint = {
  operationId: "postApiGameJamsStatus" as const,
  method: "POST" as const,
  path: "/api/game-jams/{id}/status/{status}" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface GetApiGameJamsSubmissionsInput {
  id: string;
}
export type GetApiGameJamsSubmissionsOutput =
  Array<Types.GameJamsJamSubmission>;
export const getApiGameJamsSubmissionsEndpoint = {
  operationId: "getApiGameJamsSubmissions" as const,
  method: "GET" as const,
  path: "/api/game-jams/{id}/submissions" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface PostApiGameJamsSubmissionsInput {
  id: string;
  body?: Types.GameJamsSubmitJamEntryInput;
}
export type PostApiGameJamsSubmissionsOutput = Types.GameJamsJamSubmission;
export const postApiGameJamsSubmissionsEndpoint = {
  operationId: "postApiGameJamsSubmissions" as const,
  method: "POST" as const,
  path: "/api/game-jams/{id}/submissions" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface GetApiGameJamsCriteriaInput {
  id: string;
}
export type GetApiGameJamsCriteriaOutput = Array<Types.GameJamsJamCriteria>;
export const getApiGameJamsCriteriaEndpoint = {
  operationId: "getApiGameJamsCriteria" as const,
  method: "GET" as const,
  path: "/api/game-jams/{id}/criteria" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface PostApiGameJamsCriteriaInput {
  id: string;
  body?: Types.GameJamsAddJamCriteriaInput;
}
export type PostApiGameJamsCriteriaOutput = Types.GameJamsJamCriteria;
export const postApiGameJamsCriteriaEndpoint = {
  operationId: "postApiGameJamsCriteria" as const,
  method: "POST" as const,
  path: "/api/game-jams/{id}/criteria" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

export interface PostApiGameJamsSubmissionsScoresInput {
  submissionId: string;
  body?: Types.GameJamsScoreJamSubmissionInput;
}
export type PostApiGameJamsSubmissionsScoresOutput = Types.GameJamsJamScoreDto;
export const postApiGameJamsSubmissionsScoresEndpoint = {
  operationId: "postApiGameJamsSubmissionsScores" as const,
  method: "POST" as const,
  path: "/api/game-jams/submissions/{submissionId}/scores" as const,
  tags: ["GameJams"] as const,
  requiresAuth: true,
} as const;

/**
 * Application information endpoint
 *
 * Provides application version, build details, and runtime information for debugging and deployment monitoring.
 */
export type GetInfoInput = void;
export type GetInfoOutput = Types.APIControllersApplicationInfoOutput;
export const getInfoEndpoint = {
  operationId: "getInfo" as const,
  method: "GET" as const,
  path: "/info" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Comprehensive application health check
 *
 * Performs a comprehensive health check of all registered services and dependencies. Returns detailed status information for monitoring systems, load balancers, and orchestration platforms.
 */
export type GetHealthInput = void;
export type GetHealthOutput = Types.APIControllersHealthinessOutput;
export const getHealthEndpoint = {
  operationId: "getHealth" as const,
  method: "GET" as const,
  path: "/health" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Comprehensive application health check
 *
 * Performs a comprehensive health check of all registered services and dependencies. Returns detailed status information for monitoring systems, load balancers, and orchestration platforms.
 */
export type GetApiHealthInput = void;
export type GetApiHealthOutput = Types.APIControllersHealthinessOutput;
export const getApiHealthEndpoint = {
  operationId: "getApiHealth" as const,
  method: "GET" as const,
  path: "/api/health" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Readiness probe for traffic routing decisions
 *
 * Kubernetes-style readiness probe that determines whether the application is ready to serve traffic. Checks all dependencies and services required for proper request handling.
 */
export type GetReadyInput = void;
export type GetReadyOutput = Types.APIControllersReadinessOutput;
export const getReadyEndpoint = {
  operationId: "getReady" as const,
  method: "GET" as const,
  path: "/ready" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Readiness probe for traffic routing decisions
 *
 * Kubernetes-style readiness probe that determines whether the application is ready to serve traffic. Checks all dependencies and services required for proper request handling.
 */
export type GetApiReadyInput = void;
export type GetApiReadyOutput = Types.APIControllersReadinessOutput;
export const getApiReadyEndpoint = {
  operationId: "getApiReady" as const,
  method: "GET" as const,
  path: "/api/ready" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Liveness probe for container restart decisions
 *
 * Kubernetes-style liveness probe that indicates whether the application process is running correctly. Used by orchestration platforms to determine if containers should be restarted.
 */
export type GetLiveInput = void;
export type GetLiveOutput = Types.APIControllersLivenessOutput;
export const getLiveEndpoint = {
  operationId: "getLive" as const,
  method: "GET" as const,
  path: "/live" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Liveness probe for container restart decisions
 *
 * Kubernetes-style liveness probe that indicates whether the application process is running correctly. Used by orchestration platforms to determine if containers should be restarted.
 */
export type GetApiLiveInput = void;
export type GetApiLiveOutput = Types.APIControllersLivenessOutput;
export const getApiLiveEndpoint = {
  operationId: "getApiLive" as const,
  method: "GET" as const,
  path: "/api/live" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Detailed dependency health check
 *
 * Provides comprehensive health status of all external dependencies including databases, APIs, caches, and message queues.
 */
export type GetHealthDependenciesInput = void;
export type GetHealthDependenciesOutput =
  Types.APIControllersDependencyHealthOutput;
export const getHealthDependenciesEndpoint = {
  operationId: "getHealthDependencies" as const,
  method: "GET" as const,
  path: "/health/dependencies" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Detailed dependency health check
 *
 * Provides comprehensive health status of all external dependencies including databases, APIs, caches, and message queues.
 */
export type GetApiHealthDependenciesInput = void;
export type GetApiHealthDependenciesOutput =
  Types.APIControllersDependencyHealthOutput;
export const getApiHealthDependenciesEndpoint = {
  operationId: "getApiHealthDependencies" as const,
  method: "GET" as const,
  path: "/api/health/dependencies" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

/**
 * Prometheus metrics endpoint
 *
 * Exposes application metrics in Prometheus text format for monitoring, alerting, and observability dashboards.
 */
export type GetMetricsInput = void;
export type GetMetricsOutput = void;
export const getMetricsEndpoint = {
  operationId: "getMetrics" as const,
  method: "GET" as const,
  path: "/metrics" as const,
  tags: ["Health"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadForGetLaunchPadInput {
  query?: {
    status?: Types.LaunchPadLaunchPlanStatus;
  };
}
export type GetLaunchPadForGetLaunchPadOutput =
  Array<Types.LaunchPadLaunchPlan>;
export const getLaunchPadForGetLaunchPadEndpoint = {
  operationId: "getLaunchPadForGetLaunchPad" as const,
  method: "GET" as const,
  path: "/v1/launch-pad" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadInput {
  body?: Types.LaunchPadCreateLaunchPlanInput;
}
export type PostLaunchPadOutput = void;
export const postLaunchPadEndpoint = {
  operationId: "postLaunchPad" as const,
  method: "POST" as const,
  path: "/v1/launch-pad" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadForGetLaunchPadByIdInput {
  id: string;
}
export type GetLaunchPadForGetLaunchPadByIdOutput = Types.LaunchPadLaunchPlan;
export const getLaunchPadForGetLaunchPadByIdEndpoint = {
  operationId: "getLaunchPadForGetLaunchPadById" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/{id}" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadProjectsInput {
  projectId: string;
}
export type GetLaunchPadProjectsOutput = Types.LaunchPadLaunchPlan;
export const getLaunchPadProjectsEndpoint = {
  operationId: "getLaunchPadProjects" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/projects/{projectId}" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadChecklistCompleteInput {
  id: string;
  itemId: string;
}
export type PostLaunchPadChecklistCompleteOutput = Types.LaunchPadLaunchPlan;
export const postLaunchPadChecklistCompleteEndpoint = {
  operationId: "postLaunchPadChecklistComplete" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/{id}/checklist/{itemId}:complete" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadPublishInput {
  id: string;
}
export type PostLaunchPadPublishOutput = Types.LaunchPadLaunchPlan;
export const postLaunchPadPublishEndpoint = {
  operationId: "postLaunchPadPublish" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/{id}:publish" as const,
  tags: ["LaunchPad"] as const,
  requiresAuth: true,
} as const;

export type GetLaunchPadEventsPublicForGetLaunchPadEventsPublicInput = void;
export type GetLaunchPadEventsPublicForGetLaunchPadEventsPublicOutput =
  Array<Types.LaunchPadLaunchPadEventProjection>;
export const getLaunchPadEventsPublicForGetLaunchPadEventsPublicEndpoint = {
  operationId: "getLaunchPadEventsPublicForGetLaunchPadEventsPublic" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/public" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadEventsPublicForGetLaunchPadEventsPublicByIdInput {
  id: string;
}
export type GetLaunchPadEventsPublicForGetLaunchPadEventsPublicByIdOutput =
  Types.LaunchPadLaunchPadEventDetailProjection;
export const getLaunchPadEventsPublicForGetLaunchPadEventsPublicByIdEndpoint = {
  operationId:
    "getLaunchPadEventsPublicForGetLaunchPadEventsPublicById" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/public/{id}" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export type GetLaunchPadEventsManagementForGetLaunchPadEventsManagementInput =
  void;
export type GetLaunchPadEventsManagementForGetLaunchPadEventsManagementOutput =
  Array<Types.LaunchPadLaunchPadEventProjection>;
export const getLaunchPadEventsManagementForGetLaunchPadEventsManagementEndpoint =
  {
    operationId:
      "getLaunchPadEventsManagementForGetLaunchPadEventsManagement" as const,
    method: "GET" as const,
    path: "/v1/launch-pad/events/management" as const,
    tags: ["LaunchPadEvents"] as const,
    requiresAuth: true,
  } as const;

export interface GetLaunchPadEventsManagementForGetLaunchPadEventsByIdManagementInput {
  id: string;
}
export type GetLaunchPadEventsManagementForGetLaunchPadEventsByIdManagementOutput =
  Types.LaunchPadLaunchPadEventDetailProjection;
export const getLaunchPadEventsManagementForGetLaunchPadEventsByIdManagementEndpoint =
  {
    operationId:
      "getLaunchPadEventsManagementForGetLaunchPadEventsByIdManagement" as const,
    method: "GET" as const,
    path: "/v1/launch-pad/events/{id}/management" as const,
    tags: ["LaunchPadEvents"] as const,
    requiresAuth: true,
  } as const;

export interface PostLaunchPadEventsInput {
  body?: Types.LaunchPadCreateLaunchPadEventInput;
}
export type PostLaunchPadEventsOutput = Types.LaunchPadLaunchPadEventProjection;
export const postLaunchPadEventsEndpoint = {
  operationId: "postLaunchPadEvents" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsTransitionInput {
  id: string;
  body?: Types.LaunchPadTransitionLaunchPadEventInput;
}
export type PostLaunchPadEventsTransitionOutput =
  Types.LaunchPadLaunchPadEventProjection;
export const postLaunchPadEventsTransitionEndpoint = {
  operationId: "postLaunchPadEventsTransition" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/{id}:transition" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutLaunchPadEventsInput {
  id: string;
  body?: Types.LaunchPadUpdateLaunchPadEventInput;
}
export type PutLaunchPadEventsOutput = Types.LaunchPadLaunchPadEventProjection;
export const putLaunchPadEventsEndpoint = {
  operationId: "putLaunchPadEvents" as const,
  method: "PUT" as const,
  path: "/v1/launch-pad/events/{id}" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsSlotsInput {
  eventId: string;
  body?: Types.LaunchPadCreateLaunchPadSlotInput;
}
export type PostLaunchPadEventsSlotsOutput =
  Types.LaunchPadLaunchPadSlotProjection;
export const postLaunchPadEventsSlotsEndpoint = {
  operationId: "postLaunchPadEventsSlots" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/{eventId}/slots" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutLaunchPadEventsSlotsInput {
  slotId: string;
  body?: Types.LaunchPadCreateLaunchPadSlotInput;
}
export type PutLaunchPadEventsSlotsOutput =
  Types.LaunchPadLaunchPadSlotProjection;
export const putLaunchPadEventsSlotsEndpoint = {
  operationId: "putLaunchPadEventsSlots" as const,
  method: "PUT" as const,
  path: "/v1/launch-pad/events/slots/{slotId}" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface DeleteLaunchPadEventsSlotsInput {
  slotId: string;
}
export type DeleteLaunchPadEventsSlotsOutput = void;
export const deleteLaunchPadEventsSlotsEndpoint = {
  operationId: "deleteLaunchPadEventsSlots" as const,
  method: "DELETE" as const,
  path: "/v1/launch-pad/events/slots/{slotId}" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsApplicationsInput {
  eventId: string;
  body?: Types.LaunchPadSubmitLaunchPadApplicationInput;
}
export type PostLaunchPadEventsApplicationsOutput =
  Types.LaunchPadLaunchPadApplicationProjection;
export const postLaunchPadEventsApplicationsEndpoint = {
  operationId: "postLaunchPadEventsApplications" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/{eventId}/applications" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export type GetLaunchPadEventsApplicationsMeInput = void;
export type GetLaunchPadEventsApplicationsMeOutput =
  Array<Types.LaunchPadLaunchPadApplicationProjection>;
export const getLaunchPadEventsApplicationsMeEndpoint = {
  operationId: "getLaunchPadEventsApplicationsMe" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/applications/me" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutLaunchPadEventsApplicationsInput {
  applicationId: string;
  body?: Types.LaunchPadUpdateLaunchPadApplicationInput;
}
export type PutLaunchPadEventsApplicationsOutput =
  Types.LaunchPadLaunchPadApplicationProjection;
export const putLaunchPadEventsApplicationsEndpoint = {
  operationId: "putLaunchPadEventsApplications" as const,
  method: "PUT" as const,
  path: "/v1/launch-pad/events/applications/{applicationId}" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsApplicationsWithdrawInput {
  applicationId: string;
}
export type PostLaunchPadEventsApplicationsWithdrawOutput =
  Types.LaunchPadLaunchPadApplicationProjection;
export const postLaunchPadEventsApplicationsWithdrawEndpoint = {
  operationId: "postLaunchPadEventsApplicationsWithdraw" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/applications/{applicationId}:withdraw" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadEventsApplicationsManagementInput {
  eventId: string;
}
export type GetLaunchPadEventsApplicationsManagementOutput =
  Array<Types.LaunchPadLaunchPadApplicationProjection>;
export const getLaunchPadEventsApplicationsManagementEndpoint = {
  operationId: "getLaunchPadEventsApplicationsManagement" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/{eventId}/applications/management" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsApplicationsReviewInput {
  applicationId: string;
  body?: Types.LaunchPadReviewLaunchPadApplicationInput;
}
export type PostLaunchPadEventsApplicationsReviewOutput =
  Types.LaunchPadLaunchPadApplicationProjection;
export const postLaunchPadEventsApplicationsReviewEndpoint = {
  operationId: "postLaunchPadEventsApplicationsReview" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/applications/{applicationId}:review" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsSlotsRegistrationsInput {
  slotId: string;
}
export type PostLaunchPadEventsSlotsRegistrationsOutput =
  Types.LaunchPadLaunchPadRegistrationProjection;
export const postLaunchPadEventsSlotsRegistrationsEndpoint = {
  operationId: "postLaunchPadEventsSlotsRegistrations" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/slots/{slotId}/registrations" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export type GetLaunchPadEventsRegistrationsMeInput = void;
export type GetLaunchPadEventsRegistrationsMeOutput =
  Array<Types.LaunchPadLaunchPadRegistrationProjection>;
export const getLaunchPadEventsRegistrationsMeEndpoint = {
  operationId: "getLaunchPadEventsRegistrationsMe" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/registrations/me" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsRegistrationsCancelInput {
  registrationId: string;
}
export type PostLaunchPadEventsRegistrationsCancelOutput =
  Types.LaunchPadLaunchPadRegistrationProjection;
export const postLaunchPadEventsRegistrationsCancelEndpoint = {
  operationId: "postLaunchPadEventsRegistrationsCancel" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/registrations/{registrationId}:cancel" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostLaunchPadEventsRegistrationsTransitionInput {
  registrationId: string;
  body?: Types.LaunchPadTransitionLaunchPadRegistrationInput;
}
export type PostLaunchPadEventsRegistrationsTransitionOutput =
  Types.LaunchPadLaunchPadRegistrationProjection;
export const postLaunchPadEventsRegistrationsTransitionEndpoint = {
  operationId: "postLaunchPadEventsRegistrationsTransition" as const,
  method: "POST" as const,
  path: "/v1/launch-pad/events/registrations/{registrationId}:transition" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetLaunchPadEventsRegistrationsManagementInput {
  eventId: string;
}
export type GetLaunchPadEventsRegistrationsManagementOutput =
  Array<Types.LaunchPadLaunchPadRegistrationProjection>;
export const getLaunchPadEventsRegistrationsManagementEndpoint = {
  operationId: "getLaunchPadEventsRegistrationsManagement" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/{eventId}/registrations/management" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export type GetLaunchPadEventsAnalyticsInput = void;
export type GetLaunchPadEventsAnalyticsOutput =
  Types.LaunchPadLaunchPadAnalyticsProjection;
export const getLaunchPadEventsAnalyticsEndpoint = {
  operationId: "getLaunchPadEventsAnalytics" as const,
  method: "GET" as const,
  path: "/v1/launch-pad/events/analytics" as const,
  tags: ["LaunchPadEvents"] as const,
  requiresAuth: true,
} as const;

export type GetLearningMeDashboardInput = void;
export type GetLearningMeDashboardOutput =
  Types.LearningWorkspacesLearnerDashboard;
export const getLearningMeDashboardEndpoint = {
  operationId: "getLearningMeDashboard" as const,
  method: "GET" as const,
  path: "/v1/learning/me/dashboard" as const,
  tags: ["LearningWorkspacesLearnerWorkspace"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningCoursesWorkspaceInput {
  courseId: string;
}
export type GetLearningCoursesWorkspaceOutput =
  Types.LearningWorkspacesLearnerCourseWorkspace;
export const getLearningCoursesWorkspaceEndpoint = {
  operationId: "getLearningCoursesWorkspace" as const,
  method: "GET" as const,
  path: "/v1/learning/courses/{courseId}/workspace" as const,
  tags: ["LearningWorkspacesLearnerWorkspace"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningMeSearchInput {
  query?: {
    q?: string;
    take?: number;
  };
}
export type GetLearningMeSearchOutput =
  Array<Types.LearningWorkspacesLearnerSearchResult>;
export const getLearningMeSearchEndpoint = {
  operationId: "getLearningMeSearch" as const,
  method: "GET" as const,
  path: "/v1/learning/me/search" as const,
  tags: ["LearningWorkspacesLearnerWorkspace"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsForGetLearningPathsInput {
  query?: {
    tenantId?: string;
    difficulty?: Types.LearningExperienceLearningPathsLearningPathDifficulty;
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsForGetLearningPathsOutput =
  Array<Types.LearningExperienceLearningPathsLearningPath>;
export const getLearningPathsForGetLearningPathsEndpoint = {
  operationId: "getLearningPathsForGetLearningPaths" as const,
  method: "GET" as const,
  path: "/v1/learning-paths" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsInput {
  query?: {
    creatorId?: string;
    tenantId?: string;
  };
  body?: Types.LearningExperienceLearningPathsCreateLearningPath;
}
export type PostLearningPathsOutput =
  Types.LearningExperienceLearningPathsLearningPath;
export const postLearningPathsEndpoint = {
  operationId: "postLearningPaths" as const,
  method: "POST" as const,
  path: "/v1/learning-paths" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsSearchInput {
  query?: {
    q?: string;
    tenantId?: string;
    difficulty?: Types.LearningExperienceLearningPathsLearningPathDifficulty;
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsSearchOutput =
  Array<Types.LearningExperienceLearningPathsLearningPath>;
export const getLearningPathsSearchEndpoint = {
  operationId: "getLearningPathsSearch" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/search" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsFeaturedInput {
  query?: {
    tenantId?: string;
    take?: number;
  };
}
export type GetLearningPathsFeaturedOutput =
  Array<Types.LearningExperienceLearningPathsLearningPath>;
export const getLearningPathsFeaturedEndpoint = {
  operationId: "getLearningPathsFeatured" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/featured" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsPopularInput {
  query?: {
    tenantId?: string;
    daysBack?: number;
    take?: number;
  };
}
export type GetLearningPathsPopularOutput =
  Array<Types.LearningExperienceLearningPathsLearningPath>;
export const getLearningPathsPopularEndpoint = {
  operationId: "getLearningPathsPopular" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/popular" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsSlugInput {
  slug: string;
  query?: {
    tenantId?: string;
  };
}
export type GetLearningPathsSlugOutput =
  Types.LearningExperienceLearningPathsLearningPathDetail;
export const getLearningPathsSlugEndpoint = {
  operationId: "getLearningPathsSlug" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/slug/{slug}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsForGetLearningPathsByIdInput {
  id: string;
}
export type GetLearningPathsForGetLearningPathsByIdOutput =
  Types.LearningExperienceLearningPathsLearningPathDetail;
export const getLearningPathsForGetLearningPathsByIdEndpoint = {
  operationId: "getLearningPathsForGetLearningPathsById" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/{id}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PutLearningPathsInput {
  id: string;
  body?: Types.LearningExperienceLearningPathsUpdateLearningPath;
}
export type PutLearningPathsOutput =
  Types.LearningExperienceLearningPathsLearningPath;
export const putLearningPathsEndpoint = {
  operationId: "putLearningPaths" as const,
  method: "PUT" as const,
  path: "/v1/learning-paths/{id}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface DeleteLearningPathsInput {
  id: string;
}
export type DeleteLearningPathsOutput = void;
export const deleteLearningPathsEndpoint = {
  operationId: "deleteLearningPaths" as const,
  method: "DELETE" as const,
  path: "/v1/learning-paths/{id}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsCreatorInput {
  creatorId: string;
  query?: {
    includeUnpublished?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsCreatorOutput =
  Array<Types.LearningExperienceLearningPathsLearningPath>;
export const getLearningPathsCreatorEndpoint = {
  operationId: "getLearningPathsCreator" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/creator/{creatorId}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsPublishInput {
  id: string;
}
export type PostLearningPathsPublishOutput =
  Types.LearningExperienceLearningPathsLearningPath;
export const postLearningPathsPublishEndpoint = {
  operationId: "postLearningPathsPublish" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/publish" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsUnpublishInput {
  id: string;
}
export type PostLearningPathsUnpublishOutput =
  Types.LearningExperienceLearningPathsLearningPath;
export const postLearningPathsUnpublishEndpoint = {
  operationId: "postLearningPathsUnpublish" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/unpublish" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsCoursesInput {
  id: string;
  body?: Types.LearningExperienceLearningPathsAddCourseToPath;
}
export type PostLearningPathsCoursesOutput =
  Types.LearningExperienceLearningPathsLearningPathDetail;
export const postLearningPathsCoursesEndpoint = {
  operationId: "postLearningPathsCourses" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/courses" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface DeleteLearningPathsCoursesInput {
  id: string;
  courseId: string;
}
export type DeleteLearningPathsCoursesOutput = void;
export const deleteLearningPathsCoursesEndpoint = {
  operationId: "deleteLearningPathsCourses" as const,
  method: "DELETE" as const,
  path: "/v1/learning-paths/{id}/courses/{courseId}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PutLearningPathsCoursesOrderInput {
  id: string;
  body?: Types.LearningExperienceLearningPathsReorderCourses;
}
export type PutLearningPathsCoursesOrderOutput =
  Types.LearningExperienceLearningPathsLearningPathDetail;
export const putLearningPathsCoursesOrderEndpoint = {
  operationId: "putLearningPathsCoursesOrder" as const,
  method: "PUT" as const,
  path: "/v1/learning-paths/{id}/courses/order" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsEnrollInput {
  id: string;
  query?: {
    userId?: string;
  };
}
export type PostLearningPathsEnrollOutput =
  Types.LearningExperienceLearningPathsLearningPathEnrollment;
export const postLearningPathsEnrollEndpoint = {
  operationId: "postLearningPathsEnroll" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/enroll" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsUnenrollInput {
  id: string;
  query?: {
    userId?: string;
  };
}
export type PostLearningPathsUnenrollOutput = void;
export const postLearningPathsUnenrollEndpoint = {
  operationId: "postLearningPathsUnenroll" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/unenroll" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsEnrollmentInput {
  id: string;
  userId: string;
}
export type GetLearningPathsEnrollmentOutput =
  Types.LearningExperienceLearningPathsLearningPathEnrollment;
export const getLearningPathsEnrollmentEndpoint = {
  operationId: "getLearningPathsEnrollment" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/{id}/enrollment/{userId}" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsEnrollmentCheckInput {
  id: string;
  userId: string;
}
export type GetLearningPathsEnrollmentCheckOutput = boolean;
export const getLearningPathsEnrollmentCheckEndpoint = {
  operationId: "getLearningPathsEnrollmentCheck" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/{id}/enrollment/{userId}/check" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PutLearningPathsProgressInput {
  id: string;
  query?: {
    userId?: string;
  };
  body?: Types.LearningExperienceLearningPathsUpdatePathProgress;
}
export type PutLearningPathsProgressOutput =
  Types.LearningExperienceLearningPathsLearningPathEnrollment;
export const putLearningPathsProgressEndpoint = {
  operationId: "putLearningPathsProgress" as const,
  method: "PUT" as const,
  path: "/v1/learning-paths/{id}/progress" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsCompleteInput {
  id: string;
  query?: {
    userId?: string;
  };
}
export type PostLearningPathsCompleteOutput =
  Types.LearningExperienceLearningPathsLearningPathEnrollment;
export const postLearningPathsCompleteEndpoint = {
  operationId: "postLearningPathsComplete" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/complete" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface PostLearningPathsAbandonInput {
  id: string;
  query?: {
    userId?: string;
  };
}
export type PostLearningPathsAbandonOutput = void;
export const postLearningPathsAbandonEndpoint = {
  operationId: "postLearningPathsAbandon" as const,
  method: "POST" as const,
  path: "/v1/learning-paths/{id}/abandon" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsUserEnrollmentsInput {
  userId: string;
  query?: {
    status?: Types.LearningExperienceLearningPathsLearningPathEnrollmentStatus;
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsUserEnrollmentsOutput =
  Array<Types.LearningExperienceLearningPathsLearningPathEnrollment>;
export const getLearningPathsUserEnrollmentsEndpoint = {
  operationId: "getLearningPathsUserEnrollments" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/user/{userId}/enrollments" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsUserCompletedInput {
  userId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsUserCompletedOutput =
  Array<Types.LearningExperienceLearningPathsLearningPathEnrollment>;
export const getLearningPathsUserCompletedEndpoint = {
  operationId: "getLearningPathsUserCompleted" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/user/{userId}/completed" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsStatisticsInput {
  id: string;
}
export type GetLearningPathsStatisticsOutput =
  Types.LearningExperienceLearningPathsLearningPathStatistics;
export const getLearningPathsStatisticsEndpoint = {
  operationId: "getLearningPathsStatistics" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/{id}/statistics" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetLearningPathsEnrollmentsInput {
  id: string;
  query?: {
    status?: Types.LearningExperienceLearningPathsLearningPathEnrollmentStatus;
    skip?: number;
    take?: number;
  };
}
export type GetLearningPathsEnrollmentsOutput =
  Array<Types.LearningExperienceLearningPathsLearningPathEnrollment>;
export const getLearningPathsEnrollmentsEndpoint = {
  operationId: "getLearningPathsEnrollments" as const,
  method: "GET" as const,
  path: "/v1/learning-paths/{id}/enrollments" as const,
  tags: ["LearningExperienceLearningPathsLearningPath"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesInteractionsEventsInput {
  programId: string;
  interactionId: string;
}
export type GetCoursesInteractionsEventsOutput =
  Array<Types.LearningCoursesContentInteractionEvent>;
export const getCoursesInteractionsEventsEndpoint = {
  operationId: "getCoursesInteractionsEvents" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/interactions/{interactionId}/events" as const,
  tags: ["LearningCoursesLessonInteractionEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesInteractionsEventsInput {
  programId: string;
  interactionId: string;
  body?: Types.LearningCoursesRecordContentInteractionEventInput;
}
export type PostCoursesInteractionsEventsOutput =
  Types.LearningCoursesContentInteractionEvent;
export const postCoursesInteractionsEventsEndpoint = {
  operationId: "postCoursesInteractionsEvents" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/interactions/{interactionId}/events" as const,
  tags: ["LearningCoursesLessonInteractionEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialCoursesLikeInput {
  courseId: string;
}
export type PostApiSocialCoursesLikeOutput =
  Types.LearningExperienceSocialServicesCourseLike;
export const postApiSocialCoursesLikeEndpoint = {
  operationId: "postApiSocialCoursesLike" as const,
  method: "POST" as const,
  path: "/api/social/courses/{courseId}/like" as const,
  tags: ["LearningExperienceSocialLikes"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialCoursesLikeInput {
  courseId: string;
}
export type DeleteApiSocialCoursesLikeOutput = void;
export const deleteApiSocialCoursesLikeEndpoint = {
  operationId: "deleteApiSocialCoursesLike" as const,
  method: "DELETE" as const,
  path: "/api/social/courses/{courseId}/like" as const,
  tags: ["LearningExperienceSocialLikes"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesLikeCheckInput {
  courseId: string;
}
export type GetApiSocialCoursesLikeCheckOutput = boolean;
export const getApiSocialCoursesLikeCheckEndpoint = {
  operationId: "getApiSocialCoursesLikeCheck" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/like/check" as const,
  tags: ["LearningExperienceSocialLikes"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesLikeCountInput {
  courseId: string;
}
export type GetApiSocialCoursesLikeCountOutput = number;
export const getApiSocialCoursesLikeCountEndpoint = {
  operationId: "getApiSocialCoursesLikeCount" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/like/count" as const,
  tags: ["LearningExperienceSocialLikes"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialLikesMeInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialLikesMeOutput =
  Array<Types.LearningExperienceSocialServicesCourseLike>;
export const getApiSocialLikesMeEndpoint = {
  operationId: "getApiSocialLikesMe" as const,
  method: "GET" as const,
  path: "/api/social/likes/me" as const,
  tags: ["LearningExperienceSocialLikes"] as const,
  requiresAuth: true,
} as const;

export interface GetApiMetricsProductInput {
  query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
  };
}
export type GetApiMetricsProductOutput = Types.AnalyticsProductMetricsOutput;
export const getApiMetricsProductEndpoint = {
  operationId: "getApiMetricsProduct" as const,
  method: "GET" as const,
  path: "/api/metrics/product" as const,
  tags: ["AnalyticsMetricsProduct"] as const,
  requiresAuth: true,
} as const;

export interface GetApiMetricsProductExportInput {
  query?: {
    startUtc?: string;
    endUtc?: string;
    tenantId?: string;
    format?: Types.AnalyticsProductMetricsExportFormat;
  };
}
export type GetApiMetricsProductExportOutput = Blob;
export const getApiMetricsProductExportEndpoint = {
  operationId: "getApiMetricsProductExport" as const,
  method: "GET" as const,
  path: "/api/metrics/product/export" as const,
  tags: ["AnalyticsMetricsProduct"] as const,
  requiresAuth: true,
} as const;

export interface GetSlaSlosForGetSlaSlosInput {
  query?: {
    tenantId?: string;
    serviceName?: string;
    isEnabled?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetSlaSlosForGetSlaSlosOutput = Array<Types.MonitoringSLASlo>;
export const getSlaSlosForGetSlaSlosEndpoint = {
  operationId: "getSlaSlosForGetSlaSlos" as const,
  method: "GET" as const,
  path: "/api/v1/sla/slos" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface PostSlaSlosInput {
  body?: Types.MonitoringSLACreateSloCommand;
}
export type PostSlaSlosOutput = Types.MonitoringSLASlo;
export const postSlaSlosEndpoint = {
  operationId: "postSlaSlos" as const,
  method: "POST" as const,
  path: "/api/v1/sla/slos" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface GetSlaSlosForGetSlaSlosByIdInput {
  id: string;
}
export type GetSlaSlosForGetSlaSlosByIdOutput = Types.MonitoringSLASlo;
export const getSlaSlosForGetSlaSlosByIdEndpoint = {
  operationId: "getSlaSlosForGetSlaSlosById" as const,
  method: "GET" as const,
  path: "/api/v1/sla/slos/{id}" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface PutSlaSlosInput {
  id: string;
  body?: Types.MonitoringSLAUpdateSloCommand;
}
export type PutSlaSlosOutput = Types.MonitoringSLASlo;
export const putSlaSlosEndpoint = {
  operationId: "putSlaSlos" as const,
  method: "PUT" as const,
  path: "/api/v1/sla/slos/{id}" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface DeleteSlaSlosInput {
  id: string;
}
export type DeleteSlaSlosOutput = void;
export const deleteSlaSlosEndpoint = {
  operationId: "deleteSlaSlos" as const,
  method: "DELETE" as const,
  path: "/api/v1/sla/slos/{id}" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface PostSlaSlisInput {
  body?: Types.MonitoringSLARecordSliMetricCommand;
}
export type PostSlaSlisOutput = void;
export const postSlaSlisEndpoint = {
  operationId: "postSlaSlis" as const,
  method: "POST" as const,
  path: "/api/v1/sla/slis" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface GetSlaSlosComplianceInput {
  id: string;
  query?: {
    startDate?: string;
    endDate?: string;
  };
}
export type GetSlaSlosComplianceOutput = Types.MonitoringSLASloCompliance;
export const getSlaSlosComplianceEndpoint = {
  operationId: "getSlaSlosCompliance" as const,
  method: "GET" as const,
  path: "/api/v1/sla/slos/{id}/compliance" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface GetSlaSlosErrorBudgetInput {
  id: string;
}
export type GetSlaSlosErrorBudgetOutput = Types.MonitoringSLAErrorBudget;
export const getSlaSlosErrorBudgetEndpoint = {
  operationId: "getSlaSlosErrorBudget" as const,
  method: "GET" as const,
  path: "/api/v1/sla/slos/{id}/error-budget" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface GetSlaViolationsInput {
  query?: {
    sloId?: string;
    tenantId?: string;
    onlyUnresolved?: boolean;
    startDate?: string;
    endDate?: string;
    skip?: number;
    take?: number;
  };
}
export type GetSlaViolationsOutput = Array<Types.MonitoringSLASloViolation>;
export const getSlaViolationsEndpoint = {
  operationId: "getSlaViolations" as const,
  method: "GET" as const,
  path: "/api/v1/sla/violations" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface PostSlaViolationsResolveInput {
  id: string;
  body?: Types.MonitoringSLAResolveSloViolationCommand;
}
export type PostSlaViolationsResolveOutput = void;
export const postSlaViolationsResolveEndpoint = {
  operationId: "postSlaViolationsResolve" as const,
  method: "POST" as const,
  path: "/api/v1/sla/violations/{id}/resolve" as const,
  tags: ["MonitoringSla"] as const,
  requiresAuth: true,
} as const;

export interface GetApiNotificationsForGetApiNotificationsInput {
  query?: {
    skip?: number;
    take?: number;
    isRead?: boolean;
  };
}
export type GetApiNotificationsForGetApiNotificationsOutput =
  Array<Types.NotificationsControllersNotification>;
export const getApiNotificationsForGetApiNotificationsEndpoint = {
  operationId: "getApiNotificationsForGetApiNotifications" as const,
  method: "GET" as const,
  path: "/api/notifications" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export type GetApiNotificationsUnreadCountInput = void;
export type GetApiNotificationsUnreadCountOutput =
  Types.NotificationsControllersUnreadCountOutput;
export const getApiNotificationsUnreadCountEndpoint = {
  operationId: "getApiNotificationsUnreadCount" as const,
  method: "GET" as const,
  path: "/api/notifications/unread-count" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface GetApiNotificationsForGetApiNotificationsByIdInput {
  id: string;
}
export type GetApiNotificationsForGetApiNotificationsByIdOutput =
  Types.NotificationsControllersNotification;
export const getApiNotificationsForGetApiNotificationsByIdEndpoint = {
  operationId: "getApiNotificationsForGetApiNotificationsById" as const,
  method: "GET" as const,
  path: "/api/notifications/{id}" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiNotificationsInput {
  id: string;
}
export type DeleteApiNotificationsOutput = void;
export const deleteApiNotificationsEndpoint = {
  operationId: "deleteApiNotifications" as const,
  method: "DELETE" as const,
  path: "/api/notifications/{id}" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface PostApiNotificationsReadInput {
  id: string;
}
export type PostApiNotificationsReadOutput = void;
export const postApiNotificationsReadEndpoint = {
  operationId: "postApiNotificationsRead" as const,
  method: "POST" as const,
  path: "/api/notifications/{id}/read" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export type PostApiNotificationsReadAllInput = void;
export type PostApiNotificationsReadAllOutput = void;
export const postApiNotificationsReadAllEndpoint = {
  operationId: "postApiNotificationsReadAll" as const,
  method: "POST" as const,
  path: "/api/notifications/read-all" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface PostApiNotificationsUnreadInput {
  id: string;
}
export type PostApiNotificationsUnreadOutput = void;
export const postApiNotificationsUnreadEndpoint = {
  operationId: "postApiNotificationsUnread" as const,
  method: "POST" as const,
  path: "/api/notifications/{id}/unread" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export type DeleteApiNotificationsReadInput = void;
export type DeleteApiNotificationsReadOutput =
  Types.NotificationsControllersDeletedCountOutput;
export const deleteApiNotificationsReadEndpoint = {
  operationId: "deleteApiNotificationsRead" as const,
  method: "DELETE" as const,
  path: "/api/notifications/read" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export type GetApiNotificationsPreferencesInput = void;
export type GetApiNotificationsPreferencesOutput =
  Types.NotificationsControllersNotificationPreference;
export const getApiNotificationsPreferencesEndpoint = {
  operationId: "getApiNotificationsPreferences" as const,
  method: "GET" as const,
  path: "/api/notifications/preferences" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface PutApiNotificationsPreferencesInput {
  body?: Types.NotificationsControllersUpdatePreferencesInput;
}
export type PutApiNotificationsPreferencesOutput =
  Types.NotificationsControllersNotificationPreference;
export const putApiNotificationsPreferencesEndpoint = {
  operationId: "putApiNotificationsPreferences" as const,
  method: "PUT" as const,
  path: "/api/notifications/preferences" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

export interface PutApiNotificationsPreferencesQuietHoursInput {
  body?: Types.NotificationsControllersSetQuietHoursInput;
}
export type PutApiNotificationsPreferencesQuietHoursOutput = void;
export const putApiNotificationsPreferencesQuietHoursEndpoint = {
  operationId: "putApiNotificationsPreferencesQuietHours" as const,
  method: "PUT" as const,
  path: "/api/notifications/preferences/quiet-hours" as const,
  tags: ["Notifications"] as const,
  requiresAuth: true,
} as const;

/**
 * List subscription billing notifications
 *
 * Lists local billing notification records tied to subscriptions.
 */
export interface GetNotificationsSubscriptionsInput {
  query?: {
    tenantId?: string;
    subscriptionId?: string;
    channel?: Types.NotificationsNotificationChannel;
    isSent?: boolean;
    page?: number;
    pageSize?: number;
  };
}
export type GetNotificationsSubscriptionsOutput =
  Types.PagedResultOfCommerceSubscriptionsSubscriptionNotification;
export const getNotificationsSubscriptionsEndpoint = {
  operationId: "getNotificationsSubscriptions" as const,
  method: "GET" as const,
  path: "/api/v1/notifications/subscriptions" as const,
  tags: ["NotificationsSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Resend subscription billing notification
 *
 * Creates a new local delivery record from an existing subscription billing notification.
 */
export interface PostNotificationsSubscriptionsResendInput {
  notificationId: string;
  body?: Types.CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput;
}
export type PostNotificationsSubscriptionsResendOutput =
  Types.CommerceSubscriptionsSubscriptionNotification;
export const postNotificationsSubscriptionsResendEndpoint = {
  operationId: "postNotificationsSubscriptionsResend" as const,
  method: "POST" as const,
  path: "/api/v1/notifications/subscriptions/{notificationId}:resend" as const,
  tags: ["NotificationsSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Retrieve all payment transactions with optional filtering
 *
 * Retrieves a paginated list of all payment transactions with support for filtering by tenant, status, and date range. This is the primary endpoint for payment administration and reporting.
 */
export interface GetPaymentsInput {
  query?: {
    tenantId?: string;
    status?: string;
    startDate?: string;
    endDate?: string;
    page?: number;
    pageSize?: number;
  };
}
export type GetPaymentsOutput = Array<Types.CommercePaymentsPaymentResult>;
export const getPaymentsEndpoint = {
  operationId: "getPayments" as const,
  method: "GET" as const,
  path: "/api/v1/payments" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Process a new payment transaction
 *
 * Initiates a new payment transaction for a subscription. This endpoint handles the complete payment processing workflow including payment method validation, amount verification, and transaction execution. Returns the payment result immediately with a transaction ID that can be used to track payment status.
 */
export interface PostPaymentsInput {
  body?: Types.CommercePaymentsPaymentsControllerProcessPaymentInput;
}
export type PostPaymentsOutput = Types.CommercePaymentsPaymentResult;
export const postPaymentsEndpoint = {
  operationId: "postPayments" as const,
  method: "POST" as const,
  path: "/api/v1/payments" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a Stripe SetupIntent for subscription checkout
 *
 * Creates or reuses a Stripe customer for the subscription and returns a SetupIntent client secret for PaymentElement-based card collection.
 */
export interface PostPaymentsSetupIntentsInput {
  body?: Types.CommercePaymentsPaymentsControllerCreateSetupIntentInput;
}
export type PostPaymentsSetupIntentsOutput =
  Types.CommercePaymentsPaymentsControllerCreateSetupIntentOutput;
export const postPaymentsSetupIntentsEndpoint = {
  operationId: "postPaymentsSetupIntents" as const,
  method: "POST" as const,
  path: "/api/v1/payments/setup-intents" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Complete subscription checkout after setup confirmation
 *
 * Sets the confirmed Stripe payment method as the customer's default and processes the first subscription charge.
 */
export interface PostPaymentsSubscriptionCheckoutsCompleteInput {
  body?: Types.CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput;
}
export type PostPaymentsSubscriptionCheckoutsCompleteOutput =
  Types.CommercePaymentsPaymentResult;
export const postPaymentsSubscriptionCheckoutsCompleteEndpoint = {
  operationId: "postPaymentsSubscriptionCheckoutsComplete" as const,
  method: "POST" as const,
  path: "/api/v1/payments/subscription-checkouts:complete" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Retrieve a specific payment by its unique identifier
 *
 * Retrieves detailed information about a specific payment transaction, including its current status, amount, payment method, and processing details. Use this endpoint to track payment progress and verify transaction completion.
 */
export interface GetPaymentByIdInput {
  paymentId: string;
}
export type GetPaymentByIdOutput = Types.CommercePaymentsPaymentResult;
export const getPaymentByIdEndpoint = {
  operationId: "getPaymentById" as const,
  method: "GET" as const,
  path: "/api/v1/payments/{paymentId}" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel a payment transaction
 *
 * Cancels a payment transaction that is in progress or pending. Custom action per Google API guidelines. Once canceled, a payment cannot be processed and may require a new payment attempt.
 */
export interface PostPaymentsCancelInput {
  paymentId: string;
  body?: Types.CommercePaymentsPaymentsControllerCancelPaymentInput;
}
export type PostPaymentsCancelOutput =
  Types.CommercePaymentsPaymentCancellationResult;
export const postPaymentsCancelEndpoint = {
  operationId: "postPaymentsCancel" as const,
  method: "POST" as const,
  path: "/api/v1/payments/{paymentId}:cancel" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Process a refund for a completed payment
 *
 * Processes a full or partial refund for a completed payment. Custom action per Google API guidelines. Refunds are processed back to the original payment method.
 */
export interface PostPaymentsRefundInput {
  paymentId: string;
  body?: Types.CommercePaymentsPaymentsControllerRefundInput;
}
export type PostPaymentsRefundOutput =
  Types.CommercePaymentsProcessRefundResult;
export const postPaymentsRefundEndpoint = {
  operationId: "postPaymentsRefund" as const,
  method: "POST" as const,
  path: "/api/v1/payments/{paymentId}:refund" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

/**
 * Retry a failed payment transaction
 *
 * Retries a failed payment using the original payment method. Custom action per Google API guidelines. Creates a new transaction attempt while maintaining the link to the original payment record.
 */
export interface PostPaymentsRetryInput {
  paymentId: string;
}
export type PostPaymentsRetryOutput = Types.CommercePaymentsPaymentRetryResult;
export const postPaymentsRetryEndpoint = {
  operationId: "postPaymentsRetry" as const,
  method: "POST" as const,
  path: "/api/v1/payments/{paymentId}:retry" as const,
  tags: ["CommercePayments"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsCommentsInput {
  postId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsCommentsOutput = void;
export const getPostsCommentsEndpoint = {
  operationId: "getPostsComments" as const,
  method: "GET" as const,
  path: "/api/v1/posts/{postId}/comments" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsCommentsInput {
  postId: string;
  body?: Types.SocialPostsControllersAddCommentInput;
}
export type PostPostsCommentsOutput = void;
export const postPostsCommentsEndpoint = {
  operationId: "postPostsComments" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/comments" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface PutPostsCommentsInput {
  postId: string;
  commentId: string;
  body?: Types.SocialPostsControllersUpdateCommentInput;
}
export type PutPostsCommentsOutput = void;
export const putPostsCommentsEndpoint = {
  operationId: "putPostsComments" as const,
  method: "PUT" as const,
  path: "/api/v1/posts/{postId}/comments/{commentId}" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface DeletePostsCommentsInput {
  postId: string;
  commentId: string;
}
export type DeletePostsCommentsOutput = void;
export const deletePostsCommentsEndpoint = {
  operationId: "deletePostsComments" as const,
  method: "DELETE" as const,
  path: "/api/v1/posts/{postId}/comments/{commentId}" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsTagsPopularInput {
  query?: {
    count?: number;
  };
}
export type GetPostsTagsPopularOutput = void;
export const getPostsTagsPopularEndpoint = {
  operationId: "getPostsTagsPopular" as const,
  method: "GET" as const,
  path: "/api/v1/posts/tags/popular" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsTagsInput {
  postId: string;
}
export type GetPostsTagsOutput = void;
export const getPostsTagsEndpoint = {
  operationId: "getPostsTags" as const,
  method: "GET" as const,
  path: "/api/v1/posts/{postId}/tags" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsTagsSearchInput {
  query?: {
    tags?: Array<string>;
    skip?: number;
    take?: number;
  };
}
export type GetPostsTagsSearchOutput = void;
export const getPostsTagsSearchEndpoint = {
  operationId: "getPostsTagsSearch" as const,
  method: "GET" as const,
  path: "/api/v1/posts/tags/search" as const,
  tags: ["SocialPostsComments"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsLikeInput {
  postId: string;
  query?: {
    reactionType?: string;
  };
}
export type PostPostsLikeOutput = void;
export const postPostsLikeEndpoint = {
  operationId: "postPostsLike" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/like" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsPinInput {
  postId: string;
}
export type PostPostsPinOutput = void;
export const postPostsPinEndpoint = {
  operationId: "postPostsPin" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/pin" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsShareInput {
  postId: string;
}
export type PostPostsShareOutput = void;
export const postPostsShareEndpoint = {
  operationId: "postPostsShare" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/share" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsViewInput {
  postId: string;
}
export type PostPostsViewOutput = void;
export const postPostsViewEndpoint = {
  operationId: "postPostsView" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/view" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsStatisticsInput {
  postId: string;
}
export type GetPostsStatisticsOutput = void;
export const getPostsStatisticsEndpoint = {
  operationId: "getPostsStatistics" as const,
  method: "GET" as const,
  path: "/api/v1/posts/{postId}/statistics" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsFollowInput {
  postId: string;
}
export type GetPostsFollowOutput = void;
export const getPostsFollowEndpoint = {
  operationId: "getPostsFollow" as const,
  method: "GET" as const,
  path: "/api/v1/posts/{postId}/follow" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsFollowInput {
  postId: string;
  body?: Types.SocialPostsControllersFollowPostInput;
}
export type PostPostsFollowOutput = void;
export const postPostsFollowEndpoint = {
  operationId: "postPostsFollow" as const,
  method: "POST" as const,
  path: "/api/v1/posts/{postId}/follow" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface DeletePostsFollowInput {
  postId: string;
}
export type DeletePostsFollowOutput = void;
export const deletePostsFollowEndpoint = {
  operationId: "deletePostsFollow" as const,
  method: "DELETE" as const,
  path: "/api/v1/posts/{postId}/follow" as const,
  tags: ["SocialPostsInteractions"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsForGetPostsInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsForGetPostsOutput = void;
export const getPostsForGetPostsEndpoint = {
  operationId: "getPostsForGetPosts" as const,
  method: "GET" as const,
  path: "/api/v1/posts" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostPostsInput {
  body?: Types.SocialPostsControllersCreatePostInput;
}
export type PostPostsOutput = void;
export const postPostsEndpoint = {
  operationId: "postPosts" as const,
  method: "POST" as const,
  path: "/api/v1/posts" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsForGetPostsByPostIdInput {
  postId: string;
}
export type GetPostsForGetPostsByPostIdOutput = void;
export const getPostsForGetPostsByPostIdEndpoint = {
  operationId: "getPostsForGetPostsByPostId" as const,
  method: "GET" as const,
  path: "/api/v1/posts/{postId}" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface PutPostsInput {
  postId: string;
  body?: Types.SocialPostsControllersUpdatePostInput;
}
export type PutPostsOutput = void;
export const putPostsEndpoint = {
  operationId: "putPosts" as const,
  method: "PUT" as const,
  path: "/api/v1/posts/{postId}" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface DeletePostsInput {
  postId: string;
}
export type DeletePostsOutput = void;
export const deletePostsEndpoint = {
  operationId: "deletePosts" as const,
  method: "DELETE" as const,
  path: "/api/v1/posts/{postId}" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsFeedInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsFeedOutput = void;
export const getPostsFeedEndpoint = {
  operationId: "getPostsFeed" as const,
  method: "GET" as const,
  path: "/api/v1/posts/feed" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsTrendingInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsTrendingOutput = void;
export const getPostsTrendingEndpoint = {
  operationId: "getPostsTrending" as const,
  method: "GET" as const,
  path: "/api/v1/posts/trending" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsAuthorInput {
  authorId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsAuthorOutput = void;
export const getPostsAuthorEndpoint = {
  operationId: "getPostsAuthor" as const,
  method: "GET" as const,
  path: "/api/v1/posts/author/{authorId}" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsMyInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetPostsMyOutput = void;
export const getPostsMyEndpoint = {
  operationId: "getPostsMy" as const,
  method: "GET" as const,
  path: "/api/v1/posts/my" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface GetPostsSearchInput {
  query?: {
    q?: string;
    skip?: number;
    take?: number;
  };
}
export type GetPostsSearchOutput = void;
export const getPostsSearchEndpoint = {
  operationId: "getPostsSearch" as const,
  method: "GET" as const,
  path: "/api/v1/posts/search" as const,
  tags: ["SocialPosts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiPrerequisitesInput {
  body?: Types.LearningCoursesCreatePrerequisiteApiInput;
}
export type PostApiPrerequisitesOutput = Types.LearningCoursesPrerequisite;
export const postApiPrerequisitesEndpoint = {
  operationId: "postApiPrerequisites" as const,
  method: "POST" as const,
  path: "/api/prerequisites" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesInput {
  id: string;
}
export type GetApiPrerequisitesOutput = Types.LearningCoursesPrerequisite;
export const getApiPrerequisitesEndpoint = {
  operationId: "getApiPrerequisites" as const,
  method: "GET" as const,
  path: "/api/prerequisites/{id}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface PutApiPrerequisitesInput {
  id: string;
  body?: Types.LearningCoursesUpdatePrerequisiteApiInput;
}
export type PutApiPrerequisitesOutput = Types.LearningCoursesPrerequisite;
export const putApiPrerequisitesEndpoint = {
  operationId: "putApiPrerequisites" as const,
  method: "PUT" as const,
  path: "/api/prerequisites/{id}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiPrerequisitesInput {
  id: string;
}
export type DeleteApiPrerequisitesOutput = void;
export const deleteApiPrerequisitesEndpoint = {
  operationId: "deleteApiPrerequisites" as const,
  method: "DELETE" as const,
  path: "/api/prerequisites/{id}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesCourseInput {
  courseId: string;
}
export type GetApiPrerequisitesCourseOutput =
  Array<Types.LearningCoursesPrerequisite>;
export const getApiPrerequisitesCourseEndpoint = {
  operationId: "getApiPrerequisitesCourse" as const,
  method: "GET" as const,
  path: "/api/prerequisites/course/{courseId}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesDependentsInput {
  courseId: string;
}
export type GetApiPrerequisitesDependentsOutput =
  Array<Types.LearningCoursesPrerequisite>;
export const getApiPrerequisitesDependentsEndpoint = {
  operationId: "getApiPrerequisitesDependents" as const,
  method: "GET" as const,
  path: "/api/prerequisites/dependents/{courseId}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesCourseChainInput {
  courseId: string;
}
export type GetApiPrerequisitesCourseChainOutput =
  Array<Types.LearningCoursesPrerequisite>;
export const getApiPrerequisitesCourseChainEndpoint = {
  operationId: "getApiPrerequisitesCourseChain" as const,
  method: "GET" as const,
  path: "/api/prerequisites/course/{courseId}/chain" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckInput {
  courseId: string;
}
export type GetApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckOutput =
  Types.LearningCoursesPrerequisiteCheckResult;
export const getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckEndpoint =
  {
    operationId:
      "getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheck" as const,
    method: "GET" as const,
    path: "/api/prerequisites/course/{courseId}/check" as const,
    tags: ["LearningCoursesPrerequisites"] as const,
    requiresAuth: true,
  } as const;

export interface GetApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserIdInput {
  courseId: string;
  userId: string;
}
export type GetApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserIdOutput =
  Types.LearningCoursesPrerequisiteCheckResult;
export const getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserIdEndpoint =
  {
    operationId:
      "getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserId" as const,
    method: "GET" as const,
    path: "/api/prerequisites/course/{courseId}/check/{userId}" as const,
    tags: ["LearningCoursesPrerequisites"] as const,
    requiresAuth: true,
  } as const;

export interface PostApiPrerequisitesCourseReorderInput {
  courseId: string;
  body?: Types.LearningCoursesReorderPrerequisitesInput;
}
export type PostApiPrerequisitesCourseReorderOutput = void;
export const postApiPrerequisitesCourseReorderEndpoint = {
  operationId: "postApiPrerequisitesCourseReorder" as const,
  method: "POST" as const,
  path: "/api/prerequisites/course/{courseId}/reorder" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetApiPrerequisitesCourseWouldCreateCycleInput {
  courseId: string;
  prerequisiteCourseId: string;
}
export type GetApiPrerequisitesCourseWouldCreateCycleOutput =
  Types.LearningCoursesCircularDependencyCheckResult;
export const getApiPrerequisitesCourseWouldCreateCycleEndpoint = {
  operationId: "getApiPrerequisitesCourseWouldCreateCycle" as const,
  method: "GET" as const,
  path: "/api/prerequisites/course/{courseId}/would-create-cycle/{prerequisiteCourseId}" as const,
  tags: ["LearningCoursesPrerequisites"] as const,
  requiresAuth: true,
} as const;

export interface GetProductsForGetProductsByProductIdInput {
  productId: string;
  query?: {
    includePricing?: boolean;
    includeUnpublished?: boolean;
  };
}
export type GetProductsForGetProductsByProductIdOutput =
  Types.CommerceProductsProduct;
export const getProductsForGetProductsByProductIdEndpoint = {
  operationId: "getProductsForGetProductsByProductId" as const,
  method: "GET" as const,
  path: "/v1/products/{productId}" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PutProductsInput {
  productId: string;
  body?: Types.CommerceProductsUpdateProductInput;
}
export type PutProductsOutput = Types.CommerceProductsProduct;
export const putProductsEndpoint = {
  operationId: "putProducts" as const,
  method: "PUT" as const,
  path: "/v1/products/{productId}" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProductsInput {
  productId: string;
  query?: {
    softDelete?: boolean;
    reason?: string;
  };
}
export type DeleteProductsOutput = void;
export const deleteProductsEndpoint = {
  operationId: "deleteProducts" as const,
  method: "DELETE" as const,
  path: "/v1/products/{productId}" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PatchProductsInput {
  productId: string;
  body?: Types.CommerceProductsPatchProductInput;
}
export type PatchProductsOutput = Types.CommerceProductsProduct;
export const patchProductsEndpoint = {
  operationId: "patchProducts" as const,
  method: "PATCH" as const,
  path: "/v1/products/{productId}" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface HeadProductsInput {
  productId: string;
  query?: {
    includeUnpublished?: boolean;
  };
}
export type HeadProductsOutput = void;
export const headProductsEndpoint = {
  operationId: "headProducts" as const,
  method: "HEAD" as const,
  path: "/v1/products/{productId}" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface GetProductsPricingInput {
  productId: string;
  query?: {
    includeUnpublished?: boolean;
  };
}
export type GetProductsPricingOutput =
  Array<Types.CommerceProductsProductPricing>;
export const getProductsPricingEndpoint = {
  operationId: "getProductsPricing" as const,
  method: "GET" as const,
  path: "/v1/products/{productId}/pricing" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface GetProductsForGetProductsInput {
  query?: {
    type?: Types.CommerceProductsProductType;
    creatorId?: string;
    searchTerm?: string;
    isBundle?: boolean;
    includeUnpublished?: boolean;
    skip?: number;
    take?: number;
    sortBy?: string;
    sortDirection?: string;
  };
}
export type GetProductsForGetProductsOutput =
  Types.PagedResultOfCommerceProductsProduct;
export const getProductsForGetProductsEndpoint = {
  operationId: "getProductsForGetProducts" as const,
  method: "GET" as const,
  path: "/v1/products" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProductsInput {
  body?: Types.CommerceProductsCreateProductInput;
}
export type PostProductsOutput = Types.CommerceProductsProduct;
export const postProductsEndpoint = {
  operationId: "postProducts" as const,
  method: "POST" as const,
  path: "/v1/products" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProductsBatchCreateInput {
  body?: Types.CommerceProductsBatchCreateProductsInput;
}
export type PostProductsBatchCreateOutput =
  Array<Types.CommerceProductsProduct>;
export const postProductsBatchCreateEndpoint = {
  operationId: "postProductsBatchCreate" as const,
  method: "POST" as const,
  path: "/v1/products/:batch-create" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProductsActivateInput {
  productId: string;
}
export type PostProductsActivateOutput = Types.CommerceProductsProduct;
export const postProductsActivateEndpoint = {
  operationId: "postProductsActivate" as const,
  method: "POST" as const,
  path: "/v1/products/{productId}:activate" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProductsDeactivateInput {
  productId: string;
}
export type PostProductsDeactivateOutput = Types.CommerceProductsProduct;
export const postProductsDeactivateEndpoint = {
  operationId: "postProductsDeactivate" as const,
  method: "POST" as const,
  path: "/v1/products/{productId}:deactivate" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProductsArchiveInput {
  productId: string;
}
export type PostProductsArchiveOutput = Types.CommerceProductsProduct;
export const postProductsArchiveEndpoint = {
  operationId: "postProductsArchive" as const,
  method: "POST" as const,
  path: "/v1/products/{productId}:archive" as const,
  tags: ["CommerceProducts"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentInput {
  programId: string;
  query?: {
    level?: string;
  };
}
export type GetCoursesContentOutput =
  Array<Types.LearningCoursesProgramContent>;
export const getCoursesContentEndpoint = {
  operationId: "getCoursesContent" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesContentInput {
  programId: string;
  body?: Types.LearningCoursesCreateProgramContent;
}
export type PostCoursesContentOutput = Types.LearningCoursesProgramContent;
export const postCoursesContentEndpoint = {
  operationId: "postCoursesContent" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/content" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentByIdInput {
  programId: string;
  id: string;
}
export type GetCoursesContentByIdOutput = Types.LearningCoursesProgramContent;
export const getCoursesContentByIdEndpoint = {
  operationId: "getCoursesContentById" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/{id}" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesContentInput {
  programId: string;
  id: string;
  body?: Types.LearningCoursesUpdateProgramContent;
}
export type PutCoursesContentOutput = Types.LearningCoursesProgramContent;
export const putCoursesContentEndpoint = {
  operationId: "putCoursesContent" as const,
  method: "PUT" as const,
  path: "/v1/courses/{programId}/content/{id}" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface DeleteCoursesContentInput {
  programId: string;
  id: string;
}
export type DeleteCoursesContentOutput = void;
export const deleteCoursesContentEndpoint = {
  operationId: "deleteCoursesContent" as const,
  method: "DELETE" as const,
  path: "/v1/courses/{programId}/content/{id}" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesContentSubmitInput {
  programId: string;
  id: string;
  body?: Types.LearningCoursesSubmitUserContent;
}
export type PostCoursesContentSubmitOutput =
  Types.LearningCoursesContentInteraction;
export const postCoursesContentSubmitEndpoint = {
  operationId: "postCoursesContentSubmit" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/content/{id}/submit" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentChildrenInput {
  programId: string;
  parentId: string;
}
export type GetCoursesContentChildrenOutput =
  Array<Types.LearningCoursesProgramContent>;
export const getCoursesContentChildrenEndpoint = {
  operationId: "getCoursesContentChildren" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/{parentId}/children" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesContentReorderInput {
  programId: string;
  body?: Types.LearningCoursesReorderContent;
}
export type PostCoursesContentReorderOutput = void;
export const postCoursesContentReorderEndpoint = {
  operationId: "postCoursesContentReorder" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/content/reorder" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesContentMoveInput {
  programId: string;
  id: string;
  body?: Types.LearningCoursesMoveContent;
}
export type PostCoursesContentMoveOutput = void;
export const postCoursesContentMoveEndpoint = {
  operationId: "postCoursesContentMove" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/content/{id}/move" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentRequiredInput {
  programId: string;
}
export type GetCoursesContentRequiredOutput =
  Array<Types.LearningCoursesProgramContent>;
export const getCoursesContentRequiredEndpoint = {
  operationId: "getCoursesContentRequired" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/required" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentByTypeInput {
  programId: string;
  type: Types.LearningCoursesProgramContentType;
}
export type GetCoursesContentByTypeOutput =
  Array<Types.LearningCoursesProgramContent>;
export const getCoursesContentByTypeEndpoint = {
  operationId: "getCoursesContentByType" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/by-type/{type}" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentByVisibilityInput {
  programId: string;
  visibility: Types.LearningCoursesVisibility;
}
export type GetCoursesContentByVisibilityOutput =
  Array<Types.LearningCoursesProgramContent>;
export const getCoursesContentByVisibilityEndpoint = {
  operationId: "getCoursesContentByVisibility" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/by-visibility/{visibility}" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesContentSearchInput {
  programId: string;
  body?: Types.LearningCoursesSearchContent;
}
export type PostCoursesContentSearchOutput =
  Array<Types.LearningCoursesProgramContent>;
export const postCoursesContentSearchEndpoint = {
  operationId: "postCoursesContentSearch" as const,
  method: "POST" as const,
  path: "/v1/courses/{programId}/content/search" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentStatsInput {
  programId: string;
}
export type GetCoursesContentStatsOutput = Types.LearningCoursesContentStats;
export const getCoursesContentStatsEndpoint = {
  operationId: "getCoursesContentStats" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/stats" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentCodingAssignmentInput {
  programId: string;
  id: string;
}
export type GetCoursesContentCodingAssignmentOutput =
  Types.LearningCoursesCodingAssignmentContent;
export const getCoursesContentCodingAssignmentEndpoint = {
  operationId: "getCoursesContentCodingAssignment" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/{id}/coding-assignment" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesContentCodingAssignmentInput {
  programId: string;
  id: string;
  body?: Types.LearningCoursesCodingAssignmentContent;
}
export type PutCoursesContentCodingAssignmentOutput =
  Types.LearningCoursesCodingAssignmentContent;
export const putCoursesContentCodingAssignmentEndpoint = {
  operationId: "putCoursesContentCodingAssignment" as const,
  method: "PUT" as const,
  path: "/v1/courses/{programId}/content/{id}/coding-assignment" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesContentCodingAssignmentFullInput {
  programId: string;
  id: string;
}
export type GetCoursesContentCodingAssignmentFullOutput =
  Types.LearningCoursesCodingAssignmentContent;
export const getCoursesContentCodingAssignmentFullEndpoint = {
  operationId: "getCoursesContentCodingAssignmentFull" as const,
  method: "GET" as const,
  path: "/v1/courses/{programId}/content/{id}/coding-assignment/full" as const,
  tags: ["LearningCoursesProgramContent"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesForGetCoursesInput {
  query?: {
    status?: string;
    category?: Types.ProgramCategory;
    difficulty?: Types.LearningCoursesProgramDifficulty;
    creatorId?: string;
    q?: string;
    sort?: string;
    skip?: number;
    take?: number;
  };
}
export type GetCoursesForGetCoursesOutput = Array<Types.LearningCoursesProgram>;
export const getCoursesForGetCoursesEndpoint = {
  operationId: "getCoursesForGetCourses" as const,
  method: "GET" as const,
  path: "/v1/courses" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesInput {
  body?: Types.LearningCoursesCreateProgram;
}
export type PostCoursesOutput = Types.LearningCoursesProgram;
export const postCoursesEndpoint = {
  operationId: "postCourses" as const,
  method: "POST" as const,
  path: "/v1/courses" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesPublicInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetCoursesPublicOutput = Array<Types.LearningCoursesProgram>;
export const getCoursesPublicEndpoint = {
  operationId: "getCoursesPublic" as const,
  method: "GET" as const,
  path: "/v1/courses/public" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export type GetCoursesMeInput = void;
export type GetCoursesMeOutput = Array<Types.LearningCoursesProgram>;
export const getCoursesMeEndpoint = {
  operationId: "getCoursesMe" as const,
  method: "GET" as const,
  path: "/v1/courses/me" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesForGetCoursesByIdInput {
  id: string;
}
export type GetCoursesForGetCoursesByIdOutput = Types.LearningCoursesProgram;
export const getCoursesForGetCoursesByIdEndpoint = {
  operationId: "getCoursesForGetCoursesById" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesInput {
  id: string;
  body?: Types.LearningCoursesUpdateProgram;
}
export type PutCoursesOutput = Types.LearningCoursesProgram;
export const putCoursesEndpoint = {
  operationId: "putCourses" as const,
  method: "PUT" as const,
  path: "/v1/courses/{id}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface DeleteCoursesInput {
  id: string;
}
export type DeleteCoursesOutput = void;
export const deleteCoursesEndpoint = {
  operationId: "deleteCourses" as const,
  method: "DELETE" as const,
  path: "/v1/courses/{id}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesWithContentInput {
  id: string;
}
export type GetCoursesWithContentOutput = Types.LearningCoursesProgram;
export const getCoursesWithContentEndpoint = {
  operationId: "getCoursesWithContent" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/with-content" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesCloneInput {
  id: string;
  body?: Types.LearningCoursesCloneProgram;
}
export type PostCoursesCloneOutput = Types.LearningCoursesProgram;
export const postCoursesCloneEndpoint = {
  operationId: "postCoursesClone" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:clone" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesSlugInput {
  slug: string;
}
export type GetCoursesSlugOutput = Types.LearningCoursesProgram;
export const getCoursesSlugEndpoint = {
  operationId: "getCoursesSlug" as const,
  method: "GET" as const,
  path: "/v1/courses/slug/{slug}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesSelfEnrollInput {
  id: string;
}
export type PostCoursesSelfEnrollOutput = Types.LearningCoursesUserProgress;
export const postCoursesSelfEnrollEndpoint = {
  operationId: "postCoursesSelfEnroll" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:self-enroll" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesUsersInput {
  id: string;
  userId: string;
}
export type PostCoursesUsersOutput = Types.LearningCoursesUserProgress;
export const postCoursesUsersEndpoint = {
  operationId: "postCoursesUsers" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}/users/{userId}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface DeleteCoursesUsersInput {
  id: string;
  userId: string;
}
export type DeleteCoursesUsersOutput = void;
export const deleteCoursesUsersEndpoint = {
  operationId: "deleteCoursesUsers" as const,
  method: "DELETE" as const,
  path: "/v1/courses/{id}/users/{userId}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesUsersInput {
  id: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetCoursesUsersOutput = Array<Types.LearningCoursesUserProgress>;
export const getCoursesUsersEndpoint = {
  operationId: "getCoursesUsers" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/users" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesUsersProgressInput {
  id: string;
  userId: string;
}
export type GetCoursesUsersProgressOutput = Types.LearningCoursesUserProgress;
export const getCoursesUsersProgressEndpoint = {
  operationId: "getCoursesUsersProgress" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/users/{userId}/progress" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesUsersProgressInput {
  id: string;
  userId: string;
  body?: Types.LearningCoursesUpdateProgress;
}
export type PutCoursesUsersProgressOutput = Types.LearningCoursesUserProgress;
export const putCoursesUsersProgressEndpoint = {
  operationId: "putCoursesUsersProgress" as const,
  method: "PUT" as const,
  path: "/v1/courses/{id}/users/{userId}/progress" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesMeProgressInput {
  id: string;
}
export type GetCoursesMeProgressOutput = Types.LearningCoursesUserProgress;
export const getCoursesMeProgressEndpoint = {
  operationId: "getCoursesMeProgress" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/me/progress" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesMeProgressInput {
  id: string;
  body?: Types.LearningCoursesUpdateProgress;
}
export type PutCoursesMeProgressOutput = Types.LearningCoursesUserProgress;
export const putCoursesMeProgressEndpoint = {
  operationId: "putCoursesMeProgress" as const,
  method: "PUT" as const,
  path: "/v1/courses/{id}/me/progress" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesUsersContentCompleteInput {
  id: string;
  userId: string;
  contentId: string;
}
export type PostCoursesUsersContentCompleteOutput = void;
export const postCoursesUsersContentCompleteEndpoint = {
  operationId: "postCoursesUsersContentComplete" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}/users/{userId}/content/{contentId}:complete" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesMeContentCompleteInput {
  id: string;
  contentId: string;
}
export type PostCoursesMeContentCompleteOutput = void;
export const postCoursesMeContentCompleteEndpoint = {
  operationId: "postCoursesMeContentComplete" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}/me/content/{contentId}:complete" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesUsersResetInput {
  id: string;
  userId: string;
}
export type PostCoursesUsersResetOutput = void;
export const postCoursesUsersResetEndpoint = {
  operationId: "postCoursesUsersReset" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}/users/{userId}:reset" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesMonetizeInput {
  id: string;
  body?: Types.LearningCoursesMonetization;
}
export type PostCoursesMonetizeOutput = Types.LearningCoursesProgram;
export const postCoursesMonetizeEndpoint = {
  operationId: "postCoursesMonetize" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:monetize" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesDisableMonetizationInput {
  id: string;
}
export type PostCoursesDisableMonetizationOutput = Types.LearningCoursesProgram;
export const postCoursesDisableMonetizationEndpoint = {
  operationId: "postCoursesDisableMonetization" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:disable-monetization" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesPricingInput {
  id: string;
}
export type GetCoursesPricingOutput = Types.LearningCoursesPricing;
export const getCoursesPricingEndpoint = {
  operationId: "getCoursesPricing" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/pricing" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PutCoursesPricingInput {
  id: string;
  body?: Types.LearningCoursesUpdatePricing;
}
export type PutCoursesPricingOutput = Types.LearningCoursesPricing;
export const putCoursesPricingEndpoint = {
  operationId: "putCoursesPricing" as const,
  method: "PUT" as const,
  path: "/v1/courses/{id}/pricing" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesAnalyticsInput {
  id: string;
}
export type GetCoursesAnalyticsOutput = Types.LearningCoursesProgramAnalytics;
export const getCoursesAnalyticsEndpoint = {
  operationId: "getCoursesAnalytics" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/analytics" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesAnalyticsCompletionRatesInput {
  id: string;
}
export type GetCoursesAnalyticsCompletionRatesOutput =
  Types.LearningCoursesCompletionRates;
export const getCoursesAnalyticsCompletionRatesEndpoint = {
  operationId: "getCoursesAnalyticsCompletionRates" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/analytics/completion-rates" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesAnalyticsEngagementInput {
  id: string;
}
export type GetCoursesAnalyticsEngagementOutput =
  Types.LearningCoursesEngagementMetrics;
export const getCoursesAnalyticsEngagementEndpoint = {
  operationId: "getCoursesAnalyticsEngagement" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/analytics/engagement" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesAnalyticsRevenueInput {
  id: string;
}
export type GetCoursesAnalyticsRevenueOutput =
  Types.LearningCoursesRevenueAnalytics;
export const getCoursesAnalyticsRevenueEndpoint = {
  operationId: "getCoursesAnalyticsRevenue" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/analytics/revenue" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesCreateProductInput {
  id: string;
  body?: Types.LearningCoursesCreateProductFromProgram;
}
export type PostCoursesCreateProductOutput = string;
export const postCoursesCreateProductEndpoint = {
  operationId: "postCoursesCreateProduct" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:create-product" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesLinkProductInput {
  id: string;
  productId: string;
}
export type PostCoursesLinkProductOutput = void;
export const postCoursesLinkProductEndpoint = {
  operationId: "postCoursesLinkProduct" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:link-product/{productId}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface DeleteCoursesUnlinkProductInput {
  id: string;
  productId: string;
}
export type DeleteCoursesUnlinkProductOutput = void;
export const deleteCoursesUnlinkProductEndpoint = {
  operationId: "deleteCoursesUnlinkProduct" as const,
  method: "DELETE" as const,
  path: "/v1/courses/{id}:unlink-product/{productId}" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface GetCoursesProductsInput {
  id: string;
}
export type GetCoursesProductsOutput = Array<string>;
export const getCoursesProductsEndpoint = {
  operationId: "getCoursesProducts" as const,
  method: "GET" as const,
  path: "/v1/courses/{id}/products" as const,
  tags: ["LearningCoursesProgram"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesSubmitInput {
  id: string;
}
export type PostCoursesSubmitOutput = Types.LearningCoursesProgram;
export const postCoursesSubmitEndpoint = {
  operationId: "postCoursesSubmit" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:submit" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesApproveInput {
  id: string;
}
export type PostCoursesApproveOutput = Types.LearningCoursesProgram;
export const postCoursesApproveEndpoint = {
  operationId: "postCoursesApprove" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:approve" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesRejectInput {
  id: string;
  body?: Types.LearningCoursesRejectProgram;
}
export type PostCoursesRejectOutput = Types.LearningCoursesProgram;
export const postCoursesRejectEndpoint = {
  operationId: "postCoursesReject" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:reject" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesWithdrawInput {
  id: string;
}
export type PostCoursesWithdrawOutput = Types.LearningCoursesProgram;
export const postCoursesWithdrawEndpoint = {
  operationId: "postCoursesWithdraw" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:withdraw" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesArchiveInput {
  id: string;
}
export type PostCoursesArchiveOutput = Types.LearningCoursesProgram;
export const postCoursesArchiveEndpoint = {
  operationId: "postCoursesArchive" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:archive" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesRestoreInput {
  id: string;
}
export type PostCoursesRestoreOutput = Types.LearningCoursesProgram;
export const postCoursesRestoreEndpoint = {
  operationId: "postCoursesRestore" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:restore" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesPublishInput {
  id: string;
}
export type PostCoursesPublishOutput = Types.LearningCoursesProgram;
export const postCoursesPublishEndpoint = {
  operationId: "postCoursesPublish" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:publish" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesUnpublishInput {
  id: string;
}
export type PostCoursesUnpublishOutput = Types.LearningCoursesProgram;
export const postCoursesUnpublishEndpoint = {
  operationId: "postCoursesUnpublish" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:unpublish" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface PostCoursesScheduleInput {
  id: string;
  body?: Types.LearningCoursesScheduleProgram;
}
export type PostCoursesScheduleOutput = Types.LearningCoursesProgram;
export const postCoursesScheduleEndpoint = {
  operationId: "postCoursesSchedule" as const,
  method: "POST" as const,
  path: "/v1/courses/{id}:schedule" as const,
  tags: ["LearningCoursesProgramLifecycle"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsOwnershipInput {
  projectId: string;
}
export type GetProjectsOwnershipOutput = Types.APIProjectsProjectOwnership;
export const getProjectsOwnershipEndpoint = {
  operationId: "getProjectsOwnership" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/ownership" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipTeamsInput {
  projectId: string;
  body?: Types.APIProjectsAddProjectTeamInput;
}
export type PostProjectsOwnershipTeamsOutput =
  Types.APIProjectsProjectTeamOwnership;
export const postProjectsOwnershipTeamsEndpoint = {
  operationId: "postProjectsOwnershipTeams" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/teams" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsOwnershipTeamsInput {
  projectId: string;
  projectTeamId: string;
  body?: Types.APIProjectsUpdateProjectTeamInput;
}
export type PutProjectsOwnershipTeamsOutput =
  Types.APIProjectsProjectTeamOwnership;
export const putProjectsOwnershipTeamsEndpoint = {
  operationId: "putProjectsOwnershipTeams" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/ownership/teams/{projectTeamId}" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsOwnershipTeamsInput {
  projectId: string;
  projectTeamId: string;
}
export type DeleteProjectsOwnershipTeamsOutput = void;
export const deleteProjectsOwnershipTeamsEndpoint = {
  operationId: "deleteProjectsOwnershipTeams" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/ownership/teams/{projectTeamId}" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipOwnerTeamInput {
  projectId: string;
  body?: Types.APIProjectsTransferProjectOwnerTeamInput;
}
export type PostProjectsOwnershipOwnerTeamOutput =
  Types.APIProjectsProjectOwnership;
export const postProjectsOwnershipOwnerTeamEndpoint = {
  operationId: "postProjectsOwnershipOwnerTeam" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/owner-team" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAllocationsInput {
  projectId: string;
  body?: Types.APIProjectsCreateProjectAllocationInput;
}
export type PostProjectsOwnershipAllocationsOutput =
  Types.APIProjectsProjectAllocation;
export const postProjectsOwnershipAllocationsEndpoint = {
  operationId: "postProjectsOwnershipAllocations" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/allocations" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsOwnershipAllocationsInput {
  projectId: string;
  allocationId: string;
  body?: Types.APIProjectsUpdateProjectAllocationInput;
}
export type PutProjectsOwnershipAllocationsOutput =
  Types.APIProjectsProjectAllocation;
export const putProjectsOwnershipAllocationsEndpoint = {
  operationId: "putProjectsOwnershipAllocations" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/ownership/allocations/{allocationId}" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsOwnershipAllocationsInput {
  projectId: string;
  allocationId: string;
}
export type DeleteProjectsOwnershipAllocationsOutput = void;
export const deleteProjectsOwnershipAllocationsEndpoint = {
  operationId: "deleteProjectsOwnershipAllocations" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/ownership/allocations/{allocationId}" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAgreementsInput {
  projectId: string;
  body?: Types.APIProjectsCreateProjectTeamAgreementInput;
}
export type PostProjectsOwnershipAgreementsOutput =
  Types.APIProjectsProjectTeamAgreement;
export const postProjectsOwnershipAgreementsEndpoint = {
  operationId: "postProjectsOwnershipAgreements" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/agreements" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAgreementsCounterInput {
  projectId: string;
  agreementId: string;
  body?: Types.APIProjectsCounterProjectTeamAgreementInput;
}
export type PostProjectsOwnershipAgreementsCounterOutput =
  Types.APIProjectsProjectTeamAgreement;
export const postProjectsOwnershipAgreementsCounterEndpoint = {
  operationId: "postProjectsOwnershipAgreementsCounter" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/agreements/{agreementId}/counter" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAgreementsAcceptInput {
  projectId: string;
  agreementId: string;
}
export type PostProjectsOwnershipAgreementsAcceptOutput =
  Types.APIProjectsProjectTeamAgreement;
export const postProjectsOwnershipAgreementsAcceptEndpoint = {
  operationId: "postProjectsOwnershipAgreementsAccept" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/agreements/{agreementId}/accept" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAgreementsCancelInput {
  projectId: string;
  agreementId: string;
}
export type PostProjectsOwnershipAgreementsCancelOutput =
  Types.APIProjectsProjectTeamAgreement;
export const postProjectsOwnershipAgreementsCancelEndpoint = {
  operationId: "postProjectsOwnershipAgreementsCancel" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/agreements/{agreementId}/cancel" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsOwnershipAgreementsCompleteInput {
  projectId: string;
  agreementId: string;
}
export type PostProjectsOwnershipAgreementsCompleteOutput =
  Types.APIProjectsProjectTeamAgreement;
export const postProjectsOwnershipAgreementsCompleteEndpoint = {
  operationId: "postProjectsOwnershipAgreementsComplete" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/ownership/agreements/{agreementId}/complete" as const,
  tags: ["ApiProjectsOwnership"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsPermissionsMyPermissionsInput {
  projectId: string;
}
export type GetProjectsPermissionsMyPermissionsOutput =
  Array<Types.ProjectsEffectivePermission>;
export const getProjectsPermissionsMyPermissionsEndpoint = {
  operationId: "getProjectsPermissionsMyPermissions" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/permissions/my-permissions" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsPermissionsCollaboratorsInput {
  projectId: string;
}
export type GetProjectsPermissionsCollaboratorsOutput =
  Array<Types.ProjectsProjectCollaboratorDto>;
export const getProjectsPermissionsCollaboratorsEndpoint = {
  operationId: "getProjectsPermissionsCollaborators" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/permissions/collaborators" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsPermissionsCollaboratorsInput {
  projectId: string;
  body?: Types.ProjectsAddCollaboratorInput;
}
export type PostProjectsPermissionsCollaboratorsOutput =
  Types.ProjectsInvitationResult;
export const postProjectsPermissionsCollaboratorsEndpoint = {
  operationId: "postProjectsPermissionsCollaborators" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/permissions/collaborators" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsPermissionsCollaboratorsInput {
  projectId: string;
  collaboratorUserId: string;
  body?: Types.ProjectsUpdateCollaboratorInput;
}
export type PutProjectsPermissionsCollaboratorsOutput =
  Types.ProjectsPermissionUpdateResult;
export const putProjectsPermissionsCollaboratorsEndpoint = {
  operationId: "putProjectsPermissionsCollaborators" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/permissions/collaborators/{collaboratorUserId}" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsPermissionsCollaboratorsInput {
  projectId: string;
  collaboratorUserId: string;
}
export type DeleteProjectsPermissionsCollaboratorsOutput =
  Types.ProjectsPermissionUpdateResult;
export const deleteProjectsPermissionsCollaboratorsEndpoint = {
  operationId: "deleteProjectsPermissionsCollaborators" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/permissions/collaborators/{collaboratorUserId}" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsPermissionsRoleTemplatesInput {
  projectId: string;
}
export type GetProjectsPermissionsRoleTemplatesOutput =
  Array<Types.ProjectsProjectRoleTemplate>;
export const getProjectsPermissionsRoleTemplatesEndpoint = {
  operationId: "getProjectsPermissionsRoleTemplates" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/permissions/role-templates" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsPermissionsShareWithRoleInput {
  projectId: string;
  body?: Types.ProjectsShareProjectWithRoleInput;
}
export type PostProjectsPermissionsShareWithRoleOutput =
  Types.ProjectsShareResult;
export const postProjectsPermissionsShareWithRoleEndpoint = {
  operationId: "postProjectsPermissionsShareWithRole" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/permissions/:share-with-role" as const,
  tags: ["ProjectsPermission"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsForGetProjectsInput {
  query?: {
    type?: Types.ProjectsProjectType;
    status?: Types.ContentStatus;
    visibility?: Types.ContentVisibility;
    creatorId?: string;
    categoryId?: string;
    searchTerm?: string;
    featured?: boolean;
    popular?: boolean;
    recent?: boolean;
    currentTenantOnly?: boolean;
    includeArchived?: boolean;
    skip?: number;
    take?: number;
    sortBy?: string;
    sortDirection?: string;
  };
}
export type GetProjectsForGetProjectsOutput =
  Array<Types.ProjectsProjectApiOutput>;
export const getProjectsForGetProjectsEndpoint = {
  operationId: "getProjectsForGetProjects" as const,
  method: "GET" as const,
  path: "/v1/projects" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsInput {
  body?: Types.ProjectsCreateProjectInput;
}
export type PostProjectsOutput = Types.ProjectsProjectApiOutput;
export const postProjectsEndpoint = {
  operationId: "postProjects" as const,
  method: "POST" as const,
  path: "/v1/projects" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsMineInput {
  query?: {
    includeArchived?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetProjectsMineOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsMineEndpoint = {
  operationId: "getProjectsMine" as const,
  method: "GET" as const,
  path: "/v1/projects/mine" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsAccessibleVersionsInput {
  query?: {
    take?: number;
  };
}
export type GetProjectsAccessibleVersionsOutput =
  Array<Types.ProjectsProjectVersionOptionProjection>;
export const getProjectsAccessibleVersionsEndpoint = {
  operationId: "getProjectsAccessibleVersions" as const,
  method: "GET" as const,
  path: "/v1/projects/accessible-versions" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsForGetProjectsByIdInput {
  id: string;
  query?: {
    includeTeam?: boolean;
    includeReleases?: boolean;
    includeCollaborators?: boolean;
    includeStatistics?: boolean;
  };
}
export type GetProjectsForGetProjectsByIdOutput =
  Types.ProjectsProjectApiOutput;
export const getProjectsForGetProjectsByIdEndpoint = {
  operationId: "getProjectsForGetProjectsById" as const,
  method: "GET" as const,
  path: "/v1/projects/{id}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsInput {
  id: string;
  body?: Types.ProjectsUpdateProjectInput;
}
export type PutProjectsOutput = Types.ProjectsProjectApiOutput;
export const putProjectsEndpoint = {
  operationId: "putProjects" as const,
  method: "PUT" as const,
  path: "/v1/projects/{id}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsInput {
  id: string;
  query?: {
    softDelete?: boolean;
    reason?: string;
  };
}
export type DeleteProjectsOutput = boolean;
export const deleteProjectsEndpoint = {
  operationId: "deleteProjects" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{id}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsVersionsInput {
  id: string;
}
export type GetProjectsVersionsOutput =
  Array<Types.ProjectsProjectVersionApiOutput>;
export const getProjectsVersionsEndpoint = {
  operationId: "getProjectsVersions" as const,
  method: "GET" as const,
  path: "/v1/projects/{id}/versions" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsVersionsInput {
  id: string;
  body?: Types.ProjectsCreateProjectVersionInput;
}
export type PostProjectsVersionsOutput = Types.ProjectsProjectVersionApiOutput;
export const postProjectsVersionsEndpoint = {
  operationId: "postProjectsVersions" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}/versions" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsSlugInput {
  slug: string;
  query?: {
    includeTeam?: boolean;
    includeReleases?: boolean;
    includeCollaborators?: boolean;
  };
}
export type GetProjectsSlugOutput = Types.ProjectsProjectApiOutput;
export const getProjectsSlugEndpoint = {
  operationId: "getProjectsSlug" as const,
  method: "GET" as const,
  path: "/v1/projects/slug/{slug}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsPublishInput {
  id: string;
}
export type PostProjectsPublishOutput = Types.ProjectsProjectApiOutput;
export const postProjectsPublishEndpoint = {
  operationId: "postProjectsPublish" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}:publish" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsUnpublishInput {
  id: string;
}
export type PostProjectsUnpublishOutput = Types.ProjectsProjectApiOutput;
export const postProjectsUnpublishEndpoint = {
  operationId: "postProjectsUnpublish" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}:unpublish" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsArchiveInput {
  id: string;
}
export type PostProjectsArchiveOutput = Types.ProjectsProjectApiOutput;
export const postProjectsArchiveEndpoint = {
  operationId: "postProjectsArchive" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}:archive" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsRestoreInput {
  id: string;
}
export type PostProjectsRestoreOutput = Types.ProjectsProjectApiOutput;
export const postProjectsRestoreEndpoint = {
  operationId: "postProjectsRestore" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}:restore" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsSearchInput {
  query?: {
    searchTerm?: string;
    type?: Types.ProjectsProjectType;
    categoryId?: string;
    status?: Types.ContentStatus;
    visibility?: Types.ContentVisibility;
    skip?: number;
    take?: number;
    sortBy?: string;
    sortDirection?: string;
  };
}
export type GetProjectsSearchOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsSearchEndpoint = {
  operationId: "getProjectsSearch" as const,
  method: "GET" as const,
  path: "/v1/projects/search" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsPopularInput {
  query?: {
    type?: Types.ProjectsProjectType;
    take?: number;
  };
}
export type GetProjectsPopularOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsPopularEndpoint = {
  operationId: "getProjectsPopular" as const,
  method: "GET" as const,
  path: "/v1/projects/popular" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsRecentInput {
  query?: {
    type?: Types.ProjectsProjectType;
    take?: number;
  };
}
export type GetProjectsRecentOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsRecentEndpoint = {
  operationId: "getProjectsRecent" as const,
  method: "GET" as const,
  path: "/v1/projects/recent" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsFeaturedInput {
  query?: {
    type?: Types.ProjectsProjectType;
    take?: number;
  };
}
export type GetProjectsFeaturedOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsFeaturedEndpoint = {
  operationId: "getProjectsFeatured" as const,
  method: "GET" as const,
  path: "/v1/projects/featured" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsStatisticsInput {
  id: string;
  query?: {
    fromDate?: string;
    toDate?: string;
  };
}
export type GetProjectsStatisticsOutput = Types.ProjectsProjectStatistics;
export const getProjectsStatisticsEndpoint = {
  operationId: "getProjectsStatistics" as const,
  method: "GET" as const,
  path: "/v1/projects/{id}/statistics" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsCategoryInput {
  categoryId: string;
  query?: {
    status?: Types.ContentStatus;
    skip?: number;
    take?: number;
  };
}
export type GetProjectsCategoryOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsCategoryEndpoint = {
  operationId: "getProjectsCategory" as const,
  method: "GET" as const,
  path: "/v1/projects/category/{categoryId}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsCreatorInput {
  creatorId: string;
  query?: {
    status?: Types.ContentStatus;
    skip?: number;
    take?: number;
  };
}
export type GetProjectsCreatorOutput = Array<Types.ProjectsProjectApiOutput>;
export const getProjectsCreatorEndpoint = {
  operationId: "getProjectsCreator" as const,
  method: "GET" as const,
  path: "/v1/projects/creator/{creatorId}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export type GetProjectsRoleTemplatesInput = void;
export type GetProjectsRoleTemplatesOutput = Array<Record<string, unknown>>;
export const getProjectsRoleTemplatesEndpoint = {
  operationId: "getProjectsRoleTemplates" as const,
  method: "GET" as const,
  path: "/v1/projects/role-templates" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export type GetProjectsMyInvitationsInput = void;
export type GetProjectsMyInvitationsOutput =
  Array<Types.ProjectsProjectInvitation>;
export const getProjectsMyInvitationsEndpoint = {
  operationId: "getProjectsMyInvitations" as const,
  method: "GET" as const,
  path: "/v1/projects/my-invitations" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsRolesPermissionsInput {
  roleName: string;
}
export type GetProjectsRolesPermissionsOutput =
  Array<Types.IdentityAuthorizationPermissionType>;
export const getProjectsRolesPermissionsEndpoint = {
  operationId: "getProjectsRolesPermissions" as const,
  method: "GET" as const,
  path: "/v1/projects/roles/{roleName}/permissions" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsInvitationsAcceptInput {
  invitationToken: string;
}
export type PostProjectsInvitationsAcceptOutput =
  Types.ProjectsProjectInvitation;
export const postProjectsInvitationsAcceptEndpoint = {
  operationId: "postProjectsInvitationsAccept" as const,
  method: "POST" as const,
  path: "/v1/projects/invitations/{invitationToken}:accept" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsInvitationsDeclineInput {
  invitationToken: string;
}
export type PostProjectsInvitationsDeclineOutput =
  Types.ProjectsProjectInvitation;
export const postProjectsInvitationsDeclineEndpoint = {
  operationId: "postProjectsInvitationsDecline" as const,
  method: "POST" as const,
  path: "/v1/projects/invitations/{invitationToken}:decline" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsCollaboratorsInput {
  id: string;
}
export type GetProjectsCollaboratorsOutput = Array<Types.ProjectsCollaborator>;
export const getProjectsCollaboratorsEndpoint = {
  operationId: "getProjectsCollaborators" as const,
  method: "GET" as const,
  path: "/v1/projects/{id}/collaborators" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsCollaboratorsInput {
  id: string;
  body?: Types.ProjectsAddProjectCollaboratorInput;
}
export type PostProjectsCollaboratorsOutput = Types.ProjectsCollaborator;
export const postProjectsCollaboratorsEndpoint = {
  operationId: "postProjectsCollaborators" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}/collaborators" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsCollaboratorsInput {
  id: string;
  collaboratorId: string;
  body?: Types.ProjectsUpdateProjectCollaboratorInput;
}
export type PutProjectsCollaboratorsOutput = Types.ProjectsCollaborator;
export const putProjectsCollaboratorsEndpoint = {
  operationId: "putProjectsCollaborators" as const,
  method: "PUT" as const,
  path: "/v1/projects/{id}/collaborators/{collaboratorId}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsCollaboratorsInput {
  id: string;
  collaboratorId: string;
}
export type DeleteProjectsCollaboratorsOutput = void;
export const deleteProjectsCollaboratorsEndpoint = {
  operationId: "deleteProjectsCollaborators" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{id}/collaborators/{collaboratorId}" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsShareInput {
  id: string;
  body?: Types.ProjectsShareProjectInput;
}
export type PostProjectsShareOutput = Types.ProjectsCollaborator;
export const postProjectsShareEndpoint = {
  operationId: "postProjectsShare" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}:share" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsInvitationsInput {
  id: string;
  body?: Types.ProjectsInviteProjectCollaboratorInput;
}
export type PostProjectsInvitationsOutput = Types.ProjectsProjectInvitation;
export const postProjectsInvitationsEndpoint = {
  operationId: "postProjectsInvitations" as const,
  method: "POST" as const,
  path: "/v1/projects/{id}/invitations" as const,
  tags: ["Projects"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsStoreProductsInput {
  projectId: string;
}
export type GetProjectsStoreProductsOutput =
  Array<Types.ProjectsProjectStoreProductProjection>;
export const getProjectsStoreProductsEndpoint = {
  operationId: "getProjectsStoreProducts" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/store-products" as const,
  tags: ["ProjectsStoreProducts"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsStoreProductsInput {
  projectId: string;
  body?: Types.ProjectsLinkProjectStoreProductInput;
}
export type PostProjectsStoreProductsOutput =
  Types.ProjectsProjectStoreProductProjection;
export const postProjectsStoreProductsEndpoint = {
  operationId: "postProjectsStoreProducts" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/store-products" as const,
  tags: ["ProjectsStoreProducts"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsStoreProductsInput {
  projectId: string;
  productId: string;
}
export type DeleteProjectsStoreProductsOutput = void;
export const deleteProjectsStoreProductsEndpoint = {
  operationId: "deleteProjectsStoreProducts" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/store-products/{productId}" as const,
  tags: ["ProjectsStoreProducts"] as const,
  requiresAuth: true,
} as const;

export interface GetStoreProductsProjectsInput {
  productId: string;
}
export type GetStoreProductsProjectsOutput =
  Array<Types.ProjectsProjectStoreProductProjection>;
export const getStoreProductsProjectsEndpoint = {
  operationId: "getStoreProductsProjects" as const,
  method: "GET" as const,
  path: "/v1/store/products/{productId}/projects" as const,
  tags: ["ProjectsStoreProducts"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsWorkInput {
  projectId: string;
}
export type GetProjectsWorkOutput = Types.APIProjectWorkProjectBoard;
export const getProjectsWorkEndpoint = {
  operationId: "getProjectsWork" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/work" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkColumnsInput {
  projectId: string;
  body?: Types.APIProjectWorkConfigureProjectWorkColumnInput;
}
export type PostProjectsWorkColumnsOutput =
  Types.APIProjectWorkProjectWorkColumn;
export const postProjectsWorkColumnsEndpoint = {
  operationId: "postProjectsWorkColumns" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/columns" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkColumnsInput {
  projectId: string;
  columnId: string;
  body?: Types.APIProjectWorkConfigureProjectWorkColumnInput;
}
export type PutProjectsWorkColumnsOutput =
  Types.APIProjectWorkProjectWorkColumn;
export const putProjectsWorkColumnsEndpoint = {
  operationId: "putProjectsWorkColumns" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/columns/{columnId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkColumnsInput {
  projectId: string;
  columnId: string;
}
export type DeleteProjectsWorkColumnsOutput = void;
export const deleteProjectsWorkColumnsEndpoint = {
  operationId: "deleteProjectsWorkColumns" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/columns/{columnId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsWorkMilestonesInput {
  projectId: string;
}
export type GetProjectsWorkMilestonesOutput =
  Array<Types.APIProjectWorkProjectMilestone>;
export const getProjectsWorkMilestonesEndpoint = {
  operationId: "getProjectsWorkMilestones" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/work/milestones" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkMilestonesInput {
  projectId: string;
  body?: Types.APIProjectWorkCreateProjectMilestoneInput;
}
export type PostProjectsWorkMilestonesOutput =
  Types.APIProjectWorkProjectMilestone;
export const postProjectsWorkMilestonesEndpoint = {
  operationId: "postProjectsWorkMilestones" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/milestones" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkMilestonesInput {
  projectId: string;
  milestoneId: string;
  body?: Types.APIProjectWorkUpdateProjectMilestoneInput;
}
export type PutProjectsWorkMilestonesOutput =
  Types.APIProjectWorkProjectMilestone;
export const putProjectsWorkMilestonesEndpoint = {
  operationId: "putProjectsWorkMilestones" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/milestones/{milestoneId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkMilestonesInput {
  projectId: string;
  milestoneId: string;
}
export type DeleteProjectsWorkMilestonesOutput = void;
export const deleteProjectsWorkMilestonesEndpoint = {
  operationId: "deleteProjectsWorkMilestones" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/milestones/{milestoneId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkTasksInput {
  projectId: string;
  body?: Types.APIProjectWorkCreateProjectWorkTaskInput;
}
export type PostProjectsWorkTasksOutput = Types.APIProjectWorkProjectWorkTask;
export const postProjectsWorkTasksEndpoint = {
  operationId: "postProjectsWorkTasks" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/tasks" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsWorkTasksInput {
  projectId: string;
  taskId: string;
}
export type GetProjectsWorkTasksOutput =
  Types.APIProjectWorkProjectWorkTaskDetails;
export const getProjectsWorkTasksEndpoint = {
  operationId: "getProjectsWorkTasks" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkTasksInput {
  projectId: string;
  taskId: string;
  body?: Types.APIProjectWorkUpdateProjectWorkTaskInput;
}
export type PutProjectsWorkTasksOutput = Types.APIProjectWorkProjectWorkTask;
export const putProjectsWorkTasksEndpoint = {
  operationId: "putProjectsWorkTasks" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkTasksInput {
  projectId: string;
  taskId: string;
}
export type DeleteProjectsWorkTasksOutput = void;
export const deleteProjectsWorkTasksEndpoint = {
  operationId: "deleteProjectsWorkTasks" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkTasksMoveInput {
  projectId: string;
  taskId: string;
  body?: Types.APIProjectWorkMoveProjectWorkTaskInput;
}
export type PutProjectsWorkTasksMoveOutput =
  Types.APIProjectWorkProjectWorkTask;
export const putProjectsWorkTasksMoveEndpoint = {
  operationId: "putProjectsWorkTasksMove" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/move" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkTasksDependenciesInput {
  projectId: string;
  taskId: string;
  body?: Types.APIProjectWorkAddProjectTaskDependencyInput;
}
export type PostProjectsWorkTasksDependenciesOutput = void;
export const postProjectsWorkTasksDependenciesEndpoint = {
  operationId: "postProjectsWorkTasksDependencies" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/dependencies" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkTasksDependenciesInput {
  projectId: string;
  taskId: string;
  dependencyId: string;
}
export type DeleteProjectsWorkTasksDependenciesOutput = void;
export const deleteProjectsWorkTasksDependenciesEndpoint = {
  operationId: "deleteProjectsWorkTasksDependencies" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/dependencies/{dependencyId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsWorkLabelsInput {
  projectId: string;
}
export type GetProjectsWorkLabelsOutput =
  Array<Types.APIProjectWorkProjectTaskLabel>;
export const getProjectsWorkLabelsEndpoint = {
  operationId: "getProjectsWorkLabels" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/work/labels" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkLabelsInput {
  projectId: string;
  body?: Types.APIProjectWorkCreateProjectTaskLabelInput;
}
export type PostProjectsWorkLabelsOutput = Types.APIProjectWorkProjectTaskLabel;
export const postProjectsWorkLabelsEndpoint = {
  operationId: "postProjectsWorkLabels" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/labels" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkLabelsInput {
  projectId: string;
  labelId: string;
}
export type DeleteProjectsWorkLabelsOutput = void;
export const deleteProjectsWorkLabelsEndpoint = {
  operationId: "deleteProjectsWorkLabels" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/labels/{labelId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkTasksLabelsInput {
  projectId: string;
  taskId: string;
  labelId: string;
}
export type PostProjectsWorkTasksLabelsOutput = void;
export const postProjectsWorkTasksLabelsEndpoint = {
  operationId: "postProjectsWorkTasksLabels" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/labels/{labelId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkTasksLabelsInput {
  projectId: string;
  taskId: string;
  labelId: string;
}
export type DeleteProjectsWorkTasksLabelsOutput = void;
export const deleteProjectsWorkTasksLabelsEndpoint = {
  operationId: "deleteProjectsWorkTasksLabels" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/labels/{labelId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkTasksCommentsInput {
  projectId: string;
  taskId: string;
  body?: Types.APIProjectWorkAddProjectTaskCommentInput;
}
export type PostProjectsWorkTasksCommentsOutput = void;
export const postProjectsWorkTasksCommentsEndpoint = {
  operationId: "postProjectsWorkTasksComments" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/comments" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkTasksCommentsInput {
  projectId: string;
  taskId: string;
  commentId: string;
  body?: Types.APIProjectWorkUpdateProjectTaskCommentInput;
}
export type PutProjectsWorkTasksCommentsOutput =
  Types.APIProjectWorkProjectTaskComment;
export const putProjectsWorkTasksCommentsEndpoint = {
  operationId: "putProjectsWorkTasksComments" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/comments/{commentId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkTasksCommentsInput {
  projectId: string;
  taskId: string;
  commentId: string;
}
export type DeleteProjectsWorkTasksCommentsOutput = void;
export const deleteProjectsWorkTasksCommentsEndpoint = {
  operationId: "deleteProjectsWorkTasksComments" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/comments/{commentId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PostProjectsWorkTasksChecklistInput {
  projectId: string;
  taskId: string;
  body?: Types.APIProjectWorkAddProjectTaskChecklistInput;
}
export type PostProjectsWorkTasksChecklistOutput = void;
export const postProjectsWorkTasksChecklistEndpoint = {
  operationId: "postProjectsWorkTasksChecklist" as const,
  method: "POST" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/checklist" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface PutProjectsWorkTasksChecklistInput {
  projectId: string;
  taskId: string;
  itemId: string;
  body?: Types.APIProjectWorkUpdateProjectTaskChecklistInput;
}
export type PutProjectsWorkTasksChecklistOutput =
  Types.APIProjectWorkProjectChecklistItem;
export const putProjectsWorkTasksChecklistEndpoint = {
  operationId: "putProjectsWorkTasksChecklist" as const,
  method: "PUT" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/checklist/{itemId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface DeleteProjectsWorkTasksChecklistInput {
  projectId: string;
  taskId: string;
  itemId: string;
}
export type DeleteProjectsWorkTasksChecklistOutput = void;
export const deleteProjectsWorkTasksChecklistEndpoint = {
  operationId: "deleteProjectsWorkTasksChecklist" as const,
  method: "DELETE" as const,
  path: "/v1/projects/{projectId}/work/tasks/{taskId}/checklist/{itemId}" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface GetProjectsWorkHistoryInput {
  projectId: string;
  query?: {
    take?: number;
  };
}
export type GetProjectsWorkHistoryOutput =
  Array<Types.APIProjectWorkProjectWorkHistory>;
export const getProjectsWorkHistoryEndpoint = {
  operationId: "getProjectsWorkHistory" as const,
  method: "GET" as const,
  path: "/v1/projects/{projectId}/work/history" as const,
  tags: ["ApiProjectWork"] as const,
  requiresAuth: true,
} as const;

export interface GetPromoCodesForGetPromoCodesInput {
  query?: {
    status?: string;
    isActive?: boolean;
    type?: Types.CommerceProductsPromoCodeType;
    productId?: string;
    searchTerm?: string;
    skip?: number;
    take?: number;
  };
}
export type GetPromoCodesForGetPromoCodesOutput =
  Types.PagedResultOfCommerceProductsPromoCode;
export const getPromoCodesForGetPromoCodesEndpoint = {
  operationId: "getPromoCodesForGetPromoCodes" as const,
  method: "GET" as const,
  path: "/v1/promo-codes" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PostPromoCodesInput {
  body?: Types.CommerceProductsCreatePromoCodeInput;
}
export type PostPromoCodesOutput = Types.CommerceProductsPromoCode;
export const postPromoCodesEndpoint = {
  operationId: "postPromoCodes" as const,
  method: "POST" as const,
  path: "/v1/promo-codes" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface GetPromoCodesForGetPromoCodesByPromoCodeIdInput {
  promoCodeId: string;
}
export type GetPromoCodesForGetPromoCodesByPromoCodeIdOutput =
  Types.CommerceProductsPromoCode;
export const getPromoCodesForGetPromoCodesByPromoCodeIdEndpoint = {
  operationId: "getPromoCodesForGetPromoCodesByPromoCodeId" as const,
  method: "GET" as const,
  path: "/v1/promo-codes/{promoCodeId}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PutPromoCodesInput {
  promoCodeId: string;
  body?: Types.CommerceProductsUpdatePromoCodeInput;
}
export type PutPromoCodesOutput = Types.CommerceProductsPromoCode;
export const putPromoCodesEndpoint = {
  operationId: "putPromoCodes" as const,
  method: "PUT" as const,
  path: "/v1/promo-codes/{promoCodeId}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface DeletePromoCodesInput {
  promoCodeId: string;
}
export type DeletePromoCodesOutput = void;
export const deletePromoCodesEndpoint = {
  operationId: "deletePromoCodes" as const,
  method: "DELETE" as const,
  path: "/v1/promo-codes/{promoCodeId}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PatchPromoCodesInput {
  promoCodeId: string;
  body?: Types.CommerceProductsPatchPromoCodeInput;
}
export type PatchPromoCodesOutput = Types.CommerceProductsPromoCode;
export const patchPromoCodesEndpoint = {
  operationId: "patchPromoCodes" as const,
  method: "PATCH" as const,
  path: "/v1/promo-codes/{promoCodeId}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface HeadPromoCodesInput {
  promoCodeId: string;
}
export type HeadPromoCodesOutput = void;
export const headPromoCodesEndpoint = {
  operationId: "headPromoCodes" as const,
  method: "HEAD" as const,
  path: "/v1/promo-codes/{promoCodeId}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface GetPromoCodesByCodeInput {
  code: string;
}
export type GetPromoCodesByCodeOutput = Types.CommerceProductsPromoCode;
export const getPromoCodesByCodeEndpoint = {
  operationId: "getPromoCodesByCode" as const,
  method: "GET" as const,
  path: "/v1/promo-codes/by-code/{code}" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface GetPromoCodesUsageInput {
  promoCodeId: string;
}
export type GetPromoCodesUsageOutput = Types.CommerceProductsPromoCodeUsage;
export const getPromoCodesUsageEndpoint = {
  operationId: "getPromoCodesUsage" as const,
  method: "GET" as const,
  path: "/v1/promo-codes/{promoCodeId}/usage" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PostPromoCodesActivateInput {
  promoCodeId: string;
}
export type PostPromoCodesActivateOutput = Types.CommerceProductsPromoCode;
export const postPromoCodesActivateEndpoint = {
  operationId: "postPromoCodesActivate" as const,
  method: "POST" as const,
  path: "/v1/promo-codes/{promoCodeId}:activate" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PostPromoCodesDeactivateInput {
  promoCodeId: string;
}
export type PostPromoCodesDeactivateOutput = Types.CommerceProductsPromoCode;
export const postPromoCodesDeactivateEndpoint = {
  operationId: "postPromoCodesDeactivate" as const,
  method: "POST" as const,
  path: "/v1/promo-codes/{promoCodeId}:deactivate" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PostPromoCodesValidateInput {
  body?: Types.CommerceProductsValidatePromoCodeInput;
}
export type PostPromoCodesValidateOutput =
  Types.CommerceProductsPromoCodeValidationResult;
export const postPromoCodesValidateEndpoint = {
  operationId: "postPromoCodesValidate" as const,
  method: "POST" as const,
  path: "/v1/promo-codes/:validate" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface PostPromoCodesApplyInput {
  body?: Types.CommerceProductsApplyPromoCodesInput;
}
export type PostPromoCodesApplyOutput =
  Types.CommerceProductsPromoCodeApplicationResult;
export const postPromoCodesApplyEndpoint = {
  operationId: "postPromoCodesApply" as const,
  method: "POST" as const,
  path: "/v1/promo-codes/:apply" as const,
  tags: ["CommerceProductsPromoCodes"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialReactionsTargetInput {
  targetType: Types.SocialReactionsReactionTargetType;
  targetId: string;
}
export type GetApiSocialReactionsTargetOutput =
  Types.SocialReactionsTargetReactionSummary;
export const getApiSocialReactionsTargetEndpoint = {
  operationId: "getApiSocialReactionsTarget" as const,
  method: "GET" as const,
  path: "/api/social/reactions/target/{targetType}/{targetId}" as const,
  tags: ["SocialReactions"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialReactionsUsersTargetInput {
  userId: string;
  targetType: Types.SocialReactionsReactionTargetType;
  targetId: string;
}
export type GetApiSocialReactionsUsersTargetOutput =
  Types.SocialReactionsReaction;
export const getApiSocialReactionsUsersTargetEndpoint = {
  operationId: "getApiSocialReactionsUsersTarget" as const,
  method: "GET" as const,
  path: "/api/social/reactions/users/{userId}/target/{targetType}/{targetId}" as const,
  tags: ["SocialReactions"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialReactionsInput {
  body?: Types.SocialReactionsSetReactionInput;
}
export type PutApiSocialReactionsOutput = Types.SocialReactionsReaction;
export const putApiSocialReactionsEndpoint = {
  operationId: "putApiSocialReactions" as const,
  method: "PUT" as const,
  path: "/api/social/reactions" as const,
  tags: ["SocialReactions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialReactionsInput {
  body?: Types.SocialReactionsRemoveReactionInput;
}
export type DeleteApiSocialReactionsOutput = void;
export const deleteApiSocialReactionsEndpoint = {
  operationId: "deleteApiSocialReactions" as const,
  method: "DELETE" as const,
  path: "/api/social/reactions" as const,
  tags: ["SocialReactions"] as const,
  requiresAuth: true,
} as const;

export interface GetRecommendationsMeInput {
  query?: {
    tenantId?: string;
    type?: Types.LearningExperienceRecommendationsRecommendationType;
    includeViewed?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetRecommendationsMeOutput =
  Array<Types.LearningExperienceRecommendationsRecommendation>;
export const getRecommendationsMeEndpoint = {
  operationId: "getRecommendationsMe" as const,
  method: "GET" as const,
  path: "/v1/recommendations/me" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PostRecommendationsMeGenerateInput {
  query?: {
    tenantId?: string;
    maxResults?: number;
  };
}
export type PostRecommendationsMeGenerateOutput =
  Array<Types.LearningExperienceRecommendationsRecommendation>;
export const postRecommendationsMeGenerateEndpoint = {
  operationId: "postRecommendationsMeGenerate" as const,
  method: "POST" as const,
  path: "/v1/recommendations/me/generate" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PostRecommendationsViewedInput {
  id: string;
}
export type PostRecommendationsViewedOutput = void;
export const postRecommendationsViewedEndpoint = {
  operationId: "postRecommendationsViewed" as const,
  method: "POST" as const,
  path: "/v1/recommendations/{id}/viewed" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PostRecommendationsDismissInput {
  id: string;
}
export type PostRecommendationsDismissOutput = void;
export const postRecommendationsDismissEndpoint = {
  operationId: "postRecommendationsDismiss" as const,
  method: "POST" as const,
  path: "/v1/recommendations/{id}/dismiss" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PostRecommendationsMeRefreshInput {
  query?: {
    tenantId?: string;
  };
}
export type PostRecommendationsMeRefreshOutput = void;
export const postRecommendationsMeRefreshEndpoint = {
  operationId: "postRecommendationsMeRefresh" as const,
  method: "POST" as const,
  path: "/v1/recommendations/me/refresh" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export type GetRecommendationsMeStatisticsInput = void;
export type GetRecommendationsMeStatisticsOutput =
  Types.LearningExperienceRecommendationsRecommendationStatistics;
export const getRecommendationsMeStatisticsEndpoint = {
  operationId: "getRecommendationsMeStatistics" as const,
  method: "GET" as const,
  path: "/v1/recommendations/me/statistics" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export type GetRecommendationsMeProfileInput = void;
export type GetRecommendationsMeProfileOutput =
  Types.LearningExperienceRecommendationsUserLearningProfile;
export const getRecommendationsMeProfileEndpoint = {
  operationId: "getRecommendationsMeProfile" as const,
  method: "GET" as const,
  path: "/v1/recommendations/me/profile" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PutRecommendationsMeProfileInput {
  body?: Types.LearningExperienceRecommendationsCreateOrUpdateLearningProfile;
}
export type PutRecommendationsMeProfileOutput =
  Types.LearningExperienceRecommendationsUserLearningProfile;
export const putRecommendationsMeProfileEndpoint = {
  operationId: "putRecommendationsMeProfile" as const,
  method: "PUT" as const,
  path: "/v1/recommendations/me/profile" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface PostRecommendationsMeProfileSkillsInput {
  body?: Types.LearningExperienceRecommendationsAddSkillInput;
}
export type PostRecommendationsMeProfileSkillsOutput =
  Types.LearningExperienceRecommendationsUserLearningProfile;
export const postRecommendationsMeProfileSkillsEndpoint = {
  operationId: "postRecommendationsMeProfileSkills" as const,
  method: "POST" as const,
  path: "/v1/recommendations/me/profile/skills" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface DeleteRecommendationsMeProfileSkillsInput {
  skill: string;
}
export type DeleteRecommendationsMeProfileSkillsOutput =
  Types.LearningExperienceRecommendationsUserLearningProfile;
export const deleteRecommendationsMeProfileSkillsEndpoint = {
  operationId: "deleteRecommendationsMeProfileSkills" as const,
  method: "DELETE" as const,
  path: "/v1/recommendations/me/profile/skills/{skill}" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface GetRecommendationsPopularInput {
  query?: {
    tenantId?: string;
    category?: string;
    skip?: number;
    take?: number;
  };
}
export type GetRecommendationsPopularOutput =
  Array<Types.LearningExperienceRecommendationsPopularCourse>;
export const getRecommendationsPopularEndpoint = {
  operationId: "getRecommendationsPopular" as const,
  method: "GET" as const,
  path: "/v1/recommendations/popular" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface GetRecommendationsTrendingInput {
  query?: {
    tenantId?: string;
    daysWindow?: number;
    skip?: number;
    take?: number;
  };
}
export type GetRecommendationsTrendingOutput =
  Array<Types.LearningExperienceRecommendationsTrendingCourse>;
export const getRecommendationsTrendingEndpoint = {
  operationId: "getRecommendationsTrending" as const,
  method: "GET" as const,
  path: "/v1/recommendations/trending" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface GetRecommendationsCoursesSimilarInput {
  courseId: string;
  query?: {
    tenantId?: string;
    maxResults?: number;
  };
}
export type GetRecommendationsCoursesSimilarOutput =
  Array<Types.LearningExperienceRecommendationsSimilarCourse>;
export const getRecommendationsCoursesSimilarEndpoint = {
  operationId: "getRecommendationsCoursesSimilar" as const,
  method: "GET" as const,
  path: "/v1/recommendations/courses/{courseId}/similar" as const,
  tags: ["LearningExperienceRecommendations"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialDiscussionsRepliesInput {
  discussionId: string;
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialDiscussionsRepliesOutput =
  Array<Types.LearningExperienceSocialServicesDiscussionReply>;
export const getApiSocialDiscussionsRepliesEndpoint = {
  operationId: "getApiSocialDiscussionsReplies" as const,
  method: "GET" as const,
  path: "/api/social/discussions/{discussionId}/replies" as const,
  tags: ["LearningExperienceSocialReplies"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialDiscussionsRepliesInput {
  discussionId: string;
  body?: Types.LearningExperienceSocialServicesCreateReplyInput;
}
export type PostApiSocialDiscussionsRepliesOutput =
  Types.LearningExperienceSocialServicesDiscussionReply;
export const postApiSocialDiscussionsRepliesEndpoint = {
  operationId: "postApiSocialDiscussionsReplies" as const,
  method: "POST" as const,
  path: "/api/social/discussions/{discussionId}/replies" as const,
  tags: ["LearningExperienceSocialReplies"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialRepliesAcceptInput {
  id: string;
}
export type PostApiSocialRepliesAcceptOutput =
  Types.LearningExperienceSocialServicesDiscussionReply;
export const postApiSocialRepliesAcceptEndpoint = {
  operationId: "postApiSocialRepliesAccept" as const,
  method: "POST" as const,
  path: "/api/social/replies/{id}/accept" as const,
  tags: ["LearningExperienceSocialReplies"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialRepliesUpvoteInput {
  id: string;
}
export type PostApiSocialRepliesUpvoteOutput =
  Types.LearningExperienceSocialServicesDiscussionReply;
export const postApiSocialRepliesUpvoteEndpoint = {
  operationId: "postApiSocialRepliesUpvote" as const,
  method: "POST" as const,
  path: "/api/social/replies/{id}/upvote" as const,
  tags: ["LearningExperienceSocialReplies"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialRepliesInput {
  id: string;
}
export type DeleteApiSocialRepliesOutput = void;
export const deleteApiSocialRepliesEndpoint = {
  operationId: "deleteApiSocialReplies" as const,
  method: "DELETE" as const,
  path: "/api/social/replies/{id}" as const,
  tags: ["LearningExperienceSocialReplies"] as const,
  requiresAuth: true,
} as const;

/**
 * Get subscription churn and retention report
 *
 * Calculates churn, retention, MRR, and subscription status breakdown for the selected period.
 */
export interface GetReportsChurnInput {
  query?: {
    tenantId?: string;
    startDate?: string;
    endDate?: string;
  };
}
export type GetReportsChurnOutput =
  Types.CommerceSubscriptionsSubscriptionChurnReport;
export const getReportsChurnEndpoint = {
  operationId: "getReportsChurn" as const,
  method: "GET" as const,
  path: "/api/v1/reports/churn" as const,
  tags: ["CommerceSubscriptionsReportsSubscriptions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get resource usage by type
 *
 * Retrieves aggregated resource usage across all tenants within the specified date range for the given resource type.
 */
export interface GetResourcesUsageInput {
  query?: {
    type?: Types.ResourcesResourceUsageType;
    startDate?: string;
    endDate?: string;
  };
}
export type GetResourcesUsageOutput = Record<string, number>;
export const getResourcesUsageEndpoint = {
  operationId: "getResourcesUsage" as const,
  method: "GET" as const,
  path: "/v1/resources/usage" as const,
  tags: ["Resources"] as const,
  requiresAuth: true,
} as const;

/**
 * Get resource usage trends over time
 *
 * Retrieves resource usage trends with time-series data aggregated by the specified granularity.
 */
export interface GetResourcesUsageTrendsInput {
  query?: {
    type?: Types.ResourcesResourceUsageType;
    startDate?: string;
    endDate?: string;
    granularity?: Types.ResourcesTrendGranularity;
  };
}
export type GetResourcesUsageTrendsOutput = Types.ResourcesUsageTrendsResult;
export const getResourcesUsageTrendsEndpoint = {
  operationId: "getResourcesUsageTrends" as const,
  method: "GET" as const,
  path: "/v1/resources/usage-trends" as const,
  tags: ["Resources"] as const,
  requiresAuth: true,
} as const;

/**
 * Archive old resource usage records
 *
 * Archives resource usage records older than the specified date for storage optimization.
 */
export interface PostResourcesArchiveInput {
  body?: Types.ResourcesArchiveResourceUsageRecordsInput;
}
export type PostResourcesArchiveOutput = void;
export const postResourcesArchiveEndpoint = {
  operationId: "postResourcesArchive" as const,
  method: "POST" as const,
  path: "/v1/resources:archive" as const,
  tags: ["Resources"] as const,
  requiresAuth: true,
} as const;

/**
 * Cleanup orphaned resources
 *
 * Identifies and removes orphaned resources that are no longer associated with any tenant or user.
 */
export interface PostResourcesCleanupInput {
  body?: Types.ResourcesCleanupOrphanedResourcesInput;
}
export type PostResourcesCleanupOutput = void;
export const postResourcesCleanupEndpoint = {
  operationId: "postResourcesCleanup" as const,
  method: "POST" as const,
  path: "/v1/resources:cleanup" as const,
  tags: ["Resources"] as const,
  requiresAuth: true,
} as const;

export interface PostDocumentContractsGenerateInput {
  body?: Types.ResourcesContentsGenerateContractInput;
}
export type PostDocumentContractsGenerateOutput =
  Types.ResourcesContentsGeneratedContractOutput;
export const postDocumentContractsGenerateEndpoint = {
  operationId: "postDocumentContractsGenerate" as const,
  method: "POST" as const,
  path: "/v1/document-contracts/generate" as const,
  tags: ["ResourcesContentsContracts"] as const,
  requiresAuth: true,
} as const;

export interface PostDocumentContractsGenerateBulkInput {
  body?: Types.ResourcesContentsBulkGenerateContractsInput;
}
export type PostDocumentContractsGenerateBulkOutput =
  Types.ResourcesContentsBulkGeneratedContractsOutput;
export const postDocumentContractsGenerateBulkEndpoint = {
  operationId: "postDocumentContractsGenerateBulk" as const,
  method: "POST" as const,
  path: "/v1/document-contracts/generate:bulk" as const,
  tags: ["ResourcesContentsContracts"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningDraftsInput {
  body?: Types.ResourcesContentsCreateDraftInput;
}
export type PostApiContentsVersioningDraftsOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningDraftsEndpoint = {
  operationId: "postApiContentsVersioningDrafts" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/drafts" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PutApiContentsVersioningDraftsInput {
  versionId: string;
  body?: Types.ResourcesContentsUpdateDraftInput;
}
export type PutApiContentsVersioningDraftsOutput =
  Types.ResourcesContentsContentVersion;
export const putApiContentsVersioningDraftsEndpoint = {
  operationId: "putApiContentsVersioningDrafts" as const,
  method: "PUT" as const,
  path: "/api/contents/versioning/drafts/{versionId}" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningInput {
  versionId: string;
}
export type GetApiContentsVersioningOutput =
  Types.ResourcesContentsContentVersion;
export const getApiContentsVersioningEndpoint = {
  operationId: "getApiContentsVersioning" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/{versionId}" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningEntityHistoryInput {
  entityType: string;
  entityId: string;
}
export type GetApiContentsVersioningEntityHistoryOutput =
  Array<Types.ResourcesContentsContentVersion>;
export const getApiContentsVersioningEntityHistoryEndpoint = {
  operationId: "getApiContentsVersioningEntityHistory" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/entity/{entityType}/{entityId}/history" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningEntityCurrentInput {
  entityType: string;
  entityId: string;
}
export type GetApiContentsVersioningEntityCurrentOutput =
  Types.ResourcesContentsContentVersion;
export const getApiContentsVersioningEntityCurrentEndpoint = {
  operationId: "getApiContentsVersioningEntityCurrent" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/entity/{entityType}/{entityId}/current" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningEntityVersionInput {
  entityType: string;
  entityId: string;
  versionNumber: number;
}
export type GetApiContentsVersioningEntityVersionOutput =
  Types.ResourcesContentsContentVersion;
export const getApiContentsVersioningEntityVersionEndpoint = {
  operationId: "getApiContentsVersioningEntityVersion" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/entity/{entityType}/{entityId}/version/{versionNumber}" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningSubmitForReviewInput {
  versionId: string;
}
export type PostApiContentsVersioningSubmitForReviewOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningSubmitForReviewEndpoint = {
  operationId: "postApiContentsVersioningSubmitForReview" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/submit-for-review" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningPendingReviewInput {
  query?: {
    entityType?: string;
    skip?: number;
    take?: number;
  };
}
export type GetApiContentsVersioningPendingReviewOutput =
  Array<Types.ResourcesContentsContentVersion>;
export const getApiContentsVersioningPendingReviewEndpoint = {
  operationId: "getApiContentsVersioningPendingReview" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/pending-review" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningApproveInput {
  versionId: string;
  body?: Types.ResourcesContentsReviewInput;
}
export type PostApiContentsVersioningApproveOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningApproveEndpoint = {
  operationId: "postApiContentsVersioningApprove" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/approve" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningRejectInput {
  versionId: string;
  body?: Types.ResourcesContentsReviewInput;
}
export type PostApiContentsVersioningRejectOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningRejectEndpoint = {
  operationId: "postApiContentsVersioningReject" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/reject" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningPublishInput {
  versionId: string;
}
export type PostApiContentsVersioningPublishOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningPublishEndpoint = {
  operationId: "postApiContentsVersioningPublish" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/publish" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningScheduleInput {
  versionId: string;
  body?: Types.ResourcesContentsScheduleInput;
}
export type PostApiContentsVersioningScheduleOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningScheduleEndpoint = {
  operationId: "postApiContentsVersioningSchedule" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/schedule" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningCancelScheduleInput {
  versionId: string;
}
export type PostApiContentsVersioningCancelScheduleOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningCancelScheduleEndpoint = {
  operationId: "postApiContentsVersioningCancelSchedule" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/cancel-schedule" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface GetApiContentsVersioningCompareInput {
  query?: {
    versionId1?: string;
    versionId2?: string;
  };
}
export type GetApiContentsVersioningCompareOutput =
  Types.ResourcesContentsContentVersionDiff;
export const getApiContentsVersioningCompareEndpoint = {
  operationId: "getApiContentsVersioningCompare" as const,
  method: "GET" as const,
  path: "/api/contents/versioning/compare" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningEntityRollbackInput {
  entityType: string;
  entityId: string;
  body?: Types.ResourcesContentsRollbackInput;
}
export type PostApiContentsVersioningEntityRollbackOutput =
  Types.ResourcesContentsContentVersion;
export const postApiContentsVersioningEntityRollbackEndpoint = {
  operationId: "postApiContentsVersioningEntityRollback" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/entity/{entityType}/{entityId}/rollback" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiContentsVersioningReviewsInput {
  versionId: string;
  body?: Types.ResourcesContentsAddReviewInput;
}
export type PostApiContentsVersioningReviewsOutput =
  Types.ResourcesContentsContentVersionReview;
export const postApiContentsVersioningReviewsEndpoint = {
  operationId: "postApiContentsVersioningReviews" as const,
  method: "POST" as const,
  path: "/api/contents/versioning/{versionId}/reviews" as const,
  tags: ["ResourcesContentsVersioning"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialReviewsInput {
  body?: Types.LearningExperienceSocialServicesCreateReviewInput;
}
export type PostApiSocialReviewsOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const postApiSocialReviewsEndpoint = {
  operationId: "postApiSocialReviews" as const,
  method: "POST" as const,
  path: "/api/social/reviews" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialReviewsInput {
  id: string;
}
export type GetApiSocialReviewsOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const getApiSocialReviewsEndpoint = {
  operationId: "getApiSocialReviews" as const,
  method: "GET" as const,
  path: "/api/social/reviews/{id}" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialReviewsInput {
  id: string;
}
export type DeleteApiSocialReviewsOutput = void;
export const deleteApiSocialReviewsEndpoint = {
  operationId: "deleteApiSocialReviews" as const,
  method: "DELETE" as const,
  path: "/api/social/reviews/{id}" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesReviewsInput {
  courseId: string;
  query?: {
    skip?: number;
    take?: number;
    approvedOnly?: boolean;
  };
}
export type GetApiSocialCoursesReviewsOutput =
  Array<Types.LearningExperienceSocialServicesCourseReview>;
export const getApiSocialCoursesReviewsEndpoint = {
  operationId: "getApiSocialCoursesReviews" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/reviews" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialReviewsMeInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialReviewsMeOutput =
  Array<Types.LearningExperienceSocialServicesCourseReview>;
export const getApiSocialReviewsMeEndpoint = {
  operationId: "getApiSocialReviewsMe" as const,
  method: "GET" as const,
  path: "/api/social/reviews/me" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialReviewsHelpfulInput {
  id: string;
}
export type PostApiSocialReviewsHelpfulOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const postApiSocialReviewsHelpfulEndpoint = {
  operationId: "postApiSocialReviewsHelpful" as const,
  method: "POST" as const,
  path: "/api/social/reviews/{id}/helpful" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialCoursesRatingStatsInput {
  courseId: string;
}
export type GetApiSocialCoursesRatingStatsOutput =
  Types.LearningExperienceSocialServicesCourseRatingStats;
export const getApiSocialCoursesRatingStatsEndpoint = {
  operationId: "getApiSocialCoursesRatingStats" as const,
  method: "GET" as const,
  path: "/api/social/courses/{courseId}/rating-stats" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialReviewsApproveInput {
  id: string;
}
export type PostApiSocialReviewsApproveOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const postApiSocialReviewsApproveEndpoint = {
  operationId: "postApiSocialReviewsApprove" as const,
  method: "POST" as const,
  path: "/api/social/reviews/{id}/approve" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialReviewsFeatureInput {
  id: string;
}
export type PostApiSocialReviewsFeatureOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const postApiSocialReviewsFeatureEndpoint = {
  operationId: "postApiSocialReviewsFeature" as const,
  method: "POST" as const,
  path: "/api/social/reviews/{id}/feature" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface PatchApiSocialReviewsModerationInput {
  id: string;
  body?: Types.LearningExperienceSocialControllersUpdateReviewModerationInput;
}
export type PatchApiSocialReviewsModerationOutput =
  Types.LearningExperienceSocialServicesCourseReview;
export const patchApiSocialReviewsModerationEndpoint = {
  operationId: "patchApiSocialReviewsModeration" as const,
  method: "PATCH" as const,
  path: "/api/social/reviews/{id}/moderation" as const,
  tags: ["LearningExperienceSocialReviews"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialProfilesUsersInput {
  userId: string;
}
export type GetApiSocialProfilesUsersOutput = Types.SocialProfilesSocialProfile;
export const getApiSocialProfilesUsersEndpoint = {
  operationId: "getApiSocialProfilesUsers" as const,
  method: "GET" as const,
  path: "/api/social/profiles/users/{userId}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialProfilesUsersInput {
  userId: string;
  body?: Types.SocialProfilesUpdateSocialProfileBody;
}
export type PutApiSocialProfilesUsersOutput = Types.SocialProfilesSocialProfile;
export const putApiSocialProfilesUsersEndpoint = {
  operationId: "putApiSocialProfilesUsers" as const,
  method: "PUT" as const,
  path: "/api/social/profiles/users/{userId}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialProfilesInput {
  handle: string;
}
export type GetApiSocialProfilesOutput = Types.SocialProfilesSocialProfile;
export const getApiSocialProfilesEndpoint = {
  operationId: "getApiSocialProfiles" as const,
  method: "GET" as const,
  path: "/api/social/profiles/@{handle}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialProfilesSearchInput {
  query?: {
    query?: string;
    take?: number;
  };
}
export type GetApiSocialProfilesSearchOutput =
  Array<Types.SocialProfilesSocialProfile>;
export const getApiSocialProfilesSearchEndpoint = {
  operationId: "getApiSocialProfilesSearch" as const,
  method: "GET" as const,
  path: "/api/social/profiles/search" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialProfilesUsersPrivacyInput {
  userId: string;
  body?: Types.SocialProfilesUpdateProfilePrivacyBody;
}
export type PutApiSocialProfilesUsersPrivacyOutput =
  Types.SocialProfilesSocialProfile;
export const putApiSocialProfilesUsersPrivacyEndpoint = {
  operationId: "putApiSocialProfilesUsersPrivacy" as const,
  method: "PUT" as const,
  path: "/api/social/profiles/users/{userId}/privacy" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialProfilesUsersStatsInput {
  userId: string;
  body?: Types.SocialProfilesUpdateProfileStatsBody;
}
export type PutApiSocialProfilesUsersStatsOutput =
  Types.SocialProfilesSocialProfile;
export const putApiSocialProfilesUsersStatsEndpoint = {
  operationId: "putApiSocialProfilesUsersStats" as const,
  method: "PUT" as const,
  path: "/api/social/profiles/users/{userId}/stats" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialProfilesSkillsInput {
  profileId: string;
  body?: Types.SocialProfilesAddProfileSkillBody;
}
export type PostApiSocialProfilesSkillsOutput =
  Types.SocialProfilesProfileSkill;
export const postApiSocialProfilesSkillsEndpoint = {
  operationId: "postApiSocialProfilesSkills" as const,
  method: "POST" as const,
  path: "/api/social/profiles/{profileId}/skills" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialProfilesSkillsInput {
  skillId: string;
}
export type DeleteApiSocialProfilesSkillsOutput = void;
export const deleteApiSocialProfilesSkillsEndpoint = {
  operationId: "deleteApiSocialProfilesSkills" as const,
  method: "DELETE" as const,
  path: "/api/social/profiles/skills/{skillId}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialProfilesPortfolioInput {
  profileId: string;
  body?: Types.SocialProfilesAddProfilePortfolioItemBody;
}
export type PostApiSocialProfilesPortfolioOutput =
  Types.SocialProfilesProfilePortfolioItem;
export const postApiSocialProfilesPortfolioEndpoint = {
  operationId: "postApiSocialProfilesPortfolio" as const,
  method: "POST" as const,
  path: "/api/social/profiles/{profileId}/portfolio" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialProfilesPortfolioInput {
  itemId: string;
  body?: Types.SocialProfilesUpdateProfilePortfolioItemBody;
}
export type PutApiSocialProfilesPortfolioOutput =
  Types.SocialProfilesProfilePortfolioItem;
export const putApiSocialProfilesPortfolioEndpoint = {
  operationId: "putApiSocialProfilesPortfolio" as const,
  method: "PUT" as const,
  path: "/api/social/profiles/portfolio/{itemId}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialProfilesPortfolioInput {
  itemId: string;
}
export type DeleteApiSocialProfilesPortfolioOutput = void;
export const deleteApiSocialProfilesPortfolioEndpoint = {
  operationId: "deleteApiSocialProfilesPortfolio" as const,
  method: "DELETE" as const,
  path: "/api/social/profiles/portfolio/{itemId}" as const,
  tags: ["SocialProfiles"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialGroupsForGetApiSocialGroupsInput {
  query?: {
    tenantId?: string;
    ownerId?: string;
    type?: Types.SocialGroupsSocialGroupType;
    visibility?: Types.SocialGroupsSocialGroupVisibility;
    status?: Types.SocialGroupsSocialGroupStatus;
    search?: string;
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialGroupsForGetApiSocialGroupsOutput =
  Array<Types.SocialGroupsSocialGroup>;
export const getApiSocialGroupsForGetApiSocialGroupsEndpoint = {
  operationId: "getApiSocialGroupsForGetApiSocialGroups" as const,
  method: "GET" as const,
  path: "/api/social/groups" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsInput {
  body?: Types.SocialGroupsCreateSocialGroupInput;
}
export type PostApiSocialGroupsOutput = Types.SocialGroupsSocialGroup;
export const postApiSocialGroupsEndpoint = {
  operationId: "postApiSocialGroups" as const,
  method: "POST" as const,
  path: "/api/social/groups" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialGroupsForGetApiSocialGroupsByIdInput {
  id: string;
}
export type GetApiSocialGroupsForGetApiSocialGroupsByIdOutput =
  Types.SocialGroupsSocialGroup;
export const getApiSocialGroupsForGetApiSocialGroupsByIdEndpoint = {
  operationId: "getApiSocialGroupsForGetApiSocialGroupsById" as const,
  method: "GET" as const,
  path: "/api/social/groups/{id}" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialGroupsInput {
  id: string;
  body?: Types.SocialGroupsUpdateSocialGroupInput;
}
export type PutApiSocialGroupsOutput = Types.SocialGroupsSocialGroup;
export const putApiSocialGroupsEndpoint = {
  operationId: "putApiSocialGroups" as const,
  method: "PUT" as const,
  path: "/api/social/groups/{id}" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsActivateInput {
  id: string;
}
export type PostApiSocialGroupsActivateOutput = void;
export const postApiSocialGroupsActivateEndpoint = {
  operationId: "postApiSocialGroupsActivate" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/activate" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsArchiveInput {
  id: string;
}
export type PostApiSocialGroupsArchiveOutput = void;
export const postApiSocialGroupsArchiveEndpoint = {
  operationId: "postApiSocialGroupsArchive" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/archive" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsSuspendInput {
  id: string;
}
export type PostApiSocialGroupsSuspendOutput = void;
export const postApiSocialGroupsSuspendEndpoint = {
  operationId: "postApiSocialGroupsSuspend" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/suspend" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialGroupsMembersInput {
  id: string;
  query?: {
    status?: Types.SocialGroupsSocialGroupMembershipStatus;
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialGroupsMembersOutput =
  Array<Types.SocialGroupsSocialGroupMember>;
export const getApiSocialGroupsMembersEndpoint = {
  operationId: "getApiSocialGroupsMembers" as const,
  method: "GET" as const,
  path: "/api/social/groups/{id}/members" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsMembersInput {
  id: string;
  body?: Types.SocialGroupsJoinSocialGroupInput;
}
export type PostApiSocialGroupsMembersOutput =
  Types.SocialGroupsSocialGroupMember;
export const postApiSocialGroupsMembersEndpoint = {
  operationId: "postApiSocialGroupsMembers" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/members" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsMembersApproveInput {
  id: string;
  userId: string;
  body?: Types.SocialGroupsApproveSocialGroupMemberInput;
}
export type PostApiSocialGroupsMembersApproveOutput = void;
export const postApiSocialGroupsMembersApproveEndpoint = {
  operationId: "postApiSocialGroupsMembersApprove" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/members/{userId}/approve" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialGroupsMembersRejectInput {
  id: string;
  userId: string;
}
export type PostApiSocialGroupsMembersRejectOutput = void;
export const postApiSocialGroupsMembersRejectEndpoint = {
  operationId: "postApiSocialGroupsMembersReject" as const,
  method: "POST" as const,
  path: "/api/social/groups/{id}/members/{userId}/reject" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialGroupsMembersRoleInput {
  id: string;
  userId: string;
  body?: Types.SocialGroupsChangeSocialGroupMemberRoleInput;
}
export type PutApiSocialGroupsMembersRoleOutput = void;
export const putApiSocialGroupsMembersRoleEndpoint = {
  operationId: "putApiSocialGroupsMembersRole" as const,
  method: "PUT" as const,
  path: "/api/social/groups/{id}/members/{userId}/role" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialGroupsMembersInput {
  id: string;
  userId: string;
}
export type DeleteApiSocialGroupsMembersOutput = void;
export const deleteApiSocialGroupsMembersEndpoint = {
  operationId: "deleteApiSocialGroupsMembers" as const,
  method: "DELETE" as const,
  path: "/api/social/groups/{id}/members/{userId}" as const,
  tags: ["SocialGroupsSocialGroups"] as const,
  requiresAuth: true,
} as const;

export interface GetSupportTicketsInput {
  query?: {
    tenantId?: string;
    status?: Types.CommerceProductsSupportTicketStatus;
    priority?: Types.CommerceProductsSupportTicketPriority;
    search?: string;
    skip?: number;
    take?: number;
    customerId?: string;
  };
}
export type GetSupportTicketsOutput =
  Types.PagedResultOfCommerceProductsSupportTicket;
export const getSupportTicketsEndpoint = {
  operationId: "getSupportTickets" as const,
  method: "GET" as const,
  path: "/v1/support/tickets" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsInput {
  body?: Types.CommerceProductsCreateSupportTicketInput;
}
export type PostSupportTicketsOutput = Types.CommerceProductsSupportTicket;
export const postSupportTicketsEndpoint = {
  operationId: "postSupportTickets" as const,
  method: "POST" as const,
  path: "/v1/support/tickets" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface GetSupportTicketByIdInput {
  ticketId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetSupportTicketByIdOutput = Types.CommerceProductsSupportTicket;
export const getSupportTicketByIdEndpoint = {
  operationId: "getSupportTicketById" as const,
  method: "GET" as const,
  path: "/v1/support/tickets/{ticketId}" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsMessagesInput {
  ticketId: string;
  body?: Types.CommerceProductsAddSupportTicketMessageInput;
}
export type PostSupportTicketsMessagesOutput =
  Types.CommerceProductsSupportTicket;
export const postSupportTicketsMessagesEndpoint = {
  operationId: "postSupportTicketsMessages" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/{ticketId}/messages" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsAssignInput {
  ticketId: string;
  body?: Types.CommerceProductsAssignSupportTicketInput;
}
export type PostSupportTicketsAssignOutput =
  Types.CommerceProductsSupportTicket;
export const postSupportTicketsAssignEndpoint = {
  operationId: "postSupportTicketsAssign" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/{ticketId}:assign" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsResolveInput {
  ticketId: string;
  body?: Types.CommerceProductsResolveSupportTicketInput;
}
export type PostSupportTicketsResolveOutput =
  Types.CommerceProductsSupportTicket;
export const postSupportTicketsResolveEndpoint = {
  operationId: "postSupportTicketsResolve" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/{ticketId}:resolve" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsCloseInput {
  ticketId: string;
  body?: Types.CommerceProductsCloseSupportTicketInput;
}
export type PostSupportTicketsCloseOutput = Types.CommerceProductsSupportTicket;
export const postSupportTicketsCloseEndpoint = {
  operationId: "postSupportTicketsClose" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/{ticketId}:close" as const,
  tags: ["CommerceProductsSupportTickets"] as const,
  requiresAuth: true,
} as const;

export interface GetSupportTicketsMineInput {
  query?: {
    status?: Types.CommerceProductsSupportTicketStatus;
    skip?: number;
    take?: number;
  };
}
export type GetSupportTicketsMineOutput =
  Types.PagedResultOfCommerceProductsSupportTicket;
export const getSupportTicketsMineEndpoint = {
  operationId: "getSupportTicketsMine" as const,
  method: "GET" as const,
  path: "/v1/support/tickets/mine" as const,
  tags: ["CommerceProductsSupportTicketsSelfService"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsMineInput {
  body?: Types.CommerceProductsCreateMySupportTicketInput;
}
export type PostSupportTicketsMineOutput = Types.CommerceProductsSupportTicket;
export const postSupportTicketsMineEndpoint = {
  operationId: "postSupportTicketsMine" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/mine" as const,
  tags: ["CommerceProductsSupportTicketsSelfService"] as const,
  requiresAuth: true,
} as const;

export interface PostSupportTicketsMineMessagesInput {
  ticketId: string;
  body?: Types.CommerceProductsAddMySupportTicketMessageInput;
}
export type PostSupportTicketsMineMessagesOutput =
  Types.CommerceProductsSupportTicket;
export const postSupportTicketsMineMessagesEndpoint = {
  operationId: "postSupportTicketsMineMessages" as const,
  method: "POST" as const,
  path: "/v1/support/tickets/mine/{ticketId}/messages" as const,
  tags: ["CommerceProductsSupportTicketsSelfService"] as const,
  requiresAuth: true,
} as const;

export type GetTaxJurisdictionsForGetTaxJurisdictionsInput = void;
export type GetTaxJurisdictionsForGetTaxJurisdictionsOutput =
  Array<Types.CommercePaymentsTaxRate>;
export const getTaxJurisdictionsForGetTaxJurisdictionsEndpoint = {
  operationId: "getTaxJurisdictionsForGetTaxJurisdictions" as const,
  method: "GET" as const,
  path: "/api/v1/tax-jurisdictions" as const,
  tags: ["CommercePaymentsTaxJurisdictions"] as const,
  requiresAuth: true,
} as const;

/**
 * Create tax jurisdiction
 *
 * Creates a new tax jurisdiction with the provided information.
 */
export interface PostTaxJurisdictionsInput {
  body?: Types.CommercePaymentsCreateTaxJurisdictionInput;
}
export type PostTaxJurisdictionsOutput = void;
export const postTaxJurisdictionsEndpoint = {
  operationId: "postTaxJurisdictions" as const,
  method: "POST" as const,
  path: "/api/v1/tax-jurisdictions" as const,
  tags: ["CommercePaymentsTaxJurisdictions"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tax jurisdiction by ID
 *
 * Retrieves detailed information for a specific tax jurisdiction.
 */
export interface GetTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionIdInput {
  jurisdictionId: string;
}
export type GetTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionIdOutput =
  Types.CommercePaymentsTaxJurisdictionDto;
export const getTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionIdEndpoint =
  {
    operationId:
      "getTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionId" as const,
    method: "GET" as const,
    path: "/api/v1/tax-jurisdictions/{jurisdictionId}" as const,
    tags: ["CommercePaymentsTaxJurisdictions"] as const,
    requiresAuth: true,
  } as const;

/**
 * Delete tax jurisdiction
 *
 * Deletes a tax jurisdiction by ID.
 */
export interface DeleteTaxJurisdictionsInput {
  jurisdictionId: string;
}
export type DeleteTaxJurisdictionsOutput = void;
export const deleteTaxJurisdictionsEndpoint = {
  operationId: "deleteTaxJurisdictions" as const,
  method: "DELETE" as const,
  path: "/api/v1/tax-jurisdictions/{jurisdictionId}" as const,
  tags: ["CommercePaymentsTaxJurisdictions"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update tax jurisdiction
 *
 * Updates specific fields of a tax jurisdiction.
 */
export interface PatchTaxJurisdictionsInput {
  jurisdictionId: string;
  body?: Types.CommercePaymentsPatchTaxJurisdictionInput;
}
export type PatchTaxJurisdictionsOutput = void;
export const patchTaxJurisdictionsEndpoint = {
  operationId: "patchTaxJurisdictions" as const,
  method: "PATCH" as const,
  path: "/api/v1/tax-jurisdictions/{jurisdictionId}" as const,
  tags: ["CommercePaymentsTaxJurisdictions"] as const,
  requiresAuth: true,
} as const;

export interface GetTaxRulesForGetTaxRulesInput {
  query?: {
    jurisdictionCode?: string;
    customerType?: string;
    effectiveDate?: string;
  };
}
export type GetTaxRulesForGetTaxRulesOutput =
  Array<Types.CommercePaymentsTaxRate>;
export const getTaxRulesForGetTaxRulesEndpoint = {
  operationId: "getTaxRulesForGetTaxRules" as const,
  method: "GET" as const,
  path: "/api/v1/tax-rules" as const,
  tags: ["CommercePaymentsTaxRules"] as const,
  requiresAuth: true,
} as const;

/**
 * Create tax rule
 *
 * Creates a new tax rule with the provided information.
 */
export interface PostTaxRulesInput {
  body?: Types.CommercePaymentsCreateTaxRuleInput;
}
export type PostTaxRulesOutput = void;
export const postTaxRulesEndpoint = {
  operationId: "postTaxRules" as const,
  method: "POST" as const,
  path: "/api/v1/tax-rules" as const,
  tags: ["CommercePaymentsTaxRules"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tax rule by ID
 *
 * Retrieves detailed information for a specific tax rule.
 */
export interface GetTaxRulesForGetTaxRulesByRuleIdInput {
  ruleId: string;
}
export type GetTaxRulesForGetTaxRulesByRuleIdOutput =
  Types.CommercePaymentsTaxRuleDto;
export const getTaxRulesForGetTaxRulesByRuleIdEndpoint = {
  operationId: "getTaxRulesForGetTaxRulesByRuleId" as const,
  method: "GET" as const,
  path: "/api/v1/tax-rules/{ruleId}" as const,
  tags: ["CommercePaymentsTaxRules"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete tax rule
 *
 * Deletes a tax rule by ID.
 */
export interface DeleteTaxRulesInput {
  ruleId: string;
}
export type DeleteTaxRulesOutput = void;
export const deleteTaxRulesEndpoint = {
  operationId: "deleteTaxRules" as const,
  method: "DELETE" as const,
  path: "/api/v1/tax-rules/{ruleId}" as const,
  tags: ["CommercePaymentsTaxRules"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update tax rule
 *
 * Updates specific fields of a tax rule.
 */
export interface PatchTaxRulesInput {
  ruleId: string;
  body?: Types.CommercePaymentsPatchTaxRuleInput;
}
export type PatchTaxRulesOutput = void;
export const patchTaxRulesEndpoint = {
  operationId: "patchTaxRules" as const,
  method: "PATCH" as const,
  path: "/api/v1/tax-rules/{ruleId}" as const,
  tags: ["CommercePaymentsTaxRules"] as const,
  requiresAuth: true,
} as const;

export interface PostPaymentsTaxCalculateInput {
  body?: Types.CommercePaymentsCalculateTaxInput;
}
export type PostPaymentsTaxCalculateOutput =
  Types.CommercePaymentsTaxCalculationResult;
export const postPaymentsTaxCalculateEndpoint = {
  operationId: "postPaymentsTaxCalculate" as const,
  method: "POST" as const,
  path: "/api/v1/payments/tax/calculate" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

export interface PostTaxesCalculateInput {
  body?: Types.CommercePaymentsCalculateTaxInput;
}
export type PostTaxesCalculateOutput =
  Types.CommercePaymentsTaxCalculationResult;
export const postTaxesCalculateEndpoint = {
  operationId: "postTaxesCalculate" as const,
  method: "POST" as const,
  path: "/api/v1/taxes/calculate" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate tax exemption
 *
 * Validates whether a tax exemption certificate or status is valid for a given transaction.
 */
export interface PostPaymentsTaxValidateVatInput {
  body?: Types.CommercePaymentsValidateTaxExemptionInput;
}
export type PostPaymentsTaxValidateVatOutput =
  Types.CommercePaymentsTaxExemptionValidationResult;
export const postPaymentsTaxValidateVatEndpoint = {
  operationId: "postPaymentsTaxValidateVat" as const,
  method: "POST" as const,
  path: "/api/v1/payments/tax/validate-vat" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate tax exemption
 *
 * Validates whether a tax exemption certificate or status is valid for a given transaction.
 */
export interface PostTaxesValidateVatInput {
  body?: Types.CommercePaymentsValidateTaxExemptionInput;
}
export type PostTaxesValidateVatOutput =
  Types.CommercePaymentsTaxExemptionValidationResult;
export const postTaxesValidateVatEndpoint = {
  operationId: "postTaxesValidateVat" as const,
  method: "POST" as const,
  path: "/api/v1/taxes/validate-vat" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate tax exemption
 *
 * Validates whether a tax exemption certificate or status is valid for a given transaction.
 */
export interface PostPaymentsTaxValidateExemptionInput {
  body?: Types.CommercePaymentsValidateTaxExemptionInput;
}
export type PostPaymentsTaxValidateExemptionOutput =
  Types.CommercePaymentsTaxExemptionValidationResult;
export const postPaymentsTaxValidateExemptionEndpoint = {
  operationId: "postPaymentsTaxValidateExemption" as const,
  method: "POST" as const,
  path: "/api/v1/payments/tax/validate-exemption" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate tax exemption
 *
 * Validates whether a tax exemption certificate or status is valid for a given transaction.
 */
export interface PostTaxesValidateExemptionInput {
  body?: Types.CommercePaymentsValidateTaxExemptionInput;
}
export type PostTaxesValidateExemptionOutput =
  Types.CommercePaymentsTaxExemptionValidationResult;
export const postTaxesValidateExemptionEndpoint = {
  operationId: "postTaxesValidateExemption" as const,
  method: "POST" as const,
  path: "/api/v1/taxes/validate-exemption" as const,
  tags: ["CommercePaymentsTaxes"] as const,
  requiresAuth: true,
} as const;

export interface GetTeamsProjectsInput {
  teamId: string;
}
export type GetTeamsProjectsOutput = Array<Types.APITeamsTeamProjectSummary>;
export const getTeamsProjectsEndpoint = {
  operationId: "getTeamsProjects" as const,
  method: "GET" as const,
  path: "/v1/teams/{teamId}/projects" as const,
  tags: ["ApiTeamsProjects"] as const,
  requiresAuth: true,
} as const;

export interface GetTeamsForGetTeamsInput {
  query?: {
    search?: string;
    visibility?: Types.TeamsTeamVisibility;
    status?: Types.TeamsTeamStatus;
    includeArchived?: boolean;
    skip?: number;
    take?: number;
  };
}
export type GetTeamsForGetTeamsOutput = Array<Types.APITeamsTeam>;
export const getTeamsForGetTeamsEndpoint = {
  operationId: "getTeamsForGetTeams" as const,
  method: "GET" as const,
  path: "/v1/teams" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsInput {
  body?: Types.APITeamsCreateTeamInput;
}
export type PostTeamsOutput = Types.APITeamsTeam;
export const postTeamsEndpoint = {
  operationId: "postTeams" as const,
  method: "POST" as const,
  path: "/v1/teams" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface GetTeamsMineInput {
  query?: {
    includeArchived?: boolean;
    search?: string;
    skip?: number;
    take?: number;
  };
}
export type GetTeamsMineOutput = Array<Types.APITeamsTeam>;
export const getTeamsMineEndpoint = {
  operationId: "getTeamsMine" as const,
  method: "GET" as const,
  path: "/v1/teams/mine" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface GetTeamsForGetTeamsByTeamIdInput {
  teamId: string;
}
export type GetTeamsForGetTeamsByTeamIdOutput = Types.APITeamsTeam;
export const getTeamsForGetTeamsByTeamIdEndpoint = {
  operationId: "getTeamsForGetTeamsByTeamId" as const,
  method: "GET" as const,
  path: "/v1/teams/{teamId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PutTeamsInput {
  teamId: string;
  body?: Types.APITeamsUpdateTeamInput;
}
export type PutTeamsOutput = Types.APITeamsTeam;
export const putTeamsEndpoint = {
  operationId: "putTeams" as const,
  method: "PUT" as const,
  path: "/v1/teams/{teamId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTeamsInput {
  teamId: string;
}
export type DeleteTeamsOutput = void;
export const deleteTeamsEndpoint = {
  operationId: "deleteTeams" as const,
  method: "DELETE" as const,
  path: "/v1/teams/{teamId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsRestoreInput {
  teamId: string;
}
export type PostTeamsRestoreOutput = Types.APITeamsTeam;
export const postTeamsRestoreEndpoint = {
  operationId: "postTeamsRestore" as const,
  method: "POST" as const,
  path: "/v1/teams/{teamId}:restore" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsMembersInput {
  teamId: string;
  body?: Types.APITeamsAddTeamMemberInput;
}
export type PostTeamsMembersOutput = Types.APITeamsTeamMember;
export const postTeamsMembersEndpoint = {
  operationId: "postTeamsMembers" as const,
  method: "POST" as const,
  path: "/v1/teams/{teamId}/members" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PutTeamsMembersInput {
  teamId: string;
  userId: string;
  body?: Types.APITeamsChangeTeamMemberInput;
}
export type PutTeamsMembersOutput = Types.APITeamsTeamMember;
export const putTeamsMembersEndpoint = {
  operationId: "putTeamsMembers" as const,
  method: "PUT" as const,
  path: "/v1/teams/{teamId}/members/{userId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTeamsMembersInput {
  teamId: string;
  userId: string;
}
export type DeleteTeamsMembersOutput = void;
export const deleteTeamsMembersEndpoint = {
  operationId: "deleteTeamsMembers" as const,
  method: "DELETE" as const,
  path: "/v1/teams/{teamId}/members/{userId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface GetTeamsInvitationsInput {
  teamId: string;
}
export type GetTeamsInvitationsOutput = Array<Types.APITeamsTeamInvitation>;
export const getTeamsInvitationsEndpoint = {
  operationId: "getTeamsInvitations" as const,
  method: "GET" as const,
  path: "/v1/teams/{teamId}/invitations" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsInvitationsInput {
  teamId: string;
  body?: Types.APITeamsCreateTeamInvitationInput;
}
export type PostTeamsInvitationsOutput = Types.APITeamsTeamInvitationCreated;
export const postTeamsInvitationsEndpoint = {
  operationId: "postTeamsInvitations" as const,
  method: "POST" as const,
  path: "/v1/teams/{teamId}/invitations" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export type GetTeamsMyInvitationsInput = void;
export type GetTeamsMyInvitationsOutput = Array<Types.APITeamsMyTeamInvitation>;
export const getTeamsMyInvitationsEndpoint = {
  operationId: "getTeamsMyInvitations" as const,
  method: "GET" as const,
  path: "/v1/teams/my-invitations" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTeamsInvitationsInput {
  teamId: string;
  invitationId: string;
}
export type DeleteTeamsInvitationsOutput = void;
export const deleteTeamsInvitationsEndpoint = {
  operationId: "deleteTeamsInvitations" as const,
  method: "DELETE" as const,
  path: "/v1/teams/{teamId}/invitations/{invitationId}" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsInvitationsAcceptForPostTeamsInvitationsAcceptInput {
  body?: Types.APITeamsAcceptTeamInvitationInput;
}
export type PostTeamsInvitationsAcceptForPostTeamsInvitationsAcceptOutput =
  Types.APITeamsTeam;
export const postTeamsInvitationsAcceptForPostTeamsInvitationsAcceptEndpoint = {
  operationId:
    "postTeamsInvitationsAcceptForPostTeamsInvitationsAccept" as const,
  method: "POST" as const,
  path: "/v1/teams/invitations/accept" as const,
  tags: ["ApiTeams"] as const,
  requiresAuth: true,
} as const;

export interface PostTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAcceptInput {
  invitationId: string;
}
export type PostTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAcceptOutput =
  Types.APITeamsTeam;
export const postTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAcceptEndpoint =
  {
    operationId:
      "postTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAccept" as const,
    method: "POST" as const,
    path: "/v1/teams/invitations/{invitationId}:accept" as const,
    tags: ["ApiTeams"] as const,
    requiresAuth: true,
  } as const;

/**
 * Bulk create tenants
 *
 * Creates multiple tenant organizations at once.
 */
export interface PostTenantsCreateInput {
  body?: Types.IdentityTenantsBulkCreateTenantsCommand;
}
export type PostTenantsCreateOutput = Types.BulkOperationOutput;
export const postTenantsCreateEndpoint = {
  operationId: "postTenantsCreate" as const,
  method: "POST" as const,
  path: "/v1/tenants:create" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk partial update tenants
 *
 * Updates multiple tenants with partial data.
 */
export interface PostTenantsUpdateInput {
  body?: Types.IdentityTenantsBulkUpdateTenantsCommand;
}
export type PostTenantsUpdateOutput = Types.BulkOperationOutput;
export const postTenantsUpdateEndpoint = {
  operationId: "postTenantsUpdate" as const,
  method: "POST" as const,
  path: "/v1/tenants:update" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk full update tenants
 *
 * Updates multiple tenants with complete data.
 */
export interface PostTenantsReplaceInput {
  body?: Types.IdentityTenantsBulkUpdateTenantsCommand;
}
export type PostTenantsReplaceOutput = Types.BulkOperationOutput;
export const postTenantsReplaceEndpoint = {
  operationId: "postTenantsReplace" as const,
  method: "POST" as const,
  path: "/v1/tenants:replace" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk soft delete tenants
 *
 * Soft deletes multiple tenants at once.
 */
export interface PostTenantsDeleteInput {
  body?: Types.IdentityTenantsBulkDeleteTenantsCommand;
}
export type PostTenantsDeleteOutput = Types.BulkOperationOutput;
export const postTenantsDeleteEndpoint = {
  operationId: "postTenantsDelete" as const,
  method: "POST" as const,
  path: "/v1/tenants:delete" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk activate tenant accounts
 *
 * Activates multiple tenant accounts at once.
 */
export interface PostTenantsActivateForPostTenantsActivateInput {
  body?: Types.IdentityTenantsBulkActivateTenantsCommand;
}
export type PostTenantsActivateForPostTenantsActivateOutput =
  Types.BulkOperationOutput;
export const postTenantsActivateForPostTenantsActivateEndpoint = {
  operationId: "postTenantsActivateForPostTenantsActivate" as const,
  method: "POST" as const,
  path: "/v1/tenants:activate" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk deactivate tenant accounts
 *
 * Deactivates multiple tenant accounts at once.
 */
export interface PostTenantsDeactivateForPostTenantsDeactivateInput {
  body?: Types.IdentityTenantsBulkDeactivateTenantsCommand;
}
export type PostTenantsDeactivateForPostTenantsDeactivateOutput =
  Types.BulkOperationOutput;
export const postTenantsDeactivateForPostTenantsDeactivateEndpoint = {
  operationId: "postTenantsDeactivateForPostTenantsDeactivate" as const,
  method: "POST" as const,
  path: "/v1/tenants:deactivate" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk archive tenant accounts
 *
 * Archives multiple tenant accounts at once.
 */
export interface PostTenantsArchiveForPostTenantsArchiveInput {
  body?: Types.IdentityTenantsBulkArchiveTenantsCommand;
}
export type PostTenantsArchiveForPostTenantsArchiveOutput =
  Types.BulkOperationOutput;
export const postTenantsArchiveForPostTenantsArchiveEndpoint = {
  operationId: "postTenantsArchiveForPostTenantsArchive" as const,
  method: "POST" as const,
  path: "/v1/tenants:archive" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk undelete soft-deleted tenants
 *
 * Restores multiple soft-deleted tenants at once.
 */
export interface PostTenantsUndeleteForPostTenantsUndeleteInput {
  body?: Types.IdentityTenantsBulkUndeleteTenantsCommand;
}
export type PostTenantsUndeleteForPostTenantsUndeleteOutput =
  Types.BulkOperationOutput;
export const postTenantsUndeleteForPostTenantsUndeleteEndpoint = {
  operationId: "postTenantsUndeleteForPostTenantsUndelete" as const,
  method: "POST" as const,
  path: "/v1/tenants:undelete" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk hard delete tenants (irreversible purge)
 *
 * Permanently deletes multiple tenants. Admin operation requiring proper authorization.
 */
export interface PostTenantsPurgeForPostTenantsPurgeInput {
  body?: Types.IdentityTenantsBulkPurgeTenantsCommand;
}
export type PostTenantsPurgeForPostTenantsPurgeOutput =
  Types.BulkOperationOutput;
export const postTenantsPurgeForPostTenantsPurgeEndpoint = {
  operationId: "postTenantsPurgeForPostTenantsPurge" as const,
  method: "POST" as const,
  path: "/v1/tenants:purge" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Activate tenant account
 *
 * Activates a tenant organization by ID.
 */
export interface PostTenantsActivateForPostTenantsByTenantIdActivateInput {
  tenantId: string;
}
export type PostTenantsActivateForPostTenantsByTenantIdActivateOutput = void;
export const postTenantsActivateForPostTenantsByTenantIdActivateEndpoint = {
  operationId: "postTenantsActivateForPostTenantsByTenantIdActivate" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}:activate" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Deactivate tenant account
 *
 * Deactivates a tenant organization by ID.
 */
export interface PostTenantsDeactivateForPostTenantsByTenantIdDeactivateInput {
  tenantId: string;
}
export type PostTenantsDeactivateForPostTenantsByTenantIdDeactivateOutput =
  void;
export const postTenantsDeactivateForPostTenantsByTenantIdDeactivateEndpoint = {
  operationId:
    "postTenantsDeactivateForPostTenantsByTenantIdDeactivate" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}:deactivate" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Archive (soft delete) tenant account
 *
 * Archives a tenant organization by ID.
 */
export interface PostTenantsArchiveForPostTenantsByTenantIdArchiveInput {
  tenantId: string;
  body?: Types.IdentityTenantsArchiveInput;
}
export type PostTenantsArchiveForPostTenantsByTenantIdArchiveOutput = void;
export const postTenantsArchiveForPostTenantsByTenantIdArchiveEndpoint = {
  operationId: "postTenantsArchiveForPostTenantsByTenantIdArchive" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}:archive" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Undelete a soft-deleted tenant account
 *
 * Undeletes a previously soft-deleted (archived) tenant organization.
 */
export interface PostTenantsUndeleteForPostTenantsByTenantIdUndeleteInput {
  tenantId: string;
  body?: Types.IdentityTenantsRecoverInput;
}
export type PostTenantsUndeleteForPostTenantsByTenantIdUndeleteOutput = void;
export const postTenantsUndeleteForPostTenantsByTenantIdUndeleteEndpoint = {
  operationId: "postTenantsUndeleteForPostTenantsByTenantIdUndelete" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}:undelete" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Permanently delete (hard delete) tenant account
 *
 * Permanently and irreversibly deletes a tenant organization. Admin operation requiring proper authorization.
 */
export interface PostTenantsPurgeForPostTenantsByTenantIdPurgeInput {
  tenantId: string;
}
export type PostTenantsPurgeForPostTenantsByTenantIdPurgeOutput = void;
export const postTenantsPurgeForPostTenantsByTenantIdPurgeEndpoint = {
  operationId: "postTenantsPurgeForPostTenantsByTenantIdPurge" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}:purge" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant audit log
 *
 * Retrieves the audit log for a tenant showing all changes, actions, and who performed them.
 */
export interface GetTenantsAuditLogInput {
  tenantId: string;
  query?: {
    startDate?: string;
    endDate?: string;
    action?: string;
    actorId?: string;
    page?: number;
    pageSize?: number;
  };
}
export type GetTenantsAuditLogOutput =
  Types.PagedResultOfIdentityTenantsTenantAuditLogEntry;
export const getTenantsAuditLogEndpoint = {
  operationId: "getTenantsAuditLog" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/audit-log" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenants with pagination, search, and sorting
 *
 * Retrieves a paginated list of all tenant organizations accessible to the requesting user.
 */
export interface GetTenantsForGetTenantsInput {
  query?: {
    page?: number;
    pageSize?: number;
    status?: string;
    searchTerm?: string;
  };
}
export type GetTenantsForGetTenantsOutput =
  Types.PagedResultOfIdentityTenantsTenant;
export const getTenantsForGetTenantsEndpoint = {
  operationId: "getTenantsForGetTenants" as const,
  method: "GET" as const,
  path: "/v1/tenants" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a new tenant organization
 *
 * Creates a new tenant organization within the GameGuild platform.
 */
export interface PostTenantsInput {
  body?: Types.IdentityTenantsCreateTenantInput;
}
export type PostTenantsOutput = void;
export const postTenantsEndpoint = {
  operationId: "postTenants" as const,
  method: "POST" as const,
  path: "/v1/tenants" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Get payment history for tenant
 *
 * Retrieves payment history for a specific tenant with optional date filtering.
 */
export interface GetTenantsPaymentsInput {
  tenantId: string;
  query?: {
    startDate?: string;
    endDate?: string;
  };
}
export type GetTenantsPaymentsOutput =
  Array<Types.CommercePaymentsPaymentResult>;
export const getTenantsPaymentsEndpoint = {
  operationId: "getTenantsPayments" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/payments" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Validate tenant data before creation
 *
 * Validates tenant data without creating. Returns errors, warnings, and suggestions.
 */
export interface PostTenantsValidateInput {
  body?: Types.IdentityTenantsValidateTenantInput;
}
export type PostTenantsValidateOutput =
  Types.IdentityTenantsTenantValidationOutput;
export const postTenantsValidateEndpoint = {
  operationId: "postTenantsValidate" as const,
  method: "POST" as const,
  path: "/v1/tenants:validate" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant by ID
 *
 * Retrieves detailed information for a specific tenant by their unique identifier.
 */
export interface GetTenantsForGetTenantsByTenantIdInput {
  tenantId: string;
}
export type GetTenantsForGetTenantsByTenantIdOutput =
  Types.IdentityTenantsTenant;
export const getTenantsForGetTenantsByTenantIdEndpoint = {
  operationId: "getTenantsForGetTenantsByTenantId" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant by ID
 *
 * Fully updates a tenant by ID with complete tenant data.
 */
export interface PutTenantsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantInput;
}
export type PutTenantsOutput = void;
export const putTenantsEndpoint = {
  operationId: "putTenants" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Soft delete tenant by ID
 *
 * Soft deletes a tenant by ID (can be restored).
 */
export interface DeleteTenantsInput {
  tenantId: string;
  body?: Types.IdentityTenantsArchiveInput;
}
export type DeleteTenantsOutput = void;
export const deleteTenantsEndpoint = {
  operationId: "deleteTenants" as const,
  method: "DELETE" as const,
  path: "/v1/tenants/{tenantId}" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update tenant by ID
 *
 * Updates specific fields of a tenant by ID.
 */
export interface PatchTenantsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantInput;
}
export type PatchTenantsOutput = void;
export const patchTenantsEndpoint = {
  operationId: "patchTenants" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if tenant exists by ID
 *
 * Checks if a tenant exists by ID without returning the body.
 */
export interface HeadTenantsInput {
  tenantId: string;
}
export type HeadTenantsOutput = void;
export const headTenantsEndpoint = {
  operationId: "headTenants" as const,
  method: "HEAD" as const,
  path: "/v1/tenants/{tenantId}" as const,
  tags: ["Tenants"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant AI history
 *
 * Retrieves recent AI conversation history for a specific tenant.
 */
export interface GetTenantsAiHistoryInput {
  tenantId: string;
  query?: {
    take?: number;
  };
}
export type GetTenantsAiHistoryOutput =
  Array<Types.AIAiConversationHistoryEntry>;
export const getTenantsAiHistoryEndpoint = {
  operationId: "getTenantsAiHistory" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/ai/history" as const,
  tags: ["TenantsAi"] as const,
  requiresAuth: true,
} as const;

/**
 * Export tenant AI history
 */
export interface GetTenantsAiHistoryExportInput {
  tenantId: string;
  query?: {
    format?: string;
    take?: number;
  };
}
export type GetTenantsAiHistoryExportOutput = void;
export const getTenantsAiHistoryExportEndpoint = {
  operationId: "getTenantsAiHistoryExport" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/ai/history/export" as const,
  tags: ["TenantsAi"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant AI quotas
 */
export interface GetTenantsAiQuotasInput {
  tenantId: string;
}
export type GetTenantsAiQuotasOutput = Types.AIAiQuotaStatusOutput;
export const getTenantsAiQuotasEndpoint = {
  operationId: "getTenantsAiQuotas" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/ai/quotas" as const,
  tags: ["TenantsAi"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant metadata by tenant ID
 *
 * Retrieves comprehensive tenant metadata including custom fields, tags, external references, and business information.
 */
export interface GetTenantsMetadataInput {
  tenantId: string;
}
export type GetTenantsMetadataOutput = Types.IdentityTenantsTenantMetadata;
export const getTenantsMetadataEndpoint = {
  operationId: "getTenantsMetadata" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/metadata" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace all tenant metadata by tenant ID
 *
 * Replaces all tenant metadata with new values. All existing metadata is replaced with the provided data.
 */
export interface PutTenantsMetadataInput {
  tenantId: string;
  body?: Types.IdentityTenantsReplaceTenantMetadataInput;
}
export type PutTenantsMetadataOutput = void;
export const putTenantsMetadataEndpoint = {
  operationId: "putTenantsMetadata" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/metadata" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update tenant metadata by tenant ID
 *
 * Updates specific tenant metadata fields without affecting other metadata. Only the provided metadata keys are modified.
 */
export interface PatchTenantsMetadataInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantMetadataInput;
}
export type PatchTenantsMetadataOutput = void;
export const patchTenantsMetadataEndpoint = {
  operationId: "patchTenantsMetadata" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/metadata" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant custom fields
 *
 * Retrieves all custom fields configured for the tenant as a key-value dictionary for storing tenant-specific data.
 */
export interface GetTenantsMetadataCustomFieldsInput {
  tenantId: string;
}
export type GetTenantsMetadataCustomFieldsOutput = Record<
  string,
  Record<string, unknown>
>;
export const getTenantsMetadataCustomFieldsEndpoint = {
  operationId: "getTenantsMetadataCustomFields" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/metadata/custom-fields" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant custom fields
 *
 * Updates specific custom fields for the tenant. Existing fields not specified are preserved.
 */
export interface PatchTenantsMetadataCustomFieldsInput {
  tenantId: string;
  body?: Record<string, Record<string, unknown>>;
}
export type PatchTenantsMetadataCustomFieldsOutput = void;
export const patchTenantsMetadataCustomFieldsEndpoint = {
  operationId: "patchTenantsMetadataCustomFields" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/metadata/custom-fields" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant tags
 *
 * Retrieves all tags configured for the tenant for categorization and filtering purposes.
 */
export interface GetTenantsMetadataTagsInput {
  tenantId: string;
}
export type GetTenantsMetadataTagsOutput = Array<string>;
export const getTenantsMetadataTagsEndpoint = {
  operationId: "getTenantsMetadataTags" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/metadata/tags" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace all tenant tags
 *
 * Replaces all existing tags with the provided list of tags.
 */
export interface PutTenantsMetadataTagsInput {
  tenantId: string;
  body?: Array<string>;
}
export type PutTenantsMetadataTagsOutput = void;
export const putTenantsMetadataTagsEndpoint = {
  operationId: "putTenantsMetadataTags" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/metadata/tags" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant tags
 *
 * Updates the tags for the tenant. Existing tags are merged with the new tags.
 */
export interface PatchTenantsMetadataTagsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantTagsInput;
}
export type PatchTenantsMetadataTagsOutput = void;
export const patchTenantsMetadataTagsEndpoint = {
  operationId: "patchTenantsMetadataTags" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/metadata/tags" as const,
  tags: ["TenantsMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all quotas for a tenant
 *
 * Retrieves all configured resource quotas for a specific tenant organization.
 */
export interface GetTenantsQuotasForGetTenantsByTenantIdQuotasInput {
  tenantId: string;
}
export type GetTenantsQuotasForGetTenantsByTenantIdQuotasOutput =
  Array<Types.ResourcesResourceQuotaOutput>;
export const getTenantsQuotasForGetTenantsByTenantIdQuotasEndpoint = {
  operationId: "getTenantsQuotasForGetTenantsByTenantIdQuotas" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/quotas" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Get specific quota for a resource type
 *
 * Retrieves the quota configuration for a specific resource type for a tenant.
 */
export interface GetTenantsQuotasForGetTenantsByTenantIdQuotasByTypeInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
}
export type GetTenantsQuotasForGetTenantsByTenantIdQuotasByTypeOutput =
  Types.ResourcesResourceQuotaOutput;
export const getTenantsQuotasForGetTenantsByTenantIdQuotasByTypeEndpoint = {
  operationId: "getTenantsQuotasForGetTenantsByTenantIdQuotasByType" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Set or update a quota for a resource type
 *
 * Creates or updates the quota configuration for a specific resource type for a tenant.
 */
export interface PutTenantsQuotasInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesSetQuotaInput;
}
export type PutTenantsQuotasOutput = void;
export const putTenantsQuotasEndpoint = {
  operationId: "putTenantsQuotas" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete a quota for a resource type
 *
 * Removes the quota configuration for a specific resource type for a tenant.
 */
export interface DeleteTenantsQuotasInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
}
export type DeleteTenantsQuotasOutput = void;
export const deleteTenantsQuotasEndpoint = {
  operationId: "deleteTenantsQuotas" as const,
  method: "DELETE" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset quota usage to zero
 *
 * Resets the current usage counter for a specific resource quota to zero without changing the quota limits.
 */
export interface PostTenantsQuotasResetInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
}
export type PostTenantsQuotasResetOutput = void;
export const postTenantsQuotasResetEndpoint = {
  operationId: "postTenantsQuotasReset" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}:reset" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Toggle quota activation status
 *
 * Activates or deactivates a resource quota. Inactive quotas are not enforced.
 */
export interface PostTenantsQuotasToggleInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesToggleResourceQuotaInput;
}
export type PostTenantsQuotasToggleOutput = void;
export const postTenantsQuotasToggleEndpoint = {
  operationId: "postTenantsQuotasToggle" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}:toggle" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if a usage amount would exceed quota
 *
 * Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.
 */
export interface PostTenantsQuotasCheckInput {
  tenantId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesCheckResourceQuotaInput;
}
export type PostTenantsQuotasCheckOutput =
  Types.ResourcesResourceQuotaEnforcementResult;
export const postTenantsQuotasCheckEndpoint = {
  operationId: "postTenantsQuotasCheck" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/quotas/{type}:check" as const,
  tags: ["TenantsQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Get usage records for a tenant
 *
 * Retrieves paginated resource usage records for a specific tenant with optional filtering by type and date range.
 */
export interface GetTenantsResourcesUsageRecordsInput {
  tenantId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
    startDate?: string;
    endDate?: string;
    pageNumber?: number;
    pageSize?: number;
  };
}
export type GetTenantsResourcesUsageRecordsOutput = void;
export const getTenantsResourcesUsageRecordsEndpoint = {
  operationId: "getTenantsResourcesUsageRecords" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/resources/usage-records" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Get current usage summary for a tenant
 *
 * Retrieves the current aggregated resource usage summary for a specific tenant.
 */
export interface GetTenantsResourcesUsageSummaryInput {
  tenantId: string;
}
export type GetTenantsResourcesUsageSummaryOutput = {
  Users?: number;
  Projects?: number;
  Storage?: number;
  ApiCalls?: number;
  Programs?: number;
  Courses?: number;
  FeatureFlags?: number;
  SubscriptionPlans?: number;
  Products?: number;
  TestingSessions?: number;
  Roles?: number;
  Tenants?: number;
  Subscriptions?: number;
  SLOs?: number;
  AccessReviewCampaigns?: number;
  SoDRules?: number;
  AbacPolicies?: number;
  ConditionalPolicies?: number;
  Wallets?: number;
  Disputes?: number;
  PromoCodes?: number;
  Orders?: number;
  AuditEntries?: number;
  Assets?: number;
  AssetStorage?: number;
  AssetDownloads?: number;
  AssetTransformations?: number;
  AiRequests?: number;
  AiTokens?: number;
  Teams?: number;
};
export const getTenantsResourcesUsageSummaryEndpoint = {
  operationId: "getTenantsResourcesUsageSummary" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/resources/usage-summary" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Check resource limits for a tenant
 *
 * Checks current resource usage against configured limits for a specific tenant.
 */
export interface GetTenantsResourcesLimitsInput {
  tenantId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
  };
}
export type GetTenantsResourcesLimitsOutput = {
  Users?: boolean;
  Projects?: boolean;
  Storage?: boolean;
  ApiCalls?: boolean;
  Programs?: boolean;
  Courses?: boolean;
  FeatureFlags?: boolean;
  SubscriptionPlans?: boolean;
  Products?: boolean;
  TestingSessions?: boolean;
  Roles?: boolean;
  Tenants?: boolean;
  Subscriptions?: boolean;
  SLOs?: boolean;
  AccessReviewCampaigns?: boolean;
  SoDRules?: boolean;
  AbacPolicies?: boolean;
  ConditionalPolicies?: boolean;
  Wallets?: boolean;
  Disputes?: boolean;
  PromoCodes?: boolean;
  Orders?: boolean;
  AuditEntries?: boolean;
  Assets?: boolean;
  AssetStorage?: boolean;
  AssetDownloads?: boolean;
  AssetTransformations?: boolean;
  AiRequests?: boolean;
  AiTokens?: boolean;
  Teams?: boolean;
};
export const getTenantsResourcesLimitsEndpoint = {
  operationId: "getTenantsResourcesLimits" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/resources/limits" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Record resource usage for a tenant
 *
 * Records a new resource usage entry for the specified tenant.
 */
export interface PostTenantsResourcesRecordInput {
  tenantId: string;
  body?: Types.ResourcesRecordTenantResourceUsageInput;
}
export type PostTenantsResourcesRecordOutput = void;
export const postTenantsResourcesRecordEndpoint = {
  operationId: "postTenantsResourcesRecord" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/resources:record" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Record resource usage with quota enforcement for a tenant
 *
 * Records a new resource usage entry after verifying it doesn't exceed configured quotas. Returns 429 if quota would be exceeded.
 */
export interface PostTenantsResourcesRecordWithQuotaCheckInput {
  tenantId: string;
  body?: Types.ResourcesRecordTenantResourceUsageInput;
}
export type PostTenantsResourcesRecordWithQuotaCheckOutput = void;
export const postTenantsResourcesRecordWithQuotaCheckEndpoint = {
  operationId: "postTenantsResourcesRecordWithQuotaCheck" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/resources:record-with-quota-check" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset resource usage for a tenant
 *
 * Resets the resource usage counters for a specific tenant and resource type to zero.
 */
export interface PostTenantsResourcesResetInput {
  tenantId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
  };
}
export type PostTenantsResourcesResetOutput = void;
export const postTenantsResourcesResetEndpoint = {
  operationId: "postTenantsResourcesReset" as const,
  method: "POST" as const,
  path: "/v1/tenants/{tenantId}/resources:reset" as const,
  tags: ["TenantsResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all metadata entries for a tenant
 *
 * Retrieves all resource metadata entries for a specific tenant, optionally filtered by category.
 */
export interface GetTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataInput {
  tenantId: string;
  query?: {
    category?: string;
  };
}
export type GetTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataOutput =
  Array<Types.ResourcesResourceMetadata>;
export const getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataEndpoint =
  {
    operationId:
      "getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadata" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/resources/metadata" as const,
    tags: ["TenantsResourcesMetadata"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get a specific metadata entry by key
 *
 * Retrieves a specific resource metadata entry by its key for a tenant.
 */
export interface GetTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKeyInput {
  tenantId: string;
  key: string;
}
export type GetTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKeyOutput =
  Types.ResourcesResourceMetadata;
export const getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKeyEndpoint =
  {
    operationId:
      "getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKey" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/resources/metadata/{key}" as const,
    tags: ["TenantsResourcesMetadata"] as const,
    requiresAuth: true,
  } as const;

/**
 * Create or update a metadata entry
 *
 * Creates a new metadata entry or updates an existing one for a tenant.
 */
export interface PutTenantsResourcesMetadataInput {
  tenantId: string;
  key: string;
  body?: Types.ResourcesSetResourceMetadataInput;
}
export type PutTenantsResourcesMetadataOutput = Types.ResourcesResourceMetadata;
export const putTenantsResourcesMetadataEndpoint = {
  operationId: "putTenantsResourcesMetadata" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/resources/metadata/{key}" as const,
  tags: ["TenantsResourcesMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete a metadata entry
 *
 * Removes a resource metadata entry for a tenant.
 */
export interface DeleteTenantsResourcesMetadataInput {
  tenantId: string;
  key: string;
}
export type DeleteTenantsResourcesMetadataOutput = void;
export const deleteTenantsResourcesMetadataEndpoint = {
  operationId: "deleteTenantsResourcesMetadata" as const,
  method: "DELETE" as const,
  path: "/v1/tenants/{tenantId}/resources/metadata/{key}" as const,
  tags: ["TenantsResourcesMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all settings for a tenant
 *
 * Retrieves all resource settings for a specific tenant, optionally filtered by category.
 */
export interface GetTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsInput {
  tenantId: string;
  query?: {
    category?: string;
  };
}
export type GetTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsOutput =
  Array<Types.ResourcesResourceSettings>;
export const getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsEndpoint =
  {
    operationId:
      "getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettings" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/resources/settings" as const,
    tags: ["TenantsResourcesSettings"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get a specific setting by key
 *
 * Retrieves a specific resource setting by its key for a tenant.
 */
export interface GetTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKeyInput {
  tenantId: string;
  key: string;
}
export type GetTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKeyOutput =
  Types.ResourcesResourceSettings;
export const getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKeyEndpoint =
  {
    operationId:
      "getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKey" as const,
    method: "GET" as const,
    path: "/v1/tenants/{tenantId}/resources/settings/{key}" as const,
    tags: ["TenantsResourcesSettings"] as const,
    requiresAuth: true,
  } as const;

/**
 * Create or update a setting
 *
 * Creates a new setting or updates an existing one for a tenant.
 */
export interface PutTenantsResourcesSettingsInput {
  tenantId: string;
  key: string;
  body?: Types.ResourcesSetResourceSettingsInput;
}
export type PutTenantsResourcesSettingsOutput = Types.ResourcesResourceSettings;
export const putTenantsResourcesSettingsEndpoint = {
  operationId: "putTenantsResourcesSettings" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/resources/settings/{key}" as const,
  tags: ["TenantsResourcesSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete a setting
 *
 * Removes a resource setting for a tenant.
 */
export interface DeleteTenantsResourcesSettingsInput {
  tenantId: string;
  key: string;
}
export type DeleteTenantsResourcesSettingsOutput = void;
export const deleteTenantsResourcesSettingsEndpoint = {
  operationId: "deleteTenantsResourcesSettings" as const,
  method: "DELETE" as const,
  path: "/v1/tenants/{tenantId}/resources/settings/{key}" as const,
  tags: ["TenantsResourcesSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get effective value for a setting
 *
 * Retrieves the effective value for a setting, considering user-level overrides if a user ID is provided.
 */
export interface GetTenantsResourcesSettingsEffectiveInput {
  tenantId: string;
  key: string;
  query?: {
    userId?: string;
  };
}
export type GetTenantsResourcesSettingsEffectiveOutput =
  Types.ResourcesEffectiveSettingOutput;
export const getTenantsResourcesSettingsEffectiveEndpoint = {
  operationId: "getTenantsResourcesSettingsEffective" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/resources/settings/{key}/effective" as const,
  tags: ["TenantsResourcesSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant settings by tenant ID
 *
 * Retrieves comprehensive tenant settings including system configuration, feature toggles, business rules, and operational preferences.
 */
export interface GetTenantsSettingsInput {
  tenantId: string;
}
export type GetTenantsSettingsOutput = Types.IdentityTenantsTenantSettingsDto;
export const getTenantsSettingsEndpoint = {
  operationId: "getTenantsSettings" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/settings" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace all tenant settings by tenant ID
 *
 * Replaces all tenant settings with new values. All existing settings are replaced with the provided data.
 */
export interface PutTenantsSettingsInput {
  tenantId: string;
  body?: Types.IdentityTenantsReplaceTenantSettingsInput;
}
export type PutTenantsSettingsOutput = void;
export const putTenantsSettingsEndpoint = {
  operationId: "putTenantsSettings" as const,
  method: "PUT" as const,
  path: "/v1/tenants/{tenantId}/settings" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update tenant settings by tenant ID
 *
 * Updates specific tenant settings fields without affecting other settings. Only the provided settings are modified.
 */
export interface PatchTenantsSettingsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantSettingsInput;
}
export type PatchTenantsSettingsOutput = void;
export const patchTenantsSettingsEndpoint = {
  operationId: "patchTenantsSettings" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/settings" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant feature flags
 *
 * Retrieves all feature flags configured for the tenant for experimental features and A/B testing.
 */
export interface GetTenantsSettingsFeatureFlagsInput {
  tenantId: string;
}
export type GetTenantsSettingsFeatureFlagsOutput = Record<string, boolean>;
export const getTenantsSettingsFeatureFlagsEndpoint = {
  operationId: "getTenantsSettingsFeatureFlags" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/settings/feature-flags" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant feature flags
 *
 * Updates specific feature flags for the tenant. Existing flags not specified are preserved.
 */
export interface PatchTenantsSettingsFeatureFlagsInput {
  tenantId: string;
  body?: Record<string, boolean>;
}
export type PatchTenantsSettingsFeatureFlagsOutput = void;
export const patchTenantsSettingsFeatureFlagsEndpoint = {
  operationId: "patchTenantsSettingsFeatureFlags" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/settings/feature-flags" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant system limits
 *
 * Retrieves system limits and resource constraints configured for the tenant.
 */
export interface GetTenantsSettingsSystemLimitsInput {
  tenantId: string;
}
export type GetTenantsSettingsSystemLimitsOutput =
  Types.IdentityTenantsTenantSystemLimits;
export const getTenantsSettingsSystemLimitsEndpoint = {
  operationId: "getTenantsSettingsSystemLimits" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/settings/system-limits" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant system limits
 *
 * Updates system limits and resource constraints for the tenant.
 */
export interface PatchTenantsSettingsSystemLimitsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantSystemLimitsInput;
}
export type PatchTenantsSettingsSystemLimitsOutput = void;
export const patchTenantsSettingsSystemLimitsEndpoint = {
  operationId: "patchTenantsSettingsSystemLimits" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/settings/system-limits" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get tenant integration settings
 *
 * Retrieves third-party integration configurations for the tenant.
 */
export interface GetTenantsSettingsIntegrationSettingsInput {
  tenantId: string;
}
export type GetTenantsSettingsIntegrationSettingsOutput =
  Types.IdentityTenantsTenantIntegrationSettings;
export const getTenantsSettingsIntegrationSettingsEndpoint = {
  operationId: "getTenantsSettingsIntegrationSettings" as const,
  method: "GET" as const,
  path: "/v1/tenants/{tenantId}/settings/integration-settings" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant integration settings
 *
 * Updates third-party integration configurations for the tenant.
 */
export interface PatchTenantsSettingsIntegrationSettingsInput {
  tenantId: string;
  body?: Types.IdentityTenantsUpdateTenantIntegrationSettingsInput;
}
export type PatchTenantsSettingsIntegrationSettingsOutput = void;
export const patchTenantsSettingsIntegrationSettingsEndpoint = {
  operationId: "patchTenantsSettingsIntegrationSettings" as const,
  method: "PATCH" as const,
  path: "/v1/tenants/{tenantId}/settings/integration-settings" as const,
  tags: ["TenantsSettings"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingAnalyticsInput {
  query?: {
    fromDate?: string;
    toDate?: string;
    includeComparison?: boolean;
  };
}
export type GetTestingAnalyticsOutput =
  Types.TestingLabTestingLabAnalyticsReportProjection;
export const getTestingAnalyticsEndpoint = {
  operationId: "getTestingAnalytics" as const,
  method: "GET" as const,
  path: "/v1/testing/analytics" as const,
  tags: ["TestingLabTestingAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingAnalyticsExportInput {
  query?: {
    fromDate?: string;
    toDate?: string;
  };
}
export type GetTestingAnalyticsExportOutput = Blob;
export const getTestingAnalyticsExportEndpoint = {
  operationId: "getTestingAnalyticsExport" as const,
  method: "GET" as const,
  path: "/v1/testing/analytics/export" as const,
  tags: ["TestingLabTestingAnalytics"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsSlotsRegistrationsInput {
  slotId: string;
  query?: {
    status?: Types.TestingLabTestingSlotRegistrationStatus;
  };
}
export type GetTestingEventsSlotsRegistrationsOutput =
  Array<Types.TestingLabTestingSlotRegistrationProjection>;
export const getTestingEventsSlotsRegistrationsEndpoint = {
  operationId: "getTestingEventsSlotsRegistrations" as const,
  method: "GET" as const,
  path: "/v1/testing/events/slots/{slotId}/registrations" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsSlotsRegistrationsInput {
  slotId: string;
  body?: Types.TestingLabRegisterTestingEventSlotInput;
}
export type PostTestingEventsSlotsRegistrationsOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const postTestingEventsSlotsRegistrationsEndpoint = {
  operationId: "postTestingEventsSlotsRegistrations" as const,
  method: "POST" as const,
  path: "/v1/testing/events/slots/{slotId}/registrations" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingEventsRegistrationsInput {
  registrationId: string;
}
export type DeleteTestingEventsRegistrationsOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const deleteTestingEventsRegistrationsEndpoint = {
  operationId: "deleteTestingEventsRegistrations" as const,
  method: "DELETE" as const,
  path: "/v1/testing/events/registrations/{registrationId}" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsRegistrationsMeInput {
  query?: {
    eventId?: string;
  };
}
export type GetTestingEventsRegistrationsMeOutput =
  Array<Types.TestingLabTestingSlotRegistrationProjection>;
export const getTestingEventsRegistrationsMeEndpoint = {
  operationId: "getTestingEventsRegistrationsMe" as const,
  method: "GET" as const,
  path: "/v1/testing/events/registrations/me" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsParticipantsInput {
  query?: {
    search?: string;
    status?: Types.TestingLabTestingSlotRegistrationStatus;
    skip?: number;
    take?: number;
  };
}
export type GetTestingEventsParticipantsOutput =
  Types.TestingLabTestingParticipantDirectoryProjection;
export const getTestingEventsParticipantsEndpoint = {
  operationId: "getTestingEventsParticipants" as const,
  method: "GET" as const,
  path: "/v1/testing/events/participants" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRegistrationsCheckInInput {
  registrationId: string;
}
export type PostTestingEventsRegistrationsCheckInOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const postTestingEventsRegistrationsCheckInEndpoint = {
  operationId: "postTestingEventsRegistrationsCheckIn" as const,
  method: "POST" as const,
  path: "/v1/testing/events/registrations/{registrationId}:check-in" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRegistrationsCheckOutInput {
  registrationId: string;
}
export type PostTestingEventsRegistrationsCheckOutOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const postTestingEventsRegistrationsCheckOutEndpoint = {
  operationId: "postTestingEventsRegistrationsCheckOut" as const,
  method: "POST" as const,
  path: "/v1/testing/events/registrations/{registrationId}:check-out" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRegistrationsNoShowInput {
  registrationId: string;
}
export type PostTestingEventsRegistrationsNoShowOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const postTestingEventsRegistrationsNoShowEndpoint = {
  operationId: "postTestingEventsRegistrationsNoShow" as const,
  method: "POST" as const,
  path: "/v1/testing/events/registrations/{registrationId}:no-show" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRegistrationsTestedProjectsInput {
  registrationId: string;
  body?: Types.TestingLabAssignTestingProjectToTesterInput;
}
export type PostTestingEventsRegistrationsTestedProjectsOutput =
  Types.TestingLabTestingFeedbackObligationProjection;
export const postTestingEventsRegistrationsTestedProjectsEndpoint = {
  operationId: "postTestingEventsRegistrationsTestedProjects" as const,
  method: "POST" as const,
  path: "/v1/testing/events/registrations/{registrationId}/tested-projects" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRegistrationsCompleteInput {
  registrationId: string;
}
export type PostTestingEventsRegistrationsCompleteOutput =
  Types.TestingLabTestingSlotRegistrationProjection;
export const postTestingEventsRegistrationsCompleteEndpoint = {
  operationId: "postTestingEventsRegistrationsComplete" as const,
  method: "POST" as const,
  path: "/v1/testing/events/registrations/{registrationId}:complete" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsFeedbackObligationsMeInput {
  query?: {
    eventId?: string;
  };
}
export type GetTestingEventsFeedbackObligationsMeOutput =
  Array<Types.TestingLabTestingFeedbackObligationProjection>;
export const getTestingEventsFeedbackObligationsMeEndpoint = {
  operationId: "getTestingEventsFeedbackObligationsMe" as const,
  method: "GET" as const,
  path: "/v1/testing/events/feedback-obligations/me" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsFeedbackInput {
  eventId: string;
}
export type GetTestingEventsFeedbackOutput =
  Array<Types.TestingLabTestingEventFeedbackReviewProjection>;
export const getTestingEventsFeedbackEndpoint = {
  operationId: "getTestingEventsFeedback" as const,
  method: "GET" as const,
  path: "/v1/testing/events/{eventId}/feedback" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsFeedbackObligationsFeedbackInput {
  obligationId: string;
  body?: Types.TestingLabSubmitTestingEventFeedbackInput;
}
export type PostTestingEventsFeedbackObligationsFeedbackOutput =
  Types.TestingLabTestingEventFeedbackProjection;
export const postTestingEventsFeedbackObligationsFeedbackEndpoint = {
  operationId: "postTestingEventsFeedbackObligationsFeedback" as const,
  method: "POST" as const,
  path: "/v1/testing/events/feedback-obligations/{obligationId}/feedback" as const,
  tags: ["TestingLabTestingEventParticipation"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsForGetTestingEventsInput {
  query?: {
    status?: Types.TestingLabTestingEventStatus;
    skip?: number;
    take?: number;
  };
}
export type GetTestingEventsForGetTestingEventsOutput =
  Array<Types.TestingLabTestingEventProjection>;
export const getTestingEventsForGetTestingEventsEndpoint = {
  operationId: "getTestingEventsForGetTestingEvents" as const,
  method: "GET" as const,
  path: "/v1/testing/events" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsInput {
  body?: Types.TestingLabCreateTestingEventInput;
}
export type PostTestingEventsOutput = Types.TestingLabTestingEventProjection;
export const postTestingEventsEndpoint = {
  operationId: "postTestingEvents" as const,
  method: "POST" as const,
  path: "/v1/testing/events" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsArchivedInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetTestingEventsArchivedOutput =
  Array<Types.TestingLabTestingEventProjection>;
export const getTestingEventsArchivedEndpoint = {
  operationId: "getTestingEventsArchived" as const,
  method: "GET" as const,
  path: "/v1/testing/events/archived" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsPublicForGetTestingEventsPublicInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetTestingEventsPublicForGetTestingEventsPublicOutput =
  Array<Types.TestingLabPublicTestingEventProjection>;
export const getTestingEventsPublicForGetTestingEventsPublicEndpoint = {
  operationId: "getTestingEventsPublicForGetTestingEventsPublic" as const,
  method: "GET" as const,
  path: "/v1/testing/events/public" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsPublicForGetTestingEventsPublicByEventIdInput {
  eventId: string;
}
export type GetTestingEventsPublicForGetTestingEventsPublicByEventIdOutput =
  Types.TestingLabPublicTestingEventProjection;
export const getTestingEventsPublicForGetTestingEventsPublicByEventIdEndpoint =
  {
    operationId:
      "getTestingEventsPublicForGetTestingEventsPublicByEventId" as const,
    method: "GET" as const,
    path: "/v1/testing/events/public/{eventId}" as const,
    tags: ["TestingLabTestingEvents"] as const,
    requiresAuth: true,
  } as const;

export interface GetTestingEventsForGetTestingEventsByEventIdInput {
  eventId: string;
}
export type GetTestingEventsForGetTestingEventsByEventIdOutput =
  Types.TestingLabTestingEventProjection;
export const getTestingEventsForGetTestingEventsByEventIdEndpoint = {
  operationId: "getTestingEventsForGetTestingEventsByEventId" as const,
  method: "GET" as const,
  path: "/v1/testing/events/{eventId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingEventsInput {
  eventId: string;
  body?: Types.TestingLabUpdateTestingEventInput;
}
export type PutTestingEventsOutput = Types.TestingLabTestingEventProjection;
export const putTestingEventsEndpoint = {
  operationId: "putTestingEvents" as const,
  method: "PUT" as const,
  path: "/v1/testing/events/{eventId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingEventsInput {
  eventId: string;
}
export type DeleteTestingEventsOutput = boolean;
export const deleteTestingEventsEndpoint = {
  operationId: "deleteTestingEvents" as const,
  method: "DELETE" as const,
  path: "/v1/testing/events/{eventId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsArchiveInput {
  eventId: string;
}
export type PostTestingEventsArchiveOutput = boolean;
export const postTestingEventsArchiveEndpoint = {
  operationId: "postTestingEventsArchive" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:archive" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsRestoreInput {
  eventId: string;
}
export type PostTestingEventsRestoreOutput = boolean;
export const postTestingEventsRestoreEndpoint = {
  operationId: "postTestingEventsRestore" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:restore" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsOpenApplicationsInput {
  eventId: string;
}
export type PostTestingEventsOpenApplicationsOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsOpenApplicationsEndpoint = {
  operationId: "postTestingEventsOpenApplications" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:open-applications" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsCloseApplicationsInput {
  eventId: string;
}
export type PostTestingEventsCloseApplicationsOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsCloseApplicationsEndpoint = {
  operationId: "postTestingEventsCloseApplications" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:close-applications" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsScheduleInput {
  eventId: string;
}
export type PostTestingEventsScheduleOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsScheduleEndpoint = {
  operationId: "postTestingEventsSchedule" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:schedule" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsActivateInput {
  eventId: string;
}
export type PostTestingEventsActivateOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsActivateEndpoint = {
  operationId: "postTestingEventsActivate" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:activate" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsCompleteInput {
  eventId: string;
}
export type PostTestingEventsCompleteOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsCompleteEndpoint = {
  operationId: "postTestingEventsComplete" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:complete" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsCancelInput {
  eventId: string;
  body?: Types.TestingLabCancelTestingEventInput;
}
export type PostTestingEventsCancelOutput =
  Types.TestingLabTestingEventProjection;
export const postTestingEventsCancelEndpoint = {
  operationId: "postTestingEventsCancel" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}:cancel" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingEventsLearningInput {
  eventId: string;
  body?: Types.TestingLabConfigureTestingEventLearningInput;
}
export type PutTestingEventsLearningOutput =
  Types.TestingLabTestingEventProjection;
export const putTestingEventsLearningEndpoint = {
  operationId: "putTestingEventsLearning" as const,
  method: "PUT" as const,
  path: "/v1/testing/events/{eventId}/learning" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsSlotsInput {
  eventId: string;
}
export type GetTestingEventsSlotsOutput =
  Array<Types.TestingLabTestingEventSlotProjection>;
export const getTestingEventsSlotsEndpoint = {
  operationId: "getTestingEventsSlots" as const,
  method: "GET" as const,
  path: "/v1/testing/events/{eventId}/slots" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsSlotsInput {
  eventId: string;
  body?: Types.TestingLabUpsertTestingEventSlotInput;
}
export type PostTestingEventsSlotsOutput =
  Types.TestingLabTestingEventSlotProjection;
export const postTestingEventsSlotsEndpoint = {
  operationId: "postTestingEventsSlots" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}/slots" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingEventsSlotsInput {
  eventId: string;
  slotId: string;
  body?: Types.TestingLabUpsertTestingEventSlotInput;
}
export type PutTestingEventsSlotsOutput =
  Types.TestingLabTestingEventSlotProjection;
export const putTestingEventsSlotsEndpoint = {
  operationId: "putTestingEventsSlots" as const,
  method: "PUT" as const,
  path: "/v1/testing/events/{eventId}/slots/{slotId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingEventsSlotsInput {
  eventId: string;
  slotId: string;
}
export type DeleteTestingEventsSlotsOutput = boolean;
export const deleteTestingEventsSlotsEndpoint = {
  operationId: "deleteTestingEventsSlots" as const,
  method: "DELETE" as const,
  path: "/v1/testing/events/{eventId}/slots/{slotId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsCommitteeInput {
  eventId: string;
}
export type GetTestingEventsCommitteeOutput =
  Array<Types.TestingLabTestingEventCommitteeMemberProjection>;
export const getTestingEventsCommitteeEndpoint = {
  operationId: "getTestingEventsCommittee" as const,
  method: "GET" as const,
  path: "/v1/testing/events/{eventId}/committee" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsCommitteeInput {
  eventId: string;
  body?: Types.TestingLabAddTestingEventCommitteeMemberInput;
}
export type PostTestingEventsCommitteeOutput =
  Types.TestingLabTestingEventCommitteeMemberProjection;
export const postTestingEventsCommitteeEndpoint = {
  operationId: "postTestingEventsCommittee" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}/committee" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingEventsCommitteeInput {
  eventId: string;
  userId: string;
}
export type DeleteTestingEventsCommitteeOutput = boolean;
export const deleteTestingEventsCommitteeEndpoint = {
  operationId: "deleteTestingEventsCommittee" as const,
  method: "DELETE" as const,
  path: "/v1/testing/events/{eventId}/committee/{userId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsApplicationsForGetTestingEventsByEventIdApplicationsInput {
  eventId: string;
  query?: {
    status?: Types.TestingLabTestingApplicationStatus;
    skip?: number;
    take?: number;
  };
}
export type GetTestingEventsApplicationsForGetTestingEventsByEventIdApplicationsOutput =
  Array<Types.TestingLabTestingProjectApplicationProjection>;
export const getTestingEventsApplicationsForGetTestingEventsByEventIdApplicationsEndpoint =
  {
    operationId:
      "getTestingEventsApplicationsForGetTestingEventsByEventIdApplications" as const,
    method: "GET" as const,
    path: "/v1/testing/events/{eventId}/applications" as const,
    tags: ["TestingLabTestingEvents"] as const,
    requiresAuth: true,
  } as const;

export interface PostTestingEventsApplicationsInput {
  eventId: string;
  body?: Types.TestingLabSubmitTestingProjectApplicationInput;
}
export type PostTestingEventsApplicationsOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsEndpoint = {
  operationId: "postTestingEventsApplications" as const,
  method: "POST" as const,
  path: "/v1/testing/events/{eventId}/applications" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsApplicationsTesterEligibilityInput {
  eventId: string;
  query?: {
    testerUserIds?: Array<string>;
  };
}
export type GetTestingEventsApplicationsTesterEligibilityOutput =
  Array<Types.TestingLabTestingApplicationTesterEligibilityProjection>;
export const getTestingEventsApplicationsTesterEligibilityEndpoint = {
  operationId: "getTestingEventsApplicationsTesterEligibility" as const,
  method: "GET" as const,
  path: "/v1/testing/events/{eventId}/applications/tester-eligibility" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsApplicationsMeInput {
  query?: {
    eventId?: string;
  };
}
export type GetTestingEventsApplicationsMeOutput =
  Array<Types.TestingLabTestingProjectApplicationProjection>;
export const getTestingEventsApplicationsMeEndpoint = {
  operationId: "getTestingEventsApplicationsMe" as const,
  method: "GET" as const,
  path: "/v1/testing/events/applications/me" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationIdInput {
  applicationId: string;
}
export type GetTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationIdOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const getTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationIdEndpoint =
  {
    operationId:
      "getTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationId" as const,
    method: "GET" as const,
    path: "/v1/testing/events/applications/{applicationId}" as const,
    tags: ["TestingLabTestingEvents"] as const,
    requiresAuth: true,
  } as const;

export interface PutTestingEventsApplicationsInput {
  applicationId: string;
  body?: Types.TestingLabUpdateTestingProjectApplicationInput;
}
export type PutTestingEventsApplicationsOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const putTestingEventsApplicationsEndpoint = {
  operationId: "putTestingEventsApplications" as const,
  method: "PUT" as const,
  path: "/v1/testing/events/applications/{applicationId}" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingEventsApplicationsReviewPackageInput {
  applicationId: string;
}
export type GetTestingEventsApplicationsReviewPackageOutput =
  Types.TestingLabTestingApplicationReviewPackageProjection;
export const getTestingEventsApplicationsReviewPackageEndpoint = {
  operationId: "getTestingEventsApplicationsReviewPackage" as const,
  method: "GET" as const,
  path: "/v1/testing/events/applications/{applicationId}/review-package" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsWithdrawInput {
  applicationId: string;
}
export type PostTestingEventsApplicationsWithdrawOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsWithdrawEndpoint = {
  operationId: "postTestingEventsApplicationsWithdraw" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}:withdraw" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsReviewInput {
  applicationId: string;
}
export type PostTestingEventsApplicationsReviewOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsReviewEndpoint = {
  operationId: "postTestingEventsApplicationsReview" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}:review" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsVotesInput {
  applicationId: string;
  body?: Types.TestingLabCastTestingApplicationVoteInput;
}
export type PostTestingEventsApplicationsVotesOutput =
  Types.TestingLabTestingApplicationVoteProjection;
export const postTestingEventsApplicationsVotesEndpoint = {
  operationId: "postTestingEventsApplicationsVotes" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}/votes" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsApproveInput {
  applicationId: string;
  body?: Types.TestingLabDecideTestingProjectApplicationInput;
}
export type PostTestingEventsApplicationsApproveOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsApproveEndpoint = {
  operationId: "postTestingEventsApplicationsApprove" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}:approve" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsRejectInput {
  applicationId: string;
  body?: Types.TestingLabDecideTestingProjectApplicationInput;
}
export type PostTestingEventsApplicationsRejectOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsRejectEndpoint = {
  operationId: "postTestingEventsApplicationsReject" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}:reject" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingEventsApplicationsWaitlistInput {
  applicationId: string;
  body?: Types.TestingLabDecideTestingProjectApplicationInput;
}
export type PostTestingEventsApplicationsWaitlistOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const postTestingEventsApplicationsWaitlistEndpoint = {
  operationId: "postTestingEventsApplicationsWaitlist" as const,
  method: "POST" as const,
  path: "/v1/testing/events/applications/{applicationId}:waitlist" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingEventsApplicationsSlotInput {
  applicationId: string;
  body?: Types.TestingLabAssignTestingProjectApplicationSlotInput;
}
export type PutTestingEventsApplicationsSlotOutput =
  Types.TestingLabTestingProjectApplicationProjection;
export const putTestingEventsApplicationsSlotEndpoint = {
  operationId: "putTestingEventsApplicationsSlot" as const,
  method: "PUT" as const,
  path: "/v1/testing/events/applications/{applicationId}/slot" as const,
  tags: ["TestingLabTestingEvents"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingFeedbackInput {
  query?: {
    Search?: string;
    Source?: Types.TestingLabTestingFeedbackSource;
    EventId?: string;
    RequestId?: string;
    UserId?: string;
    Reported?: boolean;
    Quality?: Types.TestingLabFeedbackQuality;
    Skip?: number;
    Take?: number;
  };
}
export type GetTestingFeedbackOutput =
  Types.TestingLabTestingFeedbackDirectoryPage;
export const getTestingFeedbackEndpoint = {
  operationId: "getTestingFeedback" as const,
  method: "GET" as const,
  path: "/v1/testing/feedback" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingFeedbackInput {
  body?: Types.TestingLabSubmitFeedback;
}
export type PostTestingFeedbackOutput = void;
export const postTestingFeedbackEndpoint = {
  operationId: "postTestingFeedback" as const,
  method: "POST" as const,
  path: "/v1/testing/feedback" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsFeedbackInput {
  requestId: string;
}
export type GetTestingRequestsFeedbackOutput =
  Array<Types.TestingLabTestingFeedback>;
export const getTestingRequestsFeedbackEndpoint = {
  operationId: "getTestingRequestsFeedback" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{requestId}/feedback" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingRequestsFeedbackInput {
  requestId: string;
  body?: Types.TestingLabFeedbackInput;
}
export type PostTestingRequestsFeedbackOutput = Types.TestingLabTestingFeedback;
export const postTestingRequestsFeedbackEndpoint = {
  operationId: "postTestingRequestsFeedback" as const,
  method: "POST" as const,
  path: "/v1/testing/requests/{requestId}/feedback" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingFeedbackByUserInput {
  userId: string;
}
export type GetTestingFeedbackByUserOutput =
  Array<Types.TestingLabTestingFeedback>;
export const getTestingFeedbackByUserEndpoint = {
  operationId: "getTestingFeedbackByUser" as const,
  method: "GET" as const,
  path: "/v1/testing/feedback/by-user/{userId}" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingFeedbackReportInput {
  feedbackId: string;
  body?: Types.TestingLabReportFeedback;
}
export type PostTestingFeedbackReportOutput = void;
export const postTestingFeedbackReportEndpoint = {
  operationId: "postTestingFeedbackReport" as const,
  method: "POST" as const,
  path: "/v1/testing/feedback/{feedbackId}/report" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingFeedbackQualityInput {
  feedbackId: string;
  body?: Types.TestingLabRateFeedbackQuality;
}
export type PostTestingFeedbackQualityOutput = void;
export const postTestingFeedbackQualityEndpoint = {
  operationId: "postTestingFeedbackQuality" as const,
  method: "POST" as const,
  path: "/v1/testing/feedback/{feedbackId}/quality" as const,
  tags: ["TestingLabTestingFeedback"] as const,
  requiresAuth: true,
} as const;

export type GetApiTestingLabPermissionsRoleTemplatesInput = void;
export type GetApiTestingLabPermissionsRoleTemplatesOutput =
  Array<Types.TestingLabTestingLabRoleTemplate>;
export const getApiTestingLabPermissionsRoleTemplatesEndpoint = {
  operationId: "getApiTestingLabPermissionsRoleTemplates" as const,
  method: "GET" as const,
  path: "/api/testing-lab/permissions/role-templates" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface PostApiTestingLabPermissionsRoleTemplatesInput {
  body?: Types.TestingLabCreateTestingLabRoleInput;
}
export type PostApiTestingLabPermissionsRoleTemplatesOutput =
  Types.TestingLabTestingLabRoleTemplate;
export const postApiTestingLabPermissionsRoleTemplatesEndpoint = {
  operationId: "postApiTestingLabPermissionsRoleTemplates" as const,
  method: "POST" as const,
  path: "/api/testing-lab/permissions/role-templates" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface PutApiTestingLabPermissionsRoleTemplatesInput {
  idOrName: string;
  body?: Types.TestingLabUpdateTestingLabRoleInput;
}
export type PutApiTestingLabPermissionsRoleTemplatesOutput =
  Types.TestingLabTestingLabRoleTemplate;
export const putApiTestingLabPermissionsRoleTemplatesEndpoint = {
  operationId: "putApiTestingLabPermissionsRoleTemplates" as const,
  method: "PUT" as const,
  path: "/api/testing-lab/permissions/role-templates/{idOrName}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiTestingLabPermissionsRoleTemplatesInput {
  idOrName: string;
}
export type DeleteApiTestingLabPermissionsRoleTemplatesOutput = void;
export const deleteApiTestingLabPermissionsRoleTemplatesEndpoint = {
  operationId: "deleteApiTestingLabPermissionsRoleTemplates" as const,
  method: "DELETE" as const,
  path: "/api/testing-lab/permissions/role-templates/{idOrName}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiTestingLabPermissionsRoleTemplatesByNameInput {
  name: string;
}
export type DeleteApiTestingLabPermissionsRoleTemplatesByNameOutput = void;
export const deleteApiTestingLabPermissionsRoleTemplatesByNameEndpoint = {
  operationId: "deleteApiTestingLabPermissionsRoleTemplatesByName" as const,
  method: "DELETE" as const,
  path: "/api/testing-lab/permissions/role-templates/by-name/{name}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface GetApiTestingLabPermissionsUsersInput {
  userId: string;
  query?: {
    tenantId?: string;
  };
}
export type GetApiTestingLabPermissionsUsersOutput =
  Types.TestingLabUserTestingLabPermissions;
export const getApiTestingLabPermissionsUsersEndpoint = {
  operationId: "getApiTestingLabPermissionsUsers" as const,
  method: "GET" as const,
  path: "/api/testing-lab/permissions/users/{userId}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface PostApiTestingLabPermissionsUsersRolesInput {
  userId: string;
  body?: Types.TestingLabAssignTestingLabRoleInput;
}
export type PostApiTestingLabPermissionsUsersRolesOutput = void;
export const postApiTestingLabPermissionsUsersRolesEndpoint = {
  operationId: "postApiTestingLabPermissionsUsersRoles" as const,
  method: "POST" as const,
  path: "/api/testing-lab/permissions/users/{userId}/roles" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiTestingLabPermissionsUsersRolesInput {
  userId: string;
  roleName: string;
  query?: {
    tenantId?: string;
  };
}
export type DeleteApiTestingLabPermissionsUsersRolesOutput = void;
export const deleteApiTestingLabPermissionsUsersRolesEndpoint = {
  operationId: "deleteApiTestingLabPermissionsUsersRoles" as const,
  method: "DELETE" as const,
  path: "/api/testing-lab/permissions/users/{userId}/roles/{roleName}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface PostApiTestingLabPermissionsUsersResourcesInput {
  userId: string;
  resourceType: string;
  resourceId: string;
  body?: Types.TestingLabGrantResourcePermissionInput;
}
export type PostApiTestingLabPermissionsUsersResourcesOutput = void;
export const postApiTestingLabPermissionsUsersResourcesEndpoint = {
  operationId: "postApiTestingLabPermissionsUsersResources" as const,
  method: "POST" as const,
  path: "/api/testing-lab/permissions/users/{userId}/resources/{resourceType}/{resourceId}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiTestingLabPermissionsUsersResourcesInput {
  userId: string;
  resourceType: string;
  resourceId: string;
  query?: {
    action?: string;
    tenantId?: string;
  };
}
export type DeleteApiTestingLabPermissionsUsersResourcesOutput = void;
export const deleteApiTestingLabPermissionsUsersResourcesEndpoint = {
  operationId: "deleteApiTestingLabPermissionsUsersResources" as const,
  method: "DELETE" as const,
  path: "/api/testing-lab/permissions/users/{userId}/resources/{resourceType}/{resourceId}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export interface GetApiTestingLabPermissionsUsersCheckInput {
  userId: string;
  resourceType: string;
  query?: {
    action?: string;
    resourceId?: string;
    tenantId?: string;
  };
}
export type GetApiTestingLabPermissionsUsersCheckOutput = boolean;
export const getApiTestingLabPermissionsUsersCheckEndpoint = {
  operationId: "getApiTestingLabPermissionsUsersCheck" as const,
  method: "GET" as const,
  path: "/api/testing-lab/permissions/users/{userId}/check/{resourceType}" as const,
  tags: ["TestingLabPermission"] as const,
  requiresAuth: true,
} as const;

export type GetApiTestingLabSettingsInput = void;
export type GetApiTestingLabSettingsOutput = Types.TestingLabTestingLabSettings;
export const getApiTestingLabSettingsEndpoint = {
  operationId: "getApiTestingLabSettings" as const,
  method: "GET" as const,
  path: "/api/testing-lab/settings" as const,
  tags: ["TestingLabSettings"] as const,
  requiresAuth: true,
} as const;

export interface PutApiTestingLabSettingsInput {
  body?: Types.TestingLabCreateTestingLabSettings;
}
export type PutApiTestingLabSettingsOutput = Types.TestingLabTestingLabSettings;
export const putApiTestingLabSettingsEndpoint = {
  operationId: "putApiTestingLabSettings" as const,
  method: "PUT" as const,
  path: "/api/testing-lab/settings" as const,
  tags: ["TestingLabSettings"] as const,
  requiresAuth: true,
} as const;

export interface PatchApiTestingLabSettingsInput {
  body?: Types.TestingLabUpdateTestingLabSettings;
}
export type PatchApiTestingLabSettingsOutput =
  Types.TestingLabTestingLabSettings;
export const patchApiTestingLabSettingsEndpoint = {
  operationId: "patchApiTestingLabSettings" as const,
  method: "PATCH" as const,
  path: "/api/testing-lab/settings" as const,
  tags: ["TestingLabSettings"] as const,
  requiresAuth: true,
} as const;

export type PostApiTestingLabSettingsResetInput = void;
export type PostApiTestingLabSettingsResetOutput =
  Types.TestingLabTestingLabSettings;
export const postApiTestingLabSettingsResetEndpoint = {
  operationId: "postApiTestingLabSettingsReset" as const,
  method: "POST" as const,
  path: "/api/testing-lab/settings/reset" as const,
  tags: ["TestingLabSettings"] as const,
  requiresAuth: true,
} as const;

export type GetApiTestingLabSettingsExistsInput = void;
export type GetApiTestingLabSettingsExistsOutput = boolean;
export const getApiTestingLabSettingsExistsEndpoint = {
  operationId: "getApiTestingLabSettingsExists" as const,
  method: "GET" as const,
  path: "/api/testing-lab/settings/exists" as const,
  tags: ["TestingLabSettings"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingLocationsForGetTestingLocationsInput {
  query?: {
    skip?: number;
    take?: number;
    includeArchived?: boolean;
  };
}
export type GetTestingLocationsForGetTestingLocationsOutput =
  Array<Types.TestingLabTestingLocation>;
export const getTestingLocationsForGetTestingLocationsEndpoint = {
  operationId: "getTestingLocationsForGetTestingLocations" as const,
  method: "GET" as const,
  path: "/v1/testing/locations" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingLocationsInput {
  body?: Types.TestingLabCreateTestingLocation;
}
export type PostTestingLocationsOutput = Types.TestingLabTestingLocation;
export const postTestingLocationsEndpoint = {
  operationId: "postTestingLocations" as const,
  method: "POST" as const,
  path: "/v1/testing/locations" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingLocationsForGetTestingLocationsByIdInput {
  id: string;
}
export type GetTestingLocationsForGetTestingLocationsByIdOutput =
  Types.TestingLabTestingLocation;
export const getTestingLocationsForGetTestingLocationsByIdEndpoint = {
  operationId: "getTestingLocationsForGetTestingLocationsById" as const,
  method: "GET" as const,
  path: "/v1/testing/locations/{id}" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingLocationsInput {
  id: string;
  body?: Types.TestingLabUpdateTestingLocation;
}
export type PutTestingLocationsOutput = Types.TestingLabTestingLocation;
export const putTestingLocationsEndpoint = {
  operationId: "putTestingLocations" as const,
  method: "PUT" as const,
  path: "/v1/testing/locations/{id}" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingLocationsInput {
  id: string;
}
export type DeleteTestingLocationsOutput = void;
export const deleteTestingLocationsEndpoint = {
  operationId: "deleteTestingLocations" as const,
  method: "DELETE" as const,
  path: "/v1/testing/locations/{id}" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingLocationsRestoreInput {
  id: string;
}
export type PostTestingLocationsRestoreOutput = void;
export const postTestingLocationsRestoreEndpoint = {
  operationId: "postTestingLocationsRestore" as const,
  method: "POST" as const,
  path: "/v1/testing/locations/{id}/restore" as const,
  tags: ["TestingLabTestingLocations"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingRequestsParticipantsInput {
  requestId: string;
  userId: string;
}
export type PostTestingRequestsParticipantsOutput =
  Types.TestingLabTestingParticipantMutationProjection;
export const postTestingRequestsParticipantsEndpoint = {
  operationId: "postTestingRequestsParticipants" as const,
  method: "POST" as const,
  path: "/v1/testing/requests/{requestId}/participants/{userId}" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingRequestsParticipantsInput {
  requestId: string;
  userId: string;
}
export type DeleteTestingRequestsParticipantsOutput = void;
export const deleteTestingRequestsParticipantsEndpoint = {
  operationId: "deleteTestingRequestsParticipants" as const,
  method: "DELETE" as const,
  path: "/v1/testing/requests/{requestId}/participants/{userId}" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsParticipantsInput {
  requestId: string;
}
export type GetTestingRequestsParticipantsOutput =
  Array<Types.TestingLabTestingParticipant>;
export const getTestingRequestsParticipantsEndpoint = {
  operationId: "getTestingRequestsParticipants" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{requestId}/participants" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsParticipantsCheckInput {
  requestId: string;
  userId: string;
}
export type GetTestingRequestsParticipantsCheckOutput = boolean;
export const getTestingRequestsParticipantsCheckEndpoint = {
  operationId: "getTestingRequestsParticipantsCheck" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{requestId}/participants/{userId}/check" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsRegisterInput {
  sessionId: string;
  body?: Types.TestingLabSessionRegistrationInput;
}
export type PostTestingSessionsRegisterOutput =
  Types.TestingLabSessionRegistration;
export const postTestingSessionsRegisterEndpoint = {
  operationId: "postTestingSessionsRegister" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions/{sessionId}/register" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingSessionsRegisterInput {
  sessionId: string;
}
export type DeleteTestingSessionsRegisterOutput = void;
export const deleteTestingSessionsRegisterEndpoint = {
  operationId: "deleteTestingSessionsRegister" as const,
  method: "DELETE" as const,
  path: "/v1/testing/sessions/{sessionId}/register" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsRegistrationsInput {
  sessionId: string;
}
export type GetTestingSessionsRegistrationsOutput =
  Array<Types.TestingLabSessionRegistration>;
export const getTestingSessionsRegistrationsEndpoint = {
  operationId: "getTestingSessionsRegistrations" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{sessionId}/registrations" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsWaitlistInput {
  sessionId: string;
}
export type GetTestingSessionsWaitlistOutput =
  Array<Types.TestingLabSessionWaitlist>;
export const getTestingSessionsWaitlistEndpoint = {
  operationId: "getTestingSessionsWaitlist" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{sessionId}/waitlist" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsWaitlistInput {
  sessionId: string;
  body?: Types.TestingLabSessionRegistrationInput;
}
export type PostTestingSessionsWaitlistOutput = Types.TestingLabSessionWaitlist;
export const postTestingSessionsWaitlistEndpoint = {
  operationId: "postTestingSessionsWaitlist" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions/{sessionId}/waitlist" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingSessionsWaitlistInput {
  sessionId: string;
}
export type DeleteTestingSessionsWaitlistOutput = void;
export const deleteTestingSessionsWaitlistEndpoint = {
  operationId: "deleteTestingSessionsWaitlist" as const,
  method: "DELETE" as const,
  path: "/v1/testing/sessions/{sessionId}/waitlist" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingUsersActivityInput {
  userId: string;
}
export type GetTestingUsersActivityOutput = void;
export const getTestingUsersActivityEndpoint = {
  operationId: "getTestingUsersActivity" as const,
  method: "GET" as const,
  path: "/v1/testing/users/{userId}/activity" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export type GetTestingAttendanceStudentsInput = void;
export type GetTestingAttendanceStudentsOutput = void;
export const getTestingAttendanceStudentsEndpoint = {
  operationId: "getTestingAttendanceStudents" as const,
  method: "GET" as const,
  path: "/v1/testing/attendance/students" as const,
  tags: ["TestingLabTestingParticipants"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsForGetTestingRequestsInput {
  query?: {
    skip?: number;
    take?: number;
    includeArchived?: boolean;
  };
}
export type GetTestingRequestsForGetTestingRequestsOutput =
  Array<Types.TestingLabTestingRequestDetailProjection>;
export const getTestingRequestsForGetTestingRequestsEndpoint = {
  operationId: "getTestingRequestsForGetTestingRequests" as const,
  method: "GET" as const,
  path: "/v1/testing/requests" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingRequestsInput {
  body?: Types.TestingLabCreateTestingInput;
}
export type PostTestingRequestsOutput = Types.TestingLabTestingInput;
export const postTestingRequestsEndpoint = {
  operationId: "postTestingRequests" as const,
  method: "POST" as const,
  path: "/v1/testing/requests" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsForGetTestingRequestsByIdInput {
  id: string;
}
export type GetTestingRequestsForGetTestingRequestsByIdOutput =
  Types.TestingLabTestingRequestDetailProjection;
export const getTestingRequestsForGetTestingRequestsByIdEndpoint = {
  operationId: "getTestingRequestsForGetTestingRequestsById" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{id}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingRequestsInput {
  id: string;
  body?: Types.TestingLabUpdateTestingInput;
}
export type PutTestingRequestsOutput =
  Types.TestingLabTestingRequestDetailProjection;
export const putTestingRequestsEndpoint = {
  operationId: "putTestingRequests" as const,
  method: "PUT" as const,
  path: "/v1/testing/requests/{id}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingRequestsInput {
  id: string;
}
export type DeleteTestingRequestsOutput = void;
export const deleteTestingRequestsEndpoint = {
  operationId: "deleteTestingRequests" as const,
  method: "DELETE" as const,
  path: "/v1/testing/requests/{id}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsDetailsInput {
  id: string;
}
export type GetTestingRequestsDetailsOutput = Types.TestingLabTestingInput;
export const getTestingRequestsDetailsEndpoint = {
  operationId: "getTestingRequestsDetails" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{id}/details" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingRequestsRestoreInput {
  id: string;
}
export type PostTestingRequestsRestoreOutput = void;
export const postTestingRequestsRestoreEndpoint = {
  operationId: "postTestingRequestsRestore" as const,
  method: "POST" as const,
  path: "/v1/testing/requests/{id}:restore" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsByProjectVersionInput {
  projectVersionId: string;
}
export type GetTestingRequestsByProjectVersionOutput =
  Array<Types.TestingLabTestingInput>;
export const getTestingRequestsByProjectVersionEndpoint = {
  operationId: "getTestingRequestsByProjectVersion" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/by-project-version/{projectVersionId}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsByCreatorInput {
  creatorId: string;
}
export type GetTestingRequestsByCreatorOutput =
  Array<Types.TestingLabTestingInput>;
export const getTestingRequestsByCreatorEndpoint = {
  operationId: "getTestingRequestsByCreator" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/by-creator/{creatorId}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsByStatusInput {
  status: Types.TestingLabTestingRequestStatus;
}
export type GetTestingRequestsByStatusOutput =
  Array<Types.TestingLabTestingInput>;
export const getTestingRequestsByStatusEndpoint = {
  operationId: "getTestingRequestsByStatus" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/by-status/{status}" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsSearchInput {
  query?: {
    searchTerm?: string;
  };
}
export type GetTestingRequestsSearchOutput =
  Array<Types.TestingLabTestingInput>;
export const getTestingRequestsSearchEndpoint = {
  operationId: "getTestingRequestsSearch" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/search" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSubmitSimpleInput {
  body?: Types.TestingLabCreateSimpleTestingInput;
}
export type PostTestingSubmitSimpleOutput =
  Types.TestingLabTestingRequestDetailProjection;
export const postTestingSubmitSimpleEndpoint = {
  operationId: "postTestingSubmitSimple" as const,
  method: "POST" as const,
  path: "/v1/testing/submit-simple" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export type GetTestingMyRequestsInput = void;
export type GetTestingMyRequestsOutput = Array<Types.TestingLabTestingInput>;
export const getTestingMyRequestsEndpoint = {
  operationId: "getTestingMyRequests" as const,
  method: "GET" as const,
  path: "/v1/testing/my-requests" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export type GetTestingAvailableForTestingInput = void;
export type GetTestingAvailableForTestingOutput =
  Array<Types.TestingLabTestingInput>;
export const getTestingAvailableForTestingEndpoint = {
  operationId: "getTestingAvailableForTesting" as const,
  method: "GET" as const,
  path: "/v1/testing/available-for-testing" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingRequestsStatisticsInput {
  requestId: string;
}
export type GetTestingRequestsStatisticsOutput = void;
export const getTestingRequestsStatisticsEndpoint = {
  operationId: "getTestingRequestsStatistics" as const,
  method: "GET" as const,
  path: "/v1/testing/requests/{requestId}/statistics" as const,
  tags: ["TestingLabTestingRequests"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsForGetTestingSessionsInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetTestingSessionsForGetTestingSessionsOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsForGetTestingSessionsEndpoint = {
  operationId: "getTestingSessionsForGetTestingSessions" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsInput {
  body?: Types.TestingLabCreateTestingSession;
}
export type PostTestingSessionsOutput = Types.TestingLabTestingSession;
export const postTestingSessionsEndpoint = {
  operationId: "postTestingSessions" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsForGetTestingSessionsByIdInput {
  id: string;
}
export type GetTestingSessionsForGetTestingSessionsByIdOutput =
  Types.TestingLabTestingSession;
export const getTestingSessionsForGetTestingSessionsByIdEndpoint = {
  operationId: "getTestingSessionsForGetTestingSessionsById" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{id}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface PutTestingSessionsInput {
  id: string;
  body?: Types.TestingLabTestingSession;
}
export type PutTestingSessionsOutput = Types.TestingLabTestingSession;
export const putTestingSessionsEndpoint = {
  operationId: "putTestingSessions" as const,
  method: "PUT" as const,
  path: "/v1/testing/sessions/{id}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingSessionsInput {
  id: string;
}
export type DeleteTestingSessionsOutput = void;
export const deleteTestingSessionsEndpoint = {
  operationId: "deleteTestingSessions" as const,
  method: "DELETE" as const,
  path: "/v1/testing/sessions/{id}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsDetailsInput {
  id: string;
}
export type GetTestingSessionsDetailsOutput = Types.TestingLabTestingSession;
export const getTestingSessionsDetailsEndpoint = {
  operationId: "getTestingSessionsDetails" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{id}/details" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsRestoreInput {
  id: string;
}
export type PostTestingSessionsRestoreOutput = void;
export const postTestingSessionsRestoreEndpoint = {
  operationId: "postTestingSessionsRestore" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions/{id}:restore" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingPublicSessionsInput {
  query?: {
    take?: number;
  };
}
export type GetTestingPublicSessionsOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingPublicSessionsEndpoint = {
  operationId: "getTestingPublicSessions" as const,
  method: "GET" as const,
  path: "/v1/testing/public/sessions" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsByRequestInput {
  testingRequestId: string;
}
export type GetTestingSessionsByRequestOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsByRequestEndpoint = {
  operationId: "getTestingSessionsByRequest" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/by-request/{testingRequestId}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsByLocationInput {
  locationId: string;
}
export type GetTestingSessionsByLocationOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsByLocationEndpoint = {
  operationId: "getTestingSessionsByLocation" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/by-location/{locationId}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsByStatusInput {
  status: Types.TestingLabSessionStatus;
}
export type GetTestingSessionsByStatusOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsByStatusEndpoint = {
  operationId: "getTestingSessionsByStatus" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/by-status/{status}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsByManagerInput {
  managerId: string;
}
export type GetTestingSessionsByManagerOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsByManagerEndpoint = {
  operationId: "getTestingSessionsByManager" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/by-manager/{managerId}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsSearchInput {
  query?: {
    searchTerm?: string;
  };
}
export type GetTestingSessionsSearchOutput =
  Array<Types.TestingLabTestingSession>;
export const getTestingSessionsSearchEndpoint = {
  operationId: "getTestingSessionsSearch" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/search" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsStatisticsInput {
  sessionId: string;
}
export type GetTestingSessionsStatisticsOutput = void;
export const getTestingSessionsStatisticsEndpoint = {
  operationId: "getTestingSessionsStatistics" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{sessionId}/statistics" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export type GetTestingAttendanceSessionsInput = void;
export type GetTestingAttendanceSessionsOutput = void;
export const getTestingAttendanceSessionsEndpoint = {
  operationId: "getTestingAttendanceSessions" as const,
  method: "GET" as const,
  path: "/v1/testing/attendance/sessions" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsAttendanceInput {
  sessionId: string;
  body?: Types.TestingLabUpdateAttendance;
}
export type PostTestingSessionsAttendanceOutput = void;
export const postTestingSessionsAttendanceEndpoint = {
  operationId: "postTestingSessionsAttendance" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions/{sessionId}/attendance" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface GetTestingSessionsProjectsInput {
  sessionId: string;
  query?: {
    includeInactive?: boolean;
  };
}
export type GetTestingSessionsProjectsOutput =
  Array<Types.TestingLabSessionProjectProjection>;
export const getTestingSessionsProjectsEndpoint = {
  operationId: "getTestingSessionsProjects" as const,
  method: "GET" as const,
  path: "/v1/testing/sessions/{sessionId}/projects" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface PostTestingSessionsProjectsInput {
  sessionId: string;
  body?: Types.TestingLabLinkSessionProjectInput;
}
export type PostTestingSessionsProjectsOutput =
  Types.TestingLabSessionProjectProjection;
export const postTestingSessionsProjectsEndpoint = {
  operationId: "postTestingSessionsProjects" as const,
  method: "POST" as const,
  path: "/v1/testing/sessions/{sessionId}/projects" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

export interface DeleteTestingSessionsProjectsInput {
  sessionId: string;
  projectId: string;
}
export type DeleteTestingSessionsProjectsOutput = void;
export const deleteTestingSessionsProjectsEndpoint = {
  operationId: "deleteTestingSessionsProjects" as const,
  method: "DELETE" as const,
  path: "/v1/testing/sessions/{sessionId}/projects/{projectId}" as const,
  tags: ["TestingLabTestingSessions"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk create users
 *
 * Creates multiple user accounts at once.
 */
export interface PostUsersCreateInput {
  body?: Types.IdentityUsersBulkCreateUsersInput;
}
export type PostUsersCreateOutput = Types.IdentityUsersBulkCreateUsersOutput;
export const postUsersCreateEndpoint = {
  operationId: "postUsersCreate" as const,
  method: "POST" as const,
  path: "/v1/users:create" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk partial update users
 *
 * Updates multiple users with partial data.
 */
export interface PostUsersUpdateInput {
  body?: Types.IdentityUsersBulkUpdateUsersInput;
}
export type PostUsersUpdateOutput = void;
export const postUsersUpdateEndpoint = {
  operationId: "postUsersUpdate" as const,
  method: "POST" as const,
  path: "/v1/users:update" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk full update users
 *
 * Updates multiple users with complete data.
 */
export interface PostUsersReplaceInput {
  body?: Types.IdentityUsersBulkUpdateUsersInput;
}
export type PostUsersReplaceOutput = void;
export const postUsersReplaceEndpoint = {
  operationId: "postUsersReplace" as const,
  method: "POST" as const,
  path: "/v1/users:replace" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk soft delete users
 *
 * Soft deletes multiple users at once.
 */
export interface PostUsersDeleteInput {
  body?: Types.IdentityUsersBulkDeleteUsersInput;
}
export type PostUsersDeleteOutput = void;
export const postUsersDeleteEndpoint = {
  operationId: "postUsersDelete" as const,
  method: "POST" as const,
  path: "/v1/users:delete" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk activate user accounts
 *
 * Activates multiple user accounts at once.
 */
export interface PostUsersActivateForPostUsersActivateInput {
  body?: Types.IdentityUsersBulkActivateUsersInput;
}
export type PostUsersActivateForPostUsersActivateOutput =
  Types.IdentityUsersBulkActivateUsersOutput;
export const postUsersActivateForPostUsersActivateEndpoint = {
  operationId: "postUsersActivateForPostUsersActivate" as const,
  method: "POST" as const,
  path: "/v1/users:activate" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk deactivate user accounts
 *
 * Deactivates multiple user accounts at once.
 */
export interface PostUsersDeactivateForPostUsersDeactivateInput {
  body?: Types.IdentityUsersBulkDeactivateUsersInput;
}
export type PostUsersDeactivateForPostUsersDeactivateOutput =
  Types.IdentityUsersBulkDeactivateUsersOutput;
export const postUsersDeactivateForPostUsersDeactivateEndpoint = {
  operationId: "postUsersDeactivateForPostUsersDeactivate" as const,
  method: "POST" as const,
  path: "/v1/users:deactivate" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk suspend user accounts
 *
 * Suspends multiple user accounts at once.
 */
export interface PostUsersSuspendForPostUsersSuspendInput {
  body?: Types.IdentityUsersBulkSuspendUsersInput;
}
export type PostUsersSuspendForPostUsersSuspendOutput =
  Types.IdentityUsersBulkSuspendUsersOutput;
export const postUsersSuspendForPostUsersSuspendEndpoint = {
  operationId: "postUsersSuspendForPostUsersSuspend" as const,
  method: "POST" as const,
  path: "/v1/users:suspend" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk unsuspend user accounts
 *
 * Unsuspends multiple user accounts at once.
 */
export interface PostUsersUnsuspendForPostUsersUnsuspendInput {
  body?: Types.IdentityUsersBulkUnsuspendUsersInput;
}
export type PostUsersUnsuspendForPostUsersUnsuspendOutput =
  Types.IdentityUsersBulkUnsuspendUsersOutput;
export const postUsersUnsuspendForPostUsersUnsuspendEndpoint = {
  operationId: "postUsersUnsuspendForPostUsersUnsuspend" as const,
  method: "POST" as const,
  path: "/v1/users:unsuspend" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk undelete soft-deleted users
 *
 * Restores multiple soft-deleted users at once.
 */
export interface PostUsersUndeleteForPostUsersUndeleteInput {
  body?: Types.IdentityUsersBulkRestoreUsersInput;
}
export type PostUsersUndeleteForPostUsersUndeleteOutput =
  Types.IdentityUsersBulkRestoreUsersOutput;
export const postUsersUndeleteForPostUsersUndeleteEndpoint = {
  operationId: "postUsersUndeleteForPostUsersUndelete" as const,
  method: "POST" as const,
  path: "/v1/users:undelete" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Bulk hard delete users (irreversible purge)
 *
 * Permanently deletes multiple users. Admin operation requiring proper authorization.
 */
export interface PostUsersPurgeForPostUsersPurgeInput {
  body?: Types.IdentityUsersBulkPurgeUsersInput;
}
export type PostUsersPurgeForPostUsersPurgeOutput = void;
export const postUsersPurgeForPostUsersPurgeEndpoint = {
  operationId: "postUsersPurgeForPostUsersPurge" as const,
  method: "POST" as const,
  path: "/v1/users:purge" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Activate user account
 *
 * Activates a user account by ID.
 */
export interface PostUsersActivateForPostUsersByUserIdActivateInput {
  userId: string;
}
export type PostUsersActivateForPostUsersByUserIdActivateOutput =
  Types.IdentityUsersUserDto;
export const postUsersActivateForPostUsersByUserIdActivateEndpoint = {
  operationId: "postUsersActivateForPostUsersByUserIdActivate" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:activate" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Deactivate user account
 *
 * Deactivates a user account by ID.
 */
export interface PostUsersDeactivateForPostUsersByUserIdDeactivateInput {
  userId: string;
}
export type PostUsersDeactivateForPostUsersByUserIdDeactivateOutput =
  Types.IdentityUsersUserDto;
export const postUsersDeactivateForPostUsersByUserIdDeactivateEndpoint = {
  operationId: "postUsersDeactivateForPostUsersByUserIdDeactivate" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:deactivate" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Suspend user account
 *
 * Suspends a user account by ID.
 */
export interface PostUsersSuspendForPostUsersByUserIdSuspendInput {
  userId: string;
}
export type PostUsersSuspendForPostUsersByUserIdSuspendOutput =
  Types.IdentityUsersUserDto;
export const postUsersSuspendForPostUsersByUserIdSuspendEndpoint = {
  operationId: "postUsersSuspendForPostUsersByUserIdSuspend" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:suspend" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Unsuspend user account
 *
 * Unsuspends a user account by ID.
 */
export interface PostUsersUnsuspendForPostUsersByUserIdUnsuspendInput {
  userId: string;
}
export type PostUsersUnsuspendForPostUsersByUserIdUnsuspendOutput =
  Types.IdentityUsersUserDto;
export const postUsersUnsuspendForPostUsersByUserIdUnsuspendEndpoint = {
  operationId: "postUsersUnsuspendForPostUsersByUserIdUnsuspend" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:unsuspend" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Undelete soft-deleted user by ID
 *
 * Restores a soft-deleted user by ID.
 */
export interface PostUsersUndeleteForPostUsersByUserIdUndeleteInput {
  userId: string;
}
export type PostUsersUndeleteForPostUsersByUserIdUndeleteOutput =
  Types.IdentityUsersUserDto;
export const postUsersUndeleteForPostUsersByUserIdUndeleteEndpoint = {
  operationId: "postUsersUndeleteForPostUsersByUserIdUndelete" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:undelete" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Hard delete user by ID (irreversible purge)
 *
 * Permanently deletes a user by ID (irreversible).
 */
export interface PostUsersPurgeForPostUsersByUserIdPurgeInput {
  userId: string;
}
export type PostUsersPurgeForPostUsersByUserIdPurgeOutput = void;
export const postUsersPurgeForPostUsersByUserIdPurgeEndpoint = {
  operationId: "postUsersPurgeForPostUsersByUserIdPurge" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}:purge" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Get users with pagination, search, and sorting
 *
 * Retrieves a paginated list of users with optional filtering by email, status, and text search.
 */
export interface GetUsersForGetUsersInput {
  query?: {
    email?: string;
    status?: string;
    includeDeleted?: boolean;
    q?: string;
    cursor?: string;
    limit?: number;
    sort?: string;
  };
}
export type GetUsersForGetUsersOutput = Types.PagedResultOfIdentityUsersUser;
export const getUsersForGetUsersEndpoint = {
  operationId: "getUsersForGetUsers" as const,
  method: "GET" as const,
  path: "/v1/users" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Create a new user
 *
 * Creates a new user account with the provided information.
 */
export interface PostUsersInput {
  body?: Types.IdentityUsersCreateUserInput;
}
export type PostUsersOutput = Types.IdentityUsersUserDto;
export const postUsersEndpoint = {
  operationId: "postUsers" as const,
  method: "POST" as const,
  path: "/v1/users" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Get user by ID
 *
 * Retrieves detailed information for a specific user by their unique identifier.
 */
export interface GetUsersForGetUsersByUserIdInput {
  userId: string;
}
export type GetUsersForGetUsersByUserIdOutput = Types.IdentityUsersUserDto;
export const getUsersForGetUsersByUserIdEndpoint = {
  operationId: "getUsersForGetUsersByUserId" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Update user by ID
 *
 * Fully updates a user by ID with complete user data.
 */
export interface PutUsersInput {
  userId: string;
  body?: Types.IdentityUsersCreateUserInput;
}
export type PutUsersOutput = Types.IdentityUsersUserDto;
export const putUsersEndpoint = {
  operationId: "putUsers" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Soft delete user by ID
 *
 * Soft deletes a user by ID (can be restored). Users can delete their own account.
 */
export interface DeleteUsersInput {
  userId: string;
}
export type DeleteUsersOutput = void;
export const deleteUsersEndpoint = {
  operationId: "deleteUsers" as const,
  method: "DELETE" as const,
  path: "/v1/users/{userId}" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update user by ID
 *
 * Updates specific fields of a user by ID.
 */
export interface PatchUsersInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserInput;
}
export type PatchUsersOutput = Types.IdentityUsersUserDto;
export const patchUsersEndpoint = {
  operationId: "patchUsers" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if user exists by ID
 *
 * Checks if a user exists by ID without returning the body.
 */
export interface HeadUsersInput {
  userId: string;
}
export type HeadUsersOutput = void;
export const headUsersEndpoint = {
  operationId: "headUsers" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}" as const,
  tags: ["Users"] as const,
  requiresAuth: true,
} as const;

export type GetUsersMeEntitlementsInput = void;
export type GetUsersMeEntitlementsOutput =
  Array<Types.CommerceProductsEntitlementInfo>;
export const getUsersMeEntitlementsEndpoint = {
  operationId: "getUsersMeEntitlements" as const,
  method: "GET" as const,
  path: "/v1/users/me/entitlements" as const,
  tags: ["UsersEntitlements"] as const,
  requiresAuth: true,
} as const;

export interface GetUsersEntitlementsInput {
  userId: string;
}
export type GetUsersEntitlementsOutput =
  Array<Types.CommerceProductsEntitlementInfo>;
export const getUsersEntitlementsEndpoint = {
  operationId: "getUsersEntitlements" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/entitlements" as const,
  tags: ["UsersEntitlements"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all tenant memberships for a user
 *
 * Returns all tenants the user belongs to, with role and membership status. Similar to Discord's 'My Servers' view.
 */
export interface GetUsersMembershipsInput {
  userId: string;
  query?: {
    includeInactive?: boolean;
  };
}
export type GetUsersMembershipsOutput =
  Types.IdentityTenantsGetUserMembershipsOutput;
export const getUsersMembershipsEndpoint = {
  operationId: "getUsersMemberships" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/memberships" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Add a tenant membership for a user
 *
 * Adds the specified user to a tenant with the requested role so the user can access that workspace.
 */
export interface PostUsersMembershipsInput {
  userId: string;
  body?: Types.IdentityTenantsAddUserMembershipInput;
}
export type PostUsersMembershipsOutput =
  Types.IdentityTenantsAddTenantMemberOutput;
export const postUsersMembershipsEndpoint = {
  operationId: "postUsersMemberships" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if user has any tenant memberships
 */
export interface HeadUsersMembershipsInput {
  userId: string;
}
export type HeadUsersMembershipsOutput = void;
export const headUsersMembershipsEndpoint = {
  operationId: "headUsersMemberships" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/memberships" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Update tenant membership role
 *
 * Updates the user's role in the specified tenant/workspace. Use this for console promotion/demotion flows.
 */
export interface PatchUsersMembershipsRoleInput {
  userId: string;
  tenantId: string;
  body?: Types.IdentityTenantsUpdateUserMembershipRoleInput;
}
export type PatchUsersMembershipsRoleOutput =
  Types.IdentityTenantsUpdateTenantMemberRoleOutput;
export const patchUsersMembershipsRoleEndpoint = {
  operationId: "patchUsersMembershipsRole" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}/role" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Deactivate a tenant membership
 *
 * Suspends access to the specified tenant without deleting membership history.
 */
export interface PostUsersMembershipsDeactivateInput {
  userId: string;
  tenantId: string;
  body?: Types.IdentityTenantsSetTenantMembershipStatusInput;
}
export type PostUsersMembershipsDeactivateOutput =
  Types.IdentityTenantsSetTenantMembershipStatusOutput;
export const postUsersMembershipsDeactivateEndpoint = {
  operationId: "postUsersMembershipsDeactivate" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}:deactivate" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Activate a tenant membership
 *
 * Restores access to the specified tenant membership.
 */
export interface PostUsersMembershipsActivateInput {
  userId: string;
  tenantId: string;
}
export type PostUsersMembershipsActivateOutput =
  Types.IdentityTenantsSetTenantMembershipStatusOutput;
export const postUsersMembershipsActivateEndpoint = {
  operationId: "postUsersMembershipsActivate" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}:activate" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Resend tenant membership invite
 */
export interface PostUsersMembershipsInviteResendInput {
  userId: string;
  tenantId: string;
  body?: Types.IdentityTenantsUpdateUserMembershipInviteInput;
}
export type PostUsersMembershipsInviteResendOutput =
  Types.IdentityTenantsUpdateTenantMemberInviteOutput;
export const postUsersMembershipsInviteResendEndpoint = {
  operationId: "postUsersMembershipsInviteResend" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}/invite:resend" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Cancel tenant membership invite
 */
export interface PostUsersMembershipsInviteCancelInput {
  userId: string;
  tenantId: string;
  body?: Types.IdentityTenantsUpdateUserMembershipInviteInput;
}
export type PostUsersMembershipsInviteCancelOutput =
  Types.IdentityTenantsUpdateTenantMemberInviteOutput;
export const postUsersMembershipsInviteCancelEndpoint = {
  operationId: "postUsersMembershipsInviteCancel" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}/invite:cancel" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Accept tenant membership invite
 */
export interface PostUsersMembershipsInviteAcceptInput {
  userId: string;
  tenantId: string;
  body?: Types.IdentityTenantsUpdateUserMembershipInviteInput;
}
export type PostUsersMembershipsInviteAcceptOutput =
  Types.IdentityTenantsUpdateTenantMemberInviteOutput;
export const postUsersMembershipsInviteAcceptEndpoint = {
  operationId: "postUsersMembershipsInviteAccept" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/memberships/{tenantId}/invite:accept" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Get count of user's active tenant memberships
 */
export interface GetUsersMembershipsCountInput {
  userId: string;
}
export type GetUsersMembershipsCountOutput =
  Types.IdentityTenantsMembershipCountOutput;
export const getUsersMembershipsCountEndpoint = {
  operationId: "getUsersMembershipsCount" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/memberships:count" as const,
  tags: ["UsersMemberships"] as const,
  requiresAuth: true,
} as const;

/**
 * Get user metadata by user ID
 */
export interface GetUsersMetadataInput {
  userId: string;
}
export type GetUsersMetadataOutput = Types.IdentityUsersUserMetadataDto;
export const getUsersMetadataEndpoint = {
  operationId: "getUsersMetadata" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/metadata" as const,
  tags: ["UsersMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace user metadata by user ID
 */
export interface PutUsersMetadataInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserMetadataInput;
}
export type PutUsersMetadataOutput = void;
export const putUsersMetadataEndpoint = {
  operationId: "putUsersMetadata" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/metadata" as const,
  tags: ["UsersMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update user metadata by user ID
 */
export interface PatchUsersMetadataInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserMetadataInput;
}
export type PatchUsersMetadataOutput = void;
export const patchUsersMetadataEndpoint = {
  operationId: "patchUsersMetadata" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/metadata" as const,
  tags: ["UsersMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get user notifications with pagination, search, and sorting
 */
export interface GetUsersNotificationsForGetUsersByUserIdNotificationsInput {
  userId: string;
  query?: {
    page?: number;
    pageSize?: number;
    search?: string;
    sortBy?: string;
    sortDirection?: string;
    isRead?: boolean;
    isArchived?: boolean;
    type?: string;
    priority?: string;
    fromDate?: string;
    toDate?: string;
  };
}
export type GetUsersNotificationsForGetUsersByUserIdNotificationsOutput =
  Types.PagedResultOfIdentityUsersUserNotification;
export const getUsersNotificationsForGetUsersByUserIdNotificationsEndpoint = {
  operationId: "getUsersNotificationsForGetUsersByUserIdNotifications" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/notifications" as const,
  tags: ["UsersNotifications"] as const,
  requiresAuth: true,
} as const;

/**
 * Mark multiple notifications as read for a user
 */
export interface PostUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsReadInput {
  userId: string;
  body?: Types.IdentityUsersBulkNotificationInput;
}
export type PostUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsReadOutput =
  void;
export const postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsReadEndpoint =
  {
    operationId:
      "postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsRead" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications:mark-as-read" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Mark multiple notifications as unread for a user
 */
export interface PostUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnreadInput {
  userId: string;
  body?: Types.IdentityUsersBulkNotificationInput;
}
export type PostUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnreadOutput =
  void;
export const postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnreadEndpoint =
  {
    operationId:
      "postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnread" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications:mark-as-unread" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Archive multiple notifications for a user
 */
export interface PostUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchiveInput {
  userId: string;
  body?: Types.IdentityUsersBulkNotificationInput;
}
export type PostUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchiveOutput =
  void;
export const postUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchiveEndpoint =
  {
    operationId:
      "postUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchive" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications:archive" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Unarchive multiple notifications for a user
 */
export interface PostUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchiveInput {
  userId: string;
  body?: Types.IdentityUsersBulkNotificationInput;
}
export type PostUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchiveOutput =
  void;
export const postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchiveEndpoint =
  {
    operationId:
      "postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchive" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications:unarchive" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get detailed notification by ID
 */
export interface GetUsersNotificationsForGetUsersByUserIdNotificationsByNotificationIdInput {
  userId: string;
  notificationId: string;
}
export type GetUsersNotificationsForGetUsersByUserIdNotificationsByNotificationIdOutput =
  Types.IdentityUsersUserNotificationDetail;
export const getUsersNotificationsForGetUsersByUserIdNotificationsByNotificationIdEndpoint =
  {
    operationId:
      "getUsersNotificationsForGetUsersByUserIdNotificationsByNotificationId" as const,
    method: "GET" as const,
    path: "/v1/users/{userId}/notifications/{notificationId}" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Check if user notification exists
 */
export interface HeadUsersNotificationsInput {
  userId: string;
  notificationId: string;
}
export type HeadUsersNotificationsOutput = void;
export const headUsersNotificationsEndpoint = {
  operationId: "headUsersNotifications" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/notifications/{notificationId}" as const,
  tags: ["UsersNotifications"] as const,
  requiresAuth: true,
} as const;

/**
 * Mark notification as read
 */
export interface PostUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsReadInput {
  userId: string;
  notificationId: string;
}
export type PostUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsReadOutput =
  void;
export const postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsReadEndpoint =
  {
    operationId:
      "postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsRead" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications/{notificationId}:mark-as-read" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Mark notification as unread
 */
export interface PostUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnreadInput {
  userId: string;
  notificationId: string;
}
export type PostUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnreadOutput =
  void;
export const postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnreadEndpoint =
  {
    operationId:
      "postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnread" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications/{notificationId}:mark-as-unread" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Archive notification
 */
export interface PostUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchiveInput {
  userId: string;
  notificationId: string;
}
export type PostUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchiveOutput =
  void;
export const postUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchiveEndpoint =
  {
    operationId:
      "postUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchive" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications/{notificationId}:archive" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Unarchive notification
 */
export interface PostUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchiveInput {
  userId: string;
  notificationId: string;
}
export type PostUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchiveOutput =
  void;
export const postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchiveEndpoint =
  {
    operationId:
      "postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchive" as const,
    method: "POST" as const,
    path: "/v1/users/{userId}/notifications/{notificationId}:unarchive" as const,
    tags: ["UsersNotifications"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get user preferences
 */
export interface GetUsersPreferencesInput {
  userId: string;
}
export type GetUsersPreferencesOutput = Types.IdentityUsersUserPreferencesDto;
export const getUsersPreferencesEndpoint = {
  operationId: "getUsersPreferences" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/preferences" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace user preferences by user ID
 */
export interface PutUsersPreferencesInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserPreferencesInput;
}
export type PutUsersPreferencesOutput = void;
export const putUsersPreferencesEndpoint = {
  operationId: "putUsersPreferences" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/preferences" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update user preferences by user ID
 */
export interface PatchUsersPreferencesInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserPreferencesInput;
}
export type PatchUsersPreferencesOutput = void;
export const patchUsersPreferencesEndpoint = {
  operationId: "patchUsersPreferences" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/preferences" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset user preferences to defaults
 */
export interface PostUsersPreferencesResetInput {
  userId: string;
}
export type PostUsersPreferencesResetOutput = void;
export const postUsersPreferencesResetEndpoint = {
  operationId: "postUsersPreferencesReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/preferences:reset" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Get notification settings for user
 */
export interface GetUsersPreferencesNotificationsInput {
  userId: string;
}
export type GetUsersPreferencesNotificationsOutput =
  Types.IdentityUsersUserNotificationPreferences;
export const getUsersPreferencesNotificationsEndpoint = {
  operationId: "getUsersPreferencesNotifications" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/preferences/notifications" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace notification preferences for user (full update)
 */
export interface PutUsersPreferencesNotificationsInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserNotificationPreferencesInput;
}
export type PutUsersPreferencesNotificationsOutput = void;
export const putUsersPreferencesNotificationsEndpoint = {
  operationId: "putUsersPreferencesNotifications" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/preferences/notifications" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update notification preferences for user
 */
export interface PatchUsersPreferencesNotificationsInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserNotificationPreferencesInput;
}
export type PatchUsersPreferencesNotificationsOutput = void;
export const patchUsersPreferencesNotificationsEndpoint = {
  operationId: "patchUsersPreferencesNotifications" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/preferences/notifications" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if notification preferences exist
 */
export interface HeadUsersPreferencesNotificationsInput {
  userId: string;
}
export type HeadUsersPreferencesNotificationsOutput = void;
export const headUsersPreferencesNotificationsEndpoint = {
  operationId: "headUsersPreferencesNotifications" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/preferences/notifications" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset notification preferences to defaults
 */
export interface PostUsersPreferencesNotificationsResetInput {
  userId: string;
}
export type PostUsersPreferencesNotificationsResetOutput = void;
export const postUsersPreferencesNotificationsResetEndpoint = {
  operationId: "postUsersPreferencesNotificationsReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/preferences/notifications:reset" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Get accessibility settings for user
 */
export interface GetUsersPreferencesAccessibilityInput {
  userId: string;
}
export type GetUsersPreferencesAccessibilityOutput =
  Types.IdentityUsersUserAccessibilityPreferences;
export const getUsersPreferencesAccessibilityEndpoint = {
  operationId: "getUsersPreferencesAccessibility" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/preferences/accessibility" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace accessibility preferences for user (full update)
 */
export interface PutUsersPreferencesAccessibilityInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserAccessibilityPreferencesInput;
}
export type PutUsersPreferencesAccessibilityOutput = void;
export const putUsersPreferencesAccessibilityEndpoint = {
  operationId: "putUsersPreferencesAccessibility" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/preferences/accessibility" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update accessibility preferences for user
 */
export interface PatchUsersPreferencesAccessibilityInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserAccessibilityPreferencesInput;
}
export type PatchUsersPreferencesAccessibilityOutput = void;
export const patchUsersPreferencesAccessibilityEndpoint = {
  operationId: "patchUsersPreferencesAccessibility" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/preferences/accessibility" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if accessibility preferences exist
 */
export interface HeadUsersPreferencesAccessibilityInput {
  userId: string;
}
export type HeadUsersPreferencesAccessibilityOutput = void;
export const headUsersPreferencesAccessibilityEndpoint = {
  operationId: "headUsersPreferencesAccessibility" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/preferences/accessibility" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset accessibility preferences to defaults
 */
export interface PostUsersPreferencesAccessibilityResetInput {
  userId: string;
}
export type PostUsersPreferencesAccessibilityResetOutput = void;
export const postUsersPreferencesAccessibilityResetEndpoint = {
  operationId: "postUsersPreferencesAccessibilityReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/preferences/accessibility:reset" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Get privacy settings for user
 */
export interface GetUsersPreferencesPrivacyInput {
  userId: string;
}
export type GetUsersPreferencesPrivacyOutput =
  Types.IdentityUsersUserPrivacyPreferences;
export const getUsersPreferencesPrivacyEndpoint = {
  operationId: "getUsersPreferencesPrivacy" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/preferences/privacy" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace privacy preferences for user (full update)
 */
export interface PutUsersPreferencesPrivacyInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserPrivacyPreferencesInput;
}
export type PutUsersPreferencesPrivacyOutput = void;
export const putUsersPreferencesPrivacyEndpoint = {
  operationId: "putUsersPreferencesPrivacy" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/preferences/privacy" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update privacy preferences for user
 */
export interface PatchUsersPreferencesPrivacyInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserPrivacyPreferencesInput;
}
export type PatchUsersPreferencesPrivacyOutput = void;
export const patchUsersPreferencesPrivacyEndpoint = {
  operationId: "patchUsersPreferencesPrivacy" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/preferences/privacy" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if privacy preferences exist
 */
export interface HeadUsersPreferencesPrivacyInput {
  userId: string;
}
export type HeadUsersPreferencesPrivacyOutput = void;
export const headUsersPreferencesPrivacyEndpoint = {
  operationId: "headUsersPreferencesPrivacy" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/preferences/privacy" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset privacy preferences to defaults
 */
export interface PostUsersPreferencesPrivacyResetInput {
  userId: string;
}
export type PostUsersPreferencesPrivacyResetOutput = void;
export const postUsersPreferencesPrivacyResetEndpoint = {
  operationId: "postUsersPreferencesPrivacyReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/preferences/privacy:reset" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Get localization settings for user
 */
export interface GetUsersPreferencesLocalizationInput {
  userId: string;
}
export type GetUsersPreferencesLocalizationOutput =
  Types.IdentityUsersUserLocalizationPreferences;
export const getUsersPreferencesLocalizationEndpoint = {
  operationId: "getUsersPreferencesLocalization" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/preferences/localization" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace localization preferences for user (full update)
 */
export interface PutUsersPreferencesLocalizationInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserLocalizationPreferencesInput;
}
export type PutUsersPreferencesLocalizationOutput = void;
export const putUsersPreferencesLocalizationEndpoint = {
  operationId: "putUsersPreferencesLocalization" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/preferences/localization" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Partially update localization preferences for user
 */
export interface PatchUsersPreferencesLocalizationInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserLocalizationPreferencesInput;
}
export type PatchUsersPreferencesLocalizationOutput = void;
export const patchUsersPreferencesLocalizationEndpoint = {
  operationId: "patchUsersPreferencesLocalization" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/preferences/localization" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if localization preferences exist
 */
export interface HeadUsersPreferencesLocalizationInput {
  userId: string;
}
export type HeadUsersPreferencesLocalizationOutput = void;
export const headUsersPreferencesLocalizationEndpoint = {
  operationId: "headUsersPreferencesLocalization" as const,
  method: "HEAD" as const,
  path: "/v1/users/{userId}/preferences/localization" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset localization preferences to defaults
 */
export interface PostUsersPreferencesLocalizationResetInput {
  userId: string;
}
export type PostUsersPreferencesLocalizationResetOutput = void;
export const postUsersPreferencesLocalizationResetEndpoint = {
  operationId: "postUsersPreferencesLocalizationReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/preferences/localization:reset" as const,
  tags: ["UsersPreferences"] as const,
  requiresAuth: true,
} as const;

/**
 * Find all user profiles with pagination, search, and sorting
 */
export interface GetUsersProfilesInput {
  query?: {
    page?: number;
    pageSize?: number;
    search?: string;
    sortBy?: string;
    sortDirection?: string;
  };
}
export type GetUsersProfilesOutput =
  Types.PagedResultOfIdentityUsersUserProfile;
export const getUsersProfilesEndpoint = {
  operationId: "getUsersProfiles" as const,
  method: "GET" as const,
  path: "/v1/users/profiles" as const,
  tags: ["UsersProfiles"] as const,
  requiresAuth: true,
} as const;

/**
 * Get user profile by user ID
 */
export interface GetUsersProfileInput {
  userId: string;
}
export type GetUsersProfileOutput = Types.IdentityUsersUserProfileDto;
export const getUsersProfileEndpoint = {
  operationId: "getUsersProfile" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/profile" as const,
  tags: ["UsersProfiles"] as const,
  requiresAuth: true,
} as const;

/**
 * Replace user profile (full update)
 */
export interface PutUsersProfileInput {
  userId: string;
  body?: Types.IdentityUsersReplaceUserProfileInput;
}
export type PutUsersProfileOutput = Types.IdentityUsersUserProfileDto;
export const putUsersProfileEndpoint = {
  operationId: "putUsersProfile" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/profile" as const,
  tags: ["UsersProfiles"] as const,
  requiresAuth: true,
} as const;

/**
 * Update user profile (partial update)
 */
export interface PatchUsersProfileInput {
  userId: string;
  body?: Types.IdentityUsersUpdateUserProfileInput;
}
export type PatchUsersProfileOutput = Types.IdentityUsersUserProfileDto;
export const patchUsersProfileEndpoint = {
  operationId: "patchUsersProfile" as const,
  method: "PATCH" as const,
  path: "/v1/users/{userId}/profile" as const,
  tags: ["UsersProfiles"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all quotas for a user
 *
 * Retrieves all configured resource quotas for a specific user.
 */
export interface GetUsersQuotasForGetUsersByUserIdQuotasInput {
  userId: string;
}
export type GetUsersQuotasForGetUsersByUserIdQuotasOutput =
  Array<Types.ResourcesResourceQuotaOutput>;
export const getUsersQuotasForGetUsersByUserIdQuotasEndpoint = {
  operationId: "getUsersQuotasForGetUsersByUserIdQuotas" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/quotas" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Get specific quota for a resource type
 *
 * Retrieves the quota configuration for a specific resource type for a user.
 */
export interface GetUsersQuotasForGetUsersByUserIdQuotasByTypeInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
}
export type GetUsersQuotasForGetUsersByUserIdQuotasByTypeOutput =
  Types.ResourcesResourceQuotaOutput;
export const getUsersQuotasForGetUsersByUserIdQuotasByTypeEndpoint = {
  operationId: "getUsersQuotasForGetUsersByUserIdQuotasByType" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/quotas/{type}" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Set or update a quota for a resource type
 *
 * Creates or updates the quota configuration for a specific resource type for a user.
 */
export interface PutUsersQuotasInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesSetQuotaInput;
}
export type PutUsersQuotasOutput = void;
export const putUsersQuotasEndpoint = {
  operationId: "putUsersQuotas" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/quotas/{type}" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Delete a quota for a resource type
 *
 * Removes the quota configuration for a specific resource type for a user.
 */
export interface DeleteUsersQuotasInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
}
export type DeleteUsersQuotasOutput = void;
export const deleteUsersQuotasEndpoint = {
  operationId: "deleteUsersQuotas" as const,
  method: "DELETE" as const,
  path: "/v1/users/{userId}/quotas/{type}" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset quota usage to zero
 *
 * Resets the current usage counter for a specific resource quota to zero without changing the quota limits.
 */
export interface PostUsersQuotasResetInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
}
export type PostUsersQuotasResetOutput = void;
export const postUsersQuotasResetEndpoint = {
  operationId: "postUsersQuotasReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/quotas/{type}:reset" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Toggle quota activation status
 *
 * Activates or deactivates a resource quota. Inactive quotas are not enforced.
 */
export interface PostUsersQuotasToggleInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesToggleResourceQuotaInput;
}
export type PostUsersQuotasToggleOutput = void;
export const postUsersQuotasToggleEndpoint = {
  operationId: "postUsersQuotasToggle" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/quotas/{type}:toggle" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if a usage amount would exceed quota
 *
 * Validates whether a proposed usage amount would exceed the configured quota limits without recording any usage.
 */
export interface PostUsersQuotasCheckInput {
  userId: string;
  type: Types.ResourcesResourceUsageType;
  body?: Types.ResourcesCheckResourceQuotaInput;
}
export type PostUsersQuotasCheckOutput =
  Types.ResourcesResourceQuotaEnforcementResult;
export const postUsersQuotasCheckEndpoint = {
  operationId: "postUsersQuotasCheck" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/quotas/{type}:check" as const,
  tags: ["UsersQuotas"] as const,
  requiresAuth: true,
} as const;

/**
 * Get usage records for a user
 *
 * Retrieves resource usage records for a specific user with optional filtering by type and date range.
 */
export interface GetUsersResourcesUsageRecordsInput {
  userId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
    startDate?: string;
    endDate?: string;
  };
}
export type GetUsersResourcesUsageRecordsOutput =
  Array<Types.ResourcesUsageRecord>;
export const getUsersResourcesUsageRecordsEndpoint = {
  operationId: "getUsersResourcesUsageRecords" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/resources/usage-records" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Get current usage summary for a user
 *
 * Retrieves the current aggregated resource usage summary for a specific user.
 */
export interface GetUsersResourcesUsageSummaryInput {
  userId: string;
}
export type GetUsersResourcesUsageSummaryOutput = {
  Users?: number;
  Projects?: number;
  Storage?: number;
  ApiCalls?: number;
  Programs?: number;
  Courses?: number;
  FeatureFlags?: number;
  SubscriptionPlans?: number;
  Products?: number;
  TestingSessions?: number;
  Roles?: number;
  Tenants?: number;
  Subscriptions?: number;
  SLOs?: number;
  AccessReviewCampaigns?: number;
  SoDRules?: number;
  AbacPolicies?: number;
  ConditionalPolicies?: number;
  Wallets?: number;
  Disputes?: number;
  PromoCodes?: number;
  Orders?: number;
  AuditEntries?: number;
  Assets?: number;
  AssetStorage?: number;
  AssetDownloads?: number;
  AssetTransformations?: number;
  AiRequests?: number;
  AiTokens?: number;
  Teams?: number;
};
export const getUsersResourcesUsageSummaryEndpoint = {
  operationId: "getUsersResourcesUsageSummary" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/resources/usage-summary" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Check resource limits for a user
 *
 * Checks current resource usage against configured limits for a specific user.
 */
export interface GetUsersResourcesLimitsInput {
  userId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
  };
}
export type GetUsersResourcesLimitsOutput = {
  Users?: boolean;
  Projects?: boolean;
  Storage?: boolean;
  ApiCalls?: boolean;
  Programs?: boolean;
  Courses?: boolean;
  FeatureFlags?: boolean;
  SubscriptionPlans?: boolean;
  Products?: boolean;
  TestingSessions?: boolean;
  Roles?: boolean;
  Tenants?: boolean;
  Subscriptions?: boolean;
  SLOs?: boolean;
  AccessReviewCampaigns?: boolean;
  SoDRules?: boolean;
  AbacPolicies?: boolean;
  ConditionalPolicies?: boolean;
  Wallets?: boolean;
  Disputes?: boolean;
  PromoCodes?: boolean;
  Orders?: boolean;
  AuditEntries?: boolean;
  Assets?: boolean;
  AssetStorage?: boolean;
  AssetDownloads?: boolean;
  AssetTransformations?: boolean;
  AiRequests?: boolean;
  AiTokens?: boolean;
  Teams?: boolean;
};
export const getUsersResourcesLimitsEndpoint = {
  operationId: "getUsersResourcesLimits" as const,
  method: "GET" as const,
  path: "/v1/users/{userId}/resources/limits" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Record resource usage for a user
 *
 * Records a new resource usage entry for the specified user.
 */
export interface PostUsersResourcesRecordInput {
  userId: string;
  body?: Types.ResourcesRecordUserResourceUsageInput;
}
export type PostUsersResourcesRecordOutput = void;
export const postUsersResourcesRecordEndpoint = {
  operationId: "postUsersResourcesRecord" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/resources:record" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Record resource usage with quota enforcement for a user
 *
 * Records a new resource usage entry after verifying it doesn't exceed configured quotas. Returns 429 if quota would be exceeded.
 */
export interface PostUsersResourcesRecordWithQuotaCheckInput {
  userId: string;
  body?: Types.ResourcesRecordUserResourceUsageInput;
}
export type PostUsersResourcesRecordWithQuotaCheckOutput = void;
export const postUsersResourcesRecordWithQuotaCheckEndpoint = {
  operationId: "postUsersResourcesRecordWithQuotaCheck" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/resources:record-with-quota-check" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Reset resource usage for a user
 *
 * Resets the resource usage counters for a specific user and resource type to zero.
 */
export interface PostUsersResourcesResetInput {
  userId: string;
  query?: {
    usageType?: Types.ResourcesResourceUsageType;
  };
}
export type PostUsersResourcesResetOutput = void;
export const postUsersResourcesResetEndpoint = {
  operationId: "postUsersResourcesReset" as const,
  method: "POST" as const,
  path: "/v1/users/{userId}/resources:reset" as const,
  tags: ["UsersResources"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all metadata entries for a user
 *
 * Retrieves all resource metadata entries for a specific user.
 */
export interface GetUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataInput {
  userId: string;
}
export type GetUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataOutput =
  Array<Types.ResourcesResourceMetadata>;
export const getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataEndpoint =
  {
    operationId:
      "getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadata" as const,
    method: "GET" as const,
    path: "/v1/users/{userId}/resources/metadata" as const,
    tags: ["UsersResourcesMetadata"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get a specific metadata entry by key for a user
 *
 * Retrieves a specific resource metadata entry by its key for a user.
 */
export interface GetUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKeyInput {
  userId: string;
  key: string;
}
export type GetUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKeyOutput =
  Types.ResourcesResourceMetadata;
export const getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKeyEndpoint =
  {
    operationId:
      "getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKey" as const,
    method: "GET" as const,
    path: "/v1/users/{userId}/resources/metadata/{key}" as const,
    tags: ["UsersResourcesMetadata"] as const,
    requiresAuth: true,
  } as const;

/**
 * Create or update a metadata entry for a user
 *
 * Creates a new metadata entry or updates an existing one for a user.
 */
export interface PutUsersResourcesMetadataInput {
  userId: string;
  key: string;
  body?: Types.ResourcesSetResourceMetadataInput;
}
export type PutUsersResourcesMetadataOutput = Types.ResourcesResourceMetadata;
export const putUsersResourcesMetadataEndpoint = {
  operationId: "putUsersResourcesMetadata" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/resources/metadata/{key}" as const,
  tags: ["UsersResourcesMetadata"] as const,
  requiresAuth: true,
} as const;

/**
 * Get all setting overrides for a user
 *
 * Retrieves all resource setting overrides for a specific user.
 */
export interface GetUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsInput {
  userId: string;
}
export type GetUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsOutput =
  Array<Types.ResourcesResourceSettings>;
export const getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsEndpoint =
  {
    operationId:
      "getUsersResourcesSettingsForGetUsersByUserIdResourcesSettings" as const,
    method: "GET" as const,
    path: "/v1/users/{userId}/resources/settings" as const,
    tags: ["UsersResourcesSettings"] as const,
    requiresAuth: true,
  } as const;

/**
 * Get a specific setting override by key for a user
 *
 * Retrieves a specific resource setting override by its key for a user.
 */
export interface GetUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKeyInput {
  userId: string;
  key: string;
}
export type GetUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKeyOutput =
  Types.ResourcesResourceSettings;
export const getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKeyEndpoint =
  {
    operationId:
      "getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKey" as const,
    method: "GET" as const,
    path: "/v1/users/{userId}/resources/settings/{key}" as const,
    tags: ["UsersResourcesSettings"] as const,
    requiresAuth: true,
  } as const;

/**
 * Create or update a setting override for a user
 *
 * Creates a new setting override or updates an existing one for a user.
 */
export interface PutUsersResourcesSettingsInput {
  userId: string;
  key: string;
  body?: Types.ResourcesSetUserResourceSettingsInput;
}
export type PutUsersResourcesSettingsOutput = Types.ResourcesResourceSettings;
export const putUsersResourcesSettingsEndpoint = {
  operationId: "putUsersResourcesSettings" as const,
  method: "PUT" as const,
  path: "/v1/users/{userId}/resources/settings/{key}" as const,
  tags: ["UsersResourcesSettings"] as const,
  requiresAuth: true,
} as const;

/**
 * Get my wallet
 */
export type GetWalletInput = void;
export type GetWalletOutput = Types.CommercePaymentsUserWallet;
export const getWalletEndpoint = {
  operationId: "getWallet" as const,
  method: "GET" as const,
  path: "/api/v1/wallet" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Create my wallet
 */
export interface PostWalletInput {
  body?: Types.CommercePaymentsCreateWalletInput;
}
export type PostWalletOutput = Types.CommercePaymentsUserWallet;
export const postWalletEndpoint = {
  operationId: "postWallet" as const,
  method: "POST" as const,
  path: "/api/v1/wallet" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Get my wallet balance
 */
export type GetWalletBalanceInput = void;
export type GetWalletBalanceOutput = void;
export const getWalletBalanceEndpoint = {
  operationId: "getWalletBalance" as const,
  method: "GET" as const,
  path: "/api/v1/wallet/balance" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Lock my wallet
 */
export interface PostWalletLockInput {
  body?: Types.CommercePaymentsLockWalletInput;
}
export type PostWalletLockOutput = void;
export const postWalletLockEndpoint = {
  operationId: "postWalletLock" as const,
  method: "POST" as const,
  path: "/api/v1/wallet:lock" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Unlock my wallet
 */
export type PostWalletUnlockInput = void;
export type PostWalletUnlockOutput = void;
export const postWalletUnlockEndpoint = {
  operationId: "postWalletUnlock" as const,
  method: "POST" as const,
  path: "/api/v1/wallet:unlock" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * List all wallets
 */
export interface GetWalletsForGetWalletsInput {
  query?: {
    page?: number;
    pageSize?: number;
    currency?: string;
    isFrozen?: boolean;
  };
}
export type GetWalletsForGetWalletsOutput = void;
export const getWalletsForGetWalletsEndpoint = {
  operationId: "getWalletsForGetWallets" as const,
  method: "GET" as const,
  path: "/api/v1/wallets" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Get wallet by ID
 */
export interface GetWalletsForGetWalletsByWalletIdInput {
  walletId: string;
}
export type GetWalletsForGetWalletsByWalletIdOutput = void;
export const getWalletsForGetWalletsByWalletIdEndpoint = {
  operationId: "getWalletsForGetWalletsByWalletId" as const,
  method: "GET" as const,
  path: "/api/v1/wallets/{walletId}" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Close wallet
 */
export interface DeleteWalletsInput {
  walletId: string;
}
export type DeleteWalletsOutput = void;
export const deleteWalletsEndpoint = {
  operationId: "deleteWallets" as const,
  method: "DELETE" as const,
  path: "/api/v1/wallets/{walletId}" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Update wallet settings
 */
export interface PatchWalletsInput {
  walletId: string;
  body?: Types.CommercePaymentsModelsPatchWalletInput;
}
export type PatchWalletsOutput = void;
export const patchWalletsEndpoint = {
  operationId: "patchWallets" as const,
  method: "PATCH" as const,
  path: "/api/v1/wallets/{walletId}" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Check if wallet exists
 */
export interface HeadWalletsInput {
  walletId: string;
}
export type HeadWalletsOutput = void;
export const headWalletsEndpoint = {
  operationId: "headWallets" as const,
  method: "HEAD" as const,
  path: "/api/v1/wallets/{walletId}" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Freeze wallet
 */
export interface PostWalletsFreezeInput {
  walletId: string;
  body?: Types.CommercePaymentsModelsFreezeWalletInput;
}
export type PostWalletsFreezeOutput = void;
export const postWalletsFreezeEndpoint = {
  operationId: "postWalletsFreeze" as const,
  method: "POST" as const,
  path: "/api/v1/wallets/{walletId}:freeze" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Unfreeze wallet
 */
export interface PostWalletsUnfreezeInput {
  walletId: string;
}
export type PostWalletsUnfreezeOutput = void;
export const postWalletsUnfreezeEndpoint = {
  operationId: "postWalletsUnfreeze" as const,
  method: "POST" as const,
  path: "/api/v1/wallets/{walletId}:unfreeze" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

/**
 * Get wallet audit log
 */
export interface GetWalletsAuditLogInput {
  walletId: string;
  query?: {
    page?: number;
    pageSize?: number;
  };
}
export type GetWalletsAuditLogOutput = void;
export const getWalletsAuditLogEndpoint = {
  operationId: "getWalletsAuditLog" as const,
  method: "GET" as const,
  path: "/api/v1/wallets/{walletId}/audit-log" as const,
  tags: ["CommercePaymentsWallets"] as const,
  requiresAuth: true,
} as const;

export interface PostApiSocialWishlistInput {
  courseId: string;
  query?: {
    notifyOnSale?: boolean;
    notifyOnUpdate?: boolean;
  };
}
export type PostApiSocialWishlistOutput =
  Types.LearningExperienceSocialServicesCourseWishlist;
export const postApiSocialWishlistEndpoint = {
  operationId: "postApiSocialWishlist" as const,
  method: "POST" as const,
  path: "/api/social/wishlist/{courseId}" as const,
  tags: ["LearningExperienceSocialWishlists"] as const,
  requiresAuth: true,
} as const;

export interface DeleteApiSocialWishlistInput {
  courseId: string;
}
export type DeleteApiSocialWishlistOutput = void;
export const deleteApiSocialWishlistEndpoint = {
  operationId: "deleteApiSocialWishlist" as const,
  method: "DELETE" as const,
  path: "/api/social/wishlist/{courseId}" as const,
  tags: ["LearningExperienceSocialWishlists"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialWishlistMeInput {
  query?: {
    skip?: number;
    take?: number;
  };
}
export type GetApiSocialWishlistMeOutput =
  Array<Types.LearningExperienceSocialServicesCourseWishlist>;
export const getApiSocialWishlistMeEndpoint = {
  operationId: "getApiSocialWishlistMe" as const,
  method: "GET" as const,
  path: "/api/social/wishlist/me" as const,
  tags: ["LearningExperienceSocialWishlists"] as const,
  requiresAuth: true,
} as const;

export interface GetApiSocialWishlistCheckInput {
  courseId: string;
}
export type GetApiSocialWishlistCheckOutput = boolean;
export const getApiSocialWishlistCheckEndpoint = {
  operationId: "getApiSocialWishlistCheck" as const,
  method: "GET" as const,
  path: "/api/social/wishlist/{courseId}/check" as const,
  tags: ["LearningExperienceSocialWishlists"] as const,
  requiresAuth: true,
} as const;

export interface PutApiSocialWishlistPreferencesInput {
  courseId: string;
  body?: Types.LearningExperienceSocialServicesWishlistPreferencesInput;
}
export type PutApiSocialWishlistPreferencesOutput =
  Types.LearningExperienceSocialServicesCourseWishlist;
export const putApiSocialWishlistPreferencesEndpoint = {
  operationId: "putApiSocialWishlistPreferences" as const,
  method: "PUT" as const,
  path: "/api/social/wishlist/{courseId}/preferences" as const,
  tags: ["LearningExperienceSocialWishlists"] as const,
  requiresAuth: true,
} as const;

/** Registry of all endpoints */
export const endpoints = {
  postAccessReviewsCampaigns: postAccessReviewsCampaignsEndpoint,
  getAccessReviewsCampaigns: getAccessReviewsCampaignsEndpoint,
  getAccessReviewsCampaignsActive: getAccessReviewsCampaignsActiveEndpoint,
  postAccessReviewsCampaignsStart: postAccessReviewsCampaignsStartEndpoint,
  postAccessReviewsCampaignsComplete:
    postAccessReviewsCampaignsCompleteEndpoint,
  postAccessReviewsCampaignsCancel: postAccessReviewsCampaignsCancelEndpoint,
  postAccessReviewsCampaignsSendReminders:
    postAccessReviewsCampaignsSendRemindersEndpoint,
  getAccessReviewsItemsPending: getAccessReviewsItemsPendingEndpoint,
  postAccessReviewsItemsApprove: postAccessReviewsItemsApproveEndpoint,
  postAccessReviewsItemsRevoke: postAccessReviewsItemsRevokeEndpoint,
  postAccessReviewsCampaignsProcessExpired:
    postAccessReviewsCampaignsProcessExpiredEndpoint,
  postDelegatedAdmin: postDelegatedAdminEndpoint,
  getDelegatedAdmin: getDelegatedAdminEndpoint,
  deleteDelegatedAdmin: deleteDelegatedAdminEndpoint,
  getDelegatedAdminUserScopes: getDelegatedAdminUserScopesEndpoint,
  getDelegatedAdminUserManagedUsers: getDelegatedAdminUserManagedUsersEndpoint,
  getDelegatedAdminUserManagedResources:
    getDelegatedAdminUserManagedResourcesEndpoint,
  getDelegatedAdminUserCanManageUser:
    getDelegatedAdminUserCanManageUserEndpoint,
  getDelegatedAdminUserCanManageResource:
    getDelegatedAdminUserCanManageResourceEndpoint,
  postJitElevations: postJitElevationsEndpoint,
  postJitElevationsApprove: postJitElevationsApproveEndpoint,
  postJitElevationsDeny: postJitElevationsDenyEndpoint,
  postJitElevationsRevoke: postJitElevationsRevokeEndpoint,
  getJitElevations: getJitElevationsEndpoint,
  getJitElevationsPending: getJitElevationsPendingEndpoint,
  getJitElevationsUser: getJitElevationsUserEndpoint,
  getJitElevationsUserActive: getJitElevationsUserActiveEndpoint,
  getJitElevationsUserCheck: getJitElevationsUserCheckEndpoint,
  postJitElevationsCleanup: postJitElevationsCleanupEndpoint,
  getPermissionAnalyticsUsage: getPermissionAnalyticsUsageEndpoint,
  getPermissionAnalyticsUserActivity:
    getPermissionAnalyticsUserActivityEndpoint,
  getPermissionAnalyticsResourcePatterns:
    getPermissionAnalyticsResourcePatternsEndpoint,
  getPermissionAnalyticsTrends: getPermissionAnalyticsTrendsEndpoint,
  getPermissionAnalyticsAnomalies: getPermissionAnalyticsAnomaliesEndpoint,
  getPermissionAnalyticsReport: getPermissionAnalyticsReportEndpoint,
  postPermissionDelegations: postPermissionDelegationsEndpoint,
  getPermissionDelegations: getPermissionDelegationsEndpoint,
  deletePermissionDelegations: deletePermissionDelegationsEndpoint,
  getPermissionDelegationsDelegate: getPermissionDelegationsDelegateEndpoint,
  getPermissionDelegationsDelegator: getPermissionDelegationsDelegatorEndpoint,
  getPermissionDelegationsCheck: getPermissionDelegationsCheckEndpoint,
  postPermissionDelegationsCleanup: postPermissionDelegationsCleanupEndpoint,
  getAuthorizationResourcesInvitations:
    getAuthorizationResourcesInvitationsEndpoint,
  deleteAuthorizationResourcesInvitations:
    deleteAuthorizationResourcesInvitationsEndpoint,
  getAuthorizationResourcesInvitationsPending:
    getAuthorizationResourcesInvitationsPendingEndpoint,
  postAuthorizationResourcesInvitationsAccept:
    postAuthorizationResourcesInvitationsAcceptEndpoint,
  postAuthorizationResourcesInvitationsDecline:
    postAuthorizationResourcesInvitationsDeclineEndpoint,
  postAuthorizationResourcesShare: postAuthorizationResourcesShareEndpoint,
  putAuthorizationResourcesUsersPermissions:
    putAuthorizationResourcesUsersPermissionsEndpoint,
  deleteAuthorizationResourcesUsersAccess:
    deleteAuthorizationResourcesUsersAccessEndpoint,
  getAuthorizationResourcesPermissions:
    getAuthorizationResourcesPermissionsEndpoint,
  getAuthorizationResourcesHasPermission:
    getAuthorizationResourcesHasPermissionEndpoint,
  getAuthorizationResourcesUsers: getAuthorizationResourcesUsersEndpoint,
  getSodRulesForGetSodRules: getSodRulesForGetSodRulesEndpoint,
  postSodRules: postSodRulesEndpoint,
  getSodRulesForGetSodRulesById: getSodRulesForGetSodRulesByIdEndpoint,
  putSodRules: putSodRulesEndpoint,
  deleteSodRules: deleteSodRulesEndpoint,
  getSodViolationsDetect: getSodViolationsDetectEndpoint,
  getSodViolationsUser: getSodViolationsUserEndpoint,
  getSodViolationsActive: getSodViolationsActiveEndpoint,
  postSodViolationsResolve: postSodViolationsResolveEndpoint,
  postSodViolationsException: postSodViolationsExceptionEndpoint,
  postSodViolationsScan: postSodViolationsScanEndpoint,
  postAuthorizationTenantsGrant: postAuthorizationTenantsGrantEndpoint,
  postAuthorizationTenantsRevoke: postAuthorizationTenantsRevokeEndpoint,
  getAuthorizationTenantsPermissions:
    getAuthorizationTenantsPermissionsEndpoint,
  getAuthorizationTenantsHasPermission:
    getAuthorizationTenantsHasPermissionEndpoint,
  postAuthorizationTenantsGlobalDefaults:
    postAuthorizationTenantsGlobalDefaultsEndpoint,
  postAuthorizationTenantsDefaults: postAuthorizationTenantsDefaultsEndpoint,
  postAuthorizationTenantsDeny: postAuthorizationTenantsDenyEndpoint,
  postAuthorizationTenantsDenyRemove:
    postAuthorizationTenantsDenyRemoveEndpoint,
  getAccessCapabilities: getAccessCapabilitiesEndpoint,
  postCoursesActivityGrades: postCoursesActivityGradesEndpoint,
  getCoursesActivityGradesInteraction:
    getCoursesActivityGradesInteractionEndpoint,
  getCoursesActivityGradesGrader: getCoursesActivityGradesGraderEndpoint,
  getCoursesActivityGradesStudent: getCoursesActivityGradesStudentEndpoint,
  putCoursesActivityGrades: putCoursesActivityGradesEndpoint,
  deleteCoursesActivityGrades: deleteCoursesActivityGradesEndpoint,
  getCoursesActivityGradesPending: getCoursesActivityGradesPendingEndpoint,
  getCoursesActivityGradesStatistics:
    getCoursesActivityGradesStatisticsEndpoint,
  getCoursesActivityGradesContent: getCoursesActivityGradesContentEndpoint,
  getAiStatus: getAiStatusEndpoint,
  postAiChat: postAiChatEndpoint,
  postAiGenerateContent: postAiGenerateContentEndpoint,
  postAiGenerateContentEmail: postAiGenerateContentEmailEndpoint,
  postAiEmail: postAiEmailEndpoint,
  postAiGenerateContentReport: postAiGenerateContentReportEndpoint,
  postAiReport: postAiReportEndpoint,
  postAiGenerateContentListingDescription:
    postAiGenerateContentListingDescriptionEndpoint,
  postAiGenerate: postAiGenerateEndpoint,
  getAiHistory: getAiHistoryEndpoint,
  getAiHistoryExport: getAiHistoryExportEndpoint,
  getAiQuotas: getAiQuotasEndpoint,
  getAiPromptTemplatesForGetAiPromptTemplates:
    getAiPromptTemplatesForGetAiPromptTemplatesEndpoint,
  postAiPromptTemplates: postAiPromptTemplatesEndpoint,
  getAiPromptTemplatesForGetAiPromptTemplatesById:
    getAiPromptTemplatesForGetAiPromptTemplatesByIdEndpoint,
  putAiPromptTemplates: putAiPromptTemplatesEndpoint,
  deleteAiPromptTemplates: deleteAiPromptTemplatesEndpoint,
  postAiPromptTemplatesRender: postAiPromptTemplatesRenderEndpoint,
  postAiPromptTemplatesGenerate: postAiPromptTemplatesGenerateEndpoint,
  postApiAnalyticsEvents: postApiAnalyticsEventsEndpoint,
  getApiAnalyticsTimeseries: getApiAnalyticsTimeseriesEndpoint,
  getApiAnalyticsKpi: getApiAnalyticsKpiEndpoint,
  postApiAnalyticsFunnel: postApiAnalyticsFunnelEndpoint,
  postApiAnalyticsWarehouseRun: postApiAnalyticsWarehouseRunEndpoint,
  getApiAnalyticsWarehouseFacts: getApiAnalyticsWarehouseFactsEndpoint,
  getApiAnalyticsWarehouseExport: getApiAnalyticsWarehouseExportEndpoint,
  getApiAnalyticsDashboards: getApiAnalyticsDashboardsEndpoint,
  postApiAnalyticsDashboards: postApiAnalyticsDashboardsEndpoint,
  getAnalyticsDashboardById: getAnalyticsDashboardByIdEndpoint,
  putApiAnalyticsDashboards: putApiAnalyticsDashboardsEndpoint,
  postAssessments: postAssessmentsEndpoint,
  getAssessments: getAssessmentsEndpoint,
  putAssessments: putAssessmentsEndpoint,
  deleteAssessments: deleteAssessmentsEndpoint,
  getAssessmentsDefinition: getAssessmentsDefinitionEndpoint,
  getAssessmentsCourse: getAssessmentsCourseEndpoint,
  getAssessmentsCourseGroups: getAssessmentsCourseGroupsEndpoint,
  getAssessmentsCourseAnalytics: getAssessmentsCourseAnalyticsEndpoint,
  postAssessmentsGroups: postAssessmentsGroupsEndpoint,
  putAssessmentsGroups: putAssessmentsGroupsEndpoint,
  deleteAssessmentsGroups: deleteAssessmentsGroupsEndpoint,
  putAssessmentsGroup: putAssessmentsGroupEndpoint,
  getAssessmentsInteractiveVideoCues:
    getAssessmentsInteractiveVideoCuesEndpoint,
  postAssessmentsInteractiveVideoCues:
    postAssessmentsInteractiveVideoCuesEndpoint,
  deleteAssessmentsInteractiveVideoCues:
    deleteAssessmentsInteractiveVideoCuesEndpoint,
  getAssessmentsInteractiveVideoCuesContentEnrollments:
    getAssessmentsInteractiveVideoCuesContentEnrollmentsEndpoint,
  postAssessmentsRestore: postAssessmentsRestoreEndpoint,
  postAssessmentsSubmissionsStart: postAssessmentsSubmissionsStartEndpoint,
  postAssessmentsSubmissionsSubmit: postAssessmentsSubmissionsSubmitEndpoint,
  postAssessmentsSubmissionsGrade: postAssessmentsSubmissionsGradeEndpoint,
  getAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionId:
    getAssessmentsSubmissionsForGetAssessmentsSubmissionsBySubmissionIdEndpoint,
  getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissions:
    getAssessmentsSubmissionsForGetAssessmentsByAssessmentIdSubmissionsEndpoint,
  getAssessmentsMySubmissions: getAssessmentsMySubmissionsEndpoint,
  getAssessmentsCanAttempt: getAssessmentsCanAttemptEndpoint,
  getAssetLibraries: getAssetLibrariesEndpoint,
  postAssetLibrariesFolders: postAssetLibrariesFoldersEndpoint,
  putAssetLibrariesFoldersRestriction:
    putAssetLibrariesFoldersRestrictionEndpoint,
  postAssetLibrariesAssetsCopy: postAssetLibrariesAssetsCopyEndpoint,
  getAssetLibrariesAssetsRevisions: getAssetLibrariesAssetsRevisionsEndpoint,
  postAssetLibrariesAssetsRevisionsRestore:
    postAssetLibrariesAssetsRevisionsRestoreEndpoint,
  getAssetsForGetAssets: getAssetsForGetAssetsEndpoint,
  postAssets: postAssetsEndpoint,
  postAssetsBulkUpload: postAssetsBulkUploadEndpoint,
  postAssetsChunkedUploads: postAssetsChunkedUploadsEndpoint,
  postAssetsChunkedUploadsParts: postAssetsChunkedUploadsPartsEndpoint,
  postAssetsChunkedUploadsComplete: postAssetsChunkedUploadsCompleteEndpoint,
  deleteAssetsChunkedUploads: deleteAssetsChunkedUploadsEndpoint,
  getAssetsForGetAssetsById: getAssetsForGetAssetsByIdEndpoint,
  deleteAssets: deleteAssetsEndpoint,
  patchAssets: patchAssetsEndpoint,
  getAssetsPreview: getAssetsPreviewEndpoint,
  getAssetExtractedText: getAssetExtractedTextEndpoint,
  postAssetsGenerateAccessUrl: postAssetsGenerateAccessUrlEndpoint,
  postAssetsBulkDownload: postAssetsBulkDownloadEndpoint,
  getAssetsContent: getAssetsContentEndpoint,
  getSignedAssetExtractedText: getSignedAssetExtractedTextEndpoint,
  postAssetsBulkDelete: postAssetsBulkDeleteEndpoint,
  postAssetsReport: postAssetsReportEndpoint,
  getAssetsSearch: getAssetsSearchEndpoint,
  getAdminAssetsStatistics: getAdminAssetsStatisticsEndpoint,
  getAdminAssetsStatisticsExport: getAdminAssetsStatisticsExportEndpoint,
  getAdminAssetsModerationQueue: getAdminAssetsModerationQueueEndpoint,
  getAdminAssetsReports: getAdminAssetsReportsEndpoint,
  postAdminAssetsReportsReview: postAdminAssetsReportsReviewEndpoint,
  postAdminAssetsForceDelete: postAdminAssetsForceDeleteEndpoint,
  getAdminAssets: getAdminAssetsEndpoint,
  getAdminAssetsGcCandidates: getAdminAssetsGcCandidatesEndpoint,
  postAdminAssetsRunVirusScan: postAdminAssetsRunVirusScanEndpoint,
  getAdminAssetsRetention: getAdminAssetsRetentionEndpoint,
  postAdminAssetsRunGc: postAdminAssetsRunGcEndpoint,
  postAdminAssetsRetentionRun: postAdminAssetsRetentionRunEndpoint,
  postAdminAssetsMarkUndeletable: postAdminAssetsMarkUndeletableEndpoint,
  postAdminAssetsUnmarkUndeletable: postAdminAssetsUnmarkUndeletableEndpoint,
  postAdminAssetsReviewModeration: postAdminAssetsReviewModerationEndpoint,
  getAssetsForGetAssetsByReferenceIdByToken:
    getAssetsForGetAssetsByReferenceIdByTokenEndpoint,
  getE: getEEndpoint,
  getT: getTEndpoint,
  getApiAssetsContent: getApiAssetsContentEndpoint,
  postApiAssetsAccessUrl: postApiAssetsAccessUrlEndpoint,
  postAuthSignUp: postAuthSignUpEndpoint,
  postAuthSignIn: postAuthSignInEndpoint,
  postAuthGoogleSignIn: postAuthGoogleSignInEndpoint,
  postAuthMagicLinkRequest: postAuthMagicLinkRequestEndpoint,
  postAuthMagicLinkConsume: postAuthMagicLinkConsumeEndpoint,
  getAuthGithubAuthorize: getAuthGithubAuthorizeEndpoint,
  postAuthDiscordSignInAuthorize: postAuthDiscordSignInAuthorizeEndpoint,
  postAuthDiscordSignInCallback: postAuthDiscordSignInCallbackEndpoint,
  postAuthTokensRefresh: postAuthTokensRefreshEndpoint,
  postAuthTokensRevoke: postAuthTokensRevokeEndpoint,
  postAuthWeb3Challenge: postAuthWeb3ChallengeEndpoint,
  postAuthEmailSendVerification: postAuthEmailSendVerificationEndpoint,
  postAuthEmailVerify: postAuthEmailVerifyEndpoint,
  postAuthPasswordResetRequest: postAuthPasswordResetRequestEndpoint,
  postAuthPasswordReset: postAuthPasswordResetEndpoint,
  postAuthPasswordChange: postAuthPasswordChangeEndpoint,
  getAuthGithubCallback: getAuthGithubCallbackEndpoint,
  postAuthWeb3Verify: postAuthWeb3VerifyEndpoint,
  headAuthExternalLogins: headAuthExternalLoginsEndpoint,
  postAuthExternalLoginsGoogle: postAuthExternalLoginsGoogleEndpoint,
  postAuthExternalLoginsDiscordLinkAuthorize:
    postAuthExternalLoginsDiscordLinkAuthorizeEndpoint,
  postAuthExternalLoginsDiscordLinkCallback:
    postAuthExternalLoginsDiscordLinkCallbackEndpoint,
  deleteAuthExternalLogins: deleteAuthExternalLoginsEndpoint,
  getAuthApiKeys: getAuthApiKeysEndpoint,
  postAuthApiKeys: postAuthApiKeysEndpoint,
  postAuthApiKeysRevoke: postAuthApiKeysRevokeEndpoint,
  getAuthMfa: getAuthMfaEndpoint,
  postAuthMfaTotpSetup: postAuthMfaTotpSetupEndpoint,
  postAuthMfaTotpComplete: postAuthMfaTotpCompleteEndpoint,
  postAuthMfaVerify: postAuthMfaVerifyEndpoint,
  getAuthMfaBackupCodes: getAuthMfaBackupCodesEndpoint,
  postAuthMfaBackupCodesRegenerate: postAuthMfaBackupCodesRegenerateEndpoint,
  postAuthMfaSmsSetup: postAuthMfaSmsSetupEndpoint,
  postAuthMfaSmsComplete: postAuthMfaSmsCompleteEndpoint,
  getAuthMfaMethods: getAuthMfaMethodsEndpoint,
  postAuthMfaDisable: postAuthMfaDisableEndpoint,
  getRolesForGetRoles: getRolesForGetRolesEndpoint,
  postRoles: postRolesEndpoint,
  getRolesForGetRolesByRoleId: getRolesForGetRolesByRoleIdEndpoint,
  putRoles: putRolesEndpoint,
  deleteRoles: deleteRolesEndpoint,
  getRolesUser: getRolesUserEndpoint,
  postRolesAssign: postRolesAssignEndpoint,
  postRolesRemove: postRolesRemoveEndpoint,
  getAuthServiceAccountsForGetAuthServiceAccounts:
    getAuthServiceAccountsForGetAuthServiceAccountsEndpoint,
  postAuthServiceAccounts: postAuthServiceAccountsEndpoint,
  getAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountId:
    getAuthServiceAccountsForGetAuthServiceAccountsByServiceAccountIdEndpoint,
  deleteAuthServiceAccounts: deleteAuthServiceAccountsEndpoint,
  patchAuthServiceAccounts: patchAuthServiceAccountsEndpoint,
  headAuthServiceAccounts: headAuthServiceAccountsEndpoint,
  postAuthServiceAccountsRotateSecret:
    postAuthServiceAccountsRotateSecretEndpoint,
  postAuthServiceAccountsUnlock: postAuthServiceAccountsUnlockEndpoint,
  postAuthServiceAccountsLock: postAuthServiceAccountsLockEndpoint,
  getAuthServiceAccountsAuditLog: getAuthServiceAccountsAuditLogEndpoint,
  postAuthServiceAccountsDeactivate: postAuthServiceAccountsDeactivateEndpoint,
  postAuthServiceAccountsReactivate: postAuthServiceAccountsReactivateEndpoint,
  patchAuthServiceAccountsScopes: patchAuthServiceAccountsScopesEndpoint,
  postOauthToken: postOauthTokenEndpoint,
  getAuthSessions: getAuthSessionsEndpoint,
  getAuthSessionsAnalyzeSecurity: getAuthSessionsAnalyzeSecurityEndpoint,
  deleteAuthSessions: deleteAuthSessionsEndpoint,
  postAuthSessionsTerminateOthers: postAuthSessionsTerminateOthersEndpoint,
  postAuthSessionsTerminateAll: postAuthSessionsTerminateAllEndpoint,
  postAuthSessionsRefresh: postAuthSessionsRefreshEndpoint,
  getAuthSigningKeys: getAuthSigningKeysEndpoint,
  postAuthSigningKeysRotate: postAuthSigningKeysRotateEndpoint,
  postAuthSigningKeysCleanup: postAuthSigningKeysCleanupEndpoint,
  getAuthTrustedDevices: getAuthTrustedDevicesEndpoint,
  postAuthTrustedDevices: postAuthTrustedDevicesEndpoint,
  deleteAuthTrustedDevices: deleteAuthTrustedDevicesEndpoint,
  postAuthWebauthnRegistrationBegin: postAuthWebauthnRegistrationBeginEndpoint,
  postAuthWebauthnRegistrationComplete:
    postAuthWebauthnRegistrationCompleteEndpoint,
  postAuthWebauthnAuthenticationBegin:
    postAuthWebauthnAuthenticationBeginEndpoint,
  postAuthWebauthnAuthenticationComplete:
    postAuthWebauthnAuthenticationCompleteEndpoint,
  getAuthWebauthnCredentialsForGetAuthWebauthnCredentials:
    getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsEndpoint,
  getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialId:
    getAuthWebauthnCredentialsForGetAuthWebauthnCredentialsByCredentialIdEndpoint,
  deleteAuthWebauthnCredentials: deleteAuthWebauthnCredentialsEndpoint,
  patchAuthWebauthnCredentials: patchAuthWebauthnCredentialsEndpoint,
  headAuthWebauthnCredentials: headAuthWebauthnCredentialsEndpoint,
  postAuthWebauthnCredentialsVerify: postAuthWebauthnCredentialsVerifyEndpoint,
  getAuthWebauthn: getAuthWebauthnEndpoint,
  getBillingCharges: getBillingChargesEndpoint,
  postBillingCharges: postBillingChargesEndpoint,
  getBillingChargeById: getBillingChargeByIdEndpoint,
  postBillingChargesRetry: postBillingChargesRetryEndpoint,
  postBillingChargesRefund: postBillingChargesRefundEndpoint,
  postBillingChargesCancel: postBillingChargesCancelEndpoint,
  postBillingInvoicesRetry: postBillingInvoicesRetryEndpoint,
  getBillingSubscriptions: getBillingSubscriptionsEndpoint,
  postBillingSubscriptions: postBillingSubscriptionsEndpoint,
  getBillingSubscriptionById: getBillingSubscriptionByIdEndpoint,
  postBillingSubscriptionsRenew: postBillingSubscriptionsRenewEndpoint,
  postBillingSubscriptionsCancel: postBillingSubscriptionsCancelEndpoint,
  postBillingWebhooksGooglePay: postBillingWebhooksGooglePayEndpoint,
  postBillingWebhooksApplePay: postBillingWebhooksApplePayEndpoint,
  postBillingWebhooksStripe: postBillingWebhooksStripeEndpoint,
  postBillingWebhooksPaypal: postBillingWebhooksPaypalEndpoint,
  getBillingWebhooksWebhookEvents: getBillingWebhooksWebhookEventsEndpoint,
  postBillingWebhooksWebhookEventsRetry:
    postBillingWebhooksWebhookEventsRetryEndpoint,
  getApiSocialBlogForGetApiSocialBlog:
    getApiSocialBlogForGetApiSocialBlogEndpoint,
  postApiSocialBlog: postApiSocialBlogEndpoint,
  getApiSocialBlogForGetApiSocialBlogById:
    getApiSocialBlogForGetApiSocialBlogByIdEndpoint,
  postApiSocialBlogPublish: postApiSocialBlogPublishEndpoint,
  postApiSocialBlogUnpublish: postApiSocialBlogUnpublishEndpoint,
  postApiSocialBlogFeature: postApiSocialBlogFeatureEndpoint,
  postApiSocialBlogViews: postApiSocialBlogViewsEndpoint,
  getTenantsCapabilitiesForGetTenantsByTenantIdCapabilities:
    getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesEndpoint,
  postTenantsCapabilities: postTenantsCapabilitiesEndpoint,
  getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapability:
    getTenantsCapabilitiesForGetTenantsByTenantIdCapabilitiesByCapabilityEndpoint,
  deleteTenantsCapabilities: deleteTenantsCapabilitiesEndpoint,
  postTenantsCapabilitiesSync: postTenantsCapabilitiesSyncEndpoint,
  getTenantsCapabilitiesAuditLog: getTenantsCapabilitiesAuditLogEndpoint,
  getApiCertificatesMy: getApiCertificatesMyEndpoint,
  getApiCertificates: getApiCertificatesEndpoint,
  getApiCertificatesVerify: getApiCertificatesVerifyEndpoint,
  postApiCertificatesIssue: postApiCertificatesIssueEndpoint,
  postApiCertificatesRevoke: postApiCertificatesRevokeEndpoint,
  getApiCertificatesCourse: getApiCertificatesCourseEndpoint,
  getApiCertificatesTemplatesCourse: getApiCertificatesTemplatesCourseEndpoint,
  getApiCertificatesTemplates: getApiCertificatesTemplatesEndpoint,
  putApiCertificatesTemplates: putApiCertificatesTemplatesEndpoint,
  deleteApiCertificatesTemplates: deleteApiCertificatesTemplatesEndpoint,
  postApiCertificatesTemplates: postApiCertificatesTemplatesEndpoint,
  getApiCertificatesExpiring: getApiCertificatesExpiringEndpoint,
  getClients: getClientsEndpoint,
  postClients: postClientsEndpoint,
  getClientById: getClientByIdEndpoint,
  putClients: putClientsEndpoint,
  deleteClients: deleteClientsEndpoint,
  getClientsModules: getClientsModulesEndpoint,
  putClientsModules: putClientsModulesEndpoint,
  patchClientsModules: patchClientsModulesEndpoint,
  postApiCohorts: postApiCohortsEndpoint,
  getApiCohorts: getApiCohortsEndpoint,
  putApiCohorts: putApiCohortsEndpoint,
  deleteApiCohorts: deleteApiCohortsEndpoint,
  getApiCohortsCourse: getApiCohortsCourseEndpoint,
  getApiCohortsCourseActive: getApiCohortsCourseActiveEndpoint,
  getApiCohortsCourseEnrollable: getApiCohortsCourseEnrollableEndpoint,
  postApiCohortsOpen: postApiCohortsOpenEndpoint,
  postApiCohortsClose: postApiCohortsCloseEndpoint,
  postApiCohortsComplete: postApiCohortsCompleteEndpoint,
  postApiCohortsCancel: postApiCohortsCancelEndpoint,
  getCoursesCohortsScheduleAvailableContent:
    getCoursesCohortsScheduleAvailableContentEndpoint,
  getCoursesCohortsSchedule: getCoursesCohortsScheduleEndpoint,
  putCoursesCohortsSchedule: putCoursesCohortsScheduleEndpoint,
  postCoursesCohortsSchedulePreview: postCoursesCohortsSchedulePreviewEndpoint,
  patchCoursesCohortsScheduleItems: patchCoursesCohortsScheduleItemsEndpoint,
  postCoursesCohortsScheduleItemsShift:
    postCoursesCohortsScheduleItemsShiftEndpoint,
  getCoursesCohortsCalendar: getCoursesCohortsCalendarEndpoint,
  postOrders: postOrdersEndpoint,
  postOrdersItems: postOrdersItemsEndpoint,
  postOrdersComplete: postOrdersCompleteEndpoint,
  getOrders: getOrdersEndpoint,
  postOrdersCapture: postOrdersCaptureEndpoint,
  getSubscriptionsGetMetrics: getSubscriptionsGetMetricsEndpoint,
  getSubscriptionsInvoices: getSubscriptionsInvoicesEndpoint,
  getSubscriptionsUsage: getSubscriptionsUsageEndpoint,
  getSubscriptionsBillingHistory: getSubscriptionsBillingHistoryEndpoint,
  postSubscriptionsActivate: postSubscriptionsActivateEndpoint,
  postSubscriptionsStartTrial: postSubscriptionsStartTrialEndpoint,
  postSubscriptionsEndTrial: postSubscriptionsEndTrialEndpoint,
  postSubscriptionsCancel: postSubscriptionsCancelEndpoint,
  postSubscriptionsSuspend: postSubscriptionsSuspendEndpoint,
  postSubscriptionsPause: postSubscriptionsPauseEndpoint,
  postSubscriptionsResume: postSubscriptionsResumeEndpoint,
  postSubscriptionsReactivate: postSubscriptionsReactivateEndpoint,
  postSubscriptionsUpgrade: postSubscriptionsUpgradeEndpoint,
  postSubscriptionsDowngrade: postSubscriptionsDowngradeEndpoint,
  postSubscriptionsRenew: postSubscriptionsRenewEndpoint,
  postSubscriptionsAutoRenew: postSubscriptionsAutoRenewEndpoint,
  postSubscriptionsExternalIds: postSubscriptionsExternalIdsEndpoint,
  getSubscriptionsForGetSubscriptions:
    getSubscriptionsForGetSubscriptionsEndpoint,
  postSubscriptions: postSubscriptionsEndpoint,
  getSubscriptionsForGetSubscriptionsBySubscriptionId:
    getSubscriptionsForGetSubscriptionsBySubscriptionIdEndpoint,
  putSubscriptions: putSubscriptionsEndpoint,
  deleteSubscriptions: deleteSubscriptionsEndpoint,
  patchSubscriptions: patchSubscriptionsEndpoint,
  headSubscriptions: headSubscriptionsEndpoint,
  getSubscriptionPlansUsage: getSubscriptionPlansUsageEndpoint,
  getSubscriptionPlansSuggestUpgrades:
    getSubscriptionPlansSuggestUpgradesEndpoint,
  getSubscriptionPlansPricing: getSubscriptionPlansPricingEndpoint,
  patchSubscriptionPlansPricing: patchSubscriptionPlansPricingEndpoint,
  postSubscriptionPlansValidateLimits:
    postSubscriptionPlansValidateLimitsEndpoint,
  patchSubscriptionPlansDetails: patchSubscriptionPlansDetailsEndpoint,
  patchSubscriptionPlansLimits: patchSubscriptionPlansLimitsEndpoint,
  patchSubscriptionPlansFeatures: patchSubscriptionPlansFeaturesEndpoint,
  postSubscriptionPlansActivate: postSubscriptionPlansActivateEndpoint,
  postSubscriptionPlansDeactivate: postSubscriptionPlansDeactivateEndpoint,
  postSubscriptionPlansArchive: postSubscriptionPlansArchiveEndpoint,
  postSubscriptionPlansClone: postSubscriptionPlansCloneEndpoint,
  postSubscriptionPlansFeatured: postSubscriptionPlansFeaturedEndpoint,
  postSubscriptionPlansExternalId: postSubscriptionPlansExternalIdEndpoint,
  getSubscriptionPlansForGetSubscriptionPlans:
    getSubscriptionPlansForGetSubscriptionPlansEndpoint,
  postSubscriptionPlans: postSubscriptionPlansEndpoint,
  postSubscriptionPlansCompare: postSubscriptionPlansCompareEndpoint,
  getSubscriptionPlansForGetSubscriptionPlansByPlanId:
    getSubscriptionPlansForGetSubscriptionPlansByPlanIdEndpoint,
  putSubscriptionPlans: putSubscriptionPlansEndpoint,
  deleteSubscriptionPlans: deleteSubscriptionPlansEndpoint,
  headSubscriptionPlans: headSubscriptionPlansEndpoint,
  getAdminAuditLogs: getAdminAuditLogsEndpoint,
  getAdminAuditLogsStatistics: getAdminAuditLogsStatisticsEndpoint,
  postAdminAuditLogsExport: postAdminAuditLogsExportEndpoint,
  getAdminSecurityAudit: getAdminSecurityAuditEndpoint,
  getAdminSecurityAuditAuthentication:
    getAdminSecurityAuditAuthenticationEndpoint,
  getAdminSecurityAuditPermissions: getAdminSecurityAuditPermissionsEndpoint,
  getAdminSecurityAuditDashboard: getAdminSecurityAuditDashboardEndpoint,
  postAdminSecurityAuditExport: postAdminSecurityAuditExportEndpoint,
  getApiComplianceConsentPolicies: getApiComplianceConsentPoliciesEndpoint,
  postApiComplianceConsentPolicies: postApiComplianceConsentPoliciesEndpoint,
  postApiComplianceConsentPoliciesVersions:
    postApiComplianceConsentPoliciesVersionsEndpoint,
  getApiComplianceConsentUsers: getApiComplianceConsentUsersEndpoint,
  postApiComplianceConsentGrant: postApiComplianceConsentGrantEndpoint,
  postApiComplianceConsentRevoke: postApiComplianceConsentRevokeEndpoint,
  postApiComplianceConsentDataSubjectRequests:
    postApiComplianceConsentDataSubjectRequestsEndpoint,
  postApiComplianceConsentDataSubjectRequestsProcess:
    postApiComplianceConsentDataSubjectRequestsProcessEndpoint,
  getApiComplianceConsentDataSubjectRequestsPending:
    getApiComplianceConsentDataSubjectRequestsPendingEndpoint,
  getApiComplianceFerpaStudentsRecords:
    getApiComplianceFerpaStudentsRecordsEndpoint,
  getApiComplianceFerpaStudentsDirectoryInformation:
    getApiComplianceFerpaStudentsDirectoryInformationEndpoint,
  postApiComplianceFerpaRecords: postApiComplianceFerpaRecordsEndpoint,
  getApiComplianceFerpaDirectoryPolicy:
    getApiComplianceFerpaDirectoryPolicyEndpoint,
  putApiComplianceFerpaDirectoryPolicy:
    putApiComplianceFerpaDirectoryPolicyEndpoint,
  getApiComplianceFerpaStudentsConsents:
    getApiComplianceFerpaStudentsConsentsEndpoint,
  postApiComplianceFerpaConsents: postApiComplianceFerpaConsentsEndpoint,
  postApiComplianceFerpaConsentsRevoke:
    postApiComplianceFerpaConsentsRevokeEndpoint,
  postApiComplianceFerpaDisclosures: postApiComplianceFerpaDisclosuresEndpoint,
  getApiComplianceFerpaStudentsDisclosures:
    getApiComplianceFerpaStudentsDisclosuresEndpoint,
  postApiComplianceFerpaInspectionRequests:
    postApiComplianceFerpaInspectionRequestsEndpoint,
  postApiComplianceFerpaInspectionRequestsComplete:
    postApiComplianceFerpaInspectionRequestsCompleteEndpoint,
  getApiComplianceFerpaInspectionRequestsPending:
    getApiComplianceFerpaInspectionRequestsPendingEndpoint,
  getMarketingLeads: getMarketingLeadsEndpoint,
  postMarketingLeads: postMarketingLeadsEndpoint,
  getMarketingLeadById: getMarketingLeadByIdEndpoint,
  getPagesForGetPages: getPagesForGetPagesEndpoint,
  postPages: postPagesEndpoint,
  getPagesForGetPagesById: getPagesForGetPagesByIdEndpoint,
  putPages: putPagesEndpoint,
  deletePages: deletePagesEndpoint,
  getPagesBySlug: getPagesBySlugEndpoint,
  getPagesSitemap: getPagesSitemapEndpoint,
  postPagesPublish: postPagesPublishEndpoint,
  postPagesUnpublish: postPagesUnpublishEndpoint,
  getPagesSectionsForGetPagesByPageIdSections:
    getPagesSectionsForGetPagesByPageIdSectionsEndpoint,
  postPagesSections: postPagesSectionsEndpoint,
  getPagesSectionsForGetPagesByPageIdSectionsBySectionId:
    getPagesSectionsForGetPagesByPageIdSectionsBySectionIdEndpoint,
  putPagesSections: putPagesSectionsEndpoint,
  deletePagesSections: deletePagesSectionsEndpoint,
  postPagesSectionsReorder: postPagesSectionsReorderEndpoint,
  getOg: getOgEndpoint,
  getContentResourcesForGetContentResources:
    getContentResourcesForGetContentResourcesEndpoint,
  postContentResources: postContentResourcesEndpoint,
  getContentResourcesForGetContentResourcesById:
    getContentResourcesForGetContentResourcesByIdEndpoint,
  putContentResources: putContentResourcesEndpoint,
  deleteContentResources: deleteContentResourcesEndpoint,
  getContentResourcesBySlug: getContentResourcesBySlugEndpoint,
  postContentResourcesPublish: postContentResourcesPublishEndpoint,
  postCourseInteractions: postCourseInteractionsEndpoint,
  putCourseInteractionsProgress: putCourseInteractionsProgressEndpoint,
  postCourseInteractionsSubmit: postCourseInteractionsSubmitEndpoint,
  postCourseInteractionsComplete: postCourseInteractionsCompleteEndpoint,
  getCourseInteractionsUserContent: getCourseInteractionsUserContentEndpoint,
  getCourseInteractionsUser: getCourseInteractionsUserEndpoint,
  getCourseInteractionsContentSurveyResults:
    getCourseInteractionsContentSurveyResultsEndpoint,
  getCourseInteractionsContentSurveyResultsVisible:
    getCourseInteractionsContentSurveyResultsVisibleEndpoint,
  getCourseInteractionsContentReflectionResponses:
    getCourseInteractionsContentReflectionResponsesEndpoint,
  getCourseInteractionsContentReflectionResponsesVisible:
    getCourseInteractionsContentReflectionResponsesVisibleEndpoint,
  putCourseInteractionsTimeSpent: putCourseInteractionsTimeSpentEndpoint,
  postVCoursesCheckoutComplete: postVCoursesCheckoutCompleteEndpoint,
  postCoursesStudentsMessage: postCoursesStudentsMessageEndpoint,
  getCoursesSupportTicketsForGetCoursesByCourseIdSupportTickets:
    getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsEndpoint,
  getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketId:
    getCoursesSupportTicketsForGetCoursesByCourseIdSupportTicketsByTicketIdEndpoint,
  postCoursesSupportTicketsMessages: postCoursesSupportTicketsMessagesEndpoint,
  postCoursesSupportTicketsResolve: postCoursesSupportTicketsResolveEndpoint,
  getDiscoveryFeaturedForGetDiscoveryFeatured:
    getDiscoveryFeaturedForGetDiscoveryFeaturedEndpoint,
  postDiscoveryFeatured: postDiscoveryFeaturedEndpoint,
  getDiscoveryFeaturedType: getDiscoveryFeaturedTypeEndpoint,
  getDiscoveryFeaturedForGetDiscoveryFeaturedById:
    getDiscoveryFeaturedForGetDiscoveryFeaturedByIdEndpoint,
  putDiscoveryFeatured: putDiscoveryFeaturedEndpoint,
  deleteDiscoveryFeatured: deleteDiscoveryFeaturedEndpoint,
  patchDiscoveryFeaturedToggle: patchDiscoveryFeaturedToggleEndpoint,
  getDiscoveryCollectionsForGetDiscoveryCollections:
    getDiscoveryCollectionsForGetDiscoveryCollectionsEndpoint,
  postDiscoveryCollections: postDiscoveryCollectionsEndpoint,
  getDiscoveryCollectionsFeatured: getDiscoveryCollectionsFeaturedEndpoint,
  getDiscoveryCollectionsSlug: getDiscoveryCollectionsSlugEndpoint,
  getDiscoveryCollectionsForGetDiscoveryCollectionsById:
    getDiscoveryCollectionsForGetDiscoveryCollectionsByIdEndpoint,
  putDiscoveryCollections: putDiscoveryCollectionsEndpoint,
  deleteDiscoveryCollections: deleteDiscoveryCollectionsEndpoint,
  getDiscoveryCollectionsCurator: getDiscoveryCollectionsCuratorEndpoint,
  postDiscoveryCollectionsPublish: postDiscoveryCollectionsPublishEndpoint,
  postDiscoveryCollectionsUnpublish: postDiscoveryCollectionsUnpublishEndpoint,
  postDiscoverySearchRecord: postDiscoverySearchRecordEndpoint,
  postDiscoverySearchClick: postDiscoverySearchClickEndpoint,
  getDiscoverySearchHistory: getDiscoverySearchHistoryEndpoint,
  getDiscoverySearchPopular: getDiscoverySearchPopularEndpoint,
  postApiSocialDiscussions: postApiSocialDiscussionsEndpoint,
  getApiSocialDiscussions: getApiSocialDiscussionsEndpoint,
  deleteApiSocialDiscussions: deleteApiSocialDiscussionsEndpoint,
  getApiSocialCoursesDiscussions: getApiSocialCoursesDiscussionsEndpoint,
  getApiSocialCoursesContentDiscussions:
    getApiSocialCoursesContentDiscussionsEndpoint,
  postApiSocialDiscussionsPin: postApiSocialDiscussionsPinEndpoint,
  postApiSocialDiscussionsUnpin: postApiSocialDiscussionsUnpinEndpoint,
  postApiSocialDiscussionsResolve: postApiSocialDiscussionsResolveEndpoint,
  getEconomyWallet: getEconomyWalletEndpoint,
  getEconomyWalletTransactions: getEconomyWalletTransactionsEndpoint,
  postEconomyConversionsHardToSoft: postEconomyConversionsHardToSoftEndpoint,
  getEconomyCapabilities: getEconomyCapabilitiesEndpoint,
  getEconomyPayoutsForGetEconomyPayouts:
    getEconomyPayoutsForGetEconomyPayoutsEndpoint,
  getEconomyPayoutRequests: getEconomyPayoutRequestsEndpoint,
  postEconomyPayoutRequests: postEconomyPayoutRequestsEndpoint,
  postEconomyPayoutRequestsCancel: postEconomyPayoutRequestsCancelEndpoint,
  getEconomyPayoutsForGetEconomyPayoutsByOperationId:
    getEconomyPayoutsForGetEconomyPayoutsByOperationIdEndpoint,
  getApiLearningEnrollments: getApiLearningEnrollmentsEndpoint,
  getApiLearningEnrollmentsUsers: getApiLearningEnrollmentsUsersEndpoint,
  getApiLearningEnrollmentsCourses: getApiLearningEnrollmentsCoursesEndpoint,
  postApiLearningEnrollments: postApiLearningEnrollmentsEndpoint,
  patchApiLearningEnrollmentsProgress:
    patchApiLearningEnrollmentsProgressEndpoint,
  postApiLearningEnrollmentsStatus: postApiLearningEnrollmentsStatusEndpoint,
  getEntitlements: getEntitlementsEndpoint,
  postEntitlements: postEntitlementsEndpoint,
  getEntitlementsCheck: getEntitlementsCheckEndpoint,
  postEntitlementsCheckBatch: postEntitlementsCheckBatchEndpoint,
  postEntitlementsRevoke: postEntitlementsRevokeEndpoint,
  postFeaturesEvaluate: postFeaturesEvaluateEndpoint,
  getFeaturesValue: getFeaturesValueEndpoint,
  postFeaturesEvaluateBulk: postFeaturesEvaluateBulkEndpoint,
  getFeaturesEnabled: getFeaturesEnabledEndpoint,
  getFeatures: getFeaturesEndpoint,
  postFeatures: postFeaturesEndpoint,
  getFeatureByKey: getFeatureByKeyEndpoint,
  putFeatures: putFeaturesEndpoint,
  deleteFeatures: deleteFeaturesEndpoint,
  getFeaturesExists: getFeaturesExistsEndpoint,
  postFeaturesEnable: postFeaturesEnableEndpoint,
  postFeaturesDisable: postFeaturesDisableEndpoint,
  postFeaturesToggle: postFeaturesToggleEndpoint,
  getApiSocialFeedMe: getApiSocialFeedMeEndpoint,
  postApiSocialFeedMeGenerate: postApiSocialFeedMeGenerateEndpoint,
  postApiSocialFeedViewed: postApiSocialFeedViewedEndpoint,
  postApiSocialFeedDismiss: postApiSocialFeedDismissEndpoint,
  getApiSocialFeedUsers: getApiSocialFeedUsersEndpoint,
  postApiSocialFeed: postApiSocialFeedEndpoint,
  postApiSocialFeedRead: postApiSocialFeedReadEndpoint,
  postApiSocialFeedHide: postApiSocialFeedHideEndpoint,
  getApiGameJamsForGetApiGameJams: getApiGameJamsForGetApiGameJamsEndpoint,
  postApiGameJams: postApiGameJamsEndpoint,
  getApiGameJamsForGetApiGameJamsById:
    getApiGameJamsForGetApiGameJamsByIdEndpoint,
  postApiGameJamsStatus: postApiGameJamsStatusEndpoint,
  getApiGameJamsSubmissions: getApiGameJamsSubmissionsEndpoint,
  postApiGameJamsSubmissions: postApiGameJamsSubmissionsEndpoint,
  getApiGameJamsCriteria: getApiGameJamsCriteriaEndpoint,
  postApiGameJamsCriteria: postApiGameJamsCriteriaEndpoint,
  postApiGameJamsSubmissionsScores: postApiGameJamsSubmissionsScoresEndpoint,
  getInfo: getInfoEndpoint,
  getHealth: getHealthEndpoint,
  getApiHealth: getApiHealthEndpoint,
  getReady: getReadyEndpoint,
  getApiReady: getApiReadyEndpoint,
  getLive: getLiveEndpoint,
  getApiLive: getApiLiveEndpoint,
  getHealthDependencies: getHealthDependenciesEndpoint,
  getApiHealthDependencies: getApiHealthDependenciesEndpoint,
  getMetrics: getMetricsEndpoint,
  getLaunchPadForGetLaunchPad: getLaunchPadForGetLaunchPadEndpoint,
  postLaunchPad: postLaunchPadEndpoint,
  getLaunchPadForGetLaunchPadById: getLaunchPadForGetLaunchPadByIdEndpoint,
  getLaunchPadProjects: getLaunchPadProjectsEndpoint,
  postLaunchPadChecklistComplete: postLaunchPadChecklistCompleteEndpoint,
  postLaunchPadPublish: postLaunchPadPublishEndpoint,
  getLaunchPadEventsPublicForGetLaunchPadEventsPublic:
    getLaunchPadEventsPublicForGetLaunchPadEventsPublicEndpoint,
  getLaunchPadEventsPublicForGetLaunchPadEventsPublicById:
    getLaunchPadEventsPublicForGetLaunchPadEventsPublicByIdEndpoint,
  getLaunchPadEventsManagementForGetLaunchPadEventsManagement:
    getLaunchPadEventsManagementForGetLaunchPadEventsManagementEndpoint,
  getLaunchPadEventsManagementForGetLaunchPadEventsByIdManagement:
    getLaunchPadEventsManagementForGetLaunchPadEventsByIdManagementEndpoint,
  postLaunchPadEvents: postLaunchPadEventsEndpoint,
  postLaunchPadEventsTransition: postLaunchPadEventsTransitionEndpoint,
  putLaunchPadEvents: putLaunchPadEventsEndpoint,
  postLaunchPadEventsSlots: postLaunchPadEventsSlotsEndpoint,
  putLaunchPadEventsSlots: putLaunchPadEventsSlotsEndpoint,
  deleteLaunchPadEventsSlots: deleteLaunchPadEventsSlotsEndpoint,
  postLaunchPadEventsApplications: postLaunchPadEventsApplicationsEndpoint,
  getLaunchPadEventsApplicationsMe: getLaunchPadEventsApplicationsMeEndpoint,
  putLaunchPadEventsApplications: putLaunchPadEventsApplicationsEndpoint,
  postLaunchPadEventsApplicationsWithdraw:
    postLaunchPadEventsApplicationsWithdrawEndpoint,
  getLaunchPadEventsApplicationsManagement:
    getLaunchPadEventsApplicationsManagementEndpoint,
  postLaunchPadEventsApplicationsReview:
    postLaunchPadEventsApplicationsReviewEndpoint,
  postLaunchPadEventsSlotsRegistrations:
    postLaunchPadEventsSlotsRegistrationsEndpoint,
  getLaunchPadEventsRegistrationsMe: getLaunchPadEventsRegistrationsMeEndpoint,
  postLaunchPadEventsRegistrationsCancel:
    postLaunchPadEventsRegistrationsCancelEndpoint,
  postLaunchPadEventsRegistrationsTransition:
    postLaunchPadEventsRegistrationsTransitionEndpoint,
  getLaunchPadEventsRegistrationsManagement:
    getLaunchPadEventsRegistrationsManagementEndpoint,
  getLaunchPadEventsAnalytics: getLaunchPadEventsAnalyticsEndpoint,
  getLearningMeDashboard: getLearningMeDashboardEndpoint,
  getLearningCoursesWorkspace: getLearningCoursesWorkspaceEndpoint,
  getLearningMeSearch: getLearningMeSearchEndpoint,
  getLearningPathsForGetLearningPaths:
    getLearningPathsForGetLearningPathsEndpoint,
  postLearningPaths: postLearningPathsEndpoint,
  getLearningPathsSearch: getLearningPathsSearchEndpoint,
  getLearningPathsFeatured: getLearningPathsFeaturedEndpoint,
  getLearningPathsPopular: getLearningPathsPopularEndpoint,
  getLearningPathsSlug: getLearningPathsSlugEndpoint,
  getLearningPathsForGetLearningPathsById:
    getLearningPathsForGetLearningPathsByIdEndpoint,
  putLearningPaths: putLearningPathsEndpoint,
  deleteLearningPaths: deleteLearningPathsEndpoint,
  getLearningPathsCreator: getLearningPathsCreatorEndpoint,
  postLearningPathsPublish: postLearningPathsPublishEndpoint,
  postLearningPathsUnpublish: postLearningPathsUnpublishEndpoint,
  postLearningPathsCourses: postLearningPathsCoursesEndpoint,
  deleteLearningPathsCourses: deleteLearningPathsCoursesEndpoint,
  putLearningPathsCoursesOrder: putLearningPathsCoursesOrderEndpoint,
  postLearningPathsEnroll: postLearningPathsEnrollEndpoint,
  postLearningPathsUnenroll: postLearningPathsUnenrollEndpoint,
  getLearningPathsEnrollment: getLearningPathsEnrollmentEndpoint,
  getLearningPathsEnrollmentCheck: getLearningPathsEnrollmentCheckEndpoint,
  putLearningPathsProgress: putLearningPathsProgressEndpoint,
  postLearningPathsComplete: postLearningPathsCompleteEndpoint,
  postLearningPathsAbandon: postLearningPathsAbandonEndpoint,
  getLearningPathsUserEnrollments: getLearningPathsUserEnrollmentsEndpoint,
  getLearningPathsUserCompleted: getLearningPathsUserCompletedEndpoint,
  getLearningPathsStatistics: getLearningPathsStatisticsEndpoint,
  getLearningPathsEnrollments: getLearningPathsEnrollmentsEndpoint,
  getCoursesInteractionsEvents: getCoursesInteractionsEventsEndpoint,
  postCoursesInteractionsEvents: postCoursesInteractionsEventsEndpoint,
  postApiSocialCoursesLike: postApiSocialCoursesLikeEndpoint,
  deleteApiSocialCoursesLike: deleteApiSocialCoursesLikeEndpoint,
  getApiSocialCoursesLikeCheck: getApiSocialCoursesLikeCheckEndpoint,
  getApiSocialCoursesLikeCount: getApiSocialCoursesLikeCountEndpoint,
  getApiSocialLikesMe: getApiSocialLikesMeEndpoint,
  getApiMetricsProduct: getApiMetricsProductEndpoint,
  getApiMetricsProductExport: getApiMetricsProductExportEndpoint,
  getSlaSlosForGetSlaSlos: getSlaSlosForGetSlaSlosEndpoint,
  postSlaSlos: postSlaSlosEndpoint,
  getSlaSlosForGetSlaSlosById: getSlaSlosForGetSlaSlosByIdEndpoint,
  putSlaSlos: putSlaSlosEndpoint,
  deleteSlaSlos: deleteSlaSlosEndpoint,
  postSlaSlis: postSlaSlisEndpoint,
  getSlaSlosCompliance: getSlaSlosComplianceEndpoint,
  getSlaSlosErrorBudget: getSlaSlosErrorBudgetEndpoint,
  getSlaViolations: getSlaViolationsEndpoint,
  postSlaViolationsResolve: postSlaViolationsResolveEndpoint,
  getApiNotificationsForGetApiNotifications:
    getApiNotificationsForGetApiNotificationsEndpoint,
  getApiNotificationsUnreadCount: getApiNotificationsUnreadCountEndpoint,
  getApiNotificationsForGetApiNotificationsById:
    getApiNotificationsForGetApiNotificationsByIdEndpoint,
  deleteApiNotifications: deleteApiNotificationsEndpoint,
  postApiNotificationsRead: postApiNotificationsReadEndpoint,
  postApiNotificationsReadAll: postApiNotificationsReadAllEndpoint,
  postApiNotificationsUnread: postApiNotificationsUnreadEndpoint,
  deleteApiNotificationsRead: deleteApiNotificationsReadEndpoint,
  getApiNotificationsPreferences: getApiNotificationsPreferencesEndpoint,
  putApiNotificationsPreferences: putApiNotificationsPreferencesEndpoint,
  putApiNotificationsPreferencesQuietHours:
    putApiNotificationsPreferencesQuietHoursEndpoint,
  getNotificationsSubscriptions: getNotificationsSubscriptionsEndpoint,
  postNotificationsSubscriptionsResend:
    postNotificationsSubscriptionsResendEndpoint,
  getPayments: getPaymentsEndpoint,
  postPayments: postPaymentsEndpoint,
  postPaymentsSetupIntents: postPaymentsSetupIntentsEndpoint,
  postPaymentsSubscriptionCheckoutsComplete:
    postPaymentsSubscriptionCheckoutsCompleteEndpoint,
  getPaymentById: getPaymentByIdEndpoint,
  postPaymentsCancel: postPaymentsCancelEndpoint,
  postPaymentsRefund: postPaymentsRefundEndpoint,
  postPaymentsRetry: postPaymentsRetryEndpoint,
  getPostsComments: getPostsCommentsEndpoint,
  postPostsComments: postPostsCommentsEndpoint,
  putPostsComments: putPostsCommentsEndpoint,
  deletePostsComments: deletePostsCommentsEndpoint,
  getPostsTagsPopular: getPostsTagsPopularEndpoint,
  getPostsTags: getPostsTagsEndpoint,
  getPostsTagsSearch: getPostsTagsSearchEndpoint,
  postPostsLike: postPostsLikeEndpoint,
  postPostsPin: postPostsPinEndpoint,
  postPostsShare: postPostsShareEndpoint,
  postPostsView: postPostsViewEndpoint,
  getPostsStatistics: getPostsStatisticsEndpoint,
  getPostsFollow: getPostsFollowEndpoint,
  postPostsFollow: postPostsFollowEndpoint,
  deletePostsFollow: deletePostsFollowEndpoint,
  getPostsForGetPosts: getPostsForGetPostsEndpoint,
  postPosts: postPostsEndpoint,
  getPostsForGetPostsByPostId: getPostsForGetPostsByPostIdEndpoint,
  putPosts: putPostsEndpoint,
  deletePosts: deletePostsEndpoint,
  getPostsFeed: getPostsFeedEndpoint,
  getPostsTrending: getPostsTrendingEndpoint,
  getPostsAuthor: getPostsAuthorEndpoint,
  getPostsMy: getPostsMyEndpoint,
  getPostsSearch: getPostsSearchEndpoint,
  postApiPrerequisites: postApiPrerequisitesEndpoint,
  getApiPrerequisites: getApiPrerequisitesEndpoint,
  putApiPrerequisites: putApiPrerequisitesEndpoint,
  deleteApiPrerequisites: deleteApiPrerequisitesEndpoint,
  getApiPrerequisitesCourse: getApiPrerequisitesCourseEndpoint,
  getApiPrerequisitesDependents: getApiPrerequisitesDependentsEndpoint,
  getApiPrerequisitesCourseChain: getApiPrerequisitesCourseChainEndpoint,
  getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheck:
    getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckEndpoint,
  getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserId:
    getApiPrerequisitesCourseCheckForGetApiPrerequisitesCourseByCourseIdCheckByUserIdEndpoint,
  postApiPrerequisitesCourseReorder: postApiPrerequisitesCourseReorderEndpoint,
  getApiPrerequisitesCourseWouldCreateCycle:
    getApiPrerequisitesCourseWouldCreateCycleEndpoint,
  getProductsForGetProductsByProductId:
    getProductsForGetProductsByProductIdEndpoint,
  putProducts: putProductsEndpoint,
  deleteProducts: deleteProductsEndpoint,
  patchProducts: patchProductsEndpoint,
  headProducts: headProductsEndpoint,
  getProductsPricing: getProductsPricingEndpoint,
  getProductsForGetProducts: getProductsForGetProductsEndpoint,
  postProducts: postProductsEndpoint,
  postProductsBatchCreate: postProductsBatchCreateEndpoint,
  postProductsActivate: postProductsActivateEndpoint,
  postProductsDeactivate: postProductsDeactivateEndpoint,
  postProductsArchive: postProductsArchiveEndpoint,
  getCoursesContent: getCoursesContentEndpoint,
  postCoursesContent: postCoursesContentEndpoint,
  getCoursesContentById: getCoursesContentByIdEndpoint,
  putCoursesContent: putCoursesContentEndpoint,
  deleteCoursesContent: deleteCoursesContentEndpoint,
  postCoursesContentSubmit: postCoursesContentSubmitEndpoint,
  getCoursesContentChildren: getCoursesContentChildrenEndpoint,
  postCoursesContentReorder: postCoursesContentReorderEndpoint,
  postCoursesContentMove: postCoursesContentMoveEndpoint,
  getCoursesContentRequired: getCoursesContentRequiredEndpoint,
  getCoursesContentByType: getCoursesContentByTypeEndpoint,
  getCoursesContentByVisibility: getCoursesContentByVisibilityEndpoint,
  postCoursesContentSearch: postCoursesContentSearchEndpoint,
  getCoursesContentStats: getCoursesContentStatsEndpoint,
  getCoursesContentCodingAssignment: getCoursesContentCodingAssignmentEndpoint,
  putCoursesContentCodingAssignment: putCoursesContentCodingAssignmentEndpoint,
  getCoursesContentCodingAssignmentFull:
    getCoursesContentCodingAssignmentFullEndpoint,
  getCoursesForGetCourses: getCoursesForGetCoursesEndpoint,
  postCourses: postCoursesEndpoint,
  getCoursesPublic: getCoursesPublicEndpoint,
  getCoursesMe: getCoursesMeEndpoint,
  getCoursesForGetCoursesById: getCoursesForGetCoursesByIdEndpoint,
  putCourses: putCoursesEndpoint,
  deleteCourses: deleteCoursesEndpoint,
  getCoursesWithContent: getCoursesWithContentEndpoint,
  postCoursesClone: postCoursesCloneEndpoint,
  getCoursesSlug: getCoursesSlugEndpoint,
  postCoursesSelfEnroll: postCoursesSelfEnrollEndpoint,
  postCoursesUsers: postCoursesUsersEndpoint,
  deleteCoursesUsers: deleteCoursesUsersEndpoint,
  getCoursesUsers: getCoursesUsersEndpoint,
  getCoursesUsersProgress: getCoursesUsersProgressEndpoint,
  putCoursesUsersProgress: putCoursesUsersProgressEndpoint,
  getCoursesMeProgress: getCoursesMeProgressEndpoint,
  putCoursesMeProgress: putCoursesMeProgressEndpoint,
  postCoursesUsersContentComplete: postCoursesUsersContentCompleteEndpoint,
  postCoursesMeContentComplete: postCoursesMeContentCompleteEndpoint,
  postCoursesUsersReset: postCoursesUsersResetEndpoint,
  postCoursesMonetize: postCoursesMonetizeEndpoint,
  postCoursesDisableMonetization: postCoursesDisableMonetizationEndpoint,
  getCoursesPricing: getCoursesPricingEndpoint,
  putCoursesPricing: putCoursesPricingEndpoint,
  getCoursesAnalytics: getCoursesAnalyticsEndpoint,
  getCoursesAnalyticsCompletionRates:
    getCoursesAnalyticsCompletionRatesEndpoint,
  getCoursesAnalyticsEngagement: getCoursesAnalyticsEngagementEndpoint,
  getCoursesAnalyticsRevenue: getCoursesAnalyticsRevenueEndpoint,
  postCoursesCreateProduct: postCoursesCreateProductEndpoint,
  postCoursesLinkProduct: postCoursesLinkProductEndpoint,
  deleteCoursesUnlinkProduct: deleteCoursesUnlinkProductEndpoint,
  getCoursesProducts: getCoursesProductsEndpoint,
  postCoursesSubmit: postCoursesSubmitEndpoint,
  postCoursesApprove: postCoursesApproveEndpoint,
  postCoursesReject: postCoursesRejectEndpoint,
  postCoursesWithdraw: postCoursesWithdrawEndpoint,
  postCoursesArchive: postCoursesArchiveEndpoint,
  postCoursesRestore: postCoursesRestoreEndpoint,
  postCoursesPublish: postCoursesPublishEndpoint,
  postCoursesUnpublish: postCoursesUnpublishEndpoint,
  postCoursesSchedule: postCoursesScheduleEndpoint,
  getProjectsOwnership: getProjectsOwnershipEndpoint,
  postProjectsOwnershipTeams: postProjectsOwnershipTeamsEndpoint,
  putProjectsOwnershipTeams: putProjectsOwnershipTeamsEndpoint,
  deleteProjectsOwnershipTeams: deleteProjectsOwnershipTeamsEndpoint,
  postProjectsOwnershipOwnerTeam: postProjectsOwnershipOwnerTeamEndpoint,
  postProjectsOwnershipAllocations: postProjectsOwnershipAllocationsEndpoint,
  putProjectsOwnershipAllocations: putProjectsOwnershipAllocationsEndpoint,
  deleteProjectsOwnershipAllocations:
    deleteProjectsOwnershipAllocationsEndpoint,
  postProjectsOwnershipAgreements: postProjectsOwnershipAgreementsEndpoint,
  postProjectsOwnershipAgreementsCounter:
    postProjectsOwnershipAgreementsCounterEndpoint,
  postProjectsOwnershipAgreementsAccept:
    postProjectsOwnershipAgreementsAcceptEndpoint,
  postProjectsOwnershipAgreementsCancel:
    postProjectsOwnershipAgreementsCancelEndpoint,
  postProjectsOwnershipAgreementsComplete:
    postProjectsOwnershipAgreementsCompleteEndpoint,
  getProjectsPermissionsMyPermissions:
    getProjectsPermissionsMyPermissionsEndpoint,
  getProjectsPermissionsCollaborators:
    getProjectsPermissionsCollaboratorsEndpoint,
  postProjectsPermissionsCollaborators:
    postProjectsPermissionsCollaboratorsEndpoint,
  putProjectsPermissionsCollaborators:
    putProjectsPermissionsCollaboratorsEndpoint,
  deleteProjectsPermissionsCollaborators:
    deleteProjectsPermissionsCollaboratorsEndpoint,
  getProjectsPermissionsRoleTemplates:
    getProjectsPermissionsRoleTemplatesEndpoint,
  postProjectsPermissionsShareWithRole:
    postProjectsPermissionsShareWithRoleEndpoint,
  getProjectsForGetProjects: getProjectsForGetProjectsEndpoint,
  postProjects: postProjectsEndpoint,
  getProjectsMine: getProjectsMineEndpoint,
  getProjectsAccessibleVersions: getProjectsAccessibleVersionsEndpoint,
  getProjectsForGetProjectsById: getProjectsForGetProjectsByIdEndpoint,
  putProjects: putProjectsEndpoint,
  deleteProjects: deleteProjectsEndpoint,
  getProjectsVersions: getProjectsVersionsEndpoint,
  postProjectsVersions: postProjectsVersionsEndpoint,
  getProjectsSlug: getProjectsSlugEndpoint,
  postProjectsPublish: postProjectsPublishEndpoint,
  postProjectsUnpublish: postProjectsUnpublishEndpoint,
  postProjectsArchive: postProjectsArchiveEndpoint,
  postProjectsRestore: postProjectsRestoreEndpoint,
  getProjectsSearch: getProjectsSearchEndpoint,
  getProjectsPopular: getProjectsPopularEndpoint,
  getProjectsRecent: getProjectsRecentEndpoint,
  getProjectsFeatured: getProjectsFeaturedEndpoint,
  getProjectsStatistics: getProjectsStatisticsEndpoint,
  getProjectsCategory: getProjectsCategoryEndpoint,
  getProjectsCreator: getProjectsCreatorEndpoint,
  getProjectsRoleTemplates: getProjectsRoleTemplatesEndpoint,
  getProjectsMyInvitations: getProjectsMyInvitationsEndpoint,
  getProjectsRolesPermissions: getProjectsRolesPermissionsEndpoint,
  postProjectsInvitationsAccept: postProjectsInvitationsAcceptEndpoint,
  postProjectsInvitationsDecline: postProjectsInvitationsDeclineEndpoint,
  getProjectsCollaborators: getProjectsCollaboratorsEndpoint,
  postProjectsCollaborators: postProjectsCollaboratorsEndpoint,
  putProjectsCollaborators: putProjectsCollaboratorsEndpoint,
  deleteProjectsCollaborators: deleteProjectsCollaboratorsEndpoint,
  postProjectsShare: postProjectsShareEndpoint,
  postProjectsInvitations: postProjectsInvitationsEndpoint,
  getProjectsStoreProducts: getProjectsStoreProductsEndpoint,
  postProjectsStoreProducts: postProjectsStoreProductsEndpoint,
  deleteProjectsStoreProducts: deleteProjectsStoreProductsEndpoint,
  getStoreProductsProjects: getStoreProductsProjectsEndpoint,
  getProjectsWork: getProjectsWorkEndpoint,
  postProjectsWorkColumns: postProjectsWorkColumnsEndpoint,
  putProjectsWorkColumns: putProjectsWorkColumnsEndpoint,
  deleteProjectsWorkColumns: deleteProjectsWorkColumnsEndpoint,
  getProjectsWorkMilestones: getProjectsWorkMilestonesEndpoint,
  postProjectsWorkMilestones: postProjectsWorkMilestonesEndpoint,
  putProjectsWorkMilestones: putProjectsWorkMilestonesEndpoint,
  deleteProjectsWorkMilestones: deleteProjectsWorkMilestonesEndpoint,
  postProjectsWorkTasks: postProjectsWorkTasksEndpoint,
  getProjectsWorkTasks: getProjectsWorkTasksEndpoint,
  putProjectsWorkTasks: putProjectsWorkTasksEndpoint,
  deleteProjectsWorkTasks: deleteProjectsWorkTasksEndpoint,
  putProjectsWorkTasksMove: putProjectsWorkTasksMoveEndpoint,
  postProjectsWorkTasksDependencies: postProjectsWorkTasksDependenciesEndpoint,
  deleteProjectsWorkTasksDependencies:
    deleteProjectsWorkTasksDependenciesEndpoint,
  getProjectsWorkLabels: getProjectsWorkLabelsEndpoint,
  postProjectsWorkLabels: postProjectsWorkLabelsEndpoint,
  deleteProjectsWorkLabels: deleteProjectsWorkLabelsEndpoint,
  postProjectsWorkTasksLabels: postProjectsWorkTasksLabelsEndpoint,
  deleteProjectsWorkTasksLabels: deleteProjectsWorkTasksLabelsEndpoint,
  postProjectsWorkTasksComments: postProjectsWorkTasksCommentsEndpoint,
  putProjectsWorkTasksComments: putProjectsWorkTasksCommentsEndpoint,
  deleteProjectsWorkTasksComments: deleteProjectsWorkTasksCommentsEndpoint,
  postProjectsWorkTasksChecklist: postProjectsWorkTasksChecklistEndpoint,
  putProjectsWorkTasksChecklist: putProjectsWorkTasksChecklistEndpoint,
  deleteProjectsWorkTasksChecklist: deleteProjectsWorkTasksChecklistEndpoint,
  getProjectsWorkHistory: getProjectsWorkHistoryEndpoint,
  getPromoCodesForGetPromoCodes: getPromoCodesForGetPromoCodesEndpoint,
  postPromoCodes: postPromoCodesEndpoint,
  getPromoCodesForGetPromoCodesByPromoCodeId:
    getPromoCodesForGetPromoCodesByPromoCodeIdEndpoint,
  putPromoCodes: putPromoCodesEndpoint,
  deletePromoCodes: deletePromoCodesEndpoint,
  patchPromoCodes: patchPromoCodesEndpoint,
  headPromoCodes: headPromoCodesEndpoint,
  getPromoCodesByCode: getPromoCodesByCodeEndpoint,
  getPromoCodesUsage: getPromoCodesUsageEndpoint,
  postPromoCodesActivate: postPromoCodesActivateEndpoint,
  postPromoCodesDeactivate: postPromoCodesDeactivateEndpoint,
  postPromoCodesValidate: postPromoCodesValidateEndpoint,
  postPromoCodesApply: postPromoCodesApplyEndpoint,
  getApiSocialReactionsTarget: getApiSocialReactionsTargetEndpoint,
  getApiSocialReactionsUsersTarget: getApiSocialReactionsUsersTargetEndpoint,
  putApiSocialReactions: putApiSocialReactionsEndpoint,
  deleteApiSocialReactions: deleteApiSocialReactionsEndpoint,
  getRecommendationsMe: getRecommendationsMeEndpoint,
  postRecommendationsMeGenerate: postRecommendationsMeGenerateEndpoint,
  postRecommendationsViewed: postRecommendationsViewedEndpoint,
  postRecommendationsDismiss: postRecommendationsDismissEndpoint,
  postRecommendationsMeRefresh: postRecommendationsMeRefreshEndpoint,
  getRecommendationsMeStatistics: getRecommendationsMeStatisticsEndpoint,
  getRecommendationsMeProfile: getRecommendationsMeProfileEndpoint,
  putRecommendationsMeProfile: putRecommendationsMeProfileEndpoint,
  postRecommendationsMeProfileSkills:
    postRecommendationsMeProfileSkillsEndpoint,
  deleteRecommendationsMeProfileSkills:
    deleteRecommendationsMeProfileSkillsEndpoint,
  getRecommendationsPopular: getRecommendationsPopularEndpoint,
  getRecommendationsTrending: getRecommendationsTrendingEndpoint,
  getRecommendationsCoursesSimilar: getRecommendationsCoursesSimilarEndpoint,
  getApiSocialDiscussionsReplies: getApiSocialDiscussionsRepliesEndpoint,
  postApiSocialDiscussionsReplies: postApiSocialDiscussionsRepliesEndpoint,
  postApiSocialRepliesAccept: postApiSocialRepliesAcceptEndpoint,
  postApiSocialRepliesUpvote: postApiSocialRepliesUpvoteEndpoint,
  deleteApiSocialReplies: deleteApiSocialRepliesEndpoint,
  getReportsChurn: getReportsChurnEndpoint,
  getResourcesUsage: getResourcesUsageEndpoint,
  getResourcesUsageTrends: getResourcesUsageTrendsEndpoint,
  postResourcesArchive: postResourcesArchiveEndpoint,
  postResourcesCleanup: postResourcesCleanupEndpoint,
  postDocumentContractsGenerate: postDocumentContractsGenerateEndpoint,
  postDocumentContractsGenerateBulk: postDocumentContractsGenerateBulkEndpoint,
  postApiContentsVersioningDrafts: postApiContentsVersioningDraftsEndpoint,
  putApiContentsVersioningDrafts: putApiContentsVersioningDraftsEndpoint,
  getApiContentsVersioning: getApiContentsVersioningEndpoint,
  getApiContentsVersioningEntityHistory:
    getApiContentsVersioningEntityHistoryEndpoint,
  getApiContentsVersioningEntityCurrent:
    getApiContentsVersioningEntityCurrentEndpoint,
  getApiContentsVersioningEntityVersion:
    getApiContentsVersioningEntityVersionEndpoint,
  postApiContentsVersioningSubmitForReview:
    postApiContentsVersioningSubmitForReviewEndpoint,
  getApiContentsVersioningPendingReview:
    getApiContentsVersioningPendingReviewEndpoint,
  postApiContentsVersioningApprove: postApiContentsVersioningApproveEndpoint,
  postApiContentsVersioningReject: postApiContentsVersioningRejectEndpoint,
  postApiContentsVersioningPublish: postApiContentsVersioningPublishEndpoint,
  postApiContentsVersioningSchedule: postApiContentsVersioningScheduleEndpoint,
  postApiContentsVersioningCancelSchedule:
    postApiContentsVersioningCancelScheduleEndpoint,
  getApiContentsVersioningCompare: getApiContentsVersioningCompareEndpoint,
  postApiContentsVersioningEntityRollback:
    postApiContentsVersioningEntityRollbackEndpoint,
  postApiContentsVersioningReviews: postApiContentsVersioningReviewsEndpoint,
  postApiSocialReviews: postApiSocialReviewsEndpoint,
  getApiSocialReviews: getApiSocialReviewsEndpoint,
  deleteApiSocialReviews: deleteApiSocialReviewsEndpoint,
  getApiSocialCoursesReviews: getApiSocialCoursesReviewsEndpoint,
  getApiSocialReviewsMe: getApiSocialReviewsMeEndpoint,
  postApiSocialReviewsHelpful: postApiSocialReviewsHelpfulEndpoint,
  getApiSocialCoursesRatingStats: getApiSocialCoursesRatingStatsEndpoint,
  postApiSocialReviewsApprove: postApiSocialReviewsApproveEndpoint,
  postApiSocialReviewsFeature: postApiSocialReviewsFeatureEndpoint,
  patchApiSocialReviewsModeration: patchApiSocialReviewsModerationEndpoint,
  getApiSocialProfilesUsers: getApiSocialProfilesUsersEndpoint,
  putApiSocialProfilesUsers: putApiSocialProfilesUsersEndpoint,
  getApiSocialProfiles: getApiSocialProfilesEndpoint,
  getApiSocialProfilesSearch: getApiSocialProfilesSearchEndpoint,
  putApiSocialProfilesUsersPrivacy: putApiSocialProfilesUsersPrivacyEndpoint,
  putApiSocialProfilesUsersStats: putApiSocialProfilesUsersStatsEndpoint,
  postApiSocialProfilesSkills: postApiSocialProfilesSkillsEndpoint,
  deleteApiSocialProfilesSkills: deleteApiSocialProfilesSkillsEndpoint,
  postApiSocialProfilesPortfolio: postApiSocialProfilesPortfolioEndpoint,
  putApiSocialProfilesPortfolio: putApiSocialProfilesPortfolioEndpoint,
  deleteApiSocialProfilesPortfolio: deleteApiSocialProfilesPortfolioEndpoint,
  getApiSocialGroupsForGetApiSocialGroups:
    getApiSocialGroupsForGetApiSocialGroupsEndpoint,
  postApiSocialGroups: postApiSocialGroupsEndpoint,
  getApiSocialGroupsForGetApiSocialGroupsById:
    getApiSocialGroupsForGetApiSocialGroupsByIdEndpoint,
  putApiSocialGroups: putApiSocialGroupsEndpoint,
  postApiSocialGroupsActivate: postApiSocialGroupsActivateEndpoint,
  postApiSocialGroupsArchive: postApiSocialGroupsArchiveEndpoint,
  postApiSocialGroupsSuspend: postApiSocialGroupsSuspendEndpoint,
  getApiSocialGroupsMembers: getApiSocialGroupsMembersEndpoint,
  postApiSocialGroupsMembers: postApiSocialGroupsMembersEndpoint,
  postApiSocialGroupsMembersApprove: postApiSocialGroupsMembersApproveEndpoint,
  postApiSocialGroupsMembersReject: postApiSocialGroupsMembersRejectEndpoint,
  putApiSocialGroupsMembersRole: putApiSocialGroupsMembersRoleEndpoint,
  deleteApiSocialGroupsMembers: deleteApiSocialGroupsMembersEndpoint,
  getSupportTickets: getSupportTicketsEndpoint,
  postSupportTickets: postSupportTicketsEndpoint,
  getSupportTicketById: getSupportTicketByIdEndpoint,
  postSupportTicketsMessages: postSupportTicketsMessagesEndpoint,
  postSupportTicketsAssign: postSupportTicketsAssignEndpoint,
  postSupportTicketsResolve: postSupportTicketsResolveEndpoint,
  postSupportTicketsClose: postSupportTicketsCloseEndpoint,
  getSupportTicketsMine: getSupportTicketsMineEndpoint,
  postSupportTicketsMine: postSupportTicketsMineEndpoint,
  postSupportTicketsMineMessages: postSupportTicketsMineMessagesEndpoint,
  getTaxJurisdictionsForGetTaxJurisdictions:
    getTaxJurisdictionsForGetTaxJurisdictionsEndpoint,
  postTaxJurisdictions: postTaxJurisdictionsEndpoint,
  getTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionId:
    getTaxJurisdictionsForGetTaxJurisdictionsByJurisdictionIdEndpoint,
  deleteTaxJurisdictions: deleteTaxJurisdictionsEndpoint,
  patchTaxJurisdictions: patchTaxJurisdictionsEndpoint,
  getTaxRulesForGetTaxRules: getTaxRulesForGetTaxRulesEndpoint,
  postTaxRules: postTaxRulesEndpoint,
  getTaxRulesForGetTaxRulesByRuleId: getTaxRulesForGetTaxRulesByRuleIdEndpoint,
  deleteTaxRules: deleteTaxRulesEndpoint,
  patchTaxRules: patchTaxRulesEndpoint,
  postPaymentsTaxCalculate: postPaymentsTaxCalculateEndpoint,
  postTaxesCalculate: postTaxesCalculateEndpoint,
  postPaymentsTaxValidateVat: postPaymentsTaxValidateVatEndpoint,
  postTaxesValidateVat: postTaxesValidateVatEndpoint,
  postPaymentsTaxValidateExemption: postPaymentsTaxValidateExemptionEndpoint,
  postTaxesValidateExemption: postTaxesValidateExemptionEndpoint,
  getTeamsProjects: getTeamsProjectsEndpoint,
  getTeamsForGetTeams: getTeamsForGetTeamsEndpoint,
  postTeams: postTeamsEndpoint,
  getTeamsMine: getTeamsMineEndpoint,
  getTeamsForGetTeamsByTeamId: getTeamsForGetTeamsByTeamIdEndpoint,
  putTeams: putTeamsEndpoint,
  deleteTeams: deleteTeamsEndpoint,
  postTeamsRestore: postTeamsRestoreEndpoint,
  postTeamsMembers: postTeamsMembersEndpoint,
  putTeamsMembers: putTeamsMembersEndpoint,
  deleteTeamsMembers: deleteTeamsMembersEndpoint,
  getTeamsInvitations: getTeamsInvitationsEndpoint,
  postTeamsInvitations: postTeamsInvitationsEndpoint,
  getTeamsMyInvitations: getTeamsMyInvitationsEndpoint,
  deleteTeamsInvitations: deleteTeamsInvitationsEndpoint,
  postTeamsInvitationsAcceptForPostTeamsInvitationsAccept:
    postTeamsInvitationsAcceptForPostTeamsInvitationsAcceptEndpoint,
  postTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAccept:
    postTeamsInvitationsAcceptForPostTeamsInvitationsByInvitationIdAcceptEndpoint,
  postTenantsCreate: postTenantsCreateEndpoint,
  postTenantsUpdate: postTenantsUpdateEndpoint,
  postTenantsReplace: postTenantsReplaceEndpoint,
  postTenantsDelete: postTenantsDeleteEndpoint,
  postTenantsActivateForPostTenantsActivate:
    postTenantsActivateForPostTenantsActivateEndpoint,
  postTenantsDeactivateForPostTenantsDeactivate:
    postTenantsDeactivateForPostTenantsDeactivateEndpoint,
  postTenantsArchiveForPostTenantsArchive:
    postTenantsArchiveForPostTenantsArchiveEndpoint,
  postTenantsUndeleteForPostTenantsUndelete:
    postTenantsUndeleteForPostTenantsUndeleteEndpoint,
  postTenantsPurgeForPostTenantsPurge:
    postTenantsPurgeForPostTenantsPurgeEndpoint,
  postTenantsActivateForPostTenantsByTenantIdActivate:
    postTenantsActivateForPostTenantsByTenantIdActivateEndpoint,
  postTenantsDeactivateForPostTenantsByTenantIdDeactivate:
    postTenantsDeactivateForPostTenantsByTenantIdDeactivateEndpoint,
  postTenantsArchiveForPostTenantsByTenantIdArchive:
    postTenantsArchiveForPostTenantsByTenantIdArchiveEndpoint,
  postTenantsUndeleteForPostTenantsByTenantIdUndelete:
    postTenantsUndeleteForPostTenantsByTenantIdUndeleteEndpoint,
  postTenantsPurgeForPostTenantsByTenantIdPurge:
    postTenantsPurgeForPostTenantsByTenantIdPurgeEndpoint,
  getTenantsAuditLog: getTenantsAuditLogEndpoint,
  getTenantsForGetTenants: getTenantsForGetTenantsEndpoint,
  postTenants: postTenantsEndpoint,
  getTenantsPayments: getTenantsPaymentsEndpoint,
  postTenantsValidate: postTenantsValidateEndpoint,
  getTenantsForGetTenantsByTenantId: getTenantsForGetTenantsByTenantIdEndpoint,
  putTenants: putTenantsEndpoint,
  deleteTenants: deleteTenantsEndpoint,
  patchTenants: patchTenantsEndpoint,
  headTenants: headTenantsEndpoint,
  getTenantsAiHistory: getTenantsAiHistoryEndpoint,
  getTenantsAiHistoryExport: getTenantsAiHistoryExportEndpoint,
  getTenantsAiQuotas: getTenantsAiQuotasEndpoint,
  getTenantsMetadata: getTenantsMetadataEndpoint,
  putTenantsMetadata: putTenantsMetadataEndpoint,
  patchTenantsMetadata: patchTenantsMetadataEndpoint,
  getTenantsMetadataCustomFields: getTenantsMetadataCustomFieldsEndpoint,
  patchTenantsMetadataCustomFields: patchTenantsMetadataCustomFieldsEndpoint,
  getTenantsMetadataTags: getTenantsMetadataTagsEndpoint,
  putTenantsMetadataTags: putTenantsMetadataTagsEndpoint,
  patchTenantsMetadataTags: patchTenantsMetadataTagsEndpoint,
  getTenantsQuotasForGetTenantsByTenantIdQuotas:
    getTenantsQuotasForGetTenantsByTenantIdQuotasEndpoint,
  getTenantsQuotasForGetTenantsByTenantIdQuotasByType:
    getTenantsQuotasForGetTenantsByTenantIdQuotasByTypeEndpoint,
  putTenantsQuotas: putTenantsQuotasEndpoint,
  deleteTenantsQuotas: deleteTenantsQuotasEndpoint,
  postTenantsQuotasReset: postTenantsQuotasResetEndpoint,
  postTenantsQuotasToggle: postTenantsQuotasToggleEndpoint,
  postTenantsQuotasCheck: postTenantsQuotasCheckEndpoint,
  getTenantsResourcesUsageRecords: getTenantsResourcesUsageRecordsEndpoint,
  getTenantsResourcesUsageSummary: getTenantsResourcesUsageSummaryEndpoint,
  getTenantsResourcesLimits: getTenantsResourcesLimitsEndpoint,
  postTenantsResourcesRecord: postTenantsResourcesRecordEndpoint,
  postTenantsResourcesRecordWithQuotaCheck:
    postTenantsResourcesRecordWithQuotaCheckEndpoint,
  postTenantsResourcesReset: postTenantsResourcesResetEndpoint,
  getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadata:
    getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataEndpoint,
  getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKey:
    getTenantsResourcesMetadataForGetTenantsByTenantIdResourcesMetadataByKeyEndpoint,
  putTenantsResourcesMetadata: putTenantsResourcesMetadataEndpoint,
  deleteTenantsResourcesMetadata: deleteTenantsResourcesMetadataEndpoint,
  getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettings:
    getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsEndpoint,
  getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKey:
    getTenantsResourcesSettingsForGetTenantsByTenantIdResourcesSettingsByKeyEndpoint,
  putTenantsResourcesSettings: putTenantsResourcesSettingsEndpoint,
  deleteTenantsResourcesSettings: deleteTenantsResourcesSettingsEndpoint,
  getTenantsResourcesSettingsEffective:
    getTenantsResourcesSettingsEffectiveEndpoint,
  getTenantsSettings: getTenantsSettingsEndpoint,
  putTenantsSettings: putTenantsSettingsEndpoint,
  patchTenantsSettings: patchTenantsSettingsEndpoint,
  getTenantsSettingsFeatureFlags: getTenantsSettingsFeatureFlagsEndpoint,
  patchTenantsSettingsFeatureFlags: patchTenantsSettingsFeatureFlagsEndpoint,
  getTenantsSettingsSystemLimits: getTenantsSettingsSystemLimitsEndpoint,
  patchTenantsSettingsSystemLimits: patchTenantsSettingsSystemLimitsEndpoint,
  getTenantsSettingsIntegrationSettings:
    getTenantsSettingsIntegrationSettingsEndpoint,
  patchTenantsSettingsIntegrationSettings:
    patchTenantsSettingsIntegrationSettingsEndpoint,
  getTestingAnalytics: getTestingAnalyticsEndpoint,
  getTestingAnalyticsExport: getTestingAnalyticsExportEndpoint,
  getTestingEventsSlotsRegistrations:
    getTestingEventsSlotsRegistrationsEndpoint,
  postTestingEventsSlotsRegistrations:
    postTestingEventsSlotsRegistrationsEndpoint,
  deleteTestingEventsRegistrations: deleteTestingEventsRegistrationsEndpoint,
  getTestingEventsRegistrationsMe: getTestingEventsRegistrationsMeEndpoint,
  getTestingEventsParticipants: getTestingEventsParticipantsEndpoint,
  postTestingEventsRegistrationsCheckIn:
    postTestingEventsRegistrationsCheckInEndpoint,
  postTestingEventsRegistrationsCheckOut:
    postTestingEventsRegistrationsCheckOutEndpoint,
  postTestingEventsRegistrationsNoShow:
    postTestingEventsRegistrationsNoShowEndpoint,
  postTestingEventsRegistrationsTestedProjects:
    postTestingEventsRegistrationsTestedProjectsEndpoint,
  postTestingEventsRegistrationsComplete:
    postTestingEventsRegistrationsCompleteEndpoint,
  getTestingEventsFeedbackObligationsMe:
    getTestingEventsFeedbackObligationsMeEndpoint,
  getTestingEventsFeedback: getTestingEventsFeedbackEndpoint,
  postTestingEventsFeedbackObligationsFeedback:
    postTestingEventsFeedbackObligationsFeedbackEndpoint,
  getTestingEventsForGetTestingEvents:
    getTestingEventsForGetTestingEventsEndpoint,
  postTestingEvents: postTestingEventsEndpoint,
  getTestingEventsArchived: getTestingEventsArchivedEndpoint,
  getTestingEventsPublicForGetTestingEventsPublic:
    getTestingEventsPublicForGetTestingEventsPublicEndpoint,
  getTestingEventsPublicForGetTestingEventsPublicByEventId:
    getTestingEventsPublicForGetTestingEventsPublicByEventIdEndpoint,
  getTestingEventsForGetTestingEventsByEventId:
    getTestingEventsForGetTestingEventsByEventIdEndpoint,
  putTestingEvents: putTestingEventsEndpoint,
  deleteTestingEvents: deleteTestingEventsEndpoint,
  postTestingEventsArchive: postTestingEventsArchiveEndpoint,
  postTestingEventsRestore: postTestingEventsRestoreEndpoint,
  postTestingEventsOpenApplications: postTestingEventsOpenApplicationsEndpoint,
  postTestingEventsCloseApplications:
    postTestingEventsCloseApplicationsEndpoint,
  postTestingEventsSchedule: postTestingEventsScheduleEndpoint,
  postTestingEventsActivate: postTestingEventsActivateEndpoint,
  postTestingEventsComplete: postTestingEventsCompleteEndpoint,
  postTestingEventsCancel: postTestingEventsCancelEndpoint,
  putTestingEventsLearning: putTestingEventsLearningEndpoint,
  getTestingEventsSlots: getTestingEventsSlotsEndpoint,
  postTestingEventsSlots: postTestingEventsSlotsEndpoint,
  putTestingEventsSlots: putTestingEventsSlotsEndpoint,
  deleteTestingEventsSlots: deleteTestingEventsSlotsEndpoint,
  getTestingEventsCommittee: getTestingEventsCommitteeEndpoint,
  postTestingEventsCommittee: postTestingEventsCommitteeEndpoint,
  deleteTestingEventsCommittee: deleteTestingEventsCommitteeEndpoint,
  getTestingEventsApplicationsForGetTestingEventsByEventIdApplications:
    getTestingEventsApplicationsForGetTestingEventsByEventIdApplicationsEndpoint,
  postTestingEventsApplications: postTestingEventsApplicationsEndpoint,
  getTestingEventsApplicationsTesterEligibility:
    getTestingEventsApplicationsTesterEligibilityEndpoint,
  getTestingEventsApplicationsMe: getTestingEventsApplicationsMeEndpoint,
  getTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationId:
    getTestingEventsApplicationsForGetTestingEventsApplicationsByApplicationIdEndpoint,
  putTestingEventsApplications: putTestingEventsApplicationsEndpoint,
  getTestingEventsApplicationsReviewPackage:
    getTestingEventsApplicationsReviewPackageEndpoint,
  postTestingEventsApplicationsWithdraw:
    postTestingEventsApplicationsWithdrawEndpoint,
  postTestingEventsApplicationsReview:
    postTestingEventsApplicationsReviewEndpoint,
  postTestingEventsApplicationsVotes:
    postTestingEventsApplicationsVotesEndpoint,
  postTestingEventsApplicationsApprove:
    postTestingEventsApplicationsApproveEndpoint,
  postTestingEventsApplicationsReject:
    postTestingEventsApplicationsRejectEndpoint,
  postTestingEventsApplicationsWaitlist:
    postTestingEventsApplicationsWaitlistEndpoint,
  putTestingEventsApplicationsSlot: putTestingEventsApplicationsSlotEndpoint,
  getTestingFeedback: getTestingFeedbackEndpoint,
  postTestingFeedback: postTestingFeedbackEndpoint,
  getTestingRequestsFeedback: getTestingRequestsFeedbackEndpoint,
  postTestingRequestsFeedback: postTestingRequestsFeedbackEndpoint,
  getTestingFeedbackByUser: getTestingFeedbackByUserEndpoint,
  postTestingFeedbackReport: postTestingFeedbackReportEndpoint,
  postTestingFeedbackQuality: postTestingFeedbackQualityEndpoint,
  getApiTestingLabPermissionsRoleTemplates:
    getApiTestingLabPermissionsRoleTemplatesEndpoint,
  postApiTestingLabPermissionsRoleTemplates:
    postApiTestingLabPermissionsRoleTemplatesEndpoint,
  putApiTestingLabPermissionsRoleTemplates:
    putApiTestingLabPermissionsRoleTemplatesEndpoint,
  deleteApiTestingLabPermissionsRoleTemplates:
    deleteApiTestingLabPermissionsRoleTemplatesEndpoint,
  deleteApiTestingLabPermissionsRoleTemplatesByName:
    deleteApiTestingLabPermissionsRoleTemplatesByNameEndpoint,
  getApiTestingLabPermissionsUsers: getApiTestingLabPermissionsUsersEndpoint,
  postApiTestingLabPermissionsUsersRoles:
    postApiTestingLabPermissionsUsersRolesEndpoint,
  deleteApiTestingLabPermissionsUsersRoles:
    deleteApiTestingLabPermissionsUsersRolesEndpoint,
  postApiTestingLabPermissionsUsersResources:
    postApiTestingLabPermissionsUsersResourcesEndpoint,
  deleteApiTestingLabPermissionsUsersResources:
    deleteApiTestingLabPermissionsUsersResourcesEndpoint,
  getApiTestingLabPermissionsUsersCheck:
    getApiTestingLabPermissionsUsersCheckEndpoint,
  getApiTestingLabSettings: getApiTestingLabSettingsEndpoint,
  putApiTestingLabSettings: putApiTestingLabSettingsEndpoint,
  patchApiTestingLabSettings: patchApiTestingLabSettingsEndpoint,
  postApiTestingLabSettingsReset: postApiTestingLabSettingsResetEndpoint,
  getApiTestingLabSettingsExists: getApiTestingLabSettingsExistsEndpoint,
  getTestingLocationsForGetTestingLocations:
    getTestingLocationsForGetTestingLocationsEndpoint,
  postTestingLocations: postTestingLocationsEndpoint,
  getTestingLocationsForGetTestingLocationsById:
    getTestingLocationsForGetTestingLocationsByIdEndpoint,
  putTestingLocations: putTestingLocationsEndpoint,
  deleteTestingLocations: deleteTestingLocationsEndpoint,
  postTestingLocationsRestore: postTestingLocationsRestoreEndpoint,
  postTestingRequestsParticipants: postTestingRequestsParticipantsEndpoint,
  deleteTestingRequestsParticipants: deleteTestingRequestsParticipantsEndpoint,
  getTestingRequestsParticipants: getTestingRequestsParticipantsEndpoint,
  getTestingRequestsParticipantsCheck:
    getTestingRequestsParticipantsCheckEndpoint,
  postTestingSessionsRegister: postTestingSessionsRegisterEndpoint,
  deleteTestingSessionsRegister: deleteTestingSessionsRegisterEndpoint,
  getTestingSessionsRegistrations: getTestingSessionsRegistrationsEndpoint,
  getTestingSessionsWaitlist: getTestingSessionsWaitlistEndpoint,
  postTestingSessionsWaitlist: postTestingSessionsWaitlistEndpoint,
  deleteTestingSessionsWaitlist: deleteTestingSessionsWaitlistEndpoint,
  getTestingUsersActivity: getTestingUsersActivityEndpoint,
  getTestingAttendanceStudents: getTestingAttendanceStudentsEndpoint,
  getTestingRequestsForGetTestingRequests:
    getTestingRequestsForGetTestingRequestsEndpoint,
  postTestingRequests: postTestingRequestsEndpoint,
  getTestingRequestsForGetTestingRequestsById:
    getTestingRequestsForGetTestingRequestsByIdEndpoint,
  putTestingRequests: putTestingRequestsEndpoint,
  deleteTestingRequests: deleteTestingRequestsEndpoint,
  getTestingRequestsDetails: getTestingRequestsDetailsEndpoint,
  postTestingRequestsRestore: postTestingRequestsRestoreEndpoint,
  getTestingRequestsByProjectVersion:
    getTestingRequestsByProjectVersionEndpoint,
  getTestingRequestsByCreator: getTestingRequestsByCreatorEndpoint,
  getTestingRequestsByStatus: getTestingRequestsByStatusEndpoint,
  getTestingRequestsSearch: getTestingRequestsSearchEndpoint,
  postTestingSubmitSimple: postTestingSubmitSimpleEndpoint,
  getTestingMyRequests: getTestingMyRequestsEndpoint,
  getTestingAvailableForTesting: getTestingAvailableForTestingEndpoint,
  getTestingRequestsStatistics: getTestingRequestsStatisticsEndpoint,
  getTestingSessionsForGetTestingSessions:
    getTestingSessionsForGetTestingSessionsEndpoint,
  postTestingSessions: postTestingSessionsEndpoint,
  getTestingSessionsForGetTestingSessionsById:
    getTestingSessionsForGetTestingSessionsByIdEndpoint,
  putTestingSessions: putTestingSessionsEndpoint,
  deleteTestingSessions: deleteTestingSessionsEndpoint,
  getTestingSessionsDetails: getTestingSessionsDetailsEndpoint,
  postTestingSessionsRestore: postTestingSessionsRestoreEndpoint,
  getTestingPublicSessions: getTestingPublicSessionsEndpoint,
  getTestingSessionsByRequest: getTestingSessionsByRequestEndpoint,
  getTestingSessionsByLocation: getTestingSessionsByLocationEndpoint,
  getTestingSessionsByStatus: getTestingSessionsByStatusEndpoint,
  getTestingSessionsByManager: getTestingSessionsByManagerEndpoint,
  getTestingSessionsSearch: getTestingSessionsSearchEndpoint,
  getTestingSessionsStatistics: getTestingSessionsStatisticsEndpoint,
  getTestingAttendanceSessions: getTestingAttendanceSessionsEndpoint,
  postTestingSessionsAttendance: postTestingSessionsAttendanceEndpoint,
  getTestingSessionsProjects: getTestingSessionsProjectsEndpoint,
  postTestingSessionsProjects: postTestingSessionsProjectsEndpoint,
  deleteTestingSessionsProjects: deleteTestingSessionsProjectsEndpoint,
  postUsersCreate: postUsersCreateEndpoint,
  postUsersUpdate: postUsersUpdateEndpoint,
  postUsersReplace: postUsersReplaceEndpoint,
  postUsersDelete: postUsersDeleteEndpoint,
  postUsersActivateForPostUsersActivate:
    postUsersActivateForPostUsersActivateEndpoint,
  postUsersDeactivateForPostUsersDeactivate:
    postUsersDeactivateForPostUsersDeactivateEndpoint,
  postUsersSuspendForPostUsersSuspend:
    postUsersSuspendForPostUsersSuspendEndpoint,
  postUsersUnsuspendForPostUsersUnsuspend:
    postUsersUnsuspendForPostUsersUnsuspendEndpoint,
  postUsersUndeleteForPostUsersUndelete:
    postUsersUndeleteForPostUsersUndeleteEndpoint,
  postUsersPurgeForPostUsersPurge: postUsersPurgeForPostUsersPurgeEndpoint,
  postUsersActivateForPostUsersByUserIdActivate:
    postUsersActivateForPostUsersByUserIdActivateEndpoint,
  postUsersDeactivateForPostUsersByUserIdDeactivate:
    postUsersDeactivateForPostUsersByUserIdDeactivateEndpoint,
  postUsersSuspendForPostUsersByUserIdSuspend:
    postUsersSuspendForPostUsersByUserIdSuspendEndpoint,
  postUsersUnsuspendForPostUsersByUserIdUnsuspend:
    postUsersUnsuspendForPostUsersByUserIdUnsuspendEndpoint,
  postUsersUndeleteForPostUsersByUserIdUndelete:
    postUsersUndeleteForPostUsersByUserIdUndeleteEndpoint,
  postUsersPurgeForPostUsersByUserIdPurge:
    postUsersPurgeForPostUsersByUserIdPurgeEndpoint,
  getUsersForGetUsers: getUsersForGetUsersEndpoint,
  postUsers: postUsersEndpoint,
  getUsersForGetUsersByUserId: getUsersForGetUsersByUserIdEndpoint,
  putUsers: putUsersEndpoint,
  deleteUsers: deleteUsersEndpoint,
  patchUsers: patchUsersEndpoint,
  headUsers: headUsersEndpoint,
  getUsersMeEntitlements: getUsersMeEntitlementsEndpoint,
  getUsersEntitlements: getUsersEntitlementsEndpoint,
  getUsersMemberships: getUsersMembershipsEndpoint,
  postUsersMemberships: postUsersMembershipsEndpoint,
  headUsersMemberships: headUsersMembershipsEndpoint,
  patchUsersMembershipsRole: patchUsersMembershipsRoleEndpoint,
  postUsersMembershipsDeactivate: postUsersMembershipsDeactivateEndpoint,
  postUsersMembershipsActivate: postUsersMembershipsActivateEndpoint,
  postUsersMembershipsInviteResend: postUsersMembershipsInviteResendEndpoint,
  postUsersMembershipsInviteCancel: postUsersMembershipsInviteCancelEndpoint,
  postUsersMembershipsInviteAccept: postUsersMembershipsInviteAcceptEndpoint,
  getUsersMembershipsCount: getUsersMembershipsCountEndpoint,
  getUsersMetadata: getUsersMetadataEndpoint,
  putUsersMetadata: putUsersMetadataEndpoint,
  patchUsersMetadata: patchUsersMetadataEndpoint,
  getUsersNotificationsForGetUsersByUserIdNotifications:
    getUsersNotificationsForGetUsersByUserIdNotificationsEndpoint,
  postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsRead:
    postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsMarkAsReadEndpoint,
  postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnread:
    postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsMarkAsUnreadEndpoint,
  postUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchive:
    postUsersNotificationsArchiveForPostUsersByUserIdNotificationsArchiveEndpoint,
  postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchive:
    postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsUnarchiveEndpoint,
  getUsersNotificationsForGetUsersByUserIdNotificationsByNotificationId:
    getUsersNotificationsForGetUsersByUserIdNotificationsByNotificationIdEndpoint,
  headUsersNotifications: headUsersNotificationsEndpoint,
  postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsRead:
    postUsersNotificationsMarkAsReadForPostUsersByUserIdNotificationsByNotificationIdMarkAsReadEndpoint,
  postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnread:
    postUsersNotificationsMarkAsUnreadForPostUsersByUserIdNotificationsByNotificationIdMarkAsUnreadEndpoint,
  postUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchive:
    postUsersNotificationsArchiveForPostUsersByUserIdNotificationsByNotificationIdArchiveEndpoint,
  postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchive:
    postUsersNotificationsUnarchiveForPostUsersByUserIdNotificationsByNotificationIdUnarchiveEndpoint,
  getUsersPreferences: getUsersPreferencesEndpoint,
  putUsersPreferences: putUsersPreferencesEndpoint,
  patchUsersPreferences: patchUsersPreferencesEndpoint,
  postUsersPreferencesReset: postUsersPreferencesResetEndpoint,
  getUsersPreferencesNotifications: getUsersPreferencesNotificationsEndpoint,
  putUsersPreferencesNotifications: putUsersPreferencesNotificationsEndpoint,
  patchUsersPreferencesNotifications:
    patchUsersPreferencesNotificationsEndpoint,
  headUsersPreferencesNotifications: headUsersPreferencesNotificationsEndpoint,
  postUsersPreferencesNotificationsReset:
    postUsersPreferencesNotificationsResetEndpoint,
  getUsersPreferencesAccessibility: getUsersPreferencesAccessibilityEndpoint,
  putUsersPreferencesAccessibility: putUsersPreferencesAccessibilityEndpoint,
  patchUsersPreferencesAccessibility:
    patchUsersPreferencesAccessibilityEndpoint,
  headUsersPreferencesAccessibility: headUsersPreferencesAccessibilityEndpoint,
  postUsersPreferencesAccessibilityReset:
    postUsersPreferencesAccessibilityResetEndpoint,
  getUsersPreferencesPrivacy: getUsersPreferencesPrivacyEndpoint,
  putUsersPreferencesPrivacy: putUsersPreferencesPrivacyEndpoint,
  patchUsersPreferencesPrivacy: patchUsersPreferencesPrivacyEndpoint,
  headUsersPreferencesPrivacy: headUsersPreferencesPrivacyEndpoint,
  postUsersPreferencesPrivacyReset: postUsersPreferencesPrivacyResetEndpoint,
  getUsersPreferencesLocalization: getUsersPreferencesLocalizationEndpoint,
  putUsersPreferencesLocalization: putUsersPreferencesLocalizationEndpoint,
  patchUsersPreferencesLocalization: patchUsersPreferencesLocalizationEndpoint,
  headUsersPreferencesLocalization: headUsersPreferencesLocalizationEndpoint,
  postUsersPreferencesLocalizationReset:
    postUsersPreferencesLocalizationResetEndpoint,
  getUsersProfiles: getUsersProfilesEndpoint,
  getUsersProfile: getUsersProfileEndpoint,
  putUsersProfile: putUsersProfileEndpoint,
  patchUsersProfile: patchUsersProfileEndpoint,
  getUsersQuotasForGetUsersByUserIdQuotas:
    getUsersQuotasForGetUsersByUserIdQuotasEndpoint,
  getUsersQuotasForGetUsersByUserIdQuotasByType:
    getUsersQuotasForGetUsersByUserIdQuotasByTypeEndpoint,
  putUsersQuotas: putUsersQuotasEndpoint,
  deleteUsersQuotas: deleteUsersQuotasEndpoint,
  postUsersQuotasReset: postUsersQuotasResetEndpoint,
  postUsersQuotasToggle: postUsersQuotasToggleEndpoint,
  postUsersQuotasCheck: postUsersQuotasCheckEndpoint,
  getUsersResourcesUsageRecords: getUsersResourcesUsageRecordsEndpoint,
  getUsersResourcesUsageSummary: getUsersResourcesUsageSummaryEndpoint,
  getUsersResourcesLimits: getUsersResourcesLimitsEndpoint,
  postUsersResourcesRecord: postUsersResourcesRecordEndpoint,
  postUsersResourcesRecordWithQuotaCheck:
    postUsersResourcesRecordWithQuotaCheckEndpoint,
  postUsersResourcesReset: postUsersResourcesResetEndpoint,
  getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadata:
    getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataEndpoint,
  getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKey:
    getUsersResourcesMetadataForGetUsersByUserIdResourcesMetadataByKeyEndpoint,
  putUsersResourcesMetadata: putUsersResourcesMetadataEndpoint,
  getUsersResourcesSettingsForGetUsersByUserIdResourcesSettings:
    getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsEndpoint,
  getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKey:
    getUsersResourcesSettingsForGetUsersByUserIdResourcesSettingsByKeyEndpoint,
  putUsersResourcesSettings: putUsersResourcesSettingsEndpoint,
  getWallet: getWalletEndpoint,
  postWallet: postWalletEndpoint,
  getWalletBalance: getWalletBalanceEndpoint,
  postWalletLock: postWalletLockEndpoint,
  postWalletUnlock: postWalletUnlockEndpoint,
  getWalletsForGetWallets: getWalletsForGetWalletsEndpoint,
  getWalletsForGetWalletsByWalletId: getWalletsForGetWalletsByWalletIdEndpoint,
  deleteWallets: deleteWalletsEndpoint,
  patchWallets: patchWalletsEndpoint,
  headWallets: headWalletsEndpoint,
  postWalletsFreeze: postWalletsFreezeEndpoint,
  postWalletsUnfreeze: postWalletsUnfreezeEndpoint,
  getWalletsAuditLog: getWalletsAuditLogEndpoint,
  postApiSocialWishlist: postApiSocialWishlistEndpoint,
  deleteApiSocialWishlist: deleteApiSocialWishlistEndpoint,
  getApiSocialWishlistMe: getApiSocialWishlistMeEndpoint,
  getApiSocialWishlistCheck: getApiSocialWishlistCheckEndpoint,
  putApiSocialWishlistPreferences: putApiSocialWishlistPreferencesEndpoint,
} as const;

export type EndpointId = keyof typeof endpoints;
