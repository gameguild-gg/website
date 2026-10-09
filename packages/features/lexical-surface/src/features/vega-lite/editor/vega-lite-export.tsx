"use client";

import { Button } from "@game-guild/ui/components/button";
import { useAssetRepository } from "@game-guild/assets/react";
import { Download } from "lucide-react";
import { useDarkMode } from "../../../shared/ui/use-dark-mode";
import {
  applyThemeOverrides,
  DARK_THEME_OVERRIDES,
  LIGHT_THEME_OVERRIDES,
} from "../theme/vega-theme-overrides";
import { loadCsvDataIntoSpec } from "../data/vega-csv-loader";
import { resolveVegaAttachments } from "../data/vega-asset-loader";
import type { VegaDataAttachment } from "../vega-lite-data";
import type { Config } from "vega-lite";

// Function to create dark version of any theme
function createDarkTheme(baseTheme: Config): Config {
  const section = (value: unknown): Record<string, unknown> =>
    typeof value === "object" && value !== null
      ? (value as Record<string, unknown>)
      : {};
  const theme = baseTheme as unknown as Record<string, unknown>;
  return {
    ...theme,
    background: "#1a1a1a",
    view: {
      ...section(theme.view),
      fill: "#1a1a1a",
      stroke: "#404040",
    },
    axis: {
      ...section(theme.axis),
      domainColor: "#666666",
      gridColor: "#333333",
      tickColor: "#666666",
      labelColor: "#cccccc",
      titleColor: "#ffffff",
    },
    legend: {
      ...section(theme.legend),
      labelColor: "#cccccc",
      titleColor: "#ffffff",
    },
    title: {
      ...section(theme.title),
      color: "#ffffff",
    },
    text: {
      ...section(theme.text),
      fill: "#cccccc",
    },
  } as unknown as Config;
}

interface VegaLiteExportProps {
  spec: string;
  themeLight?: string;
  themeDark?: string;
  layout?: "square" | "rectangular";
  title?: string;
  isValid: boolean;
  disabled?: boolean;
  className?: string;
  attachments?: Record<string, VegaDataAttachment>;
}

