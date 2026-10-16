'use server';

import { getToken } from '@/auth';
import { createServerClient } from '@game-guild/client';
import type {
  AssessmentExecutionRuntimeViewV1,
  AssessmentResponseEnvelopeV1,
  AssessmentRuntimeSubmissionStatus,
  AssessmentSubmissionRuntimeViewV1,
  AssessmentTestRunStatus,
  AssessmentTestRunRuntimeViewV1,
  GradeRoundRuntimeViewV1,
  GradeResultReleaseScheduleRuntimeViewV1,
  GradingRuntimeExecutionStatus,
  GradingRuntimeRoundStatus,
  InstructorReviewResolutionV1,
  PeerReviewClaimResultV1,
  PeerReviewClaimRuntimeViewV1,
  ScoreValue,
  SelfReviewEvidenceV1,
} from '@game-guild/grading';

export type GradingRuntimeActionResult<T> =
  | { success: true; data: T }
  | { success: false; error: string; status?: number };

interface ApiErrorShape {
  status?: number;
  message?: string;
  detail?: string;
}

type RuntimeNormalizer<T> = (value: unknown) => T;

const SUBMISSION_STATUS_MAP: Record<string, AssessmentRuntimeSubmissionStatus> =
  {
    InProgress: 'inProgress',
    Submitted: 'submitted',
    Graded: 'graded',
    Returned: 'returned',
    Late: 'late',
    inProgress: 'inProgress',
    submitted: 'submitted',
    graded: 'graded',
    returned: 'returned',
    late: 'late',
  };

const TEST_RUN_STATUS_MAP: Record<string, AssessmentTestRunStatus> = {
  Draft: 'draft',
  Running: 'running',
  Completed: 'completed',
  Cancelled: 'cancelled',
  draft: 'draft',
  running: 'running',
  completed: 'completed',
  cancelled: 'cancelled',
};

const EXECUTION_STATUS_MAP: Record<string, GradingRuntimeExecutionStatus> = {
  Pending: 'pending',
  Running: 'running',
  AwaitingReview: 'awaitingReview',
  Completed: 'completed',
  Failed: 'failed',
  pending: 'pending',
  running: 'running',
  awaitingReview: 'awaitingReview',
  completed: 'completed',
  failed: 'failed',
};

const ROUND_STATUS_MAP: Record<string, GradingRuntimeRoundStatus> = {
  Pending: 'pending',
  Running: 'running',
  AwaitingEvidence: 'awaitingEvidence',
  AwaitingInstructorResolution: 'awaitingInstructorResolution',
  Failed: 'failed',
  Finalized: 'finalized',
  pending: 'pending',
  running: 'running',
  awaitingEvidence: 'awaitingEvidence',
  awaitingInstructorResolution: 'awaitingInstructorResolution',
  failed: 'failed',
  finalized: 'finalized',
};

const REVIEW_STAGE_STATUS_MAP = {
  Pending: 'pending',
  Running: 'running',
  AwaitingEvidence: 'awaiting-evidence',
  AwaitingInstructorResolution: 'awaiting-instructor-resolution',
  Completed: 'completed',
  Failed: 'failed',
  pending: 'pending',
  running: 'running',
  awaitingEvidence: 'awaiting-evidence',
  awaitingInstructorResolution: 'awaiting-instructor-resolution',
  completed: 'completed',
  failed: 'failed',
} as const;

const RELEASE_SCHEDULE_STATUS_MAP = {
  Scheduled: 'scheduled',
  Cancelled: 'cancelled',
  Released: 'released',
  scheduled: 'scheduled',
  cancelled: 'cancelled',
  released: 'released',
} as const;

const PEER_REVIEW_CLAIM_STATUS_MAP = {
  Assigned: 'assigned',
  Submitted: 'submitted',
  Expired: 'expired',
  Cancelled: 'cancelled',
  assigned: 'assigned',
  submitted: 'submitted',
  expired: 'expired',
  cancelled: 'cancelled',
} as const;

