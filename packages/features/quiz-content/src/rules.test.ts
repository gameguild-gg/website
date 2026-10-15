import { describe, expect, it } from "vitest";
import { createTrueFalseEntry } from "@game-guild/quiz";
import { QUIZ_BLOCK_TYPE, QUIZ_CONTENT_SCHEMA_VERSION } from "./constants";
import { toQuizLearnerContentDocument, parseQuizLearnerContentDocument } from "./learner";
import { parseQuizContentDocument } from "./parsing";
import {
  QUIZ_RULES_LIMITS,
  hasQuizTimeLimit,
  hasRemainingQuizAttempts,
  isQuizPastDue,
  isValidQuizRules,
  normalizeQuizRules,
  parseQuizRules,
} from "./rules";
import {
  quizContentItemsToDocument,
  readQuizContentRules,
  updateQuizContentRules,
} from "./grading";
import type { QuizContentDocument } from "./types";

function documentWithRules(rules: unknown): QuizContentDocument {
  return {
    schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
    order: [],
    blocks: {},
    ...(rules ? { rules: rules as QuizContentDocument["rules"] } : {}),
  };
}

describe("parseQuizRules (issue #432)", () => {
  it("accepts valid rules", () => {
    const result = parseQuizRules({
      dueAt: "2026-01-15T23:59:00.000Z",
      timeLimitMinutes: 30,
      maxAttempts: 3,
    });
    expect(result.issues).toEqual([]);
    expect(result.rules).toEqual({
      dueAt: "2026-01-15T23:59:00.000Z",
      timeLimitMinutes: 30,
      maxAttempts: 3,
    });
  });

  it("treats absent and empty rules as unconfigured", () => {
    expect(parseQuizRules(undefined)).toEqual({ rules: undefined, issues: [] });
    expect(parseQuizRules(null).issues).toEqual([]);
    expect(parseQuizRules({}).rules).toBeUndefined();
  });

  it("rejects non-object rules payloads", () => {
    const result = parseQuizRules(42);
    expect(result.rules).toBeUndefined();
    expect(result.issues).toEqual([
      { code: "invalid-rules", path: "rules", message: "Quiz rules must be an object" },
    ]);
  });

  it("reports invalid field values and keeps the valid ones", () => {
    const result = parseQuizRules({
      dueAt: "not-a-date",
      timeLimitMinutes: 0,
      maxAttempts: "three",
    });
    expect(result.rules).toBeUndefined();
    expect(result.issues.map((issue) => issue.path)).toEqual([
      "rules.dueAt",
      "rules.timeLimitMinutes",
      "rules.maxAttempts",
    ]);
    expect(result.issues.every((issue) => issue.code === "invalid-rules-field")).toBe(true);
  });

  it("reports unknown rules fields", () => {
    const result = parseQuizRules({ shuffle: true });
    expect(result.issues[0]).toMatchObject({
      code: "invalid-rules",
      path: "rules.shuffle",
    });
  });

  it("enforces documented bounds", () => {
    expect(
      parseQuizRules({ timeLimitMinutes: QUIZ_RULES_LIMITS.maxTimeLimitMinutes + 1 }).issues,
    ).toHaveLength(1);
    expect(
      parseQuizRules({ maxAttempts: QUIZ_RULES_LIMITS.maxMaxAttempts + 1 }).issues,
    ).toHaveLength(1);
    expect(
      parseQuizRules({ timeLimitMinutes: QUIZ_RULES_LIMITS.minTimeLimitMinutes }).issues,
    ).toHaveLength(0);
  });

  it("isValidQuizRules and normalizeQuizRules agree with the parser", () => {
    expect(isValidQuizRules({ maxAttempts: 2 })).toBe(true);
    expect(isValidQuizRules({ maxAttempts: 0 })).toBe(false);
    expect(normalizeQuizRules({ timeLimitMinutes: 10 })).toEqual({ timeLimitMinutes: 10 });
    expect(normalizeQuizRules({ timeLimitMinutes: -1 })).toBeUndefined();
  });
});

