import assert from "node:assert/strict";
import test from "node:test";
import { createTestingLabMfaSignIn } from "./testing-lab-mfa-support.mjs";

test("API continuation sends only native challenge data to anonymous MFA routes", async () => {
  const firstFactor = { requiresMfa: true, mfaToken: "limited", tenantId: "bound" };
  const calls = [];
  const verified = { accessToken: "verified", tenantId: "bound" };
  const bridge = createTestingLabMfaSignIn(async (first, send, account) => {
    assert.equal(first, firstFactor);
    assert.equal(account, "admin");
    return send("complete", { mfaToken: "limited", method: "Totp", code: "generated" });
  });
  const result = await bridge.api(firstFactor, async (route, init) => {
    calls.push({ route, method: init.method, body: JSON.parse(init.body) });
    return verified;
  }, "admin");
  assert.equal(result, verified);
  assert.deepEqual(calls, [
    { route: "/v1/auth/mfa/sign-in/complete", method: "POST", body: { mfaToken: "limited", method: "Totp", code: "generated" } },
  ]);
});

test("ordinary browser sign-in leaves the form and response unchanged", async () => {
  const verified = { url: "/workspace" };
  const bridge = createTestingLabMfaSignIn(async first => first);
  assert.equal(await bridge.browser(verified, {}, "member"), verified);
});

function fakePage({ fail = false } = {}) {
  const calls = [];
  const page = {
    waitForResponse(predicate) {
      const route = "/api/auth/signin/credentials";
      assert.equal(predicate({ url: () => `http://example.test${route}`, request: () => ({ method: () => "POST" }) }), true);
      assert.equal(predicate({ url: () => `http://example.test${route}`, request: () => ({ method: () => "GET" }) }), false);
      calls.push(["listen", route]);
      return Promise.resolve({ ok: () => !fail, status: () => fail ? 401 : 200,
        json: async () => ({ url: "/workspace" }) });
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

test("browser MFA uses the visible verification form controls", async () => {
  const fixture = fakePage();
  const bridge = createTestingLabMfaSignIn(async (_, send) =>
    send("complete", { mfaToken: "limited", code: "generated" }));
  assert.deepEqual(await bridge.browser({ requiresMfa: true }, fixture.page, "admin"), { url: "/workspace" });
  assert.deepEqual(fixture.calls, [
    ["listen", "/api/auth/signin/credentials"], ["fill", "generated"], ["click", "Verify and sign in"],
  ]);
});

test("a denied browser factor fails once without retrying or returning credentials", async () => {
  const fixture = fakePage({ fail: true });
  const bridge = createTestingLabMfaSignIn(async (_, send) => send("complete", { mfaToken: "limited" }));
  await assert.rejects(bridge.browser({ requiresMfa: true }, fixture.page, "admin"), /HTTP 401/);
  assert.equal(fixture.calls.filter(call => call[0] === "click").length, 1);
});

test("rejects a challenge for an account without a provisioned secret", async () => {
  const bridge = createTestingLabMfaSignIn();
  await assert.rejects(
    bridge.api({ requiresMfa: true, mfaToken: "x".repeat(43) }, async () => assert.fail("should not send"), "unknown"),
    /enroll 2FA in settings/,
  );
});
