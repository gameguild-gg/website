#!/usr/bin/env node

import { execFileSync } from "node:child_process";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";
import { pathToFileURL } from "node:url";

function normalizePath(filePath) {
  return filePath.trim().replaceAll("\\", "/").replace(/^\.\//, "");
}

export function selectAffectedDotnetTestNames(filePaths, availableProjects) {
  const selected = new Set();
  let requiresCoreFallback = false;

  for (const rawPath of filePaths) {
    const filePath = normalizePath(rawPath);
    const moduleMatch = filePath.match(
      /^apps\/api\/Source\/Modules\/(GameGuild\.[^/]+)\//u,
    );
    const testProjectMatch = filePath.match(
      /^apps\/api\/tests\/(GameGuild\.[^/]+Tests)\//u,
    );

    if (moduleMatch?.[1]) {
      const testName = `${moduleMatch[1]}.UnitTests`;
      if (availableProjects.includes(testName)) selected.add(testName);
      continue;
    }

    if (
      testProjectMatch?.[1] &&
      !testProjectMatch[1].endsWith("PerformanceTests") &&
      availableProjects.includes(testProjectMatch[1])
    ) {
      selected.add(testProjectMatch[1]);
      continue;
    }

    if (filePath.startsWith("apps/api/Source/")) requiresCoreFallback = true;
  }

  if (requiresCoreFallback) {
    for (const fallback of ["GameGuild.API.UnitTests", "GameGuild.SharedKernel.UnitTests"]) {
      if (availableProjects.includes(fallback)) selected.add(fallback);
    }
  }

  return [...selected].sort();
}

function parseArguments(argv) {
  const options = { base: "", head: "HEAD", filesFrom: "" };

  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    const value = argv[index + 1];
    if (argument === "--base" && value) options.base = value;
    else if (argument === "--head" && value) options.head = value;
    else if (argument === "--files-from" && value) options.filesFrom = value;
    else throw new TypeError(`Unknown or incomplete argument: ${argument ?? ""}`);
    index += 1;
  }

  return options;
}

function changedFiles(options) {
  if (options.filesFrom) {
    return readFileSync(options.filesFrom, "utf8").split(/\r?\n/u).filter(Boolean);
  }
  if (!options.base) throw new TypeError("--base is required when --files-from is not provided");

  return execFileSync(
    "git",
    ["diff", "--name-only", "--diff-filter=ACMR", `${options.base}...${options.head}`],
    { encoding: "utf8" },
  )
    .split(/\r?\n/u)
    .filter(Boolean);
}

function availableTestProjects(testRoot) {
  return readdirSync(testRoot, { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.endsWith("Tests"))
    .map((entry) => entry.name)
    .sort();
}

function main() {
  const options = parseArguments(process.argv.slice(2));
  const testRoot = join(process.cwd(), "apps", "api", "tests");
  const selected = selectAffectedDotnetTestNames(
    changedFiles(options),
    availableTestProjects(testRoot),
  );

  for (const name of selected) {
    process.stdout.write(`apps/api/tests/${name}/${name}.csproj\n`);
  }
}

if (import.meta.url === pathToFileURL(process.argv[1] ?? "").href) {
  main();
}
