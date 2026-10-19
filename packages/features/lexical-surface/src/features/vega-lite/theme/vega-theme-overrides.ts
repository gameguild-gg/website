/**
 * Overrides manuais para temas Vega-Lite
 *
 * Este arquivo permite ajustar manualmente as cores e estilos dos temas,
 * especialmente as variantes dark que podem não ter cores adequadas.
 *
 * Para cada tema, você pode definir overrides para:
 * - Versão light (opcional)
 * - Versão dark (recomendado para melhorar contraste)
 */

import { isVegaSpecRecord, type VegaSpecRecord } from "../vega-spec";

export interface ThemeOverride {
  background?: string;
  view?: {
    fill?: string;
    stroke?: string;
  };
  axis?: {
    domainColor?: string;
    gridColor?: string;
    tickColor?: string;
    labelColor?: string;
    titleColor?: string;
  };
  legend?: {
    labelColor?: string;
    titleColor?: string;
  };
  title?: {
    color?: string;
    fontSize?: number;
    fontWeight?: string | number;
  };
  text?: {
    fill?: string;
  };
  mark?: {
    color?: string;
    strokeWidth?: number;
    opacity?: number;
    fill?: string;
  };
  line?: {
    strokeWidth?: number;
    stroke?: string;
  };
  bar?: {
    cornerRadiusTopLeft?: number;
    cornerRadiusTopRight?: number;
    [key: string]: unknown;
  };
  point?: {
    filled?: boolean;
    size?: number;
    [key: string]: unknown;
  };
  range?: {
    category?: string[];
    diverging?: string[];
    heatmap?: string[];
    ordinal?: string[];
    ramp?: string[];
  };
  style?: {
    [key: string]: unknown;
  };
  group?: {
    fill?: string;
    [key: string]: unknown;
  };
  arc?: {
    fill?: string;
    [key: string]: unknown;
  };
  area?: {
    fill?: string;
    [key: string]: unknown;
  };
  path?: {
    stroke?: string;
    [key: string]: unknown;
  };
  rect?: {
    fill?: string;
    [key: string]: unknown;
  };
  shape?: {
    stroke?: string;
    [key: string]: unknown;
  };
  symbol?: {
    fill?: string;
    size?: number;
    [key: string]: unknown;
  };
}

/**
 * Overrides para as versões light dos temas
 * Geralmente não é necessário, mas pode ser ajustado se precisar
 */
export const LIGHT_THEME_OVERRIDES: Record<string, ThemeOverride> = {
  // Exemplo: ajustar o tema default light
  // "default": {
  //   background: "#ffffff",
  //   title: {
  //     color: "#000000",
  //     fontSize: 16
  //   }
  // },

  ggplot2: {
    axis: {
      gridColor: "#f3f4f6",
    },
  },
};

/**
 * Overrides para as versões dark dos temas
 * Aqui você pode ajustar cores para melhor contraste e legibilidade
 */
