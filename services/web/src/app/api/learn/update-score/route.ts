import { NextResponse } from 'next/server';
import fs from 'fs/promises';
import path from 'path';

export async function POST(request: Request) {
  try {
    const { courseId, questionId, userId, newScore } = await request.json();

    // In a real application, you would update the score in your database
    // For this example, we'll simulate updating a JSON file

    const filePath = path.join(process.cwd(), 'src', 'docs', 'teach', `userListQuestion${questionId}.json`);
    const fileContents = await fs.readFile(filePath, 'utf8');
    const data = JSON.parse(fileContents);

    if (data.submissions[userId]) {
      data.submissions[userId].score = newScore;
      await fs.writeFile(filePath, JSON.stringify(data, null, 2));

      return NextResponse.json({ message: 'Score updated successfully' });
    } else {
      return NextResponse.json({ error: 'User submission not found' }, { status: 404 });
    }
  } catch (error) {
    console.error('Error updating score:', error);
    return NextResponse.json({ error: 'Failed to update score' }, { status: 500 });
  }
}

