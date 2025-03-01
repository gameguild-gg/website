import { NextResponse } from 'next/server';
import fs from 'fs/promises';
import path from 'path';
import { LessonUpdateRequestv1_0_0, LessonUpdateResponsev1_0_0 } from '@/lib/interface-base/lesson.update.v1.0.0';
import { LessonBasev1_0_0 } from '@/lib/interface-base/lesson.base.v1.0.0';

export async function PUT(
  request: Request,
  { params }: { params: { id: string } }
) {
  try {
    const updateData: LessonUpdateRequestv1_0_0 = await request.json();

    const filePath = path.join(process.cwd(), 'src', 'docs', 'course', 'lessons', `lesson${params.id}.json`);
    const fileContents = await fs.readFile(filePath, 'utf8');
    const lessonData: LessonBasev1_0_0 = JSON.parse(fileContents);

    // Update the lesson data
    lessonData.content = updateData.content;
    if (updateData.title) lessonData.title = updateData.title;
    if (updateData.description) lessonData.description = updateData.description;

    await fs.writeFile(filePath, JSON.stringify(lessonData, null, 2));

    const response: LessonUpdateResponsev1_0_0 = {
      message: 'Lesson updated successfully',
      updatedLesson: lessonData
    };

    return NextResponse.json(response);
  } catch (error) {
    console.error('Error updating lesson:', error);
    return NextResponse.json({ error: 'Failed to update lesson' }, { status: 500 });
  }
}

