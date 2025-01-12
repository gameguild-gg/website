import React from 'react';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import rehypeHighlight from 'rehype-highlight';
import rehypeMermaid from 'rehype-mermaidjs';
import remarkGfm from 'remark-gfm';
import remarkAdmonitions from 'remark-admonitions';
import 'highlight.js/styles/github.css'; // Import a code theme
import 'remark-admonitions/styles/classic.css'; // Import admonitions styles

export default function MarkdownRenderer({ markdown }: { markdown: string }) {
  return (
    <div>
      <ReactMarkdown
      // Allow HTML in the Markdown
      // rehypePlugins={[rehypeRaw, rehypeHighlight, rehypeMermaid]}
      // Support GitHub Flavored Markdown (tables, task lists, etc.)
      // remarkPlugins={[remarkGfm, remarkAdmonitions]}
      >
        {markdown}
      </ReactMarkdown>
    </div>
  );
}
