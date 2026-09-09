import {
  FillBlankInputType,
  parseQuizPoints,
  QuizEntryType,
  type QuizAnswer,
  type QuizEntry,
} from "@game-guild/quiz";
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
  categorization: { type: QuizEntryType.Categorization, categoryIdsByItem: { item: ["category"] } },
  rating: { type: QuizEntryType.Rating, value: 4 },
  numeric: { type: QuizEntryType.Numeric, value: "42.5" },
  formula: { type: QuizEntryType.Formula, expression: "x * 2" },
  hotspot: { type: QuizEntryType.Hotspot, point: { x: 25, y: 75 } },
  highlight: { type: QuizEntryType.Highlight, spans: [{ start: 0, end: 6 }] },
} as const satisfies Record<string, QuizAnswer>;

export const quizAnswerEnvelopeV1Fixture: QuizAnswerEnvelopeV1 = createQuizAnswerEnvelope(
  quizAnswerVariantsV1,
);

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

export const allQuizEntryTypesV1: QuizEntry[] = [
  { type: QuizEntryType.SingleChoice, stem: "", options: [{ id: "a", text: "A" }], correctOptionId: "a", settings: { allowRetry: false } },
  { type: QuizEntryType.MultipleChoice, stem: "", options: [{ id: "a", text: "A" }], correctOptionIds: ["a"], settings: { allowRetry: false } },
  { type: QuizEntryType.TrueFalse, stem: "", correctAnswer: true, settings: { allowRetry: false } },
  { type: QuizEntryType.FillInTheBlank, stem: "___", blanks: [{ id: "blank", position: 0, input: { type: FillBlankInputType.Text, acceptedAnswers: ["answer"] } }], settings: { allowRetry: false } },
  { type: QuizEntryType.ShortAnswer, stem: "", acceptedAnswers: ["answer"], settings: { allowRetry: false } },
  { type: QuizEntryType.Essay, stem: "", settings: { allowRetry: false } },
  { type: QuizEntryType.Matching, stem: "", pairs: [{ id: "a", left: "A", right: "1" }], settings: { allowRetry: false } },
  { type: QuizEntryType.Ordering, stem: "", items: [{ id: "a", text: "A", correctPosition: 0 }], settings: { allowRetry: false } },
  { type: QuizEntryType.Categorization, stem: "", categories: [{ id: "c", name: "C" }], items: [{ id: "a", text: "A", correctCategoryIds: ["c"] }], settings: { allowRetry: false } },
  { type: QuizEntryType.Rating, stem: "", scale: { min: 1, max: 5, step: 1 }, correctRating: 4, settings: { allowRetry: false } },
  { type: QuizEntryType.Numeric, stem: "", variables: [{ id: "x", name: "x", min: 1, max: 1, decimals: 0 }], formula: "x", toleranceType: "absolute", tolerance: 0, decimalPlaces: 0, settings: { allowRetry: false } },
  { type: QuizEntryType.Formula, stem: "", variables: [{ id: "x", name: "x", min: 1, max: 1, decimals: 0 }], formula: "x", toleranceType: "absolute", tolerance: 0, decimalPlaces: 0, settings: { allowRetry: false } },
  { type: QuizEntryType.Hotspot, stem: "", imageAssetUri: null, imageWidth: 100, imageHeight: 100, hotspots: [{ id: "h", x: 50, y: 50, zones: [{ radius: 10, label: "Target" }] }], settings: { allowRetry: false } },
  { type: QuizEntryType.Highlight, stem: "", sourceText: "__answer__", plainText: "answer", highlights: [{ start: 0, end: 6 }], settings: { allowRetry: false } },
];
