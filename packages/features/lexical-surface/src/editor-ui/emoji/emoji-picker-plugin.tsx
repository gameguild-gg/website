/**
 * EmojiPickerPlugin — typeahead trigger `:` for inserting Unicode emoji
 * glyphs. Adapted from `lexical-playground/src/plugins/EmojiPickerPlugin`
 * but without `EmojiNode`/sprite: inserts a plain `TextNode` with the
 * glyph.
 */
"use client";

import * as React from "react";
import { useCallback, useEffect, useMemo, useState } from "react";
import { createPortal } from "react-dom";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import {
  LexicalTypeaheadMenuPlugin,
  MenuOption,
  useBasicTypeaheadTriggerMatch,
} from "@lexical/react/LexicalTypeaheadMenuPlugin";
import {
  $createTextNode,
  $getSelection,
  $isRangeSelection,
  type TextNode,
} from "lexical";
import { cn } from "@game-guild/ui/lib/utils";
import type { Emoji } from "./emoji-list";

class EmojiOption extends MenuOption {
  title: string;
  emoji: string;
  keywords: string[];
  constructor(title: string, emoji: string, options: { keywords?: string[] }) {
    super(title);
    this.title = title;
    this.emoji = emoji;
    this.keywords = options.keywords ?? [];
  }
}

const MAX_EMOJI_SUGGESTION_COUNT = 10;

export function EmojiPickerPlugin(): React.JSX.Element | null {
  const [editor] = useLexicalComposerContext();
  const [queryString, setQueryString] = useState<string | null>(null);
  const [emojis, setEmojis] = useState<Emoji[]>([]);

  useEffect(() => {
    let cancelled = false;
    void import("./emoji-list").then((mod) => {
      if (!cancelled) setEmojis(mod.default);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  const emojiOptions = useMemo(
    () =>
      emojis.map(({ emoji, aliases, tags }) => {
        const firstAlias = aliases[0] ?? emoji;
        return new EmojiOption(`${emoji} ${firstAlias}`, emoji, {
          keywords: [...aliases, ...tags],
        });
      }),
    [emojis],
  );

  const checkForTriggerMatch = useBasicTypeaheadTriggerMatch(":", {
    minLength: 1,
    punctuation: "\\.,\\+\\*\\?\\$\\@\\|#{}\\(\\)\\^\\[\\]\\\\/!%'\"~=<>:;",
  });

  const options = useMemo(() => {
    if (!queryString) return emojiOptions.slice(0, MAX_EMOJI_SUGGESTION_COUNT);
    const query = queryString.toLowerCase();
    return emojiOptions
      .filter(
        (option) =>
          option.title.toLowerCase().includes(query) ||
          option.keywords.some((keyword) =>
            keyword.toLowerCase().includes(query),
          ),
      )
      .slice(0, MAX_EMOJI_SUGGESTION_COUNT);
  }, [emojiOptions, queryString]);

  const onSelectOption = useCallback(
    (
      selectedOption: EmojiOption,
      nodeToRemove: TextNode | null,
      closeMenu: () => void,
    ) => {
      editor.update(() => {
        const selection = $getSelection();
        if (!$isRangeSelection(selection) || selectedOption == null) return;
        if (nodeToRemove) nodeToRemove.remove();
        selection.insertNodes([$createTextNode(selectedOption.emoji)]);
        closeMenu();
      });
    },
    [editor],
  );

  return (
    <LexicalTypeaheadMenuPlugin<EmojiOption>
      onQueryChange={setQueryString}
      onSelectOption={onSelectOption}
      triggerFn={checkForTriggerMatch}
      options={options}
      anchorClassName="z-[60]"
      menuRenderFn={(
        anchorElementRef,
        { selectedIndex, selectOptionAndCleanUp, setHighlightedIndex },
      ) => {
        if (!anchorElementRef.current || options.length === 0) return null;
        return createPortal(
          <div
            role="listbox"
            className={cn(
              "z-50 min-w-[220px] max-h-[280px] overflow-y-auto rounded-md p-1 shadow-2xl",
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
                  <span className="text-base leading-none">{option.emoji}</span>
                  <span className="truncate">
                    {option.title.replace(option.emoji, "").trim()}
                  </span>
                </button>
              );
            })}
          </div>,
          anchorElementRef.current,
        );
      }}
    />
  );
}
