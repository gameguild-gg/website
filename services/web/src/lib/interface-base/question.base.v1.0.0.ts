import { ReactNode } from "react";

export interface QuestionBasev1_0_0 {
    id: number;
    format_ver: "1.0.0";
    type: "code" | "answer" | "multiple-choice" | "essay";
    rules: string[];
    status: QuestionStatus;
    subject: string[];
    score: number[];
    title: string;
    description: string;
}

export interface CodeQuestionv1_0_0 extends QuestionBasev1_0_0 {
    testResults: any;
    content: any;
    type: "code";
    initialCode: string | string[];
    codeName: string[];
    inputs: any[];
    outputs: string[];
    outputsScore: number[];
}

export interface AnswerQuestionv1_0_0 extends QuestionBasev1_0_0 {
    submittedAnswer: ReactNode;
    type: "answer";
    question: string;
    answer: string;
    maxLetters: number;
}

export interface EssayQuestionv1_0_0 extends QuestionBasev1_0_0 {
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

