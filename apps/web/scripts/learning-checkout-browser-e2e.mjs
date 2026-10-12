#!/usr/bin/env node

import { randomUUID } from "node:crypto";
import { createClient, GeneratedApi } from "@game-guild/client";
import { chromium } from "playwright";
import {
  assertSharedAuthCookie,
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
const headless = !["0", "false", "no"].includes(
  (process.env.LEARNING_E2E_HEADLESS ?? "true").toLowerCase(),
);
const expectedCookieDomain = process.env.LEARNING_E2E_COOKIE_DOMAIN;
function getLearningPath(path) {
  return new URL(`${learningBaseUrl}${path}`).pathname;
}

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
    timeout: 20_000,
    devtools: { enabled: false },
    ...(accessToken
      ? {
          auth: { getAccessToken: async () => accessToken },
        }
      : {}),
    ...(tenantId
      ? {
          tenant: { getTenantId: async () => tenantId },
        }
      : {}),
  });
}

function createCourseModules(client) {
  return {
    programs: new GeneratedApi.LearningCoursesProgramModule(client),
    content: new GeneratedApi.LearningCoursesProgramContentModule(client),
    lifecycle: new GeneratedApi.LearningCoursesProgramLifecycleModule(client),
  };
}

async function assertHttpOk(url, label) {
  const response = await fetch(resolveNodeHealthCheckUrl(url), {
    method: "GET",
    redirect: "manual",
  });
  if (!response.ok && (response.status < 300 || response.status >= 400)) {
    throw new Error(`${label} is not reachable at ${url}: ${response.status}`);
  }
}

async function seedPaidPublishedCourse() {
  await assertHttpOk(`${apiBaseUrl}/health`, "API");

  const tag = unique();
  const email = `learning-checkout-author-${tag}@example.test`;
  const password = "Str0ng!Passw0rd123!";
  const publicClient = createApiClient();

  const signUp = unwrap(
    await publicClient.request({
      method: "POST",
      path: "/v1/auth/sign-up",
      body: {
        username: `learning_checkout_author_${tag.replace(/[^a-z0-9]/gi, "_")}`,
        email,
        password,
        ...(existingTenantId ? { tenantId: existingTenantId } : {}),
      },
      requiresAuth: false,
    }),
    "Author sign-up",
  );

  let accessToken = signUp.accessToken;
  let tenantId = existingTenantId;

  if (!tenantId) {
    const tenantClient = createApiClient(accessToken);
    tenantId = unwrap(
      await tenantClient.request({
        method: "POST",
        path: "/v1/tenants",
        body: {
          name: `Learning Checkout Tenant ${tag}`,
          slug: `learning-checkout-${tag}`,
          adminEmail: email,
          description: "Tenant created by the learning checkout browser E2E.",
        },
        requiresAuth: true,
      }),
      "Create learning checkout tenant",
    ).id;

    accessToken = unwrap(
      await publicClient.request({
        method: "POST",
        path: "/v1/auth/sign-in",
        body: {
          email,
          password,
          tenantId,
        },
        requiresAuth: false,
      }),
      "Author tenant sign-in",
    ).accessToken;
  }

  const authorClient = createApiClient(accessToken, tenantId);
  const { programs, content, lifecycle } = createCourseModules(authorClient);
  const slug = `browser-paid-course-${tag}`;
  const title = "Browser E2E Paid Course";

  const course = unwrap(
    await programs.postCourses({
      title,
      description:
        "A paid course seeded by Playwright to validate prospect registration, checkout, and classroom access.",
      slug,
      thumbnail:
        "https://images.unsplash.com/photo-1550745165-9bc0b252726f?w=1400&h=900&fit=crop",
      estimatedHours: 2,
    }),
    "Create browser E2E course",
  );

  const courseId = course.id;
  if (!courseId) throw new Error("Create browser E2E course returned no id");

  const lesson = unwrap(
    await content.postCoursesContent(courseId, {
      programId: courseId,
      title: "Lesson 1: Build a readable game loop",
      description:
        "Start with a practical loop that is easy to inspect, test, and iterate.",
      type: "Lesson",
      body: "# Build a readable game loop\n\nCreate a small loop, instrument the state changes, and write down what you would improve next.",
      sortOrder: 1,
      isRequired: true,
      estimatedMinutes: 35,
      visibility: "Public",
    }),
    "Create browser E2E lesson",
  );

  unwrap(
    await lifecycle.postCoursesSubmit(courseId),
    "Submit browser E2E course",
  );
  unwrap(
    await lifecycle.postCoursesApprove(courseId),
    "Approve browser E2E course",
  );
  unwrap(
    await lifecycle.postCoursesPublish(courseId),
    "Publish browser E2E course",
  );

  const productId = unwrap(
    await programs.postCoursesCreateProduct(courseId, {
      name: "Paid course access",
      description:
        "Grants immediate classroom access for the browser E2E course.",
      basePrice: 49,
      currency: "USD",
    }),
    "Create browser E2E paid product",
  );

  if (!lesson.slug) throw new Error("Create browser E2E lesson returned no slug");
  return { courseId, slug, title, productId, lessonSlug: lesson.slug };
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState("domcontentloaded");
  await page.waitForFunction(
    () => document.body.innerText.trim().length > 0,
    undefined,
    { timeout: 30_000 },
  );
  const body = await page.locator("body").innerText({ timeout: 10_000 });
  if (
    /404|page not found|Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(
      body,
    )
  ) {
    throw new Error(
      `${label} rendered an error surface:\n${body.slice(0, 1_000)}`,
    );
  }
}

