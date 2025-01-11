import { courses } from '@/data/courses';
import { CourseCard } from '@/components/courses/course-card';
import Link from 'next/link';

export default function CoursesPage() {
  return (
    <div className="container mx-auto py-8">
      <h1 className="text-3xl font-bold mb-8">Available Courses</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {courses.map((course) => (
          <CourseCard course={course} />
        ))}
      </div>
    </div>
  );
}
