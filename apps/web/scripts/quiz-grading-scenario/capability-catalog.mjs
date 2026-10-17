import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));

export const QUESTION_TYPE_FIXTURE_PATH = resolve(
  SCRIPT_DIR,
  "../../../../packages/features/grading-adapter-quiz/fixtures/quiz-validation-cases-v1.json",
);

export const VALIDATION_SUITES = Object.freeze([
  "review",
  "question-type",
  "lifecycle-access",
  "failure-recovery",
]);

export const VALIDATION_RESULT_STATUSES = Object.freeze([
  "pass",
  "bug",
  "partial",
  "missing",
  "decision-needed",
  "harness-gap",
  "external-blocker",
  "not-applicable",
]);

export const QUESTION_TYPE_FIXTURE = readQuestionTypeFixture();
export const QUESTION_TYPE_CASES = Object.freeze(
  QUESTION_TYPE_FIXTURE.cases.map(createQuestionTypeValidationCase),
);

const REVIEW_CASES = Object.freeze([
  reviewCase(
    "QG-REVIEW-AUTOMATED-001",
    "automated",
    "AutomatedReview finalizes a deterministic submission.",
  ),
  reviewCase(
    "QG-REVIEW-INSTRUCTOR-001",
    "instructor",
    "InstructorReview owns the final score.",
  ),
  reviewCase(
    "QG-REVIEW-AUTOMATED-INSTRUCTOR-001",
    "automated-instructor",
    "InstructorReview can replace the automated proposal before release.",
  ),
  reviewCase(
    "QG-REVIEW-SELF-001",
    "self",
    "The submitting learner completes SelfReview.",
  ),
  reviewCase(
    "QG-REVIEW-SELF-INSTRUCTOR-001",
    "self-instructor",
    "InstructorReview follows and can replace SelfReview.",
  ),
  reviewCase(
    "QG-REVIEW-PEER-001",
    "peer",
    "Enrolled learners review one another without identity disclosure.",
  ),
  reviewCase(
    "QG-REVIEW-PEER-INSTRUCTOR-001",
    "peer-instructor",
    "InstructorReview follows and can replace PeerReview.",
  ),
  {
    ...reviewCase(
      "QG-REVIEW-COLLECTIVE-001",
      "collective-automated-instructor",
      "One collective submission projects the same released result to every member.",
      "api",
    ),
    personas: ["instructor", "learnerA", "learnerB"],
  },
  validationCase({
    id: "QG-REVIEW-AI-001",
    suite: "review",
    capability: "AIReview provider execution",
    personas: ["instructor", "learnerA"],
    preconditions: ["An approved AIReview provider is registered."],
    actions: ["Submit an answer requiring the AIReview stage."],
    expected: {
      grading: [
        "The provider result is persisted with its handler version and evidence.",
      ],
    },
    automation: "manual",
    requirement: "exploratory",
    baseline: {
      status: "external-blocker",
      reason:
        "The durable port exists, but no approved AIReview provider is registered.",
      tracking: "decision:QG-AI-PROVIDER",
      owner: "learning-assessments",
    },
  }),
]);

