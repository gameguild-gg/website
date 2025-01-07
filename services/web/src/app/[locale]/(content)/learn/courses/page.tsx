'use client'

import { useState, useEffect } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import { UserBasev1_0_0 } from '@/lib/interface-base/user.base.v1.0.0'
import { CourseBasev1_0_0 } from '@/lib/interface-base/course.base.v1.0.0'
import { HierarchyBasev1_0_0 } from '@/lib/interface-base/structure.base.v1.0.0'
import RoleSelectionModal from './components/RoleSelectionModal'
import { Sun, Moon, ZapOff } from 'lucide-react'
import Image from 'next/image'
import Link from 'next/link'

export default function CoursesPage() {
  const [user, setUser] = useState<UserBasev1_0_0 | null>(null)
  const [courses, setCourses] = useState<CourseBasev1_0_0[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [selectedCourse, setSelectedCourse] = useState<CourseBasev1_0_0 | null>(null)
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [hierarchy, setHierarchy] = useState<HierarchyBasev1_0_0 | null>(null)
  const router = useRouter()
  const searchParams = useSearchParams()
  const userId = searchParams.get('userId')
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>('dark')
  const [role, setRole] = useState<'student' | 'teacher' | null>(null); // Added state for role

  useEffect(() => {
    const fetchData = async () => {
      try {
        setIsLoading(true)
        
        // Fetch user data
        const userResponse = await fetch(`../../api/learn/user/${userId}`)
        if (!userResponse.ok) {
          const errorData = await userResponse.json()
          throw new Error(`Failed to fetch user data: ${errorData.error}. Details: ${errorData.details}`)
        }
        const userData: UserBasev1_0_0 = await userResponse.json()
        setUser(userData)

        // Fetch courses data
        const coursesPromises = userData.idCourses.map(courseId => 
          fetch(`../../api/learn/course/${courseId}`).then(async (res) => {
            if (!res.ok) {
              const errorData = await res.json()
              throw new Error(`Failed to fetch course ${courseId}: ${errorData.error}. Details: ${errorData.details}`)
            }
            return res.json()
          })
        )
        const coursesData = await Promise.all(coursesPromises)
        setCourses(coursesData)
      } catch (error) {
        console.error('Error fetching data:', error)
        setError(error instanceof Error ? error.message : 'An unknown error occurred')
      } finally {
        setIsLoading(false)
      }
    }

    if (userId) {
      fetchData()
    }
  }, [userId])

  useEffect(() => {
    const storedMode = localStorage.getItem('colorMode') as 'light' | 'dark' | 'high-contrast' | null
    if (storedMode) {
      setMode(storedMode)
    }
  }, [])

  useEffect(() => {
    localStorage.setItem('colorMode', mode)
  }, [mode])

  const handleCourseSelect = (course: CourseBasev1_0_0) => {
    const newHierarchy: HierarchyBasev1_0_0 = {
      idHierarchy: [course.id],
      idUser: userId!,
      idRole: course.teachRole.includes(userId!) ? 'teacher' : 'student'
    }
    setHierarchy(newHierarchy)
    setRole(course.teachRole.includes(userId!) ? 'teacher' : 'student'); // Set role

    if (course.teachRole.includes(userId!)) {
      setSelectedCourse(course)
      setIsModalOpen(true)
    } else {
      router.push(`/learn/course/${course.id}?userId=${userId}&role=student`)
    }
  }

  const handleRoleSelection = (role: 'student' | 'teacher') => {
    setIsModalOpen(false)
    if (selectedCourse) {
      router.push(`/learn/course/${selectedCourse.id}?userId=${userId}&role=${role}`)
    }
  }

  const toggleMode = () => {
    const newMode = mode === 'light' ? 'dark' : mode === 'dark' ? 'high-contrast' : 'light'
    setMode(newMode)
  }

  if (isLoading) {
    return <div className="min-h-screen bg-gray-100 p-8">Loading courses...</div> 
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
    <div className={`min-h-screen p-8 ${
      mode === 'light' ? 'bg-gray-100 text-gray-900' :
      mode === 'dark' ? 'bg-gray-800 text-gray-100' :
      'bg-black text-yellow-300'
    }`}>
      <div className="max-w-5xl mx-auto">
        <div className="flex justify-between items-center mb-4">
          <h1 className="text-3xl font-bold">Available Courses</h1>
          <Link href={`/learn/coding-environment?id=0&type=sandbox&userId=${userId}&role=${role}`}>
            <button className={`p-2 rounded-full transition-colors duration-200 mr-2 ${
              mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
              mode === 'dark' ? 'bg-gray-700 text-gray-200 hover:bg-gray-600' :
              'bg-yellow-300 text-black hover:bg-yellow-400'
            }`}>
              Sandbox
            </button>
          </Link>
          <button onClick={toggleMode} className={`p-2 rounded-full transition-colors duration-200 ${
            mode === 'light' ? 'bg-gray-200 text-gray-800 hover:bg-gray-300' :
            mode === 'dark' ? 'bg-gray-700 text-gray-200 hover:bg-gray-600' :
            'bg-yellow-300 text-black hover:bg-yellow-400'
          }`}>
            {mode === 'light' ? <Sun className="w-5 h-5" /> : mode === 'dark' ? <Moon className="w-5 h-5" /> : <ZapOff className="w-5 h-5" />}
          </button>
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {courses.map(course => (
            <div key={course.id} className="bg-white p-6 rounded-lg shadow-md relative w-72" style={{ width: '384px', height: '384px' }}>
              <h3 className="text-lg font-semibold mb-2">{course.title}</h3>
              <div className="mb-4 relative" style={{ height: '140px' }}>
                {course.thumbnail ? (
                  <Image
                    src={course.thumbnail}
                    alt={`Thumbnail for ${course.title}`}
                    fill
                    style={{ objectFit: 'cover' }}
                    className="rounded-md"
                  />
                ) : (
                  <Image
                    src="/placeholder.svg"
                    alt="Placeholder Thumbnail"
                    fill
                    style={{ objectFit: 'cover' }}
                    className="rounded-md bg-gray-200"
                  />
                )}
              </div>
              <button
                onClick={() => handleCourseSelect(course)}
                className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded mx-auto absolute bottom-4 left-4 right-4"
              >
                Select Course
              </button>
            </div>
          ))}
        </div>
        <RoleSelectionModal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          onSelectRole={handleRoleSelection}
        />
      </div>
    </div>
  )
}

