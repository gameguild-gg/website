/**
 * Top toolbar for our LexicalSurface. Ported from facebook/lexical
 * playground `ToolbarPlugin/index.tsx` with package-specific adjustments:
 *
 * - Tailwind classes throughout (no playground CSS imports).
 * - Icons via `lucide-react` (mapped in `../icons`).
 * - Uses Prism for code languages without the playground's theme dropdown.
 * - The Insert dropdown is filtered by the surface feature flags.
 * - Shared formatting controls are owned by `editor-ui/formatting` so this
 *   module only coordinates top-toolbar state and layout.
 * - Feature insertions come from the package insertion catalog.
 */
"use client";

import * as React from "react";
import { Dispatch, useCallback, useEffect, useState } from "react";
import { $isCodeNode } from "@lexical/code";
import {
  CODE_LANGUAGE_FRIENDLY_NAME_MAP,
  getCodeLanguageOptions,
  getLanguageFriendlyName,
} from "@lexical/code-prism";
import { $isLinkNode, TOGGLE_LINK_COMMAND } from "@lexical/link";
import { $isListNode, ListNode } from "@lexical/list";
import { $isHeadingNode } from "@lexical/rich-text";
import {
  $getSelectionStyleValueForProperty,
  $isParentElementRTL,
  $patchStyleText,
} from "@lexical/selection";
import { $isTableNode, $isTableSelection } from "@lexical/table";
import {
  $findMatchingParent,
  $getNearestNodeOfType,
  $isEditorIsNestedEditor,
  IS_APPLE,
  mergeRegister,
} from "@lexical/utils";
import {
  $addUpdateTag,
  $getNodeByKey,
  $getSelection,
  $createTextNode,
  $isElementNode,
  $isNodeSelection,
  $isRangeSelection,
  $isRootOrShadowRoot,
  CAN_REDO_COMMAND,
  CAN_UNDO_COMMAND,
  COMMAND_PRIORITY_CRITICAL,
  CommandPayloadType,
  FORMAT_TEXT_COMMAND,
  HISTORIC_TAG,
  LexicalEditor,
  LexicalNode,
  REDO_COMMAND,
  SELECTION_CHANGE_COMMAND,
  SKIP_DOM_SELECTION_TAG,
  SKIP_SELECTION_FOCUS_TAG,
  TextFormatType,
  UNDO_COMMAND,
} from "lexical";
import { cn } from "@game-guild/ui/lib/utils";
import {
  BgColorIcon,
  BoldIcon,
  ClearFormatIcon,
  CodeInlineIcon,
  HighlightIcon,
  SubscriptIcon,
  SuperscriptIcon,
  InsertIcon,
  ItalicIcon,
  LinkIcon,
  RedoIcon,
  StrikethroughIcon,
  TextColorIcon,
  UnderlineIcon,
  UndoIcon,
} from "../../icons";
import { DropDown, DropDownItem } from "../../shared/ui/dropdown";
import { DropdownColorPicker } from "../../shared/ui/dropdown-color-picker";
import { PageSettingsDropDown } from "./page-settings-menu";
import { getSelectedNode } from "../../shared/lexical/get-selected-node";
import {
  blockTypeToBlockName,
  DEFAULT_FONT_SIZE,
  useToolbarState,
} from "./toolbar-context";
import {
  $getEnclosingCodeNode,
  $readTextFormatState,
  BlockFormatDropDown,
  CaseFormatDropDown,
  ElementFormatDropdown,
  FontDropDown,
  FontSizeStepper,
  clearFormatting,
  isKeyboardInput,
} from "../formatting";
import { $isEquationNode } from "../../features/equation/equation-node";
import { EmojiPickerPanel } from "../emoji";
import { Smile as EmojiIcon } from "lucide-react";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@game-guild/ui/components/popover";
import type { LexicalSurfaceFeatures } from "../../capabilities/feature-flags";
import { getEnabledInsertions } from "../../capabilities/insertion-catalog";
import { InsertionDialog } from "../../capabilities/insertion-dialog";
import type { InsertionDialogDefinition } from "../../capabilities/insertion-types";
// ─── sub-components ─────────────────────────────────────────────────────────

function Divider() {
  return (
    <div
      className="w-px h-5 mx-1 bg-gray-200 dark:bg-gray-700 self-center"
      aria-hidden
    />
  );
}

