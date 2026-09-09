'use server';

import { getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type LearningAssessmentsAnonymousReviewSubmission,
  type LearningAssessmentsPeerReviewSubmitInput,
  type LearningAssessmentsReceivedPeerReview,
} from '@game-guild/client';
import { pointsToScoreUnits, rubricScoresToUnits } from '@/lib/learning/academic-values';

type ActionResult<T> = { success: true; data: T } | { success: false; error: string };

export type ReviewWorkspaceResult = { ok: true; review: LearningAssessmentsAnonymousReviewSubmission } | { ok: false; error: string };

export interface SubmitPeerReviewInput {
  score?: number;
  feedback: string;
  rubricScores?: string;
}

export type ReceivedPeerReviewsResult = { ok: true; reviews: LearningAssessmentsReceivedPeerReview[] } | { ok: false; error: string };

function getApiClient() {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

  return createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });
}

function extractApiError(err: unknown): string {
  const e = err as { status?: number; message?: string; detail?: string } | undefined;
  return e?.detail || e?.message || 'Request failed.';
}

/** Claim a peer review target (todo 7 endpoint): least-reviewed random pick. */
export async function claimPeerReview(assessmentId: string): Promise<ActionResult<{ reviewId: string }>> {
  try {
    const module = new GeneratedApi.LearningAssessmentsPeerReviewsModule(getApiClient());
    const result = await module.postAssessmentsPeerReviewsClaim(assessmentId);

    if (!result.ok) {
      return { success: false, error: extractApiError(result.error) };
    }
    const reviewId = result.data.reviewId;
    if (!reviewId) {
      return { success: false, error: 'Failed to claim a peer review.' };
    }
    return { success: true, data: { reviewId } };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

/** Anonymous review workspace payload (todo 8a endpoint) — carries no identity fields. */
export async function fetchPeerReviewWorkspace(reviewId: string): Promise<ReviewWorkspaceResult> {
  try {
    const module = new GeneratedApi.LearningAssessmentsPeerReviewsModule(getApiClient());
    const result = await module.getAssessmentsPeerReviews(reviewId);

    if (!result.ok) {
      return { ok: false, error: 'Failed to load the review.' };
    }
    return { ok: true, review: result.data };
  } catch {
    return { ok: false, error: 'Failed to load the review.' };
  }
}

/** Submit a peer review (todo 8b endpoint): feedback always required; rubric reviews add rubricScores + score. */
export async function submitPeerReview(reviewId: string, input: SubmitPeerReviewInput): Promise<ActionResult<null>> {
  const feedback = input.feedback.trim();
  if (!feedback) {
    return { success: false, error: 'Feedback comment is required' };
  }

  try {
    const body: LearningAssessmentsPeerReviewSubmitInput = {
      score: input.score == null ? null : pointsToScoreUnits(input.score),
      feedback,
      rubricScores: input.rubricScores == null ? null : rubricScoresToUnits(input.rubricScores),
    };
    const module = new GeneratedApi.LearningAssessmentsPeerReviewsModule(getApiClient());
    const result = await module.postAssessmentsPeerReviewsSubmit(reviewId, body);

    if (!result.ok) {
      return { success: false, error: extractApiError(result.error) };
    }
    return { success: true, data: null };
  } catch (e) {
    return {
      success: false,
      error: `Unexpected error: ${e instanceof Error ? e.message : String(e)}`,
    };
  }
}

/** Received (anonymized) peer reviews for one of the actor's own submissions (todo 8c endpoint). */
export async function fetchReceivedPeerReviews(submissionId: string): Promise<ReceivedPeerReviewsResult> {
  try {
    const module = new GeneratedApi.LearningAssessmentsPeerReviewsModule(getApiClient());
    const result = await module.getAssessmentsSubmissionsReceivedPeerReviews(submissionId);

    if (!result.ok) {
      return { ok: false, error: 'Failed to load received reviews.' };
    }
    return { ok: true, reviews: result.data ?? [] };
  } catch {
    return { ok: false, error: 'Failed to load received reviews.' };
  }
}
