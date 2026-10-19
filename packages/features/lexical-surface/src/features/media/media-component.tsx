"use client";

import * as React from "react";
import { useCallback, useEffect, useRef, useState } from "react";
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
import {
  Image as ImageIcon,
  Video as VideoIcon,
  Music as AudioIcon,
  Trash2,
  Plus,
  Upload,
  ChevronDown,
  Settings,
  Grid as GridIcon,
  X,
  Play,
  Pause,
  Volume2,
  VolumeX,
  Maximize,
  Move,
  Check,
  Columns,
  FileText,
  Type,
} from "lucide-react";
import { cn } from "@game-guild/ui/lib/utils";
import type { AssetRecord } from "@game-guild/assets";
import {
  AssetPickerDialog,
  useResolvedAssetUrl,
} from "@game-guild/assets/react";
import { DeleteConfirmDialog } from "../../shared/ui/dialogs/delete-confirm-dialog";
import { useNodeDeleteProtection } from "../../shared/lexical/node-delete-protection";
import { $isMediaLexicalNode, type MediaLexicalNode } from "./media-node";
import type { BaseMediaData, MediaType } from "./media-data";
import { Slider } from "@game-guild/ui/components/slider";
import { Button } from "@game-guild/ui/components/button";
import { Input } from "@game-guild/ui/components/input";
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
  detectVideoEmbedType,
  detectAudioEmbedType,
  detectVideoFileType,
  detectAudioFileType,
} from "./url-detection";

interface MediaLexicalComponentProps {
  mediaType: MediaType;
  src: string;
  alt: string;
  caption: string;
  size: number;
  videoType: string;
  embedType: BaseMediaData["embedType"];
  audioType: string;
  embedAudioType: BaseMediaData["embedAudioType"];
  galleryItems: BaseMediaData[];
  galleryColumns: number;
  galleryCaption: string;
  galleryAspect: "square" | "landscape" | "classic" | "auto";
  showCellCaptions: boolean;
  showGalleryCaption: boolean;
  showCaption: boolean;
  nodeKey: string;
}

function ResolvedImage({
  src,
  ...props
}: React.ImgHTMLAttributes<HTMLImageElement>) {
  const { url: resolvedSrc } = useResolvedAssetUrl(
    typeof src === "string" ? src : "",
  );

  return <img src={resolvedSrc} {...props} />;
}

