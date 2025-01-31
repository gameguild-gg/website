'use client';

import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

type Props = {
  body: string;
};

export function CourseDescription({ body }: Readonly<Props>) {
  return (
    <>
      <h3 className="text-xl font-semibold mb-2 text-center">
        Course Description:
      </h3>
      <MarkdownRenderer content={body} />;
    </>
  );
}
