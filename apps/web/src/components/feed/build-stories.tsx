"use client";

import {
  createStory,
  deleteStory,
  getSocialMediaStatus,
  markStoryViewed,
  uploadSocialMedia,
} from "@/lib/feed/actions";
import type { SocialStory } from "@/lib/feed/contracts";
import { Button } from "@game-guild/ui/components/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@game-guild/ui/components/dialog";
import { ChevronLeft, ChevronRight, Loader2, Plus, Trash2 } from "lucide-react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import * as React from "react";
import { toast } from "sonner";

export interface SocialStoryPreview extends SocialStory {
  authorName: string;
  authorHandle: string;
  authorAvatarUrl: string | null;
  isOwn: boolean;
}

function initials(name: string) {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

async function uploadStoryFile(file: File) {
  if (![
    "image/jpeg",
    "image/png",
    "image/webp",
    "image/gif",
    "video/mp4",
  ].includes(file.type)) {
    throw new Error("Choose a JPEG, PNG, WebP, GIF, or MP4 file.");
  }
  const limit = file.type === "video/mp4" ? 100 * 1024 * 1024 : 10 * 1024 * 1024;
  if (file.size > limit) throw new Error("This story file is too large.");
  const body = new FormData();
  body.append("file", file);
  let asset = await uploadSocialMedia(body);
  for (let attempt = 0; asset.state === "Processing" && attempt < 20; attempt += 1) {
    await new Promise((resolve) => setTimeout(resolve, Math.min(500 + attempt * 250, 2_000)));
    asset = await getSocialMediaStatus(asset.assetReferenceId);
  }
  if (asset.state !== "Ready") throw new Error("The story media could not be processed.");
  return asset;
}

export function BuildStories({
  userName,
  stories,
}: {
  userName: string;
  stories: SocialStoryPreview[];
}): React.JSX.Element {
  const router = useRouter();
  const [storyItems, setStoryItems] = React.useState(stories);
  const [activeIndex, setActiveIndex] = React.useState<number | null>(null);
  const [uploading, setUploading] = React.useState(false);
  const fileRef = React.useRef<HTMLInputElement>(null);
  const active = activeIndex === null ? null : storyItems[activeIndex] ?? null;

  async function addStory(file: File | null) {
    if (!file) return;
    setUploading(true);
    try {
      const asset = await uploadStoryFile(file);
      await createStory(asset.assetReferenceId);
      toast.success("Story published for 24 hours.");
      router.refresh();
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Story could not be published.");
    } finally {
      setUploading(false);
      if (fileRef.current) fileRef.current.value = "";
    }
  }

  const openStory = React.useCallback(async (index: number) => {
    const story = storyItems[index];
    if (!story) return;
    setActiveIndex(index);
    if (!story.isViewed) {
      setStoryItems((current) => current.map((item) => item.id === story.id ? { ...item, isViewed: true } : item));
      try { await markStoryViewed(story.id); }
      catch { /* A read receipt must not block viewing. */ }
    }
  }, [storyItems]);

  const moveStory = React.useCallback((direction: -1 | 1) => {
    if (activeIndex === null) return;
    const next = activeIndex + direction;
    if (next < 0) return;
    if (next >= storyItems.length) {
      setActiveIndex(null);
      return;
    }
    void openStory(next);
  }, [activeIndex, openStory, storyItems.length]);

  React.useEffect(() => {
    if (!active || active.mediaType?.startsWith("video")) return;
    const reducedMotion = window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false;
    if (reducedMotion) return;
    const timer = window.setTimeout(() => moveStory(1), 5_000);
    return () => window.clearTimeout(timer);
  }, [active, moveStory]);

  return (
    <>
      <section aria-label="Stories" className="overflow-hidden px-4 py-4 sm:px-6">
        <div className="flex gap-4 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          <button type="button" disabled={uploading} onClick={() => fileRef.current?.click()} className="group flex w-[4.5rem] shrink-0 flex-col items-center gap-2 text-center" aria-label="Add story">
            <span className="relative flex size-14 items-center justify-center rounded-full bg-muted text-sm font-bold text-foreground transition-transform group-hover:scale-[1.03]">
              {uploading ? <Loader2 className="size-4 animate-spin" /> : initials(userName)}
              <span className="absolute -bottom-0.5 -right-0.5 flex size-5 items-center justify-center rounded-full border-2 border-background bg-primary text-primary-foreground"><Plus className="size-3" /></span>
            </span>
            <span className="w-full truncate text-[11px] font-medium text-muted-foreground">Your story</span>
          </button>
          <input
            ref={fileRef}
            type="file"
            accept="image/jpeg,image/png,image/webp,image/gif,video/mp4"
            className="sr-only"
            aria-label="Choose story media"
            onChange={(event) => void addStory(event.target.files?.[0] ?? null)}
          />
          {storyItems.map((story, index) => (
            <button
              key={story.id}
              type="button"
              onClick={() => void openStory(index)}
              className="group flex w-[4.5rem] shrink-0 flex-col items-center gap-2 text-center"
              aria-label={`View ${story.authorName}'s story`}
            >
              <span
                className={`rounded-full p-[2px] ${story.isViewed ? "bg-border" : "bg-gradient-to-br from-primary via-highlight to-success"}`}
              >
                <span className="flex size-[3.25rem] items-center justify-center overflow-hidden rounded-full border-2 border-background bg-muted text-xs font-bold text-foreground transition-transform group-hover:scale-[1.03]">
                  {story.authorAvatarUrl ? (
                    <Image
                      src={story.authorAvatarUrl}
                      alt=""
                      width={52}
                      height={52}
                      unoptimized
                      className="size-[3.25rem] object-cover"
                    />
                  ) : (
                    initials(story.authorName)
                  )}
                </span>
              </span>
              <span className="w-full truncate text-[11px] font-medium text-muted-foreground">
                {story.isOwn ? "Your story" : story.authorName}
              </span>
            </button>
          ))}
        </div>
      </section>

      <Dialog open={Boolean(active)} onOpenChange={(open) => { if (!open) setActiveIndex(null); }}>
        <DialogContent
          className="overflow-hidden p-0 sm:max-w-lg"
          showCloseButton
          onKeyDown={(event) => {
            if (event.key === "ArrowLeft") moveStory(-1);
            if (event.key === "ArrowRight") moveStory(1);
          }}
        >
          {active ? (
            <>
              <div className="absolute inset-x-3 top-2 z-10 flex gap-1 pr-9" aria-hidden="true">
                {storyItems.map((story, index) => (
                  <span key={story.id} className={`h-0.5 flex-1 rounded-full ${activeIndex !== null && index <= activeIndex ? "bg-primary" : "bg-foreground/20"}`} />
                ))}
              </div>
              <DialogHeader className="px-4 pt-4 text-left">
                <DialogTitle>{active.authorName}</DialogTitle>
                <DialogDescription>@{active.authorHandle}</DialogDescription>
              </DialogHeader>
              <div className="relative flex min-h-80 items-center justify-center bg-black">
                {active.mediaType?.startsWith("video") ? <video src={active.mediaUrl ?? undefined} autoPlay controls onEnded={() => moveStory(1)} className="max-h-[70vh] w-full object-contain" /> : active.mediaUrl ? <Image src={active.mediaUrl} alt={active.caption || `Story by ${active.authorName}`} width={900} height={1200} unoptimized className="max-h-[70vh] w-full object-contain" /> : null}
                <Button type="button" variant="secondary" size="icon" aria-label="Previous story" disabled={activeIndex === 0} onClick={() => moveStory(-1)} className="absolute left-3 top-1/2 -translate-y-1/2 rounded-full bg-background/80">
                  <ChevronLeft className="size-5" />
                </Button>
                <Button type="button" variant="secondary" size="icon" aria-label="Next story" onClick={() => moveStory(1)} className="absolute right-3 top-1/2 -translate-y-1/2 rounded-full bg-background/80">
                  <ChevronRight className="size-5" />
                </Button>
              </div>
              {active.caption || active.isOwn ? (
                <div className="flex items-center gap-3 px-4 pb-4">
                  <p className="min-w-0 flex-1 text-sm text-foreground">{active.caption}</p>
                  {active.isOwn ? (
                    <Button
                      variant="ghost"
                      size="sm"
                      aria-label="Delete story"
                      onClick={async () => {
                        try {
                          await deleteStory(active.id);
                          setStoryItems((current) => current.filter((story) => story.id !== active.id));
                          setActiveIndex(null);
                          router.refresh();
                          toast.success("Story deleted.");
                        } catch (error) {
                          toast.error(
                            error instanceof Error
                              ? error.message
                              : "Story could not be deleted.",
                          );
                        }
                      }}
                    >
                      <Trash2 className="size-4" /> Delete
                    </Button>
                  ) : null}
                </div>
              ) : null}
            </>
          ) : null}
        </DialogContent>
      </Dialog>
    </>
  );
}
