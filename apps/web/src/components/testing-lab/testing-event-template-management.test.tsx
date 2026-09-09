import type { TestingLabTestingEventTemplateProjection } from '@game-guild/client';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { TestingEventTemplateManagement } from './testing-event-template-management';

vi.mock('@/i18n/navigation', () => ({
  useRouter: () => ({ refresh: vi.fn() }),
}));

vi.mock('@/lib/testing-lab/events-actions', () => ({
  saveTestingEventTemplate: vi.fn(),
  setTestingEventTemplateArchived: vi.fn(),
}));

vi.mock('./questionnaire-builder', () => ({
  QuestionnaireBuilder: ({ value }: { value: { title?: string } }) => (
    <div>{value.title}</div>
  ),
}));

describe('TestingEventTemplateManagement', () => {
  it('presents calendar language and shadcn form controls', () => {
    render(<TestingEventTemplateManagement templates={[]} />);

    expect(screen.getByRole('button', { name: 'New calendar' })).toBeVisible();
    expect(
      screen.getByRole('heading', { name: 'Create an event calendar' }),
    ).toBeVisible();
    expect(screen.getByLabelText('Calendar name')).toBeRequired();
    expect(screen.getAllByRole('combobox')).toHaveLength(2);
    expect(document.querySelector('select')).not.toBeInTheDocument();
    expect(
      screen.getByRole('checkbox', {
        name: 'Require developer feedback by default',
      }),
    ).toBeChecked();
  });

  it('opens an existing calendar without card borders in the list', () => {
    const templates = [
      {
        id: 'template-1',
        name: 'Community playtests',
        currentRevisionNumber: 2,
        currentRevision: {
          generalRules: 'Be respectful.',
          candidateInstructions: 'Bring a playable build.',
          testerInstructions: 'Complete the assigned tasks.',
          defaultMode: 'Hybrid',
          defaultApprovalMode: 'Committee',
          defaultRequiresFeedback: false,
          projectApplicationSchema: {
            title: 'Project application',
            questions: [],
          },
          testerRegistrationSchema: {
            title: 'Tester registration',
            questions: [],
          },
        },
      },
    ] as TestingLabTestingEventTemplateProjection[];

    render(<TestingEventTemplateManagement templates={templates} />);

    expect(screen.getByText('Community playtests')).toBeVisible();
    expect(screen.getByText('Revision 2')).toBeVisible();
    expect(screen.getByLabelText('Calendar name')).toHaveValue(
      'Community playtests',
    );
    expect(screen.getByRole('button', { name: 'Archive' })).toBeVisible();
    expect(
      screen.getByText('Community playtests').closest('button'),
    ).not.toHaveClass('border');
  });
});
