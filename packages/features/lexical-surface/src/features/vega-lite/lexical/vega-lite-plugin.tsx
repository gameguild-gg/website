"use client";

import { useEffect, useState } from "react";
import { useLexicalComposerContext } from "@lexical/react/LexicalComposerContext";
import { $wrapNodeInElement } from "@lexical/utils";
import {
  $createParagraphNode,
  $insertNodes,
  $isRootOrShadowRoot,
  COMMAND_PRIORITY_EDITOR,
  createCommand,
  type LexicalCommand,
} from "lexical";
import { VegaLiteEditor } from "../editor/vega-lite-editor";
import {
  $createVegaLiteLexicalNode,
  VegaLiteLexicalNode,
} from "./vega-lite-node";
import type { VegaLiteData } from "../vega-lite-data";

export const INSERT_VEGA_LITE_LEXICAL_COMMAND: LexicalCommand<void> =
  createCommand("INSERT_VEGA_LITE_LEXICAL_COMMAND");

export function VegaLitePlugin() {
  const [editor] = useLexicalComposerContext();
  const [isModalOpen, setModalOpen] = useState(false);
  useEffect(() => {
    if (!editor.hasNodes([VegaLiteLexicalNode])) {
      throw new Error(
        "VegaLitePlugin: VegaLiteLexicalNode not registered on editor",
      );
    }
    return editor.registerCommand<void>(
      INSERT_VEGA_LITE_LEXICAL_COMMAND,
      () => {
        setModalOpen(true);
        return true;
      },
      COMMAND_PRIORITY_EDITOR,
    );
  }, [editor]);

  const handleSave = (data: VegaLiteData) => {
    editor.update(() => {
      const node = $createVegaLiteLexicalNode(data.spec);
      node.setTitle(data.title || "");
      node.setCaption(data.caption || "");
      node.setSize(data.size ?? 100);
      node.setTheme(data.theme || "default");
      node.setThemeMode(data.themeMode ?? "system");
      node.setLayout(data.layout || "rectangular");
      node.setAttachments(data.attachments || {});

      $insertNodes([node]);
      if ($isRootOrShadowRoot(node.getParentOrThrow())) {
        $wrapNodeInElement(node, $createParagraphNode).selectEnd();
      }
    });
    setModalOpen(false);
  };

  const handleCancel = () => {
    setModalOpen(false);
  };

  return isModalOpen ? (
    <VegaLiteEditor onSave={handleSave} onCancel={handleCancel} />
  ) : null;
}
