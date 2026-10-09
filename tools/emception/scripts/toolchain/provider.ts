import { createHash } from "node:crypto";

import type { LockedTool, ToolName } from "./lock.ts";
import type { ToolSourceProvider } from "./sources.ts";

type GitHubRef = {
  object: { sha: string; type: "commit" | "tag"; url: string };
};

const GITHUB_API_ORIGIN = "https://api.github.com";
const GITHUB_DOWNLOAD_ORIGINS = new Set([
  "https://github.com",
  "https://codeload.github.com",
  "https://release-assets.githubusercontent.com",
  "https://objects.githubusercontent.com",
]);
// mirror.msys2.org redirects to these official Tier 1 mirrors. Keep this list
// aligned with https://www.msys2.org/dev/mirrors/ when upstream mirrors change.
const MSYS2_ORIGINS = new Set([
  "https://repo.msys2.org",
  "https://mirror.msys2.org",
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
]);
const SOURCE_ORIGINS = new Set([
  GITHUB_API_ORIGIN,
  "https://raw.githubusercontent.com",
  "https://chromium.googlesource.com",
  ...GITHUB_DOWNLOAD_ORIGINS,
  ...MSYS2_ORIGINS,
]);
const REDIRECT_STATUSES = new Set([301, 302, 303, 307, 308]);
const MAX_REDIRECTS = 5;
const MAX_TAG_REFERENCES = 32;

function assertSourceUrl(
  input: string | URL,
  allowedOrigins: ReadonlySet<string>,
  base?: URL,
): URL {
  let parsed: URL;
  try {
    parsed = new URL(input, base);
  } catch {
    throw new Error("Invalid Toolchain source URL");
  }
  if (
    parsed.protocol !== "https:" ||
    parsed.username ||
    parsed.password ||
    !allowedOrigins.has(parsed.origin)
  ) {
    throw new Error("Unsafe Toolchain source URL");
  }
  return parsed;
}

function headers(): HeadersInit {
  const result: Record<string, string> = {
    Accept: "application/vnd.github+json",
  };
  if (process.env.GITHUB_TOKEN)
    result.Authorization = `Bearer ${process.env.GITHUB_TOKEN}`;
  return result;
}

async function get(url: string): Promise<Response> {
  let current = assertSourceUrl(url, SOURCE_ORIGINS);
  const apiRequest = current.origin === GITHUB_API_ORIGIN;
  const allowedOrigins = MSYS2_ORIGINS.has(current.origin)
    ? MSYS2_ORIGINS
    : GITHUB_DOWNLOAD_ORIGINS.has(current.origin)
      ? GITHUB_DOWNLOAD_ORIGINS
      : new Set([current.origin]);

  for (let redirects = 0; ; redirects += 1) {
    const response = await fetch(current, {
      redirect: "manual",
      headers:
        apiRequest && current.origin === GITHUB_API_ORIGIN
          ? headers()
          : undefined,
    });
    if (REDIRECT_STATUSES.has(response.status)) {
      const location = response.headers.get("location");
      await response.body?.cancel();
      if (!location)
        throw new Error("Toolchain source redirect has no Location");
      if (redirects >= MAX_REDIRECTS)
        throw new Error("Too many Toolchain source redirects");
      current = assertSourceUrl(location, allowedOrigins, current);
      continue;
    }
    if (!response.ok) {
      await response.body?.cancel();
      throw new Error(
        `HTTP ${response.status} for ${current.origin}${current.pathname}`,
      );
    }
    return response;
  }
}

async function json<T>(url: string): Promise<T> {
  return get(url).then((response) => response.json() as Promise<T>);
}

async function text(url: string): Promise<string> {
  return get(url).then((response) => response.text());
}

async function bytes(url: string): Promise<Buffer> {
  const body = await (await get(url)).arrayBuffer();
  return Buffer.from(body as ArrayBuffer);
}

async function archiveIdentity(repository: string, commit: string) {
  const url = `https://codeload.github.com/${repository}/tar.gz/${commit}`;
  const body = await bytes(url);
  return { url, sha256: createHash("sha256").update(body).digest("hex") };
}

