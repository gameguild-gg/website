"use client";

import { useEffect, useRef, useState } from "react";
import { isVegaSpecRecord, type VegaSpecRecord } from "../vega-spec";
import {
  applyThemeOverrides,
  DARK_THEME_OVERRIDES,
  LIGHT_THEME_OVERRIDES,
} from "../theme/vega-theme-overrides";

const THEME_MAP: Record<string, string> = {
  default: "default",
  dark: "dark",
  excel: "excel",
  ggplot2: "ggplot2",
  quartz: "quartz",
  vox: "vox",
  fivethirtyeight: "fivethirtyeight",
  latimes: "latimes",
  urbaninstitute: "urbaninstitute",
  googlecharts: "googlecharts",
  powerbi: "powerbi",
  "excel-dark": "excel",
  "ggplot2-dark": "ggplot2",
  "quartz-dark": "quartz",
  "vox-dark": "vox",
  "fivethirtyeight-dark": "fivethirtyeight",
  "latimes-dark": "latimes",
  "urbaninstitute-dark": "urbaninstitute",
  "googlecharts-dark": "googlecharts",
  "powerbi-dark": "powerbi",
};

const renderGenerations = new WeakMap<HTMLElement, number>();

const VEGA_LITE_VIEW_KEYS = [
  "mark",
  "encoding",
  "layer",
  "facet",
  "repeat",
  "concat",
  "hconcat",
  "vconcat",
] as const;

const VEGA_VIEW_KEYS = ["marks", "scales", "signals", "projections"] as const;

// Heuristic mirroring content-rendering: $schema URL disambiguates when
// present; Vega-Lite view keys win otherwise, Vega-Lite stays the default.
export function isPlainVegaSpec(spec: VegaSpecRecord): boolean {
  const schema = typeof spec.$schema === "string" ? spec.$schema : "";
  if (schema.includes("/schema/vega-lite/")) return false;
  if (schema.includes("/schema/vega/")) return true;
  if (VEGA_LITE_VIEW_KEYS.some((key) => key in spec)) return false;
  return VEGA_VIEW_KEYS.some((key) => key in spec);
}

function nextRenderGeneration(container: HTMLElement): number {
  const generation = (renderGenerations.get(container) ?? 0) + 1;
  renderGenerations.set(container, generation);
  return generation;
}

function createDarkTheme(baseTheme: VegaSpecRecord): VegaSpecRecord {
  const section = (value: unknown): VegaSpecRecord =>
    isVegaSpecRecord(value) ? value : {};
  return {
    ...baseTheme,
    background: "#1a1a1a",
    view: { ...section(baseTheme.view), fill: "#1a1a1a", stroke: "#404040" },
    axis: {
      ...section(baseTheme.axis),
      domainColor: "#666666",
      gridColor: "#333333",
      tickColor: "#666666",
      labelColor: "#cccccc",
      titleColor: "#ffffff",
    },
    legend: {
      ...section(baseTheme.legend),
      labelColor: "#cccccc",
      titleColor: "#ffffff",
    },
    title: { ...section(baseTheme.title), color: "#ffffff" },
    text: { ...section(baseTheme.text), fill: "#cccccc" },
  };
}

interface VegaChartData {
  parsedSpec: VegaSpecRecord | null;
  isLoading: boolean;
  error: string;
  vegaRef: React.RefObject<HTMLDivElement | null>;
  fullscreenVegaRef: React.RefObject<HTMLDivElement | null>;
}

