'use client';

import 'katex/dist/katex.min.css';

import type React from 'react';
import ReactMarkdown from 'react-markdown';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import rehypeKatex from 'rehype-katex';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import remarkMath from 'remark-math';

import { MermaidDiagram } from './mermaid-diagram';

export type MarkdownRendererMode = 'markdown' | 'reveal';
export type MarkdownRendererTone = 'default' | 'learning';

export interface MarkdownRendererProps {
  content: string;
  renderer?: MarkdownRendererMode;
  tone?: MarkdownRendererTone;
}

type MarkdownDivProps = React.HTMLAttributes<HTMLDivElement> & {
  'data-title'?: string;
  'data-type'?: string;
};

function extractRenderableContent(content: string): string {
  const trimmedContent = content.trim();

  if (!trimmedContent.startsWith('{') && !trimmedContent.startsWith('[')) {
    return content;
  }

  try {
    const parsedContent = JSON.parse(trimmedContent) as unknown;

    const unwrapValue = (value: unknown): string | null => {
      if (typeof value === 'string') {
        return value;
      }

      if (Array.isArray(value)) {
        const renderedItems = value.map(unwrapValue).filter((item): item is string => Boolean(item));
        return renderedItems.length > 0 ? renderedItems.join('\n\n') : null;
      }

      if (value && typeof value === 'object') {
        const candidateKeys = ['markdown', 'content', 'body', 'text', 'html'] as const;

        for (const key of candidateKeys) {
          const candidate = unwrapValue((value as Record<string, unknown>)[key]);
          if (candidate) {
            return candidate;
          }
        }
      }

      return null;
    };

    return unwrapValue(parsedContent) ?? content;
  } catch {
    return content;
  }
}

function preprocessMarkdown(content: string): string {
  return extractRenderableContent(content)
    .replace(
      /:::\s*(note|abstract|info|tip|success|question|warning|failure|danger|bug|example|quote)(?:\s+"([^"]*)")?\n([\s\S]*?):::/g,
      (_, type, title, body) => `<div class="admonition admonition-${type}"${title ? ` data-title="${title}"` : ''}>\n\n${body}\n\n</div>`,
    )
    .replace(/!!!\s*(quiz|code)\n([\s\S]*?)\n!!!/g, (_, type, body) => {
      const safeBody = type === 'code' ? body.replace(/</g, '&lt;').replace(/>/g, '&gt;') : body;
      return `<div class="markdown-activity" data-type="${type}">${safeBody}</div>`;
    });
}

function getAdmonitionTone(type: string | undefined, tone: MarkdownRendererTone) {
  if (tone === 'learning') {
    if (type === 'warning') return 'border-yellow-400 bg-yellow-500/10';
    if (type === 'danger') return 'border-red-400 bg-red-500/10';
    if (type === 'info') return 'border-sky-400 bg-sky-500/10';
    return 'border-slate-500 bg-slate-900';
  }

  if (type === 'warning') return 'border-yellow-400 bg-yellow-50';
  if (type === 'danger') return 'border-red-400 bg-red-50';
  if (type === 'info') return 'border-blue-400 bg-blue-50';
  return 'border-gray-400 bg-gray-50';
}