async function commitForTag(repository: string, tag: string): Promise<string> {
  let object = (
    await json<GitHubRef>(
      `https://api.github.com/repos/${repository}/git/ref/tags/${encodeURIComponent(tag)}`,
    )
  ).object;
  const visited = new Set<string>();
  while (object.type === "tag") {
    const tagUrl = assertSourceUrl(object.url, new Set([GITHUB_API_ORIGIN]));
    // Fragments do not reach the HTTP server, so cannot identify another tag request.
    const identity = `${tagUrl.origin}${tagUrl.pathname}${tagUrl.search}`;
    if (visited.has(identity))
      throw new Error(`Tag ${repository}@${tag} contains a cycle`);
    if (visited.size >= MAX_TAG_REFERENCES)
      throw new Error(`Tag ${repository}@${tag} has too many references`);
    visited.add(identity);
    object = (await json<GitHubRef>(tagUrl.href)).object;
  }
  if (!/^[0-9a-f]{40}$/.test(object.sha))
    throw new Error(`Tag ${repository}@${tag} did not resolve to a commit`);
  return object.sha;
}

function versionParts(version: string): number[] {
  const match = version.match(/\d+(?:\.\d+)*/);
  return match ? match[0].split(".").map(Number) : [];
}

function compareVersions(left: string, right: string): number {
  const a = versionParts(left);
  const b = versionParts(right);
  for (let index = 0; index < Math.max(a.length, b.length); index += 1) {
    const difference = (a[index] ?? 0) - (b[index] ?? 0);
    if (difference !== 0) return difference;
  }
  return left.localeCompare(right);
}

function tagFor(name: ToolName, version: string): string {
  if (name === "cmake" || name === "brotli")
    return `v${version.replace(/^v/, "")}`;
  if (name === "imgui")
    return version.startsWith("v") ? version : `v${version}`;
  return version;
}

function versionFromTag(name: ToolName, tag: string): string | null {
  if (name === "cmake") return /^v(3\.\d+\.\d+)$/.exec(tag)?.[1] ?? null;
  if (name === "brotli") return /^v(\d+\.\d+\.\d+)$/.exec(tag)?.[1] ?? null;
  if (name === "imgui") return /^v\d+\.\d+(?:\.\d+)?$/.test(tag) ? tag : null;
  if (name === "emsdk") return /^\d+\.\d+\.\d+$/.test(tag) ? tag : null;
  return /^\d+(?:\.\d+)+$/.test(tag) ? tag : null;
}

async function lockedGitArchive(
  name: ToolName,
  version: string,
  repository: string,
  tag?: string,
): Promise<LockedTool> {
  const commit = await commitForTag(repository, tag ?? tagFor(name, version));
  const identity = await archiveIdentity(repository, commit);
  return {
    version,
    source: { kind: "git-archive", repository, commit, ...identity },
  };
}

async function latestGitHubVersion(
  name: ToolName,
  repository: string,
): Promise<string> {
  const tags = await json<Array<{ name: string }>>(
    `https://api.github.com/repos/${repository}/tags?per_page=100`,
  );
  const versions = tags
    .map(({ name: tag }) => versionFromTag(name, tag))
    .filter((value): value is string => Boolean(value));
  if (versions.length === 0)
    throw new Error(`No allowed release tags found for ${repository}`);
  return versions.sort(compareVersions).at(-1)!;
}

