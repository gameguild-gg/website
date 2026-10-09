import React from "react";
import { forbidden, notFound } from "next/navigation";
import {
  getAssessment,
  getAssessmentRubric,
  getAssessmentAuthoringState,
  getCourseAccessCapabilities,
  getCourseAssessmentGroups,
  getCourseContent,
  getCourseGroupSets,
} from "@/lib/learning";
import { AssessmentEditor } from "@/components/learning/console/courses/[course]/assessments/[assessmentId]/assessment-editor";

/**
 * Assessment Detail/Editor Page
 *
 * Route: /courses/[course]/assessments/[assessmentId]
 */
export default async function AssessmentDetailPage({
  params,
}: PageProps<"/[locale]/workspace/learning/courses/[course]/assessments/[assessmentSlug]">): Promise<React.JSX.Element> {
  const { course: courseId, assessmentSlug } = await params;

  const [assessment, assessmentGroups, courseContent, groupSets, access] =
    await Promise.all([
      getAssessment(courseId, assessmentSlug),
      getCourseAssessmentGroups(courseId),
      getCourseContent(courseId),
      getCourseGroupSets(courseId),
      getCourseAccessCapabilities(courseId),
    ]);

  if (!assessment) {
    notFound();
  }

  if (!access.canEdit) {
    forbidden();
  }

  const [rubric, authoringState] = await Promise.all([
    getAssessmentRubric(assessment.id),
    assessment.contentId ? getAssessmentAuthoringState(assessment.id) : null,
  ]);

  return (
    <AssessmentEditor
      courseId={courseId}
      assessment={assessment}
      authoringState={authoringState}
      assessmentGroups={assessmentGroups}
      courseContent={courseContent.items}
      groupSets={groupSets.map((set) => ({ id: set.id, name: set.name }))}
      rubric={rubric.rubric}
      rubricLocked={rubric.locked}
      canManage={access.canEdit}
    />
  );
}
