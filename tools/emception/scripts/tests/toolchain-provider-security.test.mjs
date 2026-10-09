import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { once } from "node:events";
import { createServer } from "node:http";
import test from "node:test";

import { createToolSourceProvider } from "../toolchain/provider.ts";

const commit = "a".repeat(40);
const archive = Buffer.from("owned-test-archive");
const archiveHash = createHash("sha256").update(archive).digest("hex");
const referenceUrl =
  "https://api.github.com/repos/google/brotli/git/ref/tags/v1.2.1";
const tagUrl = `https://api.github.com/repos/google/brotli/git/tags/${"b".repeat(40)}`;
const archiveUrl = `https://codeload.github.com/google/brotli/tar.gz/${commit}`;
const currentGit = {
  version: "1.2.0",
  source: {
    kind: "git-archive",
    repository: "google/brotli",
    commit,
    url: archiveUrl,
    sha256: archiveHash,
  },
};

function replaceFetch(context, fetcher) {
  const original = globalThis.fetch;
  globalThis.fetch = fetcher;
  context.after(() => {
    globalThis.fetch = original;
  });
}

function replaceToken(context) {
  const original = process.env.GITHUB_TOKEN;
  process.env.GITHUB_TOKEN = "owned-test-token";
  context.after(() => {
    if (original === undefined) delete process.env.GITHUB_TOKEN;
    else process.env.GITHUB_TOKEN = original;
  });
}

function objectResponse(type, url = tagUrl) {
  return Response.json({ object: { type, url, sha: commit } });
}

test("remote annotated-tag URLs cannot contact a private HTTP server", async (context) => {
  const requests = [];
  const destination = createServer((request, response) => {
    requests.push(request.url);
    response.writeHead(200, { "Content-Type": "application/json" });
    response.end(
      JSON.stringify({ object: { type: "commit", sha: commit, url: tagUrl } }),
    );
  });
  destination.listen(0, "127.0.0.1");
  await once(destination, "listening");
  context.after(async () => {
    await new Promise((resolve, reject) => {
      destination.close((error) => (error ? reject(error) : resolve()));
      destination.closeAllConnections();
    });
  });
  const address = destination.address();
  assert.ok(address && typeof address !== "string");
  const privateUrl = `http://127.0.0.1:${address.port}/private`;
  const realFetch = globalThis.fetch;
  replaceFetch(context, async (input, init) => {
    const url = String(input);
    if (url === referenceUrl) return objectResponse("tag", privateUrl);
    if (url === archiveUrl) return new Response(archive);
    return realFetch(input, init);
  });
  let rejected = false;
  try {
    await createToolSourceProvider().resolve("brotli", "1.2.1", currentGit);
  } catch {
    rejected = true;
  }
  assert.equal(
    requests.length,
    0,
    "the actual owned private server must receive no request",
  );
  assert.equal(
    rejected,
    true,
    "unsafe tag URL must fail before the network request",
  );
});

const unsafeUrls = [
  "http://api.github.com/repos/google/brotli/git/tags/test",
  "https://127.0.0.1/private",
  "https://2130706433/private",
  "https://0x7f000001/private",
  "https://0177.0.0.1/private",
  "https://[::1]/private",
  "https://[::ffff:127.0.0.1]/private",
  "https://10.0.0.1/private",
  "https://169.254.169.254/latest/meta-data",
  "https://localhost/private",
  "https://api.github.com.attacker.invalid/private",
  "https://api.github.com./repos/google/brotli/git/tags/test",
  "https://api.github.com:444/repos/google/brotli/git/tags/test",
  "https://user:password@api.github.com/repos/google/brotli/git/tags/test",
  "https://api.github.com@attacker.invalid/private",
  "https://raw.githubusercontent.com/attacker/tag.json",
  "//api.github.com/repos/google/brotli/git/tags/test",
  "ftp://api.github.com/private",
  "file:///etc/passwd",
  "data:application/json,{}",
];

