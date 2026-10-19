import { describe, expect, it } from "vitest";
import * as vegaThemes from "vega-themes";
import { applyThemeOverrides, type ThemeOverride } from "./vega-theme-overrides";
import { isVegaSpecRecord } from "../data/vega-spec";

describe("applyThemeOverrides", () => {
  it("returns the original theme when there are no overrides", () => {
    const base = { background: "white", view: { stroke: "gray" } };
    expect(applyThemeOverrides(base)).toBe(base);
  });

  it("retains base properties while applying each supported section", () => {
    const base = {
      view: { stroke: "retained" }, axis: { tickSize: 5 }, legend: { columns: 2 },
      title: { anchor: "start" }, text: { font: "retained" }, mark: { size: 5 },
      line: { opacity: 0.7 }, bar: { width: 12 }, point: { opacity: 0.6 },
      area: { opacity: 0.5 }, arc: { opacity: 0.4 }, path: { opacity: 0.3 },
      rect: { opacity: 0.2 }, shape: { opacity: 0.1 }, symbol: { opacity: 0.9 },
      group: { stroke: "retained" }, style: { retained: { fill: "gray" } },
      range: { ramp: ["retained"] }, customExtension: { retained: true },
    };
    const overrides: ThemeOverride = {
      background: "black", view: { fill: "new" }, axis: { gridColor: "new" },
      legend: { labelColor: "new" }, title: { color: "new" }, text: { fill: "new" },
      mark: { color: "new" }, line: { stroke: "new" }, bar: { cornerRadiusTopLeft: 3 },
      point: { filled: true }, area: { fill: "new" }, arc: { fill: "new" },
      path: { stroke: "new" }, rect: { fill: "new" }, shape: { stroke: "new" },
      symbol: { fill: "new" }, group: { fill: "new" },
      style: { added: { fill: "new" } }, range: { category: ["new"] },
    };
    expect(applyThemeOverrides(base, overrides)).toEqual({
      background: "black", view: { stroke: "retained", fill: "new" },
      axis: { tickSize: 5, gridColor: "new" }, legend: { columns: 2, labelColor: "new" },
      title: { anchor: "start", color: "new" }, text: { font: "retained", fill: "new" },
      mark: { size: 5, color: "new" }, line: { opacity: 0.7, stroke: "new" },
      bar: { width: 12, cornerRadiusTopLeft: 3 }, point: { opacity: 0.6, filled: true },
      area: { opacity: 0.5, fill: "new" }, arc: { opacity: 0.4, fill: "new" },
      path: { opacity: 0.3, stroke: "new" }, rect: { opacity: 0.2, fill: "new" },
      shape: { opacity: 0.1, stroke: "new" }, symbol: { opacity: 0.9, fill: "new" },
      group: { stroke: "retained", fill: "new" },
      style: { retained: { fill: "gray" }, added: { fill: "new" } },
      range: { ramp: ["retained"], category: ["new"] }, customExtension: { retained: true },
    });
  });

  it.each([
    { section: null }, { section: undefined }, { section: false },
    { section: 7 }, { section: "invalid" }, { section: ["invalid"] },
  ])("does not spread a malformed theme section $section", ({ section }) => {
    expect(applyThemeOverrides({ view: section }, { view: { fill: "new" } }).view)
      .toEqual({ fill: "new" });
  });

  it("preserves an explicit empty background and replaces range arrays", () => {
    const result = applyThemeOverrides({ background: "white", range: { category: ["old"], ramp: ["keep"] } },
      { background: "", range: { category: ["new"] } });
    expect(result.background).toBe("");
    expect(result.range).toEqual({ category: ["new"], ramp: ["keep"] });
  });

  it("retains the original shallow style merge semantics", () => {
    const result = applyThemeOverrides({ style: { heading: { fill: "old", fontSize: 14 } } },
      { style: { heading: { fill: "new" } } });
    expect(result.style).toEqual({ heading: { fill: "new" } });
  });

  it("does not mutate frozen base or override objects", () => {
    const base = Object.freeze({ view: Object.freeze({ fill: "old", stroke: "retained" }) });
    const overrides = Object.freeze({ view: Object.freeze({ fill: "new" }) });
    expect(applyThemeOverrides(base, overrides).view).toEqual({ fill: "new", stroke: "retained" });
    expect(base.view.fill).toBe("old");
    expect(overrides.view).toEqual({ fill: "new" });
  });

  it.each([
    "dark", "excel", "fivethirtyeight", "ggplot2", "latimes", "quartz", "vox",
    "urbaninstitute", "googlecharts", "powerbi",
  ])("merges overrides into the real %s dependency theme without mutation", (name) => {
    const theme = (vegaThemes as unknown as Record<string, unknown>)[name];
    expect(isVegaSpecRecord(theme)).toBe(true);
    if (!isVegaSpecRecord(theme)) throw new Error("Expected an object theme");
    const before = JSON.stringify(theme);
    const result = applyThemeOverrides(theme, { axis: { gridColor: "#123456" } });
    expect(result.axis).toMatchObject({ gridColor: "#123456" });
    expect(JSON.stringify(theme)).toBe(before);
  });
});
