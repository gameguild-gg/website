import type { Metadata } from 'next';
import { notFound } from 'next/navigation';
import { courses } from '@/data/courses';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';
import { Api } from '@game-guild/apiclient';

export const dynamic = 'force-dynamic';

interface CourseLecturePageProps {
  params: { course?: string; lecture?: string };
}

export async function generateMetadata({
  params,
}: CourseLecturePageProps): Promise<Metadata> {
  const course = courses.find((c) => c.slug === params.course);
  const lecture = course?.chapters
    .flatMap((chapter) => chapter.lectures || [])
    .find((l) => l.slug === params.lecture);

  if (!course || !lecture) {
    return {};
  }

  return {
    title: `${lecture.title} - ${course.title}`,
    description: lecture.summary,
  };
}

export default function CourseLecturePage({ params }: CourseLecturePageProps) {
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
    <div className="w-full">
      {lecture.renderer !== Api.LectureEntity.Renderer.Enum.Reveal && (
        <>
          <h2 className="text-2xl font-semibold mb-4">{lecture.title}</h2>
          <p className="mb-4">
            Lecture {lecture.order} in Chapter {lecture.chapter.order}
          </p>
        </>
      )}
      {lecture.body && (
        <MarkdownRenderer content={lecture.body} renderer={lecture.renderer} />
      )}
    </div>
  );
}
