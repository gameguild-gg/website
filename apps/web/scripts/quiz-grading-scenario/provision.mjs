import { randomUUID } from "node:crypto";
import {
  assertStackAvailable,
  authenticatePersona,
  isMissing,
  request,
  unwrap,
} from "./api.mjs";
import {
  assertApplicableCheckpoint,
  checkpointIndex,
  getScenarioDefinition,
  hasInstructorReview,
  requestHash,
} from "./definitions.mjs";
import { createManifest, readManifest, writeManifest } from "./manifest.mjs";
import { buildScenarioUrls } from "./urls.mjs";

export async function prepareScenario({
  scenarioKey,
  checkpoint,
  apiBaseUrl,
  webBaseUrl,
  onProgress = () => {},
}) {
  const definition = getScenarioDefinition(scenarioKey);
  assertApplicableCheckpoint(definition, checkpoint);
  let manifest = await readManifest(scenarioKey, { optional: true });
  if (!manifest) {
    manifest = createManifest({
      definitionKey: scenarioKey,
      apiBaseUrl,
      webBaseUrl,
    });
    await persist(manifest);
  } else {
    assertEnvironmentMatches(manifest, apiBaseUrl, webBaseUrl);
  }

  onProgress("Checking the local API and web application.");
  await assertStackAvailable(
    manifest.environment.apiBaseUrl,
    manifest.environment.webBaseUrl,
  );
  onProgress("Creating or authenticating the isolated personas.");
  const sessions = await ensurePersonas(manifest);
  await assertExistingCourseStillExists(manifest, sessions.instructor);

  if (checkpointIndex(manifest.checkpoint) >= checkpointIndex(checkpoint)) {
    onProgress(
      `Manifest is already at ${manifest.checkpoint}; no provisioning is needed.`,
    );
    return manifest;
  }

  onProgress("Preparing the course, quiz, assessment, and grading group.");
  await ensureAuthoring(manifest, definition, sessions);
  setCheckpoint(manifest, "authoring-ready");
  await persist(manifest);
  if (checkpoint === "authoring-ready") return manifest;

  onProgress("Preparing the immutable assessment candidate.");
  await ensureCandidate(manifest, sessions.instructor);
  setCheckpoint(manifest, "test-run-ready");
  await persist(manifest);
  if (checkpoint === "test-run-ready") return manifest;

  onProgress("Publishing the revision and enrolling the learner personas.");
  await ensureLearnerReady(manifest, definition, sessions);
  setCheckpoint(manifest, "learner-ready");
  await persist(manifest);
  if (checkpoint === "learner-ready") return manifest;

  onProgress(
    "Submitting the learner attempt and running deterministic grading.",
  );
  await ensureSubmitted(manifest, definition, sessions);
  if (hasInstructorReview(definition)) {
    setCheckpoint(manifest, "review-ready");
    await persist(manifest);
    if (checkpoint === "review-ready") return manifest;
    onProgress("Applying the instructor review.");
    await ensureInstructorReview(manifest, definition, sessions.instructor);
  }

  setCheckpoint(manifest, "release-ready");
  await persist(manifest);
  if (checkpoint === "release-ready") return manifest;

  onProgress("Releasing the result to the learner.");
  await ensureReleased(manifest, sessions.instructor);
  setCheckpoint(manifest, "released");
  await persist(manifest);
  return manifest;
}

async function ensurePersonas(manifest) {
  const sessions = {};
  sessions.instructor = await authenticatePersona(manifest, "instructor");
  await persist(manifest);
  for (const key of Object.keys(manifest.personas).filter(
    (key) => key !== "instructor",
  )) {
    sessions[key] = await authenticatePersona(manifest, key);
    await persist(manifest);
  }
  return sessions;
}

async function assertExistingCourseStillExists(manifest, instructor) {
  const id = manifest.resources.courseId;
  if (!id) return;
  const result =
    await instructor.modules.courses.getCoursesForGetCoursesById(id);
  if (!result.ok && isMissing(result.error)) {
    throw new Error(
      `Manifest references course ${id}, but it no longer exists. Run reset to discard this execution, then prepare it again.`,
    );
  }
  unwrap(result, "Read existing scenario course");
}

