import { Link } from '@/i18n/navigation';
import { buildDashboardCoursePath, getCourseRouteParam } from '@/lib/learning/course-route';
import { getCourse, getCourseContent } from '@/lib/learning';
import {
  deriveCourseLaunchSummary,
  formatDurationLabel,
  type AcademyState,
  type CourseReadinessState,
  type StorefrontState,
} from '@/lib/learning/course-launch';
import { Badge } from '@game-guild/ui/components/badge';
import { buttonVariants } from '@game-guild/ui/components/button-variants';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@game-guild/ui/components/card';
import { AlertCircle, BookOpen, Edit, Globe, ImageIcon, Images, Rocket, Shield, Users } from 'lucide-react';
import { notFound } from 'next/navigation';
import React from 'react';
import { ListingLaunchForm } from '@/components/learning/console/courses/[course]/listing/listing-launch-form';

const storefrontStateMeta: Record<StorefrontState, { label: string; description: string; className: string }> = {
  hidden: {
    label: 'Hidden',
    description: 'The course is not public yet.',
    className: 'border-slate-600 bg-slate-100 text-slate-700 dark:bg-slate-900 dark:text-slate-200',
  },
  teaser: {
    label: 'Teaser',
    description: 'Public preview without open enrollment.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  'enrollment-open': {
    label: 'Enrollment Open',
    description: 'Catalog visitors can enroll right now.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
  'enrollment-closed': {
    label: 'Enrollment Closed',
    description: 'The course is public but enrollments are closed.',
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
    description: 'Publishing is ahead of the current delivery readiness.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  live: {
    label: 'Live',
    description: 'The academy layer is ready for enrolled learners.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
};

const readinessStateMeta: Record<CourseReadinessState, { label: string; description: string; className: string }> = {
  incomplete: {
    label: 'Incomplete',
    description: 'Core launch requirements are still missing.',
    className: 'border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-300',
  },
  'storefront-ready': {
    label: 'Storefront Ready',
    description: 'Catalog-facing essentials are ready.',
    className: 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900 dark:bg-blue-950 dark:text-blue-300',
  },
  'academy-ready': {
    label: 'Academy Ready',
    description: 'Catalog and delivery essentials are configured.',
    className: 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-300',
  },
  live: {
    label: 'Live',
    description: 'The current contract is ready across all defined surfaces.',
    className: 'border-green-200 bg-green-50 text-green-700 dark:border-green-900 dark:bg-green-950 dark:text-green-300',
  },
};

function formatDateTime(value: string | null): string {
  if (!value) {
    return 'Not scheduled';
  }

  return new Date(value).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
  });
}

