import {
  validateContentGradingDefinition,
  type ContentGradingDefinitionV2,
} from "@game-guild/grading";
import {
  createQuizGradingDefinition,
  syncQuizGradingDefinition,
} from "@game-guild/grading-adapter-quiz";
import { QUIZ_CONTENT_SCHEMA_VERSION } from "./constants";
import { assertQuizContentDocument } from "./parsing";
import { quizDocumentToGradingItems } from "./grading-projection";
import {
  quizContentItemsToStorage,
} from "./storage";
import { normalizeQuizRules, type QuizRules } from "./rules";
import type {
  QuizContentDocument,
  QuizContentDocumentInput,
} from "./types";

export function readQuizContentGrading(
  document: QuizContentDocument,
): ContentGradingDefinitionV2 | null {
  return document.grading ?? null;
}

export function enableQuizContentGrading(
  document: QuizContentDocument,
): QuizContentDocument {
  return {
    ...document,
    grading: createQuizGradingDefinition(quizDocumentToGradingItems(document)),
  };
}

export function disableQuizContentGrading(
  document: QuizContentDocument,
): QuizContentDocument {
  const { grading: _grading, ...content } = document;
  return content;
}

export function updateQuizContentGrading(
  document: QuizContentDocument,
  updater: (current: ContentGradingDefinitionV2) => ContentGradingDefinitionV2,
): QuizContentDocument {
  const current = readQuizContentGrading(document) ??
    createQuizGradingDefinition(quizDocumentToGradingItems(document));
  const next = validateContentGradingDefinition(updater(current));
  return {
    ...document,
    grading: syncQuizGradingDefinition(
      quizDocumentToGradingItems(document),
      next,
    ),
  };
}

export function syncQuizContentGrading(
  document: QuizContentDocument,
): QuizContentDocument {
  if (!document.grading) return document;
  return {
    ...document,
    grading: syncQuizGradingDefinition(
      quizDocumentToGradingItems(document),
      document.grading,
    ),
  };
}

export function quizContentItemsToDocument({
  items,
  rules,
  grading,
}: QuizContentDocumentInput): QuizContentDocument {
  const storage = quizContentItemsToStorage(items);
  let document: QuizContentDocument = {
    schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
    order: storage.order,
    blocks: storage.blocks,
    ...(normalizeQuizRules(rules) ? { rules: normalizeQuizRules(rules) } : {}),
  };
  if (grading) {
    document = {
      ...document,
      grading: syncQuizGradingDefinition(quizDocumentToGradingItems(document), grading),
    };
  }
  return assertQuizContentDocument(document);
}

/** Reads the quiz-level rules (issue #432); undefined when none are configured. */
export function readQuizContentRules(
  document: QuizContentDocument,
): QuizRules | undefined {
  return document.rules;
}

/** Returns a copy of `document` with `rules` replaced (or removed when omitted). */
export function updateQuizContentRules(
  document: QuizContentDocument,
  rules: QuizRules | null | undefined,
): QuizContentDocument {
  const normalized = normalizeQuizRules(rules);
  if (!normalized) {
    const { rules: _rules, ...withoutRules } = document;
    return withoutRules;
  }
  return { ...document, rules: normalized };
}
