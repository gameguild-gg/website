import { QuestionTypev1_0_0, CodeQuestionv1_0_0, AnswerQuestionv1_0_0, MultipleChoiceQuestionv1_0_0 } from './question.base.v1.0.0'

export interface QuestionSubmissionBasev1_0_0 {
  id: number;
  format_ver: "1.0.0";
  type: "code" | "answer" | "multiple-choice" | "essay";
  rules: string[];
  status: number;
  subject: string[];
  score: number[];
  title: string;
  description: string;
}

export interface CodeQuestionSubmissionv1_0_0 extends QuestionSubmissionBasev1_0_0 {
  type: "code";
  submittedCode: string[];
  output: string;
  testResults: {
    expectedOutput: string;
    actualOutput: string;
    passed: boolean;
  }[];
}

export interface AnswerQuestionSubmissionv1_0_0 extends QuestionSubmissionBasev1_0_0 {
  answer: any;
  type: "answer";
  submittedAnswer: string;
}

export interface EssayQuestionSubmissionv1_0_0 extends QuestionSubmissionBasev1_0_0 {
  type: "essay";
  submittedEssay: string;
}

export interface MultipleChoiceQuestionSubmissionv1_0_0 extends QuestionSubmissionBasev1_0_0 {
    selectedOption: any;
    options: any;
    type: "multiple-choice";
    submittedAnswers: string[];
}

export type QuestionSubmissionv1_0_0 = CodeQuestionSubmissionv1_0_0 | AnswerQuestionSubmissionv1_0_0 | MultipleChoiceQuestionSubmissionv1_0_0 | EssayQuestionSubmissionv1_0_0;

