import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const parse = vi.fn<(code: string) => Promise<unknown>>();

async function loadValidator() {
  const { MermaidValidator } = await import("./mermaid-validator");
  return MermaidValidator;
}

beforeEach(() => {
  vi.resetModules();
  parse.mockReset();
  vi.doMock("mermaid", () => ({ default: { mermaidAPI: { parse } } }));
  vi.spyOn(console, "error").mockImplementation(() => {});
});

afterEach(() => {
  vi.doUnmock("mermaid");
  vi.restoreAllMocks();
});

describe("Mermaid validator parser boundary", () => {
  it.each(["", " \n\t"])("rejects blank input without calling the parser (%j)", async (code) => {
    const validator = await loadValidator();

    expect(await validator.validateCode(code)).toEqual({
      isValid: false,
      error: "Code cannot be empty",
    });
    expect(parse).not.toHaveBeenCalled();
  });

  it("accepts a valid diagram using the installed Mermaid parser", async () => {
    const native = await vi.importActual<typeof import("mermaid")>("mermaid");
    parse.mockImplementationOnce(native.default.mermaidAPI.parse.bind(native.default.mermaidAPI));
    const validator = await loadValidator();
    const code = "flowchart TD\n A[Start] --> B[End]";

    expect(await validator.validateCode(code)).toEqual({ isValid: true });
    expect(parse).toHaveBeenCalledWith(code);
    await expect(parse.mock.results[0]?.value).resolves.toMatchObject({
      diagramType: expect.any(String),
    });
  });

  it("reports invalid syntax from the installed Mermaid parser", async () => {
    const native = await vi.importActual<typeof import("mermaid")>("mermaid");
    parse.mockImplementationOnce(native.default.mermaidAPI.parse.bind(native.default.mermaidAPI));
    const validator = await loadValidator();
    const code = "flowchart TD\n A -->";

    const result = await validator.validateCode(code);
    expect(result.isValid).toBe(false);
    expect(result.error).toEqual(expect.any(String));
    expect(result.error).not.toBe("");
    expect(parse).toHaveBeenCalledWith(code);
    await expect(parse.mock.results[0]?.value).rejects.toBeDefined();
  });

  it("preserves formatted Error messages and their line numbers", async () => {
    parse.mockRejectedValueOnce(new Error("Parse error on line 4: Expecting a node"));
    const validator = await loadValidator();

    expect(await validator.validateCode("flowchart TD")).toEqual({
      isValid: false,
      error: "Syntax error: Expecting a node",
      line: 4,
    });
  });

  it("handles plain error records without requiring an Error instance", async () => {
    parse.mockRejectedValueOnce({ message: "Lexical error on line 3: invalid token" });
    const validator = await loadValidator();

    expect(await validator.validateCode("flowchart TD")).toEqual({
      isValid: false,
      error: "Invalid character or syntax error",
      line: 3,
    });
  });

  it("preserves string parser failures and applies existing message formatting", async () => {
    parse.mockRejectedValueOnce("Unknown diagram type: unsupported");
    const validator = await loadValidator();

    expect(await validator.validateCode("unsupported diagram")).toEqual({
      isValid: false,
      error: "Unknown diagram type. Use: flowchart, sequenceDiagram, classDiagram, etc.",
      line: undefined,
    });
  });

  it.each([
    { label: "null", value: null },
    { label: "undefined", value: undefined },
    { label: "number", value: 0 },
    { label: "boolean", value: false },
    { label: "record without a message", value: {} },
    { label: "non-string message", value: { message: 17 } },
    { label: "empty message", value: { message: "" } },
    { label: "symbol", value: Symbol("parser failure") },
  ])("normalizes a $label parser failure without throwing", async ({ value }) => {
    parse.mockRejectedValueOnce(value);
    const validator = await loadValidator();

    await expect(validator.validateCode("flowchart TD")).resolves.toEqual({
      isValid: false,
      error: "Unknown parsing error",
      line: undefined,
    });
  });

  it("reports an unavailable API without invoking the parser", async () => {
    vi.doMock("mermaid", () => ({ default: { mermaidAPI: null } }));
    const validator = await loadValidator();

    expect(await validator.validateCode("flowchart TD")).toEqual({
      isValid: false,
      error: "Mermaid API not available",
    });
    expect(parse).not.toHaveBeenCalled();
  });

  it("retains the unavailable-API result after a module-load failure", async () => {
    vi.doMock("mermaid", () => { throw new Error("Mermaid asset unavailable"); });
    const validator = await loadValidator();

    expect(await validator.validateCode("flowchart TD")).toEqual({
      isValid: false,
      error: "Mermaid API not available",
    });
    expect(console.error).toHaveBeenCalled();
    expect(parse).not.toHaveBeenCalled();
  });

  it.each([
    { label: "Error", value: new Error("initialization failed"), message: "initialization failed" },
    { label: "string", value: "initialization rejected", message: "initialization rejected" },
    { label: "null", value: null, message: "Unknown error" },
  ])("normalizes an unexpected $label initialization rejection", async ({ value, message }) => {
    const validator = await loadValidator();
    vi.spyOn(validator, "initialize").mockRejectedValueOnce(value);

    await expect(validator.validateCode("flowchart TD")).resolves.toEqual({
      isValid: false,
      error: `Validation error: ${message}`,
    });
    expect(parse).not.toHaveBeenCalled();
  });
});
