'use client'

import { useState, useEffect } from 'react'
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Button } from "@/components/ui/button"
import InformationTab from '@/components/courses/create/InformationTab'
import ModulesTab from '@/components/courses/create/ModulesTab'
import LessonsTab from '@/components/courses/create/LessonsTab'
import ExercisesTab from '@/components/courses/create/ExercisesTab'
import QuestionsTab from '@/components/courses/create/QuestionsTab'
import PricingTab from '@/components/courses/create/PricingTab'
import FinalTab from '@/components/courses/create/FinalTab'
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert"
import { AlertCircle, CheckCircle2 } from 'lucide-react'

interface ContentLink {
  order: string;
  link: string;
}

interface Lesson {
  id: string;
  moduleId: string;
  order: string;
  title: string;
  description: string;
  contentLinks: ContentLink[];
  additionalText: string;
}

interface Exercise {
  id: string;
  moduleId?: string;
  lessonId?: string;
  order: string;
  title: string;
  description: string;
  contentLinks: ContentLink[];
  time: {
    hours: number;
    minutes: number;
  };
  averageGrades: 'no average' | 'lesson average' | 'module average' | 'course average';
}

interface Question {
  id: string;
  exerciseId: string;
  order: number;
  type: string;
  question: string;
  correctScore: number | undefined;
  incorrectScore: number | undefined;
  time: number | undefined;
  contentLinks: ContentLink[];
  // Additional fields will be added dynamically based on the question type
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
  exercises: Exercise[];
  questions: Question[];
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
    questions: [],
    pricing: {
      priceInstallments: [],
      fullPrice: 0,
      maxDiscount: 0,
      maxUnits: 0,
      launchDate: '',
      closingDate: ''
    }
  })
  const [unsavedChanges, setUnsavedChanges] = useState(false)
  const [saveConfirmed, setSaveConfirmed] = useState(false)


  useEffect(() => {
    if (courseId !== 'new') {
      const api = new CoursesApi({
        basePath: process.env.NEXT_PUBLIC_API_URL,
      });

      const fetchCourseData = async () => {
        try {
          const session = await auth();
          if (!session || !session.user?.accessToken) {
            throw new Error('No valid session found');
          }

          const response = await api.getOneBaseCoursesControllerCourseEntity(
            { slug: courseId }, // Ajuste para utilizar o ID do curso
            { headers: { Authorization: `Bearer ${session.user.accessToken}` } }
          );

          // Atualiza o estado com os dados do curso retornados
          setCourseData((prev) => ({
            ...prev,
            ...response.body,
          }));
        } catch (error) {
          console.error('Error fetching course data:', error);
        }
      };

      fetchCourseData();
    }
  }, [courseId]);

  const updateCourseData = (tab: keyof CourseData, data: any) => {
    setCourseData(prev => {
      const newData = { ...prev, [tab]: data }
      // Auto-save logic
      saveCourseData(newData)
      return newData
    })
    setUnsavedChanges(false)
    setSaveConfirmed(true)
    setTimeout(() => setSaveConfirmed(false), 3000) // Hide the confirmation after 3 seconds
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
      {unsavedChanges && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Warning</AlertTitle>
          <AlertDescription>
            You have unsaved changes. Please save your data before leaving this page.
          </AlertDescription>
        </Alert>
      )}
      {saveConfirmed && (
        <Alert variant="default" className="bg-green-50 border-green-200">
          <CheckCircle2 className="h-4 w-4 text-green-500" />
          <AlertTitle>Success</AlertTitle>
          <AlertDescription>
            Your changes have been saved successfully.
          </AlertDescription>
        </Alert>
      )}
      <Tabs defaultValue="information" className="w-full">
        <div className="flex justify-center mb-6">
          <TabsList className="grid grid-cols-3 sm:grid-cols-7 gap-2">
            <TabsTrigger value="information">Information</TabsTrigger>
            <TabsTrigger value="modules">Modules</TabsTrigger>
            <TabsTrigger value="lessons">Lessons</TabsTrigger>
            <TabsTrigger value="exercises">Exercises</TabsTrigger>
            <TabsTrigger value="questions">Questions</TabsTrigger>
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
            lessons={courseData.lessons}
            updateData={(data) => updateCourseData('exercises', data)} 
          />
        </TabsContent>
        <TabsContent value="questions">
          <QuestionsTab 
            questions={courseData.questions}
            exercises={courseData.exercises}
            updateData={(data) => updateCourseData('questions', data)}
          />
        </TabsContent>
        <TabsContent value="pricing">
          <PricingTab 
            data={courseData.pricing} 
            updateData={(data) => updateCourseData('pricing', data)} 
            setUnsavedChanges={setUnsavedChanges}
          />
        </TabsContent>
        <TabsContent value="final">
          <FinalTab courseData={courseData} />
        </TabsContent>
      </Tabs>
    </div>
  )
}