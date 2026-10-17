import { createHash } from "node:crypto";
import { QUESTION_TYPE_CASES } from "./capability-catalog.mjs";

export const DEFINITION_SCHEMA_VERSION = 2;

export const REVIEW_METHODS = Object.freeze({
  PeerReview: 1,
  AIReview: 2,
  AutomatedReview: 4,
  InstructorReview: 8,
  SelfReview: 16,
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
  order: [
    ["q1", "quiz"],
    ["q2", "quiz"],
  ],
  blocks: {
    q1: {
      type: "TRUE_FALSE",
      stem: "The statement is true.",
      points: 100,
      correctAnswer: true,
      settings: { allowRetry: false },
    },
    q2: {
      type: "SINGLE_CHOICE",
      stem: "Which option equals two plus two?",
      points: 100,
      options: [
        { id: "three", text: "Three" },
        { id: "four", text: "Four" },
        { id: "five", text: "Five" },
      ],
      correctOptionId: "four",
      settings: { allowRetry: false },
    },
  },
  grading: { schemaVersion: 2, items: { q1: {}, q2: {} } },
});

const CORRECT_ANSWER = Object.freeze({
  schemaVersion: 1,
  contentType: "quiz",
  payloadSchema: "quiz-answer/v1",
  payload: {
    answers: {
      q1: { type: "TRUE_FALSE", value: true },
      q2: { type: "SINGLE_CHOICE", optionId: "four" },
    },
  },
});

const BASE_QUIZ = Object.freeze({
  subject: "individual",
  release: "manual",
  maxSupportedCheckpoint: "released",
  description: "Local quiz grading scenario fixture.",
  document: QUIZ_DOCUMENT,
  correctAnswer: CORRECT_ANSWER,
  expected: {
    maxScore: 200,
    automatedScore: 200,
    selfReviewScore: 175,
    peerReviewScore: 175,
    instructorScore: 175,
  },
});

const REVIEW_QUIZZES = Object.freeze({
  automated: makeQuizDefinition({
    key: "automated",
    title: "Automated review",
    reviewMethods: ["AutomatedReview"],
  }),
  instructor: makeQuizDefinition({
    key: "instructor",
    title: "Instructor review",
    reviewMethods: ["InstructorReview"],
  }),
  "automated-instructor": makeQuizDefinition({
    key: "automated-instructor",
    title: "Automated and instructor review",
    reviewMethods: ["AutomatedReview", "InstructorReview"],
  }),
  self: makeQuizDefinition({
    key: "self",
    title: "Self review",
    reviewMethods: ["SelfReview"],
  }),
  "self-instructor": makeQuizDefinition({
    key: "self-instructor",
    title: "Self and instructor review",
    reviewMethods: ["SelfReview", "InstructorReview"],
  }),
  peer: makeQuizDefinition({
    key: "peer",
    title: "Peer review",
    reviewMethods: ["PeerReview"],
  }),
  "peer-instructor": makeQuizDefinition({
    key: "peer-instructor",
    title: "Peer and instructor review",
    reviewMethods: ["PeerReview", "InstructorReview"],
  }),
  "collective-automated-instructor": makeQuizDefinition({
    key: "collective-automated-instructor",
    title: "Collective automated and instructor review",
    subject: "collective",
    reviewMethods: ["AutomatedReview", "InstructorReview"],
  }),
});

const QUESTION_TYPE_QUIZZES = Object.freeze(
  Object.fromEntries(
    QUESTION_TYPE_CASES.map((validationCase) => [
      validationCase.key,
      makeQuestionTypeQuizDefinition(validationCase),
    ]),
  ),
);

const QUIZZES = Object.freeze({
  ...REVIEW_QUIZZES,
  ...QUESTION_TYPE_QUIZZES,
});

