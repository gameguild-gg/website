#!/usr/bin/env node

import { mkdir } from "node:fs/promises";
import { randomUUID } from "node:crypto";
import { resolve } from "node:path";
import { createClient, GeneratedApi } from "@game-guild/client";
import { chromium } from "playwright";
import {
  assertSharedAuthCookie,
  learningViewports,
  resolveNodeHealthCheckUrl,
  trackAppHttpFailures,
} from "./learning-browser-e2e-support.mjs";

const apiBaseUrl = (
  process.env.API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:8080"
).replace(/\/$/, "");
const webBaseUrl = (
  process.env.PUBLIC_E2E_BASE_URL ??
  process.env.NEXT_PUBLIC_APP_URL ??
  "http://gameguild.localhost:3011"
).replace(/\/$/, "");
const learningBaseUrl = `${webBaseUrl}/learn`;
const existingTenantId =
  process.env.API_TENANT_ID ?? process.env.TENANT_ID ?? undefined;
const evidenceDir = resolve(
  process.env.LEARNING_E2E_EVIDENCE_DIR ??
    "apps/web/test-results/learning-student",
);
const headless = !["0", "false", "no"].includes(
  (process.env.LEARNING_E2E_HEADLESS ?? "true").toLowerCase(),
);
const expectedCookieDomain = process.env.LEARNING_E2E_COOKIE_DOMAIN;
function getLearningPath(path) {
  return new URL(`${learningBaseUrl}${path}`).pathname;
}
const refreshWaitMs = Number.parseInt(
  process.env.LEARNING_E2E_REFRESH_WAIT_MS ?? "0",
  10,
);

function unique() {
  return `${Date.now()}-${randomUUID().replaceAll("-", "").slice(0, 8)}`;
}

function formatApiError(error) {
  if (!error) return "unknown API error";
  const detail = typeof error.detail === "string" ? ` ${error.detail}` : "";
  return `${error.status ?? "unknown"} ${error.message ?? "request failed"}${detail}`.trim();
}

function unwrap(result, label) {
  if (result.ok) return result.data;
  throw new Error(`${label} failed: ${formatApiError(result.error)}`);
}

function createApiClient(accessToken, tenantId) {
  return createClient({
    baseUrl: apiBaseUrl,
    timeout: 30_000,
    devtools: { enabled: false },
    ...(accessToken
      ? { auth: { getAccessToken: async () => accessToken } }
      : {}),
    ...(tenantId ? { tenant: { getTenantId: async () => tenantId } } : {}),
  });
}

async function enableCapability(client, tenantId, capability) {
  unwrap(
    await client.request({
      method: "POST",
      path: `/v1/tenants/${tenantId}/capabilities`,
      body: {
        capability,
        isEnabled: true,
        source: "override:e2e",
        reason: `Enable ${capability} for the learner browser E2E`,
        expiresAt: null,
      },
      requiresAuth: true,
    }),
    `Enable ${capability} for tenant ${tenantId}`,
  );
}

async function assertHttpOk(url, label) {
  const response = await fetch(resolveNodeHealthCheckUrl(url), {
    redirect: "manual",
  });
  if (!response.ok && (response.status < 300 || response.status >= 400)) {
    throw new Error(
      `${label} is not reachable at ${url}: HTTP ${response.status}`,
    );
  }
}

async function waitForSubmissionResult(page, label) {
  const success = page.getByText("Submission received").first();
  const persistedAssessment = page.getByText("Awaiting grading").first();
  const persistedContent = page.getByText("Activity completed").first();
  const failure = page.getByText("Submission failed").first();
  try {
    await Promise.any([
      success.waitFor({ timeout: 45_000 }),
      persistedAssessment.waitFor({ timeout: 45_000 }),
      persistedContent.waitFor({ timeout: 45_000 }),
      failure.waitFor({ timeout: 45_000 }),
    ]);
  } catch {
    const body = await page
      .locator("body")
      .innerText()
      .catch(() => "");
    throw new Error(
      `${label} did not render a success or failure state at ${page.url()}:\n${body.slice(0, 3000)}`,
    );
  }
  if (await failure.isVisible()) {
    const alert = failure.locator('xpath=ancestor::*[@role="alert"][1]');
    throw new Error(
      `${label} failed in the learner UI: ${(await alert.textContent())?.trim() || "unknown error"}`,
    );
  }
}

