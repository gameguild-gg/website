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
import { VegaLiteDiagram } from './vega-lite-diagram';

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
      /:::\s*(note|abstract|info|tip|success|question|warning|failure|danger|bug|example|quote|important|caution|attention|hint|check|summary)(?:\s+"([^"]*)")?\n([\s\S]*?):::/g,
      (_, type, title, body) => `<div class="admonition admonition-${type}"${title ? ` data-title="${title}"` : ''}>\n\n${body}\n\n</div>`,
    )
    .replace(/!!!\s*(quiz|code)\n([\s\S]*?)\n!!!/g, (_, type, body) => {
      const safeBody = type === 'code' ? body.replace(/</g, '&lt;').replace(/>/g, '&gt;') : body;
      return `<div class="markdown-activity" data-type="${type}">${safeBody}</div>`;
    });
}

// Accents mirror the lexical-surface Admonition ACCENT_BY_TYPE palette so
// markdown `:::` callouts and Lexical admonition nodes look the same.
const LEARNING_TONE_CLASSES: Record<string, string> = {
  note: 'border-blue-400 bg-blue-500/10 dark:border-blue-500',
  abstract: 'border-sky-400 bg-sky-500/10 dark:border-sky-500',
  info: 'border-cyan-400 bg-cyan-500/10 dark:border-cyan-500',
  tip: 'border-lime-400 bg-lime-500/10 dark:border-lime-500',
  success: 'border-green-400 bg-green-500/10 dark:border-green-500',
  question: 'border-amber-400 bg-amber-500/10 dark:border-amber-500',
  warning: 'border-yellow-400 bg-yellow-500/10 dark:border-yellow-500',
  failure: 'border-red-400 bg-red-500/10 dark:border-red-500',
  danger: 'border-orange-400 bg-orange-500/10 dark:border-orange-500',
  bug: 'border-stone-400 bg-stone-500/10 dark:border-stone-500',
  example: 'border-teal-400 bg-teal-500/10 dark:border-teal-500',
  quote: 'border-pink-400 bg-pink-500/10 dark:border-pink-500',
  important: 'border-purple-400 bg-purple-500/10 dark:border-purple-500',
  caution: 'border-rose-400 bg-rose-500/10 dark:border-rose-500',
  attention: 'border-fuchsia-400 bg-fuchsia-500/10 dark:border-fuchsia-500',
  hint: 'border-emerald-400 bg-emerald-500/10 dark:border-emerald-500',
  check: 'border-indigo-400 bg-indigo-500/10 dark:border-indigo-500',
  summary: 'border-violet-400 bg-violet-500/10 dark:border-violet-500',
};

const ADMONITION_BODY_TEXT = 'text-slate-800 dark:text-slate-100';

function getAdmonitionTone(type: string | undefined, tone: MarkdownRendererTone) {
  if (tone === 'learning') {
    return `${LEARNING_TONE_CLASSES[type ?? ''] ?? LEARNING_TONE_CLASSES.note} ${ADMONITION_BODY_TEXT}`;
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
    p: (props: React.HTMLAttributes<HTMLParagraphElement>) => <p className={isLearningTone ? 'mb-4 text-slate-700 dark:text-slate-200' : 'mb-4'} {...props} />,
    ul: (props: React.HTMLAttributes<HTMLUListElement>) => <ul className="mb-4 list-disc pl-5" {...props} />,
    ol: (props: React.HTMLAttributes<HTMLOListElement>) => <ol className="mb-4 list-decimal pl-5" {...props} />,
    li: (props: React.HTMLAttributes<HTMLLIElement>) => <li className="mb-1" {...props} />,
    a: (props: React.AnchorHTMLAttributes<HTMLAnchorElement>) => <a className={isLearningTone ? 'text-sky-700 hover:text-sky-600 hover:underline dark:text-sky-400 dark:hover:text-sky-300' : 'text-blue-600 hover:underline'} {...props} />,
    blockquote: (props: React.HTMLAttributes<HTMLQuoteElement>) => <blockquote className={isLearningTone ? 'my-4 border-l-4 border-slate-300 pl-4 italic text-slate-600 dark:border-slate-500 dark:text-slate-300' : 'border-l-4 border-gray-300 pl-4 italic my-4'} {...props} />,
    code: ({ className, children, ...props }: React.HTMLAttributes<HTMLElement> & { className?: string }) => {
      const match = /language-([\w-]+)/.exec(className || '');
      const language = match && match[1] ? match[1] : '';
      const code = String(children).replace(/\n$/, '');
      const inline = !code.includes('\n');

      if (language === 'mermaid') {
        return <MermaidDiagram code={code} />;
      }

      if (language === 'vegalite' || language === 'vega-lite') {
        return <VegaLiteDiagram spec={code} />;
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
        <code className={isLearningTone ? 'rounded-full border border-slate-300 bg-slate-100 px-2 py-1 font-mono text-sm text-slate-800 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100' : 'bg-gray-100 border border-gray-300 rounded-full px-2 py-1 font-mono text-sm inline whitespace-nowrap'} {...props}>
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
            {title ? <div className={isLearningTone ? 'mb-2 font-semibold text-slate-900 dark:text-white' : 'font-semibold mb-2'}>{title}</div> : null}
            {children}
          </div>
        );
      }

      if (className === 'markdown-activity') {
        const activityType = props['data-type'] === 'code' ? 'code' : 'quiz';
        const label = activityType === 'code' ? 'Code activity' : 'Knowledge check';
        const toneClasses = activityType === 'code' ? 'border-violet-500/40 bg-violet-500/10' : 'border-emerald-500/40 bg-emerald-500/10';

        return (
          <div className={`my-4 rounded-xl border p-4 text-sm text-slate-800 dark:text-slate-100 ${toneClasses}`}>
            <div className="mb-2 text-xs font-semibold uppercase tracking-[0.18em] text-slate-600 dark:text-slate-300">{label}</div>
            <div className={isLearningTone ? 'whitespace-pre-wrap leading-6 text-slate-800 dark:text-slate-100' : 'prose prose-invert max-w-none'}>{children}</div>
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
    <div className={isLearningTone ? 'prose dark:prose-invert max-w-none prose-pre:bg-transparent' : 'markdown-content'}>
      <ReactMarkdown remarkPlugins={[remarkGfm, remarkMath]} rehypePlugins={[rehypeRaw, rehypeKatex]} components={components}>
        {processedContent}
      </ReactMarkdown>
    </div>
  );
}

export default MarkdownRenderer;
