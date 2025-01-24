import { notFound } from 'next/navigation';
import Image from 'next/image';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';
import { courses } from '@/data/courses';

export default function CourseHomePage({
  params,
}: {
  params: { course?: string };
}) {
  if (!params.course) {
    notFound();
  }

  const course = courses.find((c) => c.slug === params.course);

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
        <MarkdownRenderer content={course.summary || ''} />
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
      {course.body && (
        <div className="mt-8">
          <h3 className="text-xl font-semibold mb-2 text-center">
            Course Description:
          </h3>
          <MarkdownRenderer content={course.body} />
        </div>
      )}
    </div>
  );
}
