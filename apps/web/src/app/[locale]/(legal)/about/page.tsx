// app/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer/markdown-renderer';
import { MarkdownContent } from '@/components/markdown-content';

const markdownContent: string = `
# About GameGuild

**GameGuild** is a platform dedicated to bringing together gamers, developers, and enthusiasts through a unique combination of educational resources, a blog, and exciting game tests.

## What We Offer
- **Educational Resources:** Tutorials and guides to help you improve your skills in game development and design.
- **Blog:** Stay updated with the latest trends, tips, and stories in the gaming world.
- **Game Testing:** Try out new games and provide valuable feedback to developers.

## Our Vision
We aim to be a hub for creativity and innovation in the gaming industry, empowering individuals to turn their ideas into reality.

&copy; 2025 GameGuild. All rights reserved.

Dual Licensed under [AGPLv3](https://www.gnu.org/licenses/agpl-3.0.en.html) and [COMMERCIAL](https://github.com/gameguild-gg/website/blob/main/COMMERCIAL_LICENSE.md)
`;

export default function Page(): JSX.Element {
  return (
    <div className="prose prose-lg max-w-none mx-auto px-4 py-6 prose-headings:text-blue-600 prose-a:text-blue-500 hover:prose-a:underline prose-strong:text-gray-800">
      <MarkdownContent content={markdownContent} />
    </div>
  );
}
