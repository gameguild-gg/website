#!/usr/bin/env node

import { appendFileSync, readFileSync } from "node:fs";
import { execFileSync } from "node:child_process";
import { pathToFileURL } from "node:url";

const emptyClassification = Object.freeze({
  api: false,
  web: false,
  apiRuntimeChanged: false,
  webRuntimeChanged: false,
  testingLab: false,
  economyCritical: false,
  openApi: false,
  migration: false,
  runtimeChanged: false,
});

const economyPathPattern =
  /(?:^|[./-])(?:economy|commerce|billing|payments?|payouts?|treasury|marketplace|bounties|ad-?rewards?|kyc|financial-?crime)(?:[./-]|$)/i;
const testingLabPathPattern = /(?:testing-?lab|testinglab)/i;
const migrationPathPattern =
  /(?:^|\/)(?:migrations?|database\/migrations?)(?:\/|$)|\.(?:sql|ddl)$/i;
const apiContractPathPattern =
  /(?:^|\/)(?:controllers?|endpoints?|contracts?|dtos?|requests?|responses?|openapi)(?:\/|$)|(?:^|\/)[^/]*(?:controller|endpoint|contracts?|dtos?|requests?|responses?)\.cs$/i;
const testPathPattern =
  /(?:^|\/)(?:tests?|__tests__|e2e)(?:\/|$)|\.(?:test|spec)\.[cm]?[jt]sx?$/i;

function normalizePath(filePath) {
  return filePath.trim().replaceAll("\\", "/").replace(/^\.\//, "");
}

function isDocumentationOrCi(filePath) {
  return (
    filePath.startsWith("docs/") ||
    filePath.startsWith(".github/") ||
    filePath.startsWith("scripts/ci/") ||
    filePath === "README.md" ||
    filePath.endsWith(".md") ||
    filePath.startsWith(".codegraph/")
  );
}

function isNodeRuntimeRoot(filePath) {
  return [
    "package.json",
    "pnpm-lock.yaml",
    "pnpm-workspace.yaml",
    "turbo.json",
    "tsconfig.json",
    "tsconfig.base.json",
  ].includes(filePath);
}

function isApiBuildRoot(filePath) {
  return [
    "Directory.Build.props",
    "Directory.Packages.props",
    "global.json",
  ].includes(filePath);
}

function isSharedNodePackage(filePath) {
  return (
    filePath.startsWith("packages/") &&
    !filePath.startsWith("packages/testing/")
  );
}

function isRuntimeConfiguration(filePath) {
  return (
    filePath.startsWith("deploy/") ||
    filePath.startsWith("infra/") ||
    filePath.startsWith("infrastructure/") ||
    /(?:^|\/)(?:dockerfile(?:\.[^/]+)?|compose(?:\.[^/]+)?\.ya?ml)$/i.test(
      filePath,
    )
  );
}

export function classifyReleaseChanges(filePaths) {
  const classification = { ...emptyClassification };

  for (const rawPath of filePaths) {
    const filePath = normalizePath(rawPath);
    // Verification infrastructure must run its selected API suites without deploying the API.
    if (
      [
        "scripts/ci/select-affected-dotnet-tests.mjs",
        "scripts/ci/run-affected-dotnet-tests.sh",
        "scripts/ci/api-integration-filter.mjs",
        "scripts/ci/economy-integration-shards.json",
        "scripts/ci/tests/api-integration-filter.test.mjs",
        "scripts/ci/tests/select-affected-dotnet-tests.test.mjs",
      ].includes(filePath)
    ) {
      classification.api = true;
    }
    if (
      [
        "scripts/ci/verify-openapi-client.sh",
        "scripts/ci/verify-openapi-surface.sh",
      ].includes(filePath)
    ) {
      classification.openApi = true;
    }
    // Changes to the gate itself must exercise the complete Economy contract,
    // while remaining CI-only changes must not deploy runtime services.
    if (
      [
        ".github/workflows/pr-verify.yml",
        "scripts/ci/verify-economy.sh",
        "scripts/ci/economy-gate.sh",
        "scripts/ci/economy-projects.json",
        "scripts/ci/economy-artifacts.sh",
        "scripts/ci/economy-integration-shards.json",
        "scripts/ci/api-integration-filter.mjs",
        "scripts/ci/tests/api-integration-filter.test.mjs",
        "scripts/ci/classify-release-changes.mjs",
        "scripts/ci/tests/verify-economy.sh",
        "scripts/ci/tests/classify-release-changes.test.mjs",
      ].includes(filePath)
    ) {
      classification.economyCritical = true;
    }
    if (!filePath || isDocumentationOrCi(filePath)) continue;

    const isTest = testPathPattern.test(filePath);
    const isApi = filePath.startsWith("apps/api/");
    const isWeb = filePath.startsWith("apps/web/");
    const isShared = isSharedNodePackage(filePath);
    const isNodeRoot = isNodeRuntimeRoot(filePath);
    const isApiRoot = isApiBuildRoot(filePath);
    const isRuntimeConfig = isRuntimeConfiguration(filePath);

    if (isApi) classification.api = true;
    if (isWeb) classification.web = true;

    if (isShared || isNodeRoot) {
      classification.web = true;
    }

    if (isApiRoot) classification.api = true;

    if (isRuntimeConfig) {
      classification.api = true;
      classification.web = true;
    }

    if (testingLabPathPattern.test(filePath)) classification.testingLab = true;
    if (economyPathPattern.test(filePath))
      classification.economyCritical = true;

    if (
      (isApi &&
        filePath.startsWith("apps/api/Source/") &&
        apiContractPathPattern.test(filePath)) ||
      (isApi && filePath.includes("/Modules/")) ||
      filePath.startsWith("packages/infrastructure/client/src/generated/") ||
      filePath.includes("openapi")
    ) {
      classification.openApi = true;
    }

    if (migrationPathPattern.test(filePath)) {
      classification.api = true;
      classification.openApi = true;
      classification.migration = true;
    }

    if (!isTest) {
      if (isApi || isApiRoot) classification.apiRuntimeChanged = true;
      if (isWeb || isShared || isNodeRoot)
        classification.webRuntimeChanged = true;
      if (isRuntimeConfig) {
        classification.apiRuntimeChanged = true;
        classification.webRuntimeChanged = true;
      }

      classification.runtimeChanged =
        classification.apiRuntimeChanged || classification.webRuntimeChanged;
    }
  }

  return Object.freeze(classification);
}

export function releaseServiceMatrix(classification) {
  return ["api", "web"].filter((service) => classification[service]);
}

export function runtimeReleaseServiceMatrix(classification) {
  return ["api", "web"].filter(
    (service) => classification[`${service}RuntimeChanged`],
  );
}

function parseArguments(argv) {
  const options = { base: "", head: "HEAD", filesFrom: "" };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    const value = argv[index + 1];

    if (argument === "--base" && value) {
      options.base = value;
      index += 1;
    } else if (argument === "--head" && value) {
      options.head = value;
      index += 1;
    } else if (argument === "--files-from" && value) {
      options.filesFrom = value;
      index += 1;
    } else {
      throw new TypeError(`Unknown or incomplete argument: ${argument ?? ""}`);
    }
  }

  return options;
}

