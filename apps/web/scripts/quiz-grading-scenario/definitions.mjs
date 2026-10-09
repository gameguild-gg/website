import { createHash } from "node:crypto";

export const DEFINITION_SCHEMA_VERSION = 1;

export const REVIEW_METHODS = Object.freeze({
  AutomatedReview: 4,
  InstructorReview: 8,
});

export const CHECKPOINTS = Object.freeze([
  "empty",
  "authoring-ready",
  "test-run-ready",
  "learner-ready",
  "review-ready",
  "release-ready",
  "released",
]);

const QUIZ_DOCUMENT = Object.freeze({
  schemaVersion: 1,
  order: [["q1", "quiz"]],
  blocks: {
    q1: {
      type: "TRUE_FALSE",
      stem: "The statement is true.",
      points: 200,
      correctAnswer: true,
      settings: { allowRetry: false },
    },
  },
  grading: { schemaVersion: 2, items: { q1: {} } },
});

const CORRECT_ANSWER = Object.freeze({
  schemaVersion: 1,
  contentType: "quiz",
  payloadSchema: "quiz-answer/v1",
  payload: {
    answers: {
      q1: { type: "TRUE_FALSE", value: true },
    },
  },
});

const BASE = Object.freeze({
  schemaVersion: DEFINITION_SCHEMA_VERSION,
  release: "manual",
  gradingGroup: {
    title: "Scenario gradebook",
    // PercentValue uses hundredths of one percent: 100% = 10,000.
    weightPercent: 10_000,
  },
  quiz: {
    title: "Deterministic grading quiz",
    description: "Local quiz grading scenario fixture.",
    document: QUIZ_DOCUMENT,
    correctAnswer: CORRECT_ANSWER,
  },
  expected: {
    maxScore: 200,
    automatedScore: 200,
    instructorScore: 175,
  },
});

const DEFINITIONS = Object.freeze({
  automated: makeDefinition({
    key: "automated",
    title: "Automated review",
    subject: "individual",
    reviewMethods: ["AutomatedReview"],
  }),
  instructor: makeDefinition({
    key: "instructor",
    title: "Instructor review",
    subject: "individual",
    reviewMethods: ["InstructorReview"],
  }),
  "automated-instructor": makeDefinition({
    key: "automated-instructor",
    title: "Automated and instructor review",
    subject: "individual",
    reviewMethods: ["AutomatedReview", "InstructorReview"],
  }),
  "collective-automated-instructor": makeDefinition({
    key: "collective-automated-instructor",
    title: "Collective automated and instructor review",
    subject: "collective",
    reviewMethods: ["AutomatedReview", "InstructorReview"],
  }),
});

function makeDefinition(input) {
  return Object.freeze({
    ...BASE,
    ...input,
    reviewMethodValue: input.reviewMethods.reduce(
      (value, method) => value | REVIEW_METHODS[method],
      0,
    ),
  });
}

export function listScenarioDefinitions() {
  return Object.values(DEFINITIONS);
}

export function getScenarioDefinition(key) {
  const definition = DEFINITIONS[key];
  if (!definition) {
    throw new Error(
      `Unknown scenario "${key}". Available scenarios: ${Object.keys(DEFINITIONS).join(", ")}.`,
    );
  }
  return definition;
}

export function assertCheckpoint(value) {
  if (!CHECKPOINTS.includes(value)) {
    throw new Error(
      `Unknown checkpoint "${value}". Available checkpoints: ${CHECKPOINTS.slice(1).join(", ")}.`,
    );
  }
  return value;
}

export function checkpointIndex(checkpoint) {
  assertCheckpoint(checkpoint);
  return CHECKPOINTS.indexOf(checkpoint);
}

export function hasInstructorReview(definition) {
  return definition.reviewMethods.includes("InstructorReview");
}

export function assertApplicableCheckpoint(definition, checkpoint) {
  assertCheckpoint(checkpoint);
  if (checkpoint === "review-ready" && !hasInstructorReview(definition)) {
    throw new Error(
      `Checkpoint review-ready does not apply to scenario "${definition.key}" because it has no InstructorReview stage. Use release-ready instead.`,
    );
  }
}

export function nextApplicableCheckpoint(definition, checkpoint) {
  const currentIndex = checkpointIndex(checkpoint);
  return (
    CHECKPOINTS.slice(currentIndex + 1).find(
      (candidate) =>
        candidate !== "review-ready" || hasInstructorReview(definition),
    ) ?? null
  );
}

export function stableJson(value) {
  return JSON.stringify(sortValue(value));
}

export function requestHash(value) {
  return createHash("sha256").update(stableJson(value)).digest("hex");
}

function sortValue(value) {
  if (Array.isArray(value)) return value.map(sortValue);
  if (value === null || typeof value !== "object") return value;
  return Object.fromEntries(
    Object.entries(value)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, child]) => [key, sortValue(child)]),
  );
}
