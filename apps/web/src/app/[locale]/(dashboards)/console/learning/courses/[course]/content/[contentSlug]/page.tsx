import React from 'react';
import { notFound } from 'next/navigation';
import {
  getCourse,
  getContentItem,
  getCourseAssessments,
  getCodingDefinitionPublic,
} from '@/lib/learning';
import { ContentItemEditor } from '@/components/learning/console/courses/[course]/content/[contentId]/content-item-editor';

export default async function ContentItemPage({
  params,
}: PageProps<'/[locale]/console/learning/courses/[course]/content/[contentSlug]'>): Promise<React.JSX.Element> {
  const { course: courseId, contentSlug } = await params;

  const [course, contentItem] = await Promise.all([
    getCourse(courseId),
    getContentItem(courseId, contentSlug),
  ]);

  if (!course) {
    notFound();
  }

  if (!contentItem) {
    notFound();
  }

  // For graded content types (Assignment/Quiz/Project/Code), look up the linked
  // Assessment and any existing v2 coding definition so the editor can bridge to
  // the coding-definition authoring route without an extra round-trip. The
  // coding definition itself only exists for coding-capable types.
  const GRADED_TYPES = new Set([
    "Assignment",
    "Questionnaire",
    "Project",
    "Code",
  ]);
  const CODING_TYPES = new Set(["Assignment", "Project", "Code"]);

  let linkedAssessment: Awaited<ReturnType<typeof getCourseAssessments>>["assessments"][number] | undefined;
  let initialCodingDefinition: Awaited<
    ReturnType<typeof getCodingDefinitionPublic>
  > = null;
  if (GRADED_TYPES.has(contentItem.type)) {
    const assessmentsResp = await getCourseAssessments(courseId);
    const linked = assessmentsResp.assessments.find(
      (a) => a.contentId === contentItem.id,
    );
    if (linked) {
      linkedAssessment = linked;
      if (CODING_TYPES.has(contentItem.type)) {
        initialCodingDefinition = await getCodingDefinitionPublic(linked.id);
      }
    }
  }

  return (
    <ContentItemEditor
      courseId={courseId}
      item={contentItem}
      courseTitle={course.title}
      linkedAssessment={linkedAssessment}
      initialCodingDefinition={initialCodingDefinition}
    />
  );
}
