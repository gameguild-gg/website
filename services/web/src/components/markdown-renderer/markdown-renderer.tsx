`use client`;

import type React from 'react';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeRaw from 'rehype-raw';
import { Prism as SyntaxHighlighter } from 'react-syntax-highlighter';
import { vscDarkPlus } from 'react-syntax-highlighter/dist/esm/styles/prism';
import { Admonition } from './Admonition';
import Mermaid from './Mermaid';
import RevealJS from './RevealJS';
import { Api } from '@game-guild/apiclient';
import { MarkdownQuizActivity } from './MarkdownQuizActivity';
import { MarkdownCodeActivity } from './MarkdownCodeActivity';
import LectureEntity = Api.LectureEntity;

export interface MarkdownRendererProps {
  content: string;
  renderer?: LectureEntity.Renderer;
}

const MarkdownRenderer: React.FC<MarkdownRendererProps> = ({ content, renderer = LectureEntity.Renderer.Enum.Markdown }) => {
  if (renderer === LectureEntity.Renderer.Enum.Reveal) {
    return (
      <div className="gameguild-revealjs-wrapper">
        <RevealJS content={content} />
      </div>
    );
  }

  const processedContent = content
    .replace(
      /:::\s*(note|abstract|info|tip|success|question|warning|failure|danger|bug|example|quote)(?:\s+"([^"]*)")?\n([\s\S]*?):::/g,
      (_, type, title, body) => `<div class="admonition admonition-${type}"${title ? ` data-title="${title}"` : ''}>\n\n${body}\n\n</div>`,
    )
    .replace(/!!!\s*(quiz|code)\r?\n([\s\S]*?)\r?\n!!!/g, (_, type, content) => {
      // HTML escape angle brackets in the content if it's a code block
      if (type === 'code') {
        content = content.replace(/</g, '&lt;').replace(/>/g, '&gt;');
      }
      return `<div class="markdown-activity" data-type="${type}">${content}</div>`;
    });

  const components: Record<string, React.FC<any>> = {
    h1: (props) => <h1 className="text-4xl font-bold mt-6 mb-4" {...props} />,
    h2: (props) => <h2 className="text-3xl font-semibold mt-5 mb-3" {...props} />,
    h3: (props) => <h3 className="text-2xl font-semibold mt-4 mb-2" {...props} />,
    h4: (props) => <h4 className="text-xl font-semibold mt-3 mb-2" {...props} />,
    h5: (props) => <h5 className="text-lg font-semibold mt-2 mb-1" {...props} />,
    h6: (props) => <h6 className="text-base font-semibold mt-2 mb-1" {...props} />,
    p: (props) => <p className="mb-4" {...props} />,
    ul: (props) => <ul className="list-disc pl-5 mb-4" {...props} />,
    ol: (props) => <ol className="list-decimal pl-5 mb-4" {...props} />,
    li: (props) => <li className="mb-1" {...props} />,
    a: (props) => <a className="text-blue-600 hover:underline" {...props} />,
    blockquote: (props) => <blockquote className="border-l-4 border-gray-300 pl-4 italic my-4" {...props} />,
    code: ({ node, className, children, ...props }) => {
      const match = /language-(\w+)/.exec(className || '');
      const lang = match && match[1] ? match[1] : '';

      if (lang === 'mermaid') {
        return <Mermaid chart={String(children).replace(/\n$/, '')} />;
      }

      const codeContent = String(children).replace(/\n$/, '');
      const inline = !codeContent.includes('\n');

      if (!inline) {
        return (
          <SyntaxHighlighter
            style={vscDarkPlus}
            language={lang}
            PreTag="div"
            customStyle={{
              padding: '1rem',
              borderRadius: '0.375rem',
              marginBottom: '1rem',
            }}
            codeTagProps={{
              style: {
                whiteSpace: 'pre-wrap',
                wordBreak: 'keep-all',
                overflowWrap: 'break-word',
              },
            }}
            wrapLines={true}
            className="syntax-highlighter"
          >
            {String(children).replace(/\n$/, '')}
          </SyntaxHighlighter>
        );
      }

      return (
        <code className="bg-gray-100 border border-gray-300 rounded-full px-2 py-1 font-mono text-sm inline whitespace-nowrap" {...props}>
          {children}
        </code>
      );
    },
    pre: ({ children }) => <>{children}</>,
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
      if (className === 'markdown-activity') {
        const type = props['data-type'];
        if (type === 'quiz' || type === 'code') {
          try {
            // remove new lines if it is code
            const jsonString = children as string;
            const processedString = type === 'code' ? jsonString.replace(/\n/g, '') : jsonString;
            const data = JSON.parse(processedString);
            if (type === 'quiz') {
              return <MarkdownQuizActivity {...data} />;
            } else if (type === 'code') {
              return <MarkdownCodeActivity {...data} />;
            }
          } catch (error) {
            console.error('Error parsing custom block:', error);
            return <div>Error rendering custom block</div>;
          }
        }
      }
      return (
        <div className={className} {...props}>
          {children}
        </div>
      );
    },
  };

  return (
    <>
      <ReactMarkdown className="markdown-content" remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeRaw]} components={components}>
        {processedContent}
      </ReactMarkdown>
      <style jsx global>{`
        .syntax-highlighter {
          overflow-x: auto;
        }

        .syntax-highlighter pre {
          white-space: pre-wrap !important;
          word-break: keep-all !important;
          overflow-wrap: break-word !important;
        }
      `}</style>
    </>
  );
};

export default MarkdownRenderer;
