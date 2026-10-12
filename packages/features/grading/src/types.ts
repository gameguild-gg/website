import type { AssessmentReviewMethod, ReviewMethods } from "./review-methods";
import type { PercentValue, ScoreValue } from "./values";

export const CONTENT_GRADING_SCHEMA_VERSION = 2 as const;
export const ASSESSMENT_EXECUTION_POLICY_SCHEMA_VERSION = 1 as const;
export const ASSESSMENT_RESPONSE_SCHEMA_VERSION = 1 as const;
export const ASSESSMENT_EXECUTION_MANIFEST_SCHEMA_VERSION = 1 as const;
export const ASSESSMENT_EXECUTION_DELIVERY_SCHEMA_VERSION = 1 as const;
export const GRADE_RESULT_SCHEMA_VERSION = 1 as const;
export const AUTHORING_SOURCE_HASH_VERSION = "sha256-jcs-v1" as const;
export const EXECUTION_SNAPSHOT_HASH_VERSION = "sha256-jcs-v1" as const;
export const DELIVERY_HASH_VERSION = "sha256-jcs-v1" as const;

export interface GradingItemAuthoringV2 {
  rubricRef?: string;
}

export interface ContentGradingDefinitionV2 {
  schemaVersion: typeof CONTENT_GRADING_SCHEMA_VERSION;
  items: Record<string, GradingItemAuthoringV2>;
}

export type AttemptContributionModeV1 =
  | "first-finalized"
  | "last-finalized"
  | "highest-finalized";

export interface AttemptContributionPolicyV1 {
  mode: AttemptContributionModeV1;
}

export interface AssessmentAvailabilityPolicyV1 {
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  allowLateSubmissions: boolean;
  lateSubmissionDeadline?: string | null;
}

export type AssessmentContentCompletionMode =
  | "on-submit"
  | "on-finalize"
  | "on-release"
  | "on-release-and-pass";

export interface AssessmentContentCompletionPolicyV1 {
  mode: AssessmentContentCompletionMode;
}

export type AssessmentResultReleasePolicyV1 =
  | { mode: "immediate" }
  | { mode: "manual" }
  | { mode: "scheduled"; scheduledFor: string };

export interface AssessmentPresentationPolicyV1 {
  mode: "continuous" | "single-step";
}

export interface AssessmentReviewPolicyV1 {
  schemaVersion: 1;
  methods: ReviewMethods;
  peer?: {
    reviewsPerReviewer: number;
    reviewsRequiredPerSubmission: number;
    minimumReviewsToFinalize: number;
    aggregation: "mean" | "median";
    claimLeaseMinutes: number;
    evidenceWindowMinutes: number;
    onInsufficientEvidence: "await-instructor-resolution";
  };
  ai?: {
    providerKey: string;
    policyVersion: string;
  };
  self?: {
    instructions?: string;
    requireFeedback: boolean;
  };
  instructor?: {
    requireOverrideReason: boolean;
  };
}

export interface AssessmentExecutionPolicyV1 {
  schemaVersion: typeof ASSESSMENT_EXECUTION_POLICY_SCHEMA_VERSION;
  passingScore?: ScoreValue;
  maxAttempts?: number;
  attemptContribution?: AttemptContributionPolicyV1;
  timeLimitMinutes?: number;
  availability: AssessmentAvailabilityPolicyV1;
  completion: AssessmentContentCompletionPolicyV1;
  resultRelease: AssessmentResultReleasePolicyV1;
  presentation: AssessmentPresentationPolicyV1;
  review: AssessmentReviewPolicyV1;
}

export interface AssessmentAuthoringSourceV1<TContent = unknown> {
  schemaVersion: 1;
  contentType: string;
  content: TContent;
  grading: ContentGradingDefinitionV2;
  policy: AssessmentExecutionPolicyV1;
}

export interface AssessmentItemManifestV1 {
  itemId: string;
  itemType: string;
  adapterKey: string;
  adapterVersion: string;
}

export interface AssessmentReviewStageManifestV1 {
  method: AssessmentReviewMethod;
  handlerKey: string;
  handlerVersion: string;
  providerKey?: string;
  providerPolicyVersion?: string;
}

export interface AssessmentExecutionManifestV1 {
  schemaVersion: typeof ASSESSMENT_EXECUTION_MANIFEST_SCHEMA_VERSION;
  items: AssessmentItemManifestV1[];
  stages: AssessmentReviewStageManifestV1[];
  policies: Array<{ policyKey: string; policyVersion: string }>;
}

export interface AssessmentItemProjectionV1 {
  schemaVersion: number;
  itemId: string;
  itemType: string;
  maxScore: ScoreValue;
  source: {
    contentType: string;
    itemId: string;
  };
}

