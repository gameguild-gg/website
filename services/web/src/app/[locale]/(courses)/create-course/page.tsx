'use client'

import { useEffect } from 'react'
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { FileUploader } from '@/components/courses/create/file-uploader'
import { LessonForm } from '@/components/courses/create/lesson-form'
import { LessonList } from '@/components/courses/create/lesson-list'
import { PricingForm } from '@/components/courses/create/pricing-form'
import { useCourseStore } from '@/store/courseStore'

export default function CreateCourse() {
  const { title, description, setTitle, setDescription, setThumbnail, setDemoContent } = useCourseStore()

  useEffect(() => {
    // Load data from localStorage if available
    const savedCourse = localStorage.getItem('course-storage')
    if (savedCourse) {
      const parsedCourse = JSON.parse(savedCourse)
      setTitle(parsedCourse.title)
      setDescription(parsedCourse.description)
    }
  }, [])

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target
    if (name === 'title') setTitle(value)
    if (name === 'description') setDescription(value)
  }

  const handleFileUpload = (file: File, type: 'thumbnail' | 'demo') => {
    if (type === 'thumbnail') {
      setThumbnail(file)
    } else {
      setDemoContent([file])
    }
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    const courseData = useCourseStore.getState()
    console.log('Submitting course data:', courseData)
    // Implement your API call here
    // await api.createCourse(courseData)
  }

  return (
    <form onSubmit={handleSubmit} className="container mx-auto px-4 py-8">
      <h1 className="text-3xl font-bold mb-6">Create New Course</h1>
      
      <Tabs defaultValue="info" className="mb-6">
        <TabsList>
          <TabsTrigger value="info">Course Info</TabsTrigger>
          <TabsTrigger value="content">Add Lesson</TabsTrigger>
          <TabsTrigger value="lessons">Lesson List</TabsTrigger>
          <TabsTrigger value="pricing">Pricing & Publication</TabsTrigger>
        </TabsList>
        
        <TabsContent value="info">
          <Card>
            <CardHeader>
              <CardTitle>Course Information</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <Input
                name="title"
                placeholder="Course Title"
                value={title}
                onChange={handleInputChange}
                required
              />
              <Textarea
                name="description"
                placeholder="Course Description"
                value={description}
                onChange={handleInputChange}
                required
              />
              <FileUploader
                onFileSelect={(file) => handleFileUpload(file, 'thumbnail')}
                accept="image/*"
                label="Upload Thumbnail"
              />
              <FileUploader
                onFileSelect={(file) => handleFileUpload(file, 'demo')}
                accept="image/*,video/*"
                label="Upload Demo Content"
                multiple
              />
            </CardContent>
          </Card>
        </TabsContent>
        
        <TabsContent value="content">
          <Card>
            <CardHeader>
              <CardTitle>Add New Lesson</CardTitle>
            </CardHeader>
            <CardContent>
              <LessonForm />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="lessons">
          <Card>
            <CardHeader>
              <CardTitle>Lesson List</CardTitle>
            </CardHeader>
            <CardContent>
              <LessonList />
            </CardContent>
          </Card>
        </TabsContent>
        
        <TabsContent value="pricing">
          <PricingForm />
        </TabsContent>
      </Tabs>
      
      <Button type="submit" size="lg">Create Course</Button>
    </form>
  )
}

