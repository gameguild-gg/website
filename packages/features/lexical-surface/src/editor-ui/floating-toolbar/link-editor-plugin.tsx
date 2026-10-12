/**
 * FloatingLinkEditorPlugin — ported from facebook/lexical playground.
 * Uses `@floating-ui/react` for positioning (matches upstream).
 *
 * Keeps the upstream behavior with package-specific styling:
 * - Floats below the link's range, flips/shifts to stay in viewport.
 * - View mode: shows the URL with edit + delete icon buttons.
 * - Edit mode: text input + cancel/confirm buttons.
 * - Cmd/Ctrl-click on a linked range opens the URL in a new tab.
 */
"use client";

import * as React from "react";
import { Dispatch, useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import {
  autoUpdate,
  flip,
  inline,
  offset,
  shift,
  useFloating,
} from "@floating-ui/react";
import {
  $createLinkNode,
  $isAutoLinkNode,
  $isLinkNode,
  LinkNode,
  TOGGLE_LINK_COMMAND,
} from "@lexical/link";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { $findMatchingParent, mergeRegister } from "@lexical/utils";
import {
  $getSelection,
  $isDecoratorNode,
  $isLineBreakNode,
  $isNodeSelection,
  $isRangeSelection,
  BaseSelection,
  CLICK_COMMAND,
  COMMAND_PRIORITY_CRITICAL,
  COMMAND_PRIORITY_HIGH,
  COMMAND_PRIORITY_LOW,
  getDOMSelection,
  KEY_ESCAPE_COMMAND,
  LexicalEditor,
  RangeSelection,
  SELECTION_CHANGE_COMMAND,
} from "lexical";
import { cn } from "@game-guild/ui/lib/utils";
import { CheckIcon, CloseIcon, DeleteIcon, EditIcon } from "../../icons";
import { getSelectedNode } from "../../shared/lexical/get-selected-node";
import { openSafeUrl, sanitizeUrl } from "../../shared/security/safe-url";

function $getSelectedLinkNode(selection: RangeSelection): LinkNode | null {
  const node = getSelectedNode(selection);
  if ($isLinkNode(node)) {
    return node;
  }
  const linkParent = $findMatchingParent(node, $isLinkNode);
  if ($isLinkNode(linkParent)) {
    return linkParent;
  }
  if (selection.isCollapsed()) {
    const anchor = selection.anchor;
    if (anchor.type === "text") {
      const anchorNode = anchor.getNode();
      if (anchor.offset === anchorNode.getTextContentSize()) {
        const nextSibling = anchorNode.getNextSibling();
        if ($isLinkNode(nextSibling)) {
          return nextSibling;
        }
      }
    }
  }
  return null;
}

function preventDefault(
  event: React.KeyboardEvent<HTMLInputElement> | React.MouseEvent<HTMLElement>,
): void {
  event.preventDefault();
}

function FloatingLinkEditor({
  editor,
  isLink,
  setIsLink,
  anchorElem,
  isLinkEditMode,
  setIsLinkEditMode,
}: {
  editor: LexicalEditor;
  isLink: boolean;
  setIsLink: Dispatch<boolean>;
  anchorElem: HTMLElement;
  isLinkEditMode: boolean;
  setIsLinkEditMode: Dispatch<boolean>;
}) {
  const editorRef = useRef<HTMLDivElement | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const [linkUrl, setLinkUrl] = useState("");
  const [editedLinkUrl, setEditedLinkUrl] = useState("https://");
  const [lastSelection, setLastSelection] = useState<BaseSelection | null>(
    null,
  );

  const scrollerElem = anchorElem.parentElement;

  const { refs, floatingStyles } = useFloating({
    middleware: [
      inline(),
      offset(10),
      flip({ boundary: scrollerElem ?? undefined, padding: 10 }),
      shift({
        boundary: scrollerElem || undefined,
        crossAxis: true,
        mainAxis: true,
        padding: 10,
      }),
    ],
    placement: "bottom-start",
    strategy: "absolute",
    whileElementsMounted: (...args) =>
      autoUpdate(...args, { ancestorScroll: false }),
  });

  const $updateLinkEditor = useCallback(() => {
    const selection = $getSelection();
    if ($isRangeSelection(selection)) {
      const linkNode = $getSelectedLinkNode(selection);
      if (linkNode) {
        setLinkUrl(linkNode.getURL());
      } else {
        setLinkUrl("");
      }
      if (isLinkEditMode) {
        setEditedLinkUrl(linkUrl);
      }
    } else if ($isNodeSelection(selection)) {
      const nodes = selection.getNodes();
      const node = nodes[0];
      if (node) {
        const parent = node.getParent();
        if ($isLinkNode(parent)) {
          setLinkUrl(parent.getURL());
        } else if ($isLinkNode(node)) {
          setLinkUrl(node.getURL());
        } else {
          setLinkUrl("");
        }
        if (isLinkEditMode) {
          setEditedLinkUrl(linkUrl);
        }
      }
    }

    const nativeSelection = getDOMSelection(editor._window);
    const activeElement = document.activeElement;
    const rootElement = editor.getRootElement();

    if (selection !== null && rootElement !== null && editor.isEditable()) {
      let referenceElement: Element | null = null;

      if ($isNodeSelection(selection)) {
        const nodes = selection.getNodes();
        const firstNode = nodes[0];
        if (firstNode) {
          referenceElement = editor.getElementByKey(firstNode.getKey());
        }
      } else if (
        $isRangeSelection(selection) &&
        nativeSelection !== null &&
        nativeSelection.rangeCount > 0 &&
        rootElement.contains(nativeSelection.anchorNode)
      ) {
        const linkNode = $getSelectedLinkNode(selection);
        if (linkNode) {
          const onlyChild =
            linkNode.getChildrenSize() === 1 ? linkNode.getFirstChild() : null;
          referenceElement =
            onlyChild && $isDecoratorNode(onlyChild)
              ? editor.getElementByKey(onlyChild.getKey())
              : editor.getElementByKey(linkNode.getKey());
        }
      }

      if (referenceElement) {
        const refEl = referenceElement;
        refs.setPositionReference({
          getBoundingClientRect: () => refEl.getBoundingClientRect(),
          getClientRects: () => refEl.getClientRects(),
        });
      } else if (
        nativeSelection !== null &&
        nativeSelection.rangeCount > 0 &&
        rootElement.contains(nativeSelection.anchorNode)
      ) {
        refs.setPositionReference(nativeSelection.getRangeAt(0));
      }
      setLastSelection(selection);
    } else if (!activeElement || activeElement.className !== "link-input") {
      setLastSelection(null);
      setIsLinkEditMode(false);
      setLinkUrl("");
    }

    return true;
  }, [editor, setIsLinkEditMode, isLinkEditMode, linkUrl, refs]);

  useEffect(() => {
    return mergeRegister(
      editor.registerUpdateListener(({ editorState }) => {
        editorState.read(() => {
          $updateLinkEditor();
        });
      }),
      editor.registerCommand(
        SELECTION_CHANGE_COMMAND,
        () => {
          $updateLinkEditor();
          return true;
        },
        COMMAND_PRIORITY_LOW,
      ),
      editor.registerCommand(
        KEY_ESCAPE_COMMAND,
        () => {
          if (isLink) {
            setIsLink(false);
            return true;
          }
          return false;
        },
        COMMAND_PRIORITY_HIGH,
      ),
    );
  }, [editor, $updateLinkEditor, setIsLink, isLink]);

  useEffect(() => {
    editor.getEditorState().read(() => {
      $updateLinkEditor();
    });
  }, [editor, $updateLinkEditor]);

  useEffect(() => {
    if (isLinkEditMode && inputRef.current) {
      inputRef.current.focus();
    }
  }, [isLinkEditMode, isLink]);

  useEffect(() => {
    const editorElement = editorRef.current;
    if (editorElement === null) {
      return;
    }
    const handleBlur = (event: FocusEvent) => {
      if (!editorElement.contains(event.relatedTarget as Element) && isLink) {
        setIsLink(false);
        setIsLinkEditMode(false);
      }
    };
    editorElement.addEventListener("focusout", handleBlur);
    return () => {
      editorElement.removeEventListener("focusout", handleBlur);
    };
  }, [editorRef, setIsLink, setIsLinkEditMode, isLink]);

  const handleLinkSubmission = (
    event:
      React.KeyboardEvent<HTMLInputElement> | React.MouseEvent<HTMLElement>,
  ) => {
    event.preventDefault();
    if (lastSelection !== null) {
      if (linkUrl !== "") {
        editor.update(() => {
          editor.dispatchCommand(
            TOGGLE_LINK_COMMAND,
            sanitizeUrl(editedLinkUrl),
          );
          const selection = $getSelection();
          if ($isRangeSelection(selection)) {
            const parent = getSelectedNode(selection).getParent();
            if ($isAutoLinkNode(parent)) {
              const linkNode = $createLinkNode(parent.getURL(), {
                rel: parent.__rel,
                target: parent.__target,
                title: parent.__title,
              });
              parent.replace(linkNode, true);
            }
          }
        });
      }
      setEditedLinkUrl("https://");
      setIsLinkEditMode(false);
    }
  };

  const monitorInputInteraction = (
    event: React.KeyboardEvent<HTMLInputElement>,
  ) => {
    if (event.key === "Enter") {
      handleLinkSubmission(event);
    } else if (event.key === "Escape") {
      event.preventDefault();
      setIsLinkEditMode(false);
    }
  };

  return (
    <div
      ref={(el) => {
        editorRef.current = el;
        refs.setFloating(el);
      }}
      className={cn(
        "z-50 flex items-center gap-1 p-1.5 rounded-md shadow-lg",
        "bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700",
      )}
      style={{
        ...floatingStyles,
        opacity: isLink ? 1 : 0,
        pointerEvents: isLink ? "auto" : "none",
      }}
    >
      {!isLink ? null : isLinkEditMode ? (
        <>
          <input
            ref={inputRef}
            className="link-input flex-1 min-w-[200px] h-7 px-2 text-sm rounded border border-gray-300 dark:border-gray-700 bg-transparent focus:outline-none focus:ring-2 focus:ring-blue-500"
            value={editedLinkUrl}
            onChange={(e) => setEditedLinkUrl(e.target.value)}
            onKeyDown={monitorInputInteraction}
          />
          <button
            type="button"
            aria-label="Cancel"
            onMouseDown={preventDefault}
            onClick={() => setIsLinkEditMode(false)}
            className="inline-flex w-7 h-7 items-center justify-center rounded hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <CloseIcon className="w-4 h-4" />
          </button>
          <button
            type="button"
            aria-label="Confirm"
            onMouseDown={preventDefault}
            onClick={handleLinkSubmission}
            className="inline-flex w-7 h-7 items-center justify-center rounded hover:bg-gray-100 dark:hover:bg-gray-800 text-blue-600 dark:text-blue-400"
          >
            <CheckIcon className="w-4 h-4" />
          </button>
        </>
      ) : (
        <>
          <a
            href={sanitizeUrl(linkUrl)}
            target="_blank"
            rel="noopener noreferrer"
            className="max-w-[300px] truncate text-sm text-blue-600 dark:text-blue-400 underline px-1"
          >
            {linkUrl}
          </a>
          <button
            type="button"
            aria-label="Edit link"
            onMouseDown={preventDefault}
            onClick={(e) => {
              e.preventDefault();
              setEditedLinkUrl(linkUrl);
              setIsLinkEditMode(true);
            }}
            className="inline-flex w-7 h-7 items-center justify-center rounded hover:bg-gray-100 dark:hover:bg-gray-800"
          >
            <EditIcon className="w-4 h-4" />
          </button>
          <button
            type="button"
            aria-label="Remove link"
            onMouseDown={preventDefault}
            onClick={() => editor.dispatchCommand(TOGGLE_LINK_COMMAND, null)}
            className="inline-flex w-7 h-7 items-center justify-center rounded hover:bg-gray-100 dark:hover:bg-gray-800 text-red-600 dark:text-red-400"
          >
            <DeleteIcon className="w-4 h-4" />
          </button>
        </>
      )}
    </div>
  );
}

function useFloatingLinkEditorToolbar(
  editor: LexicalEditor,
  anchorElem: HTMLElement,
  isLinkEditMode: boolean,
  setIsLinkEditMode: Dispatch<boolean>,
) {
  const [activeEditor, setActiveEditor] = useState(editor);
  const [isLink, setIsLink] = useState(false);

  useEffect(() => {
    function $updateToolbar() {
      const selection = $getSelection();
      if ($isRangeSelection(selection)) {
        const focusLinkNode = $getSelectedLinkNode(selection);
        const focusNode = getSelectedNode(selection);
        const focusAutoLinkNode = $findMatchingParent(
          focusNode,
          $isAutoLinkNode,
        );
        if (!(focusLinkNode || focusAutoLinkNode)) {
          setIsLink(false);
          return;
        }
        const badNode = selection
          .getNodes()
          .filter((node) => !$isLineBreakNode(node))
          .find((node) => {
            const linkNode = $findMatchingParent(node, $isLinkNode);
            const autoLinkNode = $findMatchingParent(node, $isAutoLinkNode);
            return (
              (focusLinkNode && !focusLinkNode.is(linkNode)) ||
              (linkNode && !linkNode.is(focusLinkNode)) ||
              (focusAutoLinkNode && !focusAutoLinkNode.is(autoLinkNode)) ||
              (autoLinkNode &&
                (!autoLinkNode.is(focusAutoLinkNode) ||
                  autoLinkNode.getIsUnlinked()))
            );
          });
        setIsLink(!badNode);
      } else if ($isNodeSelection(selection)) {
        const nodes = selection.getNodes();
        const node = nodes[0];
        if (!node) {
          setIsLink(false);
          return;
        }
        const parent = node.getParent();
        setIsLink($isLinkNode(parent) || $isLinkNode(node));
      }
    }
    return mergeRegister(
      editor.registerUpdateListener(({ editorState }) => {
        editorState.read(() => {
          $updateToolbar();
        });
      }),
      editor.registerCommand(
        SELECTION_CHANGE_COMMAND,
        (_payload, newEditor) => {
          $updateToolbar();
          setActiveEditor(newEditor);
          return false;
        },
        COMMAND_PRIORITY_CRITICAL,
      ),
      editor.registerCommand(
        CLICK_COMMAND,
        (payload) => {
          const selection = $getSelection();
          if ($isRangeSelection(selection)) {
            const node = getSelectedNode(selection);
            const linkNode = $findMatchingParent(node, $isLinkNode);
            if ($isLinkNode(linkNode) && (payload.metaKey || payload.ctrlKey)) {
              openSafeUrl(linkNode.getURL());
              return true;
            }
          }
          return false;
        },
        COMMAND_PRIORITY_LOW,
      ),
    );
  }, [editor]);

  return createPortal(
    <FloatingLinkEditor
      editor={activeEditor}
      isLink={isLink}
      anchorElem={anchorElem}
      setIsLink={setIsLink}
      isLinkEditMode={isLinkEditMode}
      setIsLinkEditMode={setIsLinkEditMode}
    />,
    anchorElem,
  );
}

export default function FloatingLinkEditorPlugin({
  anchorElem,
  isLinkEditMode,
  setIsLinkEditMode,
}: {
  anchorElem: HTMLElement;
  isLinkEditMode: boolean;
  setIsLinkEditMode: Dispatch<boolean>;
}) {
  const [editor] = useLexicalComposerContext();
  return useFloatingLinkEditorToolbar(
    editor,
    anchorElem,
    isLinkEditMode,
    setIsLinkEditMode,
  );
}
