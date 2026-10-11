import { test, expect, request, type APIRequestContext } from "playwright/test";

// Feed regression e2e — soft-nav no-remount + infinite scroll.
// Skip unless E2E_RUN=1; needs the signed-in dev web app (see signIn helper).
test.skip(!process.env.E2E_RUN, "set E2E_RUN=1 (needs web on the configured baseURL)");

const WEB_ORIGIN = (
  process.env.PLAYWRIGHT_WEB_BASE_URL ?? "http://localhost:3000"
).replace(/\/$/, "");

async function signIn(api: APIRequestContext): Promise<string> {
  const csrf = await api.get(`${WEB_ORIGIN}/api/auth/csrf`);
  const csrfToken = (await csrf.json()).csrfToken as string;
  const res = await api.post(`${WEB_ORIGIN}/api/auth/signin`, {
    form: {
      csrfToken,
      email: process.env.SIGNIN_EMAIL ?? "admin@game-guild.com",
      password: process.env.SIGNIN_PASSWORD ?? "Admin123!",
    },
    headers: { Origin: WEB_ORIGIN },
  });
  const cookie = (res as unknown as { headers(): Record<string, string> }).headers()["set-cookie"]
    ?.split(";")[0];
  if (!cookie?.includes("session-token")) throw new Error(`sign-in failed: ${res.status()} ${cookie ?? "no cookie"}`);
  return cookie;
}

test.use({
  storageState: async ({ }, use) => {
    const state = await storageStateFixture();
    await use(state);
  },
});

async function storageStateFixture(): Promise<{ cookies: { name: string; value: string; domain: string; path: string; expires: number; httpOnly: boolean; secure: boolean; sameSite: "Strict" | "Lax" | "None" }[]; origins: [] }> {
  const api = await request.newContext();
  try {
    const cookie = await signIn(api);
    const [name, value] = cookie.split("=");
    return { cookies: [{ name, value, domain: new URL(WEB_ORIGIN).hostname, path: "/", expires: -1, httpOnly: true, secure: false, sameSite: "Lax" }], origins: [] };
  } finally {
    await api.dispose();
  }
}

test("tab switch keeps the shell mounted (soft nav, no full reload)", async ({ page }) => {
  await page.goto("/feed");
  await expect(page.getByRole("navigation", { name: "Social feed" })).toBeVisible();
  const shellInit = await page.evaluate(() => {
    if (!window.__shellInit) window.__shellInit = { t: Date.now() };
    return window.__shellInit.t;
  });
  let navigations = 0;
  await page.evaluate(() => {
    document.addEventListener("DOMContentLoaded", () => undefined);
    window.__navProbe = true;
  });
  page.on("domcontentloaded", () => void (navigations += 1));

  await page.getByRole("link", { name: "Following" }).click();
  await expect(page).toHaveURL(/\/feed\?tab=following/);
  await expect(page.getByRole("link", { name: "Following" })).toHaveAttribute("aria-current", "page");

  const after = await page.evaluate(() => window.__shellInit?.t);
  expect(after).toBe(shellInit);
  expect(navigations).toBe(0);
});

test("scrolling the feed loads the next page of posts", async ({ page, browser }) => {
  const context = await browser.newContext({ storageState: await storageStateFixture() });
  const probePage = await context.newPage();
  let paginates = false;
  try {
    const first = await probePage.request.get(`${WEB_ORIGIN}/api/social/feed?scope=for-you&take=10`);
    const body = (await first.json()) as { nextCursor?: string | null };
    paginates = typeof body.nextCursor === "string" && body.nextCursor.length > 0;
  } finally {
    await context.close();
  }
  test.skip(!paginates, "seeded for-you feed must paginate for this check");

  await page.goto("/feed");
  await expect(page.getByRole("navigation", { name: "Social feed" })).toBeVisible();
  await page.getByTestId("post-card").first().waitFor({ state: "visible" });

  const before = await page.getByTestId("post-card").count();
  // The app shell scrolls in #dashboard-main (h-svh layout), not the window.
  const paged = page.waitForResponse((r) => r.url().includes("/api/social/feed") && r.url().includes("cursor="), { timeout: 20_000 });
  await page.locator("#dashboard-main").evaluate((el) => el.scrollTo({ top: el.scrollHeight }));
  await paged;
  await expect.poll(() => page.getByTestId("post-card").count(), { timeout: 10_000 }).toBeGreaterThan(before);
});

declare global {
  interface Window {
    __shellInit?: { t: number };
    __navProbe?: boolean;
  }
}
