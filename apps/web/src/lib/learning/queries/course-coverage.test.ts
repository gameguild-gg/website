import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  auth: vi.fn(),
  getToken: vi.fn(),
  createServerClient: vi.fn(),
  clientRequest: vi.fn(),
  getCourseById: vi.fn(),
  getCourseBySlug: vi.fn(),
  getAnalytics: vi.fn(),
  getStudents: vi.fn(),
  getContent: vi.fn(),
  getContentById: vi.fn(),
  getUser: vi.fn(),
  hasPermission: vi.fn(),
  readGrading: vi.fn(),
}));

vi.mock('react', () => ({ cache: (callback: unknown) => callback }));
vi.mock('@/auth', () => ({ auth: mocks.auth, getToken: mocks.getToken }));
vi.mock('@game-guild/grading', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@game-guild/grading')>()),
  readContentGradingDefinition: mocks.readGrading,
}));
vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  hasRole: (session: { user?: { roles?: string[] } }, role: string) => Boolean(session.user?.roles?.includes(role)),
  GeneratedApi: {
    LearningCoursesProgramModule: class {
      getCoursesForGetCoursesById = mocks.getCourseById;
      getCoursesSlug = mocks.getCourseBySlug;
      getCoursesAnalytics = mocks.getAnalytics;
      getCoursesUsers = mocks.getStudents;
    },
    LearningCoursesProgramContentModule: class {
      getCoursesContent = mocks.getContent;
      getCoursesContentById = mocks.getContentById;
    },
    UsersModule: class {
      getUsersForGetUsersByUserId = mocks.getUser;
    },
    AccessControlResourcePermissionsModule: class {
      getAuthorizationResourcesHasPermission = mocks.hasPermission;
    },
  },
}));

import {
  canEditCourse,
  canManageCourse,
  getCourseAccessCapabilities,
  getContentItem,
  getCourse,
  getCourseAnalytics,
  getCourseContent,
  getCourseStudents,
  resolveCourseId,
} from './course';

const courseId = '08691da8-245e-4d9e-b729-83c9023ba061';
const contentId = '18691da8-245e-4d9e-b729-83c9023ba062';

function accessProjection(overrides: Record<string, boolean> = {}) {
  return {
    courseId,
    courseExists: false,
    isTenantMember: false,
    isOwner: false,
    hasActiveEnrollment: false,
    canLearn: false,
    canEdit: false,
    canPublish: false,
    canReviewAsStaff: false,
    canAccessWorkspace: false,
    ...overrides,
  };
}