for (const unsafeUrl of unsafeUrls) {
  test(`rejects untrusted annotated-tag URL ${unsafeUrl}`, async (context) => {
    const calls = [];
    replaceToken(context);
    replaceFetch(context, async (input, init) => {
      calls.push({ url: String(input), init });
      if (String(input) === referenceUrl)
        return objectResponse("tag", unsafeUrl);
      if (String(input) === archiveUrl) return new Response(archive);
      return objectResponse("commit");
    });
    let rejected = false;
    try {
      await createToolSourceProvider().resolve("brotli", "1.2.1", currentGit);
    } catch {
      rejected = true;
    }
    assert.deepEqual(
      calls.map((call) => call.url),
      [referenceUrl],
    );
    assert.equal(rejected, true);
  });
}

test("preserves annotated GitHub tags, API credentials and immutable archive hashes", async (context) => {
  const calls = [];
  replaceToken(context);
  replaceFetch(context, async (input, init) => {
    const url = String(input);
    calls.push({ url, init });
    if (url === referenceUrl) return objectResponse("tag");
    if (url === tagUrl) return objectResponse("commit");
    assert.equal(url, archiveUrl);
    return new Response(archive);
  });
  const result = await createToolSourceProvider().resolve(
    "brotli",
    "1.2.1",
    currentGit,
  );
  assert.deepEqual(result, {
    version: "1.2.1",
    source: {
      kind: "git-archive",
      repository: "google/brotli",
      commit,
      url: archiveUrl,
      sha256: archiveHash,
    },
  });
  assert.deepEqual(
    calls.map((call) => call.url),
    [referenceUrl, tagUrl, archiveUrl],
  );
  assert.equal(
    new Headers(calls[0].init.headers).get("authorization"),
    "Bearer owned-test-token",
  );
  assert.equal(
    new Headers(calls[1].init.headers).get("authorization"),
    "Bearer owned-test-token",
  );
  assert.equal(new Headers(calls[2].init.headers).get("authorization"), null);
});

test("preserves direct release archive hashing", async (context) => {
  const url =
    "https://github.com/facebook/zstd/releases/download/v1.5.7/zstd-v1.5.7-win64.zip";
  replaceFetch(context, async (input) => {
    assert.equal(String(input), url);
    return new Response(archive);
  });
  const result = await createToolSourceProvider().resolve(
    "zstdWindows",
    "1.5.7",
    {
      version: "1.5.6",
      source: {
        kind: "archive",
        url: url.replaceAll("1.5.7", "1.5.6"),
        sha256: archiveHash,
      },
    },
  );
  assert.deepEqual(result, {
    version: "1.5.7",
    source: { kind: "archive", url, sha256: archiveHash },
  });
});

test("preserves unchanged versions without network traffic", async (context) => {
  replaceFetch(context, () => {
    throw new Error("Unchanged lock must not contact the network");
  });
  assert.equal(
    await createToolSourceProvider().resolve(
      "brotli",
      currentGit.version,
      currentGit,
    ),
    currentGit,
  );
});

for (const status of [301, 302, 303, 307, 308]) {
  for (const location of [
    "http://api.github.com/private",
    "https://127.0.0.1/private",
    "https://api.github.com.attacker.invalid/private",
    "https://user:password@api.github.com/private",
    "https://release-assets.githubusercontent.com/private",
  ]) {
    test(`rejects unsafe API redirect ${status} to ${location}`, async (context) => {
      const calls = [];
      const cancelled = [];
      replaceToken(context);
      replaceFetch(context, async (input, init) => {
        calls.push({ url: String(input), init });
        return new Response(
          new ReadableStream({
            cancel() {
              cancelled.push(true);
            },
          }),
          { status, headers: { Location: location } },
        );
      });
      await assert.rejects(
        createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
        /Unsafe Toolchain source URL/,
      );
      assert.deepEqual(
        calls.map((call) => call.url),
        [referenceUrl],
      );
      assert.equal(calls[0].init.redirect, "manual");
      assert.equal(cancelled.length, 1);
    });
  }
}