function requireRecord(value: unknown, label: string): Record<string, unknown> {
  if (value === null || typeof value !== 'object' || Array.isArray(value)) {
    throw new Error(`Invalid ${label} returned by the grading API.`);
  }
  return value as Record<string, unknown>;
}

function normalizeStatus<T extends string>(
  value: unknown,
  statuses: Record<string, T>,
  label: string,
): T {
  if (typeof value !== 'string' || !Object.hasOwn(statuses, value)) {
    throw new Error(
      `Unsupported ${label} returned by the grading API: ${String(value)}.`,
    );
  }
  return statuses[value];
}

function normalizeGradeRound(value: unknown): GradeRoundRuntimeViewV1 {
  const round = requireRecord(value, 'grade round');
  const releaseSchedule =
    round.releaseSchedule == null
      ? null
      : requireRecord(round.releaseSchedule, 'grade release schedule');
  return {
    ...round,
    status: normalizeStatus(
      round.status,
      ROUND_STATUS_MAP,
      'grade round status',
    ),
    releaseSchedule:
      releaseSchedule == null
        ? null
        : {
            ...releaseSchedule,
            status: normalizeStatus(
              releaseSchedule.status,
              RELEASE_SCHEDULE_STATUS_MAP,
              'grade release schedule status',
            ),
          },
  } as unknown as GradeRoundRuntimeViewV1;
}

function normalizeExecution<TLearnerPayload = unknown>(
  value: unknown,
): AssessmentExecutionRuntimeViewV1<TLearnerPayload> {
  const execution = requireRecord(value, 'grading execution');
  if (!Array.isArray(execution.history)) {
    throw new Error(
      'Invalid grading execution history returned by the grading API.',
    );
  }
  return {
    ...execution,
    status: normalizeStatus(
      execution.status,
      EXECUTION_STATUS_MAP,
      'grading execution status',
    ),
    history: execution.history.map(normalizeGradeRound),
    selfReview:
      execution.selfReview == null
        ? execution.selfReview
        : {
            ...requireRecord(execution.selfReview, 'self review state'),
            status: normalizeStatus(
              requireRecord(execution.selfReview, 'self review state').status,
              REVIEW_STAGE_STATUS_MAP,
              'self review stage status',
            ),
          },
  } as unknown as AssessmentExecutionRuntimeViewV1<TLearnerPayload>;
}

function normalizeRuntimeSubmission(
  value: unknown,
): AssessmentSubmissionRuntimeViewV1 {
  const submission = requireRecord(value, 'runtime submission');
  return {
    ...submission,
    status: normalizeStatus(
      submission.status,
      SUBMISSION_STATUS_MAP,
      'runtime submission status',
    ),
    execution: normalizeExecution(submission.execution),
  } as unknown as AssessmentSubmissionRuntimeViewV1;
}

function normalizeTestRun(value: unknown): AssessmentTestRunRuntimeViewV1 {
  const testRun = requireRecord(value, 'assessment test run');
  const subjects = Array.isArray(testRun.subjects)
    ? testRun.subjects.map((rawSubject) => {
        const subject = requireRecord(rawSubject, 'assessment test run subject');
        return { ...subject, execution: normalizeExecution(subject.execution) };
      })
    : undefined;
  return {
    ...testRun,
    status: normalizeStatus(
      testRun.status,
      TEST_RUN_STATUS_MAP,
      'assessment test run status',
    ),
    execution: normalizeExecution(testRun.execution),
    subjects,
  } as unknown as AssessmentTestRunRuntimeViewV1;
}

function normalizePeerReviewClaim(value: unknown): PeerReviewClaimRuntimeViewV1 {
  const claim = requireRecord(value, 'peer-review claim');
  return {
    ...claim,
    status: normalizeStatus(
      claim.status,
      PEER_REVIEW_CLAIM_STATUS_MAP,
      'peer-review claim status',
    ),
  } as unknown as PeerReviewClaimRuntimeViewV1;
}

function getRuntimeClient() {
  return createServerClient({
    baseUrl:
      process.env.API_URL ||
      process.env.NEXT_PUBLIC_API_URL ||
      'http://localhost:8080',
    auth: { getAccessToken: () => getToken() },
  });
}

