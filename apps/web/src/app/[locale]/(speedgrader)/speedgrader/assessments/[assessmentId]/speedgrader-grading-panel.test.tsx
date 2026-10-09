import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type {
  LearningAssessmentsGradingQueueAssessment,
  LearningAssessmentsGradingQueueItem,
} from '@game-guild/client';
import type { AssessmentSubmissionRuntimeViewV1 } from '@game-guild/grading';

const actions = vi.hoisted(() => ({
  get: vi.fn(),
  resolve: vi.fn(),
  regrade: vi.fn(),
  release: vi.fn(),
}));
const router = vi.hoisted(() => ({ refresh: vi.fn() }));

vi.mock('@/lib/learning/grading-runtime-actions', () => ({
  getRuntimeSubmission: actions.get,
  resolveRuntimeInstructorReview: actions.resolve,
  regradeRuntimeSubmission: actions.regrade,
  releaseRuntimeSubmission: actions.release,
}));
vi.mock('@/i18n/navigation', () => ({ useRouter: () => router }));

import { GradingPanel } from './grading-panel';

const assessment = {
  id: 'assessment-1',
  title: 'Quiz',
  maxScore: 300,
} satisfies LearningAssessmentsGradingQueueAssessment;

const individualItem = {
  submissionId: 'submission-1',
  canonicalSubmissionId: 'submission-1',
  displayName: 'Ada Lovelace',
  attemptNumber: 1,
  attemptCount: 1,
  status: 'Submitted',
  submittedAt: '2026-08-01T10:00:00Z',
  isLate: false,
  isGroup: false,
} satisfies LearningAssessmentsGradingQueueItem;

const groupItem = {
  ...individualItem,
  isGroup: true,
  groupId: 'group-1',
  groupName: 'Team One',
  memberNames: ['Ada Lovelace', 'Grace Hopper'],
} satisfies LearningAssessmentsGradingQueueItem;

function runtimeSubmission(
  overrides: Partial<AssessmentSubmissionRuntimeViewV1> = {},
): AssessmentSubmissionRuntimeViewV1 {
  return {
    submissionId: 'submission-1',
    assessmentId: 'assessment-1',
    definitionRevisionId: 'revision-1',
    enrollmentId: 'enrollment-1',
    courseGroupId: null,
    attemptNumber: 1,
    status: 'submitted',
    draftVersion: 0,
    version: 4,
    startedAt: '2026-08-01T09:00:00Z',
    submittedAt: '2026-08-01T10:00:00Z',
    submittedByUserId: 'user-1',
    contentCompleted: false,
    execution: {
      executionId: 'execution-1',
      definitionRevisionId: 'revision-1',
      context: 'official-submission',
      executionSnapshotHash: 'a'.repeat(64),
      deliveryHash: 'b'.repeat(64),
      delivery: {
        schemaVersion: 1,
        definitionRevisionId: 'revision-1',
        executionSnapshotHash: 'a'.repeat(64),
        itemOrder: ['q1', 'q2'],
        items: {
          q1: {
            adapterKey: 'quiz-assessment-type',
            adapterVersion: '1',
            learnerPayload: { itemId: 'q1', entry: { stem: 'First question' } },
          },
          q2: {
            adapterKey: 'quiz-assessment-type',
            adapterVersion: '1',
            learnerPayload: { itemId: 'q2', entry: { stem: 'Second question' } },
          },
        },
      },
      itemMaxScores: { q1: 100, q2: 200 },
      submittedResponse: null,
      status: 'awaitingReview',
      activeRoundId: 'round-1',
      instructorVisibleResult: {
        schemaVersion: 1,
        state: 'partial',
        score: null,
        maxScore: 300,
        evidenceRefs: [],
        feedback: 'Automated pass',
        items: [
          {
            itemId: 'q1',
            state: 'graded',
            score: 100,
            maxScore: 100,
            evidenceRefs: [],
            reviewMethod: 'AutomatedReview',
            handlerKey: 'quiz-automated-review',
            handlerVersion: '1',
          },
          {
            itemId: 'q2',
            state: 'pending',
            score: null,
            maxScore: 200,
            evidenceRefs: [],
            reviewMethod: 'AutomatedReview',
            handlerKey: 'quiz-automated-review',
            handlerVersion: '1',
          },
        ],
      },
      learnerVisibleResult: null,
      requiresInstructorReview: true,
      released: false,
      history: [],
    },
    ...overrides,
  };
}

