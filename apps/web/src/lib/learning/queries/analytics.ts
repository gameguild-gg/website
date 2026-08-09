import { getToken } from '@/auth';
import { createServerClient, GeneratedApi } from '@game-guild/client';
import { cache } from 'react';
import { getCourseContent, resolveCourseId } from './course';

// =============================================================================
// COURSE ANALYTICS QUERIES
// =============================================================================
// Detailed analytics beyond the overview dashboard.
// =============================================================================

/**
 * Engagement metrics
 */
export interface CourseEngagementAnalytics {
  courseId: string;
  period: { from: string; to: string };
  
  // Activity metrics
  activeStudents: number;
  totalViews: number;
  avgSessionDuration: number;  // seconds
  
  // Content engagement
  contentViews: Array<{
    contentId: string;
    contentTitle: string;
    views: number;
    avgWatchTime: number;
    completionRate: number;
  }>;
  
  // Time-based data
  dailyActivity: Array<{
    date: string;
    activeUsers: number;
    contentViews: number;
    completions: number;
  }>;
  
  // Peak hours (0-23)
  peakHours: Array<{
    hour: number;
    activity: number;
  }>;
}

/**
 * Completion analytics
 */
export interface CourseCompletionAnalytics {
  courseId: string;
  period: { from: string; to: string };
  
  // Overall completion
  totalEnrolled: number;
  totalCompleted: number;
  completionRate: number;
  avgCompletionTime: number;   // days
  
  // Drop-off analysis
  dropOffPoints: Array<{
    contentId: string;
    contentTitle: string;
    startedCount: number;
    completedCount: number;
    dropOffRate: number;
  }>;
  
  // Completion funnel
  funnel: Array<{
    stage: string;
    count: number;
    percentage: number;
  }>;
  
  // Completion over time
  completionTrend: Array<{
    date: string;
    completions: number;
    cumulative: number;
  }>;
}

/**
 * Revenue analytics (conditional: hasPricing)
 */
export interface CourseRevenueAnalytics {
  courseId: string;
  period: { from: string; to: string };
  currency: string;
  
  // Summary
  totalRevenue: number;
  totalTransactions: number;
  avgTransactionValue: number;
  refundRate: number;
  
  // By tier
  revenueByTier: Array<{
    tierId: string;
    tierName: string;
    revenue: number;
    count: number;
  }>;
  
  // By source
  revenueBySource: Array<{
    source: string;           // direct, affiliate, coupon, etc.
    revenue: number;
    count: number;
  }>;
  
  // Over time
  revenueTrend: Array<{
    date: string;
    revenue: number;
    transactions: number;
  }>;
  
  // Top discounts
  discountUsage: Array<{
    code: string;
    timesUsed: number;
    revenueImpact: number;    // Negative = discount amount
  }>;
}

// =============================================================================
// FETCH FUNCTIONS
// =============================================================================

function createProgramModule() {
  const apiUrl = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
  const client = createServerClient({
    baseUrl: apiUrl,
    auth: { getAccessToken: () => getToken() },
  });

  return new GeneratedApi.LearningCoursesProgramModule(client);
}

function parseDurationToSeconds(value?: string | null): number {
  if (!value) return 0;
  const parts = value.split(':').map((part) => Number(part));
  if (parts.length === 3 && parts.every(Number.isFinite)) {
    return parts[0] * 3600 + parts[1] * 60 + parts[2];
  }

  return 0;
}

/**
 * Fetch engagement analytics.
 * Cache: revalidate 300s (computed, expensive)
 */
export const getCourseEngagementAnalytics = cache(async (
  courseId: string,
  period?: { from: string; to: string }
): Promise<CourseEngagementAnalytics> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const [engagementResult, content] = await Promise.all([
    createProgramModule().getCoursesAnalyticsEngagement(resolvedCourseId),
    getCourseContent(resolvedCourseId),
  ]);
  const metrics = engagementResult.ok ? engagementResult.data : undefined;

  const now = new Date();
  const defaultPeriod = {
    from: new Date(now.getFullYear(), now.getMonth(), now.getDate() - 6).toISOString(),
    to: now.toISOString(),
  };
  const contentById = new Map(content.items.map((item) => [item.id, item]));
  const contentViews = Object.entries(metrics?.contentEngagement ?? {}).map(([contentId, views]) => ({
    contentId,
    contentTitle: contentById.get(contentId)?.title ?? contentId,
    views,
    avgWatchTime: 0,
    completionRate: 0,
  }));

  return {
    courseId: resolvedCourseId,
    period: period ?? defaultPeriod,
    activeStudents: metrics?.weeklyActiveUsers ?? metrics?.dailyActiveUsers ?? 0,
    totalViews: metrics?.totalSessions ?? 0,
    avgSessionDuration: parseDurationToSeconds(metrics?.averageSessionDuration),
    contentViews,
    dailyActivity: [],
    peakHours: [],
  };
});

