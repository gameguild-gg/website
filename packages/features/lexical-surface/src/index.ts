export { LexicalSurface } from "./surface/lexical-surface";
export type {
  LexicalSurfaceFeatures,
  LexicalSurfaceProps,
} from "./surface/lexical-surface";
export { LEXICAL_SURFACE_THEME } from "./schema/theme";
export type { PageSettings } from "./features/page";
export { stripSelection } from "./schema/initial-editor-state";
export type { BaseMediaData, MediaType } from "./features/media/media-data";
export type { MermaidData } from "./features/mermaid/mermaid-data";
export type {
  VegaDataAttachment,
  VegaLiteData,
} from "./features/vega-lite/vega-lite-data";
export { MermaidEditor } from "./features/mermaid/editor/mermaid-editor";
export { MermaidViewer } from "./features/mermaid/rendering/mermaid-viewer";
export { VegaLiteEditor } from "./features/vega-lite/editor/vega-lite-editor";
export { VegaLiteViewer } from "./features/vega-lite/rendering/vega-lite-viewer";
export { getThemePair as getVegaLiteThemePair } from "./features/vega-lite/theme/vega-theme-helper";
export type {
  ThemeMode as VegaLiteThemeMode,
  VegaThemeBase,
} from "./features/vega-lite/theme/vega-theme-helper";
