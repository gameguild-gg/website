import type {
  AssessmentTypeAdapterV1,
  ReviewExecutionContext,
} from "@game-guild/grading";
import type { QuizEvaluationContext } from "@game-guild/quiz";
import { registerQuizGradingCapabilities } from "./capabilities";
import {
  QUIZ_ASSESSMENT_TYPE_ADAPTER,
  QUIZ_CONTENT_TYPE,
  type QuizAnswerPayloadV1,
  type QuizGradingItemInputV1,
  type QuizItemProjectionV1,
  type QuizLearnerDeliveryItemV1,
} from "./contracts";
import { createQuizExecutionDelivery, createQuizItemManifest } from "./delivery";
import { evaluateDeterministicQuiz } from "./evaluation";
import { projectQuizGradingItems } from "./items";
import { decodeQuizAnswerEnvelope } from "./responses";

export type QuizEvaluationContextsV1 = Readonly<Record<string, QuizEvaluationContext>>;

const contexts = ["author-test"] as const satisfies readonly ReviewExecutionContext[];

export const quizAssessmentTypeAdapter: AssessmentTypeAdapterV1<
  readonly QuizGradingItemInputV1[],
  QuizItemProjectionV1,
  QuizLearnerDeliveryItemV1,
  QuizAnswerPayloadV1,
  QuizEvaluationContextsV1
> = Object.freeze({
  ...QUIZ_ASSESSMENT_TYPE_ADAPTER,
  contentType: QUIZ_CONTENT_TYPE,
  isCurrentForAuthoring: true,
  contexts,
  capability: Object.freeze({
    kind: "assessment-type-adapter" as const,
    ...QUIZ_ASSESSMENT_TYPE_ADAPTER,
    contexts,
  }),
  projectAuthoring: projectQuizGradingItems,
  createManifest: createQuizItemManifest,
  createDelivery: createQuizExecutionDelivery,
  decodeResponse: decodeQuizAnswerEnvelope,
  evaluateDeterministic: evaluateDeterministicQuiz,
});

export function registerQuizAssessmentTypeAdapter(
  registry: Parameters<typeof registerQuizGradingCapabilities>[0],
  supportedContexts: readonly ReviewExecutionContext[] = contexts,
): void {
  registerQuizGradingCapabilities(registry, supportedContexts);
}
