'use client'

import { useState, useEffect } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import ContentLinks from '../ContentLinks'
import SavedItemsDisplay from '../SavedItemsDisplay'

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

interface Module {
  id: string;
  order: string;
  title: string;
}

export default function LessonsTab({ lessons, modules, updateData }) {
  const { register, control, handleSubmit, reset, setValue } = useForm<Lesson>({
    defaultValues: {
      moduleId: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [],
      additionalText: '',
      id: ''
    }
  })
  const [editingIndex, setEditingIndex] = useState(-1)

  const getNextLessonId = () => {
    if (lessons.length === 0) return '10';
    const maxId = Math.max(...lessons.map(l => parseInt(l.id)));
    return (maxId + 1).toString();
  }

  useEffect(() => {
    if (editingIndex !== -1 && lessons[editingIndex]) {
      reset(lessons[editingIndex])
    }
  }, [editingIndex, lessons, reset])

  const onSubmit = (data: Lesson) => {
    const submissionData = {
      ...data,
      moduleId: data.moduleId === 'none' ? '' : data.moduleId,
      id: editingIndex === -1 ? getNextLessonId() : lessons[editingIndex].id
    };

    if (editingIndex === -1) {
      updateData([...lessons, submissionData])
    } else {
      const updatedLessons = [...lessons]
      updatedLessons[editingIndex] = submissionData
      updateData(updatedLessons)
      setEditingIndex(-1)
    }
    reset()
  }

  const handleEdit = (index: number) => {
    setEditingIndex(index)
  }

  const handleRemove = (index: number) => {
    const updatedLessons = lessons.filter((_, i) => i !== index)
    updateData(updatedLessons)
  }

  const getChildLessons = (lesson: Lesson) => {
    return []; // Lessons don't have child items
  }

  const renderSpecificFields = (lesson: Lesson) => {
    return (
      <>
        <div className="text-sm">
          <strong>Content Links:</strong>
          <ul className="list-disc pl-5">
            {lesson.contentLinks && lesson.contentLinks
              .sort((a, b) => Number(a.order) - Number(b.order))
              .map((item, linkIndex) => (
                <li key={linkIndex}>
                  Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline">{item.link}</a>
                </li>
              ))}
          </ul>
        </div>
        <div className="mt-2">
          <strong>Additional Text:</strong>
          <p className="text-sm">{lesson.additionalText}</p>
        </div>
      </>
    );
  }

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="moduleId">Module</Label>
          <Controller
            name="moduleId"
            control={control}
            render={({ field }) => (
              <Select onValueChange={field.onChange} value={field.value}>
                <SelectTrigger>
                  <SelectValue placeholder="Select module" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">None</SelectItem>
                  {modules.map((module) => (
                    <SelectItem key={module.id} value={module.id}>
                      {module.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
        </div>
        <div>
          <Label htmlFor="order">Order</Label>
          <Input id="order" type="number" {...register('order')} />
        </div>
        <div>
          <Label htmlFor="title">Title</Label>
          <Input id="title" {...register('title')} />
        </div>
        <div>
          <Label htmlFor="description">Description</Label>
          <Textarea id="description" {...register('description')} />
        </div>
        <div>
          <Label>Content Links</Label>
          <ContentLinks control={control} name="contentLinks" />
        </div>
        <div>
          <Label htmlFor="additionalText">Additional Text</Label>
          <Textarea id="additionalText" {...register('additionalText')} className="min-h-[200px]" />
        </div>
        <Button type="submit">Save Lesson</Button>
      </form>
      <SavedItemsDisplay
        items={lessons}
        itemType="lesson"
        onEdit={handleEdit}
        onRemove={handleRemove}
        renderSpecificFields={renderSpecificFields}
        groupByParent={true}
        parentItems={modules}
      />
    </div>
  )
}

