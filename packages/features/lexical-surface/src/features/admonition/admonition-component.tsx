/**
 * AdmonitionLexicalComponent — Inline-editable renderer for AdmonitionLexicalNode.
 * Title and content editable directly. Settings via DropdownMenu (like StickyNode).
 */
"use client";

import * as React from "react";
import { useCallback, useEffect, useRef, useState } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { $getNodeByKey } from "lexical";
import { cn } from "@game-guild/ui/lib/utils";
import {
  Settings2,
  ChevronDown,
  Check,
  Trash2,
  Paintbrush,
  Notebook,
  FileText,
  Info,
  Flame,
  CheckCircle,
  HelpCircle,
  AlertTriangle,
  AlertCircle,
  Skull,
  Bug,
  List,
  Quote,
  Zap,
  ShieldAlert,
  Bell,
  Lightbulb,
  BookMarked,
} from "lucide-react";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu";
import { Admonition, type AdmonitionType } from "./admonition";
import type { AdmonitionDesign } from "./admonition";
import { $isAdmonitionLexicalNode, type AdmonitionLexicalNode } from "./admonition-node";
import ColorPicker from "../../shared/ui/color-picker";

interface AdmonitionLexicalComponentProps {
  admonitionType: AdmonitionType;
  title: string;
  content: string;
  design: AdmonitionDesign;
  customBorderColor: string;
  customTextColor: string;
  nodeKey: string;
}

const TYPE_LIST: {
  id: AdmonitionType;
  label: string;
  Icon: React.ComponentType<{ className?: string }>;
}[] = [
  { id: "note", label: "Note", Icon: Notebook },
  { id: "abstract", label: "Abstract", Icon: FileText },
  { id: "info", label: "Info", Icon: Info },
  { id: "tip", label: "Tip", Icon: Flame },
  { id: "success", label: "Success", Icon: CheckCircle },
  { id: "question", label: "Question", Icon: HelpCircle },
  { id: "warning", label: "Warning", Icon: AlertTriangle },
  { id: "failure", label: "Failure", Icon: AlertCircle },
  { id: "danger", label: "Danger", Icon: Skull },
  { id: "bug", label: "Bug", Icon: Bug },
  { id: "example", label: "Example", Icon: List },
  { id: "quote", label: "Quote", Icon: Quote },
  { id: "important", label: "Important", Icon: Zap },
  { id: "caution", label: "Caution", Icon: ShieldAlert },
  { id: "attention", label: "Attention", Icon: Bell },
  { id: "hint", label: "Hint", Icon: Lightbulb },
  { id: "check", label: "Check", Icon: Check },
  { id: "summary", label: "Summary", Icon: BookMarked },
];

const DESIGN_LIST: { id: AdmonitionDesign; label: string }[] = [
  { id: "default", label: "Default" },
  { id: "compact", label: "Compact" },
  { id: "bordered", label: "Bordered" },
  { id: "vertical-bar", label: "Vertical Bar" },
];

