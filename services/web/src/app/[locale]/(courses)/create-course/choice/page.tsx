'use client';

import { useState, useEffect } from 'react';
import { Button } from '@/components/ui/button';
import CourseTabs from '../edit/page';
import {CoursesApi} from "@game-guild/apiclient";
import {auth} from "@/auth";
import {Api} from "@game-guild/apiclient";

export default function CoursePage() {
  const [courses, setCourses] = useState<Api.CourseEntity[]>([]);
  const [selectedCourse, setSelectedCourse] = useState<string | null>(null);
  const api = new CoursesApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  })
    // Fetch courses from the server
    const fetchCourses = async () => {
      const session = await auth();
      const response = await api.getManyBaseCoursesControllerCourseEntity({page: 0, limit: 10}, { headers: "Bearer " + session?.user?.accessToken });
      setCourses(response.body as Api.CourseEntity[]);
    };

    fetchCourses();
  }, []);

  const handleNewCourse = () => {
    setSelectedCourse('new');
  };

  const handleSelectCourse = (id: string) => {
    setSelectedCourse(id);
  };

  if (selectedCourse) {
    return (
      <CourseTabs
        courseId={selectedCourse}
        onBack={() => setSelectedCourse(null)}
      />
    );
  }

  return (
    <div className="space-y-6">
      <Button onClick={handleNewCourse}>New Course</Button>
      <div className="space-y-2">
        <h2 className="text-xl font-semibold">Existing Courses</h2>
        <ul className="space-y-2">
          {courses.map((course) => (
            <li
              key={course.id}
              className="flex items-center justify-between bg-gray-100 p-3 rounded-md"
            >
              <span>{course.information.title}</span>
              <Button
                onClick={() => handleSelectCourse(course.id)}
                variant="outline"
              >
                Edit
              </Button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
