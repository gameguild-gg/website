import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'

export async function GET(request: Request, { params }: { params: { id: string } }) {
  try {
    const filePath = path.join(process.cwd(), 'src', 'docs', 'courses', 'user', 'example-course', 'assignments', `${params.id}.json`)
    const fileContents = await fs.readFile(filePath, 'utf8')
    const assignmentData = JSON.parse(fileContents)
    return NextResponse.json(assignmentData)
  } catch (error) {
    console.error('Error reading assignment file:', error)
    return NextResponse.json({ error: 'Failed to load assignment' }, { status: 500 })
  }
}

