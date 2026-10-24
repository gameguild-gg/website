import '@testing-library/jest-dom/vitest';
import { render, screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const getPublicCourseCatalogMock = vi.fn();
const publicCourseCatalogMock = vi.fn();

vi.mock('@/lib/courses/services/course.service', async () => {
  const actual = await vi.importActual('@/lib/courses/services/course.service');
  return {
    ...(actual as object),
    getPublicCourseCatalog: getPublicCourseCatalogMock,
  };
});

vi.mock('@/components/courses/public-course-catalog', () => ({
  PublicCourseCatalog: ({ initialCourses }: { initialCourses: Array<{ title: string }> }) => {
    publicCourseCatalogMock(initialCourses);
    return (
      <div data-testid="public-course-catalog">
        {initialCourses.map((course) => (
          <span key={course.title}>{course.title}</span>
        ))}
      </div>
    );
  },
}));

vi.mock('next/image', () => ({
  default: ({ alt, src, fill: _fill, unoptimized: _unoptimized, priority: _priority, ...rest }: { alt: string; src: string; fill?: boolean; unoptimized?: boolean; priority?: boolean }) => <img alt={alt} src={src} {...rest} />,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, ...rest }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

const courseFixtures = [
  {
    id: 'course-python',
    title: 'Python Programming',
    slug: 'python',
    description: 'Students will learn Python programming fundamentals.',
    category: 'Programming',
    difficulty: 'Beginner',
    estimatedHours: 10,
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
  },
  {
    id: 'course-portfolio',
    title: 'Portfolio Development',
    slug: 'portfolio',
    description: 'Build a professional portfolio to showcase your work.',
    category: 'Design',
    difficulty: 'Beginner',
    estimatedHours: 20,
    currentEnrollments: 0,
    averageRating: 0,
    totalRatings: 0,
    isEnrollmentOpen: false,
    thumbnail: null,
    videoShowcaseUrl: null,
    visibility: 'Public',
    status: 'Published',
    maxEnrollments: null,
    enrollmentDeadline: null,
    skillsRequired: null,
    skillsProvided: null,
    programContents: null,
  },
];

const { default: CoursesPage } = await import('./page');

function hasStat(value: string, label: string) {
  const statLabel = screen.getByText(label, { selector: 'p' });
  const statCard = statLabel.parentElement;
  if (!statCard) {
    throw new Error(`Stat card for ${label} not found`);
  }
  return within(statCard).getByText(value, { selector: 'dd' });
}

describe('CoursesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders course storefront stats and passes courses to the catalog component', async () => {
    getPublicCourseCatalogMock.mockResolvedValue({
      success: true,
      source: 'api',
      data: courseFixtures,
    });

    render(await CoursesPage());

    expect(screen.getByText('Build the game development portfolio you want to be known for.')).toBeInTheDocument();
    expect(hasStat('2', 'Courses')).toBeInTheDocument();
    expect(hasStat('1', 'Open enrollment')).toBeInTheDocument();
    expect(hasStat('30h', 'Study time')).toBeInTheDocument();
    expect(screen.getAllByText('Python Programming').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Portfolio Development').length).toBeGreaterThan(0);
    expect(publicCourseCatalogMock).toHaveBeenCalledWith(courseFixtures);
  });

  it('links returning learners to their enrolled courses from the hero', async () => {
    getPublicCourseCatalogMock.mockResolvedValue({
      success: true,
      source: 'api',
      data: courseFixtures,
    });

    render(await CoursesPage());

    const continueLearning = screen.getByRole('link', { name: /continue learning/i });
    expect(continueLearning).toHaveAttribute('href', '/learn/courses');
  });

  it('renders an empty live catalog without falling back to imported snapshots', async () => {
    getPublicCourseCatalogMock.mockResolvedValue({
      success: true,
      source: 'api',
      data: [],
    });

    render(await CoursesPage());

    expect(hasStat('0', 'Courses')).toBeInTheDocument();
    expect(screen.queryByText(/showing the imported GameGuild course snapshot/i)).not.toBeInTheDocument();
    expect(publicCourseCatalogMock).toHaveBeenCalledWith([]);
  });

  it('renders an explicit unavailable state instead of an empty catalog when the learning API fails', async () => {
    getPublicCourseCatalogMock.mockResolvedValue({
      success: false,
      data: [],
      error: '[503] Learning API unreachable',
    });

    render(await CoursesPage());

    expect(screen.getByText('The course catalog is temporarily unavailable')).toBeInTheDocument();
    expect(screen.getByText(/This is not an empty catalog/i)).toBeInTheDocument();
    expect(screen.getByText(/latest error: \[503\] learning api unreachable/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /try again/i })).toHaveAttribute('href', '/courses');
    expect(screen.getByRole('link', { name: /go to my courses/i })).toHaveAttribute('href', '/learn/courses');

    expect(screen.queryByText('Build the game development portfolio you want to be known for.')).not.toBeInTheDocument();
    expect(publicCourseCatalogMock).not.toHaveBeenCalled();
  });

});
