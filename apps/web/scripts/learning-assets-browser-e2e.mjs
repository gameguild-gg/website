#!/usr/bin/env node

import { randomUUID } from "node:crypto";
import { chromium } from "playwright";
import {
  waitForAuthenticatedNavigation,
  waitForInteractiveCredentials,
} from "./learning-browser-e2e-support.mjs";

const apiBaseUrl = (process.env.API_BASE_URL ?? "http://127.0.0.1:8080").replace(/\/$/, "");
const webBaseUrl = (process.env.LEARNING_ASSETS_E2E_BASE_URL ?? "http://gameguild.localhost:3011").replace(/\/$/, "");
const password = "Str0ng!Passw0rd123!";

function uniqueTag() {
  return `${Date.now()}-${randomUUID().replaceAll("-", "").slice(0, 6)}`;
}

async function apiResponse(path, init = {}, accessToken) {
  return fetch(`${apiBaseUrl}${path}`, {
    ...init,
    headers: {
      ...(init.body instanceof FormData ? {} : { "content-type": "application/json" }),
      ...(accessToken ? { authorization: `Bearer ${accessToken}` } : {}),
      ...init.headers,
    },
  });
}

async function apiRequest(path, init = {}, accessToken) {
  const response = await apiResponse(path, init, accessToken);
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(`${init.method ?? "GET"} ${path} failed with ${response.status}: ${JSON.stringify(body)}`);
  }
  return body;
}

async function signIn(page, email) {
  console.log(`[learning-assets-e2e] signing in ${email}`);
  await page.goto(`${webBaseUrl}/sign-in`, { waitUntil: "domcontentloaded" });
  const form = await waitForInteractiveCredentials(page, { timeoutMs: 180_000 });
  await form.email.fill(email);
  await form.password.fill(password);
  await form.submit.click();
  try {
    await waitForAuthenticatedNavigation(page, { timeoutMs: 180_000 });
  } catch (error) {
    const cookies = await page.context().cookies();
    const body = await page.locator("body").innerText().catch(() => "");
    throw new Error(`Web sign-in failed at ${page.url()} with cookies [${cookies.map((cookie) => cookie.name).join(", ")}]. ${body.slice(0, 1200)}`, { cause: error });
  }
  let lastNavigationError;
  for (let attempt = 0; attempt < 4; attempt += 1) {
    try {
      await page.waitForLoadState("domcontentloaded").catch(() => undefined);
      // This route contains no application JavaScript. A unique query prevents
      // Playwright from treating a retry as a same-document navigation.
      const stableResponse = await page.goto(`${webBaseUrl}/api/health?learning-assets-e2e=${Date.now()}-${attempt}`, { waitUntil: "load" });
      if (stableResponse?.ok()) return;
      lastNavigationError = new Error(`Health route returned ${stableResponse?.status() ?? "no response"}.`);
    } catch (error) {
      lastNavigationError = error;
    }
  }
  throw new Error("Could not stabilize the authenticated browser page.", { cause: lastNavigationError });
}

async function pollAsset(assetId, accessToken) {
  const deadline = Date.now() + 60_000;
  let last;
  while (Date.now() < deadline) {
    last = await apiRequest(`/v1/assets/${assetId}?includeContent=true`, {}, accessToken);
    const virus = last?.content?.virusScanStatus;
    const moderation = last?.content?.moderationStatus;
    if (virus === "Clean" && ["Approved", "ApprovedWithWarning"].includes(moderation)) return last;
    await new Promise((resolve) => setTimeout(resolve, 250));
  }
  throw new Error(`Asset processing did not complete: ${JSON.stringify(last)}`);
}

