import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import { createHash, randomBytes } from "node:crypto";
import { createServer } from "node:http";
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
    assert.equal(require(join(packageRoot, "package.json")).version, "1.32.0");
  });
}

for (const packageRoot of installedConsumers("launch-editor")) {
  const constructCommand = require(join(packageRoot, "construct-windows-launch-command.js"));
  const parseFile = require(join(packageRoot, "parse-file.js"));
  const editorArguments = require(join(packageRoot, "get-args.js"));

  test(packageRoot + ": Windows editor arguments reject variable expansion and line breaks", () => {
    for (const value of ["%EDITOR_INPUT%", "%CMDCMDLINE:~-38%", "%VAR:old=new%", "line\rbreak", "line\nbreak"]) {
      assert.throws(() => constructCommand("code", ["C:\\project\\" + value]), /argument containing/);
    }
    assert.equal(require(join(packageRoot, "package.json")).version, "2.14.2");
  });

  test(packageRoot + ": ordinary editor arguments retain quoting and route characters", () => {
    assert.equal(
      constructCommand("C:\\Program Files\\Editor\\editor.exe", ["C:\\my project\\file.js"]),
      '"C:\\Program Files\\Editor\\editor.exe" "C:\\my project\\file.js"',
    );
    for (const name of ["+page.svelte", "$route.tsx", "(group)\\page.tsx", "@route\\page.tsx", "[slug]\\page.tsx"]) {
      assert.equal(constructCommand("code", [name]), "code " + name);
    }
    assert.equal(constructCommand("code", ["C:\\project\\&file.js"]), 'code ^"C:\\project\\^&file.js^"');
  });

  test(packageRoot + ": option-like file names become absolute editor operands", () => {
    for (const name of ["+fixture.js", "--fixture.js"]) {
      const expected = resolve(name);
      for (const editor of ["vim", "emacs", "notepad++"]) {
        assert.deepEqual(editorArguments(editor, name), [expected]);
      }
    }
    const file = resolve("ordinary.js");
    assert.deepEqual(editorArguments("code", file, "12", "3"), ["-r", "-g", file + ":12:3"]);
  });

  test(packageRoot + ": position parsing resolves the exact path used by the existence check", () => {
    const parent = join(repositoryRoot, "synthetic-project");
    assert.deepEqual(parseFile(join(parent, "synthetic-component", "..") + ":1:1"), {
      fileName: parent, lineNumber: "1", columnNumber: "1",
    });
    const rawTraversal = parent + "/synthetic-component/..:1:1";
    assert.equal(parseFile(rawTraversal).fileName, parent);
    assert.equal(parseFile(parent + "/%EDITOR_INPUT%/..:1:1").fileName, parent);
    assert.equal(parseFile(":1:1").fileName, "");
    const ordinary = join(repositoryRoot, "ordinary.js");
    assert.equal(parseFile(pathToFileURL(ordinary).href).fileName, ordinary);
  });
}

function readFixtureRequest(incoming, complete) {
  let body = "";
  incoming.setEncoding("utf8");
  incoming.on("data", (chunk) => { body += chunk; });
  incoming.on("end", () => complete({ headers: incoming.headers, body }));
}

