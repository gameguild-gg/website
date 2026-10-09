import React from 'react';
import { Link } from '@/i18n/navigation';
import { ArrowLeft } from 'lucide-react';
import { getAssessment, getAssessmentSubmissions, getCourseAccessCapabilities } from '@/lib/learning';
import { forbidden } from 'next/navigation';
import { SubmissionsList } from '@/components/learning/console/courses/[course]/assessments/[assessmentId]/submissions/submissions-list';

/**
 * Instructor view: list of submissions for an assessment.
 *
 * Route: /[locale]/workspace/learning/courses/[course]/assessments/[assessmentId]/submissions
 *
 * ponytail: simple list render; add server-side pagination when submission count exceeds 200
 */
export default async function AssessmentSubmissionsPage({
  params,
}: {
  params: Promise<{ locale: string; course: string; assessmentSlug: string }>;
}): Promise<React.JSX.Element> {
  const { course, assessmentSlug } = await params;

  const [assessment, access] = await Promise.all([
    getAssessment(course, assessmentSlug),
    getCourseAccessCapabilities(course),
  ]);
  if (!access.canReviewAsStaff) forbidden();
  const submissions = assessment
    ? await getAssessmentSubmissions(assessment.id)
    : [];

  const maxScore = assessment?.maxScore ?? 0;
  const backHref = `/workspace/learning/courses/${course}/assessments/${assessmentSlug}`;

  return (
    <div className="flex flex-col gap-4 p-6">
      <div className="flex items-center gap-2">
        <Link
          href={backHref}
          className="inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeft className="h-4 w-4" />
          Back to assessment
        </Link>
      </div>
      <header>
        <h1 className="text-2xl font-semibold">Submissions</h1>
        {assessment ? (
          <p className="text-sm text-muted-foreground">{assessment.title}</p>
        ) : null}
      </header>
      <SubmissionsList
        courseSlug={course}
        assessmentId={assessment?.id ?? assessmentSlug}
        assessmentSlug={assessmentSlug}
        maxScore={maxScore}
        submissions={submissions}
      />
    </div>
  );
}
