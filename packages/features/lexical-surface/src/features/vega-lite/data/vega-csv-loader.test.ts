import { afterEach, describe, expect, it, vi } from "vitest";
import {
  extractDataFilenames,
  hasDataReferences,
  loadCsvDataIntoSpec,
} from "./vega-csv-loader";
import { isVegaSpecRecord } from "../vega-spec";

afterEach(() => vi.restoreAllMocks());

describe("loadCsvDataIntoSpec", () => {
  it("inlines CSV string cells and retains the column order and data options", () => {
    const result = loadCsvDataIntoSpec(
      JSON.stringify({ data: { url: "data:values.csv", format: { type: "csv" } } }),
      { "values.csv": 'label,value\n"A, B",01\nC,2' },
    );
    expect(isVegaSpecRecord(result)).toBe(true);
    if (!isVegaSpecRecord(result) || !isVegaSpecRecord(result.data)) {
      throw new Error("Expected an inline data object");
    }
    expect(result.data).not.toHaveProperty("url");
    expect(result.data.format).toEqual({ type: "csv" });
    const rows = result.data.values;
    expect(Array.isArray(rows)).toBe(true);
    if (!Array.isArray(rows)) throw new Error("Expected CSV rows");
    expect(rows[0]).toEqual({ label: "A, B", value: "01" });
    expect(rows[1]).toEqual({ label: "C", value: "2" });
    expect(rows).toHaveProperty("columns", ["label", "value"]);
  });

  it("preserves JSON arrays without wrapping them", () => {
    expect(loadCsvDataIntoSpec('{"data":{"url":"data:values.json","name":"rows"}}', {
      "values.json": '[{"x":1},{"x":2}]',
    })).toEqual({ data: { name: "rows", values: [{ x: 1 }, { x: 2 }] } });
  });

  it.each([
    ["object", '{"x":1}', [{ x: 1 }]],
    ["null", "null", [null]],
    ["number", "7", [7]],
    ["string", '"value"', ["value"]],
  ] satisfies Array<[string, string, unknown[]]>)("wraps a JSON %s dataset once", (_name, content, expected) => {
    expect(loadCsvDataIntoSpec('{"data":{"url":"data:values.json"}}', {
      "values.json": content,
    })).toEqual({ data: { values: expected } });
  });

  it("traverses nested layers and retains unrelated arrays and primitives", () => {
    const spec = {
      layer: [{ data: { url: "data:a.json" } }, { data: { url: "data:b.json" } }],
      metadata: [null, false, 2, "text"],
    };
    expect(loadCsvDataIntoSpec(JSON.stringify(spec), {
      "a.json": '[{"x":1}]', "b.json": '[{"x":2}]',
    })).toEqual({
      layer: [{ data: { values: [{ x: 1 }] } }, { data: { values: [{ x: 2 }] } }],
      metadata: spec.metadata,
    });
  });

  it.each([{ url: null }, { url: 17 }, { url: false }, { url: {} }, { url: [] }])(
    "retains a non-string data URL $url and still processes later siblings",
    ({ url }) => {
      const spec = { layer: [{ data: { url } }, { data: { url: "data:valid.json" } }] };
      expect(loadCsvDataIntoSpec(JSON.stringify(spec), { "valid.json": '[{"x":3}]' }))
        .toEqual({ layer: [{ data: { url } }, { data: { values: [{ x: 3 }] } }] });
    },
  );

  it("retains remote URLs and existing inline values", () => {
    const spec = { layer: [
      { data: { url: "https://example.test/values.csv" } },
      { data: { values: [{ x: 1 }] } },
    ] };
    expect(loadCsvDataIntoSpec(JSON.stringify(spec))).toEqual(spec);
  });

  it("retains a missing dataset URL and reports its filename", () => {
    const warning = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const spec = { data: { url: "data:missing.csv" } };
    expect(loadCsvDataIntoSpec(JSON.stringify(spec))).toEqual(spec);
    expect(warning).toHaveBeenCalledWith("Arquivo não encontrado: missing.csv");
  });

  it("retains an unsupported dataset URL", () => {
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const spec = { data: { url: "data:values.tsv" } };
    expect(loadCsvDataIntoSpec(JSON.stringify(spec), { "values.tsv": "x\n1" })).toEqual(spec);
  });

  it("retains a malformed JSON dataset while processing its valid sibling", () => {
    const error = vi.spyOn(console, "error").mockImplementation(() => undefined);
    const spec = { layer: [
      { data: { url: "data:bad.json" } }, { data: { url: "data:good.json" } },
    ] };
    expect(loadCsvDataIntoSpec(JSON.stringify(spec), {
      "bad.json": "{", "good.json": '[{"x":4}]',
    })).toEqual({ layer: [
      { data: { url: "data:bad.json" } }, { data: { values: [{ x: 4 }] } },
    ] });
    expect(error).toHaveBeenCalledWith("Erro ao parsear JSON bad.json:", expect.any(Error));
  });

  it("returns the original text when the specification is malformed JSON", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    expect(loadCsvDataIntoSpec("{not-json")).toBe("{not-json");
  });

  it.each(["null", "false", "7", '"text"', "[]"])(
    "preserves a non-object JSON root %s for the caller to validate",
    (text) => expect(loadCsvDataIntoSpec(text)).toEqual(JSON.parse(text)),
  );

  it("does not mutate the caller's dataset map", () => {
    const files = Object.freeze({ "values.json": '[{"x":1}]' });
    loadCsvDataIntoSpec('{"data":{"url":"data:values.json"}}', files);
    expect(files).toEqual({ "values.json": '[{"x":1}]' });
  });
});

describe("data reference discovery", () => {
  it("finds and deduplicates CSV and JSON virtual filenames", () => {
    const spec = '{"layer":[{"data":{"url":"data:a.csv"}},' +
      '{"data":{"url":"data:b.json"}},{"data":{"url":"data:a.csv"}}]}';
    expect(hasDataReferences(spec)).toBe(true);
    expect(extractDataFilenames(spec)).toEqual({ csv: ["a.csv"], json: ["b.json"] });
    expect(extractDataFilenames('{"data":{"url":"https://example.test/a.csv"}}'))
      .toEqual({ csv: [], json: [] });
    expect(hasDataReferences('{"mark":"bar"}')).toBe(false);
  });
});
