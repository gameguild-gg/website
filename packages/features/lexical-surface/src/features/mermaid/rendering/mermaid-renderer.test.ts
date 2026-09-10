import { beforeAll, describe, expect, it, vi } from "vitest";
import { getMermaidRenderConfig, renderMermaidSvg } from "./mermaid-renderer";

class TestCSSStyleSheet {
  cssRules: Array<{ cssText: string }> = [];

  insertRule(cssText: string, index: number): number {
    this.cssRules.splice(index, 0, { cssText });
    return index;
  }

  replaceSync(cssText: string): void {
    this.cssRules = [{ cssText }];
  }
}

beforeAll(() => {
  vi.stubGlobal("CSSStyleSheet", TestCSSStyleSheet);
  Object.defineProperties(SVGElement.prototype, {
    getBBox: {
      configurable: true,
      value: () => ({ x: 0, y: 0, width: 80, height: 30 }),
    },
    getComputedTextLength: {
      configurable: true,
      value: () => 40,
    },
  });
});

describe("Mermaid render configuration", () => {
  it.each(["default", "default-dark"])(
    "uses native SVG labels for the %s theme",
    (theme) => {
      expect(getMermaidRenderConfig(theme)).toMatchObject({
        securityLevel: "strict",
        htmlLabels: false,
        flowchart: { htmlLabels: false },
      });
    },
  );

  it(
    "preserves flowchart node labels in the sanitized SVG",
    async () => {
      const svg = await renderMermaidSvg(
        "graph TD\n A[start] --> B[end]",
        "default",
      );

      expect(svg).toMatch(/>start<\/tspan>/);
      expect(svg).toMatch(/>end<\/tspan>/);
      expect(svg).not.toContain("foreignObject");
    },
  );
});
