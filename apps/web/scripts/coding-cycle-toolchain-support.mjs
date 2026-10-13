import { readFile } from "node:fs/promises";
import {
  CODE_TOOLCHAIN_V1,
  requireFrozenCodeToolchain,
} from "../../../tools/emception/grading/toolchain-binding.mjs";

/** Reject an incompatible deployment before the isolated cycle creates data. */
export async function verifyCodingCycleToolchain(manifestPath) {
  const manifest = JSON.parse(await readFile(manifestPath, "utf8"));
  requireFrozenCodeToolchain(CODE_TOOLCHAIN_V1, manifest);
}
