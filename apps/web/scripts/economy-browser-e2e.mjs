#!/usr/bin/env node

import { chromium } from 'playwright';
import { writeBrowserEvidence } from './browser-smoke-evidence.mjs';

const baseUrl = (process.env.PUBLIC_E2E_BASE_URL ?? process.env.NEXT_PUBLIC_APP_URL ?? 'http://localhost:3011').replace(/\/$/, '');
const evidencePath = process.env.PLAYWRIGHT_JSON_OUTPUT_NAME;

const publicRoutes = [
  ['Marketplace', '/marketplace', /Learn, build, and hire from trusted creators/i],
  ['Marketplace cart', '/marketplace/cart', /^Cart$/i],
  ['Marketplace checkout', '/marketplace/checkout', /^Checkout$/i],
];

const protectedRoutes = [
  '/workspace/economy',
  '/workspace/economy/wallet',
  '/workspace/economy/top-ups',
  '/workspace/economy/transfers',
  '/workspace/economy/kyc',
  '/workspace/economy/payouts',
  '/workspace/economy/bounties',
  '/workspace/economy/ad-rewards',
  '/workspace/economy/orders',
  '/workspace/economy/marketplace/seller',
  '/console/economy',
  '/console/economy/payout-reviews',
  '/console/economy/payout-operations',
  '/console/economy/risk-reviews',
  '/console/economy/compliance/financial-crime',
  '/console/economy/compliance/trust-safety',
  '/console/economy/policies',
  '/console/economy/reserves',
  '/console/economy/ledger',
  '/console/economy/kill-switches',
  '/console/economy/ad-rewards',
  '/console/economy/marketplace',
  '/console/economy/bounties',
  '/console/economy/treasury',
  '/console/economy/legacy-migration',
];

async function assertRendered(page, label, route, heading) {
  const response = await page.goto(`${baseUrl}${route}`, { waitUntil: 'domcontentloaded' });
  if (!response || response.status() >= 500 || response.status() === 404) {
    throw new Error(`${label}: unexpected HTTP ${response?.status() ?? 'no response'}`);
  }
  await page.getByRole('heading', { name: heading }).first().waitFor({ timeout: 20_000 });
  const body = await page.locator('body').innerText();
  if (/Unhandled Runtime Error|Build Error|Application error/i.test(body)) {
    throw new Error(`${label}: rendered an application error surface`);
  }
}

async function assertProtected(page, route) {
  await page.goto(`${baseUrl}${route}`, { waitUntil: 'domcontentloaded' });
  await page.waitForURL(/\/sign-in(?:\?|$)/, { timeout: 20_000 });
  await page.getByRole('heading', { name: /^Welcome back$/i }).waitFor({ timeout: 20_000 });
}

async function main() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
  const page = await context.newPage();

  try {
    for (const [label, route, heading] of publicRoutes) {
      await assertRendered(page, label, route, heading);
    }
    for (const route of protectedRoutes) {
      await assertProtected(page, route);
    }

    await writeBrowserEvidence(evidencePath, { passed: true, errors: [] });
    console.log(`Economy browser surface passed against ${baseUrl}`);
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