function ToolbarButton({
  active,
  disabled,
  onClick,
  title,
  ariaLabel,
  children,
}: {
  active?: boolean;
  disabled?: boolean;
  onClick: (e: React.MouseEvent<HTMLButtonElement>) => void;
  title?: string;
  ariaLabel: string;
  children: React.ReactNode;
}) {
  return (
    <button
      type="button"
      disabled={disabled}
      onClick={onClick}
      title={title}
      aria-label={ariaLabel}
      className={cn(
        "inline-flex items-center justify-center w-8 h-8 rounded text-gray-700 dark:text-gray-200",
        "hover:bg-gray-100 dark:hover:bg-gray-800 disabled:opacity-40 disabled:pointer-events-none",
        active &&
          "bg-blue-50 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300",
      )}
    >
      {children}
    </button>
  );
}

function CodeLanguageDropDown({
  editor,
  language,
  codeNodeKey,
  disabled,
}: {
  editor: LexicalEditor;
  language: string;
  codeNodeKey: string;
  disabled?: boolean;
}) {
  const options = React.useMemo(() => getCodeLanguageOptions(), []);
  const friendly =
    CODE_LANGUAGE_FRIENDLY_NAME_MAP[language] ??
    (language ? getLanguageFriendlyName(language) : "Plain Text");

  const onSelect = (value: string) => {
    editor.update(() => {
      const node = $getNodeByKey(codeNodeKey);
      if (node && $isCodeNode(node)) node.setLanguage(value);
    });
  };

  return (
    <DropDown
      disabled={disabled}
      buttonLabel={friendly}
      buttonClassName="w-[140px] truncate justify-start"
      buttonAriaLabel="Select code language"
    >
      {options.map(([value, label]) => (
        <DropDownItem
          key={value || "plain"}
          active={value === language}
          onClick={() => onSelect(value)}
        >
          {label}
        </DropDownItem>
      ))}
    </DropDown>
  );
}

function $findTopLevelElement(node: LexicalNode) {
  let topLevelElement =
    node.getKey() === "root"
      ? node
      : $findMatchingParent(node, (e) => {
          const parent = e.getParent();
          return parent !== null && $isRootOrShadowRoot(parent);
        });

  if (topLevelElement === null) {
    topLevelElement = node.getTopLevelElementOrThrow();
  }
  return topLevelElement;
}

// ─── main plugin ────────────────────────────────────────────────────────────

function EmojiPickerPopover({
  editor,
  disabled,
}: {
  editor: LexicalEditor;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);

  const insert = useCallback(
    (emoji: string) => {
      editor.update(() => {
        $addUpdateTag(SKIP_SELECTION_FOCUS_TAG);
        const selection = $getSelection();
        if ($isRangeSelection(selection)) {
          selection.insertNodes([$createTextNode(emoji)]);
        }
      });
      setOpen(false);
    },
    [editor],
  );

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger
        render={
          <button
            type="button"
            disabled={disabled}
            title="Insert emoji"
            aria-label="Insert emoji"
            className="inline-flex h-8 items-center justify-center gap-1 rounded px-2 text-sm text-gray-800 hover:bg-gray-100 disabled:pointer-events-none disabled:opacity-40 dark:text-gray-100 dark:hover:bg-gray-800"
          >
            <EmojiIcon className="h-4 w-4" />
            <span className="hidden sm:inline">Emoji</span>
          </button>
        }
      />
      <PopoverContent align="start" sideOffset={4} className="w-auto p-2">
        <EmojiPickerPanel onSelect={insert} />
      </PopoverContent>
    </Popover>
  );
}

