import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

// The web image builds in two stages that must agree on how pnpm resolves
// patch files:
//
//   1. `pruner` runs `turbo prune --docker` on `COPY . .`, which is filtered by
//      .dockerignore. turbo only emits patches/ into out/json when the files are
//      present in that build context.
//   2. `deps` runs `pnpm install --frozen-lockfile`, which reads the root
//      package.json `pnpm.patchedDependencies` map and every `path:` entry in
//      pnpm-lock.yaml, resolving each patch by its repository-relative path.
//
// If .dockerignore excludes patches/, the pruner's COPY drops them, turbo omits
// them from out/json, and the deps stage fails with a hard `ENOENT ... patches/
// braces@3.0.3.patch` (exit 254). This test locks the invariant so a deny-all
// .dockerignore edit cannot silently re-break the Devtron build.

const repositoryRoot = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../..",
);

const read = (relativePath) =>
  readFileSync(join(repositoryRoot, relativePath), "utf8");

// pnpm v10 keeps patchedDependencies in the root pnpm-workspace.yaml (pnpm v9
// kept them in the root package.json `pnpm` key). Accept either location so the
// test survives the config move.
const readPatchedDependencies = () => {
  const workspace = read("pnpm-workspace.yaml");
  const block = workspace.match(/\npatchedDependencies:\s*\n([\s\S]*?)(?=\n\S|$)/);
  if (block) {
    const entries = {};
    for (const line of block[1].split("\n")) {
      const match = line.match(/^\s*"?([^":\s]+)"?:\s*"?([^"\s]+)"?\s*$/);
      if (match) entries[match[1]] = match[2];
    }
    if (Object.keys(entries).length > 0) return entries;
  }
  return JSON.parse(read("package.json")).pnpm?.patchedDependencies ?? {};
};

// Minimal .dockerignore matcher: returns true when the most specific matching
// pattern for `path` is a negation (an allow rule). Only the subset needed here
// is implemented: `**`, `*`, leading `!`, and literal path prefixes.
const isAllowedByDockerignore = (dockerignore, path) => {
  const segments = path.split("/");
  let allowed = false;
  for (const rawLine of dockerignore.split("\n")) {
    const line = rawLine.trim();
    if (line === "" || line.startsWith("#")) continue;
    const negated = line.startsWith("!");
    const pattern = (negated ? line.slice(1) : line).replace(/\/$/, "");
    const escaped = pattern
      .replace(/[.+^${}()|[\]\\]/g, "\\$&")
      .replace(/\*\*/g, "\u0000")
      .replace(/\*/g, "[^/]*")
      .replaceAll("\u0000", ".*");
    const anchored = pattern.includes("/");
    let matches;
    if (anchored) {
      matches = new RegExp(`^${escaped}$`).test(path);
    } else {
      matches = segments.some((segment) =>
        new RegExp(`^${escaped}$`).test(segment),
      );
    }
    if (matches) allowed = negated;
  }
  return allowed;
};

const depsStage = (dockerfile) => {
  const match = dockerfile.match(/FROM\s+\S+\s+AS\s+deps([\s\S]*?)(?=\nFROM\s)/);
  assert.ok(match, "apps/web/Dockerfile must declare a `deps` stage");
  return match[1];
};

test("every local pnpm patch path is reachable from the pruner build context", () => {
  const patched = readPatchedDependencies();
  assert.ok(
    Object.keys(patched).length > 0,
    "pnpm-workspace.yaml (or root package.json) must declare at least one patchedDependencies entry",
  );

  const dockerignore = read(".dockerignore");
  const dockerfile = read("apps/web/Dockerfile");

  // The pruner `COPY . .` only sees files the .dockerignore allows, and turbo's
  // out/json tree preserves each patch at its repository-relative path.
  for (const patchPath of Object.values(patched)) {
    assert.ok(
      isAllowedByDockerignore(dockerignore, patchPath),
      `apps/web/Dockerfile pruner stage will not receive ${patchPath}: ` +
        `.dockerignore excludes it. Add \`!patches\` (and keep it above the ` +
        `deny-all \`**\`) so turbo prune emits it into out/json.`,
    );
  }

  // The deps stage copies out/json wholesale and installs against the lockfile,
  // so the patch must live under the copied tree (root-relative paths).
  const deps = depsStage(dockerfile);
  assert.match(
    deps,
    /COPY\s+--from=pruner\s+\/repo\/out\/json\/\s+\.\//,
    "the deps stage must copy the pruned json tree (including patches/) into place",
  );
});

test("pnpm-lock.yaml patch paths stay in sync with package.json patchedDependencies", () => {
  const declared = readPatchedDependencies();
  const lock = read("pnpm-lock.yaml");

  const lockSection =
    lock.match(/\npatchedDependencies:\n([\s\S]*?)\n\S/)?.[1] ?? "";
  assert.ok(lockSection, "pnpm-lock.yaml must contain a patchedDependencies section");

  for (const [specifier, patchPath] of Object.entries(declared)) {
    assert.ok(
      lockSection.includes(patchPath),
      `pnpm-lock.yaml is missing the path for ${specifier} (${patchPath}); ` +
        `run \`pnpm install\` to refresh the lockfile`,
    );
    assert.ok(
      isAllowedByDockerignore(read(".dockerignore"), patchPath),
      `${patchPath} referenced by the lockfile is excluded from the Docker build context`,
    );
  }
});
