/** @file Register and discover repository-scoped development processes and remove their registrations. */

import { createHash } from "node:crypto";
import { readFileSync, unlinkSync } from "node:fs";
import { mkdir, readFile, readdir, realpath, rename, unlink, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import path from "node:path";
import { fileURLToPath } from "node:url";

export const repositoryRoot = await realpath(
  fileURLToPath(new URL("../", import.meta.url)),
);

const repositoryKey = createHash("sha256")
  .update(repositoryRoot)
  .digest("hex")
  .slice(0, 16);

export const runtimeDirectory = path.join(
  tmpdir(),
  "gameguild-dev",
  repositoryKey,
);

function runtimeFile(mode, pid) {
  return path.join(runtimeDirectory, `${mode}-${pid}.json`);
}

export async function registerDevProcess(mode) {
  const file = runtimeFile(mode, process.pid);

  try {
    await mkdir(runtimeDirectory, { recursive: true, mode: 0o700 });
    const temporaryFile = `${file}.${process.pid}.tmp`;
    await writeFile(
      temporaryFile,
      `${JSON.stringify(
        {
          version: 1,
          mode,
          pid: process.pid,
          root: repositoryRoot,
          script: process.argv[1],
          startedAt: new Date().toISOString(),
        },
        null,
        2,
      )}\n`,
      { mode: 0o600 },
    );
    await rename(temporaryFile, file);
  } catch (error) {
    console.warn(
      `[${mode}] could not register the development process: ${
        error instanceof Error ? error.message : String(error)
      }`,
    );
    return () => {};
  }

  let removed = false;
  // Node cannot wait for asynchronous I/O in an exit listener.
  const unregister = () => {
    if (removed) return;
    removed = true;

    try {
      const record = JSON.parse(readFileSync(file, "utf8"));
      if (record.pid === process.pid) unlinkSync(file);
    } catch (error) {
      if (error?.code !== "ENOENT") {
        console.warn(
          `[${mode}] could not remove the development process registration: ${
            error instanceof Error ? error.message : String(error)
          }`,
        );
      }
    }
  };

  process.once("exit", unregister);
  return unregister;
}

export async function listRegisteredDevProcesses() {
  let files;
  try {
    files = await readdir(runtimeDirectory, { withFileTypes: true });
  } catch (error) {
    if (error?.code === "ENOENT") return [];
    throw error;
  }

  const records = [];
  for (const entry of files) {
    if (!entry.isFile() || !entry.name.endsWith(".json")) continue;
    const file = path.join(runtimeDirectory, entry.name);
    try {
      const record = JSON.parse(await readFile(file, "utf8"));
      if (
        Number.isInteger(record.pid) &&
        typeof record.mode === "string" &&
        record.root === repositoryRoot
      ) {
        records.push({ ...record, file });
      } else {
        await unlink(file);
      }
    } catch {
      try {
        await unlink(file);
      } catch {
        // Another process may have removed the stale registration.
      }
    }
  }

  return records;
}

export async function removeDevProcessRegistration(file) {
  try {
    await unlink(file);
  } catch (error) {
    if (error?.code !== "ENOENT") throw error;
  }
}
