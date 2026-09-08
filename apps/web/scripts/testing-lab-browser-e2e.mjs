#!/usr/bin/env node

import { randomUUID } from "node:crypto";
import { mkdir } from "node:fs/promises";
import path from "node:path";
import { chromium } from "playwright";
import {
  cleanupTestingLabFixture,
  collectAccessibilityFailures,
  collectViewportFailures,
  requireDisposableDatabaseMode,
  responseFailure,
  throwForBrowserQualityFailures,
} from "./testing-lab-browser-quality.mjs";

const apiBaseUrl = (
  process.env.API_BASE_URL ??
  process.env.NEXT_PUBLIC_API_URL ??
  "http://localhost:8080"
).replace(/\/$/, "");
const webBaseUrl = (
  process.env.TESTING_LAB_E2E_BASE_URL ??
  process.env.NEXT_PUBLIC_APP_URL ??
  "http://localhost:3005"
).replace(/\/$/, "");
const adminEmail = process.env.E2E_SYSTEM_ADMIN_EMAIL ?? "admin@game-guild.com";
const adminPassword = process.env.E2E_SYSTEM_ADMIN_PASSWORD ?? "Admin123!";
const headless = !["0", "false", "no"].includes(
  (process.env.TESTING_LAB_E2E_HEADLESS ?? "true").toLowerCase(),
);
const artifactsDirectory = path.resolve(
  process.env.TESTING_LAB_E2E_ARTIFACTS ??
    path.join(process.cwd(), ".tmp", "testing-lab-browser-e2e"),
);
const quality = {
  accessibilityFailures: [],
  browserErrors: [],
  failedResponses: [],
  viewportFailures: [],
};
let activePage;
let activePageLabel = "initial browser session";

function unique() {
  return `${Date.now()}-${randomUUID().slice(0, 8)}`;
}

function accessTokenRoles(accessToken) {
  const payload = accessToken.split(".")[1];
  if (!payload) return [];

  const claims = JSON.parse(Buffer.from(payload, "base64url").toString("utf8"));
  const roleClaims = [
    claims.role,
    claims.roles,
    claims["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"],
  ];
  return roleClaims
    .flatMap((claim) => (Array.isArray(claim) ? claim : [claim]))
    .filter((claim) => typeof claim === "string");
}

async function apiRequest(pathname, init = {}, accessToken, tenantId) {
  const response = await fetch(`${apiBaseUrl}${pathname}`, {
    ...init,
    headers: {
      "content-type": "application/json",
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...(tenantId ? { "x-tenant-id": tenantId } : {}),
      ...init.headers,
    },
  });
  const body =
    response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(
      `${init.method ?? "GET"} ${pathname} failed with ${response.status}: ${JSON.stringify(body)}`,
    );
  }
  return body;
}
async function warmSsr(pathname, label) {
  let lastError;
  for (let attempt = 1; attempt <= 12; attempt += 1) {
    try {
      const response = await fetch(`${webBaseUrl}${pathname}`);
      if (response.ok) {
        await response.arrayBuffer();
        return;
      }
      lastError = new Error(`${label} returned ${response.status}.`);
    } catch (error) {
      lastError = error;
    }
    await new Promise((resolve) => setTimeout(resolve, 1_000));
  }
  throw new Error(
    `${label} warmup failed after retries: ${lastError instanceof Error ? lastError.message : String(lastError)}`,
  );
}

async function warmTestingLabSsr() {
  await warmSsr("/en-US/testing-lab", "Testing Lab SSR");
}

async function warmTestingLabEventSsr(eventId) {
  await warmSsr(
    `/en-US/testing-lab/events/${eventId}`,
    "Testing Lab event SSR",
  );
}

