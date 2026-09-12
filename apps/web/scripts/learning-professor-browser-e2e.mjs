#!/usr/bin/env node

import { mkdir } from "node:fs/promises";
import { join } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";
import {
  assertSharedAuthCookie,
  trackAppHttpFailures,
} from "./learning-browser-e2e-support.mjs";

const apiBaseUrl = (
  process.env.API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:8080"
).replace(/\/$/, "");
const webBaseUrl = (
  process.env.PROFESSOR_E2E_BASE_URL ??
  process.env.NEXT_PUBLIC_APP_URL ??
  "http://gameguild.localhost:3011"
).replace(/\/$/, "");
const learningBaseUrl = `${webBaseUrl}/learn`;
const expectedCookieDomain = process.env.LEARNING_E2E_COOKIE_DOMAIN;
function getLearningPath(path) {
  return new URL(`${learningBaseUrl}${path}`).pathname;
}
const configuredProfessorEmail = process.env.PROFESSOR_E2E_EMAIL;
const configuredProfessorPassword = process.env.PROFESSOR_E2E_PASSWORD;
const headless = !["0", "false", "no"].includes(
  (process.env.PROFESSOR_E2E_HEADLESS ?? "true").toLowerCase(),
);
const authoringOnly = ["1", "true", "yes"].includes(
  (process.env.PROFESSOR_E2E_AUTHORING_ONLY ?? "false").toLowerCase(),
);

function unique() {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

async function apiRequest(path, init = {}, accessToken) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      "content-type": "application/json",
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  const body =
    response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(
      `${init.method ?? "GET"} ${path} failed with ${response.status}: ${JSON.stringify(body)}`,
    );
  }
  return body;
}

async function apiStatus(path, init = {}, accessToken) {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      "content-type": "application/json",
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
  return response.status;
}

async function deleteFixture(path, accessToken) {
  const deleteStatus = await apiStatus(path, { method: "DELETE" }, accessToken);
  if (![204, 404].includes(deleteStatus)) {
    throw new Error(`DELETE ${path} failed with ${deleteStatus}`);
  }
}

function flattenCourseContent(value) {
  const roots = Array.isArray(value)
    ? value
    : Array.isArray(value?.items)
      ? value.items
      : [];
  const flattened = [];
  const visit = (item) => {
    if (!item || typeof item !== "object") return;
    flattened.push(item);
    for (const child of Array.isArray(item.children) ? item.children : [])
      visit(child);
  };
  for (const root of roots) visit(root);
  return flattened;
}

async function bootstrap() {
  const tag = unique();
  if (
    Boolean(configuredProfessorEmail) !== Boolean(configuredProfessorPassword)
  ) {
    throw new Error(
      "PROFESSOR_E2E_EMAIL and PROFESSOR_E2E_PASSWORD must be provided together.",
    );
  }

  const professorEmail =
    configuredProfessorEmail ?? `professor-browser-author-${tag}@example.test`;
  const professorPassword =
    configuredProfessorPassword ?? "Str0ng!Passw0rd123!";
  const signIn = configuredProfessorEmail
    ? await apiRequest("/v1/auth/sign-in", {
        method: "POST",
        body: JSON.stringify({
          email: professorEmail,
          password: professorPassword,
        }),
      })
    : await apiRequest("/v1/auth/sign-up", {
        method: "POST",
        body: JSON.stringify({
          username: `professor_browser_author_${tag.replace(/[^a-z0-9]/gi, "_")}`,
          email: professorEmail,
          password: professorPassword,
        }),
      });

  if (!signIn.accessToken || !signIn.tenantId) {
    throw new Error(
      "The professor session did not expose accessToken and tenantId.",
    );
  }

  if (authoringOnly) {
    return {
      accessToken: signIn.accessToken,
      professorEmail,
      professorPassword,
      studentAccessToken: null,
      studentEmail: null,
      studentId: null,
      studentPassword: null,
      tag,
    };
  }
  const studentEmail = `professor-browser-student-${tag}@example.test`;
  const studentPassword = "Str0ng!Passw0rd123!";
  const studentSignUp = await apiRequest("/v1/auth/sign-up", {
    method: "POST",
    body: JSON.stringify({
      username: `professor_browser_student_${tag.replace(/[^a-z0-9]/gi, "_")}`,
      email: studentEmail,
      password: studentPassword,
      tenantId: signIn.tenantId,
    }),
  });
  const studentId = studentSignUp.userId ?? studentSignUp.user?.id;
  if (!studentId || !studentSignUp.accessToken)
    throw new Error(
      `The temporary professor E2E student sign-up did not expose a user id and access token for ${studentEmail}.`,
    );

  return {
    accessToken: signIn.accessToken,
    professorEmail,
    professorPassword,
    studentAccessToken: studentSignUp.accessToken,
    studentEmail,
    studentId,
    studentPassword,
    tag,
  };
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState("domcontentloaded");
  await page.locator("body").waitFor({ state: "visible" });
  const body = await page.locator("body").innerText();
  if (
    /This page could not be found|Course not found|Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(
      body,
    )
  ) {
    throw new Error(
      `${label} rendered an error surface at ${page.url()}:\n${body.slice(0, 1200)}`,
    );
  }
}

async function assertNoHorizontalOverflow(page, label) {
  const dimensions = await page.evaluate(() => ({
    viewportWidth: window.innerWidth,
    documentWidth: Math.max(
      document.documentElement.scrollWidth,
      document.body?.scrollWidth ?? 0,
    ),
  }));

  if (dimensions.documentWidth > dimensions.viewportWidth + 1) {
    throw new Error(
      `${label} overflows horizontally: ${dimensions.documentWidth}px document width at ${dimensions.viewportWidth}px viewport width.`,
    );
  }
}

async function assertHeaderContentContained(page, label) {
  const overflow = await page
    .locator("header")
    .first()
    .evaluate((header) => {
      const headerBounds = header.getBoundingClientRect();
      const visibleDescendants = [...header.querySelectorAll("*")].filter(
        (element) => {
          const bounds = element.getBoundingClientRect();
          const styles = window.getComputedStyle(element);
          return (
            styles.display !== "none" &&
            styles.visibility !== "hidden" &&
            bounds.width > 0 &&
            bounds.height > 0
          );
        },
      );
      const contentBottom = visibleDescendants.reduce(
        (bottom, element) =>
          Math.max(bottom, element.getBoundingClientRect().bottom),
        headerBounds.bottom,
      );

      return { contentBottom, headerBottom: headerBounds.bottom };
    });

  if (overflow.contentBottom > overflow.headerBottom + 1) {
    throw new Error(
      `${label} header content escapes its bounds: ${overflow.contentBottom}px content bottom vs ${overflow.headerBottom}px header bottom.`,
    );
  }
}

