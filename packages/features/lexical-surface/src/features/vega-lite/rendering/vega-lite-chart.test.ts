import { describe, expect, it } from "vitest";
import { isPlainVegaSpec } from "./vega-lite-chart";
import type { VegaSpecRecord } from "../data/vega-spec";

describe("isPlainVegaSpec", () => {
  it.each([
    { name: "Vega-Lite schema wins over Vega keys", spec: { $schema: "https://vega.github.io/schema/vega-lite/v6.json", marks: [] }, expected: false },
    { name: "Vega schema wins over Vega-Lite keys", spec: { $schema: "https://vega.github.io/schema/vega/v6.json", mark: "bar" }, expected: true },
    { name: "Vega-Lite keys win without a schema", spec: { mark: "bar", marks: [] }, expected: false },
    { name: "Vega signals work without a schema", spec: { signals: [] }, expected: true },
    { name: "unknown schema defaults to Vega-Lite", spec: { $schema: "custom" }, expected: false },
    { name: "empty object defaults to Vega-Lite", spec: {}, expected: false },
    { name: "non-string schema does not hide Vega keys", spec: { $schema: 7, marks: [] }, expected: true },
    { name: "key presence retains the original heuristic", spec: { marks: null }, expected: true },
  ] satisfies Array<{ name: string; spec: VegaSpecRecord; expected: boolean }>)(
    "$name", ({ spec, expected }) => expect(isPlainVegaSpec(spec)).toBe(expected),
  );

  it.each(["mark", "encoding", "layer", "facet", "repeat", "concat", "hconcat", "vconcat"])(
    "prefers the Vega-Lite %s key over ambiguous Vega keys",
    (key) => expect(isPlainVegaSpec({ [key]: null, marks: [] })).toBe(false),
  );

  it.each(["marks", "scales", "signals", "projections"])(
    "recognizes the plain Vega %s key",
    (key) => expect(isPlainVegaSpec({ [key]: [] })).toBe(true),
  );
});
