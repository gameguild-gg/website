import { getCourseRouteParam } from '@/lib/learning/course-route';
import { getCourse, getCourseAccessCapabilities, getCourseAnalytics, getCourseCohorts, getCourseContent, getCourseStudents } from '@/lib/learning';
import { forbidden, notFound } from 'next/navigation';
import React from 'react';
import { CourseNav } from '@/components/learning/console/courses/[course]/course-nav';

/**
 * Course Detail Layout
 *
 * Shared layout with sidebar navigation for all course subroutes.
 * Uses Parallel Data Preload Pattern for optimal performance.
 */
export default async function Layout({ children, params }: LayoutProps<'/[locale]/workspace/learning/courses/[course]'>): Promise<React.JSX.Element> {
  const { locale, course: courseIdentifier } = await params;
  const course = await getCourse(courseIdentifier);

  if (!course) {
    notFound();
  }

  const courseId = course.id;
  const courseRouteParam = getCourseRouteParam(course);
  const access = await getCourseAccessCapabilities(courseId);

  if (!access.canAccessWorkspace) {
    forbidden();
  }

  // Preload only data the current contextual capabilities may read.
  if (access.canReviewAsStaff) {
    getCourseAnalytics(courseId);
    getCourseStudents(courseId);
  }

  if (access.canEdit) {
    getCourseContent(courseId);
  }

  if (access.canEdit && course.features.hasClasses) {
    getCourseCohorts(courseId);
  }

  return (
    <CourseNav
      courseTitle={course.title}
      courseDescription={course.description}
      courseStatus={course.status}
      courseSlug={course.slug}
      courseRouteParam={courseRouteParam}
      locale={locale}
      features={course.features}
      access={access}
    >
      {children}
    </CourseNav>
  );
}
