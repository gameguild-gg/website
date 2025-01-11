import { notFound } from 'next/navigation';
import { courses } from '@/data/courses';

export default function CourseLecturePage({
  params,
}: {
  params: { course?: string; lecture?: string };
}) {
  if (!params.course || !params.lecture) {
    notFound();
  }

  const course = courses.find((c) => c.slug === params.course);

  if (!course) {
    notFound();
  }

  const lecture = course.chapters
    .flatMap((chapter) => chapter.lectures || [])
    .find((l) => l.slug === params.lecture);

  if (!lecture) {
    notFound();
  }

  return (
    <div>
      <h2 className="text-2xl font-semibold mb-4">{lecture.title}</h2>
      <p className="mb-4">
        Lecture {lecture.order} in Chapter {lecture.chapter.order}
      </p>
      {lecture.body && (
        <div
          className="prose max-w-none"
          dangerouslySetInnerHTML={{ __html: lecture.body }}
        />
      )}
    </div>
  );
}