async function waitForClientHydration(page) {
  await page.waitForLoadState("networkidle").catch(() => undefined);
  await page
    .locator('script[src*="/_next/static"]')
    .first()
    .waitFor({ timeout: 30_000 })
    .catch(() => undefined);
  await page.waitForTimeout(750);
}

async function waitForCompletionEvidence(page) {
  const completionSignals = [
    page.getByText("1/1 items done"),
    page.getByText("100% complete"),
    page.getByText("Completed").first(),
  ];

  try {
    await Promise.any(
      completionSignals.map((locator) => locator.waitFor({ timeout: 20_000 })),
    );
  } catch {
    const body = await page
      .locator("body")
      .innerText({ timeout: 10_000 })
      .catch(() => "");
    throw new Error(
      `Lesson completion did not refresh in the classroom at ${page.url()}:\n${body.slice(0, 1_500)}`,
    );
  }
}

async function runBrowserJourney(course) {
  await assertHttpOk(webBaseUrl, "Web app");
  await assertHttpOk(learningBaseUrl, "Learning app");

  const browser = await chromium.launch({ headless });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  const httpFailures = trackAppHttpFailures(page, [
    webBaseUrl,
    learningBaseUrl,
  ]);
  const errors = [];

  page.setDefaultTimeout(30_000);
  page.on("console", (message) => {
    if (message.type() === "error") {
      errors.push(message.text());
    }
  });
  page.on("pageerror", (error) => {
    errors.push(error.message);
  });

  try {
    await page.goto(`${webBaseUrl}/courses/${course.slug}`, {
      waitUntil: "domcontentloaded",
    });
    await assertNoErrorSurface(page, "Signed-out course page");
    await page
      .getByRole("heading", { name: course.title, exact: true })
      .first()
      .waitFor();
    await page.getByRole("link", { name: "Sign in to enroll" }).first().click();

    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-in") &&
        url.searchParams.get("redirectTo") === `/courses/${course.slug}`,
    );
    await assertNoErrorSurface(page, "Course sign-in page");
    await waitForClientHydration(page);
    await page.getByRole("link", { name: "Sign up" }).click();

    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith("/sign-up") &&
        url.searchParams.get("redirectTo") === `/courses/${course.slug}`,
    );
    await assertNoErrorSurface(page, "Course sign-up page");
    await waitForClientHydration(page);

    const learnerTag = unique();
    const learnerEmail = `learning-checkout-learner-${learnerTag}@example.test`;
    const learnerPassword = "Str0ng!Passw0rd123!";

    await page.getByLabel(/Full name/i).fill("Learning Checkout Learner");
    await page.getByLabel("Email").fill(learnerEmail);
    await page.getByLabel("Password", { exact: true }).fill(learnerPassword);
    await page.getByLabel(/Confirm password/i).fill(learnerPassword);
    await page.getByRole("button", { name: /Create account/i }).click();

    await page.waitForURL(
      (url) =>
        url.origin === new URL(webBaseUrl).origin &&
        url.pathname.endsWith(`/courses/${course.slug}`),
    );
    assertSharedAuthCookie(
      await context.cookies([webBaseUrl, learningBaseUrl]),
      expectedCookieDomain,
    );
    await assertNoErrorSurface(page, "Signed-in paid course page");

    const checkoutButton = page
      .getByRole("button", { name: /Enroll for \$49(?:\.00)?/ })
      .first();
    try {
      await checkoutButton.waitFor();
    } catch {
      const body = await page
        .locator("body")
        .innerText({ timeout: 10_000 })
        .catch(() => "");
      throw new Error(
        `Paid checkout action was not available at ${page.url()}:\n${body.slice(0, 2_000)}`,
      );
    }
    await checkoutButton.click();
    await page.getByRole("heading", { name: "Complete enrollment" }).waitFor();
    await page.getByText("Total due today").waitFor();
    await page.getByText("$49").first().waitFor();
    await page
      .getByRole("button", { name: "Confirm and enter classroom" })
      .click();

    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname === getLearningPath(`/courses/${course.slug}/content`),
      { timeout: 45_000 },
    );
    await page.waitForLoadState("domcontentloaded");
    assertSharedAuthCookie(
      await context.cookies([webBaseUrl, learningBaseUrl]),
      expectedCookieDomain,
    );

    await assertNoErrorSurface(page, "Learning app classroom");
    await page
      .getByRole("heading", { name: "Course content", exact: true })
      .waitFor();
    await page
      .getByRole("main")
      .getByText(course.title, { exact: true })
      .waitFor();
    await page.getByRole("navigation", { name: "Course navigation" }).waitFor();
    await page
      .getByRole("link", { name: /Lesson 1: Build a readable game loop/ })
      .click();
    await page.waitForURL(
      (url) =>
        url.origin === new URL(learningBaseUrl).origin &&
        url.pathname ===
          getLearningPath(`/courses/${course.slug}/lessons/${course.lessonSlug}`),
      { timeout: 180_000 },
    );

    const startButton = page.getByRole("button", { name: "Start lesson" });
    if (await startButton.count()) {
      await startButton.click();
      await page.waitForLoadState("networkidle").catch(() => undefined);
    }

    const completeButton = page.getByRole("button", { name: "Mark complete" });
    await completeButton.waitFor();
    await completeButton.click();
    await page.waitForLoadState("networkidle").catch(() => undefined);
    await waitForCompletionEvidence(page);

    httpFailures.assertNone("Learning checkout journey");
    const relevantErrors = errors.filter(
      (message) => !/favicon/i.test(message),
    );
    if (relevantErrors.length > 0) {
      throw new Error(
        `Console/page errors detected:\n${relevantErrors.join("\n")}`,
      );
    }
  } finally {
    await browser.close();
  }
}

async function main() {
  const course = await seedPaidPublishedCourse();
  await runBrowserJourney(course);
  console.log(
    `Learning checkout browser E2E passed for ${webBaseUrl}/courses/${course.slug} using product ${course.productId}`,
  );
}

main().catch((error) => {
  console.error(
    error instanceof Error ? (error.stack ?? error.message) : error,
  );
  process.exit(1);
});
