'use client';

import MarkdownRenderer, {
  MarkdownRendererProps,
} from '@/components/markdown-renderer/markdown-renderer';
import { Api } from '@game-guild/apiclient';

export function MarkdownContent({
  content,
  renderer = Api.LectureEntity.Renderer.Enum.Markdown,
}: Readonly<MarkdownRendererProps>): JSX.Element {
  return <MarkdownRenderer content={content} renderer={renderer} />;
}
