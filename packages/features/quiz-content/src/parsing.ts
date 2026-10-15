import {
  tryParseContentGradingDefinition,
} from "@game-guild/grading";
import { syncQuizGradingDefinition } from "@game-guild/grading-adapter-quiz";
import { safeParseQuizEntry } from "@game-guild/quiz";
import { QUIZ_BLOCK_TYPE, QUIZ_CONTENT_SCHEMA_VERSION } from "./constants";
import { quizDocumentToGradingItems } from "./grading-projection";
import { parseQuizRules, quizRulesIssuesToParseIssues } from "./rules";
import { createEmptyQuizContentDocument } from "./storage";
import type {
  QuizContentDocument,
  QuizContentParseIssue,
  QuizContentParseResult,
} from "./types";

type UnknownRecord = Record<string, unknown>;
const ROOT_KEYS = new Set(["schemaVersion", "order", "blocks", "rules", "grading"]);

export class QuizContentValidationError extends Error {
  readonly issues: QuizContentParseIssue[];

  constructor(issues: QuizContentParseIssue[]) {
    super(issues.map((issue) => `${issue.path}: ${issue.message}`).join("; "));
    this.name = "QuizContentValidationError";
    this.issues = issues;
  }
}

export function parseQuizContentDocument(value: unknown): QuizContentParseResult {
  const root = asRecord(value);
  if (!root) {
    return failedRoot("Quiz content must be an object");
  }
  if (root.schemaVersion !== QUIZ_CONTENT_SCHEMA_VERSION) {
    return {
      document: createEmptyQuizContentDocument(),
      issues: [{
        code: "unsupported-version",
        path: "schemaVersion",
        message: `Expected quiz content schema version ${QUIZ_CONTENT_SCHEMA_VERSION}`,
      }],
    };
  }

  const issues: QuizContentParseIssue[] = [];
  for (const key of Object.keys(root)) {
    if (!ROOT_KEYS.has(key)) {
      issues.push({
        code: "invalid-root",
        path: key,
        message: "Unknown quiz content field",
      });
    }
  }

  const order = Array.isArray(root.order) ? root.order : null;
  const sourceBlocks = asRecord(root.blocks);
  if (!order) {
    issues.push({
      code: "invalid-root",
      path: "order",
      message: "Quiz content order must be an array",
    });
  }
  if (!sourceBlocks) {
    issues.push({
      code: "invalid-root",
      path: "blocks",
      message: "Quiz content blocks must be an object",
    });
  }
  if (!order || !sourceBlocks) {
    return { document: createEmptyQuizContentDocument(), issues };
  }

  const normalizedOrder: QuizContentDocument["order"] = [];
  const normalizedBlocks: QuizContentDocument["blocks"] = {};
  const seen = new Set<string>();

  for (const [index, rawEntry] of order.entries()) {
    const path = `order.${index}`;
    if (
      !Array.isArray(rawEntry) ||
      rawEntry.length !== 2 ||
      typeof rawEntry[0] !== "string" ||
      !rawEntry[0].trim() ||
      rawEntry[1] !== QUIZ_BLOCK_TYPE
    ) {
      issues.push({
        code: "invalid-order-entry",
        path,
        message: 'Expected [non-empty id, "quiz"]',
      });
      continue;
    }

    const id = rawEntry[0];
    if (seen.has(id)) {
      issues.push({
        code: "duplicate-block-id",
        path,
        message: `Duplicate quiz block id: ${id}`,
      });
      continue;
    }
    seen.add(id);

    if (!Object.prototype.hasOwnProperty.call(sourceBlocks, id)) {
      issues.push({
        code: "missing-block-payload",
        path: `blocks.${id}`,
        message: `Missing payload for quiz block ${id}`,
      });
      continue;
    }

    const parsed = safeParseQuizEntry(sourceBlocks[id]);
    if (!parsed.success) {
      issues.push({
        code: "invalid-quiz-entry",
        path: `blocks.${id}`,
        message: parsed.error.issues
          .map((issue) => `${issue.path.join(".") || "entry"}: ${issue.message}`)
          .join("; "),
      });
      continue;
    }

    normalizedOrder.push([id, QUIZ_BLOCK_TYPE]);
    normalizedBlocks[id] = parsed.data;
  }

  for (const id of Object.keys(sourceBlocks)) {
    if (!seen.has(id)) {
      issues.push({
        code: "orphan-block-payload",
        path: `blocks.${id}`,
        message: `Quiz block ${id} is not referenced by order`,
      });
    }
  }

  let document: QuizContentDocument = {
    schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
    order: normalizedOrder,
    blocks: normalizedBlocks,
  };

  if (Object.prototype.hasOwnProperty.call(root, "rules")) {
    const parsedRules = parseQuizRules(root.rules);
    if (parsedRules.rules) {
      document = { ...document, rules: parsedRules.rules };
    }
    issues.push(...quizRulesIssuesToParseIssues(parsedRules.issues));
  }

  if (Object.prototype.hasOwnProperty.call(root, "grading")) {
    const grading = tryParseContentGradingDefinition(root.grading);
    if (!grading) {
      issues.push({
        code: "invalid-grading",
        path: "grading",
        message: "Grading must be a valid ContentGradingDefinitionV2",
      });
    } else {
      document = {
        ...document,
        grading: syncQuizGradingDefinition(quizDocumentToGradingItems(document), grading),
      };
    }
  }

  return { document, issues };
}

export function assertQuizContentDocument(value: unknown): QuizContentDocument {
  const result = parseQuizContentDocument(value);
  if (result.issues.length > 0) throw new QuizContentValidationError(result.issues);
  return result.document;
}

export function serializeQuizContentDocument(
  value: QuizContentDocument,
): QuizContentDocument {
  return assertQuizContentDocument(value);
}

export function isQuizContentDocument(value: unknown): value is QuizContentDocument {
  return parseQuizContentDocument(value).issues.length === 0;
}

function failedRoot(message: string): QuizContentParseResult {
  return {
    document: createEmptyQuizContentDocument(),
    issues: [{ code: "invalid-root", path: "", message }],
  };
}

function asRecord(value: unknown): UnknownRecord | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? value as UnknownRecord
    : null;
}
