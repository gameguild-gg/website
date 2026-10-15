"use client";

import { lazy, Suspense, useMemo, useRef, useState } from "react";
import { Label } from "@game-guild/ui/components/label";
import { Skeleton } from "@game-guild/ui/components/skeleton";
import { type LexicalSurfaceFeatures } from "@game-guild/lexical-surface";
import type { SerializedEditorState } from "lexical";
import { getBlogAssetRepository } from "@/lib/blogs/assets";
import type { BlogContentFormat } from "@/lib/blogs/types";
import { toast } from "sonner";
import { getDefaultBrowserAssetRepository } from "@game-guild/assets/browser";
import type { AssetRecord } from "@game-guild/assets";
import { Image } from "lucide-react";
import { Button } from "@game-guild/ui/components/button";
import { AssetPickerDialog, AssetsProvider } from "@game-guild/assets/react";
import { INSERT_MEDIA_LEXICAL_COMMAND, type MediaType, MediaPlugin } from "@game-guild/lexical-surface/features/media";

const MonacoCodeEditor = lazy(async () => {
  const mod = await import(
    "@/components/block-content-editor/extras/code-studio/monaco-code-editor"
  );
  return { default: mod.MonacoCodeEditor };
});

const MarkdownRenderer = lazy(async () => {
  const mod = await import("@game-guild/content-rendering");
  return { default: mod.MarkdownRenderer };
});

const LexicalSurface = lazy(async () => {
  const mod = await import("@game-guild/lexical-surface");
  return { default: mod.LexicalSurface };
});

const BLOG_EDITOR_FEATURES = {
  toolbar: true,
  insertMenu: true,
  floatingTextFormat: true,
  floatingLinkEditor: true,
  draggable: true,
  picker: true,
  pageLayout: false,
  shortcuts: true,
  equation: true,
  excalidraw: true,
  emoji: true,
  autoEmbed: true,
  contextMenu: true,
  codeAction: true,
  table: true,
  layout: true,
  collapsible: true,
  sticky: true,
  admonition: true,
  button: true,
  divider: true,
  mermaid: true,
  vegaLite: true,
  media: true,
  history: true,
  list: true,
  link: true,
  checkList: true,
  tabIndentation: true,
} satisfies LexicalSurfaceFeatures;

interface BlogBodyEditorProps {
  postId: string;
  format: BlogContentFormat;
  markdown: string;
  jsonBody: string | null;
  onMarkdownChange: (value: string) => void;
  onJsonBodyChange: (value: string) => void;
  onCursorOffsetChange?: (offset: number) => void;
}

function EditorLoadingState() {
  return (
    <div className="space-y-3 rounded-lg border border-gray-200 p-4 dark:border-gray-700">
      <Skeleton className="h-10 w-full" />
      <Skeleton className="h-[300px] w-full" />
    </div>
  );
}

export function BlogBodyEditor({
  postId,
  format,
  markdown,
  jsonBody,
  onMarkdownChange,
  onJsonBodyChange,
  onCursorOffsetChange,
}: BlogBodyEditorProps) {
  const assetRepository = useMemo(() => getBlogAssetRepository(), []);

  if (format === "Lexical") {
    return (
      <div className="space-y-2">
        <Label>Body</Label>
        <p className="text-muted-foreground text-xs">
          Use the rich-text editor to write your post.
        </p>
        <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700">
           <Suspense fallback={<EditorLoadingState />}>
             <LexicalSurface
               namespace="BlogPostEditor"
               mountKey={postId}
               initialState={
                 (jsonBody ?? null) as unknown as SerializedEditorState | null
               }
               onChange={(state) =>
                 onJsonBodyChange(JSON.stringify(state) as unknown as string)
               }
               accessibleLabel="Body"
               placeholder="Start writing your post..."
               contentStyle={{ minHeight: "400px" }}
               contentClassName="max-w-none"
               features={BLOG_EDITOR_FEATURES}
               assetsRepository={assetRepository}
               assetScope={{ type: "BlogPost", id: postId }}
               mediaPlugin={<MediaPlugin />}
             />
           </Suspense>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <Label>Body</Label>
      <div className="grid gap-4 lg:grid-cols-2">
        <div
          className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700"
          style={{ height: "400px" }}
        >
          <Suspense fallback={<EditorLoadingState />}>
            <MonacoCodeEditor
              value={markdown}
              language="markdown"
              ariaLabel="Blog body"
              onChange={onMarkdownChange}
              onCursorOffsetChange={onCursorOffsetChange}
              height="100%"
            />
          </Suspense>
        </div>
        <div
          data-testid="blog-preview"
          className="flex h-[452px] flex-col overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700"
        >
          <div className="border-b border-gray-200 px-4 py-2 text-sm font-medium text-muted-foreground dark:border-gray-700">
            Preview
          </div>
          <div className="flex-1 overflow-auto p-4">
            <Suspense
              fallback={
                <div className="min-h-32 animate-pulse rounded-md bg-muted" />
              }
            >
              <MarkdownRenderer content={markdown} />
            </Suspense>
          </div>
        </div>
      </div>
    </div>
  );
}
