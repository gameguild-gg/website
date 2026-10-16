import {
  assertStackAvailable,
  authenticatePersona,
  isForbidden,
  request,
  unwrap,
} from "./api.mjs";
import {
  checkpointIndex,
  expectedFinalScore,
  getScenarioDefinition,
  hasInstructorReview,
  hasPeerReview,
  nextApplicableCheckpoint,
} from "./definitions.mjs";
import { readManifest, selectedManifestQuizzes } from "./manifest.mjs";

export async function verifyScenario(scenarioKey, { strict = true } = {}) {
  const manifest = await readManifest(scenarioKey);
  const definition = getScenarioDefinition(scenarioKey);
  const quizzes = selectedManifestQuizzes(manifest);
  const checks = [];
  const confirmedByQuiz = {};

  await assertStackAvailable(
    manifest.environment.apiBaseUrl,
    manifest.environment.webBaseUrl,
  );
  const instructor = await authenticatePersona(manifest, "instructor", {
    create: false,
  });

  try {
    const course = await verifySharedResources(
      manifest,
      definition,
      instructor,
      checks,
    );
    const learnerA = manifest.personas.learnerA.userId
      ? await authenticatePersona(manifest, "learnerA", { create: false })
      : null;
    const learnerB = manifest.personas.learnerB?.userId
      ? await authenticatePersona(manifest, "learnerB", { create: false })
      : null;
    const outsider = manifest.personas.outsider.userId
      ? await authenticatePersona(manifest, "outsider", { create: false })
      : null;

    if (learnerA && outsider) {
      await verifyPersonaAccess(manifest, course, learnerA, outsider, checks);
    }
    if (quizzes.some((quiz) => quiz.subject === "collective")) {
      await verifyCollectiveGroup(manifest, instructor, checks);
    }

    for (const quiz of quizzes) {
      confirmedByQuiz[quiz.key] = await verifyQuiz({
        manifest,
        definition,
        quiz,
        course,
        instructor,
        learners: { learnerA, learnerB },
        outsider,
        checks,
      });
    }
  } catch (error) {
    checks.push({
      name: "verification request",
      ok: false,
      detail: error.message,
    });
  }

  return buildReport({
    manifest,
    definition,
    scenarioKey,
    confirmedByQuiz,
    checks,
    strict,
  });
}

async function verifySharedResources(manifest, definition, instructor, checks) {
  const resources = manifest.resources;
  const course = unwrap(
    await instructor.modules.courses.getCoursesForGetCoursesById(
      resources.courseId,
    ),
    "Verify course",
  );
  expect(
    course.slug === resources.courseSlug &&
      course.title?.includes(manifest.marker),
    "course ownership",
    checks,
    `expected marker ${manifest.marker}`,
  );

  const groups = unwrap(
    await instructor.modules.assessments.getAssessmentsCourseGroups(
      resources.courseId,
    ),
    "Verify assessment groups",
  );
  expect(
    groups.some(
      (group) =>
        group.id === resources.gradingGroupId &&
        group.name?.includes(manifest.marker) &&
        group.weightPercent === definition.gradingGroup.weightPercent,
    ),
    "assessment group",
    checks,
    "group, ownership marker, or weight differs",
  );
  return course;
}

async function verifyPersonaAccess(
  manifest,
  course,
  learner,
  outsider,
  checks,
) {
  if (course.status !== "Published") return;
  const learnerDashboard = await request(
    learner.client,
    "Verify learner dashboard",
    { method: "GET", path: "/v1/learning/me/dashboard" },
  );
  const learnerAccess = unwrap(
    await learner.client.request({
      method: "GET",
      path: `/v1/courses/${manifest.resources.courseId}/access/capabilities`,
      requiresAuth: true,
    }),
    "Verify learner capabilities",
  );
  const outsiderAccess = unwrap(
    await outsider.client.request({
      method: "GET",
      path: `/v1/courses/${manifest.resources.courseId}/access/capabilities`,
      requiresAuth: true,
    }),
    "Verify outsider capabilities",
  );
  expect(learnerAccess.canLearn === true, "learner enrollment access", checks);
  expect(
    learnerDashboard.courses?.some(
      (entry) =>
        entry.courseId === manifest.resources.courseId &&
        entry.enrollmentStatus === "Active",
    ),
    "learner dashboard membership",
    checks,
    "active course is missing from /v1/learning/me/dashboard",
  );
  expect(learnerAccess.canEdit === false, "learner cannot edit", checks);
  expect(
    outsiderAccess.canLearn === false,
    "outsider denied learning access",
    checks,
  );
  expect(
    outsiderAccess.canAccessWorkspace === false,
    "outsider denied workspace",
    checks,
  );
}

