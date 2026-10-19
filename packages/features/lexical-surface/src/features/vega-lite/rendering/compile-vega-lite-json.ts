import type { compile, TopLevelSpec } from "vega-lite";
import { isVegaSpecRecord } from "../data/vega-spec";

/** Compile dynamic JSON through the library's runtime validation boundary. */
export function compileVegaLiteJson(
  compiler: typeof compile,
  spec: unknown,
): ReturnType<typeof compile> {
  if (!isVegaSpecRecord(spec)) {
    throw new Error("Specification must be a JSON object");
  }

  // Object shape alone does not establish TopLevelSpec. The SDK owns validation
  // of every supported composition; expose only its compiled result and let
  // failures propagate. Keep this interoperability assertion at one boundary.
  return compiler(spec as unknown as TopLevelSpec);
}