const LIFECYCLE_ACCESS_CASES = Object.freeze([
  lifecycleCase(
    "QG-LIFECYCLE-REVISION-001",
    "Immutable candidate and published revision",
    "api",
    ["Prepare a candidate.", "Publish it.", "Edit the mutable draft."],
    ["Existing attempts remain linked to the published immutable revision."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-TEST-RUN-001",
    "Instructor Test Run isolation",
    "browser",
    ["Complete an Assessment Test Run as the instructor."],
    ["No enrollment, gradebook row, or academic submission is created."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-ATTEMPT-RESUME-001",
    "Attempt reload and resume",
    "browser",
    ["Start an attempt.", "Answer one item.", "Reload and resume."],
    ["The same attempt and draft answers are restored."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-MANUAL-RELEASE-001",
    "Manual result release",
    "browser",
    ["Finalize a result.", "Inspect as learner.", "Release as instructor."],
    ["Score remains hidden until release and appears after release."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-REGRADE-001",
    "Regrade and instructor override audit",
    "api",
    ["Finalize a round.", "Apply an instructor correction."],
    ["The active round changes without erasing the prior round."],
    ["The actor, reason, round, and correlation are auditable."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-WEIGHT-ZERO-001",
    "Zero-weight practice group",
    "api",
    ["Release a result assigned to a zero-weight group."],
    ["Feedback is retained and the course total receives no contribution."],
  ),
  lifecycleCase(
    "QG-LIFECYCLE-WEIGHTED-001",
    "Positive-weight gradebook group",
    "api",
    ["Release a result assigned to a positive-weight group."],
    ["The canonical integer score contributes exactly once."],
  ),
  lifecycleCase(
    "QG-ACCESS-LEARNER-001",
    "Active enrollment learning access",
    "browser",
    ["Open the published course as an enrolled learner."],
    ["Learning pages are available while authoring actions remain denied."],
  ),
  lifecycleCase(
    "QG-ACCESS-OUTSIDER-001",
    "Outsider direct URL denial",
    "browser",
    ["Open a known learner activity and submission URL as an outsider."],
    ["The outsider cannot start, read, review, release, or edit."],
  ),
  lifecycleCase(
    "QG-ACCESS-COLLECTIVE-001",
    "Collective subject projection",
    "api",
    ["Submit once for an enrolled course group."],
    ["Every member sees one shared submission and one released result."],
  ),
]);

const FAILURE_RECOVERY_CASES = Object.freeze([
  recoveryCase(
    "QG-RECOVERY-DOUBLE-SUBMIT-001",
    "Double submit",
    "api",
    "Two concurrent submit requests do not duplicate grading effects.",
    {
      status: "partial",
      reason:
        "Same-key replay is covered, but simultaneous HTTP submissions have not been proven against PostgreSQL.",
      tracking: "test-gap:QG-RECOVERY-DOUBLE-SUBMIT-001",
    },
  ),
  recoveryCase(
    "QG-RECOVERY-IDEMPOTENT-REPLAY-001",
    "Same-key replay",
    "api",
    "Replaying the same idempotency key and payload returns the original effect.",
  ),
  recoveryCase(
    "QG-RECOVERY-DIVERGENT-REPLAY-001",
    "Divergent replay",
    "api",
    "Reusing an idempotency key with another payload fails closed.",
  ),
  recoveryCase(
    "QG-RECOVERY-STALE-RELEASE-001",
    "Stale round release",
    "api",
    "A superseded round cannot overwrite or release the active result.",
    {
      status: "partial",
      reason:
        "Regrade visibility is covered, but an explicit release attempt against the superseded round is not.",
      tracking: "test-gap:QG-RECOVERY-STALE-RELEASE-001",
    },
  ),
  recoveryCase(
    "QG-RECOVERY-REVIEW-CONTENTION-001",
    "Concurrent reviewer mutation",
    "api",
    "Optimistic concurrency prevents silent last-writer-wins grading.",
    {
      status: "partial",
      reason:
        "Versioned review writes exist, but two simultaneous instructor mutations have not been exercised against PostgreSQL.",
      tracking: "test-gap:QG-RECOVERY-REVIEW-CONTENTION-001",
    },
  ),
  recoveryCase(
    "QG-RECOVERY-MANIFEST-RESUME-001",
    "Interrupted local scenario resume",
    "contract",
    "The runner resumes from the persisted checkpoint without duplicating resources.",
  ),
  recoveryCase(
    "QG-RECOVERY-SESSION-REVOKED-001",
    "Permission revoked during workflow",
    "api",
    "A stale session cannot perform a newly forbidden review or release action.",
    {
      status: "partial",
      reason:
        "Enrollment revocation updates contextual access, but review and release denial with the stale session are not covered end to end.",
      tracking: "test-gap:QG-RECOVERY-SESSION-REVOKED-001",
    },
  ),
]);

export const VALIDATION_CASES = Object.freeze([
  ...REVIEW_CASES,
  ...QUESTION_TYPE_CASES,
  ...LIFECYCLE_ACCESS_CASES,
  ...FAILURE_RECOVERY_CASES,
]);

validateCapabilityCatalog(VALIDATION_CASES);

export function getValidationCase(idOrKey) {
  const result = VALIDATION_CASES.find(
    (candidate) =>
      candidate.id === idOrKey ||
      candidate.key === idOrKey ||
      candidate.quizKey === idOrKey,
  );
  if (!result) {
    throw new Error(`Unknown validation case "${idOrKey}".`);
  }
  return result;
}

export function getQuestionTypeCase(key) {
  const result = QUESTION_TYPE_CASES.find(
    (candidate) => candidate.key === key || candidate.id === key,
  );
  if (!result) {
    throw new Error(
      `Unknown question-type case "${key}". Available cases: ${QUESTION_TYPE_CASES.map((candidate) => candidate.key).join(", ")}.`,
    );
  }
  return result;
}

export function selectValidationCases({ suites, caseIds, questionTypes } = {}) {
  const requestedSuites = new Set(normalizeSelection(suites));
  const requestedCases = new Set(normalizeSelection(caseIds));
  const requestedTypes = new Set(normalizeSelection(questionTypes));
  const unknownSuites = [...requestedSuites].filter(
    (value) => !VALIDATION_SUITES.includes(value),
  );
  if (unknownSuites.length > 0) {
    throw new Error(
      `Unknown validation suite selection: ${unknownSuites.join(", ")}.`,
    );
  }
  const selected = VALIDATION_CASES.filter((candidate) => {
    if (requestedSuites.size > 0 && !requestedSuites.has(candidate.suite)) {
      return false;
    }
    if (
      requestedCases.size > 0 &&
      !requestedCases.has(candidate.id) &&
      !requestedCases.has(candidate.key)
    ) {
      return false;
    }
    if (
      requestedTypes.size > 0 &&
      !requestedTypes.has(candidate.questionType) &&
      !requestedTypes.has(candidate.key)
    ) {
      return false;
    }
    return true;
  });
  const matchedCaseTokens = new Set(
    selected.flatMap((candidate) =>
      [candidate.id, candidate.key].filter(Boolean),
    ),
  );
  const unknownCases = [...requestedCases].filter(
    (value) => !matchedCaseTokens.has(value),
  );
  if (unknownCases.length > 0) {
    throw new Error(
      `Unknown validation case selection: ${unknownCases.join(", ")}.`,
    );
  }
  const knownTypes = new Set(
    selected.flatMap((candidate) =>
      [candidate.questionType, candidate.key].filter(Boolean),
    ),
  );
  const unknownTypes = [...requestedTypes].filter(
    (value) => !knownTypes.has(value),
  );
  if (unknownTypes.length > 0) {
    throw new Error(
      `Unknown question type selection: ${unknownTypes.join(", ")}.`,
    );
  }
  return selected;
}

export function validateCapabilityCatalog(cases) {
  if (!Array.isArray(cases) || cases.length === 0) {
    throw new Error(
      "Quiz grading validation catalog must be a non-empty array.",
    );
  }
  const ids = new Set();
  for (const candidate of cases) {
    requireString(candidate.id, "case id");
    if (!/^QG-[A-Z0-9-]+-\d{3}$/.test(candidate.id)) {
      throw new Error(`Validation case ID "${candidate.id}" is not stable.`);
    }
    if (ids.has(candidate.id)) {
      throw new Error(`Duplicate validation case ID "${candidate.id}".`);
    }
    ids.add(candidate.id);
    if (!VALIDATION_SUITES.includes(candidate.suite)) {
      throw new Error(`Validation case ${candidate.id} has unknown suite.`);
    }
    requireString(candidate.capability, `${candidate.id} capability`);
    requireStringArray(candidate.personas, `${candidate.id} personas`);
    requireStringArray(
      candidate.preconditions,
      `${candidate.id} preconditions`,
    );
    requireStringArray(candidate.actions, `${candidate.id} actions`);
    if (!candidate.expected || typeof candidate.expected !== "object") {
      throw new Error(
        `Validation case ${candidate.id} requires expected oracles.`,
      );
    }
    if (
      !["contract", "api", "browser", "manual"].includes(candidate.automation)
    ) {
      throw new Error(
        `Validation case ${candidate.id} has unknown automation.`,
      );
    }
    if (
      !["required", "recommended", "exploratory"].includes(
        candidate.requirement,
      )
    ) {
      throw new Error(
        `Validation case ${candidate.id} has unknown requirement.`,
      );
    }
    if (!VALIDATION_RESULT_STATUSES.includes(candidate.baseline?.status)) {
      throw new Error(
        `Validation case ${candidate.id} has unknown baseline status.`,
      );
    }
    if (candidate.baseline.status !== "pass") {
      requireString(
        candidate.baseline.reason,
        `${candidate.id} baseline reason`,
      );
      requireString(
        candidate.baseline.tracking,
        `${candidate.id} baseline tracking`,
      );
    }
    requireString(candidate.baseline.owner, `${candidate.id} owner`);
  }
  return cases;
}

function readQuestionTypeFixture() {
  const value = JSON.parse(readFileSync(QUESTION_TYPE_FIXTURE_PATH, "utf8"));
  if (value?.schemaVersion !== 1 || !Array.isArray(value.cases)) {
    throw new Error("Unsupported quiz validation fixture schema.");
  }
  const keys = new Set();
  for (const candidate of value.cases) {
    for (const property of [
      "id",
      "key",
      "quizEntryType",
      "officialCapability",
      "maxSupportedCheckpoint",
    ]) {
      requireString(candidate[property], `question fixture ${property}`);
    }
    if (keys.has(candidate.key)) {
      throw new Error(`Duplicate question fixture key "${candidate.key}".`);
    }
    keys.add(candidate.key);
    if (candidate.entry?.type !== candidate.quizEntryType) {
      throw new Error(
        `Question fixture ${candidate.key} has a mismatched entry type.`,
      );
    }
    if (candidate.answers?.correct?.type !== candidate.quizEntryType) {
      throw new Error(
        `Question fixture ${candidate.key} requires a representative answer.`,
      );
    }
    if (candidate.browser?.status !== "required") {
      requireString(
        candidate.browser?.reason,
        `${candidate.key} browser reason`,
      );
    }
  }
  return Object.freeze({
    ...value,
    cases: Object.freeze(value.cases.map((candidate) => deepFreeze(candidate))),
  });
}

function createQuestionTypeValidationCase(fixture) {
  const baselineStatus =
    fixture.browser.status === "required" ? "partial" : fixture.browser.status;
  return validationCase({
    id: fixture.id,
    key: fixture.key,
    quizKey: fixture.key,
    suite: "question-type",
    capability: `${fixture.quizEntryType}${fixture.fillBlankInputType ? `/${fixture.fillBlankInputType}` : ""}`,
    personas:
      fixture.officialCapability === "instructor-review"
        ? ["instructor", "learnerA"]
        : ["learnerA"],
    preconditions: [
      "The authored entry and response pass their versioned schemas.",
    ],
    actions: [
      "Round-trip the authoring entry and learner projection.",
      "Submit the representative response through the quiz grading adapter.",
    ],
    expected: {
      api: [
        `Official capability is ${fixture.officialCapability}.`,
        `No answer key is exposed by learner delivery for ${fixture.key}.`,
      ],
      grading: [
        `Correct response state is ${fixture.expected.correct.resultState}.`,
        `Missing response state is ${fixture.expected.omitted.resultState}.`,
      ],
      learner: [
        "The learner can render and restore the answer without private authoring data.",
      ],
    },
    automation: fixture.browser.status === "required" ? "browser" : "contract",
    requirement:
      fixture.officialCapability === "unsupported" ? "exploratory" : "required",
    baseline: {
      status: baselineStatus,
      reason:
        fixture.browser.status === "required"
          ? "Package and runtime contracts are covered; the browser case must pass before this capability is promoted."
          : fixture.browser.reason,
      tracking:
        fixture.browser.status === "required"
          ? `command:grading:scenario test --scenario question-type-matrix --case ${fixture.id}`
          : `decision:${fixture.id}-BROWSER`,
      owner: "quiz",
    },
    questionType: fixture.quizEntryType,
    fillBlankInputType: fixture.fillBlankInputType,
    officialCapability: fixture.officialCapability,
    browser: fixture.browser,
    fixture,
  });
}

function reviewCase(id, quizKey, capability, automation = "browser") {
  return validationCase({
    id,
    key: quizKey,
    quizKey,
    suite: "review",
    capability,
    personas: quizKey.includes("peer")
      ? ["instructor", "learnerA", "learnerB", "outsider"]
      : ["instructor", "learnerA", "outsider"],
    preconditions: [
      "The published assessment uses the declared review workflow.",
    ],
    actions: [
      "Submit the official attempt.",
      "Complete each configured review stage.",
      "Release the result.",
    ],
    expected: {
      grading: [capability],
      learner: [
        "The learner sees the result only after its release policy allows it.",
      ],
      audit: ["Review and release actors are attributable."],
    },
    automation,
    requirement: "required",
    baseline: {
      status: "pass",
      reason: null,
      tracking: null,
      owner: "learning-assessments",
    },
  });
}

function lifecycleCase(
  id,
  capability,
  automation,
  actions,
  grading,
  audit = [],
) {
  const attemptResumeGap = id === "QG-LIFECYCLE-ATTEMPT-RESUME-001";
  return validationCase({
    id,
    suite: "lifecycle-access",
    capability,
    personas: ["instructor", "learnerA", "outsider"],
    preconditions: ["A published deterministic assessment exists."],
    actions,
    expected: { grading, ...(audit.length ? { audit } : {}) },
    automation,
    requirement: "required",
    ...(automation === "browser"
      ? {
          browser: {
            status: attemptResumeGap ? "missing" : "required",
            reason: attemptResumeGap
              ? "The learner UI does not persist an in-progress item answer before final submission."
              : null,
          },
        }
      : {}),
    baseline: {
      status: attemptResumeGap ? "missing" : "pass",
      reason: attemptResumeGap
        ? "Reloading before final submission loses the local answer state."
        : null,
      tracking: attemptResumeGap
        ? "decision:QG-ATTEMPT-DRAFT-PERSISTENCE"
        : null,
      owner: "learning-assessments",
    },
  });
}

function recoveryCase(id, capability, automation, expectation, baseline = {}) {
  return validationCase({
    id,
    suite: "failure-recovery",
    capability,
    personas: ["instructor", "learnerA"],
    preconditions: [
      "The deterministic baseline passes before failure injection.",
    ],
    actions: [capability],
    expected: {
      api: [expectation],
      audit: ["The resulting effect remains attributable and non-duplicated."],
    },
    automation,
    requirement: "required",
    baseline: {
      status: "pass",
      reason: null,
      tracking: null,
      owner: "learning-assessments",
      ...baseline,
    },
  });
}

function validationCase(value) {
  return deepFreeze(value);
}

function normalizeSelection(value) {
  if (!value) return [];
  return [...new Set(Array.isArray(value) ? value : [value])];
}

function requireString(value, label) {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${label} must be a non-empty string.`);
  }
}

function requireStringArray(value, label) {
  if (
    !Array.isArray(value) ||
    value.some((entry) => typeof entry !== "string")
  ) {
    throw new Error(`${label} must be a string array.`);
  }
}

function deepFreeze(value) {
  if (!value || typeof value !== "object" || Object.isFrozen(value))
    return value;
  for (const child of Object.values(value)) deepFreeze(child);
  return Object.freeze(value);
}
