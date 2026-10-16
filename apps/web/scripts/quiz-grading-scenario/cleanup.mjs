import { authenticatePersona, isMissing, request, unwrap } from "./api.mjs";
import { getScenarioDefinition } from "./definitions.mjs";
import {
  createManifest,
  readManifestForReset,
  removeScenarioSecrets,
  writeReport,
} from "./manifest.mjs";

export async function resetScenario(scenarioKey) {
  const manifest = await readManifestForReset(scenarioKey, { optional: true });
  if (!manifest) {
    return {
      scenario: scenarioKey,
      removed: [],
      skipped: ["manifest not found"],
    };
  }
  const resources = manifest.resources;
  const removed = [];
  const skipped = [];
  const failures = [];

  if (!resources.courseId) {
    skipped.push("no remote resources were recorded");
    const report = makeReport(manifest, removed, skipped, failures);
    await writeReport(scenarioKey, report);
    await removeScenarioSecrets(scenarioKey);
    await writeReport(scenarioKey, report);
    return report;
  }

  const owner = await authenticateResetOwner(manifest);
  if (owner.kind === "recreated-environment") {
    skipped.push(
      `stale manifest from tenant ${owner.previousTenantId}; the current local tenant is ${owner.currentTenantId}`,
    );
    const report = makeReport(manifest, removed, skipped, failures);
    await removeScenarioSecrets(scenarioKey);
    await writeReport(scenarioKey, report);
    return report;
  }
  const instructor = owner.session;
  const courseResult =
    await instructor.modules.courses.getCoursesForGetCoursesById(
      resources.courseId,
    );
  if (!courseResult.ok) {
    if (isMissing(courseResult.error)) {
      skipped.push(`course ${resources.courseId} already absent`);
      await removeScenarioSecrets(scenarioKey);
      return { scenario: scenarioKey, removed, skipped, failures };
    }
    failures.push(`read course: ${formatError(courseResult.error)}`);
  } else if (!courseResult.data.title?.includes(manifest.marker)) {
    throw new Error(
      `Reset refused: course ${resources.courseId} does not contain ownership marker ${manifest.marker}.`,
    );
  }
  await stopOnFailure(manifest, removed, skipped, failures);

  if (resources.courseGroupId && resources.courseId) {
    for (const key of ["learnerA", "learnerB"]) {
      const userId = manifest.personas[key]?.userId;
      if (!userId) continue;
      await remove(
        `group member ${key}`,
        () =>
          instructor.modules.groupSets.deleteCoursesGroupSetsGroupsMembers(
            resources.courseId,
            resources.courseGroupId,
            userId,
          ),
        removed,
        skipped,
        failures,
      );
    }
    await stopOnFailure(manifest, removed, skipped, failures);
  }

  for (const { key, quiz, resources: quizResources } of scenarioQuizzes(
    manifest,
  ).reverse()) {
    if (quizResources.assessmentId) {
      let state = null;
      try {
        state = await request(
          instructor.client,
          `Read assessment ${key} before reset`,
          {
            method: "GET",
            path: `/v1/assessments/${quizResources.assessmentId}/authoring-state`,
          },
        );
      } catch {
        state = null;
      }
      if (state?.published?.revisionId) {
        const assessment = await instructor.modules.assessments.getAssessments(
          quizResources.assessmentId,
        );
        if (assessment.ok && assessment.data.version != null) {
          await remove(
            `published assessment revision ${key}`,
            () =>
              instructor.client.request({
                method: "POST",
                path: `/v1/assessments/${quizResources.assessmentId}/revisions/unpublish`,
                body: {
                  expectedAssessmentVersion: assessment.data.version,
                  expectedRevisionId: state.published.revisionId,
                  idempotencyKey: `qgs-reset-${manifest.runId}-${key}`,
                },
                requiresAuth: true,
              }),
            removed,
            skipped,
            failures,
          );
        }
      }
      await stopOnFailure(manifest, removed, skipped, failures);
      await disableContentGrading(
        manifest,
        quiz,
        quizResources,
        instructor,
        removed,
        skipped,
        failures,
      );
      await stopOnFailure(manifest, removed, skipped, failures);
    }

    if (quizResources.contentId) {
      await remove(
        `content ${key} (${quizResources.contentId})`,
        () =>
          instructor.modules.content.deleteCoursesContent(
            resources.courseId,
            quizResources.contentId,
          ),
        removed,
        skipped,
        failures,
      );
      await stopOnFailure(manifest, removed, skipped, failures);
    }
  }

  if (resources.gradingGroupId) {
    await remove(
      `assessment group ${resources.gradingGroupId}`,
      () =>
        instructor.modules.assessments.deleteAssessmentsGroups(
          resources.gradingGroupId,
        ),
      removed,
      skipped,
      failures,
    );
    await stopOnFailure(manifest, removed, skipped, failures);
  }

  if (courseResult.data.status === "Published") {
    await remove(
      "published course state",
      () =>
        instructor.modules.lifecycle.postCoursesUnpublish(resources.courseId),
      removed,
      skipped,
      failures,
    );
    await stopOnFailure(manifest, removed, skipped, failures);
  }
  await remove(
    `course ${resources.courseId}`,
    () => instructor.modules.courses.deleteCourses(resources.courseId),
    removed,
    skipped,
    failures,
  );
  await stopOnFailure(manifest, removed, skipped, failures);

  const report = makeReport(manifest, removed, skipped, failures);
  await writeReport(scenarioKey, report);
  await removeScenarioSecrets(scenarioKey);
  await writeReport(scenarioKey, report);
  return report;
}