for (const status of [301, 302, 303, 307, 308]) {
  test(`preserves approved GitHub archive redirect ${status} without bearer credentials`, async (context) => {
    const url =
      "https://github.com/facebook/zstd/releases/download/v1.5.7/zstd-v1.5.7-win64.zip";
    const destination =
      "https://release-assets.githubusercontent.com/github-production-release-asset/test?download=signed-test";
    const calls = [];
    replaceToken(context);
    replaceFetch(context, async (input, init) => {
      const address = String(input);
      calls.push({ url: address, init });
      if (address === url)
        return new Response(null, {
          status,
          headers: { Location: destination },
        });
      assert.equal(address, destination);
      return new Response(archive);
    });
    const result = await createToolSourceProvider().resolve(
      "zstdWindows",
      "1.5.7",
      {
        version: "1.5.6",
        source: {
          kind: "archive",
          url: url.replaceAll("1.5.7", "1.5.6"),
          sha256: archiveHash,
        },
      },
    );
    assert.deepEqual(result, {
      version: "1.5.7",
      source: { kind: "archive", url, sha256: archiveHash },
    });
    assert.deepEqual(
      calls.map((call) => call.url),
      [url, destination],
    );
    for (const call of calls) {
      assert.equal(call.init.redirect, "manual");
      assert.equal(new Headers(call.init.headers).get("authorization"), null);
    }
  });
}

for (const mirror of [
  "https://repo.msys2.org",
  "https://mirror.umd.edu",
  "https://mirror.accum.se",
  "https://ftp.nluug.nl",
  "https://ftp2.osuosl.org",
  "https://mirror.internet.asn.au",
  "https://mirror.selfnet.de",
  "https://mirror.yandex.ru",
  "https://mirrors.dotsrc.org",
  "https://mirrors.tuna.tsinghua.edu.cn",
  "https://mirrors.ustc.edu.cn",
  "https://mirror.nju.edu.cn",
  "https://mirrors.bfsu.edu.cn",
  "https://mirror.clarkson.edu",
  "https://distrohub.kyiv.ua",
  "https://mirror.archlinux.tw",
  "https://us.mirrors.cicku.me",
  "https://ca.mirrors.cicku.me",
  "https://mirrors.qlu.edu.cn",
]) {
  test(`preserves official MSYS2 mirror redirect to ${mirror}`, async (context) => {
    const url =
      "https://mirror.msys2.org/mingw/mingw64/make-4.4.1-6.pkg.tar.zst";
    const destination = `${mirror}/msys2/mingw/mingw64/make-4.4.1-6.pkg.tar.zst`;
    const calls = [];
    replaceToken(context);
    replaceFetch(context, async (input, init) => {
      const address = String(input);
      calls.push({ url: address, init });
      if (address === url)
        return new Response(null, {
          status: 302,
          headers: { Location: destination },
        });
      assert.equal(address, destination);
      return new Response(archive);
    });
    const result = await createToolSourceProvider().resolve(
      "msys2Make",
      "4.4.1-6",
      {
        version: "4.4.1-5",
        source: {
          kind: "archive",
          url: url.replaceAll("4.4.1-6", "4.4.1-5"),
          sha256: archiveHash,
        },
      },
    );
    assert.deepEqual(result, {
      version: "4.4.1-6",
      source: { kind: "archive", url, sha256: archiveHash },
    });
    assert.deepEqual(
      calls.map((call) => call.url),
      [url, destination],
    );
    for (const call of calls) {
      assert.equal(call.init.redirect, "manual");
      assert.equal(new Headers(call.init.headers).get("authorization"), null);
    }
  });
}