async function seedLearnerCourse() {
  await assertHttpOk(`${apiBaseUrl}/health`, "API");
  const tag = unique();
  const authorEmail = `learning-student-author-${tag}@example.test`;
  const password = "Str0ng!Passw0rd123!";
  const publicClient = createApiClient();
  const signUp = unwrap(
    await publicClient.request({
      method: "POST",
      path: "/v1/auth/sign-up",
      body: {
        username: `learning_student_author_${tag.replace(/[^a-z0-9]/gi, "_")}`,
        email: authorEmail,
        password,
        ...(existingTenantId ? { tenantId: existingTenantId } : {}),
      },
      requiresAuth: false,
    }),
    "Author sign-up",
  );

  let accessToken = signUp.accessToken;
  const bootstrapClient = createApiClient(accessToken);
  const tenantsPage = unwrap(
    await bootstrapClient.request({
      method: "GET",
      path: "/v1/tenants",
      params: { page: 1, pageSize: 500, status: "active" },
      requiresAuth: true,
    }),
    "Load active tenants for learner E2E",
  );
  const platformTenantId = tenantsPage.items?.find(
    (tenant) => tenant.isDefault,
  )?.id;
  if (!platformTenantId)
    throw new Error(
      "The learner E2E requires an active default platform tenant.",
    );

  let tenantId = existingTenantId;
  if (!tenantId) {
    const tenantClient = createApiClient(accessToken);
    tenantId = unwrap(
      await tenantClient.request({
        method: "POST",
        path: "/v1/tenants",
        body: {
          name: `Learning Student Tenant ${tag}`,
          slug: `learning-student-${tag}`,
          adminEmail: authorEmail,
          description: "Tenant created by the learner browser E2E.",
        },
        requiresAuth: true,
      }),
      "Create learner E2E tenant",
    ).id;
    accessToken = unwrap(
      await publicClient.request({
        method: "POST",
        path: "/v1/auth/sign-in",
        body: { email: authorEmail, password, tenantId },
        requiresAuth: false,
      }),
      "Author tenant sign-in",
    ).accessToken;
  }

  const client = createApiClient(accessToken, tenantId);
  for (const entitlementTenantId of new Set([platformTenantId, tenantId])) {
    await enableCapability(client, entitlementTenantId, "lxp.social");
  }

  const programs = new GeneratedApi.LearningCoursesProgramModule(client);
  const content = new GeneratedApi.LearningCoursesProgramContentModule(client);
  const lifecycle = new GeneratedApi.LearningCoursesProgramLifecycleModule(
    client,
  );
  const assessments = new GeneratedApi.LearningAssessmentsModule(client);
  const slug = `learner-e2e-${tag}`;
  const title = "Learner E2E Game Production";
  const course = unwrap(
    await programs.postCourses({
      title,
      description:
        "A complete learner journey with lessons, graded work, reflection, survey, discussion, and community.",
      slug,
      thumbnail:
        "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=1400&h=900&fit=crop",
    }),
    "Create learner course",
  );
  if (!course.id) throw new Error("Create learner course returned no id.");

  const module = unwrap(
    await content.postCoursesContent(course.id, {
      programId: course.id,
      title: "Production foundations",
      description: "Core production work for the learner journey.",
      type: "Module",
      sortOrder: 1,
      isRequired: true,
      visibility: "Public",
    }),
    "Create course module",
  );
  if (!module.id) throw new Error("Create course module returned no id.");

  const lesson = unwrap(
    await content.postCoursesContent(course.id, {
      programId: course.id,
      parentId: module.id,
      title: "Build a readable game loop",
      description:
        "Instrument a small loop and explain the production decisions.",
      type: "Lesson",
      lessonFormat: "Markdown",
      body: "# Build a readable game loop\n\nCreate a small loop, instrument the state changes, and record what you would improve next.",
      sortOrder: 1,
      isRequired: true,
      estimatedMinutes: 35,
      visibility: "Public",
    }),
    "Create lesson",
  );

  const reflection = unwrap(
    await content.postCoursesContent(course.id, {
      programId: course.id,
      parentId: module.id,
      title: "Production reflection",
      description: "Reflect on the strongest and weakest production decision.",
      type: "Reflection",
      body: "What would you preserve, and what would you change in the next iteration?",
      sortOrder: 2,
      isRequired: true,
      estimatedMinutes: 10,
      visibility: "Public",
    }),
    "Create reflection",
  );

  await content
    .postCoursesContent(course.id, {
      programId: course.id,
      parentId: module.id,
      title: "Iteration survey",
      description:
        "Share your confidence after completing the production loop.",
      type: "Survey",
      body: "How confident are you about instrumenting the next game loop?",
      sortOrder: 3,
      isRequired: false,
      estimatedMinutes: 5,
      visibility: "Public",
    })
    .then((result) => unwrap(result, "Create survey"));

  await content
    .postCoursesContent(course.id, {
      programId: course.id,
      parentId: module.id,
      title: "Peer production discussion",
      description: "Compare production approaches with the course community.",
      type: "Discussion",
      body: "Which instrumentation signal helped you make the best decision?",
      sortOrder: 4,
      isRequired: false,
      estimatedMinutes: 10,
      visibility: "Public",
    })
    .then((result) => unwrap(result, "Create discussion activity"));

  const assessment = unwrap(
    await assessments.postAssessments({
      courseId: course.id,
      title: "Game loop knowledge check",
      description:
        "Explain which signal best identifies an unstable game loop.",
      type: "Quiz",
      maxScore: 10,
      passingScore: 7,
      maxAttempts: 2,
      isRequired: true,
      submissionModalities: "StructuredAnswer",
      presentationMode: "SingleStep",
      dueAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(),
    }),
    "Create learner assessment",
  );

  const assignment = unwrap(
    await assessments.postAssessments({
      courseId: course.id,
      title: "Playable build submission",
      description: "Submit a playable build URL for instructor review.",
      type: "Assignment",
      maxScore: 20,
      passingScore: 14,
      maxAttempts: 2,
      isRequired: true,
      submissionModalities: "Url",
      presentationMode: "SingleStep",
      dueAt: new Date(Date.now() + 10 * 24 * 60 * 60 * 1000).toISOString(),
    }),
    "Create learner assignment",
  );

  const projectAssessment = unwrap(
    await assessments.postAssessments({
      courseId: course.id,
      title: "Portfolio game project",
      description:
        "Attach an existing Game Guild project as the final artifact.",
      type: "Project",
      maxScore: 40,
      passingScore: 28,
      maxAttempts: 1,
      isRequired: true,
      submissionModalities: "Project",
      presentationMode: "SingleStep",
      dueAt: new Date(Date.now() + 14 * 24 * 60 * 60 * 1000).toISOString(),
    }),
    "Create learner project assessment",
  );

  unwrap(await lifecycle.postCoursesSubmit(course.id), "Submit learner course");
  unwrap(
    await lifecycle.postCoursesApprove(course.id),
    "Approve learner course",
  );
  unwrap(
    await lifecycle.postCoursesPublish(course.id),
    "Publish learner course",
  );

  if (!lesson.slug) throw new Error("Create lesson returned no slug.");

  return {
    courseId: course.id,
    slug,
    title,
    platformTenantId,
    lessonSlug: lesson.slug,
    reflectionId: reflection.id,
    assessmentId: assessment.id,
    assignmentId: assignment.id,
    projectAssessmentId: projectAssessment.id,
  };
}

