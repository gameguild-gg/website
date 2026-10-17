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
  hasPeerReview,
  hasSelfReview,
  requestHash,
  selectQuizDefinitions,
} from "./definitions.mjs";
import {
  createManifest,
  readManifest,
  recomputeManifestCheckpoint,
  selectedManifestQuizzes,
  writeManifest,
} from "./manifest.mjs";
import { buildScenarioUrls } from "./urls.mjs";

export async function prepareScenario({
  scenarioKey,
  checkpoint,
  quizKeys,
  apiBaseUrl,
  webBaseUrl,
  onProgress = () => {},
}) {
  const definition = getScenarioDefinition(scenarioKey);
  const requestedQuizzes = selectQuizDefinitions(definition, quizKeys);
  assertApplicableCheckpoint(definition, checkpoint, requestedQuizzes);
  let manifest = await readManifest(scenarioKey, { optional: true });
  if (!manifest) {
    manifest = createManifest({
      definitionKey: scenarioKey,
      quizKeys: requestedQuizzes.map((quiz) => quiz.key),
      apiBaseUrl,
      webBaseUrl,
    });
    await persist(manifest);
  } else {
    assertEnvironmentMatches(manifest, apiBaseUrl, webBaseUrl);
    assertSelectionMatches(manifest, quizKeys);
  }
  const quizzes = selectedManifestQuizzes(manifest);
  assertApplicableCheckpoint(definition, checkpoint, quizzes);

  onProgress("Checking the local API and web application.");
  await assertStackAvailable(
    manifest.environment.apiBaseUrl,
    manifest.environment.webBaseUrl,
  );
  onProgress("Creating or authenticating the isolated personas.");
  let sessions;
  try {
    sessions = await ensurePersonas(manifest);
  } catch (error) {
    const recovered = await recoverRecreatedLocalEnvironment(
      manifest,
      scenarioKey,
      apiBaseUrl,
      webBaseUrl,
      onProgress,
    );
    if (!recovered) throw error;
    manifest = recovered.manifest;
    sessions = recovered.sessions;
  }
  await assertExistingCourseStillExists(manifest, sessions.instructor);

  if (
    quizzes.every(
      (quiz) =>
        checkpointIndex(manifest.resources.quizzes[quiz.key].checkpoint) >=
        checkpointIndex(checkpoint),
    )
  ) {
    onProgress(
      `Every selected quiz is already at or beyond ${checkpoint}; no provisioning is needed.`,
    );
    return manifest;
  }

  onProgress("Preparing the course, quizzes, assessments, and grading group.");
  await ensureSharedAuthoring(manifest, definition, quizzes, sessions);
  for (const quiz of quizzes) {
    await ensureQuizAuthoring(manifest, definition, quiz, sessions);
    setQuizCheckpoint(manifest, quiz.key, "authoring-ready");
  }
  await persist(manifest);
  if (checkpoint === "authoring-ready") return manifest;

  onProgress("Preparing immutable assessment candidates.");
  for (const quiz of quizzes) {
    await ensureCandidate(
      manifest,
      manifest.resources.quizzes[quiz.key],
      sessions.instructor,
    );
    setQuizCheckpoint(manifest, quiz.key, "test-run-ready");
  }
  await persist(manifest);
  if (checkpoint === "test-run-ready") return manifest;

  onProgress("Publishing the revisions and enrolling learner personas.");
  await ensureLearnerReady(manifest, quizzes, sessions);
  for (const quiz of quizzes) {
    setQuizCheckpoint(manifest, quiz.key, "learner-ready");
  }
  await persist(manifest);
  if (checkpoint === "learner-ready") return manifest;

  onProgress("Submitting learner attempts and completing primary reviews.");
  for (const quiz of quizzes) {
    await ensureSubmitted(manifest, quiz, sessions);
    setQuizCheckpoint(
      manifest,
      quiz.key,
      hasInstructorReview(quiz) ? "review-ready" : "release-ready",
    );
  }
  await persist(manifest);
  if (checkpoint === "review-ready") return manifest;

  for (const quiz of quizzes.filter(hasInstructorReview)) {
    onProgress(`Applying instructor review for "${quiz.title}".`);
    await ensureInstructorReview(manifest, quiz, sessions.instructor);
    setQuizCheckpoint(manifest, quiz.key, "release-ready");
  }
  await persist(manifest);
  if (checkpoint === "release-ready") return manifest;

  onProgress("Releasing every finalized result to the learners.");
  for (const quiz of quizzes) {
    await ensureReleased(manifest, quiz, sessions.instructor);
    setQuizCheckpoint(manifest, quiz.key, "released");
  }
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

async function recoverRecreatedLocalEnvironment(
  manifest,
  scenarioKey,
  apiBaseUrl,
  webBaseUrl,
  onProgress,
) {
  const previousTenantId = manifest.environment.tenantId;
  if (!previousTenantId) return null;

  const probe = createManifest({
    definitionKey: scenarioKey,
    quizKeys: manifest.quizKeys,
    apiBaseUrl,
    webBaseUrl,
  });
  let currentInstructor;
  try {
    currentInstructor = await authenticatePersona(probe, "instructor");
  } catch {
    return null;
  }
  const currentTenantId = currentInstructor.identity.tenantId;
  if (!currentTenantId || currentTenantId === previousTenantId) return null;

  onProgress(
    "The local database uses a new tenant; replacing the stale scenario manifest.",
  );
  const replacement = createManifest({
    definitionKey: scenarioKey,
    quizKeys: manifest.quizKeys,
    apiBaseUrl,
    webBaseUrl,
  });
  await persist(replacement);
  return {
    manifest: replacement,
    sessions: await ensurePersonas(replacement),
  };
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

async function ensureSharedAuthoring(manifest, definition, quizzes, sessions) {
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

  if (
    quizzes.some((quiz) => quiz.subject === "collective") &&
    !resources.groupSetId
  ) {
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
          { name: setName },
        ),
        "Create group set",
      );
    requireId(groupSet, "group set");
    resources.groupSetId = groupSet.id;
    await persist(manifest);
  }
}

