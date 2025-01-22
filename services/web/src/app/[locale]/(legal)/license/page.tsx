// app/code-of-conduct/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer-new/markdown-renderer-new';

const markdownContent: string = `
## DUAL LICENSE

This software is licensed for dual license mode. You can see this licenses here:

[AGPLv3](https://www.gnu.org/licenses/agpl-3.0.en.html)

[COMMERCIAL](https://github.com/gameguild-gg/website/blob/main/COMMERCIAL_LICENSE.md)
`;

export default function Page(): JSX.Element {
  return <MarkdownRenderer content={markdownContent} />;
}
