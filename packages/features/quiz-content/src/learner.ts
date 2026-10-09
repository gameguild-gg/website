import {
  safeParseQuizLearnerEntry,
  toQuizLearnerEntry,
} from "@game-guild/quiz";
import { QUIZ_BLOCK_TYPE, QUIZ_CONTENT_SCHEMA_VERSION } from "./constants";
import type {
  QuizContentParseIssue,
  QuizContentDocument,
  QuizLearnerContentDocument,
  QuizLearnerContentParseResult,
  QuizRuntimeContentDocument,
} from "./types";

type UnknownRecord = Record<string, unknown>;
const LEARNER_ROOT_KEYS = new Set(["schemaVersion", "order", "blocks"]);

export function toQuizLearnerContentDocument(
  document: QuizContentDocument,
): QuizLearnerContentDocument {
  return {
    schemaVersion: document.schemaVersion,
    order: document.order.map(([id, type]) => [id, type]),
    blocks: Object.fromEntries(
      document.order.map(([id]) => [
        id,
        toQuizLearnerEntry(document.blocks[id]!),
      ]),
    ),
  };
}

export function prepareQuizContentForRuntime(
  document: QuizContentDocument,
  mode: "local-practice" | "server-graded",
): QuizRuntimeContentDocument {
  return mode === "server-graded"
    ? { mode, document: toQuizLearnerContentDocument(document) }
    : { mode, document };
}

export function prepareQuizLearnerContentForRuntime(
  document: QuizLearnerContentDocument,
): QuizRuntimeContentDocument {
  return { mode: "server-graded", document };
}

export function parseQuizLearnerContentDocument(
  value: unknown,
): QuizLearnerContentParseResult {
  const root = asRecord(value);
  if (!root) return failedLearnerRoot("Quiz content must be an object");
  if (root.schemaVersion !== QUIZ_CONTENT_SCHEMA_VERSION) {
    return failedLearnerRoot(
      `Expected quiz content schema version ${QUIZ_CONTENT_SCHEMA_VERSION}`,
      "unsupported-version",
      "schemaVersion",
    );
  }

  const issues: QuizContentParseIssue[] = [];
  for (const key of Object.keys(root)) {
    if (!LEARNER_ROOT_KEYS.has(key)) {
      issues.push({
        code: "invalid-root",
        path: key,
        message: "Unknown learner quiz content field",
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
    return { document: emptyLearnerDocument(), issues };
  }

  const normalizedOrder: QuizLearnerContentDocument["order"] = [];
  const normalizedBlocks: QuizLearnerContentDocument["blocks"] = {};
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

    const parsed = safeParseQuizLearnerEntry(sourceBlocks[id]);
    if (!parsed.success) {
      issues.push({
        code: "invalid-quiz-entry",
        path: `blocks.${id}`,
        message: parsed.error.issues
          .map(
            (issue) => `${issue.path.join(".") || "entry"}: ${issue.message}`,
          )
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

  return {
    document: {
      schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
      order: normalizedOrder,
      blocks: normalizedBlocks,
    },
    issues,
  };
}

export function isQuizRuntimeContentDocument(
  value: unknown,
): value is QuizRuntimeContentDocument {
  if (!value || typeof value !== "object" || Array.isArray(value)) return false;
  const runtime = value as Record<string, unknown>;
  if (runtime.mode !== "local-practice" && runtime.mode !== "server-graded") {
    return false;
  }
  if (
    !runtime.document ||
    typeof runtime.document !== "object" ||
    Array.isArray(runtime.document)
  ) {
    return false;
  }

  const document = runtime.document as Record<string, unknown>;
  return (
    Array.isArray(document.order) &&
    Boolean(document.blocks) &&
    typeof document.blocks === "object" &&
    !Array.isArray(document.blocks)
  );
}

function emptyLearnerDocument(): QuizLearnerContentDocument {
  return {
    schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
    order: [],
    blocks: {},
  };
}

function failedLearnerRoot(
  message: string,
  code: QuizContentParseIssue["code"] = "invalid-root",
  path = "",
): QuizLearnerContentParseResult {
  return {
    document: emptyLearnerDocument(),
    issues: [{ code, path, message }],
  };
}

function asRecord(value: unknown): UnknownRecord | null {
  return value && typeof value === "object" && !Array.isArray(value)
    ? (value as UnknownRecord)
    : null;
}
