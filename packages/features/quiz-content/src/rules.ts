import type { QuizContentParseIssue } from "./types";

/**
 * Quiz-level rules and configuration (issue #432): scheduling and attempt
 * constraints that apply to the whole quiz rather than to a single question.
 *
 * All fields are optional so documents authored before rules existed keep
 * parsing unchanged; authoring-time validation reports invalid values.
 */
export interface QuizRules {
  /** UTC ISO-8601 timestamp after which submissions are no longer accepted. */
  dueAt?: string;
  /** Wall-clock time limit for a single attempt, in minutes. */
  timeLimitMinutes?: number;
  /** Maximum number of graded attempts a learner may consume. */
  maxAttempts?: number;
}

/** Inclusive bounds for quiz rule values; mirrors product defaults. */
export const QUIZ_RULES_LIMITS = {
  minTimeLimitMinutes: 1,
  maxTimeLimitMinutes: 24 * 60,
  minMaxAttempts: 1,
  maxMaxAttempts: 100,
} as const;

export type QuizRulesIssueCode = "invalid-rules" | "invalid-rules-field";

export interface QuizRulesIssue {
  code: QuizRulesIssueCode;
  path: string;
  message: string;
}

export interface QuizRulesParseResult {
  rules?: QuizRules;
  issues: QuizRulesIssue[];
}

const RULES_KEYS = new Set(["dueAt", "timeLimitMinutes", "maxAttempts"]);

function isRecord(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value > 0;
}

function isInclusiveIntegerInRange(
  value: unknown,
  min: number,
  max: number,
): value is number {
  return isPositiveInteger(value) && value >= min && value <= max;
}

function isValidIsoTimestamp(value: string): boolean {
  const parsed = Date.parse(value);
  return Number.isFinite(parsed);
}

/** Parses and validates a raw `rules` payload; returns normalized rules plus issues. */
export function parseQuizRules(value: unknown): QuizRulesParseResult {
  if (value === undefined || value === null) {
    return { rules: undefined, issues: [] };
  }

  if (!isRecord(value)) {
    return {
      rules: undefined,
      issues: [
        {
          code: "invalid-rules",
          path: "rules",
          message: "Quiz rules must be an object",
        },
      ],
    };
  }

  const issues: QuizRulesIssue[] = [];
  for (const key of Object.keys(value)) {
    if (!RULES_KEYS.has(key)) {
      issues.push({
        code: "invalid-rules",
        path: `rules.${key}`,
        message: "Unknown quiz rules field",
      });
    }
  }

  const rules: QuizRules = {};

  if (value.dueAt !== undefined && value.dueAt !== null) {
    if (typeof value.dueAt === "string" && isValidIsoTimestamp(value.dueAt)) {
      rules.dueAt = value.dueAt;
    } else {
      issues.push({
        code: "invalid-rules-field",
        path: "rules.dueAt",
        message: "dueAt must be an ISO-8601 timestamp",
      });
    }
  }

  if (value.timeLimitMinutes !== undefined && value.timeLimitMinutes !== null) {
    if (
      isInclusiveIntegerInRange(
        value.timeLimitMinutes,
        QUIZ_RULES_LIMITS.minTimeLimitMinutes,
        QUIZ_RULES_LIMITS.maxTimeLimitMinutes,
      )
    ) {
      rules.timeLimitMinutes = value.timeLimitMinutes;
    } else {
      issues.push({
        code: "invalid-rules-field",
        path: "rules.timeLimitMinutes",
        message: `timeLimitMinutes must be an integer between ${QUIZ_RULES_LIMITS.minTimeLimitMinutes} and ${QUIZ_RULES_LIMITS.maxTimeLimitMinutes}`,
      });
    }
  }

  if (value.maxAttempts !== undefined && value.maxAttempts !== null) {
    if (
      isInclusiveIntegerInRange(
        value.maxAttempts,
        QUIZ_RULES_LIMITS.minMaxAttempts,
        QUIZ_RULES_LIMITS.maxMaxAttempts,
      )
    ) {
      rules.maxAttempts = value.maxAttempts;
    } else {
      issues.push({
        code: "invalid-rules-field",
        path: "rules.maxAttempts",
        message: `maxAttempts must be an integer between ${QUIZ_RULES_LIMITS.minMaxAttempts} and ${QUIZ_RULES_LIMITS.maxMaxAttempts}`,
      });
    }
  }

  return { rules: Object.keys(rules).length > 0 ? rules : undefined, issues };
}

/** True when every present rule field is valid. */
export function isValidQuizRules(value: unknown): boolean {
  return parseQuizRules(value).issues.length === 0;
}

/** Normalized copy of `rules` with invalid/unknown fields dropped. */
export function normalizeQuizRules(value: unknown): QuizRules | undefined {
  return parseQuizRules(value).rules;
}

/** Converts `QuizRulesIssue`s to document-level parse issues under `rules`. */
export function quizRulesIssuesToParseIssues(
  issues: readonly QuizRulesIssue[],
): QuizContentParseIssue[] {
  return issues.map((issue) => ({
    code: issue.code,
    path: issue.path,
    message: issue.message,
  }));
}

/** Runtime gate: true when the quiz's due date has passed at `now`. */
export function isQuizPastDue(rules: QuizRules | undefined, now: Date = new Date()): boolean {
  if (!rules?.dueAt) return false;
  const dueAt = Date.parse(rules.dueAt);
  return Number.isFinite(dueAt) && now.getTime() >= dueAt;
}

/** Runtime gate: true when the quiz enforces a time limit. */
export function hasQuizTimeLimit(rules: QuizRules | undefined): boolean {
  return rules?.timeLimitMinutes !== undefined;
}

/** Runtime gate: true when the learner has remaining attempts under `maxAttempts`. */
export function hasRemainingQuizAttempts(
  rules: QuizRules | undefined,
  attemptsUsed: number,
): boolean {
  if (rules?.maxAttempts === undefined) return true;
  return attemptsUsed < rules.maxAttempts;
}