export function MediaLexicalComponent({
  mediaType,
  src,
  alt,
  caption,
  size,
  embedType,
  embedAudioType,
  galleryItems,
  galleryColumns,
  galleryCaption,
  galleryAspect,
  showCellCaptions,
  showGalleryCaption,
  showCaption,
  nodeKey,
}: MediaLexicalComponentProps): React.JSX.Element {
  const [editor] = useLexicalComposerContext();
  const isEditable = useLexicalEditable();
  const [confirmDeleteOpen, setConfirmDeleteOpen] = useState(false);
  const containerRef = useRef<HTMLDivElement | null>(null);
  const [isSelected, setSelected, clearSelection] =
    useLexicalNodeSelection(nodeKey);
  const { url: resolvedAssetSrc } = useResolvedAssetUrl(src);

  // Upload dialog state
  const [uploadOpen, setUploadOpen] = useState(false);
  const [uploadTargetIndex, setUploadTargetIndex] = useState<number | null>(
    null,
  ); // null means main block, >= 0 means gallery index

  // Video/Audio local preview state
  const [resolvedSrc, setResolvedSrc] = useState<string | null>(null);
  const [isPlaying, setIsPlaying] = useState(false);
  const [currentTime, setCurrentTime] = useState(0);
  const [duration, setDuration] = useState(0);
  const [volume, setVolume] = useState(0.7);
  const [muted, setMuted] = useState(false);
  const [showVideoControls, setShowVideoControls] = useState(false);
  const mediaRef = useRef<HTMLVideoElement | HTMLAudioElement>(null);

  // Drag and drop state for gallery reordering
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // URL input field state
  const [inputUrl, setInputUrl] = useState(src);

  useEffect(() => {
    setResolvedSrc(resolvedAssetSrc || null);
    setInputUrl(src);
  }, [resolvedAssetSrc, src]);

  // Sync volume state to video/audio tag
  useEffect(() => {
    if (mediaRef.current) {
      mediaRef.current.volume = volume;
      mediaRef.current.muted = muted;
    }
  }, [volume, muted]);

  // Delete protection
  useNodeDeleteProtection({
    nodeKey,
    enabled: isEditable,
    onRequestDelete: () => setConfirmDeleteOpen(true),
  });

  // Select node on click
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
            // Let buttons/inputs/controls take event precedence
            if (
              targetEl.closest("button") ||
              targetEl.closest("input") ||
              targetEl.closest("select") ||
              targetEl.closest("[role='slider']") ||
              targetEl.closest(".z-50")
            ) {
              return false;
            }

            if (!event.shiftKey) clearSelection();
            setSelected(!isSelected);
            return true;
          }
          return false;
        },
        COMMAND_PRIORITY_LOW,
      ),
    );
  }, [clearSelection, editor, isSelected, setSelected, isEditable]);

  const deleteNode = useCallback(() => {
    setConfirmDeleteOpen(false);
    editor.update(() => {
      const node = $getNodeByKey(nodeKey);
      if (node) node.remove();
    });
  }, [editor, nodeKey]);

  // Update helper
  const updateNodeData = useCallback(
    (updater: (node: MediaLexicalNode) => void) => {
      editor.update(() => {
        const node = $getNodeByKey(nodeKey);
        if ($isMediaLexicalNode(node)) {
          updater(node);
        }
      });
    },
    [editor, nodeKey],
  );

  // Audio/Video player helpers
  const formatTime = (time: number) => {
    const mins = Math.floor(time / 60);
    const secs = Math.floor(time % 60);
    return `${mins}:${secs < 10 ? "0" : ""}${secs}`;
  };

  const togglePlay = () => {
    if (!mediaRef.current) return;
    try {
      if (isPlaying) {
        mediaRef.current.pause();
        setIsPlaying(false);
      } else {
        const p = mediaRef.current.play();
        if (p !== undefined) {
          p.then(() => setIsPlaying(true)).catch(() => setIsPlaying(false));
        }
      }
    } catch (e) {
      console.error(e);
    }
  };

  // Handle URL change & auto-detect embed type
  const handleUrlSubmit = (url: string) => {
    setInputUrl(url);
    updateNodeData((node) => {
      node.setSrc(url);
      if (mediaType === "video") {
        const det = detectVideoEmbedType(url);
        node.setEmbedType(det);
        if (det === "direct") {
          node.setVideoType(detectVideoFileType(url));
        }
      } else if (mediaType === "audio") {
        const det = detectAudioEmbedType(url);
        node.setEmbedAudioType(det);
        if (det === "direct") {
          node.setAudioType(detectAudioFileType(url));
        }
      }
    });
  };

  // Handle media selection from uploader dialog
  const handleMediaSelected = (result: AssetRecord | AssetRecord[]) => {
    const urls = Array.isArray(result)
      ? result.map((item) => item.uri)
      : [result.uri];
    const firstUrl = urls[0];
    if (!firstUrl) return;

    if (uploadTargetIndex !== null) {
      // Replace or add within gallery
      const items = [...ensureGalleryItems()];
      if (uploadTargetIndex < items.length) {
        const currentItem = items[uploadTargetIndex];
        if (!currentItem) return;
        items[uploadTargetIndex] = {
          ...currentItem,
          src: firstUrl,
          isPlaceholder: false,
        };
      } else {
        urls.forEach((u) => {
          items.push({
            type: "image",
            src: u,
            size: 100,
          });
        });
      }
      updateNodeData((node) => {
        node.setGalleryItems(items);
        node.setSrc(""); // Clear main src to prevent duplicate/ambiguity
      });
    } else {
      // Main block upload
      if (mediaType === "image" && urls.length > 1) {
        // Multi-image upload converts into gallery directly
        const items: BaseMediaData[] = urls.map((u) => ({
          type: "image",
          src: u,
          size: 100,
        }));
        updateNodeData((node) => {
          node.setGalleryItems(items);
          node.setSrc(""); // Clear main src to prevent duplicate/ambiguity
        });
      } else {
        handleUrlSubmit(firstUrl);
      }
    }
    setUploadOpen(false);
    setUploadTargetIndex(null);
  };

  // Add more images to convert single image to gallery
  const handleAddGalleryImages = () => {
    const currentItems = ensureGalleryItems();
    setUploadTargetIndex(currentItems.length);
    setUploadOpen(true);
  };

  // Convert current single image into gallery with items
  const ensureGalleryItems = (): BaseMediaData[] => {
    if (galleryItems.length > 0) return galleryItems;
    if (src) {
      return [{ type: "image", src, size: 100 }];
    }
    return [];
  };

  // HTML5 Drag & Drop handlers for gallery reordering
  const onDragStart = (e: React.DragEvent, index: number) => {
    if (!isEditable) return;
    e.dataTransfer.effectAllowed = "move";
    setDraggedIndex(index);
  };

  const onDragOver = (e: React.DragEvent, index: number) => {
    if (!isEditable) return;
    e.preventDefault();
    if (draggedIndex !== null && draggedIndex !== index) {
      setDragOverIndex(index);
    }
  };

  const onDragLeave = () => {
    setDragOverIndex(null);
  };

  const onDrop = (e: React.DragEvent, targetIndex: number) => {
    if (!isEditable || draggedIndex === null) return;
    e.preventDefault();
    if (draggedIndex !== targetIndex) {
      const items = [...galleryItems];
      const temp = items[draggedIndex];
      const target = items[targetIndex];
      if (!temp || !target) return;
      items[draggedIndex] = target;
      items[targetIndex] = temp;
      updateNodeData((node) => {
        node.setGalleryItems(items);
      });
    }
    setDraggedIndex(null);
    setDragOverIndex(null);
  };

  // Renders aspect ratios based on choice
  const aspectClass = {
    square: "aspect-square object-cover",
    landscape: "aspect-video object-cover",
    classic: "aspect-[4/3] object-cover",
    auto: "aspect-auto",
  }[galleryAspect];

  return (
    <>
      <div
        ref={containerRef}
        className={cn(
          "relative my-6 w-full rounded-xl border bg-card text-card-foreground shadow-sm transition-all overflow-hidden",
          isSelected
            ? "border-blue-500 ring-2 ring-blue-500/10"
            : "border-gray-200 dark:border-gray-800 hover:border-gray-300 dark:hover:border-gray-700",
        )}
        style={{ width: `${size}%`, margin: "1.5rem auto" }}
      >
        {/* Floating Mini-Toolbar */}
        {isSelected && isEditable && (
          <div
            className="absolute right-3 top-3 z-30 flex items-center gap-1.5 rounded-lg border border-gray-200 dark:border-gray-800 bg-white/95 dark:bg-gray-900/95 shadow-md px-2 py-1 text-xs"
            onMouseDown={(e) => e.stopPropagation()}
          >
            <span className="text-gray-500 dark:text-gray-400 capitalize font-medium mr-1.5">
              {galleryItems.length > 1 ? "Gallery" : mediaType}
            </span>
            <span className="h-4 w-px bg-gray-200 dark:bg-gray-800" />

            {/* Settings DropdownMenu */}
            <DropdownMenu>
              <DropdownMenuTrigger
                render={
                  <button
                    type="button"
                    className="h-6 px-1.5 inline-flex items-center gap-1 rounded hover:bg-gray-100 dark:hover:bg-gray-800 text-gray-700 dark:text-gray-300 transition-colors"
                    title="Media settings"
                  >
                    <Settings className="w-3.5 h-3.5" />
                    <ChevronDown className="w-3 h-3" />
                  </button>
                }
              />
              <DropdownMenuContent align="end" className="w-56">
                {/* Gallery options */}
                {galleryItems.length > 1 && (
                  <>
                    {/* Columns submenu */}
                    <DropdownMenuSub>
                      <DropdownMenuSubTrigger>
                        <Columns className="w-4 h-4 mr-2" />
                        Columns: {galleryColumns}
                      </DropdownMenuSubTrigger>
                      <DropdownMenuSubContent>
                        {[1, 2, 3, 4].map((col) => (
                          <DropdownMenuItem
                            key={col}
                            onSelect={(e) => {
                              e.preventDefault();
                              updateNodeData((node) =>
                                node.setGalleryColumns(col),
                              );
                            }}
                          >
                            {col} Column{col > 1 ? "s" : ""}
                            {galleryColumns === col && (
                              <Check className="ml-auto w-4 h-4" />
                            )}
                          </DropdownMenuItem>
                        ))}
                      </DropdownMenuSubContent>
                    </DropdownMenuSub>

                    {/* Aspect Ratio submenu */}
                    <DropdownMenuSub>
                      <DropdownMenuSubTrigger>
                        <GridIcon className="w-4 h-4 mr-2" />
                        Ratio:{" "}
                        <span className="capitalize ml-1">{galleryAspect}</span>
                      </DropdownMenuSubTrigger>
                      <DropdownMenuSubContent>
                        {(
                          ["auto", "square", "landscape", "classic"] as const
                        ).map((asp) => (
                          <DropdownMenuItem
                            key={asp}
                            onSelect={(e) => {
                              e.preventDefault();
                              updateNodeData((node) =>
                                node.setGalleryAspect(asp),
                              );
                            }}
                            className="capitalize"
                          >
                            {asp}
                            {galleryAspect === asp && (
                              <Check className="ml-auto w-4 h-4" />
                            )}
                          </DropdownMenuItem>
                        ))}
                      </DropdownMenuSubContent>
                    </DropdownMenuSub>

                    <DropdownMenuItem onSelect={handleAddGalleryImages}>
                      <Plus className="w-4 h-4 mr-2 text-green-500" />
                      Add Image
                    </DropdownMenuItem>

                    <DropdownMenuSeparator />

                    <DropdownMenuItem
                      onSelect={(e) => {
                        e.preventDefault();
                        updateNodeData((node) =>
                          node.setShowCellCaptions(!showCellCaptions),
                        );
                      }}
                    >
                      <FileText className="w-4 h-4 mr-2" />
                      Show Cell Captions
                      {showCellCaptions && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>

                    <DropdownMenuItem
                      onSelect={(e) => {
                        e.preventDefault();
                        updateNodeData((node) =>
                          node.setShowGalleryCaption(!showGalleryCaption),
                        );
                      }}
                    >
                      <Type className="w-4 h-4 mr-2" />
                      Show Gallery Caption
                      {showGalleryCaption && (
                        <Check className="ml-auto w-4 h-4" />
                      )}
                    </DropdownMenuItem>

                    <DropdownMenuSeparator />
                  </>
                )}

                {/* Single Image Options */}
                {galleryItems.length <= 1 && mediaType === "image" && (
                  <>
                    <DropdownMenuItem onSelect={handleAddGalleryImages}>
                      <Plus className="w-4 h-4 mr-2 text-green-500" />
                      Convert to Gallery
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onSelect={() => {
                        setUploadTargetIndex(null);
                        setUploadOpen(true);
                      }}
                    >
                      <Upload className="w-4 h-4 mr-2" />
                      Replace Image
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                  </>
                )}

                {/* Video/Audio Options */}
                {mediaType !== "image" && (
                  <>
                    <div
                      className="px-2 py-1.5 text-xs"
                      onKeyDown={(e) => e.stopPropagation()}
                    >
                      <div className="flex items-center justify-between mb-1 text-gray-500 dark:text-gray-400">
                        <span>Source URL</span>
                      </div>
                      <div className="flex items-center gap-1">
                        <input
                          type="text"
                          value={inputUrl}
                          onChange={(e) => setInputUrl(e.target.value)}
                          onBlur={() => handleUrlSubmit(inputUrl)}
                          onKeyDown={(e) => {
                            if (e.key === "Enter") {
                              handleUrlSubmit(inputUrl);
                            }
                            e.stopPropagation();
                          }}
                          onClick={(e) => e.stopPropagation()}
                          onMouseDown={(e) => e.stopPropagation()}
                          onPointerDown={(e) => e.stopPropagation()}
                          className="flex-1 min-w-0 bg-gray-50 dark:bg-gray-900 border border-gray-200 dark:border-gray-800 rounded px-1.5 py-1 focus:ring-1 focus:ring-blue-500 focus:outline-none text-gray-900 dark:text-gray-100"
                        />
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            setUploadTargetIndex(null);
                            setUploadOpen(true);
                          }}
                          className="p-1 rounded bg-gray-100 hover:bg-gray-200 dark:bg-gray-800 dark:hover:bg-gray-700 text-gray-600 dark:text-gray-300"
                          title={`Upload ${mediaType}`}
                        >
                          <Upload className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                    <DropdownMenuSeparator />
                  </>
                )}

                {/* Single Media Caption Toggle */}
                {galleryItems.length <= 1 && (
                  <>
                    <DropdownMenuItem
                      onSelect={(e) => {
                        e.preventDefault();
                        updateNodeData((node) =>
                          node.setShowCaption(!showCaption),
                        );
                      }}
                    >
                      <Type className="w-4 h-4 mr-2" />
                      Show Caption
                      {showCaption && <Check className="ml-auto w-4 h-4" />}
                    </DropdownMenuItem>
                    <DropdownMenuSeparator />
                  </>
                )}

                {/* Block Width submenu with slider */}
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Settings className="w-4 h-4 mr-2" />
                    Block Width: {size}%
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent className="p-3 w-48">
                    <div className="space-y-2">
                      <div className="flex justify-between text-xs font-medium text-gray-500">
                        <span>Width</span>
                        <span>{size}%</span>
                      </div>
                      <Slider
                        value={[size]}
                        min={25}
                        max={100}
                        step={5}
                        onValueChange={(val) => {
                          const next = Array.isArray(val) ? val[0] : val;
                          if (next !== undefined) {
                            updateNodeData((node) => node.setSize(next));
                          }
                        }}
                      />
                    </div>
                  </DropdownMenuSubContent>
                </DropdownMenuSub>

                <DropdownMenuSeparator />

                {/* Delete Block */}
                <DropdownMenuItem
                  onSelect={() => setConfirmDeleteOpen(true)}
                  className="text-red-600 focus:text-red-600 focus:bg-red-50 dark:focus:bg-red-950/30"
                >
                  <Trash2 className="w-4 h-4 mr-2" />
                  Delete Block
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>

            <span className="h-4 w-px bg-gray-200 dark:bg-gray-800" />
            <button
              type="button"
              onClick={() => setConfirmDeleteOpen(true)}
              className="h-6 w-6 inline-flex items-center justify-center rounded hover:bg-red-50 dark:hover:bg-red-950 text-red-600 transition-colors"
              title="Delete block"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        )}

        {/* 1. Empty setup state */}
        {!src && galleryItems.length === 0 ? (
          <div className="p-8 flex flex-col items-center justify-center text-center bg-gray-50 dark:bg-gray-900/40 min-h-[220px]">
            <div className="flex gap-2 mb-6">
              <Button
                variant={mediaType === "image" ? "default" : "outline"}
                size="sm"
                onClick={() =>
                  updateNodeData((node) => node.setMediaType("image"))
                }
                className="gap-1.5"
              >
                <ImageIcon className="h-4 w-4" /> Image
              </Button>
              <Button
                variant={mediaType === "video" ? "default" : "outline"}
                size="sm"
                onClick={() =>
                  updateNodeData((node) => node.setMediaType("video"))
                }
                className="gap-1.5"
              >
                <VideoIcon className="h-4 w-4" /> Video
              </Button>
              <Button
                variant={mediaType === "audio" ? "default" : "outline"}
                size="sm"
                onClick={() =>
                  updateNodeData((node) => node.setMediaType("audio"))
                }
                className="gap-1.5"
              >
                <AudioIcon className="h-4 w-4" /> Audio
              </Button>
            </div>

            <div className="w-full max-w-md space-y-3">
              <div className="flex gap-2">
                <Input
                  value={inputUrl}
                  onChange={(e) => setInputUrl(e.target.value)}
                  placeholder={`Paste ${mediaType} URL...`}
                  className="h-9"
                  onKeyDown={(e) => {
                    if (e.key === "Enter") handleUrlSubmit(inputUrl);
                  }}
                />
                <Button size="sm" onClick={() => handleUrlSubmit(inputUrl)}>
                  Apply
                </Button>
              </div>

              <div className="relative flex items-center justify-center py-2">
                <span className="absolute inset-x-0 h-px bg-gray-200 dark:bg-gray-800" />
                <span className="relative bg-card px-3 text-xs text-muted-foreground uppercase">
                  Or
                </span>
              </div>

              <Button
                variant="outline"
                className="w-full gap-2 border-dashed border-2 hover:border-solid hover:bg-accent/40"
                onClick={() => {
                  setUploadTargetIndex(null);
                  setUploadOpen(true);
                }}
              >
                <Upload className="h-4 w-4 text-blue-500" />
                Upload / Choose File
              </Button>
            </div>
          </div>
        ) : (
          /* 2. Content view states */
          <div className="p-4 space-y-4">
            {/* Gallery View */}
            {galleryItems.length > 1 ? (
              <div className="space-y-4">
                {/* Grid gallery display */}
                <div
                  className="grid gap-3"
                  style={{
                    gridTemplateColumns: `repeat(${galleryColumns}, 1fr)`,
                  }}
                >
                  {galleryItems.map((item, index) => {
                    const isDragged = draggedIndex === index;
                    const isOver = dragOverIndex === index;
                    return (
                      <div
                        key={index}
                        draggable={isEditable}
                        onDragStart={(e) => onDragStart(e, index)}
                        onDragOver={(e) => onDragOver(e, index)}
                        onDragLeave={onDragLeave}
                        onDrop={(e) => onDrop(e, index)}
                        className={cn(
                          "relative group border border-gray-100 dark:border-gray-800 rounded-lg overflow-hidden transition-all bg-muted/40",
                          isDragged && "opacity-45 scale-95 border-blue-500",
                          isOver && "border-blue-500 ring-2 ring-blue-500/50",
                          isEditable && "cursor-grab active:cursor-grabbing",
                        )}
                      >
                        <div className="relative">
                          {item.src ? (
                            <ResolvedImage
                              src={item.src}
                              alt={item.alt || ""}
                              className={cn("w-full object-cover", aspectClass)}
                            />
                          ) : (
                            <div className="w-full aspect-video flex flex-col items-center justify-center p-4">
                              <ImageIcon className="h-8 w-8 text-muted-foreground/50 mb-2" />
                              <span className="text-xs text-muted-foreground">
                                Empty image slot
                              </span>
                            </div>
                          )}

                          {/* Hover edit menu inside cell */}
                          {isEditable && (
                            <div className="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center gap-2">
                              <button
                                type="button"
                                onClick={() => {
                                  setUploadTargetIndex(index);
                                  setUploadOpen(true);
                                }}
                                className="p-1.5 rounded bg-white dark:bg-gray-900 text-gray-800 dark:text-gray-100 hover:bg-gray-100"
                                title="Replace image"
                              >
                                <Upload className="h-4 w-4" />
                              </button>
                              <button
                                type="button"
                                onClick={() => {
                                  const list = galleryItems.filter(
                                    (_, i) => i !== index,
                                  );
                                  updateNodeData((node) => {
                                    node.setGalleryItems(list);
                                    if (list.length === 1 && list[0]) {
                                      // Demote to single image block
                                      node.setSrc(list[0].src || "");
                                      node.setAlt(list[0].alt || "");
                                      node.setGalleryItems([]);
                                    }
                                  });
                                }}
                                className="p-1.5 rounded bg-red-600 text-white hover:bg-red-700"
                                title="Remove from gallery"
                              >
                                <X className="h-4 w-4" />
                              </button>
                              <div className="absolute top-2 left-2 cursor-grab text-white pointer-events-none opacity-80">
                                <Move className="h-4 w-4" />
                              </div>
                            </div>
                          )}
                        </div>

                        {/* Direct caption input for cell */}
                        {showCellCaptions && (
                          <div className="p-2 border-t border-gray-100 dark:border-gray-800 bg-white dark:bg-gray-900">
                            {isEditable ? (
                              <input
                                type="text"
                                value={item.caption || ""}
                                onChange={(e) => {
                                  const list = [...galleryItems];
                                  if (list[index]) {
                                    list[index] = {
                                      ...list[index],
                                      caption: e.target.value,
                                    };
                                    updateNodeData((node) =>
                                      node.setGalleryItems(list),
                                    );
                                  }
                                }}
                                placeholder="Add cell caption..."
                                className="w-full text-center text-xs bg-transparent border-0 focus:ring-0 focus:outline-none placeholder:text-gray-400 dark:placeholder:text-gray-600"
                              />
                            ) : (
                              item.caption && (
                                <p className="text-center text-xs text-muted-foreground">
                                  {item.caption}
                                </p>
                              )
                            )}
                          </div>
                        )}
                      </div>
                    );
                  })}
                </div>

                {/* Overall Gallery Caption */}
                {showGalleryCaption && (
                  <div className="pt-2 border-t border-gray-100 dark:border-gray-800">
                    {isEditable ? (
                      <input
                        type="text"
                        value={galleryCaption}
                        onChange={(e) =>
                          updateNodeData((node) =>
                            node.setGalleryCaption(e.target.value),
                          )
                        }
                        placeholder="Add gallery caption..."
                        className="w-full text-center text-sm bg-transparent border-0 focus:ring-0 focus:outline-none text-muted-foreground"
                      />
                    ) : (
                      galleryCaption && (
                        <p className="text-center text-sm text-muted-foreground">
                          {galleryCaption}
                        </p>
                      )
                    )}
                  </div>
                )}
              </div>
            ) : mediaType === "image" ? (
              /* Single Image View */
              <div className="space-y-4">
                <div className="flex justify-center w-full relative group">
                  <ResolvedImage
                    src={src}
                    alt={alt}
                    className="max-h-[500px] w-auto rounded-lg object-contain"
                  />

                  {/* Inline toolbar for Single Image */}
                  {isEditable && (
                    <div className="absolute bottom-4 left-1/2 -translate-x-1/2 opacity-0 group-hover:opacity-100 transition-opacity bg-black/75 rounded-lg p-1.5 flex items-center gap-1.5 text-xs text-white">
                      <button
                        onClick={() => {
                          setUploadTargetIndex(null);
                          setUploadOpen(true);
                        }}
                        className="px-2 py-1 rounded hover:bg-white/10 flex items-center gap-1"
                      >
                        <Upload className="h-3.5 w-3.5 text-blue-400" /> Replace
                      </button>
                      <span className="w-px h-4 bg-white/20" />
                      <button
                        onClick={handleAddGalleryImages}
                        className="px-2 py-1 rounded hover:bg-white/10 flex items-center gap-1"
                      >
                        <Plus className="h-3.5 w-3.5 text-green-400" /> Convert
                        to Gallery
                      </button>
                    </div>
                  )}
                </div>

                {/* Direct Caption input */}
                {showCaption && (
                  <div className="w-full">
                    {isEditable ? (
                      <input
                        type="text"
                        value={caption}
                        onChange={(e) =>
                          updateNodeData((node) =>
                            node.setCaption(e.target.value),
                          )
                        }
                        placeholder="Add caption..."
                        className="w-full text-center text-sm bg-transparent border-0 focus:ring-0 focus:outline-none text-muted-foreground"
                      />
                    ) : (
                      caption && (
                        <p className="text-center text-sm text-muted-foreground">
                          {caption}
                        </p>
                      )
                    )}
                  </div>
                )}
              </div>
            ) : mediaType === "video" ? (
              /* Video View */
              <div className="space-y-4">
                {embedType && embedType !== "direct" ? (
                  /* Video Embed */
                  <div className="relative pt-[56.25%] bg-black rounded-lg overflow-hidden">
                    <iframe
                      src={
                        embedType === "youtube"
                          ? `https://www.youtube-nocookie.com/embed/${
                              src.match(
                                /(?:youtube\.com\/(?:[^/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?/\s]{11})/i,
                              )?.[1]
                            }?enablejsapi=1`
                          : embedType === "vimeo"
                            ? `https://player.vimeo.com/video/${
                                src.match(
                                  /(?:vimeo\.com\/(?:video\/)?|player\.vimeo\.com\/video\/)([0-9]+)/i,
                                )?.[1]
                              }`
                            : `https://www.dailymotion.com/embed/video/${
                                src.match(
                                  /(?:dailymotion\.com\/(?:video\/|embed\/video\/)|dai\.ly\/)([a-zA-Z0-9]+)/i,
                                )?.[1]
                              }`
                      }
                      className="absolute inset-0 w-full h-full"
                      allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                      allowFullScreen
                      // @ts-expect-error credentialless is not yet in React's iframe types
                      credentialless="true"
                    />
                  </div>
                ) : (
                  /* Direct Video player */
                  <div
                    className="relative bg-black rounded-lg overflow-hidden"
                    onMouseEnter={() => setShowVideoControls(true)}
                    onMouseLeave={() => setShowVideoControls(false)}
                  >
                    <video
                      ref={mediaRef as React.RefObject<HTMLVideoElement>}
                      src={resolvedSrc || src}
                      className="w-full h-auto"
                      onTimeUpdate={() => {
                        if (mediaRef.current) {
                          setCurrentTime(mediaRef.current.currentTime);
                          if (mediaRef.current.duration && !duration) {
                            setDuration(mediaRef.current.duration);
                          }
                        }
                      }}
                      onEnded={() => setIsPlaying(false)}
                    />
                    {showVideoControls && (
                      <div className="absolute bottom-0 left-0 right-0 bg-black/70 text-white p-2 flex flex-col gap-1 text-xs">
                        <Slider
                          value={[currentTime]}
                          max={duration || 100}
                          step={0.1}
                          onValueChange={(vals) => {
                            const next = Array.isArray(vals)
                              ? vals[0]
                              : vals;
                            if (mediaRef.current && next !== undefined) {
                              mediaRef.current.currentTime = next;
                              setCurrentTime(next);
                            }
                          }}
                        />
                        <div className="flex items-center justify-between mt-1">
                          <div className="flex items-center gap-2">
                            <button onClick={togglePlay}>
                              {isPlaying ? (
                                <Pause className="h-4 w-4" />
                              ) : (
                                <Play className="h-4 w-4" />
                              )}
                            </button>
                            <span>
                              {formatTime(currentTime)} / {formatTime(duration)}
                            </span>
                          </div>
                          <div className="flex items-center gap-2">
                            <button onClick={() => setMuted(!muted)}>
                              {muted ? (
                                <VolumeX className="h-4 w-4" />
                              ) : (
                                <Volume2 className="h-4 w-4" />
                              )}
                            </button>
                            <Slider
                              value={[muted ? 0 : volume]}
                              max={1}
                              step={0.05}
                              className="w-16"
                              onValueChange={(v) => {
                                const next = Array.isArray(v)
                                  ? v[0]
                                  : v;
                                if (next !== undefined) {
                                  setVolume(next);
                                  setMuted(next === 0);
                                }
                              }}
                            />
                            <button
                              onClick={() => {
                                if (mediaRef.current)
                                  mediaRef.current.requestFullscreen();
                              }}
                            >
                              <Maximize className="h-4 w-4" />
                            </button>
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                )}

                {/* Video Caption */}
                {showCaption && (
                  <div>
                    {isEditable ? (
                      <input
                        type="text"
                        value={caption}
                        onChange={(e) =>
                          updateNodeData((node) =>
                            node.setCaption(e.target.value),
                          )
                        }
                        placeholder="Add caption..."
                        className="w-full text-center text-sm bg-transparent border-0 focus:ring-0 focus:outline-none text-muted-foreground"
                      />
                    ) : (
                      caption && (
                        <p className="text-center text-sm text-muted-foreground">
                          {caption}
                        </p>
                      )
                    )}
                  </div>
                )}
              </div>
            ) : (
              /* Audio View */
              <div className="space-y-4">
                {embedAudioType && embedAudioType !== "direct" ? (
                  /* Audio Embed player */
                  <div className="w-full bg-card border rounded-xl overflow-hidden shadow-sm">
                    <iframe
                      src={
                        embedAudioType === "youtube"
                          ? `https://www.youtube-nocookie.com/embed/${
                              src.match(
                                /(?:youtube\.com\/(?:[^/]+\/.+\/|(?:v|e(?:mbed)?)\/|.*[?&]v=)|youtu\.be\/)([^"&?/\s]{11})/i,
                              )?.[1]
                            }?controls=1`
                          : embedAudioType === "spotify"
                            ? `https://open.spotify.com/embed/track/${
                                src.match(
                                  /(?:spotify\.com\/track\/|spotify:track:)([a-zA-Z0-9]+)/i,
                                )?.[1]
                              }`
                            : `https://w.soundcloud.com/player/?url=https%3A//soundcloud.com/${
                                src.match(
                                  /soundcloud\.com\/([^/]+\/[^/]+)/i,
                                )?.[1]
                              }&color=%23ff5500`
                      }
                      height={embedAudioType === "soundcloud" ? "166" : "80"}
                      className="w-full"
                      allow="autoplay; clipboard-write; encrypted-media; fullscreen"
                      loading="lazy"
                      // @ts-expect-error credentialless is not yet in React's iframe types
                      credentialless="true"
                    />
                  </div>
                ) : (
                  /* Audio Direct player */
                  <div className="bg-card border rounded-lg p-4 space-y-2">
                    <audio
                      ref={mediaRef as React.RefObject<HTMLAudioElement>}
                      src={resolvedSrc || src}
                      onTimeUpdate={() => {
                        if (mediaRef.current) {
                          setCurrentTime(mediaRef.current.currentTime);
                          if (mediaRef.current.duration && !duration) {
                            setDuration(mediaRef.current.duration);
                          }
                        }
                      }}
                      onEnded={() => setIsPlaying(false)}
                    />
                    <Slider
                      value={[currentTime]}
                      max={duration || 100}
                      step={0.1}
                       onValueChange={(vals) => {
                         const next = Array.isArray(vals)
                           ? vals[0]
                           : vals;
                         if (mediaRef.current && next !== undefined) {
                           mediaRef.current.currentTime = next;
                           setCurrentTime(next);
                         }
                       }}
                    />
                    <div className="flex items-center justify-between text-xs">
                      <div className="flex items-center gap-2">
                        <button onClick={togglePlay}>
                          {isPlaying ? (
                            <Pause className="h-4.5 w-4.5" />
                          ) : (
                            <Play className="h-4.5 w-4.5" />
                          )}
                        </button>
                        <span>
                          {formatTime(currentTime)} / {formatTime(duration)}
                        </span>
                      </div>
                      <div className="flex items-center gap-2">
                        <button onClick={() => setMuted(!muted)}>
                          {muted ? (
                            <VolumeX className="h-4 w-4" />
                          ) : (
                            <Volume2 className="h-4 w-4" />
                          )}
                        </button>
                        <Slider
                          value={[muted ? 0 : volume]}
                          max={1}
                          step={0.05}
                          className="w-16"
                           onValueChange={(v) => {
                             const next = Array.isArray(v) ? v[0] : v;
                             if (next !== undefined) {
                               setVolume(next);
                               setMuted(next === 0);
                             }
                           }}
                        />
                      </div>
                    </div>
                  </div>
                )}

                {/* Audio Caption */}
                {showCaption && (
                  <div>
                    {isEditable ? (
                      <input
                        type="text"
                        value={caption}
                        onChange={(e) =>
                          updateNodeData((node) =>
                            node.setCaption(e.target.value),
                          )
                        }
                        placeholder="Add caption..."
                        className="w-full text-center text-sm bg-transparent border-0 focus:ring-0 focus:outline-none text-muted-foreground"
                      />
                    ) : (
                      caption && (
                        <p className="text-center text-sm text-muted-foreground">
                          {caption}
                        </p>
                      )
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>

      {/* Delete Confirmation Dialog */}
      <DeleteConfirmDialog
        open={confirmDeleteOpen}
        onOpenChange={setConfirmDeleteOpen}
        title="Remove media block?"
        itemName="this media block"
        itemType="media"
        onConfirm={deleteNode}
        confirmText="Remove"
      />

      <AssetPickerDialog
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        onSelect={handleMediaSelected}
        title={`Add ${uploadTargetIndex !== null ? "image to gallery" : mediaType}`}
        accept={
          uploadTargetIndex !== null || mediaType === "image"
            ? "image/*"
            : mediaType === "video"
              ? "video/*"
              : "audio/*"
        }
        kinds={[
          uploadTargetIndex !== null ? "image" : mediaType,
        ]}
        maxSizeBytes={250 * 1024 * 1024}
        multiple={uploadTargetIndex !== null || mediaType === "image"}
      />
    </>
  );
}
