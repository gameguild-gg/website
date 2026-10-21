import { Link } from '@/i18n/navigation';
import { getGradingScenarioLabAccess } from '@/lib/grading-scenarios/access.server';
import { getInstructorStats, getRecentActivity } from '@/lib/learning';
import { buttonVariants } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Activity, BookOpen, FlaskConical, Star, TrendingUp, Users } from 'lucide-react';
import React from 'react';

function formatActivityType(type: 'enrollment' | 'completion' | 'review' | 'comment' | 'activity'): string {
  switch (type) {
    case 'enrollment':
      return 'enrolled in';
    case 'completion':
      return 'completed';
    case 'review':
      return 'reviewed';
    case 'comment':
      return 'commented on';
    case 'activity':
      return 'was active in';
  }
}

export default async function Page({ params }: PageProps<'/[locale]/workspace/learning/overview'>): Promise<React.JSX.Element> {
  const { locale } = await params;

  const [stats, activity, validationLabAccess] = await Promise.all([
    getInstructorStats(),
    getRecentActivity(),
    getGradingScenarioLabAccess(),
  ]);

  const totalCourses = stats.courses.length;
  const totalEnrollments = stats.courses.reduce((acc, course) => acc + course.enrolledCount, 0);
  const completionSamples = stats.courses
    .map((course) => course.completionPercent)
    .filter((completionPercent): completionPercent is number => completionPercent !== null);
  const avgCompletionRate =
    completionSamples.length > 0
      ? completionSamples.reduce((acc, completionPercent) => acc + completionPercent, 0) / completionSamples.length
      : null;
  const totalRatings = stats.courses.reduce((acc, course) => acc + course.totalRatings, 0);
  const avgRating =
    totalRatings > 0
      ? stats.courses.reduce((acc, course) => acc + ((course.averageRating ?? 0) * course.totalRatings), 0) / totalRatings
      : null;

  const kpis = [
    { label: 'Total Courses', value: totalCourses, icon: BookOpen, description: 'Courses you manage' },
    { label: 'Active Enrollments', value: totalEnrollments, icon: Users, description: 'Current learners across managed courses' },
    { label: 'Avg. Completion', value: avgCompletionRate !== null ? `${Math.round(avgCompletionRate)}%` : '—', icon: TrendingUp, description: 'Average course completion rate' },
    { label: 'Avg. Rating', value: avgRating !== null ? avgRating.toFixed(1) : '—', icon: Star, description: 'Weighted average course rating' },
  ];

  return (
    <div className="flex flex-col gap-6 p-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold tracking-tight">Learning Overview</h1>
          <p className="text-muted-foreground">Your instructor dashboard — courses, students, and performance.</p>
        </div>
        {validationLabAccess.allowed ? (
          <Link
            href="/workspace/learning/grading-validation"
            locale={locale}
            className={buttonVariants({ variant: 'outline' })}
          >
            <FlaskConical className="size-4" />
            Grading validation
          </Link>
        ) : null}
      </div>

      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {kpis.map((kpi) => (
          <Card key={kpi.label}>
            <CardHeader className="flex flex-row items-center justify-between pb-2">
              <CardTitle className="text-sm font-medium">{kpi.label}</CardTitle>
              <kpi.icon className="size-4 text-muted-foreground" />
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{kpi.value}</div>
              <CardDescription>{kpi.description}</CardDescription>
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Activity className="size-5" />
            Recent Activity
          </CardTitle>
          <CardDescription>Latest enrollments, completions, and reviews</CardDescription>
        </CardHeader>
        <CardContent>
          {activity.activities.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              No recent activity. Activity will appear here as students interact with your courses.
            </p>
          ) : (
            <div className="space-y-3">
              {activity.activities.map((item, i) => (
                <div key={i} className="flex items-center justify-between rounded-md border p-3">
                  <div>
                    <span className="font-medium">{item.studentName}</span>
                    <span className="text-muted-foreground"> — {formatActivityType(item.type)} </span>
                    <span className="font-medium">{item.courseName}</span>
                  </div>
                  <span className="text-xs text-muted-foreground">{new Date(item.timestamp).toLocaleDateString()}</span>
                </div>
              ))}
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
