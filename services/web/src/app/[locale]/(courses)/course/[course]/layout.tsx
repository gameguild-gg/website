'use client';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { usePathname } from 'next/navigation';
import { courses } from '@/data/courses';

export default function CourseLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: { course?: string };
}) {
  if (!params.course) {
    notFound();
  }

  const course = courses.find((c) => c.slug === params.course);

  if (!course) {
    notFound();
  }

  const pathname = usePathname();

  return (
    <div className="container mx-auto py-8">
      <h1 className="text-3xl font-bold mb-6">{course.title}</h1>
      <div className="flex flex-col md:flex-row gap-8">
        <aside className="w-full md:w-1/4">
          <nav>
            <ul className="space-y-4 sm:space-y-2">
              <li className="mb-4 sm:mb-2">
                <Link
                  href={`/course/${course.slug}`}
                  className={`text-blue-600 hover:underline font-semibold block py-2 px-3 rounded-md ${
                    pathname === `/course/${course.slug}`
                      ? 'bg-blue-100 text-blue-800'
                      : 'bg-gray-100'
                  }`}
                >
                  Course Home
                </Link>
              </li>
              {course.chapters.map((chapter) => (
                <li key={chapter.id} className="mb-4 sm:mb-2">
                  <details
                    className="group"
                    open={chapter.lectures.some(
                      (lecture) =>
                        pathname === `/course/${course.slug}/${lecture.slug}`,
                    )}
                  >
                    <summary
                      className={`font-semibold cursor-pointer list-none flex items-center justify-between p-3 rounded-md ${
                        chapter.lectures.some(
                          (lecture) =>
                            pathname ===
                            `/course/${course.slug}/${lecture.slug}`,
                        )
                          ? 'bg-blue-100 text-blue-800'
                          : 'bg-gray-100'
                      }`}
                    >
                      <span className="flex-grow">{chapter.title}</span>
                      <svg
                        className="w-5 h-5 transition-transform group-open:rotate-90"
                        xmlns="http://www.w3.org/2000/svg"
                        viewBox="0 0 20 20"
                        fill="currentColor"
                      >
                        <path
                          fillRule="evenodd"
                          d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z"
                          clipRule="evenodd"
                        />
                      </svg>
                    </summary>
                    <ul className="mt-2 space-y-2 pl-4">
                      {chapter.lectures?.map((lecture) => (
                        <li key={lecture.id}>
                          <Link
                            href={`/course/${course.slug}/${lecture.slug}`}
                            className={`block py-2 px-3 rounded-md ${
                              pathname ===
                              `/course/${course.slug}/${lecture.slug}`
                                ? 'bg-blue-100 text-blue-800 font-bold'
                                : 'bg-gray-50 text-blue-600 hover:underline'
                            }`}
                          >
                            {lecture.title}
                          </Link>
                        </li>
                      ))}
                    </ul>
                  </details>
                </li>
              ))}
            </ul>
          </nav>
        </aside>
        <main className="w-full md:w-3/4">{children}</main>
      </div>
    </div>
  );
}
