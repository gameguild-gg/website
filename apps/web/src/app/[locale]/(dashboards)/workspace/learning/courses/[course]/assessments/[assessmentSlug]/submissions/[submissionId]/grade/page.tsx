import { forbidden, notFound } from 'next/navigation';
import { redirect } from '@/i18n/navigation';
import { getAssessment, getCourseAccessCapabilities } from '@/lib/learning';

/** Redirects the former grading page to the runtime-backed SpeedGrader. */
export default async function GradeSubmissionPage({
  params,
}: {
  params: Promise<{
    locale: string;
    course: string;
    assessmentSlug: string;
    submissionId: string;
  }>;
}): Promise<void> {
  const { locale, course, assessmentSlug, submissionId } = await params;
  const [assessment, access] = await Promise.all([
    getAssessment(course, assessmentSlug),
    getCourseAccessCapabilities(course),
  ]);
  if (!assessment) notFound();
  if (!access.canReviewAsStaff) forbidden();

  redirect({
    href: `/speedgrader/assessments/${assessment.id}?course=${encodeURIComponent(course)}&submission=${encodeURIComponent(submissionId)}`,
    locale,
  });
}