async function bootstrap() {
  const auth = await apiRequest("/v1/auth/sign-in", {
    method: "POST",
    body: JSON.stringify({ email: adminEmail, password: adminPassword }),
  });
  if (!auth.accessToken || !auth.tenantId) {
    throw new Error(
      "The system administrator session did not expose accessToken and tenantId.",
    );
  }
  const adminRoles = accessTokenRoles(auth.accessToken);
  if (!adminRoles.some((role) => role.toLowerCase() === "systemadmin")) {
    throw new Error(
      `The seeded administrator token is missing SystemAdmin. Issued roles: ${adminRoles.join(", ") || "none"}.`,
    );
  }

  const tag = unique();
  async function createFixtureIdentity(kind) {
    const email = `testing-lab-browser-${kind}-${tag}@example.test`;
    const password = "Str0ng!Passw0rd123!";
    const signUp = await apiRequest("/v1/auth/sign-up", {
      method: "POST",
      body: JSON.stringify({
        username: `testing_lab_browser_${kind}_${tag.replace(/[^a-z0-9]/gi, "_")}`,
        email,
        password,
        tenantId: auth.tenantId,
      }),
    });
    const userId = signUp.userId ?? signUp.user?.id;
    if (!userId)
      throw new Error(`The ${kind} sign-up did not expose a user id.`);
    const memberships = await apiRequest(
      `/v1/users/${userId}/memberships?includeInactive=true`,
      {},
      auth.accessToken,
      auth.tenantId,
    );
    const hasMembership = memberships.memberships?.some(
      (membership) =>
        String(membership.tenantId).toLowerCase() ===
          String(auth.tenantId).toLowerCase() && membership.isActive,
    );
    if (!hasMembership) {
      await apiRequest(
        `/v1/users/${userId}/memberships`,
        {
          method: "POST",
          body: JSON.stringify({
            tenantId: auth.tenantId,
            role: "Member",
            requiresAcceptance: false,
            invitedByEmail: adminEmail,
            inviteeEmail: email,
          }),
        },
        auth.accessToken,
        auth.tenantId,
      );
    }
    const tenantAuth = await apiRequest("/v1/auth/sign-in", {
      method: "POST",
      body: JSON.stringify({ email, password, tenantId: auth.tenantId }),
    });
    if (!tenantAuth.accessToken)
      throw new Error(
        `The ${kind} tenant sign-in did not expose an access token.`,
      );
    return { accessToken: tenantAuth.accessToken, email, password, userId };
  }

  const [owner, reviewer, tester] = await Promise.all([
    createFixtureIdentity("owner"),
    createFixtureIdentity("reviewer"),
    createFixtureIdentity("tester"),
  ]);
  const project = await apiRequest(
    "/v1/projects",
    {
      method: "POST",
      body: JSON.stringify({
        title: `Browser Lab Project ${tag}`,
        description:
          "A playable project created for the Testing Lab browser journey.",
        shortDescription: "Real browser E2E fixture",
        type: 0,
        visibility: 4,
        status: 2,
        tags: ["testing-lab", "browser-e2e"],
      }),
    },
    owner.accessToken,
    auth.tenantId,
  );
  const projectVersion = await apiRequest(
    `/v1/projects/${project.id}/versions`,
    {
      method: "POST",
      body: JSON.stringify({
        versionNumber: "1.0.0-browser-e2e",
        releaseNotes: "Browser-verified Testing Lab build.",
      }),
    },
    owner.accessToken,
    auth.tenantId,
  );
  await apiRequest(
    `/v1/projects/${project.id}/versions/${projectVersion.id}:ready`,
    { method: "POST" },
    owner.accessToken,
    auth.tenantId,
  );

  const now = Date.now();
  const event = await apiRequest(
    "/v1/testing/events",
    {
      method: "POST",
      body: JSON.stringify({
        name: `Browser Testing Showcase ${tag}`,
        description:
          "A real campus testing event used to verify public, applicant, tester, and manager browser journeys.",
        mode: 1,
        approvalMode: 1,
        applicationsOpenAt: new Date(now - 60_000).toISOString(),
        applicationsCloseAt: new Date(now + 60 * 60_000).toISOString(),
        startsAt: new Date(now + 2 * 60 * 60_000).toISOString(),
        endsAt: new Date(now + 5 * 60 * 60_000).toISOString(),
        requiresFeedback: true,
      }),
    },
    auth.accessToken,
    auth.tenantId,
  );

  const slot = await apiRequest(
    `/v1/testing/events/${event.id}/slots`,
    {
      method: "POST",
      body: JSON.stringify({
        mode: 1,
        startsAt: new Date(now + 2 * 60 * 60_000).toISOString(),
        endsAt: new Date(now + 4 * 60 * 60_000).toISOString(),
        maxTesters: 4,
        maxProjects: 2,
        campusName: "Browser E2E Campus",
        roomName: "Interaction Lab",
        meetingUrl: null,
        locationId: null,
      }),
    },
    auth.accessToken,
    auth.tenantId,
  );

  await apiRequest(
    `/v1/testing/events/${event.id}/configuration`,
    {
      method: "PUT",
      body: JSON.stringify({
        generalRules:
          "Respect the code of conduct, protect confidential builds, and complete assigned feedback.",
        candidateInstructions:
          "Provide a playable build and clear test tasks for the assigned testers.",
        testerInstructions:
          "Follow the project brief, complete every task, and submit the developer questionnaire.",
        projectApplicationSchema: {
          title: "Event application questions",
          questions: [
            {
              id: "target-audience",
              prompt: "Who is the target audience?",
              type: "FreeText",
              required: true,
              options: [],
            },
          ],
        },
        testerRegistrationSchema: {
          title: "Tester registration questions",
          questions: [
            {
              id: "device",
              prompt: "Which device will you use?",
              type: "FreeText",
              required: true,
              options: [],
            },
          ],
        },
      }),
    },
    auth.accessToken,
    auth.tenantId,
  );

  await apiRequest(
    `/v1/testing/events/${event.id}/committee`,
    {
      method: "POST",
      body: JSON.stringify({ userId: reviewer.userId, isChair: true }),
    },
    auth.accessToken,
    auth.tenantId,
  );

  const reviewerApplicationAccess = await apiRequest(
    `/v1/testing/events/${event.id}/applications/access`,
    { method: "GET" },
    reviewer.accessToken,
    auth.tenantId,
  );
  if (
    reviewerApplicationAccess.canViewApplications !== true ||
    reviewerApplicationAccess.canManageApplications === true ||
    reviewerApplicationAccess.canVote !== true
  ) {
    throw new Error(
      "An active committee reviewer did not receive the expected application access.",
    );
  }

  await apiRequest(
    `/v1/testing/events/${event.id}:open-applications`,
    {
      method: "POST",
    },
    auth.accessToken,
    auth.tenantId,
  );
  const publicEvents = await apiRequest(
    "/v1/testing/events/public?skip=0&take=100",
  );
  if (
    !Array.isArray(publicEvents) ||
    !publicEvents.some((candidate) => candidate.id === event.id)
  ) {
    throw new Error(
      `The public event directory did not expose fixture ${event.id}.`,
    );
  }

  const publicEvent = await apiRequest(`/v1/testing/events/public/${event.id}`);
  if (publicEvent?.id !== event.id) {
    throw new Error(
      `The public event detail did not expose fixture ${event.id}.`,
    );
  }

  return {
    accessToken: auth.accessToken,
    event,
    owner,
    project,
    projectVersion,
    reviewer,
    slot,
    tag,
    tenantId: auth.tenantId,
    tester,
  };
}

