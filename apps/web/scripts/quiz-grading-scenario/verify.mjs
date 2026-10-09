import {
  assertStackAvailable,
  authenticatePersona,
  isForbidden,
  request,
  unwrap,
} from "./api.mjs";
import {
  checkpointIndex,
  getScenarioDefinition,
  hasInstructorReview,
  nextApplicableCheckpoint,
} from "./definitions.mjs";
import { readManifest } from "./manifest.mjs";

export async function verifyScenario(scenarioKey, { strict = true } = {}) {
  const manifest = await readManifest(scenarioKey);
  const definition = getScenarioDefinition(scenarioKey);
  const checks = [];
  let confirmed = "empty";

  await assertStackAvailable(
    manifest.environment.apiBaseUrl,
    manifest.environment.webBaseUrl,
  );
  const instructor = await authenticatePersona(manifest, "instructor", {
    create: false,
  });

  try {
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

    const content = unwrap(
      await instructor.modules.content.getCoursesContentById(
        resources.courseId,
        resources.contentId,
      ),
      "Verify quiz content",
    );
    expect(
      content.slug?.includes(manifest.marker) &&
        content.type === "Questionnaire",
      "quiz content",
      checks,
      "content is not the owned Questionnaire",
    );

    const assessment = unwrap(
      await instructor.modules.assessments.getAssessments(
        resources.assessmentId,
      ),
      "Verify assessment",
    );
    expect(
      assessment.contentId === resources.contentId,
      "assessment content link",
      checks,
    );
    expect(
      assessment.assessmentGroupId === resources.gradingGroupId,
      "assessment grading group",
      checks,
    );
    expect(
      assessment.reviewMethods === definition.reviewMethodValue,
      "review workflow",
      checks,
      `expected ${definition.reviewMethodValue}, received ${assessment.reviewMethods}`,
    );
    expect(
      assessment.resultReleaseMode === "manual",
      "manual release policy",
      checks,
    );
    expect(
      assessment.maxScore === definition.expected.maxScore,
      "canonical max score",
      checks,
      `expected ${definition.expected.maxScore}, received ${assessment.maxScore}`,
    );
    if (definition.subject === "collective") {
      expect(
        assessment.groupSetId === resources.groupSetId,
        "collective group set",
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
        "candidate revision identity",
        checks,
      );
      confirmed = "test-run-ready";
    }

    const learner = manifest.personas.learnerA.userId
      ? await authenticatePersona(manifest, "learnerA", { create: false })
      : null;
    const outsider = manifest.personas.outsider.userId
      ? await authenticatePersona(manifest, "outsider", { create: false })
      : null;
    const publishedRevisionId = state.published?.revisionId;
    const learnerReady =
      Boolean(publishedRevisionId) &&
      course.status === "Published" &&
      Boolean(manifest.personas.learnerA.enrollmentId);

    if (learnerReady && learner && outsider) {
      const learnerDashboard = await request(
        learner.client,
        "Verify learner dashboard",
        {
          method: "GET",
          path: "/v1/learning/me/dashboard",
        },
      );
      const learnerAccess = unwrap(
        await learner.client.request({
          method: "GET",
          path: `/v1/courses/${resources.courseId}/access/capabilities`,
          requiresAuth: true,
        }),
        "Verify learner capabilities",
      );
      const outsiderAccess = unwrap(
        await outsider.client.request({
          method: "GET",
          path: `/v1/courses/${resources.courseId}/access/capabilities`,
          requiresAuth: true,
        }),
        "Verify outsider capabilities",
      );
      expect(
        learnerAccess.canLearn === true,
        "learner enrollment access",
        checks,
      );
      expect(
        learnerDashboard.courses?.some(
          (entry) =>
            entry.courseId === resources.courseId &&
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

      if (definition.subject === "collective") {
        await verifyCollectiveGroup(manifest, instructor, checks);
      }
      confirmed = "learner-ready";
    }

    if (resources.submissionId && learner) {
      const submission = await request(
        learner.client,
        "Verify official submission",
        {
          method: "GET",
          path: `/v1/assessments/runtime-submissions/${resources.submissionId}`,
        },
      );
      expect(
        submission.definitionRevisionId === publishedRevisionId,
        "submission frozen revision",
        checks,
      );
      expect(
        submission.status !== "InProgress",
        "official response submitted",
        checks,
      );

      if (outsider) {
        const unauthorizedRead = await outsider.client.request({
          method: "GET",
          path: `/v1/assessments/runtime-submissions/${resources.submissionId}`,
          requiresAuth: true,
        });
        expect(
          !unauthorizedRead.ok && isForbidden(unauthorizedRead.error),
          "outsider denied submission read",
          checks,
          unauthorizedRead.ok
            ? "outsider unexpectedly read the submission"
            : undefined,
        );
      }

      if (submission.execution?.requiresInstructorReview) {
        expect(
          hasInstructorReview(definition),
          "review-ready matches workflow",
          checks,
        );
        expect(
          submission.execution.learnerVisibleResult == null,
          "result withheld before review/release",
          checks,
        );
        confirmed = "review-ready";
      } else {
        const finalResult = getFinalResult(submission.execution);
        if (!finalResult) {
          expect(
            false,
            "final review result",
            checks,
            "execution does not contain a finalized result",
          );
        } else {
          const expectedScore = hasInstructorReview(definition)
            ? definition.expected.instructorScore
            : definition.expected.automatedScore;
          expect(
            finalResult.score === expectedScore,
            "final score",
            checks,
            `expected ${expectedScore}, received ${finalResult.score}`,
          );
          if (!submission.execution.released) {
            expect(
              submission.execution.learnerVisibleResult == null,
              "manual release withholds learner result",
              checks,
            );
            confirmed = "release-ready";
          } else {
            expect(
              submission.execution.learnerVisibleResult?.score ===
                expectedScore,
              "released learner score",
              checks,
            );
            const learnerB =
              definition.subject === "collective"
                ? await authenticatePersona(manifest, "learnerB", {
                    create: false,
                  })
                : null;
            await verifyGradebook(
              manifest,
              definition,
              { learnerA: learner, learnerB },
              { learnerA: submission.enrollmentId },
              checks,
            );
            confirmed = "released";
          }
        }
      }
    }
  } catch (error) {
    checks.push({
      name: "verification request",
      ok: false,
      detail: error.message,
    });
  }

  return buildReport({ manifest, scenarioKey, confirmed, checks, strict });
}

function buildReport({ manifest, scenarioKey, confirmed, checks, strict }) {
  const declaredIndex = checkpointIndex(manifest.checkpoint);
  const confirmedIndex = checkpointIndex(confirmed);
  if (confirmedIndex < declaredIndex) {
    checks.push({
      name: "manifest checkpoint",
      ok: false,
      detail: `manifest declares ${manifest.checkpoint}, server confirms only ${confirmed}`,
    });
  } else {
    checks.push({
      name: "manifest checkpoint",
      ok: true,
      detail: `declared ${manifest.checkpoint}, confirmed ${confirmed}`,
    });
  }

  const report = {
    scenario: scenarioKey,
    runId: manifest.runId,
    declaredCheckpoint: manifest.checkpoint,
    confirmedCheckpoint: confirmed,
    nextCheckpoint: nextApplicableCheckpoint(
      getScenarioDefinition(scenarioKey),
      confirmed,
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
  if (visibleResult?.state?.toLowerCase() === "final") return visibleResult;

  const activeRound = execution.history?.find(
    (round) => round.roundId === execution.activeRoundId,
  );
  if (
    activeRound?.status?.toLowerCase() === "finalized" &&
    activeRound.result?.state?.toLowerCase() === "final"
  ) {
    return activeRound.result;
  }
  return null;
}

async function verifyCollectiveGroup(manifest, instructor, checks) {
  const resources = manifest.resources;
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

async function verifyGradebook(
  manifest,
  definition,
  learners,
  authoritativeEnrollmentIds,
  checks,
) {
  const personaKeys =
    definition.subject === "collective"
      ? ["learnerA", "learnerB"]
      : ["learnerA"];
  const projectedSubmissionIds = [];
  for (const personaKey of personaKeys) {
    const enrollmentId =
      authoritativeEnrollmentIds[personaKey] ??
      manifest.personas[personaKey].enrollmentId;
    const learner = learners[personaKey];
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
      .find(
        (assessment) =>
          assessment.assessmentId === manifest.resources.assessmentId,
      );
    expect(
      projection?.released === true &&
        projection.submissionId === manifest.resources.submissionId,
      `gradebook projection ${enrollmentId}`,
      checks,
    );
    projectedSubmissionIds.push(projection?.submissionId);
  }
  if (definition.subject === "collective") {
    expect(
      new Set(projectedSubmissionIds).size === 1,
      "single collective result projection",
      checks,
    );
  }
}

function expect(condition, name, checks, detail) {
  const ok = Boolean(condition);
  checks.push({ name, ok, ...(!ok && detail ? { detail } : {}) });
}
