import assert from "node:assert/strict";
import { statSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import path from "node:path";
import test from "node:test";
import { resolveDevelopmentDataProtectionEnvironment } from "./dev-data-protection.mjs";

function temporaryDirectory() {
  return mkdtempSync(path.join(tmpdir(), "gameguild-data-protection-test-"));
}

test("keeps an explicitly configured certificate pair", () => {
  const directory = temporaryDirectory();
  try {
    const environment = resolveDevelopmentDataProtectionEnvironment({
      environment: {
        DATAPROTECTION_CERTIFICATE_BASE64: "certificate",
        DATAPROTECTION_CERTIFICATE_KEY_BASE64: "key",
      },
      certificateDirectory: directory,
      executeOpenSsl: () => {
        throw new Error("OpenSSL must not run for explicit configuration");
      },
    });

    assert.equal(environment.DATAPROTECTION_CERTIFICATE_BASE64, "certificate");
    assert.equal(environment.DATAPROTECTION_CERTIFICATE_KEY_BASE64, "key");
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("rejects a partially configured certificate pair", () => {
  const directory = temporaryDirectory();
  try {
    assert.throws(
      () =>
        resolveDevelopmentDataProtectionEnvironment({
          environment: {
            DATAPROTECTION_CERTIFICATE_BASE64: "certificate",
          },
          certificateDirectory: directory,
        }),
      /must be configured together/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("generates and reuses a private local certificate pair", () => {
  const directory = temporaryDirectory();
  const messages = [];
  try {
    const first = resolveDevelopmentDataProtectionEnvironment({
      environment: {},
      certificateDirectory: directory,
      log: (message) => messages.push(message),
    });
    const second = resolveDevelopmentDataProtectionEnvironment({
      environment: {},
      certificateDirectory: directory,
      log: (message) => messages.push(message),
    });

    assert.match(
      Buffer.from(first.DATAPROTECTION_CERTIFICATE_BASE64, "base64").toString(
        "utf8",
      ),
      /BEGIN CERTIFICATE/,
    );
    assert.match(
      Buffer.from(
        first.DATAPROTECTION_CERTIFICATE_KEY_BASE64,
        "base64",
      ).toString("utf8"),
      /BEGIN PRIVATE KEY/,
    );
    assert.equal(
      second.DATAPROTECTION_CERTIFICATE_BASE64,
      first.DATAPROTECTION_CERTIFICATE_BASE64,
    );
    assert.equal(
      second.DATAPROTECTION_CERTIFICATE_KEY_BASE64,
      first.DATAPROTECTION_CERTIFICATE_KEY_BASE64,
    );
    assert.equal(messages.length, 1);

    if (process.platform !== "win32") {
      assert.equal(
        statSync(path.join(directory, "key.pem")).mode & 0o777,
        0o600,
      );
    }
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
