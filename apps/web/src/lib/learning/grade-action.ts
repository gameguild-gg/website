'use server';

import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
} from '@game-guild/client';
import { pointsToScoreUnits, rubricScoresToUnits } from '@/lib/learning/academic-values';

type ActionResult<T> =
  | { success: true; data: T }
  | { success: false; error: string };

function getApiClient() {
  const apiUrl =
    process.env.API_URL ||
    process.env.NEXT_PUBLIC_API_URL ||
    'http://localhost:8080';
  return createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });
}

/**
 * Post an instructor grade for an assessment submission.
 *
 * Backend `POST /v1.0/assessments/submissions/{id}/grade` enforces
 * `CanReviewCourseAsync`; the actor's `gradedBy` is taken from auth context,
 * not the request body, so the body's `gradedBy` is omitted.
 *
 * `rubricScores` — rubric-graded assessments only: JSON string of
 * `{"<criterionId>": {"points": number, "comment": string}}`. The server
 * validates per-criterion [0..Points] and Σ points == score.
 */
export async function gradeSubmission(input: {
  submissionId: string;
  score: number;
  feedback: string;
  rubricScores?: string;
}): Promise<ActionResult<{ submissionId: string }>> {
  try {
    const client = getApiClient();
    const assessments = new GeneratedApi.LearningAssessmentsModule(client);
    const result = await assessments.postAssessmentsSubmissionsGrade(
      input.submissionId,
      {
        score: pointsToScoreUnits(input.score),
        feedback: input.feedback,
        rubricScores: input.rubricScores == null ? null : rubricScoresToUnits(input.rubricScores),
      },
    );
    if (!result.ok) {
      return {
        success: false,
        error: result.error?.message ?? 'Failed to post grade.',
      };
    }
    return {
      success: true,
      data: { submissionId: input.submissionId },
    };
  } catch (error) {
    return {
      success: false,
      error: error instanceof Error ? error.message : 'Invalid grade.',
    };
  }
}
