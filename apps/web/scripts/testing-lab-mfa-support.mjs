import { createMfaSignInContinuation } from "./sign-in-mfa-support.mjs";

/** Completes native MFA for disposable Testing Lab accounts without bypassing the sign-in form. */
export function createTestingLabMfaSignIn(continuation = createMfaSignInContinuation()) {
  return {
    api(firstFactor, request, accountKey) {
      return continuation(firstFactor, (action, body) => request(`/v1/auth/mfa/sign-in/${action}`, {
        method: "POST", body: JSON.stringify(body),
      }), accountKey);
    },
    async browser(firstFactor, page, accountKey) {
      const completed = await continuation(firstFactor, async (action, body) => {
        const endpoint = action === "enrollment" ? "/api/auth/mfa/enrollment" : "/api/auth/signin/credentials";
        const response = page.waitForResponse(candidate =>
          new URL(candidate.url()).pathname === endpoint && candidate.request().method() === "POST");
        if (action === "enrollment") {
          await page.getByRole("button", { name: "Set up authenticator", exact: true }).click();
        } else {
          await page.locator('input[name="code"]').fill(body.code);
          await page.getByRole("button", { name: "Verify and sign in", exact: true }).click({ noWaitAfter: true });
        }
        const result = await response;
        if (!result.ok()) throw new Error(`Testing Lab browser MFA ${action} failed (HTTP ${result.status()})`);
        return result.json();
      }, accountKey);
      if (completed.mfaEnrollmentBackupCodes?.length) {
        await page.getByRole("button", { name: "I saved my codes — continue", exact: true }).click();
      }
      return completed;
    },
  };
}