async function verifyQuiz({
  manifest,
  quiz,
  course,
  instructor,
  learners,
  outsider,
  checks,
}) {
  const resources = manifest.resources.quizzes[quiz.key];
  const prefix = `[${quiz.key}]`;
  let confirmed = "empty";

  const content = unwrap(
    await instructor.modules.content.getCoursesContentById(
      manifest.resources.courseId,
      resources.contentId,
    ),
    `Verify quiz content ${quiz.key}`,
  );
  expect(
    content.slug === `${manifest.marker}-${quiz.key}` &&
      content.type === "Questionnaire",
    `${prefix} quiz content`,
    checks,
    "content is not the owned Questionnaire",
  );

  const assessment = unwrap(
    await instructor.modules.assessments.getAssessments(resources.assessmentId),
    `Verify assessment ${quiz.key}`,
  );
  expect(
    assessment.contentId === resources.contentId,
    `${prefix} assessment content link`,
    checks,
  );
  expect(
    assessment.assessmentGroupId === manifest.resources.gradingGroupId,
    `${prefix} assessment grading group`,
    checks,
  );
  expect(
    assessment.reviewMethods === quiz.reviewMethodValue,
    `${prefix} review workflow`,
    checks,
    `expected ${quiz.reviewMethodValue}, received ${assessment.reviewMethods}`,
  );
  expect(
    assessment.resultReleaseMode === quiz.release,
    `${prefix} release policy`,
    checks,
  );
  expect(
    assessment.maxScore === quiz.expected.maxScore,
    `${prefix} canonical max score`,
    checks,
    `expected ${quiz.expected.maxScore}, received ${assessment.maxScore}`,
  );
  if (quiz.subject === "collective") {
    expect(
      assessment.groupSetId === manifest.resources.groupSetId,
      `${prefix} collective group set`,
      checks,
    );
  }
  confirmed = "authoring-ready";

  const state = await request(instructor.client, "Verify authoring state", {
    method: "GET",
    path: `/v1/assessments/${resources.assessmentId}/authoring-state`,
  });
  if (
    (state.candidate?.revisionId && state.candidateMatchesDraft) ||
    (state.published?.revisionId && state.publishedMatchesDraft)
  ) {
    expect(
      state.candidate?.revisionId === resources.candidateRevisionId ||
        state.published?.revisionId === resources.candidateRevisionId ||
        state.published?.revisionId === resources.publishedRevisionId,
      `${prefix} candidate revision identity`,
      checks,
    );
    confirmed = "test-run-ready";
  }

  const learnerReady =
    Boolean(state.published?.revisionId) &&
    course.status === "Published" &&
    Boolean(manifest.personas.learnerA.enrollmentId);
  if (learnerReady) confirmed = "learner-ready";

  const submissionEntries = Object.entries(resources.submissions).filter(
    ([, value]) => value.submissionId,
  );
  if (submissionEntries.length === 0) return confirmed;

  let awaitingInstructor = false;
  let allFinal = true;
  let allReleased = true;
  const expectedScore = expectedFinalScore(quiz);
  for (const [personaKey, submissionResources] of submissionEntries) {
    const submission = await request(
      instructor.client,
      `Verify official submission ${quiz.key}/${personaKey}`,
      {
        method: "GET",
        path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}`,
      },
    );
    expect(
      submission.definitionRevisionId === state.published?.revisionId,
      `${prefix} ${personaKey} frozen revision`,
      checks,
    );
    expect(
      normalizeStatus(submission.status) !== "inprogress",
      `${prefix} ${personaKey} response submitted`,
      checks,
    );

    if (outsider) {
      const unauthorizedRead = await outsider.client.request({
        method: "GET",
        path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}`,
        requiresAuth: true,
      });
      expect(
        !unauthorizedRead.ok && isForbidden(unauthorizedRead.error),
        `${prefix} outsider denied ${personaKey} submission read`,
        checks,
      );
    }

    if (submission.execution?.requiresInstructorReview) {
      awaitingInstructor = true;
      allFinal = false;
      allReleased = false;
      expect(
        hasInstructorReview(quiz),
        `${prefix} review-ready matches workflow`,
        checks,
      );
      expect(
        submission.execution.learnerVisibleResult == null,
        `${prefix} result withheld before instructor review`,
        checks,
      );
      continue;
    }

    const finalResult = getFinalResult(submission.execution);
    if (!finalResult) {
      allFinal = false;
      allReleased = false;
      expect(
        false,
        `${prefix} ${personaKey} final review result`,
        checks,
        "execution does not contain a finalized result",
      );
      continue;
    }
    expect(
      finalResult.score === expectedScore,
      `${prefix} ${personaKey} final score`,
      checks,
      `expected ${expectedScore}, received ${finalResult.score}`,
    );
    if (!submission.execution.released) {
      allReleased = false;
      expect(
        submission.execution.learnerVisibleResult == null,
        `${prefix} manual release withholds ${personaKey} result`,
        checks,
      );
    } else {
      expect(
        submission.execution.learnerVisibleResult?.score === expectedScore,
        `${prefix} ${personaKey} released learner score`,
        checks,
      );
    }
  }

  if (awaitingInstructor) return "review-ready";
  if (!allFinal) return "learner-ready";
  if (!allReleased) return "release-ready";

  await verifyGradebook(manifest, quiz, resources, learners, checks);
  return "released";
}

