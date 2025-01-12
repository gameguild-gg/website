import { notFound } from 'next/navigation';
import Image from 'next/image';
import { courses } from '@/data/courses';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

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
    <div>
      <Image
        src={imageUrl}
        alt={course.thumbnail?.description || course.title}
        width={course.thumbnail?.width || 600}
        height={course.thumbnail?.height || 400}
        className="w-full object-cover h-64 rounded-lg mb-6"
      />
      <h2 className="text-2xl font-semibold mb-4">Welcome to {course.title}</h2>
      <p className="mb-4">{course.summary}</p>
      <div className="mb-4">
        {course.price ? (
          <p className="font-semibold">Price: ${course.price.toFixed(2)}</p>
        ) : (
          <p className="font-semibold text-green-600">This course is free!</p>
        )}
        {course.subscriptionAccess && (
          <p className="text-blue-600">This course requires a subscription</p>
        )}
      </div>
      <h3 className="text-xl font-semibold mb-2">Course Contents:</h3>
      <MarkdownRenderer markdown={course.body} />
      {/*<ul className="list-disc pl-6">*/}
      {/*  {course.chapters.map((chapter) => (*/}
      {/*    <li key={chapter.id}>*/}
      {/*      {chapter.title}*/}
      {/*      <ul className="list-circle pl-6">*/}
      {/*        {chapter.lectures?.map((lecture) => (*/}
      {/*          <li key={lecture.id}>{lecture.title}</li>*/}
      {/*        ))}*/}
      {/*      </ul>*/}
      {/*    </li>*/}
      {/*  ))}*/}
      {/*</ul>*/}
    </div>
  );
}
