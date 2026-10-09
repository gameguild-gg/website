'use server';

import { getToken } from '@/auth';
import { createServerClient } from '@game-guild/client';
import type {
  AssessmentResponseEnvelopeV1,
  AssessmentSubmissionRuntimeViewV1,
  AssessmentTestRunRuntimeViewV1,
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
): Promise<GradingRuntimeActionResult<T>> {
  try {
    const result = await getRuntimeClient().request<T>({
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
    return { success: true, data: result.data };
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
  );
}

export async function startIndividualRuntimeSubmission(
  assessmentId: string,
  enrollmentId: string,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest('POST',
    `/v1.0/assessments/${encodeURIComponent(assessmentId)}/runtime-submissions/individual`,
    { enrollmentId, idempotencyKey });
}

export async function getRuntimeSubmission(
  submissionId: string,
): Promise<GradingRuntimeActionResult<AssessmentSubmissionRuntimeViewV1>> {
  return runtimeRequest(
    'GET',
    `/v1.0/assessments/runtime-submissions/${encodeURIComponent(submissionId)}`,
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
  );
}

export async function releaseRuntimeSubmission(
  submission: Pick<AssessmentSubmissionRuntimeViewV1, 'submissionId' | 'version'> & {
    expectedRoundId: string;
  },
  reason: string | null,
  idempotencyKey: string,
): Promise<GradingRuntimeActionResult<{ releaseId: string; gradeRoundId: string; releasedAt: string }>> {
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
  );
}
