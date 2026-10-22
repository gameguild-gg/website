import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import { randomBytes } from "node:crypto";
import { createServer } from "node:http";
import { once } from "node:events";
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

const nextRoots = new Set(installedConsumers("next"));
for (const workspace of ["apps/web", "demos/emception-ide-next"]) {
  const consumerRequire = createRequire(join(repositoryRoot, workspace, "package.json"));
  nextRoots.add(dirname(realpathSync(consumerRequire.resolve("next/package.json"))));
}
for (const packageRoot of nextRoots) {
  test(`${packageRoot}: Next and its resolved image dependency use the reviewed security releases`, () => {
    const nextRequire = createRequire(join(packageRoot, "package.json"));
    assert.equal(nextRequire("./package.json").version, "16.4.1");
    assert.equal(nextRequire("@next/env/package.json").version, "16.4.1");
    assert.equal(nextRequire("sharp").versions.sharp, "0.35.5");
  });
}

for (const packageRoot of installedConsumers("toml")) {
  const toml = require(packageRoot);
  test(`${packageRoot}: reviewed TOML release preserves ordinary connection profiles and tables`, () => {
    assert.equal(require(join(packageRoot, "package.json")).version, "5.0.1");
    const profile = toml.parse('[connections.fixture]\naccount = "fixture"\nport = 443\nkeep_alive = true\n');
    assert.equal(profile.connections.fixture.account, "fixture");
    assert.equal(profile.connections.fixture.port, 443);
    assert.equal(profile.connections.fixture.keep_alive, true);
    const tables = toml.parse(Array.from({ length: 250 }, (_, index) => `[[rows]]\nindex = ${index}\n`).join(""));
    assert.equal(tables.rows.length, 250);
    assert.equal(tables.rows[249].index, 249);
    const keys = Array.from({ length: 64 }, (_, index) => `key${index}`);
    const nested = toml.parse(`${keys.join(".")} = 42\n`);
    assert.equal(keys.reduce((value, key) => value[key], nested), 42);
  });
  test(`${packageRoot}: TOML rejects lossy or out-of-range integers`, () => {
    assert.throws(() => toml.parse("value = 9007199254740992"), /losslessly/);
    assert.equal(toml.parse("value = 9007199254740992", { bigint: true }).value, 9007199254740992n);
    assert.throws(() => toml.parse("value = 9223372036854775808", { bigint: true }), /64-bit/);
    assert.throws(() => toml.parse(`value = ${"9".repeat(4096)}`));
  });
}

const webRequire = createRequire(join(repositoryRoot, "apps/web/package.json"));
const { JSDOM } = webRequire("jsdom");
for (const packageRoot of installedConsumers("dompurify")) {
  test(`${packageRoot}: reviewed sanitizer retains ordinary content and removes executable attributes`, context => {
    assert.equal(require(join(packageRoot, "package.json")).version, "3.4.17");
    // Default jsdom settings do not execute scripts or load external resources.
    const dom = new JSDOM("");
    context.after(() => dom.window.close());
    const purify = require(packageRoot)(dom.window);
    const fragment = purify.sanitize('<p><strong>Retained</strong><img src="fixture.png" onerror="alert(1)"></p>', {
      RETURN_DOM_FRAGMENT: true,
    });
    assert.equal(fragment.querySelector("strong").textContent, "Retained");
    assert.equal(fragment.querySelector("img").getAttribute("src"), "fixture.png");
    assert.equal(fragment.querySelector("img").hasAttribute("onerror"), false);
    const template = purify.sanitize("<p>{{ fixture }}</p>", { SAFE_FOR_TEMPLATES: true, RETURN_DOM_FRAGMENT: true });
    assert.equal(template.textContent.includes("{{"), false);
  });
}

for (const packageRoot of installedConsumers("ip-address")) {
  test(`${packageRoot}: reviewed address parser preserves IPv4, IPv6 and zones and rejects malformed long inputs`, () => {
    assert.equal(require(join(packageRoot, "package.json")).version, "10.7.4");
    const { Address4, Address6 } = require(packageRoot);
    assert.equal(new Address4("192.0.2.1").correctForm(), "192.0.2.1");
    assert.equal(new Address6("2001:db8::1").correctForm(), "2001:db8::1");
    const zoned = new Address6("fe80::1%eth0");
    assert.equal(zoned.correctForm(), "fe80::1");
    assert.equal(zoned.zone, "%eth0");
    assert.equal(Address6.isValid(`${"%".repeat(4096)}\n`), false);
    assert.equal(Address6.isValid("f".repeat(4096)), false);
  });
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

async function startHeaderServer(context, handler) {
  const server = createServer(handler);
  context.after(() => new Promise((resolveClose, rejectClose) => {
    server.closeAllConnections();
    server.close(error => error ? rejectClose(error) : resolveClose());
  }));
  server.listen(0, "127.0.0.1");
  await once(server, "listening");
  return `http://127.0.0.1:${server.address().port}`;
}

function readRedirectedHeaders(client, url, headers) {
  return new Promise((resolveHeaders, rejectHeaders) => {
    const request = client.get(url, { headers, sensitiveHeaders: ["X-Test-Credential"] }, response => {
      const chunks = [];
      response.on("data", chunk => chunks.push(chunk));
      response.on("error", rejectHeaders);
      response.on("end", () => {
        try {
          assert.equal(response.statusCode, 200);
          resolveHeaders(JSON.parse(Buffer.concat(chunks).toString("utf8")));
        } catch (error) {
          rejectHeaders(error);
        }
      });
    });
    request.on("error", rejectHeaders);
    request.setTimeout(5000, () => request.destroy(new Error("Redirect fixture timed out")));
  });
}

for (const packageRoot of installedConsumers("follow-redirects")) {
  for (const arrayHeaders of [false, true]) {
    for (const crossOrigin of [false, true]) {
      test(`${packageRoot}: ${arrayHeaders ? "array" : "object"} headers ${crossOrigin ? "strip credentials across origins" : "preserve credentials on the same origin"}`, async context => {
        assert.equal(require(join(packageRoot, "package.json")).version, "1.16.1");
        const client = require(packageRoot).http;
        const capture = (request, response) => {
          request.resume();
          response.setHeader("Content-Type", "application/json");
          response.end(JSON.stringify(request.headers));
        };
        const target = await startHeaderServer(context, capture);
        const origin = await startHeaderServer(context, (request, response) => {
          if (request.url === "/capture") {
            capture(request, response);
            return;
          }
          request.resume();
          response.writeHead(302, { Location: crossOrigin ? target + "/capture" : "/capture" });
          response.end();
        });
        const value = randomBytes(16).toString("hex");
        const credentials = { Authorization: value, "Proxy-Authorization": value, Cookie: `fixture=${value}`, "X-Test-Credential": value };
        const headers = { ...credentials, "X-Ordinary": "retained" };
        const observed = await readRedirectedHeaders(client, origin, arrayHeaders ? Object.entries(headers).flat() : headers);
        for (const [name, expected] of Object.entries(credentials)) {
          assert.equal(observed[name.toLowerCase()], crossOrigin ? undefined : expected);
        }
        assert.equal(observed["x-ordinary"], "retained");
        assert.equal(observed.host, new URL(crossOrigin ? target : origin).host);
      });
    }
  }
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

for (const packageRoot of installedConsumers("launch-editor")) {
  test(`${packageRoot}: every editor consumer uses the reviewed security release`, () => {
    assert.equal(require(join(packageRoot, "package.json")).version, "2.14.2");
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
    assert.equal(require(join(packageRoot, "package.json")).version, "1.32.1");
  });
}
