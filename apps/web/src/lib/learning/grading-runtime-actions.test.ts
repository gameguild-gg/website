import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { AssessmentResponseEnvelopeV1 } from '@game-guild/grading';

const mocks = vi.hoisted(() => ({ request: vi.fn(), client: vi.fn(), token: vi.fn() }));
vi.mock('@/auth', () => ({ getToken: mocks.token }));
vi.mock('@game-guild/client', () => ({ createServerClient: mocks.client }));
import { startIndividualRuntimeSubmission, submitRuntimeSubmission, submitAssessmentTestRun } from './grading-runtime-actions';

describe('official grading runtime actions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.client.mockReturnValue({ request: mocks.request });
    mocks.request.mockResolvedValue({ ok: true, data: { submissionId: 'official' } });
  });

  it('starts the assessment with its enrollment and stable idempotency key', async () => {
    await startIndividualRuntimeSubmission('assessment/id', 'enrollment', 'start-key');
    expect(mocks.request).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({
      method: 'POST', path: '/v1.0/assessments/assessment%2Fid/runtime-submissions/individual',
      requiresAuth: true, body: { enrollmentId: 'enrollment', idempotencyKey: 'start-key' },
    }));
  });

  it.each([submitRuntimeSubmission, submitAssessmentTestRun])('allows the bounded trusted Code worker to return its receipt', async (submit) => {
    const response: AssessmentResponseEnvelopeV1 = {
      schemaVersion: 1, contentType: 'coding-assignment', payloadSchema: 'code-files/v1', payload: { files: {} },
    };
    await submit('attempt', response, 'submit-key');
    expect(mocks.request).toHaveBeenCalledExactlyOnceWith(expect.objectContaining({
      timeout: 330_000, requiresAuth: true, body: { response, idempotencyKey: 'submit-key', expectedDraftVersion: null },
    }));
  });

  it('retains the regular transport deadline for other assessment types', async () => {
    await submitRuntimeSubmission('attempt', {
      schemaVersion: 1, contentType: 'quiz', payloadSchema: 'quiz-answer/v1', payload: {},
    }, 'submit-key');
    expect(mocks.request.mock.calls[0][0]).not.toHaveProperty('timeout');
  });
});
