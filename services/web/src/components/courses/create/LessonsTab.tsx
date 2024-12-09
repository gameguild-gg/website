'use client'

import { useState, useEffect } from 'react'
import { useForm, useFieldArray, Controller } from 'react-hook-form'
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { Button } from "@/components/ui/button"
import { Label } from "@/components/ui/label"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"
import { ChevronDown, ChevronRight } from 'lucide-react'

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
}

export default function LessonsTab({ lessons, modules, updateData }) {
  const { register, control, handleSubmit, reset, setValue } = useForm<Lesson>({
    defaultValues: {
      module: '',
      order: '',
      title: '',
      description: '',
      contentLinks: [{ order: '', link: '' }],
      additionalText: '',
    }
  })
  const { fields, append, remove } = useFieldArray({
    control,
    name: "contentLinks"
  })
  const [editingIndex, setEditingIndex] = useState(-1)
  const [expandedModules, setExpandedModules] = useState<string[]>([])

  useEffect(() => {
    if (editingIndex !== -1 && lessons[editingIndex]) {
      reset(lessons[editingIndex])
    }
  }, [editingIndex, lessons, reset])

  const onSubmit = (data: Lesson) => {
    if (editingIndex === -1) {
      updateData([...lessons, data])
    } else {
      const updatedLessons = [...lessons]
      updatedLessons[editingIndex] = data
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

  const toggleModule = (moduleTitle: string) => {
    setExpandedModules(prev => 
      prev.includes(moduleTitle)
        ? prev.filter(title => title !== moduleTitle)
        : [...prev, moduleTitle]
    )
  }

  const groupedLessons = lessons.reduce((acc, lesson) => {
    if (!acc[lesson.module]) {
      acc[lesson.module] = []
    }
    acc[lesson.module].push(lesson)
    return acc
  }, {} as Record<string, Lesson[]>)

  const sortedModules = [...modules].sort((a, b) => Number(a.order) - Number(b.order))

  return (
    <div className="grid md:grid-cols-2 gap-4">
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        <div>
          <Label htmlFor="module">Module</Label>
          <Controller
            name="module"
            control={control}
            render={({ field }) => (
              <Select onValueChange={field.onChange} value={field.value || ""}>
                <SelectTrigger>
                  <SelectValue placeholder="Select module" />
                </SelectTrigger>
                <SelectContent>
                  {modules.map((module, index) => (
                    <SelectItem key={index} value={module.title}>
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
          {fields.map((field, index) => (
            <div key={field.id} className="flex items-center space-x-2 mt-2">
              <Input
                {...register(`contentLinks.${index}.order`)}
                placeholder="Order"
                type="number"
              />
              <Input
                {...register(`contentLinks.${index}.link`)}
                placeholder="Link"
              />
              <Button type="button" onClick={() => remove(index)} variant="destructive" size="sm">
                Remove
              </Button>
            </div>
          ))}
          <Button
            type="button"
            onClick={() => append({ order: '', link: '' })}
            variant="outline"
            size="sm"
            className="mt-2"
          >
            Add New Content
          </Button>
        </div>
        <div>
          <Label htmlFor="additionalText">Additional Text</Label>
          <Textarea id="additionalText" {...register('additionalText')} className="min-h-[200px]" />
        </div>
        <Button type="submit">Save Lesson</Button>
      </form>
      <div>
        <h3 className="text-lg font-semibold mb-2">Saved Lessons</h3>
        <ul className="space-y-2">
          {sortedModules.map((module) => {
            const moduleLessons = groupedLessons[module.title] || []
            if (moduleLessons.length === 0) return null
            
            return (
              <li key={module.title} className="bg-gray-100 p-4 rounded-md">
                <div 
                  className="flex items-center justify-between cursor-pointer"
                  onClick={() => toggleModule(module.title)}
                >
                  <span className="font-semibold">
                    {expandedModules.includes(module.title) ? <ChevronDown className="inline mr-2" /> : <ChevronRight className="inline mr-2" />}
                    Module {module.order}: {module.title}
                  </span>
                  <span>{moduleLessons.length} lesson(s)</span>
                </div>
                {expandedModules.includes(module.title) && (
                  <ul className="mt-2 space-y-2">
                    {moduleLessons
                      .sort((a, b) => Number(a.order) - Number(b.order))
                      .map((lesson, lessonIndex) => (
                        <li key={lessonIndex} className="bg-white p-3 rounded-md">
                          <div className="flex justify-between items-center mb-2">
                            <span className="font-semibold">Lesson {lesson.order}: {lesson.title}</span>
                            <div className="space-x-2">
                              <Button onClick={() => handleEdit(lessons.findIndex(l => l === lesson))} variant="outline" size="sm">Edit</Button>
                              <Button onClick={() => handleRemove(lessons.findIndex(l => l === lesson))} variant="destructive" size="sm">Remove</Button>
                            </div>
                          </div>
                          <p className="text-sm text-gray-600 mb-2">{lesson.description}</p>
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
                        </li>
                    ))}
                  </ul>
                )}
              </li>
            )
          })}
        </ul>
      </div>
    </div>
  )
}

