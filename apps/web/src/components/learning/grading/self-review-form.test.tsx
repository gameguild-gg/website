import '@testing-library/jest-dom/vitest';

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import type { SelfReviewRuntimeStateV1 } from '@game-guild/grading';
import { SelfReviewForm } from './self-review-form';

const items = [{ itemId: 'question-1', label: 'Question one', maxScore: 200 }];

function state(
  overrides: Partial<SelfReviewRuntimeStateV1> = {},
): SelfReviewRuntimeStateV1 {
  return {
    reviewStageId: 'stage-1',
    status: 'awaiting-evidence',
    instructions: 'Review your response.',
    requireFeedback: true,
    draftVersion: 2,
    draft: {
      schemaVersion: 1,
      items: [{ itemId: 'question-1', score: 125, feedback: 'Draft feedback' }],
      feedback: 'Draft reflection',
    },
    lastDraftActorId: 'learner-1',
    submittedEvidence: null,
    submittedByActorId: null,
    ...overrides,
  };
}

describe('SelfReviewForm', () => {
  it('resumes the versioned draft and submits scores as integer units', async () => {
    const user = userEvent.setup();
    const onSubmit = vi.fn().mockResolvedValue(undefined);

    render(
      <SelfReviewForm
        items={items}
        state={state()}
        onSubmit={onSubmit}
      />,
    );

    const score = screen.getByLabelText('Score / 2');
    expect(score).toHaveValue(1.25);
    await user.clear(score);
    await user.type(score, '1.5');
    await user.click(screen.getByRole('button', { name: 'Submit self review' }));

    expect(onSubmit).toHaveBeenCalledWith({
      schemaVersion: 1,
      items: [{ itemId: 'question-1', score: 150, feedback: 'Draft feedback' }],
      feedback: 'Draft reflection',
    });
  });

  it('saves a collective draft through the same evidence contract', async () => {
    const user = userEvent.setup();
    const onSaveDraft = vi.fn().mockResolvedValue(undefined);

    render(
      <SelfReviewForm
        items={items}
        state={state()}
        onSaveDraft={onSaveDraft}
        onSubmit={vi.fn()}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Save draft' }));

    expect(onSaveDraft).toHaveBeenCalledWith(state().draft);
  });

  it('is read-only after final evidence has been submitted', () => {
    const submitted = state().draft!;

    render(
      <SelfReviewForm
        items={items}
        state={state({
          status: 'completed',
          draft: null,
          submittedEvidence: submitted,
          submittedByActorId: 'learner-1',
        })}
        onSubmit={vi.fn()}
      />,
    );

    expect(screen.getByText('Self review submitted.')).toBeInTheDocument();
    expect(screen.getByLabelText('Score / 2')).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Submit self review' })).not.toBeInTheDocument();
  });
});
