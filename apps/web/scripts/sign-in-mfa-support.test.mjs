import assert from "node:assert/strict";
import { test } from "node:test";
import {
  completeMfaSignInWithReplayRetry,
  createMfaSignInContinuation,
  generateTotpCode,
} from "./sign-in-mfa-support.mjs";

// Public RFC 6238 Appendix B SHA-1 test key (ASCII "12345678901234567890").
const rfc6238PublicTestKey = "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ";
const mfaToken = "x".repeat(43);
test("RFC 6238 SHA-1 vectors independently produce native six-digit TOTP", () => {
  for (const [seconds, expected] of [
    [59, "287082"],
    [1111111109, "081804"],
    [1111111111, "050471"],
    [1234567890, "005924"],
    [2000000000, "279037"],
    [20000000000, "353130"],
  ]) {
    assert.equal(
      generateTotpCode(rfc6238PublicTestKey, seconds * 1000),
      expected,
    );
  }
});
test("malformed setup keys cannot become authenticator proofs", () => {
  for (const key of ["", "NOT BASE32!", undefined])
    assert.throws(() => generateTotpCode(key));
});
test("ordinary sign-in remains unchanged without requesting MFA", async () => {
  const response = { accessToken: "verified" };
  assert.equal(
    await createMfaSignInContinuation()(
      response,
      () => {
        throw new Error("unexpected MFA");
      },
      "account",
    ),
    response,
  );
});
test("completion sends only server-bound challenge data", async () => {
  const calls = [];
  const continuation = createMfaSignInContinuation({
    secrets: new Map([["account", rfc6238PublicTestKey]]),
    now: () => 59000,
  });
  const response = await continuation(
    { requiresMfa: true, mfaToken, tenantId: "untrusted", userId: "untrusted" },
    async (action, body) => {
      calls.push({ action, body });
      return { accessToken: "verified" };
    },
    "account",
  );
  assert.deepEqual(response, { accessToken: "verified" });
  assert.deepEqual(calls, [
    { action: "complete", body: { mfaToken, method: "Totp", code: "287082" } },
  ]);
});
test("second sign-in waits for a new counter instead of bypassing replay protection", async () => {
  let clock = 59000;
  const waits = [],
    codes = [];
  const continuation = createMfaSignInContinuation({
    secrets: new Map([["account", rfc6238PublicTestKey]]),
    now: () => clock,
    wait: async (ms) => {
      waits.push(ms);
      clock += ms;
    },
  });
  const send = async (action, body) => {
    assert.equal(action, "complete");
    codes.push(body.code);
    return { user: { id: "verified" } };
  };
  await continuation({ error: "MfaRequired", mfaToken }, send, "account");
  await continuation({ error: "MfaRequired", mfaToken }, send, "account");
  assert.deepEqual(waits, [1000]);
  assert.notEqual(codes[0], codes[1]);
});
test("rejected replay gets one fresh challenge in the next TOTP counter", async () => {
  let clock = 59000;
  const challenges = [];
  const codes = [];
  const waits = [];
  const continuation = createMfaSignInContinuation({
    secrets: new Map([["account", rfc6238PublicTestKey]]),
    now: () => clock,
    wait: async (ms) => {
      waits.push(ms);
      clock += ms;
    },
  });

  const result = await completeMfaSignInWithReplayRetry({
    accountKey: "account",
    begin: async () => {
      const sequence = challenges.length + 1;
      const token = `${"x".repeat(42)}${sequence}`;
      challenges.push(token);
      return { error: "MfaRequired", mfaToken: token };
    },
    continuation,
    isReplayFailure: (error) => error.status === 401,
    send: async (_action, body) => {
      codes.push(body.code);
      if (codes.length === 1) {
        const error = new Error("replayed TOTP");
        error.status = 401;
        throw error;
      }
      return { accessToken: "verified" };
    },
  });

  assert.deepEqual(result, { accessToken: "verified" });
  assert.equal(challenges.length, 2);
  assert.deepEqual(waits, [1000]);
  assert.notEqual(codes[0], codes[1]);
});
test("non-replay MFA failures are not retried", async () => {
  let challenges = 0;
  await assert.rejects(
    completeMfaSignInWithReplayRetry({
      accountKey: "account",
      begin: async () => {
        challenges += 1;
        return { error: "MfaRequired", mfaToken };
      },
      continuation: createMfaSignInContinuation({
        secrets: new Map([["account", rfc6238PublicTestKey]]),
        now: () => 59000,
      }),
      isReplayFailure: (error) => error.status === 401,
      send: async () => {
        const error = new Error("service unavailable");
        error.status = 503;
        throw error;
      },
    }),
    /service unavailable/,
  );
  assert.equal(challenges, 1);
});
test("denied completion or a missing secret never returns an authenticated response", async () => {
  await assert.rejects(
    createMfaSignInContinuation({
      secrets: new Map([["account", rfc6238PublicTestKey]]),
    })(
      { requiresMfa: true, mfaToken },
      async () => ({ error: "Denied" }),
      "account",
    ),
  );
  await assert.rejects(
    createMfaSignInContinuation()(
      { requiresMfa: true, mfaToken },
      async () => ({ success: true }),
      "account",
    ),
  );
});
test("accepts a challenge for an elevated-role account with a provisioned secret", async () => {
  const bridge = createMfaSignInContinuation({
    secrets: new Map([
      ["admin@game-guild.com", "GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ"],
    ]),
  });
  const response = { accessToken: "verified" };
  assert.deepEqual(
    await bridge(
      {
        requiresMfa: true,
        mfaToken: "x".repeat(43),
        tenantId: "bound",
        userId: "bound",
      },
      async () => ({
        accessToken: "verified",
        tenantId: "bound",
        userId: "bound",
      }),
      "admin@game-guild.com",
    ),
    { ...response, tenantId: "bound", userId: "bound" },
  );
});

test("SignInForm keeps void callbacks without obsolete promise suppressions", async () => {
  const { readFile } = await import("node:fs/promises");
  const [baselineText, form] = await Promise.all([
    readFile(new URL("../eslint-suppressions.json", import.meta.url), "utf8"),
    readFile(
      new URL("../src/components/sign-in-form.tsx", import.meta.url),
      "utf8",
    ),
  ]);
  assert.equal(
    Object.hasOwn(JSON.parse(baselineText), "src/components/sign-in-form.tsx"),
    false,
  );
  assert.match(
    form,
    /onSubmit=\{\(event\) => \{\s+void handleMfaSubmit\(event\);\s+\}\}/,
  );
  assert.match(
    form,
    /onSubmit=\{\(event\) => \{\s+void handleSubmit\(event\);\s+\}\}/,
  );
});
