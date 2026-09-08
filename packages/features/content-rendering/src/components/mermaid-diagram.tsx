'use client';

import DOMPurify from 'dompurify';
import { useEffect, useSyncExternalStore, useState } from 'react';

const DARK_MODE_QUERY = '(prefers-color-scheme: dark)';

function getDarkModeSnapshot(): boolean {
  if (typeof document === 'undefined') return false;

  const root = document.documentElement;
  const explicitTheme = root.dataset.theme;
  if (explicitTheme === 'dark' || root.classList.contains('dark')) return true;
  if (explicitTheme === 'light' || root.classList.contains('light')) return false;

  return window.matchMedia(DARK_MODE_QUERY).matches;
}

function subscribeToDarkMode(onStoreChange: () => void): () => void {
  if (typeof document === 'undefined') return () => undefined;

  const media = window.matchMedia(DARK_MODE_QUERY);
  const observer = new MutationObserver(onStoreChange);
  observer.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['class', 'data-theme'],
  });
  media.addEventListener('change', onStoreChange);

  return () => {
    observer.disconnect();
    media.removeEventListener('change', onStoreChange);
  };
}

export function useDarkMode(): boolean {
  return useSyncExternalStore(subscribeToDarkMode, getDarkModeSnapshot, () => false);
}

const DARK_THEME_VARIABLES = {
  primaryColor: '#2a5d7d',
  primaryTextColor: '#e8e8e8',
  primaryBorderColor: '#3d7a9f',
  secondaryColor: '#2a5d4a',
  secondaryTextColor: '#e8e8e8',
  secondaryBorderColor: '#3d8a6f',
  tertiaryColor: '#5d2a4a',
  tertiaryTextColor: '#e8e8e8',
  tertiaryBorderColor: '#8a3d6f',
  lineColor: '#7a7a7a',
  textColor: '#e8e8e8',
  mainBkg: '#2a4a5d',
  secondBkg: '#2a5d4a',
  noteBkg: '#3d5d4a',
  noteBorder: '#4d7f6a',
  clusterBkg: '#2a3d4a',
  clusterBorder: '#3d5d6f',
  edgeLabelBackground: '#2a3543',
  titleColor: '#e8e8e8',
};

function sanitizeSvg(svg: string): string {
  return DOMPurify.sanitize(svg, {
    USE_PROFILES: { svg: true, svgFilters: true },
    FORBID_TAGS: ['foreignObject', 'iframe', 'object', 'embed', 'script'],
  });
}

// mermaid.render is not concurrency-safe (it uses shared temp DOM ids), so
// renders are serialized through a module-level queue.
let renderQueue: Promise<void> = Promise.resolve();
let renderId = 0;

async function renderMermaidSvg(code: string, isDarkMode: boolean): Promise<string> {
  const task = renderQueue.then(async () => {
    const mermaid = (await import('mermaid')).default;
    mermaid.initialize({
      startOnLoad: false,
      // Built-in 'dark' themes every diagram type (xyChart, pie, gantt, …);
      // DARK_THEME_VARIABLES only overrides flowchart-ish brand colors.
      theme: isDarkMode ? 'dark' : 'default',
      themeVariables: isDarkMode ? DARK_THEME_VARIABLES : undefined,
      securityLevel: 'strict',
      htmlLabels: false,
      fontFamily: 'inherit',
      flowchart: { useMaxWidth: true, htmlLabels: false },
      logLevel: 'error',
      suppressErrorRendering: true,
    });

    const id = `markdown-mermaid-${++renderId}`;
    const { svg } = await mermaid.render(id, code);
    if (!svg) throw new Error('No SVG content generated');
    return sanitizeSvg(svg);
  });

  renderQueue = task.then(
    () => undefined,
    () => undefined,
  );
  return task;
}

export interface MermaidDiagramProps {
  code: string;
  className?: string;
}

export function MermaidDiagram({ code, className = '' }: MermaidDiagramProps) {
  const isDarkMode = useDarkMode();
  const [svgContent, setSvgContent] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    let active = true;

    if (!code.trim()) {
      setSvgContent('');
      setError('');
      return;
    }

    let timeoutId: ReturnType<typeof setTimeout> | undefined;
    const renderDiagram = async () => {
      try {
        const timeoutPromise = new Promise<never>((_, reject) => {
          timeoutId = setTimeout(() => reject(new Error('Rendering timeout')), 10000);
        });
        const svg = await Promise.race([renderMermaidSvg(code, isDarkMode), timeoutPromise]);
        if (!active) return;
        setSvgContent(svg);
        setError('');
      } catch (err: unknown) {
        if (!active) return;
        setError(err instanceof Error ? err.message : 'Failed to render diagram');
        setSvgContent('');
      }
    };

    void renderDiagram();
    return () => {
      active = false;
      if (timeoutId) clearTimeout(timeoutId);
    };
  }, [code, isDarkMode]);

  if (error) {
    return (
      <div className={`my-4 rounded-lg border border-red-400/40 bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-300 ${className}`}>
        <p className="mb-2 font-semibold">Diagram failed to render</p>
        <pre className="overflow-x-auto whitespace-pre-wrap font-mono text-xs text-red-700/80 dark:text-red-200/80">{code}</pre>
      </div>
    );
  }

  return (
    <div
      // mermaid bug (htmlLabels:false): mindmap root <text> gets no
      // text-anchor, so the label starts at the circle center and spills
      // right. Recenters it; scoped to mindmap's .section-root.
      className={`my-4 overflow-x-auto rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700/60 dark:bg-slate-900 [&_svg_.section-root_text]:[text-anchor:middle] ${className}`}
    >
      {svgContent ? (
        <div
          className="[&>svg]:mx-auto [&>svg]:h-auto [&>svg]:max-w-full"
          // Sanitized SVG from mermaid strict mode + DOMPurify (svg profile,
          // script/foreignObject/iframe forbidden).
          dangerouslySetInnerHTML={{ __html: svgContent }}
        />
      ) : (
        <p className="py-8 text-center text-sm text-slate-500 dark:text-slate-400">Loading diagram…</p>
      )}
    </div>
  );
}

export default MermaidDiagram;
