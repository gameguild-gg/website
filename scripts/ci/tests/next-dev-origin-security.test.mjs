import assert from "node:assert/strict";
import { createServer, request } from "node:http";
import { createRequire } from "node:module";
import { resolve } from "node:path";
import { after, before, test } from "node:test";

const require = createRequire(
  process.env.NEXT_SECURITY_REQUIRE_ANCHOR ||
    resolve("apps/web/package.json"),
);
const { blockCrossSiteDEV } = require(
  "next/dist/server/lib/router-utils/block-cross-site-dev",
);
const paths = [
  "/_next/mcp",
  "/_next/mcp?marker=/_next/image",
  "/_next/mcp?marker=/_next/static/media",
  "/_next/mcp?marker=/_next/static/immutable/media",
];
const cases = [
  {
    name: "untrusted origin",
    headers: { origin: "https://untrusted.invalid" },
    status: 403,
  },
  { name: "opaque origin", headers: { origin: "null" }, status: 403 },
  {
    name: "cross-site no-cors",
    headers: {
      "sec-fetch-mode": "no-cors",
      "sec-fetch-site": "cross-site",
      referer: "https://untrusted.invalid/page",
    },
    status: 403,
  },
  {
    name: "local same-origin control",
    headers: { origin: "http://localhost" },
    status: 200,
  },
  {
    name: "configured trusted origin control",
    headers: { origin: "https://trusted.invalid" },
    status: 200,
  },
  { name: "ordinary request without origin control", headers: {}, status: 200 },
];

let server;
let port;
before(async () => {
  server = createServer((req, res) => {
    req.resume();
    if (blockCrossSiteDEV(req, res, ["trusted.invalid"], "127.0.0.1")) return;
    res.statusCode = 200;
    res.end("owned-origin-control");
  });
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, "127.0.0.1", resolve);
  });
  port = server.address().port;
});
after(async () => {
  if (server?.listening)
    await new Promise((resolve, reject) =>
      server.close((error) => (error ? reject(error) : resolve())),
    );
});

for (const path of paths) {
  for (const method of ["GET", "POST"]) {
    for (const scenario of cases) {
      test(method + " " + path + ": " + scenario.name, async () => {
        const response = await new Promise((resolve, reject) => {
          const req = request(
            { hostname: "127.0.0.1", port, path, method, headers: scenario.headers },
            (res) => {
              let body = "";
              res.setEncoding("utf8");
              res.on("data", (chunk) => (body += chunk));
              res.on("end", () => resolve({ status: res.statusCode, body }));
              res.on("error", reject);
            },
          );
          req.on("error", reject);
          req.setTimeout(5_000, () => req.destroy(new Error("Owned HTTP fixture timed out")));
          req.end();
        });
        assert.equal(response.status, scenario.status);
        assert.equal(
          response.body,
          scenario.status === 403 ? "Unauthorized" : "owned-origin-control",
        );
      });
    }
  }
}
