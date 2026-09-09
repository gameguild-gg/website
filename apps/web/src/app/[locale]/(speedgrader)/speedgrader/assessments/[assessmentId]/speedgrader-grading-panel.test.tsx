import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { LearningAssessmentsGradingQueueAssessment, LearningAssessmentsGradingQueueItem } from '@game-guild/client';
import { GradingPanel } from './grading-panel';
import { gradeSubmission } from '@/lib/learning/grade-action';

type GradingQueueAssessmentFixture = LearningAssessmentsGradingQueueAssessment & {
  reviewMethods: number;
};

const actionsMock = vi.hoisted(() => ({
  fetchPeerReviews: vi.fn(),
}));

const routerMocks = vi.hoisted(() => ({
  refresh: vi.fn(),
}));

vi.mock('./speedgrader-actions', () => ({
  fetchPeerReviewsAction: actionsMock.fetchPeerReviews,
}));

vi.mock('@/i18n/navigation', () => ({
  useRouter: () => routerMocks,
}));

vi.mock('@/lib/learning/grade-action', () => ({
  gradeSubmission: vi.fn(),
}));

// --- Fixtures ---------------------------------------------------------------

const rubricAssessment = {
  id: 'assessment-1',
  title: 'Final Project',
  maxScore: 10_000,
  hasRubric: true,
  reviewMethods: 8,
  rubric: {
    id: 'rubric-1',
    title: 'Project rubric',
    criteria: [
      { id: 'c1', description: 'Correctness', points: 6_000, order: 0 },
      { id: 'c2', description: 'Style', points: 4_000, order: 1 },
    ],
  },
} satisfies GradingQueueAssessmentFixture;

const plainAssessment = {
  id: 'assessment-1',
  title: 'Essay',
  maxScore: 10_000,
  hasRubric: false,
  reviewMethods: 8,
} satisfies GradingQueueAssessmentFixture;

const peerAssessment = {
  ...plainAssessment,
  reviewMethods: 9,
  peerReviewsRequiredCount: 3,
} satisfies GradingQueueAssessmentFixture;

const individualItem = {
  submissionId: 'sub-1',
  canonicalSubmissionId: 'sub-1',
  displayName: 'Ada Lovelace',
  attemptNumber: 1,
  status: 'Submitted',
  submittedAt: '2026-08-01T10:00:00Z',
  isLate: false,
  isGroup: false,
} satisfies LearningAssessmentsGradingQueueItem;

const groupItem = {
  ...individualItem,
  isGroup: true,
  groupId: 'group-1',
  groupName: 'Team Rocket',
  memberNames: ['Ada Lovelace', 'Grace Hopper'],
} satisfies LearningAssessmentsGradingQueueItem;

function renderPanel(props: Partial<React.ComponentProps<typeof GradingPanel>> = {}) {
  return render(<GradingPanel item={individualItem} assessment={rubricAssessment} {...props} />);
}

async function setCriterionPoints(user: ReturnType<typeof userEvent.setup>, criterionId: string, value: string) {
  await user.clear(screen.getByTestId(`criterion-points-${criterionId}`));
  await user.type(screen.getByTestId(`criterion-points-${criterionId}`), value);
}

// --- Tests ------------------------------------------------------------------

