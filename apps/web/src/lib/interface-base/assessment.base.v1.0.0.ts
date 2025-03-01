export interface AssessmentBasev1_0_0 {
  id: number;
  idQuestions: { title: string; questions: number[] }[]; // Updated idQuestions
  title: string;
  description: string;
  maxDate?: string;
  scoreTotal: { maxDate: string; discount: number[] }[];
  scoreLetters: { scoreRange: number[]; letter: string }[];
}

