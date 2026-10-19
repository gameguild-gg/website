/**
 * AutoEmbedPlugin — detects a pasted YouTube / X / Figma URL and offers
 * a typeahead menu to embed it. Adapted from
 * `lexical-playground/src/plugins/AutoEmbedPlugin/index.tsx` with shadcn
 * Dialog as the manual-embed shell.
 */
"use client";

import * as React from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import {
  AutoEmbedOption,
  type EmbedConfig,
  type EmbedMatchResult,
  LexicalAutoEmbedPlugin,
  URL_MATCHER,
} from "@lexical/react/LexicalAutoEmbedPlugin";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { $wrapNodeInElement } from "@lexical/utils";
import {
  $createParagraphNode,
  $insertNodes,
  $isRootOrShadowRoot,
  COMMAND_PRIORITY_EDITOR,
  createCommand,
  type LexicalCommand,
  type LexicalEditor,
} from "lexical";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
import { cn } from "@game-guild/ui/lib/utils";
import { $createYouTubeNode, YouTubeNode } from "./youtube-node";
import { $createTweetNode, TweetNode } from "./tweet-node";
import { $createFigmaNode, FigmaNode } from "./figma-node";
import {
  parseFigmaEmbedUrl,
  parseTwitterEmbedUrl,
  parseYoutubeEmbedUrl,
} from "./embed-url";

export const INSERT_YOUTUBE_COMMAND: LexicalCommand<string> = createCommand(
  "INSERT_YOUTUBE_COMMAND",
);
export const INSERT_TWEET_COMMAND: LexicalCommand<string> = createCommand(
  "INSERT_TWEET_COMMAND",
);
export const INSERT_FIGMA_COMMAND: LexicalCommand<string> = createCommand(
  "INSERT_FIGMA_COMMAND",
);

function insertEmbedNode(
  editor: LexicalEditor,
  factory: () => YouTubeNode | TweetNode | FigmaNode,
) {
  editor.update(() => {
    const node = factory();
    $insertNodes([node]);
    if ($isRootOrShadowRoot(node.getParentOrThrow())) {
      $wrapNodeInElement(node, $createParagraphNode).selectEnd();
    }
  });
}

function useEmbedCommands() {
  const [editor] = useLexicalComposerContext();
  useEffect(() => {
    if (!editor.hasNodes([YouTubeNode, TweetNode, FigmaNode])) {
      throw new Error(
        "AutoEmbedPlugin: YouTubeNode/TweetNode/FigmaNode not registered on editor",
      );
    }
    const unregYT = editor.registerCommand<string>(
      INSERT_YOUTUBE_COMMAND,
      (id) => {
        insertEmbedNode(editor, () => $createYouTubeNode(id));
        return true;
      },
      COMMAND_PRIORITY_EDITOR,
    );
    const unregTW = editor.registerCommand<string>(
      INSERT_TWEET_COMMAND,
      (id) => {
        insertEmbedNode(editor, () => $createTweetNode(id));
        return true;
      },
      COMMAND_PRIORITY_EDITOR,
    );
    const unregFG = editor.registerCommand<string>(
      INSERT_FIGMA_COMMAND,
      (id) => {
        insertEmbedNode(editor, () => $createFigmaNode(id));
        return true;
      },
      COMMAND_PRIORITY_EDITOR,
    );
    return () => {
      unregYT();
      unregTW();
      unregFG();
    };
  }, [editor]);
}

interface PlaygroundEmbedConfig extends EmbedConfig {
  contentName: string;
  exampleUrl: string;
  keywords: string[];
}

export const YoutubeEmbedConfig: PlaygroundEmbedConfig = {
  contentName: "YouTube Video",
  exampleUrl: "https://www.youtube.com/watch?v=jNQXAC9IVRw",
  insertNode: (editor, result) =>
    editor.dispatchCommand(INSERT_YOUTUBE_COMMAND, result.id),
  keywords: ["youtube", "video"],
  parseUrl: parseYoutubeEmbedUrl,
  type: "youtube-video",
};

export const TwitterEmbedConfig: PlaygroundEmbedConfig = {
  contentName: "X (Tweet)",
  exampleUrl: "https://x.com/jack/status/20",
  insertNode: (editor, result) =>
    editor.dispatchCommand(INSERT_TWEET_COMMAND, result.id),
  keywords: ["tweet", "twitter", "x"],
  parseUrl: parseTwitterEmbedUrl,
  type: "tweet",
};

export const FigmaEmbedConfig: PlaygroundEmbedConfig = {
  contentName: "Figma Document",
  exampleUrl: "https://www.figma.com/file/LKQ4FJ4bTnCSjedbRpk931/Sample-File",
  insertNode: (editor, result) =>
    editor.dispatchCommand(INSERT_FIGMA_COMMAND, result.id),
  keywords: ["figma", "mock-up"],
  parseUrl: parseFigmaEmbedUrl,
  type: "figma",
};