describe("quiz document rules integration", () => {
  it("parses and normalizes rules on authoring documents", () => {
    const result = parseQuizContentDocument({
      schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
      order: [],
      blocks: {},
      rules: { dueAt: "2026-01-15T23:59:00.000Z", maxAttempts: 2 },
    });
    expect(result.issues).toEqual([]);
    expect(result.document.rules).toEqual({
      dueAt: "2026-01-15T23:59:00.000Z",
      maxAttempts: 2,
    });
  });

  it("reports invalid rules at the document level", () => {
    const result = parseQuizContentDocument({
      schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
      order: [],
      blocks: {},
      rules: { dueAt: "yesterday-ish" },
    });
    expect(result.issues[0]).toMatchObject({
      code: "invalid-rules-field",
      path: "rules.dueAt",
    });
    expect(result.document.rules).toBeUndefined();
  });

  it("keeps pre-rules documents parsing unchanged", () => {
    const result = parseQuizContentDocument({
      schemaVersion: QUIZ_CONTENT_SCHEMA_VERSION,
      order: [],
      blocks: {},
    });
    expect(result.issues).toEqual([]);
    expect(result.document.rules).toBeUndefined();
  });

  it("round-trips rules through read/update helpers", () => {
    const base = documentWithRules({ timeLimitMinutes: 20 });
    expect(readQuizContentRules(base)).toEqual({ timeLimitMinutes: 20 });

    const updated = updateQuizContentRules(base, { maxAttempts: 5 });
    expect(readQuizContentRules(updated)).toEqual({ maxAttempts: 5 });

    const cleared = updateQuizContentRules(updated, null);
    expect(readQuizContentRules(cleared)).toBeUndefined();
    expect("rules" in cleared).toBe(false);
  });

  it("quizContentItemsToDocument carries rules and questions together", () => {
    const entry = createTrueFalseEntry("Q?");
    const document = quizContentItemsToDocument({
      items: [{ id: "1", entry }],
      rules: { dueAt: "2026-02-01T00:00:00.000Z", timeLimitMinutes: 45, maxAttempts: 1 },
    });
    expect(document.rules).toEqual({
      dueAt: "2026-02-01T00:00:00.000Z",
      timeLimitMinutes: 45,
      maxAttempts: 1,
    });
    expect(document.order).toEqual([["1", QUIZ_BLOCK_TYPE]]);
  });

  it("learner projections keep rules so the runtime can enforce them", () => {
    const authoring = documentWithRules({ dueAt: "2026-01-15T23:59:00.000Z" });
    const learner = toQuizLearnerContentDocument(authoring);
    expect(learner.rules).toEqual({ dueAt: "2026-01-15T23:59:00.000Z" });

    const parsed = parseQuizLearnerContentDocument(learner);
    expect(parsed.issues).toEqual([]);
    expect(parsed.document.rules).toEqual({ dueAt: "2026-01-15T23:59:00.000Z" });
  });
});

describe("quiz rules runtime gates", () => {
  const now = new Date("2026-01-15T12:00:00.000Z");

  it("isQuizPastDue only trips once the due date has passed", () => {
    expect(isQuizPastDue({ dueAt: "2026-01-15T11:00:00.000Z" }, now)).toBe(true);
    expect(isQuizPastDue({ dueAt: "2026-01-15T13:00:00.000Z" }, now)).toBe(false);
    expect(isQuizPastDue(undefined, now)).toBe(false);
  });

  it("hasQuizTimeLimit reflects the configured limit", () => {
    expect(hasQuizTimeLimit({ timeLimitMinutes: 10 })).toBe(true);
    expect(hasQuizTimeLimit({ maxAttempts: 1 })).toBe(false);
    expect(hasQuizTimeLimit(undefined)).toBe(false);
  });

  it("hasRemainingQuizAttempts enforces maxAttempts and defaults to open", () => {
    expect(hasRemainingQuizAttempts({ maxAttempts: 3 }, 2)).toBe(true);
    expect(hasRemainingQuizAttempts({ maxAttempts: 3 }, 3)).toBe(false);
    expect(hasRemainingQuizAttempts(undefined, 99)).toBe(true);
  });
});
