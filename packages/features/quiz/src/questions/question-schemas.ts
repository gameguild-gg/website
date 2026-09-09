import { isAssetUri, type AssetUri } from "@game-guild/assets";
import { z } from "zod";
import {
  FillBlankInputType,
  QuizEntryType,
  type QuizEntry,
} from "./question-types";
import { MAX_QUIZ_POINTS_UNITS, parseQuizPoints } from "./quiz-points";

const finiteNumber = z.number().finite();
const nonNegativeInteger = z.number().int().nonnegative();

const quizFeedbackSchema = z.object({
  correct: z.string().optional(),
  incorrect: z.string().optional(),
  general: z.string().optional(),
}).strict();

const quizSettingsSchema = z.object({
  allowRetry: z.boolean(),
  shuffleOptions: z.boolean().optional(),
  showFeedback: z.boolean().optional(),
  showCorrectAnswer: z.boolean().optional(),
}).strict();

const assetUriSchema = z.custom<AssetUri>(isAssetUri, {
  message: "Expected a valid asset:// UUID",
});

const quizAttachmentSchema = z.object({
  assetUri: assetUriSchema,
  role: z.enum(["question", "answer", "feedback", "source"]),
  label: z.string().optional(),
  altText: z.string().optional(),
}).strict();

const quizAuthoringAttachmentsSchema = z.object({
  learnerVisible: z.array(quizAttachmentSchema).optional(),
  authorOnly: z.array(quizAttachmentSchema).optional(),
}).strict();

const entryBaseShape = {
  stem: z.string(),
  points: z.number().int().min(0).max(MAX_QUIZ_POINTS_UNITS).transform(parseQuizPoints).optional(),
  feedback: quizFeedbackSchema.optional(),
  settings: quizSettingsSchema,
  attachments: quizAuthoringAttachmentsSchema.optional(),
};

const choiceOptionSchema = z.object({
  id: z.string(),
  text: z.string(),
}).strict();

const singleChoiceEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.SingleChoice),
  options: z.array(choiceOptionSchema),
  correctOptionId: z.string(),
}).strict();

const multipleChoiceEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.MultipleChoice),
  options: z.array(choiceOptionSchema),
  correctOptionIds: z.array(z.string()),
  selectionLimit: z.number().int().positive().optional(),
}).strict();

const trueFalseEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.TrueFalse),
  correctAnswer: z.boolean(),
}).strict();

const fillBlankTextInputSchema = z.object({
  type: z.literal(FillBlankInputType.Text),
  acceptedAnswers: z.array(z.string()),
  caseSensitive: z.boolean().optional(),
}).strict();

const fillBlankNumberInputSchema = z.object({
  type: z.literal(FillBlankInputType.Number),
  correctValue: finiteNumber,
  tolerance: finiteNumber.nonnegative().optional(),
  requiredPrecision: nonNegativeInteger.optional(),
  unit: z.string().optional(),
  requireUnit: z.boolean().optional(),
  allowNegative: z.boolean().optional(),
}).strict();

const fillBlankDropdownInputSchema = z.object({
  type: z.literal(FillBlankInputType.Dropdown),
  options: z.array(z.string()),
}).strict();

const fillBlankWordBankInputSchema = z.object({
  type: z.literal(FillBlankInputType.WordBank),
  words: z.array(z.string()),
}).strict();

const fillBlankInputSchema = z.discriminatedUnion("type", [
  fillBlankTextInputSchema,
  fillBlankNumberInputSchema,
  fillBlankDropdownInputSchema,
  fillBlankWordBankInputSchema,
]);

const fillBlankFieldSchema = z.object({
  id: z.string(),
  position: nonNegativeInteger,
  input: fillBlankInputSchema,
}).strict();

const fillInTheBlankEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.FillInTheBlank),
  blanks: z.array(fillBlankFieldSchema),
}).strict();

const shortAnswerEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.ShortAnswer),
  acceptedAnswers: z.array(z.string()),
  caseSensitive: z.boolean().optional(),
}).strict();

const serializedRichTextPayloadSchema = z.record(z.string(), z.unknown()).nullable();

const essayEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Essay),
  minWordCount: nonNegativeInteger.optional(),
  maxWordCount: nonNegativeInteger.optional(),
  showWordCount: z.boolean().optional(),
  correctAnswer: serializedRichTextPayloadSchema.optional(),
  correctAnswerPlain: z.string().optional(),
  requireFormatting: z.boolean().optional(),
}).strict();