test("preserves relative redirects within the API with credentials", async (context) => {
  const destination =
    "https://api.github.com/repos/google/brotli/git/ref/tags/canonical";
  const calls = [];
  replaceToken(context);
  replaceFetch(context, async (input, init) => {
    const url = String(input);
    calls.push({ url, init });
    if (url === referenceUrl)
      return new Response(null, {
        status: 301,
        headers: { Location: "/repos/google/brotli/git/ref/tags/canonical" },
      });
    if (url === destination) return objectResponse("commit");
    assert.equal(url, archiveUrl);
    return new Response(archive);
  });
  const result = await createToolSourceProvider().resolve(
    "brotli",
    "1.2.1",
    currentGit,
  );
  assert.equal(result.source.sha256, archiveHash);
  assert.deepEqual(
    calls.map((call) => call.url),
    [referenceUrl, destination, archiveUrl],
  );
  assert.equal(
    new Headers(calls[0].init.headers).get("authorization"),
    "Bearer owned-test-token",
  );
  assert.equal(
    new Headers(calls[1].init.headers).get("authorization"),
    "Bearer owned-test-token",
  );
  assert.equal(new Headers(calls[2].init.headers).get("authorization"), null);
});

for (const { initial, tool, version } of [
  {
    initial:
      "https://raw.githubusercontent.com/emscripten-core/emsdk/main/emscripten-releases-tags.json",
    tool: "emsdk",
  },
  {
    initial:
      "https://github.com/facebook/zstd/releases/download/v1.5.7/zstd-v1.5.7-win64.zip",
    tool: "zstdWindows",
    version: "1.5.7",
  },
  {
    initial: "https://mirror.msys2.org/mingw/make-4.4.1-6.pkg.tar.zst",
    tool: "msys2Make",
    version: "4.4.1-6",
  },
]) {
  test(`does not cross the source family or attach an API token on a redirect from ${new URL(initial).origin}`, async (context) => {
    const calls = [];
    replaceToken(context);
    replaceFetch(context, async (input, init) => {
      calls.push({ url: String(input), init });
      return new Response(null, {
        status: 302,
        headers: { Location: "https://api.github.com/user" },
      });
    });
    const provider = createToolSourceProvider();
    const operation =
      tool === "emsdk"
        ? provider.latestVersion("emsdk", currentGit)
        : provider.resolve(tool, version, {
            version: "old",
            source: { kind: "archive", url: initial, sha256: archiveHash },
          });
    await assert.rejects(operation, /Unsafe Toolchain source URL/);
    assert.equal(calls.length, 1);
    assert.equal(new Headers(calls[0].init.headers).get("authorization"), null);
  });
}

test("bounds HTTP redirect loops and cancels each intermediate body", async (context) => {
  const calls = [];
  const cancelled = [];
  replaceFetch(context, async (input, init) => {
    calls.push({ url: String(input), init });
    return new Response(
      new ReadableStream({
        cancel() {
          cancelled.push(true);
        },
      }),
      { status: 302, headers: { Location: referenceUrl } },
    );
  });
  await assert.rejects(
    createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
    /Too many Toolchain source redirects/,
  );
  assert.equal(calls.length, 6);
  assert.equal(cancelled.length, 6);
});

test("rejects missing redirect destinations and cancels the body", async (context) => {
  let cancelled = false;
  replaceFetch(
    context,
    async () =>
      new Response(
        new ReadableStream({
          cancel() {
            cancelled = true;
          },
        }),
        { status: 302 },
      ),
  );
  await assert.rejects(
    createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
    /has no Location/,
  );
  assert.equal(cancelled, true);
});

test("rejects malformed redirect URLs without retaining credential or query values", async (context) => {
  let cancelled = false;
  let calls = 0;
  replaceFetch(context, async () => {
    calls += 1;
    return new Response(
      new ReadableStream({
        cancel() {
          cancelled = true;
        },
      }),
      {
        status: 302,
        headers: {
          Location:
            "https://user:owned-test-secret@[invalid]/?signature=owned-query-secret",
        },
      },
    );
  });
  await assert.rejects(
    createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
    (error) => {
      assert.equal(error.message, "Invalid Toolchain source URL");
      assert.equal("input" in error, false);
      assert.equal("cause" in error, false);
      assert.equal(JSON.stringify(error).includes("owned-test-secret"), false);
      assert.equal(JSON.stringify(error).includes("owned-query-secret"), false);
      return true;
    },
  );
  assert.equal(calls, 1);
  assert.equal(cancelled, true);
});