describe('GradingPanel — rubric mode', () => {
  beforeEach(() => {
    vi.mocked(gradeSubmission).mockReset();
    actionsMock.fetchPeerReviews.mockReset();
    actionsMock.fetchPeerReviews.mockResolvedValue({ ok: true, reviews: [] });
    routerMocks.refresh.mockReset();
  });

  it('renders a row per criterion with its cap and a read-only derived score', () => {
    renderPanel();

    expect(screen.getByTestId('criterion-points-c1')).toBeInTheDocument();
    expect(screen.getByTestId('criterion-points-c2')).toBeInTheDocument();
    expect(screen.getByText('Correctness')).toBeInTheDocument();
    expect(screen.getByText(/\/ 60/)).toBeInTheDocument();
    // No manual score input in rubric mode — the score is auto-derived from Σ.
    expect(screen.queryByTestId('plain-score-input')).not.toBeInTheDocument();
  });

  it('auto-derives the score from Σ of criterion points', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel();

    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '25');

    expect(screen.getByTestId('rubric-total')).toHaveTextContent('85');
    expect(screen.getByTestId('derived-score')).toHaveTextContent('85');
    expect(screen.getByTestId('submit-grade')).not.toBeDisabled();

    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalledTimes(1));
    const call = vi.mocked(gradeSubmission).mock.calls[0][0];
    expect(call.submissionId).toBe('sub-1');
    expect(call.score).toBe(85);
    const rubricScores = JSON.parse(call.rubricScores ?? '{}');
    expect(rubricScores).toEqual({
      c1: { points: 60, comment: '' },
      c2: { points: 25, comment: '' },
    });
    expect(routerMocks.refresh).toHaveBeenCalled();
  });

  it('includes criterion comments in the rubricScores payload', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel();

    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '40');
    await user.type(screen.getByTestId('criterion-comment-c1'), 'nice work');

    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalled());
    const rubricScores = JSON.parse(vi.mocked(gradeSubmission).mock.calls[0][0].rubricScores ?? '{}');
    expect(rubricScores.c1).toEqual({ points: 60, comment: 'nice work' });
  });

  it('blocks submit and shows an error when a criterion exceeds its cap', async () => {
    const user = userEvent.setup();
    renderPanel();

    await setCriterionPoints(user, 'c1', '61');
    await setCriterionPoints(user, 'c2', '40');

    expect(screen.getByTestId('criterion-error-c1')).toHaveTextContent(/0 to 60/i);
    expect(screen.getByTestId('submit-grade')).toBeDisabled();
    expect(gradeSubmission).not.toHaveBeenCalled();
  });

  it('blocks submit when a criterion is empty (incomplete, not red)', async () => {
    const user = userEvent.setup();
    renderPanel();

    await setCriterionPoints(user, 'c1', '60');
    // c2 left empty.
    expect(screen.queryByTestId('criterion-error-c2')).not.toBeInTheDocument();
    expect(screen.getByTestId('submit-grade')).toBeDisabled();
  });

  it('partial credit Σ < maxScore submits fine (no Σ==max gating)', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel();

    await setCriterionPoints(user, 'c1', '10');
    await setCriterionPoints(user, 'c2', '5');

    // Σ=15 of 100 — submit must stay enabled.
    expect(screen.getByTestId('submit-grade')).not.toBeDisabled();
    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalledWith(expect.objectContaining({ score: 15 })));
  });

  it('shows the overall comment textarea and sends composed feedback', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel();

    await setCriterionPoints(user, 'c1', '60');
    await setCriterionPoints(user, 'c2', '40');
    await user.type(screen.getByTestId('overall-comment'), 'Solid work.');

    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalled());
    expect(vi.mocked(gradeSubmission).mock.calls[0][0].feedback).toContain('Solid work.');
  });
});

describe('GradingPanel — plain score mode', () => {
  beforeEach(() => {
    vi.mocked(gradeSubmission).mockReset();
    actionsMock.fetchPeerReviews.mockReset();
    actionsMock.fetchPeerReviews.mockResolvedValue({ ok: true, reviews: [] });
  });

  it('submits the plain score without rubricScores', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel({ assessment: plainAssessment });

    await user.type(screen.getByTestId('plain-score-input'), '90');
    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalledTimes(1));
    const call = vi.mocked(gradeSubmission).mock.calls[0][0];
    expect(call.score).toBe(90);
    expect(call.rubricScores).toBeUndefined();
  });

  it('accepts scores with two decimal places', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    renderPanel({ assessment: plainAssessment });

    await user.type(screen.getByTestId('plain-score-input'), '0.5');
    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalledWith(expect.objectContaining({ score: 0.5 })));
  });

  it('seeds the score input from a computed (run-tests) score', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: true,
      data: { submissionId: 'sub-1' },
    });
    const { rerender } = renderPanel({
      assessment: plainAssessment,
      computedScore: null,
    });

    rerender(<GradingPanel item={individualItem} assessment={plainAssessment} computedScore={{ score: 67, autoFeedback: 'Score: 67/100' }} />);

    expect(screen.getByTestId('plain-score-input')).toHaveValue(67);
    await user.click(screen.getByTestId('submit-grade'));

    await waitFor(() => expect(gradeSubmission).toHaveBeenCalledWith(expect.objectContaining({ score: 67 })));
  });

  it('rejects out-of-range plain scores', async () => {
    const user = userEvent.setup();
    renderPanel({ assessment: plainAssessment });

    await user.type(screen.getByTestId('plain-score-input'), '150');

    expect(screen.getByTestId('plain-score-error')).toBeInTheDocument();
    expect(screen.getByTestId('submit-grade')).toBeDisabled();
  });
});

