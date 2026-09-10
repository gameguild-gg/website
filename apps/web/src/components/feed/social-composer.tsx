"use client";

import {
  createSocialPost,
  getSocialMediaStatus,
  uploadSocialMedia,
} from "@/lib/feed/actions";
import type { SocialMediaAsset } from "@/lib/feed/contracts";
import { Button } from "@game-guild/ui/components/button";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  FileVideo2,
  ImageIcon,
  Loader2,
  Send,
  Upload,
  X,
} from "lucide-react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import * as React from "react";
import { toast } from "sonner";

const IMAGE_LIMIT = 10 * 1024 * 1024;
const VIDEO_LIMIT = 100 * 1024 * 1024;
const ACCEPTED_TYPES = new Set([
  "image/jpeg",
  "image/png",
  "image/webp",
  "image/gif",
  "video/mp4",
]);

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

function mediaError(file: File) {
  if (!ACCEPTED_TYPES.has(file.type)) {
    return "Choose a JPEG, PNG, WebP, GIF, or MP4 file.";
  }
  const limit = file.type === "video/mp4" ? VIDEO_LIMIT : IMAGE_LIMIT;
  if (file.size > limit) {
    return file.type === "video/mp4"
      ? "MP4 videos can be up to 100 MB."
      : "Images can be up to 10 MB.";
  }
  return null;
}

function tagsFrom(content: string) {
  return [
    ...new Set(
      [...content.matchAll(/#([\p{L}\p{N}_]+)/gu)].map((match) =>
        match[1]!.toLowerCase(),
      ),
    ),
  ].slice(0, 10);
}

async function waitUntilReady(asset: SocialMediaAsset, onProcessing: () => void) {
  let current = asset;
  if (current.state === "Processing") onProcessing();
  for (let attempt = 0; current.state === "Processing" && attempt < 20; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, Math.min(500 + attempt * 250, 2_000)));
    current = await getSocialMediaStatus(current.assetReferenceId);
  }
  if (current.state === "Rejected") throw new Error("The media file was rejected during processing.");
  if (current.state !== "Ready") throw new Error("Media processing timed out. Please try again.");
  return current;
}