export function AdmonitionLexicalComponent({
  admonitionType,
  title,
  content,
  design,
  customBorderColor,
  customTextColor,
  nodeKey,
}: AdmonitionLexicalComponentProps) {
  const [editor] = useLexicalComposerContext();
  const [localTitle, setLocalTitle] = useState(title);
  const [localContent, setLocalContent] = useState(content);
  const [isFocused, setIsFocused] = useState(false);
  const [isEditable, setIsEditable] = useState(() => editor.isEditable());
  const titleRef = useRef<HTMLInputElement>(null);
  const contentRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    setLocalTitle(title);
  }, [title]);
  useEffect(() => {
    setLocalContent(content);
  }, [content]);

  useEffect(() => {
    return editor.registerEditableListener((editable) =>
      setIsEditable(editable),
    );
  }, [editor]);

  // Auto-focus title on creation
  useEffect(() => {
    if (content === "" && isEditable && titleRef.current) {
      const timer = setTimeout(() => {
        const rootElement = editor.getRootElement();
        if (rootElement) rootElement.blur();
        titleRef.current?.focus();
      }, 50);
      return () => clearTimeout(timer);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Autogrow textarea
  const adjustHeight = useCallback(() => {
    const textarea = contentRef.current;
    if (textarea) {
      textarea.style.height = "auto";
      textarea.style.height = `${textarea.scrollHeight}px`;
    }
  }, []);
  useEffect(() => {
    adjustHeight();
  }, [localContent, adjustHeight]);

  const stopLexicalPropagation = useCallback((e: React.SyntheticEvent) => {
    e.stopPropagation();
  }, []);

  const handleWrapperMouseDown = useCallback(
    (e: React.MouseEvent) => {
      if (!isEditable) return;
      const target = e.target as HTMLElement;
      if (target.tagName === "INPUT" || target.tagName === "TEXTAREA") return;
      if (
        target.closest("button") ||
        target.closest("[data-radix-popper-content-wrapper]")
      )
        return;
      const rootElement = editor.getRootElement();
      if (rootElement) rootElement.blur();
      requestAnimationFrame(() => {
        // Focus the content textarea by default, or title if content is already filled
        contentRef.current?.focus();
      });
    },
    [editor, isEditable],
  );

  const handleTitleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.stopPropagation();
    const val = e.target.value;
    setLocalTitle(val);
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if ($isAdmonitionLexicalNode(node)) node.setTitle(val);
    });
  };

  const handleContentChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    e.stopPropagation();
    const val = e.target.value;
    setLocalContent(val);
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if ($isAdmonitionLexicalNode(node)) node.setContent(val);
    });
  };

  const updateField = (updater: (node: AdmonitionLexicalNode) => void) => {
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if ($isAdmonitionLexicalNode(node)) updater(node);
    });
  };

  const handleDelete = () => {
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if (node) node.remove();
    });
  };

  const currentTypeEntry =
    TYPE_LIST.find((t) => t.id === admonitionType) ?? TYPE_LIST[0]!;

  return (
    <div
      className={cn(
        "group relative",
        isEditable &&
          (isFocused
            ? "ring-2 ring-blue-400/30 rounded-md"
            : "hover:ring-2 hover:ring-blue-400/20 hover:rounded-md"),
      )}
      onFocus={() => isEditable && setIsFocused(true)}
      onBlur={(e) => {
        if (!e.currentTarget.contains(e.relatedTarget)) setIsFocused(false);
      }}
      onMouseDown={handleWrapperMouseDown}
      onKeyDown={stopLexicalPropagation}
      onKeyUp={stopLexicalPropagation}
      onInput={stopLexicalPropagation}
      onBeforeInput={stopLexicalPropagation}
      onCopy={stopLexicalPropagation}
      onCut={stopLexicalPropagation}
      onPaste={stopLexicalPropagation}
      tabIndex={isEditable ? 0 : -1}
    >
      {/* ── Settings Menu (top-right) ── */}
      {isEditable && (
        <div className="absolute top-2 right-2 z-10 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <button
                  type="button"
                  aria-label="Admonition settings"
                  className={cn(
                    "inline-flex h-6 items-center justify-center gap-1 rounded px-1.5",
                    "border border-gray-300 dark:border-gray-700",
                    "bg-white/90 dark:bg-gray-800/90 text-gray-700 dark:text-gray-200",
                    "shadow-sm hover:bg-gray-100 dark:hover:bg-gray-700",
                  )}
                >
                  <Settings2 className="h-3.5 w-3.5" />
                  <ChevronDown className="h-3.5 w-3.5" />
                </button>
              }
            />
            <DropdownMenuContent align="end" className="w-56">
              {/* Type submenu */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  {React.createElement(currentTypeEntry.Icon, {
                    className: "w-4 h-4 mr-2",
                  })}
                  Type: {currentTypeEntry.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent className="max-h-[320px] overflow-y-auto">
                  {TYPE_LIST.map(({ id, label, Icon }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setAdmonitionType(id));
                      }}
                    >
                      <Icon className="w-4 h-4 mr-2" />
                      {label}
                      {admonitionType === id && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Design submenu */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <Paintbrush className="w-4 h-4 mr-2 text-gray-500" />
                  Design: {DESIGN_LIST.find((d) => d.id === design)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {DESIGN_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setDesign(id));
                      }}
                    >
                      {label}
                      {design === id && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Border color */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <span
                    className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                    style={{ backgroundColor: customBorderColor || "#3b82f6" }}
                  />
                  Border Color
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent className="p-3">
                  <ColorPicker
                    color={customBorderColor || "#3b82f6"}
                    onChange={(c) => {
                      if (typeof c === "string")
                        updateField((n) => n.setCustomBorderColor(c));
                    }}
                  />
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onSelect={(e) => {
                      e.preventDefault();
                      updateField((n) => n.setCustomBorderColor(""));
                    }}
                  >
                    Use default color
                  </DropdownMenuItem>
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Text color */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <span
                    className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                    style={{ backgroundColor: customTextColor || "#ffffff" }}
                  />
                  Text Color
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent className="p-3">
                  <ColorPicker
                    color={customTextColor || "#ffffff"}
                    onChange={(c) => {
                      if (typeof c === "string")
                        updateField((n) => n.setCustomTextColor(c));
                    }}
                  />
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onSelect={(e) => {
                      e.preventDefault();
                      updateField((n) => n.setCustomTextColor(""));
                    }}
                  >
                    Use default color
                  </DropdownMenuItem>
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              <DropdownMenuSeparator />
              <DropdownMenuItem
                onSelect={handleDelete}
                className="text-red-600 focus:text-red-600 focus:bg-red-50 dark:focus:bg-red-950/30"
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Delete admonition
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}

      {/* ── Admonition body ── */}
      {isEditable ? (
        <Admonition
          type={admonitionType}
          design={design}
          customBorderColor={customBorderColor || undefined}
          customTextColor={customTextColor || undefined}
          title={
            <input
              ref={titleRef}
              value={localTitle}
              onChange={handleTitleChange}
              placeholder={currentTypeEntry.label}
              className="bg-transparent border-none outline-none w-full font-semibold placeholder:opacity-50"
              style={customTextColor ? { color: customTextColor } : undefined}
            />
          }
          content={
            <textarea
              ref={contentRef}
              value={localContent}
              onChange={handleContentChange}
              placeholder="Type admonition content..."
              rows={1}
              className="w-full resize-none bg-transparent border-none outline-none text-sm leading-relaxed placeholder:opacity-40"
            />
          }
        />
      ) : (
        <Admonition
          type={admonitionType}
          design={design}
          customBorderColor={customBorderColor || undefined}
          customTextColor={customTextColor || undefined}
          title={localTitle || undefined}
          content={localContent || undefined}
        />
      )}
    </div>
  );
}
