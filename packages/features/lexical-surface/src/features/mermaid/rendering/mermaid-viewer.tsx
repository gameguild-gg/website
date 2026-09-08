"use client";

import { useEffect, useState, useRef } from "react";
import { GitBranch, ZoomIn, ZoomOut, RotateCcw, Maximize2 } from "lucide-react";
import { Button } from "@game-guild/ui/components/button";
import type { MermaidData } from "../mermaid-data";
import {
  getMermaidThemePair,
  getCurrentMermaidTheme,
  type MermaidTheme,
} from "../theme/mermaid-theme-helper";
import { useDarkMode } from "../../../shared/ui/use-dark-mode";
import { renderMermaidSvg } from "./mermaid-renderer";

interface MermaidViewerProps {
  data: MermaidData;
  title?: string;
  caption?: string;
  size?: number;
  showControls?: boolean;
  allowFullscreen?: boolean;
  className?: string;
}

export function MermaidViewer({
  data,
  title,
  caption,
  size = 100,
  showControls = true,
  allowFullscreen = true,
  className = "",
}: MermaidViewerProps) {
  const [zoom, setZoom] = useState(100);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const [fullscreenZoom, setFullscreenZoom] = useState(100);
  const [position, setPosition] = useState({ x: 0, y: 0 });
  const [fullscreenPosition, setFullscreenPosition] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [isFullscreenDragging, setIsFullscreenDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });
  const [fullscreenDragStart, setFullscreenDragStart] = useState({
    x: 0,
    y: 0,
  });
  const [lastPosition, setLastPosition] = useState({ x: 0, y: 0 });
  const [lastFullscreenPosition, setLastFullscreenPosition] = useState({
    x: 0,
    y: 0,
  });
  const [svgContent, setSvgContent] = useState<string>("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string>("");
  const [baseScale, setBaseScale] = useState(1); // Fator de escala para 100% = fit-to-container
  const [fullscreenBaseScale, setFullscreenBaseScale] = useState(1);

  const containerRef = useRef<HTMLDivElement>(null);
  const fullscreenContainerRef = useRef<HTMLDivElement>(null);

  // Refs espelhando estado para uso dentro de listeners nativos (zoom-to-cursor).
  const zoomRef = useRef(zoom);
  const positionRef = useRef(position);
  const baseScaleRef = useRef(baseScale);
  const fullscreenZoomRef = useRef(fullscreenZoom);
  const fullscreenPositionRef = useRef(fullscreenPosition);
  const fullscreenBaseScaleRef = useRef(fullscreenBaseScale);
  useEffect(() => {
    zoomRef.current = zoom;
  }, [zoom]);
  useEffect(() => {
    positionRef.current = position;
  }, [position]);
  useEffect(() => {
    baseScaleRef.current = baseScale;
  }, [baseScale]);
  useEffect(() => {
    fullscreenZoomRef.current = fullscreenZoom;
  }, [fullscreenZoom]);
  useEffect(() => {
    fullscreenPositionRef.current = fullscreenPosition;
  }, [fullscreenPosition]);
  useEffect(() => {
    fullscreenBaseScaleRef.current = fullscreenBaseScale;
  }, [fullscreenBaseScale]);

  // Get current theme
  const isDarkMode = useDarkMode();

  // Calculate the actual theme to use
  const themePair = getMermaidThemePair(
    (data.theme as MermaidTheme) || "default",
    (data.themeMode as any) || "system",
  );
  const currentTheme = getCurrentMermaidTheme(
    themePair.themeLight,
    themePair.themeDark,
    isDarkMode,
  );

  // Calcula o fator de escala para que o SVG se ajuste ao container (100% = fit-to-container)
  const calculateBaseScale = () => {
    if (!containerRef.current) return;

    const container = containerRef.current;
    const svgElement = container.querySelector("svg");

    if (!svgElement) return;

    const containerWidth = container.offsetWidth;
    const containerHeight = container.offsetHeight;

    // Obter dimensões naturais do SVG
    const svgWidth =
      svgElement.viewBox.baseVal.width ||
      svgElement.width.baseVal.value ||
      svgElement.clientWidth;
    const svgHeight =
      svgElement.viewBox.baseVal.height ||
      svgElement.height.baseVal.value ||
      svgElement.clientHeight;

    if (
      svgWidth === 0 ||
      svgHeight === 0 ||
      containerWidth === 0 ||
      containerHeight === 0
    )
      return;

    // Calcular escala para fit (com padding)
    const padding = 40;
    const availableWidth = containerWidth - padding * 2;
    const availableHeight = containerHeight - padding * 2;

    const scaleX = availableWidth / svgWidth;
    const scaleY = availableHeight / svgHeight;

    // Usar o menor para garantir que cabe completamente
    let scale = Math.min(scaleX, scaleY);

    // Limites práticos:
    // - Mínimo: 0.4 (40%) para diagramas muito grandes
    // - Máximo: 1.0 (100%) para não aumentar além do tamanho natural por padrão
    scale = Math.max(1.0, Math.min(scale, 2.0));

    setBaseScale(scale);
  };

  // Calcula o fator de escala para fullscreen
  const calculateFullscreenBaseScale = () => {
    if (!fullscreenContainerRef.current) return;

    const container = fullscreenContainerRef.current;
    const svgElement = container.querySelector("svg");

    if (!svgElement) return;

    const containerWidth = container.offsetWidth;
    const containerHeight = container.offsetHeight;

    // Obter dimensões naturais do SVG
    const svgWidth =
      svgElement.viewBox.baseVal.width ||
      svgElement.width.baseVal.value ||
      svgElement.clientWidth;
    const svgHeight =
      svgElement.viewBox.baseVal.height ||
      svgElement.height.baseVal.value ||
      svgElement.clientHeight;

    if (
      svgWidth === 0 ||
      svgHeight === 0 ||
      containerWidth === 0 ||
      containerHeight === 0
    )
      return;

    // Calcular escala para fit no fullscreen
    const padding = 20;
    const availableWidth = containerWidth - padding * 2;
    const availableHeight = containerHeight - padding * 2;

    const scaleX = availableWidth / svgWidth;
    const scaleY = availableHeight / svgHeight;

    let scale = Math.min(scaleX, scaleY);

    // Limites para fullscreen: clamp alto o suficiente para diagramas pequenos
    // ocuparem boa parte do modal (auto-fit real). Pequenos flowcharts (ex.
    // viewBox 200x300) precisam de fator >>2x para preencher um modal full-page.
    scale = Math.max(0.5, Math.min(scale, 10));

    setFullscreenBaseScale(scale);
  };

  // Render diagram
  useEffect(() => {
    let active = true;
    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    let scaleTimerId: ReturnType<typeof setTimeout> | undefined;

    const renderDiagram = async () => {
      if (!data.code || !data.code.trim()) {
        if (active) {
          setSvgContent("");
          setError("");
          setIsLoading(false);
        }
        return;
      }

      setIsLoading(true);
      setError("");

      try {
        const timeoutPromise = new Promise<never>((_, reject) => {
          timeoutId = setTimeout(
            () => reject(new Error("Rendering timeout")),
            10000,
          );
        });
        const svg = await Promise.race([
          renderMermaidSvg(data.code, currentTheme),
          timeoutPromise,
        ]);
        if (timeoutId) clearTimeout(timeoutId);
        if (!active) return;

        setSvgContent(svg);
        setError("");

        scaleTimerId = setTimeout(calculateBaseScale, 100);
      } catch (err: unknown) {
        if (!active) return;

        let errorMessage = "Failed to render diagram";

        if (err instanceof Error) {
          if (err.message.includes("Parse error")) {
            errorMessage = "Syntax error in diagram code";
          } else if (err.message.includes("timeout")) {
            errorMessage = "Diagram rendering timed out";
          } else if (err.message.includes("Empty")) {
            errorMessage = "No diagram code provided";
          } else {
            errorMessage = `Error: ${err.message}`;
          }
        }

        setError(errorMessage);
        setSvgContent("");
      } finally {
        if (active) setIsLoading(false);
      }
    };

    void renderDiagram();
    return () => {
      active = false;
      if (timeoutId) clearTimeout(timeoutId);
      if (scaleTimerId) clearTimeout(scaleTimerId);
    };
  }, [data.code, currentTheme]);

  // Handle escape key for fullscreen
  useEffect(() => {
    const handleEscape = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsFullscreen(false);
      }
    };

    if (isFullscreen) {
      document.addEventListener("keydown", handleEscape);
      return () => document.removeEventListener("keydown", handleEscape);
    }
  }, [isFullscreen]);

  // Recalcular baseScale do fullscreen via ResizeObserver + retry agressivo.
  // O único setTimeout(100ms) anterior não era confiável: se o modal ou o
  // SVG ainda não tivessem sido layouted, a função saa cedo e o baseScale
  // ficava travado em 1 (resultando no diagrama abrindo minsculo a 100%).
  useEffect(() => {
    if (!isFullscreen) return;
    const container = fullscreenContainerRef.current;
    if (!container) return;

    // Tenta calcular j em vrios frames at obter dimenses vlidas.
    let attempts = 0;
    const maxAttempts = 30;
    let rafId = 0;
    const tryCalc = () => {
      attempts++;
      const svg = container.querySelector("svg");
      const ok =
        container.offsetWidth > 0 &&
        container.offsetHeight > 0 &&
        svg &&
        (svg.viewBox.baseVal.width > 0 || svg.clientWidth > 0);
      if (ok) {
        calculateFullscreenBaseScale();
        return;
      }
      if (attempts < maxAttempts) {
        rafId = requestAnimationFrame(tryCalc);
      }
    };
    rafId = requestAnimationFrame(tryCalc);

    const ro = new ResizeObserver(() => calculateFullscreenBaseScale());
    ro.observe(container);

    return () => {
      cancelAnimationFrame(rafId);
      ro.disconnect();
    };
  }, [isFullscreen, svgContent]);

  // Recalcular baseScale quando o container redimensiona
  useEffect(() => {
    if (!containerRef.current) return;

    const resizeObserver = new ResizeObserver(() => {
      calculateBaseScale();
    });

    resizeObserver.observe(containerRef.current);

    return () => {
      resizeObserver.disconnect();
    };
  }, [svgContent]);

  // Reset position when zoom returns to 100%
  useEffect(() => {
    if (zoom === 100 && (position.x !== 0 || position.y !== 0)) {
      const resetTimer = setTimeout(() => {
        setPosition({ x: 0, y: 0 });
      }, 50);
      return () => clearTimeout(resetTimer);
    }
  }, [zoom, position.x, position.y]);

  // Reset fullscreen position when zoom returns to 100%
  useEffect(() => {
    if (
      fullscreenZoom === 100 &&
      (fullscreenPosition.x !== 0 || fullscreenPosition.y !== 0)
    ) {
      const resetTimer = setTimeout(() => {
        setFullscreenPosition({ x: 0, y: 0 });
      }, 50);
      return () => clearTimeout(resetTimer);
    }
  }, [fullscreenZoom, fullscreenPosition.x, fullscreenPosition.y]);

  // Zoom control functions
  const handleZoomIn = () => {
    setZoom((prev) => Math.min(prev + 50, 300));
  };

  const handleZoomOut = () => {
    setZoom((prev) => Math.max(prev - 50, 100));
  };

  const handleZoomReset = () => {
    setZoom(100);
    setPosition({ x: 0, y: 0 });
  };

  const handleFullscreen = () => {
    setIsFullscreen(true);
  };

  // Fullscreen zoom control functions
  const handleFullscreenZoomIn = () => {
    setFullscreenZoom((prev) => Math.min(prev + 50, 1000));
  };

  const handleFullscreenZoomOut = () => {
    setFullscreenZoom((prev) => Math.max(prev - 50, 100));
  };

  const handleFullscreenZoomReset = () => {
    setFullscreenZoom(100);
    setFullscreenPosition({ x: 0, y: 0 });
  };

  // Pan/Drag control functions
  const handleMouseDown = (e: React.MouseEvent) => {
    if (zoom > 100) {
      setIsDragging(true);
      setDragStart({ x: e.clientX, y: e.clientY });
      setLastPosition(position);
      e.preventDefault();
      e.stopPropagation();
    }
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (isDragging && zoom > 100) {
      e.preventDefault();
      const deltaX = (e.clientX - dragStart.x) * 0.8;
      const deltaY = (e.clientY - dragStart.y) * 0.8;
      setPosition({
        x: lastPosition.x + deltaX,
        y: lastPosition.y + deltaY,
      });
    }
  };

  const handleMouseUp = () => {
    if (isDragging) {
      setIsDragging(false);
    }
  };

  const handleMouseLeave = () => {
    if (isDragging) {
      setIsDragging(false);
    }
  };

  // Wheel zoom (handler React mantido apenas como fallback visual; o listener
  // nativo abaixo é quem realmente intercepta o zoom da página).
  const handleWheel = (_e: React.WheelEvent) => {
    // no-op: lógica real fica no listener nativo non-passive.
  };

  // Listener wheel nativo non-passive — necessário para que Ctrl+scroll
  // não dispare o zoom do navegador (preventDefault não funciona em
  // listeners passive, que é o default em alguns paths do React).
  // Implementa zoom-to-cursor: ancora o ponto sob o cursor durante o zoom.
  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;
    const onWheelNative = (e: WheelEvent) => {
      if (!(e.ctrlKey || e.metaKey)) return;
      e.preventDefault();
      const oldZoom = zoomRef.current;
      // Multiplicativo: cada tick = 25% do zoom atual.
      const factor = e.deltaY > 0 ? 1 / 1.25 : 1.25;
      const newZoom = Math.round(
        Math.max(100, Math.min(300, oldZoom * factor)),
      );
      if (newZoom === oldZoom) return;
      const rect = container.getBoundingClientRect();
      const ox = e.clientX - (rect.left + rect.width / 2);
      const oy = e.clientY - (rect.top + rect.height / 2);
      const base = baseScaleRef.current || 1;
      const oldEff = (oldZoom / 100) * base;
      const newEff = (newZoom / 100) * base;
      const ratio = oldEff === 0 ? 1 : newEff / oldEff;
      const pos = positionRef.current;
      setZoom(newZoom);
      setPosition({
        x: ox - (ox - pos.x) * ratio,
        y: oy - (oy - pos.y) * ratio,
      });
    };
    container.addEventListener("wheel", onWheelNative, { passive: false });
    return () => container.removeEventListener("wheel", onWheelNative);
  }, []);

  // Fullscreen Pan/Drag control functions
  const handleFullscreenMouseDown = (e: React.MouseEvent) => {
    if (fullscreenZoom > 100) {
      setIsFullscreenDragging(true);
      setFullscreenDragStart({ x: e.clientX, y: e.clientY });
      setLastFullscreenPosition(fullscreenPosition);
      e.preventDefault();
      e.stopPropagation();
    }
  };

  const handleFullscreenMouseMove = (e: React.MouseEvent) => {
    if (isFullscreenDragging && fullscreenZoom > 100) {
      e.preventDefault();
      const deltaX = (e.clientX - fullscreenDragStart.x) * 0.8;
      const deltaY = (e.clientY - fullscreenDragStart.y) * 0.8;
      setFullscreenPosition({
        x: lastFullscreenPosition.x + deltaX,
        y: lastFullscreenPosition.y + deltaY,
      });
    }
  };

  const handleFullscreenMouseUp = () => {
    if (isFullscreenDragging) {
      setIsFullscreenDragging(false);
    }
  };

  const handleFullscreenMouseLeave = () => {
    if (isFullscreenDragging) {
      setIsFullscreenDragging(false);
    }
  };

  // Fullscreen wheel zoom (no-op no React; lógica real no listener nativo).
  const handleFullscreenWheel = (_e: React.WheelEvent) => {
    // no-op
  };

  // Listener wheel nativo non-passive para o modal fullscreen.
  // Implementa zoom-to-cursor: ancora o ponto sob o cursor durante o zoom.
  useEffect(() => {
    if (!isFullscreen) return;
    const container = fullscreenContainerRef.current;
    if (!container) return;
    const onWheelNative = (e: WheelEvent) => {
      if (!(e.ctrlKey || e.metaKey)) return;
      e.preventDefault();
      const oldZoom = fullscreenZoomRef.current;
      // Multiplicativo: cada tick = 10% do zoom atual.
      const factor = e.deltaY > 0 ? 1 / 1.1 : 1.1;
      const newZoom = Math.round(
        Math.max(100, Math.min(1000, oldZoom * factor)),
      );
      if (newZoom === oldZoom) return;
      const rect = container.getBoundingClientRect();
      const ox = e.clientX - (rect.left + rect.width / 2);
      const oy = e.clientY - (rect.top + rect.height / 2);
      const base = fullscreenBaseScaleRef.current || 1;
      const oldEff = (oldZoom / 100) * base;
      const newEff = (newZoom / 100) * base;
      const ratio = oldEff === 0 ? 1 : newEff / oldEff;
      const pos = fullscreenPositionRef.current;
      setFullscreenZoom(newZoom);
      setFullscreenPosition({
        x: ox - (ox - pos.x) * ratio,
        y: oy - (oy - pos.y) * ratio,
      });
    };
    container.addEventListener("wheel", onWheelNative, { passive: false });
    return () => container.removeEventListener("wheel", onWheelNative);
  }, [isFullscreen]);

  // Atalhos de teclado para zoom no modo fullscreen (+ / - / 0).
  useEffect(() => {
    if (!isFullscreen) return;
    const onKey = (e: KeyboardEvent) => {
      const tag = (e.target as HTMLElement | null)?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA") return;
      if (e.key === "+" || e.key === "=") {
        e.preventDefault();
        setFullscreenZoom((prev) => Math.min(1000, prev + 25));
      } else if (e.key === "-" || e.key === "_") {
        e.preventDefault();
        setFullscreenZoom((prev) => Math.max(100, prev - 25));
      } else if (e.key === "0") {
        e.preventDefault();
        setFullscreenZoom(100);
        setFullscreenPosition({ x: 0, y: 0 });
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [isFullscreen]);

  // Trava o scroll da página enquanto o modal fullscreen está aberto.
  // Sem isso, scroll do mouse (sem Ctrl) dentro do modal propaga e rola a
  // página por trás.
  useEffect(() => {
    if (!isFullscreen) return;
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = prevOverflow;
    };
  }, [isFullscreen]);

  if (!data.code) {
    return (
      <div className={`my-4 py-8 ${className}`}>
        <div className="text-center text-gray-400 dark:text-gray-500">
          <GitBranch className="h-8 w-8 mx-auto mb-2 opacity-50" />
          <p className="text-sm">No Mermaid diagram provided</p>
        </div>
      </div>
    );
  }

  return (
    <>
      <div
        className={`relative group my-4 ${className}`}
        style={{ width: `${size}%` }}
      >
        {/* Title */}
        {title && (
          <h3 className="text-lg font-semibold mb-3 text-center dark:text-white">
            {title}
          </h3>
        )}

        {/* Chart Container */}
        <div className="relative">
          {/* Floating Controls */}
          {showControls && (
            <div className="absolute top-3 right-3 z-50 flex items-center gap-1 bg-white/95 dark:bg-gray-900/95 backdrop-blur-md rounded-md p-1 shadow-lg border border-gray-200/50 dark:border-gray-700/50 opacity-0 group-hover:opacity-100 transition-all duration-300">
              <Button
                variant="ghost"
                size="sm"
                onClick={handleZoomOut}
                disabled={zoom <= 100}
                className="h-8 w-8 p-0"
                title="Zoom Out"
              >
                <ZoomOut className="h-4 w-4" />
              </Button>
              <span className="text-xs font-mono px-2 py-1 bg-gray-100 dark:bg-gray-700 rounded min-w-[3rem] text-center">
                {zoom}%
              </span>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleZoomIn}
                disabled={zoom >= 300}
                className="h-8 w-8 p-0"
                title="Zoom In"
              >
                <ZoomIn className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleZoomReset}
                className="h-8 w-8 p-0"
                title="Reset Zoom"
              >
                <RotateCcw className="h-4 w-4" />
              </Button>
              {allowFullscreen && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleFullscreen}
                  className="h-8 w-8 p-0 relative z-60"
                  title="Fullscreen"
                >
                  <Maximize2 className="h-4 w-4" />
                </Button>
              )}
            </div>
          )}

          {/* Diagram Content */}
          <div
            ref={containerRef}
            className={`w-full flex justify-center items-center relative overflow-hidden rounded-lg ${
              zoom > 100 ? "cursor-move" : "cursor-default"
            }`}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            onMouseLeave={handleMouseLeave}
            onWheel={handleWheel}
            style={{
              position: "relative",
              zIndex: 10,
              minHeight: "300px",
              height: "auto",
              width: "100%",
              backgroundColor: "transparent",
              padding: "20px",
            }}
          >
            {isLoading ? (
              <div className="text-gray-400 dark:text-gray-500 text-sm flex items-center gap-2">
                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-gray-400"></div>
                Rendering diagram...
              </div>
            ) : error ? (
              <div className="text-red-500 p-3 rounded-md bg-red-50/50 dark:bg-red-900/10 max-w-full">
                <div className="font-medium text-sm">
                  Error rendering diagram:
                </div>
                <div className="text-xs mt-1 opacity-80">{error}</div>
              </div>
            ) : svgContent ? (
              <div
                // mermaid bug (htmlLabels:false): mindmap root <text> gets no
                // text-anchor and spills right of the circle. Recenter it.
                className="[&_svg_.section-root_text]:[text-anchor:middle]"
                style={{
                  transform: `scale(${(zoom / 100) * baseScale}) translate(${position.x / ((zoom / 100) * baseScale)}px, ${position.y / ((zoom / 100) * baseScale)}px)`,
                  transformOrigin: "center center",
                  display: "flex",
                  justifyContent: "center",
                  alignItems: "center",
                  width: "100%",
                  height: "100%",
                  userSelect: zoom > 100 ? "none" : "auto",
                  pointerEvents: zoom > 100 ? "none" : "auto",
                  transition: isDragging
                    ? "none"
                    : "transform 0.15s cubic-bezier(0.4, 0, 0.2, 1)",
                }}
                dangerouslySetInnerHTML={{ __html: svgContent }}
              />
            ) : (
              <div className="text-gray-400 dark:text-gray-500 text-sm">
                No diagram to display
              </div>
            )}
          </div>
        </div>

        {/* Caption */}
        {caption && (
          <p className="text-sm text-gray-500 dark:text-gray-400 mt-3 text-center italic">
            {caption}
          </p>
        )}
      </div>

      {/* Fullscreen Modal — ocupa página inteira */}
      {isFullscreen && allowFullscreen && (
        <div className="fixed inset-0 bg-black/80 backdrop-blur-sm flex items-center justify-center z-50">
          <div className="bg-white dark:bg-gray-900 shadow-2xl w-screen h-screen flex flex-col">
            {/* Fullscreen Header */}
            <div className="flex items-center justify-between gap-4 p-4 border-b border-gray-200 dark:border-gray-700">
              <div className="flex items-center gap-2 min-w-0 flex-shrink-0">
                <GitBranch className="h-5 w-5 text-blue-600 dark:text-blue-400" />
                <h2 className="text-xl font-semibold text-gray-900 dark:text-gray-100 truncate">
                  {title || data.title || "Mermaid Diagram"}
                </h2>
              </div>

              {/* Zoom Slider Bar - Centered */}
              <div className="flex items-center gap-3 flex-1 max-w-md mx-auto">
                <ZoomOut className="h-4 w-4 text-gray-500 dark:text-gray-400 flex-shrink-0" />
                <input
                  type="range"
                  min="100"
                  max="1000"
                  step="25"
                  value={fullscreenZoom}
                  onChange={(e) => setFullscreenZoom(Number(e.target.value))}
                  className="flex-1 h-2 rounded-lg appearance-none cursor-pointer [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-4 [&::-webkit-slider-thumb]:h-4 [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-blue-600 dark:[&::-webkit-slider-thumb]:bg-blue-400 [&::-webkit-slider-thumb]:cursor-pointer [&::-webkit-slider-thumb]:shadow-md [&::-moz-range-thumb]:w-4 [&::-moz-range-thumb]:h-4 [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-blue-600 dark:[&::-moz-range-thumb]:bg-blue-400 [&::-moz-range-thumb]:cursor-pointer [&::-moz-range-thumb]:border-0 [&::-moz-range-thumb]:shadow-md"
                  style={{
                    background: isDarkMode
                      ? `linear-gradient(to right, rgb(96, 165, 250) 0%, rgb(96, 165, 250) ${((fullscreenZoom - 100) / 900) * 100}%, rgb(55, 65, 81) ${((fullscreenZoom - 100) / 900) * 100}%, rgb(55, 65, 81) 100%)`
                      : `linear-gradient(to right, rgb(37, 99, 235) 0%, rgb(37, 99, 235) ${((fullscreenZoom - 100) / 900) * 100}%, rgb(229, 231, 235) ${((fullscreenZoom - 100) / 900) * 100}%, rgb(229, 231, 235) 100%)`,
                  }}
                />
                <ZoomIn className="h-4 w-4 text-gray-500 dark:text-gray-400 flex-shrink-0" />
              </div>

              {/* Fullscreen Zoom Controls */}
              <div className="flex items-center gap-4 flex-shrink-0">
                <div className="flex items-center gap-1 bg-gray-100 dark:bg-gray-800 rounded-md p-1">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleFullscreenZoomOut}
                    disabled={fullscreenZoom <= 100}
                    className="h-8 w-8 p-0"
                    title="Zoom Out"
                  >
                    <ZoomOut className="h-4 w-4" />
                  </Button>
                  <span className="text-xs font-mono px-2 py-1 bg-gray-200 dark:bg-gray-700 rounded min-w-[3rem] text-center">
                    {fullscreenZoom}%
                  </span>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleFullscreenZoomIn}
                    disabled={fullscreenZoom >= 1000}
                    className="h-8 w-8 p-0"
                    title="Zoom In"
                  >
                    <ZoomIn className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={handleFullscreenZoomReset}
                    className="h-8 w-8 p-0"
                    title="Reset Zoom"
                  >
                    <RotateCcw className="h-4 w-4" />
                  </Button>
                </div>

                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => setIsFullscreen(false)}
                  className="hover:bg-gray-100 dark:hover:bg-gray-800"
                >
                  ✕
                </Button>
              </div>
            </div>

            {/* Fullscreen Content */}
            <div className="flex-1 p-6 overflow-auto bg-gray-50 dark:bg-gray-900">
              <div
                ref={fullscreenContainerRef}
                className={`flex justify-center items-center h-full ${
                  fullscreenZoom > 100 ? "cursor-move" : "cursor-default"
                }`}
                onMouseDown={handleFullscreenMouseDown}
                onMouseMove={handleFullscreenMouseMove}
                onMouseUp={handleFullscreenMouseUp}
                onMouseLeave={handleFullscreenMouseLeave}
                onWheel={handleFullscreenWheel}
              >
                {svgContent && (
                  <div
                    // Same mindmap root-label recenter as the inline viewer.
                    className="[&_svg_.section-root_text]:[text-anchor:middle] flex justify-center items-center max-w-full max-h-full"
                    style={{
                      transform: `scale(${(fullscreenZoom / 100) * fullscreenBaseScale}) translate(${fullscreenPosition.x / ((fullscreenZoom / 100) * fullscreenBaseScale)}px, ${fullscreenPosition.y / ((fullscreenZoom / 100) * fullscreenBaseScale)}px)`,
                      transformOrigin: "center",
                      userSelect: fullscreenZoom > 100 ? "none" : "auto",
                      pointerEvents: fullscreenZoom > 100 ? "none" : "auto",
                      transition: isFullscreenDragging
                        ? "none"
                        : "transform 0.15s cubic-bezier(0.4, 0, 0.2, 1)",
                      // Promove para camada GPU: acelera o scale de SVGs
                      // complexos (mermaid) que senão reprocessam layout/paint
                      // a cada quadro de zoom.
                      willChange: "transform",
                      backfaceVisibility: "hidden",
                    }}
                    dangerouslySetInnerHTML={{ __html: svgContent }}
                  />
                )}
              </div>
            </div>

            {(caption || data.caption) && (
              <div className="p-4 border-t border-gray-200 dark:border-gray-700 bg-gray-50 dark:bg-gray-800">
                <p className="text-center text-gray-600 dark:text-gray-300 italic">
                  {caption || data.caption}
                </p>
              </div>
            )}
          </div>
        </div>
      )}
    </>
  );
}
