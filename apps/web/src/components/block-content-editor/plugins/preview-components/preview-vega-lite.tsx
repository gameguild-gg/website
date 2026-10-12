"use client";

import {
  getVegaLiteThemePair as getThemePair,
  VegaLiteViewer,
} from "@game-guild/lexical-surface";

interface PreviewVegaLiteProps {
  node: {
    data: {
      spec: string;
      title?: string;
      caption?: string;
      theme?: Parameters<typeof getThemePair>[0];
      themeMode?: Parameters<typeof getThemePair>[1];
      layout?: "square" | "rectangular";
      size?: number;
    };
  };
}

export function PreviewVegaLite({ node }: PreviewVegaLiteProps) {
  const { spec, title, caption, theme, themeMode, layout, size } = node.data;
  const themePair = getThemePair(
    theme || "default",
    themeMode || "system",
  );

  return (
    <VegaLiteViewer
      spec={spec}
      layout={layout}
      themeLight={themePair.themeLight}
      themeDark={themePair.themeDark}
      title={title}
      caption={caption}
      size={size}
      showControls={true}
      allowFullscreen={true}
    />
  );
}
