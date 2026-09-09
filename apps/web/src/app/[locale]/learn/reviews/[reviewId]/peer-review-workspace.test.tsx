import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { LearningAssessmentsAnonymousReviewSubmission } from '@game-guild/client';
import { ReviewWorkspace } from './review-workspace';
import { fetchPeerReviewWorkspace, submitPeerReview } from '@/lib/learning/actions-peer-review';

const actionsMock = vi.hoisted(() => ({
  fetchWorkspace: vi.fn(),
  submit: vi.fn(),
}));

const routerMocks = vi.hoisted(() => ({
  push: vi.fn(),
  refresh: vi.fn(),
}));

vi.mock('@/lib/learning/actions-peer-review', () => ({
  fetchPeerReviewWorkspace: actionsMock.fetchWorkspace,
  submitPeerReview: actionsMock.submit,
}));

vi.mock('@/i18n/navigation', () => ({
  useRouter: () => routerMocks,
}));

// --- Fixtures ---------------------------------------------------------------

const rubricReview = {
  reviewId: 'review-1',
  status: 'Assigned',
  attemptNumber: 2,
  submittedAt: '2026-08-01T10:00:00Z',
  submissionStatus: 'Submitted',
  assessment: { id: 'assessment-1', title: 'Final Project', maxScore: 10_000 },
  rubric: {
    criteria: [
      { id: 'c1', description: 'Correctness', points: 6_000, order: 0 },
      { id: 'c2', description: 'Style', points: 4_000, order: 1 },
    ],
  },
  textPayload: 'My project reflection essay.',
} satisfies LearningAssessmentsAnonymousReviewSubmission;

const plainReview = {
  ...rubricReview,
  reviewId: 'review-plain',
  rubric: undefined,
  assessment: { id: 'assessment-2', title: 'Essay', maxScore: 5_000 },
} satisfies LearningAssessmentsAnonymousReviewSubmission;

const submittedReview = {
  ...rubricReview,
  status: 'Submitted',
} satisfies LearningAssessmentsAnonymousReviewSubmission;

function renderWorkspace(review: LearningAssessmentsAnonymousReviewSubmission) {
  actionsMock.fetchWorkspace.mockResolvedValue({ ok: true, review });
  return render(<ReviewWorkspace reviewId={review.reviewId ?? 'review-1'} />);
}

async function setCriterionPoints(user: ReturnType<typeof userEvent.setup>, criterionId: string, value: string) {
  const input = screen.getByTestId(`criterion-points-${criterionId}`);
  await user.clear(input);
  await user.type(input, value);
}

// --- Tests ------------------------------------------------------------------

describe('ReviewWorkspace', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    routerMocks.push.mockReset();
  });

  it('renders an anonymized header with no name fields anywhere', async () => {
    renderWorkspace(rubricReview);

    expect(await screen.findByText('Anonymous submission · attempt 2 · Final Project')).toBeInTheDocument();
    // Payload renders through the reused viewer.
    expect(await screen.findByTestId('text-viewer')).toHaveTextContent('My project reflection essay.');
    // The anonymity UI boundary: no reviewee/reviewer name-shaped strings render.
    expect(screen.queryByText(/ada lovelace/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/grace hopper/i)).not.toBeInTheDocument();
    expect(screen.queryByTestId('reviewee-name')).not.toBeInTheDocument();
    expect(screen.queryByTestId('reviewer-name')).not.toBeInTheDocument();
  });

  it('submits a rubric review with the derived Σ score and rubricScores payload (partial credit)', async () => {
    const user = userEvent.setup();
    actionsMock.submit.mockResolvedValue({ success: true, data: null });
    renderWorkspace(rubricReview);

    await screen.findByTestId('criterion-points-c1');
    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '25');
    await user.type(screen.getByTestId('peer-feedback'), 'Solid work overall.');

    expect(screen.getByTestId('rubric-total')).toHaveTextContent('85');

    await user.click(screen.getByTestId('submit-review'));

    await waitFor(() => expect(actionsMock.submit).toHaveBeenCalledTimes(1));
    expect(actionsMock.submit).toHaveBeenCalledWith('review-1', {
      score: 85,
      feedback: 'Solid work overall.',
      rubricScores: JSON.stringify({
        c1: { points: 60, comment: '' },
        c2: { points: 25, comment: '' },
      }),
    });
    await waitFor(() => expect(routerMocks.push).toHaveBeenCalledWith('/learn/reviews'));
  });

  it('blocks submit with an error when feedback is empty', async () => {
    const user = userEvent.setup();
    renderWorkspace(rubricReview);

    await screen.findByTestId('criterion-points-c1');
    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '25');

    await user.click(screen.getByTestId('submit-review'));

    expect(await screen.findByText('Feedback comment is required')).toBeInTheDocument();
    expect(actionsMock.submit).not.toHaveBeenCalled();
    expect(routerMocks.push).not.toHaveBeenCalled();
  });

  it('blocks submit when a criterion exceeds its cap', async () => {
    const user = userEvent.setup();
    renderWorkspace(rubricReview);

    await screen.findByTestId('criterion-points-c1');
    await setCriterionPoints(user, 'c1', '70');

    expect(screen.getByTestId('criterion-error-c1')).toHaveTextContent('Enter 0 to 60');
    expect(screen.getByTestId('submit-review')).toBeDisabled();
    expect(actionsMock.submit).not.toHaveBeenCalled();
  });

  it('submits plain mode with score + feedback and no rubricScores', async () => {
    const user = userEvent.setup();
    actionsMock.submit.mockResolvedValue({ success: true, data: null });
    renderWorkspace(plainReview);

    const scoreInput = await screen.findByTestId('peer-score-input');
    await user.type(scoreInput, '42');
    await user.type(screen.getByTestId('peer-feedback'), 'Nice essay.');

    await user.click(screen.getByTestId('submit-review'));

    await waitFor(() => expect(actionsMock.submit).toHaveBeenCalledWith('review-plain', { score: 42, feedback: 'Nice essay.' }));
    expect(screen.queryByTestId('rubric-grid')).not.toBeInTheDocument();
    await waitFor(() => expect(routerMocks.push).toHaveBeenCalledWith('/learn/reviews'));
  });

  it('surfaces the server error as an alert on submit failure', async () => {
    const user = userEvent.setup();
    actionsMock.submit.mockResolvedValue({ success: false, error: 'Review window closed' });
    renderWorkspace(rubricReview);

    await screen.findByTestId('criterion-points-c1');
    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '25');
    await user.type(screen.getByTestId('peer-feedback'), 'Too late.');

    await user.click(screen.getByTestId('submit-review'));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Review window closed');
    expect(routerMocks.push).not.toHaveBeenCalled();
  });

  it('renders a persisted submitted review without submit controls', async () => {
    renderWorkspace(submittedReview);

    expect(await screen.findByText('Review already submitted.')).toBeInTheDocument();
    expect(screen.queryByTestId('submit-review')).not.toBeInTheDocument();
  });

  it('renders an error state when the workspace cannot be loaded', async () => {
    actionsMock.fetchWorkspace.mockResolvedValue({ ok: false, error: 'Failed to load the review.' });
    render(<ReviewWorkspace reviewId="review-x" />);

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Failed to load the review.');
  });
});
