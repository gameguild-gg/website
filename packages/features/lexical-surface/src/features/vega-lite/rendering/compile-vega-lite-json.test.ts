import { compile } from "vega-lite";
import { describe, expect, it, vi } from "vitest";
import { compileVegaLiteJson } from "./compile-vega-lite-json";

const data = { values: [{ value: 1, group: "a" }] };
const unit = {
  mark: "point",
  encoding: { x: { field: "value", type: "quantitative" } },
};

describe("dynamic Vega-Lite compiler boundary", () => {
  it.each([
    { name: "unit", view: unit },
    { name: "layer", view: { layer: [unit, { ...unit, mark: "line" }] } },
    { name: "concat", view: { concat: [unit, unit] } },
    { name: "hconcat", view: { hconcat: [unit, unit] } },
    { name: "vconcat", view: { vconcat: [unit, unit] } },
    { name: "facet", view: { facet: { field: "group", type: "nominal" }, spec: unit } },
    {
      name: "repeat",
      view: {
        repeat: ["value"],
        spec: {
          mark: "point",
          encoding: { x: { field: { repeat: "repeat" }, type: "quantitative" } },
        },
      },
    },
    {
      name: "parameterized mark",
      view: {
        ...unit,
        params: [{ name: "cutoff", value: 0 }],
        transform: [{ filter: "datum.value >= cutoff" }],
      },
    },
  ])("preserves native compilation of $name compositions", ({ view }) => {
    const compiled = compileVegaLiteJson(compile, { data, ...view });
    expect(compiled.spec.$schema).toMatch(/\/schema\/vega\/v\d+\.json$/);
    expect(compiled.spec.marks?.length).toBeGreaterThan(0);
  });

  it.each([
    { name: "null", spec: null },
    { name: "undefined", spec: undefined },
    { name: "array", spec: [] },
    { name: "number", spec: 7 },
    { name: "string", spec: "{}" },
    { name: "boolean", spec: false },
    { name: "symbol", spec: Symbol("spec") },
  ])("rejects $name before calling the SDK", ({ spec }) => {
    const compiler = vi.fn(compile);
    expect(() => compileVegaLiteJson(compiler, spec))
      .toThrowError("Specification must be a JSON object");
    expect(compiler).not.toHaveBeenCalled();
  });

  it("keeps unsupported object shapes subject to the real SDK", () => {
    expect(() => compileVegaLiteJson(compile, {})).toThrow();
  });

  it("passes the original object to the SDK without projecting fields", () => {
    const spec = { data, ...unit, usermeta: { preserved: true } };
    const compiler = vi.fn(compile);
    compileVegaLiteJson(compiler, spec);
    expect(compiler).toHaveBeenCalledTimes(1);
    expect(compiler.mock.calls[0]?.[0]).toBe(spec);
  });

  it("propagates the original compiler failure", () => {
    const failure = new Error("compiler rejected the specification");
    const compiler = vi.fn(() => { throw failure; });
    expect(() => compileVegaLiteJson(compiler, { data, ...unit })).toThrow(failure);
  });
});
