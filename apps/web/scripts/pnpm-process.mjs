/**
 * @file Windows pnpm process resolver. Package-manager shims need a shell, so
 * this module resolves their JavaScript entry point and executes it with Node
 * directly, keeping every caller argument a literal argument.
 */

import { existsSync } from "node:fs";
import path from "node:path";

/**
 * Resolve the command and argument vector that spawn a pnpm process without a
 * shell on the current platform.
 *
 * @param {string[]} args - Literal pnpm arguments forwarded by the caller.
 * @param {{platform?: NodeJS.Platform, nodePath?: string, env?: NodeJS.ProcessEnv, fileExists?: (candidate: string) => boolean}} [options] - Injectable environment for testing.
 * @returns {{command: string, args: string[]}} The spawn target for pnpm.
 */
export function resolvePnpmProcess(
  args,
  {
    platform = process.platform,
    nodePath = process.execPath,
    env = process.env,
    fileExists = existsSync,
  } = {},
) {
  if (platform !== "win32") return { command: "pnpm", args };

  const paths = path.win32;
  const entry = env.npm_execpath;
  if (
    entry &&
    paths.isAbsolute(entry) &&
    /^pnpm\.(?:cjs|mjs|js)$/i.test(paths.basename(entry)) &&
    fileExists(entry)
  ) {
    return { command: nodePath, args: [entry, ...args] };
  }

  // Node selects the first key in lexical order when Windows environment
  // objects contain differently cased copies of PATH.
  const pathKey = Object.keys(env)
    .filter((key) => key.toLowerCase() === "path")
    .sort()[0];
  const searchPath = (pathKey && env[pathKey]) || "";
  const directories = [
    ...searchPath.split(";").filter(Boolean),
    env.PNPM_HOME,
    paths.dirname(nodePath),
  ];
  for (const directory of new Set(directories)) {
    if (typeof directory !== "string" || directory.length === 0) continue;
    const executable = paths.join(directory, "pnpm.exe");
    if (fileExists(executable)) return { command: executable, args };
    for (const relative of [
      "node_modules/pnpm/bin/pnpm.cjs",
      "node_modules/corepack/dist/pnpm.js",
      "pnpm.cjs",
    ]) {
      const candidate = paths.join(directory, relative);
      if (fileExists(candidate))
        return { command: nodePath, args: [candidate, ...args] };
    }
  }
  throw new Error(
    "Cannot find a pnpm executable or JavaScript entry point. Run this script through pnpm or install pnpm on PATH.",
  );
}