async function ensureAuthoring(manifest, definition, sessions) {
  const instructor = sessions.instructor;
  const resources = manifest.resources;
  if (!resources.courseId) {
    const existing = unwrap(
      await instructor.modules.courses.getCoursesForGetCourses({
        q: manifest.marker,
        take: 100,
      }),
      "Find scenario course",
    ).find((course) => course.slug === manifest.marker);
    const course =
      existing ??
      unwrap(
        await instructor.modules.courses.postCourses({
          title: `[${manifest.marker}] ${definition.title}`,
          description: `Owned by quiz grading scenario ${manifest.runId}.`,
          slug: manifest.marker,
        }),
        "Create scenario course",
      );
    requireId(course, "course");
    resources.courseId = course.id;
    resources.courseSlug = course.slug ?? manifest.marker;
    await persist(manifest);
  }

  if (!resources.gradingGroupId) {
    const groups = unwrap(
      await instructor.modules.assessments.getAssessmentsCourseGroups(
        resources.courseId,
      ),
      "List assessment groups",
    );
    const expectedName = `${manifest.marker} ${definition.gradingGroup.title}`;
    const group =
      groups.find((candidate) => candidate.name === expectedName) ??
      unwrap(
        await instructor.modules.assessments.postAssessmentsGroups({
          courseId: resources.courseId,
          name: expectedName,
          description: `Owned by ${manifest.marker}.`,
          order: 0,
          weightPercent: definition.gradingGroup.weightPercent,
        }),
        "Create assessment group",
      );
    requireId(group, "assessment group");
    resources.gradingGroupId = group.id;
    await persist(manifest);
  }

  if (definition.subject === "collective" && !resources.groupSetId) {
    const sets = unwrap(
      await instructor.modules.groupSets.getCoursesGroupSets(
        resources.courseId,
      ),
      "List group sets",
    );
    const setName = `${manifest.marker} teams`;
    const groupSet =
      sets.find((candidate) => candidate.name === setName) ??
      unwrap(
        await instructor.modules.groupSets.postCoursesGroupSets(
          resources.courseId,
          {
            name: setName,
          },
        ),
        "Create group set",
      );
    requireId(groupSet, "group set");
    resources.groupSetId = groupSet.id;
    await persist(manifest);
  }

  if (!resources.contentId) {
    const contents = unwrap(
      await instructor.modules.content.getCoursesContent(resources.courseId),
      "List course content",
    );
    const contentSlug = `${manifest.marker}-quiz`;
    const content =
      contents.find((candidate) => candidate.slug === contentSlug) ??
      unwrap(
        await instructor.modules.content.postCoursesContent(
          resources.courseId,
          {
            programId: resources.courseId,
            title: `[${manifest.marker}] ${definition.quiz.title}`,
            slug: contentSlug,
            description: definition.quiz.description,
            type: "Questionnaire",
            jsonBody: null,
            isRequired: true,
            estimatedMinutesSource: "Auto",
            visibility: "Public",
          },
        ),
        "Create quiz content",
      );
    requireId(content, "quiz content");
    resources.contentId = content.id;
    resources.contentVersion = content.version;
    await persist(manifest);
  }

  if (!resources.assessmentId) {
    const assessments = unwrap(
      await instructor.modules.assessments.getAssessmentsCourse(
        resources.courseId,
      ),
      "List course assessments",
    );
    const existing = assessments.find(
      (candidate) => candidate.contentId === resources.contentId,
    );
    if (existing?.id) {
      resources.assessmentId = existing.id;
      resources.assessmentVersion = existing.version;
    } else {
      const saved = unwrap(
        await instructor.modules.assessments.putAssessmentsCourseContentDraft(
          resources.courseId,
          resources.contentId,
          {
            expectedContentVersion: requireNumber(
              resources.contentVersion,
              "content version",
            ),
            expectedAssessmentVersion: null,
            title: `[${manifest.marker}] ${definition.quiz.title}`,
            slug: `${manifest.marker}-assessment`,
            description: definition.quiz.description,
            document: definition.quiz.document,
            visibility: "Public",
            isRequired: true,
            estimatedMinutes: null,
            estimatedMinutesSource: "Auto",
            reviewMethods: definition.reviewMethodValue,
            passingScore: definition.expected.maxScore,
            maxAttempts: 1,
            contentCompletionMode: "on-release-and-pass",
            resultReleaseMode: "manual",
            reviewConfigurationCanonicalJson: hasInstructorReview(definition)
              ? '{"schemaVersion":1,"instructor":{"requireOverrideReason":false}}'
              : null,
          },
        ),
        "Save atomic quiz assessment draft",
      );
      if (!saved.assessmentId) {
        throw new Error(
          "Saving the quiz draft did not return an assessmentId.",
        );
      }
      resources.assessmentId = saved.assessmentId;
      resources.assessmentVersion = saved.assessmentVersion;
      resources.contentVersion = saved.contentVersion;
    }
    await persist(manifest);
  }

  let assessment = unwrap(
    await instructor.modules.assessments.getAssessments(resources.assessmentId),
    "Read scenario assessment",
  );
  if (assessment.assessmentGroupId !== resources.gradingGroupId) {
    assessment = unwrap(
      await instructor.modules.assessments.putAssessmentsGroup(
        resources.assessmentId,
        {
          assessmentGroupId: resources.gradingGroupId,
          clearAssessmentGroup: false,
        },
      ),
      "Assign assessment group",
    );
    resources.assessmentVersion = assessment.version;
    await persist(manifest);
  }
  if (
    definition.subject === "collective" &&
    assessment.groupSetId !== resources.groupSetId
  ) {
    assessment = unwrap(
      await instructor.modules.assessments.putAssessments(
        resources.assessmentId,
        {
          expectedVersion: requireNumber(
            assessment.version,
            "assessment version",
          ),
          groupSetId: resources.groupSetId,
        },
      ),
      "Assign collective group set",
    );
    resources.assessmentVersion = assessment.version;
    await persist(manifest);
  }
  resources.assessmentVersion = assessment.version;
}

