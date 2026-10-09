import { readFileSync } from "node:fs";
import { join } from "node:path";

/**
 * Reads the pnpm settings that govern the dependency security gate from
 * pnpm-workspace.yaml — the canonical pnpm 10 location since the root
 * package.json migration. Parses only the flat top-level sections the CI
 * gate asserts on (patchedDependencies, onlyBuiltDependencies) and keeps
 * every other section observable for absence checks (auditConfig and
 * friends must stay undefined). Malformed entries throw: the gate fails
 * closed rather than silently skipping a pinned patch.
 */
export function readPnpmSecurityConfig(repositoryRoot) {
  const lines = readFileSync(join(repositoryRoot, "pnpm-workspace.yaml"), "utf8").split(/\r?\n/u);
  const sections = new Map();
  let current = null;

  for (const line of lines) {
    if (!line.trim() || line.trim().startsWith("#")) continue;
    if (/^\S/u.test(line)) {
      const match = line.match(/^([^:#\s]+):\s*(?:#.*)?$/u);
      if (!match) {
        throw new Error(`Unsupported top-level entry in pnpm-workspace.yaml: ${line}`);
      }
      current = [];
      sections.set(match[1], current);
      continue;
    }
    if (current) current.push(line.replace(/^[\t ]*/u, ""));
  }

  const config = {};
  for (const [name, entries] of sections) {
    if (name === "onlyBuiltDependencies") {
      config[name] = parseListEntries(name, entries);
    } else if (name === "patchedDependencies") {
      config[name] = parseMapEntries(name, entries);
    } else if (name !== "packages" && name !== "overrides") {
      config[name] = entries;
    }
  }
  return config;
}

function parseListEntries(section, entries) {
  return entries.map((entry) => {
    const match = entry.match(/^-\s+(?:"([^"]+)"|'([^']+)'|([^#\s]+))\s*(?:#.*)?$/u);
    if (!match) {
      throw new Error(`Malformed ${section} entry in pnpm-workspace.yaml: ${entry}`);
    }
    return match[1] ?? match[2] ?? match[3];
  });
}

function parseMapEntries(section, entries) {
  const map = {};
  for (const entry of entries) {
    const match = entry.match(
      /^(?:"([^"]+)"|'([^']+)'|([^:\s]+)):\s*(?:"([^"]*)"|'([^']*)'|([^#\s]+))\s*(?:#.*)?$/u,
    );
    if (!match) {
      throw new Error(`Malformed ${section} entry in pnpm-workspace.yaml: ${entry}`);
    }
    map[match[1] ?? match[2] ?? match[3]] = match[4] ?? match[5] ?? match[6];
  }
  return map;
}
