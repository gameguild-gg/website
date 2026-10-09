import { cleanup, render, screen } from '@testing-library/react';
import type React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  redirect: vi.fn((target: string | { href: string; locale: string }) => {
    const href = typeof target === 'string' ? target : `/${target.locale}${target.href}`;
    throw new Error(`redirect:${href}`);
  }),
  notFound: vi.fn(() => {
    throw new Error('not-found');
  }),
  forbidden: vi.fn(() => {
    throw new Error('forbidden');
  }),
  getCourse: vi.fn(),
  getCourseAccessCapabilities: vi.fn(),
  getCourseAnalytics: vi.fn(),
  getCourseCompletionAnalytics: vi.fn(),
  getCourseEngagementAnalytics: vi.fn(),
  getCourseRevenueAnalytics: vi.fn(),
  getCourseCohorts: vi.fn(),
  getCourseContent: vi.fn(),
  getCourseStudents: vi.fn(),
  getCourseAssessments: vi.fn(),
  getCourseAssessmentGroups: vi.fn(),
  getCourseAssessmentAnalytics: vi.fn(),
  getCourseCertificates: vi.fn(),
  getMyTasks: vi.fn(),
  canManageCourse: vi.fn(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/workspace/learning',
  redirect: mocks.redirect,
  notFound: mocks.notFound,
  forbidden: mocks.forbidden,
}));

