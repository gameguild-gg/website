import '@testing-library/jest-dom/vitest';
import React from 'react';
import { render, screen, within } from '@testing-library/react';
import type { AnchorHTMLAttributes, ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';
import {
  SubmissionsList,
  statusBadgeVariant,
} from './submissions-list';
import type { LearningAssessmentsAssessmentSubmission } from '@game-guild/client';

vi.mock('@/i18n/navigation', () => ({
  Link: ({
    href,
    children,
    ...props
  }: AnchorHTMLAttributes<HTMLAnchorElement> & {
    href: string;
    children: ReactNode;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@game-guild/ui/components/table', async () => {
  const React = await import('react');
  return {
    Table: (props: { children: ReactNode; [k: string]: unknown }) =>
      React.createElement('table', props),
    TableHeader: (props: { children: ReactNode }) =>
      React.createElement('thead', props),
    TableBody: (props: { children: ReactNode }) =>
      React.createElement('tbody', props),
    TableRow: (props: { children: ReactNode; [k: string]: unknown }) =>
      React.createElement('tr', props),
    TableHead: (props: { children: ReactNode }) =>
      React.createElement('th', props),
    TableCell: (props: { children: ReactNode; [k: string]: unknown }) =>
      React.createElement('td', props),
  };
});

vi.mock('@game-guild/ui/components/badge', async () => {
  const React = await import('react');
  return {
    Badge: ({
      children,
      variant,
      ...rest
    }: {
      children: ReactNode;
      variant?: string;
      [k: string]: unknown;
    }) =>
      React.createElement(
        'span',
        { 'data-variant': variant, ...rest },
        children,
      ),
  };
});

vi.mock('lucide-react', () => ({
  Loader2: () => React.createElement('span', { 'data-testid': 'loader-icon' }),
  ArrowLeft: () => React.createElement('span', { 'data-testid': 'arrow-icon' }),
}));

const baseProps = {
  courseSlug: 'course-1',
  assessmentId: 'assessment-1',
  assessmentSlug: 'assessment-1',
  maxScore: 100,
};

function makeSubmission(
  overrides: Partial<LearningAssessmentsAssessmentSubmission> & { id: string },
): LearningAssessmentsAssessmentSubmission {
  return {
    userId: '11111111-1111-1111-1111-111111111111',
    attemptNumber: 1,
    startedAt: '2024-01-01T10:00:00.000Z',
    submittedAt: '2024-01-01T11:00:00.000Z',
    status: 'Submitted',
    score: null,
    ...overrides,
  };
}

describe('SubmissionsList', () => {
  it('renders empty state when no submissions', () => {
    render(<SubmissionsList {...baseProps} submissions={[]} />);
    expect(screen.getByTestId('submissions-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('submissions-table')).not.toBeInTheDocument();
  });

  it('renders a row per submission with status badge + Grade link', () => {
    const submissions = [
      makeSubmission({
        id: 'sub-1',
        attemptNumber: 1,
        status: 'Submitted',
        score: null,
      }),
      makeSubmission({
        id: 'sub-2',
        userId: '22222222-2222-2222-2222-222222222222',
        attemptNumber: 2,
        status: 'Graded',
        score: 8700,
      }),
      makeSubmission({
        id: 'sub-3',
        attemptNumber: 1,
        status: 'InProgress',
        submittedAt: null,
        score: null,
        userId: '33333333-3333-3333-3333-333333333333',
      }),
    ];

    render(<SubmissionsList {...baseProps} submissions={submissions} />);

    const table = screen.getByTestId('submissions-table');
    expect(table).toBeInTheDocument();

    const rows = screen.getAllByTestId(/^submission-row-/);
    expect(rows).toHaveLength(3);

    const row1 = screen.getByTestId('submission-row-sub-1');
    expect(within(row1).getByText('1')).toBeInTheDocument();
    expect(within(row1).getByText('—')).toBeInTheDocument();
    const status1 = within(row1).getByTestId('submission-status-sub-1');
    expect(status1).toHaveTextContent('Submitted');
    expect(status1).toHaveAttribute('data-variant', 'default');

    const row2 = screen.getByTestId('submission-row-sub-2');
    const status2 = within(row2).getByTestId('submission-status-sub-2');
    expect(status2).toHaveTextContent('Graded');
    expect(status2).toHaveAttribute('data-variant', 'secondary');
    expect(within(row2).getByText('87/100')).toBeInTheDocument();

    const row3 = screen.getByTestId('submission-row-sub-3');
    const status3 = within(row3).getByTestId('submission-status-sub-3');
    expect(status3).toHaveTextContent('InProgress');
    expect(status3).toHaveAttribute('data-variant', 'outline');

    const gradeLinks = screen.getAllByTestId(/^submission-grade-link-/);
    expect(gradeLinks).toHaveLength(3);
    expect(gradeLinks[0]).toHaveAttribute(
      'href',
      '/workspace/learning/courses/course-1/assessments/assessment-1/submissions/sub-1/grade',
    );
    expect(gradeLinks[2]).toHaveAttribute(
      'href',
      '/workspace/learning/courses/course-1/assessments/assessment-1/submissions/sub-3/grade',
    );
  });

  it('renders loading indicator while fetching', () => {
    render(<SubmissionsList {...baseProps} submissions={[]} isLoading />);
    expect(screen.getByTestId('submissions-loading')).toBeInTheDocument();
    expect(screen.queryByTestId('submissions-table')).not.toBeInTheDocument();
  });

  it('renders error message instead of crashing when fetch rejects', () => {
    render(
      <SubmissionsList
        {...baseProps}
        submissions={[]}
        error="Failed to load submissions."
      />,
    );
    expect(screen.getByTestId('submissions-error')).toHaveTextContent(
      'Failed to load submissions.',
    );
    expect(screen.queryByTestId('submissions-table')).not.toBeInTheDocument();
  });
});

describe('statusBadgeVariant', () => {
  it('maps each known status to a stable variant', () => {
    expect(statusBadgeVariant('Submitted')).toBe('default');
    expect(statusBadgeVariant('Graded')).toBe('secondary');
    expect(statusBadgeVariant('Late')).toBe('destructive');
    expect(statusBadgeVariant('InProgress')).toBe('outline');
    expect(statusBadgeVariant('Returned')).toBe('outline');
    expect(statusBadgeVariant(undefined)).toBe('outline');
  });
});