async function emsdkComponents(
  emsdk: LockedTool,
): Promise<Partial<Record<ToolName, LockedTool>>> {
  if (emsdk.source.kind !== "git-archive")
    throw new Error("emsdk must use a git-archive source");
  const commit = emsdk.source.commit;
  const releases = await json<{ releases: Record<string, string> }>(
    `https://raw.githubusercontent.com/emscripten-core/emsdk/${commit}/emscripten-releases-tags.json`,
  );
  const releaseRevision = releases.releases[emsdk.version];
  if (!releaseRevision)
    throw new Error(`EMSDK ${emsdk.version} has no releases revision`);
  const depsBase64 = await text(
    `https://chromium.googlesource.com/emscripten-releases/+/${releaseRevision}/DEPS?format=TEXT`,
  );
  const deps = Buffer.from(depsBase64, "base64").toString("utf8");
  const llvmCommit = /'llvm_project_revision': '([0-9a-f]{40})'/.exec(
    deps,
  )?.[1];
  const binaryenCommit = /'binaryen_revision': '([0-9a-f]{40})'/.exec(
    deps,
  )?.[1];
  if (!llvmCommit || !binaryenCommit)
    throw new Error(
      `Unable to derive component revisions for EMSDK ${emsdk.version}`,
    );

  const [llvmVersionSource, binaryenVersionSource, manifest] =
    await Promise.all([
      text(
        `https://raw.githubusercontent.com/llvm/llvm-project/${llvmCommit}/cmake/Modules/LLVMVersion.cmake`,
      ),
      text(
        `https://raw.githubusercontent.com/WebAssembly/binaryen/${binaryenCommit}/CMakeLists.txt`,
      ),
      json<{ tools: Array<{ id: string; version: string }> }>(
        `https://raw.githubusercontent.com/emscripten-core/emsdk/${commit}/emsdk_manifest.json`,
      ),
    ]);
  const llvmPart = (part: string) =>
    new RegExp(`LLVM_VERSION_${part}\\s+(\\d+)`).exec(llvmVersionSource)?.[1];
  const llvmVersion = `${llvmPart("MAJOR")}.${llvmPart("MINOR")}.${llvmPart("PATCH")}${/LLVM_VERSION_SUFFIX\s+git/.test(llvmVersionSource) ? "git" : ""}`;
  const binaryenVersion = /project\(binaryen[^\n]*VERSION\s+(\d+)/.exec(
    binaryenVersionSource,
  )?.[1];
  const pythonVersion = manifest.tools
    .filter(
      (tool) => tool.id === "python" && /^\d+\.\d+\.\d+$/.test(tool.version),
    )
    .map((tool) => tool.version)
    .sort(compareVersions)
    .at(-1);
  if (!binaryenVersion || !pythonVersion || llvmVersion.includes("undefined")) {
    throw new Error(
      `Unable to derive component versions for EMSDK ${emsdk.version}`,
    );
  }

  const [llvmIdentity, binaryenIdentity, python] = await Promise.all([
    archiveIdentity("llvm/llvm-project", llvmCommit),
    archiveIdentity("WebAssembly/binaryen", binaryenCommit),
    lockedGitArchive(
      "python",
      pythonVersion,
      "python/cpython",
      `v${pythonVersion}`,
    ),
  ]);
  return {
    llvm: {
      version: llvmVersion,
      derivedFrom: "emsdk",
      source: {
        kind: "git-archive",
        repository: "llvm/llvm-project",
        commit: llvmCommit,
        ...llvmIdentity,
      },
    },
    binaryen: {
      version: binaryenVersion,
      derivedFrom: "emsdk",
      source: {
        kind: "git-archive",
        repository: "WebAssembly/binaryen",
        commit: binaryenCommit,
        ...binaryenIdentity,
      },
    },
    python: { ...python, derivedFrom: "emsdk" },
    sdl3: {
      version: `emsdk-${emsdk.version}`,
      derivedFrom: "emsdk",
      source: {
        kind: "emsdk-component",
        emsdkVersion: emsdk.version,
        revision: releaseRevision,
        contentHash: emsdk.source.sha256,
      },
    },
  };
}

export function createToolSourceProvider(): ToolSourceProvider {
  return {
    async resolve(name, requested, current) {
      if (!current) throw new Error(`Cannot resolve absent tool ${name}`);
      const version =
        requested === "latest"
          ? await this.latestVersion(name, current)
          : requested;
      if (version === current.version) return current;
      if (current.source.kind === "git-archive") {
        return lockedGitArchive(name, version, current.source.repository);
      }
      if (current.source.kind === "archive") {
        if (name === "msys2Make" && requested === "latest") return current;
        const url = current.source.url.replaceAll(current.version, version);
        const body = await bytes(url);
        return {
          version,
          source: {
            kind: "archive",
            url,
            sha256: createHash("sha256").update(body).digest("hex"),
          },
        };
      }
      throw new Error(`${name} cannot be updated independently`);
    },
    inspectEmsdk: emsdkComponents,
    async latestVersion(name, current) {
      if (name === "emsdk") {
        const metadata = await json<{ aliases: { latest: string } }>(
          "https://raw.githubusercontent.com/emscripten-core/emsdk/main/emscripten-releases-tags.json",
        );
        return metadata.aliases.latest;
      }
      if (current.source.kind === "git-archive")
        return latestGitHubVersion(name, current.source.repository);
      if (name === "zstdWindows")
        return latestGitHubVersion("brotli", "facebook/zstd");
      return current.version;
    },
  };
}
