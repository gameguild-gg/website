export interface LessonBasev1_0_0 {
  id: number;
  title: string;
  description: string;
  content: (string | { content: string } | { title: string; content: string }[])[];
}

