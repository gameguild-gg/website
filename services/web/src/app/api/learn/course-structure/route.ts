import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { ModuleBasev1_0_0 } from '@/lib/interface-base/module.base.v1.0.0'
import { AssessmentBasev1_0_0 } from '@/lib/interface-base/assessment.base.v1.0.0'
import { QuestionBasev1_0_0 } from '@/lib/interface-base/question.base.v1.0.0'

async function readDirectoryStructure(dir: string): Promise<any> {
  const items = await fs.readdir(dir, { withFileTypes: true })
  const result: { [key: string]: any } = {}

  for (const item of items) {
    if (item.isDirectory()) {
      result[item.name] = await readDirectoryStructure(path.join(dir, item.name))
    } else if (item.name.endsWith('.json')) {
      const content = await fs.readFile(path.join(dir, item.name), 'utf-8')
      const parsedContent = JSON.parse(content)
      result[item.name.replace('.json', '')] = parsedContent
    }
  }

  return result
}

export async function GET() {
  try {
    const coursePath = path.join(process.cwd(), 'src', 'docs', 'course')
    const structure = {
      modules: await readDirectoryStructure(path.join(coursePath, 'modules')),
      assessments: await readDirectoryStructure(path.join(coursePath, 'assessments')),
      questions: await readDirectoryStructure(path.join(coursePath, 'questions'))
    }
    return NextResponse.json(structure)
  } catch (error) {
    console.error('Error reading course structure:', error)
    return NextResponse.json({ error: 'Failed to load course structure', details: (error as Error).message }, { status: 500 })
  }
}

