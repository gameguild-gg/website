/**
 * Mermaid Theme Helper
 * Similar to vega-theme-helper, provides theme management for Mermaid diagrams
 */

export type MermaidTheme =
  | "default"
  | "dark"
  | "forest"
  | "neutral"
  | "base"
  | "default-dark"
  | "forest-dark"
  | "neutral-dark"
  | "base-dark";

export type MermaidThemeMode = "system" | "light" | "dark" | "both";

export const AVAILABLE_MERMAID_THEMES: MermaidTheme[] = [
  "default",
  "dark",
  "forest",
  "neutral",
  "base",
  "default-dark",
  "forest-dark",
  "neutral-dark",
  "base-dark",
];

export const MERMAID_THEME_DESCRIPTIONS: Record<MermaidTheme, string> = {
  default: "Default",
  dark: "Dark",
  forest: "Forest",
  neutral: "Neutral",
  base: "Base",
  "default-dark": "Default Dark",
  "forest-dark": "Forest Dark",
  "neutral-dark": "Neutral Dark",
  "base-dark": "Base Dark",
};

export const MERMAID_THEME_MODE_DESCRIPTIONS: Record<
  MermaidThemeMode,
  {
    label: string;
    description: string;
  }
> = {
  system: {
    label: "System",
    description: "Follows system theme",
  },
  light: {
    label: "Light Only",
    description: "Always use light theme",
  },
  dark: {
    label: "Dark Only",
    description: "Always use dark theme",
  },
  both: {
    label: "Both",
    description: "Use theme for both modes",
  },
};

/**
 * Get the dark variant of a theme
 */
function getDarkVariant(theme: MermaidTheme): MermaidTheme {
  // If already a dark theme, return as is
  if (theme.endsWith("-dark") || theme === "dark") {
    return theme;
  }

  // Map each theme to its dark variant
  switch (theme) {
    case "default":
      return "default-dark";
    case "forest":
      return "forest-dark";
    case "neutral":
      return "neutral-dark";
    case "base":
      return "base-dark";
    default:
      return "dark";
  }
}

/**
 * Get the appropriate theme pair based on the selected theme and mode
 */
export function getMermaidThemePair(
  theme: MermaidTheme,
  mode: MermaidThemeMode,
): {
  themeLight: MermaidTheme;
  themeDark: MermaidTheme;
} {
  switch (mode) {
    case "light": {
      // Always use the selected theme (treating it as light)
      // If a dark variant is selected, use the light version
      const lightTheme = theme.replace("-dark", "") as MermaidTheme;
      return {
        themeLight: lightTheme,
        themeDark: lightTheme,
      };
    }
    case "dark": {
      // Always use dark variant
      const darkTheme = getDarkVariant(theme);
      return {
        themeLight: darkTheme,
        themeDark: darkTheme,
      };
    }
    case "both":
      // Use the same theme for both modes
      return {
        themeLight: theme,
        themeDark: theme,
      };

    case "system":
    default: {
      // Smart pairing: use dark variant for dark mode, light theme for light mode
      const baseTheme = theme.replace("-dark", "") as MermaidTheme;
      return {
        themeLight: baseTheme,
        themeDark: getDarkVariant(baseTheme),
      };
    }
  }
}

/**
 * Get the current theme based on system theme and user preferences
 */
export function getCurrentMermaidTheme(
  themeLight: MermaidTheme,
  themeDark: MermaidTheme,
  isDarkMode: boolean,
): MermaidTheme {
  return isDarkMode ? themeDark : themeLight;
}
