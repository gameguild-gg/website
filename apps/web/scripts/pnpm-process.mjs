/**
 * @file Windows pnpm process resolver. Package-manager shims need a shell, so
 * this module resolves their JavaScript entry point and executes it with Node
 * directly, keeping every caller argument a literal argument.
 */

import { access } from "node:fs/promises";
import path from "node:path";

const windowsPaths = path.win32;
const relativeEntries = [
  "pnpm.exe",
  "node_modules/pnpm/bin/pnpm.cjs",
  "node_modules/corepack/dist/pnpm.js",
  "pnpm.cjs",
];

async function fileExists(candidate) {
  try {
    await access(candidate);
    return true;
  } catch {
    return false;
  }
}

function resolveOptions(options = {}) {
  return Object.fromEntries(Object.entries({
    platform: process.platform,
    nodePath: process.execPath,
    env: process.env,
    fileExists,
  }).map(([key, fallback]) => {
    const value = options[key];
    return [key, value === undefined ? fallback : value];
  }));
}

function searchDirectories(env, nodePath) {
  // Node selects the first PATH key in lexical order on Windows.
  const pathKey = Object.keys(env).filter((key) => key.toLowerCase() === "path").sort()[0];
  const searchPath = (pathKey && env[pathKey]) || "";
  return new Set([
    ...searchPath.split(";").filter(Boolean),
    env.PNPM_HOME,
    windowsPaths.dirname(nodePath),
  ].filter((directory) => typeof directory === "string" && directory.length > 0));
}

async function findExistingEntry(candidates, fileExists) {
  for (const candidate of candidates) {
    if (await fileExists(candidate)) return candidate;
  }
  return undefined;
}

/**
 * @param {NodeJS.ProcessEnv} env - Environment used for executable discovery.
 * @param {string} nodePath - Node executable used to locate adjacent shims.
 * @param {(candidate: string) => boolean | Promise<boolean>} fileExists - File existence probe.
 * @returns {Promise<string | undefined>} The first supported executable or script.
 */
async function findWindowsExecutable(env, nodePath, fileExists) {
  const managedEntry = await findExistingEntry(
    [env.npm_execpath].filter((entry) => typeof entry === "string" && windowsPaths.isAbsolute(entry) &&
      /^pnpm\.(?:cjs|mjs|js)$/i.test(windowsPaths.basename(entry))),
    fileExists,
  );
  if (managedEntry) return managedEntry;

  for (const directory of searchDirectories(env, nodePath)) {
    const executable = await findExistingEntry(
      relativeEntries.map((relative) => windowsPaths.join(directory, relative)),
      fileExists,
    );
    if (executable) return executable;
  }
  return undefined;
}

/**
 * Resolve the command and argument vector that spawn a pnpm process without a
 * shell on the current platform.
 *
 * @param {string[]} args - Literal pnpm arguments forwarded by the caller.
 * @param {{platform?: NodeJS.Platform, nodePath?: string, env?: NodeJS.ProcessEnv, fileExists?: (candidate: string) => boolean | Promise<boolean>}} [options] - Injectable environment for testing.
 * @returns {Promise<{command: string, args: string[]}>} The spawn target for pnpm.
 */
export async function resolvePnpmProcess(args, options) {
  const { platform, nodePath, env, fileExists } = resolveOptions(options);
  if (platform !== "win32") return { command: "pnpm", args };

  const executable = await findWindowsExecutable(env, nodePath, fileExists);
  if (!executable) {
    throw new Error(
      "Cannot find a pnpm executable or JavaScript entry point. Run this script through pnpm or install pnpm on PATH.",
    );
  }
  return /\.exe$/i.test(executable)
    ? { command: executable, args }
    : { command: nodePath, args: [executable, ...args] };
}