export const EmbedConfigs = [
  TwitterEmbedConfig,
  YoutubeEmbedConfig,
  FigmaEmbedConfig,
];

const debounce = (callback: (text: string) => void, delay: number) => {
  let timeoutId: number;
  return (text: string) => {
    window.clearTimeout(timeoutId);
    timeoutId = window.setTimeout(() => callback(text), delay);
  };
};

export function ManualEmbedDialog({
  embedConfig,
  onClose,
}: {
  embedConfig: PlaygroundEmbedConfig;
  onClose: () => void;
}) {
  const [text, setText] = useState("");
  const [editor] = useLexicalComposerContext();
  const [embedResult, setEmbedResult] = useState<EmbedMatchResult | null>(null);

  const validateText = useMemo(
    () =>
      debounce((inputText: string) => {
        const urlMatch = URL_MATCHER.exec(inputText);
        if (embedConfig != null && inputText != null && urlMatch != null) {
          void Promise.resolve(embedConfig.parseUrl(inputText)).then(
            (parseResult) => setEmbedResult(parseResult),
          );
        } else if (embedResult != null) {
          setEmbedResult(null);
        }
      }, 200),
    [embedConfig, embedResult],
  );

  const onClick = () => {
    if (embedResult != null) {
      embedConfig.insertNode(editor, embedResult);
      onClose();
    }
  };

  return (
    <div className="flex flex-col gap-3 min-w-[420px]">
      <input
        type="text"
        placeholder={embedConfig.exampleUrl}
        value={text}
        onChange={(e) => {
          setText(e.target.value);
          validateText(e.target.value);
        }}
        className={cn(
          "h-8 px-2 rounded border text-sm",
          "border-gray-300 dark:border-gray-700",
          "bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100",
          "focus:outline-none focus:ring-1 focus:ring-blue-500",
        )}
      />
      <div className="flex justify-end gap-2">
        <button
          type="button"
          onClick={onClose}
          className="h-8 px-3 rounded border text-sm border-gray-300 dark:border-gray-700 hover:bg-gray-100 dark:hover:bg-gray-800"
        >
          Cancel
        </button>
        <button
          type="button"
          disabled={!embedResult}
          onClick={onClick}
          className="h-8 px-3 rounded text-sm bg-blue-600 text-white hover:bg-blue-700 disabled:opacity-50 disabled:pointer-events-none"
        >
          Embed
        </button>
      </div>
    </div>
  );
}

export function AutoEmbedPlugin(): React.JSX.Element {
  useEmbedCommands();
  const [active, setActive] = useState<PlaygroundEmbedConfig | null>(null);

  const openEmbedModal = useCallback((embedConfig: PlaygroundEmbedConfig) => {
    setActive(embedConfig);
  }, []);

  const getMenuOptions = useCallback(
    (
      activeEmbedConfig: PlaygroundEmbedConfig,
      embedFn: () => void,
      dismissFn: () => void,
    ) => [
      new AutoEmbedOption("Dismiss", { onSelect: dismissFn }),
      new AutoEmbedOption(`Embed ${activeEmbedConfig.contentName}`, {
        onSelect: embedFn,
      }),
    ],
    [],
  );

  return (
    <>
      <Dialog
        open={active !== null}
        onOpenChange={(open) => {
          if (!open) setActive(null);
        }}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>
              {active ? `Embed ${active.contentName}` : ""}
            </DialogTitle>
          </DialogHeader>
          {active && (
            <ManualEmbedDialog
              embedConfig={active}
              onClose={() => setActive(null)}
            />
          )}
        </DialogContent>
      </Dialog>
      <LexicalAutoEmbedPlugin<PlaygroundEmbedConfig>
        embedConfigs={EmbedConfigs}
        onOpenEmbedModalForConfig={openEmbedModal}
        getMenuOptions={getMenuOptions}
        menuRenderFn={(
          anchorElementRef,
          {
            selectedIndex,
            options,
            selectOptionAndCleanUp,
            setHighlightedIndex,
          },
        ) => {
          if (!anchorElementRef.current || options.length === 0) return null;
          return createPortal(
            <div
              role="listbox"
              className={cn(
                "z-50 min-w-[220px] rounded-md p-1 shadow-2xl",
                "border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900",
              )}
            >
              {options.map((option, i) => {
                const isSelected = selectedIndex === i;
                return (
                  <button
                    key={option.key}
                    ref={(el) => option.setRefElement(el)}
                    type="button"
                    role="option"
                    aria-selected={isSelected}
                    tabIndex={-1}
                    onMouseEnter={() => setHighlightedIndex(i)}
                    onClick={() => selectOptionAndCleanUp(option)}
                    className={cn(
                      "flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-left text-sm",
                      isSelected
                        ? "bg-blue-600 text-white"
                        : "text-gray-800 dark:text-gray-200 hover:bg-gray-100 dark:hover:bg-gray-800",
                    )}
                  >
                    {option.title}
                  </button>
                );
              })}
            </div>,
            anchorElementRef.current,
          );
        }}
      />
    </>
  );
}