export default async function ListingPage({
  params,
}: PageProps<'/[locale]/workspace/learning/courses/[course]/listing'>): Promise<React.JSX.Element> {
  const { locale, course: courseId } = await params;

  const [course, content] = await Promise.all([getCourse(courseId), getCourseContent(courseId)]);

  if (!course) {
    notFound();
  }

  const summary = deriveCourseLaunchSummary(course, content);
  const courseRouteParam = getCourseRouteParam(course);

  return (
    <div className="grid gap-6 xl:grid-cols-[minmax(0,1.1fr)_minmax(320px,0.9fr)]">
      <div className="space-y-6">
        <Card>
          <CardHeader>
            <CardTitle>Listing State</CardTitle>
            <CardDescription>
              This is the public-facing control surface for visibility and enrollment on the current API contract.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg border p-4">
                <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <Globe className="size-4" />
                  Storefront
                </div>
                <Badge variant="outline" className={storefrontStateMeta[summary.storefrontState].className}>
                  {storefrontStateMeta[summary.storefrontState].label}
                </Badge>
                <p className="mt-3 text-sm text-muted-foreground">{storefrontStateMeta[summary.storefrontState].description}</p>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <Rocket className="size-4" />
                  Academy
                </div>
                <Badge variant="outline" className={academyStateMeta[summary.academyState].className}>
                  {academyStateMeta[summary.academyState].label}
                </Badge>
                <p className="mt-3 text-sm text-muted-foreground">{academyStateMeta[summary.academyState].description}</p>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-3 flex items-center gap-2 text-sm font-medium text-muted-foreground">
                  <BookOpen className="size-4" />
                  Readiness
                </div>
                <Badge variant="outline" className={readinessStateMeta[summary.readinessState].className}>
                  {readinessStateMeta[summary.readinessState].label}
                </Badge>
                <p className="mt-3 text-sm text-muted-foreground">{readinessStateMeta[summary.readinessState].description}</p>
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg border p-4">
                <p className="text-sm font-medium">Enrollment deadline</p>
                <p className="mt-2 text-sm text-muted-foreground">{formatDateTime(course.enrollmentDeadline)}</p>
              </div>
              <div className="rounded-lg border p-4">
                <p className="text-sm font-medium">Enrollment cap</p>
                <p className="mt-2 text-sm text-muted-foreground">
                  {course.maxEnrollments ? `${course.currentEnrollments}/${course.maxEnrollments} seats used` : 'Unlimited seats'}
                </p>
              </div>
              <div className="rounded-lg border p-4">
                <p className="text-sm font-medium">Delivery footprint</p>
                <p className="mt-2 text-sm text-muted-foreground">{summary.structure.modules} modules, {summary.structure.lessons} lessons, {formatDurationLabel(summary.structure.totalDurationMinutes)}</p>
              </div>
            </div>

            {summary.blockers.length > 0 ? (
              <div className="rounded-lg border border-dashed p-4">
                <p className="mb-3 text-sm font-medium">Still blocking launch</p>
                <ul className="space-y-2 text-sm text-muted-foreground">
                  {summary.blockers.map((blocker) => (
                    <li key={blocker} className="flex items-start gap-2">
                      <AlertCircle className="mt-0.5 size-4 shrink-0 text-amber-500" />
                      <span>{blocker}</span>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Listing Assets</CardTitle>
            <CardDescription>
              Keep merchandising content and media close to the launch controls.
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-4">
            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg border p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <Edit className="size-4" />
                  Course identity
                </div>
                <p className="text-sm text-muted-foreground">Title, slug, description, category, difficulty, and outcomes.</p>
                <Link
                  href={buildDashboardCoursePath(courseRouteParam, 'listing/info')}
                  locale={locale}
                  className={buttonVariants({ variant: 'outline', className: 'mt-4 w-full justify-start' })}
                >
                  Open identity editor
                </Link>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <ImageIcon className="size-4" />
                  Media
                </div>
                <p className="text-sm text-muted-foreground">Cover image and promo video used across catalog and landing pages.</p>
                <Link
                  href={buildDashboardCoursePath(courseRouteParam, 'listing/media')}
                  locale={locale}
                  className={buttonVariants({ variant: 'outline', className: 'mt-4 w-full justify-start' })}
                >
                  Open media editor
                </Link>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <Images className="size-4" />
                  Project carousel
                </div>
                <p className="text-sm text-muted-foreground">Portfolio project slides shown on the public course landing page.</p>
                <Link
                  href={buildDashboardCoursePath(courseRouteParam, 'listing/projects')}
                  locale={locale}
                  className={buttonVariants({ variant: 'outline', className: 'mt-4 w-full justify-start' })}
                >
                  Open project editor
                </Link>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <Shield className="size-4" />
                  Access and enrollment
                </div>
                <p className="text-sm text-muted-foreground">Visibility, enrollment status, seat cap, and enrollment deadline.</p>
                <Link
                  href={buildDashboardCoursePath(courseRouteParam, 'listing/access')}
                  locale={locale}
                  className={buttonVariants({ variant: 'outline', className: 'mt-4 w-full justify-start' })}
                >
                  Open access controls
                </Link>
              </div>
            </div>

            <div className="grid gap-4 md:grid-cols-3">
              <div className="rounded-lg border p-4">
                <p className="text-sm font-medium">Slug</p>
                <p className="mt-2 break-all text-sm text-muted-foreground">/{course.slug || 'slug-missing'}</p>
              </div>
              <div className="rounded-lg border p-4">
                <p className="text-sm font-medium">Catalog status</p>
                <p className="mt-2 text-sm text-muted-foreground">{course.visibility} visibility with {course.enrollmentStatus} enrollment.</p>
              </div>
              <div className="rounded-lg border p-4">
                <div className="mb-2 flex items-center gap-2 text-sm font-medium">
                  <Users className="size-4" />
                  Enrollments
                </div>
                <p className="text-sm text-muted-foreground">{course.currentEnrollments} learners currently attached to this course.</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <ListingLaunchForm course={course} />
    </div>
  );
}