async function assertNoErrorSurface(page, label) {
  await page.waitForLoadState("domcontentloaded");
  await page.locator("body").waitFor({ state: "visible" });
  const body = await page.locator("body").innerText();
  if (
    /This page could not be found|Unhandled Runtime Error|Build Error|Application error|Internal server error/i.test(
      body,
    )
  ) {
    throw new Error(
      `${label} rendered an error surface at ${page.url()}:\n${body.slice(0, 1600)}`,
    );
  }
}

async function assertNoViewportOverflow(page, label) {
  const dimensions = await page.evaluate(() => ({
    clientWidth: document.documentElement.clientWidth,
    scrollWidth: document.documentElement.scrollWidth,
  }));
  if (dimensions.scrollWidth > dimensions.clientWidth + 2) {
    throw new Error(
      `${label} overflows the viewport: ${JSON.stringify(dimensions)}`,
    );
  }
}

async function visit(page, pathname, label) {
  activePage = page;
  activePageLabel = label;
  const expectedPathname = new URL(pathname, webBaseUrl).pathname;
  try {
    await page.goto(`${webBaseUrl}${pathname}`, {
      waitUntil: "domcontentloaded",
    });
  } catch (error) {
    // Next can finish an RSC navigation before Playwright observes the
    // browser-level load event. Only accept that abort after the requested
    // route is already active; the checks below still reject a bad page.
    const completedPathname = new URL(page.url()).pathname;
    if (
      !String(error).includes("net::ERR_ABORTED") ||
      completedPathname !== expectedPathname
    ) {
      throw new Error(
        `${label} navigation aborted before the requested route became active. ` +
          `Expected ${expectedPathname}; browser resolved ${page.url()}. Original error: ${String(error)}`,
      );
    }
  }
  await assertNoErrorSurface(page, label);
  await page.waitForFunction(() => document.readyState !== "loading");
  // RSC navigation can briefly replace the rendered route while the client
  // applies its final payload. Run semantic checks only after that boundary,
  // otherwise a valid heading can disappear between waitFor and evaluation.
  await waitForClientHydration(page);
  await page.locator("h1").first().waitFor({ state: "visible" });
  quality.accessibilityFailures.push(
    ...(await collectAccessibilityFailures(page, label)),
  );
  quality.viewportFailures.push(
    ...(await collectViewportFailures(page, label)),
  );
}

function monitorPage(page) {
  page.on("pageerror", (error) => quality.browserErrors.push(error.message));
  page.on("console", (message) => {
    if (message.type() === "error") quality.browserErrors.push(message.text());
  });
  page.on("response", (response) => {
    const failure = responseFailure(response, webBaseUrl);
    if (failure) quality.failedResponses.push(failure);
  });
}

async function waitForClientHydration(page) {
  await page
    .waitForLoadState("networkidle", { timeout: 20_000 })
    .catch(() => undefined);
  await page
    .locator('script[src*="/_next/static"]')
    .first()
    .waitFor({ timeout: 20_000 })
    .catch(() => undefined);
  await page.waitForTimeout(750);
}

async function assertAuthenticatedBrowserSession(page, label) {
  const session = await page.evaluate(async () => {
    const response = await fetch("/api/auth/session");
    return {
      body: await response.json().catch(() => null),
      status: response.status,
    };
  });
  if (session.status !== 200 || !session.body?.user?.id) {
    throw new Error(
      `${label} did not retain an authenticated browser session: ${JSON.stringify(session)}`,
    );
  }
}

async function signIn(page, email = adminEmail, password = adminPassword) {
  await visit(page, "/sign-in", "sign in");
  await waitForClientHydration(page);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page
    .getByRole("button", { name: "Sign in", exact: true })
    .click({ noWaitAfter: true });
  await page.waitForURL(/\/dashboard/, { timeout: 60_000 });
  await page.waitForURL(
    (url) => url.pathname.endsWith("/workspace"),
    { timeout: 60_000 },
  );
  await assertAuthenticatedBrowserSession(page, `Browser sign-in for ${email}`);
}

async function waitForText(page, text) {
  await page
    .getByText(text, { exact: false })
    .filter({ visible: true })
    .first()
    .waitFor();
}

