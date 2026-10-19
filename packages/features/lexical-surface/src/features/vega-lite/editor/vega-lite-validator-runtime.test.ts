import { describe, expect, it } from "vitest";
import { VegaLiteValidator } from "./vega-lite-validator";
import type { TopLevelSpec } from "vega-lite";

const data = { values: [{ category: "A", value: 3 }, { category: "B", value: 5 }] };
const encoding = {
  x: { field: "category", type: "nominal" as const },
  y: { field: "value", type: "quantitative" as const },
};

describe("VegaLiteValidator with the real compiler", () => {
  it.each([
    { name: "bar", spec: { data, mark: "bar", encoding } },
    { name: "layer", spec: { data, layer: [{ mark: "bar", encoding }, { mark: "point", encoding }] } },
    { name: "facet", spec: { data, facet: { field: "category", type: "nominal" }, spec: { mark: "bar", encoding } } },
    { name: "repeat", spec: { data, repeat: ["value"], spec: {
      mark: "point", encoding: { x: { field: { repeat: "repeat" }, type: "quantitative" } },
    } } },
  ] satisfies Array<{ name: string; spec: TopLevelSpec }>)(
    "accepts a valid $name specification using the real compiler",
    async ({ spec }) => {
      const text = JSON.stringify({ $schema: "https://vega.github.io/schema/vega-lite/v6.json", ...spec });
      await expect(VegaLiteValidator.validateSpec(text)).resolves.toEqual({ isValid: true, warnings: undefined });
    },
  );
});