async function captureResponsiveSchedule(page, label) {
  const originalViewport = page.viewportSize() ?? { width: 1440, height: 1000 };
  const outputDirectory = fileURLToPath(
    new URL("../test-results/learning-professor", import.meta.url),
  );
  await mkdir(outputDirectory, { recursive: true });

  for (const viewport of [
    { name: "desktop", width: 1440, height: 1000 },
    { name: "tablet", width: 900, height: 1000 },
    { name: "mobile", width: 390, height: 844 },
  ]) {
    await page.setViewportSize({
      width: viewport.width,
      height: viewport.height,
    });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await assertNoErrorSurface(page, `${label} ${viewport.name}`);
    await assertNoHorizontalOverflow(page, `${label} ${viewport.name}`);
    await assertHeaderContentContained(page, `${label} ${viewport.name}`);
    await page.screenshot({
      path: join(outputDirectory, `${label}-${viewport.name}.png`),
      fullPage: true,
    });
  }

  await page.setViewportSize(originalViewport);
  await page.reload({ waitUntil: "domcontentloaded" });
  await waitForClientHydration(page);
}

async function visit(page, courseRoute, suffix, expectedText) {
  const path =
    "/workspace/learning/courses/" + courseRoute + (suffix ? "/" + suffix : "");
  await page.goto(webBaseUrl + path, { waitUntil: "domcontentloaded" });
  await waitForLocation(page, (url) => url.pathname === path, 180_000);
  await assertNoErrorSurface(page, suffix || "course root");
  await waitForClientHydration(page);
  if (expectedText) await waitForText(page, expectedText, 180_000);
}

async function waitForText(page, value, timeout = 45_000) {
  await page
    .getByText(value, { exact: false })
    .filter({ visible: true })
    .first()
    .waitFor({ timeout });
}

async function waitForClientHydration(page) {
  await page.waitForFunction(
    () => {
      if (document.readyState === "loading") return false;
      const controls = Array.from(
        document.querySelectorAll(
          'main button, main input, main textarea, main [role="combobox"]',
        ),
      );
      return controls.every((control) =>
        Object.keys(control).some((key) => key.startsWith("__reactProps$")),
      );
    },
    undefined,
    { timeout: 45_000 },
  );
}

async function waitForReactControl(page, locator) {
  const element = await locator.elementHandle();
  if (!element)
    throw new Error("Could not resolve the expected React control.");

  await page.waitForFunction(
    (control) =>
      Object.keys(control).some((key) => key.startsWith("__reactProps$")),
    element,
  );
}

async function waitForEnabled(locator, timeout = 45_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (
      (await locator.isVisible().catch(() => false)) &&
      (await locator.isEnabled().catch(() => false))
    )
      return;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }

  throw new Error(
    "Timed out waiting for the expected control to become enabled.",
  );
}

function routeFromUrl(url) {
  const match = new URL(url).pathname.match(/\/courses\/([^/]+)/);
  if (!match) throw new Error(`Could not derive course route from ${url}`);
  return decodeURIComponent(match[1]);
}

async function waitForLocation(page, predicate, timeout = 45_000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    const current = new URL(page.url());
    if (predicate(current)) return current;
    await page.waitForTimeout(100);
  }

  throw new Error(
    `Timed out waiting for the expected location. Current URL: ${page.url()}`,
  );
}

async function waitForApiState(readState, predicate, timeout = 45_000) {
  const deadline = Date.now() + timeout;
  let lastState = null;

  while (Date.now() < deadline) {
    lastState = await readState();
    if (predicate(lastState)) return lastState;
    await new Promise((resolve) => setTimeout(resolve, 150));
  }

  throw new Error(
    `Timed out waiting for persisted API state. Last state: ${JSON.stringify(lastState)}`,
  );
}

function readCourseMetadata(course) {
  if (!course?.metadata) return {};
  if (typeof course.metadata === "object") return course.metadata;

  try {
    return JSON.parse(course.metadata);
  } catch {
    return {};
  }
}