const DEFINITIONS = Object.freeze({
  automated: makeScenario("automated", "Automated review", ["automated"]),
  instructor: makeScenario("instructor", "Instructor review", ["instructor"]),
  "automated-instructor": makeScenario(
    "automated-instructor",
    "Automated and instructor review",
    ["automated-instructor"],
  ),
  self: makeScenario("self", "Self review", ["self"]),
  "self-instructor": makeScenario(
    "self-instructor",
    "Self and instructor review",
    ["self-instructor"],
  ),
  peer: makeScenario("peer", "Peer review", ["peer"]),
  "peer-instructor": makeScenario(
    "peer-instructor",
    "Peer and instructor review",
    ["peer-instructor"],
  ),
  "collective-automated-instructor": makeScenario(
    "collective-automated-instructor",
    "Collective automated and instructor review",
    ["collective-automated-instructor"],
  ),
  "review-matrix": makeScenario(
    "review-matrix",
    "Quiz review workflow matrix",
    [
      "automated",
      "instructor",
      "automated-instructor",
      "self",
      "self-instructor",
      "peer",
      "peer-instructor",
    ],
  ),
  "question-type-matrix": makeScenario(
    "question-type-matrix",
    "Quiz question type matrix",
    QUESTION_TYPE_CASES.map((candidate) => candidate.key),
    { capAtQuizCapability: true },
  ),
  "lifecycle-access-matrix": makeScenario(
    "lifecycle-access-matrix",
    "Quiz lifecycle and access matrix",
    ["automated", "automated-instructor"],
  ),
  "acceptance-lab": makeScenario(
    "acceptance-lab",
    "Quiz grading acceptance lab",
    [
      "automated",
      "instructor",
      "automated-instructor",
      "self",
      "self-instructor",
      "peer",
      "peer-instructor",
      ...QUESTION_TYPE_CASES.map((candidate) => candidate.key),
    ],
    { capAtQuizCapability: true },
  ),
});

function makeQuizDefinition(input) {
  const quiz = {
    ...BASE_QUIZ,
    ...input,
    description: `${BASE_QUIZ.description} Workflow: ${input.reviewMethods.join(" -> ")}.`,
    reviewMethodValue: input.reviewMethods.reduce(
      (value, method) => value | REVIEW_METHODS[method],
      0,
    ),
  };
  return Object.freeze(quiz);
}

function makeQuestionTypeQuizDefinition(validationCase) {
  const { fixture } = validationCase;
  const itemId = "q1";
  const instructorReview = fixture.officialCapability === "instructor-review";
  return makeQuizDefinition({
    key: fixture.key,
    title: `${fixture.quizEntryType}${fixture.fillBlankInputType ? ` / ${fixture.fillBlankInputType}` : ""}`,
    reviewMethods: instructorReview
      ? ["InstructorReview"]
      : ["AutomatedReview"],
    validationCaseId: fixture.id,
    questionType: fixture.quizEntryType,
    fillBlankInputType: fixture.fillBlankInputType,
    officialCapability: fixture.officialCapability,
    maxSupportedCheckpoint: fixture.maxSupportedCheckpoint,
    document: {
      schemaVersion: 1,
      order: [[itemId, "quiz"]],
      blocks: { [itemId]: fixture.entry },
      grading: { schemaVersion: 2, items: { [itemId]: {} } },
    },
    correctAnswer: {
      schemaVersion: 1,
      contentType: "quiz",
      payloadSchema: "quiz-answer/v1",
      payload: { answers: { [itemId]: fixture.answers.correct } },
    },
    expected: {
      maxScore: fixture.entry.points,
      automatedScore: fixture.expected.correct.score,
      selfReviewScore: fixture.expected.correct.score,
      peerReviewScore: fixture.expected.correct.score,
      instructorScore: fixture.expected.instructorScore ?? fixture.entry.points,
    },
  });
}

