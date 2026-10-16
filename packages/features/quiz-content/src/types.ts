import type {
  TypedBlock,
  TypedBlockList,
  TypedBlockStorage,
  TypedBlockView,
} from "@game-guild/block-list";
import type { ContentGradingDefinitionV2 } from "@game-guild/grading";
import type { QuizEntry, QuizLearnerEntry } from "@game-guild/quiz";
import type {
  QUIZ_BLOCK_TYPE,
  QUIZ_CONTENT_SCHEMA_VERSION,
} from "./constants";
import type { QuizRules } from "./rules";

export interface QuizBlockDataMap {
  quiz: QuizEntry;
}

export type QuizBlock = TypedBlock<QuizBlockDataMap>;
export type QuizBlockList = TypedBlockList<QuizBlockDataMap>;
export type QuizBlockStorage = TypedBlockStorage<QuizBlockDataMap>;
export type QuizBlockView = TypedBlockView<QuizBlockDataMap>;
export type QuizBlockOrderEntry = readonly [
  id: string,
  type: typeof QUIZ_BLOCK_TYPE,
];

export interface QuizContentItem {
  id: string;
  entry: QuizEntry;
}

export interface QuizContentDocumentV1 {
  schemaVersion: typeof QUIZ_CONTENT_SCHEMA_VERSION;
  order: QuizBlockOrderEntry[];
  blocks: Record<string, QuizEntry>;
  /** Quiz-level rules and configuration (issue #432): due date, time limit, attempts. */
  rules?: QuizRules;
  grading?: ContentGradingDefinitionV2;
}

export type QuizContentDocument = QuizContentDocumentV1;

export interface QuizLearnerContentDocument {
  schemaVersion: typeof QUIZ_CONTENT_SCHEMA_VERSION;
  order: QuizBlockOrderEntry[];
  blocks: Record<string, QuizLearnerEntry>;
  /** Learner-visible rules (issue #432): the runtime enforces due date and time limit. */
  rules?: QuizRules;
}

export type QuizRuntimeContentDocument =
  | { mode: "local-practice"; document: QuizContentDocument }
  | { mode: "server-graded"; document: QuizLearnerContentDocument };

export type QuizContentParseIssueCode =
  | "invalid-root"
  | "unsupported-version"
  | "invalid-order-entry"
  | "duplicate-block-id"
  | "missing-block-payload"
  | "orphan-block-payload"
  | "invalid-quiz-entry"
  | "invalid-rules"
  | "invalid-rules-field"
  | "invalid-grading";

export interface QuizContentParseIssue {
  code: QuizContentParseIssueCode;
  path: string;
  message: string;
}

export interface QuizContentParseResult {
  document: QuizContentDocument;
  issues: QuizContentParseIssue[];
}

export interface QuizLearnerContentParseResult {
  document: QuizLearnerContentDocument;
  issues: QuizContentParseIssue[];
}

export interface QuizContentDocumentInput {
  items: readonly QuizContentItem[];
  rules?: QuizRules | null;
  grading?: ContentGradingDefinitionV2 | null;
}
