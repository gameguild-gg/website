import type {
  AssessmentResponseEnvelopeV1,
  ScoreValue,
} from "@game-guild/grading";
import type {
  QuizAnswer,
  QuizEntry,
  QuizEntryType,
  QuizLearnerEntry,
} from "@game-guild/quiz";

export const QUIZ_CONTENT_TYPE = "quiz" as const;
export const QUIZ_ANSWER_PAYLOAD_SCHEMA = "quiz-answer/v1" as const;
export const QUIZ_ITEM_PROJECTION_SCHEMA_VERSION = 1 as const;

export const QUIZ_ASSESSMENT_TYPE_ADAPTER = { key: "quiz-assessment-type", version: "1" } as const;
export const QUIZ_AUTOMATED_REVIEW_HANDLER = { key: "quiz-automated-review", version: "1" } as const;

export interface QuizGradingItemInputV1 {
  itemId: string;
  entry: QuizEntry;
}

export interface QuizItemProjectionV1 {
  schemaVersion: typeof QUIZ_ITEM_PROJECTION_SCHEMA_VERSION;
  itemId: string;
  itemType: QuizEntryType;
  maxScore: ScoreValue;
  source: {
    contentType: typeof QUIZ_CONTENT_TYPE;
    itemId: string;
  };
  /** Immutable private input used by the server-side review algorithm. */
  authoringEntry: QuizEntry;
}

export type QuizReviewCapabilityV1 =
  | "automated-review"
  | "instructor-review"
  | "unsupported";

export interface QuizAnswerPayloadV1 {
  answers: Record<string, QuizAnswer>;
}

export type QuizAnswerEnvelopeV1 = AssessmentResponseEnvelopeV1<QuizAnswerPayloadV1> & {
  contentType: typeof QUIZ_CONTENT_TYPE;
  payloadSchema: typeof QUIZ_ANSWER_PAYLOAD_SCHEMA;
};

export interface QuizLearnerDeliveryItemV1 {
  itemId: string;
  entry: QuizLearnerEntry;
}

export interface QuizAnswerKeyV1 {
  schemaVersion: 1;
  entries: Record<string, QuizEntry>;
}