export default function ToolbarPlugin({
  editor,
  activeEditor,
  setActiveEditor,
  setIsLinkEditMode,
  features,
}: {
  editor: LexicalEditor;
  activeEditor: LexicalEditor;
  setActiveEditor: Dispatch<LexicalEditor>;
  setIsLinkEditMode: Dispatch<boolean>;
  features: Required<LexicalSurfaceFeatures>;
}) {
  const [selectedElementKey, setSelectedElementKey] = useState<string | null>(
    null,
  );
  const [isEditable, setIsEditable] = useState(() => editor.isEditable());
  const [insertDialog, setInsertDialog] =
    useState<InsertionDialogDefinition | null>(null);
  const { toolbarState, updateToolbarState } = useToolbarState();
  const insertions = React.useMemo(
    () => getEnabledInsertions(features, "toolbar"),
    [features],
  );

  const dispatchToolbarCommand = <
    T extends typeof FORMAT_TEXT_COMMAND | typeof UNDO_COMMAND | typeof REDO_COMMAND,
  >(
    command: T,
    payload?: CommandPayloadType<T>,
    skipRefocus: boolean = false,
  ) => {
    activeEditor.update(() => {
      if (skipRefocus) {
        $addUpdateTag(SKIP_DOM_SELECTION_TAG);
      }
      activeEditor.dispatchCommand(command, payload as CommandPayloadType<T>);
    });
  };

  const dispatchFormatTextCommand = (
    payload: TextFormatType,
    skipRefocus: boolean = false,
  ) => dispatchToolbarCommand(FORMAT_TEXT_COMMAND, payload, skipRefocus);

  const $handleHeadingNode = useCallback(
    (selectedElement: LexicalNode) => {
      const type = $isHeadingNode(selectedElement)
        ? selectedElement.getTag()
        : selectedElement.getType();
      if (type in blockTypeToBlockName) {
        updateToolbarState(
          "blockType",
          type as keyof typeof blockTypeToBlockName,
        );
      }
    },
    [updateToolbarState],
  );

  const $updateToolbar = useCallback(() => {
    const selection = $getSelection();
    if ($isRangeSelection(selection)) {
      if (activeEditor !== editor && $isEditorIsNestedEditor(activeEditor)) {
        const rootElement = activeEditor.getRootElement();
        updateToolbarState(
          "isImageCaption",
          !!rootElement?.parentElement?.classList.contains(
            "image-caption-container",
          ),
        );
      } else {
        updateToolbarState("isImageCaption", false);
      }

      const anchorNode = selection.anchor.getNode();
      const element = $findTopLevelElement(anchorNode);
      const elementKey = element.getKey();
      const elementDOM = activeEditor.getElementByKey(elementKey);

      updateToolbarState("isRTL", $isParentElementRTL(selection));

      const node = getSelectedNode(selection);
      const parent = node.getParent();
      const isLink = $isLinkNode(parent) || $isLinkNode(node);
      updateToolbarState("isLink", isLink);

      const tableNode = $findMatchingParent(node, $isTableNode);
      updateToolbarState(
        "rootType",
        $isTableNode(tableNode) ? "table" : "root",
      );

      if (elementDOM !== null) {
        setSelectedElementKey(elementKey);
        if ($isListNode(element)) {
          const parentList = $getNearestNodeOfType<ListNode>(
            anchorNode,
            ListNode,
          );
          const type = parentList
            ? parentList.getListType()
            : element.getListType();
          updateToolbarState("blockType", type);
        } else {
          $handleHeadingNode(element);
          if ($isCodeNode(element)) {
            updateToolbarState("blockType", "code");
            updateToolbarState("codeLanguage", element.getLanguage() ?? "");
          }
        }
      }

      const formatState = $readTextFormatState(selection);
      updateToolbarState("fontColor", formatState.fontColor);
      updateToolbarState("bgColor", formatState.bgColor);
      updateToolbarState("fontFamily", formatState.fontFamily);
      updateToolbarState("fontSize", formatState.fontSize);
      updateToolbarState("isBold", formatState.isBold);
      updateToolbarState("isItalic", formatState.isItalic);
      updateToolbarState("isUnderline", formatState.isUnderline);
      updateToolbarState("isStrikethrough", formatState.isStrikethrough);
      updateToolbarState("isSubscript", formatState.isSubscript);
      updateToolbarState("isSuperscript", formatState.isSuperscript);
      updateToolbarState("isHighlight", formatState.isHighlight);
      updateToolbarState("isCode", formatState.isCode);
      updateToolbarState("isLowercase", formatState.isLowercase);
      updateToolbarState("isUppercase", formatState.isUppercase);
      updateToolbarState("isCapitalize", formatState.isCapitalize);

      let matchingParent;
      if ($isLinkNode(parent)) {
        matchingParent = $findMatchingParent(
          node,
          (parentNode) => $isElementNode(parentNode) && !parentNode.isInline(),
        );
      }

      updateToolbarState(
        "elementFormat",
        $isElementNode(matchingParent)
          ? matchingParent.getFormatType()
          : $isElementNode(node)
            ? node.getFormatType()
            : parent?.getFormatType() || "left",
      );
    }

    if ($isTableSelection(selection)) {
      updateToolbarState("isBold", selection.hasFormat("bold"));
      updateToolbarState("isItalic", selection.hasFormat("italic"));
      updateToolbarState("isUnderline", selection.hasFormat("underline"));
      updateToolbarState(
        "isStrikethrough",
        selection.hasFormat("strikethrough"),
      );
      updateToolbarState("isSubscript", selection.hasFormat("subscript"));
      updateToolbarState("isSuperscript", selection.hasFormat("superscript"));
      updateToolbarState("isHighlight", selection.hasFormat("highlight"));
      updateToolbarState("isCode", selection.hasFormat("code"));
      // Para blocos de código, lemos o font-size do CodeNode pai.
      const codeAncestor = $getEnclosingCodeNode();
      let codeFontSize: string | null = null;
      if (codeAncestor) {
        const raw = codeAncestor.getStyle() || "";
        for (const decl of raw.split(";")) {
          const idx = decl.indexOf(":");
          if (
            idx > 0 &&
            decl.slice(0, idx).trim().toLowerCase() === "font-size"
          ) {
            codeFontSize = decl.slice(idx + 1).trim();
            break;
          }
        }
      }
      updateToolbarState(
        "fontSize",
        codeAncestor
          ? (codeFontSize ?? `${DEFAULT_FONT_SIZE}px`)
          : $getSelectionStyleValueForProperty(
              selection,
              "font-size",
              `${DEFAULT_FONT_SIZE}px`,
            ),
      );
      updateToolbarState("isLowercase", selection.hasFormat("lowercase"));
      updateToolbarState("isUppercase", selection.hasFormat("uppercase"));
      updateToolbarState("isCapitalize", selection.hasFormat("capitalize"));
    }

    if ($isNodeSelection(selection)) {
      const nodes = selection.getNodes();
      for (const selectedNode of nodes) {
        if ($isEquationNode(selectedNode)) {
          updateToolbarState(
            "fontSize",
            `${Math.round(selectedNode.getFontSize() * DEFAULT_FONT_SIZE)}px`,
          );
        }
        const parentList = $getNearestNodeOfType<ListNode>(
          selectedNode,
          ListNode,
        );
        if (parentList) {
          updateToolbarState("blockType", parentList.getListType());
        } else {
          const selectedElement = $findTopLevelElement(selectedNode);
          $handleHeadingNode(selectedElement);
          if ($isElementNode(selectedElement)) {
            updateToolbarState(
              "elementFormat",
              selectedElement.getFormatType(),
            );
          }
        }
      }
    }
  }, [activeEditor, editor, updateToolbarState, $handleHeadingNode]);

  useEffect(() => {
    return editor.registerCommand(
      SELECTION_CHANGE_COMMAND,
      (_payload, newEditor) => {
        setActiveEditor(newEditor);
        $updateToolbar();
        return false;
      },
      COMMAND_PRIORITY_CRITICAL,
    );
  }, [editor, $updateToolbar, setActiveEditor]);

  useEffect(() => {
    activeEditor.getEditorState().read(
      () => {
        $updateToolbar();
      },
      { editor: activeEditor },
    );
  }, [activeEditor, $updateToolbar]);

  useEffect(() => {
    return mergeRegister(
      editor.registerEditableListener((editable) => setIsEditable(editable)),
      activeEditor.registerUpdateListener(({ editorState }) => {
        editorState.read(
          () => {
            $updateToolbar();
          },
          { editor: activeEditor },
        );
      }),
      activeEditor.registerCommand<boolean>(
        CAN_UNDO_COMMAND,
        (payload) => {
          updateToolbarState("canUndo", payload);
          return false;
        },
        COMMAND_PRIORITY_CRITICAL,
      ),
      activeEditor.registerCommand<boolean>(
        CAN_REDO_COMMAND,
        (payload) => {
          updateToolbarState("canRedo", payload);
          return false;
        },
        COMMAND_PRIORITY_CRITICAL,
      ),
    );
  }, [$updateToolbar, activeEditor, editor, updateToolbarState]);

  const applyStyleText = useCallback(
    (
      styles: Record<string, string>,
      skipHistoryStack?: boolean,
      skipRefocus: boolean = false,
    ) => {
      activeEditor.update(
        () => {
          if (skipRefocus) {
            $addUpdateTag(SKIP_DOM_SELECTION_TAG);
          }
          const selection = $getSelection();
          if (selection !== null) {
            $patchStyleText(selection, styles);
          }
        },
        skipHistoryStack ? { tag: HISTORIC_TAG } : {},
      );
    },
    [activeEditor],
  );

  const onFontColorSelect = useCallback(
    (value: string, skipHistoryStack: boolean, skipRefocus: boolean) => {
      applyStyleText({ color: value }, skipHistoryStack, skipRefocus);
    },
    [applyStyleText],
  );

  const onBgColorSelect = useCallback(
    (value: string, skipHistoryStack: boolean, skipRefocus: boolean) => {
      applyStyleText(
        { "background-color": value },
        skipHistoryStack,
        skipRefocus,
      );
    },
    [applyStyleText],
  );

  const insertLink = useCallback(() => {
    if (!toolbarState.isLink) {
      setIsLinkEditMode(true);
      activeEditor.dispatchCommand(TOGGLE_LINK_COMMAND, "https://");
    } else {
      setIsLinkEditMode(false);
      activeEditor.dispatchCommand(TOGGLE_LINK_COMMAND, null);
    }
  }, [activeEditor, setIsLinkEditMode, toolbarState.isLink]);

  return (
    <div
      className={cn(
        "flex flex-wrap items-center gap-0.5 p-1 rounded-t",
        "border-b border-gray-200 dark:border-gray-700",
        "bg-white dark:bg-gray-900 lexical-toolbar",
      )}
      role="toolbar"
      aria-label="Editor toolbar"
    >
      <ToolbarButton
        disabled={!toolbarState.canUndo || !isEditable}
        onClick={(e) =>
          dispatchToolbarCommand(UNDO_COMMAND, undefined, isKeyboardInput(e))
        }
        title={IS_APPLE ? "Undo (⌘Z)" : "Undo (Ctrl+Z)"}
        ariaLabel="Undo"
      >
        <UndoIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        disabled={!toolbarState.canRedo || !isEditable}
        onClick={(e) =>
          dispatchToolbarCommand(REDO_COMMAND, undefined, isKeyboardInput(e))
        }
        title={IS_APPLE ? "Redo (⇧⌘Z)" : "Redo (Ctrl+Y)"}
        ariaLabel="Redo"
      >
        <RedoIcon className="w-4 h-4" />
      </ToolbarButton>

      <Divider />

      {toolbarState.blockType in blockTypeToBlockName &&
        activeEditor === editor && (
          <>
            <BlockFormatDropDown
              disabled={!isEditable}
              blockType={toolbarState.blockType}
              editor={activeEditor}
            />
            <Divider />
          </>
        )}

      <FontDropDown
        disabled={!isEditable}
        style="font-family"
        value={toolbarState.fontFamily}
        editor={activeEditor}
      />
      <FontSizeStepper
        disabled={!isEditable}
        value={toolbarState.fontSize}
        editor={activeEditor}
      />

      <Divider />

      <CaseFormatDropDown
        editor={activeEditor}
        disabled={!isEditable}
        isLowercase={toolbarState.isLowercase}
        isUppercase={toolbarState.isUppercase}
        isCapitalize={toolbarState.isCapitalize}
        isStrikethrough={toolbarState.isStrikethrough}
        isSubscript={toolbarState.isSubscript}
        isSuperscript={toolbarState.isSuperscript}
        isHighlight={toolbarState.isHighlight}
      />

      <Divider />

      <ToolbarButton
        active={toolbarState.isBold}
        disabled={!isEditable}
        onClick={(e) => dispatchFormatTextCommand("bold", isKeyboardInput(e))}
        title="Bold"
        ariaLabel="Format Bold"
      >
        <BoldIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isItalic}
        disabled={!isEditable}
        onClick={(e) => dispatchFormatTextCommand("italic", isKeyboardInput(e))}
        title="Italic"
        ariaLabel="Format Italic"
      >
        <ItalicIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isUnderline}
        disabled={!isEditable}
        onClick={(e) =>
          dispatchFormatTextCommand("underline", isKeyboardInput(e))
        }
        title="Underline"
        ariaLabel="Format Underline"
      >
        <UnderlineIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isStrikethrough}
        disabled={!isEditable}
        onClick={(e) =>
          dispatchFormatTextCommand("strikethrough", isKeyboardInput(e))
        }
        title="Strikethrough"
        ariaLabel="Format Strikethrough"
      >
        <StrikethroughIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isSubscript}
        disabled={!isEditable}
        onClick={(e) =>
          dispatchFormatTextCommand("subscript", isKeyboardInput(e))
        }
        title="Subscript"
        ariaLabel="Format Subscript"
      >
        <SubscriptIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isSuperscript}
        disabled={!isEditable}
        onClick={(e) =>
          dispatchFormatTextCommand("superscript", isKeyboardInput(e))
        }
        title="Superscript"
        ariaLabel="Format Superscript"
      >
        <SuperscriptIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isHighlight}
        disabled={!isEditable}
        onClick={(e) =>
          dispatchFormatTextCommand("highlight", isKeyboardInput(e))
        }
        title="Highlight"
        ariaLabel="Format Highlight"
      >
        <HighlightIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isCode}
        disabled={!isEditable}
        onClick={(e) => dispatchFormatTextCommand("code", isKeyboardInput(e))}
        title="Inline Code"
        ariaLabel="Format Inline Code"
      >
        <CodeInlineIcon className="w-4 h-4" />
      </ToolbarButton>
      <ToolbarButton
        active={toolbarState.isLink}
        disabled={!isEditable}
        onClick={insertLink}
        title="Insert Link"
        ariaLabel="Insert Link"
      >
        <LinkIcon className="w-4 h-4" />
      </ToolbarButton>

      <Divider />

      <DropdownColorPicker
        disabled={!isEditable}
        buttonAriaLabel="Formatting text color"
        buttonIcon={<TextColorIcon className="w-4 h-4" />}
        color={toolbarState.fontColor}
        onChange={onFontColorSelect}
        title="Text color"
      />
      <DropdownColorPicker
        disabled={!isEditable}
        buttonAriaLabel="Formatting background color"
        buttonIcon={<BgColorIcon className="w-4 h-4" />}
        color={toolbarState.bgColor}
        onChange={onBgColorSelect}
        title="Background color"
      />
      <ToolbarButton
        disabled={!isEditable}
        onClick={(e) => clearFormatting(activeEditor, isKeyboardInput(e))}
        title="Clear formatting"
        ariaLabel="Clear all text formatting"
      >
        <ClearFormatIcon className="w-4 h-4" />
      </ToolbarButton>

      <Divider />

      {features.pageLayout && <PageSettingsDropDown disabled={!isEditable} />}

      {features.insertMenu && (
        <DropDown
          disabled={!isEditable}
          buttonLabel="Insert"
          buttonIcon={<InsertIcon className="w-4 h-4" />}
          buttonAriaLabel="Insert document feature"
        >
          {insertions.map((definition) => {
            const Icon = definition.Icon;
            return (
              <DropDownItem
                key={definition.id}
                onClick={() => {
                  if (definition.dialog) {
                    setInsertDialog(definition.dialog);
                    return;
                  }
                  activeEditor.update(() => {
                    $addUpdateTag(SKIP_SELECTION_FOCUS_TAG);
                    definition.execute?.(activeEditor);
                  });
                }}
              >
                <Icon className="w-4 h-4" /> {definition.label}
              </DropDownItem>
            );
          })}
        </DropDown>
      )}

      {features.emoji && (
        <EmojiPickerPopover editor={activeEditor} disabled={!isEditable} />
      )}

      {(features.insertMenu || features.emoji) && <Divider />}

      <ElementFormatDropdown
        disabled={!isEditable}
        value={toolbarState.elementFormat}
        editor={activeEditor}
        isRTL={toolbarState.isRTL}
      />

      {/* Área contextual: dropdowns dependentes do bloco selecionado.
          Ficam no final do toolbar (segunda linha quando não cabe na
          primeira) para não empurrar os controles principais. */}
      {toolbarState.blockType === "code" &&
        selectedElementKey &&
        activeEditor === editor && (
          <>
            <Divider />
            <CodeLanguageDropDown
              disabled={!isEditable}
              language={toolbarState.codeLanguage}
              editor={activeEditor}
              codeNodeKey={selectedElementKey}
            />
          </>
        )}

      <InsertionDialog
        definition={features.insertMenu ? insertDialog : null}
        activeEditor={activeEditor}
        onClose={() => setInsertDialog(null)}
      />
    </div>
  );
}
