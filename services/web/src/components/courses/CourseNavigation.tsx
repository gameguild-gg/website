'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ChevronRight } from 'lucide-react';
import { Api } from '@game-guild/apiclient';
import CourseEntity = Api.CourseEntity;

interface CourseNavigationProps {
  course: CourseEntity;
  isOpen: boolean;
  className?: string;
  onNavigate?: () => void;
}

export function CourseNavigation({
  course,
  isOpen,
  className = '',
  onNavigate,
}: CourseNavigationProps) {
  const pathname = usePathname();

  if (!isOpen) {
    return null;
  }

  return (
    <nav className={`${className}`}>
      <Link
        href={`/course/${course.slug}`}
        className={`block text-blue-600 hover:underline font-semibold py-2 px-3 rounded-md ${
          pathname === `/course/${course.slug}`
            ? 'bg-blue-100 text-blue-800'
            : 'hover:bg-gray-200'
        }`}
      >
        Course Home
      </Link>
      {course.chapters.map((chapter) => (
        <div key={chapter.id} className="mt-4">
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
                    pathname === `/course/${course.slug}/${lecture.slug}`,
                )
                  ? 'bg-blue-100 text-blue-800'
                  : 'hover:bg-gray-200'
              }`}
            >
              <span className="flex-grow">{chapter.title}</span>
              <ChevronRight className="w-5 h-5 transition-transform group-open:rotate-90" />
            </summary>
            <ul className="mt-2 space-y-1 pl-4">
              {chapter.lectures?.map((lecture) => (
                <li key={lecture.id}>
                  <Link
                    href={`/course/${course.slug}/${lecture.slug}`}
                    className={`block py-2 px-3 rounded-md ${
                      pathname === `/course/${course.slug}/${lecture.slug}`
                        ? 'bg-blue-100 text-blue-800 font-bold'
                        : 'hover:bg-gray-200 text-blue-600 hover:underline'
                    }`}
                    onClick={onNavigate}
                  >
                    {lecture.title}
                  </Link>
                </li>
              ))}
            </ul>
          </details>
        </div>
      ))}
    </nav>
  );
}