export const DARK_THEME_OVERRIDES: Record<string, ThemeOverride> = {
  // Tema dark padrão - melhorado para melhor contraste
  dark: {
    background: "#0a0a0a",
    view: {
      fill: "#0a0a0a",
      stroke: "#404040",
    },
    axis: {
      domainColor: "#222222",
      gridColor: "#222222",
      tickColor: "#222222",
      labelColor: "#d0d0d0",
      titleColor: "#222222",
    },
    legend: {
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    title: {
      color: "#ffffff",
      fontSize: 16,
    },
    text: {
      fill: "#d0d0d0",
    },
  },

  // Excel dark - melhorar cores para melhor legibilidade
  "excel-dark": {
    background: "#1a1a1a",
    view: {
      fill: "#1a1a1a",
      stroke: "#3a3a3a",
    },
    axis: {
      domainColor: "#5a5a5a",
      gridColor: "#2a2a2a",
      tickColor: "#5a5a5a",
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    legend: {
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    title: {
      color: "#f0f0f0",
    },
    text: {
      fill: "#cccccc",
    },
    range: {
      category: [
        "#5b9bd5",
        "#ed7d31",
        "#a5a5a5",
        "#ffc000",
        "#4472c4",
        "#70ad47",
      ],
    },
  },

  // ggplot2 dark - ajustar para fundo escuro
  "ggplot2-dark": {
    range: {
      category: [
        "#ebe6e6",
        "#7F7F7F",
        "#1A1A1A",
        "#999999",
        "#333333",
        "#B0B0B0",
        "#4D4D4D",
        "#C9C9C9",
        "#666666",
        "#DCDCDC",
      ],
      heatmap: [
        "#ebe6e6",
        "#7F7F7F",
        "#1A1A1A",
        "#999999",
        "#333333",
        "#B0B0B0",
      ],
    },
    group: { fill: "#fff" },
    arc: { fill: "#fff" },
    area: { fill: "#fff" },
    line: { stroke: "#fff" },
    path: { stroke: "#fff" },
    rect: { fill: "#fff" },
    shape: { stroke: "#fff" },
    symbol: { fill: "#fff", size: 40 },
  },

  // Quartz dark - ajustar cores
  "quartz-dark": {
    background: "#0f0f0f",
    view: {
      fill: "#0f0f0f",
      stroke: "#404040",
    },
    axis: {
      domainColor: "#606060",
      gridColor: "#252525",
      tickColor: "#606060",
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    legend: {
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    title: {
      color: "#f0f0f0",
    },
    text: {
      fill: "#cccccc",
    },
  },

  // Vox dark - melhorar contraste
  "vox-dark": {
    background: "#141414",
    view: {
      fill: "#141414",
      stroke: "#3a3a3a",
    },
    axis: {
      domainColor: "#555555",
      gridColor: "#2a2a2a",
      tickColor: "#555555",
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    legend: {
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    title: {
      color: "#ffffff",
    },
    text: {
      fill: "#d0d0d0",
    },
  },

  // FiveThirtyEight dark - ajustar para fundo escuro
  "fivethirtyeight-dark": {
    background: "#1a1a1a",
    view: {
      fill: "#1a1a1a",
      stroke: "#3a3a3a",
    },
    axis: {
      domainColor: "#555555",
      gridColor: "#2a2a2a",
      tickColor: "#555555",
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    legend: {
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    title: {
      color: "#f0f0f0",
      fontSize: 18,
      fontWeight: "bold",
    },
    text: {
      fill: "#cccccc",
    },
  },

  // LA Times dark - melhorar legibilidade
  "latimes-dark": {
    background: "#0f0f0f",
    view: {
      fill: "#0f0f0f",
      stroke: "#404040",
    },
    axis: {
      domainColor: "#606060",
      gridColor: "#252525",
      tickColor: "#606060",
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    legend: {
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    title: {
      color: "#ffffff",
      fontSize: 20,
    },
    text: {
      fill: "#d0d0d0",
    },
  },

  // Urban Institute dark - ajustar cores
  "urbaninstitute-dark": {
    background: "#121212",
    view: {
      fill: "#1a1a1a",
      stroke: "#3a3a3a",
    },
    axis: {
      domainColor: "#555555",
      gridColor: "#2a2a2a",
      tickColor: "#555555",
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    legend: {
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    title: {
      color: "#f0f0f0",
    },
    text: {
      fill: "#cccccc",
    },
    range: {
      category: [
        "#1696d2",
        "#fdbf11",
        "#d2d2d2",
        "#ec008b",
        "#55b748",
        "#5c5859",
      ],
    },
  },

  // Google Charts dark - melhorar para fundo escuro
  "googlecharts-dark": {
    background: "#1a1a1a",
    view: {
      fill: "#1a1a1a",
      stroke: "#3a3a3a",
    },
    axis: {
      domainColor: "#555555",
      gridColor: "#2a2a2a",
      tickColor: "#555555",
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    legend: {
      labelColor: "#cccccc",
      titleColor: "#e0e0e0",
    },
    title: {
      color: "#f0f0f0",
    },
    text: {
      fill: "#cccccc",
    },
  },

  // Power BI dark - ajustar para melhor contraste
  "powerbi-dark": {
    background: "#0f0f0f",
    view: {
      fill: "#0f0f0f",
      stroke: "#404040",
    },
    axis: {
      domainColor: "#606060",
      gridColor: "#252525",
      tickColor: "#606060",
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    legend: {
      labelColor: "#d0d0d0",
      titleColor: "#ffffff",
    },
    title: {
      color: "#ffffff",
    },
    text: {
      fill: "#d0d0d0",
    },
    range: {
      category: [
        "#118dff",
        "#12239e",
        "#e66c37",
        "#6b007b",
        "#e044a7",
        "#744ec2",
      ],
    },
  },
};

/**
 * Aplica overrides a um tema base
 * Faz deep merge das propriedades
 */
export function applyThemeOverrides(
  baseTheme: VegaSpecRecord,
  overrides?: ThemeOverride,
): VegaSpecRecord {
  if (!overrides) return baseTheme;
  const section = (value: unknown): VegaSpecRecord =>
    isVegaSpecRecord(value) ? value : {};

  return {
    ...baseTheme,
    ...(overrides.background !== undefined && {
      background: overrides.background,
    }),
    view: {
      ...section(baseTheme.view),
      ...overrides.view,
    },
    axis: {
      ...section(baseTheme.axis),
      ...overrides.axis,
    },
    legend: {
      ...section(baseTheme.legend),
      ...overrides.legend,
    },
    title: {
      ...section(baseTheme.title),
      ...overrides.title,
    },
    text: {
      ...section(baseTheme.text),
      ...overrides.text,
    },
    ...(overrides.mark && { mark: { ...section(baseTheme.mark), ...overrides.mark } }),
    ...(overrides.line && { line: { ...section(baseTheme.line), ...overrides.line } }),
    ...(overrides.bar && { bar: { ...section(baseTheme.bar), ...overrides.bar } }),
    ...(overrides.point && {
      point: { ...section(baseTheme.point), ...overrides.point },
    }),
    ...(overrides.area && { area: { ...section(baseTheme.area), ...overrides.area } }),
    ...(overrides.arc && { arc: { ...section(baseTheme.arc), ...overrides.arc } }),
    ...(overrides.path && { path: { ...section(baseTheme.path), ...overrides.path } }),
    ...(overrides.rect && { rect: { ...section(baseTheme.rect), ...overrides.rect } }),
    ...(overrides.shape && {
      shape: { ...section(baseTheme.shape), ...overrides.shape },
    }),
    ...(overrides.symbol && {
      symbol: { ...section(baseTheme.symbol), ...overrides.symbol },
    }),
    ...(overrides.group && {
      group: { ...section(baseTheme.group), ...overrides.group },
    }),
    ...(overrides.style && {
      style: { ...section(baseTheme.style), ...overrides.style },
    }),
    ...(overrides.range && {
      range: { ...section(baseTheme.range), ...overrides.range },
    }),
  };
}
