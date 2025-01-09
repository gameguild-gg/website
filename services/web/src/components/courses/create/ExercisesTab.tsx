'use client'

import { useEffect, useState } from 'react'
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

interface Module {
  id: string;
  order: string;
  title: string;
}

interface Lesson {
  id: string;
  title: string;
  order: string;
  moduleId: string;
}

export default function ExercisesTab({ exercises, modules, lessons, updateData }) {
  const { register, control, handleSubmit, reset, watch, setValue } = useForm<Exercise>({
    defaultValues: {
      moduleId: '',
      lessonId: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [], 
      time: { hours: 0, minutes: 0 },
      averageGrades: 'no average'
    }
  })
  const [editingIndex, setEditingIndex] = useState(-1)

  const watchModuleId = watch("moduleId");
  const watchLessonId = watch("lessonId");

  useEffect(() => {
    if (watchModuleId === 'none') {
      reset((formValues) => ({
        ...formValues,
        lessonId: 'none',
        averageGrades: 'no average'
      }));
    } else {
      const currentLesson = lessons.find(lesson => lesson.id === watchLessonId);
      if (currentLesson && currentLesson.moduleId !== watchModuleId) {
        reset((formValues) => ({
          ...formValues,
          lessonId: 'none',
          averageGrades: 'module average'
        }));
      } else {
        setValue('averageGrades', 'module average');
      }
    }
  }, [watchModuleId, reset, lessons, watchLessonId, setValue]);

  useEffect(() => {
    if (watchLessonId && watchLessonId !== 'none') {
      setValue('averageGrades', 'lesson average');
    }
  }, [watchLessonId, setValue]);

  const getNextExerciseId = () => {
    if (exercises.length === 0) return '10';
    const maxId = Math.max(...exercises.map(e => parseInt(e.id)));
    return (maxId + 1).toString();
  }

  const onSubmit = (data: Exercise) => {
    const submissionData = {
      ...data,
      id: editingIndex === -1 ? getNextExerciseId() : exercises[editingIndex].id,
      moduleId: data.moduleId === 'none' ? '' : data.moduleId,
      lessonId: data.lessonId === 'none' ? '' : data.lessonId,
    };

    if (editingIndex === -1) {
      updateData([...exercises, submissionData])
    } else {
      const updatedExercises = [...exercises]
      updatedExercises[editingIndex] = submissionData
      updateData(updatedExercises)
      setEditingIndex(-1)
    }
    
    reset({
      moduleId: '',
      lessonId: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [], 
      time: { hours: 0, minutes: 0 },
      averageGrades: 'no average'
    })
  }

  const handleEdit = (index: number) => {
    setEditingIndex(index)
    reset(exercises[index])
  }

  const handleRemove = (index: number) => {
    const updatedExercises = exercises.filter((_, i) => i !== index)
    updateData(updatedExercises)
  }

  const renderSpecificFields = (exercise: Exercise) => {
    return (
      <>
        <p><strong>Time:</strong> {exercise.time.hours}h {exercise.time.minutes}m</p>
        <p><strong>Average Grades:</strong> {exercise.averageGrades}</p>
        <div className="text-sm">
          <strong>Content Links:</strong>
          <ul className="list-disc pl-5">
            {exercise.contentLinks && exercise.contentLinks
              .sort((a, b) => Number(a.order) - Number(b.order))
              .map((item, linkIndex) => (
                <li key={linkIndex}>
                  Order: {item.order}, Link: <a href={item.link} target="_blank" rel="noopener noreferrer" className="text-blue-500 hover:underline">{item.link}</a>
                </li>
              ))}
          </ul>
        </div>
      </>
    );
  }

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div>
            <Label htmlFor="module">Module (Optional)</Label>
            <Controller
              name="moduleId"
              control={control}
              render={({ field }) => (
                <Select onValueChange={field.onChange} value={field.value || undefined}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select module" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">None</SelectItem>
                    {modules && modules.map((module) => (
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
            <Label htmlFor="lesson">Lesson (Optional)</Label>
            <Controller
              name="lessonId"
              control={control}
              render={({ field }) => (
                <Select onValueChange={field.onChange} value={field.value || undefined}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select lesson" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none">None</SelectItem>
                    {lessons
                      .filter(lesson => watchModuleId === 'none' || lesson.moduleId === watchModuleId)
                      .map((lesson) => (
                        <SelectItem key={lesson.id} value={lesson.id}>
                          {lesson.title || `Untitled Lesson ${lesson.order}`}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              )}
            />
          </div>
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
        <div className="grid grid-cols-3 gap-4">
          <div>
            <Label>Time (Hours)</Label>
            <Input
              type="number"
              {...register('time.hours', { valueAsNumber: true })}
              placeholder="Hours"
            />
          </div>
          <div>
            <Label>Time (Minutes)</Label>
            <Input
              type="number"
              {...register('time.minutes', { valueAsNumber: true })}
              placeholder="Minutes"
            />
          </div>
          <div>
            <Label htmlFor="averageGrades">Average Grades</Label>
            <Controller
              name="averageGrades"
              control={control}
              render={({ field }) => (
                <Select onValueChange={field.onChange} value={field.value}>
                  <SelectTrigger>
                    <SelectValue placeholder="Select average type" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="no average">No Average</SelectItem>
                    <SelectItem value="lesson average">Lesson Average</SelectItem>
                    <SelectItem value="module average">Module Average</SelectItem>
                    <SelectItem value="course average">Course Average</SelectItem>
                  </SelectContent>
                </Select>
              )}
            />
          </div>
        </div>
        <div>
          <Label>Content Links</Label>
          <ContentLinks control={control} name="contentLinks" />
        </div>
        <Button type="submit">Save Exercise</Button>
      </form>
      <SavedItemsDisplay
        items={exercises}
        itemType="exercise"
        onEdit={handleEdit}
        onRemove={handleRemove}
        renderSpecificFields={renderSpecificFields}
        groupByParent={true}
        parentItems={lessons}
        grandParentItems={modules}
      />
    </div>
  )
}

