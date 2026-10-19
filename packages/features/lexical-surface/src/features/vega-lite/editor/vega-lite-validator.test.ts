import { beforeEach, describe, expect, it, vi } from "vitest";
import { VegaLiteValidator } from "./vega-lite-validator";

const { compile } = vi.hoisted(() => ({ compile: vi.fn() }));
vi.mock("vega-lite", () => ({ compile }));

const validSpec = {
  $schema: "https://vega.github.io/schema/vega-lite/v6.json",
  data: { values: [{ category: "A", value: 3 }] },
  mark: "bar",
  encoding: {
    x: { field: "category", type: "nominal" },
    y: { field: "value", type: "quantitative" },
  },
};

beforeEach(() => {
  compile.mockReset();
  compile.mockReturnValue({ spec: {} });
});

describe("VegaLiteValidator.validateSpec", () => {
  it("passes the original valid specification to the compiler", async () => {
    await expect(VegaLiteValidator.validateSpec(JSON.stringify(validSpec)))
      .resolves.toEqual({ isValid: true, warnings: undefined });
    expect(compile).toHaveBeenCalledExactlyOnceWith(validSpec);
  });

  it.each(["", " \n\t"])("rejects empty input %j without compiling", async (spec) => {
    await expect(VegaLiteValidator.validateSpec(spec)).resolves.toEqual({
      isValid: false, error: "Specification cannot be empty",
    });
    expect(compile).not.toHaveBeenCalled();
  });

  it("reports malformed JSON without compiling", async () => {
    await expect(VegaLiteValidator.validateSpec("{")).resolves.toMatchObject({
      isValid: false, error: expect.stringContaining("Invalid JSON:"),
    });
    expect(compile).not.toHaveBeenCalled();
  });

  it.each(["null", "false", "7", '"text"', "[]"])(
    "rejects a non-object specification %s without compiling",
    async (spec) => {
      await expect(VegaLiteValidator.validateSpec(spec)).resolves.toEqual({
        isValid: false, error: "Specification must be a JSON object",
      });
      expect(compile).not.toHaveBeenCalled();
    },
  );

  it("requires a data source and a mark or composite view", async () => {
    await expect(VegaLiteValidator.validateSpec('{"mark":"bar"}')).resolves.toMatchObject({
      isValid: false, error: "Specification must include a 'data' field or 'datasets'",
    });
    await expect(VegaLiteValidator.validateSpec('{"data":{"values":[]}}'))
      .resolves.toMatchObject({ isValid: false, error: expect.stringContaining("'mark' field") });
    expect(compile).not.toHaveBeenCalled();
  });

  it("retains the schema and encoding warnings", async () => {
    await expect(VegaLiteValidator.validateSpec('{"data":{"values":[]},"mark":"bar"}'))
      .resolves.toEqual({
        isValid: true,
        warnings: ["Missing $schema field (recommended)", "Mark-based charts typically need an 'encoding' field"],
      });
  });

  it.each([
    { thrown: new Error("Invalid encoding"), message: "Invalid encoding" },
    { thrown: { message: "Unsupported mark" }, message: "Unsupported mark" },
    { thrown: null, message: "Unknown compilation error" },
    { thrown: undefined, message: "Unknown compilation error" },
    { thrown: "failure", message: "Unknown compilation error" },
    { thrown: { message: "" }, message: "Unknown compilation error" },
    { thrown: { message: 17 }, message: "Unknown compilation error" },
  ])("rejects a compiler failure with message $message", async ({ thrown, message }) => {
    compile.mockImplementationOnce(() => { throw thrown; });
    await expect(VegaLiteValidator.validateSpec(JSON.stringify(validSpec))).resolves.toEqual({
      isValid: false, error: `Compilation error: ${message}`,
    });
  });

  it.each([{ result: null }, { result: undefined }, { result: {} }])(
    "rejects a compiler result without a compiled specification: $result",
    async ({ result }) => {
      compile.mockReturnValueOnce(result);
      await expect(VegaLiteValidator.validateSpec(JSON.stringify(validSpec))).resolves.toEqual({
        isValid: false, error: "Failed to compile Vega-Lite specification",
      });
    },
  );
});

describe("VegaLiteValidator.getFieldSuggestions", () => {
  it("retains inline column order and appends distinct encoding fields", () => {
    const spec = {
      data: { values: [{ first: 1, second: "A" }, { third: 2 }] },
      encoding: { x: { field: "first" }, y: { field: "derived" }, color: { field: "derived" } },
    };
    expect(VegaLiteValidator.getFieldSuggestions(JSON.stringify(spec)))
      .toEqual(["first", "second", "derived"]);
  });

  it("ignores malformed encoding fields and retains valid string fields", () => {
    const spec = { encoding: {
      x: { field: 17 }, y: { field: null }, color: { field: "" },
      size: { field: {} }, detail: null, text: { field: "valid" },
      tooltip: [{ field: "existing-array-behavior" }],
    } };
    expect(VegaLiteValidator.getFieldSuggestions(JSON.stringify(spec))).toEqual(["valid"]);
  });

  it.each(["{", "null", "[]", "7", '"text"', '{"data":{"values":[null]}}',
    '{"data":{"values":[[1,2]]}}', '{"encoding":[]}'])(
    "returns no suggestions for unsupported shapes: %s",
    (spec) => expect(VegaLiteValidator.getFieldSuggestions(spec)).toEqual([]),
  );

  it("retains all four common patterns and their field names", () => {
    const patterns = VegaLiteValidator.getCommonPatterns();
    expect(Object.keys(patterns)).toEqual(["Basic Bar Chart", "Line Chart", "Scatter Plot", "Histogram"]);
    expect(patterns["Basic Bar Chart"].encoding.x).toEqual({ field: "category", type: "nominal" });
    expect(patterns.Histogram.encoding.y).toEqual({ aggregate: "count", type: "quantitative" });
  });
});
