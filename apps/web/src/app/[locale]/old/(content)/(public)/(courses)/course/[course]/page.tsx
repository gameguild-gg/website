import type { Metadata, ResolvingMetadata } from 'next';
import { notFound } from 'next/navigation';
import Image from 'next/image';
import { CourseDescription } from '@/components/course-description';
import { CourseSummary } from '@/components/course-summary';
import { getCourseBySlug, getCourses } from '../../actions';
import { OGImageDescriptor } from '@/types';

type Props = {
  params: {
    course: string;
  };
};

export async function generateStaticParams() {
  const courses = await getCourses();

  return courses.map((course) => ({
    slug: course.slug,
  }));
}

export async function generateMetadata(
  { params }: Props,
  parent: ResolvingMetadata,
): Promise<Metadata> {
  const course = await getCourseBySlug(params.course);

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
  return {
    title: course.title,
    description:
      course.summary || 'Discover this amazing course on our platform.',
    openGraph: {
      title: course.title,
      description: course.summary || 'Learn new skills with this course.',
      url: `https://gameguild.gg/course/${course.slug}`,
      siteName: 'Game Guild',
      images: thumbnail,
      type: 'website',
    },
    twitter: {
      card: 'summary_large_image',
      title: course.title,
      description: course.summary || 'Expand your knowledge with this course.',
      images: thumbnail,
    },
    alternates: {
      canonical: `https://gameguild.gg/course/${course.slug}`,
    },
    robots: {
      index: course.visibility === 'PUBLISHED',
      follow: course.visibility === 'PUBLISHED',
    },
    metadataBase: new URL('https://gameguild.gg'),
  };
}

export default async function Page({ params }: Readonly<Props>) {
  const course = await getCourseBySlug(params.course);

  if (!course) {
    notFound();
  }

  const imageUrl = course.thumbnail
    ? `${course.thumbnail.path}/${course.thumbnail.filename}`
    : '/placeholder.svg?height=400&width=600';

  return (
    <div className="w-full">
      <div className="flex justify-center mb-6">
        <Image
          src={imageUrl || '/placeholder.svg'}
          alt={course.thumbnail?.description || course.title}
          width={course.thumbnail?.width || 600}
          height={course.thumbnail?.height || 400}
          className="object-cover h-64 rounded-lg"
        />
      </div>
      <h2 className="text-2xl font-semibold mb-4 text-center">
        Welcome to {course.title}
      </h2>
      <div className="mb-4">
        <CourseSummary summary={course.summary} />
      </div>
      <div className="mb-4">
        {course.price ? (
          <p className="font-semibold text-center">
            Price: ${course.price.toFixed(2)}
          </p>
        ) : (
          <p className="font-semibold text-green-600 text-center">
            This course is free!
          </p>
        )}
        {course.subscriptionAccess && (
          <p className="text-blue-600 text-center">
            This course requires a subscription
          </p>
        )}
      </div>
      <h3 className="text-xl font-semibold mb-2 text-center">
        Course Contents:
      </h3>
      <ul className="list-disc pl-6">
        {course.chapters.map((chapter) => (
          <li key={chapter.id}>
            {chapter.title}
            <ul className="list-circle pl-6">
              {chapter.lectures?.map((lecture) => (
                <li key={lecture.id}>{lecture.title}</li>
              ))}
            </ul>
          </li>
        ))}
      </ul>
      <div className="mt-8">
        {course.body && <CourseDescription body={course.body} />}
      </div>
    </div>
  );
}
