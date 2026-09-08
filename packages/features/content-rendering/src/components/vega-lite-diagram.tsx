'use client';

import { useEffect, useRef, useState } from 'react';
import { useDarkMode } from './mermaid-diagram';

/**
 * Dark-mode config overrides mirroring the Vega-Lite dark adjustments used by
 * `@game-guild/lexical-surface`. Background stays transparent so the chart
 * blends with the page card.
 */
const DARK_CONFIG = {
  background: 'transparent',
  view: { stroke: '#404040' },
  axis: {
    domainColor: '#666666',
    gridColor: '#333333',
    tickColor: '#666666',
    labelColor: '#cccccc',
    titleColor: '#ffffff',
  },
  legend: {
    labelColor: '#cccccc',
    titleColor: '#ffffff',
  },
  title: { color: '#ffffff' },
  header: { labelColor: '#cccccc', titleColor: '#ffffff' },
};

const LIGHT_CONFIG = {
  background: 'transparent',
};

function parseSpec(spec: string): Record<string, unknown> {
  const parsed: unknown = JSON.parse(spec);
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) {
    throw new Error('Specification must be a JSON object');
  }
  const record = parsed as Record<string, unknown>;
  if (!record.data && !record.datasets) {
    throw new Error('Vega-Lite spec missing data field');
  }
  if (
    !record.mark &&
    !record.layer &&
    !record.concat &&
    !record.hconcat &&
    !record.vconcat &&
    !record.facet &&
    !record.repeat
  ) {
    throw new Error('Vega-Lite spec missing a mark or composite view');
  }
  return record;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value);
}

async function renderVegaSvg(
  container: HTMLElement,
  spec: string,
  isDarkMode: boolean,
): Promise<() => void> {
  const parsedSpec = parseSpec(spec);
  const [vegaLite, vega] = await Promise.all([import('vega-lite'), import('vega')]);

  const compiled = vegaLite.compile({
    ...parsedSpec,
    config: {
      ...(isRecord(parsedSpec.config) ? parsedSpec.config : {}),
      ...(isDarkMode ? DARK_CONFIG : LIGHT_CONFIG),
    },
  } as Parameters<typeof vegaLite.compile>[0]);

  container.replaceChildren();
  const view = new vega.View(vega.parse(compiled.spec), { renderer: 'svg' });
  view.initialize(container);

  try {
    await view.runAsync();
  } catch (error) {
    view.finalize();
    container.replaceChildren();
    throw error;
  }

  const rendered = container.firstElementChild as HTMLElement | null;
  if (rendered) {
    rendered.style.display = 'block';
    rendered.style.margin = '0 auto';
    rendered.style.maxWidth = '100%';
    rendered.style.height = 'auto';
  }

  return () => {
    view.finalize();
    container.replaceChildren();
  };
}

export interface VegaLiteDiagramProps {
  /** Vega-Lite specification as a JSON string (the fenced block content). */
  spec: string;
  className?: string;
}

export function VegaLiteDiagram({ spec, className = '' }: VegaLiteDiagramProps) {
  const isDarkMode = useDarkMode();
  const containerRef = useRef<HTMLDivElement>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    const container = containerRef.current;
    if (!container || !spec.trim()) {
      setIsLoading(false);
      return;
    }

    let active = true;
    let dispose: (() => void) | undefined;
    let timeoutId: ReturnType<typeof setTimeout> | undefined;

    setIsLoading(true);
    const timeoutPromise = new Promise<never>((_, reject) => {
      timeoutId = setTimeout(() => reject(new Error('Rendering timeout')), 10000);
    });

    void Promise.race([renderVegaSvg(container, spec, isDarkMode), timeoutPromise])
      .then((cleanup) => {
        if (!active) {
          cleanup();
          return;
        }
        dispose = cleanup;
        setError('');
        setIsLoading(false);
      })
      .catch((renderFailure: unknown) => {
        if (!active) return;
        setError(
          renderFailure instanceof Error
            ? renderFailure.message
            : 'Failed to render chart',
        );
        setIsLoading(false);
      });

    return () => {
      active = false;
      if (timeoutId) clearTimeout(timeoutId);
      dispose?.();
    };
  }, [spec, isDarkMode]);

  if (error) {
    return (
      <div
        className={`my-4 rounded-lg border border-red-400/40 bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-300 ${className}`}
      >
        <p className="mb-2 font-semibold">Chart failed to render</p>
        <pre className="overflow-x-auto whitespace-pre-wrap font-mono text-xs text-red-700/80 dark:text-red-200/80">
          {spec}
        </pre>
      </div>
    );
  }

  return (
    <div
      className={`my-4 overflow-x-auto rounded-lg border border-slate-200 bg-white p-4 dark:border-slate-700/60 dark:bg-slate-900 ${className}`}
    >
      {isLoading ? (
        <p className="py-8 text-center text-sm text-slate-500 dark:text-slate-400">
          Rendering chart…
        </p>
      ) : null}
      <div ref={containerRef} />
    </div>
  );
}

export default VegaLiteDiagram;
