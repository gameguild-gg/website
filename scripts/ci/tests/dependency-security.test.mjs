import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import { createHash, randomBytes } from "node:crypto";
import { createServer, request as httpRequest } from "node:http";
import { deflateSync, gzipSync } from "node:zlib";
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
    assert.equal(nextRequire("./package.json").version, "16.4.2");
    assert.equal(nextRequire("@next/env/package.json").version, "16.4.2");
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

for (const packageRoot of installedConsumers("undici")) {
  const consumerRequire = createRequire(join(packageRoot, "package.json"));
  const undici = consumerRequire("./index.js");
  test(`${packageRoot}: reviewed HTTP client enforces mixed-case response integrity`, { timeout: 15000 }, async context => {
    assert.ok(["7.30.1", "8.12.2"].includes(consumerRequire("./package.json").version));
    const origin = await startHeaderServer(context, (_request, response) => response.end("fixture body"));
    const valid = createHash("sha256").update("fixture body").digest("base64");
    const invalid = createHash("sha256").update("different body").digest("base64");
    for (const algorithm of ["sha256", "SHA256", "Sha256"]) {
      const response = await undici.fetch(origin, { integrity: `${algorithm}-${valid}`, signal: AbortSignal.timeout(5000) });
      assert.equal(await response.text(), "fixture body");
      await assert.rejects(undici.fetch(origin, { integrity: `${algorithm}-${invalid}`, signal: AbortSignal.timeout(5000) }),
        error => /integrity mismatch/.test(String(error.cause)));
    }
  });
  test(`${packageRoot}: excess response encodings reject without an uncaught stream error`, { timeout: 10000 }, async context => {
    const origin = await startHeaderServer(context, (_request, response) => {
      response.setHeader("Content-Encoding", Array(6).fill("gzip").join(", "));
      response.end(gzipSync("fixture"));
    });
    await assert.rejects(async () => {
      const response = await undici.fetch(origin, { signal: AbortSignal.timeout(5000) });
      await response.text();
    }, error => /too many content-encodings/.test(String(error.cause)));
  });
  test(`${packageRoot}: SSE bounds retained fields while preserving ordinary events`, { timeout: 10000 }, async context => {
    const { EventSourceStream } = consumerRequire("./lib/web/eventsource/eventsource-stream.js");
    const ordinary = new EventSourceStream({ readableObjectMode: true, maxEventSize: 128 * 1024 });
    const events = [];
    ordinary.on("data", event => events.push(event));
    context.after(() => ordinary.destroy());
    const finished = once(ordinary, "end");
    ordinary.end("data: ordinary\n\n");
    await finished;
    assert.equal(events.length, 1);
    assert.equal(events[0].options.data, "ordinary");
    const bounded = new EventSourceStream({ readableObjectMode: true, maxEventSize: 128 * 1024 });
    context.after(() => bounded.destroy());
    const rejected = once(bounded, "error");
    bounded.end("data:\n".repeat(1025));
    const [error] = await rejected;
    assert.match(error.message, /EventSource message size exceeded/);
  });
}

for (const packageRoot of installedConsumers("fast-uri")) {
  test(`${packageRoot}: URI normalization preserves authority, scheme and relative-path boundaries`, () => {
    const uri = require(packageRoot);
    assert.equal(require(join(packageRoot, "package.json")).version, "3.1.9");
    assert.equal(uri.normalize("https://EXAMPLE.test/a/../b"), "https://example.test/b");
    assert.match(uri.parse("ht%74ps://example.test/").error, /scheme is malformed/);
    assert.match(uri.parse("https://user@allowed@other.test/").error, /host is malformed/);
    assert.equal(uri.equal("a/../b", "/b"), false);
    assert.equal(uri.normalize("a/..//b"), ".//b");
    assert.equal(uri.serialize({ path: "label:value" }, { reference: "suffix" }), "label%3Avalue");
    assert.equal(uri.parse(uri.normalize("./label:value")).scheme, undefined);
  });
}

