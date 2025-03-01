import { FormQuestionEnum } from './form-question.enum';

export abstract class QuestionBase {
  type: FormQuestionEnum;
  question: string;
  order: number;
}

export abstract class QuizQuestionBase extends QuestionBase {
  points?: number;
  answer?: string | boolean | number;
}
