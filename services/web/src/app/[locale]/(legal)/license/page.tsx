// app/code-of-conduct/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';

const markdownContent: string = `
## DUAL LICENSE

This software is licensed for dual license mode. You can see this licenses here:

[AGPLv3](https://www.gnu.org/licenses/agpl-3.0.en.html)

[COMMERCIAL](https://github.com/gameguild-gg/website/blob/main/COMMERCIAL_LICENSE.md)
`;

export default function Page(): JSX.Element {
  return (
    <div className="prose prose-lg max-w-none mx-auto px-4 py-6 prose-headings:text-blue-600 prose-a:text-blue-500 hover:prose-a:underline prose-strong:text-gray-800">
      <MarkdownRenderer content={markdownContent} />
    </div>
  );
}
