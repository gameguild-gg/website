'use client';

import React from 'react';
import ReactMarkdown, { Components } from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import rehypeHighlight from 'rehype-highlight';
import Mermaid from './Mermaid';
import { Admonition } from './Admonition';
import RevealJS from './RevealJS';
import { Api } from '@game-guild/apiclient';

interface MarkdownRendererProps {
  content: string;
  renderer?: Api.LectureEntity.Renderer;
}

const MarkdownRenderer: React.FC<MarkdownRendererProps> = ({
  content,
  renderer = Api.LectureEntity.Renderer.Enum.Markdown,
}) => {
  if (renderer === Api.LectureEntity.Renderer.Enum.Reveal) {
    return (
      <div className="gameguild-revealjs-wrapper">
        <RevealJS content={content} />
      </div>
    );
  }

  const processedContent = content.replace(
    /:::\s*(note|abstract|info|tip|success|question|warning|failure|danger|bug|example|quote)(?:\s+"([^"]*)")?\n([\s\S]*?):::/g,
    (_, type, title, body) =>
      `<div class="admonition admonition-${type}"${title ? ` data-title="${title}"` : ''}>\n\n${body}\n\n</div>`,
  );

  const components: Record<string, React.FC<any>> = {
    h1: (props) => <h1 className="text-4xl font-bold mt-6 mb-4" {...props} />,
    h2: (props) => (
      <h2 className="text-3xl font-semibold mt-5 mb-3" {...props} />
    ),
    h3: (props) => (
      <h3 className="text-2xl font-semibold mt-4 mb-2" {...props} />
    ),
    h4: (props) => (
      <h4 className="text-xl font-semibold mt-3 mb-2" {...props} />
    ),
    h5: (props) => (
      <h5 className="text-lg font-semibold mt-2 mb-1" {...props} />
    ),
    h6: (props) => (
      <h6 className="text-base font-semibold mt-2 mb-1" {...props} />
    ),
    p: (props) => <p className="mb-4" {...props} />,
    ul: (props) => <ul className="list-disc pl-5 mb-4" {...props} />,
    ol: (props) => <ol className="list-decimal pl-5 mb-4" {...props} />,
    li: (props) => <li className="mb-1" {...props} />,
    a: (props) => <a className="text-blue-600 hover:underline" {...props} />,
    blockquote: (props) => (
      <blockquote
        className="border-l-4 border-gray-300 pl-4 italic my-4"
        {...props}
      />
    ),
    code: ({ className, children, ...props }) => {
      const match = /language-(\w+)/.exec(className || '');
      const language = match ? match[1] : '';

      if (language === 'mermaid') {
        return <Mermaid chart={String(children).replace(/\n$/, '')} />;
      }

      const codeContent = String(children).replace(/\n$/, '');
      const isInline = !codeContent.includes('\n');

      if (isInline) {
        return (
          <code
            className="bg-gray-100 rounded px-1 py-0.5 font-mono text-sm inline"
            {...props}
          >
            {children}
          </code>
        );
      }

      return (
        <code className={`block ${className}`} {...props}>
          {children}
        </code>
      );
    },
    pre: ({ children }) => (
      <pre className="mb-4 p-4 bg-gray-100 rounded overflow-x-auto">
        {children}
      </pre>
    ),
    div: ({ className, children, ...props }) => {
      if (className?.includes('admonition')) {
        const type = className.split('-')[1] as
          | 'note'
          | 'abstract'
          | 'info'
          | 'tip'
          | 'success'
          | 'question'
          | 'warning'
          | 'failure'
          | 'danger'
          | 'bug'
          | 'example'
          | 'quote';
        const title = props['data-title'] as string | undefined;
        return (
          <Admonition type={type} title={title}>
            {children}
          </Admonition>
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
    <ReactMarkdown
      className="markdown-content"
      remarkPlugins={[remarkGfm]}
      rehypePlugins={[rehypeRaw, rehypeHighlight]}
      components={components}
    >
      {processedContent}
    </ReactMarkdown>
  );
};

export default MarkdownRenderer;
