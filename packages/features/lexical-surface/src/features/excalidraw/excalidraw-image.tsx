/**
 * ExcalidrawImage — static SVG render of an Excalidraw scene, via
 * `exportToSvg`. Ported from
 * `lexical-playground/src/nodes/ExcalidrawNode/ExcalidrawImage.tsx`.
 */
"use client";

import * as React from "react";
import { useEffect, useRef, useState } from "react";
import type { AppState, BinaryFiles } from "@excalidraw/excalidraw/types";
import type {
  ExcalidrawElement,
  NonDeleted,
} from "@excalidraw/excalidraw/element/types";

type Dimension = "inherit" | number;

type Props = {
  appState: AppState;
  elements: NonDeleted<ExcalidrawElement>[];
  files: BinaryFiles;
  width?: Dimension;
  height?: Dimension;
  imageContainerRef: React.RefObject<HTMLDivElement | null>;
  rootClassName?: string | null;
};

const removeStyleFromSvg = (svg: SVGElement) => {
  const styleTag = svg?.firstElementChild?.firstElementChild;
  const viewBox = svg.getAttribute("viewBox");
  if (viewBox != null) {
    const parts = viewBox.split(" ");
    if (parts.length >= 4) {
      svg.setAttribute("width", parts[2]!);
      svg.setAttribute("height", parts[3]!);
    }
  }
  if (styleTag && styleTag.tagName === "style") {
    styleTag.remove();
  }
};

export default function ExcalidrawImage({
  elements,
  files,
  imageContainerRef,
  appState,
  rootClassName = null,
  width = "inherit",
  height = "inherit",
}: Props): React.JSX.Element {
  const [svg, setSvg] = useState<SVGElement | null>(null);
  const containerRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    let cancelled = false;
    const run = async () => {
      const { exportToSvg } = await import("@excalidraw/excalidraw");
      const result: SVGElement = await exportToSvg({
        appState,
        elements,
        files,
      });
      if (cancelled) return;
      removeStyleFromSvg(result);
      result.setAttribute("width", "100%");
      result.setAttribute("height", "100%");
      result.setAttribute("display", "block");
      setSvg(result);
    };
    void run();
    return () => {
      cancelled = true;
    };
  }, [elements, files, appState]);

  useEffect(() => {
    const container = containerRef.current;
    if (container === null) return;
    if (svg === null) container.replaceChildren();
    else container.replaceChildren(svg);
  }, [svg]);

  const containerStyle: React.CSSProperties = {};
  if (width !== "inherit") containerStyle.width = `${width}px`;
  if (height !== "inherit") containerStyle.height = `${height}px`;

  return (
    <div
      ref={(node) => {
        containerRef.current = node;
        if (node && imageContainerRef) imageContainerRef.current = node;
      }}
      className={rootClassName ?? ""}
      style={containerStyle}
    />
  );
}
