import { LessonBasev1_0_0 } from './lesson.base.v1.0.0';

export interface LessonUpdateRequestv1_0_0 {
  id: number;
  title?: string;
  description?: string;
  content: string;
}

export interface LessonUpdateResponsev1_0_0 {
  message: string;
  updatedLesson: LessonBasev1_0_0;
}

