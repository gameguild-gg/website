import { useState, useEffect } from 'react'
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Textarea } from "@/components/ui/textarea"
import { FileUploader } from './file-uploader'
import { useCourseStore } from '@/store/courseStore'

interface LessonFormProps {
  lesson?: {
    id: string;
    order: number;
    title: string;
    description: string;
    thumbnail: File | null;
    content: File | null;
    additionalText: string;
    extraContent: File[];
  };
  onSave?: (lesson: any) => void;
  onCancel?: () => void;
}

export function LessonForm({ lesson, onSave, onCancel }: LessonFormProps) {
  const addLesson = useCourseStore((state) => state.addLesson)
  const [formData, setFormData] = useState({
    title: '',
    description: '',
    thumbnail: null,
    content: null,
    additionalText: '',
    extraContent: [],
  })

  useEffect(() => {
    if (lesson) {
      setFormData(lesson)
    }
  }, [lesson])

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target
    setFormData(prev => ({ ...prev, [name]: value }))
  }

  const handleFileUpload = (file: File, type: 'thumbnail' | 'content' | 'extra') => {
    if (type === 'thumbnail' || type === 'content') {
      setFormData(prev => ({ ...prev, [type]: file }))
    } else {
      setFormData(prev => ({ ...prev, extraContent: [...prev.extraContent, file] }))
    }
  }

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault()
    if (lesson) {
      onSave && onSave(formData)
    } else {
      addLesson({ ...formData, id: Date.now().toString() })
      setFormData({
        title: '',
        description: '',
        thumbnail: null,
        content: null,
        additionalText: '',
        extraContent: [],
      })
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      <Input
        name="title"
        placeholder="Lesson Title"
        value={formData.title}
        onChange={handleInputChange}
        required
      />
      <Textarea
        name="description"
        placeholder="Lesson Description"
        value={formData.description}
        onChange={handleInputChange}
        required
      />
      <FileUploader
        onFileSelect={(file) => handleFileUpload(file, 'thumbnail')}
        accept="image/*"
        label="Upload Lesson Thumbnail"
      />
      <FileUploader
        onFileSelect={(file) => handleFileUpload(file, 'content')}
        accept="video/*,application/pdf"
        label="Upload Lesson Content"
      />
      <Textarea
        name="additionalText"
        placeholder="Additional Text"
        value={formData.additionalText}
        onChange={handleInputChange}
      />
      <FileUploader
        onFileSelect={(file) => handleFileUpload(file, 'extra')}
        accept="*/*"
        label="Upload Extra Content"
        multiple
      />
      <div className="flex justify-end space-x-2">
        {onCancel && (
          <Button type="button" variant="outline" onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button type="submit">{lesson ? 'Save Changes' : 'Add Lesson'}</Button>
      </div>
    </form>
  )
}

