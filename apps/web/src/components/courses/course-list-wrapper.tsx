'use client';

import type { CatalogCourse } from '@/lib/courses/catalog-types';
import type { Course } from '@/lib/courses/types';
import { useEffect, useMemo, useState } from 'react';
import CourseList from './course-list';

export default function CourseListWrapper({ courses }: { courses: CatalogCourse[] }) {
  // Ensure courses is an array and filter out invalid entries using useMemo to prevent re-renders
  const validCourses = useMemo(() =>
    Array.isArray(courses) ? courses.filter(course => course && course.id && course.area) : []
    , [courses]);

  // Transform legacy courses to new Course format
  const transformedCourses: Course[] = useMemo(() =>
    validCourses.map((legacyCourse: CatalogCourse) => ({
      id: String(legacyCourse.id),
      title: String(legacyCourse.title ?? 'Untitled course'),
      slug: String(legacyCourse.slug ?? legacyCourse.id),
      description: String(legacyCourse.description ?? ''),
      category: String(legacyCourse.area ?? 'General'),
      level: 'Beginner' as const,
      duration: '0h',
      enrolledStudents: 0,
      rating: 0,
      price: 0,
      image: String(legacyCourse.image ?? ''),
      instructor: {
        name: 'Instructor',
        avatar: '',
      },
      isEnrolled: false,
      progress: 0,
      certification: false,
    }))
    , [validCourses]);

  const categories = Array.from(new Set(validCourses.map((c) => c.area)));
  const [selectedCategory, setSelectedCategory] = useState<string>('All');
  const [filtered, setFiltered] = useState(transformedCourses);

  // Update filtered courses when transformedCourses changes
  useEffect(() => {
    setFiltered(selectedCategory === 'All' ? transformedCourses : transformedCourses.filter((c) => c.category === selectedCategory));
  }, [transformedCourses, selectedCategory]);

  function handleFilter(cat: string) {
    setSelectedCategory(cat);
    setFiltered(cat === 'All' ? transformedCourses : transformedCourses.filter((c) => c.category === cat));
  }

  return (
    <div>
      {/* Simple filter buttons */}
      <div className="flex gap-2 mb-6">
        <button onClick={() => handleFilter('All')} className={`px-3 py-1 rounded ${selectedCategory === 'All' ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}`}>
          All
        </button>
        {categories.map((cat) => (
          <button key={cat} onClick={() => handleFilter(cat)} className={`px-3 py-1 rounded ${selectedCategory === cat ? 'bg-primary text-primary-foreground' : 'bg-muted text-muted-foreground'}`}>
            {cat}
          </button>
        ))}
      </div>
      <CourseList courses={filtered} />
    </div>
  );
}
