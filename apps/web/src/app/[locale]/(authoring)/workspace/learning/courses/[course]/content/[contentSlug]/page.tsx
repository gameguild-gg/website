import { LessonAuthoringWorkspace } from "@/components/learning/authoring/lesson-authoring-workspace";
import { getAuthoringDraft } from "@/lib/learning/authoring";
import {
  getContentItem,
  getCourse,
  getCourseContent,
} from "@/lib/learning";
import { notFound } from "next/navigation";

export default async function ContentItemAuthoringPage({
  params,
}: PageProps<"/[locale]/workspace/learning/courses/[course]/content/[contentSlug]">) {
  const { course: courseIdentifier, contentSlug } = await params;
  const course = await getCourse(courseIdentifier);
  if (!course) notFound();

  const [item, curriculum] = await Promise.all([
    getContentItem(course.id, contentSlug),
    getCourseContent(course.id),
  ]);
  if (!item) notFound();

  const draft = await getAuthoringDraft(course.id, item.id);
  if (!draft.success) {
    if (draft.status === 404) notFound();
    throw new Error(draft.error);
  }

  return (
    <LessonAuthoringWorkspace
      courseId={course.id}
      courseSlug={course.slug}
      courseTitle={course.title}
      item={item}
      curriculum={curriculum.items}
      initialDraft={draft.data}
    />
  );
}
