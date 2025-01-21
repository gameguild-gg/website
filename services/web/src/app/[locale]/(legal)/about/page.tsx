// app/page.tsx
import React from 'react';
import MarkdownRenderer from '@/components/markdown-renderer-new/markdown-renderer-new';

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
`;

export default function Page(): JSX.Element {
  return <MarkdownRenderer content={markdownContent} />;
}
