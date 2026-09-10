"use client";

import { Button } from "@game-guild/ui/components/button";
import { Upload } from "lucide-react";
import * as React from "react";
import { SocialMediaPreview } from "./social-media-preview";

export const SOCIAL_MEDIA_ACCEPT = "image/jpeg,image/png,image/webp,image/gif,video/mp4";
export const SOCIAL_IMAGE_MAX_BYTES = 10 * 1024 * 1024;
export const SOCIAL_VIDEO_MAX_BYTES = 100 * 1024 * 1024;

const ACCEPTED_TYPES = new Set(SOCIAL_MEDIA_ACCEPT.split(","));

export interface SelectedSocialMedia {
  file: File;
  previewUrl: string;
  kind: "image" | "video";
}

export function validateSelectedSocialMedia(file: File): string | null {
  if (!ACCEPTED_TYPES.has(file.type)) return "Choose a JPEG, PNG, WebP, GIF, or MP4 file.";
  if (file.size > (file.type === "video/mp4" ? SOCIAL_VIDEO_MAX_BYTES : SOCIAL_IMAGE_MAX_BYTES)) {
    return file.type === "video/mp4" ? "MP4 videos can be up to 100 MB." : "Images can be up to 10 MB.";
  }
  return null;
}

export function SocialMediaPicker({
  value,
  onChange,
  disabled = false,
}: {
  value: SelectedSocialMedia | null;
  onChange: (value: SelectedSocialMedia | null) => void;
  disabled?: boolean;
}): React.JSX.Element {
  const [error, setError] = React.useState<string | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);

  React.useEffect(() => () => {
    if (value) URL.revokeObjectURL(value.previewUrl);
  }, [value]);

  function select(file: File | null) {
    if (!file) return;
    const nextError = validateSelectedSocialMedia(file);
    setError(nextError);
    if (nextError) return;
    onChange({ file, previewUrl: URL.createObjectURL(file), kind: file.type === "video/mp4" ? "video" : "image" });
  }

  return (
    <div>
      {value ? <SocialMediaPreview media={value} disabled={disabled} onRemove={() => onChange(null)} /> : null}
      {error ? <p role="alert" className="mt-2 text-xs text-destructive">{error}</p> : null}
      <input ref={inputRef} type="file" accept={SOCIAL_MEDIA_ACCEPT} className="sr-only" aria-label="Add photo or video" onChange={(event) => select(event.target.files?.[0] ?? null)} />
      <Button type="button" variant="ghost" size="sm" disabled={disabled} onClick={() => inputRef.current?.click()}>
        <Upload className="size-4" /> Photo or video
      </Button>
    </div>
  );
}