function buildReport({
  manifest,
  definition,
  scenarioKey,
  confirmedByQuiz,
  checks,
  strict,
}) {
  for (const quiz of selectedManifestQuizzes(manifest)) {
    const declared = manifest.resources.quizzes[quiz.key].checkpoint;
    const confirmed = confirmedByQuiz[quiz.key] ?? "empty";
    expect(
      checkpointIndex(confirmed) >= checkpointIndex(declared),
      `[${quiz.key}] manifest checkpoint`,
      checks,
      `manifest declares ${declared}, server confirms only ${confirmed}`,
    );
  }
  const confirmedCheckpoint = Object.values(confirmedByQuiz).reduce(
    (earliest, checkpoint) =>
      checkpointIndex(checkpoint) < checkpointIndex(earliest)
        ? checkpoint
        : earliest,
    "released",
  );
  const report = {
    scenario: scenarioKey,
    runId: manifest.runId,
    declaredCheckpoint: manifest.checkpoint,
    confirmedCheckpoint,
    checkpoints: Object.fromEntries(
      selectedManifestQuizzes(manifest).map((quiz) => [
        quiz.key,
        {
          declared: manifest.resources.quizzes[quiz.key].checkpoint,
          confirmed: confirmedByQuiz[quiz.key] ?? "empty",
        },
      ]),
    ),
    nextCheckpoint: nextApplicableCheckpoint(
      definition,
      confirmedCheckpoint,
      selectedManifestQuizzes(manifest),
    ),
    healthy: checks.every((check) => check.ok),
    checkedAt: new Date().toISOString(),
    checks,
  };
  if (strict && !report.healthy) {
    const failures = checks
      .filter((check) => !check.ok)
      .map((check) => `${check.name}: ${check.detail ?? "failed"}`)
      .join("; ");
    throw new Error(`Scenario verification failed: ${failures}`);
  }
  return { manifest, report };
}

function getFinalResult(execution) {
  if (!execution) return null;
  const visibleResult =
    execution.instructorVisibleResult ?? execution.learnerVisibleResult;
  if (normalizeStatus(visibleResult?.state) === "final") return visibleResult;
  const activeRound = execution.history?.find(
    (round) => round.roundId === execution.activeRoundId,
  );
  if (
    normalizeStatus(activeRound?.status) === "finalized" &&
    normalizeStatus(activeRound?.result?.state) === "final"
  ) {
    return activeRound.result;
  }
  return null;
}

async function verifyCollectiveGroup(manifest, instructor, checks) {
  const resources = manifest.resources;
  if (!resources.groupSetId || !resources.courseGroupId) return;
  const groups = unwrap(
    await instructor.modules.groupSets.getCoursesGroupSetsGroups(
      resources.courseId,
      resources.groupSetId,
    ),
    "Verify collective groups",
  );
  const group = groups.find(
    (candidate) => candidate.id === resources.courseGroupId,
  );
  const expectedUsers = new Set([
    manifest.personas.learnerA.userId,
    manifest.personas.learnerB.userId,
  ]);
  const actualUsers = new Set(
    (group?.members ?? []).map((member) => member.userId),
  );
  expect(
    Boolean(group) && [...expectedUsers].every((id) => actualUsers.has(id)),
    "collective membership",
    checks,
  );
}

async function verifyGradebook(manifest, quiz, resources, learners, checks) {
  const personaKeys =
    quiz.subject === "collective"
      ? ["learnerA", "learnerB"]
      : hasPeerReview(quiz)
        ? ["learnerA", "learnerB"]
        : ["learnerA"];
  const projectedSubmissionIds = [];
  for (const personaKey of personaKeys) {
    const learner = learners[personaKey];
    const enrollmentId = manifest.personas[personaKey].enrollmentId;
    if (!learner || !enrollmentId) continue;
    const gradebook = await request(
      learner.client,
      "Verify released gradebook projection",
      {
        method: "GET",
        path: `/v1/assessments/course/${manifest.resources.courseId}/gradebook/${enrollmentId}`,
      },
    );
    const projection = (gradebook.groups ?? [])
      .flatMap((group) => group.assessments ?? [])
      .find((assessment) => assessment.assessmentId === resources.assessmentId);
    const expectedSubmission =
      quiz.subject === "collective"
        ? resources.submissions.learnerA.submissionId
        : resources.submissions[personaKey]?.submissionId;
    expect(
      projection?.released === true &&
        projection.submissionId === expectedSubmission,
      `[${quiz.key}] gradebook projection ${personaKey}`,
      checks,
    );
    projectedSubmissionIds.push(projection?.submissionId);
  }
  if (quiz.subject === "collective") {
    expect(
      new Set(projectedSubmissionIds).size === 1,
      `[${quiz.key}] single collective result projection`,
      checks,
    );
  }
}

function expect(condition, name, checks, detail) {
  const ok = Boolean(condition);
  checks.push({ name, ok, ...(!ok && detail ? { detail } : {}) });
}

function normalizeStatus(value) {
  return String(value ?? "")
    .replaceAll("-", "")
    .replaceAll("_", "")
    .toLowerCase();
}
