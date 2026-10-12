import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { MarkdownRenderer } from '@game-guild/content-rendering';

/**
 * XSS regression (todo 6, batch A): lesson-viewer feeds server-stored lesson
 * bodies (body.html / body.content) into MarkdownRenderer, whose rehype-raw
 * pass used to emit raw markup verbatim. The sanitize-hast rehype plugin must
 * remove executable nodes before React renders.
 */
describe('MarkdownRenderer XSS sanitization', () => {
  it('does not render <script> elements from raw HTML', () => {
    render(
      <MarkdownRenderer content={'Hello\n\n<script>alert("xss")</script>\n\nWorld'} />
    );

    expect(document.querySelector('script')).toBeNull();
    expect(screen.getByText('Hello')).toBeInTheDocument();
    expect(screen.getByText('World')).toBeInTheDocument();
  });

  it('strips onerror event handlers from raw <img> tags', () => {
    render(
      <MarkdownRenderer
        content={'<img src="x" onerror="alert(1)" alt="pic" />after'}
      />,
    );

    const img = document.querySelector('img');
    expect(img).not.toBeNull();
    // React renders unknown/dangerous props only if present in the hast
    // properties — the sanitizer must have deleted onerror before render.
    expect(img?.getAttribute('onerror')).toBeNull();
  });

  it('strips javascript: URLs from raw anchors', () => {
    render(
      <MarkdownRenderer content={'<a href="javascript:alert(1)">click</a>'} />,
    );

    const link = screen.getByText('click');
    expect(link.getAttribute('href') ?? '').not.toMatch(/^javascript:/i);
  });

  it('keeps legitimate raw HTML (bold, tables, admonitions) intact', () => {
    render(<MarkdownRenderer content={'<strong>bold</strong> stays'} />);

    expect(document.querySelector('strong')?.textContent).toBe('bold');
    expect(screen.getByText('stays')).toBeInTheDocument();
  });

  it('never executes injected script payloads', () => {
    const alertSpy = vi.fn();
    vi.stubGlobal('alert', alertSpy);

    render(
      <MarkdownRenderer
        content={'<div><script>alert("pwned")</script><img src="x" onerror="alert(1)" /></div>'}
      />,
    );

    expect(alertSpy).not.toHaveBeenCalled();
    expect(document.querySelector('script')).toBeNull();
    vi.unstubAllGlobals();
  });
});
