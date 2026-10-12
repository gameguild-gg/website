import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AssessmentResponseEnvelopeV1 } from '@game-guild/grading';

const mocks = vi.hoisted(() => ({
  request: vi.fn(),
  client: vi.fn(),
  token: vi.fn(),
}));
vi.mock('@/auth', () => ({ getToken: mocks.token }));
vi.mock('@game-guild/client', () => ({ createServerClient: mocks.client }));
import {
  startIndividualRuntimeSubmission,
  submitRuntimeSubmission,
  submitAssessmentTestRun,
} from './grading-runtime-actions';

const wireExecution = {
  executionId: 'execution',
  definitionRevisionId: 'revision',
  context: 'official-submission',
  executionSnapshotHash: 'snapshot',
  deliveryHash: 'delivery',
  delivery: {
    schemaVersion: 1,
    definitionRevisionId: 'revision',
    executionSnapshotHash: 'snapshot',
    itemOrder: [],
    items: {},
  },
  itemMaxScores: {},
  submittedResponse: null,
  status: 'AwaitingReview',
  activeRoundId: 'round',
  instructorVisibleResult: null,
  learnerVisibleResult: null,
  requiresInstructorReview: true,
  released: false,
  history: [
    {
      roundId: 'round',
      roundNumber: 1,
      reason: 'InitialSubmission',
      reasonDetail: null,
      initiatedByActorId: null,
      status: 'AwaitingInstructorResolution',
      startedAt: '2026-10-09T00:00:00Z',
      finalizedAt: null,
      result: null,
      released: false,
      releasedAt: null,
    },
  ],
};

const wireSubmission = {
  submissionId: 'official',
  assessmentId: 'assessment',
  definitionRevisionId: 'revision',
  enrollmentId: 'enrollment',
  courseGroupId: null,
  attemptNumber: 1,
  status: 'InProgress',
  draftVersion: 0,
  version: 1,
  startedAt: '2026-10-09T00:00:00Z',
  submittedAt: null,
  submittedByUserId: null,
  contentCompleted: false,
  execution: wireExecution,
};

const wireTestRun = {
  testRunId: 'test-run',
  assessmentId: 'assessment',
  definitionRevisionId: 'revision',
  status: 'Running',
  personaKey: 'instructor-preview',
  personaDisplayName: 'Instructor preview',
  execution: { ...wireExecution, context: 'author-test' },
  candidateStillMatchesDraft: true,
  readyForPublication: false,
  diagnostics: [],
};

describe('official grading runtime actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.client.mockReturnValue({ request: mocks.request });
    mocks.request.mockImplementation(({ path }: { path: string }) =>
      Promise.resolve({
        ok: true,
        data: path.includes('/test-runs') ? wireTestRun : wireSubmission,
      }),
    );
  });

  it('starts the assessment with its enrollment and stable idempotency key', async () => {
    const result = await startIndividualRuntimeSubmission(
      'assessment/id',
      'enrollment',
      'start-key',
    );
    expect(mocks.request).toHaveBeenCalledExactlyOnceWith(
      expect.objectContaining({
        method: 'POST',
        path: '/v1.0/assessments/assessment%2Fid/runtime-submissions/individual',
        requiresAuth: true,
        body: { enrollmentId: 'enrollment', idempotencyKey: 'start-key' },
      }),
    );
    expect(result).toMatchObject({
      success: true,
      data: {
        status: 'inProgress',
        execution: {
          status: 'awaitingReview',
          history: [{ status: 'awaitingInstructorResolution' }],
        },
      },
    });
  });

  it.each([submitRuntimeSubmission, submitAssessmentTestRun])(
    'allows the bounded trusted Code worker to return its receipt',
    async (submit) => {
      const response: AssessmentResponseEnvelopeV1 = {
        schemaVersion: 1,
        contentType: 'coding-assignment',
        payloadSchema: 'code-files/v1',
        payload: { files: {} },
      };
      await submit('attempt', response, 'submit-key');
      expect(mocks.request).toHaveBeenCalledExactlyOnceWith(
        expect.objectContaining({
          timeout: 330_000,
          requiresAuth: true,
          body: {
            response,
            idempotencyKey: 'submit-key',
            expectedDraftVersion: null,
          },
        }),
      );
    },
  );

  it('retains the regular transport deadline for other assessment types', async () => {
    await submitRuntimeSubmission(
      'attempt',
      {
        schemaVersion: 1,
        contentType: 'quiz',
        payloadSchema: 'quiz-answer/v1',
        payload: {},
      },
      'submit-key',
    );
    expect(mocks.request.mock.calls[0][0]).not.toHaveProperty('timeout');
  });

  it('fails closed when the API returns an unknown runtime status', async () => {
    mocks.request.mockResolvedValue({
      ok: true,
      data: { ...wireSubmission, status: 'UnexpectedStatus' },
    });

    const result = await startIndividualRuntimeSubmission(
      'assessment',
      'enrollment',
      'start-key',
    );

    expect(result).toEqual({
      success: false,
      error:
        'Unsupported runtime submission status returned by the grading API: UnexpectedStatus.',
    });
  });
});
