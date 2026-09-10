import { render, screen, waitFor } from '@testing-library/react';
import ReactMarkdown from 'react-markdown';
import { describe, expect, it } from 'vitest';

import { MarkdownRenderer } from '@game-guild/content-rendering';

import { useMarkdownComponents } from './markdown-components';

const INVALID_SPEC = '{ not valid json';

const VALID_SPEC = JSON.stringify({
  data: { values: [{ x: 1, y: 2 }, { x: 2, y: 4 }, { x: 3, y: 6 }] },
  mark: 'bar',
  encoding: {
    x: { field: 'x', type: 'quantitative' },
    y: { field: 'y', type: 'quantitative' },
  },
});

function fenced(lang: string, body: string): string {
  return `\`\`\`${lang}\n${body}\n\`\`\``;
}

function MarkdownPreview({ content }: { content: string }) {
  const components = useMarkdownComponents();
  return <ReactMarkdown components={components}>{content}</ReactMarkdown>;
}

describe('vegalite fenced code blocks in markdown', () => {
  it('dispatches ```vegalite fences to the chart renderer (content-rendering)', async () => {
    render(<MarkdownRenderer content={fenced('vegalite', INVALID_SPEC)} />);

    // Invalid JSON must surface the chart error UI — not a syntax-highlighted block.
    await waitFor(() => {
      expect(screen.getByText('Chart failed to render')).toBeInTheDocument();
    });
  });

  it('dispatches ```vega-lite fences to the chart renderer (content-rendering)', async () => {
    render(<MarkdownRenderer content={fenced('vega-lite', INVALID_SPEC)} />);

    await waitFor(() => {
      expect(screen.getByText('Chart failed to render')).toBeInTheDocument();
    });
  });

  it('dispatches ```vegalite fences to VegaLiteViewer in the editor preview components', async () => {
    render(<MarkdownPreview content={fenced('vegalite', INVALID_SPEC)} />);

    await waitFor(() => {
      expect(screen.getByText('Error rendering chart:')).toBeInTheDocument();
    });
  });

  it('renders a valid ```vegalite spec as an SVG chart (content-rendering)', async () => {
    render(<MarkdownRenderer content={fenced('vegalite', VALID_SPEC)} />);

    await waitFor(
      () => {
        expect(document.querySelector('svg')).toBeInTheDocument();
      },
      { timeout: 15000 },
    );
  });

  it('dispatches ```mermaid fences to the diagram renderer in the editor preview components', async () => {
    render(
      <MarkdownPreview content={fenced('mermaid', 'graph TD\n  A-->B')} />,
    );

    await waitFor(
      () => {
        expect(document.querySelector('svg')).toBeInTheDocument();
      },
      { timeout: 30_000 },
    );
  }, 30_000);
});
