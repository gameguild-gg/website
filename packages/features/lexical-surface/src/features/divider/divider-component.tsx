"use client";

import * as React from "react";
import { useCallback, useEffect, useState } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { $getNodeByKey } from "lexical";
import { cn } from "@game-guild/ui/lib/utils";
import {
  Settings2,
  Check,
  Trash2,
  Paintbrush,
  ArrowDownUp,
  MoveHorizontal,
  ChevronDown,
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
  getThicknessStyles,
  getSpacingStyles,
  getColorStyles,
  getStyleClasses,
  getPaletteColor,
} from "./divider-styles";
import type {
  DividerStyle,
  DividerThickness,
  DividerSpacing,
  DividerColorPalette,
} from "./divider-node";
import { $isDividerLexicalNode } from "./divider-node";
import ColorPicker from "../../shared/ui/color-picker";

interface DividerLexicalComponentProps {
  style: DividerStyle;
  thickness: DividerThickness;
  spacing: DividerSpacing;
  colorPalette: DividerColorPalette;
  customColor: string | null;
  nodeKey: string;
}

const STYLE_LIST: { id: DividerStyle; label: string }[] = [
  { id: "simple", label: "Simple" },
  { id: "double", label: "Double" },
  { id: "dashed", label: "Dashed" },
  { id: "dotted", label: "Dotted" },
  { id: "gradient", label: "Gradient" },
];

const THICKNESS_LIST: { id: DividerThickness; label: string }[] = [
  { id: "thin", label: "Thin" },
  { id: "medium", label: "Medium" },
  { id: "thick", label: "Thick" },
];

const SPACING_LIST: { id: DividerSpacing; label: string }[] = [
  { id: "xs", label: "Extra Small" },
  { id: "sm", label: "Small" },
  { id: "md", label: "Medium" },
  { id: "lg", label: "Large" },
  { id: "xl", label: "Extra Large" },
];

const COLOR_LIST: { id: DividerColorPalette; label: string; swatch: string }[] =
  [
    { id: "blue", label: "Blue", swatch: "#3b82f6" },
    { id: "green", label: "Green", swatch: "#22c55e" },
    { id: "orange", label: "Orange", swatch: "#f97316" },
    { id: "red", label: "Red", swatch: "#ef4444" },
    { id: "purple", label: "Purple", swatch: "#a855f7" },
  ];