function readChangedFiles(options) {
  if (options.filesFrom) {
    return readFileSync(options.filesFrom, "utf8")
      .split(/\r?\n/u)
      .filter(Boolean);
  }

  if (!options.base) {
    throw new TypeError("--base is required when --files-from is not provided");
  }

  const output = execFileSync(
    "git",
    [
      "diff",
      "--name-only",
      "--diff-filter=ACMR",
      `${options.base}...${options.head}`,
    ],
    { encoding: "utf8" },
  );
  return output.split(/\r?\n/u).filter(Boolean);
}

function writeGitHubOutputs(classification, changedFiles) {
  const outputPath = process.env.GITHUB_OUTPUT;
  if (!outputPath) return;

  const lines = [
    ...Object.entries(classification).map(
      ([key, value]) => `${key}=${String(value)}`,
    ),
    `changedFiles=${JSON.stringify(changedFiles.map(normalizePath))}`,
    `serviceMatrix=${JSON.stringify(releaseServiceMatrix(classification))}`,
    `runtimeServiceMatrix=${JSON.stringify(runtimeReleaseServiceMatrix(classification))}`,
  ];
  appendFileSync(outputPath, `${lines.join("\n")}\n`, "utf8");
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  const changedFiles = readChangedFiles(options);
  const classification = classifyReleaseChanges(changedFiles);
  writeGitHubOutputs(classification, changedFiles);
  process.stdout.write(
    `${JSON.stringify({ changedFiles, ...classification }, null, 2)}\n`,
  );
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  main();
}
