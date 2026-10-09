'use client';

import { ActivityComponent } from '@/components/courses/learning/activity-component';
import type { QuizRuntimeContentDocument } from '@game-guild/quiz-content';
import { useRouter } from 'next/navigation';

interface LearnerQuizActivityProps {
  contentId: string;
  courseId: string;
  existingSubmissionId?: string | null;
  title: string;
  description?: string;
  content: QuizRuntimeContentDocument;
  isRequired: boolean;
  status: 'locked' | 'available' | 'in-progress' | 'completed';
}

export function LearnerQuizActivity({
  contentId,
  courseId,
  existingSubmissionId,
  title,
  description,
  content,
  isRequired,
  status,
}: LearnerQuizActivityProps) {
  const router = useRouter();

  return (
    <ActivityComponent
      courseId={courseId}
      existingSubmissionId={existingSubmissionId}
      item={{
        id: contentId,
        title,
        description,
        type: 'quiz',
        activityType: 'quiz',
        status,
        order: 0,
        isRequired,
        content,
      }}
      onComplete={() => router.refresh()}
    />
  );
}
