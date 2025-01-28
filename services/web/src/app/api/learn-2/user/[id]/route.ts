import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { UserBasev1_0_0 } from '@/interface-base/user.base.v1.0.0'

export async function GET(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const filePath = path.join(process.cwd(), 'docs', 'users', `user${params.id}.json`)
    console.log(`Attempting to read user file: ${filePath}`)
    
    const fileContents = await fs.readFile(filePath, 'utf8')
    console.log(`User file contents: ${fileContents}`)
    
    const userData: UserBasev1_0_0 = JSON.parse(fileContents)

    return NextResponse.json(userData)
  } catch (error) {
    console.error(`Error reading user file:`, error)
    
    if (error instanceof Error) {
      if ('code' in error && error.code === 'ENOENT') {
        return NextResponse.json({ error: 'User not found', details: error.message, path: (error as any).path }, { status: 404 })
      }
      return NextResponse.json({ error: 'An error occurred while reading the user file', details: error.message }, { status: 500 })
    }
    
    return NextResponse.json({ error: 'An unknown error occurred' }, { status: 500 })
  }
}