async function ensureCandidate(manifest, instructor) {
  const resources = manifest.resources;
  const state = await readAuthoringState(instructor, resources.assessmentId);
  if (state.candidate?.revisionId && state.candidateMatchesDraft) {
    resources.candidateRevisionId = state.candidate.revisionId;
    resources.assessmentVersion = state.assessmentVersion;
    await persist(manifest);
    return;
  }
  if (state.prepare?.available === false) {
    throw new Error(
      `Assessment candidate cannot be prepared: ${state.prepare.code ?? "unavailable"}: ${state.prepare.message ?? "no diagnostic"}.`,
    );
  }
  const prepared = await request(
    instructor.client,
    "Prepare assessment candidate",
    {
      method: "POST",
      path: `/v1/assessments/${resources.assessmentId}/revisions/prepare`,
      body: {
        expectedAssessmentVersion: requireNumber(
          state.assessmentVersion,
          "assessment version",
        ),
      },
    },
  );
  if (!prepared.revisionId)
    throw new Error("Prepared assessment returned no revisionId.");
  resources.candidateRevisionId = prepared.revisionId;
  resources.assessmentVersion = state.assessmentVersion;
  await persist(manifest);
}

async function ensureLearnerReady(manifest, definition, sessions) {
  const resources = manifest.resources;
  const instructor = sessions.instructor;
  await ensurePublishedQuizContent(manifest, definition, instructor);
  const state = await readAuthoringState(instructor, resources.assessmentId);
  if (!state.published?.revisionId || !state.publishedMatchesDraft) {
    throw new Error(
      "Publishing the quiz content did not produce an assessment revision matching the current draft.",
    );
  }
  resources.publishedRevisionId = state.published.revisionId;
  resources.assessmentVersion = state.assessmentVersion;
  await persist(manifest);

  for (const key of definition.subject === "collective"
    ? ["learnerA", "learnerB"]
    : ["learnerA"]) {
    await ensureEnrollment(manifest, sessions, key);
  }

  if (definition.subject === "collective") {
    await ensureCollectiveGroup(manifest, sessions);
  }

  const course = unwrap(
    await instructor.modules.courses.getCoursesForGetCoursesById(
      resources.courseId,
    ),
    "Read course lifecycle",
  );
  if (course.status !== "Published") {
    const published = await instructor.modules.lifecycle.postCoursesPublish(
      resources.courseId,
    );
    if (!published.ok) {
      throw new Error(
        `Publish course: HTTP ${published.error?.status ?? "?"}: ${published.error?.message ?? "unknown error"}. The scenario runner does not bypass the course lifecycle.`,
      );
    }
  }
  await persist(manifest);
}