async function run() {
  const fixture = await bootstrap();
  const browser = await chromium.launch({ headless });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  let activePage = page;
  const httpFailures = trackAppHttpFailures(page, [
    webBaseUrl,
    learningBaseUrl,
  ]);
  const browserErrors = [];
  let learnerContext = null;
  let courseId = null;
  let deletedCourseId = null;
  let courseSlug = null;
  let discussionCreated = false;
  const createdClassIds = [];

  page.setDefaultTimeout(45_000);
  page.setDefaultNavigationTimeout(180_000);
  page.on("pageerror", (error) =>
    browserErrors.push(error.stack ?? error.message),
  );
  page.on("console", (message) => {
    if (
      message.type() === "error" &&
      !/favicon|cloudflareinsights/i.test(message.text())
    ) {
      browserErrors.push(message.text());
    }
  });
  try {
    console.log("[professor-e2e] authentication");
    await page.goto(`${webBaseUrl}/sign-in`, { waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await waitForReactControl(page, page.getByLabel("Email"));
    await page.getByLabel("Email").fill(fixture.professorEmail);
    await page
      .getByLabel("Password", { exact: true })
      .fill(fixture.professorPassword);
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await waitForLocation(
      page,
      (url) => url.pathname === "/" || url.pathname.includes("/workspace"),
      180_000,
    );

    await page.goto(`${webBaseUrl}/workspace/learning/courses/new`, {
      waitUntil: "domcontentloaded",
    });
    console.log("[professor-e2e] create course");
    await waitForClientHydration(page);
    await waitForReactControl(page, page.getByLabel("Title *"));
    courseSlug = `professor-browser-${fixture.tag}`;
    await page.getByLabel("Title *").fill(`Professor Browser ${fixture.tag}`);
    await page.getByLabel("URL Slug").fill(courseSlug);
    await page
      .getByLabel("Description *")
      .fill(
        "A complete professor course used to validate every management subsection through the browser.",
      );
    await page.getByRole("button", { name: "Next", exact: true }).click();
    await page.getByLabel("Estimated Hours").fill("24");
    await page
      .getByLabel("Thumbnail URL")
      .fill(
        "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=1400&h=900&fit=crop",
      );
    await page.getByRole("button", { name: "Next", exact: true }).click();
    await page.getByLabel("Max Enrollments").fill("20");
    await page.getByLabel("Skills Required").fill("Basic game development");
    await page
      .getByLabel("Skills Provided")
      .fill("Production planning, playtesting, launch readiness");
    await page
      .getByRole("button", { name: "Create Course", exact: true })
      .click();
    await waitForLocation(
      page,
      (url) =>
        url.pathname.includes("/workspace/learning/courses/") &&
        !url.pathname.endsWith("/new"),
      60_000,
    );
    let courseRoute = routeFromUrl(page.url());
    await visit(page, courseRoute, "overview", "Course Readiness");
    await waitForText(page, "Course Readiness");

    const courseLookup = await apiRequest(
      `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
      {},
      fixture.accessToken,
    );
    courseId = courseLookup.id;

    await visit(page, courseRoute, "listing/info", "Course Identity");
    console.log("[professor-e2e] listing identity, media, launch, pricing");
    await waitForReactControl(page, page.getByLabel("Course Title"));
    const updatedSlug = `${courseSlug}-updated`;
    await page
      .getByLabel("Course Title")
      .fill(`Complete Professor Course ${fixture.tag}`);
    await page.getByLabel("URL Slug").fill(updatedSlug);
    await page
      .getByLabel("Skills Students Will Learn")
      .fill("Course design, assessment planning, cohort delivery");
    await page.getByLabel("Prerequisites").fill("Basic game development");
    await page.getByRole("button", { name: "Save Changes" }).click();
    await waitForLocation(page, (url) =>
      url.pathname.includes(`${updatedSlug}-by-`),
    );
    courseSlug = updatedSlug;
    courseRoute = routeFromUrl(page.url());
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/courses/slug/${encodeURIComponent(updatedSlug)}`,
          {},
          fixture.accessToken,
        ),
      (course) => course?.title === `Complete Professor Course ${fixture.tag}`,
    );
    await visit(page, courseRoute, "listing/info", "Course Identity");
    await page.getByLabel("Course Title").waitFor();
    if (
      (await page.getByLabel("Course Title").inputValue()) !==
      `Complete Professor Course ${fixture.tag}`
    ) {
      throw new Error(
        "Course identity changes were not persisted after the canonical route update.",
      );
    }

    await visit(page, courseRoute, "listing/media", "Cover Image");
    await page.getByLabel("Thumbnail URL").fill("");
    await page.getByLabel("Video URL").fill("");
    await page.getByRole("button", { name: "Save Media" }).click();
    await waitForText(page, "Media updated successfully");
    await visit(page, courseRoute, "listing/media", "Cover Image");
    await waitForReactControl(page, page.getByLabel("Thumbnail URL"));
    await page
      .getByLabel("Thumbnail URL")
      .fill(
        "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=1400&h=900&fit=crop",
      );
    await page
      .getByLabel("Video URL")
      .fill("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
    await page.getByRole("button", { name: "Save Media" }).click();
    await waitForText(page, "Media updated successfully");

    await visit(page, courseRoute, "listing", "Launch Controls");
    await page.getByLabel("Enrollment cap").fill("12");
    await page.getByRole("button", { name: "Save launch controls" }).click();
    await waitForText(page, "Listing controls updated successfully");
    await visit(page, courseRoute, "listing", "Launch Controls");
    await page.getByLabel("Enrollment cap").fill("0");
    await page.getByRole("button", { name: "Save launch controls" }).click();
    await waitForText(page, "Listing controls updated successfully");

    await visit(page, courseRoute, "listing/pricing", "Pricing");
    const monetization = page.getByRole("switch", {
      name: "Enable monetization",
    });
    if ((await monetization.getAttribute("data-state")) !== "checked")
      await monetization.click();
    await page.getByLabel("Price").fill("79");
    await page.getByLabel("Currency").fill("USD");
    await page.getByRole("button", { name: "Save pricing" }).click();
    await waitForText(page, "Pricing updated successfully");
    await visit(page, courseRoute, "listing/pricing", "Pricing");
    if ((await page.getByLabel("Price").inputValue()) !== "79") {
      throw new Error("The saved course offer was not restored from the API.");
    }
    await page.getByLabel("Price").fill("99");
    await page.getByRole("button", { name: "Save pricing" }).click();
    await waitForText(page, "Pricing updated successfully");

    await visit(page, courseRoute, "listing/faq", "Frequently Asked Questions");
    console.log("[professor-e2e] listing FAQ, projects, testimonials");
    await page.getByRole("button", { name: "Add question" }).click();
    await page
      .getByLabel("Question", { exact: true })
      .last()
      .fill("Who is this course for?");
    await page
      .getByLabel("Answer", { exact: true })
      .last()
      .fill("Game developers preparing a production-ready portfolio project.");
    await page.getByRole("button", { name: "Save FAQ" }).click();
    await waitForText(page, "FAQ updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingFaq?.some(
          (item) => item.question === "Who is this course for?",
        ),
    );
    await page
      .getByLabel("Question", { exact: true })
      .first()
      .fill("Who should take this production course?");
    await page.getByRole("button", { name: "Save FAQ" }).click();
    await waitForText(page, "FAQ updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingFaq?.[0]?.question ===
        "Who should take this production course?",
    );
    await page.getByRole("button", { name: "Add question" }).click();
    await page
      .getByLabel("Question", { exact: true })
      .last()
      .fill("Temporary FAQ entry");
    await page
      .getByLabel("Answer", { exact: true })
      .last()
      .fill("This entry proves FAQ removal.");
    await page.getByRole("button", { name: "Save FAQ" }).click();
    await waitForText(page, "FAQ updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingFaq?.some(
          (item) => item.question === "Temporary FAQ entry",
        ),
    );
    const temporaryFaqQuestion = page
      .getByLabel("Question", { exact: true })
      .last();
    if ((await temporaryFaqQuestion.inputValue()) !== "Temporary FAQ entry")
      throw new Error("Temporary FAQ should be the last authored entry.");
    const temporaryFaqCard = temporaryFaqQuestion.locator(
      'xpath=ancestor::div[contains(@class, "rounded-lg")][1]',
    );
    await temporaryFaqCard
      .getByRole("button", { name: /Remove question/ })
      .click();
    await page.getByRole("button", { name: "Save FAQ" }).click();
    await waitForText(page, "FAQ updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingFaq?.[0]?.question ===
          "Who should take this production course?" &&
        !metadata.landingFaq.some(
          (item) => item.question === "Temporary FAQ entry",
        ),
    );

    await visit(page, courseRoute, "listing/projects", "Project Carousel");
    await page.getByRole("button", { name: "Add project" }).click();
    await page
      .getByLabel(/Project title/)
      .last()
      .fill("Playable vertical slice");
    await page
      .getByLabel(/Summary/)
      .last()
      .fill("Build and present a focused production milestone.");
    await page
      .getByLabel(/Deliverable/)
      .last()
      .fill("A playable build and retrospective.");
    await page.getByRole("button", { name: "Save project carousel" }).click();
    await waitForText(page, "Project carousel updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingProjects?.some(
          (item) => item.title === "Playable vertical slice",
        ),
    );
    await page
      .getByLabel(/Project title/)
      .last()
      .fill("Playable vertical slice showcase");
    await page.getByRole("button", { name: "Save project carousel" }).click();
    await waitForText(page, "Project carousel updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingProjects?.[0]?.title ===
        "Playable vertical slice showcase",
    );
    await page.getByRole("button", { name: "Add project" }).click();
    await page
      .getByLabel(/Project title/)
      .last()
      .fill("Temporary project");
    await page
      .getByLabel(/Summary/)
      .last()
      .fill("Temporary entry for removal coverage.");
    await page
      .getByLabel(/Deliverable/)
      .last()
      .fill("Temporary deliverable for removal coverage.");
    await page.getByRole("button", { name: "Save project carousel" }).click();
    await waitForText(page, "Project carousel updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingProjects?.some(
          (item) => item.title === "Temporary project",
        ),
    );
    const temporaryProjectTitle = page.getByLabel(/Project title/).last();
    if ((await temporaryProjectTitle.inputValue()) !== "Temporary project")
      throw new Error("Temporary project should be the last authored slide.");
    const temporaryProjectCard = temporaryProjectTitle.locator(
      'xpath=ancestor::div[contains(@class, "rounded-lg")][1]',
    );
    await temporaryProjectCard
      .getByRole("button", { name: /Remove project/ })
      .click();
    await page.getByRole("button", { name: "Save project carousel" }).click();
    await waitForText(page, "Project carousel updated successfully");
    await waitForApiState(
      async () =>
        readCourseMetadata(
          await apiRequest(
            `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
            {},
            fixture.accessToken,
          ),
        ),
      (metadata) =>
        metadata.landingProjects?.[0]?.title ===
          "Playable vertical slice showcase" &&
        !metadata.landingProjects.some(
          (item) => item.title === "Temporary project",
        ),
    );
    await visit(page, courseRoute, "listing/testimonials", "Testimonials");

    await visit(page, courseRoute, "content", "Add Module");
    console.log("[professor-e2e] content module and lesson");
    await page
      .getByRole("button", { name: "Add Module", exact: true })
      .last()
      .click();
    await page.getByLabel("Title").fill("Production Foundations");
    await page
      .getByLabel("Description (optional)")
      .fill("Prepare the project, scope, and delivery plan.");
    await page
      .getByRole("button", { name: "Add Module", exact: true })
      .last()
      .click();
    const moduleState = await waitForApiState(
      () =>
        apiRequest(`/v1/courses/${courseId}/content`, {}, fixture.accessToken),
      (content) =>
        flattenCourseContent(content).some(
          (item) => item.title === "Production Foundations",
        ),
    );
    const createdModule = flattenCourseContent(moduleState).find(
      (item) => item.title === "Production Foundations",
    );
    if (!createdModule?.id)
      throw new Error(
        "The content API did not return the newly created module id.",
      );
    await visit(page, courseRoute, "content", "Add Module");
    await waitForText(page, "Production Foundations");
    const moduleCard = page
      .getByText("Production Foundations", { exact: true })
      .locator('xpath=ancestor::*[@data-slot="card"][1]');
    await moduleCard.getByRole("button", { name: /Add lesson/i }).click();
    await page.getByLabel("Title").fill("Define the playable promise");
    await page.getByRole("button", { name: "Add Lesson", exact: true }).click();
    const lessonState = await waitForApiState(
      () =>
        apiRequest(`/v1/courses/${courseId}/content`, {}, fixture.accessToken),
      (content) =>
        flattenCourseContent(content).some(
          (item) =>
            item.title === "Define the playable promise" &&
            String(item.parentId).toLowerCase() ===
              String(createdModule.id).toLowerCase(),
        ),
    );
    const createdLesson = flattenCourseContent(lessonState).find(
      (item) => item.title === "Define the playable promise",
    );
    if (!createdLesson?.id) {
      throw new Error("The content API did not return the newly created lesson id.");
    }
    await waitForText(page, "Define the playable promise");
    const lessonRow = page
      .getByText("Define the playable promise", { exact: true })
      .locator('xpath=ancestor::div[contains(@class, "group")][1]');
    const editLessonButton = lessonRow.getByRole("button", {
      name: "Edit Lesson",
    });
    await waitForReactControl(page, editLessonButton);
    await editLessonButton.click();
    console.log(`[professor-e2e] opening authoring at ${page.url()}`);
    await waitForLocation(
      page,
      (url) => /\/content\/[^/]+$/i.test(url.pathname),
      600_000,
    );
    console.log(`[professor-e2e] authoring route ready at ${page.url()}`);
    await waitForText(page, "Publish changes", 180_000);
    console.log("[professor-e2e] authoring controls ready");
    await page
      .getByLabel("Description")
      .fill("Define the smallest experience that proves the product promise.");
    const authoringEditor = page.locator(".monaco-editor").first();
    await authoringEditor.waitFor({ state: "visible", timeout: 180_000 });
    console.log("[professor-e2e] Monaco editor ready");
    await authoringEditor.click();
    await page.keyboard.press("Control+A");
    await page.keyboard.insertText(
      "# Playable promise\n\nDescribe the player, the outcome, and the evidence required.",
    );
    await page.getByLabel(/Estimated minutes/).fill("35");
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/courses/${courseId}/content/${createdLesson.id}/authoring`,
          {},
          fixture.accessToken,
        ),
      (savedDraft) =>
        savedDraft?.payload?.body?.includes("Describe the player, the outcome") &&
        savedDraft?.payload?.estimatedMinutes === 35,
      180_000,
    );
    console.log("[professor-e2e] authoring autosave confirmed");
    await page.getByRole("button", { name: "preview", exact: true }).click();
    await waitForText(page, "Describe the player, the outcome", 60_000);
    await page.getByRole("button", { name: "editor", exact: true }).click();
    await page.getByRole("button", { name: /Publish changes/i }).click();
    console.log("[professor-e2e] publishing authoring draft");
    await waitForApiState(
      () =>
        apiRequest(`/v1/courses/${courseId}/content`, {}, fixture.accessToken),
      (content) =>
        flattenCourseContent(content).some(
          (entry) =>
            entry.title === "Define the playable promise" &&
            entry.body?.includes("Describe the player, the outcome"),
        ),
    );
    if (authoringOnly) {
      httpFailures.assertNone("Lesson authoring journey");
      if (browserErrors.length > 0) {
        throw new Error(
          `Browser errors detected during lesson authoring journey:\n${[
            ...new Set(browserErrors),
          ].join("\n")}`,
        );
      }
      console.log(
        `Lesson authoring browser E2E passed for ${courseSlug} (${courseId}).`,
      );
      return;
    }
    await page.getByRole("button", { name: "Curriculum", exact: true }).click();
    await waitForLocation(
      page,
      (url) => url.pathname.endsWith("/content"),
      180_000,
    );
    await page.getByRole("button", { name: "Edit module" }).click();
    await page.getByLabel("Title").fill("Production Delivery");
    await page
      .getByLabel("Description (optional)")
      .fill("Updated module description for the complete professor flow.");
    await page.getByRole("button", { name: "Save Changes" }).click();
    await waitForApiState(
      () =>
        apiRequest(`/v1/courses/${courseId}/content`, {}, fixture.accessToken),
      (content) =>
        flattenCourseContent(content).some(
          (item) => item.title === "Production Delivery",
        ),
    );
    await visit(page, courseRoute, "content", "Add Module");
    await waitForText(page, "Production Delivery");

    await visit(page, courseRoute, "assessments", "Assessments");
    console.log("[professor-e2e] assessment group and assessment");
    await page
      .getByRole("button", { name: "Add Group", exact: true })
      .first()
      .click();
    await page.getByLabel("Group name").fill("Final Project");
    await page.getByLabel("Weight percent").fill("100");
    await page.getByRole("button", { name: "Create Group" }).click();
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/assessments/course/${courseId}/groups`,
          {},
          fixture.accessToken,
        ),
      (groups) =>
        Array.isArray(groups) &&
        groups.some((group) => group.name === "Final Project"),
    );
    await visit(page, courseRoute, "assessments", "Assessments");
    await waitForText(page, "Final Project");
    await page
      .getByRole("button", { name: /^(?:Add|Create) Assessment$/ })
      .first()
      .click();
    await page.getByLabel("Title").fill("Vertical Slice Review");
    await page.getByLabel("Grade group").click();
    await page.getByRole("option", { name: /Final Project/ }).click();
    await page
      .getByRole("button", { name: "Create Assessment", exact: true })
      .click();
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/assessments/course/${courseId}`,
          {},
          fixture.accessToken,
        ),
      (assessments) =>
        Array.isArray(assessments) &&
        assessments.some(
          (assessment) => assessment.title === "Vertical Slice Review",
        ),
    );
    await visit(page, courseRoute, "assessments", "Assessments");
    await waitForText(page, "Vertical Slice Review");

    await page.getByRole("link", { name: /Vertical Slice Review/ }).click();
    await waitForText(page, "Assessment Editor");
    await page.getByLabel("Title").fill("Vertical Slice Final Review");
    await page
      .getByLabel("Description")
      .fill("Updated assessment instructions for the final production review.");
    await page.getByLabel("Max Score").fill("120");
    await page.getByLabel("Passing Score").fill("84");
    await page.getByLabel("Time Limit (minutes)").fill("45");
    await page.getByLabel("Max Attempts").fill("2");
    await page.getByRole("button", { name: "Save Changes" }).click();
    await waitForText(page, "Saved successfully");
    await page.getByRole("button", { name: "Back", exact: true }).click();
    await waitForText(page, "Vertical Slice Final Review");
    await page
      .getByRole("button", { name: "Edit group Final Project" })
      .click();
    await page.getByLabel("Group name").fill("Capstone Delivery");
    await page
      .getByLabel("Description")
      .fill("Weighted capstone assessment block.");
    await page.getByLabel("Weight percent").fill("100");
    await page.getByRole("button", { name: "Save Group" }).click();
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/assessments/course/${courseId}/groups`,
          {},
          fixture.accessToken,
        ),
      (groups) =>
        Array.isArray(groups) &&
        groups.some((group) => group.name === "Capstone Delivery"),
    );
    await visit(page, courseRoute, "assessments", "Assessments");
    await waitForText(page, "Capstone Delivery");

    await visit(page, courseRoute, "classes", "Classes");
    console.log(
      "[professor-e2e] independent morning and evening class schedules",
    );
    const dateInput = (offsetDays) => {
      const date = new Date(Date.now() + offsetDays * 86_400_000);
      return [
        date.getFullYear(),
        String(date.getMonth() + 1).padStart(2, "0"),
        String(date.getDate()).padStart(2, "0"),
      ].join("-");
    };
    const createScheduledClass = async ({
      name,
      description,
      startOffsetDays,
      meetingPattern,
      meetingDay,
      meetingStartTime,
    }) => {
      await visit(page, courseRoute, "classes", "Classes");
      await page.getByRole("button", { name: "New class" }).click();
      await page.getByLabel("Class name").fill(name);
      await page.getByLabel("Description").fill(description);
      await page.getByLabel("Start date").fill(dateInput(startOffsetDays));
      await page.getByLabel("End date").fill(dateInput(startOffsetDays + 56));
      await page.getByLabel("Capacity").fill("24");
      await page.getByLabel("Meeting pattern").fill(meetingPattern);
      await page
        .getByRole("button", { name: "Create and build schedule" })
        .click();
      const scheduleLocation = await waitForLocation(page, (url) =>
        /\/classes\/[0-9a-f-]+\/schedule$/i.test(url.pathname),
      );
      const classId = scheduleLocation.pathname.match(
        /\/classes\/([0-9a-f-]+)\/schedule$/i,
      )?.[1];
      if (!classId)
        throw new Error(
          `Could not derive the newly created class id from ${scheduleLocation.pathname}.`,
        );
      createdClassIds.push(classId);

      await waitForText(page, "Class schedule");
      await page.getByRole("button", { name: "Build schedule" }).click();
      await page.getByLabel("Timezone").fill("America/Sao_Paulo");
      if (meetingDay !== "Mon") {
        const monday = page.getByRole("checkbox", { name: "Mon" });
        if (await monday.isChecked()) await monday.click();
        await page.getByRole("checkbox", { name: meetingDay }).click();
      }
      await page.getByLabel("Meeting start time").fill(meetingStartTime);
      await page.getByRole("button", { name: "Generate preview" }).click();
      await waitForText(page, "Generated schedule");
      const advisoryConfirmation = page.getByRole("checkbox", {
        name: "I reviewed the advisory conflicts",
      });
      if (await advisoryConfirmation.isVisible().catch(() => false))
        await advisoryConfirmation.click();
      await page.getByRole("button", { name: "Apply schedule" }).click();
      await page
        .getByText(/^Version \d+$/)
        .filter({ visible: true })
        .first()
        .waitFor();
      await page
        .getByRole("heading", { name: /^Week 1 - / })
        .first()
        .waitFor();

      return {
        id: classId,
        schedule: await apiRequest(
          `/v1/courses/${courseId}/cohorts/${classId}/schedule`,
          {},
          fixture.accessToken,
        ),
      };
    };

    const morningClass = await createScheduledClass({
      name: "Morning Production Cohort",
      description: "Morning delivery for students working in the evening.",
      startOffsetDays: 7,
      meetingPattern: "Monday - 09:00",
      meetingDay: "Mon",
      meetingStartTime: "09:00",
    });
    const eveningClass = await createScheduledClass({
      name: "Evening Production Cohort",
      description: "Evening delivery for students working during the day.",
      startOffsetDays: 14,
      meetingPattern: "Thursday - 19:00",
      meetingDay: "Thu",
      meetingStartTime: "19:00",
    });

    const morningBeforeShift = JSON.stringify(
      morningClass.schedule.items ?? [],
    );
    const eveningVersionBeforeShift = eveningClass.schedule.version ?? 0;
    await page
      .getByRole("button", { name: /^Shift / })
      .first()
      .click();
    await page.getByLabel("Days to shift").fill("2");
    await page
      .getByRole("radio", { name: "This and following items" })
      .click();
    await page.getByRole("button", { name: "Shift schedule item" }).click();
    const shiftedEveningSchedule = await waitForApiState(
      () =>
        apiRequest(
          `/v1/courses/${courseId}/cohorts/${eveningClass.id}/schedule`,
          {},
          fixture.accessToken,
        ),
      (schedule) => (schedule?.version ?? 0) > eveningVersionBeforeShift,
    );
    const morningAfterShift = await apiRequest(
      `/v1/courses/${courseId}/cohorts/${morningClass.id}/schedule`,
      {},
      fixture.accessToken,
    );
    if (JSON.stringify(morningAfterShift.items ?? []) !== morningBeforeShift) {
      throw new Error(
        "Shifting the evening schedule changed the morning class schedule.",
      );
    }
    if (
      JSON.stringify(shiftedEveningSchedule.items ?? []) ===
      JSON.stringify(eveningClass.schedule.items ?? [])
    ) {
      throw new Error(
        "The evening schedule version changed without shifting its schedule items.",
      );
    }

    await captureResponsiveSchedule(page, "cohort-schedule");
    await page.getByRole("button", { name: "Switch class" }).click();
    await page
      .getByRole("menuitem")
      .filter({ hasText: "Morning Production Cohort" })
      .click();
    await waitForLocation(page, (url) =>
      url.pathname.includes(`/classes/${morningClass.id}/schedule`),
    );
    await waitForText(page, "Morning Production Cohort");
    await waitForText(page, "Monday");

    await visit(page, courseRoute, "certificates", "Certificates");
    await page.getByLabel("Name").fill("Production Course Completion");
    await page.getByRole("button", { name: "Create template" }).click();
    await waitForText(page, "Certificate template created.");
    await waitForText(page, "Production Course Completion");
    await page
      .getByRole("link", { name: /Production Course Completion/ })
      .click();
    await page.getByLabel("Template name").fill("Game Production Certificate");
    await page
      .getByLabel("Description")
      .fill("Updated completion credential for production students.");
    await page
      .getByRole("button", { name: "Save certificate template" })
      .click();
    await waitForText(page, "Certificate template saved.");
    await visit(page, courseRoute, "certificates", "Certificates");
    await waitForText(page, "Game Production Certificate");

    await visit(page, courseRoute, "students", "Students");
    await page
      .getByRole("button", { name: "Enroll student", exact: true })
      .first()
      .click();
    await page
      .getByRole("textbox", { name: "Student" })
      .fill(fixture.studentEmail);
    await page
      .getByRole("button", { name: "Enroll student", exact: true })
      .last()
      .click();
    await waitForText(page, "Student enrolled successfully");
    await waitForText(page, fixture.studentEmail);
    const studentRow = page
      .getByRole("row")
      .filter({ hasText: fixture.studentEmail });
    await studentRow.getByRole("checkbox").click();
    await page.getByRole("button", { name: "Send Message" }).click();
    await page.getByLabel("Subject").fill("Production milestone reminder");
    await page
      .getByRole("textbox", { name: "Message" })
      .fill("Bring your playable build and retrospective to the next review.");
    await page.getByRole("button", { name: "Send message" }).click();
    await waitForText(page, "Message sent to 1 student");

    await visit(page, courseRoute, "overview", "Analytics");
    await waitForText(page, "Completion funnel");
    await waitForText(page, "Engagement");
    await waitForText(page, "Revenue");
    console.log("[professor-e2e] analytics, support, settings, preview");

    await visit(page, courseRoute, "support/discussions", "Discussions");
    const discussionTitle = "Milestone review expectations";
    await page.getByLabel("Title").fill(discussionTitle);
    await page
      .getByLabel("Content")
      .fill("What evidence should students bring to the milestone review?");
    await page.getByRole("button", { name: "Create discussion" }).click();
    await page.waitForFunction(
      (title) => {
        const text = document.body.innerText;
        return text.includes(title) || text.includes("lxp.social");
      },
      discussionTitle,
      { timeout: 45_000 },
    );
    const discussionsBody = await page.locator("body").innerText();
    if (!discussionsBody.includes("lxp.social")) {
      await waitForText(page, "Milestone review expectations");
      discussionCreated = true;
    }
    await visit(page, courseRoute, "support/tickets", "Support Queue");

    await visit(page, courseRoute, "listing/access", "Listing visibility");
    await page.getByLabel("Maximum Enrollments").fill("0");
    await page.getByLabel("Enrollment deadline").fill("");
    await page.getByRole("button", { name: "Save Listing Access" }).click();
    await waitForText(page, "Listing access settings saved successfully");

    await visit(page, courseRoute, "settings/notifications", "Notifications");
    await page.getByLabel("Class reminder minutes").fill("1440, 60, 10");
    await page
      .getByRole("button", { name: "Save notification settings" })
      .click();
    await waitForText(page, "Notification settings saved");

    await visit(page, courseRoute, "settings/integrations", "Integrations");
    await page.getByRole("button", { name: "Add webhook" }).click();
    await page
      .getByLabel("Webhook URL")
      .fill("https://hooks.example.test/gameguild");
    await page.getByLabel("Events").fill("enrollment.created,course.completed");
    await page.getByRole("button", { name: "Add to course" }).click();
    await page
      .getByRole("button", { name: "Save integration settings" })
      .click();
    await waitForText(page, "Integration settings saved");
    await page
      .getByRole("button", {
        name: "Remove webhook https://hooks.example.test/gameguild",
      })
      .click();
    await page
      .getByRole("button", { name: "Save integration settings" })
      .click();
    await waitForText(page, "Integration settings saved");

    await visit(page, courseRoute, "preview", "Continue learning");
    await visit(page, courseRoute, "overview", "Course Readiness");
    await page
      .getByRole("button", { name: "Publish", exact: true })
      .first()
      .click();
    await waitForText(page, "Published");

    console.log("[professor-e2e] public storefront synchronization");
    await page.goto(`${webBaseUrl}/courses/${courseSlug}`, {
      waitUntil: "domcontentloaded",
    });
    await assertNoErrorSurface(page, "public course storefront");
    await waitForText(page, `Complete Professor Course ${fixture.tag}`);
    await waitForText(page, "Who should take this production course?");
    await waitForText(page, "Playable vertical slice showcase");
    await visit(page, courseRoute, "overview", "Course Readiness");

    console.log(
      "[professor-e2e] professor changes visible in learner workspace",
    );
    learnerContext = await browser.newContext({
      viewport: { width: 1440, height: 1000 },
    });
    const learnerPage = await learnerContext.newPage();
    activePage = learnerPage;
    const learnerHttpFailures = trackAppHttpFailures(learnerPage, [
      webBaseUrl,
      learningBaseUrl,
    ]);
    const learnerErrors = [];
    learnerPage.on("pageerror", (error) =>
      learnerErrors.push(error.stack ?? error.message),
    );
    learnerPage.on("console", (message) => {
      if (
        message.type() === "error" &&
        !/favicon|cloudflareinsights/i.test(message.text())
      )
        learnerErrors.push(message.text());
    });
    const learnerDestination = getLearningPath(
      `/courses/${courseSlug}/content`,
    );
    try {
      await learnerPage.goto(`${learningBaseUrl}/courses/${courseSlug}/content`, {
        // The auth-interrupt page immediately starts a client-side replacement
        // navigation, so the original goto can remain pending after its UI is
        // already interactive.
        waitUntil: "commit",
        timeout: 10_000,
      });
    } catch (error) {
      const reachedAuthInterrupt = await learnerPage
        .getByRole("heading", { name: "Sign in to continue" })
        .isVisible()
        .catch(() => false);
      if (
        !(error instanceof Error) ||
        !/Timeout/i.test(error.name) ||
        !reachedAuthInterrupt
      ) {
        throw error;
      }
    }

    const continueToSignIn = learnerPage.getByRole("link", {
      name: "Continue to sign in",
    });
    if (await continueToSignIn.isVisible().catch(() => false)) {
      await continueToSignIn.click();
    }
    await learnerPage.waitForURL(
      (url) => {
        const redirectTo = url.searchParams.get("redirectTo");
        if (
          url.origin !== new URL(webBaseUrl).origin ||
          !url.pathname.endsWith("/sign-in") ||
          !redirectTo
        )
          return false;
        return redirectTo === learnerDestination;
      },
      { timeout: 45_000 },
    );
    await learnerPage.getByLabel("Email").fill(fixture.studentEmail);
    await learnerPage
      .getByLabel("Password", { exact: true })
      .fill(fixture.studentPassword);
    await learnerPage
      .getByRole("button", { name: "Sign in", exact: true })
      .click();
    await learnerPage.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname === getLearningPath(`/courses/${courseSlug}/content`),
      { timeout: 45_000 },
    );
    assertSharedAuthCookie(
      await learnerContext.cookies([webBaseUrl, learningBaseUrl]),
      expectedCookieDomain,
    );
    await assertNoErrorSurface(learnerPage, "learner synchronization view");
    await learnerPage
      .getByRole("heading", { name: "Course content", exact: true })
      .waitFor();
    await learnerPage
      .getByText(`Complete Professor Course ${fixture.tag}`, { exact: true })
      .first()
      .waitFor();
    await learnerPage
      .getByText("Production Delivery", { exact: true })
      .first()
      .waitFor();
    await learnerPage
      .getByText("Define the playable promise", { exact: true })
      .first()
      .waitFor();
    await assertNoHorizontalOverflow(
      learnerPage,
      "learner synchronization view",
    );
    learnerHttpFailures.assertNone("Professor-to-learner synchronization");
    if (learnerErrors.length > 0) {
      throw new Error(
        `Browser errors detected in learner synchronization:\n${[...new Set(learnerErrors)].join("\n")}`,
      );
    }
    await learnerContext.close();
    learnerContext = null;
    activePage = page;

    console.log("[professor-e2e] lifecycle and subsection cleanup");
    await page.getByRole("button", { name: "Unpublish" }).click();
    await page
      .getByRole("button", { name: "Unpublish course", exact: true })
      .click();
    await waitForText(page, "Draft");
    const republishButton = page
      .getByRole("button", { name: "Publish", exact: true })
      .first();
    await waitForEnabled(republishButton);
    await republishButton.click();
    await waitForText(page, "Published");
    await visit(page, courseRoute, "settings/danger", "Settings");
    await page.getByRole("button", { name: "Archive Course" }).click();
    await waitForText(page, "Archived successfully.");
    await waitForApiState(
      () =>
        apiRequest(
          `/v1/courses/slug/${encodeURIComponent(courseSlug)}`,
          {},
          fixture.accessToken,
        ),
      (course) => ["Archived", "archived", 3, "3"].includes(course?.status),
    );
    await visit(page, courseRoute, "overview", "Course Readiness");
    await page
      .getByRole("button", { name: "Restore", exact: true })
      .first()
      .click();
    await waitForText(page, "Draft");
    const restoredPublishButton = page
      .getByRole("button", { name: "Publish", exact: true })
      .first();
    await waitForEnabled(restoredPublishButton);
    await restoredPublishButton.click();
    await waitForText(page, "Published");

    await visit(page, courseRoute, "students", "Students");
    const enrolledStudentRow = page
      .getByRole("row")
      .filter({ hasText: fixture.studentEmail });
    await enrolledStudentRow.getByRole("checkbox").click();
    await page.getByRole("button", { name: "Remove", exact: true }).click();
    await page.getByRole("button", { name: "Confirm removal" }).click();
    await waitForText(page, "1 student removed");

    await visit(page, courseRoute, "certificates", "Certificates");
    await page
      .getByRole("button", { name: "Delete Game Production Certificate" })
      .click();
    await waitForText(page, "Certificate template deleted.");

    for (const classId of createdClassIds.splice(0)) {
      await deleteFixture(`/api/cohorts/${classId}`, fixture.accessToken);
    }

    await visit(page, courseRoute, "content", "Add Module");
    const updatedLessonRow = page
      .getByText("Define the playable promise", { exact: true })
      .locator('xpath=ancestor::div[contains(@class, "group")][1]');
    await updatedLessonRow
      .getByRole("button", { name: "Delete", exact: true })
      .click();
    await page
      .getByRole("button", { name: "Delete", exact: true })
      .last()
      .click();
    await waitForText(page, "Production Delivery");
    await page.getByRole("button", { name: "Delete module" }).click();
    await page
      .getByRole("button", { name: "Delete", exact: true })
      .last()
      .click();

    await visit(page, courseRoute, "assessments", "Assessments");
    await page
      .getByRole("link", { name: /Vertical Slice Final Review/ })
      .click();
    page.once("dialog", (dialog) => dialog.accept());
    await page.getByRole("button", { name: "Delete Assessment" }).click();
    await Promise.race([
      waitForLocation(page, (url) => url.pathname.endsWith("/assessments")),
      page
        .locator("p.text-destructive")
        .filter({ hasText: /.+/ })
        .waitFor({ state: "visible", timeout: 30_000 })
        .then(async () => {
          throw new Error(
            `Assessment deletion failed: ${await page.locator("p.text-destructive").first().innerText()}`,
          );
        }),
    ]);
    await page
      .getByRole("button", { name: "Delete group Capstone Delivery" })
      .click();
    await page.getByRole("button", { name: "Delete Group" }).click();

    if (discussionCreated) {
      await visit(page, courseRoute, "support/discussions", "Discussions");
      await page
        .getByRole("button", { name: "Delete Milestone review expectations" })
        .click();
    }

    await visit(page, courseRoute, "listing/faq", "Frequently Asked Questions");
    await page.getByRole("button", { name: "Remove question 1" }).click();
    await page.getByRole("button", { name: "Save FAQ" }).click();
    await waitForText(page, "FAQ updated successfully");

    await visit(page, courseRoute, "listing/projects", "Project Carousel");
    const projectCountBeforeCleanup = await page
      .getByLabel(/Project title/)
      .count();
    await page
      .getByRole("button", {
        name: `Remove project ${projectCountBeforeCleanup}`,
      })
      .click();
    await page.getByRole("button", { name: "Save project carousel" }).click();
    await waitForText(page, "Project carousel updated successfully");

    await visit(page, courseRoute, "settings/danger", "Settings");
    await page.getByRole("button", { name: "Delete Course" }).click();
    await page
      .getByRole("textbox", { name: /confirm deletion/i })
      .fill(`Complete Professor Course ${fixture.tag}`);
    await page.getByRole("button", { name: "Permanently Delete" }).click();
    await waitForLocation(page, (url) =>
      url.pathname.endsWith("/workspace/learning/courses"),
    );
    deletedCourseId = courseId;
    courseId = null;

    httpFailures.assertNone("Professor learning journey");
    if (browserErrors.length > 0) {
      throw new Error(
        `Browser errors detected during professor journey:\n${[...new Set(browserErrors)].join("\n")}`,
      );
    }

    console.log(
      `Professor learning browser E2E passed for ${courseSlug} (${deletedCourseId ?? courseId}).`,
    );
  } catch (error) {
    const pageText = await activePage
      .locator("body")
      .innerText()
      .catch(() => "Unable to read page body.");
    console.error(`[professor-e2e] failed at ${activePage.url()}`);
    console.error(
      `[professor-e2e] HTTP failures: ${[...new Set(httpFailures.failures)].join(", ") || "none"}`,
    );
    console.error(
      `[professor-e2e] browser errors: ${[...new Set(browserErrors)].join(" | ") || "none"}`,
    );
    console.error(`[professor-e2e] page excerpt:\n${pageText.slice(0, 2400)}`);
    throw error;
  } finally {
    if (learnerContext) await learnerContext.close();
    if (courseId) {
      await deleteFixture(`/v1/courses/${courseId}`, fixture.accessToken).catch(
        (cleanupError) =>
          console.warn(
            `[professor-e2e] course cleanup failed: ${cleanupError instanceof Error ? cleanupError.message : String(cleanupError)}`,
          ),
      );
    }
    // This journey intentionally runs as ordinary users. User lifecycle APIs are
    // administrator-only in the deployed policy, so fixture account cleanup must
    // not make the browser test depend on elevated credentials.
    await browser.close();
  }
}

run().catch((error) => {
  console.error(
    error instanceof Error ? (error.stack ?? error.message) : error,
  );
  process.exit(1);
});
