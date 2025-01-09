'use client'

import { useState, useEffect, useCallback } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import Link from 'next/link'
import { ModuleBasev1_0_0 } from '@/lib/interface-base/module.base.v1.0.0'
import { AssessmentBasev1_0_0 } from '@/lib/interface-base/assessment.base.v1.0.0'
import { CourseBasev1_0_0 } from '@/lib/interface-base/course.base.v1.0.0'
import { LessonBasev1_0_0 } from '@/lib/interface-base/lesson.base.v1.0.0'
import { ChevronRight, Sun, Moon, ZapOff, FileText, CheckCircle, ChevronLeft } from 'lucide-react'
import PageHeader from '@/components/learn/PageHeader' // Import the new component

export default function CoursePage({ params }: { params: { id: string } }) {
  const [course, setCourse] = useState<CourseBasev1_0_0 | null>(null)
  const [modules, setModules] = useState<ModuleBasev1_0_0[]>([])
  const [assessments, setAssessments] = useState<{ [key: number]: AssessmentBasev1_0_0 }>({})
  const [lessons, setLessons] = useState<{ [key: number]: LessonBasev1_0_0 }>({})
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const router = useRouter()
  const searchParams = useSearchParams()
  const userId = searchParams.get('userId')
  const role = searchParams.get('role')
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>('light')

  useEffect(() => {
    const fetchData = async () => {
      try {
        setIsLoading(true)

        // Fetch course data
        const courseResponse = await fetch(`../../../api/learn/course/${params.id}`)
        if (!courseResponse.ok) {
          const errorData = await courseResponse.json()
          throw new Error(`Failed to fetch course: ${errorData.error}. Details: ${errorData.details}`)
        }
        const courseData: CourseBasev1_0_0 = await courseResponse.json()
        setCourse(courseData)

        // Fetch modules data
        const modulesPromises = courseData.idModules.map(moduleId =>
          fetch(`../../../api/learn/content/module/${moduleId}`).then(res => {
            if (!res.ok) {
              throw new Error(`Failed to fetch module ${moduleId}: ${res.statusText}`)
            }
            return res.json()
          })
        )
        const modulesData = await Promise.all(modulesPromises)
        setModules(modulesData)

        // Fetch lessons for each module
        const lessonPromises = modulesData.flatMap((module: ModuleBasev1_0_0) =>
          module.idLessons.map((id: number) =>
            fetch(`../../../api/learn/content/lesson/${id}`).then(res => {
              if (!res.ok) {
                throw new Error(`Failed to fetch lesson ${id}: ${res.statusText}`)
              }
              return res.json()
            })
          )
        )
        const lessonsData = await Promise.all(lessonPromises)
        const lessonMap = lessonsData.reduce((acc, lesson) => {
          acc[lesson.id] = lesson
          return acc
        }, {} as { [key: number]: LessonBasev1_0_0 })
        setLessons(lessonMap)


        // Fetch assessments for each module
        const assessmentPromises = modulesData.flatMap((module: ModuleBasev1_0_0) =>
          module.idAssessments.map((id: number) => 
            fetch(`../../../api/learn/content/assessment/${id}`).then(res => {
              if (!res.ok) {
                throw new Error(`Failed to fetch assessment ${id}: ${res.statusText}`)
              }
              return res.json()
            })
          )
        )
        const assessmentData = await Promise.all(assessmentPromises)
        const assessmentMap = assessmentData.reduce((acc, assessment) => {
          acc[assessment.id] = assessment
          return acc
        }, {})
        setAssessments(assessmentMap)
      } catch (error) {
        console.error('Error fetching data:', error)
        setError(error instanceof Error ? error.message : 'An unknown error occurred')
      } finally {
        setIsLoading(false)
      }
    }

    fetchData()
  }, [params.id])

  useEffect(() => {
    const storedMode = localStorage.getItem('colorMode') as 'light' | 'dark' | 'high-contrast' | null
    if (storedMode) {
      setMode(storedMode)
    }
  }, [])

  useEffect(() => {
    localStorage.setItem('colorMode', mode)
  }, [mode])

  const handleSandboxClick = useCallback(() => {
    router.push(`/learn/coding-environment?id=0&type=sandbox&userId=${userId}&role=${role}`);
  }, [userId, role, router]);

  const toggleMode = () => {
    const newMode = mode === 'light' ? 'dark' : mode === 'dark' ? 'high-contrast' : 'light'
    setMode(newMode)
  }

  if (isLoading) {
    return <div className="min-h-screen bg-gray-100 p-8">Loading course content...</div>
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-100 p-8">
        <h1 className="text-2xl font-bold mb-4 text-red-600">Error</h1>
        <p className="text-red-500">{error}</p>
        <button
          onClick={() => window.location.reload()}
          className="mt-4 bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded"
        >
          Retry
        </button>
      </div>
    )
  }

  return (
    <div className={`p-8 ${
      mode === 'light' ? 'bg-gray-100 text-gray-900' :
      mode === 'dark' ? 'bg-gray-800 text-gray-100' :
      'bg-black text-yellow-300'
    }`}>
      <div className="max-w-5xl mx-auto">
        <PageHeader // Use the PageHeader component
          title={course?.title || 'Loading...'} // Provide title
          backLink={`/learn/courses?userId=${userId}`} // Provide back link
          userId={userId}
          mode={mode}
          onModeToggle={toggleMode}
        />

        <h2 className="text-xl font-semibold mb-4">Course Modules</h2>
        <div className="space-y-6">
          {modules.map((module) => (
            <div key={module.id} className="bg-white p-6 rounded-lg shadow-md">
              <h3 className="text-lg font-semibold mb-2">{module.title}</h3>
              <p className="text-gray-600 mb-4">{module.description}</p>
              <div className="space-y-4">
                {/* Lessons */}
                {module.idLessons.map((lessonId) => {
                  const lesson = lessons[lessonId]
                  return lesson ? (
                    <div key={lesson.id} className="border rounded-md">
                      <Link href={`/learn/lesson/${lesson.id}?userId=${userId}&role=${role}&courseId=${params.id}&moduleId=${module.id}`}> {/* Added userId and courseId */}
                        <div className="w-full text-left px-4 py-2 flex items-center justify-between hover:bg-gray-50">
                          <div className="flex items-center">
                            <FileText className="w-5 h-5 mr-2" />
                            <span>{lesson.title}</span>
                          </div>
                          <ChevronRight className="w-5 h-5" />
                        </div>
                      </Link>
                    </div>
                  ) : null
                })}
                {/* Assessments */}
                {module.idAssessments.map((assessmentId) => {
                  const assessment = assessments[assessmentId]
                  if (!assessment) return null

                  // Date comparison and styling
                  const now = new Date();
                  let displayDate = assessment.maxDate ? new Date(assessment.maxDate) : null;

                  if (!displayDate && assessment.scoreTotal && assessment.scoreTotal.length > 0) {
                    // Find the nearest future date in scoreTotal
                    const futureScoreDates = assessment.scoreTotal
                      .filter(score => new Date(score.maxDate) > now)
                      .map(score => new Date(score.maxDate));
                    
                    if (futureScoreDates.length > 0) {
                      displayDate = futureScoreDates.sort((a, b) => a.getTime() - b.getTime())[0];
                    } else {
                      // If no future dates are found, use the latest date in scoreTotal
                      displayDate = new Date(Math.max(...assessment.scoreTotal.map(score => new Date(score.maxDate).getTime())));
                    }
                  }

                  const isPastDueDate = displayDate && displayDate < now;
                  const dateBgColor = isPastDueDate ? 'bg-red-100' : 'bg-green-100'
                  const dateTextColor = isPastDueDate ? 'text-red-800' : 'text-green-800'

                  return (
                    <div key={assessment.id} className="border rounded-md flex">
                      <Link href={`/learn/assessment/${assessment.id}?userId=${userId}&role=${role}&courseId=${params.id}&moduleId=${module.id}`} className="grow"> {/* Added className="grow" */}
                        <div className="w-full text-left px-4 py-2 flex items-center justify-between hover:bg-gray-50">
                          <div className="flex items-center">
                            <CheckCircle className="w-5 h-5 mr-2" />
                            <span>{assessment.title}</span>
                          </div>
                          <ChevronRight className="w-5 h-5" />
                        </div>
                      </Link>
                      {displayDate && ( // Conditionally render the date if it exists
                        <div className={`ml-4 p-2 rounded-lg flex items-center ${dateBgColor} ${dateTextColor}`}>
                          {displayDate.toLocaleDateString()}
                        </div>
                      )}
                    </div>
                  )
                })}
              </div>
            </div>
          ))}
        </div>
        <div className="fixed bottom-4 left-4">
          <Link href={`/learn/courses?userId=${userId}`}>
            <button className="bg-blue-500 hover:bg-blue-600 text-white font-bold py-2 px-4 rounded">
              Return to Course Selection
            </button>
          </Link>
        </div>
      </div>
    </div>
  )
}