describe('course query coverage', () => {
  beforeEach(() => {
    Object.values(mocks).forEach((mock) => mock.mockReset());
    vi.stubEnv('API_URL', '');
    vi.stubEnv('NEXT_PUBLIC_API_URL', '');
    mocks.createServerClient.mockReturnValue({ request: mocks.clientRequest });
    mocks.getToken.mockResolvedValue('token');
    mocks.auth.mockResolvedValue({ user: { id: 'user-1' }, tenantId: 'tenant-1' });
    mocks.readGrading.mockReturnValue({ maxScore: 10 });
  });

  it('configures API clients with server, public, and default URLs and optional tenant scope', async () => {
    mocks.getCourseBySlug.mockResolvedValue({ ok: false, error: {} });

    vi.stubEnv('API_URL', 'https://internal.example');
    vi.stubEnv('NEXT_PUBLIC_API_URL', 'https://public.example');
    await getCourse('server-course');
    let options = mocks.createServerClient.mock.calls.at(-1)?.[0];
    expect(options.baseUrl).toBe('https://internal.example');
    expect(options.tenant).toBeUndefined();
    await expect(options.auth.getAccessToken()).resolves.toBe('token');

    vi.stubEnv('API_URL', '');
    await getCourse('public-course');
    options = mocks.createServerClient.mock.calls.at(-1)?.[0];
    expect(options.baseUrl).toBe('https://public.example');

    vi.stubEnv('NEXT_PUBLIC_API_URL', '');
    mocks.auth.mockResolvedValue({ user: { id: 'user-1' }, tenantId: 'tenant-1' });
    mocks.clientRequest.mockResolvedValue({
      ok: true,
      data: accessProjection({ canEdit: true, canAccessWorkspace: true }),
    });
    await canEditCourse(courseId);
    options = mocks.createServerClient.mock.calls.at(-1)?.[0];
    expect(options.baseUrl).toBe('http://localhost:8080');
    await expect(options.tenant.getTenantId()).resolves.toBe('tenant-1');
  });

  it.each([
    [2, 'published'],
    ['2', 'published'],
    ['Published', 'published'],
    ['published', 'published'],
    [3, 'archived'],
    ['3', 'archived'],
    ['Archived', 'archived'],
    ['archived', 'archived'],
    ['Draft', 'draft'],
  ])('maps compatibility status %s to %s', async (status, expected) => {
    mocks.getCourseBySlug.mockResolvedValue({ ok: true, data: { id: courseId, status, visibility: 'Private' } });
    await expect(getCourse(`course-${String(status)}`)).resolves.toMatchObject({ status: expected });
  });

  it('maps complete course data and resolves creator name, email, and fallback handles', async () => {
    mocks.getCourseBySlug
      .mockResolvedValueOnce({
        ok: true,
        data: {
          id: courseId, creatorId: 'creator-name', title: 'Course', description: 'Description', metadata: '{}',
          slug: 'course', status: 'Published', visibility: 'Public', thumbnail: 'cover.png', videoShowcaseUrl: 'video.mp4',
          estimatedHours: 5, passingScore: 7_500, category: 'Programming', difficulty: 'Advanced',
          skillsRequired: 'Logic', skillsProvided: 'C++', enrollmentStatus: 'Closed', maxEnrollments: 20,
          enrollmentDeadline: '2026-10-01T00:00:00.000Z', currentEnrollments: 4, averageRating: 4.5,
          totalRatings: 2, isEnrollmentOpen: false, createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z',
        },
      })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: 'creator-email' } })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: 'creator-fallback' } })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: 'creator-api-failure' } })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: 'creator-throw' } })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: '***' } })
      .mockResolvedValueOnce({ ok: true, data: { id: courseId, creatorId: null } });
    mocks.getUser.mockImplementation(async (id: string) => {
      if (id === 'creator-name') return { ok: true, data: { name: 'Ada Lovelace' } };
      if (id === 'creator-email') return { ok: true, data: { name: '', email: 'grace.hopper@example.com' } };
      if (id === 'creator-fallback') return { ok: true, data: { name: '', email: '' } };
      if (id === 'creator-api-failure') return { ok: false, error: {} };
      if (id === '***') return { ok: true, data: { name: '', email: '' } };
      throw new Error('directory unavailable');
    });

    await expect(getCourse('complete')).resolves.toMatchObject({
      creatorHandle: 'ada-lovelace', title: 'Course', description: 'Description', metadata: '{}', status: 'published',
      visibility: 'public', thumbnail: 'cover.png', videoShowcaseUrl: 'video.mp4', estimatedHours: 5, passingScore: 75,
      category: 'Programming', difficulty: 'Advanced', enrollmentStatus: 'Closed', currentEnrollments: 4,
      isEnrollmentOpen: false, createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z',
    });
    await expect(getCourse('email')).resolves.toMatchObject({ creatorHandle: 'grace-hopper' });
    await expect(getCourse('fallback')).resolves.toMatchObject({ creatorHandle: 'creator-fall' });
    await expect(getCourse('api-failure')).resolves.toMatchObject({ creatorHandle: 'creator-api-' });
    await expect(getCourse('throw')).resolves.toMatchObject({ creatorHandle: 'creator-thro' });
    await expect(getCourse('empty-fallback')).resolves.toMatchObject({ creatorHandle: null });
    await expect(getCourse('no-creator')).resolves.toMatchObject({ creatorHandle: null });
  });

  it('maps default course fields and timestamps', async () => {
    mocks.getCourseBySlug.mockResolvedValue({ ok: true, data: { id: courseId, passingScore: null } });
    const result = await getCourse('defaults');
    expect(result).toMatchObject({
      creatorId: null, creatorHandle: null, title: '', description: '', metadata: null, slug: '', status: 'draft',
      visibility: 'private', thumbnail: null, videoShowcaseUrl: null, estimatedHours: null, passingScore: null,
      category: 'GeneralEducation', difficulty: 'Beginner', skillsRequired: null, skillsProvided: null,
      enrollmentStatus: 'Open', maxEnrollments: null, enrollmentDeadline: null, currentEnrollments: 0,
      averageRating: 0, totalRatings: 0, isEnrollmentOpen: true,
    });
    expect(result?.createdAt).toMatch(/^\d{4}-/);
    expect(result?.updatedAt).toMatch(/^\d{4}-/);
  });

  it('uses createdAt as updatedAt when no explicit update exists', async () => {
    mocks.getCourseBySlug.mockResolvedValue({ ok: true, data: { id: courseId, createdAt: '2026-01-01T00:00:00.000Z' } });
    await expect(getCourse('created-only')).resolves.toMatchObject({ updatedAt: '2026-01-01T00:00:00.000Z' });
  });

  it('handles blank identifiers, slug failures, ID failures, and transport exceptions', async () => {
    await expect(getCourse('   ')).resolves.toBeNull();

    mocks.getCourseBySlug.mockResolvedValueOnce({ ok: false, error: {} }).mockRejectedValueOnce(new Error('slug failed'));
    await expect(getCourse('missing')).resolves.toBeNull();
    await expect(getCourse('throws')).resolves.toBeNull();

    mocks.getCourseById.mockResolvedValueOnce({ ok: false, error: {} }).mockRejectedValueOnce(new Error('id failed'));
    await expect(getCourse(courseId)).resolves.toBeNull();
    await expect(getCourse('28691da8-245e-4d9e-b729-83c9023ba063')).resolves.toBeNull();
  });

  it('resolves canonical GUIDs, found slugs, and unknown slugs', async () => {
    await expect(resolveCourseId(` ${courseId} `)).resolves.toBe(courseId);
    mocks.getCourseBySlug
      .mockResolvedValueOnce({ ok: true, data: { id: courseId } })
      .mockResolvedValueOnce({ ok: false, error: {} });
    await expect(resolveCourseId('known')).resolves.toBe(courseId);
    await expect(resolveCourseId('unknown')).resolves.toBe('unknown');
  });

  it('uses the API projection for management and fails closed on missing context', async () => {
    mocks.clientRequest.mockResolvedValue({
      ok: true,
      data: accessProjection({ isOwner: true, canEdit: true, canPublish: true, canReviewAsStaff: true, canAccessWorkspace: true }),
    });
    await expect(canManageCourse(courseId)).resolves.toBe(true);

    mocks.auth.mockResolvedValueOnce({ user: { id: 'other' } });
    await expect(canManageCourse(courseId)).resolves.toBe(false);
    mocks.auth.mockResolvedValueOnce({ user: {} });
    await expect(canManageCourse(courseId)).resolves.toBe(false);
    mocks.auth.mockRejectedValueOnce(new Error('auth failed'));
    await expect(canManageCourse(courseId)).resolves.toBe(false);
  });

  it('covers every course edit permission path', async () => {
    mocks.auth.mockResolvedValueOnce(null);
    await expect(canEditCourse(courseId)).resolves.toBe(false);

    mocks.auth.mockResolvedValueOnce({ user: { id: 'admin', roles: ['SystemAdmin'] }, tenantId: 'tenant-1' });
    mocks.clientRequest.mockResolvedValueOnce({
      ok: true,
      data: accessProjection({ canEdit: true, canPublish: true, canReviewAsStaff: true, canAccessWorkspace: true }),
    });
    await expect(canEditCourse(courseId)).resolves.toBe(true);

    mocks.auth.mockResolvedValueOnce({ user: { id: 'user-1' }, tenantId: 'tenant-1' });
    mocks.clientRequest.mockResolvedValueOnce({
      ok: true,
      data: accessProjection({ canEdit: true, canAccessWorkspace: true }),
    });
    await expect(canEditCourse(courseId)).resolves.toBe(true);

    mocks.auth.mockResolvedValueOnce({ user: { id: 'user-1' }, tenantId: 'tenant-1' });
    mocks.clientRequest.mockResolvedValueOnce({ ok: false, error: {} });
    await expect(canEditCourse(courseId)).resolves.toBe(false);

    mocks.auth.mockRejectedValueOnce(new Error('auth failed'));
    await expect(canEditCourse(courseId)).resolves.toBe(false);

    mocks.auth.mockResolvedValueOnce({ user: { id: 'user-1' }, tenantId: 'tenant-1' });
    mocks.clientRequest.mockRejectedValueOnce(new Error('offline'));
    await expect(getCourseAccessCapabilities(courseId)).resolves.toEqual(accessProjection());
  });

  it('maps, derives, clamps, and defaults analytics', async () => {
    mocks.getAnalytics
      .mockResolvedValueOnce({
        ok: true,
        data: {
          totalUsers: -4, activeUsers: -2, completedUsers: -1, completionRate: -20,
          averageCompletionTime: '01:00:00', totalViews: -3, lastActivity: '2026-01-01T00:00:00.000Z',
        },
      })
      .mockResolvedValueOnce({ ok: true, data: { totalUsers: 2, completedUsers: 5, completionRate: 200 } })
      .mockResolvedValueOnce({ ok: true, data: {} })
      .mockResolvedValueOnce({ ok: false, error: {} })
      .mockRejectedValueOnce(new Error('analytics failed'));

    await expect(getCourseAnalytics(courseId)).resolves.toMatchObject({ totalUsers: 0, activeUsers: 0, completedUsers: 0, completionRate: 0, totalViews: 0 });
    await expect(getCourseAnalytics(courseId)).resolves.toMatchObject({ completionRate: 100 });
    await expect(getCourseAnalytics(courseId)).resolves.toMatchObject({ completionRate: 0, averageCompletionTime: null, lastActivity: null });
    await expect(getCourseAnalytics(courseId)).resolves.toMatchObject({ totalUsers: 0 });
    await expect(getCourseAnalytics(courseId)).resolves.toMatchObject({ totalUsers: 0 });
  });

  it('maps and deduplicates nested content including default fields', async () => {
    const child = {
      id: contentId, slug: 'lesson', parentId: 'module-1', sortOrder: 2, type: 'Lesson', title: 'Lesson',
      description: 'Description', visibility: 'Private', estimatedMinutes: 20, estimatedMinutesSource: 'Manual',
      jsonBody: '{}', createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z', children: [],
    };
    mocks.getContent.mockResolvedValue({ ok: true, data: [
      { id: 'module-1', children: [child, { children: null }] },
      child,
    ] });

    const result = await getCourseContent(courseId);
    expect(result.total).toBe(2);
    expect(result.items[0]).toMatchObject({
      id: 'module-1', slug: 'module-1', parentId: null, order: 0, type: 'Lesson', title: '', description: null,
      status: 'draft', visibility: 'Public', duration: null, estimatedMinutesSource: null,
    });
    expect(result.items[1]).toMatchObject({
      id: contentId, slug: 'lesson', parentId: 'module-1', order: 2, title: 'Lesson', description: 'Description',
      status: 'draft', visibility: 'Private', duration: 20, estimatedMinutesSource: 'Manual',
      createdAt: '2026-01-01T00:00:00.000Z', updatedAt: '2026-01-02T00:00:00.000Z',
    });
    expect(mocks.readGrading).toHaveBeenCalledWith('{}');
  });

  it('returns empty content when the API fails or throws', async () => {
    mocks.getContent.mockResolvedValueOnce({ ok: false, error: {} }).mockRejectedValueOnce(new Error('content failed'));
    await expect(getCourseContent(courseId)).resolves.toEqual({ items: [], total: 0 });
    await expect(getCourseContent(courseId)).resolves.toEqual({ items: [], total: 0 });
  });

  it.each(['Markdown', 'Lexical', 'RevealJs', 'Video', 'Html', 'ExternalLink', null, undefined, 'Legacy']) (
    'normalizes lesson format %s in content detail',
    async (lessonFormat) => {
      mocks.getContentById.mockResolvedValue({
        ok: true,
        data: { id: contentId, visibility: 'Public', body: 'Body', jsonBody: '{}', isRequired: true, lessonFormat },
      });
      await expect(getContentItem(courseId, contentId)).resolves.toMatchObject({
        id: contentId,
        content: 'Body',
        jsonBody: '{}',
        status: 'published',
        settings: { isRequired: true, gradingConfig: { maxScore: 10 } },
        lessonFormat: lessonFormat === 'Legacy' ? 'Markdown' : (lessonFormat ?? null),
      });
    },
  );

  it('falls back from the direct content endpoint to recursive slug lookup', async () => {
    mocks.getContentById.mockResolvedValue({ ok: false, error: {} });
    mocks.getContent.mockResolvedValue({ ok: true, data: [{ id: 'module-1', children: [{ id: contentId, slug: 'target' }] }] });
    await expect(getContentItem(courseId, contentId)).resolves.toMatchObject({ id: contentId, slug: 'target' });

    mocks.getContent.mockResolvedValueOnce({ ok: true, data: [{ id: 'module-1', children: [{ id: contentId, slug: 'lesson-slug' }] }] });
    await expect(getContentItem(courseId, 'lesson-slug')).resolves.toMatchObject({ id: contentId });
    mocks.getContent.mockResolvedValueOnce({ ok: true, data: [{ id: 'other', children: null }] }).mockResolvedValueOnce({ ok: false, error: {} });
    await expect(getContentItem(courseId, 'missing')).resolves.toBeNull();
    await expect(getContentItem(courseId, 'failed')).resolves.toBeNull();
    mocks.getContent.mockRejectedValueOnce(new Error('content failed'));
    await expect(getContentItem(courseId, 'throws')).resolves.toBeNull();
  });

  it('maps students using names, emails, generated labels, and activity fallbacks', async () => {
    mocks.getStudents.mockResolvedValue({
      ok: true,
      data: [
        {
          enrollmentId: 'enrollment-1', userId: 'user-1', userName: ' Ada ', userEmail: ' ada@example.com ',
          startedAt: '2026-01-01T00:00:00.000Z', lastAccessedAt: '2026-01-02T00:00:00.000Z',
          completionPercentage: 4_240, completedAt: '2026-01-03T00:00:00.000Z',
        },
        { userId: 'user-2', userName: ' ', userEmail: 'grace@example.com', startedAt: '2026-02-01T00:00:00.000Z' },
        { userName: null, userEmail: null },
      ],
    });

    const result = await getCourseStudents(courseId);
    expect(result.total).toBe(3);
    expect(result.students).toMatchObject([
      { id: 'enrollment-1', userId: 'user-1', name: 'Ada', email: 'ada@example.com', progress: 42, lastActivity: '2026-01-02T00:00:00.000Z' },
      { id: 'user-2', name: 'grace', email: 'grace@example.com', progress: 0, lastActivity: '2026-02-01T00:00:00.000Z' },
      { id: 'user-2', userId: 'user-2', name: 'Student 3', email: '', progress: 0, completedAt: null },
    ]);
    expect(result.students[2]?.enrolledAt).toMatch(/^\d{4}-/);
    expect(result.students[2]?.lastActivity).toMatch(/^\d{4}-/);
  });

  it('returns empty students when the API fails or throws', async () => {
    mocks.getStudents.mockResolvedValueOnce({ ok: false, error: {} }).mockRejectedValueOnce(new Error('students failed'));
    await expect(getCourseStudents(courseId)).resolves.toEqual({ students: [], total: 0 });
    await expect(getCourseStudents(courseId)).resolves.toEqual({ students: [], total: 0 });
  });
});
