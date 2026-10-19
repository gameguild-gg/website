/**
 * ButtonLexicalComponent — Inline-editable renderer for ButtonLexicalNode.
 * Text and URL editable directly. Settings via DropdownMenu.
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
  ExternalLink,
  Download,
  Copy,
  Mail,
  Link2,
  Eye,
  EyeOff,
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
import {
  BASE_BUTTON_STYLES,
  getSizeStyles,
  getVariantBaseStyles,
  getLayoutStyles,
  getIconSpacingClass,
  getIconSizeClass,
  getColorStyles,
  getFontFamilyClass,
  getFontSizeClass,
} from "./button-styles";
import type {
  ButtonVariant,
  ButtonSize,
  ButtonActionType,
  IconVariant,
  IconPosition,
  IconSize,
  ColorPalette,
  FontFamily,
  FontSize,
  ButtonCustomColors,
} from "./button-node";
import { $isButtonLexicalNode, type ButtonLexicalNode } from "./button-node";
import ColorPicker from "../../shared/ui/color-picker";

interface ButtonLexicalComponentProps {
  text: string;
  url: string;
  actionType: ButtonActionType;
  variant: ButtonVariant;
  btnSize: ButtonSize;
  showIcon: boolean;
  iconVariant: IconVariant;
  iconPosition: IconPosition;
  iconSize: IconSize;
  colorPalette: ColorPalette;
  customColors: ButtonCustomColors | null;
  fontFamily: FontFamily;
  fontSize: FontSize;
  nodeKey: string;
}

const ACTION_TYPES: {
  id: ButtonActionType;
  label: string;
  Icon: React.ComponentType<{ className?: string }>;
}[] = [
  { id: "url", label: "Open URL", Icon: ExternalLink },
  { id: "download", label: "Download", Icon: Download },
  { id: "copy", label: "Copy to Clipboard", Icon: Copy },
  { id: "email", label: "Send Email", Icon: Mail },
];

const VARIANT_LIST: { id: ButtonVariant; label: string }[] = [
  { id: "solid", label: "Solid" },
  { id: "outline", label: "Outline" },
  { id: "soft", label: "Soft" },
  { id: "minimal", label: "Minimal" },
];

const SIZE_LIST: { id: ButtonSize; label: string }[] = [
  { id: "sm", label: "Small" },
  { id: "md", label: "Medium" },
  { id: "lg", label: "Large" },
  { id: "xl", label: "Extra Large" },
  { id: "xxl", label: "2X Large" },
];

const COLOR_LIST: { id: ColorPalette; label: string; swatch: string }[] = [
  { id: "blue", label: "Blue", swatch: "#3b82f6" },
  { id: "green", label: "Green", swatch: "#22c55e" },
  { id: "orange", label: "Orange", swatch: "#f97316" },
  { id: "red", label: "Red", swatch: "#ef4444" },
];

const ICON_POS_LIST: { id: IconPosition; label: string }[] = [
  { id: "left", label: "Left" },
  { id: "right", label: "Right" },
  { id: "top", label: "Top" },
  { id: "bottom", label: "Bottom" },
];

export function ButtonLexicalComponent({
  text,
  url,
  actionType,
  variant,
  btnSize,
  showIcon,
  iconVariant,
  iconPosition,
  iconSize,
  colorPalette,
  customColors,
  fontFamily,
  fontSize,
  nodeKey,
}: ButtonLexicalComponentProps) {
  const [editor] = useLexicalComposerContext();
  const [localText, setLocalText] = useState(text);
  const [localUrl, setLocalUrl] = useState(url);
  const [isFocused, setIsFocused] = useState(false);
  const [isEditable, setIsEditable] = useState(() => editor.isEditable());
  const textRef = useRef<HTMLInputElement>(null);
  const urlRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    setLocalText(text);
  }, [text]);
  useEffect(() => {
    setLocalUrl(url);
  }, [url]);

  useEffect(() => {
    return editor.registerEditableListener((editable) =>
      setIsEditable(editable),
    );
  }, [editor]);

  // Auto-focus on creation
  useEffect(() => {
    if (text === "Click me" && isEditable && textRef.current) {
      const timer = setTimeout(() => {
        const rootElement = editor.getRootElement();
        if (rootElement) rootElement.blur();
        textRef.current?.focus();
        textRef.current?.select();
      }, 50);
      return () => clearTimeout(timer);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const stopLexicalPropagation = useCallback((e: React.SyntheticEvent) => {
    e.stopPropagation();
  }, []);

  const handleWrapperMouseDown = useCallback(
    (e: React.MouseEvent) => {
      if (!isEditable) return;
      const target = e.target as HTMLElement;
      if (target.tagName === "INPUT" || target.tagName === "TEXTAREA") return;
      if (
        target.closest("button[aria-label]") ||
        target.closest("[data-radix-popper-content-wrapper]")
      )
        return;
      const rootElement = editor.getRootElement();
      if (rootElement) rootElement.blur();
      requestAnimationFrame(() => {
        textRef.current?.focus();
      });
    },
    [editor, isEditable],
  );

  const updateField = useCallback(
    (updater: (node: ButtonLexicalNode) => void) => {
      editor.update(() => {
        const node = $getNodeByKey(nodeKey);
        if ($isButtonLexicalNode(node)) updater(node);
      });
    },
    [editor, nodeKey],
  );

  const handleTextChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.stopPropagation();
    const val = e.target.value;
    setLocalText(val);
    updateField((n) => n.setText(val));
  };

  const handleUrlChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    e.stopPropagation();
    const val = e.target.value;
    setLocalUrl(val);
    updateField((n) => n.setUrl(val));
  };

  const handleDelete = () => {
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if (node) node.remove();
    });
  };

  // Build button styles
  const isVerticalIcon =
    showIcon && (iconPosition === "top" || iconPosition === "bottom");
  const palette = colorPalette || "blue";
  const font = fontFamily || "sans";
  const textSize = fontSize || "md";
  const buttonClasses = `${BASE_BUTTON_STYLES} ${getSizeStyles(btnSize, isVerticalIcon)} ${getVariantBaseStyles(variant, btnSize)} ${getColorStyles(palette, variant)} ${getLayoutStyles(iconPosition || "right")} ${getFontFamilyClass(font)} ${getFontSizeClass(btnSize, textSize)}`;

  const getCustomStyle = () => {
    if (colorPalette === "custom" && customColors) {
      const {
        primary,
        secondary,
        text: txt,
        hoverPrimary,
        hoverSecondary,
        hoverText,
      } = customColors;
      if (variant === "solid") {
        return {
          background: `linear-gradient(to right, ${primary}, ${secondary})`,
          color: txt,
          "--hover-bg": `linear-gradient(to right, ${hoverPrimary}, ${hoverSecondary})`,
          "--hover-text": hoverText,
        } as React.CSSProperties;
      } else if (variant === "outline") {
        return {
          borderColor: primary,
          color: txt,
          "--hover-bg": hoverPrimary,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties;
      } else if (variant === "soft") {
        return {
          backgroundColor: `${primary}20`,
          color: txt,
          "--hover-bg": `${hoverPrimary}30`,
          "--hover-text": hoverText,
        } as React.CSSProperties;
      } else if (variant === "minimal") {
        return {
          color: txt,
          "--hover-border": hoverPrimary,
          "--hover-text": hoverText,
        } as React.CSSProperties;
      }
    }
    return {};
  };

  const getActionIcon = () => {
    const cls = getIconSizeClass(btnSize, iconSize || "md");
    const icons: Record<ButtonActionType, React.ReactNode[]> = {
      url: [
        <ExternalLink className={cls} key="0" />,
        <Link2 className={cls} key="1" />,
      ],
      download: [<Download className={cls} key="0" />],
      copy: [<Copy className={cls} key="0" />],
      email: [<Mail className={cls} key="0" />],
    };
    return icons[actionType]?.[iconVariant || 0] ?? icons[actionType]?.[0];
  };

  const iconSpacing = getIconSpacingClass(iconPosition || "right");
  const currentAction =
    ACTION_TYPES.find((a) => a.id === actionType) ?? ACTION_TYPES[0]!;

  return (
    <div
      className={cn(
        "group relative my-4",
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
      <style>{`
        .custom-button-hover:hover {
          background: var(--hover-bg) !important;
          color: var(--hover-text) !important;
          border-color: var(--hover-border) !important;
        }
      `}</style>

      {/* ── Settings Menu ── */}
      {isEditable && (
        <div className="absolute top-1 right-1 z-10 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <button
                  type="button"
                  aria-label="Button settings"
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
            <DropdownMenuContent align="end" className="w-52">
              {/* Action Type */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  {React.createElement(currentAction.Icon, {
                    className: "w-4 h-4 mr-2",
                  })}
                  Action: {currentAction.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {ACTION_TYPES.map(({ id, label, Icon }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setActionType(id));
                      }}
                    >
                      <Icon className="w-4 h-4 mr-2" /> {label}
                      {actionType === id && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Variant */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  Variant: {VARIANT_LIST.find((v) => v.id === variant)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {VARIANT_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setVariant(id));
                      }}
                    >
                      {label}
                      {variant === id && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Size */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  Size: {SIZE_LIST.find((s) => s.id === btnSize)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {SIZE_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setBtnSize(id));
                      }}
                    >
                      {label}
                      {btnSize === id && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Color */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <span
                    className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                    style={{
                      backgroundColor:
                        colorPalette === "custom"
                          ? (customColors?.primary ?? "#3b82f6")
                          : (COLOR_LIST.find((c) => c.id === colorPalette)
                              ?.swatch ?? "#3b82f6"),
                    }}
                  />
                  Color:{" "}
                  {colorPalette === "custom"
                    ? "Custom"
                    : (COLOR_LIST.find((c) => c.id === colorPalette)?.label ??
                      "Blue")}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent className="w-56">
                  {COLOR_LIST.map(({ id, label, swatch }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setColorPalette(id));
                      }}
                    >
                      <span
                        className="w-4 h-4 mr-2 rounded-full"
                        style={{ backgroundColor: swatch }}
                      />
                      {label}
                      {colorPalette === id && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>
                  ))}

                  <DropdownMenuSeparator />

                  {/* Custom Primary */}
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <span
                        className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                        style={{
                          backgroundColor: customColors?.primary ?? "#3b82f6",
                        }}
                      />
                      Custom Primary
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="p-3">
                      <ColorPicker
                        color={customColors?.primary ?? "#3b82f6"}
                        onChange={(c) => {
                          if (typeof c === "string") {
                            updateField((n) => {
                              n.setColorPalette("custom");
                              n.setCustomColors({
                                ...(n.getCustomColors() || {
                                  primary: "#3b82f6",
                                  secondary: "#8b5cf6",
                                  text: "#ffffff",
                                  hoverPrimary: "#1d4ed8",
                                  hoverSecondary: "#7c3aed",
                                  hoverText: "#ffffff",
                                }),
                                primary: c,
                              });
                            });
                          }
                        }}
                      />
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>

                  {/* Custom Secondary */}
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <span
                        className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                        style={{
                          backgroundColor: customColors?.secondary ?? "#8b5cf6",
                        }}
                      />
                      Custom Secondary
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="p-3">
                      <ColorPicker
                        color={customColors?.secondary ?? "#8b5cf6"}
                        onChange={(c) => {
                          if (typeof c === "string") {
                            updateField((n) => {
                              n.setColorPalette("custom");
                              n.setCustomColors({
                                ...(n.getCustomColors() || {
                                  primary: "#3b82f6",
                                  secondary: "#8b5cf6",
                                  text: "#ffffff",
                                  hoverPrimary: "#1d4ed8",
                                  hoverSecondary: "#7c3aed",
                                  hoverText: "#ffffff",
                                }),
                                secondary: c,
                              });
                            });
                          }
                        }}
                      />
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>

                  {/* Custom Text */}
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <span
                        className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                        style={{
                          backgroundColor: customColors?.text ?? "#ffffff",
                        }}
                      />
                      Custom Text
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="p-3">
                      <ColorPicker
                        color={customColors?.text ?? "#ffffff"}
                        onChange={(c) => {
                          if (typeof c === "string") {
                            updateField((n) => {
                              n.setColorPalette("custom");
                              n.setCustomColors({
                                ...(n.getCustomColors() || {
                                  primary: "#3b82f6",
                                  secondary: "#8b5cf6",
                                  text: "#ffffff",
                                  hoverPrimary: "#1d4ed8",
                                  hoverSecondary: "#7c3aed",
                                  hoverText: "#ffffff",
                                }),
                                text: c,
                              });
                            });
                          }
                        }}
                      />
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>

                  {/* Custom Hover Sub-Group */}
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      Custom Hover Colors
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="w-56">
                      <DropdownMenuSub>
                        <DropdownMenuSubTrigger>
                          <span
                            className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                            style={{
                              backgroundColor:
                                customColors?.hoverPrimary ?? "#1d4ed8",
                            }}
                          />
                          Hover Primary
                        </DropdownMenuSubTrigger>
                        <DropdownMenuSubContent className="p-3">
                          <ColorPicker
                            color={customColors?.hoverPrimary ?? "#1d4ed8"}
                            onChange={(c) => {
                              if (typeof c === "string") {
                                updateField((n) => {
                                  n.setColorPalette("custom");
                                  n.setCustomColors({
                                    ...(n.getCustomColors() || {
                                      primary: "#3b82f6",
                                      secondary: "#8b5cf6",
                                      text: "#ffffff",
                                      hoverPrimary: "#1d4ed8",
                                      hoverSecondary: "#7c3aed",
                                      hoverText: "#ffffff",
                                    }),
                                    hoverPrimary: c,
                                  });
                                });
                              }
                            }}
                          />
                        </DropdownMenuSubContent>
                      </DropdownMenuSub>

                      <DropdownMenuSub>
                        <DropdownMenuSubTrigger>
                          <span
                            className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                            style={{
                              backgroundColor:
                                customColors?.hoverSecondary ?? "#7c3aed",
                            }}
                          />
                          Hover Secondary
                        </DropdownMenuSubTrigger>
                        <DropdownMenuSubContent className="p-3">
                          <ColorPicker
                            color={customColors?.hoverSecondary ?? "#7c3aed"}
                            onChange={(c) => {
                              if (typeof c === "string") {
                                updateField((n) => {
                                  n.setColorPalette("custom");
                                  n.setCustomColors({
                                    ...(n.getCustomColors() || {
                                      primary: "#3b82f6",
                                      secondary: "#8b5cf6",
                                      text: "#ffffff",
                                      hoverPrimary: "#1d4ed8",
                                      hoverSecondary: "#7c3aed",
                                      hoverText: "#ffffff",
                                    }),
                                    hoverSecondary: c,
                                  });
                                });
                              }
                            }}
                          />
                        </DropdownMenuSubContent>
                      </DropdownMenuSub>

                      <DropdownMenuSub>
                        <DropdownMenuSubTrigger>
                          <span
                            className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                            style={{
                              backgroundColor:
                                customColors?.hoverText ?? "#ffffff",
                            }}
                          />
                          Hover Text
                        </DropdownMenuSubTrigger>
                        <DropdownMenuSubContent className="p-3">
                          <ColorPicker
                            color={customColors?.hoverText ?? "#ffffff"}
                            onChange={(c) => {
                              if (typeof c === "string") {
                                updateField((n) => {
                                  n.setColorPalette("custom");
                                  n.setCustomColors({
                                    ...(n.getCustomColors() || {
                                      primary: "#3b82f6",
                                      secondary: "#8b5cf6",
                                      text: "#ffffff",
                                      hoverPrimary: "#1d4ed8",
                                      hoverSecondary: "#7c3aed",
                                      hoverText: "#ffffff",
                                    }),
                                    hoverText: c,
                                  });
                                });
                              }
                            }}
                          />
                        </DropdownMenuSubContent>
                      </DropdownMenuSub>
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              <DropdownMenuSeparator />

              {/* Show icon toggle */}
              <DropdownMenuItem
                onSelect={(e) => {
                  e.preventDefault();
                  updateField((n) => n.setShowIcon(!showIcon));
                }}
              >
                {showIcon ? (
                  <Eye className="w-4 h-4 mr-2" />
                ) : (
                  <EyeOff className="w-4 h-4 mr-2" />
                )}
                {showIcon ? "Hide Icon" : "Show Icon"}
              </DropdownMenuItem>

              {/* Icon position (only if icon visible) */}
              {showIcon && (
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    Icon Position:{" "}
                    {ICON_POS_LIST.find((p) => p.id === iconPosition)?.label}
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent>
                    {ICON_POS_LIST.map(({ id, label }) => (
                      <DropdownMenuItem
                        key={id}
                        onSelect={(e) => {
                          e.preventDefault();
                          updateField((n) => n.setIconPosition(id));
                        }}
                      >
                        {label}
                        {iconPosition === id && (
                          <Check className="ml-auto w-4 h-4" />
                        )}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
              )}

              {/* Icon variant cycling */}
              {showIcon && (
                <DropdownMenuItem
                  onSelect={(e) => {
                    e.preventDefault();
                    updateField((n) =>
                      n.setIconVariant(((iconVariant + 1) % 3) as 0 | 1 | 2),
                    );
                  }}
                >
                  Next Icon Style ({iconVariant + 1}/3)
                </DropdownMenuItem>
              )}

              <DropdownMenuSeparator />
              <DropdownMenuItem
                onSelect={handleDelete}
                className="text-red-600 focus:text-red-600 focus:bg-red-50 dark:focus:bg-red-950/30"
              >
                <Trash2 className="mr-2 h-4 w-4" />
                Delete button
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}

      {/* ── Button Visual ── */}
      <div className="flex flex-col items-center gap-2">
        <div
          className={cn(
            buttonClasses,
            colorPalette === "custom" && "custom-button-hover",
          )}
          style={{
            pointerEvents: isEditable ? "none" : "auto",
            ...getCustomStyle(),
          }}
        >
          {isEditable ? (
            <input
              ref={textRef}
              value={localText}
              onChange={handleTextChange}
              className="bg-transparent border-none outline-none text-center font-inherit text-inherit w-full min-w-[60px]"
              style={{ pointerEvents: "auto" }}
              placeholder="Button text"
            />
          ) : (
            <span>{localText}</span>
          )}
          {showIcon && <span className={iconSpacing}>{getActionIcon()}</span>}
        </div>

        {/* URL input — shown when focused in edit mode */}
        {isEditable && isFocused && (
          <div className="flex items-center gap-2 w-full max-w-md">
            <Link2 className="w-4 h-4 text-gray-400 shrink-0" />
            <input
              ref={urlRef}
              value={localUrl}
              onChange={handleUrlChange}
              placeholder={
                actionType === "email"
                  ? "email@example.com"
                  : "https://example.com"
              }
              className="flex-1 text-sm bg-gray-100 dark:bg-gray-800 border border-gray-200 dark:border-gray-700 rounded px-2 py-1 outline-none focus:border-blue-400 text-gray-700 dark:text-gray-200"
            />
          </div>
        )}
      </div>
    </div>
  );
}