for (const packageRoot of installedConsumers("happy-dom")) {
  test(`${packageRoot}: module imports retain valid aliases and cannot inject an initializer`, { timeout: 10000 }, async context => {
    assert.equal(require(join(packageRoot, "package.json")).version, "21.0.4");
    const { Window } = await import(pathToFileURL(join(packageRoot, "lib/index.js")).href);
    const { default: Compiler } = await import(pathToFileURL(join(packageRoot, "lib/module/ECMAScriptModuleCompiler.js")).href);
    const window = new Window({ url: "https://fixture.example/", settings: {
      enableJavaScriptEvaluation: true, suppressCodeGenerationFromStringsWarning: true, disableErrorCapturing: true,
    } });
    context.after(() => window.happyDOM.close());
    const imports = new Map([["https://fixture.example/values.json", { value: "ordinary" }]]);
    const compiler = new Compiler(window);
    const valid = compiler.compile("https://fixture.example/valid.js",
      'import { value as label } from "./values.json" with { type: "json" }; window.__fixtureValue = label;');
    await valid.execute({ imports });
    assert.equal(window.__fixtureValue, "ordinary");
    const invalid = compiler.compile("https://fixture.example/invalid.js",
      'import { absent as injected = globalThis.__fixtureInjected = true } from "./values.json" with { type: "json" };');
    await invalid.execute({ imports });
    assert.equal(window.__fixtureInjected, undefined);
  });
}

function assertConfiguredTokenEndpoint(actual, expected) {
  const url = new URL(actual);
  assert.deepEqual([...url.searchParams.keys()], ["client-request-id"]);
  assert.match(url.searchParams.get("client-request-id"), /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i);
  url.search = "";
  assert.equal(url.href, expected);
}

for (const packageRoot of installedConsumers("@azure/msal-node")) {
  test(`${packageRoot}: authorization response metadata cannot redirect credential-bearing requests`, { timeout: 10000 }, async () => {
    const consumerRequire = createRequire(join(packageRoot, "package.json"));
    const { ConfidentialClientApplication } = consumerRequire(packageRoot);
    assert.equal(consumerRequire("./package.json").version, "7.1.0");
    assert.equal(consumerRequire("@azure/msal-common/package.json").version, "16.14.2");
    const authority = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111";
    const endpoint = `${authority}/oauth2/v2.0/token`;
    const requests = [];
    const stopped = new Error("Disposable token transport stopped");
    const app = new ConfidentialClientApplication({ auth: {
      clientId: "22222222-2222-2222-2222-222222222222", clientSecret: randomBytes(32).toString("hex"),
      authority, knownAuthorities: ["login.microsoftonline.com"],
      authorityMetadata: JSON.stringify({ authorization_endpoint: `${authority}/oauth2/v2.0/authorize`,
        token_endpoint: endpoint, end_session_endpoint: `${authority}/oauth2/v2.0/logout`, issuer: `${authority}/v2.0` }),
    }, system: { disableInternalRetries: true, networkClient: {
      sendGetRequestAsync: async url => { requests.push({ method: "GET", url }); throw stopped; },
      sendPostRequestAsync: async (url, options) => { requests.push({ method: "POST", url, body: options.body }); throw stopped; },
    } } });
    await assert.rejects(app.acquireTokenByCode({ code: "disposable-code", redirectUri: "https://app.example/callback", scopes: ["User.Read"] },
      { code: "untrusted-response-code", cloud_instance_host_name: "other.example.invalid" }));
    assert.equal(requests.length, 1);
    assert.equal(requests[0].method, "POST");
    assertConfiguredTokenEndpoint(requests[0].url, endpoint);
    assert.equal(new URLSearchParams(requests[0].body).get("code"), "disposable-code");
  });
}

