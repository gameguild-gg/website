import { NextResponse } from 'next/server'
import fs from 'fs/promises'
import path from 'path'
import { UserListQuestionv1_0_0 } from '@/interface-base/user.list.question.v1.0.0';
import { UserBasev1_0_0 } from '@/interface-base/user.base.v1.0.0';

async function getUserData(userId: string): Promise<UserBasev1_0_0 | null> {
  try {
    const filePath = path.join(process.cwd(), 'docs', 'users', `user${userId}.json`);
    const fileContents = await fs.readFile(filePath, 'utf8');
    return JSON.parse(fileContents);
  } catch (error) {
    console.error(`Error reading user file user${userId}.json:`, error);
    return null;
  }
}

export async function GET(request: Request, { params }: { params: { id: string } }) {
  try {
    const courseId = request.nextUrl.searchParams.get('courseId');
    if (!courseId) {
      return NextResponse.json({ error: 'courseId is required' }, { status: 400 });
    }

    const filePath = path.join(process.cwd(), 'docs', 'teach', `userListQuestion${params.id}.json`);
    
    let fileContents: string;
    try {
      fileContents = await fs.readFile(filePath, 'utf8');
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code === 'ENOENT') {
        return NextResponse.json({ submissions: {} }, { status: 200 });
      }
      throw error;
    }

    const userListData: UserListQuestionv1_0_0 = JSON.parse(fileContents);

    const filteredSubmissions = await Promise.all(
      Object.entries(userListData.submissions).map(async ([userId, submissionData]) => {
        const userData = await getUserData(userId);
        if (userData && userData.idCourses.includes(parseInt(courseId))) {
          return [userId, submissionData];
        }
        return null;
      })
    );

    const filteredUserList: UserListQuestionv1_0_0 = {
      submissions: filteredSubmissions.reduce((acc, submission) => {
        if (submission) {
          acc[submission[0]] = submission[1];
        }
        return acc;
      }, {} as { [userId: string]: any })
    };

    return NextResponse.json(filteredUserList);
  } catch (error) {
    console.error('Error reading user list file:', error);
    return NextResponse.json({ error: 'Failed to load user list' }, { status: 500 });
  }
}