async function runtimeRequest<T>(
  method: 'GET' | 'POST' | 'PUT',
  path: string,
  body?: unknown,
  timeout?: number,
  normalize?: RuntimeNormalizer<T>,
): Promise<GradingRuntimeActionResult<T>> {
  try {
    const result = await getRuntimeClient().request<unknown>({
      method,
      path,
      body,
      requiresAuth: true,
      ...(timeout === undefined ? {} : { timeout }),
    });
    if (!result.ok) {
      const error = result.error as ApiErrorShape;
      return {
        success: false,
        error: error.detail || error.message || 'Grading request failed.',
        status: error.status,
      };
    }
    return {
      success: true,
      data: normalize ? normalize(result.data) : (result.data as T),
    };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Grading request failed.',
    };
  }
}

export async function startContentRuntimeSubmission(
  contentId: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/content/${encodeURIComponent(contentId)}/runtime-submissions/individual`,
    { idempotencyKey },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function startIndividualRuntimeSubmission(
  assessmentId: string,
  enrollmentId: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/${encodeURIComponent(assessmentId)}/runtime-submissions/individual`,
    { enrollmentId, idempotencyKey },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function getRuntimeSubmission(
  submissionId: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'GET',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}`,
    undefined,
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function submitRuntimeSubmission(
  submissionId: string,
  response: AssessmentResponseEnvelopeV1,
  idempotencyKey: string,
  expectedDraftVersion?: number,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}/submit`,
    {
      response,
      idempotencyKey,
      expectedDraftVersion: expectedDraftVersion ?? null,
    },
    // Code compilation runs in the trusted worker (300s deadline + 5s slot wait).
    // Keep the transport alive long enough to receive its durable receipt.
    response.contentType === 'coding-assignment' ? 330_000 : undefined,
    normalizeRuntimeSubmission,
  );
}

export async function saveCollectiveRuntimeSelfReviewDraft(
  submissionId: string,
  evidence: SelfReviewEvidenceV1,
  expectedVersion: number,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'PUT',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}/self-review/draft`,
    { evidence, expectedVersion, idempotencyKey },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function submitRuntimeSelfReview(
  submissionId: string,
  evidence: SelfReviewEvidenceV1,
  idempotencyKey: string,
  expectedDraftVersion?: number,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}/self-review`,
    {
      evidence,
      idempotencyKey,
      expectedDraftVersion: expectedDraftVersion ?? null,
    },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function resolveRuntimeInstructorReview(
  submissionId: string,
  resolution: InstructorReviewResolutionV1,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}/instructor-review`,
    { resolution, idempotencyKey },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function regradeRuntimeSubmission(
  submissionId: string,
  reason: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}/regrade`,
    { reason, idempotencyKey },
    undefined,
    normalizeRuntimeSubmission,
  );
}

export async function releaseRuntimeSubmission(
  submission: Pick<
    AssessmentSubmissionRuntimeViewV1,
    'submissionId' | 'version'
  > & {
    expectedRoundId: string;
  },
  reason: string | null,
  idempotencyKey: string,
): Promise<
  GradingRuntimeActionResult<{
    releaseId: string;
    gradeRoundId: string;
    releasedAt: string;
  }>
> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submission.submissionId)}/release`,
    {
      expectedRoundId: submission.expectedRoundId,
      expectedSubmissionVersion: submission.version,
      idempotencyKey,
      reason,
    },
  );
}

export async function scheduleRuntimeSubmissionRelease(
  submission: Pick<
    AssessmentSubmissionRuntimeViewV1,
    'submissionId' | 'version'
  > & {
    expectedRoundId: string;
    expectedScheduleVersion: number;
  },
  scheduledFor: string,
  reason: string | null,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<GradeResultReleaseScheduleRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submission.submissionId)}/release-schedule`,
    {
      expectedRoundId: submission.expectedRoundId,
      expectedSubmissionVersion: submission.version,
      expectedScheduleVersion: submission.expectedScheduleVersion,
      scheduledFor,
      idempotencyKey,
      reason,
    },
    undefined,
    (value) => {
      const schedule = requireRecord(value, 'grade release schedule');
      return {
        ...schedule,
        status: normalizeStatus(
          schedule.status,
          RELEASE_SCHEDULE_STATUS_MAP,
          'grade release schedule status',
        ),
      } as unknown as GradeResultReleaseScheduleRuntimeViewV1;
    },
  );
}

