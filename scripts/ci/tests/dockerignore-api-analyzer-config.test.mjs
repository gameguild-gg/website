import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";

// The API image's publish step runs `dotnet publish` with
// TreatWarningsAsErrors=true (SharedKernel sets it in its csproj). Sonar
// analyzer severities are only downgraded (S-rules → none) in the ROOT
// .editorconfig — there is no apps/api-scoped one. If .editorconfig is absent
// from the build context, Roslyn applies default severities and the publish
// fails on rules the repo has explicitly reviewed and waived (S3928, S3267,
// S1066, S125, ...). This test locks the two invariants that keep the image
// build identical to CI:
//
//   1. .dockerignore must allow .editorconfig into the build context.
//   2. apps/api/Dockerfile must COPY it into the build stage workdir (/src),
//      where Roslyn discovers it as an ancestor of Source/**.
//
// Mirrors scripts/ci/tests/dockerignore-pnpm-patches.test.mjs.

const repositoryRoot = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../..",
);

const read = (relativePath) =>
  readFileSync(join(repositoryRoot, relativePath), "utf8");

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
      .replace(/\u0000/g, ".*");
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

test("the root .editorconfig is allowed into the Docker build context", () => {
  assert.ok(
    isAllowedByDockerignore(read(".dockerignore"), ".editorconfig"),
    "`.editorconfig` is excluded from the Docker build context. " +
      "The API image publish step needs it (Sonar S-rule severities are only " +
      "downgraded there); add `!.editorconfig` to .dockerignore.",
  );
});

test("apps/api/Dockerfile copies .editorconfig into the build stage", () => {
  const dockerfile = read("apps/api/Dockerfile");
  const buildStage = dockerfile.match(
    /FROM\s+\S+\s+AS\s+build([\s\S]*?)(?=\nFROM\s)/,
  );
  assert.ok(buildStage, "apps/api/Dockerfile must declare a `build` stage");
  assert.match(
    buildStage[1],
    /COPY\s+\.editorconfig\s+\.\//,
    "the api build stage must `COPY .editorconfig ./` so Roslyn resolves the " +
      "repository severity overrides instead of Sonar defaults",
  );
});

test("the root .editorconfig actually downgrades Sonar analyzer severities", () => {
  const editorconfig = read(".editorconfig");
  const downgraded = [
    ...editorconfig.matchAll(/^\s*dotnet_diagnostic\.(S\d+)\.severity\s*=\s*none/gm),
  ];
  assert.ok(
    downgraded.length >= 50,
    `expected the root .editorconfig to waive >=50 Sonar rules, found ${downgraded.length}`,
  );
});