test("preserves HTTP failure status without exposing signed query values", async (context) => {
  let cancelled = false;
  const url =
    "https://github.com/facebook/zstd/releases/download/v1.5.7/test.zip?signature=owned-test-secret";
  replaceFetch(
    context,
    async () =>
      new Response(
        new ReadableStream({
          cancel() {
            cancelled = true;
          },
        }),
        { status: 403 },
      ),
  );
  await assert.rejects(
    createToolSourceProvider().resolve("zstdWindows", "1.5.7", {
      version: "1.5.6",
      source: { kind: "archive", url, sha256: archiveHash },
    }),
    (error) =>
      /HTTP 403/.test(error.message) &&
      !error.message.includes("owned-test-secret"),
  );
  assert.equal(cancelled, true);
});

test("rejects tag cycles including different fragments of the same HTTP URL", async (context) => {
  const calls = [];
  replaceFetch(context, async (input) => {
    calls.push(String(input));
    return objectResponse("tag", `${tagUrl}#${calls.length}`);
  });
  await assert.rejects(
    createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
    /contains a cycle/,
  );
  assert.equal(calls.length, 2);
});

test("bounds distinct annotated-tag references", async (context) => {
  const calls = [];
  replaceFetch(context, async (input) => {
    calls.push(String(input));
    return objectResponse("tag", `${tagUrl}?reference=${calls.length}`);
  });
  await assert.rejects(
    createToolSourceProvider().resolve("brotli", "1.2.1", currentGit),
    /too many references/,
  );
  assert.equal(calls.length, 33);
});

for (const unsafeUrl of unsafeUrls.filter(
  (url) => !url.startsWith("https://raw.githubusercontent.com/"),
)) {
  test(`rejects unsafe initial archive source ${unsafeUrl} before fetching`, async (context) => {
    const calls = [];
    replaceFetch(context, async (input) => {
      calls.push(String(input));
      return new Response(archive);
    });
    await assert.rejects(
      createToolSourceProvider().resolve("zstdWindows", "1.5.7", {
        version: "1.5.6",
        source: { kind: "archive", url: unsafeUrl, sha256: archiveHash },
      }),
      /(?:Invalid|Unsafe) Toolchain source URL/,
    );
    assert.deepEqual(calls, []);
  });
}

test("preserves canonical HTTPS API aliases, nested tags and empty-token requests", async (context) => {
  const originalToken = process.env.GITHUB_TOKEN;
  delete process.env.GITHUB_TOKEN;
  context.after(() => {
    if (originalToken === undefined) delete process.env.GITHUB_TOKEN;
    else process.env.GITHUB_TOKEN = originalToken;
  });
  const secondTag = `https://api.github.com/repos/google/brotli/git/tags/${"c".repeat(40)}`;
  const calls = [];
  replaceFetch(context, async (input, init) => {
    const url = String(input);
    calls.push({ url, init });
    if (url === referenceUrl)
      return objectResponse(
        "tag",
        tagUrl.replace("api.github.com", "API.GITHUB.COM:443"),
      );
    if (url === tagUrl)
      return objectResponse(
        "tag",
        secondTag.replace("api.github.com", "%61pi.github.com"),
      );
    if (url === secondTag) return objectResponse("commit");
    assert.equal(url, archiveUrl);
    return new Response(archive);
  });
  const result = await createToolSourceProvider().resolve(
    "brotli",
    "1.2.1",
    currentGit,
  );
  assert.equal(result.source.sha256, archiveHash);
  assert.deepEqual(
    calls.map((call) => call.url),
    [referenceUrl, tagUrl, secondTag, archiveUrl],
  );
  for (const call of calls)
    assert.equal(new Headers(call.init.headers).get("authorization"), null);
});