async function createLearnerProject(tenantId, email, password) {
  const publicClient = createApiClient();
  const signIn = unwrap(
    await publicClient.request({
      method: "POST",
      path: "/v1/auth/sign-in",
      body: { email, password, tenantId },
      requiresAuth: false,
    }),
    "Learner project sign-in",
  );
  const projectsClient = createApiClient(signIn.accessToken, tenantId);
  return unwrap(
    await projectsClient.request({
      method: "POST",
      path: "/v1/projects",
      body: {
        title: "Learner E2E Portfolio Game",
        description:
          "A learner-owned project used to validate project assessment submission.",
        shortDescription:
          "Portfolio project created by the learner browser E2E.",
        type: "Game",
        status: "Published",
        visibility: "Public",
      },
      requiresAuth: true,
    }),
    "Create learner portfolio project",
  );
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForFunction(
    () => document.body.innerText.trim().length > 0,
    undefined,
    { timeout: 30_000 },
  );
  const body = await page.locator("body").innerText();
  if (
    /404|page not found|Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(
      body,
    )
  ) {
    throw new Error(
      `${label} rendered an error surface:\n${body.slice(0, 1_500)}`,
    );
  }
}

async function assertNoHorizontalOverflow(page, label) {
  const dimensions = await page.evaluate(() => ({
    viewport: document.documentElement.clientWidth,
    document: document.documentElement.scrollWidth,
  }));
  if (dimensions.document > dimensions.viewport + 1) {
    throw new Error(
      `${label} has horizontal overflow: ${dimensions.document}px document in ${dimensions.viewport}px viewport.`,
    );
  }
}

