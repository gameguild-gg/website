#!/usr/bin/env node

import { chromium } from 'playwright';
import { writeBrowserEvidence } from './browser-smoke-evidence.mjs';

const baseUrl = (process.env.PUBLIC_E2E_BASE_URL ?? process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3011').replace(/\/$/, '');
const runAuthFlow = ['1', 'true', 'yes'].includes((process.env.PUBLIC_E2E_AUTH_FLOW ?? '').toLowerCase());
const evidencePath = process.env.PLAYWRIGHT_JSON_OUTPUT_NAME;

const publicRoutes = [
  ['Home', '/'],
  ['Courses', '/courses'],
  ['Programs', '/programs'],
  ['Testing Lab', '/testing-lab', 'Test. Play. Earn.'],
  ['Testing Lab introduction', '/testing-lab/landing', 'Game Testing Lab'],
  ['Launch Pad', '/launch-pad'],
  ['Projects', '/projects'],
  ['Community', '/community'],
  ['Jobs', '/jobs'],
  ['About', '/about'],
  ['Sign in', '/sign-in', 'Welcome back'],
  ['Sign up', '/sign-up', 'Create your GameGuild account'],
];

const footerRoutes = [
  ['Feed', '/feed'],
  ['Roadmap', '/about/roadmap'],
  ['Contributors', '/about/contributors'],
  ['Contact', '/contact'],
  ['Licenses', '/legal/licenses'],
  ['Terms', '/terms-of-service'],
  ['Privacy', '/polices/privacy'],
  ['Cookies', '/polices/cookies'],
];

const dashboardRoutes = [
  ['Learning courses dashboard', '/dashboard/learning/courses'],
  ['Testing Lab dashboard', '/dashboard/testing-lab'],
  ['Launch Pad dashboard', '/dashboard/launch-pad'],
  ['Community dashboard', '/dashboard/community'],
];

function routeUrl(route) {
  return `${baseUrl}${route}`;
}

async function assertNoErrorOverlay(page, label) {
  await page.waitForLoadState('networkidle', { timeout: 20_000 }).catch(() => undefined);
  await page.waitForFunction(() => document.body.innerText.trim().length > 0, undefined, { timeout: 20_000 });

  const body = await page.locator('body').innerText({ timeout: 10_000 });

  if (!body.trim()) {
    throw new Error(`${label}: rendered an empty body`);
  }

  if (/404|page not found|Unhandled Runtime Error|Build Error|Application error/i.test(body)) {
    throw new Error(`${label}: rendered an error surface`);
  }
}

async function assertRouteRenders(page, label, route) {
  await page.goto(routeUrl(route), { waitUntil: 'domcontentloaded' });
  await assertNoErrorOverlay(page, label);
}

async function waitForClientHydration(page) {
  await page.waitForLoadState('networkidle', { timeout: 20_000 }).catch(() => undefined);
  await page.locator('script[src*="/_next/static"]').first().waitFor({ timeout: 20_000 }).catch(() => undefined);
  await page.waitForTimeout(750);
}

async function assertSignedInDashboard(page, label) {
  await page.waitForURL('**/dashboard**', { timeout: 20_000 });
  await assertNoErrorOverlay(page, label);
  await page.getByRole('button', { name: /Open .* account menu/i }).waitFor({ timeout: 20_000 });
}

async function runRealAuthFlow(page) {
  const unique = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const email = `browser-smoke-${unique}@example.test`;
  const password = 'Str0ng!Passw0rd123!';

  await page.goto(routeUrl('/sign-up'), { waitUntil: 'domcontentloaded' });
  await waitForClientHydration(page);
  await page.getByLabel('Full Name').fill('Browser Smoke User');
  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await page.getByLabel('Confirm Password').fill(password);
  await page.getByRole('button', { name: 'Create Account' }).click();
  await assertSignedInDashboard(page, 'Sign-up redirect');

  const accountMenu = page.getByRole('button', { name: /Open .* account menu/i });
  await accountMenu.click();
  await page.getByRole('menuitem', { name: 'Sign out' }).click();
  await page.waitForURL('**/sign-in', { timeout: 20_000 });
  await page.getByRole('heading', { name: 'Welcome back', exact: true, level: 1 }).waitFor({ timeout: 20_000 });

  await page.getByLabel('Email').fill(email);
  await page.getByLabel('Password', { exact: true }).fill(password);
  await waitForClientHydration(page);
  await page.getByRole('button', { name: 'Sign in' }).click();
  await assertSignedInDashboard(page, 'Sign-in redirect');

  for (const [label, route] of dashboardRoutes) {
    await assertRouteRenders(page, label, route);
  }
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
  });
  const page = await context.newPage();
  const consoleErrors = [];
  const requestErrors = [];

  page.on('console', (message) => {
    if (message.type() === 'error') {
      consoleErrors.push(message.text());
    }
  });
  page.on('pageerror', (error) => {
    consoleErrors.push(error.message);
  });
  page.on('requestfailed', (request) => {
    const failure = request.failure()?.errorText ?? 'unknown request failure';
    const isBlockedGoogleIdentityButton =
      request.url().startsWith('https://accounts.google.com/gsi/button?') &&
      /ERR_BLOCKED_BY_RESPONSE/i.test(failure);

    if (!/ERR_ABORTED/i.test(failure) && !isBlockedGoogleIdentityButton) {
      requestErrors.push(`${request.method()} ${request.url()}: ${failure}`);
    }
  });
  page.on('response', (response) => {
    const status = response.status();
    const request = response.request();
    const isRefusedCloudflarePrefetch =
      status === 503 &&
      request.headers()['sec-purpose'] === 'prefetch' &&
      response.headers()['cf-speculation-refused']?.startsWith('prefetch refused:');

    if ((status === 404 || status >= 500) && !isRefusedCloudflarePrefetch) {
      requestErrors.push(`${response.request().method()} ${response.url()}: HTTP ${response.status()}`);
    }
  });

  try {
    for (const [label, route, heading] of publicRoutes) {
      await assertRouteRenders(page, label, route);
      if (heading) {
        await page.getByRole('heading', { name: heading, exact: true, level: 1 }).waitFor();
      }
    }

    for (const [label, route] of footerRoutes) {
      await assertRouteRenders(page, label, route);
    }

    await page.goto(routeUrl('/'), { waitUntil: 'domcontentloaded' });
    await waitForClientHydration(page);
    const desktopNav = page.getByRole('navigation', { name: 'Main navigation' });
    await desktopNav.getByRole('button', { name: 'Learn', exact: true }).click();
    await page.getByRole('menu').getByRole('menuitem', { name: 'Courses', exact: true }).click();
    await page.waitForURL('**/courses');
    await page.getByRole('heading', { name: /Build the game development portfolio/i }).waitFor();

    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(routeUrl('/'), { waitUntil: 'domcontentloaded' });
    await waitForClientHydration(page);
    await page.getByRole('button', { name: 'Open public navigation' }).click();
    await page.getByRole('navigation', { name: 'Mobile navigation' }).getByRole('link', { name: 'Testing Lab' }).click();
    await page.waitForURL('**/testing-lab');
    await page.getByRole('heading', { name: 'Test. Play. Earn.', exact: true, level: 1 }).waitFor();

    await page.setViewportSize({ width: 1280, height: 900 });
    await page.goto(routeUrl('/sign-in'), { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: 'Welcome back', exact: true, level: 1 }).waitFor();
    await page.getByLabel('Email').waitFor();
    await page.getByLabel('Password', { exact: true }).waitFor();

    await page.goto(routeUrl('/sign-up'), { waitUntil: 'domcontentloaded' });
    await page.getByRole('heading', { name: /Create your GameGuild account/i }).waitFor();
    await page.getByLabel('Full Name').waitFor();
    await page.getByLabel('Email').waitFor();
    await page.getByLabel('Password', { exact: true }).waitFor();
    await page.getByLabel('Confirm Password').waitFor();

    if (runAuthFlow) {
      await runRealAuthFlow(page);
    }

    if (requestErrors.length > 0) {
      throw new Error(`Failed browser requests detected:\n${requestErrors.join('\n')}`);
    }

    if (consoleErrors.length > 0) {
      const ignoredGoogleIdentityErrors = [
        "Provider's accounts list is empty.",
        'Not signed in with the identity provider.',
      ];
      const relevantErrors = consoleErrors.filter(
        (message) =>
          !/favicon/i.test(message) &&
          !ignoredGoogleIdentityErrors.includes(message) &&
          !message.startsWith('[GSI_LOGGER]: FedCM get() rejects with NetworkError:'),
      );

      if (relevantErrors.length > 0) {
        throw new Error(`Console/page errors detected:\n${relevantErrors.join('\n')}`);
      }
    }

    await writeBrowserEvidence(evidencePath, { passed: true, errors: [] });
    console.log(`Public browser smoke passed against ${baseUrl}`);
  } catch (error) {
    const headings = await page.getByRole('heading').allTextContents().catch(() => []);
    console.error(JSON.stringify({
      path: new URL(page.url()).pathname,
      headings: headings.slice(0, 6).map((heading) => heading.trim().slice(0, 256)),
    }));
    throw error;
  } finally {
    await browser.close();
  }
}

main().catch(async (error) => {
  const message = error instanceof Error ? error.message : String(error);
  await writeBrowserEvidence(evidencePath, { passed: false, errors: [message] });
  console.error(message);
  process.exitCode = 1;
});
