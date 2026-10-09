import { getCourse, getCourseAccessCapabilities, getCourseAssessments } from '@/lib/learning';
import { forbidden, notFound } from 'next/navigation';
import React from 'react';

/**
 * Assessments Layout
 *
 * Routes:
 * - /assessments - Assessment list
 * - /assessments/[assessmentId] - Assessment editor
 *
 * Condition: course.features.hasAssessments = true
 */
export default async function AssessmentsLayout({
  children,
  params,
}: LayoutProps<'/[locale]/workspace/learning/courses/[course]/assessments'>): Promise<React.JSX.Element> {
  const { course: courseId } = await params;

  const [course, access] = await Promise.all([
    getCourse(courseId),
    getCourseAccessCapabilities(courseId),
  ]);

  if (!course || !course.features.hasAssessments) {
    notFound();
  }

  if (!access.canEdit && !access.canReviewAsStaff) {
    forbidden();
  }

  // Preload assessments
  getCourseAssessments(courseId);

  return <>{children}</>;
}