/**
 * Fetch completion analytics.
 * Cache: revalidate 300s (computed, expensive)
 */
export const getCourseCompletionAnalytics = cache(async (
  courseId: string,
  period?: { from: string; to: string }
): Promise<CourseCompletionAnalytics> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const [completionResult, content] = await Promise.all([
    createProgramModule().getCoursesAnalyticsCompletionRates(resolvedCourseId),
    getCourseContent(resolvedCourseId),
  ]);
  const completion = completionResult.ok ? completionResult.data : undefined;

  const now = new Date();
  const defaultPeriod = {
    from: new Date(now.getFullYear(), now.getMonth(), 1).toISOString(),
    to: now.toISOString(),
  };
  const contentById = new Map(content.items.map((item) => [item.id, item]));
  const dropOffPoints = Object.entries(completion?.contentCompletionRates ?? {}).map(([contentId, rate]) => ({
    contentId,
    contentTitle: contentById.get(contentId)?.title ?? contentId,
    startedCount: 0,
    completedCount: 0,
    dropOffRate: Math.max(0, 100 - rate),
  }));
  const totalCompleted = completion?.completionTrends?.at(-1)?.completedCount ?? 0;
  const totalEnrolled = completion?.completionTrends?.at(-1)?.totalCount ?? 0;

  return {
    courseId: resolvedCourseId,
    period: period ?? defaultPeriod,
    totalEnrolled,
    totalCompleted,
    completionRate: completion?.overallCompletionRate ?? 0,
    avgCompletionTime: 0,
    dropOffPoints,
    funnel: [
      { stage: 'Enrolled', count: totalEnrolled, percentage: 100 },
      { stage: 'Completed', count: totalCompleted, percentage: completion?.overallCompletionRate ?? 0 },
    ],
    completionTrend: (completion?.completionTrends ?? [])
      .filter((trend): trend is { date: string; completedCount?: number; totalCount?: number; rate?: number } => Boolean(trend.date))
      .map((trend) => ({
        date: trend.date,
        completions: trend.completedCount ?? 0,
        cumulative: trend.totalCount ?? 0,
      })),
  };
});

/**
 * Fetch revenue analytics (conditional: hasPricing).
 * Cache: revalidate 300s (computed, expensive)
 */
export const getCourseRevenueAnalytics = cache(async (
  courseId: string,
  period?: { from: string; to: string }
): Promise<CourseRevenueAnalytics> => {
  const resolvedCourseId = await resolveCourseId(courseId);
  const revenueResult = await createProgramModule().getCoursesAnalyticsRevenue(resolvedCourseId);
  const revenue = revenueResult.ok ? revenueResult.data : undefined;

  const now = new Date();
  const defaultPeriod = {
    from: new Date(now.getFullYear(), now.getMonth(), 1).toISOString(),
    to: now.toISOString(),
  };
  const totalRevenue = revenue?.totalRevenue ?? 0;
  const totalTransactions = revenue?.totalPurchases ?? 0;

  return {
    courseId: resolvedCourseId,
    period: period ?? defaultPeriod,
    currency: 'USD',
    totalRevenue,
    totalTransactions,
    avgTransactionValue: totalTransactions > 0 ? totalRevenue / totalTransactions : 0,
    refundRate: 0,
    revenueByTier: [],
    revenueBySource: [],
    revenueTrend: (revenue?.revenueChart ?? [])
      .filter((point): point is { date: string; revenue?: number; purchases?: number } => Boolean(point.date))
      .map((point) => ({
        date: point.date,
        revenue: point.revenue ?? 0,
        transactions: point.purchases ?? 0,
      })),
    discountUsage: [],
  };
});
