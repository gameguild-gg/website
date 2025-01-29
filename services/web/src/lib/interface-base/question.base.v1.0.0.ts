export interface QuestionBasev1_0_0 {
  inputs: any;
  outputs: any;
  id: number;
  format_ver: "1.0.0";
  type: "code" | "answer" | "multiple-choice" | "essay";
  rules: string[];
  status: QuestionStatus;
  subject: string[];
  score: number[];
  title: string;
  description: string;
  initialCode: { [id: string]: string };
}

export interface CodeQuestionv1_0_0 extends QuestionBasev1_0_0 {
  testResults: any;
  output: any;
  submittedCode: any;
  type: "code";
  inputs: { [id: string]: any[] };
  outputs: { [id: string]: string[] };
  outputsScore: { [id: string]: number[] };
  codeName: { id: string; name: string }[];
}

export interface AnswerQuestionv1_0_0 extends QuestionBasev1_0_0 {
  submittedAnswer: string;
  type: "answer";
  question: string;
  answer: string;
  maxLetters: number;
}

export interface EssayQuestionv1_0_0 extends QuestionBasev1_0_0 {
  submittedEssay: string;
  type: "essay";
  question: string;
  answer: string;
  maxWords: number;
}

export interface MultipleChoiceQuestionv1_0_0 extends QuestionBasev1_0_0 {
  submittedAnswers: any;
  type: "multiple-choice";
  question: string;
  answers: { id: string; text: string }[];
  correctAnswers: string[];
}

export enum QuestionStatus {
  Unavailable = 0,
  Available = 1,
  Submitted = 2,
  Corrected = 3
}

export type QuestionTypev1_0_0 = CodeQuestionv1_0_0 | AnswerQuestionv1_0_0 | MultipleChoiceQuestionv1_0_0 | EssayQuestionv1_0_0;

