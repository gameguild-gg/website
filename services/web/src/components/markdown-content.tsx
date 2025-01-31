'use client';

import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

type Props = {
  content: string;
};

export function MarkdownContent({ content }: Readonly<Props>): JSX.Element {
  return <MarkdownRenderer content={content} />;
}