function finalSubmission(): AssessmentSubmissionRuntimeViewV1 {
  const value = runtimeSubmission();
  return {
    ...value,
    status: 'graded',
    execution: {
      ...value.execution,
      status: 'completed',
      requiresInstructorReview: false,
      instructorVisibleResult: {
        schemaVersion: 1,
        state: 'final',
        score: 250,
        maxScore: 300,
        evidenceRefs: [],
        feedback: 'Done',
        items: [
          {
            itemId: 'q1',
            state: 'graded',
            score: 100,
            maxScore: 100,
            evidenceRefs: [],
            reviewMethod: 'InstructorReview',
            handlerKey: 'instructor-review',
            handlerVersion: '1',
          },
          {
            itemId: 'q2',
            state: 'graded',
            score: 150,
            maxScore: 200,
            evidenceRefs: [],
            reviewMethod: 'InstructorReview',
            handlerKey: 'instructor-review',
            handlerVersion: '1',
          },
        ],
      },
      history: [
        {
          roundId: 'round-1',
          roundNumber: 1,
          reason: 'initial',
          reasonDetail: null,
          initiatedByActorId: null,
          status: 'finalized',
          startedAt: '2026-08-01T10:00:01Z',
          finalizedAt: '2026-08-01T10:10:00Z',
          result: null,
          released: false,
          releasedAt: null,
        },
      ],
    },
  };
}