async function run() {
  requireDisposableDatabaseMode(process.env.TESTING_LAB_E2E_DATABASE_MODE);
  await mkdir(artifactsDirectory, { recursive: true });
  const fixture = await bootstrap();
  await warmTestingLabSsr();
  await warmTestingLabEventSsr(fixture.event.id);
  const browser = await chromium.launch({ headless });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  activePage = page;
  let ownerContext;
  let reviewerContext;
  let testerContext;
  let testerPage;
  let eventCancelled = false;

  page.setDefaultNavigationTimeout(120_000);
  page.setDefaultTimeout(60_000);
  monitorPage(page);

  try {
    console.log(
      "[testing-lab-browser-e2e] anonymous directory and event detail",
    );
    await visit(page, "/testing-lab", "public Testing Lab landing");
    await waitForText(page, "Game Testing Lab");
    await page
      .getByRole("link", { name: "Browse Events", exact: true })
      .click();
    await page.waitForURL(/\/testing-lab\/events/);
    await waitForText(page, fixture.event.name);
    await assertNoViewportOverflow(page, "public Testing Lab directory");
    await page.screenshot({
      path: path.join(artifactsDirectory, "public-directory-desktop.png"),
      fullPage: true,
    });

    await visit(
      page,
      `/testing-lab/events/${fixture.event.id}`,
      "public Testing Lab event",
    );
    await waitForText(page, "Schedules and tester capacity");
    await waitForText(page, "Browser E2E Campus");
    await page
      .getByRole("link", { name: "Sign in to apply", exact: true })
      .waitFor();
    await assertNoViewportOverflow(page, "public Testing Lab event");

    console.log("[testing-lab-browser-e2e] authenticated project candidacy");
    ownerContext = await browser.newContext({
      viewport: { width: 1280, height: 900 },
    });
    const ownerPage = await ownerContext.newPage();
    ownerPage.setDefaultNavigationTimeout(120_000);
    ownerPage.setDefaultTimeout(60_000);
    monitorPage(ownerPage);
    await signIn(ownerPage, fixture.owner.email, fixture.owner.password);
    await visit(
      ownerPage,
      `/en-US/testing-lab/events/${fixture.event.id}`,
      "project-owner public Testing Lab event",
    );
    await assertAuthenticatedBrowserSession(
      ownerPage,
      "Project owner event visit",
    );
    await waitForClientHydration(ownerPage);
    await ownerPage
      .getByLabel("Eligible project version")
      .selectOption(fixture.projectVersion.id);
    await ownerPage
      .getByRole("button", { name: "Save and continue", exact: true })
      .click();
    await ownerPage.getByLabel("Test objective").waitFor();
    await ownerPage
      .getByLabel("Test objective")
      .fill(
        "Validate that first-time players understand the core interaction loop.",
      );
    await ownerPage
      .getByLabel("Installation and access")
      .fill(
        "Open the supplied browser build and sign in with the test account.",
      );
    await ownerPage
      .getByLabel("Test tasks (one per line)")
      .fill(
        "Complete onboarding\nFinish the first level\nOpen the results screen",
      );
    await ownerPage
      .getByLabel("Controls")
      .fill("Keyboard arrows to move; space to interact.");
    await ownerPage
      .getByLabel("Known limitations")
      .fill("Audio mixing is still being tuned.");
    await ownerPage
      .getByRole("button", { name: "Save and continue", exact: true })
      .click();
    await ownerPage
      .getByRole("button", { name: "Add question", exact: true })
      .click();
    await ownerPage
      .getByLabel("Prompt")
      .fill("What should we improve before release?");
    await ownerPage
      .getByRole("button", { name: "Save and continue", exact: true })
      .click();
    await ownerPage
      .getByLabel("Who is the target audience?")
      .fill("Players new to cooperative puzzle games.");
    await ownerPage
      .getByRole("button", { name: "Review application", exact: true })
      .click();
    await ownerPage
      .getByLabel("Preferred availability")
      .fill("The published campus schedule works for this project.");
    await ownerPage
      .getByLabel(/I have read and accept the frozen rules/i)
      .check();
    await ownerPage.screenshot({
      path: path.join(
        artifactsDirectory,
        "application-wizard-review-desktop.png",
      ),
      fullPage: true,
    });
    await ownerPage
      .getByRole("button", { name: "Submit for review", exact: true })
      .click();
    await waitForText(ownerPage, "Project application submitted.");
    await ownerContext.close();
    ownerContext = undefined;

    console.log("[testing-lab-browser-e2e] manager review");
    await signIn(page);
    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/applications`,
      "Testing Lab manager applications",
    );
    await waitForClientHydration(page);
    await waitForText(page, "Project applications");
    await page.getByRole("button", { name: "Review", exact: true }).click();
    await waitForText(page, "Under Review");

    console.log("[testing-lab-browser-e2e] committee reviewer vote");
    reviewerContext = await browser.newContext({
      viewport: { width: 1280, height: 900 },
    });
    const reviewerPage = await reviewerContext.newPage();
    reviewerPage.setDefaultNavigationTimeout(120_000);
    reviewerPage.setDefaultTimeout(60_000);
    monitorPage(reviewerPage);
    await signIn(
      reviewerPage,
      fixture.reviewer.email,
      fixture.reviewer.password,
    );
    await visit(
      reviewerPage,
      `/workspace/testing-lab/events/${fixture.event.id}/applications`,
      "committee review applications",
    );
    await waitForClientHydration(reviewerPage);
    for (const action of ["Review", "Approve", "Waitlist", "Reject"]) {
      if (
        (await reviewerPage
          .getByRole("button", { name: action, exact: true })
          .count()) > 0
      ) {
        throw new Error(
          `Committee reviewer unexpectedly received the ${action} management action.`,
        );
      }
    }
    await reviewerPage
      .getByRole("button", { name: "Vote", exact: true })
      .click();
    await reviewerPage.getByText("Choose decision", { exact: true }).click();
    await reviewerPage
      .getByRole("option", { name: "Approve", exact: true })
      .click();
    await reviewerPage
      .getByLabel("Comments")
      .fill("Approved by the committee reviewer through the browser.");
    await reviewerPage
      .getByRole("button", { name: "Record vote", exact: true })
      .click();
    await waitForText(reviewerPage, "Committee vote recorded.");
    await reviewerPage.getByRole("dialog").waitFor({ state: "hidden" });
    await reviewerContext.close();
    reviewerContext = undefined;
    activePage = page;
    activePageLabel = "Testing Lab manager applications";

    console.log(
      "[testing-lab-browser-e2e] manager approval and capacity reservation",
    );
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await assertNoErrorSurface(
      page,
      "Testing Lab manager event after committee vote",
    );
    await page.getByRole("button", { name: "Approve", exact: true }).click();
    await page.getByRole("combobox", { name: "Testing slot" }).click();
    await page.getByRole("option").first().click();
    await page
      .getByLabel("Decision notes")
      .fill("Approved through the real browser management flow.");
    await page
      .getByRole("button", { name: "Approve project", exact: true })
      .click();
    await waitForText(page, "Project application approved.");
    await page.getByRole("dialog").waitFor({ state: "hidden" });
    await waitForText(page, "Approved");

    await page
      .getByRole("button", { name: "Close applications", exact: true })
      .click();
    await waitForText(page, "Applications closed");
    await page
      .getByRole("button", { name: "Schedule event", exact: true })
      .click();
    await waitForText(page, "Scheduled");
    console.log(
      "[testing-lab-browser-e2e] tester seat through the public experience",
    );
    testerContext = await browser.newContext({
      viewport: { width: 1440, height: 1000 },
    });
    testerPage = await testerContext.newPage();
    testerPage.setDefaultNavigationTimeout(120_000);
    testerPage.setDefaultTimeout(60_000);
    monitorPage(testerPage);
    await signIn(testerPage, fixture.tester.email, fixture.tester.password);
    await visit(
      testerPage,
      `/testing-lab/events/${fixture.event.id}`,
      "scheduled public Testing Lab event",
    );
    await waitForClientHydration(testerPage);
    await testerPage
      .getByLabel("Which device will you use?")
      .fill("Desktop keyboard and mouse");
    await testerPage
      .getByRole("button", {
        name: "Confirm registration answers",
        exact: true,
      })
      .click();
    await testerPage.getByLabel(/I accept the frozen rules/i).check();
    await testerPage
      .getByRole("button", { name: "Reserve tester seat", exact: true })
      .click();
    await waitForText(testerPage, "Testing slot registration submitted.");
    await waitForText(testerPage, "Registered");
    await testerPage.screenshot({
      path: path.join(artifactsDirectory, "event-participation-desktop.png"),
      fullPage: true,
    });

    console.log("[testing-lab-browser-e2e] manager operations surfaces");
    for (const [pathname, title] of [
      ["/workspace/testing-lab", "Testing Lab"],
      ["/workspace/testing-lab/events", "Testing events"],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/overview`,
        "Event overview",
      ],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/applications`,
        "Project applications",
      ],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/schedule`,
        "Schedule and capacity",
      ],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/testers`,
        "Testers and attendance",
      ],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/feedback`,
        "Feedback review",
      ],
      [
        `/workspace/testing-lab/events/${fixture.event.id}/learning`,
        "Learning evidence",
      ],
      ["/workspace/testing-lab/projects", "Community projects"],
      [
        "/workspace/testing-lab/participants",
        "Testing Lab participants",
      ],
      ["/workspace/testing-lab/analytics", "Testing Lab analytics"],
      ["/workspace/testing-lab/settings/general", "General settings"],
      ["/workspace/testing-lab/settings/templates", "Event templates"],
      [
        "/workspace/testing-lab/settings/locations",
        "Testing locations",
      ],
      ["/workspace/testing-lab/settings/access", "Access and roles"],
    ]) {
      await visit(page, pathname, title);
      await waitForText(page, title);
      await assertNoViewportOverflow(page, title);
    }

    console.log("[testing-lab-browser-e2e] versioned event template creation");
    await visit(
      page,
      "/workspace/testing-lab/settings/templates",
      "Testing Lab event templates",
    );
    await waitForClientHydration(page);
    await page
      .getByLabel("Name")
      .fill(`Browser playtest template ${fixture.tag}`);
    await page
      .getByLabel("Description")
      .fill("Reusable browser-verified event package.");
    await page
      .getByLabel("General rules")
      .fill(
        "Respect the code of conduct and complete every assigned feedback form.",
      );
    await page
      .getByLabel("Candidate instructions")
      .fill(
        "Provide a playable build, a test brief, and a developer questionnaire.",
      );
    await page
      .getByLabel("Tester instructions")
      .fill("Follow the project tasks and submit structured feedback.");
    await page
      .getByRole("button", { name: "Create template", exact: true })
      .click();
    await waitForText(page, "Event template created.");
    await page.screenshot({
      path: path.join(artifactsDirectory, "event-templates-desktop.png"),
      fullPage: true,
    });

    console.log("[testing-lab-browser-e2e] general settings persistence");
    const labName = `GameGuild Browser Lab ${fixture.tag}`;
    await visit(
      page,
      "/workspace/testing-lab/settings/general",
      "Testing Lab general settings",
    );
    await waitForClientHydration(page);
    await page.getByLabel("Lab name").fill(labName);
    await page
      .getByLabel("Description")
      .fill("Browser-verified Testing Lab operations.");
    await page
      .getByRole("button", { name: "Save settings", exact: true })
      .click();
    await waitForText(page, "Testing Lab settings updated.");
    await page.reload({ waitUntil: "domcontentloaded" });
    await page.getByLabel("Lab name").waitFor();
    if ((await page.getByLabel("Lab name").inputValue()) !== labName) {
      throw new Error(
        "Testing Lab general settings did not persist after reload.",
      );
    }

    console.log("[testing-lab-browser-e2e] location lifecycle");
    const locationName = `Browser Operations Lab ${fixture.tag}`;
    const updatedLocationName = `${locationName} Updated`;
    await visit(
      page,
      "/workspace/testing-lab/settings/locations",
      "Testing Lab location management",
    );
    await waitForClientHydration(page);
    await page
      .getByRole("button", { name: "New location", exact: true })
      .first()
      .click();
    let actionDialog = page.getByRole("dialog");
    await actionDialog.getByLabel("Location name").fill(locationName);
    await actionDialog.getByLabel("Street address").fill("100 Browser Way");
    await actionDialog.getByLabel("City", { exact: true }).fill("E2E City");
    await actionDialog.getByLabel("State / region").fill("Test State");
    await actionDialog.getByLabel("Country").fill("Test Country");
    await actionDialog
      .getByLabel("Operations email")
      .fill("testing-lab@example.test");
    await actionDialog
      .getByRole("button", { name: "Create location", exact: true })
      .click();
    await waitForText(page, "Testing location created.");
    await actionDialog.waitFor({ state: "hidden" });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await waitForText(page, locationName);

    let locationRow = page.getByRole("row").filter({ hasText: locationName });
    await locationRow
      .getByRole("button", { name: "Edit", exact: true })
      .click();
    actionDialog = page.getByRole("dialog");
    await actionDialog.getByLabel("Location name").fill(updatedLocationName);
    await actionDialog
      .getByRole("button", { name: "Save location", exact: true })
      .click();
    await waitForText(page, "Testing location updated.");
    await actionDialog.waitFor({ state: "hidden" });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await waitForText(page, updatedLocationName);

    locationRow = page
      .getByRole("row")
      .filter({ hasText: updatedLocationName });
    await locationRow
      .getByRole("button", { name: "Archive", exact: true })
      .click();
    let confirmDialog = page.getByRole("alertdialog");
    await confirmDialog
      .getByRole("button", { name: "Archive location", exact: true })
      .click();
    await waitForText(page, "Testing location archived.");
    await confirmDialog.waitFor({ state: "hidden" });
    await visit(
      page,
      "/workspace/testing-lab/settings/locations?status=archived",
      "archived Testing Lab locations",
    );
    await waitForClientHydration(page);
    await waitForText(page, updatedLocationName);
    locationRow = page
      .getByRole("row")
      .filter({ hasText: updatedLocationName });
    await locationRow
      .getByRole("button", { name: "Restore", exact: true })
      .click();
    confirmDialog = page.getByRole("alertdialog");
    await confirmDialog
      .getByRole("button", { name: "Restore location", exact: true })
      .click();
    await waitForText(page, "Testing location restored.");
    await confirmDialog.waitFor({ state: "hidden" });

    console.log("[testing-lab-browser-e2e] role and member access lifecycle");
    const roleName = `Browser facilitator ${fixture.tag}`;
    const updatedRoleName = `${roleName} updated`;
    await visit(
      page,
      "/workspace/testing-lab/settings/access",
      "Testing Lab access and roles",
    );
    await waitForClientHydration(page);
    await page
      .getByRole("button", { name: "New role", exact: true })
      .first()
      .click();
    actionDialog = page.getByRole("dialog");
    await actionDialog.getByLabel("Role name").fill(roleName);
    await actionDialog
      .getByLabel("Description")
      .fill("Browser-verified facilitator permissions.");
    await actionDialog.getByRole("checkbox", { name: "View requests", exact: true }).click();
    await actionDialog.getByRole("checkbox", { name: "View sessions", exact: true }).click();
    await actionDialog
      .getByRole("button", { name: "Create role", exact: true })
      .click();
    await waitForText(page, "Testing Lab role created.");
    await actionDialog.waitFor({ state: "hidden" });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForClientHydration(page);
    await waitForText(page, roleName);

    let roleRow = page.locator("article").filter({ hasText: roleName });
    await roleRow.getByRole("button", { name: "Edit", exact: true }).click();
    actionDialog = page.getByRole("dialog");
    await actionDialog.locator('input[name="name"]').fill(updatedRoleName);
    await actionDialog
      .locator('textarea[name="description"]')
      .fill("Updated through the complete browser role lifecycle.");
    await actionDialog
      .getByRole("button", { name: "Save role", exact: true })
      .click();
    await waitForText(page, "Testing Lab role updated.");
    await actionDialog.waitFor({ state: "hidden" });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForText(page, updatedRoleName);
    await waitForClientHydration(page);

    await page.getByLabel("Member").click();
    await page
      .getByRole("option", { name: new RegExp(fixture.reviewer.email, "i") })
      .click();
    await page
      .getByRole("button", { name: "Manage access", exact: true })
      .click();
    const accessSheet = page.getByRole("dialog");
    await waitForText(accessSheet, "Role assignment");
    await accessSheet.getByLabel("Testing Lab role").click();
    await page
      .getByRole("option", { name: updatedRoleName, exact: true })
      .click();
    await accessSheet
      .getByRole("button", { name: "Assign", exact: true })
      .click();
    await waitForText(accessSheet, "Testing Lab role assigned.");
    await accessSheet
      .getByRole("button", { name: "Revoke", exact: true })
      .click();
    await waitForText(accessSheet, "Testing Lab role revoked.");
    await page.keyboard.press("Escape");
    await accessSheet.waitFor({ state: "hidden" });

    roleRow = page.locator("article").filter({ hasText: updatedRoleName });
    await roleRow.getByRole("button", { name: "Delete", exact: true }).click();
    confirmDialog = page.getByRole("alertdialog");
    await confirmDialog
      .getByRole("button", { name: "Delete role", exact: true })
      .click();
    await waitForText(page, "Testing Lab role deleted.");
    await confirmDialog.waitFor({ state: "hidden" });

    console.log("[testing-lab-browser-e2e] attendance and required feedback");
    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/overview`,
      "active Testing Lab event overview",
    );
    await waitForClientHydration(page);
    await page
      .getByRole("button", { name: "Start event", exact: true })
      .click();
    await waitForText(page, "Event status updated.");
    await waitForText(page, "Active");

    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/testers`,
      "Testing Lab attendance operations",
    );
    await waitForClientHydration(page);
    let attendanceForm = page
      .locator("form")
      .filter({ has: page.locator('input[name="registrationId"]') })
      .first();
    await attendanceForm.getByRole("combobox").click();
    await page.getByRole("option", { name: "Check in", exact: true }).click();
    await attendanceForm
      .getByRole("button", { name: "Update", exact: true })
      .click();
    await waitForText(page, "Checked In");

    await page
      .getByRole("button", { name: "Assign tested project", exact: true })
      .click();
    actionDialog = page.getByRole("dialog");
    await actionDialog.getByRole("combobox").click();
    await page
      .getByRole("option")
      .filter({ hasText: fixture.project.title })
      .click();
    await actionDialog
      .getByRole("button", { name: "Assign project", exact: true })
      .click();
    await waitForText(page, "Tested project assigned.");
    await actionDialog.waitFor({ state: "hidden" });

    await visit(
      testerPage,
      `/testing-lab/events/${fixture.event.id}`,
      "Testing Lab required feedback",
    );
    await waitForClientHydration(testerPage);
    await testerPage
      .getByLabel("What should we improve before release?")
      .fill(
        "The controls are clear; add a stronger visual cue before the first interaction.",
      );
    await testerPage
      .getByRole("button", {
        name: "Confirm questionnaire answers",
        exact: true,
      })
      .click();
    await testerPage.getByLabel(/Overall rating/).fill("9");
    await testerPage.getByLabel("Would you recommend it?").selectOption("yes");
    await testerPage
      .getByLabel("Additional observations (optional)")
      .fill("Browser-verified required feedback.");
    await testerPage
      .getByRole("button", { name: "Submit required feedback", exact: true })
      .click();
    await waitForText(testerPage, "All assigned feedback is complete.");

    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/testers`,
      "Testing Lab attendance completion",
    );
    await waitForClientHydration(page);
    attendanceForm = page
      .locator("form")
      .filter({ has: page.locator('input[name="registrationId"]') })
      .first();
    await attendanceForm.getByRole("combobox").click();
    await page.getByRole("option", { name: "Check out", exact: true }).click();
    await attendanceForm
      .getByRole("button", { name: "Update", exact: true })
      .click();
    await waitForText(page, "Attended");
    attendanceForm = page
      .locator("form")
      .filter({ has: page.locator('input[name="registrationId"]') })
      .first();
    await attendanceForm.getByRole("combobox").click();
    await page.getByRole("option", { name: "Complete", exact: true }).click();
    await attendanceForm
      .getByRole("button", { name: "Update", exact: true })
      .click();
    await waitForText(page, "Completed");
    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/feedback`,
      "Testing Lab feedback review after submission",
    );
    await waitForText(page, "Browser-verified required feedback.");

    console.log("[testing-lab-browser-e2e] filters search and pagination");
    await visit(
      page,
      "/workspace/testing-lab/events",
      "Testing Lab event directory filters",
    );
    await page.getByLabel("Search testing events").fill(fixture.event.name);
    await page.getByRole("button", { name: "Search", exact: true }).click();
    await page.waitForURL(/q=/);
    await waitForText(page, fixture.event.name);
    await page
      .getByRole("navigation", { name: "Filter testing events" })
      .getByRole("link", { name: "Active", exact: true })
      .click();
    await page.waitForURL(/status=Active/);
    await waitForText(page, fixture.event.name);

    await visit(
      page,
      "/workspace/testing-lab/participants",
      "Testing Lab participant filters",
    );
    await waitForClientHydration(page);
    await page.getByLabel("Search participants").fill(fixture.event.name);
    await page.getByRole("button", { name: "Search", exact: true }).click();
    await page.waitForURL(/q=/);
    await waitForText(page, fixture.event.name);
    await page.getByLabel("Filter participants by status").click();
    await page.getByRole("option", { name: "Completed", exact: true }).click();
    await page.waitForURL(/status=Completed/);
    await waitForText(page, "Completed");
    await page
      .getByRole("button", { name: "Clear participant filters", exact: true })
      .click();
    await page.waitForURL(
      (url) => !url.searchParams.has("q") && !url.searchParams.has("status"),
    );
    console.log("[testing-lab-browser-e2e] mobile public and manager surfaces");
    await page.setViewportSize({ width: 390, height: 844 });
    await visit(
      page,
      "/testing-lab/events",
      "mobile public Testing Lab directory",
    );
    await waitForText(page, fixture.event.name);
    await assertNoViewportOverflow(page, "mobile public Testing Lab directory");
    await page.screenshot({
      path: path.join(artifactsDirectory, "public-directory-mobile.png"),
      fullPage: true,
    });
    await visit(
      page,
      `/testing-lab/events/${fixture.event.id}`,
      "mobile public Testing Lab event",
    );
    await waitForText(page, "Schedules and tester capacity");
    await assertNoViewportOverflow(page, "mobile public Testing Lab event");
    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}`,
      "mobile Testing Lab manager event",
    );
    await waitForText(page, fixture.event.name);
    await assertNoViewportOverflow(page, "mobile Testing Lab manager event");
    await page.screenshot({
      path: path.join(artifactsDirectory, "manager-event-mobile.png"),
      fullPage: true,
    });

    console.log(
      "[testing-lab-browser-e2e] event cancellation and read-only history",
    );
    await page.setViewportSize({ width: 1440, height: 1000 });
    await visit(
      page,
      `/workspace/testing-lab/events/${fixture.event.id}/overview`,
      "Testing Lab event cancellation",
    );
    await waitForClientHydration(page);
    await page
      .getByRole("button", { name: "Cancel event", exact: true })
      .click();
    actionDialog = page.getByRole("dialog");
    await actionDialog
      .getByLabel("Cancellation reason")
      .fill("Browser E2E lifecycle verification completed.");
    await actionDialog
      .getByRole("button", { name: "Cancel event", exact: true })
      .click();
    await waitForText(page, "Event status updated.");
    await actionDialog.waitFor({ state: "hidden" });
    await page.reload({ waitUntil: "domcontentloaded" });
    await waitForText(
      page,
      "This event is read-only. Its audit history remains available.",
    );
    await waitForText(page, "Cancelled");
    eventCancelled = true;
    throwForBrowserQualityFailures(quality);

    console.log(
      `Testing Lab browser E2E passed for ${fixture.tag}. Artifacts: ${artifactsDirectory}`,
    );
  } catch (error) {
    const failedPage = activePage ?? page;
    const pageText = await failedPage
      .locator("body")
      .innerText()
      .catch(() => "Unable to read page body.");
    const failureLabel = activePageLabel
      .replace(/[^a-z0-9]+/gi, "-")
      .replace(/(^-|-$)/g, "")
      .toLowerCase();
    await failedPage
      .screenshot({
        path: path.join(
          artifactsDirectory,
          `failure-${failureLabel || "page"}.png`,
        ),
        fullPage: true,
      })
      .catch(() => undefined);
    console.error(
      `[testing-lab-browser-e2e] failed in ${activePageLabel} at ${failedPage.url()}`,
    );
    console.error(
      `[testing-lab-browser-e2e] HTTP failures: ${[...new Set(quality.failedResponses)].join(", ") || "none"}`,
    );
    console.error(
      `[testing-lab-browser-e2e] browser errors: ${[...new Set(quality.browserErrors)].join(" | ") || "none"}`,
    );
    console.error(
      `[testing-lab-browser-e2e] accessibility failures: ${[...new Set(quality.accessibilityFailures)].join(" | ") || "none"}`,
    );
    console.error(
      `[testing-lab-browser-e2e] viewport failures: ${[...new Set(quality.viewportFailures)].join(" | ") || "none"}`,
    );
    console.error(
      `[testing-lab-browser-e2e] page excerpt:\n${pageText.slice(0, 2600)}`,
    );
    throw error;
  } finally {
    if (ownerContext) await ownerContext.close();
    if (reviewerContext) await reviewerContext.close();
    if (testerContext) await testerContext.close();
    const cleanupFailures = eventCancelled
      ? []
      : await cleanupTestingLabFixture(
          { eventId: fixture.event.id },
          (pathname, init) =>
            apiRequest(pathname, init, fixture.accessToken, fixture.tenantId),
        );
    if (cleanupFailures.length > 0) {
      console.error(
        `[testing-lab-browser-e2e] fixture cleanup failed:\n${cleanupFailures.join("\n")}`,
      );
      process.exitCode = 1;
    }
    await browser.close();
  }
}

run().catch((error) => {
  console.error(
    error instanceof Error ? (error.stack ?? error.message) : error,
  );
  process.exit(1);
});
