'use client'

import { useState, useEffect } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import Link from 'next/link'
import ReactMarkdown from 'react-markdown'
import { LessonBasev1_0_0 } from '@/lib/interface-base/lesson.base.v1.0.0'
import { LessonUpdateRequestv1_0_0, LessonUpdateResponsev1_0_0 } from '@/lib/interface-base/lesson.update.v1.0.0';
import { Sun, Moon, ZapOff, ChevronLeft } from 'lucide-react'
import PageHeader from '@/components/learn/PageHeader'
import MarkdownEditor from '@/components/learn/MarkdownEditor'
import { Button } from '@/components/learn/ui/button'
import { Card, CardContent } from "@/components/learn/ui/card"
import { toast } from '@/components/learn/ui/use-toast'

export default function LessonPage({ params }: { params: { id: string } }) {
  const [lesson, setLesson] = useState<LessonBasev1_0_0 | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [originalContent, setOriginalContent] = useState<{[key: number]: string | { content: string } | { title: string; content: string }[]}>({});
  const router = useRouter()
  const searchParams = useSearchParams()
  const userId = searchParams.get('userId')
  const role = searchParams.get('role')
  const courseId = searchParams.get('courseId')
  const moduleId = searchParams.get('moduleId')
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>('dark')
  const [editingSections, setEditingSections] = useState<{[key: number]: boolean}>({})
  const [activeTab, setActiveTab] = useState(0)

  useEffect(() => {
    const fetchLesson = async () => {
      try {
        setIsLoading(true)
        const response = await fetch(`../../../api/learn/content/lesson/${params.id}`)
        if (!response.ok) {
          throw new Error(`Failed to fetch lesson: ${response.statusText}`)
        }
        const lessonData: LessonBasev1_0_0 = await response.json()
        setLesson(lessonData)
      } catch (error) {
        console.error('Error fetching lesson:', error)
        setError(error instanceof Error ? error.message : 'An unknown error occurred')
      } finally {
        setIsLoading(false)
      }
    }

    fetchLesson()
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

  const toggleMode = () => {
    const newMode = mode === 'light' ? 'dark' : mode === 'dark' ? 'high-contrast' : 'light'
    setMode(newMode)
  }

  const handleSave = async (index: number, newContent: string | { content: string } | { title: string; content: string }[], shouldClose: boolean = false) => {
    try {
      if (!lesson) return;

      const updatedContent = [...lesson.content];
      updatedContent[index] = newContent;

      const updateData: LessonUpdateRequestv1_0_0 = {
        id: lesson.id,
        content: updatedContent,
      };

      const response = await fetch(`../../../api/learn/lesson/${params.id}`, {
        method: 'PUT',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(updateData),
      });

      if (!response.ok) {
        throw new Error('Failed to save lesson content');
      }

      const result: LessonUpdateResponsev1_0_0 = await response.json();
      setLesson(result.updatedLesson);
      if (shouldClose) {
        setEditingSections(prev => ({ ...prev, [index]: false }));
      }
      // Show success message
      toast({
        title: "Success",
        description: "Lesson content saved successfully.",
        variant: "default",
      });
    } catch (error) {
      console.error('Error saving lesson content:', error);
      // Show error message
      toast({
        title: "Error",
        description: "Failed to save lesson content. Please try again.",
        variant: "destructive",
      });
    }
  };

  const handleEdit = (index: number) => {
    setOriginalContent(prev => ({ ...prev, [index]: lesson.content[index] }));
    setEditingSections(prev => ({ ...prev, [index]: true }));
  };

  const handleCancelEdit = (index: number) => {
    setEditingSections(prev => ({ ...prev, [index]: false }));
    if (originalContent[index]) {
      const updatedContent = [...lesson.content];
      updatedContent[index] = originalContent[index];
      setLesson(prev => prev ? { ...prev, content: updatedContent } : null);
    }
  };

  if (isLoading) {
    return <div className="min-h-screen bg-gray-100 p-8">Loading lesson...</div>
  }

  if (error) {
    return (
      <div className="min-h-screen bg-gray-100 p-8">
        <h1 className="text-2xl font-bold mb-4 text-red-600">Error</h1>
        <p className="text-red-500">{error}</p>
        <div className="mt-4">
          <Link href={`/learn/course/${courseId}?userId=${userId}&role=${role}`}>
            <span className="text-blue-500 hover:underline">Return to Course</span>
          </Link>
        </div>
      </div>
    )
  }

  if (!lesson) {
    return <div className="min-h-screen bg-gray-100 p-8">Lesson not found.</div>
  }

  const renderContent = (contentItem: string | { content: string } | { title: string; content: string }[], index: number) => {
    const isEditing = editingSections[index] || false;

    const handleEdit = () => {
      setOriginalContent(prev => ({ ...prev, [index]: lesson.content[index] }));
      setEditingSections(prev => ({ ...prev, [index]: true }));
    };

    const handleCancelEdit = () => {
      setEditingSections(prev => ({ ...prev, [index]: false }));
      if (originalContent[index]) {
        const updatedContent = [...lesson.content];
        updatedContent[index] = originalContent[index];
        setLesson(prev => prev ? { ...prev, content: updatedContent } : null);
      }
    };

    if (typeof contentItem === 'string' || 'content' in contentItem) {
      const content = typeof contentItem === 'string' ? contentItem : contentItem.content;
      return (
        <div>
          {role === 'teacher' && !isEditing && (
            <Button onClick={() => handleEdit(index)} className="mb-2">Edit</Button>
          )}
          <Card>
            <CardContent className="p-6">
              {isEditing ? (
                <div>
                  <MarkdownEditor
                    value={content}
                    onChange={(newContent) => {
                      const updatedContent = typeof contentItem === 'string' ? newContent : { ...contentItem, content: newContent };
                      handleSave(index, updatedContent, false);
                    }}
                    mode={mode}
                  />
                  <div className="mt-2 space-x-2">
                    <Button onClick={() => handleSave(index, content, true)} variant="default">Save</Button>
                    <Button onClick={() => handleCancelEdit(index)} variant="outline">Cancel</Button>
                  </div>
                </div>
              ) : (
                <div className="prose dark:prose-invert max-w-none">
                  <ReactMarkdown>{content}</ReactMarkdown>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      );
    } else if (Array.isArray(contentItem)) {
      return (
        <div>
          <div className="flex space-x-2 mb-[-1px]">
            {contentItem.map((tab, tabIndex) => (
              <button
                key={tabIndex}
                onClick={() => setActiveTab(tabIndex)}
                className={`px-4 py-2 text-sm font-medium transition-colors duration-200 ${
                  tabIndex === activeTab
                    ? mode === 'light'
                      ? 'bg-white text-blue-600 border-t-2 border-l-2 border-r-2 border-blue-500 rounded-t-lg'
                      : mode === 'dark'
                      ? 'bg-gray-800 text-blue-400 border-t-2 border-l-2 border-r-2 border-blue-500 rounded-t-lg'
                      : 'bg-black text-yellow-300 border-t-2 border-l-2 border-r-2 border-yellow-300 rounded-t-lg'
                    : mode === 'light'
                    ? 'bg-gray-100 text-gray-600 hover:bg-gray-200 rounded-t-lg'
                    : mode === 'dark'
                    ? 'bg-gray-700 text-gray-300 hover:bg-gray-600 rounded-t-lg'
                    : 'bg-gray-900 text-gray-300 hover:bg-gray-800 rounded-t-lg'
                }`}
              >
                {tab.title}
              </button>
            ))}
          </div>
          {role === 'teacher' && !isEditing && (
            <Button onClick={() => handleEdit(index)} className="mb-2">Edit Active Tab</Button>
          )}
          <Card>
            <CardContent className="p-6">
              {isEditing ? (
                <div>
                  <MarkdownEditor
                    value={contentItem[activeTab].content}
                    onChange={(newContent) => {
                      const updatedTabs = [...contentItem];
                      updatedTabs[activeTab] = { ...updatedTabs[activeTab], content: newContent };
                      handleSave(index, updatedTabs, false);
                    }}
                    mode={mode}
                  />
                  <div className="mt-2 space-x-2">
                    <Button onClick={() => handleSave(index, contentItem, true)} variant="default">Save</Button>
                    <Button onClick={() => handleCancelEdit(index)} variant="outline">Cancel</Button>
                  </div>
                </div>
              ) : (
                <div className="prose dark:prose-invert max-w-none">
                  <ReactMarkdown>{contentItem[activeTab].content}</ReactMarkdown>
                </div>
              )}
            </CardContent>
          </Card>
        </div>
      );
    }
    return null;
  };

  return (
    <div className={`min-h-screen p-8 ${
      mode === 'light' ? 'bg-gray-100 text-gray-900' :
      mode === 'dark' ? 'bg-gray-800 text-gray-100' :
      'bg-black text-yellow-300'
    }`}>
      <div className="max-w-5xl mx-auto">
        <PageHeader
          title={lesson?.title || 'Loading...'}
          backLink={`/learn/course/${courseId}?userId=${userId}&role=${role}`}
          userId={userId}
          mode={mode}
          onModeToggle={toggleMode}
        />
        <div className="space-y-6">
          {lesson?.content.map((contentItem, index) => (
            <div key={index} className="mb-6">
              {renderContent(contentItem, index)}
            </div>
          ))}
        </div>
        <div className="mt-8">
          <Link href={`/learn/course/${courseId}?userId=${userId}&role=${role}`}>
            <button className="bg-blue-500 hover:bg-blue-700 text-white font-bold py-2 px-4 rounded">
              Return to Course
            </button>
          </Link>
        </div>
      </div>
    </div>
  )
}