describe('GradingPanel — group + meta + peers', () => {
  beforeEach(() => {
    vi.mocked(gradeSubmission).mockReset();
    actionsMock.fetchPeerReviews.mockReset();
    actionsMock.fetchPeerReviews.mockResolvedValue({ ok: true, reviews: [] });
  });

  it('shows a group banner with the member count and chips', () => {
    renderPanel({ item: groupItem });

    expect(screen.getByTestId('group-banner')).toHaveTextContent('Grade applies to 2 members');
    expect(screen.getByTestId('group-members')).toHaveTextContent('Ada Lovelace');
    expect(screen.getByTestId('group-members')).toHaveTextContent('Grace Hopper');
  });

  it('shows attempt meta with isLate badge when late', () => {
    renderPanel({ item: { ...individualItem, isLate: true } });

    expect(screen.getByTestId('attempt-meta')).toHaveTextContent('attempt 1');
    expect(screen.getByTestId('late-badge')).toBeInTheDocument();
  });

  it('lists named peer reviews when the PeerReview flag is on', async () => {
    actionsMock.fetchPeerReviews.mockResolvedValue({
      ok: true,
      reviews: [
        {
          reviewId: 'rev-1',
          reviewerName: 'Grace Hopper',
          reviewerUserId: 'user-2',
          score: 8_000,
          feedback: 'Clear structure.',
          submittedAt: '2026-08-02T10:00:00Z',
        },
      ],
    });

    renderPanel({ assessment: peerAssessment });

    await waitFor(() => expect(screen.getByTestId('peer-review-rev-1')).toBeInTheDocument());
    expect(screen.getByTestId('peer-review-rev-1')).toHaveTextContent('Grace Hopper');
    expect(screen.getByTestId('peer-review-rev-1')).toHaveTextContent('80');
    expect(screen.getByTestId('peer-review-rev-1')).toHaveTextContent('Clear structure.');
  });

  it('does not fetch peer reviews without the PeerReview flag', async () => {
    renderPanel({ assessment: plainAssessment });

    await waitFor(() => expect(screen.getByTestId('grading-panel')).toBeInTheDocument());
    expect(actionsMock.fetchPeerReviews).not.toHaveBeenCalled();
    expect(screen.queryByTestId('peer-reviews')).not.toBeInTheDocument();
  });

  it('shows assignment score badge when assignmentScore is set', () => {
    renderPanel({ item: { ...individualItem, assignmentScore: 7_500 } });

    expect(screen.getByTestId('assignment-score-badge')).toHaveTextContent('Assignment: 75/100');
  });

  it('shows passed badge when assignmentPassed is true', () => {
    renderPanel({ item: { ...individualItem, assignmentPassed: true } });

    expect(screen.getByTestId('assignment-passed-badge')).toHaveTextContent('Passed');
  });

  it('shows not-passed badge when assignmentPassed is false', () => {
    renderPanel({ item: { ...individualItem, assignmentPassed: false } });

    expect(screen.getByTestId('assignment-passed-badge')).toHaveTextContent('Not passed');
  });

  it('omits assignment badges when fields are null', () => {
    renderPanel({ item: { ...individualItem, assignmentScore: null, assignmentPassed: null } });

    expect(screen.queryByTestId('assignment-score-badge')).not.toBeInTheDocument();
    expect(screen.queryByTestId('assignment-passed-badge')).not.toBeInTheDocument();
  });

  it('renders an alert when the action fails', async () => {
    const user = userEvent.setup();
    vi.mocked(gradeSubmission).mockResolvedValue({
      success: false,
      error: 'Rubric scores must sum to the submitted score',
    });
    renderPanel({ assessment: plainAssessment });

    await user.type(screen.getByTestId('plain-score-input'), '50');
    await user.click(screen.getByTestId('submit-grade'));

    const alert = await screen.findByRole('alert');
    expect(alert).toHaveTextContent('Rubric scores must sum to the submitted score');
    // Panel stays usable.
    expect(screen.getByTestId('submit-grade')).not.toBeDisabled();
  });
});
