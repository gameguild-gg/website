import { Link } from '@/i18n/navigation';
import { buildDashboardCoursePath, getCourseRouteParam } from '@/lib/learning/course-route';
import {
  getCourse,
  getCourseAnalytics,
  getCourseCompletionAnalytics,
  getCourseContent,
  getCourseEngagementAnalytics,
  getCourseRevenueAnalytics,
} from '@/lib/learning';
import {
  deriveCourseLaunchSummary,
  formatDurationLabel,
  type AcademyState,
  type CourseReadinessState,
  type StorefrontState,
} from '@/lib/learning/course-launch';
import { Badge } from '@game-guild/ui/components/badge';
import { buttonVariants } from '@game-guild/ui/components/button-variants';
import { Card, CardContent, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { Progress } from '@game-guild/ui/components/progress';
import {
  AlertCircle,
  Activity,
  BookOpen,
  CheckCircle2,
  ClipboardList,
  Clock,
  Edit,
  FileText,
  Globe,
  Image,
  Layers3,
  Rocket,
  Settings,
  Star,
  TrendingUp,
  Users,
} from 'lucide-react';
import { notFound } from 'next/navigation';
import React from 'react';

function formatDate(dateString: string) {
  return new Date(dateString).toLocaleDateString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  });
}

const storefrontStateMeta: Record<StorefrontState, { label: string; description: string; className: string }> = {
  hidden: {
    label: 'Hidden',
    description: 'Not visible in the public catalog yet.',
    className: 'border-slate-600 bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200',
  },
  teaser: {
    label: 'Teaser',
    description: 'Previewable without open enrollment.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  'enrollment-open': {
    label: 'Enrollment Open',
    description: 'Students can discover and enroll now.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
  'enrollment-closed': {
    label: 'Enrollment Closed',
    description: 'Course is public but no longer accepting enrollments.',
    className: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-300',
  },
};

const academyStateMeta: Record<AcademyState, { label: string; description: string; className: string }> = {
  hidden: {
    label: 'Hidden',
    description: 'Learner delivery is not available yet.',
    className: 'border-slate-600 bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200',
  },
  scheduled: {
    label: 'Scheduled',
    description: 'Published, but still blocked by missing delivery setup.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  live: {
    label: 'Live',
    description: 'Learners can consume the course content now.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
};

const readinessStateMeta: Record<CourseReadinessState, { label: string; description: string; className: string }> = {
  incomplete: {
    label: 'Incomplete',
    description: 'Core catalog and delivery requirements are still missing.',
    className: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-300',
  },
  'storefront-ready': {
    label: 'Storefront Ready',
    description: 'Catalog essentials are set, but delivery is not ready yet.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  'academy-ready': {
    label: 'Academy Ready',
    description: 'Catalog and delivery requirements are configured.',
    className: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  },
  live: {
    label: 'Live',
    description: 'Published and ready across both catalog and learner delivery.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
};

export default async function Page({ params }: PageProps<'/[locale]/workspace/learning/courses/[course]/overview'>): Promise<React.JSX.Element> {
  const { locale, course: courseIdentifier } = await params;

  const course = await getCourse(courseIdentifier);

  if (!course) {
    notFound();
  }

  const courseId = course.id;
  const courseRouteParam = getCourseRouteParam(course);
  const [analytics, content, completionAnalytics, engagementAnalytics, revenueAnalytics] = await Promise.all([
    getCourseAnalytics(courseId),
    getCourseContent(courseId),
    getCourseCompletionAnalytics(courseId),
    getCourseEngagementAnalytics(courseId),
    course.features.hasPricing ? getCourseRevenueAnalytics(courseId) : Promise.resolve(null),
  ]);

  const totalEnrollments = analytics.totalUsers || course.currentEnrollments;
  const completedCount = analytics.completedUsers;
  const completionRate = Math.round(analytics.completionRate);
  const avgRating = course.totalRatings > 0 ? course.averageRating.toFixed(1) : null;

  const launchSummary = deriveCourseLaunchSummary(course, content);
  const modulesCount = launchSummary.structure.modules;
  const durationStr = formatDurationLabel(launchSummary.structure.totalDurationMinutes);

  // Readiness checklist items
  const readinessChecks = launchSummary.checks.map((check) => ({
    ...check,
    href:
      check.key === 'thumbnail'
        ? buildDashboardCoursePath(courseRouteParam, 'listing/media')
        : check.key === 'module' || check.key === 'lesson'
          ? buildDashboardCoursePath(courseRouteParam, 'content')
          : buildDashboardCoursePath(courseRouteParam, 'listing/info'),
    icon:
      check.key === 'thumbnail'
        ? Image
        : check.key === 'module'
          ? BookOpen
          : check.key === 'lesson'
            ? ClipboardList
            : FileText,
  }));
  const readinessDone = readinessChecks.filter((c) => c.done).length;
  const readinessTotal = readinessChecks.length;
  const readinessPercent = Math.round((readinessDone / readinessTotal) * 100);

  return (
    <div className="flex flex-col gap-6">
      {/* Key Metrics */}
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardContent className="flex items-center gap-4 p-4">
            <div className="flex size-10 items-center justify-center rounded-lg bg-blue-100 dark:bg-blue-900">
              <Users className="size-5 text-blue-600 dark:text-blue-400" />
            </div>
            <div>
              <p className="text-2xl font-bold">{totalEnrollments}</p>
              <p className="text-sm text-muted-foreground">Enrolled</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-4 p-4">
            <div className="flex size-10 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900">
              <CheckCircle2 className="size-5 text-green-600 dark:text-green-400" />
            </div>
            <div>
              <p className="text-2xl font-bold">{completionRate}%</p>
              <p className="text-sm text-muted-foreground">Completion Rate</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-4 p-4">
            <div className="flex size-10 items-center justify-center rounded-lg bg-purple-100 dark:bg-purple-900">
              <BookOpen className="size-5 text-purple-600 dark:text-purple-400" />
            </div>
            <div>
              <p className="text-2xl font-bold">{modulesCount}</p>
              <p className="text-sm text-muted-foreground">Modules</p>
            </div>
          </CardContent>
        </Card>
        <Card>
          <CardContent className="flex items-center gap-4 p-4">
            <div className="flex size-10 items-center justify-center rounded-lg bg-orange-100 dark:bg-orange-900">
              <Clock className="size-5 text-orange-600 dark:text-orange-400" />
            </div>
            <div>
              <p className="text-2xl font-bold">{durationStr}</p>
              <p className="text-sm text-muted-foreground">Duration</p>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Main Content */}
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader>
              <CardTitle>Launch Control</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 md:grid-cols-3">
                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                    <Globe className="size-4" />
                    Storefront
                  </div>
                  <Badge variant="outline" className={storefrontStateMeta[launchSummary.storefrontState].className}>
                    {storefrontStateMeta[launchSummary.storefrontState].label}
                  </Badge>
                  <p className="mt-3 text-sm text-muted-foreground">{storefrontStateMeta[launchSummary.storefrontState].description}</p>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                    <Rocket className="size-4" />
                    Academy
                  </div>
                  <Badge variant="outline" className={academyStateMeta[launchSummary.academyState].className}>
                    {academyStateMeta[launchSummary.academyState].label}
                  </Badge>
                  <p className="mt-3 text-sm text-muted-foreground">{academyStateMeta[launchSummary.academyState].description}</p>
                </div>
                <div className="rounded-lg border p-4">
                  <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                    <Layers3 className="size-4" />
                    Readiness
                  </div>
                  <Badge variant="outline" className={readinessStateMeta[launchSummary.readinessState].className}>
                    {readinessStateMeta[launchSummary.readinessState].label}
                  </Badge>
                  <p className="mt-3 text-sm text-muted-foreground">{readinessStateMeta[launchSummary.readinessState].description}</p>
                </div>
              </div>

              {launchSummary.blockers.length > 0 ? (
                <div className="rounded-lg border border-dashed p-4">
                  <p className="mb-3 text-sm font-medium">Current launch blockers</p>
                  <ul className="space-y-2 text-sm text-muted-foreground">
                    {launchSummary.blockers.map((blocker) => (
                      <li key={blocker} className="flex items-start gap-2">
                        <AlertCircle className="mt-0.5 size-4 shrink-0 text-amber-500" />
                        <span>{blocker}</span>
                      </li>
                    ))}
                  </ul>
                </div>
              ) : (
                <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300">
                  No launch blockers remain on the current dashboard contract.
                </div>
              )}
            </CardContent>
          </Card>

          {/* Course Readiness */}
          <Card>
            <CardHeader>
              <div className="flex items-center justify-between">
                <CardTitle>Course Readiness</CardTitle>
                <Badge variant={readinessPercent === 100 ? 'default' : 'secondary'}>
                  {readinessDone}/{readinessTotal} complete
                </Badge>
              </div>
            </CardHeader>
            <CardContent className="space-y-4">
              <Progress value={readinessPercent} className="h-2" />
              <div className="space-y-2">
                {readinessChecks.map((check) => {
                  const Icon = check.icon;
                  return (
                    <Link
                      key={check.label}
                      href={check.href}
                      locale={locale}
                      prefetch={false}
                      className="flex items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-muted/50"
                    >
                      {check.done ? (
                        <CheckCircle2 className="size-5 shrink-0 text-green-500" />
                      ) : (
                        <AlertCircle className="size-5 shrink-0 text-amber-500" />
                      )}
                      <Icon className="size-4 shrink-0 text-muted-foreground" />
                      <span className={`text-sm ${check.done ? 'text-muted-foreground line-through' : 'font-medium'}`}>
                        {check.label}
                      </span>
                    </Link>
                  );
                })}
              </div>
            </CardContent>
          </Card>

          {/* Course Performance */}
          <Card>
            <CardHeader>
              <CardTitle>Analytics</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid gap-4 sm:grid-cols-3">
                {avgRating ? (
                  <div className="flex items-center gap-3 rounded-lg border p-3">
                    <Star className="size-5 text-yellow-500" />
                    <div>
                      <p className="text-lg font-bold">{avgRating}</p>
                      <p className="text-xs text-muted-foreground">{course.totalRatings} reviews</p>
                    </div>
                  </div>
                ) : (
                  <div className="flex items-center gap-3 rounded-lg border p-3">
                    <Star className="size-5 text-muted-foreground/40" />
                    <div>
                      <p className="text-sm text-muted-foreground">No ratings yet</p>
                    </div>
                  </div>
                )}
                <div className="flex items-center gap-3 rounded-lg border p-3">
                  <TrendingUp className="size-5 text-green-500" />
                  <div>
                    <p className="text-lg font-bold">{completedCount}</p>
                    <p className="text-xs text-muted-foreground">Completed</p>
                  </div>
                </div>
                <div className="flex items-center gap-3 rounded-lg border p-3">
                  <Activity className="size-5 text-purple-500" />
                  <div>
                    <p className="text-lg font-bold">{engagementAnalytics.activeStudents}</p>
                    <p className="text-xs text-muted-foreground">Active students</p>
                  </div>
                </div>
              </div>
              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Completion Rate</span>
                  <span className="font-medium">{completionRate}%</span>
                </div>
                <Progress value={completionRate} className="h-2" />
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border p-3">
                  <p className="text-sm font-medium">Completion funnel</p>
                  <div className="mt-3 space-y-2">
                    {completionAnalytics.funnel.map((stage) => (
                      <div key={stage.stage} className="space-y-1">
                        <div className="flex justify-between text-xs text-muted-foreground">
                          <span>{stage.stage}</span>
                          <span>{stage.count} · {Math.round(stage.percentage)}%</span>
                        </div>
                        <Progress value={stage.percentage} className="h-1.5" />
                      </div>
                    ))}
                  </div>
                </div>
                <div className="rounded-lg border p-3">
                  <p className="text-sm font-medium">Engagement</p>
                  <dl className="mt-3 grid grid-cols-2 gap-3 text-sm">
                    <div>
                      <dt className="text-xs text-muted-foreground">Views</dt>
                      <dd className="font-semibold">{engagementAnalytics.totalViews}</dd>
                    </div>
                    <div>
                      <dt className="text-xs text-muted-foreground">Avg. session</dt>
                      <dd className="font-semibold">{Math.round(engagementAnalytics.avgSessionDuration / 60)}m</dd>
                    </div>
                    {revenueAnalytics && (
                      <>
                        <div>
                          <dt className="text-xs text-muted-foreground">Revenue</dt>
                          <dd className="font-semibold">
                            {new Intl.NumberFormat('en-US', { style: 'currency', currency: revenueAnalytics.currency }).format(revenueAnalytics.totalRevenue)}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-xs text-muted-foreground">Transactions</dt>
                          <dd className="font-semibold">{revenueAnalytics.totalTransactions}</dd>
                        </div>
                      </>
                    )}
                  </dl>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Quick Actions */}
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Quick Actions</CardTitle>
            </CardHeader>
            <CardContent className="space-y-2">
              <Link
                href={buildDashboardCoursePath(courseRouteParam, 'listing')}
                locale={locale}
                prefetch={false}
                className={buttonVariants({ variant: 'outline', className: 'w-full justify-start' })}
              >
                  <Edit className="mr-2 size-4" />
                  Open Listing Controls
              </Link>
              <Link
                href={buildDashboardCoursePath(courseRouteParam, 'content')}
                locale={locale}
                prefetch={false}
                className={buttonVariants({ variant: 'outline', className: 'w-full justify-start' })}
              >
                  <BookOpen className="mr-2 size-4" />
                  Manage Content
              </Link>
              <Link
                href={buildDashboardCoursePath(courseRouteParam, 'students')}
                locale={locale}
                prefetch={false}
                className={buttonVariants({ variant: 'outline', className: 'w-full justify-start' })}
              >
                  <Users className="mr-2 size-4" />
                  Manage Students
              </Link>
              <Link
                href={buildDashboardCoursePath(courseRouteParam, 'settings')}
                locale={locale}
                prefetch={false}
                className={buttonVariants({ variant: 'outline', className: 'w-full justify-start' })}
              >
                  <Settings className="mr-2 size-4" />
                  Course Settings
              </Link>
            </CardContent>
          </Card>

          {/* Course Details */}
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">Details</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3 text-sm">
              <div className="flex justify-between">
                <span className="text-muted-foreground">Status</span>
                <Badge variant="outline" className="capitalize">{course.status}</Badge>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Category</span>
                <Badge variant="outline">{course.category}</Badge>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Difficulty</span>
                <Badge variant="outline">{course.difficulty}</Badge>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Enrollment</span>
                <Badge variant="outline">{course.enrollmentStatus}</Badge>
              </div>
              <div className="flex justify-between gap-3">
                <span className="text-muted-foreground">Storefront</span>
                <Badge variant="outline" className={storefrontStateMeta[launchSummary.storefrontState].className}>
                  {storefrontStateMeta[launchSummary.storefrontState].label}
                </Badge>
              </div>
              <div className="flex justify-between gap-3">
                <span className="text-muted-foreground">Academy</span>
                <Badge variant="outline" className={academyStateMeta[launchSummary.academyState].className}>
                  {academyStateMeta[launchSummary.academyState].label}
                </Badge>
              </div>
              {course.estimatedHours && (
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Est. Hours</span>
                  <span>{course.estimatedHours}h</span>
                </div>
              )}
              {course.enrollmentDeadline && (
                <div className="flex justify-between gap-3">
                  <span className="text-muted-foreground">Enrollment Deadline</span>
                  <span>{formatDate(course.enrollmentDeadline)}</span>
                </div>
              )}
              <div className="border-t pt-3">
                <div className="flex justify-between">
                  <span className="text-muted-foreground">Created</span>
                  <span>{formatDate(course.createdAt)}</span>
                </div>
              </div>
              <div className="flex justify-between">
                <span className="text-muted-foreground">Last Updated</span>
                <span>{formatDate(course.updatedAt)}</span>
              </div>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
