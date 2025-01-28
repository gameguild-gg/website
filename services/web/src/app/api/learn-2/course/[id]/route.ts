import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { CourseBasev1_0_0 } from '@/interface-base/course.base.v1.0.0'

export async function GET(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const filePath = path.join(process.cwd(), 'docs', 'course', 'course', `course${params.id}.json`)
    console.log(`Attempting to read file: ${filePath}`)
    
    const fileContents = await fs.readFile(filePath, 'utf8')
    console.log(`File contents: ${fileContents}`)
    
    let courseData: CourseBasev1_0_0;
    try {
      courseData = JSON.parse(fileContents)
    } catch (parseError) {
      console.error(`Error parsing JSON:`, parseError)
      return NextResponse.json({ error: 'Invalid JSON in course file', details: parseError.message }, { status: 500 })
    }

    return NextResponse.json(courseData)
  } catch (error) {
    console.error(`Error reading course file:`, error)
    
    if (error instanceof Error) {
      if ('code' in error && error.code === 'ENOENT') {
        return NextResponse.json({ error: 'Course not found', details: error.message, path: (error as any).path }, { status: 404 })
      }
      return NextResponse.json({ error: 'An error occurred while reading the course file', details: error.message }, { status: 500 })
    }
    
    return NextResponse.json({ error: 'An unknown error occurred' }, { status: 500 })
  }
}

