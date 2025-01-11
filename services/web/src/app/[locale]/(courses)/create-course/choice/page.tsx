'use client';

import React, { useState, useEffect } from 'react';
import CourseTabs from '../edit/page';
import { PlusCircle, Edit } from 'lucide-react';
import { CoursesApi } from '@game-guild/apiclient';
import { auth } from '@/auth';
import { Api } from '@game-guild/apiclient';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export default function CoursePage() {
  const [courses, setCourses] = useState<Api.CourseEntity[]>([]);
  const [selectedCourse, setSelectedCourse] = useState<string | null>(null);

  const api = new CoursesApi({
    basePath: process.env.NEXT_PUBLIC_API_URL,
  });

  useEffect(() => {
    const fetchData = async () => {
      try {
        const session = await auth();
        if (!session || !session.user?.accessToken) {
          throw new Error('No valid session found');
        }
        const response = await api.getManyBaseCoursesControllerCourseEntity(
          { page: 0, limit: 10 },
          { headers: { Authorization: 'Bearer ' + session.user.accessToken } },
        );
        setCourses(response.body as Api.CourseEntity[]);
      } catch (error) {
        console.error('Error fetching courses or authenticating:', error);
      }
    };

    fetchData();
  }, []);

  const handleNewCourse = () => {
    setSelectedCourse('new');
  };

  const handleSelectCourse = (id: string) => {
    setSelectedCourse(id);
  };

  if (selectedCourse) {
    return (
      <div className="space-y-4">
        <Button
          onClick={() => setSelectedCourse(null)}
          variant="outline"
          size="sm"
        >
          ← Back to Course List
        </Button>
        <CourseTabs
          courseId={selectedCourse}
          onBack={() => setSelectedCourse(null)}
        />
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <Card>
        <CardHeader>
          <CardTitle className="text-2xl">Course Management</CardTitle>
        </CardHeader>
        <CardContent>
          <Button onClick={handleNewCourse} className="w-full sm:w-auto">
            <PlusCircle className="mr-2 h-4 w-4" /> Create New Course
          </Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-xl">Existing Courses</CardTitle>
        </CardHeader>
        <CardContent>
          {courses.length === 0 ? (
            <p className="text-muted-foreground">
              No courses available. Create a new course to get started.
            </p>
          ) : (
            <ul className="space-y-4">
              {courses.map((course) => (
                <li
                  key={course.id}
                  className="flex items-center justify-between bg-muted p-4 rounded-lg"
                >
                  <span className="font-medium">
                    {course.information.title}
                  </span>
                  <Button
                    onClick={() => handleSelectCourse(course.id)}
                    variant="outline"
                    size="sm"
                  >
                    <Edit className="mr-2 h-4 w-4" /> Edit
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