export function DividerLexicalComponent({
  style,
  thickness,
  spacing,
  colorPalette,
  customColor,
  nodeKey,
}: DividerLexicalComponentProps) {
  const [editor] = useLexicalComposerContext();
  const [isFocused, setIsFocused] = useState(false);
  const [isEditable, setIsEditable] = useState(() => editor.isEditable());

  useEffect(() => {
    return editor.registerEditableListener((editable) =>
      setIsEditable(editable),
    );
  }, [editor]);

  const stopLexicalPropagation = useCallback((e: React.SyntheticEvent) => {
    e.stopPropagation();
  }, []);

  const handleWrapperMouseDown = useCallback(
    (e: React.MouseEvent) => {
      if (!isEditable) return;
      const target = e.target as HTMLElement;
      if (
        target.closest("button[aria-label]") ||
        target.closest("[data-radix-popper-content-wrapper]")
      )
        return;
      const rootElement = editor.getRootElement();
      if (rootElement) rootElement.blur();
      // By clicking the divider, we just focus its container
    },
    [editor, isEditable],
  );

  const updateField = useCallback(
    (updater: (node: any) => void) => {
      editor.update(() => {
        const node = $getNodeByKey(nodeKey);
        if ($isDividerLexicalNode(node)) updater(node);
      });
    },
    [editor, nodeKey],
  );

  const handleDelete = () => {
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if (node) node.remove();
    });
  };

  const spacingClass = getSpacingStyles(spacing);
  const thicknessClass = getThicknessStyles(thickness, style);
  const colorClass = getColorStyles(colorPalette, style);
  const styleClass = getStyleClasses(style);
  const paletteColor = getPaletteColor(colorPalette, customColor || undefined);

  const customCSSStyle =
    colorPalette === "custom" && customColor
      ? {
          borderColor: customColor,
          backgroundColor: customColor,
        }
      : {};

  const renderDivider = () => {
    switch (style) {
      case "gradient":
        return (
          <div
            className={`${spacingClass} ${thicknessClass} ${colorClass}`}
            style={customCSSStyle}
            aria-hidden="true"
          />
        );
      case "double": {
        const doubleThickness =
          thickness === "thin" ? "1px" : thickness === "medium" ? "2px" : "3px";
        const doubleGap =
          thickness === "thin" ? "2px" : thickness === "medium" ? "3px" : "4px";
        return (
          <div className={spacingClass}>
            <div
              className="relative w-full"
              style={{ height: `calc(${doubleThickness} * 2 + ${doubleGap})` }}
            >
              <div
                className="absolute top-0 left-0 right-0"
                style={{
                  height: doubleThickness,
                  backgroundColor: paletteColor,
                }}
              />
              <div
                className="absolute bottom-0 left-0 right-0"
                style={{
                  height: doubleThickness,
                  backgroundColor: paletteColor,
                }}
              />
            </div>
          </div>
        );
      }
      default:
        return (
          <hr
            className={`${spacingClass} ${thicknessClass} ${colorClass} ${styleClass} w-full`}
            style={customCSSStyle}
          />
        );
    }
  };

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
      tabIndex={isEditable ? 0 : -1}
    >
      {/* ── Settings Menu ── */}
      {isEditable && (
        <div className="absolute -top-4 right-2 z-10 opacity-0 group-hover:opacity-100 group-focus-within:opacity-100 transition-opacity">
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <button
                  type="button"
                  aria-label="Divider settings"
                  className="flex items-center gap-1.5 px-2 py-1 text-xs font-medium text-gray-600 bg-white border border-gray-200 rounded-md shadow-sm hover:bg-gray-50 focus:outline-none dark:bg-gray-800 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-700"
                >
                  <Settings2 className="w-3.5 h-3.5" />
                  <ChevronDown className="h-3.5 w-3.5" />
                </button>
              }
            />
            <DropdownMenuContent align="end" className="w-56">
              {/* Style */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <Paintbrush className="w-4 h-4 mr-2 text-gray-500" />
                  Style: {STYLE_LIST.find((s) => s.id === style)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {STYLE_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setStyle(id));
                      }}
                    >
                      {label}
                      {style === id && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Thickness */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <ArrowDownUp className="w-4 h-4 mr-2 text-gray-500" />
                  Thickness:{" "}
                  {THICKNESS_LIST.find((t) => t.id === thickness)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {THICKNESS_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setThickness(id));
                      }}
                    >
                      {label}
                      {thickness === id && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              {/* Spacing */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <MoveHorizontal className="w-4 h-4 mr-2 text-gray-500" />
                  Spacing: {SPACING_LIST.find((s) => s.id === spacing)?.label}
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  {SPACING_LIST.map(({ id, label }) => (
                    <DropdownMenuItem
                      key={id}
                      onSelect={(e) => {
                        e.preventDefault();
                        updateField((n) => n.setSpacing(id));
                      }}
                    >
                      {label}
                      {spacing === id && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                  ))}
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              <DropdownMenuSeparator />

              {/* Color */}
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <span
                    className="w-4 h-4 mr-2 rounded-full border border-gray-300 dark:border-gray-600"
                    style={{
                      backgroundColor:
                        colorPalette === "custom"
                          ? (customColor ?? "#3b82f6")
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
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <span
                        className="w-4 h-4 mr-2 rounded-full border"
                        style={{ backgroundColor: customColor || "#3b82f6" }}
                      />
                      Custom Color
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="p-3">
                      <ColorPicker
                        color={customColor || "#3b82f6"}
                        onChange={(c) => {
                          if (typeof c === "string") {
                            updateField((n) => {
                              n.setColorPalette("custom");
                              n.setCustomColor(c);
                            });
                          }
                        }}
                      />
                      <DropdownMenuSeparator />
                      <DropdownMenuItem
                        onSelect={(e) => {
                          e.preventDefault();
                          updateField((n) => n.setCustomColor(null));
                        }}
                      >
                        Clear custom color
                      </DropdownMenuItem>
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>
                </DropdownMenuSubContent>
              </DropdownMenuSub>

              <DropdownMenuSeparator />

              <DropdownMenuItem
                onSelect={handleDelete}
                className="text-red-600 dark:text-red-400 focus:text-red-700 dark:focus:text-red-300"
              >
                <Trash2 className="w-4 h-4 mr-2" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      )}

      {/* ── Divider Content ── */}
      <div className="w-full relative pointer-events-none">
        {renderDivider()}
      </div>
    </div>
  );
}