export interface AssessmentExecutionSnapshotV1<
  TContent = unknown,
  TItemProjection extends AssessmentItemProjectionV1 = AssessmentItemProjectionV1,
> {
  schemaVersion: 1;
  authoringSource: AssessmentAuthoringSourceV1<TContent>;
  manifest: AssessmentExecutionManifestV1;
  itemProjections: Record<string, TItemProjection>;
}

export interface AssessmentResponseEnvelopeV1<TPayload = unknown> {
  schemaVersion: typeof ASSESSMENT_RESPONSE_SCHEMA_VERSION;
  contentType: string;
  payloadSchema: string;
  payload: TPayload;
}

export interface AssessmentExecutionDeliveryItemV1<TPayload = unknown> {
  adapterKey: string;
  adapterVersion: string;
  learnerPayload: TPayload;
}

export interface AssessmentExecutionDeliveryV1<TPayload = unknown> {
  schemaVersion: typeof ASSESSMENT_EXECUTION_DELIVERY_SCHEMA_VERSION;
  definitionRevisionId: string;
  executionSnapshotHash: string;
  itemOrder: string[];
  items: Record<string, AssessmentExecutionDeliveryItemV1<TPayload>>;
}

export type GradeItemStateV1 = "graded" | "pending" | "unsupported";

export interface GradeItemResultV1 {
  itemId: string;
  state: GradeItemStateV1;
  score: ScoreValue | null;
  maxScore: ScoreValue;
  feedback?: string;
  evidenceRefs: string[];
  reviewMethod: AssessmentReviewMethod;
  handlerKey: string;
  handlerVersion: string;
  providerKey?: string;
}

export interface GradeResultV1 {
  schemaVersion: typeof GRADE_RESULT_SCHEMA_VERSION;
  state: "partial" | "final";
  score: ScoreValue | null;
  maxScore: ScoreValue;
  items: GradeItemResultV1[];
  feedback?: string;
  evidenceRefs: string[];
}

export type GradeRoundStatusV1 =
  | "pending"
  | "running"
  | "awaiting-evidence"
  | "awaiting-instructor-resolution"
  | "failed"
  | "finalized";

export type ReviewStageStatusV1 =
  | "pending"
  | "running"
  | "awaiting-evidence"
  | "awaiting-instructor-resolution"
  | "completed"
  | "failed";

export interface ReviewStageV1 {
  id: string;
  method: AssessmentReviewMethod;
  status: ReviewStageStatusV1;
  handlerKey: string;
  handlerVersion: string;
  providerKey?: string;
  actorIds?: string[];
  evidenceRefs?: string[];
  result?: GradeResultV1;
  startedAt?: string;
  completedAt?: string;
}

export interface GradeRoundV1 {
  id: string;
  supersedesRoundId?: string;
  reason: "initial" | "regrade";
  definitionRevisionId: string;
  configuredReviews: AssessmentReviewMethod[];
  currentReview: AssessmentReviewMethod | null;
  status: GradeRoundStatusV1;
  stages: ReviewStageV1[];
  finalResult: GradeResultV1 | null;
  initiatedBy?: string;
  initiatedAt: string;
  finalizedAt?: string;
}

export interface AssessmentEvaluationV1 {
  schemaVersion: 1;
  activeRoundId: string;
  rounds: GradeRoundV1[];
}

export interface GradingExecutionV1 {
  schemaVersion: 1;
  id: string;
  context: ReviewExecutionContext;
  definitionRevisionId: string;
  executionSnapshotHash: string;
  deliveryHash?: string;
  state: "pending" | "running" | "awaiting-review" | "completed" | "failed";
}

export type ReviewExecutionContext = "author-test" | "official-submission";

export interface IdempotentCommandEnvelopeV1<TPayload> {
  schemaVersion: 1;
  tenantId: string;
  resourceId: string;
  command: string;
  actorId: string;
  idempotencyKey: string;
  requestHash: string;
  payload: TPayload;
}

export interface VersionedCollectiveCommandV1 {
  expectedVersion: number;
}

export type SaveCollectiveAttemptDraftV1 = IdempotentCommandEnvelopeV1<
  VersionedCollectiveCommandV1 & { response: AssessmentResponseEnvelopeV1 }
>;

export type SubmitCollectiveAttemptV1 = IdempotentCommandEnvelopeV1<
  VersionedCollectiveCommandV1
>;

export type SaveCollectiveSelfReviewDraftV1 = IdempotentCommandEnvelopeV1<
  VersionedCollectiveCommandV1 & { evidence: unknown }
