import { NextResponse } from 'next/server'
import { QuestionSubmissionv1_0_0 } from '@/lib/interface-base/question.submission.v1.0.0'

// interface GradeSubmission extends QuestionSubmissionv1_0_0 {
//   [x: string]: any;
//   grade: number;
//   feedback: string;
// }

export async function POST(request: Request) {
  try {
    const gradeSubmission: QuestionSubmissionv1_0_0 = await request.json()
    
    // Validate the grade submission
    if (
      typeof gradeSubmission.id !== 'number' ||
      gradeSubmission.format_ver !== '1.0.0' ||
      !Array.isArray(gradeSubmission.rules) ||
      typeof gradeSubmission.title !== 'string' ||
      typeof gradeSubmission.description !== 'string' ||
      !Array.isArray(gradeSubmission.submittedCode) ||
      typeof gradeSubmission.output !== 'string' ||
      !Array.isArray(gradeSubmission.testResults) ||
      typeof gradeSubmission.grade !== 'number' ||
      typeof gradeSubmission.feedback !== 'string'
    ) {
      return NextResponse.json({ error: 'Invalid grade submission format' }, { status: 400 })
    }

    // Here you would typically save the graded submission to your database
    // For now, we'll just log it and return a success response
    console.log('Received grade submission:', gradeSubmission)

    return NextResponse.json({ message: 'Submission graded successfully' }, { status: 200 })
  } catch (error) {
    console.error('Error grading submission:', error)
    return NextResponse.json({ error: 'Failed to grade submission' }, { status: 500 })
  }
}

