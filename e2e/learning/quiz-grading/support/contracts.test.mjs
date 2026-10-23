import assert from "node:assert/strict";
import {
  mkdir,
  mkdtemp,
  readFile,
  rm,
  stat,
  writeFile,
} from "node:fs/promises";
import { tmpdir } from "node:os";
import { resolve } from "node:path";
import test from "node:test";
import {
  QUESTION_TYPE_CASES,
  QUESTION_TYPE_FIXTURE_PATH,
  VALIDATION_CASES,
  selectValidationCases,
  validateCapabilityCatalog,
} from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs";
import {
  CHECKPOINTS,
  assertApplicableCheckpoint,
  checkpointIndex,
  expectedFinalScore,
  getScenarioDefinition,
  nextApplicableCheckpoint,
  requestHash,
  selectQuizDefinitions,
  targetCheckpointForQuiz,
} from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/definitions.mjs";
import { authenticateResetOwner } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/cleanup.server.mjs";
import {
  assertLocalUrl,
  createManifest,
  validateManifest,
  withScenarioLock,
  writeJsonAtomic,
  REPO_ROOT,
} from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/manifest.server.mjs";
import {
  createValidationReport,
  listValidationEvidence,
  renderValidationReport,
  sanitizeEvidence,
  validationEvidenceDirectory,
  validationRunDirectory,
  writeValidationReport,
} from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/report.server.mjs";
import {
  QUESTION_TYPE_DRIVER_KEYS,
  getQuestionTypeDriver,
  validateQuestionTypeDrivers,
} from "./question-type-drivers.mjs";
import { browserSpecsForScenario } from "./spec-selection.mjs";
import { resolveScenarioTargetKeys } from "./selection.mjs";
import { buildScenarioUrls } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/urls.mjs";

test("scenario definitions expose canonical workflows and checkpoint applicability", () => {
  assert.equal(getScenarioDefinition("automated").reviewMethodValue, 4);
  assert.equal(getScenarioDefinition("instructor").reviewMethodValue, 8);
  assert.equal(
    getScenarioDefinition("automated-instructor").reviewMethodValue,
    12,
  );
  assert.equal(checkpointIndex("released"), CHECKPOINTS.length - 1);
  assert.throws(
    () =>
      assertApplicableCheckpoint(
        getScenarioDefinition("automated"),
        "review-ready",
      ),
    /does not apply/,
  );
  assert.equal(
    nextApplicableCheckpoint(
      getScenarioDefinition("automated"),
      "learner-ready",
    ),
    "release-ready",
  );
  assert.equal(
    nextApplicableCheckpoint(
      getScenarioDefinition("automated-instructor"),
      "learner-ready",
    ),
    "review-ready",
  );
});

test("stable case IDs select only their adapter targets", () => {
  assert.deepEqual(
    resolveScenarioTargetKeys("question-type-matrix", {
      caseIds: ["QG-ITEM-SINGLE-CHOICE-001"],
      quizKeys: [],
    }),
    ["single-choice"],
  );
});

test("validation commands invoke test runners without an operational CLI", async () => {
  const rootPackage = JSON.parse(
    await readFile(resolve(REPO_ROOT, "package.json"), "utf8"),
  );
  assert.equal(rootPackage.scripts["grading:scenario"], undefined);
  assert.equal(rootPackage.scripts["grading:validation:catalog"], undefined);
  assert.match(
    rootPackage.scripts["grading:validation:contracts"],
    /--filter @game-guild\/grading test/,
  );
  assert.match(
    rootPackage.scripts["grading:validation:contracts"],
    /--filter @game-guild\/grading-adapter-quiz test/,
  );
  assert.match(
    rootPackage.scripts["grading:validation:smoke"],
    /playwright test --config=playwright\.grading\.config\.mjs/,
  );
  assert.doesNotMatch(
    rootPackage.scripts["grading:validation:smoke"],
    /quiz-grading-scenario|cli\.mjs/,
  );
});

