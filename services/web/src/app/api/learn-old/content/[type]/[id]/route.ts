import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { ModuleBasev1_0_0 } from '@/lib/interface-base/module.base.v1.0.0'
import { AssessmentBasev1_0_0 } from '@/lib/interface-base/assessment.base.v1.0.0'
import { QuestionBasev1_0_0 } from '@/lib/interface-base/question.base.v1.0.0'
import { LessonBasev1_0_0 } from '@/lib/interface-base/lesson.base.v1.0.0'

export async function GET(
  request: Request,
  { params }: { params: { type: string; id: string } }
) {
  try {
    let filePath: string
    let contentType: 'module' | 'assessment' | 'question' | 'sandbox' | 'lesson' | 'submission'

    // Extract userId and questionId from id parameter for submissions
    const [questionId, userId] = params.id.split('user');


    switch (params.type) {
      case 'module':
        filePath = path.join(process.cwd(), 'src', 'docs', 'course', 'modules', `module${params.id}.json`)
        contentType = 'module'
        break
      case 'assessment':
        filePath = path.join(process.cwd(), 'src', 'docs', 'course', 'assessments', `assessment${params.id}.json`)
        contentType = 'assessment'
        break
      case 'question':
        filePath = path.join(process.cwd(), 'src', 'docs', 'course', 'questions', `question${params.id}.json`)
        contentType = 'question'
        break
      case 'sandbox':
        filePath = path.join(process.cwd(), 'src', 'docs', 'sandbox', 'questionX.json')
        contentType = 'sandbox'
        break
      case 'lesson':
        filePath = path.join(process.cwd(), 'src', 'docs', 'course', 'lessons', `lesson${params.id}.json`)
        contentType = 'lesson'
        break
      case 'submission':
        filePath = path.join(process.cwd(), 'src', 'docs', 'teach', 'userSubmission', `question${questionId}user${userId}.json`)
        contentType = 'submission'
        break
      default:
        console.error(`Invalid content type: ${params.type}`)
        return NextResponse.json({ error: 'Invalid content type' }, { status: 400 })
    }

    console.log(`Attempting to read file: ${filePath}`)
    const fileContents = await fs.readFile(filePath, 'utf8')
    console.log(`File contents: ${fileContents}`)
    
    let contentData
    try {
      contentData = JSON.parse(fileContents)
    } catch (parseError) {
      console.error('Error parsing JSON:', parseError)
      return NextResponse.json({ error: 'Invalid JSON in file', details: parseError.message }, { status: 500 })
    }

    // Validate the content data against the appropriate interface
    try {
      switch (contentType) {
        case 'module':
          validateModule(contentData)
          break
        case 'assessment':
          validateAssessment(contentData)
          break
        case 'question':
        case 'submission': // Add submission type here
        case 'sandbox':
          validateQuestion(contentData)
          break
        case 'lesson':
          validateLesson(contentData)
          break
      }
    } catch (validationError) {
      console.error('Validation error:', validationError)
      return NextResponse.json({ error: 'Content validation failed', details: validationError.message }, { status: 500 })
    }

    return NextResponse.json(contentData)
  } catch (error) {
    console.error(`Error reading ${params.type} content file:`, error)
    if (error instanceof Error) {
      if ('code' in error && error.code === 'ENOENT') {
        return NextResponse.json({ error: 'File not found', details: error.message, path: (error as any).path }, { status: 404 })
      }
      return NextResponse.json({ error: 'An error occurred while reading the file', details: error.message }, { status: 500 })
    }
    return NextResponse.json({ error: 'An unknown error occurred' }, { status: 500 })
  }
}

function validateModule(data: any): asserts data is ModuleBasev1_0_0 {
  if (
    typeof data.id !== 'number' ||
    !Array.isArray(data.idAssessments) ||
    typeof data.title !== 'string' ||
    typeof data.description !== 'string'
  ) {
    console.error('Invalid module data:', data)
    throw new Error('Invalid module data')
  }
}

function validateAssessment(data: any): asserts data is AssessmentBasev1_0_0 {
  if (
    typeof data.id !== 'number' ||
    !Array.isArray(data.idQuestions) ||
    typeof data.title !== 'string' ||
    typeof data.description !== 'string'
  ) {
    console.error('Invalid assessment data:', data)
    throw new Error('Invalid assessment data')
  }
}

function validateQuestion(data: any): asserts data is QuestionBasev1_0_0 {
  if (
    typeof data.id !== 'number' ||
    data.format_ver !== '1.0.0' ||
    !Array.isArray(data.rules) ||
    typeof data.title !== 'string' ||
    typeof data.description !== 'string' ||
    !Array.isArray(data.score) ||
    data.score.length !== 2 ||
    !['code', 'answer', 'multiple-choice', 'essay'].includes(data.type)
  ) {
    console.error('Invalid question data:', data)
    throw new Error('Invalid question data')
  }

  if (data.type === 'code') {
    if (
      !Array.isArray(data.initialCode) ||
      !Array.isArray(data.codeName) ||
      !Array.isArray(data.inputs) ||
      !Array.isArray(data.outputs)
    ) {
      console.error('Invalid code question data:', data);
      throw new Error('Invalid code question data');
    }
  } else if (data.type === 'answer') {
    if (
      typeof data.question !== 'string' ||
      typeof data.answer !== 'string' ||
      typeof data.maxLetters !== 'number'
    ) {
      console.error('Invalid answer question data:', data);
      throw new Error('Invalid answer question data');
    }
  } else if (data.type === 'multiple-choice') {
    if (
      typeof data.question !== 'string' ||
      !Array.isArray(data.answers) ||
      !data.answers.every((answer: any) => typeof answer.id === 'string' && typeof answer.text === 'string') ||
      !Array.isArray(data.correctAnswers) ||
      !data.correctAnswers.every((answerId: any) => typeof answerId === 'string')
    ) {
      console.error('Invalid multiple-choice question data:', data);
      throw new Error('Invalid multiple-choice question data');
    }
  } else if (data.type === 'essay') {
    if (
      typeof data.question !== 'string' ||
      typeof data.answer !== 'string' ||
      typeof data.maxWords !== 'number'
    ) {
      console.error('Invalid essay question data:', data);
      throw new Error('Invalid essay question data');
    }
  }
}

function validateLesson(data: any): asserts data is LessonBasev1_0_0 {
  if (
    typeof data.id !== 'number' ||
    typeof data.title !== 'string' ||
    typeof data.description !== 'string' ||
    !Array.isArray(data.content)
  ) {
    console.error('Invalid lesson data:', data)
    throw new Error('Invalid lesson data')
  }

  // Validate each item in the content array
  for (const item of data.content) {
    if (
      typeof item !== 'string' &&
      !(typeof item === 'object' && 'content' in item && typeof item.content === 'string') &&
      !(Array.isArray(item) && item.every(subItem => 
        typeof subItem === 'object' && 
        'title' in subItem && 
        'content' in subItem && 
        typeof subItem.title === 'string' && 
        typeof subItem.content === 'string'
      ))
    ) {
      console.error('Invalid content item:', item)
      throw new Error('Invalid lesson content structure')
    }
  }
}

