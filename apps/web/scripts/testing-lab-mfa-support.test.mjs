import assert from "node:assert/strict";
import test from "node:test";
import { createTestingLabMfaSignIn } from "./testing-lab-mfa-support.mjs";

test("API continuation sends only native challenge data to anonymous MFA routes", async () => {
  const firstFactor = { requiresMfa: true, mfaToken: "limited", tenantId: "bound" };
  const calls = [];
  const bridge = createTestingLabMfaSignIn(async (first, send, account) => {
    assert.equal(first, firstFactor);
    assert.equal(account, "admin");
    await send("enrollment", { mfaToken: "limited" });
    return send("complete", { mfaToken: "limited", method: "Totp", code: "generated" });
  });
  const verified = { accessToken: "verified", tenantId: "bound" };
  const result = await bridge.api(firstFactor, async (route, init) => {
    calls.push({ route, method: init.method, body: JSON.parse(init.body) });
    return route.endsWith("complete") ? verified : { success: true };
  }, "admin");
  assert.equal(result, verified);
  assert.deepEqual(calls, [
    { route: "/v1/auth/mfa/sign-in/enrollment", method: "POST", body: { mfaToken: "limited" } },
    { route: "/v1/auth/mfa/sign-in/complete", method: "POST", body: { mfaToken: "limited", method: "Totp", code: "generated" } },
  ]);
});

test("ordinary browser sign-in leaves the form and response unchanged", async () => {
  const verified = { url: "/workspace" };
  const bridge = createTestingLabMfaSignIn(async first => first);
  assert.equal(await bridge.browser(verified, {}, "member"), verified);
});

function fakePage({ fail = false, recovery = false } = {}) {
  const calls = [];
  const page = {
    waitForResponse(predicate) {
      const route = calls.some(call => call[0] === "click") ? "/api/auth/signin/credentials" : "/api/auth/mfa/enrollment";
      assert.equal(predicate({ url: () => `http://example.test${route}`, request: () => ({ method: () => "POST" }) }), true);
      assert.equal(predicate({ url: () => `http://example.test${route}`, request: () => ({ method: () => "GET" }) }), false);
      calls.push(["listen", route]);
      return Promise.resolve({ ok: () => !fail, status: () => fail ? 401 : 200,
        json: async () => route.endsWith("enrollment") ? { success: true } : recovery ? { mfaEnrollmentBackupCodes: ["one-time"] } : { url: "/workspace" } });
    },
    getByRole(role, options) {
      assert.equal(role, "button");
      return { click: async () => calls.push(["click", options.name]) };
    },
    locator(selector) {
      assert.equal(selector, 'input[name="code"]');
      return { fill: async code => calls.push(["fill", code]) };
    },
  };
  return { page, calls };
}

test("browser MFA uses the visible enrollment and verification form controls", async () => {
  const fixture = fakePage();
  const bridge = createTestingLabMfaSignIn(async (_, send) => {
    await send("enrollment", { mfaToken: "limited" });
    return send("complete", { mfaToken: "limited", code: "generated" });
  });
  assert.deepEqual(await bridge.browser({ requiresMfa: true }, fixture.page, "admin"), { url: "/workspace" });
  assert.deepEqual(fixture.calls, [
    ["listen", "/api/auth/mfa/enrollment"], ["click", "Set up authenticator"],
    ["listen", "/api/auth/signin/credentials"], ["fill", "generated"], ["click", "Verify and sign in"],
  ]);
});

test("browser enrollment acknowledges recovery codes through the real control", async () => {
  const fixture = fakePage({ recovery: true });
  const bridge = createTestingLabMfaSignIn(async (_, send) => {
    await send("enrollment", { mfaToken: "limited" });
    return send("complete", { mfaToken: "limited", code: "generated" });
  });
  await bridge.browser({ requiresMfa: true }, fixture.page, "admin");
  assert.deepEqual(fixture.calls.at(-1), ["click", "I saved my codes — continue"]);
});

test("a denied browser factor fails once without retrying or returning credentials", async () => {
  const fixture = fakePage({ fail: true });
  const bridge = createTestingLabMfaSignIn(async (_, send) => send("enrollment", { mfaToken: "limited" }));
  await assert.rejects(bridge.browser({ requiresMfa: true }, fixture.page, "admin"), /HTTP 401/);
  assert.equal(fixture.calls.filter(call => call[0] === "click").length, 1);
});