async function ensurePublishedQuizContent(manifest, definition, instructor) {
  const resources = manifest.resources;
  const authoringPath = `/v1/courses/${resources.courseId}/content/${resources.contentId}/authoring`;
  const draft = await request(
    instructor.client,
    "Read quiz content authoring draft",
    {
      method: "GET",
      path: authoringPath,
    },
  );
  if (!draft.payload || !Number.isInteger(draft.revision)) {
    throw new Error("Quiz content authoring returned an incomplete draft.");
  }
  const expectedPayload = {
    ...draft.payload,
    title: `[${manifest.marker}] ${definition.quiz.title}`,
    slug: `${manifest.marker}-quiz`,
    description: definition.quiz.description,
    type: "Questionnaire",
    body: null,
    jsonBody: definition.quiz.document,
    lessonFormat: null,
    isRequired: true,
    estimatedMinutes: null,
    estimatedMinutesSource: "Auto",
    visibility: "Public",
  };
  let currentDraft = draft;
  if (!sameJson(draft.payload, expectedPayload)) {
    currentDraft = await request(
      instructor.client,
      "Save quiz content authoring draft",
      {
        method: "PUT",
        path: authoringPath,
        body: {
          revision: requireNumber(
            draft.revision,
            "content authoring draft revision",
          ),
          payload: expectedPayload,
        },
      },
    );
    if (!Number.isInteger(currentDraft.revision)) {
      throw new Error(
        "Saving quiz content authoring returned no draft revision.",
      );
    }
  }

  const published = await request(instructor.client, "Publish quiz content", {
    method: "POST",
    path: `${authoringPath}/publish`,
    body: {
      revision: requireNumber(
        currentDraft.revision,
        "content authoring draft revision",
      ),
    },
  });
  if (
    !published.publishedContent?.id ||
    !Number.isInteger(published.publishedContent.version) ||
    !Number.isInteger(published.draft?.revision)
  ) {
    throw new Error(
      "Publishing quiz content returned an incomplete authoring result.",
    );
  }
  resources.contentVersion = published.publishedContent.version;
  resources.contentDraftRevision = published.draft.revision;
}

async function ensureEnrollment(manifest, sessions, personaKey) {
  const resources = manifest.resources;
  const persona = manifest.personas[personaKey];
  const instructor = sessions.instructor;
  const enrolled = unwrap(
    await instructor.modules.courses.getCoursesUsers(resources.courseId, {
      take: 500,
    }),
    "List course enrollments",
  ).find((entry) => entry.userId === persona.userId);
  const record =
    enrolled ??
    unwrap(
      await instructor.modules.courses.postCoursesUsers(
        resources.courseId,
        persona.userId,
      ),
      `Enroll ${personaKey}`,
    );
  if (!record.enrollmentId) {
    throw new Error(`Enrollment for ${personaKey} returned no enrollmentId.`);
  }
  const canonical = unwrap(
    await instructor.modules.enrollments.postApiLearningEnrollments({
      courseId: resources.courseId,
      userId: persona.userId,
      cohortId: null,
    }),
    `Create canonical enrollment for ${personaKey}`,
  );
  if (!canonical.id || canonical.courseId !== resources.courseId) {
    throw new Error(
      `Enrollment for ${personaKey} did not produce an active canonical Learning enrollment.`,
    );
  }
  persona.enrollmentId = canonical.id;
  if (!resources.enrollmentIds.includes(canonical.id)) {
    resources.enrollmentIds.push(canonical.id);
  }
  await persist(manifest);
}