test("browser validation boots the lean stack without SDK watchers", async () => {
  const supervisor = await readFile(
    resolve(REPO_ROOT, "scripts/e2e-stack.mjs"),
    "utf8",
  );

  assert.match(
    supervisor,
    /spawn\(process\.execPath, \["scripts\/dev-fast\.mjs"\]/,
  );
  assert.doesNotMatch(
    supervisor,
    /spawn\("pnpm", \["run", "dev:fast"\]/,
  );
  assert.doesNotMatch(supervisor, /spawn\("pnpm", \["run", "dev"\]/);
  assert.doesNotMatch(supervisor, /generate:watch|build:watch/);
  assert.match(supervisor, /DEV_FAST_API_WATCH: "0"/);
  assert.match(supervisor, /DEV_FAST_API_NO_BUILD: "1"/);
  assert.match(supervisor, /"-m:1"/);
  assert.match(supervisor, /"-nr:false"/);
  assert.match(supervisor, /DEV_FAST_WEB_BUNDLER: "turbopack"/);
  assert.match(supervisor, /DEV_FAST_WEB_MAX_OLD_SPACE_SIZE_MB/);
  assert.match(supervisor, /GRADING_VALIDATION_E2E: "1"/);
  assert.match(supervisor, /signalProcess\(dev, "SIGTERM"\)/);

  const devFast = await readFile(
    resolve(REPO_ROOT, "scripts/dev-fast.mjs"),
    "utf8",
  );
  assert.match(devFast, /web = run\(process\.execPath, webCommand/);
  assert.doesNotMatch(
    devFast,
    /\["--filter", "@game-guild\/web", "run", "dev:fast"\]/,
  );

  const nextConfig = await readFile(
    resolve(REPO_ROOT, "apps/web/next.config.ts"),
    "utf8",
  );
  assert.match(nextConfig, /GRADING_VALIDATION_E2E/);
  assert.match(nextConfig, /\.next-e2e-grading-turbo/);
});

test("review matrix keeps one isolated two-question quiz per executable workflow", () => {
  const definition = getScenarioDefinition("review-matrix");
  assert.deepEqual(
    definition.quizzes.map((quiz) => quiz.key),
    [
      "automated",
      "instructor",
      "automated-instructor",
      "self",
      "self-instructor",
      "peer",
      "peer-instructor",
    ],
  );
  for (const quiz of definition.quizzes) {
    assert.deepEqual(quiz.document.order, [
      ["q1", "quiz"],
      ["q2", "quiz"],
    ]);
    assert.deepEqual(Object.keys(quiz.correctAnswer.payload.answers), [
      "q1",
      "q2",
    ]);
    assert.equal(quiz.expected.maxScore, 200);
  }
  assert.equal(expectedFinalScore(definition.quizzes[0]), 200);
  assert.equal(expectedFinalScore(definition.quizzes[3]), 175);
  assert.equal(expectedFinalScore(definition.quizzes[5]), 175);
  assert.equal(
    expectedFinalScore(definition.quizzes[2]),
    175,
    "instructor review owns the final score when it follows another review",
  );
});

test("validation catalog is exhaustive for quiz and fill-blank discriminants", async () => {
  const source = await readFile(
    resolve(
      REPO_ROOT,
      "packages/features/quiz/src/questions/question-types.ts",
    ),
    "utf8",
  );
  const quizTypes = enumValues(source, "QuizEntryType");
  const fillTypes = enumValues(source, "FillBlankInputType");
  const fixture = JSON.parse(
    await readFile(QUESTION_TYPE_FIXTURE_PATH, "utf8"),
  );

  assert.deepEqual(
    fixture.cases
      .filter((candidate) => candidate.baseType)
      .map((candidate) => candidate.quizEntryType)
      .sort(),
    quizTypes.sort(),
  );
  assert.deepEqual(
    fixture.cases
      .map((candidate) => candidate.fillBlankInputType)
      .filter(Boolean)
      .sort(),
    fillTypes.sort(),
  );
  assert.equal(QUESTION_TYPE_CASES.length, 18);
  assert.equal(
    new Set(VALIDATION_CASES.map((candidate) => candidate.id)).size,
    VALIDATION_CASES.length,
  );
  assert.throws(
    () => validateCapabilityCatalog([...VALIDATION_CASES, VALIDATION_CASES[0]]),
    /Duplicate validation case ID/,
  );
});

test("catalog selection works by suite, stable ID and question type", () => {
  assert.equal(
    selectValidationCases({ suites: ["review"] }).every(
      (candidate) => candidate.suite === "review",
    ),
    true,
  );
  assert.deepEqual(
    selectValidationCases({ caseIds: ["QG-ITEM-ESSAY-001"] }).map(
      (candidate) => candidate.key,
    ),
    ["essay"],
  );
  assert.deepEqual(
    selectValidationCases({ questionTypes: ["FILL_IN_THE_BLANK"] }).map(
      (candidate) => candidate.key,
    ),
    ["fill-text", "fill-number", "fill-dropdown", "fill-word-bank"],
  );
  assert.throws(
    () => selectValidationCases({ caseIds: ["QG-UNKNOWN-001"] }),
    /Unknown validation case selection/,
  );
  assert.throws(
    () => selectValidationCases({ suites: ["unknown"] }),
    /Unknown validation suite selection/,
  );
});

test("recovery capabilities remain partial until their exact failure mode is proven", () => {
  const pendingRecoveryCases = [
    "QG-RECOVERY-DOUBLE-SUBMIT-001",
    "QG-RECOVERY-STALE-RELEASE-001",
    "QG-RECOVERY-REVIEW-CONTENTION-001",
    "QG-RECOVERY-SESSION-REVOKED-001",
  ];

  for (const candidate of selectValidationCases({
    caseIds: pendingRecoveryCases,
  })) {
    assert.equal(candidate.baseline.status, "partial");
    assert.match(candidate.baseline.tracking, /^test-gap:/);
    assert.ok(candidate.baseline.reason);
  }
});

test("every question fixture has an explicit browser driver or blocker", () => {
  assert.doesNotThrow(() => validateQuestionTypeDrivers());
  assert.equal(QUESTION_TYPE_DRIVER_KEYS.length, 17);
  for (const validationCase of QUESTION_TYPE_CASES) {
    assert.equal(typeof getQuestionTypeDriver(validationCase), "function");
    if (validationCase.browser.status !== "required") {
      assert.ok(validationCase.browser.reason);
      assert.ok(validationCase.baseline.tracking);
    }
  }
});

test("browser scenarios select review and question-type specs independently", () => {
  assert.deepEqual(browserSpecsForScenario("review-matrix"), [
    "e2e/learning/quiz-grading-scenario.spec.mjs",
  ]);
  assert.deepEqual(browserSpecsForScenario("question-type-matrix"), [
    "e2e/learning/quiz-grading/authoring-roundtrip.spec.mjs",
    "e2e/learning/quiz-grading/question-types.spec.mjs",
  ]);
  assert.equal(browserSpecsForScenario("acceptance-lab").length, 3);
});

test("question type and acceptance scenarios reuse catalog definitions", () => {
  const matrix = getScenarioDefinition("question-type-matrix");
  assert.deepEqual(
    matrix.quizzes.map((quiz) => quiz.validationCaseId),
    QUESTION_TYPE_CASES.map((candidate) => candidate.id),
  );
  assert.equal(
    new Set(matrix.quizzes.map((quiz) => quiz.title)).size,
    matrix.quizzes.length,
    "every validation quiz must have an unambiguous learner and gradebook title",
  );
  assert.equal(
    matrix.quizzes.find((quiz) => quiz.key === "rating-keyed").title,
    "RATING / KEYED",
  );
  assert.equal(
    matrix.quizzes.find((quiz) => quiz.key === "rating-unkeyed").title,
    "RATING / UNKEYED",
  );
  for (const quiz of matrix.quizzes) {
    assert.deepEqual(quiz.document.order, [["q1", "quiz"]]);
    assert.equal(quiz.document.blocks.q1.type, quiz.questionType);
    assert.equal(quiz.correctAnswer.payload.answers.q1.type, quiz.questionType);
  }
  const lab = getScenarioDefinition("acceptance-lab");
  assert.equal(
    QUESTION_TYPE_CASES.every((candidate) =>
      lab.quizzes.some((quiz) => quiz.validationCaseId === candidate.id),
    ),
    true,
  );
  assert.doesNotThrow(() => assertApplicableCheckpoint(matrix, "released"));
  assert.equal(
    targetCheckpointForQuiz(
      matrix,
      "released",
      matrix.quizzes.find((quiz) => quiz.key === "numeric"),
    ),
    "test-run-ready",
  );
  assert.equal(
    targetCheckpointForQuiz(
      matrix,
      "released",
      matrix.quizzes.find((quiz) => quiz.key === "hotspot"),
    ),
    "test-run-ready",
    "hotspot cannot enter learner delivery without a resolvable asset fixture",
  );
  assert.doesNotThrow(() =>
    assertApplicableCheckpoint(
      matrix,
      "released",
      selectQuizDefinitions(matrix, ["single-choice", "essay"]),
    ),
  );
});

test("validation reports redact credentials and private answer material", async () => {
  const manifest = fixtureManifest();
  const report = createValidationReport({
    manifest: {
      ...manifest,
      password: "unsafe",
      answerKey: { correctAnswer: true },
    },
    scenarioReport: {
      checks: [{ name: "[automated] final score", ok: true }],
    },
    cases: selectValidationCases({ caseIds: ["QG-REVIEW-AUTOMATED-001"] }),
    now: new Date("2026-10-17T00:00:00.000Z"),
  });
  const serialized = JSON.stringify(report);
  assert.doesNotMatch(serialized, /unsafe/);
  assert.doesNotMatch(serialized, /correctAnswer/);
  assert.match(renderValidationReport(report), /QG-REVIEW-AUTOMATED-001/);
  assert.match(
    validationRunDirectory(manifest.runId),
    /test-results[\\/]quiz-grading-validation/,
  );

  const artifacts = await writeValidationReport(report);
  try {
    const screenshotDirectory = resolve(
      validationEvidenceDirectory(manifest.runId, "QG-REVIEW-AUTOMATED-001"),
      "screenshots",
    );
    await mkdir(screenshotDirectory, { recursive: true });
    await writeFile(resolve(screenshotDirectory, "result.png"), "evidence");
    assert.equal(
      JSON.parse(await readFile(artifacts.json, "utf8")).runId,
      manifest.runId,
    );
    assert.match(
      await readFile(artifacts.markdown, "utf8"),
      /Quiz grading validation report/,
    );
    assert.deepEqual(
      await listValidationEvidence(manifest.runId, "QG-REVIEW-AUTOMATED-001"),
      ["evidence/QG-REVIEW-AUTOMATED-001/screenshots/result.png"],
    );
  } finally {
    await rm(validationRunDirectory(manifest.runId), {
      recursive: true,
      force: true,
    });
  }
});

test("browser cases remain partial until a browser execution records a pass", () => {
  const manifest = fixtureManifest();
  const validationCase = selectValidationCases({
    caseIds: ["QG-ITEM-SINGLE-CHOICE-001"],
  })[0];
  const prepared = createValidationReport({
    manifest,
    scenarioReport: { checks: [] },
    cases: [validationCase],
    completedAutomations: ["api"],
  });
  assert.equal(prepared.cases[0].status, "partial");
  assert.equal(prepared.cases[0].execution.automationCompleted, false);

  const browser = createValidationReport({
    manifest,
    scenarioReport: { checks: [] },
    cases: [validationCase],
    results: [
      {
        caseId: validationCase.id,
        status: "pass",
        evidence: ["evidence/result.png"],
      },
    ],
    completedAutomations: ["api", "browser"],
  });
  assert.equal(browser.cases[0].status, "pass");
  assert.equal(browser.cases[0].reason, null);
  assert.equal(browser.cases[0].execution.automationCompleted, true);
});

test("partial browser selection never promotes unexecuted cases", () => {
  const manifest = fixtureManifest();
  const [selected, unselected] = selectValidationCases({
    caseIds: ["QG-ITEM-SINGLE-CHOICE-001", "QG-ITEM-FILL-TEXT-001"],
  });
  const report = createValidationReport({
    manifest,
    scenarioReport: { checks: [] },
    cases: [selected, unselected],
    results: [
      {
        caseId: selected.id,
        status: "pass",
        evidence: ["evidence/selected.png"],
      },
    ],
    completedAutomations: ["api", "browser"],
  });

  assert.equal(report.cases[0].status, "pass");
  assert.equal(report.cases[0].execution.automationCompleted, true);
  assert.deepEqual(report.cases[0].execution.completedAutomations, [
    "api",
    "browser",
  ]);
  assert.equal(report.cases[1].status, "partial");
  assert.equal(report.cases[1].execution.automationCompleted, false);
  assert.deepEqual(report.cases[1].execution.completedAutomations, ["api"]);
});

test("cross-cutting verification failures override observed browser passes", () => {
  const manifest = fixtureManifest();
  const validationCase = selectValidationCases({
    caseIds: ["QG-ITEM-SINGLE-CHOICE-001"],
  })[0];
  const report = createValidationReport({
    manifest,
    scenarioReport: {
      checks: [
        {
          name: "outsider denied learning access",
          ok: false,
          detail: "unexpected access",
        },
      ],
    },
    cases: [validationCase],
    results: [
      {
        caseId: validationCase.id,
        status: "pass",
        evidence: ["evidence/result.png"],
      },
    ],
    completedAutomations: ["api", "browser"],
  });

  assert.equal(report.cases[0].status, "bug");
  assert.match(report.cases[0].reason, /outsider denied learning access/);
});

test("question-type filters do not select review workflow keys", () => {
  assert.throws(
    () => selectValidationCases({ questionTypes: ["self"] }),
    /Unknown question type selection/,
  );
});

test("evidence sanitizer handles nested secrets and cycles", () => {
  const value = { token: "secret", nested: { cookie: "session" } };
  value.self = value;
  assert.deepEqual(sanitizeEvidence(value), {
    token: "[redacted]",
    nested: { cookie: "[redacted]" },
    self: "[circular]",
  });
});

test("quiz selection is ordered, rejects unknown keys, and drives the next checkpoint", () => {
  const definition = getScenarioDefinition("review-matrix");
  const selected = selectQuizDefinitions(definition, ["peer", "self"]);
  assert.deepEqual(
    selected.map((quiz) => quiz.key),
    ["self", "peer"],
  );
  assert.equal(
    nextApplicableCheckpoint(definition, "learner-ready", selected),
    "release-ready",
  );
  assert.throws(
    () => selectQuizDefinitions(definition, ["ai"]),
    /has no quiz ai/,
  );
});

test("stable request hashes do not depend on object key order", () => {
  assert.equal(
    requestHash({ b: 2, a: { y: 2, x: 1 } }),
    requestHash({ a: { x: 1, y: 2 }, b: 2 }),
  );
});

test("local URL guard rejects remote and non-HTTP targets", () => {
  assert.equal(
    assertLocalUrl("http://localhost:8080/"),
    "http://localhost:8080",
  );
  assert.equal(
    assertLocalUrl("http://127.0.0.1:3000"),
    "http://127.0.0.1:3000",
  );
  assert.throws(() => assertLocalUrl("https://example.com"), /non-local host/);
  assert.throws(() => assertLocalUrl("file:///tmp/api"), /http or https/);
});

test("manifest validation fails closed for unknown versions and ownership", () => {
  const manifest = fixtureManifest();
  assert.equal(validateManifest(manifest, "automated").marker, manifest.marker);
  assert.throws(
    () => validateManifest({ ...manifest, schemaVersion: 99 }, "automated"),
    /Unsupported manifest schemaVersion/,
  );
  assert.throws(
    () =>
      validateManifest(
        { ...manifest, marker: "foreign-resource" },
        "automated",
      ),
    /ownership marker/,
  );
});

test("persona credentials are stable, compact, and unique where required", () => {
  const manifest = createManifest({
    definitionKey: "collective-automated-instructor",
    apiBaseUrl: "http://localhost:8080",
    webBaseUrl: "http://localhost:3000",
    runId: "11111111-2222-4333-8444-555555555555",
  });
  const usernames = Object.values(manifest.personas).map(
    (persona) => persona.username,
  );
  const emails = Object.values(manifest.personas).map(
    (persona) => persona.email,
  );
  const passwords = Object.values(manifest.personas).map(
    (persona) => persona.password,
  );
  assert.equal(new Set(usernames).size, usernames.length);
  assert.equal(new Set(emails).size, emails.length);
  assert.equal(new Set(passwords).size, 1);
  assert.ok(usernames.every((username) => username.length <= 12));
  assert.ok(emails.every((email) => email.length <= 20));
  assert.ok(passwords.every((password) => password === "Qgs!1234"));
  assert.deepEqual(manifest.personas.instructor, {
    email: "instructor1@qgs.test",
    username: "instructor-1",
    password: "Qgs!1234",
    userId: null,
    enrollmentId: null,
  });
  assert.deepEqual(manifest.personas.learnerA, {
    email: "learner1@qgs.test",
    username: "learner-a",
    password: "Qgs!1234",
    userId: null,
    enrollmentId: null,
  });
});

test("reset discards a stale manifest only after proving the local tenant changed", async () => {
  const manifest = fixtureManifest();
  manifest.environment.tenantId = "11111111-1111-4111-8111-111111111111";
  const authenticationError = new Error("stale credentials");
  const calls = [];
  const result = await authenticateResetOwner(manifest, {
    authenticate: async (candidate, personaKey, options) => {
      calls.push({ candidate, personaKey, options });
      if (calls.length === 1) throw authenticationError;
      return {
        identity: {
          tenantId: "22222222-2222-4222-8222-222222222222",
        },
      };
    },
  });

  assert.deepEqual(result, {
    kind: "recreated-environment",
    previousTenantId: "11111111-1111-4111-8111-111111111111",
    currentTenantId: "22222222-2222-4222-8222-222222222222",
  });
  assert.equal(calls.length, 2);
  assert.deepEqual(calls[0].options, { create: false });
  assert.equal(calls[1].candidate.environment.tenantId, null);
});

test("reset keeps the manifest when credentials fail in the same tenant", async () => {
  const manifest = fixtureManifest();
  manifest.environment.tenantId = "11111111-1111-4111-8111-111111111111";
  const authenticationError = new Error("invalid credentials");
  let calls = 0;

  await assert.rejects(
    authenticateResetOwner(manifest, {
      authenticate: async () => {
        calls += 1;
        if (calls === 1) throw authenticationError;
        return {
          identity: {
            tenantId: "11111111-1111-4111-8111-111111111111",
          },
        };
      },
    }),
    (error) => error === authenticationError,
  );
});

test("review matrix manifest creates peer personas and isolated quiz resources", () => {
  const manifest = createManifest({
    definitionKey: "review-matrix",
    apiBaseUrl: "http://localhost:8080",
    webBaseUrl: "http://localhost:3000",
    quizKeys: ["automated", "peer"],
    runId: "11111111-2222-4333-8444-555555555555",
  });
  assert.deepEqual(manifest.quizKeys, ["automated", "peer"]);
  assert.ok(manifest.personas.learnerB);
  assert.deepEqual(Object.keys(manifest.resources.quizzes), [
    "automated",
    "peer",
  ]);
  assert.deepEqual(
    Object.keys(manifest.resources.quizzes.automated.submissions),
    ["learnerA"],
  );
  assert.deepEqual(Object.keys(manifest.resources.quizzes.peer.submissions), [
    "learnerA",
    "learnerB",
  ]);
  assert.equal(validateManifest(manifest, "review-matrix"), manifest);
});

test("atomic JSON writer replaces the target and restricts file permissions", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-atomic-"));
  const target = resolve(root, "nested", "manifest.json");
  try {
    await writeJsonAtomic(target, { version: 1 });
    await writeJsonAtomic(target, { version: 2 });
    assert.deepEqual(JSON.parse(await readFile(target, "utf8")), {
      version: 2,
    });
    if (process.platform !== "win32") {
      assert.equal((await stat(target)).mode & 0o777, 0o600);
    }
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("scenario lock rejects concurrent work for the same definition", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-lock-"));
  let release;
  const blocker = new Promise((resolveBlocker) => {
    release = resolveBlocker;
  });
  let acquired;
  const ready = new Promise((resolveReady) => {
    acquired = resolveReady;
  });
  const first = withScenarioLock(
    "automated",
    async () => {
      acquired();
      await blocker;
    },
    { root },
  );
  try {
    await ready;
    await assert.rejects(
      withScenarioLock("automated", async () => {}, { root }),
      /locked by active process/,
    );
  } finally {
    release();
    await first;
    await rm(root, { recursive: true, force: true });
  }
});

test("scenario lock recovers a lock owned by a dead process", async () => {
  const root = await mkdtemp(resolve(tmpdir(), "qgs-stale-lock-"));
  const lockPath = resolve(root, "automated.lock");
  const recovered = [];
  try {
    await writeFile(
      lockPath,
      `${JSON.stringify({ pid: 2_147_483_647, acquiredAt: "2026-10-09T00:00:00.000Z" })}\n`,
    );
    await withScenarioLock("automated", async () => {}, {
      root,
      onStaleLock: (owner) => recovered.push(owner.pid),
    });
    assert.deepEqual(recovered, [2_147_483_647]);
    await assert.rejects(stat(lockPath), { code: "ENOENT" });
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("all browser URLs come from the centralized manifest builder", () => {
  const manifest = fixtureManifest();
  Object.assign(manifest.resources, {
    courseSlug: "scenario-course",
  });
  Object.assign(manifest.resources.quizzes.automated, {
    contentId: "content-id",
    assessmentId: "assessment-id",
  });
  manifest.resources.quizzes.automated.submissions.learnerA.submissionId =
    "submission-id";
  const urls = buildScenarioUrls(manifest);
  assert.equal(urls.learnerHome, "http://localhost:3000/en-US/learn");
  assert.equal(
    urls.learnerCourses,
    "http://localhost:3000/en-US/learn/courses",
  );
  assert.equal(
    urls.learnerCourse,
    "http://localhost:3000/en-US/learn/courses/scenario-course",
  );
  assert.equal(
    urls.quizzes.automated.learnerActivity,
    "http://localhost:3000/en-US/learn/courses/scenario-course/activities/assessment-assessment-id",
  );
  assert.match(
    urls.quizzes.automated.speedGraders.learnerA,
    /submission=submission-id/,
  );
  assert.equal(
    urls.learnerActivity,
    urls.quizzes.automated.learnerActivity,
    "single-quiz scenarios retain concise URL aliases",
  );
  assert.match(
    urls.quizzes.automated.assessmentEditor,
    /workspace\/learning\/courses\/scenario-course/,
  );
});

function fixtureManifest() {
  return createManifest({
    definitionKey: "automated",
    apiBaseUrl: "http://localhost:8080",
    webBaseUrl: "http://localhost:3000",
    runId: "11111111-2222-4333-8444-555555555555",
    now: new Date("2026-10-09T00:00:00.000Z"),
  });
}

function enumValues(source, name) {
  const match = source.match(
    new RegExp(`export enum ${name} \\{([\\s\\S]*?)\\n\\}`),
  );
  assert.ok(match, `${name} declaration was not found`);
  return [...match[1].matchAll(/=\s*"([A-Z_]+)"/g)].map((entry) => entry[1]);
}