const matchingPairSchema = z.object({
  id: z.string(),
  left: z.string(),
  right: z.string(),
}).strict();

const matchingEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Matching),
  pairs: z.array(matchingPairSchema),
  rightOptions: z.array(z.string()).optional(),
  distractors: z.array(z.string()).optional(),
  allowPartialCredit: z.boolean().optional(),
}).strict();

const orderingItemSchema = z.object({
  id: z.string(),
  text: z.string(),
  correctPosition: nonNegativeInteger,
}).strict();

const orderingEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Ordering),
  items: z.array(orderingItemSchema),
  allowPartialCredit: z.boolean().optional(),
}).strict();

const categorySchema = z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional(),
}).strict();

const categorizationItemSchema = z.object({
  id: z.string(),
  text: z.string(),
  correctCategoryIds: z.array(z.string()),
}).strict();

const categorizationEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Categorization),
  categories: z.array(categorySchema),
  items: z.array(categorizationItemSchema),
}).strict();

const ratingScaleSchema = z.object({
  min: finiteNumber,
  max: finiteNumber,
  step: finiteNumber,
  minLabel: z.string().optional(),
  maxLabel: z.string().optional(),
}).strict();

const ratingEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Rating),
  scale: ratingScaleSchema,
  correctRating: finiteNumber.optional(),
}).strict();

const formulaVariableSchema = z.object({
  id: z.string(),
  name: z.string(),
  min: finiteNumber,
  max: finiteNumber,
  decimals: nonNegativeInteger,
}).strict();

const formulaEntryShape = {
  ...entryBaseShape,
  variables: z.array(formulaVariableSchema),
  formula: z.string(),
  toleranceType: z.enum(["absolute", "percentage"]),
  tolerance: finiteNumber.nonnegative(),
  decimalPlaces: nonNegativeInteger,
};

const numericEntrySchema = z.object({
  ...formulaEntryShape,
  type: z.literal(QuizEntryType.Numeric),
}).strict();

const formulaEntrySchema = z.object({
  ...formulaEntryShape,
  type: z.literal(QuizEntryType.Formula),
}).strict();

const hotspotZoneSchema = z.object({
  radius: finiteNumber,
  label: z.string(),
}).strict();

const hotspotPointSchema = z.object({
  id: z.string(),
  x: finiteNumber,
  y: finiteNumber,
  zones: z.array(hotspotZoneSchema),
}).strict();

const hotspotEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Hotspot),
  imageAssetUri: assetUriSchema.nullable(),
  imageWidth: finiteNumber,
  imageHeight: finiteNumber,
  hotspots: z.array(hotspotPointSchema),
}).strict();

const highlightSpanSchema = z.object({
  start: nonNegativeInteger,
  end: nonNegativeInteger,
}).strict();

const highlightEntrySchema = z.object({
  ...entryBaseShape,
  type: z.literal(QuizEntryType.Highlight),
  sourceText: z.string(),
  plainText: z.string(),
  highlights: z.array(highlightSpanSchema),
}).strict();

const rawQuizEntrySchema = z.discriminatedUnion("type", [
  singleChoiceEntrySchema,
  multipleChoiceEntrySchema,
  trueFalseEntrySchema,
  fillInTheBlankEntrySchema,
  shortAnswerEntrySchema,
  essayEntrySchema,
  matchingEntrySchema,
  orderingEntrySchema,
  categorizationEntrySchema,
  ratingEntrySchema,
  numericEntrySchema,
  formulaEntrySchema,
  hotspotEntrySchema,
  highlightEntrySchema,
]);

type ParsedQuizEntry = z.infer<typeof rawQuizEntrySchema>;
const _quizEntryTypeCheck: QuizEntry = null as unknown as ParsedQuizEntry;
const _parsedQuizEntryTypeCheck: ParsedQuizEntry = null as unknown as QuizEntry;
void _quizEntryTypeCheck;
void _parsedQuizEntryTypeCheck;

export const quizEntrySchema: z.ZodType<QuizEntry> = rawQuizEntrySchema;

export type QuizEntryParseResult =
  | { success: true; data: QuizEntry }
  | { success: false; error: z.ZodError };

export function safeParseQuizEntry(value: unknown): QuizEntryParseResult {
  return quizEntrySchema.safeParse(value) as QuizEntryParseResult;
}

export function parseQuizEntry(value: unknown): QuizEntry {
  return quizEntrySchema.parse(value) as QuizEntry;
}

export function isQuizEntry(value: unknown): value is QuizEntry {
  return safeParseQuizEntry(value).success;
}
