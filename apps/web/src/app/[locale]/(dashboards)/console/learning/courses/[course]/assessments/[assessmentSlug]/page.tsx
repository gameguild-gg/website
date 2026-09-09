import React from "react";
import { notFound } from "next/navigation";
import {
  getAssessment,
  getAssessmentAuthoringState,
  getCourseAssessmentGroups,
  getCourseContent,
} from "@/lib/learning";
import { AssessmentEditor } from "@/components/learning/console/courses/[course]/assessments/[assessmentId]/assessment-editor";

/**
 * Assessment Detail/Editor Page
 *
 * Route: /courses/[course]/assessments/[assessmentId]
 */
export default async function AssessmentDetailPage({
  params,
}: PageProps<"/[locale]/console/learning/courses/[course]/assessments/[assessmentSlug]">): Promise<React.JSX.Element> {
  const { course: courseId, assessmentSlug } = await params;

  const [assessment, assessmentGroups, courseContent] = await Promise.all([
    getAssessment(courseId, assessmentSlug),
    getCourseAssessmentGroups(courseId),
    getCourseContent(courseId),
  ]);

  if (!assessment) {
    notFound();
  }

  const authoringState = assessment.contentId
    ? await getAssessmentAuthoringState(assessment.id)
    : null;

  return (
    <AssessmentEditor
      courseId={courseId}
      assessment={assessment}
      authoringState={authoringState}
      assessmentGroups={assessmentGroups}
      courseContent={courseContent.items}
    />
  );
}