for (const packageRoot of installedConsumers("@azure/identity")) {
  test(`${packageRoot}: Azure Identity remains compatible with the fixed MSAL client`, { timeout: 10000 }, async () => {
    const consumerRequire = createRequire(join(packageRoot, "package.json"));
    const { ClientSecretCredential } = consumerRequire(packageRoot);
    const { createHttpHeaders } = consumerRequire("@azure/core-rest-pipeline");
    assert.equal(consumerRequire("@azure/msal-node/package.json").version, "7.1.0");
    const authority = "https://login.microsoftonline.com/11111111-1111-1111-1111-111111111111";
    const token = randomBytes(32).toString("hex");
    const requests = [];
    const credential = new ClientSecretCredential("11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222", randomBytes(32).toString("hex"), {
      disableInstanceDiscovery: true, retryOptions: { maxRetries: 0 }, httpClient: {
        sendRequest: async request => {
          requests.push({ url: request.url, method: request.method, body: request.body });
          assert.equal(new URL(request.url).origin, "https://login.microsoftonline.com");
          const body = request.method === "GET"
            ? { authorization_endpoint: `${authority}/oauth2/v2.0/authorize`, token_endpoint: `${authority}/oauth2/v2.0/token`,
              end_session_endpoint: `${authority}/oauth2/v2.0/logout`, issuer: `${authority}/v2.0` }
            : { access_token: token, token_type: "Bearer", expires_in: 3600 };
          return { request, status: 200, headers: createHttpHeaders({ "content-type": "application/json" }), bodyAsText: JSON.stringify(body) };
        },
      },
    });
    const result = await credential.getToken("https://database.windows.net/.default");
    assert.equal(result.token, token);
    const posts = requests.filter(request => request.method === "POST");
    assert.equal(posts.length, 1);
    assertConfiguredTokenEndpoint(posts[0].url, `${authority}/oauth2/v2.0/token`);
    assert.equal(new URLSearchParams(posts[0].body).get("grant_type"), "client_credentials");
  });
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

// GHSA-27r3-jxmh-p828: the decoder must be detached after its frame completes,
// while the remaining HTTP body is still consumed. Exercise the installed graph.
for (const packageRoot of installedConsumers("body-parser")) {
  const consumerRequire = createRequire(join(packageRoot, "package.json"));
  const bodyParser = consumerRequire("./index.js");
  for (const encoding of ["identity", "gzip"]) {
    test(`${packageRoot}: patched body parser preserves ordinary ${encoding} JSON requests`, async context => {
      assert.equal(consumerRequire("./package.json").version, "2.4.0");
      assert.equal(consumerRequire("qs/package.json").version, "6.16.1");
      const parser = bodyParser.json();
      const origin = await startHeaderServer(context, (request, response) => {
        parser(request, response, error => {
          response.statusCode = error ? 400 : 200;
          response.end(JSON.stringify(error ? { rejected: true } : request.body));
        });
      });
      const payload = Buffer.from(JSON.stringify({ message: "ordinary compressed JSON", count: 3 }));
      const response = await fetch(origin, {
        method: "POST",
        headers: { "Content-Type": "application/json", "Content-Encoding": encoding },
        body: encoding === "gzip" ? gzipSync(payload) : payload,
        signal: AbortSignal.timeout(5000),
      });
      assert.equal(response.status, 200);
      assert.deepEqual(await response.json(), { message: "ordinary compressed JSON", count: 3 });
    });
  }

  test(`${packageRoot}: trailing compressed-body bytes finish without an uncaught decoder error`, { timeout: 10000 }, async context => {
    const parser = bodyParser.json();
    const payload = { message: "late request bytes" };
    const first = Buffer.concat([deflateSync(JSON.stringify(payload)), Buffer.from([0])]);
    const trailing = Buffer.alloc(1024);
    let client;
    context.after(() => client?.destroy());
    const origin = await startHeaderServer(context, (request, response) => {
      parser(request, response, error => {
        if (error) {
          response.statusCode = 400;
          response.end("rejected");
          client.end(trailing);
          return;
        }
        // Deliver the rest only after parsing has finished, then require the HTTP
        // request to finish too. Sending one combined buffer misses this lifecycle.
        request.once("end", () => response.end(JSON.stringify(request.body)));
        client.end(trailing);
      });
    });
    const result = await new Promise((resolveResponse, rejectResponse) => {
      client = httpRequest(origin, {
        method: "POST", agent: false,
        headers: { "Content-Type": "application/json", "Content-Encoding": "deflate",
          "Content-Length": first.length + trailing.length },
      }, response => {
        const chunks = [];
        response.on("data", chunk => chunks.push(chunk));
        response.on("error", rejectResponse);
        response.on("end", () => resolveResponse({ status: response.statusCode, body: Buffer.concat(chunks).toString("utf8") }));
      });
      client.on("error", rejectResponse);
      client.setTimeout(5000, () => client.destroy(new Error("Compressed request did not finish")));
      client.write(first);
    });
    assert.equal(result.status, 200);
    assert.deepEqual(JSON.parse(result.body), payload);
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
