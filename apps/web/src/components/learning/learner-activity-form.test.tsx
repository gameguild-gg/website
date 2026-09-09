import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('@/lib/learner/activity-actions', () => ({
  submitAssessment: vi.fn(),
  submitContentActivity: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  useRouter: () => ({ refresh: vi.fn() }),
}));

import { LearnerActivityForm } from './learner-activity-form';

describe('LearnerActivityForm', () => {
  it('uses a POST form action so slow hydration cannot leak responses into the URL', () => {
    const { container } = render(
      <LearnerActivityForm
        courseId="course-1"
        courseSlug="game-production"
        enrollmentId="enrollment-1"
        activity={{
          kind: 'content',
          contentId: 'discussion-1',
          contentType: 'Discussion',
          title: 'Production discussion',
        }}
      />,
    );

    expect(screen.getByRole('button', { name: 'Submit discussion' })).toBeInTheDocument();
    expect(container.querySelector('form')).toHaveAttribute('action');
  });

  it('renders project choices as a native form control before hydration', () => {
    const { container } = render(
      <LearnerActivityForm
        courseId="course-1"
        courseSlug="game-production"
        enrollmentId="enrollment-1"
        activity={{
          kind: 'assessment',
          assessment: {
            id: 'assessment-1',
            title: 'Portfolio project',
            type: 'Project',
          },
          projects: [
            {
              id: 'project-1',
              title: 'Learner portfolio game',
              slug: 'learner-portfolio-game',
              status: 'Published',
              visibility: 'Public',
              createdAt: '2026-08-01T00:00:00Z',
              updatedAt: '2026-08-01T00:00:00Z',
            },
          ],
        }}
      />,
    );

    const select = container.querySelector('select[name="response"]');
    expect(select).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'Learner portfolio game' })).toHaveValue('project-1');
    expect(screen.getByRole('button', { name: 'Submit assessment' })).toBeEnabled();
  });

  it('keeps quiz attempts out of the generic assessment form', () => {
    render(
      <LearnerActivityForm
        courseId="course-1"
        courseSlug="game-production"
        enrollmentId="enrollment-1"
        activity={{
          kind: 'assessment',
          assessment: {
            id: 'quiz-1',
            title: 'Knowledge check',
            type: 'Quiz',
            submissionModalities: 'StructuredAnswer',
          },
        }}
      />,
    );

    expect(screen.getByText('Quiz attempt unavailable')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Submit assessment' })).not.toBeInTheDocument();
  });

});