function makeScenario(
  key,
  title,
  quizKeys,
  { capAtQuizCapability = false } = {},
) {
  const quizzes = Object.freeze(quizKeys.map((quizKey) => QUIZZES[quizKey]));
  return Object.freeze({
    schemaVersion: DEFINITION_SCHEMA_VERSION,
    key,
    title,
    capAtQuizCapability,
    gradingGroup: {
      title: "Scenario gradebook",
      // PercentValue uses hundredths of one percent: 100% = 10,000.
      weightPercent: 10_000,
    },
    quizzes,
    subject: quizzes.length === 1 ? quizzes[0].subject : "multiple-workflows",
    // Single-quiz aliases keep CLI output concise without creating another
    // source of truth for scenario behavior.
    ...(quizzes.length === 1
      ? {
          quiz: quizzes[0],
          reviewMethods: quizzes[0].reviewMethods,
          reviewMethodValue: quizzes[0].reviewMethodValue,
          expected: quizzes[0].expected,
        }
      : {}),
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

export function selectQuizDefinitions(definition, requestedKeys) {
  if (!requestedKeys?.length) return definition.quizzes;
  const requested = new Set(requestedKeys);
  const unknown = [...requested].filter(
    (key) => !definition.quizzes.some((quiz) => quiz.key === key),
  );
  if (unknown.length > 0) {
    throw new Error(
      `Scenario "${definition.key}" has no quiz ${unknown.join(", ")}. Available quizzes: ${definition.quizzes.map((quiz) => quiz.key).join(", ")}.`,
    );
  }
  return definition.quizzes.filter((quiz) => requested.has(quiz.key));
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

export function hasReviewMethod(quiz, method) {
  return quiz.reviewMethods.includes(method);
}

export function hasInstructorReview(quiz) {
  return hasReviewMethod(quiz, "InstructorReview");
}

export function hasSelfReview(quiz) {
  return hasReviewMethod(quiz, "SelfReview");
}

export function hasPeerReview(quiz) {
  return hasReviewMethod(quiz, "PeerReview");
}

export function requiresLearnerB(definition) {
  return definition.quizzes.some(
    (quiz) => quiz.subject === "collective" || hasPeerReview(quiz),
  );
}

export function expectedFinalScore(quiz) {
  if (hasInstructorReview(quiz)) return quiz.expected.instructorScore;
  if (hasSelfReview(quiz)) return quiz.expected.selfReviewScore;
  if (hasPeerReview(quiz)) return quiz.expected.peerReviewScore;
  return quiz.expected.automatedScore;
}

export function assertApplicableCheckpoint(
  definition,
  checkpoint,
  quizzes = definition.quizzes,
) {
  assertCheckpoint(checkpoint);
  const unsupported = quizzes.filter(
    (quiz) =>
      checkpointIndex(checkpoint) >
      checkpointIndex(quiz.maxSupportedCheckpoint ?? "released"),
  );
  if (unsupported.length > 0 && !definition.capAtQuizCapability) {
    throw new Error(
      `Checkpoint ${checkpoint} exceeds the declared capability for ${unsupported.map((quiz) => `"${quiz.key}" (${quiz.maxSupportedCheckpoint})`).join(", ")}. Select supported cases or stop at their last observable checkpoint.`,
    );
  }
  if (checkpoint === "review-ready" && !quizzes.some(hasInstructorReview)) {
    throw new Error(
      `Checkpoint review-ready does not apply to scenario "${definition.key}" because none of its quizzes has an InstructorReview stage. Use release-ready instead.`,
    );
  }
}

export function targetCheckpointForQuiz(definition, checkpoint, quiz) {
  assertCheckpoint(checkpoint);
  const maximum = quiz.maxSupportedCheckpoint ?? "released";
  if (
    definition.capAtQuizCapability &&
    checkpointIndex(checkpoint) > checkpointIndex(maximum)
  ) {
    return maximum;
  }
  return checkpoint;
}

export function nextApplicableCheckpoint(
  definition,
  checkpoint,
  quizzes = definition.quizzes,
) {
  const currentIndex = checkpointIndex(checkpoint);
  return (
    CHECKPOINTS.slice(currentIndex + 1).find(
      (candidate) =>
        (candidate !== "review-ready" || quizzes.some(hasInstructorReview)) &&
        quizzes.every(
          (quiz) =>
            checkpointIndex(candidate) <=
            checkpointIndex(quiz.maxSupportedCheckpoint ?? "released"),
        ),
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
