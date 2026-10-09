import React from 'react';
import { getCourseAccessCapabilities, getCourseAssessmentAnalytics, getCourseAssessmentGroups, getCourseAssessments } from '@/lib/learning';
import { AssessmentsList } from '@/components/learning/console/courses/[course]/assessments/assessments-list';

/**
 * Assessments List Page
 *
 * Route: /courses/[course]/assessments
 * Condition: course.features.hasAssessments = true (checked in layout)
 */
export default async function AssessmentsPage({
  params,
}: PageProps<'/[locale]/workspace/learning/courses/[course]/assessments'>): Promise<React.JSX.Element> {
  const { course: courseId } = await params;

  const [{ assessments, total }, assessmentGroups, analytics, access] = await Promise.all([
    getCourseAssessments(courseId),
    getCourseAssessmentGroups(courseId),
    getCourseAssessmentAnalytics(courseId),
    getCourseAccessCapabilities(courseId),
  ]);

  return (
    <AssessmentsList
      courseId={courseId}
      assessments={assessments}
      total={total}
      assessmentGroups={assessmentGroups}
      analytics={analytics}
      canManage={access.canEdit}
      canReviewAsStaff={access.canReviewAsStaff}
    />
  );
}
