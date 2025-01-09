import { NextResponse } from 'next/server'
import { QuestionSubmissionv1_0_0, CodeQuestionSubmissionv1_0_0, AnswerQuestionSubmissionv1_0_0, MultipleChoiceQuestionSubmissionv1_0_0 } from '@/lib/interface-base/question.submission.v1.0.0'
//import { QuestionStatus } from '@/lib/interface-base/question.base.v1.0.0'; // Import the enum

export async function POST(request: Request) {
  try {
    const submission: QuestionSubmissionv1_0_0 = await request.json()

    if (submission.type === 'code') {
      // Handle code submission
      const codeSubmission = submission as CodeQuestionSubmissionv1_0_0;
      // Validate the submission
      if (
        typeof codeSubmission.id !== 'number' ||
        codeSubmission.format_ver !== '1.0.0' ||
        !Array.isArray(codeSubmission.rules) ||
        typeof codeSubmission.title !== 'string' ||
        typeof codeSubmission.description !== 'string' ||
        !Array.isArray(codeSubmission.submittedCode) ||
        typeof codeSubmission.output !== 'string' ||
        !Array.isArray(codeSubmission.testResults)
      ) {
        return NextResponse.json({ error: 'Invalid submission format' }, { status: 400 })
      }

      console.log('Received and updated code submission:', codeSubmission)

    } else if (submission.type === 'answer') {
      // Handle answer submission
      const answerSubmission = submission as AnswerQuestionSubmissionv1_0_0;
      // Validate the submission
      if (
        typeof answerSubmission.id !== 'number' ||
        answerSubmission.format_ver !== '1.0.0' ||
        typeof answerSubmission.title !== 'string' ||
        typeof answerSubmission.answer !== 'string'
      ) {
        return NextResponse.json({ error: 'Invalid submission format' }, { status: 400 })
      }
      console.log('Received and updated answer submission:', answerSubmission)
    } else if (submission.type === 'multiple-choice') {
        const multipleChoiceSubmission = submission as MultipleChoiceQuestionSubmissionv1_0_0;
        // ... validation and processing logic for multiple-choice submissions
        if (
          typeof multipleChoiceSubmission.id !== 'number' ||
          multipleChoiceSubmission.format_ver !== '1.0.0' ||
          typeof multipleChoiceSubmission.title !== 'string' ||
          !Array.isArray(multipleChoiceSubmission.options) ||
          typeof multipleChoiceSubmission.selectedOption !== 'number'
        ) {
          return NextResponse.json({ error: 'Invalid submission format' }, { status: 400 });
        }
        console.log('Received and updated multiple-choice submission:', multipleChoiceSubmission);
    } else {
      return NextResponse.json({ error: 'Invalid question type' }, { status: 400 });
    }


    return NextResponse.json({ message: 'Assignment submitted successfully' }, { status: 200 })
  } catch (error) {
    console.error('Error submitting assignment:', error)
    return NextResponse.json({ error: 'Failed to submit assignment' }, { status: 500 })
  }
}