async function waitForLearningReady(page) {
  await page
    .locator('[data-learning-ready=\"true\"]')
    .waitFor({ timeout: 45_000 });
}

async function visit(page, path, heading) {
  await page.goto(`${learningBaseUrl}${path}`, {
    waitUntil: "domcontentloaded",
  });
  await assertNoErrorSurface(page, path);
  await waitForLearningReady(page);
  if (heading) {
    const options =
      typeof heading === "string"
        ? { name: heading, exact: true }
        : { name: heading };
    await page.getByRole("heading", options).first().waitFor();
  }
}

async function runLearnerJourney(fixture) {
  await assertHttpOk(learningBaseUrl, "Learning app");
  await mkdir(evidenceDir, { recursive: true });
  const browser = await chromium.launch({ headless });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  const httpFailures = trackAppHttpFailures(page, [
    webBaseUrl,
    learningBaseUrl,
  ]);
  const runtimeErrors = [];
  page.setDefaultTimeout(35_000);
  page.setDefaultNavigationTimeout(90_000);
  page.on("pageerror", (error) => {
    runtimeErrors.push(error.message);
    console.error("[browser page error] " + error.message);
  });
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      !/favicon|cloudflareinsights|webpack-hmr/i.test(message.text())
    ) {
      runtimeErrors.push(message.text());
      console.error("[browser console error] " + message.text());
    }
  });
  page.on("requestfailed", (request) => {
    if (request.resourceType() === "script")
      console.error(
        "[browser script failed] " +
          request.url() +
          ": " +
          (request.failure()?.errorText ?? "unknown error"),
      );
  });

  try {
    await page.goto(`${learningBaseUrl}/courses/${fixture.slug}`, {
      waitUntil: "domcontentloaded",
    });
    await page.waitForURL((url) => {
      const redirectTo = url.searchParams.get("redirectTo");
      if (
        url.origin !== new URL(webBaseUrl).origin ||
        !url.pathname.endsWith("/sign-in") ||
        !redirectTo
      )
        return false;
      return redirectTo === getLearningPath(`/courses/${fixture.slug}`);
    });
    await assertNoErrorSurface(page, "Learner sign-in redirect");
    await page.getByRole("link", { name: /Sign up|Create one/ }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-up"),
    );

    const learnerTag = unique();
    const learnerEmail = `learning-student-${learnerTag}@example.test`;
    const learnerPassword = "Str0ng!Passw0rd123!";
    await page.getByLabel("Full name").fill("Learner Browser Journey");
    await page.getByLabel("Email").fill(learnerEmail);
    await page.getByLabel("Password", { exact: true }).fill(learnerPassword);
    await page.getByLabel("Confirm password").fill(learnerPassword);
    await page.getByRole("button", { name: "Create account" }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname === getLearningPath(`/courses/${fixture.slug}`),
      { timeout: 45_000 },
    );
    await waitForLearningReady(page);
    const initialAuthCookies = assertSharedAuthCookie(
      await context.cookies([webBaseUrl, learningBaseUrl]),
      expectedCookieDomain,
    );
    const initialAuthCookie =
      initialAuthCookies.find((cookie) => cookie.name === "gameguild") ??
      initialAuthCookies[0];
    const learnerProject = await createLearnerProject(
      fixture.platformTenantId,
      learnerEmail,
      learnerPassword,
    );
    if (!learnerProject.id)
      throw new Error("Create learner portfolio project returned no id.");
    try {
      await page.getByRole("heading", { name: "Join this course" }).waitFor();
    } catch (error) {
      console.error("[learner enrollment diagnostic] URL=" + page.url());
      console.error(
        "[learner enrollment diagnostic] headings=" +
          JSON.stringify(await page.locator("h1,h2,h3").allTextContents()),
      );
      console.error(
        "[learner enrollment diagnostic] body=" +
          (await page.locator("body").innerText()).slice(0, 4000),
      );
      await page.screenshot({
        path: resolve(evidenceDir, "enrollment-gate-failure.png"),
        fullPage: true,
      });
      throw error;
    }
    await page.getByRole("button", { name: "Enroll for free" }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname === getLearningPath(`/courses/${fixture.slug}/content`),
      { timeout: 45_000 },
    );
    await page
      .getByRole("heading", { name: "Course content", exact: true })
      .first()
      .waitFor();
    await visit(page, `/courses/${fixture.slug}`, fixture.title);
    await page.getByText("Course overview").waitFor();
    await page.screenshot({
      path: resolve(evidenceDir, "desktop-course-overview.png"),
      fullPage: true,
    });

    await visit(page, "/", /^Welcome back,/);
    await page.getByText(fixture.title, { exact: true }).first().waitFor();
    await page.goto(`${webBaseUrl}/courses`, {
      waitUntil: "domcontentloaded",
    });
    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/courses"),
    );
    await assertNoErrorSurface(page, "Course catalog");
    await page.getByText(fixture.title, { exact: true }).first().waitFor();
    await visit(page, `/courses/${fixture.slug}/content`, "Course content");
    await page
      .getByRole("link", { name: /Build a readable game loop/ })
      .click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname ===
          getLearningPath(
            `/courses/${fixture.slug}/lessons/${fixture.lessonSlug}`,
          ),
      { timeout: 180_000, waitUntil: "domcontentloaded" },
    );
    await page
      .getByRole("heading", { name: "Build a readable game loop", exact: true })
      .last()
      .waitFor();
    const start = page.getByRole("button", { name: "Start lesson" });
    if (await start.count()) {
      const progressControls = start.locator(
        'xpath=ancestor::div[contains(@class,"space-y-2")][1]',
      );
      await start.click();
      const progressError = progressControls.getByRole("alert");
      await Promise.race([
        start.waitFor({ state: "detached", timeout: 45_000 }),
        progressError.waitFor({ timeout: 45_000 }),
      ]);
      if (await progressError.isVisible()) {
        throw new Error(
          `Starting course content failed: ${await progressError.innerText()}`,
        );
      }
    }
    const complete = page.getByRole("button", { name: "Mark complete" });
    await complete.waitFor({ timeout: 45_000 });
    await complete.click();
    await page
      .getByText("Completed", { exact: true })
      .first()
      .waitFor({ timeout: 45_000 });

    await visit(page, `/courses/${fixture.slug}`, fixture.title);
    await page.getByRole("link", { name: "View activities" }).click();
    await page
      .getByRole("heading", { name: "Assignments and activities" })
      .waitFor();
    await page
      .getByText("Game loop knowledge check", { exact: true })
      .last()
      .waitFor();
    await page.getByText("Production reflection", { exact: true }).waitFor();
    await page.getByText("Iteration survey", { exact: true }).waitFor();
    await page
      .getByText("Peer production discussion", { exact: true })
      .waitFor();

    const assessmentCard = page
      .getByText("Game loop knowledge check", { exact: true })
      .last()
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await assessmentCard.getByRole("link", { name: "Start" }).click();
    await page
      .getByRole("heading", { name: "Game loop knowledge check", exact: true })
      .waitFor();
    await page
      .getByLabel("Your answer")
      .fill(
        "Frame-time variance is the clearest signal because it exposes unstable work across loop iterations.",
      );
    await page.getByRole("button", { name: "Submit assessment" }).click();
    await waitForSubmissionResult(page, "Assessment submission");

    await visit(
      page,
      `/courses/${fixture.slug}/activities`,
      "Assignments and activities",
    );
    const reflectionCard = page
      .getByText("Production reflection", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    const reflectionLink = reflectionCard.getByRole("link", {
      name: /Open|Review/,
    });
    if ((await reflectionLink.count()) === 0) {
      throw new Error(
        "Reflection activity was not available after completing its prerequisite: " +
          (await reflectionCard.innerText()),
      );
    }
    await reflectionLink.click();
    await page
      .getByRole("heading", { name: "Production reflection", exact: true })
      .waitFor();
    await page
      .getByLabel("Your reflection")
      .fill(
        "I would preserve the instrumentation boundary and shorten the feedback loop in the next iteration.",
      );
    await page.getByRole("button", { name: "Submit reflection" }).click();
    await waitForSubmissionResult(page, "Reflection submission");

    await visit(
      page,
      `/courses/${fixture.slug}/activities`,
      "Assignments and activities",
    );
    const surveyCard = page
      .getByText("Iteration survey", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await surveyCard.getByRole("link", { name: /Open|Review/ }).click();
    await page
      .getByRole("heading", { name: "Iteration survey", exact: true })
      .waitFor();
    await page
      .getByLabel("Your response")
      .fill(
        "Confident enough to instrument the next loop and compare frame-time variance.",
      );
    await page.getByRole("button", { name: "Submit survey" }).click();
    await waitForSubmissionResult(page, "Survey submission");

    await visit(
      page,
      `/courses/${fixture.slug}/activities`,
      "Assignments and activities",
    );
    const discussionCard = page
      .getByText("Peer production discussion", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await discussionCard.getByRole("link", { name: /Open|Review/ }).click();
    await page
      .getByRole("heading", { name: "Peer production discussion", exact: true })
      .waitFor();
    await page
      .getByLabel("Your contribution")
      .fill(
        "Frame-time variance made the unstable production path visible before release.",
      );
    await page.getByRole("button", { name: "Submit discussion" }).click();
    await waitForSubmissionResult(page, "Content discussion submission");

    await visit(
      page,
      `/courses/${fixture.slug}/activities`,
      "Assignments and activities",
    );
    const assignmentCard = page
      .getByText("Playable build submission", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await assignmentCard.getByRole("link", { name: "Start" }).click();
    await page
      .getByRole("heading", { name: "Playable build submission", exact: true })
      .waitFor();
    await page
      .getByLabel("Submission URL")
      .fill("https://example.test/builds/learner-e2e");
    await page.getByRole("button", { name: "Submit assessment" }).click();
    await waitForSubmissionResult(page, "Assignment submission");

    await visit(
      page,
      `/courses/${fixture.slug}/activities`,
      "Assignments and activities",
    );
    const projectCard = page
      .getByText("Portfolio game project", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await projectCard.getByRole("link", { name: "Start" }).click();
    await page
      .getByRole("heading", { name: "Portfolio game project", exact: true })
      .waitFor();
    await page
      .getByLabel("Project")
      .selectOption({ label: "Learner E2E Portfolio Game" });
    await page.getByRole("button", { name: "Submit assessment" }).click();
    await waitForSubmissionResult(page, "Project submission");

    await visit(page, `/courses/${fixture.slug}/community`, "Course community");
    await page.getByRole("button", { name: "Start discussion" }).click();
    await page.getByLabel("Title").fill("Frame pacing notes");
    await page
      .getByLabel("Message")
      .fill("Which frame pacing signal changed your production decision?");
    await page.getByRole("button", { name: "Publish discussion" }).click();
    const discussionPublished = page.getByText("Discussion published");
    const discussionError = page.getByText("Could not publish");
    await Promise.race([
      discussionPublished.waitFor({ timeout: 35_000 }),
      discussionError.waitFor({ timeout: 35_000 }),
    ]);
    if (await discussionError.isVisible()) {
      throw new Error(
        `Publishing course discussion failed: ${await page.getByRole("alert").innerText()}`,
      );
    }

    await visit(page, "/calendar", "Calendar");
    await page
      .getByText("Game loop knowledge check", { exact: true })
      .first()
      .waitFor();
    await visit(page, "/grades", "Grades and feedback");
    try {
      await page
        .getByText("Game loop knowledge check", { exact: true })
        .first()
        .waitFor();
    } catch (error) {
      console.error("[learner gradebook diagnostic] URL=" + page.url());
      console.error(
        "[learner gradebook diagnostic] body=" +
          (await page.locator("body").innerText()).slice(0, 6000),
      );
      await page.screenshot({
        path: resolve(evidenceDir, "gradebook-failure.png"),
        fullPage: true,
      });
      throw error;
    }
    await visit(page, "/certificates", "Certificates");
    await page.getByText("No certificates issued yet").first().waitFor();

    await visit(page, "/activities");

    for (const viewport of learningViewports) {
      await page.setViewportSize({
        width: viewport.width,
        height: viewport.height,
      });
      await visit(page, "/", /^Welcome back,/);
      await assertNoHorizontalOverflow(
        page,
        `${viewport.name} learner dashboard`,
      );
      await page.screenshot({
        path: resolve(evidenceDir, `${viewport.name}-learner-dashboard.png`),
        fullPage: true,
      });
      await visit(page, `/courses/${fixture.slug}/content`, "Course content");
      await assertNoHorizontalOverflow(
        page,
        `${viewport.name} learner course content`,
      );
      await page.screenshot({
        path: resolve(evidenceDir, `${viewport.name}-learner-content.png`),
        fullPage: true,
      });
    }

    await page.setViewportSize({ width: 390, height: 844 });
    await visit(page, "/", /^Welcome back,/);
    await page.getByRole("button", { name: "Toggle navigation" }).click();
    await page.getByRole("link", { name: "Calendar" }).waitFor();
    await page.keyboard.press("Escape");

    await page.getByRole("button", { name: "Open account menu" }).click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-in"),
    );
    await page
      .getByRole("heading", { name: "Welcome back to GameGuild" })
      .waitFor();

    await page.goto(`${learningBaseUrl}/courses/${fixture.slug}`, {
      waitUntil: "domcontentloaded",
    });
    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-in"),
    );
    await page.getByLabel("Email").fill(learnerEmail);
    await page.getByLabel("Password", { exact: true }).fill(learnerPassword);
    await page.getByRole("button", { name: "Sign in" }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname === getLearningPath(`/courses/${fixture.slug}`),
      { timeout: 45_000 },
    );
    await waitForLearningReady(page);
    assertSharedAuthCookie(
      await context.cookies([webBaseUrl, learningBaseUrl]),
      expectedCookieDomain,
    );

    if (refreshWaitMs > 0) {
      await page.waitForTimeout(refreshWaitMs);
      await page.reload({ waitUntil: "domcontentloaded" });
      await page
        .getByRole("heading", { name: fixture.title, exact: true })
        .waitFor();
      const refreshedCookies = assertSharedAuthCookie(
        await context.cookies([webBaseUrl, learningBaseUrl]),
        expectedCookieDomain,
      );
      const refreshedAuthCookie = refreshedCookies.find(
        (cookie) => cookie.name === initialAuthCookie.name,
      );
      if (
        !refreshedAuthCookie ||
        refreshedAuthCookie.value === initialAuthCookie.value
      ) {
        throw new Error(
          "The shared authentication cookie was not rotated after the configured refresh interval.",
        );
      }
    }

    await page.getByRole("button", { name: "Open account menu" }).click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-in"),
    );

    httpFailures.assertNone("Learning student journey");
    if (runtimeErrors.length > 0)
      throw new Error(
        `Browser runtime errors detected:\n${runtimeErrors.join("\n")}`,
      );
  } finally {
    await browser.close();
  }
}

async function main() {
  const fixture = await seedLearnerCourse();
  await runLearnerJourney(fixture);
  console.log(
    `Learning student browser E2E passed for ${learningBaseUrl}/courses/${fixture.slug}`,
  );
}

main().catch((error) => {
  console.error(
    error instanceof Error ? (error.stack ?? error.message) : error,
  );
  if (error && typeof error === "object")
    console.error(JSON.stringify(error, null, 2));
  process.exit(1);
});