export function SocialComposer({ userName }: { userName: string }): React.JSX.Element {
  const router = useRouter();
  const [expanded, setExpanded] = React.useState(false);
  const [content, setContent] = React.useState("");
  const [file, setFile] = React.useState<File | null>(null);
  const [fileError, setFileError] = React.useState<string | null>(null);
  const [pending, setPending] = React.useState(false);
  const [progressLabel, setProgressLabel] = React.useState<string | null>(null);
  const inputRef = React.useRef<HTMLInputElement>(null);
  const pendingRef = React.useRef(false);

  const previewUrl = React.useMemo(
    () => (file ? URL.createObjectURL(file) : null),
    [file],
  );

  React.useEffect(() => {
    return () => {
      if (previewUrl) URL.revokeObjectURL(previewUrl);
    };
  }, [previewUrl]);

  React.useEffect(() => {
    const openComposer = () => setExpanded(true);
    window.addEventListener("social:compose", openComposer);
    return () => window.removeEventListener("social:compose", openComposer);
  }, []);

  function chooseFile(next: File | null) {
    if (!next) return;
    const error = mediaError(next);
    setFileError(error);
    setFile(error ? null : next);
  }

  async function publish(event: React.FormEvent) {
    event.preventDefault();
    if (pendingRef.current) return;
    const text = content.trim();
    if (!text && !file) return;
    pendingRef.current = true;
    setPending(true);
    try {
      let assetReferenceId: string | null = null;
      if (file) {
        setProgressLabel("Uploading media…");
        const body = new FormData();
        body.append("file", file);
        assetReferenceId = (
          await waitUntilReady(await uploadSocialMedia(body), () => setProgressLabel("Processing media…"))
        ).assetReferenceId;
      }
      setProgressLabel("Publishing post…");
      await createSocialPost({
        content: text,
        visibility: "Public",
        assetReferenceId,
        tags: tagsFrom(text),
      });
      setContent("");
      setFile(null);
      setFileError(null);
      setExpanded(false);
      toast.success("Post published.");
      router.refresh();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "The post could not be published.");
    } finally {
      pendingRef.current = false;
      setPending(false);
      setProgressLabel(null);
    }
  }

  if (!expanded) {
    return (
      <section id="social-composer" className="px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3 rounded-xl bg-card p-2.5 text-card-foreground">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/15 text-xs font-bold text-foreground">
            {initials(userName)}
          </span>
          <button
            type="button"
            aria-label="Share your progress"
            onClick={() => setExpanded(true)}
            className="min-w-0 flex-1 rounded-lg px-2 py-2 text-left text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            Share your progress…
          </button>
          <Button type="button" variant="ghost" size="sm" onClick={() => { setExpanded(true); setTimeout(() => inputRef.current?.click(), 0); }} className="hidden text-muted-foreground sm:inline-flex">
            <ImageIcon className="size-4" /> Media
          </Button>
        </div>
      </section>
    );
  }

  return (
    <section id="social-composer" className="px-4 py-4 sm:px-6">
      <form onSubmit={publish} className="rounded-xl bg-card p-4 text-card-foreground">
        <div className="flex items-start gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/15 text-xs font-bold text-foreground">
            {initials(userName)}
          </span>
          <Textarea
            autoFocus
            rows={3}
            maxLength={4000}
            value={content}
            onChange={(event) => setContent(event.target.value)}
            placeholder="What are you building?"
            className="min-h-20 flex-1 resize-none border-0 bg-transparent px-1 py-1 text-sm shadow-none focus-visible:ring-0"
          />
          <Button type="button" variant="ghost" size="icon-sm" onClick={() => setExpanded(false)} aria-label="Close composer">
            <X className="size-4" />
          </Button>
        </div>

        {file && previewUrl ? (
          <div className="relative ml-12 mt-3 overflow-hidden rounded-xl bg-accent/50">
            {file.type === "video/mp4" ? (
              <video src={previewUrl} aria-label="Selected media preview" controls className="max-h-80 w-full object-contain" />
            ) : (
              <Image src={previewUrl} alt="Selected media preview" width={960} height={720} unoptimized className="max-h-80 w-full object-contain" />
            )}
            <div className="absolute inset-x-0 bottom-0 flex items-center gap-2 bg-background/85 px-3 py-2 text-xs backdrop-blur">
              {file.type === "video/mp4" ? <FileVideo2 className="size-4 text-primary" /> : <ImageIcon className="size-4 text-primary" />}
              <span className="min-w-0 flex-1 truncate">{file.name}</span>
              <span className="text-muted-foreground">{(file.size / 1024 / 1024).toFixed(1)} MB</span>
              <Button type="button" variant="ghost" size="icon-sm" disabled={pending} onClick={() => setFile(null)} aria-label="Remove media"><X className="size-4" /></Button>
            </div>
          </div>
        ) : null}
        {fileError ? <p role="alert" className="ml-12 mt-2 text-xs text-destructive">{fileError}</p> : null}

        <div className="ml-12 mt-3 flex items-center justify-between gap-3">
          <div>
            <input
              ref={inputRef}
              type="file"
              accept="image/jpeg,image/png,image/webp,image/gif,video/mp4"
              className="sr-only"
              aria-label="Add photo or video"
              onChange={(event) => chooseFile(event.target.files?.[0] ?? null)}
            />
            <Button type="button" variant="ghost" size="sm" disabled={pending} onClick={() => inputRef.current?.click()}>
              <Upload className="size-4" /> Photo or video
            </Button>
          </div>
          <div className="ml-auto flex items-center gap-3">
            <span className="text-xs text-muted-foreground" aria-live="polite">{progressLabel ?? `${content.length}/4000`}</span>
          <Button type="submit" size="sm" disabled={pending || (!content.trim() && !file)} aria-label="Publish">
            {pending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}
            {pending ? "Publishing…" : "Publish"}
          </Button>
          </div>
        </div>
      </form>
    </section>
  );
}
