import { notFound } from 'next/navigation';
import { courses } from '@/data/courses';
import { Api } from '@game-guild/apiclient';
import { Metadata, type ResolvingMetadata } from 'next';
import { MarkdownContent } from '@/components/markdown-content';
import { getCourseBySlug } from '@/app/[locale]/(content)/(public)/(courses)/actions';
import { OGImageDescriptor } from '@/types';

export const dynamic = 'force-dynamic';

interface CourseLecturePageProps {
  params: { course?: string; lecture?: string };
}

export async function generateMetadata(
  { params }: CourseLecturePageProps,
  parent: ResolvingMetadata,
): Promise<Metadata> {
  const course = await getCourseBySlug(params.course);
  const lecture = course?.lectures.find((l) => l.slug === params.lecture);

  if (!course) {
    return {
      title: 'Course Not Found',
      description: 'The requested course could not be found.',
      robots: {
        index: false,
        follow: false,
      },
    };
  }

  if (!lecture) {
    return {
      title: 'Lecture Not Found',
      description: 'The requested lecture could not be found.',
      robots: {
        index: false,
        follow: false,
      },
    };
  }

  let thumbnail: OGImageDescriptor;
  if (course.thumbnail)
    thumbnail = {
      url: `${course.thumbnail.path}/${course.thumbnail.filename}`,
      width: course.thumbnail.width,
      height: course.thumbnail.height,
      alt: course.thumbnail.description,
    };
  else
    thumbnail = {
      url: `/assets/images/logo-icon.png`,
      width: 338,
      height: 121,
      alt: 'Game Guild Logo',
    };

  let summary: string;
  summary = lecture.summary.slice(0, 160);
  if (!summary) summary = course.summary.slice(0, 160);
  if (!summary) summary = 'Discover this amazing course on our platform.';

  let title: string;
  title = lecture.title;
  if (!title) title = course.title.slice(0, 160);
  if (!title) title = 'Discover this amazing course on our platform.';

  const visible =
    course.visibility === 'PUBLISHED' && lecture.visibility === 'PUBLISHED';

  return {
    title: title,
    description: summary,
    openGraph: {
      title: title,
      description: summary,
      url: `https://gameguild.gg/course/${course.slug}/${lecture.slug}`,
      siteName: 'Game Guild',
      images: thumbnail,
      type: 'website',
    },
    twitter: {
      card: 'summary_large_image',
      title: title,
      description: summary,
      images: thumbnail,
    },
    alternates: {
      canonical: `https://gameguild.gg/course/${course.slug}/${lecture.slug}`,
    },
    robots: {
      index: visible,
      follow: visible,
    },
    metadataBase: new URL('https://gameguild.gg'),
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
        <MarkdownContent content={lecture.body} renderer={lecture.renderer} />
      )}
    </div>
  );
}
