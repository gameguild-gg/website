'use client';
import { useState } from 'react';
import { notFound, useParams } from 'next/navigation';
import { Menu, ChevronLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { courses } from '@/data/courses';
import { CourseNavigation } from '@/components/courses/CourseNavigation';
import { Api } from '@game-guild/apiclient';

export default function CourseLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const params = useParams();

  if (!params.course) {
    notFound();
  }

  const course = courses.find((c) => c.slug === params.course);

  if (!course) {
    notFound();
  }

  const lecture = courses
    .find((c) => c.slug === params.course)
    ?.chapters.flatMap((ch) => ch.lectures)
    .find((l) => l.slug === params.lecture);
  const isRevealJSLecture =
    lecture?.renderer === Api.LectureEntity.Renderer.Enum.Reveal;

  return (
    <div className="flex overflow-hidden">
      {/* Sidebar */}
      <aside
        className={`
        w-64 bg-gray-100 overflow-y-auto
        fixed inset-y-0 left-0 z-50 lg:relative lg:translate-x-0 transform transition-transform duration-200 ease-in-out
        ${isSidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
      `}
      >
        <div className="p-4">
          <Button
            variant="outline"
            size="icon"
            className="mb-4 lg:hidden"
            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          >
            <ChevronLeft className="h-4 w-4" />
          </Button>
          <CourseNavigation
            course={course}
            isOpen={true}
            onNavigate={() => setIsSidebarOpen(false)}
          />
        </div>
      </aside>

      {/* Main content */}
      <div className="flex-1 flex flex-col overflow-hidden">
        <header className="bg-white shadow-sm lg:hidden">
          <div className="px-4 py-2">
            <Button
              variant="outline"
              size="icon"
              onClick={() => setIsSidebarOpen(!isSidebarOpen)}
            >
              <Menu className="h-4 w-4" />
            </Button>
          </div>
        </header>

        <main
          className={`flex-1 overflow-x-hidden overflow-y-auto bg-white transition-all duration-200 ${isSidebarOpen ? 'lg:ml-64' : ''}`}
        >
          <div className="container mx-auto px-4 sm:px-6 lg:px-8 py-8">
            {!isRevealJSLecture && (
              <h1 className="text-3xl font-bold mb-6">{course.title}</h1>
            )}
            {children}
          </div>
        </main>
      </div>
    </div>
  );
}