async function ensureCollectiveGroup(manifest, sessions) {
  const resources = manifest.resources;
  const instructor = sessions.instructor;
  const groups = unwrap(
    await instructor.modules.groupSets.getCoursesGroupSetsGroups(
      resources.courseId,
      resources.groupSetId,
    ),
    "List scenario course groups",
  );
  const groupName = `${manifest.marker} team`;
  const courseGroup =
    groups.find((candidate) => candidate.name === groupName) ??
    unwrap(
      await instructor.modules.groupSets.postCoursesGroupSetsGroups(
        resources.courseId,
        resources.groupSetId,
        { name: groupName, capacity: 2 },
      ),
      "Create scenario course group",
    );
  requireId(courseGroup, "course group");
  resources.courseGroupId = courseGroup.id;
  await persist(manifest);

  const members = new Set(
    (courseGroup.members ?? []).map((member) => member.userId),
  );
  for (const key of ["learnerA", "learnerB"]) {
    const userId = manifest.personas[key].userId;
    if (!members.has(userId)) {
      unwrap(
        await instructor.modules.groupSets.postCoursesGroupSetsGroupsMembers(
          resources.courseId,
          resources.courseGroupId,
          userId,
        ),
        `Add ${key} to collective group`,
      );
    }
  }
  await persist(manifest);
}

async function ensureSubmitted(manifest, definition, sessions) {
  const resources = manifest.resources;
  const learner = sessions.learnerA;
  let submission;
  if (resources.submissionId) {
    submission = await readRuntimeSubmission(learner, resources.submissionId);
  } else {
    const intent =
      definition.subject === "collective"
        ? { courseGroupId: resources.courseGroupId }
        : { contentId: resources.contentId };
    const key = await commandKey(manifest, "start-official-submission", intent);
    submission = await request(
      learner.client,
      definition.subject === "collective"
        ? "Start collective runtime submission"
        : "Start individual runtime submission",
      {
        method: "POST",
        path:
          definition.subject === "collective"
            ? `/v1/assessments/${resources.assessmentId}/runtime-submissions/collective`
            : `/v1/assessments/content/${resources.contentId}/runtime-submissions/individual`,
        body:
          definition.subject === "collective"
            ? { courseGroupId: resources.courseGroupId, idempotencyKey: key }
            : { idempotencyKey: key },
      },
    );
    if (!submission.submissionId)
      throw new Error("Runtime start returned no submissionId.");
    resources.submissionId = submission.submissionId;
    captureExecution(resources, submission);
    await persist(manifest);
  }

  if (
    definition.subject === "collective" &&
    submission.status === "InProgress" &&
    submission.execution?.submittedResponse == null
  ) {
    const draftIntent = {
      response: definition.quiz.correctAnswer,
      expectedVersion: submission.draftVersion,
    };
    const key = await commandKey(
      manifest,
      "save-collective-response-draft",
      draftIntent,
    );
    submission = await request(
      learner.client,
      "Save collective quiz response draft",
      {
        method: "PUT",
        path: `/v1/assessments/runtime-submissions/${resources.submissionId}/draft`,
        body: { ...draftIntent, idempotencyKey: key },
      },
    );
  }

  if (submission.status === "InProgress") {
    const submitIntent = {
      response: definition.quiz.correctAnswer,
      expectedDraftVersion:
        definition.subject === "collective" ? submission.draftVersion : null,
    };
    const key = await commandKey(
      manifest,
      "submit-official-response",
      submitIntent,
    );
    submission = await request(
      learner.client,
      "Submit official quiz response",
      {
        method: "POST",
        path: `/v1/assessments/runtime-submissions/${resources.submissionId}/submit`,
        body: { ...submitIntent, idempotencyKey: key },
      },
    );
  }
  if (definition.subject === "individual" && submission.enrollmentId) {
    manifest.personas.learnerA.enrollmentId = submission.enrollmentId;
    if (!resources.enrollmentIds.includes(submission.enrollmentId)) {
      resources.enrollmentIds.push(submission.enrollmentId);
    }
  }
  captureExecution(resources, submission);
  await persist(manifest);
}