export async function cancelRuntimeSubmissionReleaseSchedule(
  submission: Pick<
    AssessmentSubmissionRuntimeViewV1,
    'submissionId' | 'version'
  > & {
    expectedRoundId: string;
    expectedScheduleVersion: number;
  },
  reason: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<GradeResultReleaseScheduleRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submission.submissionId)}/release-schedule/cancel`,
    {
      expectedRoundId: submission.expectedRoundId,
      expectedSubmissionVersion: submission.version,
      expectedScheduleVersion: submission.expectedScheduleVersion,
      idempotencyKey,
      reason,
    },
    undefined,
    (value) => {
      const schedule = requireRecord(value, 'grade release schedule');
      return {
        ...schedule,
        status: normalizeStatus(
          schedule.status,
          RELEASE_SCHEDULE_STATUS_MAP,
          'grade release schedule status',
        ),
      } as unknown as GradeResultReleaseScheduleRuntimeViewV1;
    },
  );
}

export async function startAssessmentTestRun(
  assessmentId: string,
  revisionId: string,
  idempotencyKey: string,
  personaKey = 'instructor-preview',
  personaDisplayName = 'Instructor preview',
  subjects?: Array<{ personaKey: string; personaDisplayName: string }>,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/${encodeURIComponent(assessmentId)}/test-runs`,
    { revisionId, personaKey, personaDisplayName, idempotencyKey, subjects },
    undefined,
    normalizeTestRun,
  );
}

export async function submitAssessmentTestRun(
  testRunId: string,
  response: AssessmentResponseEnvelopeV1,
  idempotencyKey: string,
  subjectId?: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/submit`,
    { response, idempotencyKey, expectedDraftVersion: null, subjectId },
    response.contentType === 'coding-assignment' ? 330_000 : undefined,
    normalizeTestRun,
  );
}

export async function resolveTestRunInstructorReview(
  testRunId: string,
  resolution: InstructorReviewResolutionV1,
  idempotencyKey: string,
  subjectId?: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/instructor-review`,
    { resolution, idempotencyKey, subjectId },
    undefined,
    normalizeTestRun,
  );
}

export async function submitTestRunSelfReview(
  testRunId: string,
  evidence: SelfReviewEvidenceV1,
  idempotencyKey: string,
  subjectId?: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/self-review`,
    { evidence, idempotencyKey, subjectId },
    undefined,
    normalizeTestRun,
  );
}

export async function claimTestRunPeerReview(
  testRunId: string,
  subjectId: string,
): Promise<GradingRuntimeActionResult<PeerReviewClaimResultV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/peer-reviews/claim`,
    { subjectId },
  );
}

export async function getTestRunPeerReview(
  testRunId: string,
  reviewId: string,
): Promise<GradingRuntimeActionResult<PeerReviewClaimRuntimeViewV1>> {
  return runtimeRequest(
    'GET',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/peer-reviews/${encodeURIComponent(reviewId)}`,
    undefined,
    undefined,
    normalizePeerReviewClaim,
  );
}

export async function submitTestRunPeerReview(
  testRunId: string,
  reviewId: string,
  score: ScoreValue,
  feedback: string,
  rubricScores?: string | null,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/peer-reviews/${encodeURIComponent(reviewId)}/submit`,
    { score, feedback, rubricScores: rubricScores ?? null },
    undefined,
    normalizeTestRun,
  );
}

export async function restartAssessmentTestRun(
  testRunId: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/restart`,
    { idempotencyKey },
    undefined,
    normalizeTestRun,
  );
}
