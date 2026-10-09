import React from 'react';
import { forbidden, notFound } from 'next/navigation';
import type { LearningAssessmentsGradingQueue } from '@game-guild/client';
import { Link } from '@/i18n/navigation';
import { fetchGradingQueue } from './grading-queue';
import { SpeedgraderWorkspace } from './speedgrader-workspace';
import { getCourseAccessCapabilities } from '@/lib/learning';

/**
 * SpeedGrader page: server-fetches the grading queue and lets each panel read
 * the immutable runtime execution for the selected submission.
 *
 * Route: `/[locale]/speedgrader/assessments/[assessmentId]?course=<slug>&nav=<index>`
 *
 * The route has no course segment, so the back link's `courseSlug` arrives via
 * the `?course=` searchParam.
 */
export default async function SpeedgraderAssessmentPage({
  params,
  searchParams,
}: {
  params: Promise<{ locale: string; assessmentId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}): Promise<React.JSX.Element> {
  const { assessmentId } = await params;
  const query = await searchParams;

  const courseParam = typeof query.course === 'string' ? query.course : undefined;
  if (!courseParam) {
    notFound();
  }

  const access = await getCourseAccessCapabilities(courseParam);
  if (!access.canReviewAsStaff) {
    forbidden();
  }

  const queue = await fetchGradingQueue(assessmentId);
  if (!queue.ok) {
    if (queue.status === 404) {
      notFound();
    }
    return (
      <main className="grid flex-1 place-items-center p-6">
        <div data-testid="speedgrader-error" className="max-w-md text-center">
          <h1 className="text-lg font-semibold text-foreground">Grading queue unavailable</h1>
          <p className="mt-2 text-sm text-muted-foreground">{queue.message}</p>
          <Link
            href={`/dashboard/learning/courses/${courseParam}/assessments/${assessmentId}/submissions`}
            className="mt-4 inline-block text-sm text-primary underline-offset-4 hover:underline"
          >
            Back to submissions
          </Link>
        </div>
      </main>
    );
  }

  const data = queue.data;
  const requestedSubmission =
    typeof query.submission === 'string' ? query.submission : null;
  const submissionIndex = requestedSubmission
    ? data.items?.findIndex((item) => item.submissionId === requestedSubmission) ?? -1
    : -1;
  const navParam =
    typeof query.nav === 'string' ? Number.parseInt(query.nav, 10) : Number.NaN;
  const initialIndex =
    submissionIndex >= 0 ? submissionIndex : Number.isFinite(navParam) ? navParam : 0;

  return (
    <SpeedgraderWorkspace
      // Cast: workspace props use the generated type, whose `rubric` field
      // wrongly excludes null (the API sends null for rubric-less
      // assessments) — grading-queue.ts holds the accurate contract.
      queue={data as LearningAssessmentsGradingQueue}
      assessmentId={assessmentId}
      courseSlug={courseParam}
      initialIndex={initialIndex}
    />
  );
}
