"use client";

import * as React from "react";
import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { useLexicalEditable } from "@lexical/react/useLexicalEditable";
import { useLexicalNodeSelection } from "@lexical/react/useLexicalNodeSelection";
import { mergeRegister } from "@lexical/utils";
import {
  $getNodeByKey,
  CLICK_COMMAND,
  COMMAND_PRIORITY_LOW,
  isDOMNode,
} from "lexical";
import { Pencil, Trash2 } from "lucide-react";
import { cn } from "@game-guild/ui/lib/utils";
import { DeleteConfirmDialog } from "../../../shared/ui/dialogs/delete-confirm-dialog";
import { useNodeDeleteProtection } from "../../../shared/lexical/node-delete-protection";
import { $isVegaLiteLexicalNode } from "./vega-lite-node";
import type { VegaLiteData } from "../vega-lite-data";
import { VegaLiteEditor } from "../editor/vega-lite-editor";
import { VegaLiteViewer } from "../rendering/vega-lite-viewer";
import { getThemePair } from "../theme/vega-theme-helper";

interface VegaLiteLexicalComponentProps {
  spec: string;
  title: string;
  caption: string;
  size: number;
  theme: NonNullable<VegaLiteData["theme"]>;
  themeMode: NonNullable<VegaLiteData["themeMode"]>;
  layout: NonNullable<VegaLiteData["layout"]>;
  attachments: NonNullable<VegaLiteData["attachments"]>;
  nodeKey: string;
}

export function VegaLiteLexicalComponent({
  spec,
  title,
  caption,
  size,
  theme,
  themeMode,
  layout,
  attachments,
  nodeKey,
}: VegaLiteLexicalComponentProps): React.JSX.Element {
  const [editor] = useLexicalComposerContext();
  const isEditable = useLexicalEditable();
  const [isModalOpen, setModalOpen] = useState(false);
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [isSelected, setSelected, clearSelection] =
    useLexicalNodeSelection(nodeKey);
  const themePair = getThemePair(theme, themeMode);

  // Protect against accidental Backspace/Delete keypresses
  useNodeDeleteProtection({
    nodeKey,
    enabled: isEditable,
    onRequestDelete: () => setConfirmDeleteOpen(true),
  });

  // Select node on click, open modal on double click
  useEffect(() => {
    if (!isEditable) {
      if (isSelected) clearSelection();
      return;
    }
    return mergeRegister(
      editor.registerCommand(
        CLICK_COMMAND,
        (event: MouseEvent) => {
          const containerElem = containerRef.current;
          const eventTarget = event.target;
          if (
            containerElem !== null &&
            isDOMNode(eventTarget) &&
            containerElem.contains(eventTarget)
          ) {
            const targetEl = eventTarget as Element;
            // Check if the click target is an interactive control (zoom buttons, fullscreen, range sliders etc.)
            if (
              targetEl.closest("button") ||
              targetEl.closest("input") ||
              targetEl.closest(".fixed") ||
              (targetEl.closest("[role='dialog']") ??
                targetEl.closest(".z-50")) ||
              targetEl.closest(".z-60")
            ) {
              // Let the viewer controls handle the event, do not intercept or select
              return false;
            }

            if (!event.shiftKey) clearSelection();
            setSelected(!isSelected);
            if (event.detail > 1) setModalOpen(true);
            return true;
          }
          return false;
        },
        COMMAND_PRIORITY_LOW,
      ),
    );
  }, [clearSelection, editor, isSelected, setSelected, isEditable]);

  const deleteNode = useCallback(() => {
    setModalOpen(false);
    setConfirmDeleteOpen(false);
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if (node) node.remove();
    });
  }, [editor, nodeKey]);

  const setVegaData = (newData: VegaLiteData) => {
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if ($isVegaLiteLexicalNode(node)) {
        node.setSpec(newData.spec);
        node.setTitle(newData.title || "");
        node.setCaption(newData.caption || "");
        node.setSize(newData.size ?? 100);
        node.setTheme(newData.theme || "default");
        node.setThemeMode(newData.themeMode || "system");
        node.setLayout(newData.layout || "rectangular");
        node.setAttachments(newData.attachments || {});
      }
    });
  };

  // Combine data for editing/rendering
  const vegaLiteData = useMemo<VegaLiteData>(
    () => ({
      spec,
      title,
      caption,
      size,
      theme,
      themeMode,
      layout,
      attachments,
    }),
    [spec, title, caption, size, theme, themeMode, layout, attachments],
  );

  return (
    <>
      {isEditable && isModalOpen && (
        <VegaLiteEditor
          initialData={vegaLiteData}
          onSave={(updatedData) => {
            setVegaData(updatedData);
            setModalOpen(false);
          }}
          onCancel={() => setModalOpen(false)}
        />
      )}

      <div className="relative my-4 flex justify-center w-full">
        {isSelected && isEditable && (
          <div
            className="absolute -top-10 left-1/2 -translate-x-1/2 z-30 flex items-center gap-1 rounded-md border border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900 shadow-md px-2 py-1 text-xs whitespace-nowrap"
            onMouseDown={(e) => e.stopPropagation()}
            onClick={(e) => e.stopPropagation()}
          >
            <button
              type="button"
              onClick={() => setModalOpen(true)}
              className="h-6 px-2 inline-flex items-center gap-1 rounded text-xs hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300 font-medium"
              aria-label="Edit chart"
              title="Edit chart"
            >
              <Pencil className="w-3.5 h-3.5 text-blue-500" /> Edit
            </button>
            <span className="mx-1 h-4 w-px bg-gray-300 dark:bg-gray-700" />
            <button
              type="button"
              onClick={() => setConfirmDeleteOpen(true)}
              className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-red-50 dark:hover:bg-red-950 text-red-600"
              aria-label="Delete chart"
              title="Delete chart"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        <div
          ref={containerRef}
          className={cn(
            "block border-2 rounded-lg text-left overflow-hidden bg-white dark:bg-gray-950 transition-all",
            isSelected
              ? "border-blue-500 ring-2 ring-blue-500/10"
              : "border-gray-200 dark:border-gray-800 hover:border-gray-300 dark:hover:border-gray-700",
          )}
          style={{ width: `${size}%` }}
        >
          <div className="p-4">
            <VegaLiteViewer
              spec={spec}
              layout={layout}
              themeLight={themePair.themeLight}
              themeDark={themePair.themeDark}
              title={title}
              caption={caption}
              size={100}
              showControls={true}
              allowFullscreen={true}
              attachments={attachments}
            />
          </div>
        </div>
      </div>

      <DeleteConfirmDialog
        open={confirmDeleteOpen}
        onOpenChange={setConfirmDeleteOpen}
        title="Remove chart?"
        itemName="this chart"
        itemType="chart"
        onConfirm={deleteNode}
        confirmText="Remove"
      />
    </>
  );
}