export async function authenticateResetOwner(
  manifest,
  {
    authenticate = authenticatePersona,
    createProbeManifest = createManifest,
  } = {},
) {
  try {
    return {
      kind: "authenticated",
      session: await authenticate(manifest, "instructor", { create: false }),
    };
  } catch (authenticationError) {
    const previousTenantId = manifest.environment?.tenantId;
    if (!previousTenantId) throw authenticationError;

    const probe = createProbeManifest({
      definitionKey: manifest.definitionKey,
      quizKeys: manifest.quizKeys,
      apiBaseUrl: manifest.environment.apiBaseUrl,
      webBaseUrl: manifest.environment.webBaseUrl,
    });
    let current;
    try {
      current = await authenticate(probe, "instructor");
    } catch {
      throw authenticationError;
    }

    const currentTenantId = current.identity?.tenantId;
    if (!currentTenantId || currentTenantId === previousTenantId) {
      throw authenticationError;
    }
    return {
      kind: "recreated-environment",
      previousTenantId,
      currentTenantId,
    };
  }
}

function scenarioQuizzes(manifest) {
  const definition = getScenarioDefinition(manifest.definitionKey);
  if (manifest.resources.quizzes) {
    return (manifest.quizKeys ?? Object.keys(manifest.resources.quizzes)).map(
      (key) => ({
        key,
        quiz: definition.quizzes.find((candidate) => candidate.key === key),
        resources: manifest.resources.quizzes[key],
      }),
    );
  }
  return [
    {
      key: definition.quiz?.key ?? definition.key,
      quiz: definition.quiz,
      resources: manifest.resources,
    },
  ];
}

async function disableContentGrading(
  manifest,
  quiz,
  quizResources,
  instructor,
  removed,
  skipped,
  failures,
) {
  const courseId = manifest.resources.courseId;
  const { contentId, assessmentId } = quizResources;
  if (!courseId || !contentId || !assessmentId || !quiz) return;

  const [contentResult, assessmentResult] = await Promise.all([
    instructor.modules.content.getCoursesContentById(courseId, contentId),
    instructor.modules.assessments.getAssessments(assessmentId),
  ]);
  if (!assessmentResult.ok && isMissing(assessmentResult.error)) {
    skipped.push(`content-owned assessment ${assessmentId} already disabled`);
    return;
  }
  if (!contentResult.ok) {
    failures.push(
      isMissing(contentResult.error)
        ? `disable content grading: content ${contentId} is absent while assessment ${assessmentId} remains active`
        : `read content before disabling grading: ${formatError(contentResult.error)}`,
    );
    return;
  }
  if (!assessmentResult.ok) {
    failures.push(
      `read assessment before disabling grading: ${formatError(assessmentResult.error)}`,
    );
    return;
  }

  const content = contentResult.data;
  const assessment = assessmentResult.data;
  const document = structuredClone(quiz.document);
  delete document.grading;
  const result =
    await instructor.modules.assessments.putAssessmentsCourseContentDraft(
      courseId,
      contentId,
      {
        expectedContentVersion: content.version,
        expectedAssessmentVersion: assessment.version,
        title: content.title,
        slug: content.slug,
        description: content.description ?? null,
        document,
        visibility: content.visibility ?? "Public",
        isRequired: content.isRequired ?? true,
        estimatedMinutes: content.estimatedMinutes ?? null,
        estimatedMinutesSource: content.estimatedMinutesSource ?? "Auto",
      },
    );
  if (!result.ok) {
    failures.push(`disable content grading: ${formatError(result.error)}`);
    return;
  }
  const disabled = unwrap(result, "Disable content grading");
  if (disabled.assessmentId != null) {
    failures.push("disable content grading returned an active assessment");
    return;
  }
  removed.push(`content-owned assessment ${assessmentId}`);
}

async function stopOnFailure(manifest, removed, skipped, failures) {
  if (failures.length === 0) return;
  await writeReport(
    manifest.definitionKey,
    makeReport(manifest, removed, skipped, failures),
  );
  throw new Error(
    `Reset left the manifest in place because cleanup was incomplete: ${failures.join("; ")}`,
  );
}

async function remove(label, operation, removed, skipped, failures) {
  const result = await operation();
  if (result.ok) {
    removed.push(label);
    return;
  }
  if (isMissing(result.error)) {
    skipped.push(`${label} already absent`);
    return;
  }
  failures.push(`${label}: ${formatError(result.error)}`);
}

function makeReport(manifest, removed, skipped, failures) {
  return {
    kind: "reset",
    scenario: manifest.definitionKey,
    runId: manifest.runId,
    completedAt: new Date().toISOString(),
    removed,
    skipped,
    failures,
  };
}

function formatError(error) {
  return `HTTP ${error?.status ?? "?"}: ${error?.message ?? "unknown error"}`;
}