async function ensureQuizAuthoring(manifest, definition, quiz, sessions) {
  const instructor = sessions.instructor;
  const shared = manifest.resources;
  const resources = shared.quizzes[quiz.key];
  const slug = `${manifest.marker}-${quiz.key}`;

  if (!resources.contentId) {
    const contents = unwrap(
      await instructor.modules.content.getCoursesContent(shared.courseId),
      "List course content",
    );
    const content =
      contents.find((candidate) => candidate.slug === slug) ??
      unwrap(
        await instructor.modules.content.postCoursesContent(shared.courseId, {
          programId: shared.courseId,
          title: `[${manifest.marker}] ${quiz.title}`,
          slug,
          description: quiz.description,
          type: "Questionnaire",
          jsonBody: null,
          isRequired: true,
          estimatedMinutesSource: "Auto",
          visibility: "Public",
        }),
        `Create quiz content ${quiz.key}`,
      );
    requireId(content, "quiz content");
    resources.contentId = content.id;
    resources.contentVersion = content.version;
    await persist(manifest);
  }

  if (!resources.assessmentId) {
    const assessments = unwrap(
      await instructor.modules.assessments.getAssessmentsCourse(
        shared.courseId,
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
          shared.courseId,
          resources.contentId,
          {
            expectedContentVersion: requireNumber(
              resources.contentVersion,
              "content version",
            ),
            expectedAssessmentVersion: null,
            title: `[${manifest.marker}] ${quiz.title}`,
            slug,
            description: quiz.description,
            document: quiz.document,
            visibility: "Public",
            isRequired: true,
            estimatedMinutes: null,
            estimatedMinutesSource: "Auto",
            reviewMethods: quiz.reviewMethodValue,
            passingScore: quiz.expected.maxScore,
            maxAttempts: 1,
            contentCompletionMode: "on-release-and-pass",
            resultReleaseMode: quiz.release,
            reviewConfigurationCanonicalJson: buildReviewConfiguration(quiz),
          },
        ),
        `Save atomic quiz assessment draft ${quiz.key}`,
      );
      if (!saved.assessmentId) {
        throw new Error(
          `Saving quiz draft "${quiz.key}" did not return an assessmentId.`,
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
    `Read scenario assessment ${quiz.key}`,
  );
  if (assessment.assessmentGroupId !== shared.gradingGroupId) {
    assessment = unwrap(
      await instructor.modules.assessments.putAssessmentsGroup(
        resources.assessmentId,
        {
          assessmentGroupId: shared.gradingGroupId,
          clearAssessmentGroup: false,
        },
      ),
      `Assign assessment group ${quiz.key}`,
    );
    resources.assessmentVersion = assessment.version;
    await persist(manifest);
  }
  if (
    quiz.subject === "collective" &&
    assessment.groupSetId !== shared.groupSetId
  ) {
    assessment = unwrap(
      await instructor.modules.assessments.putAssessments(
        resources.assessmentId,
        {
          expectedVersion: requireNumber(
            assessment.version,
            "assessment version",
          ),
          groupSetId: shared.groupSetId,
        },
      ),
      "Assign collective group set",
    );
    resources.assessmentVersion = assessment.version;
    await persist(manifest);
  }
  resources.assessmentVersion = assessment.version;
}

function buildReviewConfiguration(quiz) {
  if (
    !hasPeerReview(quiz) &&
    !hasSelfReview(quiz) &&
    !hasInstructorReview(quiz)
  ) {
    return null;
  }
  return JSON.stringify({
    schemaVersion: 1,
    peer: hasPeerReview(quiz)
      ? {
          reviewsPerReviewer: 1,
          reviewsRequiredPerSubmission: 1,
          minimumReviewsToFinalize: 1,
          aggregation: "mean",
          claimLeaseMinutes: 30,
          evidenceWindowMinutes: 10080,
          onInsufficientEvidence: "await-instructor-resolution",
        }
      : null,
    ai: null,
    self: hasSelfReview(quiz)
      ? {
          instructions:
            "Score both deterministic answers and explain the assessment.",
          requireFeedback: true,
        }
      : null,
    instructor: hasInstructorReview(quiz)
      ? { requireOverrideReason: false }
      : null,
  });
}

async function ensureCandidate(manifest, resources, instructor) {
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
  if (!prepared.revisionId) {
    throw new Error("Prepared assessment returned no revisionId.");
  }
  resources.candidateRevisionId = prepared.revisionId;
  resources.assessmentVersion = state.assessmentVersion;
  await persist(manifest);
}

async function ensureLearnerReady(manifest, quizzes, sessions) {
  const instructor = sessions.instructor;
  for (const quiz of quizzes) {
    const resources = manifest.resources.quizzes[quiz.key];
    await ensurePublishedQuizContent(manifest, quiz, resources, instructor);
    const state = await readAuthoringState(instructor, resources.assessmentId);
    if (!state.published?.revisionId || !state.publishedMatchesDraft) {
      throw new Error(
        `Publishing quiz "${quiz.key}" did not produce an assessment revision matching the current draft.`,
      );
    }
    resources.publishedRevisionId = state.published.revisionId;
    resources.assessmentVersion = state.assessmentVersion;
    await persist(manifest);
  }

  await ensureEnrollment(manifest, sessions, "learnerA");
  if (manifest.personas.learnerB) {
    await ensureEnrollment(manifest, sessions, "learnerB");
  }
  if (quizzes.some((quiz) => quiz.subject === "collective")) {
    await ensureCollectiveGroup(manifest, sessions);
  }

  const course = unwrap(
    await instructor.modules.courses.getCoursesForGetCoursesById(
      manifest.resources.courseId,
    ),
    "Read course lifecycle",
  );
  if (course.status !== "Published") {
    const published = await instructor.modules.lifecycle.postCoursesPublish(
      manifest.resources.courseId,
    );
    if (!published.ok) {
      throw new Error(
        `Publish course: HTTP ${published.error?.status ?? "?"}: ${published.error?.message ?? "unknown error"}. The scenario runner does not bypass the course lifecycle.`,
      );
    }
  }
  await persist(manifest);
}

async function ensurePublishedQuizContent(
  manifest,
  quiz,
  resources,
  instructor,
) {
  const courseId = manifest.resources.courseId;
  const authoringPath = `/v1/courses/${courseId}/content/${resources.contentId}/authoring`;
  const draft = await request(
    instructor.client,
    "Read quiz content authoring draft",
    { method: "GET", path: authoringPath },
  );
  if (!draft.payload || !Number.isInteger(draft.revision)) {
    throw new Error("Quiz content authoring returned an incomplete draft.");
  }
  const expectedPayload = {
    ...draft.payload,
    title: `[${manifest.marker}] ${quiz.title}`,
    slug: `${manifest.marker}-${quiz.key}`,
    description: quiz.description,
    type: "Questionnaire",
    body: null,
    jsonBody: quiz.document,
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
  }
  if (!Number.isInteger(currentDraft.revision)) {
    throw new Error(
      "Saving quiz content authoring returned no draft revision.",
    );
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

async function ensureSubmitted(manifest, quiz, sessions) {
  const resources = manifest.resources.quizzes[quiz.key];
  const personaKeys = hasPeerReview(quiz)
    ? ["learnerA", "learnerB"]
    : ["learnerA"];
  for (const personaKey of personaKeys) {
    await ensurePersonaSubmission(
      manifest,
      quiz,
      resources,
      sessions,
      personaKey,
    );
  }
  if (hasPeerReview(quiz)) {
    await ensurePeerReviews(manifest, quiz, resources, sessions);
  }
  for (const personaKey of personaKeys) {
    const submission = await readRuntimeSubmission(
      sessions[personaKey],
      resources.submissions[personaKey].submissionId,
    );
    captureExecution(resources.submissions[personaKey], submission);
  }
  await persist(manifest);
}

async function ensurePersonaSubmission(
  manifest,
  quiz,
  resources,
  sessions,
  personaKey,
) {
  const learner = sessions[personaKey];
  const submissionResources = resources.submissions[personaKey];
  let submission;
  if (submissionResources.submissionId) {
    submission = await readRuntimeSubmission(
      learner,
      submissionResources.submissionId,
    );
  } else {
    const collective = quiz.subject === "collective";
    const intent = collective
      ? { courseGroupId: manifest.resources.courseGroupId }
      : { contentId: resources.contentId, personaKey };
    const key = await commandKey(
      manifest,
      `${quiz.key}:${personaKey}:start-official-submission`,
      intent,
    );
    submission = await request(
      learner.client,
      collective
        ? "Start collective runtime submission"
        : "Start individual runtime submission",
      {
        method: "POST",
        path: collective
          ? `/v1/assessments/${resources.assessmentId}/runtime-submissions/collective`
          : `/v1/assessments/content/${resources.contentId}/runtime-submissions/individual`,
        body: collective
          ? {
              courseGroupId: manifest.resources.courseGroupId,
              idempotencyKey: key,
            }
          : { idempotencyKey: key },
      },
    );
    if (!submission.submissionId) {
      throw new Error("Runtime start returned no submissionId.");
    }
    submissionResources.submissionId = submission.submissionId;
    captureExecution(submissionResources, submission);
    await persist(manifest);
  }

  if (
    quiz.subject === "collective" &&
    normalizeStatus(submission.status) === "inprogress" &&
    submission.execution?.submittedResponse == null
  ) {
    const draftIntent = {
      response: quiz.correctAnswer,
      expectedVersion: submission.draftVersion,
    };
    const key = await commandKey(
      manifest,
      `${quiz.key}:${personaKey}:save-collective-response-draft`,
      draftIntent,
    );
    submission = await request(
      learner.client,
      "Save collective quiz response draft",
      {
        method: "PUT",
        path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}/draft`,
        body: { ...draftIntent, idempotencyKey: key },
      },
    );
  }

  if (normalizeStatus(submission.status) === "inprogress") {
    const submitIntent = {
      response: quiz.correctAnswer,
      expectedDraftVersion:
        quiz.subject === "collective" ? submission.draftVersion : null,
    };
    const key = await commandKey(
      manifest,
      `${quiz.key}:${personaKey}:submit-official-response`,
      submitIntent,
    );
    submission = await request(
      learner.client,
      "Submit official quiz response",
      {
        method: "POST",
        path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}/submit`,
        body: { ...submitIntent, idempotencyKey: key },
      },
    );
  }
  if (quiz.subject === "individual" && submission.enrollmentId) {
    manifest.personas[personaKey].enrollmentId = submission.enrollmentId;
    if (!manifest.resources.enrollmentIds.includes(submission.enrollmentId)) {
      manifest.resources.enrollmentIds.push(submission.enrollmentId);
    }
  }

  if (
    hasSelfReview(quiz) &&
    submission.execution?.selfReview &&
    normalizeStatus(submission.execution.selfReview.status) !== "completed"
  ) {
    const evidence = buildItemReviewEvidence(
      quiz.expected.selfReviewScore,
      "Self review completed by the scenario learner.",
    );
    const key = await commandKey(
      manifest,
      `${quiz.key}:${personaKey}:submit-self-review`,
      evidence,
    );
    submission = await request(learner.client, "Submit official self review", {
      method: "POST",
      path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}/self-review`,
      body: {
        evidence,
        idempotencyKey: key,
        expectedDraftVersion: null,
      },
    });
  }
  captureExecution(submissionResources, submission);
  await persist(manifest);
}

async function ensurePeerReviews(manifest, quiz, resources, sessions) {
  for (const reviewerKey of ["learnerA", "learnerB"]) {
    const reviewer = sessions[reviewerKey];
    let reviewId = resources.peerReviewIds[reviewerKey];
    let status = null;
    if (reviewId) {
      const existing = await request(
        reviewer.client,
        `Read peer review for ${reviewerKey}`,
        {
          method: "GET",
          path: `/v1/assessments/peer-reviews/${reviewId}`,
        },
      );
      status = normalizeStatus(existing.status);
    }
    if (!reviewId) {
      const claimed = await request(
        reviewer.client,
        `Claim peer review for ${reviewerKey}`,
        {
          method: "POST",
          path: `/v1/assessments/${resources.assessmentId}/peer-reviews/claim`,
        },
      );
      if (!claimed.reviewId) {
        throw new Error(`Peer review claim for ${reviewerKey} returned no ID.`);
      }
      reviewId = claimed.reviewId;
      resources.peerReviewIds[reviewerKey] = reviewId;
      await persist(manifest);
    }
    if (status !== "submitted") {
      await request(reviewer.client, `Submit peer review for ${reviewerKey}`, {
        method: "POST",
        path: `/v1/assessments/peer-reviews/${reviewId}/submit`,
        body: {
          score: quiz.expected.peerReviewScore,
          feedback: `Peer feedback from ${reviewerKey}.`,
          rubricScores: null,
        },
      });
    }
  }
}

async function ensureInstructorReview(manifest, quiz, instructor) {
  const resources = manifest.resources.quizzes[quiz.key];
  for (const [personaKey, submissionResources] of Object.entries(
    resources.submissions,
  )) {
    let submission = await readRuntimeSubmission(
      instructor,
      submissionResources.submissionId,
    );
    if (submission.execution?.requiresInstructorReview) {
      const resolution = {
        ...buildItemReviewEvidence(
          quiz.expected.instructorScore,
          "Reviewed by the scenario instructor.",
        ),
        overrideReason: null,
      };
      const key = await commandKey(
        manifest,
        `${quiz.key}:${personaKey}:resolve-instructor-review`,
        resolution,
      );
      submission = await request(
        instructor.client,
        "Resolve instructor review",
        {
          method: "POST",
          path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}/instructor-review`,
          body: { resolution, idempotencyKey: key },
        },
      );
    }
    if (submission.execution?.requiresInstructorReview) {
      throw new Error(
        `Instructor review for "${quiz.key}" and ${personaKey} completed but the execution still requires review.`,
      );
    }
    captureExecution(submissionResources, submission);
  }
  await persist(manifest);
}

async function ensureReleased(manifest, quiz, instructor) {
  const resources = manifest.resources.quizzes[quiz.key];
  for (const [personaKey, submissionResources] of Object.entries(
    resources.submissions,
  )) {
    let submission = await readRuntimeSubmission(
      instructor,
      submissionResources.submissionId,
    );
    if (!submission.execution?.released) {
      const roundId = submission.execution?.activeRoundId;
      if (!roundId) {
        throw new Error(
          `Finalized submission for "${quiz.key}" and ${personaKey} has no active round to release.`,
        );
      }
      const intent = {
        expectedRoundId: roundId,
        expectedSubmissionVersion: submission.version,
        reason: "Released by the quiz grading scenario runner.",
      };
      const key = await commandKey(
        manifest,
        `${quiz.key}:${personaKey}:release-result`,
        intent,
      );
      await request(instructor.client, "Release grade result", {
        method: "POST",
        path: `/v1/assessments/runtime-submissions/${submissionResources.submissionId}/release`,
        body: { ...intent, idempotencyKey: key },
      });
      submission = await readRuntimeSubmission(
        instructor,
        submissionResources.submissionId,
      );
    }
    captureExecution(submissionResources, submission);
  }
  await persist(manifest);
}

function buildItemReviewEvidence(totalScore, feedback) {
  const q1Score = Math.min(100, totalScore);
  const q2Score = Math.max(0, totalScore - q1Score);
  return {
    schemaVersion: 1,
    items: [
      { itemId: "q1", score: q1Score, feedback },
      { itemId: "q2", score: q2Score, feedback },
    ],
    feedback,
  };
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
  const idempotencyKey = `qgs-${name.replaceAll(":", "-")}-${randomUUID()}`;
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

function setQuizCheckpoint(manifest, quizKey, next) {
  const resources = manifest.resources.quizzes[quizKey];
  if (checkpointIndex(next) > checkpointIndex(resources.checkpoint)) {
    resources.checkpoint = next;
  }
  recomputeManifestCheckpoint(manifest);
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

function assertSelectionMatches(manifest, requestedKeys) {
  if (!requestedKeys?.length) return;
  const requested = [...new Set(requestedKeys)].sort();
  const existing = [...manifest.quizKeys].sort();
  if (requested.join("\0") !== existing.join("\0")) {
    throw new Error(
      `Existing manifest contains quizzes [${existing.join(", ")}], not [${requested.join(", ")}]. Reset it before changing the scenario selection.`,
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

function normalizeStatus(value) {
  return String(value ?? "")
    .replaceAll("-", "")
    .replaceAll("_", "")
    .toLowerCase();
}

function sameJson(left, right) {
  return JSON.stringify(left) === JSON.stringify(right);
}