for (const packageRoot of installedConsumers("@modelcontextprotocol/sdk")) {
  for (const status of [302, 307, 308]) {
    for (const sameOrigin of [false, true]) {
      test(packageRoot + ": MCP " + status + " redirects respect origin boundaries (" + sameOrigin + ")", { timeout: 15000 }, async (context) => {
        const { StreamableHTTPClientTransport } = await import(pathToFileURL(join(packageRoot, "dist/esm/client/streamableHttp.js")).href);
        const sourceRequests = [], targetRequests = [];
        const receiveTarget = (incoming, response) => readFixtureRequest(incoming, (observed) => {
          targetRequests.push(observed);
          response.writeHead(202);
          response.end();
        });
        const target = await startRedirectServer(context, receiveTarget);
        const source = await startRedirectServer(context, (incoming, response) => {
          if (sameOrigin && incoming.url === "/target") return receiveTarget(incoming, response);
          readFixtureRequest(incoming, (observed) => {
            sourceRequests.push(observed);
            response.writeHead(status, { Location: sameOrigin ? "/target" : target.href });
            response.end();
          });
        });
        const apiKey = randomBytes(16).toString("hex");
        const sessionId = randomBytes(16).toString("hex");
        const message = { jsonrpc: "2.0", method: "notifications/progress", params: { progressToken: "fixture", progress: 1 } };
        const transport = new StreamableHTTPClientTransport(source, {
          requestInit: { headers: { "X-API-Key": apiKey } },
          sessionId,
        });
        context.after(() => transport.close());
        await transport.start();
        if (sameOrigin && status !== 302) {
          await transport.send(message);
          assert.equal(targetRequests.length, 1);
          assert.equal(targetRequests[0].headers["x-api-key"], apiKey);
          assert.equal(targetRequests[0].headers["mcp-session-id"], sessionId);
          assert.deepEqual(JSON.parse(targetRequests[0].body), message);
        } else {
          await assert.rejects(() => transport.send(message), /redirect/i);
          assert.equal(targetRequests.length, 0, "Custom headers, session and body must not reach the redirect target");
        }
        assert.equal(sourceRequests.length, 1);
        assert.equal(sourceRequests[0].headers["x-api-key"], apiKey);
        assert.equal(sourceRequests[0].headers["mcp-session-id"], sessionId);
        assert.deepEqual(JSON.parse(sourceRequests[0].body), message);
      });
    }
  }

  for (const status of [307, 308]) {
    for (const sameOrigin of [false, true]) {
      test(packageRoot + ": OAuth " + status + " credential redirects respect origin boundaries (" + sameOrigin + ")", { timeout: 15000 }, async (context) => {
        const { fetchToken } = await import(pathToFileURL(join(packageRoot, "dist/esm/client/auth.js")).href);
        const sourceRequests = [], targetRequests = [];
        const receiveTarget = (incoming, response) => readFixtureRequest(incoming, (observed) => {
          targetRequests.push(observed);
          response.writeHead(200, { "Content-Type": "application/json" });
          response.end(JSON.stringify({ access_token: "synthetic-access-token", token_type: "Bearer" }));
        });
        const target = await startRedirectServer(context, receiveTarget);
        const source = await startRedirectServer(context, (incoming, response) => {
          if (sameOrigin && incoming.url === "/target") return receiveTarget(incoming, response);
          readFixtureRequest(incoming, (observed) => {
            sourceRequests.push(observed);
            response.writeHead(status, { Location: sameOrigin ? "/target" : target.href });
            response.end();
          });
        });
        const clientSecret = randomBytes(16).toString("hex");
        const refreshToken = randomBytes(16).toString("hex");
        const provider = {
          clientMetadata: {},
          clientInformation: () => ({ client_id: "disposable-test-client", client_secret: clientSecret, issuer: source.href }),
          prepareTokenRequest: () => new URLSearchParams({ grant_type: "refresh_token", refresh_token: refreshToken }),
        };
        const request = () => fetchToken(provider, source, {
          metadata: { issuer: source.href, token_endpoint: source.href, token_endpoint_auth_methods_supported: ["client_secret_post"] },
        });
        if (sameOrigin) {
          assert.equal((await request()).access_token, "synthetic-access-token");
          assert.equal(targetRequests.length, 1);
          assert.equal(new URLSearchParams(targetRequests[0].body).get("client_secret"), clientSecret);
          assert.equal(new URLSearchParams(targetRequests[0].body).get("refresh_token"), refreshToken);
        } else {
          await assert.rejects(request);
          assert.equal(targetRequests.length, 0, "OAuth credentials must not reach a different origin");
        }
        assert.equal(sourceRequests.length, 1);
        assert.equal(new URLSearchParams(sourceRequests[0].body).get("client_secret"), clientSecret);
        assert.equal(new URLSearchParams(sourceRequests[0].body).get("refresh_token"), refreshToken);
      });
    }
  }
}

for (const packageRoot of installedConsumers("shadcn")) {
  test(packageRoot + ": the installed shadcn MCP server retains its tool catalog", { timeout: 15000 }, async (context) => {
    const consumerRequire = createRequire(join(packageRoot, "package.json"));
    const sdkRoot = resolve(dirname(consumerRequire.resolve("@modelcontextprotocol/sdk/client/index.js")), "../../..");
    const { Client } = await import(pathToFileURL(join(sdkRoot, "dist/esm/client/index.js")).href);
    const { InMemoryTransport } = await import(pathToFileURL(join(sdkRoot, "dist/esm/inMemory.js")).href);
    const { server } = await import(pathToFileURL(join(packageRoot, "dist/mcp/index.js")).href);
    const client = new Client({ name: "dependency-compatibility-fixture", version: "1.0.0" });
    const [clientTransport, serverTransport] = InMemoryTransport.createLinkedPair();
    context.after(async () => { await client.close(); await server.close(); });
    await server.connect(serverTransport);
    await client.connect(clientTransport);
    const result = await client.listTools();
    assert.ok(result.tools.some((tool) => tool.name === "get_project_registries"));
    assert.ok(result.tools.some((tool) => tool.name === "search_items_in_registries"));
    assert.ok(result.tools.every((tool) => tool.inputSchema.type === "object"));
  });
}

async function startRedirectServer(context, handler) {
  const server = createServer(handler);
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", () => {
      server.removeListener("error", reject);
      resolve();
    });
  });
  context.after(() => new Promise((resolve, reject) => {
    server.close((error) => error ? reject(error) : resolve());
    server.closeAllConnections();
  }));
  const address = server.address();
  assert.ok(address && typeof address === "object");
  return new URL(`http://127.0.0.1:${address.port}`);
}

function echoRedirectRequest(incoming, response) {
  let body = "";
  incoming.setEncoding("utf8");
  incoming.on("data", (chunk) => { body += chunk; });
  incoming.on("end", () => {
    response.setHeader("Content-Type", "application/json");
    response.end(JSON.stringify({ headers: incoming.headers, method: incoming.method, body }));
  });
}

