import { NextResponse } from 'next/server';
import fs from 'fs/promises';
import path from 'path';

export async function GET() {
  try {
    const filePath = path.join(process.cwd(), 'docs', 'sandbox', 'questionX.json');
    const fileContents = await fs.readFile(filePath, 'utf8');
    const sandboxData = JSON.parse(fileContents);
    return NextResponse.json(sandboxData);
  } catch (error) {
    console.error('Error reading sandbox file:', error);
    return NextResponse.json({ error: 'Failed to load sandbox' }, { status: 500 });
  }
}

