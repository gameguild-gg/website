'use client'

import { useState, useEffect } from 'react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Button } from "@/components/ui/button"
import InformationTab from '@/components/courses/create/InformationTab'
import ModulesTab from '@/components/courses/create/ModulesTab'
import LessonsTab from '@/components/courses/create/LessonsTab'
import ExercisesTab from '@/components/courses/create/ExercisesTab'
import PricingTab from '@/components/courses/create/PricingTab'
import FinalTab from '@/components/courses/create/FinalTab'

interface ContentLink {
  order: string;
  link: string;
}

interface Lesson {
  module: string;
  order: string;
  title: string;
  description: string;
  contentLinks: ContentLink[];
  additionalText: string;
  hasExercise: boolean;
}

interface PriceInstallment {
  installments: number;
  price: number;
}

interface PricingData {
  priceInstallments: PriceInstallment[];
  fullPrice: number;
  maxDiscount: number;
  maxUnits: number;
  launchDate: string;
  closingDate: string;
}

interface CourseData {
  id: string;
  information: {
    title: string;
    shortDescription: string;
    fullDescription: string;
    customUrl: string;
    thumbnail: string;
    contentDemoLinks: ContentLink[];
  };
  modules: any[];
  lessons: Lesson[];
  exercises: any[];
  pricing: PricingData;
}

export default function CourseTabs({ courseId, onBack }: { courseId: string, onBack: () => void }) {
  const [courseData, setCourseData] = useState<CourseData>({
    id: courseId,
    information: {
      title: '',
      shortDescription: '',
      fullDescription: '',
      customUrl: '',
      thumbnail: '',
      contentDemoLinks: []
    },
    modules: [],
    lessons: [],
    exercises: [],
    pricing: {
      priceInstallments: [],
      fullPrice: 0,
      maxDiscount: 0,
      maxUnits: 0,
      launchDate: '',
      closingDate: ''
    }
  })

  useEffect(() => {
    if (courseId !== 'new') {
      // Fetch course data from the server
      const fetchCourseData = async () => {
        // Replace this with actual API call
        const response = await fetch(`/api/courses/${courseId}`)
        const data = await response.json()
        setCourseData(data)
      }

      fetchCourseData()
    }
  }, [courseId])

  const updateCourseData = (tab: keyof CourseData, data: any) => {
    setCourseData(prev => {
      const newData = { ...prev, [tab]: data }
      // Auto-save logic
      saveCourseData(newData)
      return newData
    })
  }

  const saveCourseData = async (data: CourseData) => {
    // Replace this with actual API call
    await fetch(`/api/courses/${courseId}`, {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(data),
    })
  }

  return (
    <div className="space-y-4">
      <Button onClick={onBack} variant="outline">Back to Course List</Button>
      <Tabs defaultValue="information" className="w-full">
        <div className="flex justify-center mb-6">
          <TabsList className="grid grid-cols-3 sm:grid-cols-6 gap-2">
            <TabsTrigger value="information">Information</TabsTrigger>
            <TabsTrigger value="modules">Modules</TabsTrigger>
            <TabsTrigger value="lessons">Lessons</TabsTrigger>
            <TabsTrigger value="exercises">Exercises</TabsTrigger>
            <TabsTrigger value="pricing">Pricing</TabsTrigger>
            <TabsTrigger value="final">Final</TabsTrigger>
          </TabsList>
        </div>
        <TabsContent value="information">
          <InformationTab data={courseData.information} updateData={(data) => updateCourseData('information', data)} />
        </TabsContent>
        <TabsContent value="modules">
          <ModulesTab 
            modules={courseData.modules} 
            updateData={(data) => updateCourseData('modules', data)} 
          />
        </TabsContent>
        <TabsContent value="lessons">
          <LessonsTab 
            lessons={courseData.lessons} 
            modules={courseData.modules}
            updateData={(data) => updateCourseData('lessons', data)} 
          />
        </TabsContent>
        <TabsContent value="exercises">
          <ExercisesTab 
            exercises={courseData.exercises} 
            modules={courseData.modules}
            lessons={courseData.lessons.filter(lesson => lesson.hasExercise)}
            updateData={(data) => updateCourseData('exercises', data)} 
          />
        </TabsContent>
        <TabsContent value="pricing">
          <PricingTab data={courseData.pricing} updateData={(data) => updateCourseData('pricing', data)} />
        </TabsContent>
        <TabsContent value="final">
          <FinalTab courseData={courseData} />
        </TabsContent>
      </Tabs>
    </div>
  )
}