function followRedirectRequest(transport, url, options, body) {
  assert.equal(url.hostname, "127.0.0.1");
  assert.equal(url.protocol, "http:");
  return new Promise((resolve, reject) => {
    const outgoing = transport.request(url, options, (response) => {
      let data = "";
      response.setEncoding("utf8");
      response.on("data", (chunk) => { data += chunk; });
      response.on("error", reject);
      response.on("end", () => {
        try { resolve(JSON.parse(data)); } catch (error) { reject(error); }
      });
    });
    outgoing.on("error", reject);
    outgoing.setTimeout(5000, () => outgoing.destroy(new Error("Redirect fixture timed out")));
    outgoing.end(body);
  });
}

for (const packageRoot of installedConsumers("follow-redirects")) {
  const { default: redirectTransport } = await import(pathToFileURL(join(packageRoot, "index.js")).href);
  const sensitiveName = "X-Private^Header|Value";
  const fixedPublisher = () => {
    const manifest = JSON.parse(readFileSync(join(packageRoot, "package.json"), "utf8"));
    assert.equal(manifest.version, "1.16.1");
    assert.equal(
      createHash("sha256").update(readFileSync(join(packageRoot, "index.js"))).digest("hex"),
      "656e169cc2e8043ec00b7d912f3ff523a01026a3fd38e04516b37fb39be94f14",
    );
  };
  const credentials = () => ({
    Authorization: randomBytes(16).toString("hex"),
    "pRoXy-AuThOrIzAtIoN": randomBytes(16).toString("hex"),
    COOKIE: `session=${randomBytes(16).toString("hex")}`,
    [sensitiveName]: randomBytes(16).toString("hex"),
  });
  const asHeaders = (representation, values) => representation === "array" ? Object.entries(values).flat() : values;

  for (const representation of ["object", "array"]) {
    test(`${packageRoot}: ${representation} credentials are stripped across redirect origins`, { timeout: 15000 }, async (context) => {
      const target = await startRedirectServer(context, echoRedirectRequest);
      const source = await startRedirectServer(context, (incoming, response) => {
        incoming.resume();
        incoming.on("end", () => { response.writeHead(302, { Location: target.href }); response.end(); });
      });
      const privateHeaders = credentials();
      const headers = asHeaders(representation, { ...privateHeaders, Host: source.host, Value: "ordinary" });
      const observed = await followRedirectRequest(redirectTransport.http, source, { headers, sensitiveHeaders: [sensitiveName] });
      for (const name of Object.keys(privateHeaders)) assert.equal(observed.headers[name.toLowerCase()], undefined);
      assert.equal(observed.headers.host, target.host);
      assert.equal(observed.headers.value, "ordinary", "Literal sensitive names must not remove unrelated headers");
      fixedPublisher();
    });

    test(`${packageRoot}: ${representation} credentials remain on same-origin redirects`, { timeout: 15000 }, async (context) => {
      const source = await startRedirectServer(context, (incoming, response) => {
        if (incoming.url === "/redirect") {
          incoming.resume();
          incoming.on("end", () => { response.writeHead(302, { Location: "/target" }); response.end(); });
        } else {
          echoRedirectRequest(incoming, response);
        }
      });
      const privateHeaders = credentials();
      const headers = asHeaders(representation, { ...privateHeaders, Host: source.host });
      const observed = await followRedirectRequest(redirectTransport.http, new URL("/redirect", source), { headers, sensitiveHeaders: [sensitiveName] });
      for (const [name, value] of Object.entries(privateHeaders)) assert.equal(observed.headers[name.toLowerCase()], value);
      assert.equal(observed.headers.host, source.host);
      fixedPublisher();
    });
  }

  test(`${packageRoot}: array headers and POST bodies are updated together on 302 redirects`, { timeout: 15000 }, async (context) => {
    const target = await startRedirectServer(context, echoRedirectRequest);
    let sourceBody = "";
    const source = await startRedirectServer(context, (incoming, response) => {
      incoming.setEncoding("utf8");
      incoming.on("data", (chunk) => { sourceBody += chunk; });
      incoming.on("end", () => { response.writeHead(302, { Location: target.href }); response.end(); });
    });
    const body = "redirect-fixture-body";
    const privateHeaders = credentials();
    const headers = asHeaders("array", { ...privateHeaders, Host: source.host, "Content-Type": "text/plain", "Content-Length": String(Buffer.byteLength(body)) });
    const observed = await followRedirectRequest(redirectTransport.http, source, { method: "POST", headers, sensitiveHeaders: [sensitiveName] }, body);
    assert.equal(sourceBody, body);
    assert.equal(observed.method, "GET");
    assert.equal(observed.body, "");
    assert.equal(observed.headers["content-type"], undefined);
    assert.equal(observed.headers["content-length"], undefined);
    for (const name of Object.keys(privateHeaders)) assert.equal(observed.headers[name.toLowerCase()], undefined);
    assert.equal(observed.headers.host, target.host);
    fixedPublisher();
  });
}
