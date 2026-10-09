import assert from "node:assert/strict";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import test from "node:test";

test("Python subprocess dispatch preserves IPC without persistent argument logging", () => {
  const python =
    process.env.PYTHON ?? (process.platform === "win32" ? "python" : "python3");
  const result = spawnSync(
    python,
    [
      "-B",
      fileURLToPath(new URL("./subprocess_shim.test.py", import.meta.url)),
    ],
    { encoding: "utf8", timeout: 30_000 },
  );
  assert.equal(result.error, undefined, result.error?.message);
  assert.equal(result.status, 0, result.stdout + "\n" + result.stderr);
  assert.match(result.stderr, /Ran 5 tests/);
});
