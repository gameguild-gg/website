'use client'

import { useState, useEffect } from 'react'
import { Button } from "@/components/ui/button"
import CourseTabs from '../edit/page'

interface Course {
  id: string;
  information: {
    title: string;
  };
}

export default function CoursePage() {
  const [courses, setCourses] = useState<Course[]>([])
  const [selectedCourse, setSelectedCourse] = useState<string | null>(null)

  useEffect(() => {
    // Fetch courses from the server
    const fetchCourses = async () => {
      // Replace this with actual API call
      const response = await fetch('/api/courses')
      const data = await response.json()
      setCourses(data)
    }

    fetchCourses()
  }, [])

  const handleNewCourse = () => {
    setSelectedCourse('new')
  }

  const handleSelectCourse = (id: string) => {
    setSelectedCourse(id)
  }

  if (selectedCourse) {
    return <CourseTabs courseId={selectedCourse} onBack={() => setSelectedCourse(null)} />
  }

  return (
    <div className="space-y-6">
      <Button onClick={handleNewCourse}>New Course</Button>
      <div className="space-y-2">
        <h2 className="text-xl font-semibold">Existing Courses</h2>
        <ul className="space-y-2">
          {courses.map((course) => (
            <li key={course.id} className="flex items-center justify-between bg-gray-100 p-3 rounded-md">
              <span>{course.information.title}</span>
              <Button onClick={() => handleSelectCourse(course.id)} variant="outline">Edit</Button>
            </li>
          ))}
        </ul>
      </div>
    </div>
  )
}