vi.mock('@/i18n/navigation', () => ({
  redirect: mocks.redirect,
  Link: ({ href, locale: _locale, prefetch: _prefetch, children, ...props }: { href: string; locale?: string; prefetch?: boolean; children: React.ReactNode }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock('@/lib/learning/course-route', () => ({
  buildDashboardCoursePath: (course: string, path = '') => `/workspace/learning/courses/${course}${path ? `/${path}` : ''}`,
  getCourseRouteParam: (course: { slug?: string | null; id: string }) => course.slug || course.id,
}));

vi.mock('@/lib/learning/course-launch', () => ({
  deriveCourseLaunchSummary: () => ({
    storefrontState: 'enrollment-open',
    academyState: 'live',
    readinessState: 'live',
    blockers: [],
    structure: {
      modules: 2,
      lessons: 4,
      totalDurationMinutes: 180,
    },
    checks: [
      { key: 'thumbnail', label: 'Cover image', done: true },
      { key: 'module', label: 'Module structure', done: true },
      { key: 'lesson', label: 'Lesson content', done: true },
    ],
  }),
  formatDurationLabel: (minutes: number) => `${minutes} min`,
}));

vi.mock('@/lib/learning', () => ({
  getCourse: mocks.getCourse,
  getCourseAccessCapabilities: mocks.getCourseAccessCapabilities,
  getCourseAnalytics: mocks.getCourseAnalytics,
  getCourseCompletionAnalytics: mocks.getCourseCompletionAnalytics,
  getCourseEngagementAnalytics: mocks.getCourseEngagementAnalytics,
  getCourseRevenueAnalytics: mocks.getCourseRevenueAnalytics,
  getCourseCohorts: mocks.getCourseCohorts,
  getCourseContent: mocks.getCourseContent,
  getCourseStudents: mocks.getCourseStudents,
  getCourseAssessments: mocks.getCourseAssessments,
  getCourseAssessmentGroups: mocks.getCourseAssessmentGroups,
  getCourseAssessmentAnalytics: mocks.getCourseAssessmentAnalytics,
  getCourseCertificates: mocks.getCourseCertificates,
  getMyTasks: mocks.getMyTasks,
  canManageCourse: mocks.canManageCourse,
}));

vi.mock('@/lib/learning/queries/assessments', () => ({
  getCourseAssessments: mocks.getCourseAssessments,
}));

vi.mock('./course-nav', () => ({
  CourseNav: ({ children, courseTitle }: { children: React.ReactNode; courseTitle: string }) => (
    <section data-testid="course-nav">
      <h1>{courseTitle}</h1>
      {children}
    </section>
  ),
}));

vi.mock('./content/content-tree', () => ({
  ContentTree: ({
    modules,
    allItems,
    virtualModuleIds,
  }: {
    modules: unknown[];
    allItems: unknown[];
    virtualModuleIds: unknown[];
  }) => (
    <div data-testid="content-tree">{`${modules.length} modules / ${allItems.length} items / ${virtualModuleIds.length} virtual`}</div>
  ),
}));

vi.mock('./assessments/assessments-list', () => ({
  AssessmentsList: ({ assessments, assessmentGroups }: { assessments: unknown[]; assessmentGroups: unknown[] }) => (
    <div data-testid="assessments-list">{`${assessments.length} assessments / ${assessmentGroups.length} groups`}</div>
  ),
}));

vi.mock('./classes/class-control-center', () => ({
  ClassControlCenter: ({ cohorts }: { cohorts: unknown[] }) => <div data-testid="classes-manager">{`${cohorts.length} classes`}</div>,
}));

vi.mock('./students/student-table', () => ({
  StudentTable: ({ students }: { students: unknown[] }) => <div data-testid="student-table">{`${students.length} students`}</div>,
}));

vi.mock('./certificates/certificate-template-manager', () => ({
  CertificateTemplateManager: ({ templates }: { templates: unknown[] }) => <div data-testid="certificate-manager">{`${templates.length} templates`}</div>,
}));

vi.mock('./listing/listing-launch-form', () => ({
  ListingLaunchForm: ({ course }: { course: { id: string } }) => <div data-testid="listing-launch-form">{course.id}</div>,
}));

import CourseRouteLayout from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/layout';
import CourseRouteRedirectPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/page';
import OverviewPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/overview/page';
import ConsoleOverviewPage from '@/app/[locale]/(dashboards)/console/learning/courses/[course]/overview/page';
import ContentPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/content/page';
import AssessmentsPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/assessments/page';
import ClassesPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/classes/page';
import StudentsPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/students/page';
import CertificatesPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/certificates/page';
import ListingPage from '@/app/[locale]/(dashboards)/workspace/learning/courses/[course]/listing/page';

const params = (path = 'advanced-game-ai') => Promise.resolve({ locale: 'en-US', course: path });

const course = {
  id: 'course-1',
  slug: 'advanced-game-ai-by-gameguild',
  title: 'Advanced Game AI',
  description: 'Course-management route test fixture.',
  status: 'Published',
  category: 'Programming',
  difficulty: 'Advanced',
  visibility: 'Public',
  enrollmentStatus: 'Open',
  currentEnrollments: 12,
  maxEnrollments: 24,
  averageRating: 4.8,
  totalRatings: 10,
  estimatedHours: 18,
  enrollmentDeadline: '2026-08-15T00:00:00.000Z',
  createdAt: '2026-01-01T00:00:00.000Z',
  updatedAt: '2026-06-01T00:00:00.000Z',
  features: {
    hasContent: true,
    hasAssessments: true,
    hasCertificates: true,
    hasClasses: true,
    hasDiscussions: true,
  },
};

const courseContent = {
  items: [
    {
      id: 'module-1',
      courseId: 'course-1',
      parentId: null,
      title: 'Week 01',
      description: 'Opening module',
      type: 'Module',
      status: 'published',
      order: 1,
      duration: 0,
      createdAt: '2026-01-01T00:00:00.000Z',
      updatedAt: '2026-01-01T00:00:00.000Z',
    },
    {
      id: 'lesson-1',
      courseId: 'course-1',
      parentId: 'module-1',
      title: 'AI Basics',
      description: 'Core lesson',
      type: 'Lesson',
      status: 'published',
      order: 1,
      duration: 45,
      createdAt: '2026-01-01T00:00:00.000Z',
      updatedAt: '2026-01-01T00:00:00.000Z',
    },
  ],
};

describe('course-management dashboard route pages', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.getCourse.mockResolvedValue(course);
    mocks.getCourseAccessCapabilities.mockResolvedValue({
      courseId: course.id,
      courseExists: true,
      isTenantMember: true,
      isOwner: true,
      hasActiveEnrollment: false,
      canLearn: false,
      canEdit: true,
      canPublish: true,
      canReviewAsStaff: true,
      canAccessWorkspace: true,
    });
    mocks.getCourseAnalytics.mockResolvedValue({
      totalUsers: 12,
      completedUsers: 5,
      completionRate: 42,
    });
    mocks.getCourseContent.mockResolvedValue(courseContent);
    mocks.getCourseCompletionAnalytics.mockResolvedValue({
      courseId: 'course-1',
      totalEnrolled: 12,
      totalCompleted: 5,
      completionRate: 42,
      avgCompletionTime: 0,
      dropOffPoints: [],
      funnel: [
        { stage: 'Enrolled', count: 12, percentage: 100 },
        { stage: 'Completed', count: 5, percentage: 42 },
      ],
      completionTrend: [],
    });
    mocks.getCourseEngagementAnalytics.mockResolvedValue({
      courseId: 'course-1',
      period: { from: '2026-06-01T00:00:00.000Z', to: '2026-06-30T00:00:00.000Z' },
      activeStudents: 8,
      totalViews: 32,
      avgSessionDuration: 600,
      contentViews: [],
      dailyActivity: [],
      peakHours: [],
    });
    mocks.getCourseRevenueAnalytics.mockResolvedValue({
      courseId: 'course-1',
      period: { from: '2026-06-01T00:00:00.000Z', to: '2026-06-30T00:00:00.000Z' },
      currency: 'USD',
      totalRevenue: 1200,
      totalTransactions: 4,
      avgTransactionValue: 300,
      refundRate: 0,
      revenueByTier: [],
      revenueBySource: [],
      revenueTrend: [],
      discountUsage: [],
    });
    mocks.getCourseStudents.mockResolvedValue({
      total: 2,
      students: [
        {
          id: 'student-1',
          userId: 'student-1',
          displayName: 'Ada Learner',
          email: 'ada@example.com',
          status: 'Active',
          enrolledAt: '2026-06-01T00:00:00.000Z',
          lastActivity: new Date().toISOString(),
          progress: 75,
        },
        {
          id: 'student-2',
          userId: 'student-2',
          displayName: 'Grace Learner',
          email: 'grace@example.com',
          status: 'Completed',
          enrolledAt: '2026-06-02T00:00:00.000Z',
          lastActivity: '2026-05-01T00:00:00.000Z',
          progress: 100,
        },
      ],
    });
    mocks.getCourseCohorts.mockResolvedValue({
      total: 1,
      scheduledCount: 1,
      activeCount: 0,
      completedCount: 0,
      cohorts: [{ id: 'class-1', name: 'June Cohort', status: 'scheduled' }],
    });
    mocks.getCourseAssessments.mockResolvedValue({
      assessments: [{ id: 'assessment-1', title: 'Week 01 Quiz' }],
      total: 1,
    });
    mocks.getCourseAssessmentGroups.mockResolvedValue([
      { id: 'group-1', name: 'Quizzes', weightPercent: 100, order: 1 },
    ]);
    mocks.getCourseAssessmentAnalytics.mockResolvedValue({
      courseId: 'course-1',
      assessmentCount: 1,
      gradedCount: 0,
      ungradedCount: 1,
      averagePercent: 0,
      passRate: 0,
      distribution: [],
      groups: [],
    });
    mocks.getCourseCertificates.mockResolvedValue({
      total: 1,
      issuedCount: 3,
      templates: [{ id: 'template-1', name: 'Completion certificate', courseId: 'course-1' }],
    });
    mocks.getMyTasks.mockResolvedValue({ ok: true, tasks: [] });
    mocks.canManageCourse.mockResolvedValue(false);
  });

  it('wraps course subroutes in the course nav and preloads related dashboards', async () => {
    render(
      await CourseRouteLayout({
        params: params(),
        children: <div>Nested course page</div>,
      } as never),
    );

    expect(screen.getByTestId('course-nav')).toHaveTextContent('Advanced Game AI');
    expect(screen.getByText('Nested course page')).toBeInTheDocument();
    expect(mocks.getCourseAnalytics).toHaveBeenCalledWith('course-1');
    expect(mocks.getCourseContent).toHaveBeenCalledWith('course-1');
    expect(mocks.getCourseStudents).toHaveBeenCalledWith('course-1');
    expect(mocks.getCourseCohorts).toHaveBeenCalledWith('course-1');
  });

  it('redirects the course root to the canonical overview slug route', async () => {
    await expect(CourseRouteRedirectPage({ params: params('course-1') } as never)).rejects.toThrow(
      'redirect:/en-US/workspace/learning/courses/advanced-game-ai-by-gameguild/overview',
    );
  });

  it('renders the overview dashboard from analytics and content data', async () => {
    render(await OverviewPage({ params: params() } as never));

    expect(screen.getByText('Launch Control')).toBeInTheDocument();
    expect(screen.getByText('Course Readiness')).toBeInTheDocument();
    expect(screen.getByText('No launch blockers remain on the current dashboard contract.')).toBeInTheDocument();
    expect(screen.getByText('Open Listing Controls')).toBeInTheDocument();
  });

  it('renders the awaiting grading card with this course grade task count for managers', async () => {
    mocks.canManageCourse.mockResolvedValueOnce(true);
    mocks.getMyTasks.mockResolvedValueOnce({
      ok: true,
      tasks: [
        { type: 'grade', courseId: 'course-1', assessmentId: 'assessment-1', countSubmitted: 2 },
        { type: 'grade', courseId: 'other-course', assessmentId: 'assessment-2', countSubmitted: 5 },
        { type: 'do', courseId: 'course-1', assessmentId: 'assessment-3' },
      ],
    });

    render(await ConsoleOverviewPage({ params: params() } as never));

    const card = screen.getByTestId('awaiting-grading-card');
    expect(card).toHaveAttribute('href', '/workspace/learning/courses/advanced-game-ai-by-gameguild/assessments');
    expect(card).toHaveTextContent('Awaiting grading');
    expect(card).toHaveTextContent('1');
  });

  it('renders an em dash on the awaiting grading card when the tasks fetch fails', async () => {
    mocks.canManageCourse.mockResolvedValueOnce(true);
    mocks.getMyTasks.mockResolvedValueOnce({ ok: false, error: 'Failed to load tasks. Please try again.' });

    render(await ConsoleOverviewPage({ params: params() } as never));

    expect(screen.getByTestId('awaiting-grading-card')).toHaveTextContent('—');
  });

  it('hides the awaiting grading card from non-managers', async () => {
    mocks.canManageCourse.mockResolvedValueOnce(false);
    mocks.getMyTasks.mockResolvedValueOnce({
      ok: true,
      tasks: [
        { type: 'grade', courseId: 'course-1', assessmentId: 'assessment-1', countSubmitted: 2 },
      ],
    });

    render(await ConsoleOverviewPage({ params: params() } as never));

    expect(screen.queryByTestId('awaiting-grading-card')).not.toBeInTheDocument();
  });

  it('renders content, assessment, class, student, certificate, and listing management pages', async () => {
    render(await ContentPage({ params: params() } as never));
    expect(screen.getByText('1 modules')).toBeInTheDocument();
    expect(screen.getByTestId('content-tree')).toHaveTextContent('1 modules / 2 items / 0 virtual');

    cleanup();
    render(await AssessmentsPage({ params: params() } as never));
    expect(screen.getByTestId('assessments-list')).toHaveTextContent('1 assessments / 1 groups');

    cleanup();
    render(await ClassesPage({ params: params() } as never));
    expect(screen.getByTestId('classes-manager')).toHaveTextContent('1 classes');

    cleanup();
    render(await StudentsPage({ params: params() } as never));
    expect(screen.getByTestId('student-table')).toHaveTextContent('2 students');
    expect(screen.getByText('Avg Progress')).toBeInTheDocument();

    cleanup();
    render(await CertificatesPage({ params: params() } as never));
    expect(screen.getByText('Templates')).toBeInTheDocument();
    expect(screen.getByTestId('certificate-manager')).toHaveTextContent('1 templates');

    cleanup();
    render(await ListingPage({ params: params() } as never));
    expect(screen.getByText('Listing State')).toBeInTheDocument();
    expect(screen.getByTestId('listing-launch-form')).toHaveTextContent('course-1');
  });

  it('wraps migrated flat course content in a virtual module', async () => {
    mocks.getCourse.mockResolvedValueOnce({ ...course, description: '' });
    mocks.getCourseContent.mockResolvedValueOnce({
      items: [
        {
          id: 'flat-lesson-1',
          courseId: 'course-1',
          parentId: null,
          title: 'Imported Lesson 01',
          description: null,
          type: 'Lesson',
          status: 'published',
          order: 0,
          duration: 75,
          createdAt: '2026-01-01T00:00:00.000Z',
          updatedAt: '2026-01-01T00:00:00.000Z',
        },
        {
          id: 'flat-lesson-2',
          courseId: 'course-1',
          parentId: null,
          title: 'Imported Lesson 02',
          description: null,
          type: 'Lesson',
          status: 'draft',
          order: 1,
          duration: 5,
          createdAt: '2026-01-01T00:00:00.000Z',
          updatedAt: '2026-01-01T00:00:00.000Z',
        },
      ],
    });

    render(await ContentPage({ params: params() } as never));

    expect(screen.getByText('2 content items')).toBeInTheDocument();
    expect(screen.getByText('1 published')).toBeInTheDocument();
    expect(screen.getByText('1h 20m')).toBeInTheDocument();
    expect(screen.getByTestId('content-tree')).toHaveTextContent('1 modules / 2 items / 1 virtual');
  });

  it('uses the Next not-found boundary when protected course-management routes cannot load the course', async () => {
    mocks.getCourse.mockResolvedValue(null);

    await expect(CourseRouteLayout({ params: params(), children: <div /> } as never)).rejects.toThrow('not-found');
    await expect(OverviewPage({ params: params() } as never)).rejects.toThrow('not-found');
    await expect(ContentPage({ params: params() } as never)).rejects.toThrow('not-found');
    await expect(ClassesPage({ params: params() } as never)).rejects.toThrow('not-found');
    await expect(StudentsPage({ params: params() } as never)).rejects.toThrow('not-found');
    await expect(ListingPage({ params: params() } as never)).rejects.toThrow('not-found');
  });
});
