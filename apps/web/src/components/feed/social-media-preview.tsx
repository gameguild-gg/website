"use client";

import type { SelectedSocialMedia } from "./social-media-picker";
import { Button } from "@game-guild/ui/components/button";
import { FileVideo2, ImageIcon, X } from "lucide-react";
import Image from "next/image";

function safeMediaSrc(value: string): string {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:" || url.protocol === "blob:" ? url.href : "";
  } catch {
    return "";
  }
}

export function SocialMediaPreview({
  media,
  disabled = false,
  onRemove,
}: {
  media: SelectedSocialMedia;
  disabled?: boolean;
  onRemove: () => void;
}): React.JSX.Element {
  const previewUrl = safeMediaSrc(media.previewUrl);
  return (
    <div className="relative overflow-hidden rounded-xl bg-accent/50">
      {media.kind === "video" ? (
        <video src={previewUrl} aria-label="Selected media preview" controls className="max-h-80 w-full object-contain" />
      ) : (
        <div className="relative h-80 w-full">
          <Image src={previewUrl} alt="Selected media preview" fill sizes="100vw" unoptimized className="object-contain" />
        </div>
      )}
      <div className="absolute inset-x-0 bottom-0 flex items-center gap-2 bg-background/85 px-3 py-2 text-xs backdrop-blur">
        {media.kind === "video" ? <FileVideo2 className="size-4 text-primary" /> : <ImageIcon className="size-4 text-primary" />}
        <span className="min-w-0 flex-1 truncate">{media.file.name}</span>
        <span className="text-muted-foreground">{(media.file.size / 1024 / 1024).toFixed(1)} MB</span>
        <Button type="button" variant="ghost" size="icon-sm" disabled={disabled} onClick={onRemove} aria-label="Remove media">
          <X className="size-4" />
        </Button>
      </div>
    </div>
  );
}
