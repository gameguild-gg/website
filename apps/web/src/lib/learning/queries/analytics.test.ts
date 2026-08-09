import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  createServerClient: vi.fn(),
  getToken: vi.fn(),
  getCourseContent: vi.fn(),
  getCoursesAnalyticsEngagement: vi.fn(),
  getCoursesAnalyticsCompletionRates: vi.fn(),
  getCoursesAnalyticsRevenue: vi.fn(),
}));

vi.mock('@/auth', () => ({ getToken: mocks.getToken }));
vi.mock('@game-guild/client', () => ({
  createServerClient: mocks.createServerClient,
  GeneratedApi: {
    LearningCoursesProgramModule: class {
      getCoursesAnalyticsEngagement = mocks.getCoursesAnalyticsEngagement;
      getCoursesAnalyticsCompletionRates = mocks.getCoursesAnalyticsCompletionRates;
      getCoursesAnalyticsRevenue = mocks.getCoursesAnalyticsRevenue;
    },
  },
}));
vi.mock('./course', () => ({
  getCourseContent: mocks.getCourseContent,
  resolveCourseId: vi.fn(async (courseId: string) => courseId),
}));

import {
  getCourseCompletionAnalytics,
  getCourseEngagementAnalytics,
  getCourseRevenueAnalytics,
} from './analytics';

describe('learning analytics queries', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    mocks.createServerClient.mockReturnValue({});
    mocks.getToken.mockResolvedValue('access-token');
    mocks.getCourseContent.mockResolvedValue({
      items: [{ id: 'lesson-1', title: 'Blocking combat reads' }],
      total: 1,
    });
    mocks.getCoursesAnalyticsEngagement.mockReset();
    mocks.getCoursesAnalyticsCompletionRates.mockReset();
    mocks.getCoursesAnalyticsRevenue.mockReset();
  });

  it('uses generated engagement aggregates without synthetic daily activity', async () => {
    mocks.getCoursesAnalyticsEngagement.mockResolvedValue({
      ok: true,
      data: {
        dailyActiveUsers: 2,
        weeklyActiveUsers: 5,
        averageSessionDuration: '00:12:30',
        totalSessions: 9,
        contentEngagement: { 'lesson-1': 4 },
      },
    });

    const analytics = await getCourseEngagementAnalytics('course-engagement-no-fake-days');

    expect(mocks.getCoursesAnalyticsEngagement).toHaveBeenCalledWith('course-engagement-no-fake-days');
    expect(analytics.activeStudents).toBe(5);
    expect(analytics.totalViews).toBe(9);
    expect(analytics.avgSessionDuration).toBe(750);
    expect(analytics.dailyActivity).toEqual([]);
    expect(analytics.contentViews).toEqual([
      {
        contentId: 'lesson-1',
        contentTitle: 'Blocking combat reads',
        views: 4,
        avgWatchTime: 0,
        completionRate: 0,
      },
    ]);
  });

  it('keeps only API-dated completion trend rows', async () => {
    mocks.getCoursesAnalyticsCompletionRates.mockResolvedValue({
      ok: true,
      data: {
        overallCompletionRate: 50,
        contentCompletionRates: { 'lesson-1': 75 },
        completionTrends: [
          { completedCount: 1, totalCount: 4 },
          { date: '2026-06-10T00:00:00.000Z', completedCount: 2, totalCount: 4 },
        ],
      },
    });

    const analytics = await getCourseCompletionAnalytics('course-completion-no-fallback-date');

    expect(mocks.getCoursesAnalyticsCompletionRates).toHaveBeenCalledWith('course-completion-no-fallback-date');
    expect(analytics.completionRate).toBe(50);
    expect(analytics.completionTrend).toEqual([
      {
        date: '2026-06-10T00:00:00.000Z',
        completions: 2,
        cumulative: 4,
      },
    ]);
  });

  it('does not invent revenue tiers, sources, or trend dates', async () => {
    mocks.getCoursesAnalyticsRevenue.mockResolvedValue({
      ok: true,
      data: {
        totalRevenue: 120,
        totalPurchases: 3,
        revenueChart: [
          { revenue: 20, purchases: 1 },
          { date: '2026-06-11T00:00:00.000Z', revenue: 100, purchases: 2 },
        ],
      },
    });

    const analytics = await getCourseRevenueAnalytics('course-revenue-no-fake-breakdown');

    expect(mocks.getCoursesAnalyticsRevenue).toHaveBeenCalledWith('course-revenue-no-fake-breakdown');
    expect(analytics.totalRevenue).toBe(120);
    expect(analytics.avgTransactionValue).toBe(40);
    expect(analytics.revenueByTier).toEqual([]);
    expect(analytics.revenueBySource).toEqual([]);
    expect(analytics.revenueTrend).toEqual([
      {
        date: '2026-06-11T00:00:00.000Z',
        revenue: 100,
        transactions: 2,
      },
    ]);
  });
});
