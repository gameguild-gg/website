'use client';

import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

type Props = {
  summary: string;
};

export function CourseSummary({ summary }: Readonly<Props>) {
  return <MarkdownRenderer content={summary} />;
}
