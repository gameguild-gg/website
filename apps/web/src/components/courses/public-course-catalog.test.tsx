import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AnchorHTMLAttributes, ImgHTMLAttributes, ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useSearchParams: () => new URLSearchParams(),
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({
    href,
    children,
    ...rest
  }: AnchorHTMLAttributes<HTMLAnchorElement> & { href: string; children: ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock('next/image', () => ({
  default: ({ alt, src, ...rest }: ImgHTMLAttributes<HTMLImageElement> & { alt: string; src: string }) => (
    <img alt={alt} src={src} {...rest} />
  ),
}));

import { PublicCourseCatalog } from './public-course-catalog';

function makeCourse(overrides: Record<string, unknown> = {}) {
  return {
    id: 'course-a',
    title: 'AI for Games',
    slug: 'ai4games',
    description: 'Build believable game agents.',
    category: 'gamedevelopment',
    difficulty: 'Advanced',
    estimatedHours: 40,
    currentEnrollments: 0,
    averageRating: 0,
    totalRatings: 0,
    isEnrollmentOpen: true,
    thumbnail: null,
    videoShowcaseUrl: null,
    visibility: 'Public',
    status: 'Published',
    maxEnrollments: null,
    enrollmentDeadline: null,
    skillsRequired: null,
    skillsProvided: null,
    programContents: null,
    ...overrides,
  };
}

const courseFixtures = [
  makeCourse(),
  makeCourse({
    id: 'course-b',
    title: 'Intro to Game Programming',
    slug: 'intro2gpro',
    description: 'Programming foundations for game development.',
    category: 'design',
    difficulty: 'Beginner',
    estimatedHours: 60,
    isEnrollmentOpen: false,
  }),
];

describe('PublicCourseCatalog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders courses with accessible search and filter groups', () => {
    render(<PublicCourseCatalog initialCourses={courseFixtures} />);

    expect(screen.getByRole('textbox', { name: /search courses by title, tool, or outcome/i })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: /filter courses by package/i })).toBeInTheDocument();
    expect(screen.getByRole('group', { name: /filter courses by discipline/i })).toBeInTheDocument();

    expect(screen.getByText('AI for Games')).toBeInTheDocument();
    expect(screen.getByText('Intro to Game Programming')).toBeInTheDocument();
    expect(screen.getByText('2 matching courses')).toBeInTheDocument();
  });

  it('marks the active package filter button as pressed', async () => {
    const user = userEvent.setup();
    render(<PublicCourseCatalog initialCourses={courseFixtures} />);

    const programmingButton = screen.getByRole('button', { name: 'Programming' });
    expect(programmingButton).toHaveAttribute('aria-pressed', 'false');

    await user.click(programmingButton);

    expect(programmingButton).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText('1 matching course')).toBeInTheDocument();
    expect(screen.getByText('Intro to Game Programming')).toBeInTheDocument();
    expect(screen.queryByText('AI for Games')).not.toBeInTheDocument();
  });

  it('shows a distinct empty state when no courses are published yet', () => {
    render(<PublicCourseCatalog initialCourses={[]} />);

    expect(screen.getByText('No courses are published yet.')).toBeInTheDocument();
    expect(screen.queryByText('No courses matched this view.')).not.toBeInTheDocument();
    expect(screen.getByText('0 matching courses')).toBeInTheDocument();
  });

  it('shows a no-match empty state with a working clear-filters action', async () => {
    const user = userEvent.setup();
    render(<PublicCourseCatalog initialCourses={courseFixtures} />);

    const search = screen.getByRole('textbox', { name: /search courses/i });
    await user.type(search, 'unmatched-discipline');

    expect(screen.getByText('0 matching courses')).toBeInTheDocument();
    expect(screen.getByText('No courses matched this view.')).toBeInTheDocument();
    expect(screen.queryByText('No courses are published yet.')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /clear filters/i }));

    expect(screen.getByText('2 matching courses')).toBeInTheDocument();
    expect(screen.getByText('AI for Games')).toBeInTheDocument();
    expect(screen.getByText('Intro to Game Programming')).toBeInTheDocument();
  });

  it('offers a reset action while filters are active and points to enrolled courses otherwise', async () => {
    const user = userEvent.setup();
    render(<PublicCourseCatalog initialCourses={courseFixtures} />);

    const enrolledLink = screen.getByRole('link', { name: /my enrolled courses/i });
    expect(enrolledLink).toHaveAttribute('href', '/learn/courses');
    expect(screen.queryByRole('button', { name: /reset filters/i })).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'AI & Systems' }));

    const resetButton = screen.getByRole('button', { name: /reset filters/i });
    await user.click(resetButton);

    expect(screen.getByText('2 matching courses')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /my enrolled courses/i })).toBeInTheDocument();
  });
});