export function MarkdownRenderer({ content, renderer = 'markdown', tone = 'learning' }: MarkdownRendererProps) {
  if (renderer === 'reveal') {
    return (
      <div className="gameguild-revealjs-wrapper">
        <div>RevealJS renderer not available</div>
      </div>
    );
  }

  const processedContent = preprocessMarkdown(content);
  const isLearningTone = tone === 'learning';

  const components = {
    h1: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h1 className={isLearningTone ? 'mt-6 mb-4 text-4xl font-bold' : 'text-4xl font-bold mt-6 mb-4'} {...props} />,
    h2: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h2 className={isLearningTone ? 'mt-5 mb-3 text-3xl font-semibold' : 'text-3xl font-semibold mt-5 mb-3'} {...props} />,
    h3: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h3 className={isLearningTone ? 'mt-4 mb-2 text-2xl font-semibold' : 'text-2xl font-semibold mt-4 mb-2'} {...props} />,
    h4: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h4 className="mt-3 mb-2 text-xl font-semibold" {...props} />,
    h5: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h5 className="mt-2 mb-1 text-lg font-semibold" {...props} />,
    h6: (props: React.HTMLAttributes<HTMLHeadingElement>) => <h6 className="mt-2 mb-1 text-base font-semibold" {...props} />,
    p: (props: React.HTMLAttributes<HTMLParagraphElement>) => <p className={isLearningTone ? 'mb-4 text-slate-200' : 'mb-4'} {...props} />,
    ul: (props: React.HTMLAttributes<HTMLUListElement>) => <ul className="mb-4 list-disc pl-5" {...props} />,
    ol: (props: React.HTMLAttributes<HTMLOListElement>) => <ol className="mb-4 list-decimal pl-5" {...props} />,
    li: (props: React.HTMLAttributes<HTMLLIElement>) => <li className="mb-1" {...props} />,
    a: (props: React.AnchorHTMLAttributes<HTMLAnchorElement>) => <a className={isLearningTone ? 'text-sky-400 hover:text-sky-300 hover:underline' : 'text-blue-600 hover:underline'} {...props} />,
    blockquote: (props: React.HTMLAttributes<HTMLQuoteElement>) => <blockquote className={isLearningTone ? 'my-4 border-l-4 border-slate-500 pl-4 italic text-slate-300' : 'border-l-4 border-gray-300 pl-4 italic my-4'} {...props} />,
    code: ({ className, children, ...props }: React.HTMLAttributes<HTMLElement> & { className?: string }) => {
      const match = /language-(\w+)/.exec(className || '');
      const language = match && match[1] ? match[1] : '';
      const code = String(children).replace(/\n$/, '');
      const inline = !code.includes('\n');

      if (language === 'mermaid') {
        return <MermaidDiagram code={code} />;
      }

      if (!inline) {
        const customStyle: Record<string, string | number> = {
          padding: '1rem',
          borderRadius: isLearningTone ? '0.75rem' : '0.375rem',
          marginBottom: '1rem',
        };

        if (isLearningTone) {
          customStyle.backgroundColor = '#020617';
        }

        return (
          <SyntaxHighlighter
            style={vscDarkPlus}
            language={language}
            PreTag="div"
            customStyle={customStyle}
            codeTagProps={{
              style: {
                whiteSpace: 'pre-wrap',
                wordBreak: 'keep-all',
                overflowWrap: 'break-word',
              },
            }}
            wrapLines={true}
          >
            {code}
          </SyntaxHighlighter>
        );
      }

      return (
        <code className={isLearningTone ? 'rounded-full border border-slate-700 bg-slate-900 px-2 py-1 font-mono text-sm text-slate-100' : 'bg-gray-100 border border-gray-300 rounded-full px-2 py-1 font-mono text-sm inline whitespace-nowrap'} {...props}>
          {children}
        </code>
      );
    },
    pre: ({ children }: React.HTMLAttributes<HTMLPreElement>) => <>{children}</>,
    div: ({ className, children, ...props }: MarkdownDivProps) => {
      if (className?.includes('admonition')) {
        const type = className.split('-')[1];
        const title = props['data-title'];

        return (
          <div className={`my-4 border-l-4 p-4 ${isLearningTone ? 'rounded-xl' : ''} ${getAdmonitionTone(type, tone)}`}>
            {title ? <div className={isLearningTone ? 'mb-2 font-semibold text-white' : 'font-semibold mb-2'}>{title}</div> : null}
            {children}
          </div>
        );
      }

      if (className === 'markdown-activity') {
        const activityType = props['data-type'] === 'code' ? 'code' : 'quiz';
        const label = activityType === 'code' ? 'Code activity' : 'Knowledge check';
        const toneClasses = activityType === 'code' ? 'border-violet-500/40 bg-violet-500/10' : 'border-emerald-500/40 bg-emerald-500/10';

        return (
          <div className={`my-4 rounded-xl border p-4 text-sm text-slate-100 ${toneClasses}`}>
            <div className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-slate-300">{label}</div>
            <div className={isLearningTone ? 'whitespace-pre-wrap leading-6 text-slate-100' : 'prose prose-invert max-w-none'}>{children}</div>
          </div>
        );
      }

      return (
        <div className={className} {...props}>
          {children}
        </div>
      );
    },
  };

  return (
    <div className={isLearningTone ? 'prose prose-invert max-w-none prose-pre:bg-transparent' : 'markdown-content'}>
      <ReactMarkdown remarkPlugins={[remarkGfm, remarkMath]} rehypePlugins={[rehypeRaw, rehypeKatex]} components={components}>
        {processedContent}
      </ReactMarkdown>
    </div>
  );
}

export default MarkdownRenderer;