>;

export type SubmitCollectiveSelfReviewV1 = IdempotentCommandEnvelopeV1<
  VersionedCollectiveCommandV1
>;

export interface ReleaseGradeResultPayloadV1 {
  submissionId: string;
  expectedRoundId: string;
  expectedVersion: number;
  reason?: string;
}

export type ReleaseGradeResultV1 = IdempotentCommandEnvelopeV1<ReleaseGradeResultPayloadV1>;

export interface GradebookAssessmentContributionV1 {
  assessmentId: string;
  effectiveScore: ScoreValue;
  capturedMaxScore: ScoreValue;
}

export interface GradebookGroupContributionV1 {
  assessmentGroupId: string;
  weightPercent: PercentValue;
  assessments: GradebookAssessmentContributionV1[];
}

/** API-facing state of one persisted grading execution. */
export type GradingRuntimeExecutionStatus =
  | "pending"
  | "running"
  | "awaitingReview"
  | "completed"
  | "failed";

/** API-facing state of a persisted grade round. */
export type GradingRuntimeRoundStatus =
  | "pending"
  | "running"
  | "awaitingEvidence"
  | "awaitingInstructorResolution"
  | "failed"
  | "finalized";

export type AssessmentTestRunStatus =
  | "draft"
  | "running"
  | "completed"
  | "cancelled";

export type AssessmentRuntimeSubmissionStatus =
  | "inProgress"
  | "submitted"
  | "graded"
  | "returned"
  | "late";

export interface GradeRoundRuntimeViewV1 {
  roundId: string;
  roundNumber: number;
  reason: string;
  reasonDetail: string | null;
  initiatedByActorId: string | null;
  status: GradingRuntimeRoundStatus;
  startedAt: string;
  finalizedAt: string | null;
  result: GradeResultV1 | null;
  released: boolean;
  releasedAt: string | null;
}

export interface AssessmentExecutionRuntimeViewV1<TLearnerPayload = unknown> {
  executionId: string;
  definitionRevisionId: string;
  context: ReviewExecutionContext;
  executionSnapshotHash: string;
  deliveryHash: string;
  delivery: AssessmentExecutionDeliveryV1<TLearnerPayload>;
  itemMaxScores: Record<string, ScoreValue>;
  submittedResponse: AssessmentResponseEnvelopeV1 | null;
  status: GradingRuntimeExecutionStatus;
  activeRoundId: string | null;
  instructorVisibleResult: GradeResultV1 | null;
  learnerVisibleResult: GradeResultV1 | null;
  requiresInstructorReview: boolean;
  released: boolean;
  history: GradeRoundRuntimeViewV1[];
  /** Frozen private Code definition; supplied only after server instructor authorization. */
  instructorVisibleContent?: unknown | null;
  instructorVisibleRubric?: {
    id: string;
    title: string;
    criteria: Array<{ id: string; description: string; points: ScoreValue; order: number }>;
  } | null;
  instructorVisibleRubricScores?: Record<string, ScoreValue> | null;
}

export interface AssessmentTestRunRuntimeViewV1<TLearnerPayload = unknown> {
  testRunId: string;
  assessmentId: string;
  definitionRevisionId: string;
  status: AssessmentTestRunStatus;
  personaKey: string;
  personaDisplayName: string;
  execution: AssessmentExecutionRuntimeViewV1<TLearnerPayload>;
  candidateStillMatchesDraft: boolean;
  readyForPublication: boolean;
  diagnostics: string[];
}

export interface AssessmentSubmissionRuntimeViewV1<TLearnerPayload = unknown> {
  submissionId: string;
  assessmentId: string;
  definitionRevisionId: string;
  enrollmentId: string | null;
  courseGroupId: string | null;
  attemptNumber: number;
  status: AssessmentRuntimeSubmissionStatus;
  draftVersion: number;
  version: number;
  startedAt: string;
  submittedAt: string | null;
  submittedByUserId: string | null;
  contentCompleted: boolean;
  execution: AssessmentExecutionRuntimeViewV1<TLearnerPayload>;
}

export interface InstructorItemResolutionV1 {
  itemId: string;
  score: ScoreValue;
  feedback?: string | null;
}

export interface InstructorReviewResolutionV1 {
  schemaVersion: 1;
  items: InstructorItemResolutionV1[];
  feedback?: string | null;
  overrideReason?: string | null;
  rubricScores?: Record<string, ScoreValue> | null;
}

export class GradingContractValidationError extends Error {
  readonly issues: string[];

  constructor(issues: string[]) {
    super(issues.join("; "));
    this.name = "GradingContractValidationError";
    this.issues = issues;
  }
}
