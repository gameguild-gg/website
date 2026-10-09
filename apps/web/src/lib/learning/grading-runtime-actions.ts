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
  GradingRuntimeExecutionStatus,
  GradingRuntimeRoundStatus,
  InstructorReviewResolutionV1,
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
  return {
    ...round,
    status: normalizeStatus(
      round.status,
      ROUND_STATUS_MAP,
      'grade round status',
    ),
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
  return {
    ...testRun,
    status: normalizeStatus(
      testRun.status,
      TEST_RUN_STATUS_MAP,
      'assessment test run status',
    ),
    execution: normalizeExecution(testRun.execution),
  } as unknown as AssessmentTestRunRuntimeViewV1;
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

export async function startAssessmentTestRun(
  assessmentId: string,
  revisionId: string,
  idempotencyKey: string,
  personaKey = 'instructor-preview',
  personaDisplayName = 'Instructor preview',
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/${encodeURIComponent(assessmentId)}/test-runs`,
    { revisionId, personaKey, personaDisplayName, idempotencyKey },
    undefined,
    normalizeTestRun,
  );
}

export async function submitAssessmentTestRun(
  testRunId: string,
  response: AssessmentResponseEnvelopeV1,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/submit`,
    { response, idempotencyKey, expectedDraftVersion: null },
    response.contentType === 'coding-assignment' ? 330_000 : undefined,
    normalizeTestRun,
  );
}

export async function resolveTestRunInstructorReview(
  testRunId: string,
  resolution: InstructorReviewResolutionV1,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentTestRunRuntimeViewV1>> {
  return runtimeRequest(
    'POST',
    `/v1.0/assessments/test-runs/${encodeURIComponent(testRunId)}/instructor-review`,
    { resolution, idempotencyKey },
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
