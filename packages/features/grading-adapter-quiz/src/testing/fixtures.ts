import {
  parseQuizAnswer,
  parseQuizEntry,
  parseQuizPoints,
  QuizEntryType,
  type QuizAnswer,
  type QuizEntry,
} from "@game-guild/quiz";
import validationFixture from "../../fixtures/quiz-validation-cases-v1.json";
import type { QuizAnswerEnvelopeV1 } from "../contracts";
import type { QuizGradingItemInputV1 } from "../contracts";
import { createQuizAnswerEnvelope } from "../responses";

export const quizAnswerVariantsV1 = {
  single: { type: QuizEntryType.SingleChoice, optionId: "a" },
  multiple: { type: QuizEntryType.MultipleChoice, optionIds: ["a", "b"] },
  boolean: { type: QuizEntryType.TrueFalse, value: true },
  blanks: { type: QuizEntryType.FillInTheBlank, values: { blank: "answer" } },
  short: { type: QuizEntryType.ShortAnswer, value: "answer" },
  essay: { type: QuizEntryType.Essay, richText: null, plainText: "answer" },
  matching: { type: QuizEntryType.Matching, matches: { left: "right" } },
  ordering: { type: QuizEntryType.Ordering, itemIds: ["first", "second"] },
  categorization: {
    type: QuizEntryType.Categorization,
    categoryIdsByItem: { item: ["category"] },
  },
  rating: { type: QuizEntryType.Rating, value: 4 },
  numeric: { type: QuizEntryType.Numeric, value: "42.5" },
  formula: { type: QuizEntryType.Formula, expression: "x * 2" },
  hotspot: { type: QuizEntryType.Hotspot, point: { x: 25, y: 75 } },
  highlight: { type: QuizEntryType.Highlight, spans: [{ start: 0, end: 6 }] },
} as const satisfies Record<string, QuizAnswer>;

export const quizAnswerEnvelopeV1Fixture: QuizAnswerEnvelopeV1 =
  createQuizAnswerEnvelope(quizAnswerVariantsV1);

export const deterministicQuizItemsV1: readonly QuizGradingItemInputV1[] = [
  {
    itemId: "true-false",
    entry: {
      type: QuizEntryType.TrueFalse,
      stem: "The statement is true.",
      points: parseQuizPoints(200),
      correctAnswer: true,
      settings: { allowRetry: false },
    },
  },
  {
    itemId: "matching",
    entry: {
      type: QuizEntryType.Matching,
      stem: "Match the values.",
      points: parseQuizPoints(300),
      pairs: [
        { id: "a", left: "A", right: "1" },
        { id: "b", left: "B", right: "2" },
        { id: "c", left: "C", right: "3" },
      ],
      allowPartialCredit: true,
      settings: { allowRetry: false },
    },
  },
];

export const quizValidationCasesV1 = validationFixture.cases.map(
  (testCase) => ({
    ...testCase,
    entry: parseQuizEntry(testCase.entry),
    answers: {
      correct: parseQuizAnswer(testCase.answers.correct),
      incorrect: testCase.answers.incorrect
        ? parseQuizAnswer(testCase.answers.incorrect)
        : null,
      partial: testCase.answers.partial
        ? parseQuizAnswer(testCase.answers.partial)
        : null,
    },
  }),
);

export const allQuizEntryTypesV1: QuizEntry[] = quizValidationCasesV1
  .filter((testCase) => testCase.baseType)
  .map((testCase) => testCase.entry);
