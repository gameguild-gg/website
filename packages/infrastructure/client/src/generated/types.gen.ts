/**
 * @game-guild/client - Generated Types and Zod Schemas
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 *
 * Generated from: GameGuild API
 * API Version: 1.0
 */
/* eslint-disable @typescript-eslint/no-explicit-any */
import { z } from "zod";

export interface AIAiChatInput {
  provider?: string | null;
  model?: string | null;
  systemPrompt?: string | null;
  messages?: Array<AIAiChatMessage> | null;
  temperature?: number | null;
  maxTokens?: number | null;
}

export interface AIAiChatMessage {
  role?: string | null;
  content?: string | null;
}

export interface AIAiCompletionOutput {
  provider?: string | null;
  model?: string | null;
  text?: string | null;
  finishReason?: string | null;
  usage?: AIAiUsage;
}

export interface AIAiConversationHistoryEntry {
  id?: string;
  userId?: string | null;
  requestKind?: string | null;
  provider?: string | null;
  model?: string | null;
  requestText?: string | null;
  systemPrompt?: string | null;
  responseText?: string | null;
  outcome?: string | null;
  outcomeCode?: string | null;
  outcomeReason?: string | null;
  finishReason?: string | null;
  usage?: AIAiUsage;
  occurredAt?: string;
}

export interface AIAiGeneratedContentDraftInput {
  subject?: string | null;
  context?: string | null;
  audience?: string | null;
  tone?: string | null;
  provider?: string | null;
  model?: string | null;
  maxTokens?: number | null;
}

export interface AIAiGeneratedContentInput {
  kind?: AIAiGeneratedContentKind;
  subject?: string | null;
  context?: string | null;
  audience?: string | null;
  tone?: string | null;
  provider?: string | null;
  model?: string | null;
  maxTokens?: number | null;
}

export type AIAiGeneratedContentKind =
  "Email" | "Report" | "ListingDescription";

export interface AIAiGenerateInput {
  provider?: string | null;
  model?: string | null;
  systemPrompt?: string | null;
  prompt?: string | null;
  temperature?: number | null;
  maxTokens?: number | null;
}

export interface AIAiPromptTemplate {
  id?: string;
  tenantId?: string | null;
  key?: string | null;
  name?: string | null;
  description?: string | null;
  category?: string | null;
  systemPrompt?: string | null;
  prompt?: string | null;
  isActive?: boolean;
  isSystemTemplate?: boolean;
  createdByUserId?: string | null;
  updatedByUserId?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface AIAiPromptTemplateGenerateInput {
  variables?: Record<string, string | null> | null;
  provider?: string | null;
  model?: string | null;
  temperature?: number | null;
  maxTokens?: number | null;
}

export interface AIAiPromptTemplateRenderInput {
  variables?: Record<string, string | null> | null;
}

export interface AIAiPromptTemplateRenderOutput {
  templateId?: string;
  key?: string | null;
  systemPrompt?: string | null;
  prompt?: string | null;
  variables?: Record<string, string | null> | null;
}

export interface AIAiProviderStatus {
  provider?: string | null;
  configured?: boolean;
  defaultModel?: string | null;
  baseUrl?: string | null;
  credentialsConfigured?: boolean;
}

export interface AIAiQuotaStatus {
  resourceType?: string | null;
  currentUsage?: number;
  softLimit?: number | null;
  hardLimit?: number | null;
  remaining?: number;
  usagePercent?: number;
  period?: string | null;
  isActive?: boolean;
  lastReset?: string | null;
  nextReset?: string | null;
}

export interface AIAiQuotaStatusOutput {
  tenantId?: string;
  quotas?: Array<AIAiQuotaStatus> | null;
  generatedAtUtc?: string;
}

export interface AIAiStatusOutput {
  enabled?: boolean;
  defaultProvider?: string | null;
  allowTenantOverrides?: boolean;
  providers?: Array<AIAiProviderStatus> | null;
}

export interface AIAiUsage {
  inputTokens?: number | null;
  outputTokens?: number | null;
  totalTokens?: number | null;
}

export interface AICreateAiPromptTemplateInput {
  key?: string | null;
  name?: string | null;
  prompt?: string | null;
  description?: string | null;
  category?: string | null;
  systemPrompt?: string | null;
  isActive?: boolean | null;
}

export interface AIUpdateAiPromptTemplateInput {
  name?: string | null;
  prompt?: string | null;
  description?: string | null;
  category?: string | null;
  systemPrompt?: string | null;
  isActive?: boolean | null;
}

export interface AnalyticsAnalyticsWarehouseFact {
  id?: string;
  tenantId?: string | null;
  factName?: string | null;
  timestamp?: string;
  runId?: string | null;
  metric?: string | null;
  count?: number | null;
  amountUsd?: number | null;
  dimensions?: Record<string, string | null> | null;
}

export interface AnalyticsAnalyticsWarehouseRunInput {
  asOfUtc?: string | null;
  lookbackDays?: number | null;
  tenantId?: string | null;
}

export interface AnalyticsAnalyticsWarehouseRunOutput {
  runId?: string;
  tenantId?: string | null;
  startUtc?: string;
  asOfUtc?: string;
  factsCreated?: number;
  factsByName?: Record<string, number> | null;
}

export interface AnalyticsAnalyzeFunnelQuery {
  steps?: Array<string> | null;
  startDate?: string;
  endDate?: string;
  tenantId?: string | null;
}

export interface AnalyticsCreateDashboardInput {
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  isDefault?: boolean;
  tenantId?: string | null;
  widgets?: Array<AnalyticsDashboardWidgetInput> | null;
}

export interface AnalyticsDashboard {
  id?: string;
  tenantId?: string | null;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  isDefault?: boolean;
  widgets?: Array<AnalyticsDashboardWidget> | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface AnalyticsDashboardWidget {
  id?: string;
  title?: string | null;
  type?: AnalyticsWidgetType;
  sortOrder?: number;
  configuration?: string | null;
}

export interface AnalyticsDashboardWidgetInput {
  title?: string | null;
  type?: AnalyticsWidgetType;
  sortOrder?: number;
  configuration?: string | null;
}

export interface AnalyticsProductCapacityMetrics {
  totalUserLimit?: number;
  totalStorageMbLimit?: number;
  totalApiCallsLimit?: number;
  unlimitedUserPlans?: number;
  unlimitedStoragePlans?: number;
  unlimitedApiCallPlans?: number;
}

export interface AnalyticsProductCatalogMetrics {
  totalProducts?: number;
  publishedProducts?: number;
  draftProducts?: number;
  bundles?: number;
}

export type AnalyticsProductMetricsExportFormat = "Csv" | "Json";

export interface AnalyticsProductMetricsOutput {
  startUtc?: string;
  endUtc?: string;
  tenantId?: string | null;
  catalog?: AnalyticsProductCatalogMetrics;
  revenue?: AnalyticsProductRevenueMetrics;
  subscriptions?: AnalyticsProductSubscriptionMetrics;
  capacity?: AnalyticsProductCapacityMetrics;
  thresholds?: Array<AnalyticsProductMetricThreshold> | null;
  generatedAtUtc?: string;
}

export interface AnalyticsProductMetricThreshold {
  key?: string | null;
  status?: AnalyticsProductMetricThresholdStatus;
  message?: string | null;
  value?: number;
  warningAt?: number;
  criticalAt?: number;
}

export type AnalyticsProductMetricThresholdStatus =
  "Healthy" | "Warning" | "Critical";

export interface AnalyticsProductRevenueMetrics {
  monthlyRecurringRevenue?: number;
  annualRecurringRevenue?: number;
  salesVolume?: number;
  currency?: string | null;
}

export interface AnalyticsProductSubscriptionMetrics {
  totalSubscribers?: number;
  activeSubscribers?: number;
  trialSubscribers?: number;
  pastDueSubscribers?: number;
  cancelledSubscribers?: number;
  cancelledInPeriod?: number;
  churnRate?: number;
  retentionRate?: number;
}

export type AnalyticsTimeSeriesGranularity = "Hour" | "Day" | "Week" | "Month";

export interface AnalyticsTrackAnalyticsEventCommand {
  eventName?: string | null;
  propertiesJson?: string | null;
  userId?: string | null;
  tenantId?: string | null;
}

export interface AnalyticsUpdateDashboardInput {
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  isDefault?: boolean;
  widgets?: Array<AnalyticsDashboardWidgetInput> | null;
}

export type AnalyticsWidgetType =
  "Counter" | "Chart" | "Table" | "Gauge" | "TimeSeries" | "Funnel";

export interface APIAccessAccessCapabilitiesOutput {
  capabilities?: Array<string> | null;
}

export interface APIControllersApplicationDetails {
  name?: string | null;
  version?: string | null;
  informationalVersion?: string | null;
  description?: string | null;
}

export interface APIControllersApplicationInfoOutput {
  application?: APIControllersApplicationDetails;
  build?: APIControllersBuildDetails;
  runtime?: APIControllersRuntimeDetails;
  process?: APIControllersProcessDetails;
  timestamp?: string;
}

export interface APIControllersBuildDetails {
  timestamp?: string | null;
  configuration?: string | null;
  framework?: string | null;
}

export interface APIControllersDependencyHealthItem {
  name?: string | null;
  status?: string | null;
  duration?: string;
  description?: string | null;
  isHealthy?: boolean;
  tags?: Array<string> | null;
  data?: Record<string, string> | null;
  exception?: string | null;
}

export interface APIControllersDependencyHealthOutput {
  status?: string | null;
  totalDuration?: string;
  timestamp?: string;
  healthyCount?: number;
  unhealthyCount?: number;
  dependencies?: Array<APIControllersDependencyHealthItem> | null;
  error?: string | null;
}

export interface APIControllersEconomySelfServiceCapability {
  capability?: EconomyRiskEconomyValueMovementCapability;
  state?: APISetupEconomyCapabilityReadinessState;
  diagnostics?: Array<string> | null;
}

export interface APIControllersHealthinessOutput {
  status?: string | null;
  duration?: string;
  timestamp?: string;
  checks?: Record<string, APIControllersHealthinessResponseItem> | null;
  error?: string | null;
}

export interface APIControllersHealthinessResponseItem {
  status?: string | null;
  duration?: string;
  description?: string | null;
  data?: Record<string, Record<string, unknown>> | null;
}

export interface APIControllersLivenessOutput {
  status?: string | null;
  alive?: boolean;
  timestamp?: string;
  uptime?: string;
  version?: string | null;
}

export interface APIControllersProcessDetails {
  startTime?: string;
  uptime?: string;
}

export interface APIControllersReadinessOutput {
  status?: string | null;
  ready?: boolean;
  timestamp?: string;
  services?: Record<string, boolean> | null;
  error?: string | null;
}

export interface APIControllersRuntimeDetails {
  dotNetVersion?: string | null;
  osDescription?: string | null;
  osArchitecture?: string | null;
  processArchitecture?: string | null;
}

export interface APIProjectsAddProjectTeamInput {
  teamId?: string;
  role?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  notes?: string | null;
  contributionPercentage?: number;
}

export interface APIProjectsCounterProjectTeamAgreementInput {
  scope?: string | null;
  deliverables?: string | null;
  startsAt?: string;
  endsAt?: string;
}

export interface APIProjectsCreateProjectAllocationInput {
  projectTeamId?: string;
  userId?: string;
  function?: string | null;
  capacityPercentage?: number;
  startsAt?: string;
  endsAt?: string | null;
}

export interface APIProjectsCreateProjectTeamAgreementInput {
  proposingTeamId?: string;
  receivingTeamId?: string;
  scope?: string | null;
  deliverables?: string | null;
  startsAt?: string;
  endsAt?: string;
}

export interface APIProjectsProjectAllocation {
  id?: string;
  projectTeamId?: string;
  userId?: string;
  function?: string | null;
  capacityPercentage?: number;
  startsAt?: string;
  endsAt?: string | null;
  isActive?: boolean;
}

export interface APIProjectsProjectOwnership {
  projectId?: string;
  teams?: Array<APIProjectsProjectTeamOwnership> | null;
  allocations?: Array<APIProjectsProjectAllocation> | null;
  agreements?: Array<APIProjectsProjectTeamAgreement> | null;
}

export interface APIProjectsProjectTeamAgreement {
  id?: string;
  proposingTeamId?: string;
  receivingTeamId?: string;
  proposedByUserId?: string;
  acceptedByUserId?: string | null;
  status?: ProjectsProjectTeamAgreementStatus;
  scope?: string | null;
  deliverables?: string | null;
  startsAt?: string;
  endsAt?: string;
  revision?: number;
}

export interface APIProjectsProjectTeamOwnership {
  id?: string;
  teamId?: string;
  teamName?: string | null;
  teamSlug?: string | null;
  role?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  permissions?: Array<string> | null;
  isActive?: boolean;
  assignedAt?: string;
  endedAt?: string | null;
}

export interface APIProjectsTransferProjectOwnerTeamInput {
  teamId?: string;
}

export interface APIProjectsUpdateProjectAllocationInput {
  function?: string | null;
  capacityPercentage?: number;
  startsAt?: string;
  endsAt?: string | null;
  isActive?: boolean;
}

export interface APIProjectsUpdateProjectTeamInput {
  role?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  notes?: string | null;
  contributionPercentage?: number;
}

export interface APIProjectWorkAddProjectTaskChecklistInput {
  text?: string | null;
}

export interface APIProjectWorkAddProjectTaskCommentInput {
  body?: string | null;
}

export interface APIProjectWorkAddProjectTaskDependencyInput {
  dependsOnTaskId?: string;
}

export interface APIProjectWorkConfigureProjectWorkColumnInput {
  name?: string | null;
  kind?: ProjectWorkProjectWorkColumnKind;
  position?: number;
  workInProgressLimit?: number | null;
}

export interface APIProjectWorkCreateProjectMilestoneInput {
  name?: string | null;
  description?: string | null;
  dueAt?: string | null;
}

export interface APIProjectWorkCreateProjectTaskLabelInput {
  name?: string | null;
  color?: string | null;
}

export interface APIProjectWorkCreateProjectWorkTaskInput {
  columnId?: string;
  title?: string | null;
  description?: string | null;
  priority?: ProjectWorkProjectWorkTaskPriority;
  assigneeUserId?: string | null;
  milestoneId?: string | null;
  dueAt?: string | null;
}

export interface APIProjectWorkMoveProjectWorkTaskInput {
  columnId?: string;
  position?: number;
}

export interface APIProjectWorkProjectBoard {
  id?: string;
  projectId?: string;
  name?: string | null;
  columns?: Array<APIProjectWorkProjectWorkColumn> | null;
}

export interface APIProjectWorkProjectChecklistItem {
  id?: string;
  text?: string | null;
  isCompleted?: boolean;
  position?: number;
}

export interface APIProjectWorkProjectMilestone {
  id?: string;
  name?: string | null;
  description?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
}

export interface APIProjectWorkProjectTaskComment {
  id?: string;
  authorUserId?: string;
  body?: string | null;
  editedAt?: string | null;
  createdAt?: string;
}

export interface APIProjectWorkProjectTaskDependency {
  id?: string;
  dependsOnTaskId?: string;
}

export interface APIProjectWorkProjectTaskLabel {
  id?: string;
  name?: string | null;
  color?: string | null;
}

export interface APIProjectWorkProjectWorkColumn {
  id?: string;
  name?: string | null;
  kind?: ProjectWorkProjectWorkColumnKind;
  position?: number;
  workInProgressLimit?: number | null;
  tasks?: Array<APIProjectWorkProjectWorkTask> | null;
}

export interface APIProjectWorkProjectWorkHistory {
  id?: string;
  taskId?: string | null;
  actorUserId?: string;
  action?: string | null;
  changesJson?: string | null;
  createdAt?: string;
}

export interface APIProjectWorkProjectWorkTask {
  id?: string;
  columnId?: string;
  title?: string | null;
  description?: string | null;
  status?: ProjectWorkProjectWorkTaskStatus;
  priority?: ProjectWorkProjectWorkTaskPriority;
  assigneeUserId?: string | null;
  milestoneId?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
  position?: number;
}

export interface APIProjectWorkProjectWorkTaskDetails {
  task?: APIProjectWorkProjectWorkTask;
  checklist?: Array<APIProjectWorkProjectChecklistItem> | null;
  comments?: Array<APIProjectWorkProjectTaskComment> | null;
  dependencies?: Array<APIProjectWorkProjectTaskDependency> | null;
  labels?: Array<APIProjectWorkProjectTaskLabel> | null;
}

export interface APIProjectWorkUpdateProjectMilestoneInput {
  name?: string | null;
  description?: string | null;
  dueAt?: string | null;
  completedAt?: string | null;
}

export interface APIProjectWorkUpdateProjectTaskChecklistInput {
  isCompleted?: boolean;
}

export interface APIProjectWorkUpdateProjectTaskCommentInput {
  body?: string | null;
}

export interface APIProjectWorkUpdateProjectWorkTaskInput {
  title?: string | null;
  description?: string | null;
  priority?: ProjectWorkProjectWorkTaskPriority;
  assigneeUserId?: string | null;
  milestoneId?: string | null;
  dueAt?: string | null;
}

export type APISetupEconomyCapabilityReadinessState =
  "Disabled" | "Ready" | "ProviderNotReady" | "InvalidConfiguration";

export interface APITeamsAcceptTeamInvitationInput {
  token?: string | null;
}

export interface APITeamsAddTeamMemberInput {
  userId?: string;
  authority?: TeamsTeamMemberAuthority;
  professionalTitle?: string | null;
}

export interface APITeamsChangeTeamMemberInput {
  authority?: TeamsTeamMemberAuthority;
  professionalTitle?: string | null;
}

export interface APITeamsCreateTeamInput {
  name?: string | null;
  slug?: string | null;
  visibility?: TeamsTeamVisibility;
  description?: string | null;
  ownerUserId?: string | null;
}

export interface APITeamsCreateTeamInvitationInput {
  userId?: string | null;
  email?: string | null;
  authority?: TeamsTeamMemberAuthority;
  expiresAt?: string;
}

export interface APITeamsMyTeamInvitation {
  id?: string;
  teamId?: string;
  teamName?: string | null;
  teamSlug?: string | null;
  authority?: TeamsTeamMemberAuthority;
  expiresAt?: string;
}

export interface APITeamsTeam {
  id?: string;
  tenantId?: string;
  name?: string | null;
  slug?: string | null;
  description?: string | null;
  visibility?: TeamsTeamVisibility;
  status?: TeamsTeamStatus;
  isPersonal?: boolean;
  members?: Array<APITeamsTeamMember> | null;
}

export interface APITeamsTeamInvitation {
  id?: string;
  invitedUserId?: string | null;
  invitedEmail?: string | null;
  authority?: TeamsTeamMemberAuthority;
  invitedByUserId?: string;
  expiresAt?: string;
  revokedAt?: string | null;
  usedAt?: string | null;
}

export interface APITeamsTeamInvitationCreated {
  id?: string;
  token?: string | null;
  expiresAt?: string;
}

export interface APITeamsTeamMember {
  userId?: string;
  authority?: TeamsTeamMemberAuthority;
  professionalTitle?: string | null;
  isActive?: boolean;
  joinedAt?: string;
}

export interface APITeamsTeamProjectSummary {
  id?: string;
  title?: string | null;
  slug?: string | null;
  status?: ContentStatus;
  visibility?: ContentVisibility;
  teamRole?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  updatedAt?: string;
}

export interface APITeamsUpdateTeamInput {
  name?: string | null;
  slug?: string | null;
  visibility?: TeamsTeamVisibility;
  description?: string | null;
}

export type AssetsAssetAccessPolicy =
  | "Private"
  | "SignedUrl"
  | "TenantPublic"
  | "Public"
  | "PaidContent"
  | "OwnerOnly"
  | "Authenticated"
  | "Unlisted"
  | "Inherited";

export interface AssetsAssetAccessUrl {
  url?: string | null;
  token?: string | null;
  expiresAt?: string;
  mimeType?: string | null;
}

export type AssetsAssetFolderRestrictionMode =
  "None" | "SelectedTeams" | "TeamAuthorities" | "AllocatedProjectMembers";

export type AssetsAssetKind =
  "Image" | "Video" | "Audio" | "Document" | "Archive" | "Other";

export interface AssetsAssetUploadResult {
  success?: boolean;
  assetReferenceId?: string | null;
  assetContentId?: string | null;
  error?: string | null;
}

export interface AssetsChunkedUploadSession {
  uploadId?: string | null;
  objectKey?: string | null;
  userId?: string;
  fileName?: string | null;
  mimeType?: string | null;
  totalSize?: number;
  totalChunks?: number;
  expiresAt?: string;
  uploadedChunks?: number;
}

export interface AssetsCommandsBulkDeleteAssetItem {
  assetReferenceId?: string;
  success?: boolean;
  contentMarkedForDeletion?: boolean;
  error?: string | null;
}

export interface AssetsCommandsBulkDeleteAssetsOutput {
  totalRequested?: number;
  successful?: number;
  failed?: number;
  items?: Array<AssetsCommandsBulkDeleteAssetItem> | null;
}

export interface AssetsCommandsBulkUploadAssetItem {
  fileName?: string | null;
  success?: boolean;
  assetReferenceId?: string | null;
  assetContentId?: string | null;
  error?: string | null;
}

export interface AssetsCommandsBulkUploadAssetsOutput {
  totalRequested?: number;
  successful?: number;
  failed?: number;
  items?: Array<AssetsCommandsBulkUploadAssetItem> | null;
}

export interface AssetsControllersAssetExtractedTextOutput {
  assetId?: string;
  mimeType?: string | null;
  status?: string | null;
  source?: string | null;
  text?: string | null;
  message?: string | null;
  usedOcr?: boolean;
  isPartial?: boolean;
}

export interface AssetsControllersBulkAssetAccessUrlInput {
  assetIds?: Array<string> | null;
  directStorageUrl?: boolean;
}

export interface AssetsControllersBulkDeleteAssetsInput {
  assetIds?: Array<string> | null;
}

export interface AssetsControllersContentModerationInput {
  status?: AssetsModerationStatus;
  labels?: Array<string> | null;
  notes?: string | null;
}

export interface AssetsControllersCopyAssetReferenceInput {
  displayName?: string | null;
  folderId?: string | null;
}

export interface AssetsControllersCreateAssetFolderInput {
  name?: string | null;
  parentFolderId?: string | null;
}

export interface AssetsControllersMarkNonDeletableInput {
  reason?: string | null;
}

export interface AssetsControllersReportAssetInput {
  reason?: AssetsReportReason;
  description?: string | null;
}

export interface AssetsControllersRestrictAssetFolderInput {
  mode?: AssetsAssetFolderRestrictionMode;
  teamIds?: Array<string> | null;
  authorities?: Array<string> | null;
}

export interface AssetsControllersReviewReportInput {
  decision?: AssetsReviewDecision;
  notes?: string | null;
}

export interface AssetsControllersUpdateAssetInput {
  displayName?: string | null;
  accessPolicy?: AssetsAssetAccessPolicy;
}

export interface AssetsControllersUpdateVirusScanInput {
  status?: AssetsVirusScanStatus;
  scanResult?: string | null;
}

export type AssetsImageFit =
  "Contain" | "Cover" | "Fill" | "Inside" | "Outside";

export type AssetsImageFormat =
  "Original" | "Jpeg" | "Png" | "Webp" | "Avif" | "Gif";

export type AssetsModerationStatus =
  | "Pending"
  | "Processing"
  | "Approved"
  | "Rejected"
  | "NeedsReview"
  | "ApprovedWithWarning"
  | "Blocked";

export interface AssetsQueriesAssetPreviewOutput {
  assetReferenceId?: string;
  assetContentId?: string;
  displayName?: string | null;
  mimeType?: string | null;
  kind?: AssetsAssetKind;
  previewMode?: string | null;
  canInlinePreview?: boolean;
  isBlocked?: boolean;
  contentUrl?: string | null;
  thumbnailUrl?: string | null;
  expiresAt?: string | null;
  extractedTextPreview?: string | null;
  usedOcr?: boolean;
  isTextTruncated?: boolean;
  warnings?: Array<string> | null;
}

export interface AssetsQueriesAssetRetentionCandidateOutput {
  assetContentId?: string;
  bucketName?: string | null;
  objectKey?: string | null;
  mimeType?: string | null;
  sizeBytes?: number;
  markedForDeletionAt?: string | null;
}

export interface AssetsQueriesAssetRetentionReportOutput {
  gracePeriodHours?: number;
  limit?: number;
  candidates?: number;
  onLegalHold?: number;
  markedForDeletion?: number;
  candidateBytes?: number;
  items?: Array<AssetsQueriesAssetRetentionCandidateOutput> | null;
}

export interface AssetsQueriesAssetSearchOutput {
  totalMatched?: number;
  returned?: number;
  items?: Array<AssetsQueriesAssetSearchResult> | null;
}

export interface AssetsQueriesAssetSearchResult {
  assetReferenceId?: string;
  assetContentId?: string;
  displayName?: string | null;
  originalFilename?: string | null;
  parentResourceType?: string | null;
  parentResourceId?: string | null;
  mimeType?: string | null;
  kind?: AssetsAssetKind;
  sizeBytes?: number;
  accessCount?: number;
  createdAt?: string;
  lastAccessedAt?: string | null;
}

export interface AssetsQueriesAssetStatisticsOutput {
  totalAssets?: number;
  totalContentObjects?: number;
  totalBytes?: number;
  documentAssets?: number;
  imageAssets?: number;
  videoAssets?: number;
  totalAccesses?: number;
  pendingVirusScans?: number;
  pendingModeration?: number;
  blockedOrRejected?: number;
  legalHoldContent?: number;
  retentionCandidates?: number;
}

export interface AssetsQueriesBulkAssetAccessUrlItem {
  assetReferenceId?: string;
  success?: boolean;
  url?: string | null;
  token?: string | null;
  expiresAt?: string | null;
  mimeType?: string | null;
  error?: string | null;
}

export interface AssetsQueriesBulkAssetAccessUrlsOutput {
  totalRequested?: number;
  successful?: number;
  failed?: number;
  items?: Array<AssetsQueriesBulkAssetAccessUrlItem> | null;
}

export type AssetsReportReason =
  | "Inappropriate"
  | "Copyright"
  | "Spam"
  | "Violence"
  | "Harassment"
  | "Misinformation"
  | "Other";

export type AssetsReviewDecision =
  | "NoAction"
  | "ContentRemoved"
  | "ContentHidden"
  | "UserWarned"
  | "UserSuspended"
  | "BlockContent";

export interface AssetsSecurityAccessUrlInput {
  transform?: string | null;
  directStorage?: boolean;
}

export type AssetsVirusScanStatus =
  "Pending" | "Scanning" | "Clean" | "Infected" | "ScanFailed";

export type BillingCycle =
  | "Weekly"
  | "Monthly"
  | "Quarterly"
  | "SemiAnnually"
  | "Annually"
  | "Biannually";

export interface BulkOperationError {
  tenantId?: string;
  tenantName?: string | null;
  errorMessage?: string | null;
  errorCode?: string | null;
}

export interface BulkOperationOutput {
  totalRequested?: number;
  successfulOperations?: number;
  failedOperations?: number;
  errors?: Array<BulkOperationError> | null;
  isComplete?: boolean;
  successRate?: number;
}

export interface CommerceBillingInvoicePaymentRetryResult {
  invoiceId?: string;
  invoiceNumber?: string | null;
  invoiceStatus?: CommerceBillingInvoiceStatus;
  accepted?: boolean;
  code?: string | null;
  message?: string | null;
  retryScheduledAt?: string | null;
}

export type CommerceBillingInvoiceStatus =
  "Draft" | "Open" | "Paid" | "Void" | "PastDue" | "Uncollectible";

export type CommerceOrderChargeState =
  | "Succeeded"
  | "Failed"
  | "Processing"
  | "RequiresAction"
  | "RequiresReconciliation";

export interface CommerceOrdersAddOrderItemInput {
  productId?: string;
  productPricingId?: string;
  productPricingVersionId?: string;
  quantity?: number;
  promoCode?: string | null;
}

export interface CommerceOrdersCaptureOrderInput {
  paymentMethodId?: string | null;
}

export interface CommerceOrdersCompleteOrderInput {
  paymentId?: string | null;
  paymentProviderReference?: string | null;
  paymentMethod?: string | null;
}

export interface CommerceOrdersCreateOrderInput {
  idempotencyKey?: string | null;
}

export interface CommerceOrdersOrder {
  id?: string;
  userId?: string;
  idempotencyKey?: string | null;
  status?: CommerceOrdersOrderStatus;
  subtotal?: number;
  discountTotal?: number;
  taxAmount?: number;
  total?: number;
  currency?: string | null;
  paymentProviderReference?: string | null;
  paymentMethod?: string | null;
  paidAt?: string | null;
  refundedAt?: string | null;
  refundAmount?: number | null;
  refundReason?: string | null;
  createdAt?: string;
  updatedAt?: string;
  lineItems?: Array<CommerceOrdersOrderLineItem> | null;
}

export interface CommerceOrdersOrderCapture {
  id?: string;
  userId?: string;
  idempotencyKey?: string | null;
  status?: CommerceOrdersOrderStatus;
  subtotal?: number;
  discountTotal?: number;
  taxAmount?: number;
  total?: number;
  currency?: string | null;
  paymentProviderReference?: string | null;
  paymentMethod?: string | null;
  paidAt?: string | null;
  refundedAt?: string | null;
  refundAmount?: number | null;
  refundReason?: string | null;
  createdAt?: string;
  updatedAt?: string;
  lineItems?: Array<CommerceOrdersOrderLineItem> | null;
  paymentState?: CommerceOrderChargeState;
  paymentId?: string | null;
  clientActionToken?: string | null;
  paymentMessage?: string | null;
}

export interface CommerceOrdersOrderLineItem {
  id?: string;
  productId?: string;
  productPricingId?: string;
  productPricingVersionId?: string;
  priceVersion?: number;
  productName?: string | null;
  unitPrice?: number;
  basePrice?: number;
  salePrice?: number | null;
  currency?: string | null;
  quantity?: number;
  discountAmount?: number;
  promoCodesApplied?: string | null;
  lineTotal?: number;
  isSubscription?: boolean;
}

export type CommerceOrdersOrderStatus =
  | "Pending"
  | "Processing"
  | "Completed"
  | "Failed"
  | "Cancelled"
  | "Refunded"
  | "PartiallyRefunded"
  | "Disputed"
  | "Paid"
  | "Fulfilled"
  | "OnHold";

export interface CommercePaymentsBillingChargesControllerCancelBillingChargeInput {
  cancellationReason?: string | null;
  canceledBy?: string | null;
}

export interface CommercePaymentsBillingChargesControllerCreateBillingChargeInput {
  tenantId?: string;
  subscriptionId?: string;
  amount?: number;
  paymentMethodId?: string | null;
}

export interface CommercePaymentsBillingChargesControllerRefundBillingChargeInput {
  amount?: number | null;
  reason?: string | null;
}

export interface CommercePaymentsCalculateTaxInput {
  jurisdictionCode: string | null;
  amount: number;
  currency: string | null;
  customerType: string | null;
  productCategory?: string | null;
  customerVatNumber?: string | null;
  isTaxInclusive?: boolean;
  transactionDate?: string | null;
  applicableExemptions?: Array<string> | null;
}

export interface CommercePaymentsCreateTaxJurisdictionInput {
  code?: string | null;
  name?: string | null;
  country?: string | null;
  state?: string | null;
  taxType?: string | null;
  defaultRate?: number;
}

export interface CommercePaymentsCreateTaxRuleInput {
  jurisdictionCode?: string | null;
  productCategory?: string | null;
  customerType?: string | null;
  rate?: number;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  description?: string | null;
}

export interface CommercePaymentsCreateWalletInput {
  currency?: string | null;
}

export type CommercePaymentsCustomerType = "B2C" | "B2B";

export interface CommercePaymentsLockWalletInput {
  reason: string | null;
}

export interface CommercePaymentsModelsFreezeWalletInput {
  reason?: string | null;
}

export interface CommercePaymentsModelsPatchWalletInput {
  currency?: string | null;
  dailyLimit?: number | null;
  monthlyLimit?: number | null;
}

export interface CommercePaymentsPatchTaxJurisdictionInput {
  name?: string | null;
  taxType?: string | null;
  defaultRate?: number | null;
  isActive?: boolean | null;
}

export interface CommercePaymentsPatchTaxRuleInput {
  rate?: number | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  description?: string | null;
  isActive?: boolean | null;
}

export interface CommercePaymentsPaymentCancellationResult {
  paymentId: string;
  cancellationReason: string | null;
  canceledAt: string;
  canceledBy?: string | null;
  success: boolean;
  errorMessage?: string | null;
  refundProcessed?: boolean;
  refundAmount?: number | null;
}

export interface CommercePaymentsPaymentResult {
  tenantId?: string;
  success?: boolean;
  transactionId?: string | null;
  paymentId?: string | null;
  amount?: Money;
  processedAt?: string | null;
  failureReason?: string | null;
  paymentMethodId?: string | null;
  status?: CommercePaymentsPaymentStatus;
  invoiceId?: string | null;
}

export interface CommercePaymentsPaymentRetryResult {
  success?: boolean;
  retryAttempt?: number;
  nextRetryAt?: string | null;
  paymentResult?: CommercePaymentsPaymentResult;
  maxRetriesReached?: boolean;
  failureReason?: string | null;
}

export interface CommercePaymentsPaymentsControllerCancelPaymentInput {
  cancellationReason?: string | null;
  canceledBy?: string | null;
  notes?: string | null;
}

export interface CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput {
  tenantId?: string;
  subscriptionId?: string;
  paymentMethodId?: string | null;
}

export interface CommercePaymentsPaymentsControllerCreateSetupIntentInput {
  tenantId?: string;
  subscriptionId?: string;
  customerEmail?: string | null;
  customerName?: string | null;
}

export interface CommercePaymentsPaymentsControllerCreateSetupIntentOutput {
  subscriptionId?: string;
  customerId?: string | null;
  setupIntentId?: string | null;
  clientSecret?: string | null;
}

export interface CommercePaymentsPaymentsControllerProcessPaymentInput {
  tenantId?: string;
  subscriptionId?: string;
  amount?: number;
  paymentMethodId?: string | null;
}

export interface CommercePaymentsPaymentsControllerRefundInput {
  amount?: number | null;
  reason?: string | null;
}

export type CommercePaymentsPaymentStatus =
  | "Pending"
  | "Processing"
  | "Succeeded"
  | "Failed"
  | "Cancelled"
  | "RequiresAction"
  | "Refunded"
  | "Disputed";

export interface CommercePaymentsProcessRefundResult {
  refundId: string;
  paymentId: string;
  refundedAmount: number;
  currency: string | null;
  status: CommercePaymentsTransactionStatus;
  reason: string | null;
  processedAt: string;
  referenceNumber?: string | null;
  estimatedCompletionDate?: string | null;
  processingFee?: number;
  isSuccess?: boolean;
  errorMessage?: string | null;
  isSuccessful?: boolean;
}

export interface CommercePaymentsTaxBreakdown {
  taxType?: CommercePaymentsTaxType;
  description?: string | null;
  rate?: number;
  taxableAmount?: number;
  taxAmount?: number;
  jurisdictionCode?: string | null;
}

export interface CommercePaymentsTaxCalculationResult {
  subtotalAmount?: number;
  taxAmount?: number;
  totalAmount?: number;
  effectiveTaxRate?: number;
  jurisdictionCode?: string | null;
  jurisdictionName?: string | null;
  taxType?: CommercePaymentsTaxType;
  taxDescription?: string | null;
  isTaxExempt?: boolean;
  isReverseCharge?: boolean;
  taxBreakdowns?: Array<CommercePaymentsTaxBreakdown> | null;
  exemptionReason?: string | null;
}

export interface CommercePaymentsTaxExemptionValidationResult {
  isValid?: boolean;
  exemptionType?: string | null;
  exemptionRate?: number;
  validFrom?: string | null;
  validTo?: string | null;
  validationMessage?: string | null;
  warnings?: Array<string> | null;
}

export interface CommercePaymentsTaxJurisdiction {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  code: string;
  name: string;
  type?: CommercePaymentsTaxJurisdictionType;
  parentJurisdictionId?: string | null;
  parentJurisdiction?: CommercePaymentsTaxJurisdiction;
  childJurisdictions?: Array<CommercePaymentsTaxJurisdiction> | null;
  isActive?: boolean;
  taxRegistrationNumber?: string | null;
  isReverseChargeApplicable?: boolean;
  taxRules?: Array<CommercePaymentsTaxRule> | null;
}

export interface CommercePaymentsTaxJurisdictionDto {
  id?: string;
  code?: string | null;
  name?: string | null;
  country?: string | null;
  state?: string | null;
  taxType?: string | null;
  defaultRate?: number;
  isActive?: boolean;
}

export type CommercePaymentsTaxJurisdictionType =
  "Country" | "State" | "Province" | "Region" | "City" | "County" | "District";

export interface CommercePaymentsTaxRate {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  taxJurisdictionId: string;
  taxJurisdiction?: CommercePaymentsTaxJurisdiction;
  taxType?: CommercePaymentsTaxType;
  rate?: number;
  productCategory?: string | null;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  isActive?: boolean;
  minimumTaxableAmount?: number | null;
  maximumTaxableAmount?: number | null;
  description?: string | null;
}

export interface CommercePaymentsTaxRule {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  description?: string | null;
  taxJurisdictionId: string;
  taxJurisdiction?: CommercePaymentsTaxJurisdiction;
  ruleType?: CommercePaymentsTaxRuleType;
  priority?: number;
  isActive?: boolean;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  customerTypeFilter?: CommercePaymentsCustomerType;
  productCategories?: string | null;
  minimumAmount?: number | null;
  maximumAmount?: number | null;
  isTaxInclusive?: boolean;
  isReverseCharge?: boolean;
  exemptionConditions?: string | null;
  defaultTaxRateId?: string | null;
  defaultTaxRate?: CommercePaymentsTaxRate;
}

export interface CommercePaymentsTaxRuleDto {
  id?: string;
  jurisdictionCode?: string | null;
  productCategory?: string | null;
  customerType?: string | null;
  rate?: number;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  description?: string | null;
  isActive?: boolean;
}

export type CommercePaymentsTaxRuleType =
  | "Standard"
  | "Reduced"
  | "ZeroRated"
  | "Exempt"
  | "ReverseCharge"
  | "WithholdingTax"
  | "Compound"
  | "Custom";

export type CommercePaymentsTaxType =
  | "VAT"
  | "GST"
  | "SalesTax"
  | "ServiceTax"
  | "WithholdingTax"
  | "ExciseTax"
  | "CustomsDuty"
  | "Other";

export type CommercePaymentsTransactionStatus =
  "Pending" | "Processing" | "Completed" | "Failed" | "Cancelled" | "Reversed";

export interface CommercePaymentsUserWallet {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  userId: string;
  balance?: number;
  currency: string;
  isActive?: boolean;
  isLocked?: boolean;
  lockReason?: string | null;
  lastTransactionAt?: string | null;
  dailyLimit?: number | null;
  monthlyLimit?: number | null;
  transactions?: Array<CommercePaymentsWalletTransaction> | null;
}

export interface CommercePaymentsValidateTaxExemptionInput {
  jurisdictionCode?: string | null;
  exemptionType?: string | null;
  exemptionCertificateNumber?: string | null;
  customerVatNumber?: string | null;
  customerId?: string | null;
  transactionDate?: string | null;
}

export interface CommercePaymentsWalletTransaction {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  walletId: string;
  wallet?: CommercePaymentsUserWallet;
  type?: CommercePaymentsWalletTransactionType;
  amount?: number;
  balanceAfter?: number;
  description: string;
  referenceId?: string | null;
  status?: CommercePaymentsTransactionStatus;
  metadata?: string | null;
  notes?: string | null;
  processedAt?: string | null;
}

export type CommercePaymentsWalletTransactionType =
  | "Credit"
  | "Debit"
  | "TransferIn"
  | "TransferOut"
  | "Refund"
  | "Fee"
  | "Adjustment";

export interface CommerceProductsAddMySupportTicketMessageInput {
  body?: string | null;
}

export interface CommerceProductsAddSupportTicketMessageInput {
  tenantId?: string;
  authorUserId?: string;
  authorName?: string | null;
  authorEmail?: string | null;
  authorType?: CommerceProductsSupportTicketMessageAuthorType;
  body?: string | null;
  isInternal?: boolean;
}

export interface CommerceProductsAppliedPromoCode {
  code?: string | null;
  discountAmount?: number;
  discountPercentage?: number | null;
}

export interface CommerceProductsApplyPromoCodesInput {
  orderAmount?: number;
  promoCodes?: Array<string> | null;
  productId?: string | null;
}

export interface CommerceProductsAssignSupportTicketInput {
  tenantId?: string;
  agentUserId?: string;
  agentName?: string | null;
}

export interface CommerceProductsBatchCreateProductsInput {
  products?: Array<CommerceProductsBatchProductCreateItem> | null;
  tenantId?: string | null;
}

export interface CommerceProductsBatchProductCreateItem {
  name?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  type?: CommerceProductsProductType;
  isBundle?: boolean;
  creatorId?: string | null;
  bundleItems?: Array<string> | null;
  referralCommissionPercentage?: number;
  maxAffiliateDiscount?: number;
  affiliateCommissionPercentage?: number;
}

export interface CommerceProductsCheckMultipleAccessInput {
  productIds?: Array<string> | null;
}

export interface CommerceProductsCloseSupportTicketInput {
  tenantId?: string;
  agentUserId?: string;
  agentName?: string | null;
  closingNotes?: string | null;
}

export interface CommerceProductsCreateMySupportTicketInput {
  subject?: string | null;
  body?: string | null;
  priority?: CommerceProductsSupportTicketPriority;
  category?: string | null;
}

export interface CommerceProductsCreateProductInput {
  name?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  type?: CommerceProductsProductType;
  isBundle?: boolean;
  creatorId?: string | null;
  bundleItems?: Array<string> | null;
  referralCommissionPercentage?: number;
  maxAffiliateDiscount?: number;
  affiliateCommissionPercentage?: number;
  tenantId?: string | null;
}

export interface CommerceProductsCreatePromoCodeInput {
  code?: string | null;
  name?: string | null;
  description?: string | null;
  type?: CommerceProductsPromoCodeType;
  discountPercentage?: number | null;
  discountAmount?: number | null;
  currency?: string | null;
  minimumOrderAmount?: number | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  validFrom?: string | null;
  validUntil?: string | null;
  isActive?: boolean;
  isExclusive?: boolean;
  stackingPriority?: number;
  productId?: string | null;
}

export interface CommerceProductsCreateSupportTicketInput {
  tenantId?: string;
  customerId?: string;
  customerName?: string | null;
  reporterUserId?: string;
  reporterName?: string | null;
  reporterEmail?: string | null;
  subject?: string | null;
  body?: string | null;
  priority?: CommerceProductsSupportTicketPriority;
  category?: string | null;
}

export interface CommerceProductsEntitlementCheckResult {
  productId?: string;
  hasAccess?: boolean;
}

export interface CommerceProductsEntitlementInfo {
  productId?: string;
  productName?: string | null;
  status?: string | null;
  acquisitionType?: string | null;
  accessStartDate?: string | null;
  accessEndDate?: string | null;
  isSubscription?: boolean;
  subscriptionStatus?: string | null;
  pricePaid?: number;
  currency?: string | null;
}

export interface CommerceProductsGrantEntitlementInput {
  userId?: string;
  productId?: string;
  acquisitionType?: CommerceProductsProductAcquisitionType;
  pricePaid?: number;
  currency?: string | null;
  expiresAt?: string | null;
}

export interface CommerceProductsPatchProductInput {
  name?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  type?: CommerceProductsProductType;
  isBundle?: boolean | null;
  bundleItems?: Array<string> | null;
  referralCommissionPercentage?: number | null;
  maxAffiliateDiscount?: number | null;
  affiliateCommissionPercentage?: number | null;
  expectedVersion?: number | null;
}

export interface CommerceProductsPatchPromoCodeInput {
  name?: string | null;
  description?: string | null;
  type?: CommerceProductsPromoCodeType;
  discountPercentage?: number | null;
  discountAmount?: number | null;
  currency?: string | null;
  minimumOrderAmount?: number | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  validFrom?: string | null;
  validUntil?: string | null;
  isActive?: boolean | null;
  isExclusive?: boolean | null;
  stackingPriority?: number | null;
  productId?: string | null;
}

export interface CommerceProductsProduct {
  id?: string;
  name?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  type?: CommerceProductsProductType;
  isBundle?: boolean;
  isPublished?: boolean;
  creatorId?: string | null;
  bundleItems?: Array<string> | null;
  referralCommissionPercentage?: number;
  maxAffiliateDiscount?: number;
  affiliateCommissionPercentage?: number;
  createdAt?: string;
  updatedAt?: string;
  pricing?: Array<CommerceProductsProductPricing> | null;
}

export type CommerceProductsProductAcquisitionType =
  | "Purchase"
  | "Subscription"
  | "Grant"
  | "PromoCode"
  | "Bundle"
  | "Trial"
  | "Referral"
  | "Free"
  | "Gift";

export interface CommerceProductsProductPricing {
  id?: string;
  productId?: string;
  name?: string | null;
  basePrice?: number;
  salePrice?: number | null;
  currency?: string | null;
  saleStartDate?: string | null;
  saleEndDate?: string | null;
  isDefault?: boolean;
  currentPrice?: number;
  isSaleActive?: boolean;
}

export type CommerceProductsProductType =
  | "Program"
  | "Course"
  | "Bundle"
  | "Subscription"
  | "Workshop"
  | "Mentorship"
  | "Ebook"
  | "ResourcePack"
  | "Community"
  | "Certification"
  | "Physical"
  | "Service"
  | "LearningPathway"
  | "Other";

export interface CommerceProductsPromoCode {
  id?: string;
  code?: string | null;
  name?: string | null;
  description?: string | null;
  type?: CommerceProductsPromoCodeType;
  discountPercentage?: number | null;
  discountAmount?: number | null;
  currency?: string | null;
  minimumOrderAmount?: number | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  validFrom?: string | null;
  validUntil?: string | null;
  isActive?: boolean;
  isExclusive?: boolean;
  stackingPriority?: number;
  productId?: string | null;
  usageCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface CommerceProductsPromoCodeApplicationResult {
  originalAmount?: number;
  finalAmount?: number;
  totalDiscount?: number;
  appliedCodes?: Array<CommerceProductsAppliedPromoCode> | null;
  rejectedCodes?: Array<CommerceProductsRejectedPromoCode> | null;
}

export type CommerceProductsPromoCodeType =
  | "PercentageOff"
  | "FixedAmountOff"
  | "FreeTrial"
  | "BuyOneGetOne"
  | "FreeShipping";

export interface CommerceProductsPromoCodeUsage {
  promoCodeId?: string;
  code?: string | null;
  totalUses?: number;
  uniqueUsers?: number;
  totalDiscountGiven?: number;
  averageDiscountPerUse?: number;
  maxUses?: number | null;
  remainingUses?: number | null;
  firstUsedAt?: string | null;
  lastUsedAt?: string | null;
}

export interface CommerceProductsPromoCodeValidationResult {
  isValid?: boolean;
  code?: string | null;
  errorMessage?: string | null;
  discountAmount?: number;
  discountPercentage?: number | null;
}

export interface CommerceProductsRejectedPromoCode {
  code?: string | null;
  reason?: string | null;
}

export interface CommerceProductsResolveSupportTicketInput {
  tenantId?: string;
  agentUserId?: string;
  agentName?: string | null;
  resolutionSummary?: string | null;
}

export interface CommerceProductsRevokeEntitlementInput {
  userId?: string;
  productId?: string;
  reason?: string | null;
}

export interface CommerceProductsSupportTicket {
  id?: string;
  tenantId?: string | null;
  customerId?: string;
  customerName?: string | null;
  reporterUserId?: string;
  reporterName?: string | null;
  reporterEmail?: string | null;
  subject?: string | null;
  category?: string | null;
  status?: CommerceProductsSupportTicketStatus;
  priority?: CommerceProductsSupportTicketPriority;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
  openedAt?: string;
  firstResponseAt?: string | null;
  responseDueBy?: string | null;
  resolvedAt?: string | null;
  closedAt?: string | null;
  resolutionSummary?: string | null;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  messageCount?: number;
  messages?: Array<CommerceProductsSupportTicketMessage> | null;
}

export interface CommerceProductsSupportTicketMessage {
  id?: string;
  ticketId?: string;
  authorUserId?: string;
  authorName?: string | null;
  authorEmail?: string | null;
  authorType?: CommerceProductsSupportTicketMessageAuthorType;
  body?: string | null;
  isInternal?: boolean;
  createdAt?: string;
}

export type CommerceProductsSupportTicketMessageAuthorType =
  "Customer" | "Agent" | "System";

export type CommerceProductsSupportTicketPriority =
  "Low" | "Normal" | "High" | "Urgent";

export type CommerceProductsSupportTicketStatus =
  "Open" | "InProgress" | "Resolved" | "Closed" | "Cancelled";

export interface CommerceProductsUpdateProductInput {
  name?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  type?: CommerceProductsProductType;
  isBundle?: boolean | null;
  bundleItems?: Array<string> | null;
  referralCommissionPercentage?: number | null;
  maxAffiliateDiscount?: number | null;
  affiliateCommissionPercentage?: number | null;
  expectedVersion?: number | null;
}

export interface CommerceProductsUpdatePromoCodeInput {
  name?: string | null;
  description?: string | null;
  type?: CommerceProductsPromoCodeType;
  discountPercentage?: number | null;
  discountAmount?: number | null;
  currency?: string | null;
  minimumOrderAmount?: number | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  validFrom?: string | null;
  validUntil?: string | null;
  isActive?: boolean | null;
  isExclusive?: boolean | null;
  stackingPriority?: number | null;
  productId?: string | null;
}

export interface CommerceProductsValidatePromoCodeInput {
  code?: string | null;
  orderAmount?: number;
  productId?: string | null;
}

export interface CommerceSubscriptionsBillingHistory {
  id?: string;
  subscriptionId?: string;
  billingDate?: string;
  amount?: number;
  currency?: string | null;
  status?: string | null;
  externalPaymentId?: string | null;
  description?: string | null;
  createdAt?: string;
}

export interface CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput {
  reason?: CommerceSubscriptionsCancellationReason;
  note?: string | null;
  effectiveDate?: string | null;
}

export interface CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput {
  tenantId?: string;
  planId?: string;
  createdByUserId?: string;
  billingCycle?: BillingCycle;
  amount?: number;
  fulfilledOrderId?: string | null;
  startDate?: string | null;
  trialDays?: number | null;
}

export type CommerceSubscriptionsCancellationReason =
  | "UserRequested"
  | "PaymentFailed"
  | "PlanDiscontinued"
  | "PolicyViolation"
  | "Downgrade"
  | "TrialEnded"
  | "Custom"
  | "ExternalRequest";

export interface CommerceSubscriptionsClientModulesOutput {
  clientId?: string;
  subscriptions?: PagedResultOfCommerceSubscriptionsSubscription;
  featureFlags?: Record<string, boolean> | null;
}

export interface CommerceSubscriptionsCreateClientInput {
  name?: string | null;
  slug?: string | null;
  adminEmail?: string | null;
  description?: string | null;
  cnpj?: string | null;
  taxId?: string | null;
  fiscalData?: Record<string, Record<string, unknown> | null> | null;
}

export interface CommerceSubscriptionsSubscription {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  createdByUserId: string;
  fulfilledOrderId?: string | null;
  lastModifyingOrderId?: string | null;
  lastRenewalIdempotencyKey?: string | null;
  lastPaymentIdempotencyKey?: string | null;
  lockedPriceVersionId?: string | null;
  lastProcessedBillingCycle?: number;
  trialEndDate?: string | null;
  cancellationReason?: CommerceSubscriptionsCancellationReason;
  cancellationNote?: string | null;
  cancelledAt?: string | null;
  externalId?: string | null;
  externalCustomerId?: string | null;
  autoRenew?: boolean;
  currentPeriodStart?: string;
  currentPeriodEnd?: string;
  billingCycleCount?: number;
  lastPaymentAt?: string | null;
  metadata?: string | null;
  rowVersion?: string | null;
  plan?: CommerceSubscriptionsSubscriptionPlan;
  status?: CommerceSubscriptionsSubscriptionStatus;
  planId: string;
  billingCycle?: BillingCycle;
  amount?: Money;
  startDate?: string;
  endDate?: string | null;
  nextBillingDate?: string;
  isActive?: boolean;
  isTrialing?: boolean;
  isCancelled?: boolean;
}

export interface CommerceSubscriptionsSubscriptionChurnReport {
  tenantId?: string | null;
  startDate?: string;
  endDate?: string;
  totalSubscriptions?: number;
  activeSubscriptions?: number;
  cancelledInPeriod?: number;
  churnRate?: number;
  retentionRate?: number;
  monthlyRecurringRevenue?: number;
  generatedAt?: string;
  statusBreakdown?: Record<string, number> | null;
}

export interface CommerceSubscriptionsSubscriptionDowngradeResult {
  success?: boolean;
  updatedSubscription?: CommerceSubscriptionsSubscription;
  effectiveDate?: string | null;
  creditIssued?: Money;
  failureReason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput {
  autoRenew?: boolean;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput {
  reason?: string | null;
  note?: string | null;
  effectiveDate?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput {
  newPlanId?: string;
  effectiveDate?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput {
  convertToPaid?: boolean;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput {
  externalSubscriptionId?: string | null;
  externalCustomerId?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput {
  pauseUntil?: string | null;
  reason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput {
  trialDays?: number;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput {
  reason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput {
  newPlanId?: string;
  effectiveDate?: string | null;
}

export interface CommerceSubscriptionsSubscriptionNotification {
  id?: string;
  recipientId?: string;
  tenantId?: string | null;
  subscriptionId?: string | null;
  channel?: string | null;
  title?: string | null;
  message?: string | null;
  isSent?: boolean;
  sentAt?: string | null;
  createdAt?: string;
}

export interface CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput {
  channel?: NotificationsNotificationChannel;
}

export interface CommerceSubscriptionsSubscriptionPlan {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  externalId?: string | null;
  isFeatured?: boolean;
  sortOrder?: number;
  hasPrioritySupport?: boolean;
  hasAdvancedAnalytics?: boolean;
  hasCustomBranding?: boolean;
  features?: string | null;
  metadata?: string | null;
  trialPeriodDays?: number;
  subscriptions?: Array<CommerceSubscriptionsSubscription> | null;
  name: string;
  slug: string;
  description?: string | null;
  monthlyPriceInCents?: number;
  annualPriceInCents?: number | null;
  currency: string;
  isActive?: boolean;
  maxUsers?: number | null;
  maxStorageMb?: number | null;
  maxApiCallsPerMonth?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput {
  newName?: string | null;
  newSlug?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput {
  externalId?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput {
  featured?: boolean;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput {
  planId?: string;
  name?: string | null;
  description?: string | null;
  sortOrder?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput {
  hasPrioritySupport?: boolean | null;
  hasAdvancedAnalytics?: boolean | null;
  hasCustomBranding?: boolean | null;
  features?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput {
  maxUsers?: number | null;
  maxStorageMb?: number | null;
  maxApiCallsPerMonth?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput {
  monthlyPriceInCents?: number;
  annualPriceInCents?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput {
  users?: number;
  storageMb?: number;
  apiCalls?: number;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput {
  basePlanId?: string;
  comparePlanIds?: Array<string> | null;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput {
  name?: string | null;
  slug?: string | null;
  monthlyPriceInCents?: number;
  currency?: string | null;
  description?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput {
  name?: string | null;
  slug?: string | null;
  description?: string | null;
  monthlyPriceInCents?: number;
  annualPriceInCents?: number | null;
  maxUsers?: number | null;
  maxStorageMb?: number | null;
  maxApiCallsPerMonth?: number | null;
  hasPrioritySupport?: boolean | null;
  hasAdvancedAnalytics?: boolean | null;
  hasCustomBranding?: boolean | null;
  features?: string | null;
  sortOrder?: number | null;
}

export interface CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput {
  tenantId?: string;
  planId?: string;
  createdByUserId?: string;
  billingCycle?: BillingCycle;
  amount?: number;
  currency?: string | null;
  fulfilledOrderId?: string | null;
  startDate?: string | null;
  trialDays?: number | null;
}

export interface CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput {
  billingCycle?: BillingCycle;
  autoRenew?: boolean | null;
  externalSubscriptionId?: string | null;
  externalCustomerId?: string | null;
  metadata?: string | null;
}

export interface CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput {
  planId?: string;
  billingCycle?: BillingCycle;
  amount?: number;
  autoRenew?: boolean;
  externalSubscriptionId?: string | null;
  externalCustomerId?: string | null;
}

export type CommerceSubscriptionsSubscriptionStatus =
  | "PendingActivation"
  | "Active"
  | "Trialing"
  | "PastDue"
  | "Suspended"
  | "Cancelled"
  | "Expired";

export interface CommerceSubscriptionsSubscriptionUpgradeResult {
  success?: boolean;
  updatedSubscription?: CommerceSubscriptionsSubscription;
  proratedAmount?: Money;
  creditApplied?: Money;
  failureReason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionUsage {
  subscriptionId?: string;
  usersCount?: number;
  maxUsers?: number | null;
  storageUsedMb?: number;
  maxStorageMb?: number | null;
  apiCallsThisMonth?: number;
  maxApiCallsPerMonth?: number | null;
  isOverLimit?: boolean;
  limitWarnings?: Array<string> | null;
}

export type ComplianceAuditAuditCategory =
  | "General"
  | "Authentication"
  | "Authorization"
  | "Permission"
  | "User"
  | "Admin"
  | "Security"
  | "Data"
  | "System"
  | "Tenant"
  | "Privacy";

export interface ComplianceAuditAuditExportInput {
  userId?: string | null;
  tenantId?: string | null;
  actionType?: string | null;
  resourceType?: string | null;
  category?: ComplianceAuditAuditCategory;
  riskLevel?: ComplianceAuditAuditRiskLevel;
  success?: boolean | null;
  startDate?: string | null;
  endDate?: string | null;
  ipAddress?: string | null;
}

export interface ComplianceAuditAuditLog {
  id?: string;
  actionType?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  userId?: string | null;
  tenantId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  sessionId?: string | null;
  description?: string | null;
  success?: boolean;
  errorMessage?: string | null;
  riskLevel?: ComplianceAuditAuditRiskLevel;
  category?: ComplianceAuditAuditCategory;
  correlationId?: string | null;
  createdAt?: string;
}

export interface ComplianceAuditAuditLogOutput {
  logs?: Array<ComplianceAuditAuditLog> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
}

export type ComplianceAuditAuditRiskLevel =
  "Low" | "Medium" | "High" | "Critical";

export interface ComplianceAuditAuditStatisticsOutput {
  startDate?: string;
  endDate?: string;
  totalEvents?: number;
  authenticationEvents?: number;
  permissionEvents?: number;
  securityEvents?: number;
  failedEvents?: number;
  highRiskEvents?: number;
}

export interface ComplianceAuditAuthenticationAuditEntry {
  id?: string;
  email?: string | null;
  userId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  isSuccessful?: boolean;
  failureReason?: string | null;
  attemptedAt?: string;
  processingTime?: string;
  geoLocation?: string | null;
  isSuspicious?: boolean;
}

export interface ComplianceAuditAuthenticationAuditOutput {
  entries?: Array<ComplianceAuditAuthenticationAuditEntry> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  successfulLogins?: number;
  failedLogins?: number;
  uniqueIpAddresses?: number;
}

export interface ComplianceAuditDailyActivityTrend {
  date?: string;
  totalEvents?: number;
  authenticationEvents?: number;
  permissionEvents?: number;
  securityViolations?: number;
}

export interface ComplianceAuditFailureReasonCount {
  reason?: string | null;
  count?: number;
}

export interface ComplianceAuditPermissionAuditEntry {
  id?: string;
  tenantId?: string | null;
  operationType?: string | null;
  userId?: string | null;
  resourceId?: string | null;
  resourceType?: string | null;
  permissionType?: string | null;
  oldValue?: string | null;
  newValue?: string | null;
  performedBy?: string;
  ipAddress?: string | null;
  reason?: string | null;
  success?: boolean;
  errorMessage?: string | null;
  timestamp?: string;
}

export interface ComplianceAuditPermissionAuditOutput {
  entries?: Array<ComplianceAuditPermissionAuditEntry> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  grantOperations?: number;
  revokeOperations?: number;
  denyOperations?: number;
}

export interface ComplianceAuditSecurityAuditDashboard {
  startDate?: string;
  endDate?: string;
  tenantId?: string | null;
  totalAuthenticationAttempts?: number;
  successfulLogins?: number;
  failedLogins?: number;
  loginSuccessRate?: number;
  uniqueUsersAuthenticated?: number;
  suspiciousLoginAttempts?: number;
  totalPermissionChanges?: number;
  permissionsGranted?: number;
  permissionsRevoked?: number;
  permissionDenials?: number;
  totalSecurityViolations?: number;
  highRiskEvents?: number;
  crossTenantAttempts?: number;
  topActiveUsers?: Array<ComplianceAuditTopUserActivity> | null;
  topIpAddresses?: Array<ComplianceAuditTopIpActivity> | null;
  topFailureReasons?: Array<ComplianceAuditFailureReasonCount> | null;
  dailyTrends?: Array<ComplianceAuditDailyActivityTrend> | null;
}

export type ComplianceAuditSecurityAuditSourceType =
  "Authentication" | "Permission" | "General" | "All";

export interface ComplianceAuditTopIpActivity {
  ipAddress?: string | null;
  eventCount?: number;
  failedAttempts?: number;
  uniqueUsers?: number;
}

export interface ComplianceAuditTopUserActivity {
  userId?: string;
  email?: string | null;
  eventCount?: number;
  failedAttempts?: number;
}

export interface ComplianceAuditUnifiedSecurityAuditEntry {
  id?: string;
  sourceType?: ComplianceAuditSecurityAuditSourceType;
  sourceEntity?: string | null;
  actionType?: string | null;
  userId?: string | null;
  userEmail?: string | null;
  tenantId?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  description?: string | null;
  success?: boolean;
  errorMessage?: string | null;
  riskLevel?: ComplianceAuditAuditRiskLevel;
  timestamp?: string;
  metadata?: string | null;
}

export interface ComplianceAuditUnifiedSecurityAuditInput {
  sourceType?: ComplianceAuditSecurityAuditSourceType;
  userId?: string | null;
  tenantId?: string | null;
  actionType?: string | null;
  success?: boolean | null;
  riskLevel?: ComplianceAuditAuditRiskLevel;
  startDate?: string | null;
  endDate?: string | null;
  ipAddress?: string | null;
  searchText?: string | null;
  skip?: number;
  take?: number;
  sortBy?: string | null;
  sortDirection?: string | null;
}

export interface ComplianceAuditUnifiedSecurityAuditOutput {
  entries?: Array<ComplianceAuditUnifiedSecurityAuditEntry> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  sourceBreakdown?: {
    Authentication?: number;
    Permission?: number;
    General?: number;
    All?: number;
  } | null;
}

export interface ComplianceConsentConsentPolicy {
  id?: string;
  name?: string | null;
  policyType?: ComplianceConsentPolicyType;
  isMandatory?: boolean;
  isActive?: boolean;
  currentVersion?: string | null;
}

export type ComplianceConsentContentType =
  "PlainText" | "Html" | "Markdown" | "Url";

export interface ComplianceConsentCreateConsentPolicyCommand {
  name?: string | null;
  policyType?: ComplianceConsentPolicyType;
  isMandatory?: boolean;
  description?: string | null;
}

export interface ComplianceConsentDataSubjectInput {
  id?: string;
  userId?: string;
  requestType?: ComplianceConsentDataSubjectRequestType;
  status?: ComplianceConsentDataSubjectRequestStatus;
  deadline?: string;
  processedAt?: string | null;
  processingNotes?: string | null;
}

export type ComplianceConsentDataSubjectRequestStatus =
  "Pending" | "InProgress" | "Completed" | "Rejected" | "Expired";

export type ComplianceConsentDataSubjectRequestType =
  | "Access"
  | "Erasure"
  | "Portability"
  | "Rectification"
  | "Restriction"
  | "Objection";

export interface ComplianceConsentGrantConsentCommand {
  userId?: string;
  policyVersionId?: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  consentMethod?: string | null;
}

export type ComplianceConsentPolicyType =
  | "PrivacyPolicy"
  | "TermsOfService"
  | "CookiePolicy"
  | "DataProcessingAgreement"
  | "MarketingConsent"
  | "ThirdPartySharing"
  | "Custom";

export interface ComplianceConsentPolicyVersion {
  id?: string;
  policyId?: string;
  versionNumber?: string | null;
  contentType?: ComplianceConsentContentType;
  effectiveFrom?: string;
  isCurrent?: boolean;
}

export interface ComplianceConsentProcessRequestBody {
  processedByUserId?: string;
  notes?: string | null;
}

export interface ComplianceConsentPublishVersionInput {
  versionNumber?: string | null;
  content?: string | null;
  contentType?: ComplianceConsentContentType;
}

export interface ComplianceConsentRevokeConsentCommand {
  userId?: string;
  policyVersionId?: string;
}

export interface ComplianceConsentSubmitDataSubjectRequestCommand {
  userId?: string;
  requestType?: ComplianceConsentDataSubjectRequestType;
  description?: string | null;
}

export interface ComplianceConsentUserConsent {
  id?: string;
  userId?: string;
  policyVersionId?: string;
  isGranted?: boolean;
  consentGivenAt?: string;
  consentRevokedAt?: string | null;
  consentMethod?: string | null;
}

export interface ComplianceFERPACompleteFerpaInspectionRequestBody {
  processedByUserId?: string;
  approved?: boolean;
  notes?: string | null;
}

export type ComplianceFERPAEducationRecordKind =
  | "CourseEnrollment"
  | "AssessmentSubmission"
  | "Grade"
  | "Certificate"
  | "Attendance"
  | "Communication"
  | "SupportCase"
  | "Custom";

export interface ComplianceFERPAFerpaDirectoryInformationPolicy {
  id?: string;
  tenantId?: string | null;
  allowedFieldsJson?: string | null;
  optOutEnabled?: boolean;
  annualNoticeSentAt?: string | null;
  noticeUrl?: string | null;
}

export type ComplianceFERPAFerpaDisclosureBasis =
  | "StudentConsent"
  | "GuardianConsent"
  | "SchoolOfficial"
  | "FinancialAid"
  | "HealthOrSafetyEmergency"
  | "AuditOrEvaluation"
  | "CourtOrder"
  | "DirectoryInformation"
  | "Other";

export interface ComplianceFERPAFerpaDisclosureConsent {
  id?: string;
  studentUserId?: string;
  guardianUserId?: string | null;
  recipient?: string | null;
  purpose?: string | null;
  scope?: string | null;
  effectiveFrom?: string;
  expiresAt?: string | null;
  revokedAt?: string | null;
  isActive?: boolean;
}

export interface ComplianceFERPAFerpaDisclosureLog {
  id?: string;
  studentUserId?: string;
  disclosedByUserId?: string;
  recipient?: string | null;
  basis?: ComplianceFERPAFerpaDisclosureBasis;
  purpose?: string | null;
  recordIdsJson?: string | null;
  disclosedAt?: string;
}

export interface ComplianceFERPAFerpaEducationRecord {
  id?: string;
  studentUserId?: string;
  recordKind?: ComplianceFERPAEducationRecordKind;
  externalRecordId?: string | null;
  title?: string | null;
  protectionLevel?: ComplianceFERPAFerpaRecordProtectionLevel;
  isDirectoryInformation?: boolean;
  retentionUntil?: string | null;
  metadataJson?: string | null;
  createdAt?: string;
}

export interface ComplianceFERPAFerpaInspectionInput {
  id?: string;
  studentUserId?: string;
  requestedByUserId?: string;
  status?: ComplianceFERPAFerpaRequestStatus;
  deadline?: string;
  processedByUserId?: string | null;
  processedAt?: string | null;
  processingNotes?: string | null;
}

export type ComplianceFERPAFerpaRecordProtectionLevel =
  | "DirectoryInformation"
  | "EducationRecord"
  | "SensitiveEducationRecord"
  | "Restricted";

export type ComplianceFERPAFerpaRequestStatus =
  "Pending" | "InReview" | "Completed" | "Denied" | "Expired";

export interface ComplianceFERPAGrantFerpaDisclosureConsentCommand {
  studentUserId?: string;
  recipient?: string | null;
  purpose?: string | null;
  scope?: string | null;
  effectiveFrom?: string;
  guardianUserId?: string | null;
  expiresAt?: string | null;
}

export interface ComplianceFERPARecordFerpaDisclosureCommand {
  studentUserId?: string;
  disclosedByUserId?: string;
  recipient?: string | null;
  basis?: ComplianceFERPAFerpaDisclosureBasis;
  purpose?: string | null;
  scope?: string | null;
  recordIdsJson?: string | null;
  disclosedAt?: string;
}

export interface ComplianceFERPARegisterEducationRecordCommand {
  studentUserId?: string;
  recordKind?: ComplianceFERPAEducationRecordKind;
  externalRecordId?: string | null;
  title?: string | null;
  protectionLevel?: ComplianceFERPAFerpaRecordProtectionLevel;
  isDirectoryInformation?: boolean;
  tenantId?: string | null;
  retentionUntil?: string | null;
  metadataJson?: string | null;
}

export interface ComplianceFERPASubmitFerpaInspectionRequestCommand {
  studentUserId?: string;
  requestedByUserId?: string;
  deadline?: string;
  description?: string | null;
}

export interface ComplianceFERPAUpsertDirectoryInformationPolicyCommand {
  tenantId?: string | null;
  allowedFieldsJson?: string | null;
  optOutEnabled?: boolean;
  annualNoticeSentAt?: string | null;
  noticeUrl?: string | null;
}

export interface ContentPagesContentResource {
  id?: string;
  slug?: string | null;
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  resourceType?: string | null;
  status?: string | null;
  locale?: string | null;
  categorySlug?: string | null;
  tags?: string | null;
  authorId?: string | null;
  authorName?: string | null;
  coverImageUrl?: string | null;
  videoUrl?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  ogImageUrl?: string | null;
  structuredData?: string | null;
  readingTimeMinutes?: number | null;
  viewCount?: number;
  isFeatured?: boolean;
  sortOrder?: number;
  publishedAt?: string | null;
  scheduledPublishAt?: string | null;
  customData?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export type ContentPagesContentResourceStatus =
  "Draft" | "InReview" | "Published" | "Archived";

export type ContentPagesContentResourceType =
  | "Article"
  | "Tutorial"
  | "Documentation"
  | "Video"
  | "Download"
  | "ExternalLink"
  | "Course"
  | "Custom";

export interface ContentPagesCreateContentResource {
  slug?: string | null;
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  resourceType?: ContentPagesContentResourceType;
  locale?: string | null;
  categorySlug?: string | null;
  tags?: string | null;
  coverImageUrl?: string | null;
  videoUrl?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  ogImageUrl?: string | null;
  structuredData?: string | null;
  readingTimeMinutes?: number | null;
  isFeatured?: boolean;
  sortOrder?: number;
  customData?: string | null;
}

export interface ContentPagesCreateMarketingLead {
  source: string;
  name?: string | null;
  email: string;
  company?: string | null;
  topic?: string | null;
  plan?: string | null;
  message?: string | null;
  locale?: string | null;
  pagePath?: string | null;
  referrer?: string | null;
  userAgent?: string | null;
}

export interface ContentPagesCreatePage {
  slug?: string | null;
  title?: string | null;
  description?: string | null;
  pageType?: ContentPagesPageType;
  locale?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  canonicalUrl?: string | null;
  robotsDirective?: string | null;
  ogTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogType?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
  structuredData?: string | null;
  body?: string | null;
  customData?: string | null;
  parentPageId?: string | null;
  sortOrder?: number;
}

export interface ContentPagesCreatePageSection {
  sectionType?: ContentPagesSectionType;
  heading?: string | null;
  subheading?: string | null;
  data?: string | null;
  sortOrder?: number;
  isVisible?: boolean;
  cssClasses?: string | null;
}

export interface ContentPagesMarketingLead {
  id?: string;
  source?: string | null;
  status?: string | null;
  name?: string | null;
  email?: string | null;
  company?: string | null;
  topic?: string | null;
  plan?: string | null;
  message?: string | null;
  locale?: string | null;
  pagePath?: string | null;
  referrer?: string | null;
  userAgent?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface ContentPagesOpenGraphMetadata {
  slug?: string | null;
  title?: string | null;
  description?: string | null;
  ogTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogType?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
  canonicalUrl?: string | null;
  robotsDirective?: string | null;
  structuredData?: string | null;
}

export interface ContentPagesPage {
  id?: string;
  slug?: string | null;
  title?: string | null;
  description?: string | null;
  pageType?: string | null;
  status?: string | null;
  locale?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  canonicalUrl?: string | null;
  robotsDirective?: string | null;
  ogTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogType?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
  structuredData?: string | null;
  body?: string | null;
  customData?: string | null;
  parentPageId?: string | null;
  sortOrder?: number;
  sections?: Array<ContentPagesPageSection> | null;
  publishedAt?: string | null;
  scheduledPublishAt?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface ContentPagesPageSection {
  id?: string;
  pageId?: string;
  sectionType?: string | null;
  heading?: string | null;
  subheading?: string | null;
  data?: string | null;
  sortOrder?: number;
  isVisible?: boolean;
  cssClasses?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export type ContentPagesPageStatus = "Draft" | "Published" | "Archived";

export type ContentPagesPageType =
  "Landing" | "Legal" | "ResourceIndex" | "Resource" | "Custom";

export type ContentPagesSectionType =
  | "Hero"
  | "Features"
  | "Testimonials"
  | "Pricing"
  | "CallToAction"
  | "Faq"
  | "RichText"
  | "Gallery"
  | "Stats"
  | "Team"
  | "LogoCloud"
  | "Newsletter"
  | "Contact"
  | "ResourceCards"
  | "Custom";

export interface ContentPagesSitemapEntry {
  slug?: string | null;
  updatedAt?: string | null;
  locale?: string | null;
}

export interface ContentPagesUpdateContentResource {
  slug?: string | null;
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  resourceType?: ContentPagesContentResourceType;
  status?: ContentPagesContentResourceStatus;
  locale?: string | null;
  categorySlug?: string | null;
  tags?: string | null;
  coverImageUrl?: string | null;
  videoUrl?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  ogImageUrl?: string | null;
  structuredData?: string | null;
  readingTimeMinutes?: number | null;
  isFeatured?: boolean | null;
  sortOrder?: number | null;
  scheduledPublishAt?: string | null;
  customData?: string | null;
}

export interface ContentPagesUpdatePage {
  slug?: string | null;
  title?: string | null;
  description?: string | null;
  pageType?: ContentPagesPageType;
  status?: ContentPagesPageStatus;
  locale?: string | null;
  metaTitle?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  canonicalUrl?: string | null;
  robotsDirective?: string | null;
  ogTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogType?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
  structuredData?: string | null;
  body?: string | null;
  customData?: string | null;
  parentPageId?: string | null;
  sortOrder?: number | null;
  scheduledPublishAt?: string | null;
}

export interface ContentPagesUpdatePageSection {
  sectionType?: ContentPagesSectionType;
  heading?: string | null;
  subheading?: string | null;
  data?: string | null;
  sortOrder?: number | null;
  isVisible?: boolean | null;
  cssClasses?: string | null;
}

export type ContentStatus =
  "Draft" | "Review" | "Published" | "Archived" | "Deleted";

export type ContentVisibility =
  "Private" | "Internal" | "Friends" | "Protected" | "Public";

export interface CQRSIDomainEvent {
  eventId?: string;
  occurredAt?: string;
  version?: number;
}

export interface CQRSModelsTenantId {
  value?: string;
}

export interface EconomyCommandsConvertMyHardToSoftInput {
  principalHardCoinUnits?: number;
  feeHardCoinUnits?: number;
  idempotencyKey?: string | null;
}

export type EconomyContractsCurrencyCode = "HardCoin" | "SoftCoin";

export interface EconomyContractsEconomyWalletSummary {
  walletId?: string;
  state?: EconomyContractsWalletLifecycleState;
  createdAt?: string;
  pendingHard?: number;
  pendingSoft?: number;
  purchasedHard?: number;
  earnedHard?: number;
  restrictedHard?: number;
  soft?: number;
  heldHard?: number;
  heldSoft?: number;
  availableHardToSpend?: number;
  availableSoftToSpend?: number;
  withdrawableHard?: number;
  outstandingHardDebt?: number;
  projectionRebuiltAt?: string;
  sourceJournalSequence?: number;
}

export interface EconomyContractsEconomyWalletTransaction {
  postingGroupId?: string;
  journalEntryId?: string;
  journalSequence?: number;
  templateKind?: EconomyContractsPostingTemplateKind;
  status?: EconomyContractsPostingStatus;
  recordedAt?: string;
  side?: EconomyContractsEntrySide;
  currency?: EconomyContractsCurrencyCode;
  amountUnits?: number;
  provenance?: EconomyContractsProvenanceKind;
}

export type EconomyContractsEntrySide = "Debit" | "Credit";

export type EconomyContractsPostingStatus =
  "Accepted" | "Rejected" | "Duplicate";

export type EconomyContractsPostingTemplateKind =
  | "ConfirmedTopUpMint"
  | "ProviderReversalFull"
  | "ProviderReversalPartial"
  | "Spend"
  | "HardToSoftConversion"
  | "SystemBackedGrant"
  | "Burn"
  | "Escrow"
  | "Reclaim"
  | "Refund"
  | "PayoutReservation"
  | "PayoutSuccess"
  | "PayoutFailure"
  | "AdminWithdrawalReservation"
  | "AdminWithdrawalSuccess"
  | "AdminWithdrawalFailure"
  | "HardToSoftConversionFee"
  | "ProviderConvertedSoftReversal"
  | "ProviderReversalDebt"
  | "ProviderReversalLoss"
  | "AdRewardIssuance"
  | "BountyEscrow"
  | "BountyClaim"
  | "BountyReclaim";

export type EconomyContractsProvenanceKind =
  | "PurchasedHard"
  | "EarnedHard"
  | "ConvertedSoft"
  | "AdRewardSoft"
  | "SystemGrantSoft"
  | "RefundRestoration"
  | "EscrowReturn";

export type EconomyContractsWalletLifecycleState =
  "Active" | "Frozen" | "Closed" | "UnderReview";

export interface EconomyFundingSelfServiceHardToSoftConversionReceipt {
  principalPostingId?: string;
  feePostingId?: string | null;
  journalSequence?: number;
  journalHash?: string | null;
  isDuplicate?: boolean;
}

export interface EconomyPayoutsCommandsCreateMyPayoutRequestInput {
  hardCoinUnits?: number;
  idempotencyKey?: string | null;
}

export type EconomyPayoutsPayoutOperationState =
  | "Reserved"
  | "Dispatching"
  | "Ambiguous"
  | "Succeeded"
  | "Failed"
  | "Cancelled";

export type EconomyPayoutsPayoutRequestState =
  "Submitted" | "Cancelled" | "Approved" | "Rejected";

export interface EconomyPayoutsQueriesEconomyPayoutInput {
  id?: string;
  hardCoinUnits?: number;
  state?: EconomyPayoutsPayoutRequestState;
  createdAt?: string;
  updatedAt?: string;
}

export interface EconomyPayoutsQueriesEconomyPayoutOperation {
  id?: string;
  hardCoinUnits?: number;
  state?: EconomyPayoutsPayoutOperationState;
  createdAt?: string;
  updatedAt?: string;
}

export type EconomyRiskEconomyValueMovementCapability =
  | "ConfirmHardCoinFunding"
  | "ConvertHardToSoft"
  | "ReverseProviderFunding"
  | "Transfer"
  | "IssueAdReward"
  | "BountyEscrow"
  | "BountyClaim"
  | "MarketplaceSettlement"
  | "PayoutExecution"
  | "AdminWithdrawalExecution";

export interface Error {
  code?: string | null;
  description?: string | null;
  type?: ErrorType;
}

export type ErrorType =
  | "Failure"
  | "Validation"
  | "Problem"
  | "NotFound"
  | "Conflict"
  | "Unauthorized"
  | "Forbidden"
  | "None";

export interface FeaturesBulkEvaluationInput {
  featureKeys?: Array<string> | null;
  context?: FeaturesFeatureContext;
}

export interface FeaturesCapabilityAuditLog {
  id?: string;
  tenantId?: string;
  capabilityKey?: string | null;
  oldValue?: boolean | null;
  newValue?: boolean;
  oldSource?: string | null;
  newSource?: string | null;
  changedByUserId?: string | null;
  changeReason?: string | null;
  changeType?: string | null;
  changedAt?: string;
}

export interface FeaturesCapabilityCheckOutput {
  capability?: string | null;
  isEnabled?: boolean;
}

export interface FeaturesCreateFeatureInput {
  key?: string | null;
  name?: string | null;
  description?: string | null;
  isEnabled?: boolean;
  tenantId?: string | null;
}

export interface FeaturesFeatureContext {
  tenantId?: string | null;
  userId?: string | null;
  subscriptionPlanId?: string | null;
  environment?: string | null;
  permissions?: Array<string> | null;
  customAttributes?: Record<string, Record<string, unknown>> | null;
  userAgent?: string | null;
  ipAddress?: string | null;
  country?: string | null;
  requestTime?: string;
}

export interface FeaturesFeatureEvaluationInput {
  featureKey?: string | null;
  defaultValue?: Record<string, unknown> | null;
  context?: FeaturesFeatureContext;
}

export interface FeaturesFeatureFlag {
  id: string;
  key: string | null;
  name: string | null;
  description?: string | null;
  isEnabled: boolean;
  type: FeaturesFeatureFlagType;
  environment?: string | null;
  tenantId?: string | null;
  defaultValue?: Record<string, unknown> | null;
  createdAt: string;
  updatedAt?: string | null;
  deletedAt?: string | null;
  targets?: Array<FeaturesFeatureFlagTarget> | null;
}

export interface FeaturesFeatureFlagTarget {
  id: string;
  featureFlagId: string;
  targetType: string | null;
  targetIdentifier: string | null;
  isEnabled: boolean;
  rolloutPercentage?: number;
  customValue?: string | null;
  metadata?: string | null;
  priority?: number;
  createdAt: string;
  updatedAt?: string | null;
  deletedAt?: string | null;
}

export type FeaturesFeatureFlagType =
  "Toggle" | "Numeric" | "String" | "Percentage" | "UserSegment";

export interface FeaturesSetCapabilityOverrideInput {
  capability?: string | null;
  isEnabled?: boolean;
  source?: string | null;
  reason?: string | null;
  expiresAt?: string | null;
}

export interface FeaturesToggleFeatureInput {
  featureKey?: string | null;
  isEnabled?: boolean;
  reason?: string | null;
  tenantId?: string | null;
  environment?: string | null;
}

export interface FeaturesUpdateFeatureInput {
  name?: string | null;
  description?: string | null;
  isEnabled?: boolean | null;
  rolloutPercentage?: number | null;
  enabledValue?: string | null;
  defaultValue?: string | null;
}

export interface Fido2NetLibAssertionOptions {
  challenge?: string | null;
  timeout?: number;
  rpId?: string | null;
  allowCredentials?: Array<ObjectsPublicKeyCredentialDescriptor> | null;
  userVerification?: ObjectsUserVerificationRequirement;
  hints?: Array<ObjectsPublicKeyCredentialHint> | null;
  extensions?: ObjectsAuthenticationExtensionsClientInputs;
}

export interface Fido2NetLibAuthenticatorSelection {
  authenticatorAttachment?: ObjectsAuthenticatorAttachment;
  residentKey?: ObjectsResidentKeyRequirement;
  requireResidentKey?: boolean;
  userVerification?: ObjectsUserVerificationRequirement;
}

export interface Fido2NetLibCredentialCreateOptions {
  rp: Fido2NetLibPublicKeyCredentialRpEntity;
  user: Fido2NetLibFido2User;
  challenge: string | null;
  pubKeyCredParams: Array<Fido2NetLibPubKeyCredParam> | null;
  timeout?: number;
  attestation?: ObjectsAttestationConveyancePreference;
  attestationFormats?: Array<ObjectsAttestationStatementFormatIdentifier> | null;
  authenticatorSelection?: Fido2NetLibAuthenticatorSelection;
  hints?: Array<ObjectsPublicKeyCredentialHint> | null;
  excludeCredentials?: Array<ObjectsPublicKeyCredentialDescriptor> | null;
  extensions?: ObjectsAuthenticationExtensionsClientInputs;
}

export interface Fido2NetLibFido2User {
  name?: string | null;
  id?: string | null;
  displayName?: string | null;
}

export interface Fido2NetLibPubKeyCredParam {
  type?: ObjectsPublicKeyCredentialType;
  alg?: ObjectsCOSEAlgorithm;
}

export interface Fido2NetLibPublicKeyCredentialRpEntity {
  id?: string | null;
  name?: string | null;
  icon?: string | null;
}

export interface GameJamsAddJamCriteriaInput {
  name?: string | null;
  description?: string | null;
  weight?: number;
  maxScore?: number;
}

export interface GameJamsCreateJamInput {
  name?: string | null;
  slug?: string | null;
  startDate?: string;
  endDate?: string;
  createdBy?: string;
  theme?: string | null;
  description?: string | null;
  rules?: string | null;
  submissionCriteria?: string | null;
  votingEndDate?: string | null;
  maxParticipants?: number | null;
}

export interface GameJamsJam {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  slug: string;
  theme?: string | null;
  description?: string | null;
  rules?: string | null;
  submissionCriteria?: string | null;
  startDate: string;
  endDate: string;
  votingEndDate?: string | null;
  maxParticipants?: number | null;
  participantCount?: number;
  status: GameJamsJamStatus;
  createdBy: string;
}

export interface GameJamsJamCriteria {
  id?: string;
  jamId?: string;
  name?: string | null;
  description?: string | null;
  weight?: number;
  maxScore?: number;
}

export interface GameJamsJamDto {
  id?: string;
  name?: string | null;
  slug?: string | null;
  theme?: string | null;
  description?: string | null;
  startDate?: string;
  endDate?: string;
  votingEndDate?: string | null;
  maxParticipants?: number | null;
  participantCount?: number;
  status?: GameJamsJamStatus;
  createdBy?: string;
}

export interface GameJamsJamScore {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  submissionId: string;
  criteriaId: string;
  judgeUserId: string;
  score: number;
  feedback?: string | null;
}

export interface GameJamsJamScoreDto {
  id?: string;
  submissionId?: string;
  criteriaId?: string;
  judgeUserId?: string;
  score?: number;
  feedback?: string | null;
}

export type GameJamsJamStatus =
  "Upcoming" | "Active" | "Voting" | "Completed" | "Cancelled";

export interface GameJamsJamSubmission {
  id?: string;
  jamId?: string;
  projectVersionId?: string;
  userId?: string;
  submissionNotes?: string | null;
}

export interface GameJamsScoreJamSubmissionInput {
  criteriaId?: string;
  judgeUserId?: string;
  score?: number;
  feedback?: string | null;
}

export interface GameJamsSubmitJamEntryInput {
  projectVersionId?: string;
  userId?: string;
  notes?: string | null;
}

export interface IdentityAuthenticationApiKey {
  id?: string;
  name?: string | null;
  keyPrefix?: string | null;
  scopes?: Array<string> | null;
  isActive?: boolean;
  expiresAt?: string | null;
  lastUsedAt?: string | null;
  usageCount?: number;
  createdAt?: string;
}

export interface IdentityAuthenticationAssignRoleToUserInput {
  userId?: string;
  roleId?: string;
  expiresAt?: string | null;
}

export interface IdentityAuthenticationBackupCodesOutput {
  codes?: Array<string> | null;
  generatedAt?: string;
}

export interface IdentityAuthenticationBackupCodesStatusOutput {
  totalCount: number;
  remainingCount: number;
  usedCount: number;
  hasBackupCodes: boolean;
}

export interface IdentityAuthenticationBeginWebAuthnAuthenticationInput {
  email?: string | null;
}

export interface IdentityAuthenticationBeginWebAuthnRegistrationInput {
  email?: string | null;
  displayName?: string | null;
  preferredAuthenticatorType?: IdentityAuthenticationWebAuthnAuthenticatorType;
}

export interface IdentityAuthenticationCleanupKeysInput {
  retentionDays?: number | null;
}

export interface IdentityAuthenticationCleanupResult {
  deletedCount?: number;
}

export interface IdentityAuthenticationClientCredentialsTokenOutput {
  accessToken?: string | null;
  tokenType?: string | null;
  expiresIn?: number;
  scope?: string | null;
}

export interface IdentityAuthenticationCompleteMfaSetupInput {
  code: string;
  secretKey: string;
}

export interface IdentityAuthenticationCompletePasswordResetInput {
  token: string;
  newPassword: string;
  confirmPassword: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationCompleteWebAuthnAuthenticationInput {
  assertionResponse?: string | null;
}

export interface IdentityAuthenticationCompleteWebAuthnRegistrationInput {
  attestationResponse?: string | null;
  friendlyName?: string | null;
  isPasswordless?: boolean;
}

export interface IdentityAuthenticationConsumeMagicLinkInput {
  token: string;
  tenantId?: string | null;
  deviceFingerprint?: string | null;
}

export interface IdentityAuthenticationCreateApiKeyCommand {
  name: string | null;
  scopes: Array<string> | null;
  expiresAt?: string | null;
  ipWhitelist?: string | null;
}

export interface IdentityAuthenticationCreateApiKeyOutput {
  id?: string;
  name?: string | null;
  apiKey?: string | null;
  keyPrefix?: string | null;
  scopes?: Array<string> | null;
  expiresAt?: string | null;
  createdAt?: string;
}

export interface IdentityAuthenticationCreateRoleInput {
  name?: string | null;
  description?: string | null;
  permissions?: Array<string> | null;
  tenantId?: string | null;
}

export interface IdentityAuthenticationCreateServiceAccountInput {
  name?: string | null;
  description?: string | null;
  tenantId?: string | null;
  scopes?: string | null;
  allowedIpAddresses?: string | null;
  expiresAt?: string | null;
}

export interface IdentityAuthenticationDeviceInfo {
  fingerprint?: string | null;
  deviceId?: string | null;
  ipAddress?: string | null;
  deviceName?: string | null;
  deviceType?: string | null;
  operatingSystem?: string | null;
  osVersion?: string | null;
  browser?: string | null;
  browserVersion?: string | null;
  screenResolution?: string | null;
  timezone?: string | null;
  language?: string | null;
  userAgent?: string | null;
  isMobile?: boolean;
  isBot?: boolean;
}

export interface IdentityAuthenticationDisableMfaInput {
  password: string;
}

export interface IdentityAuthenticationDiscordAuthorizeInput {
  redirectUri: string;
}

export interface IdentityAuthenticationDiscordCallbackInput {
  code: string;
  state: string;
  redirectUri: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationDiscordLinkAuthorizeInput {
  redirectUri: string;
}

export interface IdentityAuthenticationDiscordLinkAuthorizeOutput {
  authUrl: string | null;
  state: string | null;
}

export interface IdentityAuthenticationDiscordLinkCallbackInput {
  code: string;
  state: string;
  redirectUri: string;
}

export interface IdentityAuthenticationDiscordSignInOutput {
  authUrl: string | null;
  state: string | null;
}

export interface IdentityAuthenticationEmailVerificationOutput {
  message: string | null;
}

export interface IdentityAuthenticationEmailVerificationResult {
  success?: boolean;
  message?: string | null;
  email?: string | null;
  userId?: string | null;
  verifiedAt?: string | null;
}

export interface IdentityAuthenticationGitHubSignInOutput {
  authUrl: string | null;
}

export interface IdentityAuthenticationGoogleIdTokenInput {
  idToken: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationJwtKeyInfo {
  keyId?: string | null;
  algorithm?: string | null;
  isActive?: boolean;
  validFrom?: string;
  expiresAt?: string;
  rotatedAt?: string | null;
  rotationReason?: string | null;
  keyVersion?: number;
}

export interface IdentityAuthenticationLinkGoogleAccountInput {
  idToken: string;
}

export interface IdentityAuthenticationLocalSignInInput {
  username?: string | null;
  email: string;
  password: string;
  tenantId?: string | null;
  deviceFingerprint?: string | null;
  emailOrUsername?: string | null;
}

export interface IdentityAuthenticationLocalSignUpInput {
  username: string;
  email: string;
  password: string;
  tenantId?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityAuthenticationLocationInfo {
  ipAddress?: string | null;
  country?: string | null;
  countryCode?: string | null;
  region?: string | null;
  city?: string | null;
  postalCode?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  timezone?: string | null;
  isp?: string | null;
  organization?: string | null;
  isProxy?: boolean | null;
  isHosting?: boolean | null;
  displayLocation?: string | null;
}

export interface IdentityAuthenticationLockServiceAccountInput {
  reason?: string | null;
}

export interface IdentityAuthenticationMagicLinkRequestResult {
  success?: boolean;
  message?: string | null;
  expiresInMinutes?: number;
  developmentPreviewToken?: string | null;
}

export interface IdentityAuthenticationMfaConfigurationOutput {
  isEnabled?: boolean;
  enabledMethods?: Array<string> | null;
  enabledAt?: string | null;
  backupCodesRemaining?: number;
}

export interface IdentityAuthenticationMfaErrorOutput {
  error: string | null;
}

export type IdentityAuthenticationMfaMethod =
  "Totp" | "BackupCode" | "Sms" | "Email" | "WebAuthn";

export interface IdentityAuthenticationMfaMethodInfo {
  method: IdentityAuthenticationMfaMethod;
  name: string | null;
  description: string | null;
  isEnabled: boolean;
  isAvailable: boolean;
  priority: number;
}

export interface IdentityAuthenticationMfaMethodsOutput {
  methods: Array<IdentityAuthenticationMfaMethodInfo> | null;
  defaultMethod?: IdentityAuthenticationMfaMethod;
}

export interface IdentityAuthenticationMfaSetupOutput {
  isSuccess?: boolean;
  errorMessage?: string | null;
  secretKey?: string | null;
  qrCodeData?: string | null;
  qrCodeUri?: string | null;
  backupCodes?: Array<string> | null;
}

export interface IdentityAuthenticationMfaSuccessOutput {
  message: string | null;
}

export interface IdentityAuthenticationMfaVerificationOutput {
  isValid?: boolean;
  accessToken?: string | null;
  refreshToken?: string | null;
}

export interface IdentityAuthenticationOAuth2ErrorOutput {
  error?: string | null;
  errorDescription?: string | null;
}

export interface IdentityAuthenticationPasswordChangeInput {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
  revokeOtherSessions?: boolean;
}

export interface IdentityAuthenticationPasswordChangeResult {
  success?: boolean;
  message?: string | null;
  sessionsRevoked?: number;
}

export interface IdentityAuthenticationPasswordResetRequestResult {
  success?: boolean;
  message?: string | null;
  expiresInMinutes?: number;
}

export interface IdentityAuthenticationPasswordResetResult {
  success?: boolean;
  message?: string | null;
  userId?: string | null;
}

export interface IdentityAuthenticationPatchServiceAccountInput {
  name?: string | null;
  description?: string | null;
  scopes?: string | null;
  expiresAt?: string | null;
}

export interface IdentityAuthenticationRefreshTokenInput {
  refreshToken: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRemoveRoleFromUserInput {
  userId?: string;
  roleId?: string;
}

export interface IdentityAuthenticationRequestMagicLinkInput {
  email: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRequestPasswordResetInput {
  email: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRevokeApiKeyInput {
  reason?: string | null;
}

export interface IdentityAuthenticationRevokeRefreshTokenInput {
  token: string;
  ipAddress?: string | null;
  reason?: string | null;
}

export type IdentityAuthenticationRiskLevel =
  "Low" | "Medium" | "High" | "Critical";

export interface IdentityAuthenticationRotateKeyInput {
  reason?: string | null;
  validityDays?: number | null;
}

export interface IdentityAuthenticationSecretRotationOutput {
  clientSecret?: string | null;
  warning?: string | null;
}

export interface IdentityAuthenticationSendEmailVerificationInput {
  email: string;
}

export interface IdentityAuthenticationServiceAccountAuditEntry {
  id?: string;
  timestamp?: string;
  action?: string | null;
  performedBy?: string | null;
  ipAddress?: string | null;
  details?: string | null;
}

export interface IdentityAuthenticationServiceAccountAuditLogOutput {
  serviceAccountId?: string;
  entries?: Array<IdentityAuthenticationServiceAccountAuditEntry> | null;
  totalCount?: number;
  page?: number;
  pageSize?: number;
}

export interface IdentityAuthenticationServiceAccountCreatedOutput {
  id?: string;
  clientId?: string | null;
  clientSecret?: string | null;
  name?: string | null;
  description?: string | null;
  tenantId?: string | null;
  scopes?: string | null;
  createdAt?: string;
  expiresAt?: string | null;
  warning?: string | null;
}

export interface IdentityAuthenticationServiceAccountOutput {
  id?: string;
  clientId?: string | null;
  name?: string | null;
  description?: string | null;
  tenantId?: string | null;
  scopes?: string | null;
  isActive?: boolean;
  isLocked?: boolean;
  expiresAt?: string | null;
  createdAt?: string;
  createdBy?: string | null;
  lastAuthenticatedAt?: string | null;
  authenticationCount?: number;
  secretRotationCount?: number;
}

export interface IdentityAuthenticationSessionOutput {
  id?: string;
  deviceInfo?: IdentityAuthenticationDeviceInfo;
  location?: IdentityAuthenticationLocationInfo;
  ipAddress?: string | null;
  createdAt?: string;
  lastUsedAt?: string;
  expiresAt?: string;
  isTrustedDevice?: boolean;
  isCurrent?: boolean;
}

export interface IdentityAuthenticationSessionSecurityAnalysis {
  sessionId?: string;
  userId?: string;
  isSuspicious?: boolean;
  unusualActivityDetected?: boolean;
  riskScore?: number;
  activeSessionCount?: number;
  totalDeviceCount?: number;
  riskLevel?: IdentityAuthenticationRiskLevel;
  securityFlags?: Array<string> | null;
  riskFactors?: Array<string> | null;
  metadata?: Record<string, string> | null;
  analyzedAt?: string;
}

export interface IdentityAuthenticationSessionSuccessOutput {
  message: string | null;
}

export interface IdentityAuthenticationSessionTerminationOutput {
  message: string | null;
  terminatedCount: number;
}

export interface IdentityAuthenticationSignInOutput {
  success?: boolean;
  message?: string | null;
  accessToken?: string | null;
  refreshToken?: string | null;
  expiresAt?: string;
  accessTokenExpiresAt?: string;
  refreshTokenExpiresAt?: string;
  expiresIn?: number;
  userId?: string;
  email?: string | null;
  sessionId?: string;
  tempToken?: string | null;
  mfaToken?: string | null;
  user?: IdentityAuthenticationUser;
  tenantId?: string | null;
  availableTenants?: Array<TenantInfo> | null;
  requiresMfa?: boolean;
  mfaSessionId?: string | null;
  requiresStepUp?: boolean;
  stepUpToken?: string | null;
  stepUpExpiresAt?: string | null;
  riskLevel?: IdentityAuthenticationRiskLevel;
  riskFactors?: Array<string> | null;
  availableMethods?: Array<string> | null;
}

export interface IdentityAuthenticationSmsMfaSetupInput {
  phoneNumber: string | null;
}

export interface IdentityAuthenticationSmsMfaSetupOutput {
  message: string | null;
  phoneNumberMasked: string | null;
  expiresInSeconds: number;
}

export interface IdentityAuthenticationTrustDeviceInput {
  deviceName?: string | null;
}

export interface IdentityAuthenticationTrustedDeviceOutput {
  id?: string;
  deviceName?: string | null;
  deviceInfo?: IdentityAuthenticationDeviceInfo;
  trustedAt?: string;
  lastUsedAt?: string;
  expiresAt?: string | null;
}

export interface IdentityAuthenticationUpdateCredentialNameInput {
  friendlyName?: string | null;
}

export interface IdentityAuthenticationUpdateRoleInput {
  name?: string | null;
  description?: string | null;
  permissions?: Array<string> | null;
  isActive?: boolean | null;
}

export interface IdentityAuthenticationUpdateScopesInput {
  scopes?: string | null;
}

export interface IdentityAuthenticationUser {
  id?: string;
  email?: string | null;
  username?: string | null;
  firstName?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  emailVerified?: boolean;
  phoneNumberVerified?: boolean;
  createdAt?: string;
  lastLoginAt?: string | null;
}

export interface IdentityAuthenticationVerifyEmailInput {
  token: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationVerifyMfaInput {
  userId?: string;
  code: string;
  method?: IdentityAuthenticationMfaMethod;
}

export interface IdentityAuthenticationWeb3ChallengeInput {
  walletAddress: string;
  chainId?: string | null;
}

export interface IdentityAuthenticationWeb3ChallengeOutput {
  challenge?: string | null;
  nonce?: string | null;
  expiresAt?: string;
}

export interface IdentityAuthenticationWeb3VerifyInput {
  walletAddress: string;
  signature: string;
  nonce: string;
  chainId: string;
  tenantId?: string | null;
  deviceFingerprint?: string | null;
}

export interface IdentityAuthenticationWebAuthnAuthenticationOptionsResult {
  success?: boolean;
  error?: string | null;
  sessionId?: string | null;
  optionsJson?: string | null;
  options?: Fido2NetLibAssertionOptions;
}

export interface IdentityAuthenticationWebAuthnAuthenticationResult {
  success?: boolean;
  error?: string | null;
  userId?: string | null;
  credentialId?: string | null;
  isPasswordless?: boolean;
  email?: string | null;
  accessToken?: string | null;
  refreshToken?: string | null;
  accessTokenExpiresAt?: string | null;
  refreshTokenExpiresAt?: string | null;
  expiresIn?: number;
}

export type IdentityAuthenticationWebAuthnAuthenticatorType =
  "Platform" | "CrossPlatform";

export interface IdentityAuthenticationWebAuthnCredentialInfo {
  id?: string;
  friendlyName?: string | null;
  authenticatorType?: IdentityAuthenticationWebAuthnAuthenticatorType;
  createdAt?: string;
  lastUsedAt?: string | null;
  isPasswordless?: boolean;
  isDefault?: boolean;
  backedUp?: boolean;
}

export interface IdentityAuthenticationWebAuthnCredentialVerifyResult {
  success?: boolean;
  error?: string | null;
  isValid?: boolean;
  isExpired?: boolean;
  isRevoked?: boolean;
  lastUsedAt?: string | null;
  signatureCount?: number;
}

export interface IdentityAuthenticationWebAuthnRegistrationOptionsResult {
  success?: boolean;
  error?: string | null;
  sessionId?: string | null;
  optionsJson?: string | null;
  options?: Fido2NetLibCredentialCreateOptions;
}

export interface IdentityAuthenticationWebAuthnRegistrationResult {
  success?: boolean;
  error?: string | null;
  credentialId?: string | null;
  friendlyName?: string | null;
}

export interface IdentityAuthenticationWebAuthnStatusOutput {
  isEnabled?: boolean;
  credentialCount?: number;
  hasPasswordlessCredential?: boolean;
  hasPlatformAuthenticator?: boolean;
  hasSecurityKey?: boolean;
}

export interface IdentityAuthorizationAccessReviewCampaign {
  id?: string;
  tenantId?: CQRSModelsTenantId;
  name?: string | null;
  description?: string | null;
  reviewType?: IdentityAuthorizationAccessReviewType;
  scope?: IdentityAuthorizationAccessReviewScope;
  scopeFilter?: string | null;
  startDate?: string;
  endDate?: string;
  status?: IdentityAuthorizationAccessReviewStatus;
  totalItems?: number;
  reviewedItems?: number;
  approvedItems?: number;
  revokedItems?: number;
  createdBy?: string;
  completedBy?: string | null;
  completedAt?: string | null;
  autoRevokeOnNoResponse?: boolean;
  reminderFrequencyDays?: number;
  notificationTemplate?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  items?: Array<IdentityAuthorizationAccessReviewItem> | null;
}

export type IdentityAuthorizationAccessReviewDecision =
  "None" | "Approve" | "Revoke" | "ModifyAndApprove";

export interface IdentityAuthorizationAccessReviewItem {
  id?: string;
  campaignId?: string;
  reviewerId?: string;
  subjectUserId?: string;
  resourceId?: string | null;
  resourceType?: string | null;
  permissionDetails?: string | null;
  status?: IdentityAuthorizationAccessReviewItemStatus;
  decision?: IdentityAuthorizationAccessReviewDecision;
  decisionReason?: string | null;
  reviewedAt?: string | null;
  lastReminderSent?: string | null;
  reminderCount?: number;
  reviewerNotes?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  campaign?: IdentityAuthorizationAccessReviewCampaign;
}

export type IdentityAuthorizationAccessReviewItemStatus =
  "None" | "Pending" | "Reviewed" | "Approved" | "Revoked" | "Expired";

export type IdentityAuthorizationAccessReviewScope =
  | "None"
  | "AllUsers"
  | "Department"
  | "Team"
  | "Role"
  | "Resource"
  | "HighPrivilege"
  | "External"
  | "Custom";

export type IdentityAuthorizationAccessReviewStatus =
  "None" | "Draft" | "Active" | "InProgress" | "Completed" | "Expired";

export type IdentityAuthorizationAccessReviewType =
  | "None"
  | "PermissionReview"
  | "RoleReview"
  | "ResourceAccessReview"
  | "UserAccessReview"
  | "ComplianceAttestation";

export interface IdentityAuthorizationCommandsCreateAccessReviewCampaignCommand {
  name?: string | null;
  description?: string | null;
  tenantId?: string | null;
  reviewType?: IdentityAuthorizationAccessReviewType;
  startDate?: string;
  endDate?: string;
  createdBy?: string;
}

export interface IdentityAuthorizationCommandsCreateSoDRuleCommand {
  name?: string | null;
  description?: string | null;
  conflictingPermissions?: Array<string> | null;
  tenantId?: string | null;
  ruleType?: IdentityAuthorizationSoDRuleType;
  isEnabled?: boolean;
}

export interface IdentityAuthorizationCommandsDelegatePermissionsCommand {
  delegatorUserId?: string;
  delegateUserId?: string;
  permissions?: Array<string> | null;
  tenantId?: string | null;
  resourceId?: string | null;
  expiresAt?: string | null;
  canSubDelegate?: boolean;
  reason?: string | null;
  usageLimit?: number | null;
}

export interface IdentityAuthorizationCommandsGrantDelegatedAdminCommand {
  adminUserId?: string;
  tenantId?: string | null;
  name?: string | null;
  description?: string | null;
  managedResourceTypes?: Array<string> | null;
  managedUserIds?: Array<string> | null;
  allowedOperations?: Array<string> | null;
  organizationalUnitId?: string | null;
}

export interface IdentityAuthorizationCommandsRequestJitElevationCommand {
  requesterId?: string;
  tenantId?: string | null;
  permission?: string | null;
  justification?: string | null;
  durationMinutes?: number;
  resourceId?: string | null;
  resourceType?: string | null;
  startsAt?: string | null;
}

export interface IdentityAuthorizationControllersApproveElevationInput {
  reviewerId?: string;
  comments?: string | null;
}

export interface IdentityAuthorizationControllersApproveItemInput {
  reason?: string | null;
  notes?: string | null;
}

export interface IdentityAuthorizationControllersCompleteCampaignInput {
  completedBy?: string;
}

export interface IdentityAuthorizationControllersDenyElevationInput {
  reviewerId?: string;
  comments?: string | null;
}

export interface IdentityAuthorizationControllersGrantExceptionInput {
  approvedBy?: string;
  justification?: string | null;
}

export interface IdentityAuthorizationControllersResolveViolationInput {
  resolvedBy?: string;
  action?: IdentityAuthorizationSoDResolutionAction;
  notes?: string | null;
}

export interface IdentityAuthorizationControllersRevokeElevationInput {
  revokedBy?: string;
  reason?: string | null;
}

export interface IdentityAuthorizationControllersRevokeItemInput {
  reason?: string | null;
  notes?: string | null;
}

export interface IdentityAuthorizationControllersUpdateSoDRuleInput {
  name?: string | null;
  description?: string | null;
  conflictingPermissions?: Array<string> | null;
  ruleType?: IdentityAuthorizationSoDRuleType;
  isEnabled?: boolean;
}

export interface IdentityAuthorizationDeclineInvitationInput {
  reason?: string | null;
}

export interface IdentityAuthorizationDelegatedAdminScope {
  id?: string;
  tenantId?: CQRSModelsTenantId;
  adminUserId?: string;
  name?: string | null;
  description?: string | null;
  scopeType?: IdentityAuthorizationDelegatedAdminScopeType;
  allowedResourceTypes?: string | null;
  allowedResourceIds?: string | null;
  allowedUserIds?: string | null;
  allowedDepartments?: string | null;
  allowedTeams?: string | null;
  allowedRoles?: string | null;
  grantablePermissions?: string | null;
  deniedPermissions?: string | null;
  canManageUsers?: boolean;
  canManagePermissions?: boolean;
  canManageResources?: boolean;
  canViewAuditLogs?: boolean;
  startsAt?: string;
  expiresAt?: string | null;
  isActive?: boolean;
  createdBy?: string;
  createdAt?: string;
  updatedAt?: string | null;
}

export type IdentityAuthorizationDelegatedAdminScopeType =
  "None" | "Department" | "Team" | "Role" | "Resource" | "Custom";

export interface IdentityAuthorizationDenyTenantPermissionCommand {
  tenantId: CQRSModelsTenantId;
  userId: string;
  permissions: Array<string> | null;
  deniedBy: string;
  reason?: string | null;
}

export interface IdentityAuthorizationEffectivePermission {
  permission: string | null;
  source: string | null;
  expiresAt?: string | null;
  grantedAt?: string | null;
}

export interface IdentityAuthorizationEffectivePermissionsOutput {
  userId: string;
  resourceType: string | null;
  resourceId: string;
  permissions: Array<IdentityAuthorizationEffectivePermission> | null;
  isOwner?: boolean;
  hasFullAccess?: boolean;
}

export type IdentityAuthorizationElevationRequestStatus =
  "None" | "Pending" | "Approved" | "Denied" | "Active" | "Expired" | "Revoked";

export interface IdentityAuthorizationGetPendingResourceInvitationsOutput {
  invitations: Array<IdentityAuthorizationResourceInvitation> | null;
  totalCount?: number;
}

export interface IdentityAuthorizationGetResourceInvitationOutput {
  invitation: IdentityAuthorizationResourceInvitation;
}

export interface IdentityAuthorizationGetResourceUsersOutput {
  resourceType: string | null;
  resourceId: string | null;
  users: Array<IdentityAuthorizationResourceUser> | null;
  totalCount?: number;
  ownerCount?: number;
}

export interface IdentityAuthorizationGetTenantPermissionsOutput {
  userId: string;
  tenantId: string;
  permissions: Array<string> | null;
  isTenantAdmin?: boolean;
  isSystemAdmin?: boolean;
}

export interface IdentityAuthorizationGrantTenantPermissionCommand {
  tenantId: CQRSModelsTenantId;
  userId: string;
  permissions: Array<string> | null;
  grantedBy: string;
  expiresAt?: string | null;
  reason?: string | null;
}

export interface IdentityAuthorizationHasPermissionOutput {
  hasPermission: boolean;
  userId: string;
  resourceType: string | null;
  resourceId: string;
  permission: string | null;
  denialReason?: string | null;
}

export type IdentityAuthorizationImpactSeverity =
  "Low" | "Medium" | "High" | "Critical";

export interface IdentityAuthorizationInvitationActionResult {
  success?: boolean;
  invitationId?: string;
  status?: string | null;
  tenantId?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  errorMessage?: string | null;
}

export interface IdentityAuthorizationJitElevationInput {
  id?: string;
  requesterId?: string;
  tenantId?: CQRSModelsTenantId;
  permission?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  justification?: string | null;
  durationMinutes?: number;
  startsAt?: string | null;
  expiresAt?: string;
  status?: IdentityAuthorizationElevationRequestStatus;
  reviewerId?: string | null;
  reviewedAt?: string | null;
  reviewerComments?: string | null;
  activatedAt?: string | null;
  revokedAt?: string | null;
  revokedBy?: string | null;
  revocationReason?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface IdentityAuthorizationPermissionAnalyticsReport {
  tenantId?: string | null;
  periodStart?: string;
  periodEnd?: string;
  topPermissions?: Array<IdentityAuthorizationPermissionUsageMetrics> | null;
  topUsers?: Array<IdentityAuthorizationUserActivitySummary> | null;
  anomalies?: Array<IdentityAuthorizationPermissionAnomaly> | null;
  totalGrants?: number;
  totalRevokes?: number;
  activeUsers?: number;
}

export interface IdentityAuthorizationPermissionAnomaly {
  userId?: string;
  anomalyType?: string | null;
  description?: string | null;
  detectedAt?: string;
  severity?: IdentityAuthorizationImpactSeverity;
}

export interface IdentityAuthorizationPermissionDelegation {
  id?: string;
  delegatorUserId?: string;
  delegateUserId?: string;
  tenantId?: CQRSModelsTenantId;
  resourceId?: string | null;
  delegatedPermissions?: Array<string> | null;
  startsAt?: string;
  expiresAt?: string | null;
  canSubDelegate?: boolean;
  isActive?: boolean;
  reason?: string | null;
  conditions?: string | null;
  usageLimit?: number | null;
  usageCount?: number;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface IdentityAuthorizationPermissionTrend {
  date?: string;
  grants?: number;
  revokes?: number;
  activePermissions?: number;
}

export type IdentityAuthorizationPermissionType =
  | "Read"
  | "Comment"
  | "Reply"
  | "Vote"
  | "Share"
  | "Report"
  | "Follow"
  | "Bookmark"
  | "React"
  | "Subscribe"
  | "Mention"
  | "Tag"
  | "Categorize"
  | "Collection"
  | "Series"
  | "CrossReference"
  | "Translate"
  | "Version"
  | "Template"
  | "Create"
  | "Draft"
  | "Submit"
  | "Withdraw"
  | "Archive"
  | "Restore"
  | "Delete"
  | "HardDelete"
  | "Backup"
  | "Migrate"
  | "Clone"
  | "Edit"
  | "Proofread"
  | "FactCheck"
  | "StyleGuide"
  | "Plagiarism"
  | "Seo"
  | "Accessibility"
  | "Legal"
  | "Brand"
  | "Guidelines"
  | "Approve"
  | "Reject"
  | "RequestRevision"
  | "Escalate"
  | "Override"
  | "Delegate"
  | "FastTrack"
  | "BatchApprove"
  | "ConditionalApprove"
  | "RequireReview"
  | "Publish"
  | "Unpublish"
  | "Schedule"
  | "SetPublishDate"
  | "Visibility"
  | "Feature"
  | "Pin"
  | "Sticky"
  | "Highlight"
  | "Promote"
  | "Moderate"
  | "Hide"
  | "Flag"
  | "Warn"
  | "Suspend"
  | "Ban"
  | "Quarantine"
  | "Review"
  | "Investigate"
  | "EscalateModeration"
  | "Invite"
  | "Assign"
  | "Collaborate"
  | "CoAuthor"
  | "Contribute"
  | "Suggest"
  | "Track"
  | "Merge"
  | "Resolve"
  | "Coordinate"
  | "Score"
  | "Rate"
  | "Benchmark"
  | "Metrics"
  | "Analytics"
  | "Performance"
  | "Feedback"
  | "Audit"
  | "Standards"
  | "Improvement"
  | "Monetize"
  | "Pricing"
  | "Paywall"
  | "Manage"
  | "Admin"
  | "Execute"
  | "Export"
  | "Import"
  | "SystemAdmin"
  | "TenantAdmin"
  | "UserManagement"
  | "Configure";

export interface IdentityAuthorizationPermissionUpdateResult {
  success?: boolean;
  userId?: string;
  updatedPermissions?: Array<string> | null;
  errorMessage?: string | null;
}

export interface IdentityAuthorizationPermissionUsageMetrics {
  permission?: string | null;
  usageCount?: number;
  uniqueUsers?: number;
  lastUsed?: string;
}

export interface IdentityAuthorizationRemoveDenyPermissionsCommand {
  tenantId: CQRSModelsTenantId;
  userId: string;
  permissions: Array<string> | null;
  removedBy: string;
}

export interface IdentityAuthorizationRemoveUserAccessCommand {
  tenantId: CQRSModelsTenantId;
  resourceType: string | null;
  resourceId: string | null;
  targetUserId: string;
  removedByUserId: string;
  reason?: string | null;
}

export interface IdentityAuthorizationResourceAccessPattern {
  resourceType?: string | null;
  resourceId?: string;
  accessCount?: number;
  uniqueUsers?: number;
}

export interface IdentityAuthorizationResourceInvitation {
  invitationId?: string;
  tenantId?: string;
  email?: string | null;
  resourceType?: string | null;
  resourceId?: string | null;
  permissions?: Array<string> | null;
  message?: string | null;
  invitedByUserName?: string | null;
  invitedAt?: string;
  expiresAt?: string | null;
  status?: string | null;
}

export interface IdentityAuthorizationResourceUser {
  userId: string;
  resourceType: string | null;
  resourceId: string | null;
  permissions: Array<string> | null;
  grantedAt: string;
  grantedByUserId: string;
  expiresAt?: string | null;
  lastAccessedAt?: string | null;
  isActive?: boolean;
  isOwner?: boolean;
}

export interface IdentityAuthorizationRevokeTenantPermissionCommand {
  tenantId: CQRSModelsTenantId;
  userId: string;
  permissions: Array<string> | null;
  revokedBy: string;
  reason?: string | null;
}

export interface IdentityAuthorizationSetGlobalDefaultPermissionsCommand {
  permissions: Array<string> | null;
  setBy: string;
}

export interface IdentityAuthorizationSetTenantDefaultPermissionsCommand {
  tenantId: CQRSModelsTenantId;
  permissions: Array<string> | null;
  setBy: string;
}

export interface IdentityAuthorizationShareResourceCommand {
  tenantId: CQRSModelsTenantId;
  resourceType: string | null;
  resourceId: string | null;
  userIds: Array<string> | null;
  userEmails?: Array<string> | null;
  permissions: Array<string> | null;
  grantedByUserId: string;
  expiresAt?: string | null;
  message?: string | null;
  requireAcceptance?: boolean;
  notifyUsers?: boolean;
}

export interface IdentityAuthorizationShareResult {
  success?: boolean;
  isNewUser?: boolean;
  userId?: string | null;
  invitationId?: string | null;
  email?: string | null;
  errorMessage?: string | null;
  invitationLink?: string | null;
}

export type IdentityAuthorizationSoDResolutionAction =
  | "None"
  | "RevokePermission"
  | "RevokeRole"
  | "GrantException"
  | "ImplementCompensatingControl"
  | "TransferOwnership"
  | "NoAction";

export interface IdentityAuthorizationSoDRule {
  id?: string;
  tenantId?: CQRSModelsTenantId;
  name?: string | null;
  description?: string | null;
  ruleType?: IdentityAuthorizationSoDRuleType;
  severity?: IdentityAuthorizationSoDSeverity;
  isEnabled?: boolean;
  conflictingPermissions?: string | null;
  conflictingRoles?: string | null;
  conflictingResources?: string | null;
  allowedExceptions?: string | null;
  requireApproval?: boolean;
  approverRoles?: string | null;
  mitigationStrategy?: string | null;
  violationCount?: number;
  lastViolationDetected?: string | null;
  createdBy?: string;
  createdAt?: string;
  updatedAt?: string | null;
  violations?: Array<IdentityAuthorizationSoDViolation> | null;
}

export type IdentityAuthorizationSoDRuleType =
  | "None"
  | "PermissionConflict"
  | "RoleConflict"
  | "ResourceConflict"
  | "BusinessProcessConflict"
  | "FunctionalConflict";

export type IdentityAuthorizationSoDSeverity =
  "None" | "Low" | "Medium" | "High" | "Critical";

export interface IdentityAuthorizationSoDViolation {
  id?: string;
  ruleId?: string;
  userId?: string;
  tenantId?: CQRSModelsTenantId;
  status?: IdentityAuthorizationSoDViolationStatus;
  violationDetails?: string | null;
  conflictingItems?: string | null;
  detectedAt?: string;
  detectedBy?: string | null;
  resolvedAt?: string | null;
  resolvedBy?: string | null;
  resolutionNotes?: string | null;
  resolutionAction?: IdentityAuthorizationSoDResolutionAction;
  isException?: boolean;
  exceptionJustification?: string | null;
  approvedBy?: string | null;
  approvedAt?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  rule?: IdentityAuthorizationSoDRule;
}

export type IdentityAuthorizationSoDViolationStatus =
  | "None"
  | "Active"
  | "Acknowledged"
  | "Mitigated"
  | "Resolved"
  | "Excepted"
  | "FalsePositive";

export interface IdentityAuthorizationUpdateUserPermissionsCommand {
  tenantId: CQRSModelsTenantId;
  resourceType: string | null;
  resourceId: string | null;
  targetUserId: string;
  permissions: Array<string> | null;
  updatedByUserId: string;
  expiresAt?: string | null;
}

export interface IdentityAuthorizationUserActivitySummary {
  userId?: string;
  totalActions?: number;
  permissionChanges?: number;
  lastActivity?: string;
}

export interface IdentityTenantsAddTenantMemberOutput {
  success?: boolean;
  message?: string | null;
  memberId?: string | null;
}

export interface IdentityTenantsAddUserMembershipInput {
  tenantId?: string;
  role?: string | null;
  invitedByEmail?: string | null;
  requiresAcceptance?: boolean;
  inviteeEmail?: string | null;
  inviteeName?: string | null;
}

export interface IdentityTenantsArchiveInput {
  reason?: string | null;
}

export interface IdentityTenantsBulkActivateTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkArchiveTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkCreateTenantItem {
  name?: string | null;
  slug?: string | null;
  adminEmail?: string | null;
  description?: string | null;
}

export interface IdentityTenantsBulkCreateTenantsCommand {
  tenants?: Array<IdentityTenantsBulkCreateTenantItem> | null;
}

export interface IdentityTenantsBulkDeactivateTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkDeleteTenantsCommand {
  tenantIds?: Array<string> | null;
  hardDelete?: boolean;
}

export interface IdentityTenantsBulkPurgeTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkUndeleteTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkUpdateTenantItem {
  tenantId?: string;
  name?: string | null;
  description?: string | null;
}

export interface IdentityTenantsBulkUpdateTenantsCommand {
  updates?: Array<IdentityTenantsBulkUpdateTenantItem> | null;
}

export interface IdentityTenantsCreateTenantInput {
  name?: string | null;
  slug?: string | null;
  adminEmail?: string | null;
  description?: string | null;
}

export interface IdentityTenantsGetUserMembershipsOutput {
  memberships?: Array<IdentityTenantsUserMembership> | null;
  totalCount?: number;
}

export interface IdentityTenantsMembershipCountOutput {
  count?: number;
}

export interface IdentityTenantsRecoverInput {
  reason?: string | null;
}

export interface IdentityTenantsReplaceTenantMetadataInput {
  customFields?: Record<string, Record<string, unknown> | null> | null;
  tags?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
  businessInfo?: IdentityTenantsUpdateTenantBusinessInfoInput;
  contactInfo?: IdentityTenantsUpdateTenantContactInfoInput;
  adminNotes?: string | null;
}

export interface IdentityTenantsReplaceTenantSettingsInput {
  systemConfiguration?: IdentityTenantsUpdateTenantSystemConfigurationInput;
  featureFlags?: Record<string, boolean> | null;
  businessRules?: IdentityTenantsUpdateTenantBusinessRulesInput;
  userInterfaceSettings?: IdentityTenantsUpdateTenantUiSettingsInput;
  securitySettings?: IdentityTenantsUpdateTenantSecuritySettingsInput;
  integrationSettings?: IdentityTenantsUpdateTenantIntegrationSettingsInput;
  systemLimits?: IdentityTenantsUpdateTenantSystemLimitsInput;
}

export interface IdentityTenantsSetTenantMembershipStatusInput {
  reason?: string | null;
}

export interface IdentityTenantsSetTenantMembershipStatusOutput {
  success?: boolean;
  notFound?: boolean;
  message?: string | null;
  memberId?: string;
  isActive?: boolean;
}

export interface IdentityTenantsSlugValidation {
  isAvailable?: boolean;
  isValid?: boolean;
  suggestedAlternatives?: Array<string> | null;
}

export interface IdentityTenantsTenant {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  isDefault?: boolean;
  isArchived?: boolean;
  archivedAt?: string | null;
  tenantMembers?: Array<IdentityTenantsTenantMember> | null;
  tenantDomains?: Array<IdentityTenantsTenantDomain> | null;
  tenantSettings?: IdentityTenantsTenantSettings;
  tenantStatistics?: IdentityTenantsTenantStatistics;
  usageTrackingRecords?: Array<IdentityTenantsUsageTracking> | null;
  name: string;
  description?: string | null;
  isActive?: boolean;
  slug: string;
  adminEmail?: string | null;
  canAcceptMembers?: boolean;
  activeMemberCount?: number;
  hasActiveMembers?: boolean;
}

export interface IdentityTenantsTenantAddress {
  street?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
}

export interface IdentityTenantsTenantAuditLogEntry {
  id?: string;
  tenantId?: string;
  timestamp?: string;
  action?: string | null;
  actorId?: string | null;
  actorName?: string | null;
  actorEmail?: string | null;
  beforeValues?: Record<string, Record<string, unknown> | null> | null;
  afterValues?: Record<string, Record<string, unknown> | null> | null;
  ipAddress?: string | null;
  userAgent?: string | null;
  correlationId?: string | null;
  metadata?: Record<string, string> | null;
}

export interface IdentityTenantsTenantBranding {
  logoUrl?: string | null;
  faviconUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  companyName?: string | null;
}

export interface IdentityTenantsTenantBusinessInfo {
  industry?: string | null;
  organizationSize?: string | null;
  tenantType?: string | null;
  geographicRegion?: string | null;
  complianceRequirements?: Array<string> | null;
}

export interface IdentityTenantsTenantBusinessRules {
  workflowRules?: Record<string, Record<string, unknown> | null> | null;
  validationRules?: Record<string, Record<string, unknown> | null> | null;
  approvalRules?: Record<string, Record<string, unknown> | null> | null;
  notificationRules?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantContactInfo {
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;
  organizationName?: string | null;
  address?: IdentityTenantsTenantAddress;
  website?: string | null;
}

export interface IdentityTenantsTenantCurrencySettings {
  defaultCurrency?: string | null;
  displayFormat?: string | null;
  decimalPlaces?: number;
}

export interface IdentityTenantsTenantDomain {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId: string;
  topLevelDomain: string;
  subdomain?: string | null;
  isMainDomain?: boolean;
  isSecondaryDomain?: boolean;
  userGroupId?: string | null;
  fullDomain?: string | null;
  tenant?: IdentityTenantsTenant;
}

export interface IdentityTenantsTenantIntegrationSettings {
  externalServices?: Record<string, Record<string, unknown> | null> | null;
  webhookSettings?: Record<string, Record<string, unknown> | null> | null;
  apiKeys?: Record<string, string> | null;
  ssoConfiguration?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantMember {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId: string;
  userId: string;
  parentMemberId?: string | null;
  parentMember?: IdentityTenantsTenantMember;
  childMembers?: Array<IdentityTenantsTenantMember> | null;
  role: string;
  isActive?: boolean;
  joinedAt?: string;
  leftAt?: string | null;
  leaveReason?: string | null;
  metadata?: string | null;
  tenant?: IdentityTenantsTenant;
}

export interface IdentityTenantsTenantMetadata {
  id?: string;
  customFields?: Record<string, Record<string, unknown> | null> | null;
  tags?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
  businessInfo?: IdentityTenantsTenantBusinessInfo;
  contactInfo?: IdentityTenantsTenantContactInfo;
  adminNotes?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface IdentityTenantsTenantSecuritySettings {
  passwordPolicy?: Record<string, Record<string, unknown> | null> | null;
  sessionTimeout?: number;
  twoFactorRequired?: boolean;
  ipWhitelist?: Array<string> | null;
  apiRateLimits?: Record<string, number> | null;
}

export interface IdentityTenantsTenantSettings {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId: string;
  defaultLanguage?: string | null;
  defaultTimezone?: string | null;
  defaultCurrency?: string | null;
  allowUserRegistration?: boolean;
  requireRegistrationApproval?: boolean;
  requireTwoFactorAuth?: boolean;
  maxUsers?: number | null;
  storageQuota?: number | null;
  enableAuditLogging?: boolean;
  enableApiAccess?: boolean;
  brandingSettings?: string | null;
  notificationSettings?: string | null;
  securitySettings?: string | null;
  integrationSettingsJson?: string | null;
  tenant?: IdentityTenantsTenant;
}

export interface IdentityTenantsTenantSettingsDto {
  id?: string;
  systemConfiguration?: IdentityTenantsTenantSystemConfiguration;
  featureFlags?: Record<string, boolean> | null;
  businessRules?: IdentityTenantsTenantBusinessRules;
  userInterfaceSettings?: IdentityTenantsTenantUiSettings;
  securitySettings?: IdentityTenantsTenantSecuritySettings;
  integrationSettings?: IdentityTenantsTenantIntegrationSettings;
  systemLimits?: IdentityTenantsTenantSystemLimits;
  createdAt?: string;
  updatedAt?: string;
}

export interface IdentityTenantsTenantStatistics {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId: string;
  statisticDate?: string;
  totalMembers?: number;
  activeMembers?: number;
  inactiveMembers?: number;
  storageUsed?: number;
  apiCalls?: number;
  newMembers?: number;
  membersLeft?: number;
  customMetrics?: string | null;
  tenant?: IdentityTenantsTenant;
}

export interface IdentityTenantsTenantSystemConfiguration {
  timeZone?: string | null;
  locale?: string | null;
  dateFormat?: string | null;
  numberFormat?: string | null;
  currencySettings?: IdentityTenantsTenantCurrencySettings;
  customConfiguration?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantSystemLimits {
  maxUsers?: number;
  maxStorage?: number;
  maxApiCalls?: number;
  maxProjects?: number;
  customLimits?: Record<string, number> | null;
}

export interface IdentityTenantsTenantUiSettings {
  theme?: string | null;
  layout?: Record<string, Record<string, unknown> | null> | null;
  branding?: IdentityTenantsTenantBranding;
  customCss?: string | null;
  componentSettings?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantValidationError {
  field?: string | null;
  code?: string | null;
  message?: string | null;
}

export interface IdentityTenantsTenantValidationOutput {
  isValid?: boolean;
  errors?: Array<IdentityTenantsTenantValidationError> | null;
  warnings?: Array<IdentityTenantsTenantValidationWarning> | null;
  suggestions?: Array<string> | null;
  slugValidation?: IdentityTenantsSlugValidation;
}

export interface IdentityTenantsTenantValidationWarning {
  field?: string | null;
  code?: string | null;
  message?: string | null;
}

export interface IdentityTenantsUpdateTenantAddressInput {
  street?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
}

export interface IdentityTenantsUpdateTenantBrandingInput {
  logoUrl?: string | null;
  faviconUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
  companyName?: string | null;
}

export interface IdentityTenantsUpdateTenantBusinessInfoInput {
  industry?: string | null;
  organizationSize?: string | null;
  tenantType?: string | null;
  geographicRegion?: string | null;
  complianceRequirements?: Array<string> | null;
}

export interface IdentityTenantsUpdateTenantBusinessRulesInput {
  workflowRules?: Record<string, Record<string, unknown> | null> | null;
  validationRules?: Record<string, Record<string, unknown> | null> | null;
  approvalRules?: Record<string, Record<string, unknown> | null> | null;
  notificationRules?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateTenantContactInfoInput {
  primaryContactName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactPhone?: string | null;
  organizationName?: string | null;
  address?: IdentityTenantsUpdateTenantAddressInput;
  website?: string | null;
}

export interface IdentityTenantsUpdateTenantCurrencySettingsInput {
  defaultCurrency?: string | null;
  displayFormat?: string | null;
  decimalPlaces?: number | null;
}

export interface IdentityTenantsUpdateTenantFeatureFlagsInput {
  featureFlags?: Record<string, boolean> | null;
}

export interface IdentityTenantsUpdateTenantInput {
  name?: string | null;
  description?: string | null;
}

export interface IdentityTenantsUpdateTenantIntegrationSettingsInput {
  externalServices?: Record<string, Record<string, unknown> | null> | null;
  webhookSettings?: Record<string, Record<string, unknown> | null> | null;
  apiKeys?: Record<string, string> | null;
  ssoConfiguration?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateTenantMemberInviteOutput {
  success?: boolean;
  message?: string | null;
  memberId?: string | null;
  inviteStatus?: string | null;
}

export interface IdentityTenantsUpdateTenantMemberRoleOutput {
  success?: boolean;
  message?: string | null;
  memberId?: string;
  newRole?: string | null;
  tenantId?: string;
}

export interface IdentityTenantsUpdateTenantMetadataInput {
  customFields?: Record<string, Record<string, unknown> | null> | null;
  tags?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
  businessInfo?: IdentityTenantsUpdateTenantBusinessInfoInput;
  contactInfo?: IdentityTenantsUpdateTenantContactInfoInput;
  adminNotes?: string | null;
}

export interface IdentityTenantsUpdateTenantSecuritySettingsInput {
  passwordPolicy?: Record<string, Record<string, unknown> | null> | null;
  sessionTimeout?: number | null;
  twoFactorRequired?: boolean | null;
  ipWhitelist?: Array<string> | null;
  apiRateLimits?: Record<string, number> | null;
}

export interface IdentityTenantsUpdateTenantSettingsInput {
  systemConfiguration?: IdentityTenantsUpdateTenantSystemConfigurationInput;
  featureFlags?: Record<string, boolean> | null;
  businessRules?: IdentityTenantsUpdateTenantBusinessRulesInput;
  userInterfaceSettings?: IdentityTenantsUpdateTenantUiSettingsInput;
  securitySettings?: IdentityTenantsUpdateTenantSecuritySettingsInput;
  integrationSettings?: IdentityTenantsUpdateTenantIntegrationSettingsInput;
  systemLimits?: IdentityTenantsUpdateTenantSystemLimitsInput;
}

export interface IdentityTenantsUpdateTenantSystemConfigurationInput {
  timeZone?: string | null;
  locale?: string | null;
  dateFormat?: string | null;
  numberFormat?: string | null;
  currencySettings?: IdentityTenantsUpdateTenantCurrencySettingsInput;
  customConfiguration?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateTenantSystemLimitsInput {
  maxUsers?: number | null;
  maxStorage?: number | null;
  maxApiCalls?: number | null;
  maxProjects?: number | null;
  customLimits?: Record<string, number> | null;
}

export interface IdentityTenantsUpdateTenantTagsInput {
  tags?: Array<string> | null;
}

export interface IdentityTenantsUpdateTenantUiSettingsInput {
  theme?: string | null;
  layout?: Record<string, Record<string, unknown> | null> | null;
  branding?: IdentityTenantsUpdateTenantBrandingInput;
  customCss?: string | null;
  componentSettings?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateUserMembershipInviteInput {
  actorEmail?: string | null;
}

export interface IdentityTenantsUpdateUserMembershipRoleInput {
  role?: string | null;
}

export interface IdentityTenantsUsageTracking {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId: string;
  date?: string;
  resourceType: string;
  usageAmount?: number;
  unit?: string | null;
  cost?: number;
  metadata?: string | null;
  tenant?: IdentityTenantsTenant;
}

export interface IdentityTenantsUserMembership {
  membershipId?: string;
  tenantId?: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  tenantIsActive?: boolean;
  tenantIsDefault?: boolean;
  tenantDescription?: string | null;
  role?: string | null;
  isActive?: boolean;
  joinedAt?: string;
  leftAt?: string | null;
  inviteStatus?: string | null;
  invitedByEmail?: string | null;
  inviteeEmail?: string | null;
  inviteeName?: string | null;
  invitedAt?: string | null;
  lastInviteSentAt?: string | null;
  acceptedAt?: string | null;
  cancelledAt?: string | null;
  inviteResendCount?: number;
}

export interface IdentityTenantsValidateTenantInput {
  name?: string | null;
  slug?: string | null;
  adminEmail?: string | null;
}

export interface IdentityUsersBulkActivateUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkActivateUsersOutput {
  activatedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkCreateUsersInput {
  users?: Array<IdentityUsersCreateUserRequestItem> | null;
}

export interface IdentityUsersBulkCreateUsersOutput {
  createdUserIds?: Array<string> | null;
  failedEmails?: Array<string> | null;
}

export interface IdentityUsersBulkDeactivateUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkDeactivateUsersOutput {
  deactivatedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkDeleteUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkNotificationInput {
  notificationIds?: Array<string> | null;
  operation?: string | null;
  filterCriteria?: IdentityUsersNotificationFilterCriteria;
}

export interface IdentityUsersBulkPurgeUsersInput {
  userIds?: Array<string> | null;
  strategy?: IdentityUsersPurgeStrategy;
}

export interface IdentityUsersBulkRestoreUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkRestoreUsersOutput {
  restoredUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkSuspendUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkSuspendUsersOutput {
  suspendedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkUnsuspendUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkUnsuspendUsersOutput {
  unsuspendedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkUpdateUsersInput {
  updates?: Array<IdentityUsersUpdateUserRequestItem> | null;
}

export interface IdentityUsersCreateUserInput {
  email?: string | null;
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersCreateUserRequestItem {
  email?: string | null;
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersNotificationAction {
  id?: string | null;
  text?: string | null;
  url?: string | null;
  type?: string | null;
  isPrimary?: boolean;
}

export interface IdentityUsersNotificationFilterCriteria {
  categories?: Array<string> | null;
  priorities?: Array<string> | null;
  types?: Array<string> | null;
  isRead?: boolean | null;
  isArchived?: boolean | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

export type IdentityUsersNotificationPriority =
  "Low" | "Normal" | "High" | "Urgent" | "Critical";

export type IdentityUsersProfileVisibility =
  "Private" | "FriendsOnly" | "Public";

export type IdentityUsersPurgeStrategy =
  "Immediate" | "Scheduled" | "GracePeriod";

export interface IdentityUsersReplaceUserAccessibilityPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserLocalizationPreferencesInput {
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserMetadataInput {
  customFields?: Record<string, Record<string, unknown>> | null;
  tags?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
}

export interface IdentityUsersReplaceUserNotificationPreferencesInput {
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserPreferencesInput {
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserPrivacyPreferencesInput {
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserProfileInput {
  displayName?: string | null;
  bio?: string | null;
  location?: string | null;
  website?: string | null;
  jobTitle?: string | null;
  company?: string | null;
  timeZone?: string | null;
  language?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean;
  showLocation?: boolean;
}

export interface IdentityUsersUpdateUserAccessibilityPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserInput {
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersUpdateUserLocalizationPreferencesInput {
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserMetadataInput {
  customFields?: Record<string, Record<string, unknown>> | null;
  tagsToAdd?: Array<string> | null;
  tagsToRemove?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
}

export interface IdentityUsersUpdateUserNotificationPreferencesInput {
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserPreferencesInput {
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserPrivacyPreferencesInput {
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserProfileInput {
  displayName?: string | null;
  bio?: string | null;
  location?: string | null;
  website?: string | null;
  jobTitle?: string | null;
  company?: string | null;
  timeZone?: string | null;
  language?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean | null;
  showLocation?: boolean | null;
}

export interface IdentityUsersUpdateUserRequestItem {
  userId?: string;
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersUser {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  email: string;
  username?: string | null;
  name: string;
  isEmailVerified?: boolean;
  lastLoginAt?: string | null;
  isActive?: boolean;
  isSuspended?: boolean;
  tokenVersion?: number;
  status?: IdentityUsersUserStatus;
  phoneNumber?: string | null;
  lastSeenAt?: string | null;
  profile?: IdentityUsersUserProfile;
  metadata?: IdentityUsersUserMetadata;
  preferences?: IdentityUsersUserPreferences;
  notifications?: Array<IdentityUsersUserNotification> | null;
  tenantMemberships?: Array<IdentityTenantsTenantMember> | null;
  hasPassword?: boolean;
  canPerformActions?: boolean;
  canSignIn?: boolean;
}

export interface IdentityUsersUserAccessibilityPreferences {
  highContrast?: boolean;
  largeText?: boolean;
  screenReader?: boolean;
  reducedMotion?: boolean;
  keyboardNavigation?: boolean;
  fontSize?: number;
  colorScheme?: string | null;
  customSettings?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUserDto {
  id?: string;
  email?: string | null;
  name?: string | null;
  createdAt?: string;
  updatedAt?: string | null;
  isActive?: boolean;
  phoneNumber?: string | null;
  lastSeenAt?: string | null;
}

export interface IdentityUsersUserLocalizationPreferences {
  language?: string | null;
  timezone?: string | null;
  dateFormat?: string | null;
  timeFormat?: string | null;
  currency?: string | null;
  numberFormat?: Record<string, Record<string, unknown>> | null;
  customSettings?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUserMetadata {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  userId: string;
  user?: IdentityUsersUser;
  customFields?: string | null;
  tags?: string | null;
  externalReferences?: string | null;
  notes?: string | null;
}

export interface IdentityUsersUserMetadataDto {
  id?: string;
  userId?: string;
  customFields?: Record<string, Record<string, unknown>> | null;
  tags?: Array<string> | null;
  externalReferences?: Record<string, string> | null;
  createdAt?: string;
  updatedAt?: string | null;
  version?: string | null;
}

export interface IdentityUsersUserNotification {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  userId: string;
  user?: IdentityUsersUser;
  type: string;
  title: string;
  content: string;
  priority?: IdentityUsersNotificationPriority;
  isRead?: boolean;
  isArchived?: boolean;
  readAt?: string | null;
  archivedAt?: string | null;
  senderId?: string | null;
  source?: string | null;
  relatedEntityId?: string | null;
  relatedEntityType?: string | null;
  actionUrl?: string | null;
  metadata?: string | null;
}

export interface IdentityUsersUserNotificationDetail {
  notification?: IdentityUsersUserNotificationDto;
  relatedNotifications?: Array<IdentityUsersUserNotificationDto> | null;
  actions?: Array<IdentityUsersNotificationAction> | null;
}

export interface IdentityUsersUserNotificationDto {
  id?: string;
  userId?: string;
  type?: string | null;
  title?: string | null;
  message?: string | null;
  priority?: string | null;
  category?: string | null;
  isRead?: boolean;
  isArchived?: boolean;
  readAt?: string | null;
  archivedAt?: string | null;
  expiresAt?: string | null;
  actionUrl?: string | null;
  actionText?: string | null;
  imageUrl?: string | null;
  metadata?: Record<string, Record<string, unknown>> | null;
  createdAt?: string;
  updatedAt?: string | null;
  version?: string | null;
}

export interface IdentityUsersUserNotificationPreferences {
  emailEnabled?: boolean;
  pushEnabled?: boolean;
  smsEnabled?: boolean;
  inAppEnabled?: boolean;
  frequency?: string | null;
  quietHours?: Record<string, Record<string, unknown>> | null;
  categoryPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUserPreferences {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  userId: string;
  user?: IdentityUsersUser;
  generalPreferences?: string | null;
  notificationPreferences?: string | null;
  accessibilityPreferences?: string | null;
  privacyPreferences?: string | null;
  localizationPreferences?: string | null;
}

export interface IdentityUsersUserPreferencesDto {
  id?: string;
  userId?: string;
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
  createdAt?: string;
  updatedAt?: string | null;
  version?: string | null;
}

export interface IdentityUsersUserPrivacyPreferences {
  profileVisibility?: string | null;
  activityTracking?: boolean;
  dataCollection?: Record<string, Record<string, unknown>> | null;
  thirdPartySharing?: Record<string, Record<string, unknown>> | null;
  marketingEmails?: boolean;
  analyticsCookies?: boolean;
  personalizedContent?: boolean;
  customSettings?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUserProfile {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  userId: string;
  user?: IdentityUsersUser;
  displayName?: string | null;
  bio?: string | null;
  location?: string | null;
  website?: string | null;
  jobTitle?: string | null;
  company?: string | null;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  visibility?: IdentityUsersProfileVisibility;
  isVerified?: boolean;
}

export interface IdentityUsersUserProfileDto {
  id?: string;
  userId?: string;
  displayName?: string | null;
  bio?: string | null;
  location?: string | null;
  website?: string | null;
  jobTitle?: string | null;
  company?: string | null;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  timeZone?: string | null;
  language?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean;
  showLocation?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
  version?: string | null;
}

export interface IdentityUsersUserStatus {
  isActive?: boolean;
  isSuspended?: boolean;
}

export interface KeyValuePairStringAuthenticationExtensionsPRFValues {
  key?: string | null;
  value?: ObjectsAuthenticationExtensionsPRFValues;
}

export interface LaunchPadCreateLaunchPadEventInput {
  name?: string | null;
  description?: string | null;
  startsAt?: string;
  endsAt?: string;
  applicationsOpenAt?: string | null;
  applicationsCloseAt?: string | null;
}

export interface LaunchPadCreateLaunchPadSlotInput {
  name?: string | null;
  role?: LaunchPadLaunchPadParticipantRole;
  capacity?: number;
  startsAt?: string;
  endsAt?: string;
}

export interface LaunchPadCreateLaunchPlanInput {
  projectId?: string;
  name?: string | null;
  positioning?: string | null;
  targetLaunchAt?: string | null;
  channels?: Array<string> | null;
  checklistItems?: Array<LaunchPadLaunchChecklistItemInput> | null;
}

export interface LaunchPadLaunchChecklistItem {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  launchPlanId?: string;
  launchPlan?: LaunchPadLaunchPlan;
  title: string;
  category: string;
  isRequired?: boolean;
  isComplete?: boolean;
  completedAt?: string | null;
}

export interface LaunchPadLaunchChecklistItemInput {
  title?: string | null;
  category?: string | null;
  isComplete?: boolean;
  isRequired?: boolean;
}

export interface LaunchPadLaunchPadAnalyticsProjection {
  events?: number;
  completedEvents?: number;
  applications?: number;
  approvedApplications?: number;
  registrations?: number;
  completedRegistrations?: number;
}

export interface LaunchPadLaunchPadApplication {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  launchPadEventId?: string;
  launchPadEvent?: LaunchPadLaunchPadEvent;
  projectId?: string;
  project?: ProjectsProject;
  projectVersionId?: string;
  projectVersion?: ProjectsProjectVersion;
  submittedByUserId?: string;
  submittedByUser?: IdentityUsersUser;
  reviewedByUserId?: string | null;
  submittedAt?: string;
  reviewedAt?: string | null;
  status?: LaunchPadLaunchPadApplicationStatus;
  pitch?: string | null;
  submittedAssetReferenceIdsJson?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface LaunchPadLaunchPadApplicationProjection {
  id?: string;
  eventId?: string;
  projectId?: string;
  projectVersionId?: string;
  submittedByUserId?: string;
  status?: LaunchPadLaunchPadApplicationStatus;
  pitch?: string | null;
  submittedAt?: string;
  submittedAssetReferenceIds?: Array<string> | null;
}

export type LaunchPadLaunchPadApplicationStatus =
  | "Draft"
  | "Submitted"
  | "UnderReview"
  | "Waitlisted"
  | "Approved"
  | "Rejected"
  | "Withdrawn";

export interface LaunchPadLaunchPadEvent {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  description?: string | null;
  startsAt?: string;
  endsAt?: string;
  applicationsOpenAt?: string | null;
  applicationsCloseAt?: string | null;
  status?: LaunchPadLaunchPadEventStatus;
  applications?: Array<LaunchPadLaunchPadApplication> | null;
  slots?: Array<LaunchPadLaunchPadParticipantSlot> | null;
}

export interface LaunchPadLaunchPadEventDetailProjection {
  event?: LaunchPadLaunchPadEventProjection;
  slots?: Array<LaunchPadLaunchPadSlotProjection> | null;
}

export interface LaunchPadLaunchPadEventProjection {
  id?: string;
  name?: string | null;
  description?: string | null;
  startsAt?: string;
  endsAt?: string;
  status?: LaunchPadLaunchPadEventStatus;
  applicationsOpenAt?: string | null;
  applicationsCloseAt?: string | null;
}

export type LaunchPadLaunchPadEventStatus =
  | "Draft"
  | "ApplicationsOpen"
  | "ApplicationsClosed"
  | "Scheduled"
  | "Active"
  | "Completed"
  | "Cancelled"
  | "Archived";

export interface LaunchPadLaunchPadParticipantRegistration {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  launchPadParticipantSlotId?: string;
  launchPadParticipantSlot?: LaunchPadLaunchPadParticipantSlot;
  userId?: string;
  user?: IdentityUsersUser;
  status?: LaunchPadLaunchPadParticipantStatus;
  registeredAt?: string;
  checkedInAt?: string | null;
  completedAt?: string | null;
}

export type LaunchPadLaunchPadParticipantRole =
  "Participant" | "Mentor" | "Audience" | "Presenter";

export interface LaunchPadLaunchPadParticipantSlot {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  launchPadEventId?: string;
  launchPadEvent?: LaunchPadLaunchPadEvent;
  name: string;
  role?: LaunchPadLaunchPadParticipantRole;
  capacity?: number;
  reservedCount?: number;
  startsAt?: string;
  endsAt?: string;
  registrations?: Array<LaunchPadLaunchPadParticipantRegistration> | null;
  hasCapacity?: boolean;
}

export type LaunchPadLaunchPadParticipantStatus =
  | "Registered"
  | "Waitlisted"
  | "CheckedIn"
  | "Attended"
  | "Completed"
  | "Cancelled"
  | "NoShow";

export interface LaunchPadLaunchPadRegistrationProjection {
  id?: string;
  slotId?: string;
  userId?: string;
  status?: LaunchPadLaunchPadParticipantStatus;
  registeredAt?: string;
  checkedInAt?: string | null;
  completedAt?: string | null;
}

export interface LaunchPadLaunchPadSlotProjection {
  id?: string;
  eventId?: string;
  name?: string | null;
  role?: LaunchPadLaunchPadParticipantRole;
  capacity?: number;
  reservedCount?: number;
  startsAt?: string;
  endsAt?: string;
}

export interface LaunchPadLaunchPlan {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  launchPadEventId?: string | null;
  launchPadEvent?: LaunchPadLaunchPadEvent;
  launchPadApplicationId?: string | null;
  launchPadApplication?: LaunchPadLaunchPadApplication;
  projectVersionId?: string | null;
  projectVersion?: ProjectsProjectVersion;
  projectId?: string;
  project?: ProjectsProject;
  name: string;
  positioning?: string | null;
  targetLaunchAt?: string | null;
  launchedAt?: string | null;
  status?: LaunchPadLaunchPlanStatus;
  channels?: Array<string> | null;
  checklistItems?: Array<LaunchPadLaunchChecklistItem> | null;
  readinessPercent?: number;
}

export type LaunchPadLaunchPlanStatus =
  "Draft" | "Preparing" | "Ready" | "Launched" | "Paused";

export interface LaunchPadReviewLaunchPadApplicationInput {
  status?: LaunchPadLaunchPadApplicationStatus;
  launchPlanName?: string | null;
}

export interface LaunchPadSubmitLaunchPadApplicationInput {
  projectId?: string;
  projectVersionId?: string;
  pitch?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface LaunchPadTransitionLaunchPadEventInput {
  status?: LaunchPadLaunchPadEventStatus;
}

export interface LaunchPadTransitionLaunchPadRegistrationInput {
  status?: LaunchPadLaunchPadParticipantStatus;
}

export interface LaunchPadUpdateLaunchPadApplicationInput {
  projectVersionId?: string;
  pitch?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface LaunchPadUpdateLaunchPadEventInput {
  name?: string | null;
  description?: string | null;
  startsAt?: string;
  endsAt?: string;
  applicationsOpenAt?: string | null;
  applicationsCloseAt?: string | null;
}

export interface LearningAssessmentsAssessment {
  id?: string;
  courseId?: string;
  contentId?: string | null;
  title?: string | null;
  description?: string | null;
  type?: LearningAssessmentsAssessmentType;
  maxScore?: number;
  timeLimitMinutes?: number | null;
  maxAttempts?: number | null;
  isRequired?: boolean;
  order?: number;
  availableFrom?: string | null;
  availableUntil?: string | null;
  assessmentGroupId?: string | null;
  assessmentGroupName?: string | null;
  assessmentGroupWeightPercent?: number | null;
  assessmentGroupOrder?: number | null;
  isAvailable?: boolean;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  dueAt?: string | null;
  allowLateSubmissions?: boolean;
  lateSubmissionDeadline?: string | null;
  gradingMethods?: LearningAssessmentsAssessmentGradingMethod;
}

export interface LearningAssessmentsAssessmentDefinition {
  assessmentId?: string;
  definitionSchemaVersion?: number;
  definition?: Record<string, unknown>;
}

/** A comma-separated combination of the declared flag names. */
export type LearningAssessmentsAssessmentGradingMethod = string;

export interface LearningAssessmentsAssessmentGroup {
  id?: string;
  courseId?: string;
  name?: string | null;
  description?: string | null;
  weightPercent?: number;
  order?: number;
}

export interface LearningAssessmentsAssessmentGroupAnalytics {
  groupId?: string | null;
  groupName?: string | null;
  weightPercent?: number | null;
  assessmentCount?: number;
  gradedCount?: number;
  ungradedCount?: number;
  averagePercent?: number;
  passRate?: number;
  distribution?: Array<LearningAssessmentsAssessmentScoreBucket> | null;
}

export type LearningAssessmentsAssessmentPresentationMode =
  "SingleStep" | "Continuous";

export interface LearningAssessmentsAssessmentScoreBucket {
  label?: string | null;
  minPercent?: number;
  maxPercent?: number;
  count?: number;
}

export interface LearningAssessmentsAssessmentSubmission {
  id?: string;
  assessmentId?: string;
  enrollmentId?: string;
  userId?: string;
  attemptNumber?: number;
  score?: number | null;
  passed?: boolean | null;
  startedAt?: string;
  submittedAt?: string | null;
  gradedAt?: string | null;
  gradedBy?: string | null;
  feedback?: string | null;
  status?: LearningAssessmentsSubmissionStatus;
  isLate?: boolean;
  submittedModalities?: LearningAssessmentsSubmissionModality;
  textPayload?: string | null;
  filePayload?: string | null;
  urlPayload?: string | null;
  codePayload?: string | null;
  mediaPayload?: string | null;
  projectPayload?: string | null;
  structuredAnswerPayload?: string | null;
}

/** Legacy value Exam is normalized on read and is not valid for new assessments. */
export type LearningAssessmentsAssessmentType =
  "Quiz" | "Assignment" | "Project" | "PeerReview" | "SelfAssessment";

export interface LearningAssessmentsAssignAssessmentGroupInput {
  assessmentGroupId?: string | null;
  clearAssessmentGroup?: boolean;
}

export interface LearningAssessmentsCanAttemptOutput {
  canAttempt?: boolean;
  currentAttemptCount?: number;
}

export interface LearningAssessmentsCourseAssessmentAnalytics {
  courseId?: string;
  assessmentCount?: number;
  gradedCount?: number;
  ungradedCount?: number;
  averagePercent?: number;
  passRate?: number;
  distribution?: Array<LearningAssessmentsAssessmentScoreBucket> | null;
  groups?: Array<LearningAssessmentsAssessmentGroupAnalytics> | null;
}

export interface LearningAssessmentsCreateAssessmentGroupInput {
  courseId?: string;
  name?: string | null;
  weightPercent?: number;
  order?: number;
  description?: string | null;
}

export interface LearningAssessmentsCreateAssessmentInput {
  courseId?: string;
  title?: string | null;
  description?: string | null;
  type?: LearningAssessmentsAssessmentType;
  maxScore?: number;
  timeLimitMinutes?: number | null;
  maxAttempts?: number | null;
  isRequired?: boolean;
  availableFrom?: string | null;
  availableUntil?: string | null;
  assessmentGroupId?: string | null;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  dueAt?: string | null;
  allowLateSubmissions?: boolean;
  lateSubmissionDeadline?: string | null;
  contentId?: string | null;
  gradingMethods?: LearningAssessmentsAssessmentGradingMethod;
}

export interface LearningAssessmentsGradeSubmissionInput {
  score?: number;
  gradedBy?: string | null;
  feedback?: string | null;
}

export interface LearningAssessmentsInteractiveVideoAssessmentCue {
  id?: string;
  assessmentId?: string;
  contentId?: string;
  cueId?: string | null;
  cuePositionSeconds?: number | null;
}

export interface LearningAssessmentsLearnerAssessmentAttempt {
  submission?: LearningAssessmentsLearnerAssessmentSubmission;
}

export interface LearningAssessmentsLearnerAssessmentSubmission {
  id?: string;
  assessmentId?: string;
  enrollmentId?: string;
  attemptNumber?: number;
  score?: number | null;
  passed?: boolean | null;
  startedAt?: string;
  submittedAt?: string | null;
  gradedAt?: string | null;
  feedback?: string | null;
  status?: LearningAssessmentsSubmissionStatus;
  isLate?: boolean;
  submittedModalities?: LearningAssessmentsSubmissionModality;
  textPayload?: string | null;
  filePayload?: string | null;
  urlPayload?: string | null;
  codePayload?: string | null;
  mediaPayload?: string | null;
  projectPayload?: string | null;
  structuredAnswerPayload?: string | null;
}

export interface LearningAssessmentsLearnerInteractiveVideoAssessmentCue {
  cueId?: string | null;
  cuePositionSeconds?: number | null;
}

export interface LearningAssessmentsLinkInteractiveVideoCueInput {
  contentId?: string;
  cueId?: string | null;
  cuePositionSeconds?: number | null;
}

export interface LearningAssessmentsStartSubmissionInput {
  enrollmentId?: string;
}

/** A comma-separated combination of the declared flag names. */
export type LearningAssessmentsSubmissionModality = string;

export type LearningAssessmentsSubmissionStatus =
  "InProgress" | "Submitted" | "Graded" | "Returned" | "Late";

export interface LearningAssessmentsSubmitAssessmentInput {
  textPayload?: string | null;
  filePayload?: string | null;
  urlPayload?: string | null;
  codePayload?: string | null;
  mediaPayload?: string | null;
  projectPayload?: string | null;
  structuredAnswerPayload?: string | null;
}

export interface LearningAssessmentsUpdateAssessmentGroupInput {
  name?: string | null;
  description?: string | null;
  weightPercent?: number | null;
  order?: number | null;
}

export interface LearningAssessmentsUpdateAssessmentInput {
  title?: string | null;
  description?: string | null;
  maxScore?: number | null;
  timeLimitMinutes?: number | null;
  maxAttempts?: number | null;
  isRequired?: boolean | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  clearContentId?: boolean;
  assessmentGroupId?: string | null;
  clearAssessmentGroupId?: boolean;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  dueAt?: string | null;
  clearDueAt?: boolean;
  allowLateSubmissions?: boolean | null;
  lateSubmissionDeadline?: string | null;
  clearLateSubmissionDeadline?: boolean;
  gradingMethods?: LearningAssessmentsAssessmentGradingMethod;
}

export interface LearningCertificatesCertificate {
  id?: string;
  templateId?: string;
  enrollmentId?: string;
  userId?: string;
  courseId?: string;
  certificateNumber?: string | null;
  recipientName?: string | null;
  courseName?: string | null;
  issuedAt?: string;
  expiresAt?: string | null;
  status?: LearningCertificatesCertificateStatus;
}

export type LearningCertificatesCertificateStatus =
  "Active" | "Expired" | "Revoked";

export interface LearningCertificatesCertificateTemplate {
  id?: string;
  courseId?: string;
  tenantId?: string | null;
  name?: string | null;
  description?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface LearningCertificatesCertificateTemplateDetail {
  id?: string;
  courseId?: string;
  tenantId?: string | null;
  name?: string | null;
  description?: string | null;
  templateHtml?: string | null;
  templateStyles?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
  createdAt?: string;
  updatedAt?: string;
}

export interface LearningCertificatesCertificateVerificationResult {
  isValid?: boolean;
  certificateNumber?: string | null;
  recipientName?: string | null;
  courseName?: string | null;
  issuedAt?: string;
  expiresAt?: string | null;
  status?: LearningCertificatesCertificateStatus;
  message?: string | null;
}

export interface LearningCertificatesCreateCertificateTemplateInput {
  courseId?: string;
  name?: string | null;
  templateHtml?: string | null;
}

export interface LearningCertificatesIssueCertificateInput {
  templateId?: string;
  enrollmentId?: string;
  userId?: string;
  courseId?: string;
}

export interface LearningCertificatesRevokeCertificateInput {
  reason?: string | null;
}

export interface LearningCertificatesUpdateCertificateTemplateInput {
  name?: string | null;
  description?: string | null;
  templateHtml?: string | null;
  templateStyles?: string | null;
  isDefault?: boolean;
  isActive?: boolean;
}

export interface LearningCohortsApplyCohortScheduleInput {
  expectedVersion?: number;
  rules?: LearningCohortsPreviewCohortScheduleInput;
  confirmAdvisories?: boolean;
}

export interface LearningCohortsAvailableCohortContent {
  contentId?: string;
  parentId?: string | null;
  title?: string | null;
  description?: string | null;
  body?: string | null;
  type?: LearningCoursesProgramContentType;
  sortOrder?: number;
  instructionalWeek?: number;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
}

export interface LearningCohortsCohort {
  id?: string;
  courseId?: string;
  tenantId?: string | null;
  name?: string | null;
  description?: string | null;
  startDate?: string;
  endDate?: string;
  maxCapacity?: number;
  currentEnrollmentCount?: number;
  availableSpots?: number;
  status?: LearningCohortsCohortStatus;
  isOpen?: boolean;
  canEnroll?: boolean;
  instructorId?: string | null;
  meetingSchedule?: string | null;
  createdAt?: string;
  nextMeetingAt?: string | null;
  conflictCount?: number;
  schedule?: LearningCohortsCohortScheduleSummary;
}

export interface LearningCohortsCohortCalendarEntry {
  cohortId?: string;
  cohortName?: string | null;
  itemId?: string;
  type?: LearningCohortsCohortScheduleItemType;
  title?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
  availableFrom?: string | null;
  dueAt?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
}

export type LearningCohortsCohortPacingMode =
  "OneModulePerWeek" | "OneLessonPerMeeting" | "FixedLessonsPerWeek" | "Manual";

export type LearningCohortsCohortReleasePolicy =
  "Weekly" | "BeforeMeeting" | "Manual" | "Immediately";

export interface LearningCohortsCohortSchedule {
  id?: string;
  cohortId?: string;
  version?: number;
  timezoneId?: string | null;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingStartTime?: string;
  meetingDurationMinutes?: number;
  pacingMode?: LearningCohortsCohortPacingMode;
  unitsPerPeriod?: number;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  items?: Array<LearningCohortsCohortScheduleItem> | null;
  unscheduledContentIds?: Array<string> | null;
}

export interface LearningCohortsCohortScheduleConflict {
  code?: string | null;
  severity?: LearningCohortsScheduleConflictSeverity;
  message?: string | null;
  programContentId?: string | null;
  assessmentId?: string | null;
}

export interface LearningCohortsCohortScheduleItem {
  id?: string;
  programContentId?: string | null;
  assessmentId?: string | null;
  type?: LearningCohortsCohortScheduleItemType;
  instructionalWeek?: number;
  sortOrder?: number;
  startsAt?: string | null;
  endsAt?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  title?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
  visibilityOverride?: LearningCohortsCohortVisibilityOverride;
}

export type LearningCohortsCohortScheduleItemStatus =
  "Draft" | "Scheduled" | "Published" | "Completed" | "Cancelled";

export type LearningCohortsCohortScheduleItemType =
  "ContentRelease" | "LiveSession" | "AssessmentWindow" | "Milestone";

export interface LearningCohortsCohortSchedulePreview {
  items?: Array<LearningCohortsCohortSchedulePreviewItem> | null;
  conflicts?: Array<LearningCohortsCohortScheduleConflict> | null;
  calculatedEndDate?: string;
  hasBlockingConflicts?: boolean;
}

export interface LearningCohortsCohortSchedulePreviewItem {
  programContentId?: string | null;
  assessmentId?: string | null;
  type?: LearningCohortsCohortScheduleItemType;
  instructionalWeek?: number;
  sortOrder?: number;
  startsAt?: string | null;
  endsAt?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  title?: string | null;
}

export interface LearningCohortsCohortScheduleSummary {
  version?: number;
  timezoneId?: string | null;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingStartTime?: string;
  pacingMode?: LearningCohortsCohortPacingMode;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  itemCount?: number;
}

export type LearningCohortsCohortStatus =
  "Scheduled" | "Active" | "Completed" | "Cancelled";

export type LearningCohortsCohortVisibilityOverride =
  "Inherited" | "Hidden" | "Visible";

export interface LearningCohortsCourseCohortCalendar {
  courseId?: string;
  entries?: Array<LearningCohortsCohortCalendarEntry> | null;
}

export interface LearningCohortsCreateCohortInput {
  courseId?: string;
  name?: string | null;
  startDate?: string;
  endDate?: string;
  maxCapacity?: number;
  description?: string | null;
  tenantId?: string | null;
  instructorId?: string | null;
  meetingSchedule?: string | null;
}

export interface LearningCohortsPreviewCohortScheduleInput {
  firstInstructionalDate?: string;
  cohortEndDate?: string;
  timezoneId?: string | null;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingStartTime?: string;
  meetingDurationMinutes?: number;
  pacingMode?: LearningCohortsCohortPacingMode;
  unitsPerPeriod?: number;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  skippedDates?: Array<string> | null;
  assessmentDueOffsetDays?: number;
}

export type LearningCohortsScheduleConflictSeverity = "Advisory" | "Blocking";

export type LearningCohortsScheduleShiftScope = "Single" | "Following";

export interface LearningCohortsShiftCohortScheduleInput {
  expectedVersion?: number;
  days?: number;
  scope?: LearningCohortsScheduleShiftScope;
}

export interface LearningCohortsUpdateCohortInput {
  name?: string | null;
  description?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  maxCapacity?: number | null;
  instructorId?: string | null;
  meetingSchedule?: string | null;
}

export interface LearningCohortsUpdateCohortScheduleInput {
  expectedVersion?: number;
  item?: LearningCohortsUpdateCohortScheduleItemInput;
}

export interface LearningCohortsUpdateCohortScheduleItemInput {
  title?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
  visibilityOverride?: LearningCohortsCohortVisibilityOverride;
}

export interface LearningCoursesActivityGrade {
  id?: string;
  contentInteractionId?: string;
  graderProgramUserId?: string | null;
  grade?: number;
  feedback?: string | null;
  gradingDetails?: string | null;
  gradedAt?: string;
  createdAt?: string;
  updatedAt?: string;
  contentInteraction?: LearningCoursesContentInteractionSummary;
  grader?: LearningCoursesGraderSummary;
  isPassingGrade?: boolean;
  gradePercentage?: string | null;
  hasFeedback?: boolean;
  hasGradingDetails?: boolean;
}

export interface LearningCoursesActivitySettings {}

export interface LearningCoursesBundleFileMeta {
  content: string | null;
  encoding?: string | null;
  visibility?: string | null;
  modifiable?: boolean;
}

export interface LearningCoursesCircularDependencyCheckResult {
  wouldCreateCycle?: boolean;
}

export interface LearningCoursesCloneProgram {
  newTitle?: string | null;
  newDescription?: string | null;
}

export interface LearningCoursesCodingAssignmentContent {
  type?: string | null;
  version?: number;
  environment: LearningCoursesCodingEnvironment;
  data: LearningCoursesWorkspaceData;
  tests: LearningCoursesTestSuite;
  grading: LearningCoursesGradingConfig;
}

export interface LearningCoursesCodingEnvironment {
  language: string | null;
  tools: string | null;
  libBundle?: string | null;
  allowStudentCreateFiles?: boolean;
}

export interface LearningCoursesCompleteContentInput {
  programUserId?: string;
  contentId?: string;
}

export interface LearningCoursesCompleteCourseCheckoutInput {
  productId?: string;
  paymentProviderReference?: string | null;
  paymentMethod?: string | null;
}

export interface LearningCoursesCompleteCourseCheckoutOutput {
  courseId?: string;
  productId?: string;
  entitlementId?: string;
  enrollmentIds?: Array<string> | null;
  alreadyHadAccess?: boolean;
  amount?: number;
  currency?: string | null;
  learningUrl?: string | null;
  paymentProviderReference?: string | null;
}

export interface LearningCoursesCompletionRates {
  programId?: string;
  overallCompletionRate?: number;
  contentCompletionRates?: Record<string, number> | null;
  completionTrends?: Array<LearningCoursesCompletionTrend> | null;
}

export interface LearningCoursesCompletionTrend {
  date?: string;
  completedCount?: number;
  totalCount?: number;
  rate?: number;
}

export interface LearningCoursesContentInteraction {
  id?: string;
  programUserId?: string;
  contentId?: string;
  status?: LearningCoursesProgressStatus;
  submissionData?: string | null;
  completionPercentage?: number;
  timeSpentMinutes?: number | null;
  timeSpentSeconds?: number;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  completedAt?: string | null;
  submittedAt?: string | null;
  createdAt?: string;
  updatedAt?: string;
  content?: LearningCoursesContentSummary;
  programUser?: LearningCoursesProgramUserSummary;
  isSubmitted?: boolean;
  isCompleted?: boolean;
  canModify?: boolean;
  durationInMinutes?: number;
  durationInSeconds?: number;
}

export interface LearningCoursesContentInteractionEvent {
  id?: string;
  interactionId?: string;
  type?: LearningCoursesContentInteractionEventType;
  occurredAt?: string;
  durationSeconds?: number | null;
  positionSeconds?: number | null;
  progressPercentage?: number | null;
  payload?: string | null;
  idempotencyKey?: string | null;
}

export type LearningCoursesContentInteractionEventType =
  | "Opened"
  | "Heartbeat"
  | "Progressed"
  | "Paused"
  | "Resumed"
  | "Seeked"
  | "Completed"
  | "QuizPresented"
  | "QuizAnswered";

export interface LearningCoursesContentInteractionSummary {
  id?: string;
  programUserId?: string;
  contentId?: string;
  status?: string | null;
  submittedAt?: string | null;
  content?: LearningCoursesContentSummary;
  student?: LearningCoursesStudentSummary;
}

export interface LearningCoursesContentProgress {
  contentId?: string;
  title?: string | null;
  status?: LearningCoursesProgressStatus;
  completionPercentage?: number;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  completedAt?: string | null;
}

export interface LearningCoursesContentStats {
  programId?: string;
  totalContent?: number;
  requiredContent?: number;
  optionalContent?: number;
  contentByType?: {
    Lesson?: number;
    Page?: number;
    Assignment?: number;
    Questionnaire?: number;
    Discussion?: number;
    Code?: number;
    Challenge?: number;
    Reflection?: number;
    Survey?: number;
    Project?: number;
    Module?: number;
  } | null;
  contentByVisibility?: {
    Public?: number;
    Internal?: number;
    Private?: number;
    Restricted?: number;
  } | null;
  topLevelContent?: number;
  nestedContent?: number;
}

export interface LearningCoursesContentSummary {
  id?: string;
  title?: string | null;
  contentType?: string | null;
  estimatedMinutes?: number | null;
}

export interface LearningCoursesCourseSupportTicketMessageInput {
  message?: string | null;
  isInternal?: boolean;
}

export interface LearningCoursesCreateActivityGrade {
  contentInteractionId?: string;
  graderProgramUserId?: string;
  grade?: number;
  feedback?: string | null;
  gradingDetails?: string | null;
}

export interface LearningCoursesCreatePrerequisiteApiInput {
  courseId?: string;
  prerequisiteCourseId?: string;
  type?: LearningCoursesPrerequisiteType;
  minimumGrade?: number | null;
  description?: string | null;
  displayOrder?: number;
  prerequisiteGroup?: string | null;
}

export interface LearningCoursesCreateProductFromProgram {
  name?: string | null;
  description?: string | null;
  basePrice?: number;
  currency?: string | null;
}

export interface LearningCoursesCreateProgram {
  title?: string | null;
  description?: string | null;
  slug?: string | null;
  thumbnail?: string | null;
  creatorId?: string | null;
  passingScore?: number;
}

export interface LearningCoursesCreateProgramContent {
  programId: string;
  parentId?: string | null;
  title: string;
  description?: string | null;
  type: LearningCoursesProgramContentType;
  body?: string | null;
  jsonBody?: Record<string, unknown> | null;
  lessonFormat?: LearningCoursesLessonContentFormat;
  activitySettings?: LearningCoursesActivitySettings;
  sortOrder?: number;
  isRequired?: boolean;
  estimatedMinutes?: number | null;
  visibility?: LearningCoursesVisibility;
}

export interface LearningCoursesEngagementMetrics {
  programId?: string;
  dailyActiveUsers?: number;
  weeklyActiveUsers?: number;
  monthlyActiveUsers?: number;
  averageSessionDuration?: string;
  totalSessions?: number;
  retentionRate?: number;
  contentEngagement?: Record<string, number> | null;
}

export type LearningCoursesEnrollmentStatus =
  | "Open"
  | "Active"
  | "Paused"
  | "Cancelled"
  | "Expired"
  | "Completed"
  | "Closed"
  | "InviteOnly"
  | "Waitlist";

export interface LearningCoursesGraderSummary {
  id?: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
  role?: string | null;
}

export interface LearningCoursesGradeStatistics {
  totalGrades?: number;
  averageGrade?: number;
  minGrade?: number;
  maxGrade?: number;
  passingRate?: number;
  averageGradeFormatted?: string | null;
  passingRateFormatted?: string | null;
  hasGrades?: boolean;
}

export interface LearningCoursesGradingConfig {
  maxScore?: number;
}

export type LearningCoursesLessonContentFormat =
  "Markdown" | "Lexical" | "RevealJs" | "Video" | "Html" | "ExternalLink";

export interface LearningCoursesMonetization {
  price?: number;
  currency?: string | null;
  isSubscription?: boolean;
  subscriptionDurationDays?: number | null;
}

export interface LearningCoursesMoveContent {
  contentId: string;
  newParentId?: string | null;
  newSortOrder: number;
}

export interface LearningCoursesPrerequisite {
  id?: string;
  courseId?: string;
  prerequisiteCourseId?: string;
  prerequisiteCourseName?: string | null;
  tenantId?: string | null;
  type?: LearningCoursesPrerequisiteType;
  minimumGrade?: number | null;
  description?: string | null;
  displayOrder?: number;
  prerequisiteGroup?: string | null;
  createdAt?: string;
}

export interface LearningCoursesPrerequisiteCheckResult {
  isSatisfied?: boolean;
  prerequisites?: Array<LearningCoursesPrerequisiteStatus> | null;
}

export interface LearningCoursesPrerequisiteStatus {
  prerequisiteId?: string;
  prerequisiteCourseId?: string;
  courseName?: string | null;
  type?: LearningCoursesPrerequisiteType;
  isSatisfied?: boolean;
  requiredGrade?: number | null;
  achievedGrade?: number | null;
  reason?: string | null;
}

export type LearningCoursesPrerequisiteType =
  "Required" | "Recommended" | "Corequisite";

export interface LearningCoursesPricing {
  price?: number;
  currency?: string | null;
  isSubscription?: boolean;
  subscriptionDurationDays?: number | null;
  isMonetizationEnabled?: boolean;
}

export interface LearningCoursesProgram {
  id?: string;
  creatorId?: string | null;
  title?: string | null;
  description?: string | null;
  metadata?: string | null;
  visibility?: ContentVisibility;
  slug?: string | null;
  status?: ContentStatus;
  thumbnail?: string | null;
  videoShowcaseUrl?: string | null;
  estimatedHours?: number | null;
  passingScore?: number;
  enrollmentStatus?: LearningCoursesEnrollmentStatus;
  maxEnrollments?: number | null;
  enrollmentDeadline?: string | null;
  category?: ProgramCategory;
  difficulty?: LearningCoursesProgramDifficulty;
  skillsRequired?: string | null;
  skillsProvided?: string | null;
  currentEnrollments?: number;
  averageRating?: number;
  totalRatings?: number;
  isEnrollmentOpen?: boolean;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface LearningCoursesProgramAnalytics {
  programId?: string;
  title?: string | null;
  totalUsers?: number;
  activeUsers?: number;
  completedUsers?: number;
  completionRate?: number;
  averageCompletionTime?: string;
  totalViews?: number;
  lastActivity?: string | null;
  additionalMetrics?: Record<string, Record<string, unknown>> | null;
}

export interface LearningCoursesProgramContent {
  id?: string;
  programId?: string;
  parentId?: string | null;
  title?: string | null;
  description?: string | null;
  type?: LearningCoursesProgramContentType;
  body?: string | null;
  jsonBody?: Record<string, unknown> | null;
  lessonFormat?: LearningCoursesLessonContentFormat;
  activitySettings?: LearningCoursesActivitySettings;
  sortOrder?: number;
  isRequired?: boolean;
  estimatedMinutes?: number | null;
  visibility?: LearningCoursesVisibility;
  createdAt?: string;
  updatedAt?: string | null;
  programTitle?: string | null;
  parentTitle?: string | null;
  childrenCount?: number;
  children?: Array<LearningCoursesProgramContent> | null;
}

/** Legacy values Page and Challenge are normalized on read and are not valid for new content. */
export type LearningCoursesProgramContentType =
  | "Lesson"
  | "Assignment"
  | "Questionnaire"
  | "Discussion"
  | "Code"
  | "Reflection"
  | "Survey"
  | "Project"
  | "Module";

export type LearningCoursesProgramDifficulty =
  "Beginner" | "Intermediate" | "Advanced" | "Expert";

export interface LearningCoursesProgramUserSummary {
  id?: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
}

export type LearningCoursesProgressStatus =
  "NotStarted" | "InProgress" | "Completed" | "Submitted";

export interface LearningCoursesRecordContentInteractionEventInput {
  type?: LearningCoursesContentInteractionEventType;
  durationSeconds?: number | null;
  positionSeconds?: number | null;
  progressPercentage?: number | null;
  payload?: string | null;
  idempotencyKey?: string | null;
  occurredAt?: string | null;
}

export interface LearningCoursesReflectionResponseResult {
  responseId?: string;
  submittedAt?: string | null;
  body?: string | null;
  respondentUserId?: string | null;
}

export interface LearningCoursesRejectProgram {
  reason?: string | null;
}

export interface LearningCoursesReorderContent {
  contentIds?: Array<string> | null;
}

export interface LearningCoursesReorderPrerequisitesInput {
  prerequisiteIds?: Array<string> | null;
}

export interface LearningCoursesResolveCourseSupportTicketInput {
  summary?: string | null;
}

export interface LearningCoursesRevenueAnalytics {
  programId?: string;
  totalRevenue?: number;
  monthlyRevenue?: number;
  totalPurchases?: number;
  monthlyPurchases?: number;
  averageRevenuePerUser?: number;
  conversionRate?: number;
  revenueChart?: Array<LearningCoursesRevenueChart> | null;
}

export interface LearningCoursesRevenueChart {
  date?: string;
  revenue?: number;
  purchases?: number;
}

export interface LearningCoursesScheduleProgram {
  publishAt?: string;
}

export interface LearningCoursesSearchContent {
  programId: string;
  searchTerm: string;
  type?: LearningCoursesProgramContentType;
  visibility?: LearningCoursesVisibility;
  isRequired?: boolean | null;
  parentId?: string | null;
}

export interface LearningCoursesSendCourseStudentMessageInput {
  userIds?: Array<string> | null;
  subject?: string | null;
  message?: string | null;
}

export interface LearningCoursesSendCourseStudentMessageOutput {
  sent?: number;
}

export interface LearningCoursesStartContentInput {
  programUserId?: string;
  contentId?: string;
}

export interface LearningCoursesStudentSummary {
  id?: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
}

export interface LearningCoursesSubmitContentInput {
  programUserId?: string;
  contentId?: string;
  submissionData?: string | null;
}

export interface LearningCoursesSubmitUserContent {
  submissionData: string;
}

export interface LearningCoursesSurveyResponseResult {
  responseId?: string;
  submittedAt?: string | null;
  answers?: Record<string, Record<string, unknown>> | null;
  respondentUserId?: string | null;
}

export interface LearningCoursesTest {
  weight?: number;
  name?: string | null;
}

export interface LearningCoursesTestSuite {
  public?: Array<LearningCoursesTest> | null;
  private?: Array<LearningCoursesTest> | null;
}

export interface LearningCoursesUpdateActivityGrade {
  grade?: number | null;
  feedback?: string | null;
  gradingDetails?: string | null;
}

export interface LearningCoursesUpdatePrerequisiteApiInput {
  type?: LearningCoursesPrerequisiteType;
  minimumGrade?: number | null;
  description?: string | null;
  displayOrder?: number | null;
  prerequisiteGroup?: string | null;
}

export interface LearningCoursesUpdatePricing {
  price?: number | null;
  currency?: string | null;
  isSubscription?: boolean | null;
  subscriptionDurationDays?: number | null;
}

export interface LearningCoursesUpdateProgram {
  title?: string | null;
  description?: string | null;
  metadata?: string | null;
  slug?: string | null;
  thumbnail?: string | null;
  videoShowcaseUrl?: string | null;
  estimatedHours?: number | null;
  visibility?: ContentVisibility;
  category?: ProgramCategory;
  difficulty?: LearningCoursesProgramDifficulty;
  skillsRequired?: string | null;
  skillsProvided?: string | null;
  creatorId?: string | null;
  enrollmentStatus?: LearningCoursesEnrollmentStatus;
  maxEnrollments?: number | null;
  enrollmentDeadline?: string | null;
  passingScore?: number | null;
  clearMaxEnrollments?: boolean;
  clearEnrollmentDeadline?: boolean;
}

export interface LearningCoursesUpdateProgramContent {
  id: string;
  title?: string | null;
  description?: string | null;
  type?: LearningCoursesProgramContentType;
  body?: string | null;
  jsonBody?: Record<string, unknown> | null;
  lessonFormat?: LearningCoursesLessonContentFormat;
  activitySettings?: LearningCoursesActivitySettings;
  sortOrder?: number | null;
  isRequired?: boolean | null;
  estimatedMinutes?: number | null;
  visibility?: LearningCoursesVisibility;
}

export interface LearningCoursesUpdateProgress {
  status?: LearningCoursesProgressStatus;
  lastAccessedAt?: string | null;
  additionalData?: Record<string, Record<string, unknown>> | null;
}

export interface LearningCoursesUpdateProgressInput {
  programUserId?: string;
  contentId?: string;
  completionPercentage?: number;
}

export interface LearningCoursesUpdateTimeSpentInput {
  programUserId?: string;
  contentId?: string;
  additionalMinutes?: number;
}

export interface LearningCoursesUserProgress {
  enrollmentId?: string;
  courseId?: string;
  userId?: string;
  completionPercentage?: number;
  lastAccessedAt?: string | null;
  startedAt?: string | null;
  completedAt?: string | null;
  contentProgress?: Array<LearningCoursesContentProgress> | null;
}

export type LearningCoursesVisibility =
  "Public" | "Internal" | "Private" | "Restricted";

export interface LearningCoursesWorkspaceData {
  files?: Record<string, LearningCoursesBundleFileMeta> | null;
}

export interface LearningEnrollmentsEnrollment {
  id?: string;
  courseId?: string;
  userId?: string;
  cohortId?: string | null;
  status?: LearningEnrollmentsEnrollmentStatus;
  enrolledAt?: string;
  completedAt?: string | null;
  droppedAt?: string | null;
  progress?: number;
  lastActivityAt?: string | null;
}

export type LearningEnrollmentsEnrollmentStatus =
  "Active" | "Paused" | "Completed" | "Dropped" | "Expired";

export interface LearningEnrollmentsEnrollUserInput {
  courseId?: string;
  userId?: string;
  cohortId?: string | null;
}

export interface LearningEnrollmentsUpdateEnrollmentProgressInput {
  progress?: number;
}

export type LearningExperienceDiscoveryCollectionType =
  "Curated" | "Category" | "Skill" | "Career" | "Trending" | "NewReleases";

export interface LearningExperienceDiscoveryCourseCollection {
  id?: string;
  tenantId?: string | null;
  curatorId?: string;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  isPublished?: boolean;
  isFeatured?: boolean;
  courseCount?: number;
  type?: LearningExperienceDiscoveryCollectionType;
  createdAt?: string;
  updatedAt?: string;
}

export interface LearningExperienceDiscoveryCreateCourseCollection {
  title?: string | null;
  type?: LearningExperienceDiscoveryCollectionType;
  description?: string | null;
  imageUrl?: string | null;
}

export interface LearningExperienceDiscoveryCreateFeaturedContent {
  type?: LearningExperienceDiscoveryFeaturedContentType;
  title?: string | null;
  displayOrder?: number;
  courseId?: string | null;
  learningPathId?: string | null;
  subtitle?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
  targetAudience?: string | null;
}

export interface LearningExperienceDiscoveryFeaturedContent {
  id?: string;
  courseId?: string | null;
  learningPathId?: string | null;
  tenantId?: string | null;
  title?: string | null;
  subtitle?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  type?: LearningExperienceDiscoveryFeaturedContentType;
  displayOrder?: number;
  startsAt?: string | null;
  endsAt?: string | null;
  isActive?: boolean;
  targetAudience?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export type LearningExperienceDiscoveryFeaturedContentType =
  | "HeroBanner"
  | "CategoryHighlight"
  | "NewRelease"
  | "TopRated"
  | "TrendingNow"
  | "StaffPick"
  | "SeasonalPromotion";

export interface LearningExperienceDiscoveryPopularSearchResult {
  query?: string | null;
  searchCount?: number;
  totalClicks?: number;
  clickThroughRate?: number;
}

export interface LearningExperienceDiscoveryRecordSearch {
  query?: string | null;
  resultCount?: number;
  filters?: string | null;
}

export interface LearningExperienceDiscoveryRecordSearchClick {
  searchId?: string;
  clickedCourseId?: string;
}

export interface LearningExperienceDiscoverySearchHistory {
  id?: string;
  userId?: string | null;
  query?: string | null;
  resultCount?: number;
  clickedCourseId?: string | null;
  filters?: string | null;
  createdAt?: string;
}

export interface LearningExperienceDiscoveryUpdateCourseCollection {
  title?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  isFeatured?: boolean | null;
}

export interface LearningExperienceDiscoveryUpdateFeaturedContent {
  title?: string | null;
  subtitle?: string | null;
  imageUrl?: string | null;
  linkUrl?: string | null;
  displayOrder?: number | null;
  startsAt?: string | null;
  endsAt?: string | null;
  isActive?: boolean | null;
  targetAudience?: string | null;
}

export interface LearningExperienceLearningPathsAddCourseToPath {
  courseId?: string;
  order?: number;
  isRequired?: boolean;
}

export interface LearningExperienceLearningPathsCourseOrder {
  courseId?: string;
  order?: number;
}

export interface LearningExperienceLearningPathsCreateLearningPath {
  title?: string | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  description?: string | null;
  imageUrl?: string | null;
  estimatedHours?: number;
}

export interface LearningExperienceLearningPathsLearningPath {
  id?: string;
  tenantId?: string | null;
  creatorId?: string;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  estimatedHours?: number;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  isPublished?: boolean;
  isFeatured?: boolean;
  enrollmentCount?: number;
  completionCount?: number;
  courseCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface LearningExperienceLearningPathsLearningPathCourse {
  courseId?: string;
  order?: number;
  isRequired?: boolean;
}

export interface LearningExperienceLearningPathsLearningPathDetail {
  id?: string;
  tenantId?: string | null;
  creatorId?: string;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  estimatedHours?: number;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  isPublished?: boolean;
  isFeatured?: boolean;
  enrollmentCount?: number;
  completionCount?: number;
  courses?: Array<LearningExperienceLearningPathsLearningPathCourse> | null;
  createdAt?: string;
  updatedAt?: string;
}

export type LearningExperienceLearningPathsLearningPathDifficulty =
  "Beginner" | "Intermediate" | "Advanced" | "Expert";

export interface LearningExperienceLearningPathsLearningPathEnrollment {
  id?: string;
  learningPathId?: string;
  userId?: string;
  progress?: number;
  coursesCompleted?: number;
  totalCourses?: number;
  enrolledAt?: string;
  completedAt?: string | null;
  status?: LearningExperienceLearningPathsLearningPathEnrollmentStatus;
  createdAt?: string;
  updatedAt?: string;
}

export type LearningExperienceLearningPathsLearningPathEnrollmentStatus =
  "InProgress" | "Completed" | "Abandoned";

export interface LearningExperienceLearningPathsLearningPathStatistics {
  learningPathId?: string;
  totalEnrollments?: number;
  activeEnrollments?: number;
  completedEnrollments?: number;
  completionRate?: number;
  averageProgress?: number;
  averageCompletionTime?: string;
}

export interface LearningExperienceLearningPathsReorderCourses {
  courses?: Array<LearningExperienceLearningPathsCourseOrder> | null;
}

export interface LearningExperienceLearningPathsUpdateLearningPath {
  title?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  estimatedHours?: number | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  isFeatured?: boolean | null;
}

export interface LearningExperienceLearningPathsUpdatePathProgress {
  coursesCompleted?: number;
}

export interface LearningExperienceRecommendationsAddSkillInput {
  skill?: string | null;
}

export interface LearningExperienceRecommendationsCreateOrUpdateLearningProfile {
  preferredCategories?: Array<string> | null;
  preferredDifficulty?: string | null;
  preferredDuration?: string | null;
  learningGoals?: Array<string> | null;
  skills?: Array<string> | null;
}

export interface LearningExperienceRecommendationsPopularCourse {
  courseId?: string;
  title?: string | null;
  description?: string | null;
  thumbnail?: string | null;
  category?: string | null;
  enrollmentCount?: number;
  averageRating?: number;
  totalRatings?: number;
}

export interface LearningExperienceRecommendationsRecommendation {
  id?: string;
  userId?: string;
  courseId?: string;
  type?: LearningExperienceRecommendationsRecommendationType;
  score?: number;
  reason?: string | null;
  isViewed?: boolean;
  isDismissed?: boolean;
  expiresAt?: string;
  createdAt?: string;
}

export interface LearningExperienceRecommendationsRecommendationStatistics {
  totalRecommendations?: number;
  viewedCount?: number;
  dismissedCount?: number;
  convertedCount?: number;
  byType?: {
    PersonalizedAI?: number;
    PopularInCategory?: number;
    TrendingNow?: number;
    BasedOnHistory?: number;
    SimilarToCompleted?: number;
    NextInPath?: number;
    InstructorFollowed?: number;
    PeerRecommended?: number;
  } | null;
}

export type LearningExperienceRecommendationsRecommendationType =
  | "PersonalizedAI"
  | "PopularInCategory"
  | "TrendingNow"
  | "BasedOnHistory"
  | "SimilarToCompleted"
  | "NextInPath"
  | "InstructorFollowed"
  | "PeerRecommended";

export interface LearningExperienceRecommendationsSimilarCourse {
  courseId?: string;
  title?: string | null;
  description?: string | null;
  thumbnail?: string | null;
  category?: string | null;
  similarityScore?: number;
  matchingTags?: Array<string> | null;
}

export interface LearningExperienceRecommendationsTrendingCourse {
  courseId?: string;
  title?: string | null;
  description?: string | null;
  thumbnail?: string | null;
  category?: string | null;
  recentEnrollments?: number;
  trendScore?: number;
}

export interface LearningExperienceRecommendationsUserLearningProfile {
  id?: string;
  userId?: string;
  preferredCategories?: Array<string> | null;
  preferredDifficulty?: string | null;
  preferredDuration?: string | null;
  learningGoals?: Array<string> | null;
  skills?: Array<string> | null;
  totalCoursesCompleted?: number;
  totalHoursLearned?: number;
  lastActivityAt?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

export interface LearningExperienceSocialControllersUpdateReviewModerationInput {
  isApproved?: boolean;
  isFeatured?: boolean;
}

export type LearningExperienceSocialFeedItemType =
  | "NewCourse"
  | "PopularCourse"
  | "TrendingDiscussion"
  | "FeaturedReview"
  | "LearningPathSuggestion"
  | "CourseUpdate"
  | "InstructorActivity"
  | "PeerActivity"
  | "AchievementUnlocked"
  | "SkillMilestone";

export interface LearningExperienceSocialServicesCourseDiscussion {
  id?: string;
  courseId?: string;
  contentId?: string | null;
  authorId?: string;
  title?: string | null;
  content?: string | null;
  isPinned?: boolean;
  isResolved?: boolean;
  replyCount?: number;
  viewCount?: number;
  lastActivityAt?: string | null;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesCourseLike {
  id?: string;
  courseId?: string;
  userId?: string;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesCourseRatingStats {
  courseId?: string;
  averageRating?: number;
  totalReviews?: number;
  fiveStarCount?: number;
  fourStarCount?: number;
  threeStarCount?: number;
  twoStarCount?: number;
  oneStarCount?: number;
  featuredReviewCount?: number;
}

export interface LearningExperienceSocialServicesCourseReview {
  id?: string;
  courseId?: string;
  userId?: string;
  rating?: number;
  title?: string | null;
  content?: string | null;
  isVerifiedPurchase?: boolean;
  helpfulCount?: number;
  isApproved?: boolean;
  isFeatured?: boolean;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesCourseWishlist {
  id?: string;
  courseId?: string;
  userId?: string;
  notifyOnSale?: boolean;
  notifyOnUpdate?: boolean;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesCreateDiscussionInput {
  courseId?: string;
  title?: string | null;
  content?: string | null;
  contentId?: string | null;
}

export interface LearningExperienceSocialServicesCreateReplyInput {
  discussionId?: string;
  content?: string | null;
  parentReplyId?: string | null;
}

export interface LearningExperienceSocialServicesCreateReviewInput {
  courseId?: string;
  rating?: number;
  title?: string | null;
  content?: string | null;
  enrollmentId?: string | null;
}

export interface LearningExperienceSocialServicesDiscussionReply {
  id?: string;
  discussionId?: string;
  authorId?: string;
  parentReplyId?: string | null;
  content?: string | null;
  isAcceptedAnswer?: boolean;
  upvoteCount?: number;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesPersonalizedFeedItem {
  id?: string;
  itemType?: LearningExperienceSocialFeedItemType;
  courseId?: string | null;
  discussionId?: string | null;
  reviewId?: string | null;
  learningPathId?: string | null;
  relevanceScore?: number;
  reason?: string | null;
  isViewed?: boolean;
  expiresAt?: string;
  createdAt?: string;
}

export interface LearningExperienceSocialServicesWishlistPreferencesInput {
  notifyOnSale?: boolean;
  notifyOnUpdate?: boolean;
}

export interface LearningWorkspacesLearnerAnnouncement {
  discussionId?: string;
  courseId?: string;
  courseTitle?: string | null;
  courseSlug?: string | null;
  title?: string | null;
  content?: string | null;
  createdAt?: string;
  lastActivityAt?: string | null;
}

export interface LearningWorkspacesLearnerAssessment {
  assessmentId?: string;
  contentId?: string | null;
  groupId?: string | null;
  title?: string | null;
  description?: string | null;
  type?: string | null;
  maxScore?: number;
  timeLimitMinutes?: number | null;
  maxAttempts?: number | null;
  isRequired?: boolean;
  order?: number;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  allowLateSubmissions?: boolean;
  lateSubmissionDeadline?: string | null;
  submissionModalities?: string | null;
  presentationMode?: string | null;
}

export interface LearningWorkspacesLearnerAssessmentDeadline {
  assessmentId?: string;
  courseId?: string;
  courseTitle?: string | null;
  courseSlug?: string | null;
  contentId?: string | null;
  groupId?: string | null;
  title?: string | null;
  type?: string | null;
  maxScore?: number;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  submissionStatus?: string | null;
}

export interface LearningWorkspacesLearnerAssessmentGroup {
  groupId?: string;
  name?: string | null;
  description?: string | null;
  weightPercent?: number;
  order?: number;
}

export interface LearningWorkspacesLearnerAssessmentSubmission {
  submissionId?: string;
  assessmentId?: string;
  enrollmentId?: string;
  attemptNumber?: number;
  score?: number | null;
  passed?: boolean | null;
  startedAt?: string;
  submittedAt?: string | null;
  gradedAt?: string | null;
  feedback?: string | null;
  status?: string | null;
  isLate?: boolean;
}

export interface LearningWorkspacesLearnerCertificate {
  certificateId?: string;
  enrollmentId?: string;
  courseId?: string;
  courseName?: string | null;
  certificateNumber?: string | null;
  recipientName?: string | null;
  issuedAt?: string;
  expiresAt?: string | null;
  verificationUrl?: string | null;
  status?: string | null;
}

export interface LearningWorkspacesLearnerCohort {
  cohortId?: string;
  name?: string | null;
  description?: string | null;
  startDate?: string;
  endDate?: string;
  maxCapacity?: number;
  currentEnrollmentCount?: number;
  status?: string | null;
  instructorId?: string | null;
  meetingSchedule?: string | null;
}

export interface LearningWorkspacesLearnerContent {
  contentId?: string;
  parentId?: string | null;
  title?: string | null;
  description?: string | null;
  type?: string | null;
  body?: string | null;
  lessonFormat?: string | null;
  activitySettings?: string | null;
  sortOrder?: number;
  isRequired?: boolean;
  estimatedMinutes?: number | null;
  visibility?: string | null;
}

export interface LearningWorkspacesLearnerContentProgress {
  contentId?: string;
  status?: string | null;
  progressPercentage?: number;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  completedAt?: string | null;
  timeSpentSeconds?: number;
  score?: number | null;
  maxScore?: number | null;
  attempts?: number;
}

export interface LearningWorkspacesLearnerCourseSummary {
  courseId?: string;
  enrollmentId?: string;
  title?: string | null;
  slug?: string | null;
  description?: string | null;
  thumbnail?: string | null;
  category?: string | null;
  difficulty?: string | null;
  estimatedHours?: number | null;
  enrollmentStatus?: string | null;
  completionStatus?: string | null;
  progressPercentage?: number;
  finalGrade?: number | null;
  enrolledAt?: string;
  totalItems?: number;
  completedItems?: number;
  remainingMinutes?: number;
  currentContentId?: string | null;
  currentContentTitle?: string | null;
  currentContentType?: string | null;
}

export interface LearningWorkspacesLearnerCourseWorkspace {
  course?: LearningWorkspacesLearnerCourseSummary;
  content?: Array<LearningWorkspacesLearnerContent> | null;
  progress?: Array<LearningWorkspacesLearnerContentProgress> | null;
  cohort?: LearningWorkspacesLearnerCohort;
  calendar?: Array<LearningWorkspacesLearnerScheduleEntry> | null;
  assessmentGroups?: Array<LearningWorkspacesLearnerAssessmentGroup> | null;
  assessments?: Array<LearningWorkspacesLearnerAssessment> | null;
  submissions?: Array<LearningWorkspacesLearnerAssessmentSubmission> | null;
  discussions?: Array<LearningWorkspacesLearnerDiscussion> | null;
  certificates?: Array<LearningWorkspacesLearnerCertificate> | null;
}

export interface LearningWorkspacesLearnerDashboard {
  courses?: Array<LearningWorkspacesLearnerCourseSummary> | null;
  upcoming?: Array<LearningWorkspacesLearnerScheduleEntry> | null;
  deadlines?: Array<LearningWorkspacesLearnerAssessmentDeadline> | null;
  grades?: Array<LearningWorkspacesLearnerGradeSummary> | null;
  certificates?: Array<LearningWorkspacesLearnerCertificate> | null;
  announcements?: Array<LearningWorkspacesLearnerAnnouncement> | null;
}

export interface LearningWorkspacesLearnerDiscussion {
  discussionId?: string;
  contentId?: string | null;
  authorId?: string;
  title?: string | null;
  content?: string | null;
  isPinned?: boolean;
  isResolved?: boolean;
  replyCount?: number;
  viewCount?: number;
  lastActivityAt?: string | null;
  createdAt?: string;
}

export interface LearningWorkspacesLearnerGradeItem {
  assessmentId?: string;
  contentId?: string | null;
  groupId?: string | null;
  title?: string | null;
  type?: string | null;
  maxScore?: number;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  submissionStatus?: string | null;
  score?: number | null;
  passed?: boolean | null;
  feedback?: string | null;
  gradedAt?: string | null;
}

export interface LearningWorkspacesLearnerGradeSummary {
  courseId?: string;
  courseTitle?: string | null;
  courseSlug?: string | null;
  finalGrade?: number | null;
  gradedAssessments?: number;
  totalAssessments?: number;
  earnedPoints?: number | null;
  possiblePoints?: number | null;
  percentage?: number | null;
  groups?: Array<LearningWorkspacesLearnerAssessmentGroup> | null;
  items?: Array<LearningWorkspacesLearnerGradeItem> | null;
}

export interface LearningWorkspacesLearnerScheduleEntry {
  courseId?: string;
  courseTitle?: string | null;
  courseSlug?: string | null;
  cohortId?: string;
  cohortName?: string | null;
  scheduleItemId?: string;
  contentId?: string | null;
  assessmentId?: string | null;
  type?: string | null;
  title?: string | null;
  startsAt?: string | null;
  endsAt?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  status?: string | null;
}

export interface LearningWorkspacesLearnerSearchResult {
  id?: string;
  courseId?: string;
  courseSlug?: string | null;
  kind?: string | null;
  title?: string | null;
  description?: string | null;
  route?: string | null;
}

export interface Money {
  amount?: number;
  currency?: string | null;
}

export interface MonitoringSLACreateSloCommand {
  tenantId?: string;
  name?: string | null;
  description?: string | null;
  serviceName?: string | null;
  targetPercentage?: number;
  timeWindowDays?: number;
  errorBudgetPercentage?: number;
  alertThresholdPercentage?: number;
}

export interface MonitoringSLAErrorBudget {
  serviceLevelObjectiveId?: string;
  targetPercentage?: number;
  errorBudgetPercentage?: number;
  actualPercentage?: number;
  totalRequests?: number;
  successfulRequests?: number;
  failedRequests?: number;
  allowedFailures?: number;
  remainingBudget?: number;
  remainingBudgetPercentage?: number;
  burnRate?: number;
  timeToExhaustionHours?: number | null;
  timeWindowDays?: number;
  windowStart?: string;
  windowEnd?: string;
  isHealthy?: boolean;
}

export interface MonitoringSLARecordSliMetricCommand {
  tenantId?: string;
  serviceLevelObjectiveId?: string;
  isSuccessful?: boolean;
  value?: number;
  responseTimeMs?: number | null;
  statusCode?: number | null;
  endpoint?: string | null;
  metadata?: string | null;
  errorMessage?: string | null;
}

export interface MonitoringSLAResolveSloViolationCommand {
  violationId?: string;
  tenantId?: string;
  resolutionNotes?: string | null;
}

export interface MonitoringSLASlo {
  id?: string;
  tenantId?: string;
  name?: string | null;
  description?: string | null;
  serviceName?: string | null;
  targetPercentage?: number;
  timeWindowDays?: number;
  errorBudgetPercentage?: number;
  alertThresholdPercentage?: number;
  isEnabled?: boolean;
  status?: MonitoringSLASloStatus;
  lastEvaluatedAt?: string | null;
  currentActualPercentage?: number | null;
  remainingErrorBudget?: number | null;
  createdAt?: string;
  updatedAt?: string | null;
}

export interface MonitoringSLASloCompliance {
  serviceLevelObjectiveId?: string;
  name?: string | null;
  serviceName?: string | null;
  targetPercentage?: number;
  actualPercentage?: number;
  isCompliant?: boolean;
  status?: MonitoringSLASloStatus;
  timeWindowDays?: number;
  periodStart?: string;
  periodEnd?: string;
  totalMeasurements?: number;
  successfulMeasurements?: number;
  violationCount?: number;
  totalDowntimeMinutes?: number;
  remainingErrorBudget?: number | null;
  calculatedAt?: string;
}

export type MonitoringSLASloStatus =
  | "Active"
  | "Breached"
  | "AtRisk"
  | "Disabled"
  | "Violated"
  | "Warning"
  | "Inactive";

export interface MonitoringSLASloViolation {
  id?: string;
  serviceLevelObjectiveId?: string;
  sloName?: string | null;
  serviceName?: string | null;
  startedAt?: string;
  endedAt?: string | null;
  durationMinutes?: number;
  actualValue?: number;
  targetValue?: number;
  severity?: MonitoringSLAViolationSeverity;
  alertTriggered?: boolean;
  alertSentAt?: string | null;
  isAcknowledged?: boolean;
  acknowledgedByUserId?: string | null;
  acknowledgedAt?: string | null;
  notes?: string | null;
  description?: string | null;
  isOngoing?: boolean;
}

export interface MonitoringSLAUpdateSloCommand {
  id?: string;
  tenantId?: string;
  name?: string | null;
  description?: string | null;
  serviceName?: string | null;
  targetPercentage?: number;
  timeWindowDays?: number;
  errorBudgetPercentage?: number;
  alertThresholdPercentage?: number;
  isEnabled?: boolean;
}

export type MonitoringSLAViolationSeverity =
  "Low" | "Medium" | "High" | "Critical";

export interface MvcProblemDetails {
  type?: string | null;
  title?: string | null;
  status?: number | null;
  detail?: string | null;
  instance?: string | null;
  [key: string]: any;
}

export interface NotificationsControllersDeletedCountOutput {
  deletedCount?: number;
}

export interface NotificationsControllersNotification {
  id?: string;
  type?: string | null;
  channel?: string | null;
  title?: string | null;
  message?: string | null;
  actionUrl?: string | null;
  iconUrl?: string | null;
  isRead?: boolean;
  readAt?: string | null;
  priority?: string | null;
  referenceEntityId?: string | null;
  referenceEntityType?: string | null;
  createdAt?: string;
}

export interface NotificationsControllersNotificationPreference {
  emailEnabled?: boolean;
  pushEnabled?: boolean;
  inAppEnabled?: boolean;
  smsEnabled?: boolean;
  marketingEnabled?: boolean;
  socialEnabled?: boolean;
  learningEnabled?: boolean;
  achievementsEnabled?: boolean;
  quietHoursStart?: string | null;
  quietHoursEnd?: string | null;
  timezone?: string | null;
  emailDigestFrequency?: string | null;
}

export interface NotificationsControllersSetQuietHoursInput {
  start?: string | null;
  end?: string | null;
  timezone?: string | null;
}

export interface NotificationsControllersUnreadCountOutput {
  count?: number;
}

export interface NotificationsControllersUpdatePreferencesInput {
  emailEnabled?: boolean | null;
  pushEnabled?: boolean | null;
  inAppEnabled?: boolean | null;
  smsEnabled?: boolean | null;
  marketingEnabled?: boolean | null;
  socialEnabled?: boolean | null;
  learningEnabled?: boolean | null;
  achievementsEnabled?: boolean | null;
}

export type NotificationsNotificationChannel =
  "InApp" | "Email" | "Push" | "Sms" | "Slack" | "Discord" | "Webhook";

export type ObjectsAttestationConveyancePreference =
  "None" | "Indirect" | "Direct" | "Enterprise";

export type ObjectsAttestationStatementFormatIdentifier =
  | "Packed"
  | "Tpm"
  | "AndroidKey"
  | "AndroidSafetyNet"
  | "FidoU2f"
  | "Apple"
  | "None";

export interface ObjectsAuthenticationExtensionsClientInputs {
  "example.extension.bool"?: boolean | null;
  exts?: boolean | null;
  uvm?: boolean | null;
  credProps?: boolean | null;
  prf?: ObjectsAuthenticationExtensionsPRFInputs;
  largeBlob?: ObjectsAuthenticationExtensionsLargeBlobInputs;
  credentialProtectionPolicy?: ObjectsCredentialProtectionPolicy;
  enforceCredentialProtectionPolicy?: boolean | null;
}

export interface ObjectsAuthenticationExtensionsLargeBlobInputs {
  support?: ObjectsLargeBlobSupport;
  read?: boolean;
  write?: string | null;
}

export interface ObjectsAuthenticationExtensionsPRFInputs {
  eval?: ObjectsAuthenticationExtensionsPRFValues;
  evalByCredential?: KeyValuePairStringAuthenticationExtensionsPRFValues;
}

export interface ObjectsAuthenticationExtensionsPRFValues {
  first: string | null;
  second?: string | null;
}

export type ObjectsAuthenticatorAttachment = "Platform" | "CrossPlatform";

export type ObjectsAuthenticatorTransport =
  "Usb" | "Nfc" | "Ble" | "SmartCard" | "Hybrid" | "Internal";

export type ObjectsCOSEAlgorithm =
  | "RS1"
  | "RS512"
  | "RS384"
  | "RS256"
  | "ES256K"
  | "PS512"
  | "PS384"
  | "PS256"
  | "ES512"
  | "ES384"
  | "EdDSA"
  | "ES256";

export type ObjectsCredentialProtectionPolicy =
  | "UserVerificationOptional"
  | "UserVerificationOptionalWithCredentialIdList"
  | "UserVerificationRequired";

export type ObjectsLargeBlobSupport = "Required" | "Preferred";

export interface ObjectsPublicKeyCredentialDescriptor {
  type?: ObjectsPublicKeyCredentialType;
  id?: string | null;
  transports?: Array<ObjectsAuthenticatorTransport> | null;
}

export type ObjectsPublicKeyCredentialHint =
  "SecurityKey" | "ClientDevice" | "Hybrid";

export type ObjectsPublicKeyCredentialType = "PublicKey" | "Invalid";

export type ObjectsResidentKeyRequirement =
  "Required" | "Preferred" | "Discouraged";

export type ObjectsUserVerificationRequirement =
  "Required" | "Preferred" | "Discouraged";

export interface PagedResultOfCommerceProductsProduct {
  items?: Array<CommerceProductsProduct> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfCommerceProductsPromoCode {
  items?: Array<CommerceProductsPromoCode> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfCommerceProductsSupportTicket {
  items?: Array<CommerceProductsSupportTicket> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfCommerceSubscriptionsSubscription {
  items?: Array<CommerceSubscriptionsSubscription> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfCommerceSubscriptionsSubscriptionNotification {
  items?: Array<CommerceSubscriptionsSubscriptionNotification> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfIdentityTenantsTenant {
  items?: Array<IdentityTenantsTenant> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfIdentityTenantsTenantAuditLogEntry {
  items?: Array<IdentityTenantsTenantAuditLogEntry> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfIdentityUsersUser {
  items?: Array<IdentityUsersUserDto> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfIdentityUsersUserNotification {
  items?: Array<IdentityUsersUserNotificationDto> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export interface PagedResultOfIdentityUsersUserProfile {
  items?: Array<IdentityUsersUserProfileDto> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
  pageNumber?: number;
  pageSize?: number;
  totalPages?: number;
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
}

export type ProgramCategory =
  | "General"
  | "Programming"
  | "DataScience"
  | "WebDevelopment"
  | "MobileDevelopment"
  | "GameDevelopment"
  | "AI"
  | "Cybersecurity"
  | "DevOps"
  | "Database"
  | "Business"
  | "Design"
  | "Marketing"
  | "ProjectManagement"
  | "PersonalDevelopment"
  | "CreativeArts"
  | "Science"
  | "Language"
  | "Other";

export interface ProjectsAddCollaboratorInput {
  email?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  expiresAt?: string | null;
  message?: string | null;
  requireAcceptance?: boolean;
}

export interface ProjectsAddProjectCollaboratorInput {
  userId: string;
  role?: string | null;
  permissions?: string | null;
}

export interface ProjectsCollaborator {
  id?: string;
  userId?: string;
  userName?: string | null;
  role?: string | null;
  permissions?: string | null;
  joinedAt?: string;
  isActive?: boolean;
}

export interface ProjectsCreateProjectInput {
  title: string;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  repositoryUrl?: string | null;
  websiteUrl?: string | null;
  downloadUrl?: string | null;
  type?: ProjectsProjectType;
  categoryId?: string | null;
  visibility?: ContentVisibility;
  status?: ContentStatus;
  tags?: Array<string> | null;
  ownerTeamId?: string | null;
}

export interface ProjectsCreateProjectVersionInput {
  versionNumber: string;
  status?: string | null;
  releaseNotes?: string | null;
}

export type ProjectsDevelopmentStatus =
  | "Planning"
  | "InDevelopment"
  | "Alpha"
  | "Beta"
  | "Released"
  | "Completed"
  | "OnHold"
  | "Cancelled"
  | "Archived";

export interface ProjectsEffectivePermission {
  resourceId?: string;
  resourceType?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  isOwner?: boolean;
  expiresAt?: string | null;
}

export interface ProjectsInvitationResult {
  success?: boolean;
  errorMessage?: string | null;
  invitationId?: string | null;
}

export interface ProjectsInviteProjectCollaboratorInput {
  userId?: string | null;
  email?: string | null;
  role?: string | null;
  permissions?: string | null;
  expiresAt?: string | null;
}

export interface ProjectsLinkProjectStoreProductInput {
  productId?: string;
}

export interface ProjectsPermissionUpdateResult {
  success?: boolean;
  errorMessage?: string | null;
}

export interface ProjectsProject {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  title: string;
  slug: string;
  shortDescription?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  type?: ProjectsProjectType;
  developmentStatus?: ProjectsDevelopmentStatus;
  status: ContentStatus;
  visibility: ContentVisibility;
  category?: ProjectsProjectCategory;
  categoryId?: string | null;
  websiteUrl?: string | null;
  repositoryUrl?: string | null;
  socialLinks?: string | null;
  downloadUrl?: string | null;
  tags?: string | null;
  featuredImageUrl?: string | null;
  license?: string | null;
  copyright?: string | null;
  publishedAt?: string | null;
  projectMetadata?: ProjectsProjectMetadata;
  versions?: Array<ProjectsProjectVersion> | null;
  collaborators?: Array<ProjectsProjectCollaborator> | null;
  releases?: Array<ProjectsProjectRelease> | null;
  teams?: Array<ProjectsProjectTeam> | null;
  allocations?: Array<ProjectsProjectMemberAllocation> | null;
  teamAgreements?: Array<ProjectsProjectTeamAgreement> | null;
  followers?: Array<ProjectsProjectFollower> | null;
  feedbacks?: Array<ProjectsProjectFeedback> | null;
  jamSubmissions?: Array<ProjectsProjectJamSubmission> | null;
  createdById?: string | null;
  isActive?: boolean;
  latestVersion?: ProjectsProjectVersion;
  followerCount?: number;
  averageRating?: number | null;
  feedbackCount?: number;
  isInJam?: boolean;
  teamCount?: number;
}

export interface ProjectsProjectApiOutput {
  id?: string;
  tenantId?: string | null;
  title?: string | null;
  slug?: string | null;
  shortDescription?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  type?: ProjectsProjectType;
  developmentStatus?: ProjectsDevelopmentStatus;
  status?: ContentStatus;
  visibility?: ContentVisibility;
  categoryId?: string | null;
  category?: ProjectsProjectCategoryApiOutput;
  websiteUrl?: string | null;
  repositoryUrl?: string | null;
  socialLinks?: string | null;
  downloadUrl?: string | null;
  tags?: string | null;
  featuredImageUrl?: string | null;
  license?: string | null;
  copyright?: string | null;
  publishedAt?: string | null;
  createdById?: string | null;
  creator?: ProjectsProjectUserApiOutput;
  metadata?: ProjectsProjectMetadataApiOutput;
  versions?: Array<ProjectsProjectVersionApiOutput> | null;
  collaborators?: Array<ProjectsProjectCollaboratorApiOutput> | null;
  releases?: Array<ProjectsProjectReleaseApiOutput> | null;
  teams?: Array<ProjectsProjectTeamApiOutput> | null;
  latestVersion?: ProjectsProjectVersionApiOutput;
  followerCount?: number;
  averageRating?: number | null;
  feedbackCount?: number;
  isInJam?: boolean;
  teamCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface ProjectsProjectCategory {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  projects?: Array<ProjectsProject> | null;
}

export interface ProjectsProjectCategoryApiOutput {
  id?: string;
  name?: string | null;
}

export interface ProjectsProjectCollaborator {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  userId?: string;
  user?: IdentityUsersUser;
  role: string;
  permissions: string;
  isActive?: boolean;
  joinedAt?: string;
  leftAt?: string | null;
}

export interface ProjectsProjectCollaboratorApiOutput {
  id?: string;
  userId?: string;
  userName?: string | null;
  role?: string | null;
  permissions?: Array<string> | null;
  isActive?: boolean;
  joinedAt?: string;
  leftAt?: string | null;
}

export interface ProjectsProjectCollaboratorDto {
  userId?: string;
  userName?: string | null;
  email?: string | null;
  profilePictureUrl?: string | null;
  role?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  joinedAt?: string;
  invitedBy?: string | null;
  isOwner?: boolean;
  expiresAt?: string | null;
}

export interface ProjectsProjectFeedback {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  userId?: string;
  user?: IdentityUsersUser;
  rating?: number;
  title: string;
  content?: string | null;
  categories?: string | null;
  isFeatured?: boolean;
  isVerified?: boolean;
  status?: ContentStatus;
  helpfulVotes?: number;
  totalVotes?: number;
  platform?: string | null;
  projectVersion?: string | null;
}

export interface ProjectsProjectFollower {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  userId?: string;
  user?: IdentityUsersUser;
  followedAt?: string;
  notificationSettings?: string | null;
  emailNotifications?: boolean;
  pushNotifications?: boolean;
}

export interface ProjectsProjectInvitation {
  id?: string;
  projectId?: string;
  projectTitle?: string | null;
  invitedUserId?: string | null;
  invitedEmail?: string | null;
  invitedByUserId?: string;
  role?: string | null;
  permissions?: string | null;
  token?: string | null;
  status?: ProjectsProjectInvitationStatus;
  invitedAt?: string;
  expiresAt?: string | null;
  respondedAt?: string | null;
}

export type ProjectsProjectInvitationStatus =
  "Pending" | "Accepted" | "Declined" | "Revoked" | "Expired";

export interface ProjectsProjectJamSubmission {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  jamId?: string | null;
  jam?: GameJamsJam;
  submittedAt?: string;
  isEligible?: boolean;
  submissionNotes?: string | null;
  finalScore?: number | null;
  ranking?: number | null;
  hasAward?: boolean;
  awardDetails?: string | null;
  metadata?: string | null;
  scores?: Array<GameJamsJamScore> | null;
}

export interface ProjectsProjectMemberAllocation {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  projectTeamId?: string;
  userId?: string;
  function: string;
  capacityPercentage?: number;
  startsAt?: string;
  endsAt?: string | null;
  isActive?: boolean;
}

export interface ProjectsProjectMetadata {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  viewCount?: number;
  downloadCount?: number;
  followerCount?: number;
}

export interface ProjectsProjectMetadataApiOutput {
  id?: string;
  viewCount?: number;
  downloadCount?: number;
  followerCount?: number;
}

export interface ProjectsProjectRelease {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  title: string;
  description?: string | null;
  releaseVersion: string;
  releasedAt?: string;
  isLatest?: boolean;
  isPrerelease?: boolean;
  downloadUrl?: string | null;
  fileSize?: number | null;
  downloadCount?: number;
  releaseNotes?: string | null;
  checksum?: string | null;
  systemRequirements?: string | null;
  supportedPlatforms?: string | null;
  releaseType?: string | null;
  status?: ContentStatus;
  buildNumber?: string | null;
  releaseMetadata?: string | null;
}

export interface ProjectsProjectReleaseApiOutput {
  id?: string;
  title?: string | null;
  description?: string | null;
  releaseVersion?: string | null;
  releasedAt?: string;
  isLatest?: boolean;
  isPrerelease?: boolean;
  downloadUrl?: string | null;
  fileSize?: number | null;
  downloadCount?: number;
  releaseNotes?: string | null;
  checksum?: string | null;
  systemRequirements?: string | null;
  supportedPlatforms?: string | null;
  releaseType?: string | null;
  status?: ContentStatus;
  buildNumber?: string | null;
  releaseMetadata?: string | null;
}

export interface ProjectsProjectRoleTemplate {
  name?: string | null;
  description?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
}

export interface ProjectsProjectStatistics {
  projectId?: string;
  followerCount?: number;
  feedbackCount?: number;
  averageRating?: number | null;
  totalDownloads?: number;
  activeTeamCount?: number;
  collaboratorCount?: number;
  releaseCount?: number;
  jamSubmissionCount?: number;
  awardCount?: number;
  viewsLast30Days?: number;
  downloadsLast30Days?: number;
  newFollowersLast30Days?: number;
  calculatedAt?: string;
  trendingScore?: number;
  popularityRank?: number | null;
}

export interface ProjectsProjectStoreProductProjection {
  linkId?: string;
  projectId?: string;
  productId?: string;
}

export interface ProjectsProjectTeam {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  teamId?: string;
  team?: TeamsTeam;
  role?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  assignedAt?: string;
  endedAt?: string | null;
  isActive?: boolean;
  permissions?: string | null;
  notes?: string | null;
  contributionPercentage?: number;
  allocations?: Array<ProjectsProjectMemberAllocation> | null;
}

export interface ProjectsProjectTeamAgreement {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  proposingTeamId?: string;
  receivingTeamId?: string;
  proposedByUserId?: string;
  acceptedByUserId?: string | null;
  status?: ProjectsProjectTeamAgreementStatus;
  scope: string;
  deliverables: string;
  startsAt?: string;
  endsAt?: string;
  revision?: number;
  acceptedAt?: string | null;
  cancelledAt?: string | null;
  completedAt?: string | null;
}

export type ProjectsProjectTeamAgreementStatus =
  "Proposed" | "CounterProposed" | "Accepted" | "Cancelled" | "Completed";

export interface ProjectsProjectTeamApiOutput {
  id?: string;
  teamId?: string;
  name?: string | null;
  slug?: string | null;
  role?: ProjectsProjectTeamRole;
  participationMode?: ProjectsProjectTeamParticipationMode;
  assignedAt?: string;
  endedAt?: string | null;
  isActive?: boolean;
  permissions?: Array<string> | null;
  notes?: string | null;
  contributionPercentage?: number;
}

export type ProjectsProjectTeamParticipationMode =
  "AllMembers" | "SelectedMembers";

export type ProjectsProjectTeamRole =
  "Owner" | "CoOwner" | "Contributor" | "Guest";

export type ProjectsProjectType =
  | "Game"
  | "Tool"
  | "Art"
  | "Music"
  | "Educational"
  | "Plugin"
  | "Template"
  | "Library"
  | "Other";

export interface ProjectsProjectUserApiOutput {
  id?: string;
  name?: string | null;
  username?: string | null;
}

export interface ProjectsProjectVersion {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectId?: string;
  versionNumber: string;
  releaseNotes?: string | null;
  status: string;
  downloadCount?: number;
  createdById?: string;
}

export interface ProjectsProjectVersionApiOutput {
  id?: string;
  projectId?: string;
  versionNumber?: string | null;
  releaseNotes?: string | null;
  status?: string | null;
  downloadCount?: number;
  createdById?: string;
  createdAt?: string;
  updatedAt?: string;
}

export interface ProjectsProjectVersionOptionProjection {
  id?: string;
  projectId?: string;
  projectTitle?: string | null;
  versionNumber?: string | null;
  status?: string | null;
  updatedAt?: string;
}

export interface ProjectsShareProjectInput {
  userId: string;
  role?: string | null;
  permissions?: string | null;
}

export interface ProjectsShareProjectWithRoleInput {
  roleName?: string | null;
  userEmails?: Array<string> | null;
  userIds?: Array<string> | null;
  expiresAt?: string | null;
  message?: string | null;
  requireAcceptance?: boolean;
  notifyUsers?: boolean;
}

export interface ProjectsShareResult {
  success?: boolean;
  errorMessage?: string | null;
  successCount?: number;
  failureCount?: number;
}

export interface ProjectsUpdateCollaboratorInput {
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  expiresAt?: string | null;
}

export interface ProjectsUpdateProjectCollaboratorInput {
  role?: string | null;
  permissions?: string | null;
}

export interface ProjectsUpdateProjectInput {
  title?: string | null;
  description?: string | null;
  shortDescription?: string | null;
  imageUrl?: string | null;
  repositoryUrl?: string | null;
  websiteUrl?: string | null;
  downloadUrl?: string | null;
  type?: ProjectsProjectType;
  categoryId?: string | null;
  visibility?: ContentVisibility;
  status?: ContentStatus;
  tags?: Array<string> | null;
}

export type ProjectWorkProjectWorkColumnKind =
  "Backlog" | "Ready" | "InProgress" | "InReview" | "Done" | "Custom";

export type ProjectWorkProjectWorkTaskPriority =
  "Low" | "Normal" | "High" | "Urgent";

export type ProjectWorkProjectWorkTaskStatus =
  "Backlog" | "Ready" | "InProgress" | "InReview" | "Done" | "Cancelled";

export interface ResourcesArchiveResourceUsageRecordsInput {
  olderThan?: string;
}

export interface ResourcesCheckResourceQuotaInput {
  amount?: number;
}

export interface ResourcesCleanupOrphanedResourcesInput {
  dryRun?: boolean;
  resourceTypes?: Array<ResourcesResourceUsageType> | null;
}

export interface ResourcesContentsAddReviewInput {
  decision?: ResourcesContentsContentReviewDecision;
  feedback?: string | null;
  suggestions?: string | null;
}

export interface ResourcesContentsBulkGenerateContractsInput {
  contracts?: Array<ResourcesContentsGenerateContractInput> | null;
  continueOnError?: boolean;
}

export interface ResourcesContentsBulkGeneratedContractItemOutput {
  index?: number;
  success?: boolean;
  contract?: ResourcesContentsGeneratedContractOutput;
  error?: Error;
}

export interface ResourcesContentsBulkGeneratedContractsOutput {
  totalRequested?: number;
  successful?: number;
  failed?: number;
  hasFailures?: boolean;
  items?: Array<ResourcesContentsBulkGeneratedContractItemOutput> | null;
}

export type ResourcesContentsContentReviewDecision =
  "Pending" | "Approve" | "RequestChanges" | "Reject";

export interface ResourcesContentsContentVersion {
  id?: string;
  entityId?: string;
  entityType?: string | null;
  versionNumber?: number;
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  metadata?: string | null;
  status?: ResourcesContentsContentVersionStatus;
  createdBy?: string;
  changeNotes?: string | null;
  createdAt?: string;
  submittedForReviewAt?: string | null;
  reviewedBy?: string | null;
  reviewedAt?: string | null;
  reviewNotes?: string | null;
  publishedAt?: string | null;
  publishedBy?: string | null;
  scheduledPublishAt?: string | null;
  isCurrentVersion?: boolean;
}

export interface ResourcesContentsContentVersionDiff {
  version1Id?: string;
  version2Id?: string;
  version1Number?: number;
  version2Number?: number;
  titleChanged?: boolean;
  summaryChanged?: boolean;
  bodyChanged?: boolean;
  metadataChanged?: boolean;
  titleDiff?: string | null;
  summaryDiff?: string | null;
  bodyDiff?: string | null;
}

export interface ResourcesContentsContentVersionReview {
  id?: string;
  contentVersionId?: string;
  reviewerId?: string;
  decision?: ResourcesContentsContentReviewDecision;
  feedback?: string | null;
  suggestions?: string | null;
  createdAt?: string;
}

export type ResourcesContentsContentVersionStatus =
  | "Draft"
  | "PendingReview"
  | "Approved"
  | "Rejected"
  | "Scheduled"
  | "Published"
  | "Archived";

export interface ResourcesContentsCreateDraftInput {
  entityId?: string;
  entityType?: string | null;
  title?: string | null;
  createdBy?: string;
  summary?: string | null;
  body?: string | null;
  metadata?: string | null;
  changeNotes?: string | null;
}

export interface ResourcesContentsGenerateContractInput {
  documentTemplateId?: string;
  entityType?: string | null;
  entityId?: string | null;
  title?: string | null;
  variables?: Record<string, string | null> | null;
  summary?: string | null;
  publish?: boolean;
  allowMissingVariables?: boolean;
}

export interface ResourcesContentsGeneratedContractOutput {
  contractId?: string;
  contentVersionId?: string;
  documentTemplateId?: string;
  templateKey?: string | null;
  entityType?: string | null;
  entityId?: string;
  versionNumber?: number;
  title?: string | null;
  content?: string | null;
  missingVariables?: Array<string> | null;
  published?: boolean;
  generatedAtUtc?: string;
}

export interface ResourcesContentsReviewInput {
  reviewNotes?: string | null;
}

export interface ResourcesContentsRollbackInput {
  targetVersionNumber?: number;
  reason?: string | null;
}

export interface ResourcesContentsScheduleInput {
  scheduledAt?: string;
}

export interface ResourcesContentsUpdateDraftInput {
  title?: string | null;
  summary?: string | null;
  body?: string | null;
  metadata?: string | null;
  changeNotes?: string | null;
}

export interface ResourcesEffectiveSettingOutput {
  key?: string | null;
  value?: string | null;
  isUserOverride?: boolean;
}

export interface ResourcesRecordTenantResourceUsageInput {
  resourceUsageType?: ResourcesResourceUsageType;
  count?: number;
  periodStart?: string;
  periodEnd?: string;
  metadata?: Record<string, string> | null;
}

export interface ResourcesRecordUserResourceUsageInput {
  resourceUsageType?: ResourcesResourceUsageType;
  count?: number;
  periodStart?: string;
  periodEnd?: string;
  metadata?: Record<string, string> | null;
}

export interface ResourcesResourceMetadata {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  key: string;
  value?: string | null;
  dataType?: string | null;
  description?: string | null;
  category?: string | null;
  isSystemManaged?: boolean;
  isActive?: boolean;
  displayOrder?: number;
  userId?: string | null;
  resourceId?: string | null;
  rowVersion?: string | null;
}

export interface ResourcesResourceQuotaEnforcementResult {
  isAllowed?: boolean;
  isSoftLimitExceeded?: boolean;
  isHardLimitExceeded?: boolean;
  currentUsage?: number;
  softLimit?: number | null;
  hardLimit?: number | null;
  usagePercentage?: number;
  excessAmount?: number;
  message?: string | null;
  type?: ResourcesResourceUsageType;
  nextReset?: string | null;
  remainingQuota?: number | null;
}

export interface ResourcesResourceQuotaOutput {
  id?: string;
  tenantId?: string;
  type?: ResourcesResourceUsageType;
  limit?: number;
  currentUsage?: number;
  remainingQuota?: number;
  usagePercentage?: number;
  softLimitPercentage?: number;
  isActive?: boolean;
  period?: ResourcesResourceQuotaPeriod;
  lastResetDate?: string;
  nextResetDate?: string;
  description?: string | null;
  isSoftLimitExceeded?: boolean;
  isHardLimitExceeded?: boolean;
  shouldReset?: boolean;
  softLimit?: number | null;
  hardLimit?: number | null;
}

export type ResourcesResourceQuotaPeriod =
  "Daily" | "Weekly" | "Monthly" | "Quarterly" | "Yearly" | "Unlimited";

export interface ResourcesResourceSettings {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  key: string;
  value?: string | null;
  defaultValue?: string | null;
  dataType?: string | null;
  description?: string | null;
  category?: string | null;
  isSystemManaged?: boolean;
  isActive?: boolean;
  allowUserOverride?: boolean;
  displayOrder?: number;
  userId?: string | null;
  validationRules?: string | null;
  rowVersion?: string | null;
}

export type ResourcesResourceUsageType =
  | "Users"
  | "Projects"
  | "Storage"
  | "ApiCalls"
  | "Programs"
  | "Courses"
  | "FeatureFlags"
  | "SubscriptionPlans"
  | "Products"
  | "TestingSessions"
  | "Roles"
  | "Tenants"
  | "Subscriptions"
  | "SLOs"
  | "AccessReviewCampaigns"
  | "SoDRules"
  | "AbacPolicies"
  | "ConditionalPolicies"
  | "Wallets"
  | "Disputes"
  | "PromoCodes"
  | "Orders"
  | "AuditEntries"
  | "Assets"
  | "AssetStorage"
  | "AssetDownloads"
  | "AssetTransformations"
  | "AiRequests"
  | "AiTokens"
  | "Teams";

export interface ResourcesSetQuotaInput {
  softLimit?: number | null;
  hardLimit?: number | null;
  period?: ResourcesResourceQuotaPeriod;
  isActive?: boolean;
  resetTime?: string | null;
}

export interface ResourcesSetResourceMetadataInput {
  value?: string | null;
  dataType?: string | null;
  description?: string | null;
  category?: string | null;
  displayOrder?: number | null;
}

export interface ResourcesSetResourceSettingsInput {
  value?: string | null;
  defaultValue?: string | null;
  dataType?: string | null;
  description?: string | null;
  category?: string | null;
  allowUserOverride?: boolean | null;
  displayOrder?: number | null;
  validationRules?: string | null;
}

export interface ResourcesSetUserResourceSettingsInput {
  value?: string | null;
}

export interface ResourcesToggleResourceQuotaInput {
  isActive?: boolean;
}

export type ResourcesTrendGranularity = "Daily" | "Weekly" | "Monthly";

export interface ResourcesUsageRecord {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  type?: ResourcesResourceUsageType;
  count?: number;
  usageAmount?: number;
  periodStart?: string;
  periodEnd?: string;
  averagePerDay?: number | null;
  peakUsage?: number | null;
  peakUsageDate?: string | null;
  metadata?: string | null;
  source?: string | null;
  userId?: string | null;
  resourceId?: string | null;
  resourceQuotaId?: string | null;
}

export interface ResourcesUsageTrendDataPoint {
  period?: string;
  totalUsage?: number;
  tenantCount?: number;
}

export interface ResourcesUsageTrendsResult {
  type?: ResourcesResourceUsageType;
  startDate?: string;
  endDate?: string;
  granularity?: ResourcesTrendGranularity;
  dataPoints?: Array<ResourcesUsageTrendDataPoint> | null;
}

export interface SocialBlogBlogPost {
  id?: string;
  authorId?: string;
  tenantId?: string | null;
  title?: string | null;
  slug?: string | null;
  excerpt?: string | null;
  content?: string | null;
  coverImageUrl?: string | null;
  status?: SocialBlogBlogPostStatus;
  publishedAt?: string | null;
  isFeatured?: boolean;
  allowComments?: boolean;
  viewsCount?: number;
  likesCount?: number;
  commentsCount?: number;
  readTimeMinutes?: number;
  createdAt?: string;
  updatedAt?: string;
}

export type SocialBlogBlogPostStatus = "Draft" | "Published" | "Archived";

export interface SocialBlogCreateBlogPostInput {
  authorId?: string;
  title?: string | null;
  slug?: string | null;
  content?: string | null;
  tenantId?: string | null;
}

export interface SocialFeedAddFeedItemInput {
  userId?: string;
  contentId?: string;
  contentType?: SocialFeedFeedContentType;
  authorId?: string;
  reason?: SocialFeedFeedItemReason;
  contentCreatedAt?: string | null;
  relevanceScore?: number;
}

export type SocialFeedFeedContentType =
  | "Post"
  | "BlogPost"
  | "CourseReview"
  | "ProjectUpdate"
  | "Achievement"
  | "CourseCompletion";

export interface SocialFeedFeedItem {
  id?: string;
  userId?: string;
  contentId?: string;
  contentType?: SocialFeedFeedContentType;
  authorId?: string;
  relevanceScore?: number;
  reason?: SocialFeedFeedItemReason;
  isRead?: boolean;
  isHidden?: boolean;
  contentCreatedAt?: string;
  createdAt?: string;
}

export type SocialFeedFeedItemReason =
  | "Following"
  | "Trending"
  | "Recommended"
  | "Mentioned"
  | "Replied"
  | "Liked"
  | "InNetwork";

export interface SocialGroupsApproveSocialGroupMemberInput {
  approvedByUserId?: string;
}

export interface SocialGroupsChangeSocialGroupMemberRoleInput {
  role?: SocialGroupsSocialGroupMemberRole;
}

export interface SocialGroupsCreateSocialGroupInput {
  ownerId?: string;
  name?: string | null;
  slug?: string | null;
  type?: SocialGroupsSocialGroupType;
  visibility?: SocialGroupsSocialGroupVisibility;
  description?: string | null;
  tenantId?: string | null;
}

export interface SocialGroupsJoinSocialGroupInput {
  userId?: string;
  requestedRole?: SocialGroupsSocialGroupMemberRole;
}

export interface SocialGroupsSocialGroup {
  id?: string;
  tenantId?: string | null;
  ownerId?: string;
  name?: string | null;
  slug?: string | null;
  description?: string | null;
  type?: SocialGroupsSocialGroupType;
  visibility?: SocialGroupsSocialGroupVisibility;
  status?: SocialGroupsSocialGroupStatus;
  memberCount?: number;
  pendingMemberCount?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface SocialGroupsSocialGroupMember {
  id?: string;
  groupId?: string;
  userId?: string;
  role?: SocialGroupsSocialGroupMemberRole;
  status?: SocialGroupsSocialGroupMembershipStatus;
  requestedAt?: string;
  joinedAt?: string | null;
  approvedByUserId?: string | null;
  removedAt?: string | null;
}

export type SocialGroupsSocialGroupMemberRole =
  "Owner" | "Admin" | "Moderator" | "Member";

export type SocialGroupsSocialGroupMembershipStatus =
  "Pending" | "Active" | "Rejected" | "Removed";

export type SocialGroupsSocialGroupStatus = "Active" | "Archived" | "Suspended";

export type SocialGroupsSocialGroupType =
  | "StudyGroup"
  | "ProjectTeam"
  | "InterestCommunity"
  | "CourseCohort"
  | "Institution"
  | "GameJamTeam";

export type SocialGroupsSocialGroupVisibility =
  "Public" | "Private" | "InviteOnly";

export interface SocialGroupsUpdateSocialGroupInput {
  name?: string | null;
  slug?: string | null;
  type?: SocialGroupsSocialGroupType;
  visibility?: SocialGroupsSocialGroupVisibility;
  description?: string | null;
}

export interface SocialPostsControllersAddCommentInput {
  content?: string | null;
  parentCommentId?: string | null;
}

export interface SocialPostsControllersCreatePostInput {
  content?: string | null;
  visibility?: SocialPostsPostVisibility;
  mediaUrl?: string | null;
  mediaType?: SocialPostsMediaType;
  tenantId?: string | null;
  tags?: Array<string> | null;
}

export interface SocialPostsControllersFollowPostInput {
  notifyOnComments?: boolean;
  notifyOnLikes?: boolean;
  notifyOnShares?: boolean;
  notifyOnUpdates?: boolean;
}

export interface SocialPostsControllersUpdateCommentInput {
  content?: string | null;
}

export interface SocialPostsControllersUpdatePostInput {
  content?: string | null;
}

export type SocialPostsMediaType = "Image" | "Video" | "Audio" | "Document";

export type SocialPostsPostVisibility =
  "Public" | "Followers" | "Private" | "Unlisted";

export interface SocialProfilesAddProfilePortfolioItemBody {
  title?: string | null;
  projectId?: string | null;
  description?: string | null;
  url?: string | null;
  imageUrl?: string | null;
  isPinned?: boolean;
  displayOrder?: number;
}

export interface SocialProfilesAddProfileSkillBody {
  name?: string | null;
  proficiency?: SocialProfilesProfileSkillProficiency;
  displayOrder?: number;
}

export type SocialProfilesProfileAvailabilityStatus =
  "NotSet" | "OpenToWork" | "OpenToCollaborate" | "Busy" | "Hidden";

export interface SocialProfilesProfilePortfolioItem {
  id?: string;
  profileId?: string;
  projectId?: string | null;
  title?: string | null;
  description?: string | null;
  url?: string | null;
  imageUrl?: string | null;
  isPinned?: boolean;
  displayOrder?: number;
}

export interface SocialProfilesProfileSkill {
  id?: string;
  profileId?: string;
  name?: string | null;
  proficiency?: SocialProfilesProfileSkillProficiency;
  displayOrder?: number;
}

export type SocialProfilesProfileSkillProficiency =
  "Beginner" | "Intermediate" | "Advanced" | "Expert";

export type SocialProfilesProfileVisibility =
  "Private" | "Connections" | "Public";

export interface SocialProfilesSocialProfile {
  id?: string;
  userId?: string;
  handle?: string | null;
  displayName?: string | null;
  bio?: string | null;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  headline?: string | null;
  location?: string | null;
  timeZone?: string | null;
  websiteUrl?: string | null;
  socialLinksJson?: string | null;
  visibility?: SocialProfilesProfileVisibility;
  availabilityStatus?: SocialProfilesProfileAvailabilityStatus;
  showActivity?: boolean;
  showPortfolio?: boolean;
  showSkills?: boolean;
  verifiedAt?: string | null;
  completenessScore?: number;
  followerCount?: number;
  followingCount?: number;
  postCount?: number;
  projectCount?: number;
  skills?: Array<SocialProfilesProfileSkill> | null;
  portfolioItems?: Array<SocialProfilesProfilePortfolioItem> | null;
}

export interface SocialProfilesUpdateProfilePortfolioItemBody {
  title?: string | null;
  description?: string | null;
  url?: string | null;
  imageUrl?: string | null;
  isPinned?: boolean;
  displayOrder?: number;
}

export interface SocialProfilesUpdateProfilePrivacyBody {
  visibility?: SocialProfilesProfileVisibility;
  showActivity?: boolean;
  showPortfolio?: boolean;
  showSkills?: boolean;
}

export interface SocialProfilesUpdateProfileStatsBody {
  followerCount?: number;
  followingCount?: number;
  postCount?: number;
  projectCount?: number;
}

export interface SocialProfilesUpdateSocialProfileBody {
  handle?: string | null;
  displayName?: string | null;
  bio?: string | null;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  headline?: string | null;
  location?: string | null;
  timeZone?: string | null;
  websiteUrl?: string | null;
  socialLinksJson?: string | null;
  availabilityStatus?: SocialProfilesProfileAvailabilityStatus;
}

export interface SocialReactionsReaction {
  id?: string;
  userId?: string;
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  type?: SocialReactionsReactionType;
  createdAt?: string;
  updatedAt?: string;
}

export type SocialReactionsReactionTargetType =
  "Post" | "Comment" | "BlogPost" | "CourseReview" | "Discussion" | "Reply";

export type SocialReactionsReactionType =
  "Like" | "Love" | "Insightful" | "Celebrate" | "Support" | "Curious";

export interface SocialReactionsRemoveReactionInput {
  userId?: string;
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
}

export interface SocialReactionsSetReactionInput {
  userId?: string;
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  type?: SocialReactionsReactionType;
}

export interface SocialReactionsTargetReactionSummary {
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  counts?: {
    Like?: number;
    Love?: number;
    Insightful?: number;
    Celebrate?: number;
    Support?: number;
    Curious?: number;
  } | null;
  total?: number;
}

export type SystemDayOfWeek =
  | "Sunday"
  | "Monday"
  | "Tuesday"
  | "Wednesday"
  | "Thursday"
  | "Friday"
  | "Saturday";

export interface TeamsTeam {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  slug: string;
  description?: string | null;
  visibility?: TeamsTeamVisibility;
  status?: TeamsTeamStatus;
  isPersonal?: boolean;
  isActive?: boolean;
  members?: Array<TeamsTeamMember> | null;
  invitations?: Array<TeamsTeamInvitation> | null;
}

export interface TeamsTeamInvitation {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  teamId?: string;
  team?: TeamsTeam;
  invitedUserId?: string | null;
  invitedEmail?: string | null;
  tokenHash: string;
  authority?: TeamsTeamMemberAuthority;
  invitedByUserId?: string;
  expiresAt?: string;
  revokedAt?: string | null;
  usedAt?: string | null;
  acceptedByUserId?: string | null;
}

export interface TeamsTeamMember {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  teamId?: string;
  team?: TeamsTeam;
  userId?: string;
  user?: IdentityUsersUser;
  authority?: TeamsTeamMemberAuthority;
  professionalTitle?: string | null;
  joinedAt?: string;
  leftAt?: string | null;
  isActive?: boolean;
}

export type TeamsTeamMemberAuthority =
  "Viewer" | "Member" | "Manager" | "Owner";

export type TeamsTeamStatus = "Active" | "Archived";

export type TeamsTeamVisibility = "Private" | "Tenant" | "Public";

export interface TenantInfo {
  id?: string;
  name?: string | null;
  slug?: string | null;
  isActive?: boolean;
}

export interface TestingLabAddTestingEventCommitteeMemberInput {
  userId?: string;
  isChair?: boolean;
}

export interface TestingLabAssignTestingLabRoleInput {
  tenantId?: string | null;
  roleName?: string | null;
  expiresAt?: string | null;
}

export interface TestingLabAssignTestingProjectApplicationSlotInput {
  slotId?: string;
}

export interface TestingLabAssignTestingProjectToTesterInput {
  applicationId?: string;
}

export type TestingLabAttendanceStatus =
  "Registered" | "Present" | "Completed" | "NoShow";

export interface TestingLabCancelTestingEventInput {
  reason?: string | null;
}

export interface TestingLabCastTestingApplicationVoteInput {
  decision?: TestingLabTestingApplicationVoteDecision;
  comments?: string | null;
}

export interface TestingLabConfigureTestingEventLearningInput {
  courseId?: string;
  cohortId?: string | null;
  learningActivityId?: string;
  requirement?: TestingLabTestingLearningCompletionRequirement;
}

export interface TestingLabCreateSimpleTestingInput {
  title: string;
  description?: string | null;
  projectId?: string | null;
  versionNumber: string;
  downloadUrl?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsContent?: string | null;
  instructionsUrl?: string | null;
  feedbackFormContent?: string | null;
  maxTesters?: number | null;
  startDate?: string | null;
  endDate?: string | null;
  teamIdentifier?: string | null;
}

export interface TestingLabCreateTestingEventInput {
  name?: string | null;
  description?: string | null;
  mode?: TestingLabTestingEventMode;
  approvalMode?: TestingLabTestingEventApprovalMode;
  applicationsOpenAt?: string;
  applicationsCloseAt?: string;
  startsAt?: string;
  endsAt?: string;
  requiresFeedback?: boolean;
  recurrence?: TestingLabTestingEventRecurrenceInput;
}

export interface TestingLabCreateTestingInput {
  projectVersionId: string;
  title: string;
  description?: string | null;
  downloadUrl?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsContent?: string | null;
  instructionsUrl?: string | null;
  instructionsFileId?: string | null;
  feedbackFormContent?: string | null;
  maxTesters?: number | null;
  startDate: string;
  endDate: string;
  status: TestingLabTestingRequestStatus;
}

export interface TestingLabCreateTestingLabRoleInput {
  name?: string | null;
  description?: string | null;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabCreateTestingLabSettings {
  labName: string;
  description?: string | null;
  timezone: string;
  defaultSessionDuration: number;
  allowPublicSignups?: boolean;
  requireApproval?: boolean;
  enableNotifications?: boolean;
  maxSimultaneousSessions: number;
}

export interface TestingLabCreateTestingLocation {
  name?: string | null;
  description?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  maxTestersCapacity?: number;
  maxProjectsCapacity?: number;
  equipmentAvailable?: string | null;
  isVirtual?: boolean;
  virtualUrl?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  status?: TestingLabLocationStatus;
}

export interface TestingLabCreateTestingSession {
  testingRequestId: string;
  locationId: string;
  sessionName: string;
  sessionDate: string;
  startTime: string;
  endTime: string;
  maxTesters: number;
  maxProjects: number;
  status: TestingLabSessionStatus;
  managerUserId: string;
}

export interface TestingLabDecideTestingProjectApplicationInput {
  slotId?: string | null;
  rationale?: string | null;
}

export type TestingLabFeedbackFormType =
  "General" | "BugReport" | "Usability" | "Performance" | "Accessibility";

export interface TestingLabFeedbackInput {
  feedbackFormId?: string;
  feedbackData?: string | null;
  testingContext?: TestingLabTestingContext;
  sessionId?: string | null;
  additionalNotes?: string | null;
}

export type TestingLabFeedbackQuality = "Low" | "Medium" | "High";

export interface TestingLabFeedbackQualityRating {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  feedbackId: string;
  feedback?: TestingLabTestingFeedback;
  ratedByUserId: string;
  ratedBy?: IdentityUsersUser;
  qualityRating: number;
  reason?: string | null;
  isGlobal?: boolean;
  isPositive?: boolean;
  isNegative?: boolean;
}

export interface TestingLabGrantResourcePermissionInput {
  tenantId?: string | null;
  action?: string | null;
  expiresAt?: string | null;
}

export type TestingLabInstructionType = "Text" | "Url" | "File";

export interface TestingLabLinkSessionProjectInput {
  projectId?: string;
  projectVersionId?: string | null;
  notes?: string | null;
}

export type TestingLabLocationStatus = "Active" | "Maintenance" | "Inactive";

export type TestingLabParticipationStatus =
  "Registered" | "Active" | "Completed" | "Withdrawn" | "Suspended";

export interface TestingLabPublicTestingEventProjection {
  id?: string;
  name?: string | null;
  description?: string | null;
  mode?: TestingLabTestingEventMode;
  approvalMode?: TestingLabTestingEventApprovalMode;
  status?: TestingLabTestingEventStatus;
  applicationsOpenAt?: string;
  applicationsCloseAt?: string;
  startsAt?: string;
  endsAt?: string;
  requiresFeedback?: boolean;
  applicationCount?: number;
  slots?: Array<TestingLabPublicTestingEventSlotProjection> | null;
}

export interface TestingLabPublicTestingEventSlotProjection {
  id?: string;
  eventId?: string;
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  endsAt?: string;
  maxTesters?: number | null;
  maxProjects?: number | null;
  campusName?: string | null;
  roomName?: string | null;
  approvedProjectCount?: number;
  registeredTesterCount?: number;
  availableTesterCount?: number | null;
  availableProjectCount?: number | null;
}

export interface TestingLabRateFeedbackQuality {
  quality?: TestingLabFeedbackQuality;
}

export interface TestingLabRegisterTestingEventSlotInput {
  notes?: string | null;
}

export type TestingLabRegistrationStatus =
  "Registered" | "Confirmed" | "Cancelled" | "Attended" | "NoShow";

export type TestingLabRegistrationType = "ProjectMember" | "Tester";

export interface TestingLabReportFeedback {
  reason?: string | null;
}

export interface TestingLabSessionProjectProjection {
  linkId?: string;
  sessionId?: string;
  projectId?: string;
  projectVersionId?: string | null;
  isActive?: boolean;
}

export interface TestingLabSessionRegistration {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  sessionId: string;
  session?: TestingLabTestingSession;
  userId: string;
  user?: IdentityUsersUser;
  registrationType?: TestingLabRegistrationType;
  status?: TestingLabRegistrationStatus;
  registeredAt: string;
  confirmedAt?: string | null;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  attendanceStatus?: TestingLabAttendanceStatus;
  notes?: string | null;
  registrationNotes?: string | null;
  attendedAt?: string | null;
  isGlobal?: boolean;
  isConfirmed?: boolean;
  isCheckedIn?: boolean;
  isCheckedOut?: boolean;
  attendanceDuration?: string | null;
}

export interface TestingLabSessionRegistrationInput {
  registrationType?: TestingLabRegistrationType;
  notes?: string | null;
}

export type TestingLabSessionStatus =
  "Scheduled" | "Active" | "Completed" | "Cancelled";

export interface TestingLabSessionWaitlist {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  sessionId?: string;
  session?: TestingLabTestingSession;
  userId?: string;
  user?: IdentityUsersUser;
  registrationType: TestingLabRegistrationType;
  position: number;
  registrationNotes?: string | null;
}

export interface TestingLabSubmitFeedback {
  testingRequestId: string;
  feedbackResponses: string;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
  additionalNotes?: string | null;
  sessionId?: string | null;
}

export interface TestingLabSubmitTestingEventFeedbackInput {
  feedbackData?: string | null;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
  additionalNotes?: string | null;
}

export interface TestingLabSubmitTestingProjectApplicationInput {
  projectId?: string;
  projectVersionId?: string;
  preferredAvailability?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface TestingLabTestingApplicationReviewAssetProjection {
  assetReferenceId?: string;
  displayName?: string | null;
  mimeType?: string | null;
  accessUrl?: string | null;
  expiresAt?: string;
}

export interface TestingLabTestingApplicationReviewPackageProjection {
  applicationId?: string;
  projectId?: string;
  projectVersionId?: string;
  versionNumber?: string | null;
  versionStatus?: string | null;
  releaseNotes?: string | null;
  assets?: Array<TestingLabTestingApplicationReviewAssetProjection> | null;
}

export type TestingLabTestingApplicationStatus =
  | "Pending"
  | "UnderReview"
  | "Approved"
  | "Rejected"
  | "Waitlisted"
  | "Withdrawn";

export interface TestingLabTestingApplicationTesterEligibilityProjection {
  testerUserId?: string;
  eligibleApplicationIds?: Array<string> | null;
}

export interface TestingLabTestingApplicationVote {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  applicationId?: string;
  application?: TestingLabTestingProjectApplication;
  reviewerId?: string;
  reviewer?: IdentityUsersUser;
  decision?: TestingLabTestingApplicationVoteDecision;
  comments?: string | null;
}

export type TestingLabTestingApplicationVoteDecision =
  "Approve" | "Reject" | "Abstain";

export interface TestingLabTestingApplicationVoteProjection {
  id?: string;
  reviewerId?: string;
  decision?: TestingLabTestingApplicationVoteDecision;
  comments?: string | null;
  createdAt?: string;
}

export interface TestingLabTestingCommitteeMember {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  eventId?: string;
  event?: TestingLabTestingEvent;
  userId?: string;
  user?: IdentityUsersUser;
  isChair?: boolean;
  isActive?: boolean;
}

export type TestingLabTestingContext = "Online" | "InPerson";

export interface TestingLabTestingEvent {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  description?: string | null;
  mode?: TestingLabTestingEventMode;
  approvalMode?: TestingLabTestingEventApprovalMode;
  status?: TestingLabTestingEventStatus;
  managerUserId?: string;
  manager?: IdentityUsersUser;
  applicationsOpenAt?: string;
  applicationsCloseAt?: string;
  startsAt?: string;
  endsAt?: string;
  recurrenceSeriesId?: string | null;
  recurrenceOccurrence?: number | null;
  recurrenceFrequency?: TestingLabTestingEventRecurrenceFrequency;
  recurrenceInterval?: number | null;
  recurrenceDaysOfWeek?: string | null;
  recurrenceEndsAt?: string | null;
  recurrenceOccurrenceCount?: number | null;
  requiresFeedback?: boolean;
  learningCompletionRequirement?: TestingLabTestingLearningCompletionRequirement;
  courseId?: string | null;
  cohortId?: string | null;
  learningActivityId?: string | null;
  cancellationReason?: string | null;
  cancelledAt?: string | null;
  slots?: Array<TestingLabTestingEventSlot> | null;
  applications?: Array<TestingLabTestingProjectApplication> | null;
  committeeMembers?: Array<TestingLabTestingCommitteeMember> | null;
}

export type TestingLabTestingEventApprovalMode = "ManagerOnly" | "Committee";

export interface TestingLabTestingEventCommitteeMemberProjection {
  id?: string;
  eventId?: string;
  userId?: string;
  userName?: string | null;
  userEmail?: string | null;
  isChair?: boolean;
  isActive?: boolean;
}

export interface TestingLabTestingEventFeedbackProjection {
  id?: string;
  eventId?: string;
  applicationId?: string;
  testerUserId?: string;
  feedbackData?: string | null;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
  additionalNotes?: string | null;
  submittedAt?: string;
}

export interface TestingLabTestingEventFeedbackReviewProjection {
  obligationId?: string;
  eventId?: string;
  slotId?: string;
  applicationId?: string;
  testerUserId?: string;
  status?: TestingLabTestingFeedbackObligationStatus;
  fulfilledAt?: string | null;
  feedback?: TestingLabTestingEventFeedbackProjection;
}

export type TestingLabTestingEventMode = "Online" | "InPerson" | "Hybrid";

export interface TestingLabTestingEventProjection {
  id?: string;
  name?: string | null;
  description?: string | null;
  mode?: TestingLabTestingEventMode;
  approvalMode?: TestingLabTestingEventApprovalMode;
  status?: TestingLabTestingEventStatus;
  managerUserId?: string;
  applicationsOpenAt?: string;
  applicationsCloseAt?: string;
  startsAt?: string;
  endsAt?: string;
  requiresFeedback?: boolean;
  learningCompletionRequirement?: TestingLabTestingLearningCompletionRequirement;
  courseId?: string | null;
  cohortId?: string | null;
  learningActivityId?: string | null;
  tenantId?: string | null;
  slotCount?: number;
  applicationCount?: number;
  recurrenceSeriesId?: string | null;
  recurrenceOccurrence?: number | null;
  recurrenceFrequency?: TestingLabTestingEventRecurrenceFrequency;
  recurrenceInterval?: number | null;
  recurrenceDaysOfWeek?: Array<SystemDayOfWeek> | null;
  recurrenceEndsAt?: string | null;
  recurrenceOccurrenceCount?: number | null;
}

export type TestingLabTestingEventRecurrenceFrequency =
  "Daily" | "Weekly" | "Monthly";

export interface TestingLabTestingEventRecurrenceInput {
  frequency?: TestingLabTestingEventRecurrenceFrequency;
  interval?: number;
  daysOfWeek?: Array<SystemDayOfWeek> | null;
  endsAt?: string | null;
  occurrenceCount?: number | null;
}

export interface TestingLabTestingEventSlot {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  eventId?: string;
  event?: TestingLabTestingEvent;
  locationId?: string | null;
  location?: TestingLabTestingLocation;
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  endsAt?: string;
  maxTesters?: number | null;
  maxProjects?: number | null;
  campusName?: string | null;
  roomName?: string | null;
  meetingUrl?: string | null;
  isTesterCapacityUnlimited?: boolean;
  isProjectCapacityUnlimited?: boolean;
}

export interface TestingLabTestingEventSlotProjection {
  id?: string;
  eventId?: string;
  locationId?: string | null;
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  endsAt?: string;
  maxTesters?: number | null;
  maxProjects?: number | null;
  campusName?: string | null;
  roomName?: string | null;
  meetingUrl?: string | null;
  approvedProjectCount?: number;
  registeredTesterCount?: number;
}

export type TestingLabTestingEventStatus =
  | "Draft"
  | "ApplicationsOpen"
  | "ApplicationsClosed"
  | "Scheduled"
  | "Active"
  | "Completed"
  | "Cancelled";

export interface TestingLabTestingFeedback {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  testingRequestId?: string | null;
  testingRequest?: TestingLabTestingInput;
  feedbackFormId?: string | null;
  feedbackForm?: TestingLabTestingFeedbackForm;
  eventId?: string | null;
  event?: TestingLabTestingEvent;
  applicationId?: string | null;
  application?: TestingLabTestingProjectApplication;
  userId: string;
  user?: IdentityUsersUser;
  sessionId?: string | null;
  session?: TestingLabTestingSession;
  testingContext: TestingLabTestingContext;
  feedbackData: string;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
  additionalNotes?: string | null;
  isReported?: boolean;
  qualityRating?: TestingLabFeedbackQuality;
  reportReason?: string | null;
  reportedById?: string | null;
  reportedByUserId?: string | null;
  reportedBy?: IdentityUsersUser;
  reportedAt?: string | null;
  qualityRatings?: Array<TestingLabFeedbackQualityRating> | null;
  isGlobal?: boolean;
  isPositive?: boolean;
  isNegative?: boolean;
  averageQualityRating?: number | null;
}

export interface TestingLabTestingFeedbackDirectoryItem {
  id?: string;
  source?: TestingLabTestingFeedbackSource;
  testingRequestId?: string | null;
  requestTitle?: string | null;
  eventId?: string | null;
  eventName?: string | null;
  applicationId?: string | null;
  projectId?: string | null;
  projectTitle?: string | null;
  projectVersionId?: string | null;
  projectVersion?: string | null;
  userId?: string;
  userName?: string | null;
  userEmail?: string | null;
  testingContext?: TestingLabTestingContext;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
  feedbackData?: string | null;
  additionalNotes?: string | null;
  isReported?: boolean;
  reportReason?: string | null;
  reportedByUserId?: string | null;
  reportedAt?: string | null;
  qualityRating?: TestingLabFeedbackQuality;
  createdAt?: string;
  updatedAt?: string;
}

export interface TestingLabTestingFeedbackDirectoryPage {
  items?: Array<TestingLabTestingFeedbackDirectoryItem> | null;
  totalCount?: number;
  skip?: number;
  take?: number;
}

export interface TestingLabTestingFeedbackForm {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  description?: string | null;
  formData: string;
  testingRequestId?: string | null;
  formSchema?: string | null;
  isForOnline?: boolean;
  isForSessions?: boolean;
  isActive?: boolean;
  formType?: TestingLabFeedbackFormType;
  formVersion?: number;
  tags?: string | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  isGlobal?: boolean;
  submissionCount?: number;
  tagArray?: Array<string> | null;
}

export interface TestingLabTestingFeedbackObligationProjection {
  id?: string;
  eventId?: string;
  slotId?: string;
  applicationId?: string;
  testerUserId?: string;
  feedbackId?: string | null;
  status?: TestingLabTestingFeedbackObligationStatus;
  fulfilledAt?: string | null;
}

export type TestingLabTestingFeedbackObligationStatus =
  "Pending" | "Fulfilled" | "Waived";

export type TestingLabTestingFeedbackSource = "Request" | "Event";

export interface TestingLabTestingInput {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  projectVersionId?: string | null;
  projectVersion?: ProjectsProjectVersion;
  title: string;
  description?: string | null;
  downloadUrl?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsContent?: string | null;
  instructionsUrl?: string | null;
  instructionsFileId?: string | null;
  feedbackFormContent?: string | null;
  maxTesters?: number | null;
  currentTesterCount?: number;
  startDate: string;
  endDate: string;
  status: TestingLabTestingRequestStatus;
  createdById: string;
  createdBy?: IdentityUsersUser;
  priority?: TestingLabTestingPriority;
  estimatedDurationHours?: number | null;
  mode?: TestingLabTestingMode;
  sessions?: Array<TestingLabTestingSession> | null;
  participants?: Array<TestingLabTestingParticipant> | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  feedbackForms?: Array<TestingLabTestingFeedbackForm> | null;
  isGlobal?: boolean;
  isActive?: boolean;
  acceptsNewTesters?: boolean;
  availableSpots?: number | null;
  duration?: string;
  daysRemaining?: number | null;
}

export interface TestingLabTestingLabAnalyticsReportProjection {
  fromDate?: string;
  toDate?: string;
  generatedAt?: string;
  current?: TestingLabTestingLabAnalyticsSummaryProjection;
  previous?: TestingLabTestingLabAnalyticsSummaryProjection;
  locations?: TestingLabTestingLabLocationAnalyticsProjection;
  trend?: Array<TestingLabTestingLabAnalyticsTrendProjection> | null;
  events?: Array<TestingLabTestingLabEventAnalyticsProjection> | null;
}

export interface TestingLabTestingLabAnalyticsSummaryProjection {
  events?: number;
  completedEvents?: number;
  applications?: number;
  approvedProjects?: number;
  registeredTesters?: number;
  attendedTesters?: number;
  feedback?: number;
  averageRating?: number | null;
  recommendationRate?: number | null;
  capacity?: number;
  fillRate?: number;
}

export interface TestingLabTestingLabAnalyticsTrendProjection {
  date?: string;
  events?: number;
  applications?: number;
  registrations?: number;
  attendance?: number;
  feedback?: number;
}

export interface TestingLabTestingLabEventAnalyticsProjection {
  eventId?: string;
  name?: string | null;
  status?: TestingLabTestingEventStatus;
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  applications?: number;
  approvedProjects?: number;
  registeredTesters?: number;
  attendedTesters?: number;
  feedback?: number;
  averageRating?: number | null;
  capacity?: number;
  fillRate?: number;
}

export interface TestingLabTestingLabLocationAnalyticsProjection {
  total?: number;
  active?: number;
}

export interface TestingLabTestingLabPermissions {
  canCreateSessions?: boolean;
  canEditSessions?: boolean;
  canDeleteSessions?: boolean;
  canViewSessions?: boolean;
  canCreateLocations?: boolean;
  canEditLocations?: boolean;
  canDeleteLocations?: boolean;
  canViewLocations?: boolean;
  canCreateFeedback?: boolean;
  canEditFeedback?: boolean;
  canDeleteFeedback?: boolean;
  canViewFeedback?: boolean;
  canModerateFeedback?: boolean;
  canCreateRequests?: boolean;
  canEditRequests?: boolean;
  canDeleteRequests?: boolean;
  canViewRequests?: boolean;
  canApproveRequests?: boolean;
  canManageParticipants?: boolean;
  canViewParticipants?: boolean;
  canCreateEvents?: boolean;
  canEditEvents?: boolean;
  canDeleteEvents?: boolean;
  canViewEvents?: boolean;
  canViewApplications?: boolean;
  canApproveApplications?: boolean;
  canManageApplications?: boolean;
  canViewAnalytics?: boolean;
}

export interface TestingLabTestingLabResourcePermission {
  action?: string | null;
  resourceType?: string | null;
  resourceId?: string;
  expiresAt?: string | null;
}

export interface TestingLabTestingLabRoleTemplate {
  id?: string;
  name?: string | null;
  description?: string | null;
  isSystemRole?: boolean;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabTestingLabSettings {
  id?: string;
  labName?: string | null;
  description?: string | null;
  timezone?: string | null;
  defaultSessionDuration?: number;
  allowPublicSignups?: boolean;
  requireApproval?: boolean;
  enableNotifications?: boolean;
  maxSimultaneousSessions?: number;
  tenantId?: string | null;
  createdAt?: string;
  updatedAt?: string;
}

/** A comma-separated combination of the declared flag names. */
export type TestingLabTestingLearningCompletionRequirement = string;

export interface TestingLabTestingLocation {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  name: string;
  description?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  capacity?: number | null;
  maxTestersCapacity?: number;
  maxProjectsCapacity?: number;
  equipment?: string | null;
  equipmentAvailable?: string | null;
  isVirtual?: boolean;
  virtualUrl?: string | null;
  status?: TestingLabLocationStatus;
  contactEmail?: string | null;
  contactPhone?: string | null;
  sessions?: Array<TestingLabTestingSession> | null;
  isGlobal?: boolean;
  isAvailable?: boolean;
  fullAddress?: string | null;
  activeSessionCount?: number;
}

export type TestingLabTestingMode = "Online" | "InPerson" | "Hybrid";

export interface TestingLabTestingParticipant {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  testingRequestId: string;
  testingRequest?: TestingLabTestingInput;
  userId: string;
  user?: IdentityUsersUser;
  instructionsAcknowledged: boolean;
  instructionsAcknowledgedAt?: string | null;
  startedAt: string;
  completedAt?: string | null;
  timeSpentMinutes?: number | null;
  feedbackCount?: number;
  status?: TestingLabParticipationStatus;
  notes?: string | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  isGlobal?: boolean;
  isActive?: boolean;
  isCompleted?: boolean;
  participationDuration?: string | null;
  canProvideFeedback?: boolean;
}

export interface TestingLabTestingParticipantDirectoryItemProjection {
  registrationId?: string;
  eventId?: string;
  eventName?: string | null;
  slotId?: string;
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  endsAt?: string;
  campusName?: string | null;
  roomName?: string | null;
  userId?: string;
  userName?: string | null;
  userEmail?: string | null;
  avatarUrl?: string | null;
  status?: TestingLabTestingSlotRegistrationStatus;
  waitlistPosition?: number | null;
  notes?: string | null;
  registeredAt?: string;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  completedAt?: string | null;
  pendingFeedbackCount?: number;
}

export interface TestingLabTestingParticipantDirectoryProjection {
  items?: Array<TestingLabTestingParticipantDirectoryItemProjection> | null;
  totalCount?: number;
  registeredCount?: number;
  waitlistedCount?: number;
  checkedInCount?: number;
  attendedCount?: number;
  completedCount?: number;
  noShowCount?: number;
}

export interface TestingLabTestingParticipantMutationProjection {
  id?: string;
  testingRequestId?: string;
  userId?: string;
  status?: TestingLabParticipationStatus;
  startedAt?: string;
}

export type TestingLabTestingPriority = "Low" | "Medium" | "High" | "Critical";

export interface TestingLabTestingProjectApplication {
  isGlobal?: boolean;
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  eventId?: string;
  event?: TestingLabTestingEvent;
  projectId?: string;
  project?: ProjectsProject;
  projectVersionId?: string | null;
  projectVersion?: ProjectsProjectVersion;
  submittedByUserId?: string;
  submittedBy?: IdentityUsersUser;
  submittedAssetReferenceIdsJson?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
  preferredAvailability?: string | null;
  status?: TestingLabTestingApplicationStatus;
  assignedSlotId?: string | null;
  assignedSlot?: TestingLabTestingEventSlot;
  decidedByUserId?: string | null;
  decidedBy?: IdentityUsersUser;
  decisionRationale?: string | null;
  decidedAt?: string | null;
  votes?: Array<TestingLabTestingApplicationVote> | null;
}

export interface TestingLabTestingProjectApplicationProjection {
  id?: string;
  eventId?: string;
  projectId?: string;
  projectVersionId?: string | null;
  submittedByUserId?: string;
  preferredAvailability?: string | null;
  status?: TestingLabTestingApplicationStatus;
  assignedSlotId?: string | null;
  decidedByUserId?: string | null;
  decisionRationale?: string | null;
  decidedAt?: string | null;
  votes?: Array<TestingLabTestingApplicationVoteProjection> | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface TestingLabTestingRequestDetailProjection {
  id?: string;
  title?: string | null;
  description?: string | null;
  downloadUrl?: string | null;
  instructionsContent?: string | null;
  feedbackFormContent?: string | null;
  maxTesters?: number | null;
  currentTesterCount?: number;
  startDate?: string;
  endDate?: string;
  status?: TestingLabTestingRequestStatus;
  projectVersionId?: string | null;
  projectVersion?: TestingLabTestingRequestProjectVersionProjection;
  isDeleted?: boolean;
}

export interface TestingLabTestingRequestProjectProjection {
  id?: string;
  title?: string | null;
  slug?: string | null;
}

export interface TestingLabTestingRequestProjectVersionProjection {
  id?: string;
  projectId?: string;
  versionNumber?: string | null;
  status?: string | null;
  project?: TestingLabTestingRequestProjectProjection;
}

export type TestingLabTestingRequestStatus =
  | "Draft"
  | "Open"
  | "Active"
  | "InProgress"
  | "Paused"
  | "Completed"
  | "Cancelled";

export interface TestingLabTestingSession {
  isNew?: boolean;
  isDeleted?: boolean;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  version?: number;
  createdAt: string;
  updatedAt: string;
  deletedAt?: string | null;
  tenantId?: string | null;
  eventSlotId?: string | null;
  eventSlot?: TestingLabTestingEventSlot;
  testingRequestId: string;
  testingRequest?: TestingLabTestingInput;
  locationId: string;
  location?: TestingLabTestingLocation;
  sessionName: string;
  sessionDate: string;
  startTime: string;
  endTime: string;
  maxTesters: number;
  maxProjects: number;
  registeredTesterCount?: number;
  registeredProjectMemberCount?: number;
  registeredProjectCount?: number;
  status: TestingLabSessionStatus;
  managerId: string;
  manager?: IdentityUsersUser;
  managerUserId?: string;
  createdById: string;
  createdBy?: IdentityUsersUser;
  registrations?: Array<TestingLabSessionRegistration> | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  isGlobal?: boolean;
  isActive?: boolean;
  isCompleted?: boolean;
  allowsRegistration?: boolean;
  availableSpots?: number;
  duration?: string;
}

export interface TestingLabTestingSlotRegistrationProjection {
  id?: string;
  eventId?: string;
  slotId?: string;
  userId?: string;
  status?: TestingLabTestingSlotRegistrationStatus;
  waitlistPosition?: number | null;
  notes?: string | null;
  registeredAt?: string;
  promotedAt?: string | null;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  completedAt?: string | null;
  pendingFeedbackCount?: number;
}

export type TestingLabTestingSlotRegistrationStatus =
  | "Registered"
  | "Waitlisted"
  | "CheckedIn"
  | "Attended"
  | "Completed"
  | "Cancelled"
  | "NoShow";

export interface TestingLabUpdateAttendance {
  userId?: string;
  attendanceStatus?: TestingLabAttendanceStatus;
}

export interface TestingLabUpdateTestingEventInput {
  name?: string | null;
  description?: string | null;
  mode?: TestingLabTestingEventMode;
  approvalMode?: TestingLabTestingEventApprovalMode;
  applicationsOpenAt?: string;
  applicationsCloseAt?: string;
  startsAt?: string;
  endsAt?: string;
  requiresFeedback?: boolean;
}

export interface TestingLabUpdateTestingInput {
  projectVersionId?: string | null;
  title?: string | null;
  description?: string | null;
  downloadUrl?: string | null;
  instructionsType?: TestingLabInstructionType;
  instructionsContent?: string | null;
  instructionsUrl?: string | null;
  instructionsFileId?: string | null;
  maxTesters?: number | null;
  feedbackFormContent?: string | null;
  startDate?: string | null;
  endDate?: string | null;
  status?: TestingLabTestingRequestStatus;
}

export interface TestingLabUpdateTestingLabRoleInput {
  name?: string | null;
  description?: string | null;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabUpdateTestingLabSettings {
  labName?: string | null;
  description?: string | null;
  timezone?: string | null;
  defaultSessionDuration?: number | null;
  allowPublicSignups?: boolean | null;
  requireApproval?: boolean | null;
  enableNotifications?: boolean | null;
  maxSimultaneousSessions?: number | null;
}

export interface TestingLabUpdateTestingLocation {
  name?: string | null;
  description?: string | null;
  address?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  country?: string | null;
  maxTestersCapacity?: number | null;
  maxProjectsCapacity?: number | null;
  equipmentAvailable?: string | null;
  isVirtual?: boolean | null;
  virtualUrl?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  status?: TestingLabLocationStatus;
}

export interface TestingLabUpdateTestingProjectApplicationInput {
  projectVersionId?: string;
  preferredAvailability?: string | null;
  submittedAssetReferenceIds?: Array<string> | null;
}

export interface TestingLabUpsertTestingEventSlotInput {
  mode?: TestingLabTestingEventMode;
  startsAt?: string;
  endsAt?: string;
  maxTesters?: number | null;
  maxProjects?: number | null;
  campusName?: string | null;
  roomName?: string | null;
  meetingUrl?: string | null;
  locationId?: string | null;
}

export interface TestingLabUserTestingLabPermissions {
  userId?: string;
  tenantId?: string | null;
  assignedRoles?: Array<string> | null;
  permissions?: TestingLabTestingLabPermissions;
  resourcePermissions?: Array<TestingLabTestingLabResourcePermission> | null;
}

// Zod Schema Declarations (to handle circular references)
export let AIAiChatInputSchema: z.ZodType<AIAiChatInput>;
export let AIAiChatMessageSchema: z.ZodType<AIAiChatMessage>;
export let AIAiCompletionOutputSchema: z.ZodType<AIAiCompletionOutput>;
export let AIAiConversationHistoryEntrySchema: z.ZodType<AIAiConversationHistoryEntry>;
export let AIAiGeneratedContentDraftInputSchema: z.ZodType<AIAiGeneratedContentDraftInput>;
export let AIAiGeneratedContentInputSchema: z.ZodType<AIAiGeneratedContentInput>;
export let AIAiGeneratedContentKindSchema: z.ZodType<AIAiGeneratedContentKind>;
export let AIAiGenerateInputSchema: z.ZodType<AIAiGenerateInput>;
export let AIAiPromptTemplateSchema: z.ZodType<AIAiPromptTemplate>;
export let AIAiPromptTemplateGenerateInputSchema: z.ZodType<AIAiPromptTemplateGenerateInput>;
export let AIAiPromptTemplateRenderInputSchema: z.ZodType<AIAiPromptTemplateRenderInput>;
export let AIAiPromptTemplateRenderOutputSchema: z.ZodType<AIAiPromptTemplateRenderOutput>;
export let AIAiProviderStatusSchema: z.ZodType<AIAiProviderStatus>;
export let AIAiQuotaStatusSchema: z.ZodType<AIAiQuotaStatus>;
export let AIAiQuotaStatusOutputSchema: z.ZodType<AIAiQuotaStatusOutput>;
export let AIAiStatusOutputSchema: z.ZodType<AIAiStatusOutput>;
export let AIAiUsageSchema: z.ZodType<AIAiUsage>;
export let AICreateAiPromptTemplateInputSchema: z.ZodType<AICreateAiPromptTemplateInput>;
export let AIUpdateAiPromptTemplateInputSchema: z.ZodType<AIUpdateAiPromptTemplateInput>;
export let AnalyticsAnalyticsWarehouseFactSchema: z.ZodType<AnalyticsAnalyticsWarehouseFact>;
export let AnalyticsAnalyticsWarehouseRunInputSchema: z.ZodType<AnalyticsAnalyticsWarehouseRunInput>;
export let AnalyticsAnalyticsWarehouseRunOutputSchema: z.ZodType<AnalyticsAnalyticsWarehouseRunOutput>;
export let AnalyticsAnalyzeFunnelQuerySchema: z.ZodType<AnalyticsAnalyzeFunnelQuery>;
export let AnalyticsCreateDashboardInputSchema: z.ZodType<AnalyticsCreateDashboardInput>;
export let AnalyticsDashboardSchema: z.ZodType<AnalyticsDashboard>;
export let AnalyticsDashboardWidgetSchema: z.ZodType<AnalyticsDashboardWidget>;
export let AnalyticsDashboardWidgetInputSchema: z.ZodType<AnalyticsDashboardWidgetInput>;
export let AnalyticsProductCapacityMetricsSchema: z.ZodType<AnalyticsProductCapacityMetrics>;
export let AnalyticsProductCatalogMetricsSchema: z.ZodType<AnalyticsProductCatalogMetrics>;
export let AnalyticsProductMetricsExportFormatSchema: z.ZodType<AnalyticsProductMetricsExportFormat>;
export let AnalyticsProductMetricsOutputSchema: z.ZodType<AnalyticsProductMetricsOutput>;
export let AnalyticsProductMetricThresholdSchema: z.ZodType<AnalyticsProductMetricThreshold>;
export let AnalyticsProductMetricThresholdStatusSchema: z.ZodType<AnalyticsProductMetricThresholdStatus>;
export let AnalyticsProductRevenueMetricsSchema: z.ZodType<AnalyticsProductRevenueMetrics>;
export let AnalyticsProductSubscriptionMetricsSchema: z.ZodType<AnalyticsProductSubscriptionMetrics>;
export let AnalyticsTimeSeriesGranularitySchema: z.ZodType<AnalyticsTimeSeriesGranularity>;
export let AnalyticsTrackAnalyticsEventCommandSchema: z.ZodType<AnalyticsTrackAnalyticsEventCommand>;
export let AnalyticsUpdateDashboardInputSchema: z.ZodType<AnalyticsUpdateDashboardInput>;
export let AnalyticsWidgetTypeSchema: z.ZodType<AnalyticsWidgetType>;
export let APIAccessAccessCapabilitiesOutputSchema: z.ZodType<APIAccessAccessCapabilitiesOutput>;
export let APIControllersApplicationDetailsSchema: z.ZodType<APIControllersApplicationDetails>;
export let APIControllersApplicationInfoOutputSchema: z.ZodType<APIControllersApplicationInfoOutput>;
export let APIControllersBuildDetailsSchema: z.ZodType<APIControllersBuildDetails>;
export let APIControllersDependencyHealthItemSchema: z.ZodType<APIControllersDependencyHealthItem>;
export let APIControllersDependencyHealthOutputSchema: z.ZodType<APIControllersDependencyHealthOutput>;
export let APIControllersEconomySelfServiceCapabilitySchema: z.ZodType<APIControllersEconomySelfServiceCapability>;
export let APIControllersHealthinessOutputSchema: z.ZodType<APIControllersHealthinessOutput>;
export let APIControllersHealthinessResponseItemSchema: z.ZodType<APIControllersHealthinessResponseItem>;
export let APIControllersLivenessOutputSchema: z.ZodType<APIControllersLivenessOutput>;
export let APIControllersProcessDetailsSchema: z.ZodType<APIControllersProcessDetails>;
export let APIControllersReadinessOutputSchema: z.ZodType<APIControllersReadinessOutput>;
export let APIControllersRuntimeDetailsSchema: z.ZodType<APIControllersRuntimeDetails>;
export let APIProjectsAddProjectTeamInputSchema: z.ZodType<APIProjectsAddProjectTeamInput>;
export let APIProjectsCounterProjectTeamAgreementInputSchema: z.ZodType<APIProjectsCounterProjectTeamAgreementInput>;
export let APIProjectsCreateProjectAllocationInputSchema: z.ZodType<APIProjectsCreateProjectAllocationInput>;
export let APIProjectsCreateProjectTeamAgreementInputSchema: z.ZodType<APIProjectsCreateProjectTeamAgreementInput>;
export let APIProjectsProjectAllocationSchema: z.ZodType<APIProjectsProjectAllocation>;
export let APIProjectsProjectOwnershipSchema: z.ZodType<APIProjectsProjectOwnership>;
export let APIProjectsProjectTeamAgreementSchema: z.ZodType<APIProjectsProjectTeamAgreement>;
export let APIProjectsProjectTeamOwnershipSchema: z.ZodType<APIProjectsProjectTeamOwnership>;
export let APIProjectsTransferProjectOwnerTeamInputSchema: z.ZodType<APIProjectsTransferProjectOwnerTeamInput>;
export let APIProjectsUpdateProjectAllocationInputSchema: z.ZodType<APIProjectsUpdateProjectAllocationInput>;
export let APIProjectsUpdateProjectTeamInputSchema: z.ZodType<APIProjectsUpdateProjectTeamInput>;
export let APIProjectWorkAddProjectTaskChecklistInputSchema: z.ZodType<APIProjectWorkAddProjectTaskChecklistInput>;
export let APIProjectWorkAddProjectTaskCommentInputSchema: z.ZodType<APIProjectWorkAddProjectTaskCommentInput>;
export let APIProjectWorkAddProjectTaskDependencyInputSchema: z.ZodType<APIProjectWorkAddProjectTaskDependencyInput>;
export let APIProjectWorkConfigureProjectWorkColumnInputSchema: z.ZodType<APIProjectWorkConfigureProjectWorkColumnInput>;
export let APIProjectWorkCreateProjectMilestoneInputSchema: z.ZodType<APIProjectWorkCreateProjectMilestoneInput>;
export let APIProjectWorkCreateProjectTaskLabelInputSchema: z.ZodType<APIProjectWorkCreateProjectTaskLabelInput>;
export let APIProjectWorkCreateProjectWorkTaskInputSchema: z.ZodType<APIProjectWorkCreateProjectWorkTaskInput>;
export let APIProjectWorkMoveProjectWorkTaskInputSchema: z.ZodType<APIProjectWorkMoveProjectWorkTaskInput>;
export let APIProjectWorkProjectBoardSchema: z.ZodType<APIProjectWorkProjectBoard>;
export let APIProjectWorkProjectChecklistItemSchema: z.ZodType<APIProjectWorkProjectChecklistItem>;
export let APIProjectWorkProjectMilestoneSchema: z.ZodType<APIProjectWorkProjectMilestone>;
export let APIProjectWorkProjectTaskCommentSchema: z.ZodType<APIProjectWorkProjectTaskComment>;
export let APIProjectWorkProjectTaskDependencySchema: z.ZodType<APIProjectWorkProjectTaskDependency>;
export let APIProjectWorkProjectTaskLabelSchema: z.ZodType<APIProjectWorkProjectTaskLabel>;
export let APIProjectWorkProjectWorkColumnSchema: z.ZodType<APIProjectWorkProjectWorkColumn>;
export let APIProjectWorkProjectWorkHistorySchema: z.ZodType<APIProjectWorkProjectWorkHistory>;
export let APIProjectWorkProjectWorkTaskSchema: z.ZodType<APIProjectWorkProjectWorkTask>;
export let APIProjectWorkProjectWorkTaskDetailsSchema: z.ZodType<APIProjectWorkProjectWorkTaskDetails>;
export let APIProjectWorkUpdateProjectMilestoneInputSchema: z.ZodType<APIProjectWorkUpdateProjectMilestoneInput>;
export let APIProjectWorkUpdateProjectTaskChecklistInputSchema: z.ZodType<APIProjectWorkUpdateProjectTaskChecklistInput>;
export let APIProjectWorkUpdateProjectTaskCommentInputSchema: z.ZodType<APIProjectWorkUpdateProjectTaskCommentInput>;
export let APIProjectWorkUpdateProjectWorkTaskInputSchema: z.ZodType<APIProjectWorkUpdateProjectWorkTaskInput>;
export let APISetupEconomyCapabilityReadinessStateSchema: z.ZodType<APISetupEconomyCapabilityReadinessState>;
export let APITeamsAcceptTeamInvitationInputSchema: z.ZodType<APITeamsAcceptTeamInvitationInput>;
export let APITeamsAddTeamMemberInputSchema: z.ZodType<APITeamsAddTeamMemberInput>;
export let APITeamsChangeTeamMemberInputSchema: z.ZodType<APITeamsChangeTeamMemberInput>;
export let APITeamsCreateTeamInputSchema: z.ZodType<APITeamsCreateTeamInput>;
export let APITeamsCreateTeamInvitationInputSchema: z.ZodType<APITeamsCreateTeamInvitationInput>;
export let APITeamsMyTeamInvitationSchema: z.ZodType<APITeamsMyTeamInvitation>;
export let APITeamsTeamSchema: z.ZodType<APITeamsTeam>;
export let APITeamsTeamInvitationSchema: z.ZodType<APITeamsTeamInvitation>;
export let APITeamsTeamInvitationCreatedSchema: z.ZodType<APITeamsTeamInvitationCreated>;
export let APITeamsTeamMemberSchema: z.ZodType<APITeamsTeamMember>;
export let APITeamsTeamProjectSummarySchema: z.ZodType<APITeamsTeamProjectSummary>;
export let APITeamsUpdateTeamInputSchema: z.ZodType<APITeamsUpdateTeamInput>;
export let AssetsAssetAccessPolicySchema: z.ZodType<AssetsAssetAccessPolicy>;
export let AssetsAssetAccessUrlSchema: z.ZodType<AssetsAssetAccessUrl>;
export let AssetsAssetFolderRestrictionModeSchema: z.ZodType<AssetsAssetFolderRestrictionMode>;
export let AssetsAssetKindSchema: z.ZodType<AssetsAssetKind>;
export let AssetsAssetUploadResultSchema: z.ZodType<AssetsAssetUploadResult>;
export let AssetsChunkedUploadSessionSchema: z.ZodType<AssetsChunkedUploadSession>;
export let AssetsCommandsBulkDeleteAssetItemSchema: z.ZodType<AssetsCommandsBulkDeleteAssetItem>;
export let AssetsCommandsBulkDeleteAssetsOutputSchema: z.ZodType<AssetsCommandsBulkDeleteAssetsOutput>;
export let AssetsCommandsBulkUploadAssetItemSchema: z.ZodType<AssetsCommandsBulkUploadAssetItem>;
export let AssetsCommandsBulkUploadAssetsOutputSchema: z.ZodType<AssetsCommandsBulkUploadAssetsOutput>;
export let AssetsControllersAssetExtractedTextOutputSchema: z.ZodType<AssetsControllersAssetExtractedTextOutput>;
export let AssetsControllersBulkAssetAccessUrlInputSchema: z.ZodType<AssetsControllersBulkAssetAccessUrlInput>;
export let AssetsControllersBulkDeleteAssetsInputSchema: z.ZodType<AssetsControllersBulkDeleteAssetsInput>;
export let AssetsControllersContentModerationInputSchema: z.ZodType<AssetsControllersContentModerationInput>;
export let AssetsControllersCopyAssetReferenceInputSchema: z.ZodType<AssetsControllersCopyAssetReferenceInput>;
export let AssetsControllersCreateAssetFolderInputSchema: z.ZodType<AssetsControllersCreateAssetFolderInput>;
export let AssetsControllersMarkNonDeletableInputSchema: z.ZodType<AssetsControllersMarkNonDeletableInput>;
export let AssetsControllersReportAssetInputSchema: z.ZodType<AssetsControllersReportAssetInput>;
export let AssetsControllersRestrictAssetFolderInputSchema: z.ZodType<AssetsControllersRestrictAssetFolderInput>;
export let AssetsControllersReviewReportInputSchema: z.ZodType<AssetsControllersReviewReportInput>;
export let AssetsControllersUpdateAssetInputSchema: z.ZodType<AssetsControllersUpdateAssetInput>;
export let AssetsControllersUpdateVirusScanInputSchema: z.ZodType<AssetsControllersUpdateVirusScanInput>;
export let AssetsImageFitSchema: z.ZodType<AssetsImageFit>;
export let AssetsImageFormatSchema: z.ZodType<AssetsImageFormat>;
export let AssetsModerationStatusSchema: z.ZodType<AssetsModerationStatus>;
export let AssetsQueriesAssetPreviewOutputSchema: z.ZodType<AssetsQueriesAssetPreviewOutput>;
export let AssetsQueriesAssetRetentionCandidateOutputSchema: z.ZodType<AssetsQueriesAssetRetentionCandidateOutput>;
export let AssetsQueriesAssetRetentionReportOutputSchema: z.ZodType<AssetsQueriesAssetRetentionReportOutput>;
export let AssetsQueriesAssetSearchOutputSchema: z.ZodType<AssetsQueriesAssetSearchOutput>;
export let AssetsQueriesAssetSearchResultSchema: z.ZodType<AssetsQueriesAssetSearchResult>;
export let AssetsQueriesAssetStatisticsOutputSchema: z.ZodType<AssetsQueriesAssetStatisticsOutput>;
export let AssetsQueriesBulkAssetAccessUrlItemSchema: z.ZodType<AssetsQueriesBulkAssetAccessUrlItem>;
export let AssetsQueriesBulkAssetAccessUrlsOutputSchema: z.ZodType<AssetsQueriesBulkAssetAccessUrlsOutput>;
export let AssetsReportReasonSchema: z.ZodType<AssetsReportReason>;
export let AssetsReviewDecisionSchema: z.ZodType<AssetsReviewDecision>;
export let AssetsSecurityAccessUrlInputSchema: z.ZodType<AssetsSecurityAccessUrlInput>;
export let AssetsVirusScanStatusSchema: z.ZodType<AssetsVirusScanStatus>;
export let BillingCycleSchema: z.ZodType<BillingCycle>;
export let BulkOperationErrorSchema: z.ZodType<BulkOperationError>;
export let BulkOperationOutputSchema: z.ZodType<BulkOperationOutput>;
export let CommerceBillingInvoicePaymentRetryResultSchema: z.ZodType<CommerceBillingInvoicePaymentRetryResult>;
export let CommerceBillingInvoiceStatusSchema: z.ZodType<CommerceBillingInvoiceStatus>;
export let CommerceOrderChargeStateSchema: z.ZodType<CommerceOrderChargeState>;
export let CommerceOrdersAddOrderItemInputSchema: z.ZodType<CommerceOrdersAddOrderItemInput>;
export let CommerceOrdersCaptureOrderInputSchema: z.ZodType<CommerceOrdersCaptureOrderInput>;
export let CommerceOrdersCompleteOrderInputSchema: z.ZodType<CommerceOrdersCompleteOrderInput>;
export let CommerceOrdersCreateOrderInputSchema: z.ZodType<CommerceOrdersCreateOrderInput>;
export let CommerceOrdersOrderSchema: z.ZodType<CommerceOrdersOrder>;
export let CommerceOrdersOrderCaptureSchema: z.ZodType<CommerceOrdersOrderCapture>;
export let CommerceOrdersOrderLineItemSchema: z.ZodType<CommerceOrdersOrderLineItem>;
export let CommerceOrdersOrderStatusSchema: z.ZodType<CommerceOrdersOrderStatus>;
export let CommercePaymentsBillingChargesControllerCancelBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerCancelBillingChargeInput>;
export let CommercePaymentsBillingChargesControllerCreateBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerCreateBillingChargeInput>;
export let CommercePaymentsBillingChargesControllerRefundBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerRefundBillingChargeInput>;
export let CommercePaymentsCalculateTaxInputSchema: z.ZodType<CommercePaymentsCalculateTaxInput>;
export let CommercePaymentsCreateTaxJurisdictionInputSchema: z.ZodType<CommercePaymentsCreateTaxJurisdictionInput>;
export let CommercePaymentsCreateTaxRuleInputSchema: z.ZodType<CommercePaymentsCreateTaxRuleInput>;
export let CommercePaymentsCreateWalletInputSchema: z.ZodType<CommercePaymentsCreateWalletInput>;
export let CommercePaymentsCustomerTypeSchema: z.ZodType<CommercePaymentsCustomerType>;
export let CommercePaymentsLockWalletInputSchema: z.ZodType<CommercePaymentsLockWalletInput>;
export let CommercePaymentsModelsFreezeWalletInputSchema: z.ZodType<CommercePaymentsModelsFreezeWalletInput>;
export let CommercePaymentsModelsPatchWalletInputSchema: z.ZodType<CommercePaymentsModelsPatchWalletInput>;
export let CommercePaymentsPatchTaxJurisdictionInputSchema: z.ZodType<CommercePaymentsPatchTaxJurisdictionInput>;
export let CommercePaymentsPatchTaxRuleInputSchema: z.ZodType<CommercePaymentsPatchTaxRuleInput>;
export let CommercePaymentsPaymentCancellationResultSchema: z.ZodType<CommercePaymentsPaymentCancellationResult>;
export let CommercePaymentsPaymentResultSchema: z.ZodType<CommercePaymentsPaymentResult>;
export let CommercePaymentsPaymentRetryResultSchema: z.ZodType<CommercePaymentsPaymentRetryResult>;
export let CommercePaymentsPaymentsControllerCancelPaymentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCancelPaymentInput>;
export let CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput>;
export let CommercePaymentsPaymentsControllerCreateSetupIntentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCreateSetupIntentInput>;
export let CommercePaymentsPaymentsControllerCreateSetupIntentOutputSchema: z.ZodType<CommercePaymentsPaymentsControllerCreateSetupIntentOutput>;
export let CommercePaymentsPaymentsControllerProcessPaymentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerProcessPaymentInput>;
export let CommercePaymentsPaymentsControllerRefundInputSchema: z.ZodType<CommercePaymentsPaymentsControllerRefundInput>;
export let CommercePaymentsPaymentStatusSchema: z.ZodType<CommercePaymentsPaymentStatus>;
export let CommercePaymentsProcessRefundResultSchema: z.ZodType<CommercePaymentsProcessRefundResult>;
export let CommercePaymentsTaxBreakdownSchema: z.ZodType<CommercePaymentsTaxBreakdown>;
export let CommercePaymentsTaxCalculationResultSchema: z.ZodType<CommercePaymentsTaxCalculationResult>;
export let CommercePaymentsTaxExemptionValidationResultSchema: z.ZodType<CommercePaymentsTaxExemptionValidationResult>;
export let CommercePaymentsTaxJurisdictionSchema: z.ZodType<CommercePaymentsTaxJurisdiction>;
export let CommercePaymentsTaxJurisdictionDtoSchema: z.ZodType<CommercePaymentsTaxJurisdictionDto>;
export let CommercePaymentsTaxJurisdictionTypeSchema: z.ZodType<CommercePaymentsTaxJurisdictionType>;
export let CommercePaymentsTaxRateSchema: z.ZodType<CommercePaymentsTaxRate>;
export let CommercePaymentsTaxRuleSchema: z.ZodType<CommercePaymentsTaxRule>;
export let CommercePaymentsTaxRuleDtoSchema: z.ZodType<CommercePaymentsTaxRuleDto>;
export let CommercePaymentsTaxRuleTypeSchema: z.ZodType<CommercePaymentsTaxRuleType>;
export let CommercePaymentsTaxTypeSchema: z.ZodType<CommercePaymentsTaxType>;
export let CommercePaymentsTransactionStatusSchema: z.ZodType<CommercePaymentsTransactionStatus>;
export let CommercePaymentsUserWalletSchema: z.ZodType<CommercePaymentsUserWallet>;
export let CommercePaymentsValidateTaxExemptionInputSchema: z.ZodType<CommercePaymentsValidateTaxExemptionInput>;
export let CommercePaymentsWalletTransactionSchema: z.ZodType<CommercePaymentsWalletTransaction>;
export let CommercePaymentsWalletTransactionTypeSchema: z.ZodType<CommercePaymentsWalletTransactionType>;
export let CommerceProductsAddMySupportTicketMessageInputSchema: z.ZodType<CommerceProductsAddMySupportTicketMessageInput>;
export let CommerceProductsAddSupportTicketMessageInputSchema: z.ZodType<CommerceProductsAddSupportTicketMessageInput>;
export let CommerceProductsAppliedPromoCodeSchema: z.ZodType<CommerceProductsAppliedPromoCode>;
export let CommerceProductsApplyPromoCodesInputSchema: z.ZodType<CommerceProductsApplyPromoCodesInput>;
export let CommerceProductsAssignSupportTicketInputSchema: z.ZodType<CommerceProductsAssignSupportTicketInput>;
export let CommerceProductsBatchCreateProductsInputSchema: z.ZodType<CommerceProductsBatchCreateProductsInput>;
export let CommerceProductsBatchProductCreateItemSchema: z.ZodType<CommerceProductsBatchProductCreateItem>;
export let CommerceProductsCheckMultipleAccessInputSchema: z.ZodType<CommerceProductsCheckMultipleAccessInput>;
export let CommerceProductsCloseSupportTicketInputSchema: z.ZodType<CommerceProductsCloseSupportTicketInput>;
export let CommerceProductsCreateMySupportTicketInputSchema: z.ZodType<CommerceProductsCreateMySupportTicketInput>;
export let CommerceProductsCreateProductInputSchema: z.ZodType<CommerceProductsCreateProductInput>;
export let CommerceProductsCreatePromoCodeInputSchema: z.ZodType<CommerceProductsCreatePromoCodeInput>;
export let CommerceProductsCreateSupportTicketInputSchema: z.ZodType<CommerceProductsCreateSupportTicketInput>;
export let CommerceProductsEntitlementCheckResultSchema: z.ZodType<CommerceProductsEntitlementCheckResult>;
export let CommerceProductsEntitlementInfoSchema: z.ZodType<CommerceProductsEntitlementInfo>;
export let CommerceProductsGrantEntitlementInputSchema: z.ZodType<CommerceProductsGrantEntitlementInput>;
export let CommerceProductsPatchProductInputSchema: z.ZodType<CommerceProductsPatchProductInput>;
export let CommerceProductsPatchPromoCodeInputSchema: z.ZodType<CommerceProductsPatchPromoCodeInput>;
export let CommerceProductsProductSchema: z.ZodType<CommerceProductsProduct>;
export let CommerceProductsProductAcquisitionTypeSchema: z.ZodType<CommerceProductsProductAcquisitionType>;
export let CommerceProductsProductPricingSchema: z.ZodType<CommerceProductsProductPricing>;
export let CommerceProductsProductTypeSchema: z.ZodType<CommerceProductsProductType>;
export let CommerceProductsPromoCodeSchema: z.ZodType<CommerceProductsPromoCode>;
export let CommerceProductsPromoCodeApplicationResultSchema: z.ZodType<CommerceProductsPromoCodeApplicationResult>;
export let CommerceProductsPromoCodeTypeSchema: z.ZodType<CommerceProductsPromoCodeType>;
export let CommerceProductsPromoCodeUsageSchema: z.ZodType<CommerceProductsPromoCodeUsage>;
export let CommerceProductsPromoCodeValidationResultSchema: z.ZodType<CommerceProductsPromoCodeValidationResult>;
export let CommerceProductsRejectedPromoCodeSchema: z.ZodType<CommerceProductsRejectedPromoCode>;
export let CommerceProductsResolveSupportTicketInputSchema: z.ZodType<CommerceProductsResolveSupportTicketInput>;
export let CommerceProductsRevokeEntitlementInputSchema: z.ZodType<CommerceProductsRevokeEntitlementInput>;
export let CommerceProductsSupportTicketSchema: z.ZodType<CommerceProductsSupportTicket>;
export let CommerceProductsSupportTicketMessageSchema: z.ZodType<CommerceProductsSupportTicketMessage>;
export let CommerceProductsSupportTicketMessageAuthorTypeSchema: z.ZodType<CommerceProductsSupportTicketMessageAuthorType>;
export let CommerceProductsSupportTicketPrioritySchema: z.ZodType<CommerceProductsSupportTicketPriority>;
export let CommerceProductsSupportTicketStatusSchema: z.ZodType<CommerceProductsSupportTicketStatus>;
export let CommerceProductsUpdateProductInputSchema: z.ZodType<CommerceProductsUpdateProductInput>;
export let CommerceProductsUpdatePromoCodeInputSchema: z.ZodType<CommerceProductsUpdatePromoCodeInput>;
export let CommerceProductsValidatePromoCodeInputSchema: z.ZodType<CommerceProductsValidatePromoCodeInput>;
export let CommerceSubscriptionsBillingHistorySchema: z.ZodType<CommerceSubscriptionsBillingHistory>;
export let CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput>;
export let CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput>;
export let CommerceSubscriptionsCancellationReasonSchema: z.ZodType<CommerceSubscriptionsCancellationReason>;
export let CommerceSubscriptionsClientModulesOutputSchema: z.ZodType<CommerceSubscriptionsClientModulesOutput>;
export let CommerceSubscriptionsCreateClientInputSchema: z.ZodType<CommerceSubscriptionsCreateClientInput>;
export let CommerceSubscriptionsSubscriptionSchema: z.ZodType<CommerceSubscriptionsSubscription>;
export let CommerceSubscriptionsSubscriptionChurnReportSchema: z.ZodType<CommerceSubscriptionsSubscriptionChurnReport>;
export let CommerceSubscriptionsSubscriptionDowngradeResultSchema: z.ZodType<CommerceSubscriptionsSubscriptionDowngradeResult>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerCancelInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput>;
export let CommerceSubscriptionsSubscriptionNotificationSchema: z.ZodType<CommerceSubscriptionsSubscriptionNotification>;
export let CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput>;
export let CommerceSubscriptionsSubscriptionPlanSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlan>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput>;
export let CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionStatusSchema: z.ZodType<CommerceSubscriptionsSubscriptionStatus>;
export let CommerceSubscriptionsSubscriptionUpgradeResultSchema: z.ZodType<CommerceSubscriptionsSubscriptionUpgradeResult>;
export let CommerceSubscriptionsSubscriptionUsageSchema: z.ZodType<CommerceSubscriptionsSubscriptionUsage>;
export let ComplianceAuditAuditCategorySchema: z.ZodType<ComplianceAuditAuditCategory>;
export let ComplianceAuditAuditExportInputSchema: z.ZodType<ComplianceAuditAuditExportInput>;
export let ComplianceAuditAuditLogSchema: z.ZodType<ComplianceAuditAuditLog>;
export let ComplianceAuditAuditLogOutputSchema: z.ZodType<ComplianceAuditAuditLogOutput>;
export let ComplianceAuditAuditRiskLevelSchema: z.ZodType<ComplianceAuditAuditRiskLevel>;
export let ComplianceAuditAuditStatisticsOutputSchema: z.ZodType<ComplianceAuditAuditStatisticsOutput>;
export let ComplianceAuditAuthenticationAuditEntrySchema: z.ZodType<ComplianceAuditAuthenticationAuditEntry>;
export let ComplianceAuditAuthenticationAuditOutputSchema: z.ZodType<ComplianceAuditAuthenticationAuditOutput>;
export let ComplianceAuditDailyActivityTrendSchema: z.ZodType<ComplianceAuditDailyActivityTrend>;
export let ComplianceAuditFailureReasonCountSchema: z.ZodType<ComplianceAuditFailureReasonCount>;
export let ComplianceAuditPermissionAuditEntrySchema: z.ZodType<ComplianceAuditPermissionAuditEntry>;
export let ComplianceAuditPermissionAuditOutputSchema: z.ZodType<ComplianceAuditPermissionAuditOutput>;
export let ComplianceAuditSecurityAuditDashboardSchema: z.ZodType<ComplianceAuditSecurityAuditDashboard>;
export let ComplianceAuditSecurityAuditSourceTypeSchema: z.ZodType<ComplianceAuditSecurityAuditSourceType>;
export let ComplianceAuditTopIpActivitySchema: z.ZodType<ComplianceAuditTopIpActivity>;
export let ComplianceAuditTopUserActivitySchema: z.ZodType<ComplianceAuditTopUserActivity>;
export let ComplianceAuditUnifiedSecurityAuditEntrySchema: z.ZodType<ComplianceAuditUnifiedSecurityAuditEntry>;
export let ComplianceAuditUnifiedSecurityAuditInputSchema: z.ZodType<ComplianceAuditUnifiedSecurityAuditInput>;
export let ComplianceAuditUnifiedSecurityAuditOutputSchema: z.ZodType<ComplianceAuditUnifiedSecurityAuditOutput>;
export let ComplianceConsentConsentPolicySchema: z.ZodType<ComplianceConsentConsentPolicy>;
export let ComplianceConsentContentTypeSchema: z.ZodType<ComplianceConsentContentType>;
export let ComplianceConsentCreateConsentPolicyCommandSchema: z.ZodType<ComplianceConsentCreateConsentPolicyCommand>;
export let ComplianceConsentDataSubjectInputSchema: z.ZodType<ComplianceConsentDataSubjectInput>;
export let ComplianceConsentDataSubjectRequestStatusSchema: z.ZodType<ComplianceConsentDataSubjectRequestStatus>;
export let ComplianceConsentDataSubjectRequestTypeSchema: z.ZodType<ComplianceConsentDataSubjectRequestType>;
export let ComplianceConsentGrantConsentCommandSchema: z.ZodType<ComplianceConsentGrantConsentCommand>;
export let ComplianceConsentPolicyTypeSchema: z.ZodType<ComplianceConsentPolicyType>;
export let ComplianceConsentPolicyVersionSchema: z.ZodType<ComplianceConsentPolicyVersion>;
export let ComplianceConsentProcessRequestBodySchema: z.ZodType<ComplianceConsentProcessRequestBody>;
export let ComplianceConsentPublishVersionInputSchema: z.ZodType<ComplianceConsentPublishVersionInput>;
export let ComplianceConsentRevokeConsentCommandSchema: z.ZodType<ComplianceConsentRevokeConsentCommand>;
export let ComplianceConsentSubmitDataSubjectRequestCommandSchema: z.ZodType<ComplianceConsentSubmitDataSubjectRequestCommand>;
export let ComplianceConsentUserConsentSchema: z.ZodType<ComplianceConsentUserConsent>;
export let ComplianceFERPACompleteFerpaInspectionRequestBodySchema: z.ZodType<ComplianceFERPACompleteFerpaInspectionRequestBody>;
export let ComplianceFERPAEducationRecordKindSchema: z.ZodType<ComplianceFERPAEducationRecordKind>;
export let ComplianceFERPAFerpaDirectoryInformationPolicySchema: z.ZodType<ComplianceFERPAFerpaDirectoryInformationPolicy>;
export let ComplianceFERPAFerpaDisclosureBasisSchema: z.ZodType<ComplianceFERPAFerpaDisclosureBasis>;
export let ComplianceFERPAFerpaDisclosureConsentSchema: z.ZodType<ComplianceFERPAFerpaDisclosureConsent>;
export let ComplianceFERPAFerpaDisclosureLogSchema: z.ZodType<ComplianceFERPAFerpaDisclosureLog>;
export let ComplianceFERPAFerpaEducationRecordSchema: z.ZodType<ComplianceFERPAFerpaEducationRecord>;
export let ComplianceFERPAFerpaInspectionInputSchema: z.ZodType<ComplianceFERPAFerpaInspectionInput>;
export let ComplianceFERPAFerpaRecordProtectionLevelSchema: z.ZodType<ComplianceFERPAFerpaRecordProtectionLevel>;
export let ComplianceFERPAFerpaRequestStatusSchema: z.ZodType<ComplianceFERPAFerpaRequestStatus>;
export let ComplianceFERPAGrantFerpaDisclosureConsentCommandSchema: z.ZodType<ComplianceFERPAGrantFerpaDisclosureConsentCommand>;
export let ComplianceFERPARecordFerpaDisclosureCommandSchema: z.ZodType<ComplianceFERPARecordFerpaDisclosureCommand>;
export let ComplianceFERPARegisterEducationRecordCommandSchema: z.ZodType<ComplianceFERPARegisterEducationRecordCommand>;
export let ComplianceFERPASubmitFerpaInspectionRequestCommandSchema: z.ZodType<ComplianceFERPASubmitFerpaInspectionRequestCommand>;
export let ComplianceFERPAUpsertDirectoryInformationPolicyCommandSchema: z.ZodType<ComplianceFERPAUpsertDirectoryInformationPolicyCommand>;
export let ContentPagesContentResourceSchema: z.ZodType<ContentPagesContentResource>;
export let ContentPagesContentResourceStatusSchema: z.ZodType<ContentPagesContentResourceStatus>;
export let ContentPagesContentResourceTypeSchema: z.ZodType<ContentPagesContentResourceType>;
export let ContentPagesCreateContentResourceSchema: z.ZodType<ContentPagesCreateContentResource>;
export let ContentPagesCreateMarketingLeadSchema: z.ZodType<ContentPagesCreateMarketingLead>;
export let ContentPagesCreatePageSchema: z.ZodType<ContentPagesCreatePage>;
export let ContentPagesCreatePageSectionSchema: z.ZodType<ContentPagesCreatePageSection>;
export let ContentPagesMarketingLeadSchema: z.ZodType<ContentPagesMarketingLead>;
export let ContentPagesOpenGraphMetadataSchema: z.ZodType<ContentPagesOpenGraphMetadata>;
export let ContentPagesPageSchema: z.ZodType<ContentPagesPage>;
export let ContentPagesPageSectionSchema: z.ZodType<ContentPagesPageSection>;
export let ContentPagesPageStatusSchema: z.ZodType<ContentPagesPageStatus>;
export let ContentPagesPageTypeSchema: z.ZodType<ContentPagesPageType>;
export let ContentPagesSectionTypeSchema: z.ZodType<ContentPagesSectionType>;
export let ContentPagesSitemapEntrySchema: z.ZodType<ContentPagesSitemapEntry>;
export let ContentPagesUpdateContentResourceSchema: z.ZodType<ContentPagesUpdateContentResource>;
export let ContentPagesUpdatePageSchema: z.ZodType<ContentPagesUpdatePage>;
export let ContentPagesUpdatePageSectionSchema: z.ZodType<ContentPagesUpdatePageSection>;
export let ContentStatusSchema: z.ZodType<ContentStatus>;
export let ContentVisibilitySchema: z.ZodType<ContentVisibility>;
export let CQRSIDomainEventSchema: z.ZodType<CQRSIDomainEvent>;
export let CQRSModelsTenantIdSchema: z.ZodType<CQRSModelsTenantId>;
export let EconomyCommandsConvertMyHardToSoftInputSchema: z.ZodType<EconomyCommandsConvertMyHardToSoftInput>;
export let EconomyContractsCurrencyCodeSchema: z.ZodType<EconomyContractsCurrencyCode>;
export let EconomyContractsEconomyWalletSummarySchema: z.ZodType<EconomyContractsEconomyWalletSummary>;
export let EconomyContractsEconomyWalletTransactionSchema: z.ZodType<EconomyContractsEconomyWalletTransaction>;
export let EconomyContractsEntrySideSchema: z.ZodType<EconomyContractsEntrySide>;
export let EconomyContractsPostingStatusSchema: z.ZodType<EconomyContractsPostingStatus>;
export let EconomyContractsPostingTemplateKindSchema: z.ZodType<EconomyContractsPostingTemplateKind>;
export let EconomyContractsProvenanceKindSchema: z.ZodType<EconomyContractsProvenanceKind>;
export let EconomyContractsWalletLifecycleStateSchema: z.ZodType<EconomyContractsWalletLifecycleState>;
export let EconomyFundingSelfServiceHardToSoftConversionReceiptSchema: z.ZodType<EconomyFundingSelfServiceHardToSoftConversionReceipt>;
export let EconomyPayoutsCommandsCreateMyPayoutRequestInputSchema: z.ZodType<EconomyPayoutsCommandsCreateMyPayoutRequestInput>;
export let EconomyPayoutsPayoutOperationStateSchema: z.ZodType<EconomyPayoutsPayoutOperationState>;
export let EconomyPayoutsPayoutRequestStateSchema: z.ZodType<EconomyPayoutsPayoutRequestState>;
export let EconomyPayoutsQueriesEconomyPayoutInputSchema: z.ZodType<EconomyPayoutsQueriesEconomyPayoutInput>;
export let EconomyPayoutsQueriesEconomyPayoutOperationSchema: z.ZodType<EconomyPayoutsQueriesEconomyPayoutOperation>;
export let EconomyRiskEconomyValueMovementCapabilitySchema: z.ZodType<EconomyRiskEconomyValueMovementCapability>;
export let ErrorSchema: z.ZodType<Error>;
export let ErrorTypeSchema: z.ZodType<ErrorType>;
export let FeaturesBulkEvaluationInputSchema: z.ZodType<FeaturesBulkEvaluationInput>;
export let FeaturesCapabilityAuditLogSchema: z.ZodType<FeaturesCapabilityAuditLog>;
export let FeaturesCapabilityCheckOutputSchema: z.ZodType<FeaturesCapabilityCheckOutput>;
export let FeaturesCreateFeatureInputSchema: z.ZodType<FeaturesCreateFeatureInput>;
export let FeaturesFeatureContextSchema: z.ZodType<FeaturesFeatureContext>;
export let FeaturesFeatureEvaluationInputSchema: z.ZodType<FeaturesFeatureEvaluationInput>;
export let FeaturesFeatureFlagSchema: z.ZodType<FeaturesFeatureFlag>;
export let FeaturesFeatureFlagTargetSchema: z.ZodType<FeaturesFeatureFlagTarget>;
export let FeaturesFeatureFlagTypeSchema: z.ZodType<FeaturesFeatureFlagType>;
export let FeaturesSetCapabilityOverrideInputSchema: z.ZodType<FeaturesSetCapabilityOverrideInput>;
export let FeaturesToggleFeatureInputSchema: z.ZodType<FeaturesToggleFeatureInput>;
export let FeaturesUpdateFeatureInputSchema: z.ZodType<FeaturesUpdateFeatureInput>;
export let Fido2NetLibAssertionOptionsSchema: z.ZodType<Fido2NetLibAssertionOptions>;
export let Fido2NetLibAuthenticatorSelectionSchema: z.ZodType<Fido2NetLibAuthenticatorSelection>;
export let Fido2NetLibCredentialCreateOptionsSchema: z.ZodType<Fido2NetLibCredentialCreateOptions>;
export let Fido2NetLibFido2UserSchema: z.ZodType<Fido2NetLibFido2User>;
export let Fido2NetLibPubKeyCredParamSchema: z.ZodType<Fido2NetLibPubKeyCredParam>;
export let Fido2NetLibPublicKeyCredentialRpEntitySchema: z.ZodType<Fido2NetLibPublicKeyCredentialRpEntity>;
export let GameJamsAddJamCriteriaInputSchema: z.ZodType<GameJamsAddJamCriteriaInput>;
export let GameJamsCreateJamInputSchema: z.ZodType<GameJamsCreateJamInput>;
export let GameJamsJamSchema: z.ZodType<GameJamsJam>;
export let GameJamsJamCriteriaSchema: z.ZodType<GameJamsJamCriteria>;
export let GameJamsJamDtoSchema: z.ZodType<GameJamsJamDto>;
export let GameJamsJamScoreSchema: z.ZodType<GameJamsJamScore>;
export let GameJamsJamScoreDtoSchema: z.ZodType<GameJamsJamScoreDto>;
export let GameJamsJamStatusSchema: z.ZodType<GameJamsJamStatus>;
export let GameJamsJamSubmissionSchema: z.ZodType<GameJamsJamSubmission>;
export let GameJamsScoreJamSubmissionInputSchema: z.ZodType<GameJamsScoreJamSubmissionInput>;
export let GameJamsSubmitJamEntryInputSchema: z.ZodType<GameJamsSubmitJamEntryInput>;
export let IdentityAuthenticationApiKeySchema: z.ZodType<IdentityAuthenticationApiKey>;
export let IdentityAuthenticationAssignRoleToUserInputSchema: z.ZodType<IdentityAuthenticationAssignRoleToUserInput>;
export let IdentityAuthenticationBackupCodesOutputSchema: z.ZodType<IdentityAuthenticationBackupCodesOutput>;
export let IdentityAuthenticationBackupCodesStatusOutputSchema: z.ZodType<IdentityAuthenticationBackupCodesStatusOutput>;
export let IdentityAuthenticationBeginWebAuthnAuthenticationInputSchema: z.ZodType<IdentityAuthenticationBeginWebAuthnAuthenticationInput>;
export let IdentityAuthenticationBeginWebAuthnRegistrationInputSchema: z.ZodType<IdentityAuthenticationBeginWebAuthnRegistrationInput>;
export let IdentityAuthenticationCleanupKeysInputSchema: z.ZodType<IdentityAuthenticationCleanupKeysInput>;
export let IdentityAuthenticationCleanupResultSchema: z.ZodType<IdentityAuthenticationCleanupResult>;
export let IdentityAuthenticationClientCredentialsTokenOutputSchema: z.ZodType<IdentityAuthenticationClientCredentialsTokenOutput>;
export let IdentityAuthenticationCompleteMfaSetupInputSchema: z.ZodType<IdentityAuthenticationCompleteMfaSetupInput>;
export let IdentityAuthenticationCompletePasswordResetInputSchema: z.ZodType<IdentityAuthenticationCompletePasswordResetInput>;
export let IdentityAuthenticationCompleteWebAuthnAuthenticationInputSchema: z.ZodType<IdentityAuthenticationCompleteWebAuthnAuthenticationInput>;
export let IdentityAuthenticationCompleteWebAuthnRegistrationInputSchema: z.ZodType<IdentityAuthenticationCompleteWebAuthnRegistrationInput>;
export let IdentityAuthenticationConsumeMagicLinkInputSchema: z.ZodType<IdentityAuthenticationConsumeMagicLinkInput>;
export let IdentityAuthenticationCreateApiKeyCommandSchema: z.ZodType<IdentityAuthenticationCreateApiKeyCommand>;
export let IdentityAuthenticationCreateApiKeyOutputSchema: z.ZodType<IdentityAuthenticationCreateApiKeyOutput>;
export let IdentityAuthenticationCreateRoleInputSchema: z.ZodType<IdentityAuthenticationCreateRoleInput>;
export let IdentityAuthenticationCreateServiceAccountInputSchema: z.ZodType<IdentityAuthenticationCreateServiceAccountInput>;
export let IdentityAuthenticationDeviceInfoSchema: z.ZodType<IdentityAuthenticationDeviceInfo>;
export let IdentityAuthenticationDisableMfaInputSchema: z.ZodType<IdentityAuthenticationDisableMfaInput>;
export let IdentityAuthenticationDiscordAuthorizeInputSchema: z.ZodType<IdentityAuthenticationDiscordAuthorizeInput>;
export let IdentityAuthenticationDiscordCallbackInputSchema: z.ZodType<IdentityAuthenticationDiscordCallbackInput>;
export let IdentityAuthenticationDiscordLinkAuthorizeInputSchema: z.ZodType<IdentityAuthenticationDiscordLinkAuthorizeInput>;
export let IdentityAuthenticationDiscordLinkAuthorizeOutputSchema: z.ZodType<IdentityAuthenticationDiscordLinkAuthorizeOutput>;
export let IdentityAuthenticationDiscordLinkCallbackInputSchema: z.ZodType<IdentityAuthenticationDiscordLinkCallbackInput>;
export let IdentityAuthenticationDiscordSignInOutputSchema: z.ZodType<IdentityAuthenticationDiscordSignInOutput>;
export let IdentityAuthenticationEmailVerificationOutputSchema: z.ZodType<IdentityAuthenticationEmailVerificationOutput>;
export let IdentityAuthenticationEmailVerificationResultSchema: z.ZodType<IdentityAuthenticationEmailVerificationResult>;
export let IdentityAuthenticationGitHubSignInOutputSchema: z.ZodType<IdentityAuthenticationGitHubSignInOutput>;
export let IdentityAuthenticationGoogleIdTokenInputSchema: z.ZodType<IdentityAuthenticationGoogleIdTokenInput>;
export let IdentityAuthenticationJwtKeyInfoSchema: z.ZodType<IdentityAuthenticationJwtKeyInfo>;
export let IdentityAuthenticationLinkGoogleAccountInputSchema: z.ZodType<IdentityAuthenticationLinkGoogleAccountInput>;
export let IdentityAuthenticationLocalSignInInputSchema: z.ZodType<IdentityAuthenticationLocalSignInInput>;
export let IdentityAuthenticationLocalSignUpInputSchema: z.ZodType<IdentityAuthenticationLocalSignUpInput>;
export let IdentityAuthenticationLocationInfoSchema: z.ZodType<IdentityAuthenticationLocationInfo>;
export let IdentityAuthenticationLockServiceAccountInputSchema: z.ZodType<IdentityAuthenticationLockServiceAccountInput>;
export let IdentityAuthenticationMagicLinkRequestResultSchema: z.ZodType<IdentityAuthenticationMagicLinkRequestResult>;
export let IdentityAuthenticationMfaConfigurationOutputSchema: z.ZodType<IdentityAuthenticationMfaConfigurationOutput>;
export let IdentityAuthenticationMfaErrorOutputSchema: z.ZodType<IdentityAuthenticationMfaErrorOutput>;
export let IdentityAuthenticationMfaMethodSchema: z.ZodType<IdentityAuthenticationMfaMethod>;
export let IdentityAuthenticationMfaMethodInfoSchema: z.ZodType<IdentityAuthenticationMfaMethodInfo>;
export let IdentityAuthenticationMfaMethodsOutputSchema: z.ZodType<IdentityAuthenticationMfaMethodsOutput>;
export let IdentityAuthenticationMfaSetupOutputSchema: z.ZodType<IdentityAuthenticationMfaSetupOutput>;
export let IdentityAuthenticationMfaSuccessOutputSchema: z.ZodType<IdentityAuthenticationMfaSuccessOutput>;
export let IdentityAuthenticationMfaVerificationOutputSchema: z.ZodType<IdentityAuthenticationMfaVerificationOutput>;
export let IdentityAuthenticationOAuth2ErrorOutputSchema: z.ZodType<IdentityAuthenticationOAuth2ErrorOutput>;
export let IdentityAuthenticationPasswordChangeInputSchema: z.ZodType<IdentityAuthenticationPasswordChangeInput>;
export let IdentityAuthenticationPasswordChangeResultSchema: z.ZodType<IdentityAuthenticationPasswordChangeResult>;
export let IdentityAuthenticationPasswordResetRequestResultSchema: z.ZodType<IdentityAuthenticationPasswordResetRequestResult>;
export let IdentityAuthenticationPasswordResetResultSchema: z.ZodType<IdentityAuthenticationPasswordResetResult>;
export let IdentityAuthenticationPatchServiceAccountInputSchema: z.ZodType<IdentityAuthenticationPatchServiceAccountInput>;
export let IdentityAuthenticationRefreshTokenInputSchema: z.ZodType<IdentityAuthenticationRefreshTokenInput>;
export let IdentityAuthenticationRemoveRoleFromUserInputSchema: z.ZodType<IdentityAuthenticationRemoveRoleFromUserInput>;
export let IdentityAuthenticationRequestMagicLinkInputSchema: z.ZodType<IdentityAuthenticationRequestMagicLinkInput>;
export let IdentityAuthenticationRequestPasswordResetInputSchema: z.ZodType<IdentityAuthenticationRequestPasswordResetInput>;
export let IdentityAuthenticationRevokeApiKeyInputSchema: z.ZodType<IdentityAuthenticationRevokeApiKeyInput>;
export let IdentityAuthenticationRevokeRefreshTokenInputSchema: z.ZodType<IdentityAuthenticationRevokeRefreshTokenInput>;
export let IdentityAuthenticationRiskLevelSchema: z.ZodType<IdentityAuthenticationRiskLevel>;
export let IdentityAuthenticationRotateKeyInputSchema: z.ZodType<IdentityAuthenticationRotateKeyInput>;
export let IdentityAuthenticationSecretRotationOutputSchema: z.ZodType<IdentityAuthenticationSecretRotationOutput>;
export let IdentityAuthenticationSendEmailVerificationInputSchema: z.ZodType<IdentityAuthenticationSendEmailVerificationInput>;
export let IdentityAuthenticationServiceAccountAuditEntrySchema: z.ZodType<IdentityAuthenticationServiceAccountAuditEntry>;
export let IdentityAuthenticationServiceAccountAuditLogOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountAuditLogOutput>;
export let IdentityAuthenticationServiceAccountCreatedOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountCreatedOutput>;
export let IdentityAuthenticationServiceAccountOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountOutput>;
export let IdentityAuthenticationSessionOutputSchema: z.ZodType<IdentityAuthenticationSessionOutput>;
export let IdentityAuthenticationSessionSecurityAnalysisSchema: z.ZodType<IdentityAuthenticationSessionSecurityAnalysis>;
export let IdentityAuthenticationSessionSuccessOutputSchema: z.ZodType<IdentityAuthenticationSessionSuccessOutput>;
export let IdentityAuthenticationSessionTerminationOutputSchema: z.ZodType<IdentityAuthenticationSessionTerminationOutput>;
export let IdentityAuthenticationSignInOutputSchema: z.ZodType<IdentityAuthenticationSignInOutput>;
export let IdentityAuthenticationSmsMfaSetupInputSchema: z.ZodType<IdentityAuthenticationSmsMfaSetupInput>;
export let IdentityAuthenticationSmsMfaSetupOutputSchema: z.ZodType<IdentityAuthenticationSmsMfaSetupOutput>;
export let IdentityAuthenticationTrustDeviceInputSchema: z.ZodType<IdentityAuthenticationTrustDeviceInput>;
export let IdentityAuthenticationTrustedDeviceOutputSchema: z.ZodType<IdentityAuthenticationTrustedDeviceOutput>;
export let IdentityAuthenticationUpdateCredentialNameInputSchema: z.ZodType<IdentityAuthenticationUpdateCredentialNameInput>;
export let IdentityAuthenticationUpdateRoleInputSchema: z.ZodType<IdentityAuthenticationUpdateRoleInput>;
export let IdentityAuthenticationUpdateScopesInputSchema: z.ZodType<IdentityAuthenticationUpdateScopesInput>;
export let IdentityAuthenticationUserSchema: z.ZodType<IdentityAuthenticationUser>;
export let IdentityAuthenticationVerifyEmailInputSchema: z.ZodType<IdentityAuthenticationVerifyEmailInput>;
export let IdentityAuthenticationVerifyMfaInputSchema: z.ZodType<IdentityAuthenticationVerifyMfaInput>;
export let IdentityAuthenticationWeb3ChallengeInputSchema: z.ZodType<IdentityAuthenticationWeb3ChallengeInput>;
export let IdentityAuthenticationWeb3ChallengeOutputSchema: z.ZodType<IdentityAuthenticationWeb3ChallengeOutput>;
export let IdentityAuthenticationWeb3VerifyInputSchema: z.ZodType<IdentityAuthenticationWeb3VerifyInput>;
export let IdentityAuthenticationWebAuthnAuthenticationOptionsResultSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticationOptionsResult>;
export let IdentityAuthenticationWebAuthnAuthenticationResultSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticationResult>;
export let IdentityAuthenticationWebAuthnAuthenticatorTypeSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticatorType>;
export let IdentityAuthenticationWebAuthnCredentialInfoSchema: z.ZodType<IdentityAuthenticationWebAuthnCredentialInfo>;
export let IdentityAuthenticationWebAuthnCredentialVerifyResultSchema: z.ZodType<IdentityAuthenticationWebAuthnCredentialVerifyResult>;
export let IdentityAuthenticationWebAuthnRegistrationOptionsResultSchema: z.ZodType<IdentityAuthenticationWebAuthnRegistrationOptionsResult>;
export let IdentityAuthenticationWebAuthnRegistrationResultSchema: z.ZodType<IdentityAuthenticationWebAuthnRegistrationResult>;
export let IdentityAuthenticationWebAuthnStatusOutputSchema: z.ZodType<IdentityAuthenticationWebAuthnStatusOutput>;
export let IdentityAuthorizationAccessReviewCampaignSchema: z.ZodType<IdentityAuthorizationAccessReviewCampaign>;
export let IdentityAuthorizationAccessReviewDecisionSchema: z.ZodType<IdentityAuthorizationAccessReviewDecision>;
export let IdentityAuthorizationAccessReviewItemSchema: z.ZodType<IdentityAuthorizationAccessReviewItem>;
export let IdentityAuthorizationAccessReviewItemStatusSchema: z.ZodType<IdentityAuthorizationAccessReviewItemStatus>;
export let IdentityAuthorizationAccessReviewScopeSchema: z.ZodType<IdentityAuthorizationAccessReviewScope>;
export let IdentityAuthorizationAccessReviewStatusSchema: z.ZodType<IdentityAuthorizationAccessReviewStatus>;
export let IdentityAuthorizationAccessReviewTypeSchema: z.ZodType<IdentityAuthorizationAccessReviewType>;
export let IdentityAuthorizationCommandsCreateAccessReviewCampaignCommandSchema: z.ZodType<IdentityAuthorizationCommandsCreateAccessReviewCampaignCommand>;
export let IdentityAuthorizationCommandsCreateSoDRuleCommandSchema: z.ZodType<IdentityAuthorizationCommandsCreateSoDRuleCommand>;
export let IdentityAuthorizationCommandsDelegatePermissionsCommandSchema: z.ZodType<IdentityAuthorizationCommandsDelegatePermissionsCommand>;
export let IdentityAuthorizationCommandsGrantDelegatedAdminCommandSchema: z.ZodType<IdentityAuthorizationCommandsGrantDelegatedAdminCommand>;
export let IdentityAuthorizationCommandsRequestJitElevationCommandSchema: z.ZodType<IdentityAuthorizationCommandsRequestJitElevationCommand>;
export let IdentityAuthorizationControllersApproveElevationInputSchema: z.ZodType<IdentityAuthorizationControllersApproveElevationInput>;
export let IdentityAuthorizationControllersApproveItemInputSchema: z.ZodType<IdentityAuthorizationControllersApproveItemInput>;
export let IdentityAuthorizationControllersCompleteCampaignInputSchema: z.ZodType<IdentityAuthorizationControllersCompleteCampaignInput>;
export let IdentityAuthorizationControllersDenyElevationInputSchema: z.ZodType<IdentityAuthorizationControllersDenyElevationInput>;
export let IdentityAuthorizationControllersGrantExceptionInputSchema: z.ZodType<IdentityAuthorizationControllersGrantExceptionInput>;
export let IdentityAuthorizationControllersResolveViolationInputSchema: z.ZodType<IdentityAuthorizationControllersResolveViolationInput>;
export let IdentityAuthorizationControllersRevokeElevationInputSchema: z.ZodType<IdentityAuthorizationControllersRevokeElevationInput>;
export let IdentityAuthorizationControllersRevokeItemInputSchema: z.ZodType<IdentityAuthorizationControllersRevokeItemInput>;
export let IdentityAuthorizationControllersUpdateSoDRuleInputSchema: z.ZodType<IdentityAuthorizationControllersUpdateSoDRuleInput>;
export let IdentityAuthorizationDeclineInvitationInputSchema: z.ZodType<IdentityAuthorizationDeclineInvitationInput>;
export let IdentityAuthorizationDelegatedAdminScopeSchema: z.ZodType<IdentityAuthorizationDelegatedAdminScope>;
export let IdentityAuthorizationDelegatedAdminScopeTypeSchema: z.ZodType<IdentityAuthorizationDelegatedAdminScopeType>;
export let IdentityAuthorizationDenyTenantPermissionCommandSchema: z.ZodType<IdentityAuthorizationDenyTenantPermissionCommand>;
export let IdentityAuthorizationEffectivePermissionSchema: z.ZodType<IdentityAuthorizationEffectivePermission>;
export let IdentityAuthorizationEffectivePermissionsOutputSchema: z.ZodType<IdentityAuthorizationEffectivePermissionsOutput>;
export let IdentityAuthorizationElevationRequestStatusSchema: z.ZodType<IdentityAuthorizationElevationRequestStatus>;
export let IdentityAuthorizationGetPendingResourceInvitationsOutputSchema: z.ZodType<IdentityAuthorizationGetPendingResourceInvitationsOutput>;
export let IdentityAuthorizationGetResourceInvitationOutputSchema: z.ZodType<IdentityAuthorizationGetResourceInvitationOutput>;
export let IdentityAuthorizationGetResourceUsersOutputSchema: z.ZodType<IdentityAuthorizationGetResourceUsersOutput>;
export let IdentityAuthorizationGetTenantPermissionsOutputSchema: z.ZodType<IdentityAuthorizationGetTenantPermissionsOutput>;
export let IdentityAuthorizationGrantTenantPermissionCommandSchema: z.ZodType<IdentityAuthorizationGrantTenantPermissionCommand>;
export let IdentityAuthorizationHasPermissionOutputSchema: z.ZodType<IdentityAuthorizationHasPermissionOutput>;
export let IdentityAuthorizationImpactSeveritySchema: z.ZodType<IdentityAuthorizationImpactSeverity>;
export let IdentityAuthorizationInvitationActionResultSchema: z.ZodType<IdentityAuthorizationInvitationActionResult>;
export let IdentityAuthorizationJitElevationInputSchema: z.ZodType<IdentityAuthorizationJitElevationInput>;
export let IdentityAuthorizationPermissionAnalyticsReportSchema: z.ZodType<IdentityAuthorizationPermissionAnalyticsReport>;
export let IdentityAuthorizationPermissionAnomalySchema: z.ZodType<IdentityAuthorizationPermissionAnomaly>;
export let IdentityAuthorizationPermissionDelegationSchema: z.ZodType<IdentityAuthorizationPermissionDelegation>;
export let IdentityAuthorizationPermissionTrendSchema: z.ZodType<IdentityAuthorizationPermissionTrend>;
export let IdentityAuthorizationPermissionTypeSchema: z.ZodType<IdentityAuthorizationPermissionType>;
export let IdentityAuthorizationPermissionUpdateResultSchema: z.ZodType<IdentityAuthorizationPermissionUpdateResult>;
export let IdentityAuthorizationPermissionUsageMetricsSchema: z.ZodType<IdentityAuthorizationPermissionUsageMetrics>;
export let IdentityAuthorizationRemoveDenyPermissionsCommandSchema: z.ZodType<IdentityAuthorizationRemoveDenyPermissionsCommand>;
export let IdentityAuthorizationRemoveUserAccessCommandSchema: z.ZodType<IdentityAuthorizationRemoveUserAccessCommand>;
export let IdentityAuthorizationResourceAccessPatternSchema: z.ZodType<IdentityAuthorizationResourceAccessPattern>;
export let IdentityAuthorizationResourceInvitationSchema: z.ZodType<IdentityAuthorizationResourceInvitation>;
export let IdentityAuthorizationResourceUserSchema: z.ZodType<IdentityAuthorizationResourceUser>;
export let IdentityAuthorizationRevokeTenantPermissionCommandSchema: z.ZodType<IdentityAuthorizationRevokeTenantPermissionCommand>;
export let IdentityAuthorizationSetGlobalDefaultPermissionsCommandSchema: z.ZodType<IdentityAuthorizationSetGlobalDefaultPermissionsCommand>;
export let IdentityAuthorizationSetTenantDefaultPermissionsCommandSchema: z.ZodType<IdentityAuthorizationSetTenantDefaultPermissionsCommand>;
export let IdentityAuthorizationShareResourceCommandSchema: z.ZodType<IdentityAuthorizationShareResourceCommand>;
export let IdentityAuthorizationShareResultSchema: z.ZodType<IdentityAuthorizationShareResult>;
export let IdentityAuthorizationSoDResolutionActionSchema: z.ZodType<IdentityAuthorizationSoDResolutionAction>;
export let IdentityAuthorizationSoDRuleSchema: z.ZodType<IdentityAuthorizationSoDRule>;
export let IdentityAuthorizationSoDRuleTypeSchema: z.ZodType<IdentityAuthorizationSoDRuleType>;
export let IdentityAuthorizationSoDSeveritySchema: z.ZodType<IdentityAuthorizationSoDSeverity>;
export let IdentityAuthorizationSoDViolationSchema: z.ZodType<IdentityAuthorizationSoDViolation>;
export let IdentityAuthorizationSoDViolationStatusSchema: z.ZodType<IdentityAuthorizationSoDViolationStatus>;
export let IdentityAuthorizationUpdateUserPermissionsCommandSchema: z.ZodType<IdentityAuthorizationUpdateUserPermissionsCommand>;
export let IdentityAuthorizationUserActivitySummarySchema: z.ZodType<IdentityAuthorizationUserActivitySummary>;
export let IdentityTenantsAddTenantMemberOutputSchema: z.ZodType<IdentityTenantsAddTenantMemberOutput>;
export let IdentityTenantsAddUserMembershipInputSchema: z.ZodType<IdentityTenantsAddUserMembershipInput>;
export let IdentityTenantsArchiveInputSchema: z.ZodType<IdentityTenantsArchiveInput>;
export let IdentityTenantsBulkActivateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkActivateTenantsCommand>;
export let IdentityTenantsBulkArchiveTenantsCommandSchema: z.ZodType<IdentityTenantsBulkArchiveTenantsCommand>;
export let IdentityTenantsBulkCreateTenantItemSchema: z.ZodType<IdentityTenantsBulkCreateTenantItem>;
export let IdentityTenantsBulkCreateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkCreateTenantsCommand>;
export let IdentityTenantsBulkDeactivateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkDeactivateTenantsCommand>;
export let IdentityTenantsBulkDeleteTenantsCommandSchema: z.ZodType<IdentityTenantsBulkDeleteTenantsCommand>;
export let IdentityTenantsBulkPurgeTenantsCommandSchema: z.ZodType<IdentityTenantsBulkPurgeTenantsCommand>;
export let IdentityTenantsBulkUndeleteTenantsCommandSchema: z.ZodType<IdentityTenantsBulkUndeleteTenantsCommand>;
export let IdentityTenantsBulkUpdateTenantItemSchema: z.ZodType<IdentityTenantsBulkUpdateTenantItem>;
export let IdentityTenantsBulkUpdateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkUpdateTenantsCommand>;
export let IdentityTenantsCreateTenantInputSchema: z.ZodType<IdentityTenantsCreateTenantInput>;
export let IdentityTenantsGetUserMembershipsOutputSchema: z.ZodType<IdentityTenantsGetUserMembershipsOutput>;
export let IdentityTenantsMembershipCountOutputSchema: z.ZodType<IdentityTenantsMembershipCountOutput>;
export let IdentityTenantsRecoverInputSchema: z.ZodType<IdentityTenantsRecoverInput>;
export let IdentityTenantsReplaceTenantMetadataInputSchema: z.ZodType<IdentityTenantsReplaceTenantMetadataInput>;
export let IdentityTenantsReplaceTenantSettingsInputSchema: z.ZodType<IdentityTenantsReplaceTenantSettingsInput>;
export let IdentityTenantsSetTenantMembershipStatusInputSchema: z.ZodType<IdentityTenantsSetTenantMembershipStatusInput>;
export let IdentityTenantsSetTenantMembershipStatusOutputSchema: z.ZodType<IdentityTenantsSetTenantMembershipStatusOutput>;
export let IdentityTenantsSlugValidationSchema: z.ZodType<IdentityTenantsSlugValidation>;
export let IdentityTenantsTenantSchema: z.ZodType<IdentityTenantsTenant>;
export let IdentityTenantsTenantAddressSchema: z.ZodType<IdentityTenantsTenantAddress>;
export let IdentityTenantsTenantAuditLogEntrySchema: z.ZodType<IdentityTenantsTenantAuditLogEntry>;
export let IdentityTenantsTenantBrandingSchema: z.ZodType<IdentityTenantsTenantBranding>;
export let IdentityTenantsTenantBusinessInfoSchema: z.ZodType<IdentityTenantsTenantBusinessInfo>;
export let IdentityTenantsTenantBusinessRulesSchema: z.ZodType<IdentityTenantsTenantBusinessRules>;
export let IdentityTenantsTenantContactInfoSchema: z.ZodType<IdentityTenantsTenantContactInfo>;
export let IdentityTenantsTenantCurrencySettingsSchema: z.ZodType<IdentityTenantsTenantCurrencySettings>;
export let IdentityTenantsTenantDomainSchema: z.ZodType<IdentityTenantsTenantDomain>;
export let IdentityTenantsTenantIntegrationSettingsSchema: z.ZodType<IdentityTenantsTenantIntegrationSettings>;
export let IdentityTenantsTenantMemberSchema: z.ZodType<IdentityTenantsTenantMember>;
export let IdentityTenantsTenantMetadataSchema: z.ZodType<IdentityTenantsTenantMetadata>;
export let IdentityTenantsTenantSecuritySettingsSchema: z.ZodType<IdentityTenantsTenantSecuritySettings>;
export let IdentityTenantsTenantSettingsSchema: z.ZodType<IdentityTenantsTenantSettings>;
export let IdentityTenantsTenantSettingsDtoSchema: z.ZodType<IdentityTenantsTenantSettingsDto>;
export let IdentityTenantsTenantStatisticsSchema: z.ZodType<IdentityTenantsTenantStatistics>;
export let IdentityTenantsTenantSystemConfigurationSchema: z.ZodType<IdentityTenantsTenantSystemConfiguration>;
export let IdentityTenantsTenantSystemLimitsSchema: z.ZodType<IdentityTenantsTenantSystemLimits>;
export let IdentityTenantsTenantUiSettingsSchema: z.ZodType<IdentityTenantsTenantUiSettings>;
export let IdentityTenantsTenantValidationErrorSchema: z.ZodType<IdentityTenantsTenantValidationError>;
export let IdentityTenantsTenantValidationOutputSchema: z.ZodType<IdentityTenantsTenantValidationOutput>;
export let IdentityTenantsTenantValidationWarningSchema: z.ZodType<IdentityTenantsTenantValidationWarning>;
export let IdentityTenantsUpdateTenantAddressInputSchema: z.ZodType<IdentityTenantsUpdateTenantAddressInput>;
export let IdentityTenantsUpdateTenantBrandingInputSchema: z.ZodType<IdentityTenantsUpdateTenantBrandingInput>;
export let IdentityTenantsUpdateTenantBusinessInfoInputSchema: z.ZodType<IdentityTenantsUpdateTenantBusinessInfoInput>;
export let IdentityTenantsUpdateTenantBusinessRulesInputSchema: z.ZodType<IdentityTenantsUpdateTenantBusinessRulesInput>;
export let IdentityTenantsUpdateTenantContactInfoInputSchema: z.ZodType<IdentityTenantsUpdateTenantContactInfoInput>;
export let IdentityTenantsUpdateTenantCurrencySettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantCurrencySettingsInput>;
export let IdentityTenantsUpdateTenantFeatureFlagsInputSchema: z.ZodType<IdentityTenantsUpdateTenantFeatureFlagsInput>;
export let IdentityTenantsUpdateTenantInputSchema: z.ZodType<IdentityTenantsUpdateTenantInput>;
export let IdentityTenantsUpdateTenantIntegrationSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantIntegrationSettingsInput>;
export let IdentityTenantsUpdateTenantMemberInviteOutputSchema: z.ZodType<IdentityTenantsUpdateTenantMemberInviteOutput>;
export let IdentityTenantsUpdateTenantMemberRoleOutputSchema: z.ZodType<IdentityTenantsUpdateTenantMemberRoleOutput>;
export let IdentityTenantsUpdateTenantMetadataInputSchema: z.ZodType<IdentityTenantsUpdateTenantMetadataInput>;
export let IdentityTenantsUpdateTenantSecuritySettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSecuritySettingsInput>;
export let IdentityTenantsUpdateTenantSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSettingsInput>;
export let IdentityTenantsUpdateTenantSystemConfigurationInputSchema: z.ZodType<IdentityTenantsUpdateTenantSystemConfigurationInput>;
export let IdentityTenantsUpdateTenantSystemLimitsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSystemLimitsInput>;
export let IdentityTenantsUpdateTenantTagsInputSchema: z.ZodType<IdentityTenantsUpdateTenantTagsInput>;
export let IdentityTenantsUpdateTenantUiSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantUiSettingsInput>;
export let IdentityTenantsUpdateUserMembershipInviteInputSchema: z.ZodType<IdentityTenantsUpdateUserMembershipInviteInput>;
export let IdentityTenantsUpdateUserMembershipRoleInputSchema: z.ZodType<IdentityTenantsUpdateUserMembershipRoleInput>;
export let IdentityTenantsUsageTrackingSchema: z.ZodType<IdentityTenantsUsageTracking>;
export let IdentityTenantsUserMembershipSchema: z.ZodType<IdentityTenantsUserMembership>;
export let IdentityTenantsValidateTenantInputSchema: z.ZodType<IdentityTenantsValidateTenantInput>;
export let IdentityUsersBulkActivateUsersInputSchema: z.ZodType<IdentityUsersBulkActivateUsersInput>;
export let IdentityUsersBulkActivateUsersOutputSchema: z.ZodType<IdentityUsersBulkActivateUsersOutput>;
export let IdentityUsersBulkCreateUsersInputSchema: z.ZodType<IdentityUsersBulkCreateUsersInput>;
export let IdentityUsersBulkCreateUsersOutputSchema: z.ZodType<IdentityUsersBulkCreateUsersOutput>;
export let IdentityUsersBulkDeactivateUsersInputSchema: z.ZodType<IdentityUsersBulkDeactivateUsersInput>;
export let IdentityUsersBulkDeactivateUsersOutputSchema: z.ZodType<IdentityUsersBulkDeactivateUsersOutput>;
export let IdentityUsersBulkDeleteUsersInputSchema: z.ZodType<IdentityUsersBulkDeleteUsersInput>;
export let IdentityUsersBulkNotificationInputSchema: z.ZodType<IdentityUsersBulkNotificationInput>;
export let IdentityUsersBulkPurgeUsersInputSchema: z.ZodType<IdentityUsersBulkPurgeUsersInput>;
export let IdentityUsersBulkRestoreUsersInputSchema: z.ZodType<IdentityUsersBulkRestoreUsersInput>;
export let IdentityUsersBulkRestoreUsersOutputSchema: z.ZodType<IdentityUsersBulkRestoreUsersOutput>;
export let IdentityUsersBulkSuspendUsersInputSchema: z.ZodType<IdentityUsersBulkSuspendUsersInput>;
export let IdentityUsersBulkSuspendUsersOutputSchema: z.ZodType<IdentityUsersBulkSuspendUsersOutput>;
export let IdentityUsersBulkUnsuspendUsersInputSchema: z.ZodType<IdentityUsersBulkUnsuspendUsersInput>;
export let IdentityUsersBulkUnsuspendUsersOutputSchema: z.ZodType<IdentityUsersBulkUnsuspendUsersOutput>;
export let IdentityUsersBulkUpdateUsersInputSchema: z.ZodType<IdentityUsersBulkUpdateUsersInput>;
export let IdentityUsersCreateUserInputSchema: z.ZodType<IdentityUsersCreateUserInput>;
export let IdentityUsersCreateUserRequestItemSchema: z.ZodType<IdentityUsersCreateUserRequestItem>;
export let IdentityUsersNotificationActionSchema: z.ZodType<IdentityUsersNotificationAction>;
export let IdentityUsersNotificationFilterCriteriaSchema: z.ZodType<IdentityUsersNotificationFilterCriteria>;
export let IdentityUsersNotificationPrioritySchema: z.ZodType<IdentityUsersNotificationPriority>;
export let IdentityUsersProfileVisibilitySchema: z.ZodType<IdentityUsersProfileVisibility>;
export let IdentityUsersPurgeStrategySchema: z.ZodType<IdentityUsersPurgeStrategy>;
export let IdentityUsersReplaceUserAccessibilityPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserAccessibilityPreferencesInput>;
export let IdentityUsersReplaceUserLocalizationPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserLocalizationPreferencesInput>;
export let IdentityUsersReplaceUserMetadataInputSchema: z.ZodType<IdentityUsersReplaceUserMetadataInput>;
export let IdentityUsersReplaceUserNotificationPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserNotificationPreferencesInput>;
export let IdentityUsersReplaceUserPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserPreferencesInput>;
export let IdentityUsersReplaceUserPrivacyPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserPrivacyPreferencesInput>;
export let IdentityUsersReplaceUserProfileInputSchema: z.ZodType<IdentityUsersReplaceUserProfileInput>;
export let IdentityUsersUpdateUserAccessibilityPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserAccessibilityPreferencesInput>;
export let IdentityUsersUpdateUserInputSchema: z.ZodType<IdentityUsersUpdateUserInput>;
export let IdentityUsersUpdateUserLocalizationPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserLocalizationPreferencesInput>;
export let IdentityUsersUpdateUserMetadataInputSchema: z.ZodType<IdentityUsersUpdateUserMetadataInput>;
export let IdentityUsersUpdateUserNotificationPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserNotificationPreferencesInput>;
export let IdentityUsersUpdateUserPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserPreferencesInput>;
export let IdentityUsersUpdateUserPrivacyPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserPrivacyPreferencesInput>;
export let IdentityUsersUpdateUserProfileInputSchema: z.ZodType<IdentityUsersUpdateUserProfileInput>;
export let IdentityUsersUpdateUserRequestItemSchema: z.ZodType<IdentityUsersUpdateUserRequestItem>;
export let IdentityUsersUserSchema: z.ZodType<IdentityUsersUser>;
export let IdentityUsersUserAccessibilityPreferencesSchema: z.ZodType<IdentityUsersUserAccessibilityPreferences>;
export let IdentityUsersUserDtoSchema: z.ZodType<IdentityUsersUserDto>;
export let IdentityUsersUserLocalizationPreferencesSchema: z.ZodType<IdentityUsersUserLocalizationPreferences>;
export let IdentityUsersUserMetadataSchema: z.ZodType<IdentityUsersUserMetadata>;
export let IdentityUsersUserMetadataDtoSchema: z.ZodType<IdentityUsersUserMetadataDto>;
export let IdentityUsersUserNotificationSchema: z.ZodType<IdentityUsersUserNotification>;
export let IdentityUsersUserNotificationDetailSchema: z.ZodType<IdentityUsersUserNotificationDetail>;
export let IdentityUsersUserNotificationDtoSchema: z.ZodType<IdentityUsersUserNotificationDto>;
export let IdentityUsersUserNotificationPreferencesSchema: z.ZodType<IdentityUsersUserNotificationPreferences>;
export let IdentityUsersUserPreferencesSchema: z.ZodType<IdentityUsersUserPreferences>;
export let IdentityUsersUserPreferencesDtoSchema: z.ZodType<IdentityUsersUserPreferencesDto>;
export let IdentityUsersUserPrivacyPreferencesSchema: z.ZodType<IdentityUsersUserPrivacyPreferences>;
export let IdentityUsersUserProfileSchema: z.ZodType<IdentityUsersUserProfile>;
export let IdentityUsersUserProfileDtoSchema: z.ZodType<IdentityUsersUserProfileDto>;
export let IdentityUsersUserStatusSchema: z.ZodType<IdentityUsersUserStatus>;
export let KeyValuePairStringAuthenticationExtensionsPRFValuesSchema: z.ZodType<KeyValuePairStringAuthenticationExtensionsPRFValues>;
export let LaunchPadCreateLaunchPadEventInputSchema: z.ZodType<LaunchPadCreateLaunchPadEventInput>;
export let LaunchPadCreateLaunchPadSlotInputSchema: z.ZodType<LaunchPadCreateLaunchPadSlotInput>;
export let LaunchPadCreateLaunchPlanInputSchema: z.ZodType<LaunchPadCreateLaunchPlanInput>;
export let LaunchPadLaunchChecklistItemSchema: z.ZodType<LaunchPadLaunchChecklistItem>;
export let LaunchPadLaunchChecklistItemInputSchema: z.ZodType<LaunchPadLaunchChecklistItemInput>;
export let LaunchPadLaunchPadAnalyticsProjectionSchema: z.ZodType<LaunchPadLaunchPadAnalyticsProjection>;
export let LaunchPadLaunchPadApplicationSchema: z.ZodType<LaunchPadLaunchPadApplication>;
export let LaunchPadLaunchPadApplicationProjectionSchema: z.ZodType<LaunchPadLaunchPadApplicationProjection>;
export let LaunchPadLaunchPadApplicationStatusSchema: z.ZodType<LaunchPadLaunchPadApplicationStatus>;
export let LaunchPadLaunchPadEventSchema: z.ZodType<LaunchPadLaunchPadEvent>;
export let LaunchPadLaunchPadEventDetailProjectionSchema: z.ZodType<LaunchPadLaunchPadEventDetailProjection>;
export let LaunchPadLaunchPadEventProjectionSchema: z.ZodType<LaunchPadLaunchPadEventProjection>;
export let LaunchPadLaunchPadEventStatusSchema: z.ZodType<LaunchPadLaunchPadEventStatus>;
export let LaunchPadLaunchPadParticipantRegistrationSchema: z.ZodType<LaunchPadLaunchPadParticipantRegistration>;
export let LaunchPadLaunchPadParticipantRoleSchema: z.ZodType<LaunchPadLaunchPadParticipantRole>;
export let LaunchPadLaunchPadParticipantSlotSchema: z.ZodType<LaunchPadLaunchPadParticipantSlot>;
export let LaunchPadLaunchPadParticipantStatusSchema: z.ZodType<LaunchPadLaunchPadParticipantStatus>;
export let LaunchPadLaunchPadRegistrationProjectionSchema: z.ZodType<LaunchPadLaunchPadRegistrationProjection>;
export let LaunchPadLaunchPadSlotProjectionSchema: z.ZodType<LaunchPadLaunchPadSlotProjection>;
export let LaunchPadLaunchPlanSchema: z.ZodType<LaunchPadLaunchPlan>;
export let LaunchPadLaunchPlanStatusSchema: z.ZodType<LaunchPadLaunchPlanStatus>;
export let LaunchPadReviewLaunchPadApplicationInputSchema: z.ZodType<LaunchPadReviewLaunchPadApplicationInput>;
export let LaunchPadSubmitLaunchPadApplicationInputSchema: z.ZodType<LaunchPadSubmitLaunchPadApplicationInput>;
export let LaunchPadTransitionLaunchPadEventInputSchema: z.ZodType<LaunchPadTransitionLaunchPadEventInput>;
export let LaunchPadTransitionLaunchPadRegistrationInputSchema: z.ZodType<LaunchPadTransitionLaunchPadRegistrationInput>;
export let LaunchPadUpdateLaunchPadApplicationInputSchema: z.ZodType<LaunchPadUpdateLaunchPadApplicationInput>;
export let LaunchPadUpdateLaunchPadEventInputSchema: z.ZodType<LaunchPadUpdateLaunchPadEventInput>;
export let LearningAssessmentsAssessmentSchema: z.ZodType<LearningAssessmentsAssessment>;
export let LearningAssessmentsAssessmentDefinitionSchema: z.ZodType<LearningAssessmentsAssessmentDefinition>;
export let LearningAssessmentsAssessmentGradingMethodSchema: z.ZodType<LearningAssessmentsAssessmentGradingMethod>;
export let LearningAssessmentsAssessmentGroupSchema: z.ZodType<LearningAssessmentsAssessmentGroup>;
export let LearningAssessmentsAssessmentGroupAnalyticsSchema: z.ZodType<LearningAssessmentsAssessmentGroupAnalytics>;
export let LearningAssessmentsAssessmentPresentationModeSchema: z.ZodType<LearningAssessmentsAssessmentPresentationMode>;
export let LearningAssessmentsAssessmentScoreBucketSchema: z.ZodType<LearningAssessmentsAssessmentScoreBucket>;
export let LearningAssessmentsAssessmentSubmissionSchema: z.ZodType<LearningAssessmentsAssessmentSubmission>;
export let LearningAssessmentsAssessmentTypeSchema: z.ZodType<LearningAssessmentsAssessmentType>;
export let LearningAssessmentsAssignAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsAssignAssessmentGroupInput>;
export let LearningAssessmentsCanAttemptOutputSchema: z.ZodType<LearningAssessmentsCanAttemptOutput>;
export let LearningAssessmentsCourseAssessmentAnalyticsSchema: z.ZodType<LearningAssessmentsCourseAssessmentAnalytics>;
export let LearningAssessmentsCreateAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsCreateAssessmentGroupInput>;
export let LearningAssessmentsCreateAssessmentInputSchema: z.ZodType<LearningAssessmentsCreateAssessmentInput>;
export let LearningAssessmentsGradeSubmissionInputSchema: z.ZodType<LearningAssessmentsGradeSubmissionInput>;
export let LearningAssessmentsInteractiveVideoAssessmentCueSchema: z.ZodType<LearningAssessmentsInteractiveVideoAssessmentCue>;
export let LearningAssessmentsLearnerAssessmentAttemptSchema: z.ZodType<LearningAssessmentsLearnerAssessmentAttempt>;
export let LearningAssessmentsLearnerAssessmentSubmissionSchema: z.ZodType<LearningAssessmentsLearnerAssessmentSubmission>;
export let LearningAssessmentsLearnerInteractiveVideoAssessmentCueSchema: z.ZodType<LearningAssessmentsLearnerInteractiveVideoAssessmentCue>;
export let LearningAssessmentsLinkInteractiveVideoCueInputSchema: z.ZodType<LearningAssessmentsLinkInteractiveVideoCueInput>;
export let LearningAssessmentsStartSubmissionInputSchema: z.ZodType<LearningAssessmentsStartSubmissionInput>;
export let LearningAssessmentsSubmissionModalitySchema: z.ZodType<LearningAssessmentsSubmissionModality>;
export let LearningAssessmentsSubmissionStatusSchema: z.ZodType<LearningAssessmentsSubmissionStatus>;
export let LearningAssessmentsSubmitAssessmentInputSchema: z.ZodType<LearningAssessmentsSubmitAssessmentInput>;
export let LearningAssessmentsUpdateAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsUpdateAssessmentGroupInput>;
export let LearningAssessmentsUpdateAssessmentInputSchema: z.ZodType<LearningAssessmentsUpdateAssessmentInput>;
export let LearningCertificatesCertificateSchema: z.ZodType<LearningCertificatesCertificate>;
export let LearningCertificatesCertificateStatusSchema: z.ZodType<LearningCertificatesCertificateStatus>;
export let LearningCertificatesCertificateTemplateSchema: z.ZodType<LearningCertificatesCertificateTemplate>;
export let LearningCertificatesCertificateTemplateDetailSchema: z.ZodType<LearningCertificatesCertificateTemplateDetail>;
export let LearningCertificatesCertificateVerificationResultSchema: z.ZodType<LearningCertificatesCertificateVerificationResult>;
export let LearningCertificatesCreateCertificateTemplateInputSchema: z.ZodType<LearningCertificatesCreateCertificateTemplateInput>;
export let LearningCertificatesIssueCertificateInputSchema: z.ZodType<LearningCertificatesIssueCertificateInput>;
export let LearningCertificatesRevokeCertificateInputSchema: z.ZodType<LearningCertificatesRevokeCertificateInput>;
export let LearningCertificatesUpdateCertificateTemplateInputSchema: z.ZodType<LearningCertificatesUpdateCertificateTemplateInput>;
export let LearningCohortsApplyCohortScheduleInputSchema: z.ZodType<LearningCohortsApplyCohortScheduleInput>;
export let LearningCohortsAvailableCohortContentSchema: z.ZodType<LearningCohortsAvailableCohortContent>;
export let LearningCohortsCohortSchema: z.ZodType<LearningCohortsCohort>;
export let LearningCohortsCohortCalendarEntrySchema: z.ZodType<LearningCohortsCohortCalendarEntry>;
export let LearningCohortsCohortPacingModeSchema: z.ZodType<LearningCohortsCohortPacingMode>;
export let LearningCohortsCohortReleasePolicySchema: z.ZodType<LearningCohortsCohortReleasePolicy>;
export let LearningCohortsCohortScheduleSchema: z.ZodType<LearningCohortsCohortSchedule>;
export let LearningCohortsCohortScheduleConflictSchema: z.ZodType<LearningCohortsCohortScheduleConflict>;
export let LearningCohortsCohortScheduleItemSchema: z.ZodType<LearningCohortsCohortScheduleItem>;
export let LearningCohortsCohortScheduleItemStatusSchema: z.ZodType<LearningCohortsCohortScheduleItemStatus>;
export let LearningCohortsCohortScheduleItemTypeSchema: z.ZodType<LearningCohortsCohortScheduleItemType>;
export let LearningCohortsCohortSchedulePreviewSchema: z.ZodType<LearningCohortsCohortSchedulePreview>;
export let LearningCohortsCohortSchedulePreviewItemSchema: z.ZodType<LearningCohortsCohortSchedulePreviewItem>;
export let LearningCohortsCohortScheduleSummarySchema: z.ZodType<LearningCohortsCohortScheduleSummary>;
export let LearningCohortsCohortStatusSchema: z.ZodType<LearningCohortsCohortStatus>;
export let LearningCohortsCohortVisibilityOverrideSchema: z.ZodType<LearningCohortsCohortVisibilityOverride>;
export let LearningCohortsCourseCohortCalendarSchema: z.ZodType<LearningCohortsCourseCohortCalendar>;
export let LearningCohortsCreateCohortInputSchema: z.ZodType<LearningCohortsCreateCohortInput>;
export let LearningCohortsPreviewCohortScheduleInputSchema: z.ZodType<LearningCohortsPreviewCohortScheduleInput>;
export let LearningCohortsScheduleConflictSeveritySchema: z.ZodType<LearningCohortsScheduleConflictSeverity>;
export let LearningCohortsScheduleShiftScopeSchema: z.ZodType<LearningCohortsScheduleShiftScope>;
export let LearningCohortsShiftCohortScheduleInputSchema: z.ZodType<LearningCohortsShiftCohortScheduleInput>;
export let LearningCohortsUpdateCohortInputSchema: z.ZodType<LearningCohortsUpdateCohortInput>;
export let LearningCohortsUpdateCohortScheduleInputSchema: z.ZodType<LearningCohortsUpdateCohortScheduleInput>;
export let LearningCohortsUpdateCohortScheduleItemInputSchema: z.ZodType<LearningCohortsUpdateCohortScheduleItemInput>;
export let LearningCoursesActivityGradeSchema: z.ZodType<LearningCoursesActivityGrade>;
export let LearningCoursesActivitySettingsSchema: z.ZodType<LearningCoursesActivitySettings>;
export let LearningCoursesBundleFileMetaSchema: z.ZodType<LearningCoursesBundleFileMeta>;
export let LearningCoursesCircularDependencyCheckResultSchema: z.ZodType<LearningCoursesCircularDependencyCheckResult>;
export let LearningCoursesCloneProgramSchema: z.ZodType<LearningCoursesCloneProgram>;
export let LearningCoursesCodingAssignmentContentSchema: z.ZodType<LearningCoursesCodingAssignmentContent>;
export let LearningCoursesCodingEnvironmentSchema: z.ZodType<LearningCoursesCodingEnvironment>;
export let LearningCoursesCompleteContentInputSchema: z.ZodType<LearningCoursesCompleteContentInput>;
export let LearningCoursesCompleteCourseCheckoutInputSchema: z.ZodType<LearningCoursesCompleteCourseCheckoutInput>;
export let LearningCoursesCompleteCourseCheckoutOutputSchema: z.ZodType<LearningCoursesCompleteCourseCheckoutOutput>;
export let LearningCoursesCompletionRatesSchema: z.ZodType<LearningCoursesCompletionRates>;
export let LearningCoursesCompletionTrendSchema: z.ZodType<LearningCoursesCompletionTrend>;
export let LearningCoursesContentInteractionSchema: z.ZodType<LearningCoursesContentInteraction>;
export let LearningCoursesContentInteractionEventSchema: z.ZodType<LearningCoursesContentInteractionEvent>;
export let LearningCoursesContentInteractionEventTypeSchema: z.ZodType<LearningCoursesContentInteractionEventType>;
export let LearningCoursesContentInteractionSummarySchema: z.ZodType<LearningCoursesContentInteractionSummary>;
export let LearningCoursesContentProgressSchema: z.ZodType<LearningCoursesContentProgress>;
export let LearningCoursesContentStatsSchema: z.ZodType<LearningCoursesContentStats>;
export let LearningCoursesContentSummarySchema: z.ZodType<LearningCoursesContentSummary>;
export let LearningCoursesCourseSupportTicketMessageInputSchema: z.ZodType<LearningCoursesCourseSupportTicketMessageInput>;
export let LearningCoursesCreateActivityGradeSchema: z.ZodType<LearningCoursesCreateActivityGrade>;
export let LearningCoursesCreatePrerequisiteApiInputSchema: z.ZodType<LearningCoursesCreatePrerequisiteApiInput>;
export let LearningCoursesCreateProductFromProgramSchema: z.ZodType<LearningCoursesCreateProductFromProgram>;
export let LearningCoursesCreateProgramSchema: z.ZodType<LearningCoursesCreateProgram>;
export let LearningCoursesCreateProgramContentSchema: z.ZodType<LearningCoursesCreateProgramContent>;
export let LearningCoursesEngagementMetricsSchema: z.ZodType<LearningCoursesEngagementMetrics>;
export let LearningCoursesEnrollmentStatusSchema: z.ZodType<LearningCoursesEnrollmentStatus>;
export let LearningCoursesGraderSummarySchema: z.ZodType<LearningCoursesGraderSummary>;
export let LearningCoursesGradeStatisticsSchema: z.ZodType<LearningCoursesGradeStatistics>;
export let LearningCoursesGradingConfigSchema: z.ZodType<LearningCoursesGradingConfig>;
export let LearningCoursesLessonContentFormatSchema: z.ZodType<LearningCoursesLessonContentFormat>;
export let LearningCoursesMonetizationSchema: z.ZodType<LearningCoursesMonetization>;
export let LearningCoursesMoveContentSchema: z.ZodType<LearningCoursesMoveContent>;
export let LearningCoursesPrerequisiteSchema: z.ZodType<LearningCoursesPrerequisite>;
export let LearningCoursesPrerequisiteCheckResultSchema: z.ZodType<LearningCoursesPrerequisiteCheckResult>;
export let LearningCoursesPrerequisiteStatusSchema: z.ZodType<LearningCoursesPrerequisiteStatus>;
export let LearningCoursesPrerequisiteTypeSchema: z.ZodType<LearningCoursesPrerequisiteType>;
export let LearningCoursesPricingSchema: z.ZodType<LearningCoursesPricing>;
export let LearningCoursesProgramSchema: z.ZodType<LearningCoursesProgram>;
export let LearningCoursesProgramAnalyticsSchema: z.ZodType<LearningCoursesProgramAnalytics>;
export let LearningCoursesProgramContentSchema: z.ZodType<LearningCoursesProgramContent>;
export let LearningCoursesProgramContentTypeSchema: z.ZodType<LearningCoursesProgramContentType>;
export let LearningCoursesProgramDifficultySchema: z.ZodType<LearningCoursesProgramDifficulty>;
export let LearningCoursesProgramUserSummarySchema: z.ZodType<LearningCoursesProgramUserSummary>;
export let LearningCoursesProgressStatusSchema: z.ZodType<LearningCoursesProgressStatus>;
export let LearningCoursesRecordContentInteractionEventInputSchema: z.ZodType<LearningCoursesRecordContentInteractionEventInput>;
export let LearningCoursesReflectionResponseResultSchema: z.ZodType<LearningCoursesReflectionResponseResult>;
export let LearningCoursesRejectProgramSchema: z.ZodType<LearningCoursesRejectProgram>;
export let LearningCoursesReorderContentSchema: z.ZodType<LearningCoursesReorderContent>;
export let LearningCoursesReorderPrerequisitesInputSchema: z.ZodType<LearningCoursesReorderPrerequisitesInput>;
export let LearningCoursesResolveCourseSupportTicketInputSchema: z.ZodType<LearningCoursesResolveCourseSupportTicketInput>;
export let LearningCoursesRevenueAnalyticsSchema: z.ZodType<LearningCoursesRevenueAnalytics>;
export let LearningCoursesRevenueChartSchema: z.ZodType<LearningCoursesRevenueChart>;
export let LearningCoursesScheduleProgramSchema: z.ZodType<LearningCoursesScheduleProgram>;
export let LearningCoursesSearchContentSchema: z.ZodType<LearningCoursesSearchContent>;
export let LearningCoursesSendCourseStudentMessageInputSchema: z.ZodType<LearningCoursesSendCourseStudentMessageInput>;
export let LearningCoursesSendCourseStudentMessageOutputSchema: z.ZodType<LearningCoursesSendCourseStudentMessageOutput>;
export let LearningCoursesStartContentInputSchema: z.ZodType<LearningCoursesStartContentInput>;
export let LearningCoursesStudentSummarySchema: z.ZodType<LearningCoursesStudentSummary>;
export let LearningCoursesSubmitContentInputSchema: z.ZodType<LearningCoursesSubmitContentInput>;
export let LearningCoursesSubmitUserContentSchema: z.ZodType<LearningCoursesSubmitUserContent>;
export let LearningCoursesSurveyResponseResultSchema: z.ZodType<LearningCoursesSurveyResponseResult>;
export let LearningCoursesTestSchema: z.ZodType<LearningCoursesTest>;
export let LearningCoursesTestSuiteSchema: z.ZodType<LearningCoursesTestSuite>;
export let LearningCoursesUpdateActivityGradeSchema: z.ZodType<LearningCoursesUpdateActivityGrade>;
export let LearningCoursesUpdatePrerequisiteApiInputSchema: z.ZodType<LearningCoursesUpdatePrerequisiteApiInput>;
export let LearningCoursesUpdatePricingSchema: z.ZodType<LearningCoursesUpdatePricing>;
export let LearningCoursesUpdateProgramSchema: z.ZodType<LearningCoursesUpdateProgram>;
export let LearningCoursesUpdateProgramContentSchema: z.ZodType<LearningCoursesUpdateProgramContent>;
export let LearningCoursesUpdateProgressSchema: z.ZodType<LearningCoursesUpdateProgress>;
export let LearningCoursesUpdateProgressInputSchema: z.ZodType<LearningCoursesUpdateProgressInput>;
export let LearningCoursesUpdateTimeSpentInputSchema: z.ZodType<LearningCoursesUpdateTimeSpentInput>;
export let LearningCoursesUserProgressSchema: z.ZodType<LearningCoursesUserProgress>;
export let LearningCoursesVisibilitySchema: z.ZodType<LearningCoursesVisibility>;
export let LearningCoursesWorkspaceDataSchema: z.ZodType<LearningCoursesWorkspaceData>;
export let LearningEnrollmentsEnrollmentSchema: z.ZodType<LearningEnrollmentsEnrollment>;
export let LearningEnrollmentsEnrollmentStatusSchema: z.ZodType<LearningEnrollmentsEnrollmentStatus>;
export let LearningEnrollmentsEnrollUserInputSchema: z.ZodType<LearningEnrollmentsEnrollUserInput>;
export let LearningEnrollmentsUpdateEnrollmentProgressInputSchema: z.ZodType<LearningEnrollmentsUpdateEnrollmentProgressInput>;
export let LearningExperienceDiscoveryCollectionTypeSchema: z.ZodType<LearningExperienceDiscoveryCollectionType>;
export let LearningExperienceDiscoveryCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryCourseCollection>;
export let LearningExperienceDiscoveryCreateCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryCreateCourseCollection>;
export let LearningExperienceDiscoveryCreateFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryCreateFeaturedContent>;
export let LearningExperienceDiscoveryFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryFeaturedContent>;
export let LearningExperienceDiscoveryFeaturedContentTypeSchema: z.ZodType<LearningExperienceDiscoveryFeaturedContentType>;
export let LearningExperienceDiscoveryPopularSearchResultSchema: z.ZodType<LearningExperienceDiscoveryPopularSearchResult>;
export let LearningExperienceDiscoveryRecordSearchSchema: z.ZodType<LearningExperienceDiscoveryRecordSearch>;
export let LearningExperienceDiscoveryRecordSearchClickSchema: z.ZodType<LearningExperienceDiscoveryRecordSearchClick>;
export let LearningExperienceDiscoverySearchHistorySchema: z.ZodType<LearningExperienceDiscoverySearchHistory>;
export let LearningExperienceDiscoveryUpdateCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryUpdateCourseCollection>;
export let LearningExperienceDiscoveryUpdateFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryUpdateFeaturedContent>;
export let LearningExperienceLearningPathsAddCourseToPathSchema: z.ZodType<LearningExperienceLearningPathsAddCourseToPath>;
export let LearningExperienceLearningPathsCourseOrderSchema: z.ZodType<LearningExperienceLearningPathsCourseOrder>;
export let LearningExperienceLearningPathsCreateLearningPathSchema: z.ZodType<LearningExperienceLearningPathsCreateLearningPath>;
export let LearningExperienceLearningPathsLearningPathSchema: z.ZodType<LearningExperienceLearningPathsLearningPath>;
export let LearningExperienceLearningPathsLearningPathCourseSchema: z.ZodType<LearningExperienceLearningPathsLearningPathCourse>;
export let LearningExperienceLearningPathsLearningPathDetailSchema: z.ZodType<LearningExperienceLearningPathsLearningPathDetail>;
export let LearningExperienceLearningPathsLearningPathDifficultySchema: z.ZodType<LearningExperienceLearningPathsLearningPathDifficulty>;
export let LearningExperienceLearningPathsLearningPathEnrollmentSchema: z.ZodType<LearningExperienceLearningPathsLearningPathEnrollment>;
export let LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema: z.ZodType<LearningExperienceLearningPathsLearningPathEnrollmentStatus>;
export let LearningExperienceLearningPathsLearningPathStatisticsSchema: z.ZodType<LearningExperienceLearningPathsLearningPathStatistics>;
export let LearningExperienceLearningPathsReorderCoursesSchema: z.ZodType<LearningExperienceLearningPathsReorderCourses>;
export let LearningExperienceLearningPathsUpdateLearningPathSchema: z.ZodType<LearningExperienceLearningPathsUpdateLearningPath>;
export let LearningExperienceLearningPathsUpdatePathProgressSchema: z.ZodType<LearningExperienceLearningPathsUpdatePathProgress>;
export let LearningExperienceRecommendationsAddSkillInputSchema: z.ZodType<LearningExperienceRecommendationsAddSkillInput>;
export let LearningExperienceRecommendationsCreateOrUpdateLearningProfileSchema: z.ZodType<LearningExperienceRecommendationsCreateOrUpdateLearningProfile>;
export let LearningExperienceRecommendationsPopularCourseSchema: z.ZodType<LearningExperienceRecommendationsPopularCourse>;
export let LearningExperienceRecommendationsRecommendationSchema: z.ZodType<LearningExperienceRecommendationsRecommendation>;
export let LearningExperienceRecommendationsRecommendationStatisticsSchema: z.ZodType<LearningExperienceRecommendationsRecommendationStatistics>;
export let LearningExperienceRecommendationsRecommendationTypeSchema: z.ZodType<LearningExperienceRecommendationsRecommendationType>;
export let LearningExperienceRecommendationsSimilarCourseSchema: z.ZodType<LearningExperienceRecommendationsSimilarCourse>;
export let LearningExperienceRecommendationsTrendingCourseSchema: z.ZodType<LearningExperienceRecommendationsTrendingCourse>;
export let LearningExperienceRecommendationsUserLearningProfileSchema: z.ZodType<LearningExperienceRecommendationsUserLearningProfile>;
export let LearningExperienceSocialControllersUpdateReviewModerationInputSchema: z.ZodType<LearningExperienceSocialControllersUpdateReviewModerationInput>;
export let LearningExperienceSocialFeedItemTypeSchema: z.ZodType<LearningExperienceSocialFeedItemType>;
export let LearningExperienceSocialServicesCourseDiscussionSchema: z.ZodType<LearningExperienceSocialServicesCourseDiscussion>;
export let LearningExperienceSocialServicesCourseLikeSchema: z.ZodType<LearningExperienceSocialServicesCourseLike>;
export let LearningExperienceSocialServicesCourseRatingStatsSchema: z.ZodType<LearningExperienceSocialServicesCourseRatingStats>;
export let LearningExperienceSocialServicesCourseReviewSchema: z.ZodType<LearningExperienceSocialServicesCourseReview>;
export let LearningExperienceSocialServicesCourseWishlistSchema: z.ZodType<LearningExperienceSocialServicesCourseWishlist>;
export let LearningExperienceSocialServicesCreateDiscussionInputSchema: z.ZodType<LearningExperienceSocialServicesCreateDiscussionInput>;
export let LearningExperienceSocialServicesCreateReplyInputSchema: z.ZodType<LearningExperienceSocialServicesCreateReplyInput>;
export let LearningExperienceSocialServicesCreateReviewInputSchema: z.ZodType<LearningExperienceSocialServicesCreateReviewInput>;
export let LearningExperienceSocialServicesDiscussionReplySchema: z.ZodType<LearningExperienceSocialServicesDiscussionReply>;
export let LearningExperienceSocialServicesPersonalizedFeedItemSchema: z.ZodType<LearningExperienceSocialServicesPersonalizedFeedItem>;
export let LearningExperienceSocialServicesWishlistPreferencesInputSchema: z.ZodType<LearningExperienceSocialServicesWishlistPreferencesInput>;
export let LearningWorkspacesLearnerAnnouncementSchema: z.ZodType<LearningWorkspacesLearnerAnnouncement>;
export let LearningWorkspacesLearnerAssessmentSchema: z.ZodType<LearningWorkspacesLearnerAssessment>;
export let LearningWorkspacesLearnerAssessmentDeadlineSchema: z.ZodType<LearningWorkspacesLearnerAssessmentDeadline>;
export let LearningWorkspacesLearnerAssessmentGroupSchema: z.ZodType<LearningWorkspacesLearnerAssessmentGroup>;
export let LearningWorkspacesLearnerAssessmentSubmissionSchema: z.ZodType<LearningWorkspacesLearnerAssessmentSubmission>;
export let LearningWorkspacesLearnerCertificateSchema: z.ZodType<LearningWorkspacesLearnerCertificate>;
export let LearningWorkspacesLearnerCohortSchema: z.ZodType<LearningWorkspacesLearnerCohort>;
export let LearningWorkspacesLearnerContentSchema: z.ZodType<LearningWorkspacesLearnerContent>;
export let LearningWorkspacesLearnerContentProgressSchema: z.ZodType<LearningWorkspacesLearnerContentProgress>;
export let LearningWorkspacesLearnerCourseSummarySchema: z.ZodType<LearningWorkspacesLearnerCourseSummary>;
export let LearningWorkspacesLearnerCourseWorkspaceSchema: z.ZodType<LearningWorkspacesLearnerCourseWorkspace>;
export let LearningWorkspacesLearnerDashboardSchema: z.ZodType<LearningWorkspacesLearnerDashboard>;
export let LearningWorkspacesLearnerDiscussionSchema: z.ZodType<LearningWorkspacesLearnerDiscussion>;
export let LearningWorkspacesLearnerGradeItemSchema: z.ZodType<LearningWorkspacesLearnerGradeItem>;
export let LearningWorkspacesLearnerGradeSummarySchema: z.ZodType<LearningWorkspacesLearnerGradeSummary>;
export let LearningWorkspacesLearnerScheduleEntrySchema: z.ZodType<LearningWorkspacesLearnerScheduleEntry>;
export let LearningWorkspacesLearnerSearchResultSchema: z.ZodType<LearningWorkspacesLearnerSearchResult>;
export let MoneySchema: z.ZodType<Money>;
export let MonitoringSLACreateSloCommandSchema: z.ZodType<MonitoringSLACreateSloCommand>;
export let MonitoringSLAErrorBudgetSchema: z.ZodType<MonitoringSLAErrorBudget>;
export let MonitoringSLARecordSliMetricCommandSchema: z.ZodType<MonitoringSLARecordSliMetricCommand>;
export let MonitoringSLAResolveSloViolationCommandSchema: z.ZodType<MonitoringSLAResolveSloViolationCommand>;
export let MonitoringSLASloSchema: z.ZodType<MonitoringSLASlo>;
export let MonitoringSLASloComplianceSchema: z.ZodType<MonitoringSLASloCompliance>;
export let MonitoringSLASloStatusSchema: z.ZodType<MonitoringSLASloStatus>;
export let MonitoringSLASloViolationSchema: z.ZodType<MonitoringSLASloViolation>;
export let MonitoringSLAUpdateSloCommandSchema: z.ZodType<MonitoringSLAUpdateSloCommand>;
export let MonitoringSLAViolationSeveritySchema: z.ZodType<MonitoringSLAViolationSeverity>;
export let MvcProblemDetailsSchema: z.ZodType<MvcProblemDetails>;
export let NotificationsControllersDeletedCountOutputSchema: z.ZodType<NotificationsControllersDeletedCountOutput>;
export let NotificationsControllersNotificationSchema: z.ZodType<NotificationsControllersNotification>;
export let NotificationsControllersNotificationPreferenceSchema: z.ZodType<NotificationsControllersNotificationPreference>;
export let NotificationsControllersSetQuietHoursInputSchema: z.ZodType<NotificationsControllersSetQuietHoursInput>;
export let NotificationsControllersUnreadCountOutputSchema: z.ZodType<NotificationsControllersUnreadCountOutput>;
export let NotificationsControllersUpdatePreferencesInputSchema: z.ZodType<NotificationsControllersUpdatePreferencesInput>;
export let NotificationsNotificationChannelSchema: z.ZodType<NotificationsNotificationChannel>;
export let ObjectsAttestationConveyancePreferenceSchema: z.ZodType<ObjectsAttestationConveyancePreference>;
export let ObjectsAttestationStatementFormatIdentifierSchema: z.ZodType<ObjectsAttestationStatementFormatIdentifier>;
export let ObjectsAuthenticationExtensionsClientInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsClientInputs>;
export let ObjectsAuthenticationExtensionsLargeBlobInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsLargeBlobInputs>;
export let ObjectsAuthenticationExtensionsPRFInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsPRFInputs>;
export let ObjectsAuthenticationExtensionsPRFValuesSchema: z.ZodType<ObjectsAuthenticationExtensionsPRFValues>;
export let ObjectsAuthenticatorAttachmentSchema: z.ZodType<ObjectsAuthenticatorAttachment>;
export let ObjectsAuthenticatorTransportSchema: z.ZodType<ObjectsAuthenticatorTransport>;
export let ObjectsCOSEAlgorithmSchema: z.ZodType<ObjectsCOSEAlgorithm>;
export let ObjectsCredentialProtectionPolicySchema: z.ZodType<ObjectsCredentialProtectionPolicy>;
export let ObjectsLargeBlobSupportSchema: z.ZodType<ObjectsLargeBlobSupport>;
export let ObjectsPublicKeyCredentialDescriptorSchema: z.ZodType<ObjectsPublicKeyCredentialDescriptor>;
export let ObjectsPublicKeyCredentialHintSchema: z.ZodType<ObjectsPublicKeyCredentialHint>;
export let ObjectsPublicKeyCredentialTypeSchema: z.ZodType<ObjectsPublicKeyCredentialType>;
export let ObjectsResidentKeyRequirementSchema: z.ZodType<ObjectsResidentKeyRequirement>;
export let ObjectsUserVerificationRequirementSchema: z.ZodType<ObjectsUserVerificationRequirement>;
export let PagedResultOfCommerceProductsProductSchema: z.ZodType<PagedResultOfCommerceProductsProduct>;
export let PagedResultOfCommerceProductsPromoCodeSchema: z.ZodType<PagedResultOfCommerceProductsPromoCode>;
export let PagedResultOfCommerceProductsSupportTicketSchema: z.ZodType<PagedResultOfCommerceProductsSupportTicket>;
export let PagedResultOfCommerceSubscriptionsSubscriptionSchema: z.ZodType<PagedResultOfCommerceSubscriptionsSubscription>;
export let PagedResultOfCommerceSubscriptionsSubscriptionNotificationSchema: z.ZodType<PagedResultOfCommerceSubscriptionsSubscriptionNotification>;
export let PagedResultOfIdentityTenantsTenantSchema: z.ZodType<PagedResultOfIdentityTenantsTenant>;
export let PagedResultOfIdentityTenantsTenantAuditLogEntrySchema: z.ZodType<PagedResultOfIdentityTenantsTenantAuditLogEntry>;
export let PagedResultOfIdentityUsersUserSchema: z.ZodType<PagedResultOfIdentityUsersUser>;
export let PagedResultOfIdentityUsersUserNotificationSchema: z.ZodType<PagedResultOfIdentityUsersUserNotification>;
export let PagedResultOfIdentityUsersUserProfileSchema: z.ZodType<PagedResultOfIdentityUsersUserProfile>;
export let ProgramCategorySchema: z.ZodType<ProgramCategory>;
export let ProjectsAddCollaboratorInputSchema: z.ZodType<ProjectsAddCollaboratorInput>;
export let ProjectsAddProjectCollaboratorInputSchema: z.ZodType<ProjectsAddProjectCollaboratorInput>;
export let ProjectsCollaboratorSchema: z.ZodType<ProjectsCollaborator>;
export let ProjectsCreateProjectInputSchema: z.ZodType<ProjectsCreateProjectInput>;
export let ProjectsCreateProjectVersionInputSchema: z.ZodType<ProjectsCreateProjectVersionInput>;
export let ProjectsDevelopmentStatusSchema: z.ZodType<ProjectsDevelopmentStatus>;
export let ProjectsEffectivePermissionSchema: z.ZodType<ProjectsEffectivePermission>;
export let ProjectsInvitationResultSchema: z.ZodType<ProjectsInvitationResult>;
export let ProjectsInviteProjectCollaboratorInputSchema: z.ZodType<ProjectsInviteProjectCollaboratorInput>;
export let ProjectsLinkProjectStoreProductInputSchema: z.ZodType<ProjectsLinkProjectStoreProductInput>;
export let ProjectsPermissionUpdateResultSchema: z.ZodType<ProjectsPermissionUpdateResult>;
export let ProjectsProjectSchema: z.ZodType<ProjectsProject>;
export let ProjectsProjectApiOutputSchema: z.ZodType<ProjectsProjectApiOutput>;
export let ProjectsProjectCategorySchema: z.ZodType<ProjectsProjectCategory>;
export let ProjectsProjectCategoryApiOutputSchema: z.ZodType<ProjectsProjectCategoryApiOutput>;
export let ProjectsProjectCollaboratorSchema: z.ZodType<ProjectsProjectCollaborator>;
export let ProjectsProjectCollaboratorApiOutputSchema: z.ZodType<ProjectsProjectCollaboratorApiOutput>;
export let ProjectsProjectCollaboratorDtoSchema: z.ZodType<ProjectsProjectCollaboratorDto>;
export let ProjectsProjectFeedbackSchema: z.ZodType<ProjectsProjectFeedback>;
export let ProjectsProjectFollowerSchema: z.ZodType<ProjectsProjectFollower>;
export let ProjectsProjectInvitationSchema: z.ZodType<ProjectsProjectInvitation>;
export let ProjectsProjectInvitationStatusSchema: z.ZodType<ProjectsProjectInvitationStatus>;
export let ProjectsProjectJamSubmissionSchema: z.ZodType<ProjectsProjectJamSubmission>;
export let ProjectsProjectMemberAllocationSchema: z.ZodType<ProjectsProjectMemberAllocation>;
export let ProjectsProjectMetadataSchema: z.ZodType<ProjectsProjectMetadata>;
export let ProjectsProjectMetadataApiOutputSchema: z.ZodType<ProjectsProjectMetadataApiOutput>;
export let ProjectsProjectReleaseSchema: z.ZodType<ProjectsProjectRelease>;
export let ProjectsProjectReleaseApiOutputSchema: z.ZodType<ProjectsProjectReleaseApiOutput>;
export let ProjectsProjectRoleTemplateSchema: z.ZodType<ProjectsProjectRoleTemplate>;
export let ProjectsProjectStatisticsSchema: z.ZodType<ProjectsProjectStatistics>;
export let ProjectsProjectStoreProductProjectionSchema: z.ZodType<ProjectsProjectStoreProductProjection>;
export let ProjectsProjectTeamSchema: z.ZodType<ProjectsProjectTeam>;
export let ProjectsProjectTeamAgreementSchema: z.ZodType<ProjectsProjectTeamAgreement>;
export let ProjectsProjectTeamAgreementStatusSchema: z.ZodType<ProjectsProjectTeamAgreementStatus>;
export let ProjectsProjectTeamApiOutputSchema: z.ZodType<ProjectsProjectTeamApiOutput>;
export let ProjectsProjectTeamParticipationModeSchema: z.ZodType<ProjectsProjectTeamParticipationMode>;
export let ProjectsProjectTeamRoleSchema: z.ZodType<ProjectsProjectTeamRole>;
export let ProjectsProjectTypeSchema: z.ZodType<ProjectsProjectType>;
export let ProjectsProjectUserApiOutputSchema: z.ZodType<ProjectsProjectUserApiOutput>;
export let ProjectsProjectVersionSchema: z.ZodType<ProjectsProjectVersion>;
export let ProjectsProjectVersionApiOutputSchema: z.ZodType<ProjectsProjectVersionApiOutput>;
export let ProjectsProjectVersionOptionProjectionSchema: z.ZodType<ProjectsProjectVersionOptionProjection>;
export let ProjectsShareProjectInputSchema: z.ZodType<ProjectsShareProjectInput>;
export let ProjectsShareProjectWithRoleInputSchema: z.ZodType<ProjectsShareProjectWithRoleInput>;
export let ProjectsShareResultSchema: z.ZodType<ProjectsShareResult>;
export let ProjectsUpdateCollaboratorInputSchema: z.ZodType<ProjectsUpdateCollaboratorInput>;
export let ProjectsUpdateProjectCollaboratorInputSchema: z.ZodType<ProjectsUpdateProjectCollaboratorInput>;
export let ProjectsUpdateProjectInputSchema: z.ZodType<ProjectsUpdateProjectInput>;
export let ProjectWorkProjectWorkColumnKindSchema: z.ZodType<ProjectWorkProjectWorkColumnKind>;
export let ProjectWorkProjectWorkTaskPrioritySchema: z.ZodType<ProjectWorkProjectWorkTaskPriority>;
export let ProjectWorkProjectWorkTaskStatusSchema: z.ZodType<ProjectWorkProjectWorkTaskStatus>;
export let ResourcesArchiveResourceUsageRecordsInputSchema: z.ZodType<ResourcesArchiveResourceUsageRecordsInput>;
export let ResourcesCheckResourceQuotaInputSchema: z.ZodType<ResourcesCheckResourceQuotaInput>;
export let ResourcesCleanupOrphanedResourcesInputSchema: z.ZodType<ResourcesCleanupOrphanedResourcesInput>;
export let ResourcesContentsAddReviewInputSchema: z.ZodType<ResourcesContentsAddReviewInput>;
export let ResourcesContentsBulkGenerateContractsInputSchema: z.ZodType<ResourcesContentsBulkGenerateContractsInput>;
export let ResourcesContentsBulkGeneratedContractItemOutputSchema: z.ZodType<ResourcesContentsBulkGeneratedContractItemOutput>;
export let ResourcesContentsBulkGeneratedContractsOutputSchema: z.ZodType<ResourcesContentsBulkGeneratedContractsOutput>;
export let ResourcesContentsContentReviewDecisionSchema: z.ZodType<ResourcesContentsContentReviewDecision>;
export let ResourcesContentsContentVersionSchema: z.ZodType<ResourcesContentsContentVersion>;
export let ResourcesContentsContentVersionDiffSchema: z.ZodType<ResourcesContentsContentVersionDiff>;
export let ResourcesContentsContentVersionReviewSchema: z.ZodType<ResourcesContentsContentVersionReview>;
export let ResourcesContentsContentVersionStatusSchema: z.ZodType<ResourcesContentsContentVersionStatus>;
export let ResourcesContentsCreateDraftInputSchema: z.ZodType<ResourcesContentsCreateDraftInput>;
export let ResourcesContentsGenerateContractInputSchema: z.ZodType<ResourcesContentsGenerateContractInput>;
export let ResourcesContentsGeneratedContractOutputSchema: z.ZodType<ResourcesContentsGeneratedContractOutput>;
export let ResourcesContentsReviewInputSchema: z.ZodType<ResourcesContentsReviewInput>;
export let ResourcesContentsRollbackInputSchema: z.ZodType<ResourcesContentsRollbackInput>;
export let ResourcesContentsScheduleInputSchema: z.ZodType<ResourcesContentsScheduleInput>;
export let ResourcesContentsUpdateDraftInputSchema: z.ZodType<ResourcesContentsUpdateDraftInput>;
export let ResourcesEffectiveSettingOutputSchema: z.ZodType<ResourcesEffectiveSettingOutput>;
export let ResourcesRecordTenantResourceUsageInputSchema: z.ZodType<ResourcesRecordTenantResourceUsageInput>;
export let ResourcesRecordUserResourceUsageInputSchema: z.ZodType<ResourcesRecordUserResourceUsageInput>;
export let ResourcesResourceMetadataSchema: z.ZodType<ResourcesResourceMetadata>;
export let ResourcesResourceQuotaEnforcementResultSchema: z.ZodType<ResourcesResourceQuotaEnforcementResult>;
export let ResourcesResourceQuotaOutputSchema: z.ZodType<ResourcesResourceQuotaOutput>;
export let ResourcesResourceQuotaPeriodSchema: z.ZodType<ResourcesResourceQuotaPeriod>;
export let ResourcesResourceSettingsSchema: z.ZodType<ResourcesResourceSettings>;
export let ResourcesResourceUsageTypeSchema: z.ZodType<ResourcesResourceUsageType>;
export let ResourcesSetQuotaInputSchema: z.ZodType<ResourcesSetQuotaInput>;
export let ResourcesSetResourceMetadataInputSchema: z.ZodType<ResourcesSetResourceMetadataInput>;
export let ResourcesSetResourceSettingsInputSchema: z.ZodType<ResourcesSetResourceSettingsInput>;
export let ResourcesSetUserResourceSettingsInputSchema: z.ZodType<ResourcesSetUserResourceSettingsInput>;
export let ResourcesToggleResourceQuotaInputSchema: z.ZodType<ResourcesToggleResourceQuotaInput>;
export let ResourcesTrendGranularitySchema: z.ZodType<ResourcesTrendGranularity>;
export let ResourcesUsageRecordSchema: z.ZodType<ResourcesUsageRecord>;
export let ResourcesUsageTrendDataPointSchema: z.ZodType<ResourcesUsageTrendDataPoint>;
export let ResourcesUsageTrendsResultSchema: z.ZodType<ResourcesUsageTrendsResult>;
export let SocialBlogBlogPostSchema: z.ZodType<SocialBlogBlogPost>;
export let SocialBlogBlogPostStatusSchema: z.ZodType<SocialBlogBlogPostStatus>;
export let SocialBlogCreateBlogPostInputSchema: z.ZodType<SocialBlogCreateBlogPostInput>;
export let SocialFeedAddFeedItemInputSchema: z.ZodType<SocialFeedAddFeedItemInput>;
export let SocialFeedFeedContentTypeSchema: z.ZodType<SocialFeedFeedContentType>;
export let SocialFeedFeedItemSchema: z.ZodType<SocialFeedFeedItem>;
export let SocialFeedFeedItemReasonSchema: z.ZodType<SocialFeedFeedItemReason>;
export let SocialGroupsApproveSocialGroupMemberInputSchema: z.ZodType<SocialGroupsApproveSocialGroupMemberInput>;
export let SocialGroupsChangeSocialGroupMemberRoleInputSchema: z.ZodType<SocialGroupsChangeSocialGroupMemberRoleInput>;
export let SocialGroupsCreateSocialGroupInputSchema: z.ZodType<SocialGroupsCreateSocialGroupInput>;
export let SocialGroupsJoinSocialGroupInputSchema: z.ZodType<SocialGroupsJoinSocialGroupInput>;
export let SocialGroupsSocialGroupSchema: z.ZodType<SocialGroupsSocialGroup>;
export let SocialGroupsSocialGroupMemberSchema: z.ZodType<SocialGroupsSocialGroupMember>;
export let SocialGroupsSocialGroupMemberRoleSchema: z.ZodType<SocialGroupsSocialGroupMemberRole>;
export let SocialGroupsSocialGroupMembershipStatusSchema: z.ZodType<SocialGroupsSocialGroupMembershipStatus>;
export let SocialGroupsSocialGroupStatusSchema: z.ZodType<SocialGroupsSocialGroupStatus>;
export let SocialGroupsSocialGroupTypeSchema: z.ZodType<SocialGroupsSocialGroupType>;
export let SocialGroupsSocialGroupVisibilitySchema: z.ZodType<SocialGroupsSocialGroupVisibility>;
export let SocialGroupsUpdateSocialGroupInputSchema: z.ZodType<SocialGroupsUpdateSocialGroupInput>;
export let SocialPostsControllersAddCommentInputSchema: z.ZodType<SocialPostsControllersAddCommentInput>;
export let SocialPostsControllersCreatePostInputSchema: z.ZodType<SocialPostsControllersCreatePostInput>;
export let SocialPostsControllersFollowPostInputSchema: z.ZodType<SocialPostsControllersFollowPostInput>;
export let SocialPostsControllersUpdateCommentInputSchema: z.ZodType<SocialPostsControllersUpdateCommentInput>;
export let SocialPostsControllersUpdatePostInputSchema: z.ZodType<SocialPostsControllersUpdatePostInput>;
export let SocialPostsMediaTypeSchema: z.ZodType<SocialPostsMediaType>;
export let SocialPostsPostVisibilitySchema: z.ZodType<SocialPostsPostVisibility>;
export let SocialProfilesAddProfilePortfolioItemBodySchema: z.ZodType<SocialProfilesAddProfilePortfolioItemBody>;
export let SocialProfilesAddProfileSkillBodySchema: z.ZodType<SocialProfilesAddProfileSkillBody>;
export let SocialProfilesProfileAvailabilityStatusSchema: z.ZodType<SocialProfilesProfileAvailabilityStatus>;
export let SocialProfilesProfilePortfolioItemSchema: z.ZodType<SocialProfilesProfilePortfolioItem>;
export let SocialProfilesProfileSkillSchema: z.ZodType<SocialProfilesProfileSkill>;
export let SocialProfilesProfileSkillProficiencySchema: z.ZodType<SocialProfilesProfileSkillProficiency>;
export let SocialProfilesProfileVisibilitySchema: z.ZodType<SocialProfilesProfileVisibility>;
export let SocialProfilesSocialProfileSchema: z.ZodType<SocialProfilesSocialProfile>;
export let SocialProfilesUpdateProfilePortfolioItemBodySchema: z.ZodType<SocialProfilesUpdateProfilePortfolioItemBody>;
export let SocialProfilesUpdateProfilePrivacyBodySchema: z.ZodType<SocialProfilesUpdateProfilePrivacyBody>;
export let SocialProfilesUpdateProfileStatsBodySchema: z.ZodType<SocialProfilesUpdateProfileStatsBody>;
export let SocialProfilesUpdateSocialProfileBodySchema: z.ZodType<SocialProfilesUpdateSocialProfileBody>;
export let SocialReactionsReactionSchema: z.ZodType<SocialReactionsReaction>;
export let SocialReactionsReactionTargetTypeSchema: z.ZodType<SocialReactionsReactionTargetType>;
export let SocialReactionsReactionTypeSchema: z.ZodType<SocialReactionsReactionType>;
export let SocialReactionsRemoveReactionInputSchema: z.ZodType<SocialReactionsRemoveReactionInput>;
export let SocialReactionsSetReactionInputSchema: z.ZodType<SocialReactionsSetReactionInput>;
export let SocialReactionsTargetReactionSummarySchema: z.ZodType<SocialReactionsTargetReactionSummary>;
export let SystemDayOfWeekSchema: z.ZodType<SystemDayOfWeek>;
export let TeamsTeamSchema: z.ZodType<TeamsTeam>;
export let TeamsTeamInvitationSchema: z.ZodType<TeamsTeamInvitation>;
export let TeamsTeamMemberSchema: z.ZodType<TeamsTeamMember>;
export let TeamsTeamMemberAuthoritySchema: z.ZodType<TeamsTeamMemberAuthority>;
export let TeamsTeamStatusSchema: z.ZodType<TeamsTeamStatus>;
export let TeamsTeamVisibilitySchema: z.ZodType<TeamsTeamVisibility>;
export let TenantInfoSchema: z.ZodType<TenantInfo>;
export let TestingLabAddTestingEventCommitteeMemberInputSchema: z.ZodType<TestingLabAddTestingEventCommitteeMemberInput>;
export let TestingLabAssignTestingLabRoleInputSchema: z.ZodType<TestingLabAssignTestingLabRoleInput>;
export let TestingLabAssignTestingProjectApplicationSlotInputSchema: z.ZodType<TestingLabAssignTestingProjectApplicationSlotInput>;
export let TestingLabAssignTestingProjectToTesterInputSchema: z.ZodType<TestingLabAssignTestingProjectToTesterInput>;
export let TestingLabAttendanceStatusSchema: z.ZodType<TestingLabAttendanceStatus>;
export let TestingLabCancelTestingEventInputSchema: z.ZodType<TestingLabCancelTestingEventInput>;
export let TestingLabCastTestingApplicationVoteInputSchema: z.ZodType<TestingLabCastTestingApplicationVoteInput>;
export let TestingLabConfigureTestingEventLearningInputSchema: z.ZodType<TestingLabConfigureTestingEventLearningInput>;
export let TestingLabCreateSimpleTestingInputSchema: z.ZodType<TestingLabCreateSimpleTestingInput>;
export let TestingLabCreateTestingEventInputSchema: z.ZodType<TestingLabCreateTestingEventInput>;
export let TestingLabCreateTestingInputSchema: z.ZodType<TestingLabCreateTestingInput>;
export let TestingLabCreateTestingLabRoleInputSchema: z.ZodType<TestingLabCreateTestingLabRoleInput>;
export let TestingLabCreateTestingLabSettingsSchema: z.ZodType<TestingLabCreateTestingLabSettings>;
export let TestingLabCreateTestingLocationSchema: z.ZodType<TestingLabCreateTestingLocation>;
export let TestingLabCreateTestingSessionSchema: z.ZodType<TestingLabCreateTestingSession>;
export let TestingLabDecideTestingProjectApplicationInputSchema: z.ZodType<TestingLabDecideTestingProjectApplicationInput>;
export let TestingLabFeedbackFormTypeSchema: z.ZodType<TestingLabFeedbackFormType>;
export let TestingLabFeedbackInputSchema: z.ZodType<TestingLabFeedbackInput>;
export let TestingLabFeedbackQualitySchema: z.ZodType<TestingLabFeedbackQuality>;
export let TestingLabFeedbackQualityRatingSchema: z.ZodType<TestingLabFeedbackQualityRating>;
export let TestingLabGrantResourcePermissionInputSchema: z.ZodType<TestingLabGrantResourcePermissionInput>;
export let TestingLabInstructionTypeSchema: z.ZodType<TestingLabInstructionType>;
export let TestingLabLinkSessionProjectInputSchema: z.ZodType<TestingLabLinkSessionProjectInput>;
export let TestingLabLocationStatusSchema: z.ZodType<TestingLabLocationStatus>;
export let TestingLabParticipationStatusSchema: z.ZodType<TestingLabParticipationStatus>;
export let TestingLabPublicTestingEventProjectionSchema: z.ZodType<TestingLabPublicTestingEventProjection>;
export let TestingLabPublicTestingEventSlotProjectionSchema: z.ZodType<TestingLabPublicTestingEventSlotProjection>;
export let TestingLabRateFeedbackQualitySchema: z.ZodType<TestingLabRateFeedbackQuality>;
export let TestingLabRegisterTestingEventSlotInputSchema: z.ZodType<TestingLabRegisterTestingEventSlotInput>;
export let TestingLabRegistrationStatusSchema: z.ZodType<TestingLabRegistrationStatus>;
export let TestingLabRegistrationTypeSchema: z.ZodType<TestingLabRegistrationType>;
export let TestingLabReportFeedbackSchema: z.ZodType<TestingLabReportFeedback>;
export let TestingLabSessionProjectProjectionSchema: z.ZodType<TestingLabSessionProjectProjection>;
export let TestingLabSessionRegistrationSchema: z.ZodType<TestingLabSessionRegistration>;
export let TestingLabSessionRegistrationInputSchema: z.ZodType<TestingLabSessionRegistrationInput>;
export let TestingLabSessionStatusSchema: z.ZodType<TestingLabSessionStatus>;
export let TestingLabSessionWaitlistSchema: z.ZodType<TestingLabSessionWaitlist>;
export let TestingLabSubmitFeedbackSchema: z.ZodType<TestingLabSubmitFeedback>;
export let TestingLabSubmitTestingEventFeedbackInputSchema: z.ZodType<TestingLabSubmitTestingEventFeedbackInput>;
export let TestingLabSubmitTestingProjectApplicationInputSchema: z.ZodType<TestingLabSubmitTestingProjectApplicationInput>;
export let TestingLabTestingApplicationReviewAssetProjectionSchema: z.ZodType<TestingLabTestingApplicationReviewAssetProjection>;
export let TestingLabTestingApplicationReviewPackageProjectionSchema: z.ZodType<TestingLabTestingApplicationReviewPackageProjection>;
export let TestingLabTestingApplicationStatusSchema: z.ZodType<TestingLabTestingApplicationStatus>;
export let TestingLabTestingApplicationTesterEligibilityProjectionSchema: z.ZodType<TestingLabTestingApplicationTesterEligibilityProjection>;
export let TestingLabTestingApplicationVoteSchema: z.ZodType<TestingLabTestingApplicationVote>;
export let TestingLabTestingApplicationVoteDecisionSchema: z.ZodType<TestingLabTestingApplicationVoteDecision>;
export let TestingLabTestingApplicationVoteProjectionSchema: z.ZodType<TestingLabTestingApplicationVoteProjection>;
export let TestingLabTestingCommitteeMemberSchema: z.ZodType<TestingLabTestingCommitteeMember>;
export let TestingLabTestingContextSchema: z.ZodType<TestingLabTestingContext>;
export let TestingLabTestingEventSchema: z.ZodType<TestingLabTestingEvent>;
export let TestingLabTestingEventApprovalModeSchema: z.ZodType<TestingLabTestingEventApprovalMode>;
export let TestingLabTestingEventCommitteeMemberProjectionSchema: z.ZodType<TestingLabTestingEventCommitteeMemberProjection>;
export let TestingLabTestingEventFeedbackProjectionSchema: z.ZodType<TestingLabTestingEventFeedbackProjection>;
export let TestingLabTestingEventFeedbackReviewProjectionSchema: z.ZodType<TestingLabTestingEventFeedbackReviewProjection>;
export let TestingLabTestingEventModeSchema: z.ZodType<TestingLabTestingEventMode>;
export let TestingLabTestingEventProjectionSchema: z.ZodType<TestingLabTestingEventProjection>;
export let TestingLabTestingEventRecurrenceFrequencySchema: z.ZodType<TestingLabTestingEventRecurrenceFrequency>;
export let TestingLabTestingEventRecurrenceInputSchema: z.ZodType<TestingLabTestingEventRecurrenceInput>;
export let TestingLabTestingEventSlotSchema: z.ZodType<TestingLabTestingEventSlot>;
export let TestingLabTestingEventSlotProjectionSchema: z.ZodType<TestingLabTestingEventSlotProjection>;
export let TestingLabTestingEventStatusSchema: z.ZodType<TestingLabTestingEventStatus>;
export let TestingLabTestingFeedbackSchema: z.ZodType<TestingLabTestingFeedback>;
export let TestingLabTestingFeedbackDirectoryItemSchema: z.ZodType<TestingLabTestingFeedbackDirectoryItem>;
export let TestingLabTestingFeedbackDirectoryPageSchema: z.ZodType<TestingLabTestingFeedbackDirectoryPage>;
export let TestingLabTestingFeedbackFormSchema: z.ZodType<TestingLabTestingFeedbackForm>;
export let TestingLabTestingFeedbackObligationProjectionSchema: z.ZodType<TestingLabTestingFeedbackObligationProjection>;
export let TestingLabTestingFeedbackObligationStatusSchema: z.ZodType<TestingLabTestingFeedbackObligationStatus>;
export let TestingLabTestingFeedbackSourceSchema: z.ZodType<TestingLabTestingFeedbackSource>;
export let TestingLabTestingInputSchema: z.ZodType<TestingLabTestingInput>;
export let TestingLabTestingLabAnalyticsReportProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsReportProjection>;
export let TestingLabTestingLabAnalyticsSummaryProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsSummaryProjection>;
export let TestingLabTestingLabAnalyticsTrendProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsTrendProjection>;
export let TestingLabTestingLabEventAnalyticsProjectionSchema: z.ZodType<TestingLabTestingLabEventAnalyticsProjection>;
export let TestingLabTestingLabLocationAnalyticsProjectionSchema: z.ZodType<TestingLabTestingLabLocationAnalyticsProjection>;
export let TestingLabTestingLabPermissionsSchema: z.ZodType<TestingLabTestingLabPermissions>;
export let TestingLabTestingLabResourcePermissionSchema: z.ZodType<TestingLabTestingLabResourcePermission>;
export let TestingLabTestingLabRoleTemplateSchema: z.ZodType<TestingLabTestingLabRoleTemplate>;
export let TestingLabTestingLabSettingsSchema: z.ZodType<TestingLabTestingLabSettings>;
export let TestingLabTestingLearningCompletionRequirementSchema: z.ZodType<TestingLabTestingLearningCompletionRequirement>;
export let TestingLabTestingLocationSchema: z.ZodType<TestingLabTestingLocation>;
export let TestingLabTestingModeSchema: z.ZodType<TestingLabTestingMode>;
export let TestingLabTestingParticipantSchema: z.ZodType<TestingLabTestingParticipant>;
export let TestingLabTestingParticipantDirectoryItemProjectionSchema: z.ZodType<TestingLabTestingParticipantDirectoryItemProjection>;
export let TestingLabTestingParticipantDirectoryProjectionSchema: z.ZodType<TestingLabTestingParticipantDirectoryProjection>;
export let TestingLabTestingParticipantMutationProjectionSchema: z.ZodType<TestingLabTestingParticipantMutationProjection>;
export let TestingLabTestingPrioritySchema: z.ZodType<TestingLabTestingPriority>;
export let TestingLabTestingProjectApplicationSchema: z.ZodType<TestingLabTestingProjectApplication>;
export let TestingLabTestingProjectApplicationProjectionSchema: z.ZodType<TestingLabTestingProjectApplicationProjection>;
export let TestingLabTestingRequestDetailProjectionSchema: z.ZodType<TestingLabTestingRequestDetailProjection>;
export let TestingLabTestingRequestProjectProjectionSchema: z.ZodType<TestingLabTestingRequestProjectProjection>;
export let TestingLabTestingRequestProjectVersionProjectionSchema: z.ZodType<TestingLabTestingRequestProjectVersionProjection>;
export let TestingLabTestingRequestStatusSchema: z.ZodType<TestingLabTestingRequestStatus>;
export let TestingLabTestingSessionSchema: z.ZodType<TestingLabTestingSession>;
export let TestingLabTestingSlotRegistrationProjectionSchema: z.ZodType<TestingLabTestingSlotRegistrationProjection>;
export let TestingLabTestingSlotRegistrationStatusSchema: z.ZodType<TestingLabTestingSlotRegistrationStatus>;
export let TestingLabUpdateAttendanceSchema: z.ZodType<TestingLabUpdateAttendance>;
export let TestingLabUpdateTestingEventInputSchema: z.ZodType<TestingLabUpdateTestingEventInput>;
export let TestingLabUpdateTestingInputSchema: z.ZodType<TestingLabUpdateTestingInput>;
export let TestingLabUpdateTestingLabRoleInputSchema: z.ZodType<TestingLabUpdateTestingLabRoleInput>;
export let TestingLabUpdateTestingLabSettingsSchema: z.ZodType<TestingLabUpdateTestingLabSettings>;
export let TestingLabUpdateTestingLocationSchema: z.ZodType<TestingLabUpdateTestingLocation>;
export let TestingLabUpdateTestingProjectApplicationInputSchema: z.ZodType<TestingLabUpdateTestingProjectApplicationInput>;
export let TestingLabUpsertTestingEventSlotInputSchema: z.ZodType<TestingLabUpsertTestingEventSlotInput>;
export let TestingLabUserTestingLabPermissionsSchema: z.ZodType<TestingLabUserTestingLabPermissions>;

// Zod Schema Definitions
/** Zod schema for AIAiChatInput */
AIAiChatInputSchema = z.object({
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  messages: z
    .array(z.lazy(() => AIAiChatMessageSchema))
    .nullable()
    .optional(),
  temperature: z.number().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AIAiChatMessage */
AIAiChatMessageSchema = z.object({
  role: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
});

/** Zod schema for AIAiCompletionOutput */
AIAiCompletionOutputSchema = z.object({
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  text: z.string().nullable().optional(),
  finishReason: z.string().nullable().optional(),
  usage: z.lazy(() => AIAiUsageSchema).optional(),
});

/** Zod schema for AIAiConversationHistoryEntry */
AIAiConversationHistoryEntrySchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().nullable().optional(),
  requestKind: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  requestText: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  responseText: z.string().nullable().optional(),
  outcome: z.string().nullable().optional(),
  outcomeCode: z.string().nullable().optional(),
  outcomeReason: z.string().nullable().optional(),
  finishReason: z.string().nullable().optional(),
  usage: z.lazy(() => AIAiUsageSchema).optional(),
  occurredAt: z.string().datetime().optional(),
});

/** Zod schema for AIAiGeneratedContentDraftInput */
AIAiGeneratedContentDraftInputSchema = z.object({
  subject: z.string().nullable().optional(),
  context: z.string().nullable().optional(),
  audience: z.string().nullable().optional(),
  tone: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AIAiGeneratedContentInput */
AIAiGeneratedContentInputSchema = z.object({
  kind: z.lazy(() => AIAiGeneratedContentKindSchema).optional(),
  subject: z.string().nullable().optional(),
  context: z.string().nullable().optional(),
  audience: z.string().nullable().optional(),
  tone: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AIAiGeneratedContentKind */
AIAiGeneratedContentKindSchema = z.enum([
  "Email",
  "Report",
  "ListingDescription",
]);

/** Zod schema for AIAiGenerateInput */
AIAiGenerateInputSchema = z.object({
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  temperature: z.number().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AIAiPromptTemplate */
AIAiPromptTemplateSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  isSystemTemplate: z.boolean().optional(),
  createdByUserId: z.string().uuid().nullable().optional(),
  updatedByUserId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for AIAiPromptTemplateGenerateInput */
AIAiPromptTemplateGenerateInputSchema = z.object({
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
  provider: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  temperature: z.number().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AIAiPromptTemplateRenderInput */
AIAiPromptTemplateRenderInputSchema = z.object({
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AIAiPromptTemplateRenderOutput */
AIAiPromptTemplateRenderOutputSchema = z.object({
  templateId: z.string().uuid().optional(),
  key: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AIAiProviderStatus */
AIAiProviderStatusSchema = z.object({
  provider: z.string().nullable().optional(),
  configured: z.boolean().optional(),
  defaultModel: z.string().nullable().optional(),
  baseUrl: z.string().nullable().optional(),
  credentialsConfigured: z.boolean().optional(),
});

/** Zod schema for AIAiQuotaStatus */
AIAiQuotaStatusSchema = z.object({
  resourceType: z.string().nullable().optional(),
  currentUsage: z.number().int().optional(),
  softLimit: z.number().int().nullable().optional(),
  hardLimit: z.number().int().nullable().optional(),
  remaining: z.number().int().optional(),
  usagePercent: z.number().optional(),
  period: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  lastReset: z.string().datetime().nullable().optional(),
  nextReset: z.string().datetime().nullable().optional(),
});

/** Zod schema for AIAiQuotaStatusOutput */
AIAiQuotaStatusOutputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  quotas: z
    .array(z.lazy(() => AIAiQuotaStatusSchema))
    .nullable()
    .optional(),
  generatedAtUtc: z.string().datetime().optional(),
});

/** Zod schema for AIAiStatusOutput */
AIAiStatusOutputSchema = z.object({
  enabled: z.boolean().optional(),
  defaultProvider: z.string().nullable().optional(),
  allowTenantOverrides: z.boolean().optional(),
  providers: z
    .array(z.lazy(() => AIAiProviderStatusSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AIAiUsage */
AIAiUsageSchema = z.object({
  inputTokens: z.number().int().nullable().optional(),
  outputTokens: z.number().int().nullable().optional(),
  totalTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AICreateAiPromptTemplateInput */
AICreateAiPromptTemplateInputSchema = z.object({
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
});

/** Zod schema for AIUpdateAiPromptTemplateInput */
AIUpdateAiPromptTemplateInputSchema = z.object({
  name: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
});

/** Zod schema for AnalyticsAnalyticsWarehouseFact */
AnalyticsAnalyticsWarehouseFactSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  factName: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
  runId: z.string().uuid().nullable().optional(),
  metric: z.string().nullable().optional(),
  count: z.number().int().nullable().optional(),
  amountUsd: z.number().nullable().optional(),
  dimensions: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AnalyticsAnalyticsWarehouseRunInput */
AnalyticsAnalyticsWarehouseRunInputSchema = z.object({
  asOfUtc: z.string().datetime().nullable().optional(),
  lookbackDays: z.number().int().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AnalyticsAnalyticsWarehouseRunOutput */
AnalyticsAnalyticsWarehouseRunOutputSchema = z.object({
  runId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  startUtc: z.string().datetime().optional(),
  asOfUtc: z.string().datetime().optional(),
  factsCreated: z.number().int().optional(),
  factsByName: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for AnalyticsAnalyzeFunnelQuery */
AnalyticsAnalyzeFunnelQuerySchema = z.object({
  steps: z.array(z.string()).nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AnalyticsCreateDashboardInput */
AnalyticsCreateDashboardInputSchema = z.object({
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  widgets: z
    .array(z.lazy(() => AnalyticsDashboardWidgetInputSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AnalyticsDashboard */
AnalyticsDashboardSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  widgets: z
    .array(z.lazy(() => AnalyticsDashboardWidgetSchema))
    .nullable()
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for AnalyticsDashboardWidget */
AnalyticsDashboardWidgetSchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => AnalyticsWidgetTypeSchema).optional(),
  sortOrder: z.number().int().optional(),
  configuration: z.string().nullable().optional(),
});

/** Zod schema for AnalyticsDashboardWidgetInput */
AnalyticsDashboardWidgetInputSchema = z.object({
  title: z.string().nullable().optional(),
  type: z.lazy(() => AnalyticsWidgetTypeSchema).optional(),
  sortOrder: z.number().int().optional(),
  configuration: z.string().nullable().optional(),
});

/** Zod schema for AnalyticsProductCapacityMetrics */
AnalyticsProductCapacityMetricsSchema = z.object({
  totalUserLimit: z.number().int().optional(),
  totalStorageMbLimit: z.number().int().optional(),
  totalApiCallsLimit: z.number().int().optional(),
  unlimitedUserPlans: z.number().int().optional(),
  unlimitedStoragePlans: z.number().int().optional(),
  unlimitedApiCallPlans: z.number().int().optional(),
});

/** Zod schema for AnalyticsProductCatalogMetrics */
AnalyticsProductCatalogMetricsSchema = z.object({
  totalProducts: z.number().int().optional(),
  publishedProducts: z.number().int().optional(),
  draftProducts: z.number().int().optional(),
  bundles: z.number().int().optional(),
});

/** Zod schema for AnalyticsProductMetricsExportFormat */
AnalyticsProductMetricsExportFormatSchema = z.enum(["Csv", "Json"]);

/** Zod schema for AnalyticsProductMetricsOutput */
AnalyticsProductMetricsOutputSchema = z.object({
  startUtc: z.string().datetime().optional(),
  endUtc: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  catalog: z.lazy(() => AnalyticsProductCatalogMetricsSchema).optional(),
  revenue: z.lazy(() => AnalyticsProductRevenueMetricsSchema).optional(),
  subscriptions: z
    .lazy(() => AnalyticsProductSubscriptionMetricsSchema)
    .optional(),
  capacity: z.lazy(() => AnalyticsProductCapacityMetricsSchema).optional(),
  thresholds: z
    .array(z.lazy(() => AnalyticsProductMetricThresholdSchema))
    .nullable()
    .optional(),
  generatedAtUtc: z.string().datetime().optional(),
});

/** Zod schema for AnalyticsProductMetricThreshold */
AnalyticsProductMetricThresholdSchema = z.object({
  key: z.string().nullable().optional(),
  status: z.lazy(() => AnalyticsProductMetricThresholdStatusSchema).optional(),
  message: z.string().nullable().optional(),
  value: z.number().optional(),
  warningAt: z.number().optional(),
  criticalAt: z.number().optional(),
});

/** Zod schema for AnalyticsProductMetricThresholdStatus */
AnalyticsProductMetricThresholdStatusSchema = z.enum([
  "Healthy",
  "Warning",
  "Critical",
]);

/** Zod schema for AnalyticsProductRevenueMetrics */
AnalyticsProductRevenueMetricsSchema = z.object({
  monthlyRecurringRevenue: z.number().optional(),
  annualRecurringRevenue: z.number().optional(),
  salesVolume: z.number().optional(),
  currency: z.string().nullable().optional(),
});

/** Zod schema for AnalyticsProductSubscriptionMetrics */
AnalyticsProductSubscriptionMetricsSchema = z.object({
  totalSubscribers: z.number().int().optional(),
  activeSubscribers: z.number().int().optional(),
  trialSubscribers: z.number().int().optional(),
  pastDueSubscribers: z.number().int().optional(),
  cancelledSubscribers: z.number().int().optional(),
  cancelledInPeriod: z.number().int().optional(),
  churnRate: z.number().optional(),
  retentionRate: z.number().optional(),
});

/** Zod schema for AnalyticsTimeSeriesGranularity */
AnalyticsTimeSeriesGranularitySchema = z.enum(["Hour", "Day", "Week", "Month"]);

/** Zod schema for AnalyticsTrackAnalyticsEventCommand */
AnalyticsTrackAnalyticsEventCommandSchema = z.object({
  eventName: z.string().nullable().optional(),
  propertiesJson: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AnalyticsUpdateDashboardInput */
AnalyticsUpdateDashboardInputSchema = z.object({
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  widgets: z
    .array(z.lazy(() => AnalyticsDashboardWidgetInputSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AnalyticsWidgetType */
AnalyticsWidgetTypeSchema = z.enum([
  "Counter",
  "Chart",
  "Table",
  "Gauge",
  "TimeSeries",
  "Funnel",
]);

/** Zod schema for APIAccessAccessCapabilitiesOutput */
APIAccessAccessCapabilitiesOutputSchema = z.object({
  capabilities: z.array(z.string()).nullable().optional(),
});

/** Zod schema for APIControllersApplicationDetails */
APIControllersApplicationDetailsSchema = z.object({
  name: z.string().nullable().optional(),
  version: z.string().nullable().optional(),
  informationalVersion: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for APIControllersApplicationInfoOutput */
APIControllersApplicationInfoOutputSchema = z.object({
  application: z.lazy(() => APIControllersApplicationDetailsSchema).optional(),
  build: z.lazy(() => APIControllersBuildDetailsSchema).optional(),
  runtime: z.lazy(() => APIControllersRuntimeDetailsSchema).optional(),
  process: z.lazy(() => APIControllersProcessDetailsSchema).optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for APIControllersBuildDetails */
APIControllersBuildDetailsSchema = z.object({
  timestamp: z.string().datetime().nullable().optional(),
  configuration: z.string().nullable().optional(),
  framework: z.string().nullable().optional(),
});

/** Zod schema for APIControllersDependencyHealthItem */
APIControllersDependencyHealthItemSchema = z.object({
  name: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  duration: z.string().optional(),
  description: z.string().nullable().optional(),
  isHealthy: z.boolean().optional(),
  tags: z.array(z.string()).nullable().optional(),
  data: z.record(z.string(), z.string()).nullable().optional(),
  exception: z.string().nullable().optional(),
});

/** Zod schema for APIControllersDependencyHealthOutput */
APIControllersDependencyHealthOutputSchema = z.object({
  status: z.string().nullable().optional(),
  totalDuration: z.string().optional(),
  timestamp: z.string().datetime().optional(),
  healthyCount: z.number().int().optional(),
  unhealthyCount: z.number().int().optional(),
  dependencies: z
    .array(z.lazy(() => APIControllersDependencyHealthItemSchema))
    .nullable()
    .optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for APIControllersEconomySelfServiceCapability */
APIControllersEconomySelfServiceCapabilitySchema = z.object({
  capability: z
    .lazy(() => EconomyRiskEconomyValueMovementCapabilitySchema)
    .optional(),
  state: z.lazy(() => APISetupEconomyCapabilityReadinessStateSchema).optional(),
  diagnostics: z.array(z.string()).nullable().optional(),
});

/** Zod schema for APIControllersHealthinessOutput */
APIControllersHealthinessOutputSchema = z.object({
  status: z.string().nullable().optional(),
  duration: z.string().optional(),
  timestamp: z.string().datetime().optional(),
  checks: z
    .record(
      z.string(),
      z.lazy(() => APIControllersHealthinessResponseItemSchema),
    )
    .nullable()
    .optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for APIControllersHealthinessResponseItem */
APIControllersHealthinessResponseItemSchema = z.object({
  status: z.string().nullable().optional(),
  duration: z.string().optional(),
  description: z.string().nullable().optional(),
  data: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for APIControllersLivenessOutput */
APIControllersLivenessOutputSchema = z.object({
  status: z.string().nullable().optional(),
  alive: z.boolean().optional(),
  timestamp: z.string().datetime().optional(),
  uptime: z.string().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for APIControllersProcessDetails */
APIControllersProcessDetailsSchema = z.object({
  startTime: z.string().datetime().optional(),
  uptime: z.string().optional(),
});

/** Zod schema for APIControllersReadinessOutput */
APIControllersReadinessOutputSchema = z.object({
  status: z.string().nullable().optional(),
  ready: z.boolean().optional(),
  timestamp: z.string().datetime().optional(),
  services: z.record(z.string(), z.boolean()).nullable().optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for APIControllersRuntimeDetails */
APIControllersRuntimeDetailsSchema = z.object({
  dotNetVersion: z.string().nullable().optional(),
  osDescription: z.string().nullable().optional(),
  osArchitecture: z.string().nullable().optional(),
  processArchitecture: z.string().nullable().optional(),
});

/** Zod schema for APIProjectsAddProjectTeamInput */
APIProjectsAddProjectTeamInputSchema = z.object({
  teamId: z.string().uuid().optional(),
  role: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  notes: z.string().nullable().optional(),
  contributionPercentage: z.number().optional(),
});

/** Zod schema for APIProjectsCounterProjectTeamAgreementInput */
APIProjectsCounterProjectTeamAgreementInputSchema = z.object({
  scope: z.string().nullable().optional(),
  deliverables: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
});

/** Zod schema for APIProjectsCreateProjectAllocationInput */
APIProjectsCreateProjectAllocationInputSchema = z.object({
  projectTeamId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  function: z.string().nullable().optional(),
  capacityPercentage: z.number().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectsCreateProjectTeamAgreementInput */
APIProjectsCreateProjectTeamAgreementInputSchema = z.object({
  proposingTeamId: z.string().uuid().optional(),
  receivingTeamId: z.string().uuid().optional(),
  scope: z.string().nullable().optional(),
  deliverables: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
});

/** Zod schema for APIProjectsProjectAllocation */
APIProjectsProjectAllocationSchema = z.object({
  id: z.string().uuid().optional(),
  projectTeamId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  function: z.string().nullable().optional(),
  capacityPercentage: z.number().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for APIProjectsProjectOwnership */
APIProjectsProjectOwnershipSchema = z.object({
  projectId: z.string().uuid().optional(),
  teams: z
    .array(z.lazy(() => APIProjectsProjectTeamOwnershipSchema))
    .nullable()
    .optional(),
  allocations: z
    .array(z.lazy(() => APIProjectsProjectAllocationSchema))
    .nullable()
    .optional(),
  agreements: z
    .array(z.lazy(() => APIProjectsProjectTeamAgreementSchema))
    .nullable()
    .optional(),
});

/** Zod schema for APIProjectsProjectTeamAgreement */
APIProjectsProjectTeamAgreementSchema = z.object({
  id: z.string().uuid().optional(),
  proposingTeamId: z.string().uuid().optional(),
  receivingTeamId: z.string().uuid().optional(),
  proposedByUserId: z.string().uuid().optional(),
  acceptedByUserId: z.string().uuid().nullable().optional(),
  status: z.lazy(() => ProjectsProjectTeamAgreementStatusSchema).optional(),
  scope: z.string().nullable().optional(),
  deliverables: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  revision: z.number().int().optional(),
});

/** Zod schema for APIProjectsProjectTeamOwnership */
APIProjectsProjectTeamOwnershipSchema = z.object({
  id: z.string().uuid().optional(),
  teamId: z.string().uuid().optional(),
  teamName: z.string().nullable().optional(),
  teamSlug: z.string().nullable().optional(),
  role: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  permissions: z.array(z.string()).nullable().optional(),
  isActive: z.boolean().optional(),
  assignedAt: z.string().datetime().optional(),
  endedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectsTransferProjectOwnerTeamInput */
APIProjectsTransferProjectOwnerTeamInputSchema = z.object({
  teamId: z.string().uuid().optional(),
});

/** Zod schema for APIProjectsUpdateProjectAllocationInput */
APIProjectsUpdateProjectAllocationInputSchema = z.object({
  function: z.string().nullable().optional(),
  capacityPercentage: z.number().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for APIProjectsUpdateProjectTeamInput */
APIProjectsUpdateProjectTeamInputSchema = z.object({
  role: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  notes: z.string().nullable().optional(),
  contributionPercentage: z.number().optional(),
});

/** Zod schema for APIProjectWorkAddProjectTaskChecklistInput */
APIProjectWorkAddProjectTaskChecklistInputSchema = z.object({
  text: z.string().nullable().optional(),
});

/** Zod schema for APIProjectWorkAddProjectTaskCommentInput */
APIProjectWorkAddProjectTaskCommentInputSchema = z.object({
  body: z.string().nullable().optional(),
});

/** Zod schema for APIProjectWorkAddProjectTaskDependencyInput */
APIProjectWorkAddProjectTaskDependencyInputSchema = z.object({
  dependsOnTaskId: z.string().uuid().optional(),
});

/** Zod schema for APIProjectWorkConfigureProjectWorkColumnInput */
APIProjectWorkConfigureProjectWorkColumnInputSchema = z.object({
  name: z.string().nullable().optional(),
  kind: z.lazy(() => ProjectWorkProjectWorkColumnKindSchema).optional(),
  position: z.number().int().optional(),
  workInProgressLimit: z.number().int().nullable().optional(),
});

/** Zod schema for APIProjectWorkCreateProjectMilestoneInput */
APIProjectWorkCreateProjectMilestoneInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectWorkCreateProjectTaskLabelInput */
APIProjectWorkCreateProjectTaskLabelInputSchema = z.object({
  name: z.string().nullable().optional(),
  color: z.string().nullable().optional(),
});

/** Zod schema for APIProjectWorkCreateProjectWorkTaskInput */
APIProjectWorkCreateProjectWorkTaskInputSchema = z.object({
  columnId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  priority: z.lazy(() => ProjectWorkProjectWorkTaskPrioritySchema).optional(),
  assigneeUserId: z.string().uuid().nullable().optional(),
  milestoneId: z.string().uuid().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectWorkMoveProjectWorkTaskInput */
APIProjectWorkMoveProjectWorkTaskInputSchema = z.object({
  columnId: z.string().uuid().optional(),
  position: z.number().int().optional(),
});

/** Zod schema for APIProjectWorkProjectBoard */
APIProjectWorkProjectBoardSchema = z.object({
  id: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  columns: z
    .array(z.lazy(() => APIProjectWorkProjectWorkColumnSchema))
    .nullable()
    .optional(),
});

/** Zod schema for APIProjectWorkProjectChecklistItem */
APIProjectWorkProjectChecklistItemSchema = z.object({
  id: z.string().uuid().optional(),
  text: z.string().nullable().optional(),
  isCompleted: z.boolean().optional(),
  position: z.number().int().optional(),
});

/** Zod schema for APIProjectWorkProjectMilestone */
APIProjectWorkProjectMilestoneSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectWorkProjectTaskComment */
APIProjectWorkProjectTaskCommentSchema = z.object({
  id: z.string().uuid().optional(),
  authorUserId: z.string().uuid().optional(),
  body: z.string().nullable().optional(),
  editedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for APIProjectWorkProjectTaskDependency */
APIProjectWorkProjectTaskDependencySchema = z.object({
  id: z.string().uuid().optional(),
  dependsOnTaskId: z.string().uuid().optional(),
});

/** Zod schema for APIProjectWorkProjectTaskLabel */
APIProjectWorkProjectTaskLabelSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  color: z.string().nullable().optional(),
});

/** Zod schema for APIProjectWorkProjectWorkColumn */
APIProjectWorkProjectWorkColumnSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  kind: z.lazy(() => ProjectWorkProjectWorkColumnKindSchema).optional(),
  position: z.number().int().optional(),
  workInProgressLimit: z.number().int().nullable().optional(),
  tasks: z
    .array(z.lazy(() => APIProjectWorkProjectWorkTaskSchema))
    .nullable()
    .optional(),
});

/** Zod schema for APIProjectWorkProjectWorkHistory */
APIProjectWorkProjectWorkHistorySchema = z.object({
  id: z.string().uuid().optional(),
  taskId: z.string().uuid().nullable().optional(),
  actorUserId: z.string().uuid().optional(),
  action: z.string().nullable().optional(),
  changesJson: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for APIProjectWorkProjectWorkTask */
APIProjectWorkProjectWorkTaskSchema = z.object({
  id: z.string().uuid().optional(),
  columnId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  status: z.lazy(() => ProjectWorkProjectWorkTaskStatusSchema).optional(),
  priority: z.lazy(() => ProjectWorkProjectWorkTaskPrioritySchema).optional(),
  assigneeUserId: z.string().uuid().nullable().optional(),
  milestoneId: z.string().uuid().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  position: z.number().int().optional(),
});

/** Zod schema for APIProjectWorkProjectWorkTaskDetails */
APIProjectWorkProjectWorkTaskDetailsSchema = z.object({
  task: z.lazy(() => APIProjectWorkProjectWorkTaskSchema).optional(),
  checklist: z
    .array(z.lazy(() => APIProjectWorkProjectChecklistItemSchema))
    .nullable()
    .optional(),
  comments: z
    .array(z.lazy(() => APIProjectWorkProjectTaskCommentSchema))
    .nullable()
    .optional(),
  dependencies: z
    .array(z.lazy(() => APIProjectWorkProjectTaskDependencySchema))
    .nullable()
    .optional(),
  labels: z
    .array(z.lazy(() => APIProjectWorkProjectTaskLabelSchema))
    .nullable()
    .optional(),
});

/** Zod schema for APIProjectWorkUpdateProjectMilestoneInput */
APIProjectWorkUpdateProjectMilestoneInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIProjectWorkUpdateProjectTaskChecklistInput */
APIProjectWorkUpdateProjectTaskChecklistInputSchema = z.object({
  isCompleted: z.boolean().optional(),
});

/** Zod schema for APIProjectWorkUpdateProjectTaskCommentInput */
APIProjectWorkUpdateProjectTaskCommentInputSchema = z.object({
  body: z.string().nullable().optional(),
});

/** Zod schema for APIProjectWorkUpdateProjectWorkTaskInput */
APIProjectWorkUpdateProjectWorkTaskInputSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  priority: z.lazy(() => ProjectWorkProjectWorkTaskPrioritySchema).optional(),
  assigneeUserId: z.string().uuid().nullable().optional(),
  milestoneId: z.string().uuid().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APISetupEconomyCapabilityReadinessState */
APISetupEconomyCapabilityReadinessStateSchema = z.enum([
  "Disabled",
  "Ready",
  "ProviderNotReady",
  "InvalidConfiguration",
]);

/** Zod schema for APITeamsAcceptTeamInvitationInput */
APITeamsAcceptTeamInvitationInputSchema = z.object({
  token: z.string().nullable().optional(),
});

/** Zod schema for APITeamsAddTeamMemberInput */
APITeamsAddTeamMemberInputSchema = z.object({
  userId: z.string().uuid().optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  professionalTitle: z.string().nullable().optional(),
});

/** Zod schema for APITeamsChangeTeamMemberInput */
APITeamsChangeTeamMemberInputSchema = z.object({
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  professionalTitle: z.string().nullable().optional(),
});

/** Zod schema for APITeamsCreateTeamInput */
APITeamsCreateTeamInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  visibility: z.lazy(() => TeamsTeamVisibilitySchema).optional(),
  description: z.string().nullable().optional(),
  ownerUserId: z.string().uuid().nullable().optional(),
});

/** Zod schema for APITeamsCreateTeamInvitationInput */
APITeamsCreateTeamInvitationInputSchema = z.object({
  userId: z.string().uuid().nullable().optional(),
  email: z.string().nullable().optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  expiresAt: z.string().datetime().optional(),
});

/** Zod schema for APITeamsMyTeamInvitation */
APITeamsMyTeamInvitationSchema = z.object({
  id: z.string().uuid().optional(),
  teamId: z.string().uuid().optional(),
  teamName: z.string().nullable().optional(),
  teamSlug: z.string().nullable().optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  expiresAt: z.string().datetime().optional(),
});

/** Zod schema for APITeamsTeam */
APITeamsTeamSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  visibility: z.lazy(() => TeamsTeamVisibilitySchema).optional(),
  status: z.lazy(() => TeamsTeamStatusSchema).optional(),
  isPersonal: z.boolean().optional(),
  members: z
    .array(z.lazy(() => APITeamsTeamMemberSchema))
    .nullable()
    .optional(),
});

/** Zod schema for APITeamsTeamInvitation */
APITeamsTeamInvitationSchema = z.object({
  id: z.string().uuid().optional(),
  invitedUserId: z.string().uuid().nullable().optional(),
  invitedEmail: z.string().nullable().optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  invitedByUserId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  usedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for APITeamsTeamInvitationCreated */
APITeamsTeamInvitationCreatedSchema = z.object({
  id: z.string().uuid().optional(),
  token: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
});

/** Zod schema for APITeamsTeamMember */
APITeamsTeamMemberSchema = z.object({
  userId: z.string().uuid().optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  professionalTitle: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
});

/** Zod schema for APITeamsTeamProjectSummary */
APITeamsTeamProjectSummarySchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  teamRole: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for APITeamsUpdateTeamInput */
APITeamsUpdateTeamInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  visibility: z.lazy(() => TeamsTeamVisibilitySchema).optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for AssetsAssetAccessPolicy */
AssetsAssetAccessPolicySchema = z.enum([
  "Private",
  "SignedUrl",
  "TenantPublic",
  "Public",
  "PaidContent",
  "OwnerOnly",
  "Authenticated",
  "Unlisted",
  "Inherited",
]);

/** Zod schema for AssetsAssetAccessUrl */
AssetsAssetAccessUrlSchema = z.object({
  url: z.string().nullable().optional(),
  token: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  mimeType: z.string().nullable().optional(),
});

/** Zod schema for AssetsAssetFolderRestrictionMode */
AssetsAssetFolderRestrictionModeSchema = z.enum([
  "None",
  "SelectedTeams",
  "TeamAuthorities",
  "AllocatedProjectMembers",
]);

/** Zod schema for AssetsAssetKind */
AssetsAssetKindSchema = z.enum([
  "Image",
  "Video",
  "Audio",
  "Document",
  "Archive",
  "Other",
]);

/** Zod schema for AssetsAssetUploadResult */
AssetsAssetUploadResultSchema = z.object({
  success: z.boolean().optional(),
  assetReferenceId: z.string().uuid().nullable().optional(),
  assetContentId: z.string().uuid().nullable().optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for AssetsChunkedUploadSession */
AssetsChunkedUploadSessionSchema = z.object({
  uploadId: z.string().nullable().optional(),
  objectKey: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  fileName: z.string().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  totalSize: z.number().int().optional(),
  totalChunks: z.number().int().optional(),
  expiresAt: z.string().datetime().optional(),
  uploadedChunks: z.number().int().optional(),
});

/** Zod schema for AssetsCommandsBulkDeleteAssetItem */
AssetsCommandsBulkDeleteAssetItemSchema = z.object({
  assetReferenceId: z.string().uuid().optional(),
  success: z.boolean().optional(),
  contentMarkedForDeletion: z.boolean().optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for AssetsCommandsBulkDeleteAssetsOutput */
AssetsCommandsBulkDeleteAssetsOutputSchema = z.object({
  totalRequested: z.number().int().optional(),
  successful: z.number().int().optional(),
  failed: z.number().int().optional(),
  items: z
    .array(z.lazy(() => AssetsCommandsBulkDeleteAssetItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AssetsCommandsBulkUploadAssetItem */
AssetsCommandsBulkUploadAssetItemSchema = z.object({
  fileName: z.string().nullable().optional(),
  success: z.boolean().optional(),
  assetReferenceId: z.string().uuid().nullable().optional(),
  assetContentId: z.string().uuid().nullable().optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for AssetsCommandsBulkUploadAssetsOutput */
AssetsCommandsBulkUploadAssetsOutputSchema = z.object({
  totalRequested: z.number().int().optional(),
  successful: z.number().int().optional(),
  failed: z.number().int().optional(),
  items: z
    .array(z.lazy(() => AssetsCommandsBulkUploadAssetItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AssetsControllersAssetExtractedTextOutput */
AssetsControllersAssetExtractedTextOutputSchema = z.object({
  assetId: z.string().uuid().optional(),
  mimeType: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  source: z.string().nullable().optional(),
  text: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  usedOcr: z.boolean().optional(),
  isPartial: z.boolean().optional(),
});

/** Zod schema for AssetsControllersBulkAssetAccessUrlInput */
AssetsControllersBulkAssetAccessUrlInputSchema = z.object({
  assetIds: z.array(z.string().uuid()).nullable().optional(),
  directStorageUrl: z.boolean().optional(),
});

/** Zod schema for AssetsControllersBulkDeleteAssetsInput */
AssetsControllersBulkDeleteAssetsInputSchema = z.object({
  assetIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for AssetsControllersContentModerationInput */
AssetsControllersContentModerationInputSchema = z.object({
  status: z.lazy(() => AssetsModerationStatusSchema).optional(),
  labels: z.array(z.string()).nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for AssetsControllersCopyAssetReferenceInput */
AssetsControllersCopyAssetReferenceInputSchema = z.object({
  displayName: z.string().nullable().optional(),
  folderId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AssetsControllersCreateAssetFolderInput */
AssetsControllersCreateAssetFolderInputSchema = z.object({
  name: z.string().nullable().optional(),
  parentFolderId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AssetsControllersMarkNonDeletableInput */
AssetsControllersMarkNonDeletableInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for AssetsControllersReportAssetInput */
AssetsControllersReportAssetInputSchema = z.object({
  reason: z.lazy(() => AssetsReportReasonSchema).optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for AssetsControllersRestrictAssetFolderInput */
AssetsControllersRestrictAssetFolderInputSchema = z.object({
  mode: z.lazy(() => AssetsAssetFolderRestrictionModeSchema).optional(),
  teamIds: z.array(z.string().uuid()).nullable().optional(),
  authorities: z.array(z.string()).nullable().optional(),
});

/** Zod schema for AssetsControllersReviewReportInput */
AssetsControllersReviewReportInputSchema = z.object({
  decision: z.lazy(() => AssetsReviewDecisionSchema).optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for AssetsControllersUpdateAssetInput */
AssetsControllersUpdateAssetInputSchema = z.object({
  displayName: z.string().nullable().optional(),
  accessPolicy: z.lazy(() => AssetsAssetAccessPolicySchema).optional(),
});

/** Zod schema for AssetsControllersUpdateVirusScanInput */
AssetsControllersUpdateVirusScanInputSchema = z.object({
  status: z.lazy(() => AssetsVirusScanStatusSchema).optional(),
  scanResult: z.string().nullable().optional(),
});

/** Zod schema for AssetsImageFit */
AssetsImageFitSchema = z.enum([
  "Contain",
  "Cover",
  "Fill",
  "Inside",
  "Outside",
]);

/** Zod schema for AssetsImageFormat */
AssetsImageFormatSchema = z.enum([
  "Original",
  "Jpeg",
  "Png",
  "Webp",
  "Avif",
  "Gif",
]);

/** Zod schema for AssetsModerationStatus */
AssetsModerationStatusSchema = z.enum([
  "Pending",
  "Processing",
  "Approved",
  "Rejected",
  "NeedsReview",
  "ApprovedWithWarning",
  "Blocked",
]);

/** Zod schema for AssetsQueriesAssetPreviewOutput */
AssetsQueriesAssetPreviewOutputSchema = z.object({
  assetReferenceId: z.string().uuid().optional(),
  assetContentId: z.string().uuid().optional(),
  displayName: z.string().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  kind: z.lazy(() => AssetsAssetKindSchema).optional(),
  previewMode: z.string().nullable().optional(),
  canInlinePreview: z.boolean().optional(),
  isBlocked: z.boolean().optional(),
  contentUrl: z.string().nullable().optional(),
  thumbnailUrl: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  extractedTextPreview: z.string().nullable().optional(),
  usedOcr: z.boolean().optional(),
  isTextTruncated: z.boolean().optional(),
  warnings: z.array(z.string()).nullable().optional(),
});

/** Zod schema for AssetsQueriesAssetRetentionCandidateOutput */
AssetsQueriesAssetRetentionCandidateOutputSchema = z.object({
  assetContentId: z.string().uuid().optional(),
  bucketName: z.string().nullable().optional(),
  objectKey: z.string().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  sizeBytes: z.number().int().optional(),
  markedForDeletionAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for AssetsQueriesAssetRetentionReportOutput */
AssetsQueriesAssetRetentionReportOutputSchema = z.object({
  gracePeriodHours: z.number().int().optional(),
  limit: z.number().int().optional(),
  candidates: z.number().int().optional(),
  onLegalHold: z.number().int().optional(),
  markedForDeletion: z.number().int().optional(),
  candidateBytes: z.number().int().optional(),
  items: z
    .array(z.lazy(() => AssetsQueriesAssetRetentionCandidateOutputSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AssetsQueriesAssetSearchOutput */
AssetsQueriesAssetSearchOutputSchema = z.object({
  totalMatched: z.number().int().optional(),
  returned: z.number().int().optional(),
  items: z
    .array(z.lazy(() => AssetsQueriesAssetSearchResultSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AssetsQueriesAssetSearchResult */
AssetsQueriesAssetSearchResultSchema = z.object({
  assetReferenceId: z.string().uuid().optional(),
  assetContentId: z.string().uuid().optional(),
  displayName: z.string().nullable().optional(),
  originalFilename: z.string().nullable().optional(),
  parentResourceType: z.string().nullable().optional(),
  parentResourceId: z.string().uuid().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  kind: z.lazy(() => AssetsAssetKindSchema).optional(),
  sizeBytes: z.number().int().optional(),
  accessCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for AssetsQueriesAssetStatisticsOutput */
AssetsQueriesAssetStatisticsOutputSchema = z.object({
  totalAssets: z.number().int().optional(),
  totalContentObjects: z.number().int().optional(),
  totalBytes: z.number().int().optional(),
  documentAssets: z.number().int().optional(),
  imageAssets: z.number().int().optional(),
  videoAssets: z.number().int().optional(),
  totalAccesses: z.number().int().optional(),
  pendingVirusScans: z.number().int().optional(),
  pendingModeration: z.number().int().optional(),
  blockedOrRejected: z.number().int().optional(),
  legalHoldContent: z.number().int().optional(),
  retentionCandidates: z.number().int().optional(),
});

/** Zod schema for AssetsQueriesBulkAssetAccessUrlItem */
AssetsQueriesBulkAssetAccessUrlItemSchema = z.object({
  assetReferenceId: z.string().uuid().optional(),
  success: z.boolean().optional(),
  url: z.string().nullable().optional(),
  token: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  error: z.string().nullable().optional(),
});

/** Zod schema for AssetsQueriesBulkAssetAccessUrlsOutput */
AssetsQueriesBulkAssetAccessUrlsOutputSchema = z.object({
  totalRequested: z.number().int().optional(),
  successful: z.number().int().optional(),
  failed: z.number().int().optional(),
  items: z
    .array(z.lazy(() => AssetsQueriesBulkAssetAccessUrlItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AssetsReportReason */
AssetsReportReasonSchema = z.enum([
  "Inappropriate",
  "Copyright",
  "Spam",
  "Violence",
  "Harassment",
  "Misinformation",
  "Other",
]);

/** Zod schema for AssetsReviewDecision */
AssetsReviewDecisionSchema = z.enum([
  "NoAction",
  "ContentRemoved",
  "ContentHidden",
  "UserWarned",
  "UserSuspended",
  "BlockContent",
]);

/** Zod schema for AssetsSecurityAccessUrlInput */
AssetsSecurityAccessUrlInputSchema = z.object({
  transform: z.string().nullable().optional(),
  directStorage: z.boolean().optional(),
});

/** Zod schema for AssetsVirusScanStatus */
AssetsVirusScanStatusSchema = z.enum([
  "Pending",
  "Scanning",
  "Clean",
  "Infected",
  "ScanFailed",
]);

/** Zod schema for BillingCycle */
BillingCycleSchema = z.enum([
  "Weekly",
  "Monthly",
  "Quarterly",
  "SemiAnnually",
  "Annually",
  "Biannually",
]);

/** Zod schema for BulkOperationError */
BulkOperationErrorSchema = z.object({
  tenantId: z.string().uuid().optional(),
  tenantName: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  errorCode: z.string().nullable().optional(),
});

/** Zod schema for BulkOperationOutput */
BulkOperationOutputSchema = z.object({
  totalRequested: z.number().int().optional(),
  successfulOperations: z.number().int().optional(),
  failedOperations: z.number().int().optional(),
  errors: z
    .array(z.lazy(() => BulkOperationErrorSchema))
    .nullable()
    .optional(),
  isComplete: z.boolean().optional(),
  successRate: z.number().optional(),
});

/** Zod schema for CommerceBillingInvoicePaymentRetryResult */
CommerceBillingInvoicePaymentRetryResultSchema = z.object({
  invoiceId: z.string().uuid().optional(),
  invoiceNumber: z.string().nullable().optional(),
  invoiceStatus: z.lazy(() => CommerceBillingInvoiceStatusSchema).optional(),
  accepted: z.boolean().optional(),
  code: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  retryScheduledAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceBillingInvoiceStatus */
CommerceBillingInvoiceStatusSchema = z.enum([
  "Draft",
  "Open",
  "Paid",
  "Void",
  "PastDue",
  "Uncollectible",
]);

/** Zod schema for CommerceOrderChargeState */
CommerceOrderChargeStateSchema = z.enum([
  "Succeeded",
  "Failed",
  "Processing",
  "RequiresAction",
  "RequiresReconciliation",
]);

/** Zod schema for CommerceOrdersAddOrderItemInput */
CommerceOrdersAddOrderItemInputSchema = z.object({
  productId: z.string().uuid().optional(),
  productPricingId: z.string().uuid().optional(),
  productPricingVersionId: z.string().uuid().optional(),
  quantity: z.number().int().optional(),
  promoCode: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersCaptureOrderInput */
CommerceOrdersCaptureOrderInputSchema = z.object({
  paymentMethodId: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersCompleteOrderInput */
CommerceOrdersCompleteOrderInputSchema = z.object({
  paymentId: z.string().uuid().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersCreateOrderInput */
CommerceOrdersCreateOrderInputSchema = z.object({
  idempotencyKey: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersOrder */
CommerceOrdersOrderSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  idempotencyKey: z.string().nullable().optional(),
  status: z.lazy(() => CommerceOrdersOrderStatusSchema).optional(),
  subtotal: z.number().optional(),
  discountTotal: z.number().optional(),
  taxAmount: z.number().optional(),
  total: z.number().optional(),
  currency: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
  paidAt: z.string().datetime().nullable().optional(),
  refundedAt: z.string().datetime().nullable().optional(),
  refundAmount: z.number().nullable().optional(),
  refundReason: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
  lineItems: z
    .array(z.lazy(() => CommerceOrdersOrderLineItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommerceOrdersOrderCapture */
CommerceOrdersOrderCaptureSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  idempotencyKey: z.string().nullable().optional(),
  status: z.lazy(() => CommerceOrdersOrderStatusSchema).optional(),
  subtotal: z.number().optional(),
  discountTotal: z.number().optional(),
  taxAmount: z.number().optional(),
  total: z.number().optional(),
  currency: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
  paidAt: z.string().datetime().nullable().optional(),
  refundedAt: z.string().datetime().nullable().optional(),
  refundAmount: z.number().nullable().optional(),
  refundReason: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
  lineItems: z
    .array(z.lazy(() => CommerceOrdersOrderLineItemSchema))
    .nullable()
    .optional(),
  paymentState: z.lazy(() => CommerceOrderChargeStateSchema).optional(),
  paymentId: z.string().uuid().nullable().optional(),
  clientActionToken: z.string().nullable().optional(),
  paymentMessage: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersOrderLineItem */
CommerceOrdersOrderLineItemSchema = z.object({
  id: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  productPricingId: z.string().uuid().optional(),
  productPricingVersionId: z.string().uuid().optional(),
  priceVersion: z.number().int().optional(),
  productName: z.string().nullable().optional(),
  unitPrice: z.number().optional(),
  basePrice: z.number().optional(),
  salePrice: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  quantity: z.number().int().optional(),
  discountAmount: z.number().optional(),
  promoCodesApplied: z.string().nullable().optional(),
  lineTotal: z.number().optional(),
  isSubscription: z.boolean().optional(),
});

/** Zod schema for CommerceOrdersOrderStatus */
CommerceOrdersOrderStatusSchema = z.enum([
  "Pending",
  "Processing",
  "Completed",
  "Failed",
  "Cancelled",
  "Refunded",
  "PartiallyRefunded",
  "Disputed",
  "Paid",
  "Fulfilled",
  "OnHold",
]);

/** Zod schema for CommercePaymentsBillingChargesControllerCancelBillingChargeInput */
CommercePaymentsBillingChargesControllerCancelBillingChargeInputSchema =
  z.object({
    cancellationReason: z.string().nullable().optional(),
    canceledBy: z.string().uuid().nullable().optional(),
  });

/** Zod schema for CommercePaymentsBillingChargesControllerCreateBillingChargeInput */
CommercePaymentsBillingChargesControllerCreateBillingChargeInputSchema =
  z.object({
    tenantId: z.string().uuid().optional(),
    subscriptionId: z.string().uuid().optional(),
    amount: z.number().optional(),
    paymentMethodId: z.string().nullable().optional(),
  });

/** Zod schema for CommercePaymentsBillingChargesControllerRefundBillingChargeInput */
CommercePaymentsBillingChargesControllerRefundBillingChargeInputSchema =
  z.object({
    amount: z.number().nullable().optional(),
    reason: z.string().nullable().optional(),
  });

/** Zod schema for CommercePaymentsCalculateTaxInput */
CommercePaymentsCalculateTaxInputSchema = z.object({
  jurisdictionCode: z.string().nullable(),
  amount: z.number(),
  currency: z.string().nullable(),
  customerType: z.string().nullable(),
  productCategory: z.string().nullable().optional(),
  customerVatNumber: z.string().nullable().optional(),
  isTaxInclusive: z.boolean().optional(),
  transactionDate: z.string().datetime().nullable().optional(),
  applicableExemptions: z.array(z.string()).nullable().optional(),
});

/** Zod schema for CommercePaymentsCreateTaxJurisdictionInput */
CommercePaymentsCreateTaxJurisdictionInputSchema = z.object({
  code: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
  defaultRate: z.number().optional(),
});

/** Zod schema for CommercePaymentsCreateTaxRuleInput */
CommercePaymentsCreateTaxRuleInputSchema = z.object({
  jurisdictionCode: z.string().nullable().optional(),
  productCategory: z.string().nullable().optional(),
  customerType: z.string().nullable().optional(),
  rate: z.number().optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsCreateWalletInput */
CommercePaymentsCreateWalletInputSchema = z.object({
  currency: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsCustomerType */
CommercePaymentsCustomerTypeSchema = z.enum(["B2C", "B2B"]);

/** Zod schema for CommercePaymentsLockWalletInput */
CommercePaymentsLockWalletInputSchema = z.object({
  reason: z.string().nullable(),
});

/** Zod schema for CommercePaymentsModelsFreezeWalletInput */
CommercePaymentsModelsFreezeWalletInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsModelsPatchWalletInput */
CommercePaymentsModelsPatchWalletInputSchema = z.object({
  currency: z.string().nullable().optional(),
  dailyLimit: z.number().nullable().optional(),
  monthlyLimit: z.number().nullable().optional(),
});

/** Zod schema for CommercePaymentsPatchTaxJurisdictionInput */
CommercePaymentsPatchTaxJurisdictionInputSchema = z.object({
  name: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
  defaultRate: z.number().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
});

/** Zod schema for CommercePaymentsPatchTaxRuleInput */
CommercePaymentsPatchTaxRuleInputSchema = z.object({
  rate: z.number().nullable().optional(),
  effectiveFrom: z.string().datetime().nullable().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentCancellationResult */
CommercePaymentsPaymentCancellationResultSchema = z.object({
  paymentId: z.string().uuid(),
  cancellationReason: z.string().nullable(),
  canceledAt: z.string().datetime(),
  canceledBy: z.string().uuid().nullable().optional(),
  success: z.boolean(),
  errorMessage: z.string().nullable().optional(),
  refundProcessed: z.boolean().optional(),
  refundAmount: z.number().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentResult */
CommercePaymentsPaymentResultSchema = z.object({
  tenantId: z.string().uuid().optional(),
  success: z.boolean().optional(),
  transactionId: z.string().nullable().optional(),
  paymentId: z.string().nullable().optional(),
  amount: z.lazy(() => MoneySchema).optional(),
  processedAt: z.string().datetime().nullable().optional(),
  failureReason: z.string().nullable().optional(),
  paymentMethodId: z.string().nullable().optional(),
  status: z.lazy(() => CommercePaymentsPaymentStatusSchema).optional(),
  invoiceId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentRetryResult */
CommercePaymentsPaymentRetryResultSchema = z.object({
  success: z.boolean().optional(),
  retryAttempt: z.number().int().optional(),
  nextRetryAt: z.string().datetime().nullable().optional(),
  paymentResult: z.lazy(() => CommercePaymentsPaymentResultSchema).optional(),
  maxRetriesReached: z.boolean().optional(),
  failureReason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCancelPaymentInput */
CommercePaymentsPaymentsControllerCancelPaymentInputSchema = z.object({
  cancellationReason: z.string().nullable().optional(),
  canceledBy: z.string().uuid().nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput */
CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInputSchema =
  z.object({
    tenantId: z.string().uuid().optional(),
    subscriptionId: z.string().uuid().optional(),
    paymentMethodId: z.string().nullable().optional(),
  });

/** Zod schema for CommercePaymentsPaymentsControllerCreateSetupIntentInput */
CommercePaymentsPaymentsControllerCreateSetupIntentInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  subscriptionId: z.string().uuid().optional(),
  customerEmail: z.string().nullable().optional(),
  customerName: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCreateSetupIntentOutput */
CommercePaymentsPaymentsControllerCreateSetupIntentOutputSchema = z.object({
  subscriptionId: z.string().uuid().optional(),
  customerId: z.string().nullable().optional(),
  setupIntentId: z.string().nullable().optional(),
  clientSecret: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerProcessPaymentInput */
CommercePaymentsPaymentsControllerProcessPaymentInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  subscriptionId: z.string().uuid().optional(),
  amount: z.number().optional(),
  paymentMethodId: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerRefundInput */
CommercePaymentsPaymentsControllerRefundInputSchema = z.object({
  amount: z.number().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentStatus */
CommercePaymentsPaymentStatusSchema = z.enum([
  "Pending",
  "Processing",
  "Succeeded",
  "Failed",
  "Cancelled",
  "RequiresAction",
  "Refunded",
  "Disputed",
]);

/** Zod schema for CommercePaymentsProcessRefundResult */
CommercePaymentsProcessRefundResultSchema = z.object({
  refundId: z.string().uuid(),
  paymentId: z.string().uuid(),
  refundedAmount: z.number(),
  currency: z.string().nullable(),
  status: z.lazy(() => CommercePaymentsTransactionStatusSchema),
  reason: z.string().nullable(),
  processedAt: z.string().datetime(),
  referenceNumber: z.string().nullable().optional(),
  estimatedCompletionDate: z.string().datetime().nullable().optional(),
  processingFee: z.number().optional(),
  isSuccess: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  isSuccessful: z.boolean().optional(),
});

/** Zod schema for CommercePaymentsTaxBreakdown */
CommercePaymentsTaxBreakdownSchema = z.object({
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  description: z.string().nullable().optional(),
  rate: z.number().optional(),
  taxableAmount: z.number().optional(),
  taxAmount: z.number().optional(),
  jurisdictionCode: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxCalculationResult */
CommercePaymentsTaxCalculationResultSchema = z.object({
  subtotalAmount: z.number().optional(),
  taxAmount: z.number().optional(),
  totalAmount: z.number().optional(),
  effectiveTaxRate: z.number().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  jurisdictionName: z.string().nullable().optional(),
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  taxDescription: z.string().nullable().optional(),
  isTaxExempt: z.boolean().optional(),
  isReverseCharge: z.boolean().optional(),
  taxBreakdowns: z
    .array(z.lazy(() => CommercePaymentsTaxBreakdownSchema))
    .nullable()
    .optional(),
  exemptionReason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxExemptionValidationResult */
CommercePaymentsTaxExemptionValidationResultSchema = z.object({
  isValid: z.boolean().optional(),
  exemptionType: z.string().nullable().optional(),
  exemptionRate: z.number().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validTo: z.string().datetime().nullable().optional(),
  validationMessage: z.string().nullable().optional(),
  warnings: z.array(z.string()).nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdiction */
CommercePaymentsTaxJurisdictionSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  code: z.string().min(1).max(20),
  name: z.string().min(1).max(200),
  type: z.lazy(() => CommercePaymentsTaxJurisdictionTypeSchema).optional(),
  parentJurisdictionId: z.string().uuid().nullable().optional(),
  parentJurisdiction: z
    .lazy(() => CommercePaymentsTaxJurisdictionSchema)
    .optional(),
  childJurisdictions: z
    .array(z.lazy(() => CommercePaymentsTaxJurisdictionSchema))
    .nullable()
    .optional(),
  isActive: z.boolean().optional(),
  taxRegistrationNumber: z.string().max(100).nullable().optional(),
  isReverseChargeApplicable: z.boolean().optional(),
  taxRules: z
    .array(z.lazy(() => CommercePaymentsTaxRuleSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdictionDto */
CommercePaymentsTaxJurisdictionDtoSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
  defaultRate: z.number().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdictionType */
CommercePaymentsTaxJurisdictionTypeSchema = z.enum([
  "Country",
  "State",
  "Province",
  "Region",
  "City",
  "County",
  "District",
]);

/** Zod schema for CommercePaymentsTaxRate */
CommercePaymentsTaxRateSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  taxJurisdictionId: z.string().uuid(),
  taxJurisdiction: z
    .lazy(() => CommercePaymentsTaxJurisdictionSchema)
    .optional(),
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  rate: z.number().optional(),
  productCategory: z.string().max(100).nullable().optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  minimumTaxableAmount: z.number().nullable().optional(),
  maximumTaxableAmount: z.number().nullable().optional(),
  description: z.string().max(500).nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxRule */
CommercePaymentsTaxRuleSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(200),
  description: z.string().max(1000).nullable().optional(),
  taxJurisdictionId: z.string().uuid(),
  taxJurisdiction: z
    .lazy(() => CommercePaymentsTaxJurisdictionSchema)
    .optional(),
  ruleType: z.lazy(() => CommercePaymentsTaxRuleTypeSchema).optional(),
  priority: z.number().int().optional(),
  isActive: z.boolean().optional(),
  effectiveFrom: z.string().datetime().nullable().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  customerTypeFilter: z
    .lazy(() => CommercePaymentsCustomerTypeSchema)
    .optional(),
  productCategories: z.string().max(2000).nullable().optional(),
  minimumAmount: z.number().nullable().optional(),
  maximumAmount: z.number().nullable().optional(),
  isTaxInclusive: z.boolean().optional(),
  isReverseCharge: z.boolean().optional(),
  exemptionConditions: z.string().max(2000).nullable().optional(),
  defaultTaxRateId: z.string().uuid().nullable().optional(),
  defaultTaxRate: z.lazy(() => CommercePaymentsTaxRateSchema).optional(),
});

/** Zod schema for CommercePaymentsTaxRuleDto */
CommercePaymentsTaxRuleDtoSchema = z.object({
  id: z.string().uuid().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  productCategory: z.string().nullable().optional(),
  customerType: z.string().nullable().optional(),
  rate: z.number().optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for CommercePaymentsTaxRuleType */
CommercePaymentsTaxRuleTypeSchema = z.enum([
  "Standard",
  "Reduced",
  "ZeroRated",
  "Exempt",
  "ReverseCharge",
  "WithholdingTax",
  "Compound",
  "Custom",
]);

/** Zod schema for CommercePaymentsTaxType */
CommercePaymentsTaxTypeSchema = z.enum([
  "VAT",
  "GST",
  "SalesTax",
  "ServiceTax",
  "WithholdingTax",
  "ExciseTax",
  "CustomsDuty",
  "Other",
]);

/** Zod schema for CommercePaymentsTransactionStatus */
CommercePaymentsTransactionStatusSchema = z.enum([
  "Pending",
  "Processing",
  "Completed",
  "Failed",
  "Cancelled",
  "Reversed",
]);

/** Zod schema for CommercePaymentsUserWallet */
CommercePaymentsUserWalletSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid(),
  balance: z.number().optional(),
  currency: z.string().min(1).max(3),
  isActive: z.boolean().optional(),
  isLocked: z.boolean().optional(),
  lockReason: z.string().max(500).nullable().optional(),
  lastTransactionAt: z.string().datetime().nullable().optional(),
  dailyLimit: z.number().nullable().optional(),
  monthlyLimit: z.number().nullable().optional(),
  transactions: z
    .array(z.lazy(() => CommercePaymentsWalletTransactionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommercePaymentsValidateTaxExemptionInput */
CommercePaymentsValidateTaxExemptionInputSchema = z.object({
  jurisdictionCode: z.string().nullable().optional(),
  exemptionType: z.string().nullable().optional(),
  exemptionCertificateNumber: z.string().nullable().optional(),
  customerVatNumber: z.string().nullable().optional(),
  customerId: z.string().uuid().nullable().optional(),
  transactionDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommercePaymentsWalletTransaction */
CommercePaymentsWalletTransactionSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  walletId: z.string().uuid(),
  wallet: z.lazy(() => CommercePaymentsUserWalletSchema).optional(),
  type: z.lazy(() => CommercePaymentsWalletTransactionTypeSchema).optional(),
  amount: z.number().optional(),
  balanceAfter: z.number().optional(),
  description: z.string().min(1).max(500),
  referenceId: z.string().max(200).nullable().optional(),
  status: z.lazy(() => CommercePaymentsTransactionStatusSchema).optional(),
  metadata: z.string().max(2000).nullable().optional(),
  notes: z.string().max(1000).nullable().optional(),
  processedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommercePaymentsWalletTransactionType */
CommercePaymentsWalletTransactionTypeSchema = z.enum([
  "Credit",
  "Debit",
  "TransferIn",
  "TransferOut",
  "Refund",
  "Fee",
  "Adjustment",
]);

/** Zod schema for CommerceProductsAddMySupportTicketMessageInput */
CommerceProductsAddMySupportTicketMessageInputSchema = z.object({
  body: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsAddSupportTicketMessageInput */
CommerceProductsAddSupportTicketMessageInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  authorUserId: z.string().uuid().optional(),
  authorName: z.string().nullable().optional(),
  authorEmail: z.string().nullable().optional(),
  authorType: z
    .lazy(() => CommerceProductsSupportTicketMessageAuthorTypeSchema)
    .optional(),
  body: z.string().nullable().optional(),
  isInternal: z.boolean().optional(),
});

/** Zod schema for CommerceProductsAppliedPromoCode */
CommerceProductsAppliedPromoCodeSchema = z.object({
  code: z.string().nullable().optional(),
  discountAmount: z.number().optional(),
  discountPercentage: z.number().nullable().optional(),
});

/** Zod schema for CommerceProductsApplyPromoCodesInput */
CommerceProductsApplyPromoCodesInputSchema = z.object({
  orderAmount: z.number().optional(),
  promoCodes: z.array(z.string()).nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsAssignSupportTicketInput */
CommerceProductsAssignSupportTicketInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  agentUserId: z.string().uuid().optional(),
  agentName: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsBatchCreateProductsInput */
CommerceProductsBatchCreateProductsInputSchema = z.object({
  products: z
    .array(z.lazy(() => CommerceProductsBatchProductCreateItemSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsBatchProductCreateItem */
CommerceProductsBatchProductCreateItemSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  isBundle: z.boolean().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  referralCommissionPercentage: z.number().optional(),
  maxAffiliateDiscount: z.number().optional(),
  affiliateCommissionPercentage: z.number().optional(),
});

/** Zod schema for CommerceProductsCheckMultipleAccessInput */
CommerceProductsCheckMultipleAccessInputSchema = z.object({
  productIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for CommerceProductsCloseSupportTicketInput */
CommerceProductsCloseSupportTicketInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  agentUserId: z.string().uuid().optional(),
  agentName: z.string().nullable().optional(),
  closingNotes: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsCreateMySupportTicketInput */
CommerceProductsCreateMySupportTicketInputSchema = z.object({
  subject: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  priority: z
    .lazy(() => CommerceProductsSupportTicketPrioritySchema)
    .optional(),
  category: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsCreateProductInput */
CommerceProductsCreateProductInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  isBundle: z.boolean().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  referralCommissionPercentage: z.number().optional(),
  maxAffiliateDiscount: z.number().optional(),
  affiliateCommissionPercentage: z.number().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsCreatePromoCodeInput */
CommerceProductsCreatePromoCodeInputSchema = z.object({
  code: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  discountPercentage: z.number().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  isExclusive: z.boolean().optional(),
  stackingPriority: z.number().int().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsCreateSupportTicketInput */
CommerceProductsCreateSupportTicketInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  customerId: z.string().uuid().optional(),
  customerName: z.string().nullable().optional(),
  reporterUserId: z.string().uuid().optional(),
  reporterName: z.string().nullable().optional(),
  reporterEmail: z.string().nullable().optional(),
  subject: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  priority: z
    .lazy(() => CommerceProductsSupportTicketPrioritySchema)
    .optional(),
  category: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsEntitlementCheckResult */
CommerceProductsEntitlementCheckResultSchema = z.object({
  productId: z.string().uuid().optional(),
  hasAccess: z.boolean().optional(),
});

/** Zod schema for CommerceProductsEntitlementInfo */
CommerceProductsEntitlementInfoSchema = z.object({
  productId: z.string().uuid().optional(),
  productName: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  acquisitionType: z.string().nullable().optional(),
  accessStartDate: z.string().datetime().nullable().optional(),
  accessEndDate: z.string().datetime().nullable().optional(),
  isSubscription: z.boolean().optional(),
  subscriptionStatus: z.string().nullable().optional(),
  pricePaid: z.number().optional(),
  currency: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsGrantEntitlementInput */
CommerceProductsGrantEntitlementInputSchema = z.object({
  userId: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  acquisitionType: z
    .lazy(() => CommerceProductsProductAcquisitionTypeSchema)
    .optional(),
  pricePaid: z.number().optional(),
  currency: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsPatchProductInput */
CommerceProductsPatchProductInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  isBundle: z.boolean().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  referralCommissionPercentage: z.number().nullable().optional(),
  maxAffiliateDiscount: z.number().nullable().optional(),
  affiliateCommissionPercentage: z.number().nullable().optional(),
  expectedVersion: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceProductsPatchPromoCodeInput */
CommerceProductsPatchPromoCodeInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  discountPercentage: z.number().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  isExclusive: z.boolean().nullable().optional(),
  stackingPriority: z.number().int().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsProduct */
CommerceProductsProductSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  isBundle: z.boolean().optional(),
  isPublished: z.boolean().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  referralCommissionPercentage: z.number().optional(),
  maxAffiliateDiscount: z.number().optional(),
  affiliateCommissionPercentage: z.number().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
  pricing: z
    .array(z.lazy(() => CommerceProductsProductPricingSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommerceProductsProductAcquisitionType */
CommerceProductsProductAcquisitionTypeSchema = z.enum([
  "Purchase",
  "Subscription",
  "Grant",
  "PromoCode",
  "Bundle",
  "Trial",
  "Referral",
  "Free",
  "Gift",
]);

/** Zod schema for CommerceProductsProductPricing */
CommerceProductsProductPricingSchema = z.object({
  id: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  basePrice: z.number().optional(),
  salePrice: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  saleStartDate: z.string().datetime().nullable().optional(),
  saleEndDate: z.string().datetime().nullable().optional(),
  isDefault: z.boolean().optional(),
  currentPrice: z.number().optional(),
  isSaleActive: z.boolean().optional(),
});

/** Zod schema for CommerceProductsProductType */
CommerceProductsProductTypeSchema = z.enum([
  "Program",
  "Course",
  "Bundle",
  "Subscription",
  "Workshop",
  "Mentorship",
  "Ebook",
  "ResourcePack",
  "Community",
  "Certification",
  "Physical",
  "Service",
  "LearningPathway",
  "Other",
]);

/** Zod schema for CommerceProductsPromoCode */
CommerceProductsPromoCodeSchema = z.object({
  id: z.string().uuid().optional(),
  code: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  discountPercentage: z.number().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  isExclusive: z.boolean().optional(),
  stackingPriority: z.number().int().optional(),
  productId: z.string().uuid().nullable().optional(),
  usageCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for CommerceProductsPromoCodeApplicationResult */
CommerceProductsPromoCodeApplicationResultSchema = z.object({
  originalAmount: z.number().optional(),
  finalAmount: z.number().optional(),
  totalDiscount: z.number().optional(),
  appliedCodes: z
    .array(z.lazy(() => CommerceProductsAppliedPromoCodeSchema))
    .nullable()
    .optional(),
  rejectedCodes: z
    .array(z.lazy(() => CommerceProductsRejectedPromoCodeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommerceProductsPromoCodeType */
CommerceProductsPromoCodeTypeSchema = z.enum([
  "PercentageOff",
  "FixedAmountOff",
  "FreeTrial",
  "BuyOneGetOne",
  "FreeShipping",
]);

/** Zod schema for CommerceProductsPromoCodeUsage */
CommerceProductsPromoCodeUsageSchema = z.object({
  promoCodeId: z.string().uuid().optional(),
  code: z.string().nullable().optional(),
  totalUses: z.number().int().optional(),
  uniqueUsers: z.number().int().optional(),
  totalDiscountGiven: z.number().optional(),
  averageDiscountPerUse: z.number().optional(),
  maxUses: z.number().int().nullable().optional(),
  remainingUses: z.number().int().nullable().optional(),
  firstUsedAt: z.string().datetime().nullable().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsPromoCodeValidationResult */
CommerceProductsPromoCodeValidationResultSchema = z.object({
  isValid: z.boolean().optional(),
  code: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  discountAmount: z.number().optional(),
  discountPercentage: z.number().nullable().optional(),
});

/** Zod schema for CommerceProductsRejectedPromoCode */
CommerceProductsRejectedPromoCodeSchema = z.object({
  code: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsResolveSupportTicketInput */
CommerceProductsResolveSupportTicketInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  agentUserId: z.string().uuid().optional(),
  agentName: z.string().nullable().optional(),
  resolutionSummary: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsRevokeEntitlementInput */
CommerceProductsRevokeEntitlementInputSchema = z.object({
  userId: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsSupportTicket */
CommerceProductsSupportTicketSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  customerId: z.string().uuid().optional(),
  customerName: z.string().nullable().optional(),
  reporterUserId: z.string().uuid().optional(),
  reporterName: z.string().nullable().optional(),
  reporterEmail: z.string().nullable().optional(),
  subject: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  status: z.lazy(() => CommerceProductsSupportTicketStatusSchema).optional(),
  priority: z
    .lazy(() => CommerceProductsSupportTicketPrioritySchema)
    .optional(),
  assignedToUserId: z.string().uuid().nullable().optional(),
  assignedToName: z.string().nullable().optional(),
  openedAt: z.string().datetime().optional(),
  firstResponseAt: z.string().datetime().nullable().optional(),
  responseDueBy: z.string().datetime().nullable().optional(),
  resolvedAt: z.string().datetime().nullable().optional(),
  closedAt: z.string().datetime().nullable().optional(),
  resolutionSummary: z.string().nullable().optional(),
  lastMessageAt: z.string().datetime().nullable().optional(),
  lastMessagePreview: z.string().nullable().optional(),
  messageCount: z.number().int().optional(),
  messages: z
    .array(z.lazy(() => CommerceProductsSupportTicketMessageSchema))
    .nullable()
    .optional(),
});

/** Zod schema for CommerceProductsSupportTicketMessage */
CommerceProductsSupportTicketMessageSchema = z.object({
  id: z.string().uuid().optional(),
  ticketId: z.string().uuid().optional(),
  authorUserId: z.string().uuid().optional(),
  authorName: z.string().nullable().optional(),
  authorEmail: z.string().nullable().optional(),
  authorType: z
    .lazy(() => CommerceProductsSupportTicketMessageAuthorTypeSchema)
    .optional(),
  body: z.string().nullable().optional(),
  isInternal: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for CommerceProductsSupportTicketMessageAuthorType */
CommerceProductsSupportTicketMessageAuthorTypeSchema = z.enum([
  "Customer",
  "Agent",
  "System",
]);

/** Zod schema for CommerceProductsSupportTicketPriority */
CommerceProductsSupportTicketPrioritySchema = z.enum([
  "Low",
  "Normal",
  "High",
  "Urgent",
]);

/** Zod schema for CommerceProductsSupportTicketStatus */
CommerceProductsSupportTicketStatusSchema = z.enum([
  "Open",
  "InProgress",
  "Resolved",
  "Closed",
  "Cancelled",
]);

/** Zod schema for CommerceProductsUpdateProductInput */
CommerceProductsUpdateProductInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  isBundle: z.boolean().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  referralCommissionPercentage: z.number().nullable().optional(),
  maxAffiliateDiscount: z.number().nullable().optional(),
  affiliateCommissionPercentage: z.number().nullable().optional(),
  expectedVersion: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceProductsUpdatePromoCodeInput */
CommerceProductsUpdatePromoCodeInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  discountPercentage: z.number().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  isExclusive: z.boolean().nullable().optional(),
  stackingPriority: z.number().int().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsValidatePromoCodeInput */
CommerceProductsValidatePromoCodeInputSchema = z.object({
  code: z.string().nullable().optional(),
  orderAmount: z.number().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsBillingHistory */
CommerceSubscriptionsBillingHistorySchema = z.object({
  id: z.string().uuid().optional(),
  subscriptionId: z.string().uuid().optional(),
  billingDate: z.string().datetime().optional(),
  amount: z.number().optional(),
  currency: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  externalPaymentId: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput */
CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInputSchema =
  z.object({
    reason: z
      .lazy(() => CommerceSubscriptionsCancellationReasonSchema)
      .optional(),
    note: z.string().nullable().optional(),
    effectiveDate: z.string().datetime().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput */
CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInputSchema =
  z.object({
    tenantId: z.string().uuid().optional(),
    planId: z.string().uuid().optional(),
    createdByUserId: z.string().uuid().optional(),
    billingCycle: z.lazy(() => BillingCycleSchema).optional(),
    amount: z.number().optional(),
    fulfilledOrderId: z.string().uuid().nullable().optional(),
    startDate: z.string().datetime().nullable().optional(),
    trialDays: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsCancellationReason */
CommerceSubscriptionsCancellationReasonSchema = z.enum([
  "UserRequested",
  "PaymentFailed",
  "PlanDiscontinued",
  "PolicyViolation",
  "Downgrade",
  "TrialEnded",
  "Custom",
  "ExternalRequest",
]);

/** Zod schema for CommerceSubscriptionsClientModulesOutput */
CommerceSubscriptionsClientModulesOutputSchema = z.object({
  clientId: z.string().uuid().optional(),
  subscriptions: z
    .lazy(() => PagedResultOfCommerceSubscriptionsSubscriptionSchema)
    .optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsCreateClientInput */
CommerceSubscriptionsCreateClientInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  adminEmail: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  cnpj: z.string().nullable().optional(),
  taxId: z.string().nullable().optional(),
  fiscalData: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for CommerceSubscriptionsSubscription */
CommerceSubscriptionsSubscriptionSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  createdByUserId: z.string().uuid(),
  fulfilledOrderId: z.string().uuid().nullable().optional(),
  lastModifyingOrderId: z.string().uuid().nullable().optional(),
  lastRenewalIdempotencyKey: z.string().max(100).nullable().optional(),
  lastPaymentIdempotencyKey: z.string().max(100).nullable().optional(),
  lockedPriceVersionId: z.string().uuid().nullable().optional(),
  lastProcessedBillingCycle: z.number().int().optional(),
  trialEndDate: z.string().datetime().nullable().optional(),
  cancellationReason: z
    .lazy(() => CommerceSubscriptionsCancellationReasonSchema)
    .optional(),
  cancellationNote: z.string().max(1000).nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  externalId: z.string().max(100).nullable().optional(),
  externalCustomerId: z.string().max(100).nullable().optional(),
  autoRenew: z.boolean().optional(),
  currentPeriodStart: z.string().datetime().optional(),
  currentPeriodEnd: z.string().datetime().optional(),
  billingCycleCount: z.number().int().optional(),
  lastPaymentAt: z.string().datetime().nullable().optional(),
  metadata: z.string().max(2000).nullable().optional(),
  rowVersion: z.string().nullable().optional(),
  plan: z.lazy(() => CommerceSubscriptionsSubscriptionPlanSchema).optional(),
  status: z
    .lazy(() => CommerceSubscriptionsSubscriptionStatusSchema)
    .optional(),
  planId: z.string().uuid(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  amount: z.lazy(() => MoneySchema).optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().nullable().optional(),
  nextBillingDate: z.string().datetime().optional(),
  isActive: z.boolean().optional(),
  isTrialing: z.boolean().optional(),
  isCancelled: z.boolean().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionChurnReport */
CommerceSubscriptionsSubscriptionChurnReportSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  totalSubscriptions: z.number().int().optional(),
  activeSubscriptions: z.number().int().optional(),
  cancelledInPeriod: z.number().int().optional(),
  churnRate: z.number().optional(),
  retentionRate: z.number().optional(),
  monthlyRecurringRevenue: z.number().optional(),
  generatedAt: z.string().datetime().optional(),
  statusBreakdown: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionDowngradeResult */
CommerceSubscriptionsSubscriptionDowngradeResultSchema = z.object({
  success: z.boolean().optional(),
  updatedSubscription: z
    .lazy(() => CommerceSubscriptionsSubscriptionSchema)
    .optional(),
  effectiveDate: z.string().datetime().nullable().optional(),
  creditIssued: z.lazy(() => MoneySchema).optional(),
  failureReason: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput */
CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInputSchema =
  z.object({
    autoRenew: z.boolean().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput */
CommerceSubscriptionsSubscriptionLifecycleControllerCancelInputSchema =
  z.object({
    reason: z.string().nullable().optional(),
    note: z.string().nullable().optional(),
    effectiveDate: z.string().datetime().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput */
CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInputSchema =
  z.object({
    newPlanId: z.string().uuid().optional(),
    effectiveDate: z.string().datetime().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput */
CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInputSchema =
  z.object({
    convertToPaid: z.boolean().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput */
CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInputSchema =
  z.object({
    externalSubscriptionId: z.string().nullable().optional(),
    externalCustomerId: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput */
CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInputSchema =
  z.object({
    pauseUntil: z.string().datetime().nullable().optional(),
    reason: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput */
CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInputSchema =
  z.object({
    trialDays: z.number().int().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput */
CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInputSchema =
  z.object({
    reason: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput */
CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInputSchema =
  z.object({
    newPlanId: z.string().uuid().optional(),
    effectiveDate: z.string().datetime().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionNotification */
CommerceSubscriptionsSubscriptionNotificationSchema = z.object({
  id: z.string().uuid().optional(),
  recipientId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  subscriptionId: z.string().uuid().nullable().optional(),
  channel: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  isSent: z.boolean().optional(),
  sentAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput */
CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInputSchema =
  z.object({
    channel: z.lazy(() => NotificationsNotificationChannelSchema).optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlan */
CommerceSubscriptionsSubscriptionPlanSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  externalId: z.string().max(100).nullable().optional(),
  isFeatured: z.boolean().optional(),
  sortOrder: z.number().int().optional(),
  hasPrioritySupport: z.boolean().optional(),
  hasAdvancedAnalytics: z.boolean().optional(),
  hasCustomBranding: z.boolean().optional(),
  features: z.string().max(2000).nullable().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  trialPeriodDays: z.number().int().optional(),
  subscriptions: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionSchema))
    .nullable()
    .optional(),
  name: z.string().min(1).max(100),
  slug: z.string().min(1).max(50),
  description: z.string().max(1000).nullable().optional(),
  monthlyPriceInCents: z.number().int().optional(),
  annualPriceInCents: z.number().int().nullable().optional(),
  currency: z.string().min(1).max(3),
  isActive: z.boolean().optional(),
  maxUsers: z.number().int().nullable().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInputSchema =
  z.object({
    newName: z.string().nullable().optional(),
    newSlug: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInputSchema =
  z.object({
    externalId: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInputSchema =
  z.object({
    featured: z.boolean().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInputSchema =
  z.object({
    planId: z.string().uuid().optional(),
    name: z.string().nullable().optional(),
    description: z.string().nullable().optional(),
    sortOrder: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInputSchema =
  z.object({
    hasPrioritySupport: z.boolean().nullable().optional(),
    hasAdvancedAnalytics: z.boolean().nullable().optional(),
    hasCustomBranding: z.boolean().nullable().optional(),
    features: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInputSchema =
  z.object({
    maxUsers: z.number().int().nullable().optional(),
    maxStorageMb: z.number().int().nullable().optional(),
    maxApiCallsPerMonth: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInputSchema =
  z.object({
    monthlyPriceInCents: z.number().int().optional(),
    annualPriceInCents: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInputSchema =
  z.object({
    users: z.number().int().optional(),
    storageMb: z.number().int().optional(),
    apiCalls: z.number().int().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInputSchema =
  z.object({
    basePlanId: z.string().uuid().optional(),
    comparePlanIds: z.array(z.string().uuid()).nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInputSchema =
  z.object({
    name: z.string().nullable().optional(),
    slug: z.string().nullable().optional(),
    monthlyPriceInCents: z.number().int().optional(),
    currency: z.string().nullable().optional(),
    description: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInputSchema =
  z.object({
    name: z.string().nullable().optional(),
    slug: z.string().nullable().optional(),
    description: z.string().nullable().optional(),
    monthlyPriceInCents: z.number().int().optional(),
    annualPriceInCents: z.number().int().nullable().optional(),
    maxUsers: z.number().int().nullable().optional(),
    maxStorageMb: z.number().int().nullable().optional(),
    maxApiCallsPerMonth: z.number().int().nullable().optional(),
    hasPrioritySupport: z.boolean().nullable().optional(),
    hasAdvancedAnalytics: z.boolean().nullable().optional(),
    hasCustomBranding: z.boolean().nullable().optional(),
    features: z.string().nullable().optional(),
    sortOrder: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInputSchema =
  z.object({
    tenantId: z.string().uuid().optional(),
    planId: z.string().uuid().optional(),
    createdByUserId: z.string().uuid().optional(),
    billingCycle: z.lazy(() => BillingCycleSchema).optional(),
    amount: z.number().optional(),
    currency: z.string().nullable().optional(),
    fulfilledOrderId: z.string().uuid().nullable().optional(),
    startDate: z.string().datetime().nullable().optional(),
    trialDays: z.number().int().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInputSchema =
  z.object({
    billingCycle: z.lazy(() => BillingCycleSchema).optional(),
    autoRenew: z.boolean().nullable().optional(),
    externalSubscriptionId: z.string().nullable().optional(),
    externalCustomerId: z.string().nullable().optional(),
    metadata: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInputSchema =
  z.object({
    planId: z.string().uuid().optional(),
    billingCycle: z.lazy(() => BillingCycleSchema).optional(),
    amount: z.number().optional(),
    autoRenew: z.boolean().optional(),
    externalSubscriptionId: z.string().nullable().optional(),
    externalCustomerId: z.string().nullable().optional(),
  });

/** Zod schema for CommerceSubscriptionsSubscriptionStatus */
CommerceSubscriptionsSubscriptionStatusSchema = z.enum([
  "PendingActivation",
  "Active",
  "Trialing",
  "PastDue",
  "Suspended",
  "Cancelled",
  "Expired",
]);

/** Zod schema for CommerceSubscriptionsSubscriptionUpgradeResult */
CommerceSubscriptionsSubscriptionUpgradeResultSchema = z.object({
  success: z.boolean().optional(),
  updatedSubscription: z
    .lazy(() => CommerceSubscriptionsSubscriptionSchema)
    .optional(),
  proratedAmount: z.lazy(() => MoneySchema).optional(),
  creditApplied: z.lazy(() => MoneySchema).optional(),
  failureReason: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionUsage */
CommerceSubscriptionsSubscriptionUsageSchema = z.object({
  subscriptionId: z.string().uuid().optional(),
  usersCount: z.number().int().optional(),
  maxUsers: z.number().int().nullable().optional(),
  storageUsedMb: z.number().int().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  apiCallsThisMonth: z.number().int().optional(),
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
  isOverLimit: z.boolean().optional(),
  limitWarnings: z.array(z.string()).nullable().optional(),
});

/** Zod schema for ComplianceAuditAuditCategory */
ComplianceAuditAuditCategorySchema = z.enum([
  "General",
  "Authentication",
  "Authorization",
  "Permission",
  "User",
  "Admin",
  "Security",
  "Data",
  "System",
  "Tenant",
  "Privacy",
]);

/** Zod schema for ComplianceAuditAuditExportInput */
ComplianceAuditAuditExportInputSchema = z.object({
  userId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  actionType: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  category: z.lazy(() => ComplianceAuditAuditCategorySchema).optional(),
  riskLevel: z.lazy(() => ComplianceAuditAuditRiskLevelSchema).optional(),
  success: z.boolean().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
});

/** Zod schema for ComplianceAuditAuditLog */
ComplianceAuditAuditLogSchema = z.object({
  id: z.string().uuid().optional(),
  actionType: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  sessionId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  riskLevel: z.lazy(() => ComplianceAuditAuditRiskLevelSchema).optional(),
  category: z.lazy(() => ComplianceAuditAuditCategorySchema).optional(),
  correlationId: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for ComplianceAuditAuditLogOutput */
ComplianceAuditAuditLogOutputSchema = z.object({
  logs: z
    .array(z.lazy(() => ComplianceAuditAuditLogSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditAuditRiskLevel */
ComplianceAuditAuditRiskLevelSchema = z.enum([
  "Low",
  "Medium",
  "High",
  "Critical",
]);

/** Zod schema for ComplianceAuditAuditStatisticsOutput */
ComplianceAuditAuditStatisticsOutputSchema = z.object({
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  totalEvents: z.number().int().optional(),
  authenticationEvents: z.number().int().optional(),
  permissionEvents: z.number().int().optional(),
  securityEvents: z.number().int().optional(),
  failedEvents: z.number().int().optional(),
  highRiskEvents: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditAuthenticationAuditEntry */
ComplianceAuditAuthenticationAuditEntrySchema = z.object({
  id: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  isSuccessful: z.boolean().optional(),
  failureReason: z.string().nullable().optional(),
  attemptedAt: z.string().datetime().optional(),
  processingTime: z.string().optional(),
  geoLocation: z.string().nullable().optional(),
  isSuspicious: z.boolean().optional(),
});

/** Zod schema for ComplianceAuditAuthenticationAuditOutput */
ComplianceAuditAuthenticationAuditOutputSchema = z.object({
  entries: z
    .array(z.lazy(() => ComplianceAuditAuthenticationAuditEntrySchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  successfulLogins: z.number().int().optional(),
  failedLogins: z.number().int().optional(),
  uniqueIpAddresses: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditDailyActivityTrend */
ComplianceAuditDailyActivityTrendSchema = z.object({
  date: z.string().datetime().optional(),
  totalEvents: z.number().int().optional(),
  authenticationEvents: z.number().int().optional(),
  permissionEvents: z.number().int().optional(),
  securityViolations: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditFailureReasonCount */
ComplianceAuditFailureReasonCountSchema = z.object({
  reason: z.string().nullable().optional(),
  count: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditPermissionAuditEntry */
ComplianceAuditPermissionAuditEntrySchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  operationType: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  permissionType: z.string().nullable().optional(),
  oldValue: z.string().nullable().optional(),
  newValue: z.string().nullable().optional(),
  performedBy: z.string().uuid().optional(),
  ipAddress: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for ComplianceAuditPermissionAuditOutput */
ComplianceAuditPermissionAuditOutputSchema = z.object({
  entries: z
    .array(z.lazy(() => ComplianceAuditPermissionAuditEntrySchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  grantOperations: z.number().int().optional(),
  revokeOperations: z.number().int().optional(),
  denyOperations: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditSecurityAuditDashboard */
ComplianceAuditSecurityAuditDashboardSchema = z.object({
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  totalAuthenticationAttempts: z.number().int().optional(),
  successfulLogins: z.number().int().optional(),
  failedLogins: z.number().int().optional(),
  loginSuccessRate: z.number().optional(),
  uniqueUsersAuthenticated: z.number().int().optional(),
  suspiciousLoginAttempts: z.number().int().optional(),
  totalPermissionChanges: z.number().int().optional(),
  permissionsGranted: z.number().int().optional(),
  permissionsRevoked: z.number().int().optional(),
  permissionDenials: z.number().int().optional(),
  totalSecurityViolations: z.number().int().optional(),
  highRiskEvents: z.number().int().optional(),
  crossTenantAttempts: z.number().int().optional(),
  topActiveUsers: z
    .array(z.lazy(() => ComplianceAuditTopUserActivitySchema))
    .nullable()
    .optional(),
  topIpAddresses: z
    .array(z.lazy(() => ComplianceAuditTopIpActivitySchema))
    .nullable()
    .optional(),
  topFailureReasons: z
    .array(z.lazy(() => ComplianceAuditFailureReasonCountSchema))
    .nullable()
    .optional(),
  dailyTrends: z
    .array(z.lazy(() => ComplianceAuditDailyActivityTrendSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ComplianceAuditSecurityAuditSourceType */
ComplianceAuditSecurityAuditSourceTypeSchema = z.enum([
  "Authentication",
  "Permission",
  "General",
  "All",
]);

/** Zod schema for ComplianceAuditTopIpActivity */
ComplianceAuditTopIpActivitySchema = z.object({
  ipAddress: z.string().nullable().optional(),
  eventCount: z.number().int().optional(),
  failedAttempts: z.number().int().optional(),
  uniqueUsers: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditTopUserActivity */
ComplianceAuditTopUserActivitySchema = z.object({
  userId: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  eventCount: z.number().int().optional(),
  failedAttempts: z.number().int().optional(),
});

/** Zod schema for ComplianceAuditUnifiedSecurityAuditEntry */
ComplianceAuditUnifiedSecurityAuditEntrySchema = z.object({
  id: z.string().uuid().optional(),
  sourceType: z
    .lazy(() => ComplianceAuditSecurityAuditSourceTypeSchema)
    .optional(),
  sourceEntity: z.string().nullable().optional(),
  actionType: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  riskLevel: z.lazy(() => ComplianceAuditAuditRiskLevelSchema).optional(),
  timestamp: z.string().datetime().optional(),
  metadata: z.string().nullable().optional(),
});

/** Zod schema for ComplianceAuditUnifiedSecurityAuditInput */
ComplianceAuditUnifiedSecurityAuditInputSchema = z.object({
  sourceType: z
    .lazy(() => ComplianceAuditSecurityAuditSourceTypeSchema)
    .optional(),
  userId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  actionType: z.string().nullable().optional(),
  success: z.boolean().nullable().optional(),
  riskLevel: z.lazy(() => ComplianceAuditAuditRiskLevelSchema).optional(),
  startDate: z.string().datetime().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  searchText: z.string().nullable().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  sortBy: z.string().nullable().optional(),
  sortDirection: z.string().nullable().optional(),
});

/** Zod schema for ComplianceAuditUnifiedSecurityAuditOutput */
ComplianceAuditUnifiedSecurityAuditOutputSchema = z.object({
  entries: z
    .array(z.lazy(() => ComplianceAuditUnifiedSecurityAuditEntrySchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  sourceBreakdown: z
    .object({
      Authentication: z.number().int(),
      Permission: z.number().int(),
      General: z.number().int(),
      All: z.number().int(),
    })
    .nullable()
    .optional(),
});

/** Zod schema for ComplianceConsentConsentPolicy */
ComplianceConsentConsentPolicySchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  policyType: z.lazy(() => ComplianceConsentPolicyTypeSchema).optional(),
  isMandatory: z.boolean().optional(),
  isActive: z.boolean().optional(),
  currentVersion: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentContentType */
ComplianceConsentContentTypeSchema = z.enum([
  "PlainText",
  "Html",
  "Markdown",
  "Url",
]);

/** Zod schema for ComplianceConsentCreateConsentPolicyCommand */
ComplianceConsentCreateConsentPolicyCommandSchema = z.object({
  name: z.string().nullable().optional(),
  policyType: z.lazy(() => ComplianceConsentPolicyTypeSchema).optional(),
  isMandatory: z.boolean().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentDataSubjectInput */
ComplianceConsentDataSubjectInputSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  requestType: z
    .lazy(() => ComplianceConsentDataSubjectRequestTypeSchema)
    .optional(),
  status: z
    .lazy(() => ComplianceConsentDataSubjectRequestStatusSchema)
    .optional(),
  deadline: z.string().datetime().optional(),
  processedAt: z.string().datetime().nullable().optional(),
  processingNotes: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentDataSubjectRequestStatus */
ComplianceConsentDataSubjectRequestStatusSchema = z.enum([
  "Pending",
  "InProgress",
  "Completed",
  "Rejected",
  "Expired",
]);

/** Zod schema for ComplianceConsentDataSubjectRequestType */
ComplianceConsentDataSubjectRequestTypeSchema = z.enum([
  "Access",
  "Erasure",
  "Portability",
  "Rectification",
  "Restriction",
  "Objection",
]);

/** Zod schema for ComplianceConsentGrantConsentCommand */
ComplianceConsentGrantConsentCommandSchema = z.object({
  userId: z.string().uuid().optional(),
  policyVersionId: z.string().uuid().optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  consentMethod: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentPolicyType */
ComplianceConsentPolicyTypeSchema = z.enum([
  "PrivacyPolicy",
  "TermsOfService",
  "CookiePolicy",
  "DataProcessingAgreement",
  "MarketingConsent",
  "ThirdPartySharing",
  "Custom",
]);

/** Zod schema for ComplianceConsentPolicyVersion */
ComplianceConsentPolicyVersionSchema = z.object({
  id: z.string().uuid().optional(),
  policyId: z.string().uuid().optional(),
  versionNumber: z.string().nullable().optional(),
  contentType: z.lazy(() => ComplianceConsentContentTypeSchema).optional(),
  effectiveFrom: z.string().datetime().optional(),
  isCurrent: z.boolean().optional(),
});

/** Zod schema for ComplianceConsentProcessRequestBody */
ComplianceConsentProcessRequestBodySchema = z.object({
  processedByUserId: z.string().uuid().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentPublishVersionInput */
ComplianceConsentPublishVersionInputSchema = z.object({
  versionNumber: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  contentType: z.lazy(() => ComplianceConsentContentTypeSchema).optional(),
});

/** Zod schema for ComplianceConsentRevokeConsentCommand */
ComplianceConsentRevokeConsentCommandSchema = z.object({
  userId: z.string().uuid().optional(),
  policyVersionId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceConsentSubmitDataSubjectRequestCommand */
ComplianceConsentSubmitDataSubjectRequestCommandSchema = z.object({
  userId: z.string().uuid().optional(),
  requestType: z
    .lazy(() => ComplianceConsentDataSubjectRequestTypeSchema)
    .optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for ComplianceConsentUserConsent */
ComplianceConsentUserConsentSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  policyVersionId: z.string().uuid().optional(),
  isGranted: z.boolean().optional(),
  consentGivenAt: z.string().datetime().optional(),
  consentRevokedAt: z.string().datetime().nullable().optional(),
  consentMethod: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPACompleteFerpaInspectionRequestBody */
ComplianceFERPACompleteFerpaInspectionRequestBodySchema = z.object({
  processedByUserId: z.string().uuid().optional(),
  approved: z.boolean().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPAEducationRecordKind */
ComplianceFERPAEducationRecordKindSchema = z.enum([
  "CourseEnrollment",
  "AssessmentSubmission",
  "Grade",
  "Certificate",
  "Attendance",
  "Communication",
  "SupportCase",
  "Custom",
]);

/** Zod schema for ComplianceFERPAFerpaDirectoryInformationPolicy */
ComplianceFERPAFerpaDirectoryInformationPolicySchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  allowedFieldsJson: z.string().nullable().optional(),
  optOutEnabled: z.boolean().optional(),
  annualNoticeSentAt: z.string().datetime().nullable().optional(),
  noticeUrl: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPAFerpaDisclosureBasis */
ComplianceFERPAFerpaDisclosureBasisSchema = z.enum([
  "StudentConsent",
  "GuardianConsent",
  "SchoolOfficial",
  "FinancialAid",
  "HealthOrSafetyEmergency",
  "AuditOrEvaluation",
  "CourtOrder",
  "DirectoryInformation",
  "Other",
]);

/** Zod schema for ComplianceFERPAFerpaDisclosureConsent */
ComplianceFERPAFerpaDisclosureConsentSchema = z.object({
  id: z.string().uuid().optional(),
  studentUserId: z.string().uuid().optional(),
  guardianUserId: z.string().uuid().nullable().optional(),
  recipient: z.string().nullable().optional(),
  purpose: z.string().nullable().optional(),
  scope: z.string().nullable().optional(),
  effectiveFrom: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for ComplianceFERPAFerpaDisclosureLog */
ComplianceFERPAFerpaDisclosureLogSchema = z.object({
  id: z.string().uuid().optional(),
  studentUserId: z.string().uuid().optional(),
  disclosedByUserId: z.string().uuid().optional(),
  recipient: z.string().nullable().optional(),
  basis: z.lazy(() => ComplianceFERPAFerpaDisclosureBasisSchema).optional(),
  purpose: z.string().nullable().optional(),
  recordIdsJson: z.string().nullable().optional(),
  disclosedAt: z.string().datetime().optional(),
});

/** Zod schema for ComplianceFERPAFerpaEducationRecord */
ComplianceFERPAFerpaEducationRecordSchema = z.object({
  id: z.string().uuid().optional(),
  studentUserId: z.string().uuid().optional(),
  recordKind: z.lazy(() => ComplianceFERPAEducationRecordKindSchema).optional(),
  externalRecordId: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  protectionLevel: z
    .lazy(() => ComplianceFERPAFerpaRecordProtectionLevelSchema)
    .optional(),
  isDirectoryInformation: z.boolean().optional(),
  retentionUntil: z.string().datetime().nullable().optional(),
  metadataJson: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for ComplianceFERPAFerpaInspectionInput */
ComplianceFERPAFerpaInspectionInputSchema = z.object({
  id: z.string().uuid().optional(),
  studentUserId: z.string().uuid().optional(),
  requestedByUserId: z.string().uuid().optional(),
  status: z.lazy(() => ComplianceFERPAFerpaRequestStatusSchema).optional(),
  deadline: z.string().datetime().optional(),
  processedByUserId: z.string().uuid().nullable().optional(),
  processedAt: z.string().datetime().nullable().optional(),
  processingNotes: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPAFerpaRecordProtectionLevel */
ComplianceFERPAFerpaRecordProtectionLevelSchema = z.enum([
  "DirectoryInformation",
  "EducationRecord",
  "SensitiveEducationRecord",
  "Restricted",
]);

/** Zod schema for ComplianceFERPAFerpaRequestStatus */
ComplianceFERPAFerpaRequestStatusSchema = z.enum([
  "Pending",
  "InReview",
  "Completed",
  "Denied",
  "Expired",
]);

/** Zod schema for ComplianceFERPAGrantFerpaDisclosureConsentCommand */
ComplianceFERPAGrantFerpaDisclosureConsentCommandSchema = z.object({
  studentUserId: z.string().uuid().optional(),
  recipient: z.string().nullable().optional(),
  purpose: z.string().nullable().optional(),
  scope: z.string().nullable().optional(),
  effectiveFrom: z.string().datetime().optional(),
  guardianUserId: z.string().uuid().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ComplianceFERPARecordFerpaDisclosureCommand */
ComplianceFERPARecordFerpaDisclosureCommandSchema = z.object({
  studentUserId: z.string().uuid().optional(),
  disclosedByUserId: z.string().uuid().optional(),
  recipient: z.string().nullable().optional(),
  basis: z.lazy(() => ComplianceFERPAFerpaDisclosureBasisSchema).optional(),
  purpose: z.string().nullable().optional(),
  scope: z.string().nullable().optional(),
  recordIdsJson: z.string().nullable().optional(),
  disclosedAt: z.string().datetime().optional(),
});

/** Zod schema for ComplianceFERPARegisterEducationRecordCommand */
ComplianceFERPARegisterEducationRecordCommandSchema = z.object({
  studentUserId: z.string().uuid().optional(),
  recordKind: z.lazy(() => ComplianceFERPAEducationRecordKindSchema).optional(),
  externalRecordId: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  protectionLevel: z
    .lazy(() => ComplianceFERPAFerpaRecordProtectionLevelSchema)
    .optional(),
  isDirectoryInformation: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  retentionUntil: z.string().datetime().nullable().optional(),
  metadataJson: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPASubmitFerpaInspectionRequestCommand */
ComplianceFERPASubmitFerpaInspectionRequestCommandSchema = z.object({
  studentUserId: z.string().uuid().optional(),
  requestedByUserId: z.string().uuid().optional(),
  deadline: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPAUpsertDirectoryInformationPolicyCommand */
ComplianceFERPAUpsertDirectoryInformationPolicyCommandSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  allowedFieldsJson: z.string().nullable().optional(),
  optOutEnabled: z.boolean().optional(),
  annualNoticeSentAt: z.string().datetime().nullable().optional(),
  noticeUrl: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesContentResource */
ContentPagesContentResourceSchema = z.object({
  id: z.string().uuid().optional(),
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  authorId: z.string().uuid().nullable().optional(),
  authorName: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  viewCount: z.number().int().optional(),
  isFeatured: z.boolean().optional(),
  sortOrder: z.number().int().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  customData: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesContentResourceStatus */
ContentPagesContentResourceStatusSchema = z.enum([
  "Draft",
  "InReview",
  "Published",
  "Archived",
]);

/** Zod schema for ContentPagesContentResourceType */
ContentPagesContentResourceTypeSchema = z.enum([
  "Article",
  "Tutorial",
  "Documentation",
  "Video",
  "Download",
  "ExternalLink",
  "Course",
  "Custom",
]);

/** Zod schema for ContentPagesCreateContentResource */
ContentPagesCreateContentResourceSchema = z.object({
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  resourceType: z.lazy(() => ContentPagesContentResourceTypeSchema).optional(),
  locale: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  isFeatured: z.boolean().optional(),
  sortOrder: z.number().int().optional(),
  customData: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesCreateMarketingLead */
ContentPagesCreateMarketingLeadSchema = z.object({
  source: z.string().min(1).max(40),
  name: z.string().max(120).nullable().optional(),
  email: z.string().email().min(1).max(200),
  company: z.string().max(200).nullable().optional(),
  topic: z.string().max(40).nullable().optional(),
  plan: z.string().max(60).nullable().optional(),
  message: z.string().max(4000).nullable().optional(),
  locale: z.string().max(10).nullable().optional(),
  pagePath: z.string().max(300).nullable().optional(),
  referrer: z.string().max(2000).nullable().optional(),
  userAgent: z.string().max(500).nullable().optional(),
});

/** Zod schema for ContentPagesCreatePage */
ContentPagesCreatePageSchema = z.object({
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  pageType: z.lazy(() => ContentPagesPageTypeSchema).optional(),
  locale: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
});

/** Zod schema for ContentPagesCreatePageSection */
ContentPagesCreatePageSectionSchema = z.object({
  sectionType: z.lazy(() => ContentPagesSectionTypeSchema).optional(),
  heading: z.string().nullable().optional(),
  subheading: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isVisible: z.boolean().optional(),
  cssClasses: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesMarketingLead */
ContentPagesMarketingLeadSchema = z.object({
  id: z.string().uuid().optional(),
  source: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  topic: z.string().nullable().optional(),
  plan: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  pagePath: z.string().nullable().optional(),
  referrer: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesOpenGraphMetadata */
ContentPagesOpenGraphMetadataSchema = z.object({
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesPage */
ContentPagesPageSchema = z.object({
  id: z.string().uuid().optional(),
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  pageType: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  sections: z
    .array(z.lazy(() => ContentPagesPageSectionSchema))
    .nullable()
    .optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesPageSection */
ContentPagesPageSectionSchema = z.object({
  id: z.string().uuid().optional(),
  pageId: z.string().uuid().optional(),
  sectionType: z.string().nullable().optional(),
  heading: z.string().nullable().optional(),
  subheading: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isVisible: z.boolean().optional(),
  cssClasses: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesPageStatus */
ContentPagesPageStatusSchema = z.enum(["Draft", "Published", "Archived"]);

/** Zod schema for ContentPagesPageType */
ContentPagesPageTypeSchema = z.enum([
  "Landing",
  "Legal",
  "ResourceIndex",
  "Resource",
  "Custom",
]);

/** Zod schema for ContentPagesSectionType */
ContentPagesSectionTypeSchema = z.enum([
  "Hero",
  "Features",
  "Testimonials",
  "Pricing",
  "CallToAction",
  "Faq",
  "RichText",
  "Gallery",
  "Stats",
  "Team",
  "LogoCloud",
  "Newsletter",
  "Contact",
  "ResourceCards",
  "Custom",
]);

/** Zod schema for ContentPagesSitemapEntry */
ContentPagesSitemapEntrySchema = z.object({
  slug: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  locale: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesUpdateContentResource */
ContentPagesUpdateContentResourceSchema = z.object({
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  resourceType: z.lazy(() => ContentPagesContentResourceTypeSchema).optional(),
  status: z.lazy(() => ContentPagesContentResourceStatusSchema).optional(),
  locale: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  isFeatured: z.boolean().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  customData: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesUpdatePage */
ContentPagesUpdatePageSchema = z.object({
  slug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  pageType: z.lazy(() => ContentPagesPageTypeSchema).optional(),
  status: z.lazy(() => ContentPagesPageStatusSchema).optional(),
  locale: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesUpdatePageSection */
ContentPagesUpdatePageSectionSchema = z.object({
  sectionType: z.lazy(() => ContentPagesSectionTypeSchema).optional(),
  heading: z.string().nullable().optional(),
  subheading: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  isVisible: z.boolean().nullable().optional(),
  cssClasses: z.string().nullable().optional(),
});

/** Zod schema for ContentStatus */
ContentStatusSchema = z.enum([
  "Draft",
  "Review",
  "Published",
  "Archived",
  "Deleted",
]);

/** Zod schema for ContentVisibility */
ContentVisibilitySchema = z.enum([
  "Private",
  "Internal",
  "Friends",
  "Protected",
  "Public",
]);

/** Zod schema for CQRSIDomainEvent */
CQRSIDomainEventSchema = z.object({
  eventId: z.string().uuid().optional(),
  occurredAt: z.string().datetime().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for CQRSModelsTenantId */
CQRSModelsTenantIdSchema = z.object({
  value: z.string().uuid().optional(),
});

/** Zod schema for EconomyCommandsConvertMyHardToSoftInput */
EconomyCommandsConvertMyHardToSoftInputSchema = z.object({
  principalHardCoinUnits: z.number().int().optional(),
  feeHardCoinUnits: z.number().int().optional(),
  idempotencyKey: z.string().nullable().optional(),
});

/** Zod schema for EconomyContractsCurrencyCode */
EconomyContractsCurrencyCodeSchema = z.enum(["HardCoin", "SoftCoin"]);

/** Zod schema for EconomyContractsEconomyWalletSummary */
EconomyContractsEconomyWalletSummarySchema = z.object({
  walletId: z.string().uuid().optional(),
  state: z.lazy(() => EconomyContractsWalletLifecycleStateSchema).optional(),
  createdAt: z.string().datetime().optional(),
  pendingHard: z.number().int().optional(),
  pendingSoft: z.number().int().optional(),
  purchasedHard: z.number().int().optional(),
  earnedHard: z.number().int().optional(),
  restrictedHard: z.number().int().optional(),
  soft: z.number().int().optional(),
  heldHard: z.number().int().optional(),
  heldSoft: z.number().int().optional(),
  availableHardToSpend: z.number().int().optional(),
  availableSoftToSpend: z.number().int().optional(),
  withdrawableHard: z.number().int().optional(),
  outstandingHardDebt: z.number().int().optional(),
  projectionRebuiltAt: z.string().datetime().optional(),
  sourceJournalSequence: z.number().int().optional(),
});

/** Zod schema for EconomyContractsEconomyWalletTransaction */
EconomyContractsEconomyWalletTransactionSchema = z.object({
  postingGroupId: z.string().uuid().optional(),
  journalEntryId: z.string().uuid().optional(),
  journalSequence: z.number().int().optional(),
  templateKind: z
    .lazy(() => EconomyContractsPostingTemplateKindSchema)
    .optional(),
  status: z.lazy(() => EconomyContractsPostingStatusSchema).optional(),
  recordedAt: z.string().datetime().optional(),
  side: z.lazy(() => EconomyContractsEntrySideSchema).optional(),
  currency: z.lazy(() => EconomyContractsCurrencyCodeSchema).optional(),
  amountUnits: z.number().int().optional(),
  provenance: z.lazy(() => EconomyContractsProvenanceKindSchema).optional(),
});

/** Zod schema for EconomyContractsEntrySide */
EconomyContractsEntrySideSchema = z.enum(["Debit", "Credit"]);

/** Zod schema for EconomyContractsPostingStatus */
EconomyContractsPostingStatusSchema = z.enum([
  "Accepted",
  "Rejected",
  "Duplicate",
]);

/** Zod schema for EconomyContractsPostingTemplateKind */
EconomyContractsPostingTemplateKindSchema = z.enum([
  "ConfirmedTopUpMint",
  "ProviderReversalFull",
  "ProviderReversalPartial",
  "Spend",
  "HardToSoftConversion",
  "SystemBackedGrant",
  "Burn",
  "Escrow",
  "Reclaim",
  "Refund",
  "PayoutReservation",
  "PayoutSuccess",
  "PayoutFailure",
  "AdminWithdrawalReservation",
  "AdminWithdrawalSuccess",
  "AdminWithdrawalFailure",
  "HardToSoftConversionFee",
  "ProviderConvertedSoftReversal",
  "ProviderReversalDebt",
  "ProviderReversalLoss",
  "AdRewardIssuance",
  "BountyEscrow",
  "BountyClaim",
  "BountyReclaim",
]);

/** Zod schema for EconomyContractsProvenanceKind */
EconomyContractsProvenanceKindSchema = z.enum([
  "PurchasedHard",
  "EarnedHard",
  "ConvertedSoft",
  "AdRewardSoft",
  "SystemGrantSoft",
  "RefundRestoration",
  "EscrowReturn",
]);

/** Zod schema for EconomyContractsWalletLifecycleState */
EconomyContractsWalletLifecycleStateSchema = z.enum([
  "Active",
  "Frozen",
  "Closed",
  "UnderReview",
]);

/** Zod schema for EconomyFundingSelfServiceHardToSoftConversionReceipt */
EconomyFundingSelfServiceHardToSoftConversionReceiptSchema = z.object({
  principalPostingId: z.string().uuid().optional(),
  feePostingId: z.string().uuid().nullable().optional(),
  journalSequence: z.number().int().optional(),
  journalHash: z.string().nullable().optional(),
  isDuplicate: z.boolean().optional(),
});

/** Zod schema for EconomyPayoutsCommandsCreateMyPayoutRequestInput */
EconomyPayoutsCommandsCreateMyPayoutRequestInputSchema = z.object({
  hardCoinUnits: z.number().int().optional(),
  idempotencyKey: z.string().nullable().optional(),
});

/** Zod schema for EconomyPayoutsPayoutOperationState */
EconomyPayoutsPayoutOperationStateSchema = z.enum([
  "Reserved",
  "Dispatching",
  "Ambiguous",
  "Succeeded",
  "Failed",
  "Cancelled",
]);

/** Zod schema for EconomyPayoutsPayoutRequestState */
EconomyPayoutsPayoutRequestStateSchema = z.enum([
  "Submitted",
  "Cancelled",
  "Approved",
  "Rejected",
]);

/** Zod schema for EconomyPayoutsQueriesEconomyPayoutInput */
EconomyPayoutsQueriesEconomyPayoutInputSchema = z.object({
  id: z.string().uuid().optional(),
  hardCoinUnits: z.number().int().optional(),
  state: z.lazy(() => EconomyPayoutsPayoutRequestStateSchema).optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for EconomyPayoutsQueriesEconomyPayoutOperation */
EconomyPayoutsQueriesEconomyPayoutOperationSchema = z.object({
  id: z.string().uuid().optional(),
  hardCoinUnits: z.number().int().optional(),
  state: z.lazy(() => EconomyPayoutsPayoutOperationStateSchema).optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for EconomyRiskEconomyValueMovementCapability */
EconomyRiskEconomyValueMovementCapabilitySchema = z.enum([
  "ConfirmHardCoinFunding",
  "ConvertHardToSoft",
  "ReverseProviderFunding",
  "Transfer",
  "IssueAdReward",
  "BountyEscrow",
  "BountyClaim",
  "MarketplaceSettlement",
  "PayoutExecution",
  "AdminWithdrawalExecution",
]);

/** Zod schema for Error */
ErrorSchema = z.object({
  code: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => ErrorTypeSchema).optional(),
});

/** Zod schema for ErrorType */
ErrorTypeSchema = z.enum([
  "Failure",
  "Validation",
  "Problem",
  "NotFound",
  "Conflict",
  "Unauthorized",
  "Forbidden",
  "None",
]);

/** Zod schema for FeaturesBulkEvaluationInput */
FeaturesBulkEvaluationInputSchema = z.object({
  featureKeys: z.array(z.string()).nullable().optional(),
  context: z.lazy(() => FeaturesFeatureContextSchema).optional(),
});

/** Zod schema for FeaturesCapabilityAuditLog */
FeaturesCapabilityAuditLogSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  capabilityKey: z.string().nullable().optional(),
  oldValue: z.boolean().nullable().optional(),
  newValue: z.boolean().optional(),
  oldSource: z.string().nullable().optional(),
  newSource: z.string().nullable().optional(),
  changedByUserId: z.string().uuid().nullable().optional(),
  changeReason: z.string().nullable().optional(),
  changeType: z.string().nullable().optional(),
  changedAt: z.string().datetime().optional(),
});

/** Zod schema for FeaturesCapabilityCheckOutput */
FeaturesCapabilityCheckOutputSchema = z.object({
  capability: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for FeaturesCreateFeatureInput */
FeaturesCreateFeatureInputSchema = z.object({
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for FeaturesFeatureContext */
FeaturesFeatureContextSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  subscriptionPlanId: z.string().nullable().optional(),
  environment: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  customAttributes: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  userAgent: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  requestTime: z.string().datetime().optional(),
});

/** Zod schema for FeaturesFeatureEvaluationInput */
FeaturesFeatureEvaluationInputSchema = z.object({
  featureKey: z.string().nullable().optional(),
  defaultValue: z.record(z.string(), z.unknown()).nullable().optional(),
  context: z.lazy(() => FeaturesFeatureContextSchema).optional(),
});

/** Zod schema for FeaturesFeatureFlag */
FeaturesFeatureFlagSchema = z.object({
  id: z.string().uuid(),
  key: z.string().nullable(),
  name: z.string().nullable(),
  description: z.string().nullable().optional(),
  isEnabled: z.boolean(),
  type: z.lazy(() => FeaturesFeatureFlagTypeSchema),
  environment: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  defaultValue: z.record(z.string(), z.unknown()).nullable().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  targets: z
    .array(z.lazy(() => FeaturesFeatureFlagTargetSchema))
    .nullable()
    .optional(),
});

/** Zod schema for FeaturesFeatureFlagTarget */
FeaturesFeatureFlagTargetSchema = z.object({
  id: z.string().uuid(),
  featureFlagId: z.string().uuid(),
  targetType: z.string().nullable(),
  targetIdentifier: z.string().nullable(),
  isEnabled: z.boolean(),
  rolloutPercentage: z.number().int().optional(),
  customValue: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  priority: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for FeaturesFeatureFlagType */
FeaturesFeatureFlagTypeSchema = z.enum([
  "Toggle",
  "Numeric",
  "String",
  "Percentage",
  "UserSegment",
]);

/** Zod schema for FeaturesSetCapabilityOverrideInput */
FeaturesSetCapabilityOverrideInputSchema = z.object({
  capability: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
  source: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for FeaturesToggleFeatureInput */
FeaturesToggleFeatureInputSchema = z.object({
  featureKey: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  environment: z.string().nullable().optional(),
});

/** Zod schema for FeaturesUpdateFeatureInput */
FeaturesUpdateFeatureInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isEnabled: z.boolean().nullable().optional(),
  rolloutPercentage: z.number().int().nullable().optional(),
  enabledValue: z.string().nullable().optional(),
  defaultValue: z.string().nullable().optional(),
});

/** Zod schema for Fido2NetLibAssertionOptions */
Fido2NetLibAssertionOptionsSchema = z.object({
  challenge: z.string().nullable().optional(),
  timeout: z.number().int().optional(),
  rpId: z.string().nullable().optional(),
  allowCredentials: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialDescriptorSchema))
    .nullable()
    .optional(),
  userVerification: z
    .lazy(() => ObjectsUserVerificationRequirementSchema)
    .optional(),
  hints: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialHintSchema))
    .nullable()
    .optional(),
  extensions: z
    .lazy(() => ObjectsAuthenticationExtensionsClientInputsSchema)
    .optional(),
});

/** Zod schema for Fido2NetLibAuthenticatorSelection */
Fido2NetLibAuthenticatorSelectionSchema = z.object({
  authenticatorAttachment: z
    .lazy(() => ObjectsAuthenticatorAttachmentSchema)
    .optional(),
  residentKey: z.lazy(() => ObjectsResidentKeyRequirementSchema).optional(),
  requireResidentKey: z.boolean().optional(),
  userVerification: z
    .lazy(() => ObjectsUserVerificationRequirementSchema)
    .optional(),
});

/** Zod schema for Fido2NetLibCredentialCreateOptions */
Fido2NetLibCredentialCreateOptionsSchema = z.object({
  rp: z.lazy(() => Fido2NetLibPublicKeyCredentialRpEntitySchema),
  user: z.lazy(() => Fido2NetLibFido2UserSchema),
  challenge: z.string().nullable(),
  pubKeyCredParams: z
    .array(z.lazy(() => Fido2NetLibPubKeyCredParamSchema))
    .nullable(),
  timeout: z.number().int().optional(),
  attestation: z
    .lazy(() => ObjectsAttestationConveyancePreferenceSchema)
    .optional(),
  attestationFormats: z
    .array(z.lazy(() => ObjectsAttestationStatementFormatIdentifierSchema))
    .nullable()
    .optional(),
  authenticatorSelection: z
    .lazy(() => Fido2NetLibAuthenticatorSelectionSchema)
    .optional(),
  hints: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialHintSchema))
    .nullable()
    .optional(),
  excludeCredentials: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialDescriptorSchema))
    .nullable()
    .optional(),
  extensions: z
    .lazy(() => ObjectsAuthenticationExtensionsClientInputsSchema)
    .optional(),
});

/** Zod schema for Fido2NetLibFido2User */
Fido2NetLibFido2UserSchema = z.object({
  name: z.string().nullable().optional(),
  id: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
});

/** Zod schema for Fido2NetLibPubKeyCredParam */
Fido2NetLibPubKeyCredParamSchema = z.object({
  type: z.lazy(() => ObjectsPublicKeyCredentialTypeSchema).optional(),
  alg: z.lazy(() => ObjectsCOSEAlgorithmSchema).optional(),
});

/** Zod schema for Fido2NetLibPublicKeyCredentialRpEntity */
Fido2NetLibPublicKeyCredentialRpEntitySchema = z.object({
  id: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  icon: z.string().nullable().optional(),
});

/** Zod schema for GameJamsAddJamCriteriaInput */
GameJamsAddJamCriteriaInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  weight: z.number().optional(),
  maxScore: z.number().int().optional(),
});

/** Zod schema for GameJamsCreateJamInput */
GameJamsCreateJamInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  createdBy: z.string().uuid().optional(),
  theme: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  rules: z.string().nullable().optional(),
  submissionCriteria: z.string().nullable().optional(),
  votingEndDate: z.string().datetime().nullable().optional(),
  maxParticipants: z.number().int().nullable().optional(),
});

/** Zod schema for GameJamsJam */
GameJamsJamSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(255),
  slug: z.string().min(1).max(255),
  theme: z.string().max(500).nullable().optional(),
  description: z.string().nullable().optional(),
  rules: z.string().nullable().optional(),
  submissionCriteria: z.string().nullable().optional(),
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
  votingEndDate: z.string().datetime().nullable().optional(),
  maxParticipants: z.number().int().nullable().optional(),
  participantCount: z.number().int().optional(),
  status: z.lazy(() => GameJamsJamStatusSchema),
  createdBy: z.string().uuid(),
});

/** Zod schema for GameJamsJamCriteria */
GameJamsJamCriteriaSchema = z.object({
  id: z.string().uuid().optional(),
  jamId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  weight: z.number().optional(),
  maxScore: z.number().int().optional(),
});

/** Zod schema for GameJamsJamDto */
GameJamsJamDtoSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  theme: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  votingEndDate: z.string().datetime().nullable().optional(),
  maxParticipants: z.number().int().nullable().optional(),
  participantCount: z.number().int().optional(),
  status: z.lazy(() => GameJamsJamStatusSchema).optional(),
  createdBy: z.string().uuid().optional(),
});

/** Zod schema for GameJamsJamScore */
GameJamsJamScoreSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  submissionId: z.string().uuid(),
  criteriaId: z.string().uuid(),
  judgeUserId: z.string().uuid(),
  score: z.number().int(),
  feedback: z.string().nullable().optional(),
});

/** Zod schema for GameJamsJamScoreDto */
GameJamsJamScoreDtoSchema = z.object({
  id: z.string().uuid().optional(),
  submissionId: z.string().uuid().optional(),
  criteriaId: z.string().uuid().optional(),
  judgeUserId: z.string().uuid().optional(),
  score: z.number().int().optional(),
  feedback: z.string().nullable().optional(),
});

/** Zod schema for GameJamsJamStatus */
GameJamsJamStatusSchema = z.enum([
  "Upcoming",
  "Active",
  "Voting",
  "Completed",
  "Cancelled",
]);

/** Zod schema for GameJamsJamSubmission */
GameJamsJamSubmissionSchema = z.object({
  id: z.string().uuid().optional(),
  jamId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  submissionNotes: z.string().nullable().optional(),
});

/** Zod schema for GameJamsScoreJamSubmissionInput */
GameJamsScoreJamSubmissionInputSchema = z.object({
  criteriaId: z.string().uuid().optional(),
  judgeUserId: z.string().uuid().optional(),
  score: z.number().int().optional(),
  feedback: z.string().nullable().optional(),
});

/** Zod schema for GameJamsSubmitJamEntryInput */
GameJamsSubmitJamEntryInputSchema = z.object({
  projectVersionId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationApiKey */
IdentityAuthenticationApiKeySchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  keyPrefix: z.string().nullable().optional(),
  scopes: z.array(z.string()).nullable().optional(),
  isActive: z.boolean().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  usageCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationAssignRoleToUserInput */
IdentityAuthenticationAssignRoleToUserInputSchema = z.object({
  userId: z.string().uuid().optional(),
  roleId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationBackupCodesOutput */
IdentityAuthenticationBackupCodesOutputSchema = z.object({
  codes: z.array(z.string()).nullable().optional(),
  generatedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationBackupCodesStatusOutput */
IdentityAuthenticationBackupCodesStatusOutputSchema = z.object({
  totalCount: z.number().int(),
  remainingCount: z.number().int(),
  usedCount: z.number().int(),
  hasBackupCodes: z.boolean(),
});

/** Zod schema for IdentityAuthenticationBeginWebAuthnAuthenticationInput */
IdentityAuthenticationBeginWebAuthnAuthenticationInputSchema = z.object({
  email: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationBeginWebAuthnRegistrationInput */
IdentityAuthenticationBeginWebAuthnRegistrationInputSchema = z.object({
  email: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  preferredAuthenticatorType: z
    .lazy(() => IdentityAuthenticationWebAuthnAuthenticatorTypeSchema)
    .optional(),
});

/** Zod schema for IdentityAuthenticationCleanupKeysInput */
IdentityAuthenticationCleanupKeysInputSchema = z.object({
  retentionDays: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCleanupResult */
IdentityAuthenticationCleanupResultSchema = z.object({
  deletedCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationClientCredentialsTokenOutput */
IdentityAuthenticationClientCredentialsTokenOutputSchema = z.object({
  accessToken: z.string().nullable().optional(),
  tokenType: z.string().nullable().optional(),
  expiresIn: z.number().int().optional(),
  scope: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCompleteMfaSetupInput */
IdentityAuthenticationCompleteMfaSetupInputSchema = z.object({
  code: z.string().min(1),
  secretKey: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationCompletePasswordResetInput */
IdentityAuthenticationCompletePasswordResetInputSchema = z.object({
  token: z.string().min(1),
  newPassword: z.string().min(8),
  confirmPassword: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCompleteWebAuthnAuthenticationInput */
IdentityAuthenticationCompleteWebAuthnAuthenticationInputSchema = z.object({
  assertionResponse: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCompleteWebAuthnRegistrationInput */
IdentityAuthenticationCompleteWebAuthnRegistrationInputSchema = z.object({
  attestationResponse: z.string().nullable().optional(),
  friendlyName: z.string().nullable().optional(),
  isPasswordless: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationConsumeMagicLinkInput */
IdentityAuthenticationConsumeMagicLinkInputSchema = z.object({
  token: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
  deviceFingerprint: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCreateApiKeyCommand */
IdentityAuthenticationCreateApiKeyCommandSchema = z.object({
  name: z.string().nullable(),
  scopes: z.array(z.string()).nullable(),
  expiresAt: z.string().datetime().nullable().optional(),
  ipWhitelist: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCreateApiKeyOutput */
IdentityAuthenticationCreateApiKeyOutputSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  apiKey: z.string().nullable().optional(),
  keyPrefix: z.string().nullable().optional(),
  scopes: z.array(z.string()).nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationCreateRoleInput */
IdentityAuthenticationCreateRoleInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCreateServiceAccountInput */
IdentityAuthenticationCreateServiceAccountInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  scopes: z.string().nullable().optional(),
  allowedIpAddresses: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationDeviceInfo */
IdentityAuthenticationDeviceInfoSchema = z.object({
  fingerprint: z.string().nullable().optional(),
  deviceId: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  deviceName: z.string().nullable().optional(),
  deviceType: z.string().nullable().optional(),
  operatingSystem: z.string().nullable().optional(),
  osVersion: z.string().nullable().optional(),
  browser: z.string().nullable().optional(),
  browserVersion: z.string().nullable().optional(),
  screenResolution: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  isMobile: z.boolean().optional(),
  isBot: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationDisableMfaInput */
IdentityAuthenticationDisableMfaInputSchema = z.object({
  password: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationDiscordAuthorizeInput */
IdentityAuthenticationDiscordAuthorizeInputSchema = z.object({
  redirectUri: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationDiscordCallbackInput */
IdentityAuthenticationDiscordCallbackInputSchema = z.object({
  code: z.string().min(1),
  state: z.string().min(1),
  redirectUri: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationDiscordLinkAuthorizeInput */
IdentityAuthenticationDiscordLinkAuthorizeInputSchema = z.object({
  redirectUri: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationDiscordLinkAuthorizeOutput */
IdentityAuthenticationDiscordLinkAuthorizeOutputSchema = z.object({
  authUrl: z.string().nullable(),
  state: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationDiscordLinkCallbackInput */
IdentityAuthenticationDiscordLinkCallbackInputSchema = z.object({
  code: z.string().min(1),
  state: z.string().min(1),
  redirectUri: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationDiscordSignInOutput */
IdentityAuthenticationDiscordSignInOutputSchema = z.object({
  authUrl: z.string().nullable(),
  state: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationEmailVerificationOutput */
IdentityAuthenticationEmailVerificationOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationEmailVerificationResult */
IdentityAuthenticationEmailVerificationResultSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  verifiedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationGitHubSignInOutput */
IdentityAuthenticationGitHubSignInOutputSchema = z.object({
  authUrl: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationGoogleIdTokenInput */
IdentityAuthenticationGoogleIdTokenInputSchema = z.object({
  idToken: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationJwtKeyInfo */
IdentityAuthenticationJwtKeyInfoSchema = z.object({
  keyId: z.string().nullable().optional(),
  algorithm: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  validFrom: z.string().datetime().optional(),
  expiresAt: z.string().datetime().optional(),
  rotatedAt: z.string().datetime().nullable().optional(),
  rotationReason: z.string().nullable().optional(),
  keyVersion: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationLinkGoogleAccountInput */
IdentityAuthenticationLinkGoogleAccountInputSchema = z.object({
  idToken: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationLocalSignInInput */
IdentityAuthenticationLocalSignInInputSchema = z.object({
  username: z.string().nullable().optional(),
  email: z.string().email().min(1),
  password: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
  deviceFingerprint: z.string().nullable().optional(),
  emailOrUsername: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationLocalSignUpInput */
IdentityAuthenticationLocalSignUpInputSchema = z.object({
  username: z.string().min(1),
  email: z.string().email().min(1),
  password: z.string().min(8),
  tenantId: z.string().uuid().nullable().optional(),
  firstName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationLocationInfo */
IdentityAuthenticationLocationInfoSchema = z.object({
  ipAddress: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  countryCode: z.string().nullable().optional(),
  region: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  timezone: z.string().nullable().optional(),
  isp: z.string().nullable().optional(),
  organization: z.string().nullable().optional(),
  isProxy: z.boolean().nullable().optional(),
  isHosting: z.boolean().nullable().optional(),
  displayLocation: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationLockServiceAccountInput */
IdentityAuthenticationLockServiceAccountInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationMagicLinkRequestResult */
IdentityAuthenticationMagicLinkRequestResultSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  expiresInMinutes: z.number().int().optional(),
  developmentPreviewToken: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationMfaConfigurationOutput */
IdentityAuthenticationMfaConfigurationOutputSchema = z.object({
  isEnabled: z.boolean().optional(),
  enabledMethods: z.array(z.string()).nullable().optional(),
  enabledAt: z.string().datetime().nullable().optional(),
  backupCodesRemaining: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationMfaErrorOutput */
IdentityAuthenticationMfaErrorOutputSchema = z.object({
  error: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationMfaMethod */
IdentityAuthenticationMfaMethodSchema = z.enum([
  "Totp",
  "BackupCode",
  "Sms",
  "Email",
  "WebAuthn",
]);

/** Zod schema for IdentityAuthenticationMfaMethodInfo */
IdentityAuthenticationMfaMethodInfoSchema = z.object({
  method: z.lazy(() => IdentityAuthenticationMfaMethodSchema),
  name: z.string().nullable(),
  description: z.string().nullable(),
  isEnabled: z.boolean(),
  isAvailable: z.boolean(),
  priority: z.number().int(),
});

/** Zod schema for IdentityAuthenticationMfaMethodsOutput */
IdentityAuthenticationMfaMethodsOutputSchema = z.object({
  methods: z
    .array(z.lazy(() => IdentityAuthenticationMfaMethodInfoSchema))
    .nullable(),
  defaultMethod: z.lazy(() => IdentityAuthenticationMfaMethodSchema).optional(),
});

/** Zod schema for IdentityAuthenticationMfaSetupOutput */
IdentityAuthenticationMfaSetupOutputSchema = z.object({
  isSuccess: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  secretKey: z.string().nullable().optional(),
  qrCodeData: z.string().nullable().optional(),
  qrCodeUri: z.string().nullable().optional(),
  backupCodes: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityAuthenticationMfaSuccessOutput */
IdentityAuthenticationMfaSuccessOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationMfaVerificationOutput */
IdentityAuthenticationMfaVerificationOutputSchema = z.object({
  isValid: z.boolean().optional(),
  accessToken: z.string().nullable().optional(),
  refreshToken: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationOAuth2ErrorOutput */
IdentityAuthenticationOAuth2ErrorOutputSchema = z.object({
  error: z.string().nullable().optional(),
  errorDescription: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordChangeInput */
IdentityAuthenticationPasswordChangeInputSchema = z.object({
  currentPassword: z.string().min(1),
  newPassword: z.string().min(8),
  confirmPassword: z.string().min(1),
  revokeOtherSessions: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordChangeResult */
IdentityAuthenticationPasswordChangeResultSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  sessionsRevoked: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordResetRequestResult */
IdentityAuthenticationPasswordResetRequestResultSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  expiresInMinutes: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordResetResult */
IdentityAuthenticationPasswordResetResultSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationPatchServiceAccountInput */
IdentityAuthenticationPatchServiceAccountInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  scopes: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRefreshTokenInput */
IdentityAuthenticationRefreshTokenInputSchema = z.object({
  refreshToken: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRemoveRoleFromUserInput */
IdentityAuthenticationRemoveRoleFromUserInputSchema = z.object({
  userId: z.string().uuid().optional(),
  roleId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationRequestMagicLinkInput */
IdentityAuthenticationRequestMagicLinkInputSchema = z.object({
  email: z.string().email().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRequestPasswordResetInput */
IdentityAuthenticationRequestPasswordResetInputSchema = z.object({
  email: z.string().email().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRevokeApiKeyInput */
IdentityAuthenticationRevokeApiKeyInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRevokeRefreshTokenInput */
IdentityAuthenticationRevokeRefreshTokenInputSchema = z.object({
  token: z.string().min(1),
  ipAddress: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRiskLevel */
IdentityAuthenticationRiskLevelSchema = z.enum([
  "Low",
  "Medium",
  "High",
  "Critical",
]);

/** Zod schema for IdentityAuthenticationRotateKeyInput */
IdentityAuthenticationRotateKeyInputSchema = z.object({
  reason: z.string().nullable().optional(),
  validityDays: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSecretRotationOutput */
IdentityAuthenticationSecretRotationOutputSchema = z.object({
  clientSecret: z.string().nullable().optional(),
  warning: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSendEmailVerificationInput */
IdentityAuthenticationSendEmailVerificationInputSchema = z.object({
  email: z.string().email().min(1),
});

/** Zod schema for IdentityAuthenticationServiceAccountAuditEntry */
IdentityAuthenticationServiceAccountAuditEntrySchema = z.object({
  id: z.string().uuid().optional(),
  timestamp: z.string().datetime().optional(),
  action: z.string().nullable().optional(),
  performedBy: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  details: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountAuditLogOutput */
IdentityAuthenticationServiceAccountAuditLogOutputSchema = z.object({
  serviceAccountId: z.string().uuid().optional(),
  entries: z
    .array(z.lazy(() => IdentityAuthenticationServiceAccountAuditEntrySchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  page: z.number().int().optional(),
  pageSize: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountCreatedOutput */
IdentityAuthenticationServiceAccountCreatedOutputSchema = z.object({
  id: z.string().uuid().optional(),
  clientId: z.string().nullable().optional(),
  clientSecret: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  scopes: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  warning: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountOutput */
IdentityAuthenticationServiceAccountOutputSchema = z.object({
  id: z.string().uuid().optional(),
  clientId: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  scopes: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  isLocked: z.boolean().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  createdBy: z.string().nullable().optional(),
  lastAuthenticatedAt: z.string().datetime().nullable().optional(),
  authenticationCount: z.number().int().optional(),
  secretRotationCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationSessionOutput */
IdentityAuthenticationSessionOutputSchema = z.object({
  id: z.string().uuid().optional(),
  deviceInfo: z.lazy(() => IdentityAuthenticationDeviceInfoSchema).optional(),
  location: z.lazy(() => IdentityAuthenticationLocationInfoSchema).optional(),
  ipAddress: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  lastUsedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().optional(),
  isTrustedDevice: z.boolean().optional(),
  isCurrent: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationSessionSecurityAnalysis */
IdentityAuthenticationSessionSecurityAnalysisSchema = z.object({
  sessionId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  isSuspicious: z.boolean().optional(),
  unusualActivityDetected: z.boolean().optional(),
  riskScore: z.number().int().optional(),
  activeSessionCount: z.number().int().optional(),
  totalDeviceCount: z.number().int().optional(),
  riskLevel: z.lazy(() => IdentityAuthenticationRiskLevelSchema).optional(),
  securityFlags: z.array(z.string()).nullable().optional(),
  riskFactors: z.array(z.string()).nullable().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
  analyzedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationSessionSuccessOutput */
IdentityAuthenticationSessionSuccessOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationSessionTerminationOutput */
IdentityAuthenticationSessionTerminationOutputSchema = z.object({
  message: z.string().nullable(),
  terminatedCount: z.number().int(),
});

/** Zod schema for IdentityAuthenticationSignInOutput */
IdentityAuthenticationSignInOutputSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  accessToken: z.string().nullable().optional(),
  refreshToken: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  accessTokenExpiresAt: z.string().datetime().optional(),
  refreshTokenExpiresAt: z.string().datetime().optional(),
  expiresIn: z.number().int().optional(),
  userId: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  sessionId: z.string().uuid().optional(),
  tempToken: z.string().nullable().optional(),
  mfaToken: z.string().nullable().optional(),
  user: z.lazy(() => IdentityAuthenticationUserSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  availableTenants: z
    .array(z.lazy(() => TenantInfoSchema))
    .nullable()
    .optional(),
  requiresMfa: z.boolean().optional(),
  mfaSessionId: z.string().nullable().optional(),
  requiresStepUp: z.boolean().optional(),
  stepUpToken: z.string().nullable().optional(),
  stepUpExpiresAt: z.string().datetime().nullable().optional(),
  riskLevel: z.lazy(() => IdentityAuthenticationRiskLevelSchema).optional(),
  riskFactors: z.array(z.string()).nullable().optional(),
  availableMethods: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSmsMfaSetupInput */
IdentityAuthenticationSmsMfaSetupInputSchema = z.object({
  phoneNumber: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationSmsMfaSetupOutput */
IdentityAuthenticationSmsMfaSetupOutputSchema = z.object({
  message: z.string().nullable(),
  phoneNumberMasked: z.string().nullable(),
  expiresInSeconds: z.number().int(),
});

/** Zod schema for IdentityAuthenticationTrustDeviceInput */
IdentityAuthenticationTrustDeviceInputSchema = z.object({
  deviceName: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationTrustedDeviceOutput */
IdentityAuthenticationTrustedDeviceOutputSchema = z.object({
  id: z.string().uuid().optional(),
  deviceName: z.string().nullable().optional(),
  deviceInfo: z.lazy(() => IdentityAuthenticationDeviceInfoSchema).optional(),
  trustedAt: z.string().datetime().optional(),
  lastUsedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateCredentialNameInput */
IdentityAuthenticationUpdateCredentialNameInputSchema = z.object({
  friendlyName: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateRoleInput */
IdentityAuthenticationUpdateRoleInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  isActive: z.boolean().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateScopesInput */
IdentityAuthenticationUpdateScopesInputSchema = z.object({
  scopes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUser */
IdentityAuthenticationUserSchema = z.object({
  id: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  username: z.string().nullable().optional(),
  firstName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
  emailVerified: z.boolean().optional(),
  phoneNumberVerified: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  lastLoginAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationVerifyEmailInput */
IdentityAuthenticationVerifyEmailInputSchema = z.object({
  token: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationVerifyMfaInput */
IdentityAuthenticationVerifyMfaInputSchema = z.object({
  userId: z.string().uuid().optional(),
  code: z.string().min(1),
  method: z.lazy(() => IdentityAuthenticationMfaMethodSchema).optional(),
});

/** Zod schema for IdentityAuthenticationWeb3ChallengeInput */
IdentityAuthenticationWeb3ChallengeInputSchema = z.object({
  walletAddress: z.string().min(1),
  chainId: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWeb3ChallengeOutput */
IdentityAuthenticationWeb3ChallengeOutputSchema = z.object({
  challenge: z.string().nullable().optional(),
  nonce: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationWeb3VerifyInput */
IdentityAuthenticationWeb3VerifyInputSchema = z.object({
  walletAddress: z.string().min(1),
  signature: z.string().min(1),
  nonce: z.string().min(1),
  chainId: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
  deviceFingerprint: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticationOptionsResult */
IdentityAuthenticationWebAuthnAuthenticationOptionsResultSchema = z.object({
  success: z.boolean().optional(),
  error: z.string().nullable().optional(),
  sessionId: z.string().nullable().optional(),
  optionsJson: z.string().nullable().optional(),
  options: z.lazy(() => Fido2NetLibAssertionOptionsSchema).optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticationResult */
IdentityAuthenticationWebAuthnAuthenticationResultSchema = z.object({
  success: z.boolean().optional(),
  error: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  credentialId: z.string().uuid().nullable().optional(),
  isPasswordless: z.boolean().optional(),
  email: z.string().nullable().optional(),
  accessToken: z.string().nullable().optional(),
  refreshToken: z.string().nullable().optional(),
  accessTokenExpiresAt: z.string().datetime().nullable().optional(),
  refreshTokenExpiresAt: z.string().datetime().nullable().optional(),
  expiresIn: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticatorType */
IdentityAuthenticationWebAuthnAuthenticatorTypeSchema = z.enum([
  "Platform",
  "CrossPlatform",
]);

/** Zod schema for IdentityAuthenticationWebAuthnCredentialInfo */
IdentityAuthenticationWebAuthnCredentialInfoSchema = z.object({
  id: z.string().uuid().optional(),
  friendlyName: z.string().nullable().optional(),
  authenticatorType: z
    .lazy(() => IdentityAuthenticationWebAuthnAuthenticatorTypeSchema)
    .optional(),
  createdAt: z.string().datetime().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  isPasswordless: z.boolean().optional(),
  isDefault: z.boolean().optional(),
  backedUp: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnCredentialVerifyResult */
IdentityAuthenticationWebAuthnCredentialVerifyResultSchema = z.object({
  success: z.boolean().optional(),
  error: z.string().nullable().optional(),
  isValid: z.boolean().optional(),
  isExpired: z.boolean().optional(),
  isRevoked: z.boolean().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  signatureCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnRegistrationOptionsResult */
IdentityAuthenticationWebAuthnRegistrationOptionsResultSchema = z.object({
  success: z.boolean().optional(),
  error: z.string().nullable().optional(),
  sessionId: z.string().nullable().optional(),
  optionsJson: z.string().nullable().optional(),
  options: z.lazy(() => Fido2NetLibCredentialCreateOptionsSchema).optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnRegistrationResult */
IdentityAuthenticationWebAuthnRegistrationResultSchema = z.object({
  success: z.boolean().optional(),
  error: z.string().nullable().optional(),
  credentialId: z.string().uuid().nullable().optional(),
  friendlyName: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnStatusOutput */
IdentityAuthenticationWebAuthnStatusOutputSchema = z.object({
  isEnabled: z.boolean().optional(),
  credentialCount: z.number().int().optional(),
  hasPasswordlessCredential: z.boolean().optional(),
  hasPlatformAuthenticator: z.boolean().optional(),
  hasSecurityKey: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationAccessReviewCampaign */
IdentityAuthorizationAccessReviewCampaignSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  reviewType: z
    .lazy(() => IdentityAuthorizationAccessReviewTypeSchema)
    .optional(),
  scope: z.lazy(() => IdentityAuthorizationAccessReviewScopeSchema).optional(),
  scopeFilter: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  status: z
    .lazy(() => IdentityAuthorizationAccessReviewStatusSchema)
    .optional(),
  totalItems: z.number().int().optional(),
  reviewedItems: z.number().int().optional(),
  approvedItems: z.number().int().optional(),
  revokedItems: z.number().int().optional(),
  createdBy: z.string().uuid().optional(),
  completedBy: z.string().uuid().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  autoRevokeOnNoResponse: z.boolean().optional(),
  reminderFrequencyDays: z.number().int().optional(),
  notificationTemplate: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  items: z
    .array(z.lazy(() => IdentityAuthorizationAccessReviewItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityAuthorizationAccessReviewDecision */
IdentityAuthorizationAccessReviewDecisionSchema = z.enum([
  "None",
  "Approve",
  "Revoke",
  "ModifyAndApprove",
]);

/** Zod schema for IdentityAuthorizationAccessReviewItem */
IdentityAuthorizationAccessReviewItemSchema = z.object({
  id: z.string().uuid().optional(),
  campaignId: z.string().uuid().optional(),
  reviewerId: z.string().uuid().optional(),
  subjectUserId: z.string().uuid().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  permissionDetails: z.string().nullable().optional(),
  status: z
    .lazy(() => IdentityAuthorizationAccessReviewItemStatusSchema)
    .optional(),
  decision: z
    .lazy(() => IdentityAuthorizationAccessReviewDecisionSchema)
    .optional(),
  decisionReason: z.string().nullable().optional(),
  reviewedAt: z.string().datetime().nullable().optional(),
  lastReminderSent: z.string().datetime().nullable().optional(),
  reminderCount: z.number().int().optional(),
  reviewerNotes: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  campaign: z
    .lazy(() => IdentityAuthorizationAccessReviewCampaignSchema)
    .optional(),
});

/** Zod schema for IdentityAuthorizationAccessReviewItemStatus */
IdentityAuthorizationAccessReviewItemStatusSchema = z.enum([
  "None",
  "Pending",
  "Reviewed",
  "Approved",
  "Revoked",
  "Expired",
]);

/** Zod schema for IdentityAuthorizationAccessReviewScope */
IdentityAuthorizationAccessReviewScopeSchema = z.enum([
  "None",
  "AllUsers",
  "Department",
  "Team",
  "Role",
  "Resource",
  "HighPrivilege",
  "External",
  "Custom",
]);

/** Zod schema for IdentityAuthorizationAccessReviewStatus */
IdentityAuthorizationAccessReviewStatusSchema = z.enum([
  "None",
  "Draft",
  "Active",
  "InProgress",
  "Completed",
  "Expired",
]);

/** Zod schema for IdentityAuthorizationAccessReviewType */
IdentityAuthorizationAccessReviewTypeSchema = z.enum([
  "None",
  "PermissionReview",
  "RoleReview",
  "ResourceAccessReview",
  "UserAccessReview",
  "ComplianceAttestation",
]);

/** Zod schema for IdentityAuthorizationCommandsCreateAccessReviewCampaignCommand */
IdentityAuthorizationCommandsCreateAccessReviewCampaignCommandSchema = z.object(
  {
    name: z.string().nullable().optional(),
    description: z.string().nullable().optional(),
    tenantId: z.string().uuid().nullable().optional(),
    reviewType: z
      .lazy(() => IdentityAuthorizationAccessReviewTypeSchema)
      .optional(),
    startDate: z.string().datetime().optional(),
    endDate: z.string().datetime().optional(),
    createdBy: z.string().uuid().optional(),
  },
);

/** Zod schema for IdentityAuthorizationCommandsCreateSoDRuleCommand */
IdentityAuthorizationCommandsCreateSoDRuleCommandSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  conflictingPermissions: z.array(z.string()).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  ruleType: z.lazy(() => IdentityAuthorizationSoDRuleTypeSchema).optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationCommandsDelegatePermissionsCommand */
IdentityAuthorizationCommandsDelegatePermissionsCommandSchema = z.object({
  delegatorUserId: z.string().uuid().optional(),
  delegateUserId: z.string().uuid().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  canSubDelegate: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  usageLimit: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationCommandsGrantDelegatedAdminCommand */
IdentityAuthorizationCommandsGrantDelegatedAdminCommandSchema = z.object({
  adminUserId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  managedResourceTypes: z.array(z.string()).nullable().optional(),
  managedUserIds: z.array(z.string().uuid()).nullable().optional(),
  allowedOperations: z.array(z.string()).nullable().optional(),
  organizationalUnitId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationCommandsRequestJitElevationCommand */
IdentityAuthorizationCommandsRequestJitElevationCommandSchema = z.object({
  requesterId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  permission: z.string().nullable().optional(),
  justification: z.string().nullable().optional(),
  durationMinutes: z.number().int().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersApproveElevationInput */
IdentityAuthorizationControllersApproveElevationInputSchema = z.object({
  reviewerId: z.string().uuid().optional(),
  comments: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersApproveItemInput */
IdentityAuthorizationControllersApproveItemInputSchema = z.object({
  reason: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersCompleteCampaignInput */
IdentityAuthorizationControllersCompleteCampaignInputSchema = z.object({
  completedBy: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthorizationControllersDenyElevationInput */
IdentityAuthorizationControllersDenyElevationInputSchema = z.object({
  reviewerId: z.string().uuid().optional(),
  comments: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersGrantExceptionInput */
IdentityAuthorizationControllersGrantExceptionInputSchema = z.object({
  approvedBy: z.string().uuid().optional(),
  justification: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersResolveViolationInput */
IdentityAuthorizationControllersResolveViolationInputSchema = z.object({
  resolvedBy: z.string().uuid().optional(),
  action: z
    .lazy(() => IdentityAuthorizationSoDResolutionActionSchema)
    .optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersRevokeElevationInput */
IdentityAuthorizationControllersRevokeElevationInputSchema = z.object({
  revokedBy: z.string().uuid().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersRevokeItemInput */
IdentityAuthorizationControllersRevokeItemInputSchema = z.object({
  reason: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationControllersUpdateSoDRuleInput */
IdentityAuthorizationControllersUpdateSoDRuleInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  conflictingPermissions: z.array(z.string()).nullable().optional(),
  ruleType: z.lazy(() => IdentityAuthorizationSoDRuleTypeSchema).optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationDeclineInvitationInput */
IdentityAuthorizationDeclineInvitationInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationDelegatedAdminScope */
IdentityAuthorizationDelegatedAdminScopeSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  adminUserId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  scopeType: z
    .lazy(() => IdentityAuthorizationDelegatedAdminScopeTypeSchema)
    .optional(),
  allowedResourceTypes: z.string().nullable().optional(),
  allowedResourceIds: z.string().nullable().optional(),
  allowedUserIds: z.string().nullable().optional(),
  allowedDepartments: z.string().nullable().optional(),
  allowedTeams: z.string().nullable().optional(),
  allowedRoles: z.string().nullable().optional(),
  grantablePermissions: z.string().nullable().optional(),
  deniedPermissions: z.string().nullable().optional(),
  canManageUsers: z.boolean().optional(),
  canManagePermissions: z.boolean().optional(),
  canManageResources: z.boolean().optional(),
  canViewAuditLogs: z.boolean().optional(),
  startsAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  createdBy: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationDelegatedAdminScopeType */
IdentityAuthorizationDelegatedAdminScopeTypeSchema = z.enum([
  "None",
  "Department",
  "Team",
  "Role",
  "Resource",
  "Custom",
]);

/** Zod schema for IdentityAuthorizationDenyTenantPermissionCommand */
IdentityAuthorizationDenyTenantPermissionCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  userId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  deniedBy: z.string().uuid(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationEffectivePermission */
IdentityAuthorizationEffectivePermissionSchema = z.object({
  permission: z.string().nullable(),
  source: z.string().nullable(),
  expiresAt: z.string().datetime().nullable().optional(),
  grantedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationEffectivePermissionsOutput */
IdentityAuthorizationEffectivePermissionsOutputSchema = z.object({
  userId: z.string().uuid(),
  resourceType: z.string().nullable(),
  resourceId: z.string().uuid(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationEffectivePermissionSchema))
    .nullable(),
  isOwner: z.boolean().optional(),
  hasFullAccess: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationElevationRequestStatus */
IdentityAuthorizationElevationRequestStatusSchema = z.enum([
  "None",
  "Pending",
  "Approved",
  "Denied",
  "Active",
  "Expired",
  "Revoked",
]);

/** Zod schema for IdentityAuthorizationGetPendingResourceInvitationsOutput */
IdentityAuthorizationGetPendingResourceInvitationsOutputSchema = z.object({
  invitations: z
    .array(z.lazy(() => IdentityAuthorizationResourceInvitationSchema))
    .nullable(),
  totalCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthorizationGetResourceInvitationOutput */
IdentityAuthorizationGetResourceInvitationOutputSchema = z.object({
  invitation: z.lazy(() => IdentityAuthorizationResourceInvitationSchema),
});

/** Zod schema for IdentityAuthorizationGetResourceUsersOutput */
IdentityAuthorizationGetResourceUsersOutputSchema = z.object({
  resourceType: z.string().nullable(),
  resourceId: z.string().nullable(),
  users: z
    .array(z.lazy(() => IdentityAuthorizationResourceUserSchema))
    .nullable(),
  totalCount: z.number().int().optional(),
  ownerCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthorizationGetTenantPermissionsOutput */
IdentityAuthorizationGetTenantPermissionsOutputSchema = z.object({
  userId: z.string().uuid(),
  tenantId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  isTenantAdmin: z.boolean().optional(),
  isSystemAdmin: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationGrantTenantPermissionCommand */
IdentityAuthorizationGrantTenantPermissionCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  userId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  grantedBy: z.string().uuid(),
  expiresAt: z.string().datetime().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationHasPermissionOutput */
IdentityAuthorizationHasPermissionOutputSchema = z.object({
  hasPermission: z.boolean(),
  userId: z.string().uuid(),
  resourceType: z.string().nullable(),
  resourceId: z.string().uuid(),
  permission: z.string().nullable(),
  denialReason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationImpactSeverity */
IdentityAuthorizationImpactSeveritySchema = z.enum([
  "Low",
  "Medium",
  "High",
  "Critical",
]);

/** Zod schema for IdentityAuthorizationInvitationActionResult */
IdentityAuthorizationInvitationActionResultSchema = z.object({
  success: z.boolean().optional(),
  invitationId: z.string().uuid().optional(),
  status: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationJitElevationInput */
IdentityAuthorizationJitElevationInputSchema = z.object({
  id: z.string().uuid().optional(),
  requesterId: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  permission: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  justification: z.string().nullable().optional(),
  durationMinutes: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  status: z
    .lazy(() => IdentityAuthorizationElevationRequestStatusSchema)
    .optional(),
  reviewerId: z.string().uuid().nullable().optional(),
  reviewedAt: z.string().datetime().nullable().optional(),
  reviewerComments: z.string().nullable().optional(),
  activatedAt: z.string().datetime().nullable().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  revokedBy: z.string().uuid().nullable().optional(),
  revocationReason: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionAnalyticsReport */
IdentityAuthorizationPermissionAnalyticsReportSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  periodStart: z.string().datetime().optional(),
  periodEnd: z.string().datetime().optional(),
  topPermissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionUsageMetricsSchema))
    .nullable()
    .optional(),
  topUsers: z
    .array(z.lazy(() => IdentityAuthorizationUserActivitySummarySchema))
    .nullable()
    .optional(),
  anomalies: z
    .array(z.lazy(() => IdentityAuthorizationPermissionAnomalySchema))
    .nullable()
    .optional(),
  totalGrants: z.number().int().optional(),
  totalRevokes: z.number().int().optional(),
  activeUsers: z.number().int().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionAnomaly */
IdentityAuthorizationPermissionAnomalySchema = z.object({
  userId: z.string().uuid().optional(),
  anomalyType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  detectedAt: z.string().datetime().optional(),
  severity: z.lazy(() => IdentityAuthorizationImpactSeveritySchema).optional(),
});

/** Zod schema for IdentityAuthorizationPermissionDelegation */
IdentityAuthorizationPermissionDelegationSchema = z.object({
  id: z.string().uuid().optional(),
  delegatorUserId: z.string().uuid().optional(),
  delegateUserId: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  resourceId: z.string().uuid().nullable().optional(),
  delegatedPermissions: z.array(z.string()).nullable().optional(),
  startsAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  canSubDelegate: z.boolean().optional(),
  isActive: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  conditions: z.string().nullable().optional(),
  usageLimit: z.number().int().nullable().optional(),
  usageCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionTrend */
IdentityAuthorizationPermissionTrendSchema = z.object({
  date: z.string().datetime().optional(),
  grants: z.number().int().optional(),
  revokes: z.number().int().optional(),
  activePermissions: z.number().int().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionType */
IdentityAuthorizationPermissionTypeSchema = z.enum([
  "Read",
  "Comment",
  "Reply",
  "Vote",
  "Share",
  "Report",
  "Follow",
  "Bookmark",
  "React",
  "Subscribe",
  "Mention",
  "Tag",
  "Categorize",
  "Collection",
  "Series",
  "CrossReference",
  "Translate",
  "Version",
  "Template",
  "Create",
  "Draft",
  "Submit",
  "Withdraw",
  "Archive",
  "Restore",
  "Delete",
  "HardDelete",
  "Backup",
  "Migrate",
  "Clone",
  "Edit",
  "Proofread",
  "FactCheck",
  "StyleGuide",
  "Plagiarism",
  "Seo",
  "Accessibility",
  "Legal",
  "Brand",
  "Guidelines",
  "Approve",
  "Reject",
  "RequestRevision",
  "Escalate",
  "Override",
  "Delegate",
  "FastTrack",
  "BatchApprove",
  "ConditionalApprove",
  "RequireReview",
  "Publish",
  "Unpublish",
  "Schedule",
  "SetPublishDate",
  "Visibility",
  "Feature",
  "Pin",
  "Sticky",
  "Highlight",
  "Promote",
  "Moderate",
  "Hide",
  "Flag",
  "Warn",
  "Suspend",
  "Ban",
  "Quarantine",
  "Review",
  "Investigate",
  "EscalateModeration",
  "Invite",
  "Assign",
  "Collaborate",
  "CoAuthor",
  "Contribute",
  "Suggest",
  "Track",
  "Merge",
  "Resolve",
  "Coordinate",
  "Score",
  "Rate",
  "Benchmark",
  "Metrics",
  "Analytics",
  "Performance",
  "Feedback",
  "Audit",
  "Standards",
  "Improvement",
  "Monetize",
  "Pricing",
  "Paywall",
  "Manage",
  "Admin",
  "Execute",
  "Export",
  "Import",
  "SystemAdmin",
  "TenantAdmin",
  "UserManagement",
  "Configure",
]);

/** Zod schema for IdentityAuthorizationPermissionUpdateResult */
IdentityAuthorizationPermissionUpdateResultSchema = z.object({
  success: z.boolean().optional(),
  userId: z.string().uuid().optional(),
  updatedPermissions: z.array(z.string()).nullable().optional(),
  errorMessage: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionUsageMetrics */
IdentityAuthorizationPermissionUsageMetricsSchema = z.object({
  permission: z.string().nullable().optional(),
  usageCount: z.number().int().optional(),
  uniqueUsers: z.number().int().optional(),
  lastUsed: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthorizationRemoveDenyPermissionsCommand */
IdentityAuthorizationRemoveDenyPermissionsCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  userId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  removedBy: z.string().uuid(),
});

/** Zod schema for IdentityAuthorizationRemoveUserAccessCommand */
IdentityAuthorizationRemoveUserAccessCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  resourceType: z.string().nullable(),
  resourceId: z.string().nullable(),
  targetUserId: z.string().uuid(),
  removedByUserId: z.string().uuid(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationResourceAccessPattern */
IdentityAuthorizationResourceAccessPatternSchema = z.object({
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().uuid().optional(),
  accessCount: z.number().int().optional(),
  uniqueUsers: z.number().int().optional(),
});

/** Zod schema for IdentityAuthorizationResourceInvitation */
IdentityAuthorizationResourceInvitationSchema = z.object({
  invitationId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  message: z.string().nullable().optional(),
  invitedByUserName: z.string().nullable().optional(),
  invitedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  status: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationResourceUser */
IdentityAuthorizationResourceUserSchema = z.object({
  userId: z.string().uuid(),
  resourceType: z.string().nullable(),
  resourceId: z.string().nullable(),
  permissions: z.array(z.string()).nullable(),
  grantedAt: z.string().datetime(),
  grantedByUserId: z.string().uuid(),
  expiresAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  isOwner: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationRevokeTenantPermissionCommand */
IdentityAuthorizationRevokeTenantPermissionCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  userId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  revokedBy: z.string().uuid(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationSetGlobalDefaultPermissionsCommand */
IdentityAuthorizationSetGlobalDefaultPermissionsCommandSchema = z.object({
  permissions: z.array(z.string()).nullable(),
  setBy: z.string().uuid(),
});

/** Zod schema for IdentityAuthorizationSetTenantDefaultPermissionsCommand */
IdentityAuthorizationSetTenantDefaultPermissionsCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  permissions: z.array(z.string()).nullable(),
  setBy: z.string().uuid(),
});

/** Zod schema for IdentityAuthorizationShareResourceCommand */
IdentityAuthorizationShareResourceCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  resourceType: z.string().nullable(),
  resourceId: z.string().nullable(),
  userIds: z.array(z.string().uuid()).nullable(),
  userEmails: z.array(z.string()).nullable().optional(),
  permissions: z.array(z.string()).nullable(),
  grantedByUserId: z.string().uuid(),
  expiresAt: z.string().datetime().nullable().optional(),
  message: z.string().nullable().optional(),
  requireAcceptance: z.boolean().optional(),
  notifyUsers: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationShareResult */
IdentityAuthorizationShareResultSchema = z.object({
  success: z.boolean().optional(),
  isNewUser: z.boolean().optional(),
  userId: z.string().uuid().nullable().optional(),
  invitationId: z.string().uuid().nullable().optional(),
  email: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  invitationLink: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationSoDResolutionAction */
IdentityAuthorizationSoDResolutionActionSchema = z.enum([
  "None",
  "RevokePermission",
  "RevokeRole",
  "GrantException",
  "ImplementCompensatingControl",
  "TransferOwnership",
  "NoAction",
]);

/** Zod schema for IdentityAuthorizationSoDRule */
IdentityAuthorizationSoDRuleSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  ruleType: z.lazy(() => IdentityAuthorizationSoDRuleTypeSchema).optional(),
  severity: z.lazy(() => IdentityAuthorizationSoDSeveritySchema).optional(),
  isEnabled: z.boolean().optional(),
  conflictingPermissions: z.string().nullable().optional(),
  conflictingRoles: z.string().nullable().optional(),
  conflictingResources: z.string().nullable().optional(),
  allowedExceptions: z.string().nullable().optional(),
  requireApproval: z.boolean().optional(),
  approverRoles: z.string().nullable().optional(),
  mitigationStrategy: z.string().nullable().optional(),
  violationCount: z.number().int().optional(),
  lastViolationDetected: z.string().datetime().nullable().optional(),
  createdBy: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  violations: z
    .array(z.lazy(() => IdentityAuthorizationSoDViolationSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityAuthorizationSoDRuleType */
IdentityAuthorizationSoDRuleTypeSchema = z.enum([
  "None",
  "PermissionConflict",
  "RoleConflict",
  "ResourceConflict",
  "BusinessProcessConflict",
  "FunctionalConflict",
]);

/** Zod schema for IdentityAuthorizationSoDSeverity */
IdentityAuthorizationSoDSeveritySchema = z.enum([
  "None",
  "Low",
  "Medium",
  "High",
  "Critical",
]);

/** Zod schema for IdentityAuthorizationSoDViolation */
IdentityAuthorizationSoDViolationSchema = z.object({
  id: z.string().uuid().optional(),
  ruleId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema).optional(),
  status: z
    .lazy(() => IdentityAuthorizationSoDViolationStatusSchema)
    .optional(),
  violationDetails: z.string().nullable().optional(),
  conflictingItems: z.string().nullable().optional(),
  detectedAt: z.string().datetime().optional(),
  detectedBy: z.string().uuid().nullable().optional(),
  resolvedAt: z.string().datetime().nullable().optional(),
  resolvedBy: z.string().uuid().nullable().optional(),
  resolutionNotes: z.string().nullable().optional(),
  resolutionAction: z
    .lazy(() => IdentityAuthorizationSoDResolutionActionSchema)
    .optional(),
  isException: z.boolean().optional(),
  exceptionJustification: z.string().nullable().optional(),
  approvedBy: z.string().uuid().nullable().optional(),
  approvedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  rule: z.lazy(() => IdentityAuthorizationSoDRuleSchema).optional(),
});

/** Zod schema for IdentityAuthorizationSoDViolationStatus */
IdentityAuthorizationSoDViolationStatusSchema = z.enum([
  "None",
  "Active",
  "Acknowledged",
  "Mitigated",
  "Resolved",
  "Excepted",
  "FalsePositive",
]);

/** Zod schema for IdentityAuthorizationUpdateUserPermissionsCommand */
IdentityAuthorizationUpdateUserPermissionsCommandSchema = z.object({
  tenantId: z.lazy(() => CQRSModelsTenantIdSchema),
  resourceType: z.string().nullable(),
  resourceId: z.string().nullable(),
  targetUserId: z.string().uuid(),
  permissions: z.array(z.string()).nullable(),
  updatedByUserId: z.string().uuid(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthorizationUserActivitySummary */
IdentityAuthorizationUserActivitySummarySchema = z.object({
  userId: z.string().uuid().optional(),
  totalActions: z.number().int().optional(),
  permissionChanges: z.number().int().optional(),
  lastActivity: z.string().datetime().optional(),
});

/** Zod schema for IdentityTenantsAddTenantMemberOutput */
IdentityTenantsAddTenantMemberOutputSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  memberId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityTenantsAddUserMembershipInput */
IdentityTenantsAddUserMembershipInputSchema = z.object({
  tenantId: z.string().uuid().optional(),
  role: z.string().nullable().optional(),
  invitedByEmail: z.string().nullable().optional(),
  requiresAcceptance: z.boolean().optional(),
  inviteeEmail: z.string().nullable().optional(),
  inviteeName: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsArchiveInput */
IdentityTenantsArchiveInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkActivateTenantsCommand */
IdentityTenantsBulkActivateTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkArchiveTenantsCommand */
IdentityTenantsBulkArchiveTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkCreateTenantItem */
IdentityTenantsBulkCreateTenantItemSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  adminEmail: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkCreateTenantsCommand */
IdentityTenantsBulkCreateTenantsCommandSchema = z.object({
  tenants: z
    .array(z.lazy(() => IdentityTenantsBulkCreateTenantItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsBulkDeactivateTenantsCommand */
IdentityTenantsBulkDeactivateTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkDeleteTenantsCommand */
IdentityTenantsBulkDeleteTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
  hardDelete: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsBulkPurgeTenantsCommand */
IdentityTenantsBulkPurgeTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkUndeleteTenantsCommand */
IdentityTenantsBulkUndeleteTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkUpdateTenantItem */
IdentityTenantsBulkUpdateTenantItemSchema = z.object({
  tenantId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkUpdateTenantsCommand */
IdentityTenantsBulkUpdateTenantsCommandSchema = z.object({
  updates: z
    .array(z.lazy(() => IdentityTenantsBulkUpdateTenantItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsCreateTenantInput */
IdentityTenantsCreateTenantInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  adminEmail: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsGetUserMembershipsOutput */
IdentityTenantsGetUserMembershipsOutputSchema = z.object({
  memberships: z
    .array(z.lazy(() => IdentityTenantsUserMembershipSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsMembershipCountOutput */
IdentityTenantsMembershipCountOutputSchema = z.object({
  count: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsRecoverInput */
IdentityTenantsRecoverInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsReplaceTenantMetadataInput */
IdentityTenantsReplaceTenantMetadataInputSchema = z.object({
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  tags: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  businessInfo: z
    .lazy(() => IdentityTenantsUpdateTenantBusinessInfoInputSchema)
    .optional(),
  contactInfo: z
    .lazy(() => IdentityTenantsUpdateTenantContactInfoInputSchema)
    .optional(),
  adminNotes: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsReplaceTenantSettingsInput */
IdentityTenantsReplaceTenantSettingsInputSchema = z.object({
  systemConfiguration: z
    .lazy(() => IdentityTenantsUpdateTenantSystemConfigurationInputSchema)
    .optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  businessRules: z
    .lazy(() => IdentityTenantsUpdateTenantBusinessRulesInputSchema)
    .optional(),
  userInterfaceSettings: z
    .lazy(() => IdentityTenantsUpdateTenantUiSettingsInputSchema)
    .optional(),
  securitySettings: z
    .lazy(() => IdentityTenantsUpdateTenantSecuritySettingsInputSchema)
    .optional(),
  integrationSettings: z
    .lazy(() => IdentityTenantsUpdateTenantIntegrationSettingsInputSchema)
    .optional(),
  systemLimits: z
    .lazy(() => IdentityTenantsUpdateTenantSystemLimitsInputSchema)
    .optional(),
});

/** Zod schema for IdentityTenantsSetTenantMembershipStatusInput */
IdentityTenantsSetTenantMembershipStatusInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsSetTenantMembershipStatusOutput */
IdentityTenantsSetTenantMembershipStatusOutputSchema = z.object({
  success: z.boolean().optional(),
  notFound: z.boolean().optional(),
  message: z.string().nullable().optional(),
  memberId: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsSlugValidation */
IdentityTenantsSlugValidationSchema = z.object({
  isAvailable: z.boolean().optional(),
  isValid: z.boolean().optional(),
  suggestedAlternatives: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenant */
IdentityTenantsTenantSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  isDefault: z.boolean().optional(),
  isArchived: z.boolean().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  tenantMembers: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  tenantDomains: z
    .array(z.lazy(() => IdentityTenantsTenantDomainSchema))
    .nullable()
    .optional(),
  tenantSettings: z.lazy(() => IdentityTenantsTenantSettingsSchema).optional(),
  tenantStatistics: z
    .lazy(() => IdentityTenantsTenantStatisticsSchema)
    .optional(),
  usageTrackingRecords: z
    .array(z.lazy(() => IdentityTenantsUsageTrackingSchema))
    .nullable()
    .optional(),
  name: z.string().min(1).max(100),
  description: z.string().max(500).nullable().optional(),
  isActive: z.boolean().optional(),
  slug: z.string().min(1).max(255),
  adminEmail: z.string().max(255).nullable().optional(),
  canAcceptMembers: z.boolean().optional(),
  activeMemberCount: z.number().int().optional(),
  hasActiveMembers: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsTenantAddress */
IdentityTenantsTenantAddressSchema = z.object({
  street: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantAuditLogEntry */
IdentityTenantsTenantAuditLogEntrySchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  timestamp: z.string().datetime().optional(),
  action: z.string().nullable().optional(),
  actorId: z.string().uuid().nullable().optional(),
  actorName: z.string().nullable().optional(),
  actorEmail: z.string().nullable().optional(),
  beforeValues: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  afterValues: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  ipAddress: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  correlationId: z.string().nullable().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBranding */
IdentityTenantsTenantBrandingSchema = z.object({
  logoUrl: z.string().nullable().optional(),
  faviconUrl: z.string().nullable().optional(),
  primaryColor: z.string().nullable().optional(),
  secondaryColor: z.string().nullable().optional(),
  companyName: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBusinessInfo */
IdentityTenantsTenantBusinessInfoSchema = z.object({
  industry: z.string().nullable().optional(),
  organizationSize: z.string().nullable().optional(),
  tenantType: z.string().nullable().optional(),
  geographicRegion: z.string().nullable().optional(),
  complianceRequirements: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBusinessRules */
IdentityTenantsTenantBusinessRulesSchema = z.object({
  workflowRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  validationRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  approvalRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  notificationRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsTenantContactInfo */
IdentityTenantsTenantContactInfoSchema = z.object({
  primaryContactName: z.string().nullable().optional(),
  primaryContactEmail: z.string().nullable().optional(),
  primaryContactPhone: z.string().nullable().optional(),
  organizationName: z.string().nullable().optional(),
  address: z.lazy(() => IdentityTenantsTenantAddressSchema).optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantCurrencySettings */
IdentityTenantsTenantCurrencySettingsSchema = z.object({
  defaultCurrency: z.string().nullable().optional(),
  displayFormat: z.string().nullable().optional(),
  decimalPlaces: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantDomain */
IdentityTenantsTenantDomainSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid(),
  topLevelDomain: z.string().min(1).max(255),
  subdomain: z.string().max(100).nullable().optional(),
  isMainDomain: z.boolean().optional(),
  isSecondaryDomain: z.boolean().optional(),
  userGroupId: z.string().uuid().nullable().optional(),
  fullDomain: z.string().nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantIntegrationSettings */
IdentityTenantsTenantIntegrationSettingsSchema = z.object({
  externalServices: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  webhookSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  apiKeys: z.record(z.string(), z.string()).nullable().optional(),
  ssoConfiguration: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsTenantMember */
IdentityTenantsTenantMemberSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid(),
  userId: z.string().uuid(),
  parentMemberId: z.string().uuid().nullable().optional(),
  parentMember: z.lazy(() => IdentityTenantsTenantMemberSchema).optional(),
  childMembers: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  role: z.string().min(1).max(100),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  leaveReason: z.string().max(500).nullable().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantMetadata */
IdentityTenantsTenantMetadataSchema = z.object({
  id: z.string().uuid().optional(),
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  tags: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  businessInfo: z
    .lazy(() => IdentityTenantsTenantBusinessInfoSchema)
    .optional(),
  contactInfo: z.lazy(() => IdentityTenantsTenantContactInfoSchema).optional(),
  adminNotes: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityTenantsTenantSecuritySettings */
IdentityTenantsTenantSecuritySettingsSchema = z.object({
  passwordPolicy: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  sessionTimeout: z.number().int().optional(),
  twoFactorRequired: z.boolean().optional(),
  ipWhitelist: z.array(z.string()).nullable().optional(),
  apiRateLimits: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantSettings */
IdentityTenantsTenantSettingsSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid(),
  defaultLanguage: z.string().max(10).nullable().optional(),
  defaultTimezone: z.string().max(50).nullable().optional(),
  defaultCurrency: z.string().max(3).nullable().optional(),
  allowUserRegistration: z.boolean().optional(),
  requireRegistrationApproval: z.boolean().optional(),
  requireTwoFactorAuth: z.boolean().optional(),
  maxUsers: z.number().int().nullable().optional(),
  storageQuota: z.number().int().nullable().optional(),
  enableAuditLogging: z.boolean().optional(),
  enableApiAccess: z.boolean().optional(),
  brandingSettings: z.string().max(5000).nullable().optional(),
  notificationSettings: z.string().max(5000).nullable().optional(),
  securitySettings: z.string().max(5000).nullable().optional(),
  integrationSettingsJson: z.string().nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantSettingsDto */
IdentityTenantsTenantSettingsDtoSchema = z.object({
  id: z.string().uuid().optional(),
  systemConfiguration: z
    .lazy(() => IdentityTenantsTenantSystemConfigurationSchema)
    .optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  businessRules: z
    .lazy(() => IdentityTenantsTenantBusinessRulesSchema)
    .optional(),
  userInterfaceSettings: z
    .lazy(() => IdentityTenantsTenantUiSettingsSchema)
    .optional(),
  securitySettings: z
    .lazy(() => IdentityTenantsTenantSecuritySettingsSchema)
    .optional(),
  integrationSettings: z
    .lazy(() => IdentityTenantsTenantIntegrationSettingsSchema)
    .optional(),
  systemLimits: z
    .lazy(() => IdentityTenantsTenantSystemLimitsSchema)
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityTenantsTenantStatistics */
IdentityTenantsTenantStatisticsSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid(),
  statisticDate: z.string().datetime().optional(),
  totalMembers: z.number().int().optional(),
  activeMembers: z.number().int().optional(),
  inactiveMembers: z.number().int().optional(),
  storageUsed: z.number().int().optional(),
  apiCalls: z.number().int().optional(),
  newMembers: z.number().int().optional(),
  membersLeft: z.number().int().optional(),
  customMetrics: z.string().max(10000).nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantSystemConfiguration */
IdentityTenantsTenantSystemConfigurationSchema = z.object({
  timeZone: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  numberFormat: z.string().nullable().optional(),
  currencySettings: z
    .lazy(() => IdentityTenantsTenantCurrencySettingsSchema)
    .optional(),
  customConfiguration: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsTenantSystemLimits */
IdentityTenantsTenantSystemLimitsSchema = z.object({
  maxUsers: z.number().int().optional(),
  maxStorage: z.number().int().optional(),
  maxApiCalls: z.number().int().optional(),
  maxProjects: z.number().int().optional(),
  customLimits: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantUiSettings */
IdentityTenantsTenantUiSettingsSchema = z.object({
  theme: z.string().nullable().optional(),
  layout: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  branding: z.lazy(() => IdentityTenantsTenantBrandingSchema).optional(),
  customCss: z.string().nullable().optional(),
  componentSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsTenantValidationError */
IdentityTenantsTenantValidationErrorSchema = z.object({
  field: z.string().nullable().optional(),
  code: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantValidationOutput */
IdentityTenantsTenantValidationOutputSchema = z.object({
  isValid: z.boolean().optional(),
  errors: z
    .array(z.lazy(() => IdentityTenantsTenantValidationErrorSchema))
    .nullable()
    .optional(),
  warnings: z
    .array(z.lazy(() => IdentityTenantsTenantValidationWarningSchema))
    .nullable()
    .optional(),
  suggestions: z.array(z.string()).nullable().optional(),
  slugValidation: z.lazy(() => IdentityTenantsSlugValidationSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantValidationWarning */
IdentityTenantsTenantValidationWarningSchema = z.object({
  field: z.string().nullable().optional(),
  code: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantAddressInput */
IdentityTenantsUpdateTenantAddressInputSchema = z.object({
  street: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBrandingInput */
IdentityTenantsUpdateTenantBrandingInputSchema = z.object({
  logoUrl: z.string().nullable().optional(),
  faviconUrl: z.string().nullable().optional(),
  primaryColor: z.string().nullable().optional(),
  secondaryColor: z.string().nullable().optional(),
  companyName: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBusinessInfoInput */
IdentityTenantsUpdateTenantBusinessInfoInputSchema = z.object({
  industry: z.string().nullable().optional(),
  organizationSize: z.string().nullable().optional(),
  tenantType: z.string().nullable().optional(),
  geographicRegion: z.string().nullable().optional(),
  complianceRequirements: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBusinessRulesInput */
IdentityTenantsUpdateTenantBusinessRulesInputSchema = z.object({
  workflowRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  validationRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  approvalRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  notificationRules: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantContactInfoInput */
IdentityTenantsUpdateTenantContactInfoInputSchema = z.object({
  primaryContactName: z.string().nullable().optional(),
  primaryContactEmail: z.string().nullable().optional(),
  primaryContactPhone: z.string().nullable().optional(),
  organizationName: z.string().nullable().optional(),
  address: z
    .lazy(() => IdentityTenantsUpdateTenantAddressInputSchema)
    .optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantCurrencySettingsInput */
IdentityTenantsUpdateTenantCurrencySettingsInputSchema = z.object({
  defaultCurrency: z.string().nullable().optional(),
  displayFormat: z.string().nullable().optional(),
  decimalPlaces: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantFeatureFlagsInput */
IdentityTenantsUpdateTenantFeatureFlagsInputSchema = z.object({
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantInput */
IdentityTenantsUpdateTenantInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantIntegrationSettingsInput */
IdentityTenantsUpdateTenantIntegrationSettingsInputSchema = z.object({
  externalServices: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  webhookSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  apiKeys: z.record(z.string(), z.string()).nullable().optional(),
  ssoConfiguration: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMemberInviteOutput */
IdentityTenantsUpdateTenantMemberInviteOutputSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  memberId: z.string().uuid().nullable().optional(),
  inviteStatus: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMemberRoleOutput */
IdentityTenantsUpdateTenantMemberRoleOutputSchema = z.object({
  success: z.boolean().optional(),
  message: z.string().nullable().optional(),
  memberId: z.string().uuid().optional(),
  newRole: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMetadataInput */
IdentityTenantsUpdateTenantMetadataInputSchema = z.object({
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  tags: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  businessInfo: z
    .lazy(() => IdentityTenantsUpdateTenantBusinessInfoInputSchema)
    .optional(),
  contactInfo: z
    .lazy(() => IdentityTenantsUpdateTenantContactInfoInputSchema)
    .optional(),
  adminNotes: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSecuritySettingsInput */
IdentityTenantsUpdateTenantSecuritySettingsInputSchema = z.object({
  passwordPolicy: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  sessionTimeout: z.number().int().nullable().optional(),
  twoFactorRequired: z.boolean().nullable().optional(),
  ipWhitelist: z.array(z.string()).nullable().optional(),
  apiRateLimits: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSettingsInput */
IdentityTenantsUpdateTenantSettingsInputSchema = z.object({
  systemConfiguration: z
    .lazy(() => IdentityTenantsUpdateTenantSystemConfigurationInputSchema)
    .optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  businessRules: z
    .lazy(() => IdentityTenantsUpdateTenantBusinessRulesInputSchema)
    .optional(),
  userInterfaceSettings: z
    .lazy(() => IdentityTenantsUpdateTenantUiSettingsInputSchema)
    .optional(),
  securitySettings: z
    .lazy(() => IdentityTenantsUpdateTenantSecuritySettingsInputSchema)
    .optional(),
  integrationSettings: z
    .lazy(() => IdentityTenantsUpdateTenantIntegrationSettingsInputSchema)
    .optional(),
  systemLimits: z
    .lazy(() => IdentityTenantsUpdateTenantSystemLimitsInputSchema)
    .optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSystemConfigurationInput */
IdentityTenantsUpdateTenantSystemConfigurationInputSchema = z.object({
  timeZone: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  numberFormat: z.string().nullable().optional(),
  currencySettings: z
    .lazy(() => IdentityTenantsUpdateTenantCurrencySettingsInputSchema)
    .optional(),
  customConfiguration: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSystemLimitsInput */
IdentityTenantsUpdateTenantSystemLimitsInputSchema = z.object({
  maxUsers: z.number().int().nullable().optional(),
  maxStorage: z.number().int().nullable().optional(),
  maxApiCalls: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  customLimits: z.record(z.string(), z.number().int()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantTagsInput */
IdentityTenantsUpdateTenantTagsInputSchema = z.object({
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantUiSettingsInput */
IdentityTenantsUpdateTenantUiSettingsInputSchema = z.object({
  theme: z.string().nullable().optional(),
  layout: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
  branding: z
    .lazy(() => IdentityTenantsUpdateTenantBrandingInputSchema)
    .optional(),
  customCss: z.string().nullable().optional(),
  componentSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()).nullable())
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsUpdateUserMembershipInviteInput */
IdentityTenantsUpdateUserMembershipInviteInputSchema = z.object({
  actorEmail: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateUserMembershipRoleInput */
IdentityTenantsUpdateUserMembershipRoleInputSchema = z.object({
  role: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUsageTracking */
IdentityTenantsUsageTrackingSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid(),
  date: z.string().datetime().optional(),
  resourceType: z.string().min(1).max(100),
  usageAmount: z.number().int().optional(),
  unit: z.string().max(50).nullable().optional(),
  cost: z.number().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
});

/** Zod schema for IdentityTenantsUserMembership */
IdentityTenantsUserMembershipSchema = z.object({
  membershipId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  tenantName: z.string().nullable().optional(),
  tenantSlug: z.string().nullable().optional(),
  tenantIsActive: z.boolean().optional(),
  tenantIsDefault: z.boolean().optional(),
  tenantDescription: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  inviteStatus: z.string().nullable().optional(),
  invitedByEmail: z.string().nullable().optional(),
  inviteeEmail: z.string().nullable().optional(),
  inviteeName: z.string().nullable().optional(),
  invitedAt: z.string().datetime().nullable().optional(),
  lastInviteSentAt: z.string().datetime().nullable().optional(),
  acceptedAt: z.string().datetime().nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  inviteResendCount: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsValidateTenantInput */
IdentityTenantsValidateTenantInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  adminEmail: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersBulkActivateUsersInput */
IdentityUsersBulkActivateUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkActivateUsersOutput */
IdentityUsersBulkActivateUsersOutputSchema = z.object({
  activatedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkCreateUsersInput */
IdentityUsersBulkCreateUsersInputSchema = z.object({
  users: z
    .array(z.lazy(() => IdentityUsersCreateUserRequestItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersBulkCreateUsersOutput */
IdentityUsersBulkCreateUsersOutputSchema = z.object({
  createdUserIds: z.array(z.string().uuid()).nullable().optional(),
  failedEmails: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeactivateUsersInput */
IdentityUsersBulkDeactivateUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeactivateUsersOutput */
IdentityUsersBulkDeactivateUsersOutputSchema = z.object({
  deactivatedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeleteUsersInput */
IdentityUsersBulkDeleteUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkNotificationInput */
IdentityUsersBulkNotificationInputSchema = z.object({
  notificationIds: z.array(z.string().uuid()).nullable().optional(),
  operation: z.string().nullable().optional(),
  filterCriteria: z
    .lazy(() => IdentityUsersNotificationFilterCriteriaSchema)
    .optional(),
});

/** Zod schema for IdentityUsersBulkPurgeUsersInput */
IdentityUsersBulkPurgeUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
  strategy: z.lazy(() => IdentityUsersPurgeStrategySchema).optional(),
});

/** Zod schema for IdentityUsersBulkRestoreUsersInput */
IdentityUsersBulkRestoreUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkRestoreUsersOutput */
IdentityUsersBulkRestoreUsersOutputSchema = z.object({
  restoredUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkSuspendUsersInput */
IdentityUsersBulkSuspendUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkSuspendUsersOutput */
IdentityUsersBulkSuspendUsersOutputSchema = z.object({
  suspendedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkUnsuspendUsersInput */
IdentityUsersBulkUnsuspendUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkUnsuspendUsersOutput */
IdentityUsersBulkUnsuspendUsersOutputSchema = z.object({
  unsuspendedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkUpdateUsersInput */
IdentityUsersBulkUpdateUsersInputSchema = z.object({
  updates: z
    .array(z.lazy(() => IdentityUsersUpdateUserRequestItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersCreateUserInput */
IdentityUsersCreateUserInputSchema = z.object({
  email: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersCreateUserRequestItem */
IdentityUsersCreateUserRequestItemSchema = z.object({
  email: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersNotificationAction */
IdentityUsersNotificationActionSchema = z.object({
  id: z.string().nullable().optional(),
  text: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  isPrimary: z.boolean().optional(),
});

/** Zod schema for IdentityUsersNotificationFilterCriteria */
IdentityUsersNotificationFilterCriteriaSchema = z.object({
  categories: z.array(z.string()).nullable().optional(),
  priorities: z.array(z.string()).nullable().optional(),
  types: z.array(z.string()).nullable().optional(),
  isRead: z.boolean().nullable().optional(),
  isArchived: z.boolean().nullable().optional(),
  dateFrom: z.string().datetime().nullable().optional(),
  dateTo: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityUsersNotificationPriority */
IdentityUsersNotificationPrioritySchema = z.enum([
  "Low",
  "Normal",
  "High",
  "Urgent",
  "Critical",
]);

/** Zod schema for IdentityUsersProfileVisibility */
IdentityUsersProfileVisibilitySchema = z.enum([
  "Private",
  "FriendsOnly",
  "Public",
]);

/** Zod schema for IdentityUsersPurgeStrategy */
IdentityUsersPurgeStrategySchema = z.enum([
  "Immediate",
  "Scheduled",
  "GracePeriod",
]);

/** Zod schema for IdentityUsersReplaceUserAccessibilityPreferencesInput */
IdentityUsersReplaceUserAccessibilityPreferencesInputSchema = z.object({
  accessibilityPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersReplaceUserLocalizationPreferencesInput */
IdentityUsersReplaceUserLocalizationPreferencesInputSchema = z.object({
  localizationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersReplaceUserMetadataInput */
IdentityUsersReplaceUserMetadataInputSchema = z.object({
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  tags: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserNotificationPreferencesInput */
IdentityUsersReplaceUserNotificationPreferencesInputSchema = z.object({
  notificationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersReplaceUserPreferencesInput */
IdentityUsersReplaceUserPreferencesInputSchema = z.object({
  generalPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  notificationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  accessibilityPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  privacyPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersReplaceUserPrivacyPreferencesInput */
IdentityUsersReplaceUserPrivacyPreferencesInputSchema = z.object({
  privacyPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersReplaceUserProfileInput */
IdentityUsersReplaceUserProfileInputSchema = z.object({
  displayName: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
  jobTitle: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().optional(),
  showLocation: z.boolean().optional(),
});

/** Zod schema for IdentityUsersUpdateUserAccessibilityPreferencesInput */
IdentityUsersUpdateUserAccessibilityPreferencesInputSchema = z.object({
  accessibilityPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUpdateUserInput */
IdentityUsersUpdateUserInputSchema = z.object({
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserLocalizationPreferencesInput */
IdentityUsersUpdateUserLocalizationPreferencesInputSchema = z.object({
  localizationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUpdateUserMetadataInput */
IdentityUsersUpdateUserMetadataInputSchema = z.object({
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  tagsToAdd: z.array(z.string()).nullable().optional(),
  tagsToRemove: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserNotificationPreferencesInput */
IdentityUsersUpdateUserNotificationPreferencesInputSchema = z.object({
  notificationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUpdateUserPreferencesInput */
IdentityUsersUpdateUserPreferencesInputSchema = z.object({
  generalPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  notificationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  accessibilityPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  privacyPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUpdateUserPrivacyPreferencesInput */
IdentityUsersUpdateUserPrivacyPreferencesInputSchema = z.object({
  privacyPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUpdateUserProfileInput */
IdentityUsersUpdateUserProfileInputSchema = z.object({
  displayName: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
  jobTitle: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().nullable().optional(),
  showLocation: z.boolean().nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserRequestItem */
IdentityUsersUpdateUserRequestItemSchema = z.object({
  userId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUser */
IdentityUsersUserSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  email: z.string().email().min(1).max(255),
  username: z.string().max(256).nullable().optional(),
  name: z.string().min(1).max(100),
  isEmailVerified: z.boolean().optional(),
  lastLoginAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  isSuspended: z.boolean().optional(),
  tokenVersion: z.number().int().optional(),
  status: z.lazy(() => IdentityUsersUserStatusSchema).optional(),
  phoneNumber: z.string().max(20).nullable().optional(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  profile: z.lazy(() => IdentityUsersUserProfileSchema).optional(),
  metadata: z.lazy(() => IdentityUsersUserMetadataSchema).optional(),
  preferences: z.lazy(() => IdentityUsersUserPreferencesSchema).optional(),
  notifications: z
    .array(z.lazy(() => IdentityUsersUserNotificationSchema))
    .nullable()
    .optional(),
  tenantMemberships: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  hasPassword: z.boolean().optional(),
  canPerformActions: z.boolean().optional(),
  canSignIn: z.boolean().optional(),
});

/** Zod schema for IdentityUsersUserAccessibilityPreferences */
IdentityUsersUserAccessibilityPreferencesSchema = z.object({
  highContrast: z.boolean().optional(),
  largeText: z.boolean().optional(),
  screenReader: z.boolean().optional(),
  reducedMotion: z.boolean().optional(),
  keyboardNavigation: z.boolean().optional(),
  fontSize: z.number().int().optional(),
  colorScheme: z.string().nullable().optional(),
  customSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserDto */
IdentityUsersUserDtoSchema = z.object({
  id: z.string().uuid().optional(),
  email: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  phoneNumber: z.string().nullable().optional(),
  lastSeenAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityUsersUserLocalizationPreferences */
IdentityUsersUserLocalizationPreferencesSchema = z.object({
  language: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  timeFormat: z.string().nullable().optional(),
  currency: z.string().nullable().optional(),
  numberFormat: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  customSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserMetadata */
IdentityUsersUserMetadataSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  customFields: z.string().max(50000).nullable().optional(),
  tags: z.string().max(10000).nullable().optional(),
  externalReferences: z.string().max(25000).nullable().optional(),
  notes: z.string().max(2000).nullable().optional(),
});

/** Zod schema for IdentityUsersUserMetadataDto */
IdentityUsersUserMetadataDtoSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  customFields: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  tags: z.array(z.string()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserNotification */
IdentityUsersUserNotificationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  type: z.string().min(1).max(50),
  title: z.string().min(1).max(200),
  content: z.string().min(1).max(2000),
  priority: z.lazy(() => IdentityUsersNotificationPrioritySchema).optional(),
  isRead: z.boolean().optional(),
  isArchived: z.boolean().optional(),
  readAt: z.string().datetime().nullable().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  senderId: z.string().uuid().nullable().optional(),
  source: z.string().max(100).nullable().optional(),
  relatedEntityId: z.string().uuid().nullable().optional(),
  relatedEntityType: z.string().max(100).nullable().optional(),
  actionUrl: z.string().max(500).nullable().optional(),
  metadata: z.string().max(10000).nullable().optional(),
});

/** Zod schema for IdentityUsersUserNotificationDetail */
IdentityUsersUserNotificationDetailSchema = z.object({
  notification: z.lazy(() => IdentityUsersUserNotificationDtoSchema).optional(),
  relatedNotifications: z
    .array(z.lazy(() => IdentityUsersUserNotificationDtoSchema))
    .nullable()
    .optional(),
  actions: z
    .array(z.lazy(() => IdentityUsersNotificationActionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserNotificationDto */
IdentityUsersUserNotificationDtoSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  type: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  priority: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  isRead: z.boolean().optional(),
  isArchived: z.boolean().optional(),
  readAt: z.string().datetime().nullable().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  actionUrl: z.string().nullable().optional(),
  actionText: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  metadata: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserNotificationPreferences */
IdentityUsersUserNotificationPreferencesSchema = z.object({
  emailEnabled: z.boolean().optional(),
  pushEnabled: z.boolean().optional(),
  smsEnabled: z.boolean().optional(),
  inAppEnabled: z.boolean().optional(),
  frequency: z.string().nullable().optional(),
  quietHours: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  categoryPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserPreferences */
IdentityUsersUserPreferencesSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  generalPreferences: z.string().max(10000).nullable().optional(),
  notificationPreferences: z.string().max(10000).nullable().optional(),
  accessibilityPreferences: z.string().max(10000).nullable().optional(),
  privacyPreferences: z.string().max(10000).nullable().optional(),
  localizationPreferences: z.string().max(10000).nullable().optional(),
});

/** Zod schema for IdentityUsersUserPreferencesDto */
IdentityUsersUserPreferencesDtoSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  generalPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  notificationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  accessibilityPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  privacyPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  localizationPreferences: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserPrivacyPreferences */
IdentityUsersUserPrivacyPreferencesSchema = z.object({
  profileVisibility: z.string().nullable().optional(),
  activityTracking: z.boolean().optional(),
  dataCollection: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  thirdPartySharing: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  marketingEmails: z.boolean().optional(),
  analyticsCookies: z.boolean().optional(),
  personalizedContent: z.boolean().optional(),
  customSettings: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserProfile */
IdentityUsersUserProfileSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  displayName: z.string().max(100).nullable().optional(),
  bio: z.string().max(1000).nullable().optional(),
  location: z.string().max(100).nullable().optional(),
  website: z.string().max(255).nullable().optional(),
  jobTitle: z.string().max(100).nullable().optional(),
  company: z.string().max(100).nullable().optional(),
  avatarUrl: z.string().max(500).nullable().optional(),
  bannerUrl: z.string().max(500).nullable().optional(),
  dateOfBirth: z.string().date().nullable().optional(),
  gender: z.string().max(20).nullable().optional(),
  visibility: z.lazy(() => IdentityUsersProfileVisibilitySchema).optional(),
  isVerified: z.boolean().optional(),
});

/** Zod schema for IdentityUsersUserProfileDto */
IdentityUsersUserProfileDtoSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  displayName: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
  jobTitle: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().optional(),
  showLocation: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserStatus */
IdentityUsersUserStatusSchema = z.object({
  isActive: z.boolean().optional(),
  isSuspended: z.boolean().optional(),
});

/** Zod schema for KeyValuePairStringAuthenticationExtensionsPRFValues */
KeyValuePairStringAuthenticationExtensionsPRFValuesSchema = z.object({
  key: z.string().nullable().optional(),
  value: z
    .lazy(() => ObjectsAuthenticationExtensionsPRFValuesSchema)
    .optional(),
});

/** Zod schema for LaunchPadCreateLaunchPadEventInput */
LaunchPadCreateLaunchPadEventInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().nullable().optional(),
  applicationsCloseAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadCreateLaunchPadSlotInput */
LaunchPadCreateLaunchPadSlotInputSchema = z.object({
  name: z.string().nullable().optional(),
  role: z.lazy(() => LaunchPadLaunchPadParticipantRoleSchema).optional(),
  capacity: z.number().int().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
});

/** Zod schema for LaunchPadCreateLaunchPlanInput */
LaunchPadCreateLaunchPlanInputSchema = z.object({
  projectId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  positioning: z.string().nullable().optional(),
  targetLaunchAt: z.string().datetime().nullable().optional(),
  channels: z.array(z.string()).nullable().optional(),
  checklistItems: z
    .array(z.lazy(() => LaunchPadLaunchChecklistItemInputSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LaunchPadLaunchChecklistItem */
LaunchPadLaunchChecklistItemSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  launchPlanId: z.string().uuid().optional(),
  launchPlan: z.lazy(() => LaunchPadLaunchPlanSchema).optional(),
  title: z.string().min(1).max(200),
  category: z.string().min(1).max(100),
  isRequired: z.boolean().optional(),
  isComplete: z.boolean().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchChecklistItemInput */
LaunchPadLaunchChecklistItemInputSchema = z.object({
  title: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  isComplete: z.boolean().optional(),
  isRequired: z.boolean().optional(),
});

/** Zod schema for LaunchPadLaunchPadAnalyticsProjection */
LaunchPadLaunchPadAnalyticsProjectionSchema = z.object({
  events: z.number().int().optional(),
  completedEvents: z.number().int().optional(),
  applications: z.number().int().optional(),
  approvedApplications: z.number().int().optional(),
  registrations: z.number().int().optional(),
  completedRegistrations: z.number().int().optional(),
});

/** Zod schema for LaunchPadLaunchPadApplication */
LaunchPadLaunchPadApplicationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  launchPadEventId: z.string().uuid().optional(),
  launchPadEvent: z.lazy(() => LaunchPadLaunchPadEventSchema).optional(),
  projectId: z.string().uuid().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectVersionId: z.string().uuid().optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  submittedByUserId: z.string().uuid().optional(),
  submittedByUser: z.lazy(() => IdentityUsersUserSchema).optional(),
  reviewedByUserId: z.string().uuid().nullable().optional(),
  submittedAt: z.string().datetime().optional(),
  reviewedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LaunchPadLaunchPadApplicationStatusSchema).optional(),
  pitch: z.string().max(2000).nullable().optional(),
  submittedAssetReferenceIdsJson: z.string().max(10000).nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPadApplicationProjection */
LaunchPadLaunchPadApplicationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  submittedByUserId: z.string().uuid().optional(),
  status: z.lazy(() => LaunchPadLaunchPadApplicationStatusSchema).optional(),
  pitch: z.string().nullable().optional(),
  submittedAt: z.string().datetime().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPadApplicationStatus */
LaunchPadLaunchPadApplicationStatusSchema = z.enum([
  "Draft",
  "Submitted",
  "UnderReview",
  "Waitlisted",
  "Approved",
  "Rejected",
  "Withdrawn",
]);

/** Zod schema for LaunchPadLaunchPadEvent */
LaunchPadLaunchPadEventSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(200),
  description: z.string().max(2000).nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().nullable().optional(),
  applicationsCloseAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LaunchPadLaunchPadEventStatusSchema).optional(),
  applications: z
    .array(z.lazy(() => LaunchPadLaunchPadApplicationSchema))
    .nullable()
    .optional(),
  slots: z
    .array(z.lazy(() => LaunchPadLaunchPadParticipantSlotSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LaunchPadLaunchPadEventDetailProjection */
LaunchPadLaunchPadEventDetailProjectionSchema = z.object({
  event: z.lazy(() => LaunchPadLaunchPadEventProjectionSchema).optional(),
  slots: z
    .array(z.lazy(() => LaunchPadLaunchPadSlotProjectionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LaunchPadLaunchPadEventProjection */
LaunchPadLaunchPadEventProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  status: z.lazy(() => LaunchPadLaunchPadEventStatusSchema).optional(),
  applicationsOpenAt: z.string().datetime().nullable().optional(),
  applicationsCloseAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPadEventStatus */
LaunchPadLaunchPadEventStatusSchema = z.enum([
  "Draft",
  "ApplicationsOpen",
  "ApplicationsClosed",
  "Scheduled",
  "Active",
  "Completed",
  "Cancelled",
  "Archived",
]);

/** Zod schema for LaunchPadLaunchPadParticipantRegistration */
LaunchPadLaunchPadParticipantRegistrationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  launchPadParticipantSlotId: z.string().uuid().optional(),
  launchPadParticipantSlot: z
    .lazy(() => LaunchPadLaunchPadParticipantSlotSchema)
    .optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  status: z.lazy(() => LaunchPadLaunchPadParticipantStatusSchema).optional(),
  registeredAt: z.string().datetime().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPadParticipantRole */
LaunchPadLaunchPadParticipantRoleSchema = z.enum([
  "Participant",
  "Mentor",
  "Audience",
  "Presenter",
]);

/** Zod schema for LaunchPadLaunchPadParticipantSlot */
LaunchPadLaunchPadParticipantSlotSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  launchPadEventId: z.string().uuid().optional(),
  launchPadEvent: z.lazy(() => LaunchPadLaunchPadEventSchema).optional(),
  name: z.string().min(1).max(120),
  role: z.lazy(() => LaunchPadLaunchPadParticipantRoleSchema).optional(),
  capacity: z.number().int().optional(),
  reservedCount: z.number().int().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  registrations: z
    .array(z.lazy(() => LaunchPadLaunchPadParticipantRegistrationSchema))
    .nullable()
    .optional(),
  hasCapacity: z.boolean().optional(),
});

/** Zod schema for LaunchPadLaunchPadParticipantStatus */
LaunchPadLaunchPadParticipantStatusSchema = z.enum([
  "Registered",
  "Waitlisted",
  "CheckedIn",
  "Attended",
  "Completed",
  "Cancelled",
  "NoShow",
]);

/** Zod schema for LaunchPadLaunchPadRegistrationProjection */
LaunchPadLaunchPadRegistrationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  status: z.lazy(() => LaunchPadLaunchPadParticipantStatusSchema).optional(),
  registeredAt: z.string().datetime().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPadSlotProjection */
LaunchPadLaunchPadSlotProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  role: z.lazy(() => LaunchPadLaunchPadParticipantRoleSchema).optional(),
  capacity: z.number().int().optional(),
  reservedCount: z.number().int().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
});

/** Zod schema for LaunchPadLaunchPlan */
LaunchPadLaunchPlanSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  launchPadEventId: z.string().uuid().nullable().optional(),
  launchPadEvent: z.lazy(() => LaunchPadLaunchPadEventSchema).optional(),
  launchPadApplicationId: z.string().uuid().nullable().optional(),
  launchPadApplication: z
    .lazy(() => LaunchPadLaunchPadApplicationSchema)
    .optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  projectId: z.string().uuid().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  name: z.string().min(1).max(200),
  positioning: z.string().max(1000).nullable().optional(),
  targetLaunchAt: z.string().datetime().nullable().optional(),
  launchedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LaunchPadLaunchPlanStatusSchema).optional(),
  channels: z.array(z.string()).nullable().optional(),
  checklistItems: z
    .array(z.lazy(() => LaunchPadLaunchChecklistItemSchema))
    .nullable()
    .optional(),
  readinessPercent: z.number().int().optional(),
});

/** Zod schema for LaunchPadLaunchPlanStatus */
LaunchPadLaunchPlanStatusSchema = z.enum([
  "Draft",
  "Preparing",
  "Ready",
  "Launched",
  "Paused",
]);

/** Zod schema for LaunchPadReviewLaunchPadApplicationInput */
LaunchPadReviewLaunchPadApplicationInputSchema = z.object({
  status: z.lazy(() => LaunchPadLaunchPadApplicationStatusSchema).optional(),
  launchPlanName: z.string().nullable().optional(),
});

/** Zod schema for LaunchPadSubmitLaunchPadApplicationInput */
LaunchPadSubmitLaunchPadApplicationInputSchema = z.object({
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  pitch: z.string().nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LaunchPadTransitionLaunchPadEventInput */
LaunchPadTransitionLaunchPadEventInputSchema = z.object({
  status: z.lazy(() => LaunchPadLaunchPadEventStatusSchema).optional(),
});

/** Zod schema for LaunchPadTransitionLaunchPadRegistrationInput */
LaunchPadTransitionLaunchPadRegistrationInputSchema = z.object({
  status: z.lazy(() => LaunchPadLaunchPadParticipantStatusSchema).optional(),
});

/** Zod schema for LaunchPadUpdateLaunchPadApplicationInput */
LaunchPadUpdateLaunchPadApplicationInputSchema = z.object({
  projectVersionId: z.string().uuid().optional(),
  pitch: z.string().nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LaunchPadUpdateLaunchPadEventInput */
LaunchPadUpdateLaunchPadEventInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().nullable().optional(),
  applicationsCloseAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningAssessmentsAssessment */
LearningAssessmentsAssessmentSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => LearningAssessmentsAssessmentTypeSchema).optional(),
  maxScore: z.number().int().optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  isRequired: z.boolean().optional(),
  order: z.number().int().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  assessmentGroupName: z.string().nullable().optional(),
  assessmentGroupWeightPercent: z.number().nullable().optional(),
  assessmentGroupOrder: z.number().int().nullable().optional(),
  isAvailable: z.boolean().optional(),
  submissionModalities: z
    .lazy(() => LearningAssessmentsSubmissionModalitySchema)
    .optional(),
  presentationMode: z
    .lazy(() => LearningAssessmentsAssessmentPresentationModeSchema)
    .optional(),
  dueAt: z.string().datetime().nullable().optional(),
  allowLateSubmissions: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  gradingMethods: z
    .lazy(() => LearningAssessmentsAssessmentGradingMethodSchema)
    .optional(),
});

/** Zod schema for LearningAssessmentsAssessmentDefinition */
LearningAssessmentsAssessmentDefinitionSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  definitionSchemaVersion: z.number().int().optional(),
  definition: z.record(z.string(), z.unknown()).optional(),
});

/** Zod schema for LearningAssessmentsAssessmentGradingMethod. A comma-separated combination of the declared flag names. */
LearningAssessmentsAssessmentGradingMethodSchema = z.string();

/** Zod schema for LearningAssessmentsAssessmentGroup */
LearningAssessmentsAssessmentGroupSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  weightPercent: z.number().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentGroupAnalytics */
LearningAssessmentsAssessmentGroupAnalyticsSchema = z.object({
  groupId: z.string().uuid().nullable().optional(),
  groupName: z.string().nullable().optional(),
  weightPercent: z.number().nullable().optional(),
  assessmentCount: z.number().int().optional(),
  gradedCount: z.number().int().optional(),
  ungradedCount: z.number().int().optional(),
  averagePercent: z.number().optional(),
  passRate: z.number().optional(),
  distribution: z
    .array(z.lazy(() => LearningAssessmentsAssessmentScoreBucketSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningAssessmentsAssessmentPresentationMode */
LearningAssessmentsAssessmentPresentationModeSchema = z.enum([
  "SingleStep",
  "Continuous",
]);

/** Zod schema for LearningAssessmentsAssessmentScoreBucket */
LearningAssessmentsAssessmentScoreBucketSchema = z.object({
  label: z.string().nullable().optional(),
  minPercent: z.number().int().optional(),
  maxPercent: z.number().int().optional(),
  count: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentSubmission */
LearningAssessmentsAssessmentSubmissionSchema = z.object({
  id: z.string().uuid().optional(),
  assessmentId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  score: z.number().int().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  gradedBy: z.string().uuid().nullable().optional(),
  feedback: z.string().nullable().optional(),
  status: z.lazy(() => LearningAssessmentsSubmissionStatusSchema).optional(),
  isLate: z.boolean().optional(),
  submittedModalities: z
    .lazy(() => LearningAssessmentsSubmissionModalitySchema)
    .optional(),
  textPayload: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
  codePayload: z.string().nullable().optional(),
  mediaPayload: z.string().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentType. Legacy value Exam is normalized on read and is not valid for new assessments. */
LearningAssessmentsAssessmentTypeSchema = z.enum([
  "Quiz",
  "Assignment",
  "Project",
  "PeerReview",
  "SelfAssessment",
]);

/** Zod schema for LearningAssessmentsAssignAssessmentGroupInput */
LearningAssessmentsAssignAssessmentGroupInputSchema = z.object({
  assessmentGroupId: z.string().uuid().nullable().optional(),
  clearAssessmentGroup: z.boolean().optional(),
});

/** Zod schema for LearningAssessmentsCanAttemptOutput */
LearningAssessmentsCanAttemptOutputSchema = z.object({
  canAttempt: z.boolean().optional(),
  currentAttemptCount: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsCourseAssessmentAnalytics */
LearningAssessmentsCourseAssessmentAnalyticsSchema = z.object({
  courseId: z.string().uuid().optional(),
  assessmentCount: z.number().int().optional(),
  gradedCount: z.number().int().optional(),
  ungradedCount: z.number().int().optional(),
  averagePercent: z.number().optional(),
  passRate: z.number().optional(),
  distribution: z
    .array(z.lazy(() => LearningAssessmentsAssessmentScoreBucketSchema))
    .nullable()
    .optional(),
  groups: z
    .array(z.lazy(() => LearningAssessmentsAssessmentGroupAnalyticsSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningAssessmentsCreateAssessmentGroupInput */
LearningAssessmentsCreateAssessmentGroupInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  weightPercent: z.number().optional(),
  order: z.number().int().optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsCreateAssessmentInput */
LearningAssessmentsCreateAssessmentInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => LearningAssessmentsAssessmentTypeSchema).optional(),
  maxScore: z.number().int().optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  isRequired: z.boolean().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  submissionModalities: z
    .lazy(() => LearningAssessmentsSubmissionModalitySchema)
    .optional(),
  presentationMode: z
    .lazy(() => LearningAssessmentsAssessmentPresentationModeSchema)
    .optional(),
  dueAt: z.string().datetime().nullable().optional(),
  allowLateSubmissions: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  gradingMethods: z
    .lazy(() => LearningAssessmentsAssessmentGradingMethodSchema)
    .optional(),
});

/** Zod schema for LearningAssessmentsGradeSubmissionInput */
LearningAssessmentsGradeSubmissionInputSchema = z.object({
  score: z.number().int().optional(),
  gradedBy: z.string().uuid().nullable().optional(),
  feedback: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsInteractiveVideoAssessmentCue */
LearningAssessmentsInteractiveVideoAssessmentCueSchema = z.object({
  id: z.string().uuid().optional(),
  assessmentId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsLearnerAssessmentAttempt */
LearningAssessmentsLearnerAssessmentAttemptSchema = z.object({
  submission: z
    .lazy(() => LearningAssessmentsLearnerAssessmentSubmissionSchema)
    .optional(),
});

/** Zod schema for LearningAssessmentsLearnerAssessmentSubmission */
LearningAssessmentsLearnerAssessmentSubmissionSchema = z.object({
  id: z.string().uuid().optional(),
  assessmentId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  score: z.number().int().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  feedback: z.string().nullable().optional(),
  status: z.lazy(() => LearningAssessmentsSubmissionStatusSchema).optional(),
  isLate: z.boolean().optional(),
  submittedModalities: z
    .lazy(() => LearningAssessmentsSubmissionModalitySchema)
    .optional(),
  textPayload: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
  codePayload: z.string().nullable().optional(),
  mediaPayload: z.string().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsLearnerInteractiveVideoAssessmentCue */
LearningAssessmentsLearnerInteractiveVideoAssessmentCueSchema = z.object({
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsLinkInteractiveVideoCueInput */
LearningAssessmentsLinkInteractiveVideoCueInputSchema = z.object({
  contentId: z.string().uuid().optional(),
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsStartSubmissionInput */
LearningAssessmentsStartSubmissionInputSchema = z.object({
  enrollmentId: z.string().uuid().optional(),
});

/** Zod schema for LearningAssessmentsSubmissionModality. A comma-separated combination of the declared flag names. */
LearningAssessmentsSubmissionModalitySchema = z.string();

/** Zod schema for LearningAssessmentsSubmissionStatus */
LearningAssessmentsSubmissionStatusSchema = z.enum([
  "InProgress",
  "Submitted",
  "Graded",
  "Returned",
  "Late",
]);

/** Zod schema for LearningAssessmentsSubmitAssessmentInput */
LearningAssessmentsSubmitAssessmentInputSchema = z.object({
  textPayload: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
  codePayload: z.string().nullable().optional(),
  mediaPayload: z.string().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsUpdateAssessmentGroupInput */
LearningAssessmentsUpdateAssessmentGroupInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  weightPercent: z.number().nullable().optional(),
  order: z.number().int().nullable().optional(),
});

/** Zod schema for LearningAssessmentsUpdateAssessmentInput */
LearningAssessmentsUpdateAssessmentInputSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  maxScore: z.number().int().nullable().optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  isRequired: z.boolean().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  clearContentId: z.boolean().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  clearAssessmentGroupId: z.boolean().optional(),
  submissionModalities: z
    .lazy(() => LearningAssessmentsSubmissionModalitySchema)
    .optional(),
  presentationMode: z
    .lazy(() => LearningAssessmentsAssessmentPresentationModeSchema)
    .optional(),
  dueAt: z.string().datetime().nullable().optional(),
  clearDueAt: z.boolean().optional(),
  allowLateSubmissions: z.boolean().nullable().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  clearLateSubmissionDeadline: z.boolean().optional(),
  gradingMethods: z
    .lazy(() => LearningAssessmentsAssessmentGradingMethodSchema)
    .optional(),
});

/** Zod schema for LearningCertificatesCertificate */
LearningCertificatesCertificateSchema = z.object({
  id: z.string().uuid().optional(),
  templateId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  certificateNumber: z.string().nullable().optional(),
  recipientName: z.string().nullable().optional(),
  courseName: z.string().nullable().optional(),
  issuedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCertificatesCertificateStatusSchema).optional(),
});

/** Zod schema for LearningCertificatesCertificateStatus */
LearningCertificatesCertificateStatusSchema = z.enum([
  "Active",
  "Expired",
  "Revoked",
]);

/** Zod schema for LearningCertificatesCertificateTemplate */
LearningCertificatesCertificateTemplateSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  isActive: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCertificatesCertificateTemplateDetail */
LearningCertificatesCertificateTemplateDetailSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
  templateStyles: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  isActive: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCertificatesCertificateVerificationResult */
LearningCertificatesCertificateVerificationResultSchema = z.object({
  isValid: z.boolean().optional(),
  certificateNumber: z.string().nullable().optional(),
  recipientName: z.string().nullable().optional(),
  courseName: z.string().nullable().optional(),
  issuedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCertificatesCertificateStatusSchema).optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesCreateCertificateTemplateInput */
LearningCertificatesCreateCertificateTemplateInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesIssueCertificateInput */
LearningCertificatesIssueCertificateInputSchema = z.object({
  templateId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
});

/** Zod schema for LearningCertificatesRevokeCertificateInput */
LearningCertificatesRevokeCertificateInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesUpdateCertificateTemplateInput */
LearningCertificatesUpdateCertificateTemplateInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
  templateStyles: z.string().nullable().optional(),
  isDefault: z.boolean().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for LearningCohortsApplyCohortScheduleInput */
LearningCohortsApplyCohortScheduleInputSchema = z.object({
  expectedVersion: z.number().int().optional(),
  rules: z
    .lazy(() => LearningCohortsPreviewCohortScheduleInputSchema)
    .optional(),
  confirmAdvisories: z.boolean().optional(),
});

/** Zod schema for LearningCohortsAvailableCohortContent */
LearningCohortsAvailableCohortContentSchema = z.object({
  contentId: z.string().uuid().optional(),
  parentId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  sortOrder: z.number().int().optional(),
  instructionalWeek: z.number().int().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCohortsCohort */
LearningCohortsCohortSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  maxCapacity: z.number().int().optional(),
  currentEnrollmentCount: z.number().int().optional(),
  availableSpots: z.number().int().optional(),
  status: z.lazy(() => LearningCohortsCohortStatusSchema).optional(),
  isOpen: z.boolean().optional(),
  canEnroll: z.boolean().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  meetingSchedule: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  nextMeetingAt: z.string().datetime().nullable().optional(),
  conflictCount: z.number().int().optional(),
  schedule: z.lazy(() => LearningCohortsCohortScheduleSummarySchema).optional(),
});

/** Zod schema for LearningCohortsCohortCalendarEntry */
LearningCohortsCohortCalendarEntrySchema = z.object({
  cohortId: z.string().uuid().optional(),
  cohortName: z.string().nullable().optional(),
  itemId: z.string().uuid().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
  title: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  status: z
    .lazy(() => LearningCohortsCohortScheduleItemStatusSchema)
    .optional(),
});

/** Zod schema for LearningCohortsCohortPacingMode */
LearningCohortsCohortPacingModeSchema = z.enum([
  "OneModulePerWeek",
  "OneLessonPerMeeting",
  "FixedLessonsPerWeek",
  "Manual",
]);

/** Zod schema for LearningCohortsCohortReleasePolicy */
LearningCohortsCohortReleasePolicySchema = z.enum([
  "Weekly",
  "BeforeMeeting",
  "Manual",
  "Immediately",
]);

/** Zod schema for LearningCohortsCohortSchedule */
LearningCohortsCohortScheduleSchema = z.object({
  id: z.string().uuid().optional(),
  cohortId: z.string().uuid().optional(),
  version: z.number().int().optional(),
  timezoneId: z.string().nullable().optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingStartTime: z.string().optional(),
  meetingDurationMinutes: z.number().int().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  unitsPerPeriod: z.number().int().optional(),
  releasePolicy: z
    .lazy(() => LearningCohortsCohortReleasePolicySchema)
    .optional(),
  items: z
    .array(z.lazy(() => LearningCohortsCohortScheduleItemSchema))
    .nullable()
    .optional(),
  unscheduledContentIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCohortsCohortScheduleConflict */
LearningCohortsCohortScheduleConflictSchema = z.object({
  code: z.string().nullable().optional(),
  severity: z
    .lazy(() => LearningCohortsScheduleConflictSeveritySchema)
    .optional(),
  message: z.string().nullable().optional(),
  programContentId: z.string().uuid().nullable().optional(),
  assessmentId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCohortsCohortScheduleItem */
LearningCohortsCohortScheduleItemSchema = z.object({
  id: z.string().uuid().optional(),
  programContentId: z.string().uuid().nullable().optional(),
  assessmentId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
  instructionalWeek: z.number().int().optional(),
  sortOrder: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  title: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  status: z
    .lazy(() => LearningCohortsCohortScheduleItemStatusSchema)
    .optional(),
  visibilityOverride: z
    .lazy(() => LearningCohortsCohortVisibilityOverrideSchema)
    .optional(),
});

/** Zod schema for LearningCohortsCohortScheduleItemStatus */
LearningCohortsCohortScheduleItemStatusSchema = z.enum([
  "Draft",
  "Scheduled",
  "Published",
  "Completed",
  "Cancelled",
]);

/** Zod schema for LearningCohortsCohortScheduleItemType */
LearningCohortsCohortScheduleItemTypeSchema = z.enum([
  "ContentRelease",
  "LiveSession",
  "AssessmentWindow",
  "Milestone",
]);

/** Zod schema for LearningCohortsCohortSchedulePreview */
LearningCohortsCohortSchedulePreviewSchema = z.object({
  items: z
    .array(z.lazy(() => LearningCohortsCohortSchedulePreviewItemSchema))
    .nullable()
    .optional(),
  conflicts: z
    .array(z.lazy(() => LearningCohortsCohortScheduleConflictSchema))
    .nullable()
    .optional(),
  calculatedEndDate: z.string().date().optional(),
  hasBlockingConflicts: z.boolean().optional(),
});

/** Zod schema for LearningCohortsCohortSchedulePreviewItem */
LearningCohortsCohortSchedulePreviewItemSchema = z.object({
  programContentId: z.string().uuid().nullable().optional(),
  assessmentId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
  instructionalWeek: z.number().int().optional(),
  sortOrder: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningCohortsCohortScheduleSummary */
LearningCohortsCohortScheduleSummarySchema = z.object({
  version: z.number().int().optional(),
  timezoneId: z.string().nullable().optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingStartTime: z.string().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  releasePolicy: z
    .lazy(() => LearningCohortsCohortReleasePolicySchema)
    .optional(),
  itemCount: z.number().int().optional(),
});

/** Zod schema for LearningCohortsCohortStatus */
LearningCohortsCohortStatusSchema = z.enum([
  "Scheduled",
  "Active",
  "Completed",
  "Cancelled",
]);

/** Zod schema for LearningCohortsCohortVisibilityOverride */
LearningCohortsCohortVisibilityOverrideSchema = z.enum([
  "Inherited",
  "Hidden",
  "Visible",
]);

/** Zod schema for LearningCohortsCourseCohortCalendar */
LearningCohortsCourseCohortCalendarSchema = z.object({
  courseId: z.string().uuid().optional(),
  entries: z
    .array(z.lazy(() => LearningCohortsCohortCalendarEntrySchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCohortsCreateCohortInput */
LearningCohortsCreateCohortInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  maxCapacity: z.number().int().optional(),
  description: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  meetingSchedule: z.string().nullable().optional(),
});

/** Zod schema for LearningCohortsPreviewCohortScheduleInput */
LearningCohortsPreviewCohortScheduleInputSchema = z.object({
  firstInstructionalDate: z.string().date().optional(),
  cohortEndDate: z.string().date().optional(),
  timezoneId: z.string().nullable().optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingStartTime: z.string().optional(),
  meetingDurationMinutes: z.number().int().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  unitsPerPeriod: z.number().int().optional(),
  releasePolicy: z
    .lazy(() => LearningCohortsCohortReleasePolicySchema)
    .optional(),
  skippedDates: z.array(z.string().date()).nullable().optional(),
  assessmentDueOffsetDays: z.number().int().optional(),
});

/** Zod schema for LearningCohortsScheduleConflictSeverity */
LearningCohortsScheduleConflictSeveritySchema = z.enum([
  "Advisory",
  "Blocking",
]);

/** Zod schema for LearningCohortsScheduleShiftScope */
LearningCohortsScheduleShiftScopeSchema = z.enum(["Single", "Following"]);

/** Zod schema for LearningCohortsShiftCohortScheduleInput */
LearningCohortsShiftCohortScheduleInputSchema = z.object({
  expectedVersion: z.number().int().optional(),
  days: z.number().int().optional(),
  scope: z.lazy(() => LearningCohortsScheduleShiftScopeSchema).optional(),
});

/** Zod schema for LearningCohortsUpdateCohortInput */
LearningCohortsUpdateCohortInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  maxCapacity: z.number().int().nullable().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  meetingSchedule: z.string().nullable().optional(),
});

/** Zod schema for LearningCohortsUpdateCohortScheduleInput */
LearningCohortsUpdateCohortScheduleInputSchema = z.object({
  expectedVersion: z.number().int().optional(),
  item: z
    .lazy(() => LearningCohortsUpdateCohortScheduleItemInputSchema)
    .optional(),
});

/** Zod schema for LearningCohortsUpdateCohortScheduleItemInput */
LearningCohortsUpdateCohortScheduleItemInputSchema = z.object({
  title: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  status: z
    .lazy(() => LearningCohortsCohortScheduleItemStatusSchema)
    .optional(),
  visibilityOverride: z
    .lazy(() => LearningCohortsCohortVisibilityOverrideSchema)
    .optional(),
});

/** Zod schema for LearningCoursesActivityGrade */
LearningCoursesActivityGradeSchema = z.object({
  id: z.string().uuid().optional(),
  contentInteractionId: z.string().uuid().optional(),
  graderProgramUserId: z.string().uuid().nullable().optional(),
  grade: z.number().optional(),
  feedback: z.string().nullable().optional(),
  gradingDetails: z.string().nullable().optional(),
  gradedAt: z.string().datetime().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
  contentInteraction: z
    .lazy(() => LearningCoursesContentInteractionSummarySchema)
    .optional(),
  grader: z.lazy(() => LearningCoursesGraderSummarySchema).optional(),
  isPassingGrade: z.boolean().optional(),
  gradePercentage: z.string().nullable().optional(),
  hasFeedback: z.boolean().optional(),
  hasGradingDetails: z.boolean().optional(),
});

/** Zod schema for LearningCoursesActivitySettings */
LearningCoursesActivitySettingsSchema = z.object({});

/** Zod schema for LearningCoursesBundleFileMeta */
LearningCoursesBundleFileMetaSchema = z.object({
  content: z.string().nullable(),
  encoding: z.string().nullable().optional(),
  visibility: z.string().nullable().optional(),
  modifiable: z.boolean().optional(),
});

/** Zod schema for LearningCoursesCircularDependencyCheckResult */
LearningCoursesCircularDependencyCheckResultSchema = z.object({
  wouldCreateCycle: z.boolean().optional(),
});

/** Zod schema for LearningCoursesCloneProgram */
LearningCoursesCloneProgramSchema = z.object({
  newTitle: z.string().nullable().optional(),
  newDescription: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCodingAssignmentContent */
LearningCoursesCodingAssignmentContentSchema = z.object({
  type: z.string().nullable().optional(),
  version: z.number().int().optional(),
  environment: z.lazy(() => LearningCoursesCodingEnvironmentSchema),
  data: z.lazy(() => LearningCoursesWorkspaceDataSchema),
  tests: z.lazy(() => LearningCoursesTestSuiteSchema),
  grading: z.lazy(() => LearningCoursesGradingConfigSchema),
});

/** Zod schema for LearningCoursesCodingEnvironment */
LearningCoursesCodingEnvironmentSchema = z.object({
  language: z.string().nullable(),
  tools: z.string().nullable(),
  libBundle: z.string().nullable().optional(),
  allowStudentCreateFiles: z.boolean().optional(),
});

/** Zod schema for LearningCoursesCompleteContentInput */
LearningCoursesCompleteContentInputSchema = z.object({
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesCompleteCourseCheckoutInput */
LearningCoursesCompleteCourseCheckoutInputSchema = z.object({
  productId: z.string().uuid().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCompleteCourseCheckoutOutput */
LearningCoursesCompleteCourseCheckoutOutputSchema = z.object({
  courseId: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  entitlementId: z.string().uuid().optional(),
  enrollmentIds: z.array(z.string().uuid()).nullable().optional(),
  alreadyHadAccess: z.boolean().optional(),
  amount: z.number().optional(),
  currency: z.string().nullable().optional(),
  learningUrl: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCompletionRates */
LearningCoursesCompletionRatesSchema = z.object({
  programId: z.string().uuid().optional(),
  overallCompletionRate: z.number().optional(),
  contentCompletionRates: z
    .record(z.string(), z.number())
    .nullable()
    .optional(),
  completionTrends: z
    .array(z.lazy(() => LearningCoursesCompletionTrendSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesCompletionTrend */
LearningCoursesCompletionTrendSchema = z.object({
  date: z.string().datetime().optional(),
  completedCount: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  rate: z.number().optional(),
});

/** Zod schema for LearningCoursesContentInteraction */
LearningCoursesContentInteractionSchema = z.object({
  id: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
  submissionData: z.string().nullable().optional(),
  completionPercentage: z.number().optional(),
  timeSpentMinutes: z.number().int().nullable().optional(),
  timeSpentSeconds: z.number().int().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
  content: z.lazy(() => LearningCoursesContentSummarySchema).optional(),
  programUser: z.lazy(() => LearningCoursesProgramUserSummarySchema).optional(),
  isSubmitted: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  canModify: z.boolean().optional(),
  durationInMinutes: z.number().int().optional(),
  durationInSeconds: z.number().int().optional(),
});

/** Zod schema for LearningCoursesContentInteractionEvent */
LearningCoursesContentInteractionEventSchema = z.object({
  id: z.string().uuid().optional(),
  interactionId: z.string().uuid().optional(),
  type: z
    .lazy(() => LearningCoursesContentInteractionEventTypeSchema)
    .optional(),
  occurredAt: z.string().datetime().optional(),
  durationSeconds: z.number().int().nullable().optional(),
  positionSeconds: z.number().nullable().optional(),
  progressPercentage: z.number().nullable().optional(),
  payload: z.string().nullable().optional(),
  idempotencyKey: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesContentInteractionEventType */
LearningCoursesContentInteractionEventTypeSchema = z.enum([
  "Opened",
  "Heartbeat",
  "Progressed",
  "Paused",
  "Resumed",
  "Seeked",
  "Completed",
  "QuizPresented",
  "QuizAnswered",
]);

/** Zod schema for LearningCoursesContentInteractionSummary */
LearningCoursesContentInteractionSummarySchema = z.object({
  id: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  status: z.string().nullable().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  content: z.lazy(() => LearningCoursesContentSummarySchema).optional(),
  student: z.lazy(() => LearningCoursesStudentSummarySchema).optional(),
});

/** Zod schema for LearningCoursesContentProgress */
LearningCoursesContentProgressSchema = z.object({
  contentId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
  completionPercentage: z.number().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesContentStats */
LearningCoursesContentStatsSchema = z.object({
  programId: z.string().uuid().optional(),
  totalContent: z.number().int().optional(),
  requiredContent: z.number().int().optional(),
  optionalContent: z.number().int().optional(),
  contentByType: z
    .object({
      Lesson: z.number().int(),
      Page: z.number().int(),
      Assignment: z.number().int(),
      Questionnaire: z.number().int(),
      Discussion: z.number().int(),
      Code: z.number().int(),
      Challenge: z.number().int(),
      Reflection: z.number().int(),
      Survey: z.number().int(),
      Project: z.number().int(),
      Module: z.number().int(),
    })
    .nullable()
    .optional(),
  contentByVisibility: z
    .object({
      Public: z.number().int(),
      Internal: z.number().int(),
      Private: z.number().int(),
      Restricted: z.number().int(),
    })
    .nullable()
    .optional(),
  topLevelContent: z.number().int().optional(),
  nestedContent: z.number().int().optional(),
});

/** Zod schema for LearningCoursesContentSummary */
LearningCoursesContentSummarySchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  contentType: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesCourseSupportTicketMessageInput */
LearningCoursesCourseSupportTicketMessageInputSchema = z.object({
  message: z.string().nullable().optional(),
  isInternal: z.boolean().optional(),
});

/** Zod schema for LearningCoursesCreateActivityGrade */
LearningCoursesCreateActivityGradeSchema = z.object({
  contentInteractionId: z.string().uuid().optional(),
  graderProgramUserId: z.string().uuid().optional(),
  grade: z.number().optional(),
  feedback: z.string().nullable().optional(),
  gradingDetails: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreatePrerequisiteApiInput */
LearningCoursesCreatePrerequisiteApiInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
  minimumGrade: z.number().int().nullable().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreateProductFromProgram */
LearningCoursesCreateProductFromProgramSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  basePrice: z.number().optional(),
  currency: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreateProgram */
LearningCoursesCreateProgramSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  passingScore: z.number().optional(),
});

/** Zod schema for LearningCoursesCreateProgramContent */
LearningCoursesCreateProgramContentSchema = z.object({
  programId: z.string().uuid(),
  parentId: z.string().uuid().nullable().optional(),
  title: z.string().min(0).max(255),
  description: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema),
  body: z.string().nullable().optional(),
  jsonBody: z.record(z.string(), z.unknown()).nullable().optional(),
  lessonFormat: z
    .lazy(() => LearningCoursesLessonContentFormatSchema)
    .optional(),
  activitySettings: z
    .lazy(() => LearningCoursesActivitySettingsSchema)
    .optional(),
  sortOrder: z.number().int().optional(),
  isRequired: z.boolean().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesEngagementMetrics */
LearningCoursesEngagementMetricsSchema = z.object({
  programId: z.string().uuid().optional(),
  dailyActiveUsers: z.number().int().optional(),
  weeklyActiveUsers: z.number().int().optional(),
  monthlyActiveUsers: z.number().int().optional(),
  averageSessionDuration: z.string().optional(),
  totalSessions: z.number().int().optional(),
  retentionRate: z.number().optional(),
  contentEngagement: z
    .record(z.string(), z.number().int())
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesEnrollmentStatus */
LearningCoursesEnrollmentStatusSchema = z.enum([
  "Open",
  "Active",
  "Paused",
  "Cancelled",
  "Expired",
  "Completed",
  "Closed",
  "InviteOnly",
  "Waitlist",
]);

/** Zod schema for LearningCoursesGraderSummary */
LearningCoursesGraderSummarySchema = z.object({
  id: z.string().uuid().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesGradeStatistics */
LearningCoursesGradeStatisticsSchema = z.object({
  totalGrades: z.number().int().optional(),
  averageGrade: z.number().optional(),
  minGrade: z.number().optional(),
  maxGrade: z.number().optional(),
  passingRate: z.number().optional(),
  averageGradeFormatted: z.string().nullable().optional(),
  passingRateFormatted: z.string().nullable().optional(),
  hasGrades: z.boolean().optional(),
});

/** Zod schema for LearningCoursesGradingConfig */
LearningCoursesGradingConfigSchema = z.object({
  maxScore: z.number().int().optional(),
});

/** Zod schema for LearningCoursesLessonContentFormat */
LearningCoursesLessonContentFormatSchema = z.enum([
  "Markdown",
  "Lexical",
  "RevealJs",
  "Video",
  "Html",
  "ExternalLink",
]);

/** Zod schema for LearningCoursesMonetization */
LearningCoursesMonetizationSchema = z.object({
  price: z.number().optional(),
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesMoveContent */
LearningCoursesMoveContentSchema = z.object({
  contentId: z.string().uuid(),
  newParentId: z.string().uuid().nullable().optional(),
  newSortOrder: z.number().int(),
});

/** Zod schema for LearningCoursesPrerequisite */
LearningCoursesPrerequisiteSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  prerequisiteCourseName: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
  minimumGrade: z.number().int().nullable().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCoursesPrerequisiteCheckResult */
LearningCoursesPrerequisiteCheckResultSchema = z.object({
  isSatisfied: z.boolean().optional(),
  prerequisites: z
    .array(z.lazy(() => LearningCoursesPrerequisiteStatusSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesPrerequisiteStatus */
LearningCoursesPrerequisiteStatusSchema = z.object({
  prerequisiteId: z.string().uuid().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  courseName: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
  isSatisfied: z.boolean().optional(),
  requiredGrade: z.number().int().nullable().optional(),
  achievedGrade: z.number().int().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesPrerequisiteType */
LearningCoursesPrerequisiteTypeSchema = z.enum([
  "Required",
  "Recommended",
  "Corequisite",
]);

/** Zod schema for LearningCoursesPricing */
LearningCoursesPricingSchema = z.object({
  price: z.number().optional(),
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
  isMonetizationEnabled: z.boolean().optional(),
});

/** Zod schema for LearningCoursesProgram */
LearningCoursesProgramSchema = z.object({
  id: z.string().uuid().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  slug: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  thumbnail: z.string().nullable().optional(),
  videoShowcaseUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().nullable().optional(),
  passingScore: z.number().optional(),
  enrollmentStatus: z
    .lazy(() => LearningCoursesEnrollmentStatusSchema)
    .optional(),
  maxEnrollments: z.number().int().nullable().optional(),
  enrollmentDeadline: z.string().datetime().nullable().optional(),
  category: z.lazy(() => ProgramCategorySchema).optional(),
  difficulty: z.lazy(() => LearningCoursesProgramDifficultySchema).optional(),
  skillsRequired: z.string().nullable().optional(),
  skillsProvided: z.string().nullable().optional(),
  currentEnrollments: z.number().int().optional(),
  averageRating: z.number().optional(),
  totalRatings: z.number().int().optional(),
  isEnrollmentOpen: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesProgramAnalytics */
LearningCoursesProgramAnalyticsSchema = z.object({
  programId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  totalUsers: z.number().int().optional(),
  activeUsers: z.number().int().optional(),
  completedUsers: z.number().int().optional(),
  completionRate: z.number().optional(),
  averageCompletionTime: z.string().optional(),
  totalViews: z.number().int().optional(),
  lastActivity: z.string().datetime().nullable().optional(),
  additionalMetrics: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesProgramContent */
LearningCoursesProgramContentSchema = z.object({
  id: z.string().uuid().optional(),
  programId: z.string().uuid().optional(),
  parentId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  body: z.string().nullable().optional(),
  jsonBody: z.record(z.string(), z.unknown()).nullable().optional(),
  lessonFormat: z
    .lazy(() => LearningCoursesLessonContentFormatSchema)
    .optional(),
  activitySettings: z
    .lazy(() => LearningCoursesActivitySettingsSchema)
    .optional(),
  sortOrder: z.number().int().optional(),
  isRequired: z.boolean().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  programTitle: z.string().nullable().optional(),
  parentTitle: z.string().nullable().optional(),
  childrenCount: z.number().int().optional(),
  children: z
    .array(z.lazy(() => LearningCoursesProgramContentSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesProgramContentType. Legacy values Page and Challenge are normalized on read and are not valid for new content. */
LearningCoursesProgramContentTypeSchema = z.enum([
  "Lesson",
  "Assignment",
  "Questionnaire",
  "Discussion",
  "Code",
  "Reflection",
  "Survey",
  "Project",
  "Module",
]);

/** Zod schema for LearningCoursesProgramDifficulty */
LearningCoursesProgramDifficultySchema = z.enum([
  "Beginner",
  "Intermediate",
  "Advanced",
  "Expert",
]);

/** Zod schema for LearningCoursesProgramUserSummary */
LearningCoursesProgramUserSummarySchema = z.object({
  id: z.string().uuid().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesProgressStatus */
LearningCoursesProgressStatusSchema = z.enum([
  "NotStarted",
  "InProgress",
  "Completed",
  "Submitted",
]);

/** Zod schema for LearningCoursesRecordContentInteractionEventInput */
LearningCoursesRecordContentInteractionEventInputSchema = z.object({
  type: z
    .lazy(() => LearningCoursesContentInteractionEventTypeSchema)
    .optional(),
  durationSeconds: z.number().int().nullable().optional(),
  positionSeconds: z.number().nullable().optional(),
  progressPercentage: z.number().nullable().optional(),
  payload: z.string().nullable().optional(),
  idempotencyKey: z.string().nullable().optional(),
  occurredAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesReflectionResponseResult */
LearningCoursesReflectionResponseResultSchema = z.object({
  responseId: z.string().uuid().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  body: z.string().nullable().optional(),
  respondentUserId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCoursesRejectProgram */
LearningCoursesRejectProgramSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesReorderContent */
LearningCoursesReorderContentSchema = z.object({
  contentIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCoursesReorderPrerequisitesInput */
LearningCoursesReorderPrerequisitesInputSchema = z.object({
  prerequisiteIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCoursesResolveCourseSupportTicketInput */
LearningCoursesResolveCourseSupportTicketInputSchema = z.object({
  summary: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesRevenueAnalytics */
LearningCoursesRevenueAnalyticsSchema = z.object({
  programId: z.string().uuid().optional(),
  totalRevenue: z.number().optional(),
  monthlyRevenue: z.number().optional(),
  totalPurchases: z.number().int().optional(),
  monthlyPurchases: z.number().int().optional(),
  averageRevenuePerUser: z.number().optional(),
  conversionRate: z.number().optional(),
  revenueChart: z
    .array(z.lazy(() => LearningCoursesRevenueChartSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesRevenueChart */
LearningCoursesRevenueChartSchema = z.object({
  date: z.string().datetime().optional(),
  revenue: z.number().optional(),
  purchases: z.number().int().optional(),
});

/** Zod schema for LearningCoursesScheduleProgram */
LearningCoursesScheduleProgramSchema = z.object({
  publishAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCoursesSearchContent */
LearningCoursesSearchContentSchema = z.object({
  programId: z.string().uuid(),
  searchTerm: z.string().min(0).max(255),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
  isRequired: z.boolean().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCoursesSendCourseStudentMessageInput */
LearningCoursesSendCourseStudentMessageInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
  subject: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesSendCourseStudentMessageOutput */
LearningCoursesSendCourseStudentMessageOutputSchema = z.object({
  sent: z.number().int().optional(),
});

/** Zod schema for LearningCoursesStartContentInput */
LearningCoursesStartContentInputSchema = z.object({
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesStudentSummary */
LearningCoursesStudentSummarySchema = z.object({
  id: z.string().uuid().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesSubmitContentInput */
LearningCoursesSubmitContentInputSchema = z.object({
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  submissionData: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesSubmitUserContent */
LearningCoursesSubmitUserContentSchema = z.object({
  submissionData: z.string().min(1),
});

/** Zod schema for LearningCoursesSurveyResponseResult */
LearningCoursesSurveyResponseResultSchema = z.object({
  responseId: z.string().uuid().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  answers: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
  respondentUserId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCoursesTest */
LearningCoursesTestSchema = z.object({
  weight: z.number().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesTestSuite */
LearningCoursesTestSuiteSchema = z.object({
  public: z
    .array(z.lazy(() => LearningCoursesTestSchema))
    .nullable()
    .optional(),
  private: z
    .array(z.lazy(() => LearningCoursesTestSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesUpdateActivityGrade */
LearningCoursesUpdateActivityGradeSchema = z.object({
  grade: z.number().nullable().optional(),
  feedback: z.string().nullable().optional(),
  gradingDetails: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdatePrerequisiteApiInput */
LearningCoursesUpdatePrerequisiteApiInputSchema = z.object({
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
  minimumGrade: z.number().int().nullable().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdatePricing */
LearningCoursesUpdatePricingSchema = z.object({
  price: z.number().nullable().optional(),
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().nullable().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdateProgram */
LearningCoursesUpdateProgramSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  videoShowcaseUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  category: z.lazy(() => ProgramCategorySchema).optional(),
  difficulty: z.lazy(() => LearningCoursesProgramDifficultySchema).optional(),
  skillsRequired: z.string().nullable().optional(),
  skillsProvided: z.string().nullable().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  enrollmentStatus: z
    .lazy(() => LearningCoursesEnrollmentStatusSchema)
    .optional(),
  maxEnrollments: z.number().int().nullable().optional(),
  enrollmentDeadline: z.string().datetime().nullable().optional(),
  passingScore: z.number().nullable().optional(),
  clearMaxEnrollments: z.boolean().optional(),
  clearEnrollmentDeadline: z.boolean().optional(),
});

/** Zod schema for LearningCoursesUpdateProgramContent */
LearningCoursesUpdateProgramContentSchema = z.object({
  id: z.string().uuid(),
  title: z.string().min(0).max(255).nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  body: z.string().nullable().optional(),
  jsonBody: z.record(z.string(), z.unknown()).nullable().optional(),
  lessonFormat: z
    .lazy(() => LearningCoursesLessonContentFormatSchema)
    .optional(),
  activitySettings: z
    .lazy(() => LearningCoursesActivitySettingsSchema)
    .optional(),
  sortOrder: z.number().int().nullable().optional(),
  isRequired: z.boolean().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesUpdateProgress */
LearningCoursesUpdateProgressSchema = z.object({
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  additionalData: z
    .record(z.string(), z.record(z.string(), z.unknown()))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesUpdateProgressInput */
LearningCoursesUpdateProgressInputSchema = z.object({
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  completionPercentage: z.number().optional(),
});

/** Zod schema for LearningCoursesUpdateTimeSpentInput */
LearningCoursesUpdateTimeSpentInputSchema = z.object({
  programUserId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  additionalMinutes: z.number().int().optional(),
});

/** Zod schema for LearningCoursesUserProgress */
LearningCoursesUserProgressSchema = z.object({
  enrollmentId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  completionPercentage: z.number().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  startedAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  contentProgress: z
    .array(z.lazy(() => LearningCoursesContentProgressSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesVisibility */
LearningCoursesVisibilitySchema = z.enum([
  "Public",
  "Internal",
  "Private",
  "Restricted",
]);

/** Zod schema for LearningCoursesWorkspaceData */
LearningCoursesWorkspaceDataSchema = z.object({
  files: z
    .record(
      z.string(),
      z.lazy(() => LearningCoursesBundleFileMetaSchema),
    )
    .nullable()
    .optional(),
});

/** Zod schema for LearningEnrollmentsEnrollment */
LearningEnrollmentsEnrollmentSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  cohortId: z.string().uuid().nullable().optional(),
  status: z.lazy(() => LearningEnrollmentsEnrollmentStatusSchema).optional(),
  enrolledAt: z.string().datetime().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  droppedAt: z.string().datetime().nullable().optional(),
  progress: z.number().int().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningEnrollmentsEnrollmentStatus */
LearningEnrollmentsEnrollmentStatusSchema = z.enum([
  "Active",
  "Paused",
  "Completed",
  "Dropped",
  "Expired",
]);

/** Zod schema for LearningEnrollmentsEnrollUserInput */
LearningEnrollmentsEnrollUserInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  cohortId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningEnrollmentsUpdateEnrollmentProgressInput */
LearningEnrollmentsUpdateEnrollmentProgressInputSchema = z.object({
  progress: z.number().int().optional(),
});

/** Zod schema for LearningExperienceDiscoveryCollectionType */
LearningExperienceDiscoveryCollectionTypeSchema = z.enum([
  "Curated",
  "Category",
  "Skill",
  "Career",
  "Trending",
  "NewReleases",
]);

/** Zod schema for LearningExperienceDiscoveryCourseCollection */
LearningExperienceDiscoveryCourseCollectionSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  curatorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isPublished: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  courseCount: z.number().int().optional(),
  type: z
    .lazy(() => LearningExperienceDiscoveryCollectionTypeSchema)
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceDiscoveryCreateCourseCollection */
LearningExperienceDiscoveryCreateCourseCollectionSchema = z.object({
  title: z.string().nullable().optional(),
  type: z
    .lazy(() => LearningExperienceDiscoveryCollectionTypeSchema)
    .optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryCreateFeaturedContent */
LearningExperienceDiscoveryCreateFeaturedContentSchema = z.object({
  type: z
    .lazy(() => LearningExperienceDiscoveryFeaturedContentTypeSchema)
    .optional(),
  title: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  courseId: z.string().uuid().nullable().optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  targetAudience: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryFeaturedContent */
LearningExperienceDiscoveryFeaturedContentSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().nullable().optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  type: z
    .lazy(() => LearningExperienceDiscoveryFeaturedContentTypeSchema)
    .optional(),
  displayOrder: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  targetAudience: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceDiscoveryFeaturedContentType */
LearningExperienceDiscoveryFeaturedContentTypeSchema = z.enum([
  "HeroBanner",
  "CategoryHighlight",
  "NewRelease",
  "TopRated",
  "TrendingNow",
  "StaffPick",
  "SeasonalPromotion",
]);

/** Zod schema for LearningExperienceDiscoveryPopularSearchResult */
LearningExperienceDiscoveryPopularSearchResultSchema = z.object({
  query: z.string().nullable().optional(),
  searchCount: z.number().int().optional(),
  totalClicks: z.number().int().optional(),
  clickThroughRate: z.number().optional(),
});

/** Zod schema for LearningExperienceDiscoveryRecordSearch */
LearningExperienceDiscoveryRecordSearchSchema = z.object({
  query: z.string().nullable().optional(),
  resultCount: z.number().int().optional(),
  filters: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryRecordSearchClick */
LearningExperienceDiscoveryRecordSearchClickSchema = z.object({
  searchId: z.string().uuid().optional(),
  clickedCourseId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceDiscoverySearchHistory */
LearningExperienceDiscoverySearchHistorySchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().nullable().optional(),
  query: z.string().nullable().optional(),
  resultCount: z.number().int().optional(),
  clickedCourseId: z.string().uuid().nullable().optional(),
  filters: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceDiscoveryUpdateCourseCollection */
LearningExperienceDiscoveryUpdateCourseCollectionSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryUpdateFeaturedContent */
LearningExperienceDiscoveryUpdateFeaturedContentSchema = z.object({
  title: z.string().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  targetAudience: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceLearningPathsAddCourseToPath */
LearningExperienceLearningPathsAddCourseToPathSchema = z.object({
  courseId: z.string().uuid().optional(),
  order: z.number().int().optional(),
  isRequired: z.boolean().optional(),
});

/** Zod schema for LearningExperienceLearningPathsCourseOrder */
LearningExperienceLearningPathsCourseOrderSchema = z.object({
  courseId: z.string().uuid().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsCreateLearningPath */
LearningExperienceLearningPathsCreateLearningPathSchema = z.object({
  title: z.string().nullable().optional(),
  difficulty: z
    .lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema)
    .optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPath */
LearningExperienceLearningPathsLearningPathSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  creatorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().optional(),
  difficulty: z
    .lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema)
    .optional(),
  isPublished: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  enrollmentCount: z.number().int().optional(),
  completionCount: z.number().int().optional(),
  courseCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathCourse */
LearningExperienceLearningPathsLearningPathCourseSchema = z.object({
  courseId: z.string().uuid().optional(),
  order: z.number().int().optional(),
  isRequired: z.boolean().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathDetail */
LearningExperienceLearningPathsLearningPathDetailSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  creatorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().optional(),
  difficulty: z
    .lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema)
    .optional(),
  isPublished: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  enrollmentCount: z.number().int().optional(),
  completionCount: z.number().int().optional(),
  courses: z
    .array(
      z.lazy(() => LearningExperienceLearningPathsLearningPathCourseSchema),
    )
    .nullable()
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathDifficulty */
LearningExperienceLearningPathsLearningPathDifficultySchema = z.enum([
  "Beginner",
  "Intermediate",
  "Advanced",
  "Expert",
]);

/** Zod schema for LearningExperienceLearningPathsLearningPathEnrollment */
LearningExperienceLearningPathsLearningPathEnrollmentSchema = z.object({
  id: z.string().uuid().optional(),
  learningPathId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  progress: z.number().int().optional(),
  coursesCompleted: z.number().int().optional(),
  totalCourses: z.number().int().optional(),
  enrolledAt: z.string().datetime().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  status: z
    .lazy(
      () => LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema,
    )
    .optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathEnrollmentStatus */
LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema = z.enum([
  "InProgress",
  "Completed",
  "Abandoned",
]);

/** Zod schema for LearningExperienceLearningPathsLearningPathStatistics */
LearningExperienceLearningPathsLearningPathStatisticsSchema = z.object({
  learningPathId: z.string().uuid().optional(),
  totalEnrollments: z.number().int().optional(),
  activeEnrollments: z.number().int().optional(),
  completedEnrollments: z.number().int().optional(),
  completionRate: z.number().optional(),
  averageProgress: z.number().optional(),
  averageCompletionTime: z.string().optional(),
});

/** Zod schema for LearningExperienceLearningPathsReorderCourses */
LearningExperienceLearningPathsReorderCoursesSchema = z.object({
  courses: z
    .array(z.lazy(() => LearningExperienceLearningPathsCourseOrderSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningExperienceLearningPathsUpdateLearningPath */
LearningExperienceLearningPathsUpdateLearningPathSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  estimatedHours: z.number().int().nullable().optional(),
  difficulty: z
    .lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema)
    .optional(),
  isFeatured: z.boolean().nullable().optional(),
});

/** Zod schema for LearningExperienceLearningPathsUpdatePathProgress */
LearningExperienceLearningPathsUpdatePathProgressSchema = z.object({
  coursesCompleted: z.number().int().optional(),
});

/** Zod schema for LearningExperienceRecommendationsAddSkillInput */
LearningExperienceRecommendationsAddSkillInputSchema = z.object({
  skill: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceRecommendationsCreateOrUpdateLearningProfile */
LearningExperienceRecommendationsCreateOrUpdateLearningProfileSchema = z.object(
  {
    preferredCategories: z.array(z.string()).nullable().optional(),
    preferredDifficulty: z.string().nullable().optional(),
    preferredDuration: z.string().nullable().optional(),
    learningGoals: z.array(z.string()).nullable().optional(),
    skills: z.array(z.string()).nullable().optional(),
  },
);

/** Zod schema for LearningExperienceRecommendationsPopularCourse */
LearningExperienceRecommendationsPopularCourseSchema = z.object({
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  enrollmentCount: z.number().int().optional(),
  averageRating: z.number().optional(),
  totalRatings: z.number().int().optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendation */
LearningExperienceRecommendationsRecommendationSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  type: z
    .lazy(() => LearningExperienceRecommendationsRecommendationTypeSchema)
    .optional(),
  score: z.number().optional(),
  reason: z.string().nullable().optional(),
  isViewed: z.boolean().optional(),
  isDismissed: z.boolean().optional(),
  expiresAt: z.string().datetime().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendationStatistics */
LearningExperienceRecommendationsRecommendationStatisticsSchema = z.object({
  totalRecommendations: z.number().int().optional(),
  viewedCount: z.number().int().optional(),
  dismissedCount: z.number().int().optional(),
  convertedCount: z.number().int().optional(),
  byType: z
    .object({
      PersonalizedAI: z.number().int(),
      PopularInCategory: z.number().int(),
      TrendingNow: z.number().int(),
      BasedOnHistory: z.number().int(),
      SimilarToCompleted: z.number().int(),
      NextInPath: z.number().int(),
      InstructorFollowed: z.number().int(),
      PeerRecommended: z.number().int(),
    })
    .nullable()
    .optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendationType */
LearningExperienceRecommendationsRecommendationTypeSchema = z.enum([
  "PersonalizedAI",
  "PopularInCategory",
  "TrendingNow",
  "BasedOnHistory",
  "SimilarToCompleted",
  "NextInPath",
  "InstructorFollowed",
  "PeerRecommended",
]);

/** Zod schema for LearningExperienceRecommendationsSimilarCourse */
LearningExperienceRecommendationsSimilarCourseSchema = z.object({
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  similarityScore: z.number().optional(),
  matchingTags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for LearningExperienceRecommendationsTrendingCourse */
LearningExperienceRecommendationsTrendingCourseSchema = z.object({
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  recentEnrollments: z.number().int().optional(),
  trendScore: z.number().optional(),
});

/** Zod schema for LearningExperienceRecommendationsUserLearningProfile */
LearningExperienceRecommendationsUserLearningProfileSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  preferredCategories: z.array(z.string()).nullable().optional(),
  preferredDifficulty: z.string().nullable().optional(),
  preferredDuration: z.string().nullable().optional(),
  learningGoals: z.array(z.string()).nullable().optional(),
  skills: z.array(z.string()).nullable().optional(),
  totalCoursesCompleted: z.number().int().optional(),
  totalHoursLearned: z.number().int().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialControllersUpdateReviewModerationInput */
LearningExperienceSocialControllersUpdateReviewModerationInputSchema = z.object(
  {
    isApproved: z.boolean().optional(),
    isFeatured: z.boolean().optional(),
  },
);

/** Zod schema for LearningExperienceSocialFeedItemType */
LearningExperienceSocialFeedItemTypeSchema = z.enum([
  "NewCourse",
  "PopularCourse",
  "TrendingDiscussion",
  "FeaturedReview",
  "LearningPathSuggestion",
  "CourseUpdate",
  "InstructorActivity",
  "PeerActivity",
  "AchievementUnlocked",
  "SkillMilestone",
]);

/** Zod schema for LearningExperienceSocialServicesCourseDiscussion */
LearningExperienceSocialServicesCourseDiscussionSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  authorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  isResolved: z.boolean().optional(),
  replyCount: z.number().int().optional(),
  viewCount: z.number().int().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseLike */
LearningExperienceSocialServicesCourseLikeSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseRatingStats */
LearningExperienceSocialServicesCourseRatingStatsSchema = z.object({
  courseId: z.string().uuid().optional(),
  averageRating: z.number().optional(),
  totalReviews: z.number().int().optional(),
  fiveStarCount: z.number().int().optional(),
  fourStarCount: z.number().int().optional(),
  threeStarCount: z.number().int().optional(),
  twoStarCount: z.number().int().optional(),
  oneStarCount: z.number().int().optional(),
  featuredReviewCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseReview */
LearningExperienceSocialServicesCourseReviewSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  rating: z.number().int().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  isVerifiedPurchase: z.boolean().optional(),
  helpfulCount: z.number().int().optional(),
  isApproved: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseWishlist */
LearningExperienceSocialServicesCourseWishlistSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  notifyOnSale: z.boolean().optional(),
  notifyOnUpdate: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateDiscussionInput */
LearningExperienceSocialServicesCreateDiscussionInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateReplyInput */
LearningExperienceSocialServicesCreateReplyInputSchema = z.object({
  discussionId: z.string().uuid().optional(),
  content: z.string().nullable().optional(),
  parentReplyId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateReviewInput */
LearningExperienceSocialServicesCreateReviewInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  rating: z.number().int().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  enrollmentId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesDiscussionReply */
LearningExperienceSocialServicesDiscussionReplySchema = z.object({
  id: z.string().uuid().optional(),
  discussionId: z.string().uuid().optional(),
  authorId: z.string().uuid().optional(),
  parentReplyId: z.string().uuid().nullable().optional(),
  content: z.string().nullable().optional(),
  isAcceptedAnswer: z.boolean().optional(),
  upvoteCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesPersonalizedFeedItem */
LearningExperienceSocialServicesPersonalizedFeedItemSchema = z.object({
  id: z.string().uuid().optional(),
  itemType: z.lazy(() => LearningExperienceSocialFeedItemTypeSchema).optional(),
  courseId: z.string().uuid().nullable().optional(),
  discussionId: z.string().uuid().nullable().optional(),
  reviewId: z.string().uuid().nullable().optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  relevanceScore: z.number().optional(),
  reason: z.string().nullable().optional(),
  isViewed: z.boolean().optional(),
  expiresAt: z.string().datetime().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceSocialServicesWishlistPreferencesInput */
LearningExperienceSocialServicesWishlistPreferencesInputSchema = z.object({
  notifyOnSale: z.boolean().optional(),
  notifyOnUpdate: z.boolean().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAnnouncement */
LearningWorkspacesLearnerAnnouncementSchema = z.object({
  discussionId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  courseTitle: z.string().nullable().optional(),
  courseSlug: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessment */
LearningWorkspacesLearnerAssessmentSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  maxScore: z.number().int().optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  isRequired: z.boolean().optional(),
  order: z.number().int().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  allowLateSubmissions: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  submissionModalities: z.string().nullable().optional(),
  presentationMode: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentDeadline */
LearningWorkspacesLearnerAssessmentDeadlineSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  courseTitle: z.string().nullable().optional(),
  courseSlug: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  maxScore: z.number().int().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  submissionStatus: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentGroup */
LearningWorkspacesLearnerAssessmentGroupSchema = z.object({
  groupId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  weightPercent: z.number().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentSubmission */
LearningWorkspacesLearnerAssessmentSubmissionSchema = z.object({
  submissionId: z.string().uuid().optional(),
  assessmentId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  score: z.number().int().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  feedback: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  isLate: z.boolean().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCertificate */
LearningWorkspacesLearnerCertificateSchema = z.object({
  certificateId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  courseName: z.string().nullable().optional(),
  certificateNumber: z.string().nullable().optional(),
  recipientName: z.string().nullable().optional(),
  issuedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  verificationUrl: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCohort */
LearningWorkspacesLearnerCohortSchema = z.object({
  cohortId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  maxCapacity: z.number().int().optional(),
  currentEnrollmentCount: z.number().int().optional(),
  status: z.string().nullable().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  meetingSchedule: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerContent */
LearningWorkspacesLearnerContentSchema = z.object({
  contentId: z.string().uuid().optional(),
  parentId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  lessonFormat: z.string().nullable().optional(),
  activitySettings: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  isRequired: z.boolean().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  visibility: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerContentProgress */
LearningWorkspacesLearnerContentProgressSchema = z.object({
  contentId: z.string().uuid().optional(),
  status: z.string().nullable().optional(),
  progressPercentage: z.number().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  timeSpentSeconds: z.number().int().optional(),
  score: z.number().nullable().optional(),
  maxScore: z.number().nullable().optional(),
  attempts: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCourseSummary */
LearningWorkspacesLearnerCourseSummarySchema = z.object({
  courseId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  difficulty: z.string().nullable().optional(),
  estimatedHours: z.number().int().nullable().optional(),
  enrollmentStatus: z.string().nullable().optional(),
  completionStatus: z.string().nullable().optional(),
  progressPercentage: z.number().optional(),
  finalGrade: z.number().nullable().optional(),
  enrolledAt: z.string().datetime().optional(),
  totalItems: z.number().int().optional(),
  completedItems: z.number().int().optional(),
  remainingMinutes: z.number().int().optional(),
  currentContentId: z.string().uuid().nullable().optional(),
  currentContentTitle: z.string().nullable().optional(),
  currentContentType: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCourseWorkspace */
LearningWorkspacesLearnerCourseWorkspaceSchema = z.object({
  course: z.lazy(() => LearningWorkspacesLearnerCourseSummarySchema).optional(),
  content: z
    .array(z.lazy(() => LearningWorkspacesLearnerContentSchema))
    .nullable()
    .optional(),
  progress: z
    .array(z.lazy(() => LearningWorkspacesLearnerContentProgressSchema))
    .nullable()
    .optional(),
  cohort: z.lazy(() => LearningWorkspacesLearnerCohortSchema).optional(),
  calendar: z
    .array(z.lazy(() => LearningWorkspacesLearnerScheduleEntrySchema))
    .nullable()
    .optional(),
  assessmentGroups: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentGroupSchema))
    .nullable()
    .optional(),
  assessments: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentSchema))
    .nullable()
    .optional(),
  submissions: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentSubmissionSchema))
    .nullable()
    .optional(),
  discussions: z
    .array(z.lazy(() => LearningWorkspacesLearnerDiscussionSchema))
    .nullable()
    .optional(),
  certificates: z
    .array(z.lazy(() => LearningWorkspacesLearnerCertificateSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningWorkspacesLearnerDashboard */
LearningWorkspacesLearnerDashboardSchema = z.object({
  courses: z
    .array(z.lazy(() => LearningWorkspacesLearnerCourseSummarySchema))
    .nullable()
    .optional(),
  upcoming: z
    .array(z.lazy(() => LearningWorkspacesLearnerScheduleEntrySchema))
    .nullable()
    .optional(),
  deadlines: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentDeadlineSchema))
    .nullable()
    .optional(),
  grades: z
    .array(z.lazy(() => LearningWorkspacesLearnerGradeSummarySchema))
    .nullable()
    .optional(),
  certificates: z
    .array(z.lazy(() => LearningWorkspacesLearnerCertificateSchema))
    .nullable()
    .optional(),
  announcements: z
    .array(z.lazy(() => LearningWorkspacesLearnerAnnouncementSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningWorkspacesLearnerDiscussion */
LearningWorkspacesLearnerDiscussionSchema = z.object({
  discussionId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  authorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  isResolved: z.boolean().optional(),
  replyCount: z.number().int().optional(),
  viewCount: z.number().int().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for LearningWorkspacesLearnerGradeItem */
LearningWorkspacesLearnerGradeItemSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  maxScore: z.number().int().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  submissionStatus: z.string().nullable().optional(),
  score: z.number().int().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  feedback: z.string().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerGradeSummary */
LearningWorkspacesLearnerGradeSummarySchema = z.object({
  courseId: z.string().uuid().optional(),
  courseTitle: z.string().nullable().optional(),
  courseSlug: z.string().nullable().optional(),
  finalGrade: z.number().nullable().optional(),
  gradedAssessments: z.number().int().optional(),
  totalAssessments: z.number().int().optional(),
  earnedPoints: z.number().nullable().optional(),
  possiblePoints: z.number().nullable().optional(),
  percentage: z.number().nullable().optional(),
  groups: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentGroupSchema))
    .nullable()
    .optional(),
  items: z
    .array(z.lazy(() => LearningWorkspacesLearnerGradeItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningWorkspacesLearnerScheduleEntry */
LearningWorkspacesLearnerScheduleEntrySchema = z.object({
  courseId: z.string().uuid().optional(),
  courseTitle: z.string().nullable().optional(),
  courseSlug: z.string().nullable().optional(),
  cohortId: z.string().uuid().optional(),
  cohortName: z.string().nullable().optional(),
  scheduleItemId: z.string().uuid().optional(),
  contentId: z.string().uuid().nullable().optional(),
  assessmentId: z.string().uuid().nullable().optional(),
  type: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerSearchResult */
LearningWorkspacesLearnerSearchResultSchema = z.object({
  id: z.string().uuid().optional(),
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  kind: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  route: z.string().nullable().optional(),
});

/** Zod schema for Money */
MoneySchema = z.object({
  amount: z.number().optional(),
  currency: z.string().nullable().optional(),
});

/** Zod schema for MonitoringSLACreateSloCommand */
MonitoringSLACreateSloCommandSchema = z.object({
  tenantId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  targetPercentage: z.number().optional(),
  timeWindowDays: z.number().int().optional(),
  errorBudgetPercentage: z.number().optional(),
  alertThresholdPercentage: z.number().optional(),
});

/** Zod schema for MonitoringSLAErrorBudget */
MonitoringSLAErrorBudgetSchema = z.object({
  serviceLevelObjectiveId: z.string().uuid().optional(),
  targetPercentage: z.number().optional(),
  errorBudgetPercentage: z.number().optional(),
  actualPercentage: z.number().optional(),
  totalRequests: z.number().int().optional(),
  successfulRequests: z.number().int().optional(),
  failedRequests: z.number().int().optional(),
  allowedFailures: z.number().int().optional(),
  remainingBudget: z.number().int().optional(),
  remainingBudgetPercentage: z.number().optional(),
  burnRate: z.number().optional(),
  timeToExhaustionHours: z.number().nullable().optional(),
  timeWindowDays: z.number().int().optional(),
  windowStart: z.string().datetime().optional(),
  windowEnd: z.string().datetime().optional(),
  isHealthy: z.boolean().optional(),
});

/** Zod schema for MonitoringSLARecordSliMetricCommand */
MonitoringSLARecordSliMetricCommandSchema = z.object({
  tenantId: z.string().uuid().optional(),
  serviceLevelObjectiveId: z.string().uuid().optional(),
  isSuccessful: z.boolean().optional(),
  value: z.number().optional(),
  responseTimeMs: z.number().int().nullable().optional(),
  statusCode: z.number().int().nullable().optional(),
  endpoint: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
});

/** Zod schema for MonitoringSLAResolveSloViolationCommand */
MonitoringSLAResolveSloViolationCommandSchema = z.object({
  violationId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  resolutionNotes: z.string().nullable().optional(),
});

/** Zod schema for MonitoringSLASlo */
MonitoringSLASloSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  targetPercentage: z.number().optional(),
  timeWindowDays: z.number().int().optional(),
  errorBudgetPercentage: z.number().optional(),
  alertThresholdPercentage: z.number().optional(),
  isEnabled: z.boolean().optional(),
  status: z.lazy(() => MonitoringSLASloStatusSchema).optional(),
  lastEvaluatedAt: z.string().datetime().nullable().optional(),
  currentActualPercentage: z.number().nullable().optional(),
  remainingErrorBudget: z.number().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for MonitoringSLASloCompliance */
MonitoringSLASloComplianceSchema = z.object({
  serviceLevelObjectiveId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  targetPercentage: z.number().optional(),
  actualPercentage: z.number().optional(),
  isCompliant: z.boolean().optional(),
  status: z.lazy(() => MonitoringSLASloStatusSchema).optional(),
  timeWindowDays: z.number().int().optional(),
  periodStart: z.string().datetime().optional(),
  periodEnd: z.string().datetime().optional(),
  totalMeasurements: z.number().int().optional(),
  successfulMeasurements: z.number().int().optional(),
  violationCount: z.number().int().optional(),
  totalDowntimeMinutes: z.number().optional(),
  remainingErrorBudget: z.number().nullable().optional(),
  calculatedAt: z.string().datetime().optional(),
});

/** Zod schema for MonitoringSLASloStatus */
MonitoringSLASloStatusSchema = z.enum([
  "Active",
  "Breached",
  "AtRisk",
  "Disabled",
  "Violated",
  "Warning",
  "Inactive",
]);

/** Zod schema for MonitoringSLASloViolation */
MonitoringSLASloViolationSchema = z.object({
  id: z.string().uuid().optional(),
  serviceLevelObjectiveId: z.string().uuid().optional(),
  sloName: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  endedAt: z.string().datetime().nullable().optional(),
  durationMinutes: z.number().optional(),
  actualValue: z.number().optional(),
  targetValue: z.number().optional(),
  severity: z.lazy(() => MonitoringSLAViolationSeveritySchema).optional(),
  alertTriggered: z.boolean().optional(),
  alertSentAt: z.string().datetime().nullable().optional(),
  isAcknowledged: z.boolean().optional(),
  acknowledgedByUserId: z.string().uuid().nullable().optional(),
  acknowledgedAt: z.string().datetime().nullable().optional(),
  notes: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isOngoing: z.boolean().optional(),
});

/** Zod schema for MonitoringSLAUpdateSloCommand */
MonitoringSLAUpdateSloCommandSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  serviceName: z.string().nullable().optional(),
  targetPercentage: z.number().optional(),
  timeWindowDays: z.number().int().optional(),
  errorBudgetPercentage: z.number().optional(),
  alertThresholdPercentage: z.number().optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for MonitoringSLAViolationSeverity */
MonitoringSLAViolationSeveritySchema = z.enum([
  "Low",
  "Medium",
  "High",
  "Critical",
]);

/** Zod schema for MvcProblemDetails */
MvcProblemDetailsSchema = z
  .object({
    type: z.string().nullable().optional(),
    title: z.string().nullable().optional(),
    status: z.number().int().nullable().optional(),
    detail: z.string().nullable().optional(),
    instance: z.string().nullable().optional(),
  })
  .catchall(z.record(z.string(), z.unknown()));

/** Zod schema for NotificationsControllersDeletedCountOutput */
NotificationsControllersDeletedCountOutputSchema = z.object({
  deletedCount: z.number().int().optional(),
});

/** Zod schema for NotificationsControllersNotification */
NotificationsControllersNotificationSchema = z.object({
  id: z.string().uuid().optional(),
  type: z.string().nullable().optional(),
  channel: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  actionUrl: z.string().nullable().optional(),
  iconUrl: z.string().nullable().optional(),
  isRead: z.boolean().optional(),
  readAt: z.string().datetime().nullable().optional(),
  priority: z.string().nullable().optional(),
  referenceEntityId: z.string().uuid().nullable().optional(),
  referenceEntityType: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for NotificationsControllersNotificationPreference */
NotificationsControllersNotificationPreferenceSchema = z.object({
  emailEnabled: z.boolean().optional(),
  pushEnabled: z.boolean().optional(),
  inAppEnabled: z.boolean().optional(),
  smsEnabled: z.boolean().optional(),
  marketingEnabled: z.boolean().optional(),
  socialEnabled: z.boolean().optional(),
  learningEnabled: z.boolean().optional(),
  achievementsEnabled: z.boolean().optional(),
  quietHoursStart: z.string().nullable().optional(),
  quietHoursEnd: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
  emailDigestFrequency: z.string().nullable().optional(),
});

/** Zod schema for NotificationsControllersSetQuietHoursInput */
NotificationsControllersSetQuietHoursInputSchema = z.object({
  start: z.string().nullable().optional(),
  end: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
});

/** Zod schema for NotificationsControllersUnreadCountOutput */
NotificationsControllersUnreadCountOutputSchema = z.object({
  count: z.number().int().optional(),
});

/** Zod schema for NotificationsControllersUpdatePreferencesInput */
NotificationsControllersUpdatePreferencesInputSchema = z.object({
  emailEnabled: z.boolean().nullable().optional(),
  pushEnabled: z.boolean().nullable().optional(),
  inAppEnabled: z.boolean().nullable().optional(),
  smsEnabled: z.boolean().nullable().optional(),
  marketingEnabled: z.boolean().nullable().optional(),
  socialEnabled: z.boolean().nullable().optional(),
  learningEnabled: z.boolean().nullable().optional(),
  achievementsEnabled: z.boolean().nullable().optional(),
});

/** Zod schema for NotificationsNotificationChannel */
NotificationsNotificationChannelSchema = z.enum([
  "InApp",
  "Email",
  "Push",
  "Sms",
  "Slack",
  "Discord",
  "Webhook",
]);

/** Zod schema for ObjectsAttestationConveyancePreference */
ObjectsAttestationConveyancePreferenceSchema = z.enum([
  "None",
  "Indirect",
  "Direct",
  "Enterprise",
]);

/** Zod schema for ObjectsAttestationStatementFormatIdentifier */
ObjectsAttestationStatementFormatIdentifierSchema = z.enum([
  "Packed",
  "Tpm",
  "AndroidKey",
  "AndroidSafetyNet",
  "FidoU2f",
  "Apple",
  "None",
]);

/** Zod schema for ObjectsAuthenticationExtensionsClientInputs */
ObjectsAuthenticationExtensionsClientInputsSchema = z.object({
  "example.extension.bool": z.boolean().nullable().optional(),
  exts: z.boolean().nullable().optional(),
  uvm: z.boolean().nullable().optional(),
  credProps: z.boolean().nullable().optional(),
  prf: z.lazy(() => ObjectsAuthenticationExtensionsPRFInputsSchema).optional(),
  largeBlob: z
    .lazy(() => ObjectsAuthenticationExtensionsLargeBlobInputsSchema)
    .optional(),
  credentialProtectionPolicy: z
    .lazy(() => ObjectsCredentialProtectionPolicySchema)
    .optional(),
  enforceCredentialProtectionPolicy: z.boolean().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsLargeBlobInputs */
ObjectsAuthenticationExtensionsLargeBlobInputsSchema = z.object({
  support: z.lazy(() => ObjectsLargeBlobSupportSchema).optional(),
  read: z.boolean().optional(),
  write: z.string().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsPRFInputs */
ObjectsAuthenticationExtensionsPRFInputsSchema = z.object({
  eval: z.lazy(() => ObjectsAuthenticationExtensionsPRFValuesSchema).optional(),
  evalByCredential: z
    .lazy(() => KeyValuePairStringAuthenticationExtensionsPRFValuesSchema)
    .optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsPRFValues */
ObjectsAuthenticationExtensionsPRFValuesSchema = z.object({
  first: z.string().nullable(),
  second: z.string().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticatorAttachment */
ObjectsAuthenticatorAttachmentSchema = z.enum(["Platform", "CrossPlatform"]);

/** Zod schema for ObjectsAuthenticatorTransport */
ObjectsAuthenticatorTransportSchema = z.enum([
  "Usb",
  "Nfc",
  "Ble",
  "SmartCard",
  "Hybrid",
  "Internal",
]);

/** Zod schema for ObjectsCOSEAlgorithm */
ObjectsCOSEAlgorithmSchema = z.enum([
  "RS1",
  "RS512",
  "RS384",
  "RS256",
  "ES256K",
  "PS512",
  "PS384",
  "PS256",
  "ES512",
  "ES384",
  "EdDSA",
  "ES256",
]);

/** Zod schema for ObjectsCredentialProtectionPolicy */
ObjectsCredentialProtectionPolicySchema = z.enum([
  "UserVerificationOptional",
  "UserVerificationOptionalWithCredentialIdList",
  "UserVerificationRequired",
]);

/** Zod schema for ObjectsLargeBlobSupport */
ObjectsLargeBlobSupportSchema = z.enum(["Required", "Preferred"]);

/** Zod schema for ObjectsPublicKeyCredentialDescriptor */
ObjectsPublicKeyCredentialDescriptorSchema = z.object({
  type: z.lazy(() => ObjectsPublicKeyCredentialTypeSchema).optional(),
  id: z.string().nullable().optional(),
  transports: z
    .array(z.lazy(() => ObjectsAuthenticatorTransportSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ObjectsPublicKeyCredentialHint */
ObjectsPublicKeyCredentialHintSchema = z.enum([
  "SecurityKey",
  "ClientDevice",
  "Hybrid",
]);

/** Zod schema for ObjectsPublicKeyCredentialType */
ObjectsPublicKeyCredentialTypeSchema = z.enum(["PublicKey", "Invalid"]);

/** Zod schema for ObjectsResidentKeyRequirement */
ObjectsResidentKeyRequirementSchema = z.enum([
  "Required",
  "Preferred",
  "Discouraged",
]);

/** Zod schema for ObjectsUserVerificationRequirement */
ObjectsUserVerificationRequirementSchema = z.enum([
  "Required",
  "Preferred",
  "Discouraged",
]);

/** Zod schema for PagedResultOfCommerceProductsProduct */
PagedResultOfCommerceProductsProductSchema = z.object({
  items: z
    .array(z.lazy(() => CommerceProductsProductSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfCommerceProductsPromoCode */
PagedResultOfCommerceProductsPromoCodeSchema = z.object({
  items: z
    .array(z.lazy(() => CommerceProductsPromoCodeSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfCommerceProductsSupportTicket */
PagedResultOfCommerceProductsSupportTicketSchema = z.object({
  items: z
    .array(z.lazy(() => CommerceProductsSupportTicketSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfCommerceSubscriptionsSubscription */
PagedResultOfCommerceSubscriptionsSubscriptionSchema = z.object({
  items: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfCommerceSubscriptionsSubscriptionNotification */
PagedResultOfCommerceSubscriptionsSubscriptionNotificationSchema = z.object({
  items: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionNotificationSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfIdentityTenantsTenant */
PagedResultOfIdentityTenantsTenantSchema = z.object({
  items: z
    .array(z.lazy(() => IdentityTenantsTenantSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfIdentityTenantsTenantAuditLogEntry */
PagedResultOfIdentityTenantsTenantAuditLogEntrySchema = z.object({
  items: z
    .array(z.lazy(() => IdentityTenantsTenantAuditLogEntrySchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfIdentityUsersUser */
PagedResultOfIdentityUsersUserSchema = z.object({
  items: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfIdentityUsersUserNotification */
PagedResultOfIdentityUsersUserNotificationSchema = z.object({
  items: z
    .array(z.lazy(() => IdentityUsersUserNotificationDtoSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for PagedResultOfIdentityUsersUserProfile */
PagedResultOfIdentityUsersUserProfileSchema = z.object({
  items: z
    .array(z.lazy(() => IdentityUsersUserProfileDtoSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  totalPages: z.number().int().optional(),
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
});

/** Zod schema for ProgramCategory */
ProgramCategorySchema = z.enum([
  "General",
  "Programming",
  "DataScience",
  "WebDevelopment",
  "MobileDevelopment",
  "GameDevelopment",
  "AI",
  "Cybersecurity",
  "DevOps",
  "Database",
  "Business",
  "Design",
  "Marketing",
  "ProjectManagement",
  "PersonalDevelopment",
  "CreativeArts",
  "Science",
  "Language",
  "Other",
]);

/** Zod schema for ProjectsAddCollaboratorInput */
ProjectsAddCollaboratorInputSchema = z.object({
  email: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  message: z.string().nullable().optional(),
  requireAcceptance: z.boolean().optional(),
});

/** Zod schema for ProjectsAddProjectCollaboratorInput */
ProjectsAddProjectCollaboratorInputSchema = z.object({
  userId: z.string().uuid(),
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
});

/** Zod schema for ProjectsCollaborator */
ProjectsCollaboratorSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
  joinedAt: z.string().datetime().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for ProjectsCreateProjectInput */
ProjectsCreateProjectInputSchema = z.object({
  title: z.string().min(1).max(255),
  description: z.string().min(0).max(2000).nullable().optional(),
  shortDescription: z.string().min(0).max(500).nullable().optional(),
  imageUrl: z.string().url().nullable().optional(),
  repositoryUrl: z.string().url().nullable().optional(),
  websiteUrl: z.string().url().nullable().optional(),
  downloadUrl: z.string().url().nullable().optional(),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  categoryId: z.string().uuid().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  tags: z.array(z.string()).nullable().optional(),
  ownerTeamId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ProjectsCreateProjectVersionInput */
ProjectsCreateProjectVersionInputSchema = z.object({
  versionNumber: z.string().min(1).max(50),
  status: z.string().max(50).nullable().optional(),
  releaseNotes: z.string().max(10000).nullable().optional(),
});

/** Zod schema for ProjectsDevelopmentStatus */
ProjectsDevelopmentStatusSchema = z.enum([
  "Planning",
  "InDevelopment",
  "Alpha",
  "Beta",
  "Released",
  "Completed",
  "OnHold",
  "Cancelled",
  "Archived",
]);

/** Zod schema for ProjectsEffectivePermission */
ProjectsEffectivePermissionSchema = z.object({
  resourceId: z.string().uuid().optional(),
  resourceType: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  isOwner: z.boolean().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsInvitationResult */
ProjectsInvitationResultSchema = z.object({
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  invitationId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ProjectsInviteProjectCollaboratorInput */
ProjectsInviteProjectCollaboratorInputSchema = z.object({
  userId: z.string().uuid().nullable().optional(),
  email: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsLinkProjectStoreProductInput */
ProjectsLinkProjectStoreProductInputSchema = z.object({
  productId: z.string().uuid().optional(),
});

/** Zod schema for ProjectsPermissionUpdateResult */
ProjectsPermissionUpdateResultSchema = z.object({
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProject */
ProjectsProjectSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(500),
  slug: z.string().min(1).max(500),
  shortDescription: z.string().max(500).nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().max(500).nullable().optional(),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  developmentStatus: z.lazy(() => ProjectsDevelopmentStatusSchema).optional(),
  status: z.lazy(() => ContentStatusSchema),
  visibility: z.lazy(() => ContentVisibilitySchema),
  category: z.lazy(() => ProjectsProjectCategorySchema).optional(),
  categoryId: z.string().uuid().nullable().optional(),
  websiteUrl: z.string().max(500).nullable().optional(),
  repositoryUrl: z.string().max(500).nullable().optional(),
  socialLinks: z.string().nullable().optional(),
  downloadUrl: z.string().max(500).nullable().optional(),
  tags: z.string().nullable().optional(),
  featuredImageUrl: z.string().max(1000).nullable().optional(),
  license: z.string().max(200).nullable().optional(),
  copyright: z.string().max(500).nullable().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  projectMetadata: z.lazy(() => ProjectsProjectMetadataSchema).optional(),
  versions: z
    .array(z.lazy(() => ProjectsProjectVersionSchema))
    .nullable()
    .optional(),
  collaborators: z
    .array(z.lazy(() => ProjectsProjectCollaboratorSchema))
    .nullable()
    .optional(),
  releases: z
    .array(z.lazy(() => ProjectsProjectReleaseSchema))
    .nullable()
    .optional(),
  teams: z
    .array(z.lazy(() => ProjectsProjectTeamSchema))
    .nullable()
    .optional(),
  allocations: z
    .array(z.lazy(() => ProjectsProjectMemberAllocationSchema))
    .nullable()
    .optional(),
  teamAgreements: z
    .array(z.lazy(() => ProjectsProjectTeamAgreementSchema))
    .nullable()
    .optional(),
  followers: z
    .array(z.lazy(() => ProjectsProjectFollowerSchema))
    .nullable()
    .optional(),
  feedbacks: z
    .array(z.lazy(() => ProjectsProjectFeedbackSchema))
    .nullable()
    .optional(),
  jamSubmissions: z
    .array(z.lazy(() => ProjectsProjectJamSubmissionSchema))
    .nullable()
    .optional(),
  createdById: z.string().uuid().nullable().optional(),
  isActive: z.boolean().optional(),
  latestVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  followerCount: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  feedbackCount: z.number().int().optional(),
  isInJam: z.boolean().optional(),
  teamCount: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectApiOutput */
ProjectsProjectApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  developmentStatus: z.lazy(() => ProjectsDevelopmentStatusSchema).optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  categoryId: z.string().uuid().nullable().optional(),
  category: z.lazy(() => ProjectsProjectCategoryApiOutputSchema).optional(),
  websiteUrl: z.string().nullable().optional(),
  repositoryUrl: z.string().nullable().optional(),
  socialLinks: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  featuredImageUrl: z.string().nullable().optional(),
  license: z.string().nullable().optional(),
  copyright: z.string().nullable().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  createdById: z.string().uuid().nullable().optional(),
  creator: z.lazy(() => ProjectsProjectUserApiOutputSchema).optional(),
  metadata: z.lazy(() => ProjectsProjectMetadataApiOutputSchema).optional(),
  versions: z
    .array(z.lazy(() => ProjectsProjectVersionApiOutputSchema))
    .nullable()
    .optional(),
  collaborators: z
    .array(z.lazy(() => ProjectsProjectCollaboratorApiOutputSchema))
    .nullable()
    .optional(),
  releases: z
    .array(z.lazy(() => ProjectsProjectReleaseApiOutputSchema))
    .nullable()
    .optional(),
  teams: z
    .array(z.lazy(() => ProjectsProjectTeamApiOutputSchema))
    .nullable()
    .optional(),
  latestVersion: z.lazy(() => ProjectsProjectVersionApiOutputSchema).optional(),
  followerCount: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  feedbackCount: z.number().int().optional(),
  isInJam: z.boolean().optional(),
  teamCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for ProjectsProjectCategory */
ProjectsProjectCategorySchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(50),
  projects: z
    .array(z.lazy(() => ProjectsProjectSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsProjectCategoryApiOutput */
ProjectsProjectCategoryApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProjectCollaborator */
ProjectsProjectCollaboratorSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  role: z.string().min(1).max(100),
  permissions: z.string().min(1).max(500),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsProjectCollaboratorApiOutput */
ProjectsProjectCollaboratorApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsProjectCollaboratorDto */
ProjectsProjectCollaboratorDtoSchema = z.object({
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  profilePictureUrl: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  joinedAt: z.string().datetime().optional(),
  invitedBy: z.string().nullable().optional(),
  isOwner: z.boolean().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsProjectFeedback */
ProjectsProjectFeedbackSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  rating: z.number().int().min(1).max(5).optional(),
  title: z.string().min(1).max(200),
  content: z.string().max(2000).nullable().optional(),
  categories: z.string().max(500).nullable().optional(),
  isFeatured: z.boolean().optional(),
  isVerified: z.boolean().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  helpfulVotes: z.number().int().optional(),
  totalVotes: z.number().int().optional(),
  platform: z.string().max(100).nullable().optional(),
  projectVersion: z.string().max(50).nullable().optional(),
});

/** Zod schema for ProjectsProjectFollower */
ProjectsProjectFollowerSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  followedAt: z.string().datetime().optional(),
  notificationSettings: z.string().max(1000).nullable().optional(),
  emailNotifications: z.boolean().optional(),
  pushNotifications: z.boolean().optional(),
});

/** Zod schema for ProjectsProjectInvitation */
ProjectsProjectInvitationSchema = z.object({
  id: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectTitle: z.string().nullable().optional(),
  invitedUserId: z.string().uuid().nullable().optional(),
  invitedEmail: z.string().nullable().optional(),
  invitedByUserId: z.string().uuid().optional(),
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
  token: z.string().nullable().optional(),
  status: z.lazy(() => ProjectsProjectInvitationStatusSchema).optional(),
  invitedAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  respondedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsProjectInvitationStatus */
ProjectsProjectInvitationStatusSchema = z.enum([
  "Pending",
  "Accepted",
  "Declined",
  "Revoked",
  "Expired",
]);

/** Zod schema for ProjectsProjectJamSubmission */
ProjectsProjectJamSubmissionSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  jamId: z.string().uuid().nullable().optional(),
  jam: z.lazy(() => GameJamsJamSchema).optional(),
  submittedAt: z.string().datetime().optional(),
  isEligible: z.boolean().optional(),
  submissionNotes: z.string().max(2000).nullable().optional(),
  finalScore: z.number().nullable().optional(),
  ranking: z.number().int().nullable().optional(),
  hasAward: z.boolean().optional(),
  awardDetails: z.string().max(1000).nullable().optional(),
  metadata: z.string().max(2000).nullable().optional(),
  scores: z
    .array(z.lazy(() => GameJamsJamScoreSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsProjectMemberAllocation */
ProjectsProjectMemberAllocationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  projectTeamId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  function: z.string().min(1).max(100),
  capacityPercentage: z.number().min(1).max(100).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for ProjectsProjectMetadata */
ProjectsProjectMetadataSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  viewCount: z.number().int().optional(),
  downloadCount: z.number().int().optional(),
  followerCount: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectMetadataApiOutput */
ProjectsProjectMetadataApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  viewCount: z.number().int().optional(),
  downloadCount: z.number().int().optional(),
  followerCount: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectRelease */
ProjectsProjectReleaseSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  title: z.string().min(1).max(200),
  description: z.string().nullable().optional(),
  releaseVersion: z.string().min(1).max(50),
  releasedAt: z.string().datetime().optional(),
  isLatest: z.boolean().optional(),
  isPrerelease: z.boolean().optional(),
  downloadUrl: z.string().max(500).nullable().optional(),
  fileSize: z.number().int().nullable().optional(),
  downloadCount: z.number().int().optional(),
  releaseNotes: z.string().nullable().optional(),
  checksum: z.string().max(128).nullable().optional(),
  systemRequirements: z.string().max(1000).nullable().optional(),
  supportedPlatforms: z.string().max(500).nullable().optional(),
  releaseType: z.string().max(50).nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  buildNumber: z.string().max(100).nullable().optional(),
  releaseMetadata: z.string().max(2000).nullable().optional(),
});

/** Zod schema for ProjectsProjectReleaseApiOutput */
ProjectsProjectReleaseApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  releaseVersion: z.string().nullable().optional(),
  releasedAt: z.string().datetime().optional(),
  isLatest: z.boolean().optional(),
  isPrerelease: z.boolean().optional(),
  downloadUrl: z.string().nullable().optional(),
  fileSize: z.number().int().nullable().optional(),
  downloadCount: z.number().int().optional(),
  releaseNotes: z.string().nullable().optional(),
  checksum: z.string().nullable().optional(),
  systemRequirements: z.string().nullable().optional(),
  supportedPlatforms: z.string().nullable().optional(),
  releaseType: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  buildNumber: z.string().nullable().optional(),
  releaseMetadata: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProjectRoleTemplate */
ProjectsProjectRoleTemplateSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsProjectStatistics */
ProjectsProjectStatisticsSchema = z.object({
  projectId: z.string().uuid().optional(),
  followerCount: z.number().int().optional(),
  feedbackCount: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  totalDownloads: z.number().int().optional(),
  activeTeamCount: z.number().int().optional(),
  collaboratorCount: z.number().int().optional(),
  releaseCount: z.number().int().optional(),
  jamSubmissionCount: z.number().int().optional(),
  awardCount: z.number().int().optional(),
  viewsLast30Days: z.number().int().optional(),
  downloadsLast30Days: z.number().int().optional(),
  newFollowersLast30Days: z.number().int().optional(),
  calculatedAt: z.string().datetime().optional(),
  trendingScore: z.number().optional(),
  popularityRank: z.number().int().nullable().optional(),
});

/** Zod schema for ProjectsProjectStoreProductProjection */
ProjectsProjectStoreProductProjectionSchema = z.object({
  linkId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
});

/** Zod schema for ProjectsProjectTeam */
ProjectsProjectTeamSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  teamId: z.string().uuid().optional(),
  team: z.lazy(() => TeamsTeamSchema).optional(),
  role: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  assignedAt: z.string().datetime().optional(),
  endedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  permissions: z.string().max(1000).nullable().optional(),
  notes: z.string().max(1000).nullable().optional(),
  contributionPercentage: z.number().min(0).max(100).optional(),
  allocations: z
    .array(z.lazy(() => ProjectsProjectMemberAllocationSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsProjectTeamAgreement */
ProjectsProjectTeamAgreementSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  proposingTeamId: z.string().uuid().optional(),
  receivingTeamId: z.string().uuid().optional(),
  proposedByUserId: z.string().uuid().optional(),
  acceptedByUserId: z.string().uuid().nullable().optional(),
  status: z.lazy(() => ProjectsProjectTeamAgreementStatusSchema).optional(),
  scope: z.string().min(1).max(1000),
  deliverables: z.string().min(1).max(2000),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  revision: z.number().int().optional(),
  acceptedAt: z.string().datetime().nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsProjectTeamAgreementStatus */
ProjectsProjectTeamAgreementStatusSchema = z.enum([
  "Proposed",
  "CounterProposed",
  "Accepted",
  "Cancelled",
  "Completed",
]);

/** Zod schema for ProjectsProjectTeamApiOutput */
ProjectsProjectTeamApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  teamId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  role: z.lazy(() => ProjectsProjectTeamRoleSchema).optional(),
  participationMode: z
    .lazy(() => ProjectsProjectTeamParticipationModeSchema)
    .optional(),
  assignedAt: z.string().datetime().optional(),
  endedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  notes: z.string().nullable().optional(),
  contributionPercentage: z.number().optional(),
});

/** Zod schema for ProjectsProjectTeamParticipationMode */
ProjectsProjectTeamParticipationModeSchema = z.enum([
  "AllMembers",
  "SelectedMembers",
]);

/** Zod schema for ProjectsProjectTeamRole */
ProjectsProjectTeamRoleSchema = z.enum([
  "Owner",
  "CoOwner",
  "Contributor",
  "Guest",
]);

/** Zod schema for ProjectsProjectType */
ProjectsProjectTypeSchema = z.enum([
  "Game",
  "Tool",
  "Art",
  "Music",
  "Educational",
  "Plugin",
  "Template",
  "Library",
  "Other",
]);

/** Zod schema for ProjectsProjectUserApiOutput */
ProjectsProjectUserApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  username: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProjectVersion */
ProjectsProjectVersionSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().optional(),
  versionNumber: z.string().min(1).max(50),
  releaseNotes: z.string().nullable().optional(),
  status: z.string().min(1).max(50),
  downloadCount: z.number().int().optional(),
  createdById: z.string().uuid().optional(),
});

/** Zod schema for ProjectsProjectVersionApiOutput */
ProjectsProjectVersionApiOutputSchema = z.object({
  id: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  versionNumber: z.string().nullable().optional(),
  releaseNotes: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  downloadCount: z.number().int().optional(),
  createdById: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for ProjectsProjectVersionOptionProjection */
ProjectsProjectVersionOptionProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectTitle: z.string().nullable().optional(),
  versionNumber: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for ProjectsShareProjectInput */
ProjectsShareProjectInputSchema = z.object({
  userId: z.string().uuid(),
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
});

/** Zod schema for ProjectsShareProjectWithRoleInput */
ProjectsShareProjectWithRoleInputSchema = z.object({
  roleName: z.string().nullable().optional(),
  userEmails: z.array(z.string()).nullable().optional(),
  userIds: z.array(z.string().uuid()).nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  message: z.string().nullable().optional(),
  requireAcceptance: z.boolean().optional(),
  notifyUsers: z.boolean().optional(),
});

/** Zod schema for ProjectsShareResult */
ProjectsShareResultSchema = z.object({
  success: z.boolean().optional(),
  errorMessage: z.string().nullable().optional(),
  successCount: z.number().int().optional(),
  failureCount: z.number().int().optional(),
});

/** Zod schema for ProjectsUpdateCollaboratorInput */
ProjectsUpdateCollaboratorInputSchema = z.object({
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ProjectsUpdateProjectCollaboratorInput */
ProjectsUpdateProjectCollaboratorInputSchema = z.object({
  role: z.string().nullable().optional(),
  permissions: z.string().nullable().optional(),
});

/** Zod schema for ProjectsUpdateProjectInput */
ProjectsUpdateProjectInputSchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  repositoryUrl: z.string().nullable().optional(),
  websiteUrl: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  categoryId: z.string().uuid().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for ProjectWorkProjectWorkColumnKind */
ProjectWorkProjectWorkColumnKindSchema = z.enum([
  "Backlog",
  "Ready",
  "InProgress",
  "InReview",
  "Done",
  "Custom",
]);

/** Zod schema for ProjectWorkProjectWorkTaskPriority */
ProjectWorkProjectWorkTaskPrioritySchema = z.enum([
  "Low",
  "Normal",
  "High",
  "Urgent",
]);

/** Zod schema for ProjectWorkProjectWorkTaskStatus */
ProjectWorkProjectWorkTaskStatusSchema = z.enum([
  "Backlog",
  "Ready",
  "InProgress",
  "InReview",
  "Done",
  "Cancelled",
]);

/** Zod schema for ResourcesArchiveResourceUsageRecordsInput */
ResourcesArchiveResourceUsageRecordsInputSchema = z.object({
  olderThan: z.string().datetime().optional(),
});

/** Zod schema for ResourcesCheckResourceQuotaInput */
ResourcesCheckResourceQuotaInputSchema = z.object({
  amount: z.number().int().optional(),
});

/** Zod schema for ResourcesCleanupOrphanedResourcesInput */
ResourcesCleanupOrphanedResourcesInputSchema = z.object({
  dryRun: z.boolean().optional(),
  resourceTypes: z
    .array(z.lazy(() => ResourcesResourceUsageTypeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ResourcesContentsAddReviewInput */
ResourcesContentsAddReviewInputSchema = z.object({
  decision: z
    .lazy(() => ResourcesContentsContentReviewDecisionSchema)
    .optional(),
  feedback: z.string().nullable().optional(),
  suggestions: z.string().nullable().optional(),
});

/** Zod schema for ResourcesContentsBulkGenerateContractsInput */
ResourcesContentsBulkGenerateContractsInputSchema = z.object({
  contracts: z
    .array(z.lazy(() => ResourcesContentsGenerateContractInputSchema))
    .nullable()
    .optional(),
  continueOnError: z.boolean().optional(),
});

/** Zod schema for ResourcesContentsBulkGeneratedContractItemOutput */
ResourcesContentsBulkGeneratedContractItemOutputSchema = z.object({
  index: z.number().int().optional(),
  success: z.boolean().optional(),
  contract: z
    .lazy(() => ResourcesContentsGeneratedContractOutputSchema)
    .optional(),
  error: z.lazy(() => ErrorSchema).optional(),
});

/** Zod schema for ResourcesContentsBulkGeneratedContractsOutput */
ResourcesContentsBulkGeneratedContractsOutputSchema = z.object({
  totalRequested: z.number().int().optional(),
  successful: z.number().int().optional(),
  failed: z.number().int().optional(),
  hasFailures: z.boolean().optional(),
  items: z
    .array(z.lazy(() => ResourcesContentsBulkGeneratedContractItemOutputSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ResourcesContentsContentReviewDecision */
ResourcesContentsContentReviewDecisionSchema = z.enum([
  "Pending",
  "Approve",
  "RequestChanges",
  "Reject",
]);

/** Zod schema for ResourcesContentsContentVersion */
ResourcesContentsContentVersionSchema = z.object({
  id: z.string().uuid().optional(),
  entityId: z.string().uuid().optional(),
  entityType: z.string().nullable().optional(),
  versionNumber: z.number().int().optional(),
  title: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  status: z.lazy(() => ResourcesContentsContentVersionStatusSchema).optional(),
  createdBy: z.string().uuid().optional(),
  changeNotes: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  submittedForReviewAt: z.string().datetime().nullable().optional(),
  reviewedBy: z.string().uuid().nullable().optional(),
  reviewedAt: z.string().datetime().nullable().optional(),
  reviewNotes: z.string().nullable().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  publishedBy: z.string().uuid().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  isCurrentVersion: z.boolean().optional(),
});

/** Zod schema for ResourcesContentsContentVersionDiff */
ResourcesContentsContentVersionDiffSchema = z.object({
  version1Id: z.string().uuid().optional(),
  version2Id: z.string().uuid().optional(),
  version1Number: z.number().int().optional(),
  version2Number: z.number().int().optional(),
  titleChanged: z.boolean().optional(),
  summaryChanged: z.boolean().optional(),
  bodyChanged: z.boolean().optional(),
  metadataChanged: z.boolean().optional(),
  titleDiff: z.string().nullable().optional(),
  summaryDiff: z.string().nullable().optional(),
  bodyDiff: z.string().nullable().optional(),
});

/** Zod schema for ResourcesContentsContentVersionReview */
ResourcesContentsContentVersionReviewSchema = z.object({
  id: z.string().uuid().optional(),
  contentVersionId: z.string().uuid().optional(),
  reviewerId: z.string().uuid().optional(),
  decision: z
    .lazy(() => ResourcesContentsContentReviewDecisionSchema)
    .optional(),
  feedback: z.string().nullable().optional(),
  suggestions: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for ResourcesContentsContentVersionStatus */
ResourcesContentsContentVersionStatusSchema = z.enum([
  "Draft",
  "PendingReview",
  "Approved",
  "Rejected",
  "Scheduled",
  "Published",
  "Archived",
]);

/** Zod schema for ResourcesContentsCreateDraftInput */
ResourcesContentsCreateDraftInputSchema = z.object({
  entityId: z.string().uuid().optional(),
  entityType: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  createdBy: z.string().uuid().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  changeNotes: z.string().nullable().optional(),
});

/** Zod schema for ResourcesContentsGenerateContractInput */
ResourcesContentsGenerateContractInputSchema = z.object({
  documentTemplateId: z.string().uuid().optional(),
  entityType: z.string().nullable().optional(),
  entityId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
  summary: z.string().nullable().optional(),
  publish: z.boolean().optional(),
  allowMissingVariables: z.boolean().optional(),
});

/** Zod schema for ResourcesContentsGeneratedContractOutput */
ResourcesContentsGeneratedContractOutputSchema = z.object({
  contractId: z.string().uuid().optional(),
  contentVersionId: z.string().uuid().optional(),
  documentTemplateId: z.string().uuid().optional(),
  templateKey: z.string().nullable().optional(),
  entityType: z.string().nullable().optional(),
  entityId: z.string().uuid().optional(),
  versionNumber: z.number().int().optional(),
  title: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  missingVariables: z.array(z.string()).nullable().optional(),
  published: z.boolean().optional(),
  generatedAtUtc: z.string().datetime().optional(),
});

/** Zod schema for ResourcesContentsReviewInput */
ResourcesContentsReviewInputSchema = z.object({
  reviewNotes: z.string().nullable().optional(),
});

/** Zod schema for ResourcesContentsRollbackInput */
ResourcesContentsRollbackInputSchema = z.object({
  targetVersionNumber: z.number().int().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for ResourcesContentsScheduleInput */
ResourcesContentsScheduleInputSchema = z.object({
  scheduledAt: z.string().datetime().optional(),
});

/** Zod schema for ResourcesContentsUpdateDraftInput */
ResourcesContentsUpdateDraftInputSchema = z.object({
  title: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
  changeNotes: z.string().nullable().optional(),
});

/** Zod schema for ResourcesEffectiveSettingOutput */
ResourcesEffectiveSettingOutputSchema = z.object({
  key: z.string().nullable().optional(),
  value: z.string().nullable().optional(),
  isUserOverride: z.boolean().optional(),
});

/** Zod schema for ResourcesRecordTenantResourceUsageInput */
ResourcesRecordTenantResourceUsageInputSchema = z.object({
  resourceUsageType: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  count: z.number().int().optional(),
  periodStart: z.string().datetime().optional(),
  periodEnd: z.string().datetime().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
});

/** Zod schema for ResourcesRecordUserResourceUsageInput */
ResourcesRecordUserResourceUsageInputSchema = z.object({
  resourceUsageType: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  count: z.number().int().optional(),
  periodStart: z.string().datetime().optional(),
  periodEnd: z.string().datetime().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
});

/** Zod schema for ResourcesResourceMetadata */
ResourcesResourceMetadataSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  key: z.string().min(1).max(100),
  value: z.string().max(4000).nullable().optional(),
  dataType: z.string().max(50).nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  category: z.string().max(100).nullable().optional(),
  isSystemManaged: z.boolean().optional(),
  isActive: z.boolean().optional(),
  displayOrder: z.number().int().optional(),
  userId: z.string().uuid().nullable().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  rowVersion: z.string().nullable().optional(),
});

/** Zod schema for ResourcesResourceQuotaEnforcementResult */
ResourcesResourceQuotaEnforcementResultSchema = z.object({
  isAllowed: z.boolean().optional(),
  isSoftLimitExceeded: z.boolean().optional(),
  isHardLimitExceeded: z.boolean().optional(),
  currentUsage: z.number().int().optional(),
  softLimit: z.number().int().nullable().optional(),
  hardLimit: z.number().int().nullable().optional(),
  usagePercentage: z.number().optional(),
  excessAmount: z.number().int().optional(),
  message: z.string().nullable().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  nextReset: z.string().datetime().nullable().optional(),
  remainingQuota: z.number().int().nullable().optional(),
});

/** Zod schema for ResourcesResourceQuotaOutput */
ResourcesResourceQuotaOutputSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  limit: z.number().int().optional(),
  currentUsage: z.number().int().optional(),
  remainingQuota: z.number().int().optional(),
  usagePercentage: z.number().optional(),
  softLimitPercentage: z.number().optional(),
  isActive: z.boolean().optional(),
  period: z.lazy(() => ResourcesResourceQuotaPeriodSchema).optional(),
  lastResetDate: z.string().datetime().optional(),
  nextResetDate: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  isSoftLimitExceeded: z.boolean().optional(),
  isHardLimitExceeded: z.boolean().optional(),
  shouldReset: z.boolean().optional(),
  softLimit: z.number().int().nullable().optional(),
  hardLimit: z.number().int().nullable().optional(),
});

/** Zod schema for ResourcesResourceQuotaPeriod */
ResourcesResourceQuotaPeriodSchema = z.enum([
  "Daily",
  "Weekly",
  "Monthly",
  "Quarterly",
  "Yearly",
  "Unlimited",
]);

/** Zod schema for ResourcesResourceSettings */
ResourcesResourceSettingsSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  key: z.string().min(1).max(100),
  value: z.string().max(4000).nullable().optional(),
  defaultValue: z.string().max(4000).nullable().optional(),
  dataType: z.string().max(50).nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  category: z.string().max(100).nullable().optional(),
  isSystemManaged: z.boolean().optional(),
  isActive: z.boolean().optional(),
  allowUserOverride: z.boolean().optional(),
  displayOrder: z.number().int().optional(),
  userId: z.string().uuid().nullable().optional(),
  validationRules: z.string().max(1000).nullable().optional(),
  rowVersion: z.string().nullable().optional(),
});

/** Zod schema for ResourcesResourceUsageType */
ResourcesResourceUsageTypeSchema = z.enum([
  "Users",
  "Projects",
  "Storage",
  "ApiCalls",
  "Programs",
  "Courses",
  "FeatureFlags",
  "SubscriptionPlans",
  "Products",
  "TestingSessions",
  "Roles",
  "Tenants",
  "Subscriptions",
  "SLOs",
  "AccessReviewCampaigns",
  "SoDRules",
  "AbacPolicies",
  "ConditionalPolicies",
  "Wallets",
  "Disputes",
  "PromoCodes",
  "Orders",
  "AuditEntries",
  "Assets",
  "AssetStorage",
  "AssetDownloads",
  "AssetTransformations",
  "AiRequests",
  "AiTokens",
  "Teams",
]);

/** Zod schema for ResourcesSetQuotaInput */
ResourcesSetQuotaInputSchema = z.object({
  softLimit: z.number().int().nullable().optional(),
  hardLimit: z.number().int().nullable().optional(),
  period: z.lazy(() => ResourcesResourceQuotaPeriodSchema).optional(),
  isActive: z.boolean().optional(),
  resetTime: z.string().nullable().optional(),
});

/** Zod schema for ResourcesSetResourceMetadataInput */
ResourcesSetResourceMetadataInputSchema = z.object({
  value: z.string().nullable().optional(),
  dataType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
});

/** Zod schema for ResourcesSetResourceSettingsInput */
ResourcesSetResourceSettingsInputSchema = z.object({
  value: z.string().nullable().optional(),
  defaultValue: z.string().nullable().optional(),
  dataType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  allowUserOverride: z.boolean().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  validationRules: z.string().nullable().optional(),
});

/** Zod schema for ResourcesSetUserResourceSettingsInput */
ResourcesSetUserResourceSettingsInputSchema = z.object({
  value: z.string().nullable().optional(),
});

/** Zod schema for ResourcesToggleResourceQuotaInput */
ResourcesToggleResourceQuotaInputSchema = z.object({
  isActive: z.boolean().optional(),
});

/** Zod schema for ResourcesTrendGranularity */
ResourcesTrendGranularitySchema = z.enum(["Daily", "Weekly", "Monthly"]);

/** Zod schema for ResourcesUsageRecord */
ResourcesUsageRecordSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  count: z.number().int().optional(),
  usageAmount: z.number().int().optional(),
  periodStart: z.string().datetime().optional(),
  periodEnd: z.string().datetime().optional(),
  averagePerDay: z.number().nullable().optional(),
  peakUsage: z.number().int().nullable().optional(),
  peakUsageDate: z.string().datetime().nullable().optional(),
  metadata: z.string().max(1000).nullable().optional(),
  source: z.string().max(50).nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  resourceQuotaId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ResourcesUsageTrendDataPoint */
ResourcesUsageTrendDataPointSchema = z.object({
  period: z.string().datetime().optional(),
  totalUsage: z.number().int().optional(),
  tenantCount: z.number().int().optional(),
});

/** Zod schema for ResourcesUsageTrendsResult */
ResourcesUsageTrendsResultSchema = z.object({
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  granularity: z.lazy(() => ResourcesTrendGranularitySchema).optional(),
  dataPoints: z
    .array(z.lazy(() => ResourcesUsageTrendDataPointSchema))
    .nullable()
    .optional(),
});

/** Zod schema for SocialBlogBlogPost */
SocialBlogBlogPostSchema = z.object({
  id: z.string().uuid().optional(),
  authorId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  excerpt: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  status: z.lazy(() => SocialBlogBlogPostStatusSchema).optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  isFeatured: z.boolean().optional(),
  allowComments: z.boolean().optional(),
  viewsCount: z.number().int().optional(),
  likesCount: z.number().int().optional(),
  commentsCount: z.number().int().optional(),
  readTimeMinutes: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for SocialBlogBlogPostStatus */
SocialBlogBlogPostStatusSchema = z.enum(["Draft", "Published", "Archived"]);

/** Zod schema for SocialBlogCreateBlogPostInput */
SocialBlogCreateBlogPostInputSchema = z.object({
  authorId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  content: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for SocialFeedAddFeedItemInput */
SocialFeedAddFeedItemInputSchema = z.object({
  userId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  contentType: z.lazy(() => SocialFeedFeedContentTypeSchema).optional(),
  authorId: z.string().uuid().optional(),
  reason: z.lazy(() => SocialFeedFeedItemReasonSchema).optional(),
  contentCreatedAt: z.string().datetime().nullable().optional(),
  relevanceScore: z.number().optional(),
});

/** Zod schema for SocialFeedFeedContentType */
SocialFeedFeedContentTypeSchema = z.enum([
  "Post",
  "BlogPost",
  "CourseReview",
  "ProjectUpdate",
  "Achievement",
  "CourseCompletion",
]);

/** Zod schema for SocialFeedFeedItem */
SocialFeedFeedItemSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  contentType: z.lazy(() => SocialFeedFeedContentTypeSchema).optional(),
  authorId: z.string().uuid().optional(),
  relevanceScore: z.number().optional(),
  reason: z.lazy(() => SocialFeedFeedItemReasonSchema).optional(),
  isRead: z.boolean().optional(),
  isHidden: z.boolean().optional(),
  contentCreatedAt: z.string().datetime().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for SocialFeedFeedItemReason */
SocialFeedFeedItemReasonSchema = z.enum([
  "Following",
  "Trending",
  "Recommended",
  "Mentioned",
  "Replied",
  "Liked",
  "InNetwork",
]);

/** Zod schema for SocialGroupsApproveSocialGroupMemberInput */
SocialGroupsApproveSocialGroupMemberInputSchema = z.object({
  approvedByUserId: z.string().uuid().optional(),
});

/** Zod schema for SocialGroupsChangeSocialGroupMemberRoleInput */
SocialGroupsChangeSocialGroupMemberRoleInputSchema = z.object({
  role: z.lazy(() => SocialGroupsSocialGroupMemberRoleSchema).optional(),
});

/** Zod schema for SocialGroupsCreateSocialGroupInput */
SocialGroupsCreateSocialGroupInputSchema = z.object({
  ownerId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
  description: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for SocialGroupsJoinSocialGroupInput */
SocialGroupsJoinSocialGroupInputSchema = z.object({
  userId: z.string().uuid().optional(),
  requestedRole: z
    .lazy(() => SocialGroupsSocialGroupMemberRoleSchema)
    .optional(),
});

/** Zod schema for SocialGroupsSocialGroup */
SocialGroupsSocialGroupSchema = z.object({
  id: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  ownerId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
  status: z.lazy(() => SocialGroupsSocialGroupStatusSchema).optional(),
  memberCount: z.number().int().optional(),
  pendingMemberCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for SocialGroupsSocialGroupMember */
SocialGroupsSocialGroupMemberSchema = z.object({
  id: z.string().uuid().optional(),
  groupId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  role: z.lazy(() => SocialGroupsSocialGroupMemberRoleSchema).optional(),
  status: z
    .lazy(() => SocialGroupsSocialGroupMembershipStatusSchema)
    .optional(),
  requestedAt: z.string().datetime().optional(),
  joinedAt: z.string().datetime().nullable().optional(),
  approvedByUserId: z.string().uuid().nullable().optional(),
  removedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for SocialGroupsSocialGroupMemberRole */
SocialGroupsSocialGroupMemberRoleSchema = z.enum([
  "Owner",
  "Admin",
  "Moderator",
  "Member",
]);

/** Zod schema for SocialGroupsSocialGroupMembershipStatus */
SocialGroupsSocialGroupMembershipStatusSchema = z.enum([
  "Pending",
  "Active",
  "Rejected",
  "Removed",
]);

/** Zod schema for SocialGroupsSocialGroupStatus */
SocialGroupsSocialGroupStatusSchema = z.enum([
  "Active",
  "Archived",
  "Suspended",
]);

/** Zod schema for SocialGroupsSocialGroupType */
SocialGroupsSocialGroupTypeSchema = z.enum([
  "StudyGroup",
  "ProjectTeam",
  "InterestCommunity",
  "CourseCohort",
  "Institution",
  "GameJamTeam",
]);

/** Zod schema for SocialGroupsSocialGroupVisibility */
SocialGroupsSocialGroupVisibilitySchema = z.enum([
  "Public",
  "Private",
  "InviteOnly",
]);

/** Zod schema for SocialGroupsUpdateSocialGroupInput */
SocialGroupsUpdateSocialGroupInputSchema = z.object({
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
  description: z.string().nullable().optional(),
});

/** Zod schema for SocialPostsControllersAddCommentInput */
SocialPostsControllersAddCommentInputSchema = z.object({
  content: z.string().nullable().optional(),
  parentCommentId: z.string().uuid().nullable().optional(),
});

/** Zod schema for SocialPostsControllersCreatePostInput */
SocialPostsControllersCreatePostInputSchema = z.object({
  content: z.string().nullable().optional(),
  visibility: z.lazy(() => SocialPostsPostVisibilitySchema).optional(),
  mediaUrl: z.string().nullable().optional(),
  mediaType: z.lazy(() => SocialPostsMediaTypeSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for SocialPostsControllersFollowPostInput */
SocialPostsControllersFollowPostInputSchema = z.object({
  notifyOnComments: z.boolean().optional(),
  notifyOnLikes: z.boolean().optional(),
  notifyOnShares: z.boolean().optional(),
  notifyOnUpdates: z.boolean().optional(),
});

/** Zod schema for SocialPostsControllersUpdateCommentInput */
SocialPostsControllersUpdateCommentInputSchema = z.object({
  content: z.string().nullable().optional(),
});

/** Zod schema for SocialPostsControllersUpdatePostInput */
SocialPostsControllersUpdatePostInputSchema = z.object({
  content: z.string().nullable().optional(),
});

/** Zod schema for SocialPostsMediaType */
SocialPostsMediaTypeSchema = z.enum(["Image", "Video", "Audio", "Document"]);

/** Zod schema for SocialPostsPostVisibility */
SocialPostsPostVisibilitySchema = z.enum([
  "Public",
  "Followers",
  "Private",
  "Unlisted",
]);

/** Zod schema for SocialProfilesAddProfilePortfolioItemBody */
SocialProfilesAddProfilePortfolioItemBodySchema = z.object({
  title: z.string().nullable().optional(),
  projectId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  displayOrder: z.number().int().optional(),
});

/** Zod schema for SocialProfilesAddProfileSkillBody */
SocialProfilesAddProfileSkillBodySchema = z.object({
  name: z.string().nullable().optional(),
  proficiency: z
    .lazy(() => SocialProfilesProfileSkillProficiencySchema)
    .optional(),
  displayOrder: z.number().int().optional(),
});

/** Zod schema for SocialProfilesProfileAvailabilityStatus */
SocialProfilesProfileAvailabilityStatusSchema = z.enum([
  "NotSet",
  "OpenToWork",
  "OpenToCollaborate",
  "Busy",
  "Hidden",
]);

/** Zod schema for SocialProfilesProfilePortfolioItem */
SocialProfilesProfilePortfolioItemSchema = z.object({
  id: z.string().uuid().optional(),
  profileId: z.string().uuid().optional(),
  projectId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  displayOrder: z.number().int().optional(),
});

/** Zod schema for SocialProfilesProfileSkill */
SocialProfilesProfileSkillSchema = z.object({
  id: z.string().uuid().optional(),
  profileId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  proficiency: z
    .lazy(() => SocialProfilesProfileSkillProficiencySchema)
    .optional(),
  displayOrder: z.number().int().optional(),
});

/** Zod schema for SocialProfilesProfileSkillProficiency */
SocialProfilesProfileSkillProficiencySchema = z.enum([
  "Beginner",
  "Intermediate",
  "Advanced",
  "Expert",
]);

/** Zod schema for SocialProfilesProfileVisibility */
SocialProfilesProfileVisibilitySchema = z.enum([
  "Private",
  "Connections",
  "Public",
]);

/** Zod schema for SocialProfilesSocialProfile */
SocialProfilesSocialProfileSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  handle: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  headline: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  websiteUrl: z.string().nullable().optional(),
  socialLinksJson: z.string().nullable().optional(),
  visibility: z.lazy(() => SocialProfilesProfileVisibilitySchema).optional(),
  availabilityStatus: z
    .lazy(() => SocialProfilesProfileAvailabilityStatusSchema)
    .optional(),
  showActivity: z.boolean().optional(),
  showPortfolio: z.boolean().optional(),
  showSkills: z.boolean().optional(),
  verifiedAt: z.string().datetime().nullable().optional(),
  completenessScore: z.number().int().optional(),
  followerCount: z.number().int().optional(),
  followingCount: z.number().int().optional(),
  postCount: z.number().int().optional(),
  projectCount: z.number().int().optional(),
  skills: z
    .array(z.lazy(() => SocialProfilesProfileSkillSchema))
    .nullable()
    .optional(),
  portfolioItems: z
    .array(z.lazy(() => SocialProfilesProfilePortfolioItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for SocialProfilesUpdateProfilePortfolioItemBody */
SocialProfilesUpdateProfilePortfolioItemBodySchema = z.object({
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  displayOrder: z.number().int().optional(),
});

/** Zod schema for SocialProfilesUpdateProfilePrivacyBody */
SocialProfilesUpdateProfilePrivacyBodySchema = z.object({
  visibility: z.lazy(() => SocialProfilesProfileVisibilitySchema).optional(),
  showActivity: z.boolean().optional(),
  showPortfolio: z.boolean().optional(),
  showSkills: z.boolean().optional(),
});

/** Zod schema for SocialProfilesUpdateProfileStatsBody */
SocialProfilesUpdateProfileStatsBodySchema = z.object({
  followerCount: z.number().int().optional(),
  followingCount: z.number().int().optional(),
  postCount: z.number().int().optional(),
  projectCount: z.number().int().optional(),
});

/** Zod schema for SocialProfilesUpdateSocialProfileBody */
SocialProfilesUpdateSocialProfileBodySchema = z.object({
  handle: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  headline: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  websiteUrl: z.string().nullable().optional(),
  socialLinksJson: z.string().nullable().optional(),
  availabilityStatus: z
    .lazy(() => SocialProfilesProfileAvailabilityStatusSchema)
    .optional(),
});

/** Zod schema for SocialReactionsReaction */
SocialReactionsReactionSchema = z.object({
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  type: z.lazy(() => SocialReactionsReactionTypeSchema).optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for SocialReactionsReactionTargetType */
SocialReactionsReactionTargetTypeSchema = z.enum([
  "Post",
  "Comment",
  "BlogPost",
  "CourseReview",
  "Discussion",
  "Reply",
]);

/** Zod schema for SocialReactionsReactionType */
SocialReactionsReactionTypeSchema = z.enum([
  "Like",
  "Love",
  "Insightful",
  "Celebrate",
  "Support",
  "Curious",
]);

/** Zod schema for SocialReactionsRemoveReactionInput */
SocialReactionsRemoveReactionInputSchema = z.object({
  userId: z.string().uuid().optional(),
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
});

/** Zod schema for SocialReactionsSetReactionInput */
SocialReactionsSetReactionInputSchema = z.object({
  userId: z.string().uuid().optional(),
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  type: z.lazy(() => SocialReactionsReactionTypeSchema).optional(),
});

/** Zod schema for SocialReactionsTargetReactionSummary */
SocialReactionsTargetReactionSummarySchema = z.object({
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  counts: z
    .object({
      Like: z.number().int(),
      Love: z.number().int(),
      Insightful: z.number().int(),
      Celebrate: z.number().int(),
      Support: z.number().int(),
      Curious: z.number().int(),
    })
    .nullable()
    .optional(),
  total: z.number().int().optional(),
});

/** Zod schema for SystemDayOfWeek */
SystemDayOfWeekSchema = z.enum([
  "Sunday",
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
]);

/** Zod schema for TeamsTeam */
TeamsTeamSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(200),
  slug: z.string().min(1).max(200),
  description: z.string().max(2000).nullable().optional(),
  visibility: z.lazy(() => TeamsTeamVisibilitySchema).optional(),
  status: z.lazy(() => TeamsTeamStatusSchema).optional(),
  isPersonal: z.boolean().optional(),
  isActive: z.boolean().optional(),
  members: z
    .array(z.lazy(() => TeamsTeamMemberSchema))
    .nullable()
    .optional(),
  invitations: z
    .array(z.lazy(() => TeamsTeamInvitationSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TeamsTeamInvitation */
TeamsTeamInvitationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  teamId: z.string().uuid().optional(),
  team: z.lazy(() => TeamsTeamSchema).optional(),
  invitedUserId: z.string().uuid().nullable().optional(),
  invitedEmail: z.string().max(255).nullable().optional(),
  tokenHash: z.string().min(1).max(64),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  invitedByUserId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  usedAt: z.string().datetime().nullable().optional(),
  acceptedByUserId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TeamsTeamMember */
TeamsTeamMemberSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  teamId: z.string().uuid().optional(),
  team: z.lazy(() => TeamsTeamSchema).optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  authority: z.lazy(() => TeamsTeamMemberAuthoritySchema).optional(),
  professionalTitle: z.string().max(150).nullable().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for TeamsTeamMemberAuthority */
TeamsTeamMemberAuthoritySchema = z.enum([
  "Viewer",
  "Member",
  "Manager",
  "Owner",
]);

/** Zod schema for TeamsTeamStatus */
TeamsTeamStatusSchema = z.enum(["Active", "Archived"]);

/** Zod schema for TeamsTeamVisibility */
TeamsTeamVisibilitySchema = z.enum(["Private", "Tenant", "Public"]);

/** Zod schema for TenantInfo */
TenantInfoSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for TestingLabAddTestingEventCommitteeMemberInput */
TestingLabAddTestingEventCommitteeMemberInputSchema = z.object({
  userId: z.string().uuid().optional(),
  isChair: z.boolean().optional(),
});

/** Zod schema for TestingLabAssignTestingLabRoleInput */
TestingLabAssignTestingLabRoleInputSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  roleName: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for TestingLabAssignTestingProjectApplicationSlotInput */
TestingLabAssignTestingProjectApplicationSlotInputSchema = z.object({
  slotId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabAssignTestingProjectToTesterInput */
TestingLabAssignTestingProjectToTesterInputSchema = z.object({
  applicationId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabAttendanceStatus */
TestingLabAttendanceStatusSchema = z.enum([
  "Registered",
  "Present",
  "Completed",
  "NoShow",
]);

/** Zod schema for TestingLabCancelTestingEventInput */
TestingLabCancelTestingEventInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for TestingLabCastTestingApplicationVoteInput */
TestingLabCastTestingApplicationVoteInputSchema = z.object({
  decision: z
    .lazy(() => TestingLabTestingApplicationVoteDecisionSchema)
    .optional(),
  comments: z.string().nullable().optional(),
});

/** Zod schema for TestingLabConfigureTestingEventLearningInput */
TestingLabConfigureTestingEventLearningInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  cohortId: z.string().uuid().nullable().optional(),
  learningActivityId: z.string().uuid().optional(),
  requirement: z
    .lazy(() => TestingLabTestingLearningCompletionRequirementSchema)
    .optional(),
});

/** Zod schema for TestingLabCreateSimpleTestingInput */
TestingLabCreateSimpleTestingInputSchema = z.object({
  title: z.string().min(1).max(255),
  description: z.string().nullable().optional(),
  projectId: z.string().uuid().nullable().optional(),
  versionNumber: z.string().min(1).max(50),
  downloadUrl: z.string().max(1000).nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsContent: z.string().nullable().optional(),
  instructionsUrl: z.string().max(500).nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  teamIdentifier: z.string().max(100).nullable().optional(),
});

/** Zod schema for TestingLabCreateTestingEventInput */
TestingLabCreateTestingEventInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  approvalMode: z
    .lazy(() => TestingLabTestingEventApprovalModeSchema)
    .optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  requiresFeedback: z.boolean().optional(),
  recurrence: z
    .lazy(() => TestingLabTestingEventRecurrenceInputSchema)
    .optional(),
});

/** Zod schema for TestingLabCreateTestingInput */
TestingLabCreateTestingInputSchema = z.object({
  projectVersionId: z.string().uuid(),
  title: z.string().min(1).max(255),
  description: z.string().nullable().optional(),
  downloadUrl: z.string().max(1000).nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsContent: z.string().nullable().optional(),
  instructionsUrl: z.string().max(500).nullable().optional(),
  instructionsFileId: z.string().uuid().nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema),
});

/** Zod schema for TestingLabCreateTestingLabRoleInput */
TestingLabCreateTestingLabRoleInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabCreateTestingLabSettings */
TestingLabCreateTestingLabSettingsSchema = z.object({
  labName: z.string().min(1).max(255),
  description: z.string().max(1000).nullable().optional(),
  timezone: z.string().min(1).max(50),
  defaultSessionDuration: z.number().int().min(15).max(480),
  allowPublicSignups: z.boolean().optional(),
  requireApproval: z.boolean().optional(),
  enableNotifications: z.boolean().optional(),
  maxSimultaneousSessions: z.number().int().min(1).max(100),
});

/** Zod schema for TestingLabCreateTestingLocation */
TestingLabCreateTestingLocationSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  address: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  maxTestersCapacity: z.number().int().optional(),
  maxProjectsCapacity: z.number().int().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  isVirtual: z.boolean().optional(),
  virtualUrl: z.string().nullable().optional(),
  contactEmail: z.string().nullable().optional(),
  contactPhone: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
});

/** Zod schema for TestingLabCreateTestingSession */
TestingLabCreateTestingSessionSchema = z.object({
  testingRequestId: z.string().uuid(),
  locationId: z.string().uuid(),
  sessionName: z.string().min(1).max(255),
  sessionDate: z.string().datetime(),
  startTime: z.string().datetime(),
  endTime: z.string().datetime(),
  maxTesters: z.number().int(),
  maxProjects: z.number().int(),
  status: z.lazy(() => TestingLabSessionStatusSchema),
  managerUserId: z.string().uuid(),
});

/** Zod schema for TestingLabDecideTestingProjectApplicationInput */
TestingLabDecideTestingProjectApplicationInputSchema = z.object({
  slotId: z.string().uuid().nullable().optional(),
  rationale: z.string().nullable().optional(),
});

/** Zod schema for TestingLabFeedbackFormType */
TestingLabFeedbackFormTypeSchema = z.enum([
  "General",
  "BugReport",
  "Usability",
  "Performance",
  "Accessibility",
]);

/** Zod schema for TestingLabFeedbackInput */
TestingLabFeedbackInputSchema = z.object({
  feedbackFormId: z.string().uuid().optional(),
  feedbackData: z.string().nullable().optional(),
  testingContext: z.lazy(() => TestingLabTestingContextSchema).optional(),
  sessionId: z.string().uuid().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabFeedbackQuality */
TestingLabFeedbackQualitySchema = z.enum(["Low", "Medium", "High"]);

/** Zod schema for TestingLabFeedbackQualityRating */
TestingLabFeedbackQualityRatingSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  feedbackId: z.string().uuid(),
  feedback: z.lazy(() => TestingLabTestingFeedbackSchema).optional(),
  ratedByUserId: z.string().uuid(),
  ratedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  qualityRating: z.number().int().min(1).max(5),
  reason: z.string().max(500).nullable().optional(),
  isGlobal: z.boolean().optional(),
  isPositive: z.boolean().optional(),
  isNegative: z.boolean().optional(),
});

/** Zod schema for TestingLabGrantResourcePermissionInput */
TestingLabGrantResourcePermissionInputSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  action: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for TestingLabInstructionType */
TestingLabInstructionTypeSchema = z.enum(["Text", "Url", "File"]);

/** Zod schema for TestingLabLinkSessionProjectInput */
TestingLabLinkSessionProjectInputSchema = z.object({
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabLocationStatus */
TestingLabLocationStatusSchema = z.enum(["Active", "Maintenance", "Inactive"]);

/** Zod schema for TestingLabParticipationStatus */
TestingLabParticipationStatusSchema = z.enum([
  "Registered",
  "Active",
  "Completed",
  "Withdrawn",
  "Suspended",
]);

/** Zod schema for TestingLabPublicTestingEventProjection */
TestingLabPublicTestingEventProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  approvalMode: z
    .lazy(() => TestingLabTestingEventApprovalModeSchema)
    .optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  requiresFeedback: z.boolean().optional(),
  applicationCount: z.number().int().optional(),
  slots: z
    .array(z.lazy(() => TestingLabPublicTestingEventSlotProjectionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabPublicTestingEventSlotProjection */
TestingLabPublicTestingEventSlotProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  maxTesters: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  campusName: z.string().nullable().optional(),
  roomName: z.string().nullable().optional(),
  approvedProjectCount: z.number().int().optional(),
  registeredTesterCount: z.number().int().optional(),
  availableTesterCount: z.number().int().nullable().optional(),
  availableProjectCount: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabRateFeedbackQuality */
TestingLabRateFeedbackQualitySchema = z.object({
  quality: z.lazy(() => TestingLabFeedbackQualitySchema).optional(),
});

/** Zod schema for TestingLabRegisterTestingEventSlotInput */
TestingLabRegisterTestingEventSlotInputSchema = z.object({
  notes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabRegistrationStatus */
TestingLabRegistrationStatusSchema = z.enum([
  "Registered",
  "Confirmed",
  "Cancelled",
  "Attended",
  "NoShow",
]);

/** Zod schema for TestingLabRegistrationType */
TestingLabRegistrationTypeSchema = z.enum(["ProjectMember", "Tester"]);

/** Zod schema for TestingLabReportFeedback */
TestingLabReportFeedbackSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSessionProjectProjection */
TestingLabSessionProjectProjectionSchema = z.object({
  linkId: z.string().uuid().optional(),
  sessionId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for TestingLabSessionRegistration */
TestingLabSessionRegistrationSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  sessionId: z.string().uuid(),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema).optional(),
  status: z.lazy(() => TestingLabRegistrationStatusSchema).optional(),
  registeredAt: z.string().datetime(),
  confirmedAt: z.string().datetime().nullable().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  attendanceStatus: z.lazy(() => TestingLabAttendanceStatusSchema).optional(),
  notes: z.string().nullable().optional(),
  registrationNotes: z.string().nullable().optional(),
  attendedAt: z.string().datetime().nullable().optional(),
  isGlobal: z.boolean().optional(),
  isConfirmed: z.boolean().optional(),
  isCheckedIn: z.boolean().optional(),
  isCheckedOut: z.boolean().optional(),
  attendanceDuration: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSessionRegistrationInput */
TestingLabSessionRegistrationInputSchema = z.object({
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema).optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSessionStatus */
TestingLabSessionStatusSchema = z.enum([
  "Scheduled",
  "Active",
  "Completed",
  "Cancelled",
]);

/** Zod schema for TestingLabSessionWaitlist */
TestingLabSessionWaitlistSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  sessionId: z.string().uuid().optional(),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema),
  position: z.number().int(),
  registrationNotes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSubmitFeedback */
TestingLabSubmitFeedbackSchema = z.object({
  testingRequestId: z.string().uuid(),
  feedbackResponses: z.string().min(1),
  overallRating: z.number().int().min(1).max(10).nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
  sessionId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabSubmitTestingEventFeedbackInput */
TestingLabSubmitTestingEventFeedbackInputSchema = z.object({
  feedbackData: z.string().nullable().optional(),
  overallRating: z.number().int().nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSubmitTestingProjectApplicationInput */
TestingLabSubmitTestingProjectApplicationInputSchema = z.object({
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  preferredAvailability: z.string().nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for TestingLabTestingApplicationReviewAssetProjection */
TestingLabTestingApplicationReviewAssetProjectionSchema = z.object({
  assetReferenceId: z.string().uuid().optional(),
  displayName: z.string().nullable().optional(),
  mimeType: z.string().nullable().optional(),
  accessUrl: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingApplicationReviewPackageProjection */
TestingLabTestingApplicationReviewPackageProjectionSchema = z.object({
  applicationId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  versionNumber: z.string().nullable().optional(),
  versionStatus: z.string().nullable().optional(),
  releaseNotes: z.string().nullable().optional(),
  assets: z
    .array(
      z.lazy(() => TestingLabTestingApplicationReviewAssetProjectionSchema),
    )
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingApplicationStatus */
TestingLabTestingApplicationStatusSchema = z.enum([
  "Pending",
  "UnderReview",
  "Approved",
  "Rejected",
  "Waitlisted",
  "Withdrawn",
]);

/** Zod schema for TestingLabTestingApplicationTesterEligibilityProjection */
TestingLabTestingApplicationTesterEligibilityProjectionSchema = z.object({
  testerUserId: z.string().uuid().optional(),
  eligibleApplicationIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for TestingLabTestingApplicationVote */
TestingLabTestingApplicationVoteSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  applicationId: z.string().uuid().optional(),
  application: z
    .lazy(() => TestingLabTestingProjectApplicationSchema)
    .optional(),
  reviewerId: z.string().uuid().optional(),
  reviewer: z.lazy(() => IdentityUsersUserSchema).optional(),
  decision: z
    .lazy(() => TestingLabTestingApplicationVoteDecisionSchema)
    .optional(),
  comments: z.string().max(2000).nullable().optional(),
});

/** Zod schema for TestingLabTestingApplicationVoteDecision */
TestingLabTestingApplicationVoteDecisionSchema = z.enum([
  "Approve",
  "Reject",
  "Abstain",
]);

/** Zod schema for TestingLabTestingApplicationVoteProjection */
TestingLabTestingApplicationVoteProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  reviewerId: z.string().uuid().optional(),
  decision: z
    .lazy(() => TestingLabTestingApplicationVoteDecisionSchema)
    .optional(),
  comments: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingCommitteeMember */
TestingLabTestingCommitteeMemberSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  eventId: z.string().uuid().optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  userId: z.string().uuid().optional(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  isChair: z.boolean().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingContext */
TestingLabTestingContextSchema = z.enum(["Online", "InPerson"]);

/** Zod schema for TestingLabTestingEvent */
TestingLabTestingEventSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(255),
  description: z.string().max(2000).nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  approvalMode: z
    .lazy(() => TestingLabTestingEventApprovalModeSchema)
    .optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  managerUserId: z.string().uuid().optional(),
  manager: z.lazy(() => IdentityUsersUserSchema).optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  recurrenceSeriesId: z.string().uuid().nullable().optional(),
  recurrenceOccurrence: z.number().int().nullable().optional(),
  recurrenceFrequency: z
    .lazy(() => TestingLabTestingEventRecurrenceFrequencySchema)
    .optional(),
  recurrenceInterval: z.number().int().nullable().optional(),
  recurrenceDaysOfWeek: z.string().max(64).nullable().optional(),
  recurrenceEndsAt: z.string().datetime().nullable().optional(),
  recurrenceOccurrenceCount: z.number().int().nullable().optional(),
  requiresFeedback: z.boolean().optional(),
  learningCompletionRequirement: z
    .lazy(() => TestingLabTestingLearningCompletionRequirementSchema)
    .optional(),
  courseId: z.string().uuid().nullable().optional(),
  cohortId: z.string().uuid().nullable().optional(),
  learningActivityId: z.string().uuid().nullable().optional(),
  cancellationReason: z.string().max(1000).nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  slots: z
    .array(z.lazy(() => TestingLabTestingEventSlotSchema))
    .nullable()
    .optional(),
  applications: z
    .array(z.lazy(() => TestingLabTestingProjectApplicationSchema))
    .nullable()
    .optional(),
  committeeMembers: z
    .array(z.lazy(() => TestingLabTestingCommitteeMemberSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingEventApprovalMode */
TestingLabTestingEventApprovalModeSchema = z.enum(["ManagerOnly", "Committee"]);

/** Zod schema for TestingLabTestingEventCommitteeMemberProjection */
TestingLabTestingEventCommitteeMemberProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  isChair: z.boolean().optional(),
  isActive: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingEventFeedbackProjection */
TestingLabTestingEventFeedbackProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  applicationId: z.string().uuid().optional(),
  testerUserId: z.string().uuid().optional(),
  feedbackData: z.string().nullable().optional(),
  overallRating: z.number().int().nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
  submittedAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingEventFeedbackReviewProjection */
TestingLabTestingEventFeedbackReviewProjectionSchema = z.object({
  obligationId: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  applicationId: z.string().uuid().optional(),
  testerUserId: z.string().uuid().optional(),
  status: z
    .lazy(() => TestingLabTestingFeedbackObligationStatusSchema)
    .optional(),
  fulfilledAt: z.string().datetime().nullable().optional(),
  feedback: z
    .lazy(() => TestingLabTestingEventFeedbackProjectionSchema)
    .optional(),
});

/** Zod schema for TestingLabTestingEventMode */
TestingLabTestingEventModeSchema = z.enum(["Online", "InPerson", "Hybrid"]);

/** Zod schema for TestingLabTestingEventProjection */
TestingLabTestingEventProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  approvalMode: z
    .lazy(() => TestingLabTestingEventApprovalModeSchema)
    .optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  managerUserId: z.string().uuid().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  requiresFeedback: z.boolean().optional(),
  learningCompletionRequirement: z
    .lazy(() => TestingLabTestingLearningCompletionRequirementSchema)
    .optional(),
  courseId: z.string().uuid().nullable().optional(),
  cohortId: z.string().uuid().nullable().optional(),
  learningActivityId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  slotCount: z.number().int().optional(),
  applicationCount: z.number().int().optional(),
  recurrenceSeriesId: z.string().uuid().nullable().optional(),
  recurrenceOccurrence: z.number().int().nullable().optional(),
  recurrenceFrequency: z
    .lazy(() => TestingLabTestingEventRecurrenceFrequencySchema)
    .optional(),
  recurrenceInterval: z.number().int().nullable().optional(),
  recurrenceDaysOfWeek: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  recurrenceEndsAt: z.string().datetime().nullable().optional(),
  recurrenceOccurrenceCount: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabTestingEventRecurrenceFrequency */
TestingLabTestingEventRecurrenceFrequencySchema = z.enum([
  "Daily",
  "Weekly",
  "Monthly",
]);

/** Zod schema for TestingLabTestingEventRecurrenceInput */
TestingLabTestingEventRecurrenceInputSchema = z.object({
  frequency: z
    .lazy(() => TestingLabTestingEventRecurrenceFrequencySchema)
    .optional(),
  interval: z.number().int().optional(),
  daysOfWeek: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  endsAt: z.string().datetime().nullable().optional(),
  occurrenceCount: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabTestingEventSlot */
TestingLabTestingEventSlotSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  eventId: z.string().uuid().optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  locationId: z.string().uuid().nullable().optional(),
  location: z.lazy(() => TestingLabTestingLocationSchema).optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  maxTesters: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  campusName: z.string().max(200).nullable().optional(),
  roomName: z.string().max(200).nullable().optional(),
  meetingUrl: z.string().max(1000).nullable().optional(),
  isTesterCapacityUnlimited: z.boolean().optional(),
  isProjectCapacityUnlimited: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingEventSlotProjection */
TestingLabTestingEventSlotProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  locationId: z.string().uuid().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  maxTesters: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  campusName: z.string().nullable().optional(),
  roomName: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  approvedProjectCount: z.number().int().optional(),
  registeredTesterCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingEventStatus */
TestingLabTestingEventStatusSchema = z.enum([
  "Draft",
  "ApplicationsOpen",
  "ApplicationsClosed",
  "Scheduled",
  "Active",
  "Completed",
  "Cancelled",
]);

/** Zod schema for TestingLabTestingFeedback */
TestingLabTestingFeedbackSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  testingRequestId: z.string().uuid().nullable().optional(),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  feedbackFormId: z.string().uuid().nullable().optional(),
  feedbackForm: z.lazy(() => TestingLabTestingFeedbackFormSchema).optional(),
  eventId: z.string().uuid().nullable().optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  applicationId: z.string().uuid().nullable().optional(),
  application: z
    .lazy(() => TestingLabTestingProjectApplicationSchema)
    .optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  sessionId: z.string().uuid().nullable().optional(),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  testingContext: z.lazy(() => TestingLabTestingContextSchema),
  feedbackData: z.string().min(1),
  overallRating: z.number().int().min(1).max(10).nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
  isReported: z.boolean().optional(),
  qualityRating: z.lazy(() => TestingLabFeedbackQualitySchema).optional(),
  reportReason: z.string().max(500).nullable().optional(),
  reportedById: z.string().uuid().nullable().optional(),
  reportedByUserId: z.string().uuid().nullable().optional(),
  reportedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  reportedAt: z.string().datetime().nullable().optional(),
  qualityRatings: z
    .array(z.lazy(() => TestingLabFeedbackQualityRatingSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  isPositive: z.boolean().optional(),
  isNegative: z.boolean().optional(),
  averageQualityRating: z.number().nullable().optional(),
});

/** Zod schema for TestingLabTestingFeedbackDirectoryItem */
TestingLabTestingFeedbackDirectoryItemSchema = z.object({
  id: z.string().uuid().optional(),
  source: z.lazy(() => TestingLabTestingFeedbackSourceSchema).optional(),
  testingRequestId: z.string().uuid().nullable().optional(),
  requestTitle: z.string().nullable().optional(),
  eventId: z.string().uuid().nullable().optional(),
  eventName: z.string().nullable().optional(),
  applicationId: z.string().uuid().nullable().optional(),
  projectId: z.string().uuid().nullable().optional(),
  projectTitle: z.string().nullable().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  projectVersion: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  testingContext: z.lazy(() => TestingLabTestingContextSchema).optional(),
  overallRating: z.number().int().nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
  feedbackData: z.string().nullable().optional(),
  additionalNotes: z.string().nullable().optional(),
  isReported: z.boolean().optional(),
  reportReason: z.string().nullable().optional(),
  reportedByUserId: z.string().uuid().nullable().optional(),
  reportedAt: z.string().datetime().nullable().optional(),
  qualityRating: z.lazy(() => TestingLabFeedbackQualitySchema).optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingFeedbackDirectoryPage */
TestingLabTestingFeedbackDirectoryPageSchema = z.object({
  items: z
    .array(z.lazy(() => TestingLabTestingFeedbackDirectoryItemSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingFeedbackForm */
TestingLabTestingFeedbackFormSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(200),
  description: z.string().nullable().optional(),
  formData: z.string().min(1),
  testingRequestId: z.string().uuid().nullable().optional(),
  formSchema: z.string().nullable().optional(),
  isForOnline: z.boolean().optional(),
  isForSessions: z.boolean().optional(),
  isActive: z.boolean().optional(),
  formType: z.lazy(() => TestingLabFeedbackFormTypeSchema).optional(),
  formVersion: z.number().int().optional(),
  tags: z.string().max(500).nullable().optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  submissionCount: z.number().int().optional(),
  tagArray: z.array(z.string()).nullable().optional(),
});

/** Zod schema for TestingLabTestingFeedbackObligationProjection */
TestingLabTestingFeedbackObligationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  applicationId: z.string().uuid().optional(),
  testerUserId: z.string().uuid().optional(),
  feedbackId: z.string().uuid().nullable().optional(),
  status: z
    .lazy(() => TestingLabTestingFeedbackObligationStatusSchema)
    .optional(),
  fulfilledAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for TestingLabTestingFeedbackObligationStatus */
TestingLabTestingFeedbackObligationStatusSchema = z.enum([
  "Pending",
  "Fulfilled",
  "Waived",
]);

/** Zod schema for TestingLabTestingFeedbackSource */
TestingLabTestingFeedbackSourceSchema = z.enum(["Request", "Event"]);

/** Zod schema for TestingLabTestingInput */
TestingLabTestingInputSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  title: z.string().min(1).max(255),
  description: z.string().nullable().optional(),
  downloadUrl: z.string().max(1000).nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsContent: z.string().nullable().optional(),
  instructionsUrl: z.string().max(500).nullable().optional(),
  instructionsFileId: z.string().uuid().nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  currentTesterCount: z.number().int().optional(),
  startDate: z.string().datetime(),
  endDate: z.string().datetime(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema),
  createdById: z.string().uuid(),
  createdBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  priority: z.lazy(() => TestingLabTestingPrioritySchema).optional(),
  estimatedDurationHours: z.number().int().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingModeSchema).optional(),
  sessions: z
    .array(z.lazy(() => TestingLabTestingSessionSchema))
    .nullable()
    .optional(),
  participants: z
    .array(z.lazy(() => TestingLabTestingParticipantSchema))
    .nullable()
    .optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  feedbackForms: z
    .array(z.lazy(() => TestingLabTestingFeedbackFormSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  isActive: z.boolean().optional(),
  acceptsNewTesters: z.boolean().optional(),
  availableSpots: z.number().int().nullable().optional(),
  duration: z.string().optional(),
  daysRemaining: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabTestingLabAnalyticsReportProjection */
TestingLabTestingLabAnalyticsReportProjectionSchema = z.object({
  fromDate: z.string().datetime().optional(),
  toDate: z.string().datetime().optional(),
  generatedAt: z.string().datetime().optional(),
  current: z
    .lazy(() => TestingLabTestingLabAnalyticsSummaryProjectionSchema)
    .optional(),
  previous: z
    .lazy(() => TestingLabTestingLabAnalyticsSummaryProjectionSchema)
    .optional(),
  locations: z
    .lazy(() => TestingLabTestingLabLocationAnalyticsProjectionSchema)
    .optional(),
  trend: z
    .array(z.lazy(() => TestingLabTestingLabAnalyticsTrendProjectionSchema))
    .nullable()
    .optional(),
  events: z
    .array(z.lazy(() => TestingLabTestingLabEventAnalyticsProjectionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingLabAnalyticsSummaryProjection */
TestingLabTestingLabAnalyticsSummaryProjectionSchema = z.object({
  events: z.number().int().optional(),
  completedEvents: z.number().int().optional(),
  applications: z.number().int().optional(),
  approvedProjects: z.number().int().optional(),
  registeredTesters: z.number().int().optional(),
  attendedTesters: z.number().int().optional(),
  feedback: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  recommendationRate: z.number().nullable().optional(),
  capacity: z.number().int().optional(),
  fillRate: z.number().optional(),
});

/** Zod schema for TestingLabTestingLabAnalyticsTrendProjection */
TestingLabTestingLabAnalyticsTrendProjectionSchema = z.object({
  date: z.string().datetime().optional(),
  events: z.number().int().optional(),
  applications: z.number().int().optional(),
  registrations: z.number().int().optional(),
  attendance: z.number().int().optional(),
  feedback: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingLabEventAnalyticsProjection */
TestingLabTestingLabEventAnalyticsProjectionSchema = z.object({
  eventId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  applications: z.number().int().optional(),
  approvedProjects: z.number().int().optional(),
  registeredTesters: z.number().int().optional(),
  attendedTesters: z.number().int().optional(),
  feedback: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  capacity: z.number().int().optional(),
  fillRate: z.number().optional(),
});

/** Zod schema for TestingLabTestingLabLocationAnalyticsProjection */
TestingLabTestingLabLocationAnalyticsProjectionSchema = z.object({
  total: z.number().int().optional(),
  active: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingLabPermissions */
TestingLabTestingLabPermissionsSchema = z.object({
  canCreateSessions: z.boolean().optional(),
  canEditSessions: z.boolean().optional(),
  canDeleteSessions: z.boolean().optional(),
  canViewSessions: z.boolean().optional(),
  canCreateLocations: z.boolean().optional(),
  canEditLocations: z.boolean().optional(),
  canDeleteLocations: z.boolean().optional(),
  canViewLocations: z.boolean().optional(),
  canCreateFeedback: z.boolean().optional(),
  canEditFeedback: z.boolean().optional(),
  canDeleteFeedback: z.boolean().optional(),
  canViewFeedback: z.boolean().optional(),
  canModerateFeedback: z.boolean().optional(),
  canCreateRequests: z.boolean().optional(),
  canEditRequests: z.boolean().optional(),
  canDeleteRequests: z.boolean().optional(),
  canViewRequests: z.boolean().optional(),
  canApproveRequests: z.boolean().optional(),
  canManageParticipants: z.boolean().optional(),
  canViewParticipants: z.boolean().optional(),
  canCreateEvents: z.boolean().optional(),
  canEditEvents: z.boolean().optional(),
  canDeleteEvents: z.boolean().optional(),
  canViewEvents: z.boolean().optional(),
  canViewApplications: z.boolean().optional(),
  canApproveApplications: z.boolean().optional(),
  canManageApplications: z.boolean().optional(),
  canViewAnalytics: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingLabResourcePermission */
TestingLabTestingLabResourcePermissionSchema = z.object({
  action: z.string().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  resourceId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for TestingLabTestingLabRoleTemplate */
TestingLabTestingLabRoleTemplateSchema = z.object({
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isSystemRole: z.boolean().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabTestingLabSettings */
TestingLabTestingLabSettingsSchema = z.object({
  id: z.string().uuid().optional(),
  labName: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
  defaultSessionDuration: z.number().int().optional(),
  allowPublicSignups: z.boolean().optional(),
  requireApproval: z.boolean().optional(),
  enableNotifications: z.boolean().optional(),
  maxSimultaneousSessions: z.number().int().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingLearningCompletionRequirement. A comma-separated combination of the declared flag names. */
TestingLabTestingLearningCompletionRequirementSchema = z.string();

/** Zod schema for TestingLabTestingLocation */
TestingLabTestingLocationSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  name: z.string().min(1).max(200),
  description: z.string().nullable().optional(),
  address: z.string().max(500).nullable().optional(),
  city: z.string().max(100).nullable().optional(),
  state: z.string().max(100).nullable().optional(),
  postalCode: z.string().max(20).nullable().optional(),
  country: z.string().max(100).nullable().optional(),
  capacity: z.number().int().nullable().optional(),
  maxTestersCapacity: z.number().int().optional(),
  maxProjectsCapacity: z.number().int().optional(),
  equipment: z.string().nullable().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  isVirtual: z.boolean().optional(),
  virtualUrl: z.string().max(500).nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
  contactEmail: z.string().max(255).nullable().optional(),
  contactPhone: z.string().max(50).nullable().optional(),
  sessions: z
    .array(z.lazy(() => TestingLabTestingSessionSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  isAvailable: z.boolean().optional(),
  fullAddress: z.string().nullable().optional(),
  activeSessionCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingMode */
TestingLabTestingModeSchema = z.enum(["Online", "InPerson", "Hybrid"]);

/** Zod schema for TestingLabTestingParticipant */
TestingLabTestingParticipantSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  testingRequestId: z.string().uuid(),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  userId: z.string().uuid(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  instructionsAcknowledged: z.boolean(),
  instructionsAcknowledgedAt: z.string().datetime().nullable().optional(),
  startedAt: z.string().datetime(),
  completedAt: z.string().datetime().nullable().optional(),
  timeSpentMinutes: z.number().int().nullable().optional(),
  feedbackCount: z.number().int().optional(),
  status: z.lazy(() => TestingLabParticipationStatusSchema).optional(),
  notes: z.string().nullable().optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  isActive: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  participationDuration: z.string().nullable().optional(),
  canProvideFeedback: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingParticipantDirectoryItemProjection */
TestingLabTestingParticipantDirectoryItemProjectionSchema = z.object({
  registrationId: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  eventName: z.string().nullable().optional(),
  slotId: z.string().uuid().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  campusName: z.string().nullable().optional(),
  roomName: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
  avatarUrl: z.string().nullable().optional(),
  status: z
    .lazy(() => TestingLabTestingSlotRegistrationStatusSchema)
    .optional(),
  waitlistPosition: z.number().int().nullable().optional(),
  notes: z.string().nullable().optional(),
  registeredAt: z.string().datetime().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  pendingFeedbackCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingParticipantDirectoryProjection */
TestingLabTestingParticipantDirectoryProjectionSchema = z.object({
  items: z
    .array(
      z.lazy(() => TestingLabTestingParticipantDirectoryItemProjectionSchema),
    )
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
  registeredCount: z.number().int().optional(),
  waitlistedCount: z.number().int().optional(),
  checkedInCount: z.number().int().optional(),
  attendedCount: z.number().int().optional(),
  completedCount: z.number().int().optional(),
  noShowCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingParticipantMutationProjection */
TestingLabTestingParticipantMutationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  testingRequestId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  status: z.lazy(() => TestingLabParticipationStatusSchema).optional(),
  startedAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingPriority */
TestingLabTestingPrioritySchema = z.enum(["Low", "Medium", "High", "Critical"]);

/** Zod schema for TestingLabTestingProjectApplication */
TestingLabTestingProjectApplicationSchema = z.object({
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  eventId: z.string().uuid().optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  projectId: z.string().uuid().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  submittedByUserId: z.string().uuid().optional(),
  submittedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  submittedAssetReferenceIdsJson: z.string().max(10000).nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
  preferredAvailability: z.string().max(1000).nullable().optional(),
  status: z.lazy(() => TestingLabTestingApplicationStatusSchema).optional(),
  assignedSlotId: z.string().uuid().nullable().optional(),
  assignedSlot: z.lazy(() => TestingLabTestingEventSlotSchema).optional(),
  decidedByUserId: z.string().uuid().nullable().optional(),
  decidedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  decisionRationale: z.string().max(2000).nullable().optional(),
  decidedAt: z.string().datetime().nullable().optional(),
  votes: z
    .array(z.lazy(() => TestingLabTestingApplicationVoteSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingProjectApplicationProjection */
TestingLabTestingProjectApplicationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  submittedByUserId: z.string().uuid().optional(),
  preferredAvailability: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabTestingApplicationStatusSchema).optional(),
  assignedSlotId: z.string().uuid().nullable().optional(),
  decidedByUserId: z.string().uuid().nullable().optional(),
  decisionRationale: z.string().nullable().optional(),
  decidedAt: z.string().datetime().nullable().optional(),
  votes: z
    .array(z.lazy(() => TestingLabTestingApplicationVoteProjectionSchema))
    .nullable()
    .optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for TestingLabTestingRequestDetailProjection */
TestingLabTestingRequestDetailProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  instructionsContent: z.string().nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  currentTesterCount: z.number().int().optional(),
  startDate: z.string().datetime().optional(),
  endDate: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema).optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  projectVersion: z
    .lazy(() => TestingLabTestingRequestProjectVersionProjectionSchema)
    .optional(),
  isDeleted: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingRequestProjectProjection */
TestingLabTestingRequestProjectProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for TestingLabTestingRequestProjectVersionProjection */
TestingLabTestingRequestProjectVersionProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  versionNumber: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  project: z
    .lazy(() => TestingLabTestingRequestProjectProjectionSchema)
    .optional(),
});

/** Zod schema for TestingLabTestingRequestStatus */
TestingLabTestingRequestStatusSchema = z.enum([
  "Draft",
  "Open",
  "Active",
  "InProgress",
  "Paused",
  "Completed",
  "Cancelled",
]);

/** Zod schema for TestingLabTestingSession */
TestingLabTestingSessionSchema = z.object({
  isNew: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  version: z.number().int().optional(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  eventSlotId: z.string().uuid().nullable().optional(),
  eventSlot: z.lazy(() => TestingLabTestingEventSlotSchema).optional(),
  testingRequestId: z.string().uuid(),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  locationId: z.string().uuid(),
  location: z.lazy(() => TestingLabTestingLocationSchema).optional(),
  sessionName: z.string().min(1).max(255),
  sessionDate: z.string().datetime(),
  startTime: z.string().datetime(),
  endTime: z.string().datetime(),
  maxTesters: z.number().int(),
  maxProjects: z.number().int(),
  registeredTesterCount: z.number().int().optional(),
  registeredProjectMemberCount: z.number().int().optional(),
  registeredProjectCount: z.number().int().optional(),
  status: z.lazy(() => TestingLabSessionStatusSchema),
  managerId: z.string().uuid(),
  manager: z.lazy(() => IdentityUsersUserSchema).optional(),
  managerUserId: z.string().uuid().optional(),
  createdById: z.string().uuid(),
  createdBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  registrations: z
    .array(z.lazy(() => TestingLabSessionRegistrationSchema))
    .nullable()
    .optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  isGlobal: z.boolean().optional(),
  isActive: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  allowsRegistration: z.boolean().optional(),
  availableSpots: z.number().int().optional(),
  duration: z.string().optional(),
});

/** Zod schema for TestingLabTestingSlotRegistrationProjection */
TestingLabTestingSlotRegistrationProjectionSchema = z.object({
  id: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
  status: z
    .lazy(() => TestingLabTestingSlotRegistrationStatusSchema)
    .optional(),
  waitlistPosition: z.number().int().nullable().optional(),
  notes: z.string().nullable().optional(),
  registeredAt: z.string().datetime().optional(),
  promotedAt: z.string().datetime().nullable().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  pendingFeedbackCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingSlotRegistrationStatus */
TestingLabTestingSlotRegistrationStatusSchema = z.enum([
  "Registered",
  "Waitlisted",
  "CheckedIn",
  "Attended",
  "Completed",
  "Cancelled",
  "NoShow",
]);

/** Zod schema for TestingLabUpdateAttendance */
TestingLabUpdateAttendanceSchema = z.object({
  userId: z.string().uuid().optional(),
  attendanceStatus: z.lazy(() => TestingLabAttendanceStatusSchema).optional(),
});

/** Zod schema for TestingLabUpdateTestingEventInput */
TestingLabUpdateTestingEventInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  approvalMode: z
    .lazy(() => TestingLabTestingEventApprovalModeSchema)
    .optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  requiresFeedback: z.boolean().optional(),
});

/** Zod schema for TestingLabUpdateTestingInput */
TestingLabUpdateTestingInputSchema = z.object({
  projectVersionId: z.string().uuid().nullable().optional(),
  title: z.string().max(255).nullable().optional(),
  description: z.string().nullable().optional(),
  downloadUrl: z.string().max(500).nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema).optional(),
  instructionsContent: z.string().nullable().optional(),
  instructionsUrl: z.string().max(500).nullable().optional(),
  instructionsFileId: z.string().uuid().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema).optional(),
});

/** Zod schema for TestingLabUpdateTestingLabRoleInput */
TestingLabUpdateTestingLabRoleInputSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabUpdateTestingLabSettings */
TestingLabUpdateTestingLabSettingsSchema = z.object({
  labName: z.string().max(255).nullable().optional(),
  description: z.string().max(1000).nullable().optional(),
  timezone: z.string().max(50).nullable().optional(),
  defaultSessionDuration: z
    .number()
    .int()
    .min(15)
    .max(480)
    .nullable()
    .optional(),
  allowPublicSignups: z.boolean().nullable().optional(),
  requireApproval: z.boolean().nullable().optional(),
  enableNotifications: z.boolean().nullable().optional(),
  maxSimultaneousSessions: z
    .number()
    .int()
    .min(1)
    .max(100)
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabUpdateTestingLocation */
TestingLabUpdateTestingLocationSchema = z.object({
  name: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  address: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  maxTestersCapacity: z.number().int().nullable().optional(),
  maxProjectsCapacity: z.number().int().nullable().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  isVirtual: z.boolean().nullable().optional(),
  virtualUrl: z.string().nullable().optional(),
  contactEmail: z.string().nullable().optional(),
  contactPhone: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
});

/** Zod schema for TestingLabUpdateTestingProjectApplicationInput */
TestingLabUpdateTestingProjectApplicationInputSchema = z.object({
  projectVersionId: z.string().uuid().optional(),
  preferredAvailability: z.string().nullable().optional(),
  submittedAssetReferenceIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for TestingLabUpsertTestingEventSlotInput */
TestingLabUpsertTestingEventSlotInputSchema = z.object({
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  startsAt: z.string().datetime().optional(),
  endsAt: z.string().datetime().optional(),
  maxTesters: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  campusName: z.string().nullable().optional(),
  roomName: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  locationId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabUserTestingLabPermissions */
TestingLabUserTestingLabPermissionsSchema = z.object({
  userId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  assignedRoles: z.array(z.string()).nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
  resourcePermissions: z
    .array(z.lazy(() => TestingLabTestingLabResourcePermissionSchema))
    .nullable()
    .optional(),
});
