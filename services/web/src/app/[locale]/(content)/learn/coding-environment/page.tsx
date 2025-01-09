'use client'

import { useState, useEffect } from 'react'
import { useRouter, useSearchParams } from 'next/navigation'
import CodeEditorPanel from './components/CodeEditorPanel'
import { QuestionStatus, CodeQuestionv1_0_0 } from '@/lib/interface-base/question.base.v1.0.0'
import { QuestionSubmissionv1_0_0 } from '@/lib/interface-base/question.submission.v1.0.0'
import { toast } from '@/components/learn/ui/use-toast'
import { HierarchyBasev1_0_0 } from '@/lib/interface-base/structure.base.v1.0.0'; // Import HierarchyBasev1_0_0

export default function CodingEnvironment() {
  const [question, setQuestion] = useState<CodeQuestionv1_0_0 | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const router = useRouter()
  const searchParams = useSearchParams()
  const exerciseId = searchParams.get('id')
  const exerciseType = searchParams.get('type')
  const assessmentId = searchParams.get('assessmentId')
  const userId = searchParams.get('userId')
  const role = searchParams.get('role')
  const courseId = searchParams.get('courseId')
  const moduleId = searchParams.get('moduleId')
  const [mode, setMode] = useState<'light' | 'dark' | 'high-contrast'>('dark') // State for color mode


  useEffect(() => {
    const fetchQuestion = async () => {
      if (!exerciseId || !exerciseType) {
        setError('No exercise specified')
        setIsLoading(false)
        return
      }

      setIsLoading(true)
      setError(null)
      try {
        const response = await fetch(`../../api/learn/content/${exerciseType}/${exerciseId}`);
        if (!response.ok) {
          const errorData = await response.json();
          throw new Error(`HTTP error! status: ${response.status}, message: ${errorData.error}`);
        }
        const data = await response.json();
        setQuestion(data);
      } catch (error) {
        console.error('Error fetching question:', error);
        setError(`Failed to load question: ${error}`);
        toast({
          title: "Error",
          description: `Failed to load question: ${error}`,
          variant: "destructive",
        });
      } finally {
        setIsLoading(false);
      }
    };

    fetchQuestion()
  }, [exerciseId, exerciseType])
  
  useEffect(() => {
    const storedMode = localStorage.getItem('colorMode') as 'light' | 'dark' | 'high-contrast' | null;
    if (storedMode) {
      setMode(storedMode);
    }
  }, []);

  useEffect(() => {
    localStorage.setItem('colorMode', mode);
  }, [mode]);

  const handleReturn = () => {
    if (assessmentId && courseId && moduleId && userId && role)
      router.push(`/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`)
    else router.push('/learn/courses')
  }

  const handleSubmit = async (submission: QuestionSubmissionv1_0_0) => {
    try {
      // Update the submission status
      submission.status = QuestionStatus.Submitted;

      const response = await fetch('../../api/learn/submit-codeQuestion', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(submission),
      })

      if (response.ok) {
        toast({
          title: "Success",
          description: "Assignment submitted successfully!",
        })

        if (assessmentId && courseId && moduleId && userId && role)
          router.push(`/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`)
        else router.push('/learn/courses')
      } else {
        const errorData = await response.json();
        throw new Error(`Failed to submit codeQuestion: ${errorData.error}`);
      }
    } catch (error) {
      console.error('Error submitting codeQuestion:', error)
      toast({
        title: "Error",
        description: `Failed to submit codeQuestion: ${error}`,
        variant: "destructive",
      })
    }
  }

  if (isLoading) {
    return <div>Loading...</div>
  }

  if (error) {
    return <div>Error: {error}</div>
  }

  if (!question) {
    return <div>No codeQuestion data available.</div>
  }
  
  // Construct hierarchy object
  const hierarchy: HierarchyBasev1_0_0 = {
    idHierarchy: [Number(courseId), Number(moduleId), Number(assessmentId), question.id],
    idUser: userId!,
    idRole: role!,
    colorMode: mode // Include colorMode in hierarchy
  };

  return (
    <div className="h-screen flex flex-col">
      <CodeEditorPanel
        codeQuestion={question}
        onReturn={handleReturn}
        onSubmit={handleSubmit}
        hierarchy={hierarchy} // Pass hierarchy to CodeEditorPanel
        />
    </div>
  )
}

