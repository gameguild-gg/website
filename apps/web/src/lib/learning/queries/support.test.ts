import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getToken: vi.fn(),
  resolveCourseId: vi.fn(),
  getCoursesSupportTickets: vi.fn(),
  getCoursesSupportTickets1: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: mocks.getToken }));
vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningCoursesSupportticketsModule: class {
      getCoursesSupportTickets = mocks.getCoursesSupportTickets;
      getCoursesSupportTickets1 = mocks.getCoursesSupportTickets1;
    },
    LearningExperienceSocialDiscussionsModule: class {},
    LearningExperienceSocialRepliesModule: class {},
  },
}));
vi.mock('./course', () => ({ resolveCourseId: mocks.resolveCourseId }));

import { getCourseSupportTickets, getSupportTicket } from './support';

describe('course support ticket queries', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mocks.createServerClient.mockReturnValue({});
    mocks.getToken.mockResolvedValue('access-token');
    mocks.resolveCourseId.mockResolvedValue('course-1');
  });

  it('loads the persisted course ticket queue through the generated module', async () => {
    mocks.getCoursesSupportTickets.mockResolvedValue({
      ok: true,
      data: {
        items: [{
          id: 'ticket-1',
          customerId: 'course-1',
          reporterUserId: 'user-1',
          reporterName: 'Ada Learner',
          reporterEmail: 'ada@example.com',
          subject: 'Cannot open lesson 3',
          category: 'access',
          status: 'InProgress',
          priority: 'High',
          openedAt: '2026-07-01T10:00:00.000Z',
          lastMessageAt: '2026-07-02T10:00:00.000Z',
          messageCount: 2,
          messages: [],
        }],
        totalCount: 1,
      },
    });

    const result = await getCourseSupportTickets('course-slug');

    expect(mocks.getCoursesSupportTickets).toHaveBeenCalledWith('course-1', { skip: 0, take: 100 });
    expect(result).toMatchObject({ total: 1, openCount: 0, inProgressCount: 1, resolvedCount: 0 });
    expect(result.tickets[0]).toMatchObject({
      id: 'ticket-1',
      courseId: 'course-1',
      studentId: 'user-1',
      studentName: 'Ada Learner',
      subject: 'Cannot open lesson 3',
      status: 'in-progress',
      priority: 'high',
    });
  });

  it('loads persisted ticket messages through the generated course-scoped endpoint', async () => {
    mocks.getCoursesSupportTickets1.mockResolvedValue({
      ok: true,
      data: {
        id: 'ticket-1',
        customerId: 'course-1',
        reporterUserId: 'user-1',
        reporterName: 'Ada Learner',
        reporterEmail: 'ada@example.com',
        subject: 'Cannot open lesson 3',
        category: 'access',
        status: 'Open',
        priority: 'Normal',
        openedAt: '2026-07-01T10:00:00.000Z',
        lastMessageAt: '2026-07-01T10:00:00.000Z',
        messageCount: 1,
        messages: [{
          id: 'message-1',
          ticketId: 'ticket-1',
          authorUserId: 'user-1',
          authorName: 'Ada Learner',
          authorType: 'Customer',
          body: 'The player stays blank.',
          createdAt: '2026-07-01T10:00:00.000Z',
        }],
      },
    });

    const result = await getSupportTicket('course-slug', 'ticket-1');

    expect(mocks.getCoursesSupportTickets1).toHaveBeenCalledWith('course-1', 'ticket-1');
    expect(result?.messages).toEqual([expect.objectContaining({
      id: 'message-1',
      authorRole: 'student',
      content: 'The player stays blank.',
    })]);
  });
});