async function ensureInstructorReview(manifest, definition, instructor) {
  const resources = manifest.resources;
  let submission = await readRuntimeSubmission(
    instructor,
    resources.submissionId,
  );
  if (submission.execution?.requiresInstructorReview) {
    const resolution = {
      schemaVersion: 1,
      items: [
        {
          itemId: "q1",
          score: definition.expected.instructorScore,
          feedback: "Reviewed by the scenario instructor.",
        },
      ],
      feedback: "Scenario instructor review completed.",
      overrideReason: null,
    };
    const key = await commandKey(
      manifest,
      "resolve-instructor-review",
      resolution,
    );
    submission = await request(instructor.client, "Resolve instructor review", {
      method: "POST",
      path: `/v1/assessments/runtime-submissions/${resources.submissionId}/instructor-review`,
      body: { resolution, idempotencyKey: key },
    });
  }
  if (submission.execution?.requiresInstructorReview) {
    throw new Error(
      "Instructor review completed but the execution still requires review.",
    );
  }
  captureExecution(resources, submission);
  await persist(manifest);
}

async function ensureReleased(manifest, instructor) {
  const resources = manifest.resources;
  const submission = await readRuntimeSubmission(
    instructor,
    resources.submissionId,
  );
  if (submission.execution?.released) {
    captureExecution(resources, submission);
    await persist(manifest);
    return;
  }
  const roundId = submission.execution?.activeRoundId;
  if (!roundId)
    throw new Error("Finalized submission has no active round to release.");
  const intent = {
    expectedRoundId: roundId,
    expectedSubmissionVersion: submission.version,
    reason: "Released by the quiz grading scenario runner.",
  };
  const key = await commandKey(manifest, "release-result", intent);
  const released = await request(instructor.client, "Release grade result", {
    method: "POST",
    path: `/v1/assessments/runtime-submissions/${resources.submissionId}/release`,
    body: { ...intent, idempotencyKey: key },
  });
  resources.gradeResultId = released.gradeRoundId ?? roundId;
  await persist(manifest);
}

async function commandKey(manifest, name, payload) {
  const hash = requestHash(payload);
  const existing = manifest.commands[name];
  if (existing) {
    if (existing.requestHash !== hash) {
      throw new Error(
        `Command "${name}" was already recorded with a different request hash. Reset the scenario instead of replaying a changed command.`,
      );
    }
    return existing.idempotencyKey;
  }
  const idempotencyKey = `qgs-${name}-${randomUUID()}`;
  manifest.commands[name] = { idempotencyKey, requestHash: hash };
  await persist(manifest);
  return idempotencyKey;
}

function captureExecution(resources, submission) {
  resources.submissionId = submission.submissionId ?? resources.submissionId;
  resources.gradingExecutionId =
    submission.execution?.executionId ?? resources.gradingExecutionId;
  resources.gradeResultId =
    submission.execution?.activeRoundId ?? resources.gradeResultId;
}

function readAuthoringState(session, assessmentId) {
  return request(session.client, "Read assessment authoring state", {
    method: "GET",
    path: `/v1/assessments/${assessmentId}/authoring-state`,
  });
}

function readRuntimeSubmission(session, submissionId) {
  return request(session.client, "Read runtime submission", {
    method: "GET",
    path: `/v1/assessments/runtime-submissions/${submissionId}`,
  });
}

function setCheckpoint(manifest, next) {
  if (checkpointIndex(next) > checkpointIndex(manifest.checkpoint)) {
    manifest.checkpoint = next;
  }
}

async function persist(manifest) {
  manifest.urls = buildScenarioUrls(manifest);
  await writeManifest(manifest);
}

function assertEnvironmentMatches(manifest, apiBaseUrl, webBaseUrl) {
  const normalize = (value) => value.replace(/\/$/, "");
  if (normalize(apiBaseUrl) !== manifest.environment.apiBaseUrl) {
    throw new Error(
      `Existing manifest uses API ${manifest.environment.apiBaseUrl}, not ${apiBaseUrl}. Reset it before changing environments.`,
    );
  }
  if (normalize(webBaseUrl) !== manifest.environment.webBaseUrl) {
    throw new Error(
      `Existing manifest uses web ${manifest.environment.webBaseUrl}, not ${webBaseUrl}. Reset it before changing environments.`,
    );
  }
}

function requireId(value, label) {
  if (!value?.id) throw new Error(`Create ${label} returned no id.`);
  return value.id;
}

function requireNumber(value, label) {
  if (!Number.isInteger(value)) throw new Error(`Missing ${label}.`);
  return value;
}

function sameJson(left, right) {
  return JSON.stringify(left) === JSON.stringify(right);
}
