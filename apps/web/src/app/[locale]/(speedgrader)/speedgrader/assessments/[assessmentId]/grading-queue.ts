import { getToken } from '@/auth';
import { createServerClient, type LearningAssessmentsAssessmentType } from '@game-guild/client';
import type { ReviewMethods, ScoreValue } from '@game-guild/grading';

// Local queue contract (camelCase wire shape), kept out of the generated
// client: the generated types model assessment.rubric as non-nullable but the
// API returns null for rubric-less assessments (the default state), failing
// response validation — so the queue is fetched via the raw request channel
// and typed here.

export type SpeedgraderGradingQueue = {
  assessment?: {
    id?: string;
    title?: string | null;
    type?: LearningAssessmentsAssessmentType;
    maxScore?: ScoreValue;
    reviewMethods?: ReviewMethods;
    groupSetId?: string | null;
    hasRubric?: boolean;
    rubric?: {
      id?: string;
      title?: string | null;
      criteria?: Array<{
        id?: string;
        description?: string | null;
        points?: ScoreValue;
        order?: number;
      }> | null;
    } | null;
  };
  items?: Array<{
    submissionId?: string;
    canonicalSubmissionId?: string;
    userId?: string | null;
    displayName?: string | null;
    groupName?: string | null;
    memberNames?: string[] | null;
    groupId?: string | null;
    attemptNumber?: number;
    attemptCount?: number;
    status?: 'InProgress' | 'Submitted' | 'Graded' | 'Returned' | 'Late';
    assignmentScore?: ScoreValue | null;
    assignmentPassed?: boolean | null;
    isLate?: boolean;
    submittedAt?: string | null;
    isGroup?: boolean;
  }> | null;
  total?: number;
  needsGrading?: number;
};

export type QueueResult =
  | { ok: true; data: SpeedgraderGradingQueue }
  | { ok: false; status?: number; message: string };

export async function fetchGradingQueue(
  assessmentId: string,
): Promise<QueueResult> {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const client = createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });
  try {
    const result = await client.request<SpeedgraderGradingQueue>({
      method: 'GET',
      path: `/v1.0/assessments/${assessmentId}/grading-queue`,
      requiresAuth: true,
    });
    if (result.ok) {
      return { ok: true, data: result.data };
    }
    return {
      ok: false,
      status: result.error?.status,
      message:
        result.error?.status === 403
          ? 'You do not have permission to grade this assessment.'
          : 'The grading queue could not be loaded. Try again in a moment.',
    };
  } catch (err) {
    console.error('Error fetching grading queue:', err);
    return {
      ok: false,
      message: 'The grading queue could not be loaded. Try again in a moment.',
    };
  }
}
