import { QuestionSubmissionv1_0_0 } from './question.submission.v1.0.0';
import { UserBasev1_0_0 } from './user.base.v1.0.0';

export interface UserListQuestionv1_0_0 {
  submissions: {
    [userId: string]: {
      user: UserBasev1_0_0;
      submission?: QuestionSubmissionv1_0_0;
      maxScore: number;
      score: number;
    };
  };
}

export interface StudentSubmission {
  submittedCode: string[];
  output: string;
  testResults: {
    expectedOutput: string;
    actualOutput: string;
    passed: boolean;
  }[];
}

