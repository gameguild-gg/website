import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import { randomBytes } from "node:crypto";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import { test } from "node:test";

const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "../../..");
const virtualStore = join(repositoryRoot, "node_modules/.pnpm");
const require = createRequire(import.meta.url);

function installedConsumers(name) {
  const roots = new Set();
  for (const entry of readdirSync(virtualStore, { withFileTypes: true })) {
    if (!entry.isDirectory() || entry.name.startsWith(`${name.replaceAll("/", "+")}@`)) continue;
    const candidate = join(virtualStore, entry.name, "node_modules", name);
    if (existsSync(candidate)) roots.add(realpathSync(candidate));
  }
  for (const candidate of [
    join(virtualStore, "node_modules", name),
    join(repositoryRoot, "node_modules", name),
  ]) {
    if (existsSync(candidate)) roots.add(realpathSync(candidate));
  }
  assert.ok(roots.size > 0, `The installed graph must contain ${name}`);
  return roots;
}

test("local and CI package managers use the reviewed pnpm security release", () => {
  const manifest = JSON.parse(readFileSync(join(repositoryRoot, "package.json"), "utf8"));
  assert.equal(manifest.packageManager, "pnpm@10.34.6");
  assert.equal(manifest.engines.pnpm, ">=10.34.6 <11");
  for (const file of [".github/actions/setup-node-workspace/action.yml", ".github/workflows/pr-verify.yml"]) {
    assert.match(readFileSync(join(repositoryRoot, file), "utf8"), /version: 10\.34\.6\b/);
  }
  assert.match(readFileSync(join(repositoryRoot, ".github/workflows/emception.yml"), "utf8"), /PNPM_VERSION: "10\.34\.6"/);
});

for (const packageRoot of installedConsumers("shell-quote")) {
  const shellQuote = require(packageRoot);
  test(`${packageRoot}: comment-following line terminators cannot escape quoting`, () => {
    assert.equal(require(join(packageRoot, "package.json")).version, "1.11.0");
    for (const terminator of ["\n", "\r", "\u2028", "\u2029"]) {
      assert.throws(
        () => shellQuote.quote(["echo", "safe", { comment: "fixture" }, `a${terminator}printf injected`]),
        TypeError,
      );
    }
    const ordinary = ["echo", "a b", "c'd", "value;literal"];
    assert.deepEqual(shellQuote.parse(shellQuote.quote(ordinary)), ordinary);
  });
}

for (const packageRoot of installedConsumers("sharp")) {
  const sharp = require(packageRoot);
  test(`${packageRoot}: fixed librsvg still renders ordinary SVG images`, async () => {
    assert.equal(require(join(packageRoot, "package.json")).version, "0.35.5");
    const [major, minor, patch] = sharp.versions.rsvg.split(".").map(Number);
    assert.ok(major > 2 || (major === 2 && (minor > 63 || (minor === 63 && patch >= 2))));
    const input = Buffer.from('<svg xmlns="http://www.w3.org/2000/svg" width="8" height="8"><rect width="8" height="8" fill="red"/></svg>');
    const { data, info } = await sharp(input).png().toBuffer({ resolveWithObject: true });
    assert.equal(info.width, 8);
    assert.equal(info.height, 8);
    assert.equal(info.format, "png");
    assert.ok(data.length > 0);
  });
}

for (const packageRoot of installedConsumers("@modelcontextprotocol/sdk")) {
  test(`${packageRoot}: saved OAuth credentials cannot be sent to a different issuer`, async () => {
    const { fetchToken } = await import(pathToFileURL(join(packageRoot, "dist/esm/client/auth.js")).href);
    let requests = 0;
    const provider = {
      clientMetadata: {},
      clientInformation: () => ({
        client_id: "disposable-test-client",
        client_secret: randomBytes(32).toString("hex"),
        issuer: "https://trusted-auth.example.invalid",
      }),
      prepareTokenRequest: () => new URLSearchParams({ grant_type: "client_credentials" }),
    };
    await assert.rejects(() => fetchToken(provider, "https://other-auth.example.invalid", {
      metadata: {
        issuer: "https://other-auth.example.invalid",
        token_endpoint: "https://other-auth.example.invalid/token",
        token_endpoint_auth_methods_supported: ["client_secret_post"],
      },
      fetchFn: async () => {
        requests++;
        throw new Error("Unexpected outbound token request");
      },
    }), /issuer/i);
    assert.equal(requests, 0, "Issuer mismatch must fail before any credential-bearing request");
    assert.equal(require(join(packageRoot, "package.json")).version, "1.31.0");
  });
}