export function useVegaLiteChart({
  spec,
  layout = "rectangular",
}: {
  spec: string;
  layout?: "square" | "rectangular";
  theme?: string;
}): VegaChartData {
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [parsedSpec, setParsedSpec] = useState<VegaSpecRecord | null>(
    null,
  );
  const vegaRef = useRef<HTMLDivElement>(null);
  const fullscreenVegaRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!spec) {
      setParsedSpec(null);
      setError("");
      return;
    }

    setIsLoading(true);
    try {
      const parsed: unknown = JSON.parse(spec);
      if (!isVegaSpecRecord(parsed)) {
        throw new Error("Specification must be a JSON object");
      }

      if (isPlainVegaSpec(parsed)) {
        // Plain Vega specs own their sizing; do not inject width/height or
        // apply Vega-Lite shape validation.
        setParsedSpec(parsed);
        setError("");
      } else {
        if (!parsed.data && !parsed.datasets) {
          throw new Error("Vega-Lite spec missing data field");
        }
        if (!VEGA_LITE_VIEW_KEYS.some((key) => key in parsed)) {
          throw new Error("Vega-Lite spec missing a mark or composite view");
        }

        setParsedSpec({
          ...parsed,
          width: layout === "square" ? 400 : 800,
          height: layout === "square" ? 400 : 300,
        });
        setError("");
      }
    } catch (parseError) {
      setParsedSpec(null);
      setError(
        parseError instanceof Error
          ? parseError.message
          : "Invalid Vega-Lite specification",
      );
    } finally {
      setIsLoading(false);
    }
  }, [layout, spec]);

  return { parsedSpec, isLoading, error, vegaRef, fullscreenVegaRef };
}

export async function renderVegaChart(
  container: HTMLElement,
  parsedSpec: VegaSpecRecord,
  layout: "square" | "rectangular" = "rectangular",
  theme = "default",
): Promise<() => void> {
  const generation = nextRenderGeneration(container);
  const isCurrent = () => renderGenerations.get(container) === generation;
  const plainVega = isPlainVegaSpec(parsedSpec);
  const [vega, vegaThemes] = await Promise.all([
    import("vega"),
    import("vega-themes"),
  ]);

  if (!isCurrent()) return () => undefined;

  let specWithTheme = { ...parsedSpec };
  if (theme !== "default" && THEME_MAP[theme]) {
    const isDarkTheme = theme.endsWith("-dark");
    const baseThemeName = isDarkTheme ? theme.replace("-dark", "") : theme;
    const themeConfig = (vegaThemes as unknown as Record<string, unknown>)[
      THEME_MAP[theme]
    ];
    if (isVegaSpecRecord(themeConfig)) {
      let finalThemeConfig =
        isDarkTheme && baseThemeName !== "dark"
          ? createDarkTheme(themeConfig)
          : themeConfig;
      const overrides = isDarkTheme
        ? DARK_THEME_OVERRIDES[theme]
        : LIGHT_THEME_OVERRIDES[theme];
      if (overrides) {
        finalThemeConfig = applyThemeOverrides(finalThemeConfig, overrides);
      }
      specWithTheme = {
        ...specWithTheme,
        config: {
          ...(isVegaSpecRecord(specWithTheme.config) ? specWithTheme.config : {}),
          ...finalThemeConfig,
        },
      };
    }
  }

  const vegaSpec = plainVega
    ? specWithTheme
    : await import("vega-lite").then((vegaLite) =>
        vegaLite.compile(
          specWithTheme as Parameters<typeof vegaLite.compile>[0],
        ).spec,
      );
  if (!isCurrent()) return () => undefined;

  container.replaceChildren();
  const view = new vega.View(vega.parse(vegaSpec), { renderer: "svg" });
  view.initialize(container);

  try {
    await view.runAsync();
    if (!isCurrent()) {
      view.finalize();
      return () => undefined;
    }

    const rendered = container.firstElementChild as HTMLElement | null;
    if (rendered) {
      rendered.style.display = "block";
      rendered.style.maxWidth = "100%";
      rendered.style.height = "auto";
      if (layout === "square") rendered.style.margin = "0 auto";
    }
  } catch (error) {
    view.finalize();
    if (isCurrent()) container.replaceChildren();
    throw error;
  }

  return () => {
    view.finalize();
    if (isCurrent()) {
      renderGenerations.delete(container);
      container.replaceChildren();
    }
  };
}
