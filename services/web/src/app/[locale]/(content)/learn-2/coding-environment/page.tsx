"use client"

import { useState, useEffect, Suspense } from "react"
import { useRouter, useSearchParams } from "next/navigation"
import CodeEditorPanel from "./components/CodeEditorPanel"
import { type QuestionBasev1_0_0, QuestionStatus } from "@/interface-base/question.base.v1.0.0"
import type { QuestionSubmission } from "@/interface-base/question.submission.v1.0.0"
import { toast } from "@/components/ui/use-toast"
import type { HierarchyBasev1_0_0 } from "@/interface-base/structure.base.v1.0.0"
import Loading from "@/app/loading"

const DEFAULT_SANDBOX_QUESTION: QuestionBasev1_0_0 = {
  id: 0,
  format_ver: "1.0.0",
  type: "code",
  rules: ["inputs: y", "outputs: y", "submit: n"],
  status: 1,
  subject: ["sandbox"],
  score: [0, 0],
  title: "Sandbox",
  description:
    "Welcome to the Coding Sandbox! This environment allows you to experiment with code freely. Here's how to use it:\n\n1. Write your code in the editor.\n2. Use the 'Run' button to execute your code.\n3. View the output in the console below.\n4. You can create, rename, or delete files as needed.\n5. Experiment with different programming languages.\n\nFeel free to test ideas, practice coding, or work on small projects. Happy coding!",
  initialCode: {},
  codeName: [],
  inputs: {},
  outputs: {},
  outputsScore: {},
}

export default function CodingEnvironment() {
  const router = useRouter()
  const [question, setQuestion] = useState<QuestionBasev1_0_0 | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [mode, setMode] = useState<"light" | "dark" | "high-contrast">("dark")

  const searchParams = useSearchParams()
  const exerciseId = searchParams ? searchParams.get("id") : null
  const exerciseType = searchParams ? searchParams.get("type") : null
  const assessmentId = searchParams ? searchParams.get("assessmentId") : null
  const userId = searchParams ? searchParams.get("userId") : null
  const role = searchParams ? searchParams.get("role") : null
  const courseId = searchParams ? searchParams.get("courseId") : null
  const moduleId = searchParams ? searchParams.get("moduleId") : null

  useEffect(() => {
    const fetchQuestion = async () => {
      if (!exerciseId || !exerciseType) {
        // Use the default sandbox question
        setQuestion(DEFAULT_SANDBOX_QUESTION)
        setIsLoading(false)
        return
      }

      setIsLoading(true)
      setError(null)
      try {
        const response = await fetch(`/api/content/${exerciseType}/${exerciseId}`)
        if (!response.ok) {
          const errorData = await response.json()
          throw new Error(`HTTP error! status: ${response.status}, message: ${errorData.error}`)
        }
        const data = await response.json()
        setQuestion(data)
      } catch (error) {
        console.error("Error fetching question:", error)
        setError(`Failed to load question: ${error.message}`)
        toast({
          title: "Error",
          description: `Failed to load question: ${error.message}`,
          variant: "destructive",
        })
      } finally {
        setIsLoading(false)
      }
    }

    fetchQuestion()
  }, [exerciseId, exerciseType])

  useEffect(() => {
    const storedMode = localStorage.getItem("colorMode") as "light" | "dark" | "high-contrast" | null
    if (storedMode) {
      setMode(storedMode)
    }
  }, [])

  useEffect(() => {
    localStorage.setItem("colorMode", mode)
  }, [mode])

  const handleReturn = () => {
    if (assessmentId && courseId && moduleId && userId && role) {
      router.push(`/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`)
    } else {
      router.push("/courses")
    }
  }

  const handleSubmit = async (submission: QuestionSubmission) => {
    try {
      submission.status = QuestionStatus.Submitted

      const response = await fetch("/api/submit-assignment", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(submission),
      })

      if (response.ok) {
        toast({
          title: "Success",
          description: "Assignment submitted successfully!",
        })

        if (assessmentId && courseId && moduleId && userId && role)
          router.push(
            `/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`,
          )
        else router.push("/courses")
      } else {
        const errorData = await response.json()
        throw new Error(`Failed to submit assignment: ${errorData.error}`)
      }
    } catch (error) {
      console.error("Error submitting assignment:", error)
      toast({
        title: "Error",
        description: `Failed to submit assignment: ${error.message}`,
        variant: "destructive",
      })
    }
  }

  if (isLoading) {
    return <Loading />
  }

  if (error) {
    return <div>Error: {error}</div>
  }

  if (!question || !userId || !role) {
    return <div>No assignment data available.</div>
  }

  const hierarchy: HierarchyBasev1_0_0 = {
    idHierarchy: [Number(courseId), Number(moduleId), Number(assessmentId), question.id],
    idUser: userId,
    idRole: role,
    colorMode: mode,
  }

  return (
    <div className="h-screen flex flex-col">
      <CodeEditorPanel assignment={question} onReturn={handleReturn} onSubmit={handleSubmit} hierarchy={hierarchy} />
    </div>
  )
}