export function VegaLiteExport({
  spec,
  themeLight = "default",
  themeDark = "dark",
  layout = "rectangular",
  title,
  isValid,
  disabled = false,
  className = "",
  attachments,
}: VegaLiteExportProps) {
  const isDark = useDarkMode();
  const repository = useAssetRepository();
  const theme = isDark ? themeDark : themeLight;
  const isDisabled = disabled || !spec.trim() || !isValid;

  const parseSpec = async () => {
    const dataFiles = await resolveVegaAttachments(repository, attachments);
    return Object.keys(dataFiles).length > 0
      ? loadCsvDataIntoSpec(spec, dataFiles)
      : JSON.parse(spec);
  };

  const handleDownloadSVG = async () => {
    if (isDisabled) return;

    try {
      // Parse the specification and load data files
      let parsedSpec;
      try {
        parsedSpec = await parseSpec();
      } catch (parseError) {
        console.error("Invalid JSON specification for download");
        return;
      }

      // Apply theme if specified
      if (theme && theme !== "default") {
        try {
          const vegaThemesImport = await import("vega-themes");
          const themeMap: Record<string, string> = {
            dark: "dark",
            // Light themes
            excel: "excel",
            ggplot2: "ggplot2",
            quartz: "quartz",
            vox: "vox",
            fivethirtyeight: "fivethirtyeight",
            latimes: "latimes",
            urbaninstitute: "urbaninstitute",
            googlecharts: "googlecharts",
            powerbi: "powerbi",
            // Dark versions
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

          if (themeMap[theme]) {
            // Check if it's a dark version of a theme
            const isDarkTheme = theme.endsWith("-dark");
            const baseThemeName = isDarkTheme
              ? theme.replace("-dark", "")
              : theme;
            const themeConfig = (
              vegaThemesImport as unknown as Record<string, Config>
            )[themeMap[theme]];

            if (themeConfig) {
              let finalThemeConfig = themeConfig;

              // If it's a dark theme variant, apply dark modifications
              if (isDarkTheme && baseThemeName !== "dark") {
                finalThemeConfig = createDarkTheme(themeConfig);
              }

              // Apply manual overrides from vega-theme-overrides.ts
              const overrides = isDarkTheme
                ? DARK_THEME_OVERRIDES[theme]
                : LIGHT_THEME_OVERRIDES[theme];
              if (overrides) {
                finalThemeConfig = applyThemeOverrides(
                  finalThemeConfig,
                  overrides,
                );
              }

              parsedSpec = {
                ...parsedSpec,
                config: {
                  ...parsedSpec.config,
                  ...finalThemeConfig,
                },
              };
            }
          }
        } catch (themeError) {
          console.warn("Could not apply theme:", theme, themeError);
        }
      }

      // Apply layout settings
      if (layout === "square") {
        parsedSpec.width = 400;
        parsedSpec.height = 400;
      } else if (layout === "rectangular") {
        parsedSpec.width = "container";
        parsedSpec.height = 300;
      }

      // Dynamic import of Vega-Lite and Vega
      const vegaLiteImport = await import("vega-lite").catch(() => null);
      const vegaImport = await import("vega").catch(() => null);

      if (!vegaLiteImport || !vegaImport) {
        console.error("Vega-Lite not available for download");
        return;
      }

      // Compile Vega-Lite spec to Vega spec
      const vegaSpec = vegaLiteImport.compile(parsedSpec).spec;

      // Create a new view for SVG generation
      const view = new vegaImport.View(vegaImport.parse(vegaSpec))
        .renderer("svg")
        .initialize();

      await view.runAsync();

      // Get SVG string
      const svgString = await view.toSVG();

      // Create blob and download
      const blob = new Blob([svgString], { type: "image/svg+xml" });
      const url = URL.createObjectURL(blob);

      // Create download link
      const link = document.createElement("a");
      link.href = url;
      link.download = `${title || "vega-lite-chart"}.svg`;
      document.body.appendChild(link);
      link.click();
      document.body.removeChild(link);

      // Clean up
      URL.revokeObjectURL(url);

      console.log("SVG downloaded successfully");
    } catch (err: any) {
      console.error("Error downloading SVG:", err);
    }
  };

  const handleDownloadPNG = async () => {
    if (isDisabled) return;

    try {
      // Parse the specification and load data files
      let parsedSpec;
      try {
        parsedSpec = await parseSpec();
      } catch (parseError) {
        console.error("Invalid JSON specification for download");
        return;
      }

      // Apply theme if specified
      if (theme && theme !== "default") {
        try {
          const vegaThemesImport = await import("vega-themes");
          const themeMap: Record<string, string> = {
            dark: "dark",
            // Light themes
            excel: "excel",
            ggplot2: "ggplot2",
            quartz: "quartz",
            vox: "vox",
            fivethirtyeight: "fivethirtyeight",
            latimes: "latimes",
            urbaninstitute: "urbaninstitute",
            googlecharts: "googlecharts",
            powerbi: "powerbi",
            // Dark versions
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

          if (themeMap[theme]) {
            // Check if it's a dark version of a theme
            const isDarkTheme = theme.endsWith("-dark");
            const baseThemeName = isDarkTheme
              ? theme.replace("-dark", "")
              : theme;
            const themeConfig = (
              vegaThemesImport as unknown as Record<string, Config>
            )[themeMap[theme]];

            if (themeConfig) {
              let finalThemeConfig = themeConfig;

              // If it's a dark theme variant, apply dark modifications
              if (isDarkTheme && baseThemeName !== "dark") {
                finalThemeConfig = createDarkTheme(themeConfig);
              }

              // Apply manual overrides from vega-theme-overrides.ts
              const overrides = isDarkTheme
                ? DARK_THEME_OVERRIDES[theme]
                : LIGHT_THEME_OVERRIDES[theme];
              if (overrides) {
                finalThemeConfig = applyThemeOverrides(
                  finalThemeConfig,
                  overrides,
                );
              }

              parsedSpec = {
                ...parsedSpec,
                config: {
                  ...parsedSpec.config,
                  ...finalThemeConfig,
                },
              };
            }
          }
        } catch (themeError) {
          console.warn("Could not apply theme:", theme, themeError);
        }
      }

      // Apply layout settings with higher resolution for PNG
      if (layout === "square") {
        parsedSpec.width = 800; // Double resolution for PNG
        parsedSpec.height = 800;
      } else if (layout === "rectangular") {
        parsedSpec.width = 1200;
        parsedSpec.height = 600;
      }

      // Dynamic import of Vega-Lite and Vega
      const vegaLiteImport = await import("vega-lite").catch(() => null);
      const vegaImport = await import("vega").catch(() => null);

      if (!vegaLiteImport || !vegaImport) {
        console.error("Vega-Lite not available for download");
        return;
      }

      // Compile Vega-Lite spec to Vega spec
      const vegaSpec = vegaLiteImport.compile(parsedSpec).spec;

      // Create a new view for PNG generation
      const view = new vegaImport.View(vegaImport.parse(vegaSpec))
        .renderer("canvas")
        .initialize();

      await view.runAsync();

      // Get PNG as canvas and convert to blob
      const canvas = await view.toCanvas();
      canvas.toBlob((blob: Blob | null) => {
        if (blob) {
          const url = URL.createObjectURL(blob);

          // Create download link
          const link = document.createElement("a");
          link.href = url;
          link.download = `${title || "vega-lite-chart"}.png`;
          document.body.appendChild(link);
          link.click();
          document.body.removeChild(link);

          // Clean up
          URL.revokeObjectURL(url);

          console.log("PNG downloaded successfully");
        }
      }, "image/png");
    } catch (err: any) {
      console.error("Error downloading PNG:", err);
    }
  };

  return (
    <div className={`flex items-center gap-2 ${className}`}>
      <Button
        variant="outline"
        size="sm"
        onClick={handleDownloadSVG}
        disabled={isDisabled}
        className="flex items-center gap-2 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
        title="Download as SVG"
      >
        <Download className="h-4 w-4" />
        SVG
      </Button>
      <Button
        variant="outline"
        size="sm"
        onClick={handleDownloadPNG}
        disabled={isDisabled}
        className="flex items-center gap-2 border-gray-300 dark:border-gray-600 hover:bg-gray-50 dark:hover:bg-gray-800"
        title="Download as PNG"
      >
        <Download className="h-4 w-4" />
        PNG
      </Button>
    </div>
  );
}