async function run() {
  const tag = uniqueTag();
  const teacherEmail = `learning-assets-teacher-${tag}@example.test`;
  const studentEmail = `learning-assets-student-${tag}@example.test`;
  const teacher = await apiRequest("/v1/auth/sign-up", {
    method: "POST",
    body: JSON.stringify({ username: `learning_assets_teacher_${tag.replace(/[^a-z0-9]/gi, "_")}`, email: teacherEmail, password }),
  });
  const student = await apiRequest("/v1/auth/sign-up", {
    method: "POST",
    body: JSON.stringify({
      username: `learning_assets_student_${tag.replace(/[^a-z0-9]/gi, "_")}`,
      email: studentEmail,
      password,
      tenantId: teacher.tenantId,
    }),
  });
  if (!teacher.accessToken || !student.accessToken) throw new Error("Fixture sign-up did not return access tokens.");
  console.log(`[learning-assets-e2e] identities ready (teacher ${teacher.userId ?? teacher.user?.id}, tenant ${teacher.tenantId})`);

  const slug = `learning-assets-${tag}`;
  const course = await apiRequest("/v1/courses", {
    method: "POST",
    body: JSON.stringify({ title: `Learning Assets ${tag}`, description: "Learning and Assets production integration fixture.", slug }),
  }, teacher.accessToken);
  const lesson = await apiRequest(`/v1/courses/${course.id}/content`, {
    method: "POST",
    body: JSON.stringify({
      programId: course.id,
      title: "Portable media lesson",
      slug: "portable-media-lesson",
      description: "Remote media must survive browser contexts.",
      type: "Lesson",
      body: "Initial lesson",
      lessonFormat: "Markdown",
      sortOrder: 0,
      isRequired: true,
      estimatedMinutes: 5,
      estimatedMinutesSource: "Manual",
      visibility: "Public",
    }),
  }, teacher.accessToken);
  await apiRequest(`/v1/courses/${course.id}/users:enroll`, {
    method: "POST",
    body: JSON.stringify({ userReference: studentEmail }),
  }, teacher.accessToken);
  console.log(`[learning-assets-e2e] course ${course.id} (creator ${course.creatorId}) and lesson ${lesson.id} ready`);

  const browser = await chromium.launch({ headless: true });
  const firstContext = await browser.newContext();
  const secondContext = await browser.newContext();
  let assetId;
  try {
    const firstPage = await firstContext.newPage();
    const secondPage = await secondContext.newPage();
    firstPage.setDefaultNavigationTimeout(180_000);
    secondPage.setDefaultNavigationTimeout(180_000);
    firstPage.setDefaultTimeout(180_000);
    secondPage.setDefaultTimeout(180_000);
    await signIn(firstPage, teacherEmail);
    await secondContext.addCookies(await firstContext.cookies());
    const secondOrigin = await secondPage.goto(`${webBaseUrl}/api/health?learning-assets-e2e=restored-session`, { waitUntil: "load" });
    if (!secondOrigin?.ok()) throw new Error(`Second browser context could not open the Web origin (${secondOrigin?.status() ?? "no response"}).`);
    const readSession = async () => {
      const response = await fetch("/api/auth/session", { cache: "no-store" });
      return { status: response.status, body: await response.json().catch(() => null) };
    };
    const [browserSession, restoredSession] = await Promise.all([firstPage.evaluate(readSession), secondPage.evaluate(readSession)]);
    if (browserSession.status !== 200 || restoredSession.status !== 200 || browserSession.body?.user?.id !== restoredSession.body?.user?.id) {
      throw new Error(`Browser session restoration failed: ${JSON.stringify({ browserSession, restoredSession })}`);
    }
    console.log(`[learning-assets-e2e] browser sessions ${JSON.stringify({ browserSession, restoredSession })}`);

    assetId = crypto.randomUUID();
    const upload = await firstPage.evaluate(async ({ assetId, lessonId }) => {
      const bytes = Uint8Array.from(atob("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZQmcAAAAASUVORK5CYII="), (value) => value.charCodeAt(0));
      const data = new FormData();
      data.set("file", new File([bytes], "lesson.png", { type: "image/png" }));
      const query = new URLSearchParams({
        referenceId: assetId,
        displayName: "lesson.png",
        accessPolicy: "Private",
        parentResourceType: "ProgramContent",
        parentResourceId: lessonId,
      });
      const response = await fetch(`/api/assets?${query}`, { method: "POST", body: data });
      return { status: response.status, body: await response.json().catch(() => null) };
    }, { assetId, lessonId: lesson.id });
    if (upload.status !== 201 || upload.body?.assetReferenceId !== assetId) {
      throw new Error(`Stable-id browser upload failed: ${JSON.stringify(upload)}`);
    }
    await pollAsset(assetId, teacher.accessToken);

    const studentPrivate = await apiResponse(`/v1/assets/${assetId}?includeContent=true`, {}, student.accessToken);
    if (![403, 404].includes(studentPrivate.status)) {
      throw new Error(`A learner read a private authoring asset (${studentPrivate.status}).`);
    }

    const draft = await apiRequest(`/v1/courses/${course.id}/content/${lesson.id}/authoring`, {}, teacher.accessToken);
    const saved = await apiRequest(`/v1/courses/${course.id}/content/${lesson.id}/authoring`, {
      method: "PUT",
      body: JSON.stringify({
        revision: draft.revision,
        payload: { ...draft.payload, body: `# Portable media\n\n![Remote lesson asset](asset://${assetId})` },
      }),
    }, teacher.accessToken);

    const secondBrowserRead = await secondPage.evaluate(async ({ assetId, lessonId }) => {
      const list = await fetch(`/api/assets?resourceType=ProgramContent&resourceId=${lessonId}`);
      const listBody = await list.json().catch(() => null);
      const metadata = await fetch(`/api/assets/${assetId}?includeContent=true`);
      const content = await fetch(`/api/assets/${assetId}/content`);
      return {
        listStatus: list.status,
        listed: listBody?.items?.some((item) => item.id === assetId) ?? false,
        metadataStatus: metadata.status,
        contentStatus: content.status,
        contentType: content.headers.get("content-type"),
        byteLength: (await content.arrayBuffer()).byteLength,
      };
    }, { assetId, lessonId: lesson.id });
    if (secondBrowserRead.listStatus !== 200 || !secondBrowserRead.listed || secondBrowserRead.metadataStatus !== 200 || secondBrowserRead.contentStatus !== 200 || secondBrowserRead.byteLength === 0) {
      throw new Error(`The second browser context could not restore the remote asset: ${JSON.stringify(secondBrowserRead)}`);
    }

    const inUseDelete = await secondPage.evaluate(async (assetId) => {
      const response = await fetch(`/api/assets/${assetId}`, { method: "DELETE" });
      return response.status;
    }, assetId);
    if (inUseDelete !== 400) throw new Error(`An asset referenced by the draft was deleted (${inUseDelete}).`);

    const published = await apiRequest(`/v1/courses/${course.id}/content/${lesson.id}/authoring/publish`, {
      method: "POST",
      body: JSON.stringify({ revision: saved.revision }),
    }, teacher.accessToken);
    const learnerMetadata = await apiResponse(`/v1/assets/${assetId}?includeContent=true`, {}, student.accessToken);
    const learnerContent = await apiResponse(`/api/assets/${assetId}/content`, {}, student.accessToken);
    if (learnerMetadata.status !== 200 || learnerContent.status !== 200 || (await learnerContent.arrayBuffer()).byteLength === 0) {
      throw new Error(`The enrolled learner could not consume the published asset (${learnerMetadata.status}/${learnerContent.status}).`);
    }

    const cleared = await apiRequest(`/v1/courses/${course.id}/content/${lesson.id}/authoring`, {
      method: "PUT",
      body: JSON.stringify({ revision: published.draft.revision, payload: { ...published.draft.payload, body: "# Media removed" } }),
    }, teacher.accessToken);
    await apiRequest(`/v1/courses/${course.id}/content/${lesson.id}/authoring/publish`, {
      method: "POST",
      body: JSON.stringify({ revision: cleared.revision }),
    }, teacher.accessToken);
    const learnerAfterRemoval = await apiResponse(`/v1/assets/${assetId}?includeContent=true`, {}, student.accessToken);
    if (![403, 404].includes(learnerAfterRemoval.status)) {
      throw new Error(`A learner retained access after the asset left the published lesson (${learnerAfterRemoval.status}).`);
    }
    const deleted = await secondPage.evaluate(async (assetId) => (await fetch(`/api/assets/${assetId}`, { method: "DELETE" })).status, assetId);
    if (deleted !== 204) throw new Error(`Single asset delete did not complete after usage removal (${deleted}).`);

    console.log(JSON.stringify({
      courseId: course.id,
      lessonId: lesson.id,
      assetId,
      checks: [
        "stable remote upload",
        "private authoring authorization",
        "cross-context persistence",
        "draft deletion guard",
        "published learner delivery",
        "learner revocation after removal",
        "single delete",
      ],
    }, null, 2));
  } finally {
    await firstContext.close();
    await secondContext.close();
    await browser.close();
    await apiResponse(`/v1/courses/${course.id}`, { method: "DELETE" }, teacher.accessToken).catch(() => undefined);
  }
}

run().catch((error) => {
  console.error(error instanceof Error ? error.stack : error);
  process.exitCode = 1;
});
