/**
 * Public Lexical rich-document surface.
 *
 * The surface owns composer configuration and provider wiring. Editable layout
 * and plugin composition live in focused internal modules.
 */
"use client";

import * as React from "react";
import { useMemo } from "react";
import type { LexicalEditor, SerializedEditorState } from "lexical";
import { LexicalComposer } from "@lexical/react/LexicalComposer";
import type { AssetRepository, AssetScope } from "@game-guild/assets";
import { AssetsProvider, useHasAssetsProvider } from "@game-guild/assets/react";
import {
  resolveLexicalSurfaceFeatures,
  type LexicalSurfaceFeatures,
} from "../capabilities/feature-flags";
import { stripSelection } from "../schema/initial-editor-state";
import type { PageSettings } from "../features/page";
import { ToolbarContextProvider } from "../editor-ui/top-toolbar";
import { EditorBody } from "./editor-body";
import { createSurfaceConfig } from "./surface-config";
import { MediaPlugin } from "../features/media/media-plugin";

export type { LexicalSurfaceFeatures } from "../capabilities/feature-flags";

export interface LexicalSurfaceProps {
  initialState?: SerializedEditorState | null;
  onChange?: (state: SerializedEditorState, editor: LexicalEditor) => void;
  onContentChange?: (change: {
    state: SerializedEditorState;
    plainText: string;
  }) => void;
  placeholder?: React.ReactNode;
  accessibleLabel?: string;
  readOnly?: boolean;
  namespace?: string;
  features?: LexicalSurfaceFeatures;
  contentClassName?: string;
  className?: string;
  contentStyle?: React.CSSProperties;
  mountKey?: string | number;
  headerSlot?: React.ReactNode;
  toolbarWrapper?: (toolbar: React.ReactNode) => React.ReactNode;
  contentScrollable?: boolean;
  initialPageSettings?: PageSettings;
  assetsRepository?: AssetRepository;
  assetScope?: AssetScope;
  mediaPlugin?: React.ReactNode | null;
}

export function LexicalSurface({
  initialState,
  onChange,
  onContentChange,
  placeholder,
  accessibleLabel,
  readOnly = false,
  namespace = "LexicalSurface",
  features,
  contentClassName,
  className,
  contentStyle,
  mountKey,
  headerSlot,
  toolbarWrapper,
  contentScrollable,
  initialPageSettings,
  assetsRepository,
  assetScope,
  mediaPlugin,
}: LexicalSurfaceProps) {
  const resolvedFeatures = useMemo(
    () => resolveLexicalSurfaceFeatures(features, readOnly),
    [features, readOnly],
  );
  const hasAssetsProvider = useHasAssetsProvider();
  const seedState = readOnly ? stripSelection(initialState) : initialState;
  const initialConfig = useMemo(
    () =>
      createSurfaceConfig({
        namespace,
        readOnly,
        initialState: seedState ?? null,
      }),
    // Initial state is mount-time data. Consumers use mountKey for resets.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [mountKey, readOnly, namespace],
  );

   const content = (
     <LexicalComposer key={mountKey} initialConfig={initialConfig}>
       {mediaPlugin && <MediaPlugin />}
       <ToolbarContextProvider initialPageSettings={initialPageSettings}>
         <EditorBody
           features={resolvedFeatures}
           onChange={onChange}
           onContentChange={onContentChange}
           placeholder={placeholder}
           accessibleLabel={accessibleLabel}
           readOnly={readOnly}
           contentClassName={contentClassName}
           contentStyle={contentStyle}
           contentScrollable={contentScrollable}
         />
       </ToolbarContextProvider>
     </LexicalComposer>
   );

  return assetsRepository || assetScope || !hasAssetsProvider
    ? <AssetsProvider repository={assetsRepository} scope={assetScope}>{content}</AssetsProvider>
    : content;
}
