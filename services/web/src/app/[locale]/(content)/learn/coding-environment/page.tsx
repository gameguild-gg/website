"use client"

import { useState, useEffect, Suspense } from "react"
import { useRouter, useSearchParams } from "next/navigation"
import CodeEditorPanel from "./components/CodeEditorPanel"
import { type CodeQuestionv1_0_0, QuestionStatus } from "@/lib/interface-base/question.base.v1.0.0"
import type { CodeQuestionSubmissionv1_0_0, QuestionSubmissionBasev1_0_0 } from "@/lib/interface-base/question.submission.v1.0.0"
import { toast } from "@/components/ui/use-toast"
import type { HierarchyBasev1_0_0 } from "@/lib/interface-base/structure.base.v1.0.0"
import Loading from "../loading"
import { DEFAULT_SANDBOX_QUESTION } from "./defaultSandboxQuestion"

export default function CodingEnvironment() {
  const router = useRouter()
  const [question, setQuestion] = useState<CodeQuestionv1_0_0 | null>(null)
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

  const isSandboxMode = exerciseType === "sandbox"

  useEffect(() => {
    const fetchQuestion = async () => {
      if (isSandboxMode) {
        setQuestion(DEFAULT_SANDBOX_QUESTION)
        setIsLoading(false)
        return
      }

      if (!exerciseId || !exerciseType) {
        setError("Missing exercise information")
        setIsLoading(false)
        return
      }

      setIsLoading(true)
      setError(null)
      try {
        const response = await fetch(`../../../api/learn/content/${exerciseType}/${exerciseId}`)
        if (!response.ok) {
          const errorData = await response.json()
          throw new Error(`HTTP error! status: ${response.status}, message: ${errorData.error}`)
        }
        const data = await response.json()
        setQuestion(data)
      } catch (error) {
        console.error("Error fetching question:", error)
        setError(`Failed to load question: ${(error as Error).message}`)

        toast({
          title: "Error",
          description: `Failed to load question: ${(error as Error).message}`,
          variant: "destructive",
        })
      } finally {
        setIsLoading(false)
      }
    }

    fetchQuestion()
  }, [exerciseId, exerciseType, isSandboxMode])

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
      router.push(`/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`)
    } else {
      router.push("/learn/courses")
    }
  }

  const handleSubmit = async (submission: CodeQuestionSubmissionv1_0_0) => {
    try {
      submission.status = QuestionStatus.Submitted

      const response = await fetch("../../../api/submit-assignment", {
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
            `/learn/assessment/${assessmentId}?userId=${userId}&role=${role}&courseId=${courseId}&moduleId=${moduleId}`,
          )
        else router.push("/learn/courses")
      } else {
        const errorData = await response.json()
        throw new Error(`Failed to submit assignment: ${errorData.error}`)
      }
    } catch (error) {
      console.error("Error submitting assignment:", error)
      toast({
        title: "Error",
        description: `Failed to load question: ${(error as Error).message}`,
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

  if (!question) {
    return <div>No assignment data available.</div>
  }

  const hierarchy: HierarchyBasev1_0_0 = {
    idHierarchy: isSandboxMode ? [0] : [Number(courseId), Number(moduleId), Number(assessmentId), question.id],
    idUser: userId || "anonymous",
    idRole: role || "student",
    colorMode: mode,
  }

  return (
    <div className="h-screen flex flex-col">
      <CodeEditorPanel
        assignment={question}
        onSubmit={handleSubmit}
        hierarchy={hierarchy}
        isSandboxMode={isSandboxMode} onReturn={function (): void {
          throw new Error("Function not implemented.")
        } }
        />
    </div>
  )
}