describe('runtime GradingPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    actions.get.mockResolvedValue({ success: true, data: runtimeSubmission() });
  });

  it('prefills deterministic evidence and sends one resolution per manifest item', async () => {
    const user = userEvent.setup();
    actions.resolve.mockResolvedValue({ success: true, data: finalSubmission() });
    render(<GradingPanel item={individualItem} assessment={assessment} />);

    expect(await screen.findByText('First question')).toBeInTheDocument();
    await waitFor(() =>
      expect(screen.getByTestId('item-score-q1')).toHaveValue(1),
    );
    await user.type(screen.getByTestId('item-score-q2'), '1.5');
    await user.clear(screen.getByLabelText('Overall feedback'));
    await user.type(screen.getByLabelText('Overall feedback'), 'Reviewed');
    await user.click(screen.getByTestId('resolve-instructor-review'));

    await waitFor(() => expect(actions.resolve).toHaveBeenCalledTimes(1));
    expect(actions.resolve.mock.calls[0][1]).toMatchObject({
      schemaVersion: 1,
      feedback: 'Reviewed',
      items: [
        { itemId: 'q1', score: 100 },
        { itemId: 'q2', score: 150 },
      ],
    });
    expect(router.refresh).toHaveBeenCalled();
  });

  it('does not allow a score above the immutable item maximum', async () => {
    const user = userEvent.setup();
    render(<GradingPanel item={individualItem} assessment={assessment} />);

    const input = await screen.findByTestId('item-score-q2');
    await user.type(input, '3');
    expect(screen.getByTestId('resolve-instructor-review')).toBeDisabled();
    expect(actions.resolve).not.toHaveBeenCalled();
  });

  it('releases the active round with the current submission version', async () => {
    const user = userEvent.setup();
    actions.get
      .mockResolvedValueOnce({ success: true, data: finalSubmission() })
      .mockResolvedValueOnce({
        success: true,
        data: {
          ...finalSubmission(),
          execution: { ...finalSubmission().execution, released: true },
        },
      });
    actions.release.mockResolvedValue({
      success: true,
      data: { releaseId: 'release-1', gradeRoundId: 'round-1' },
    });
    render(<GradingPanel item={individualItem} assessment={assessment} />);

    await user.click(await screen.findByTestId('release-result'));
    await waitFor(() => expect(actions.release).toHaveBeenCalledTimes(1));
    expect(actions.release.mock.calls[0][0]).toEqual({
      submissionId: 'submission-1',
      version: 4,
      expectedRoundId: 'round-1',
    });
  });

  it('shows one collective result for the frozen participant snapshot', async () => {
    render(<GradingPanel item={groupItem} assessment={assessment} />);

    expect(await screen.findByTestId('group-banner')).toHaveTextContent(
      'One result applies to 2 frozen participants',
    );
    expect(screen.getByTestId('group-members')).toHaveTextContent(
      'Ada Lovelace',
    );
    expect(screen.getByTestId('group-members')).toHaveTextContent(
      'Grace Hopper',
    );
  });

  it('opens a new round only with an explicit regrade reason', async () => {
    const user = userEvent.setup();
    actions.get.mockResolvedValue({ success: true, data: finalSubmission() });
    actions.regrade.mockResolvedValue({
      success: true,
      data: runtimeSubmission(),
    });
    render(<GradingPanel item={individualItem} assessment={assessment} />);

    const button = await screen.findByRole('button', { name: 'Start regrade' });
    expect(button).toBeDisabled();
    await user.type(screen.getByLabelText('Regrade'), 'Corrected answer key');
    await user.click(button);

    await waitFor(() => expect(actions.regrade).toHaveBeenCalledTimes(1));
    expect(actions.regrade.mock.calls[0][1]).toBe('Corrected answer key');
  });

  it('resolves the frozen Code rubric with bounded criterion scores and their exact total', async () => {
    const user = userEvent.setup();
    const value = runtimeSubmission();
    value.execution.delivery.itemOrder = ['code'];
    value.execution.delivery.items = { code: { adapterKey: 'code-assessment-type', adapterVersion: '1', learnerPayload: {} } };
    value.execution.itemMaxScores = { code: 10000 };
    value.execution.instructorVisibleResult = null;
    value.execution.instructorVisibleRubric = { id: 'frozen-rubric', title: 'Frozen rubric', criteria: [
      { id: 'correctness', description: 'Correctness', points: 6000, order: 0 },
      { id: 'quality', description: 'Quality', points: 4000, order: 1 },
    ] };
    actions.get.mockResolvedValue({ success: true, data: value });
    actions.resolve.mockResolvedValue({ success: true, data: value });
    render(<GradingPanel item={individualItem} assessment={{ ...assessment, maxScore: 10000 }} />);

    const submit = await screen.findByTestId('submit-grade');
    expect(submit).toBeDisabled();
    expect(screen.getByTestId('item-score-code')).toBeDisabled();
    await user.type(screen.getByTestId('criterion-points-correctness'), '61');
    await user.type(screen.getByTestId('criterion-points-quality'), '35');
    expect(submit).toBeDisabled();
    await user.clear(screen.getByTestId('criterion-points-correctness'));
    await user.type(screen.getByTestId('criterion-points-correctness'), '55');
    expect(screen.getByTestId('rubric-total')).toHaveTextContent('Total: 90');
    await user.click(submit);
    await waitFor(() => expect(actions.resolve).toHaveBeenCalledOnce());
    expect(actions.resolve.mock.calls[0][1]).toEqual(expect.objectContaining({
      items: [{ itemId: 'code', score: 9000, feedback: null }],
      rubricScores: { correctness: 5500, quality: 3500 },
    }));
  });

  it('removes the previous submission scores while the next submission loads', async () => {
    const first = runtimeSubmission();
    actions.get.mockResolvedValueOnce({ success: true, data: first });
    const view = render(<GradingPanel item={individualItem} assessment={assessment} />);
    await screen.findByTestId('item-score-q1');
    const pending = Promise.withResolvers<{ success: true; data: AssessmentSubmissionRuntimeViewV1 }>();
    actions.get.mockReturnValueOnce(pending.promise);

    view.rerender(<GradingPanel item={{ ...individualItem, submissionId: 'submission-2' }} assessment={assessment} />);
    expect(screen.queryByTestId('item-score-q1')).not.toBeInTheDocument();
    await waitFor(() => expect(actions.get).toHaveBeenLastCalledWith('submission-2'));
    pending.resolve({ success: true, data: runtimeSubmission({ submissionId: 'submission-2' }) });
    await screen.findByTestId('item-score-q1');
    expect(actions.resolve).not.toHaveBeenCalled();
  });

  it('restores the criterion scores recorded for the active finalized Code round', async () => {
    const value = finalSubmission();
    value.execution.delivery.itemOrder = ['code'];
    value.execution.delivery.items = { code: { adapterKey: 'code-assessment-type', adapterVersion: '1', learnerPayload: {} } };
    value.execution.itemMaxScores = { code: 10000 };
    value.execution.instructorVisibleRubric = { id: 'frozen-rubric', title: 'Frozen rubric', criteria: [
      { id: 'correctness', description: 'Correctness', points: 6000, order: 0 },
      { id: 'quality', description: 'Quality', points: 4000, order: 1 },
    ] };
    value.execution.instructorVisibleRubricScores = { correctness: 5500, quality: 3500 };
    actions.get.mockResolvedValue({ success: true, data: value });
    render(<GradingPanel item={individualItem} assessment={{ ...assessment, maxScore: 10000 }} />);
    await waitFor(() => expect(screen.getByTestId('criterion-points-correctness')).toHaveValue(55));
    expect(screen.getByTestId('criterion-points-quality')).toHaveValue(35);
    expect(screen.getByTestId('rubric-total')).toHaveTextContent('Total: 90');
    expect(screen.getByTestId('item-score-code')).toHaveValue(90);
    expect(actions.resolve).not.toHaveBeenCalled();
  });
});
