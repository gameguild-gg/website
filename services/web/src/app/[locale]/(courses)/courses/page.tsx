import Link from 'next/link';
import { courses } from '@/data/courses';
import { CourseCard } from '@/components/courses/course-card';

export default function CoursesPage() {
  if (!courses || courses.length === 0) {
    return (
      <div className="container mx-auto py-8">
        <h1 className="text-3xl font-bold mb-8">Available Courses</h1>
        <p>No courses available at the moment.</p>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8">
      <h1 className="text-3xl font-bold mb-8">Available Courses</h1>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {courses.map((course) => (
          <Link
            href={`/course/${course.slug}`}
            key={course.id}
            className="block"
          >
            <CourseCard course={course} />
          </Link>
        ))}
      </div>
    </div>
  );
}
