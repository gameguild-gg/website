"use client";

import { createSocialPost, getSocialMediaStatus, hydrateSocialPost } from "@/lib/feed/actions";
import type { SocialMediaAsset, SocialPostItem, SocialPostPublicationResult } from "@/lib/feed/contracts";
import { uploadSocialMediaWithProgress } from "@/lib/feed/social-media-upload";
import { Button } from "@game-guild/ui/components/button";
import { Textarea } from "@game-guild/ui/components/textarea";
import { Loader2, Send, X } from "lucide-react";
import * as React from "react";
import { toast } from "sonner";
import { SocialMediaPicker, type SelectedSocialMedia } from "./social-media-picker";

const CHARACTER_LIMIT = 4000;

function initials(name: string) { return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase(); }
function tagsFrom(content: string) { return [...new Set([...content.matchAll(/#([\p{L}\p{N}_]+)/gu)].map((match) => match[1]!.toLowerCase()))].slice(0, 10); }

async function waitUntilReady(asset: SocialMediaAsset, signal: AbortSignal, processing: () => void) {
  let current = asset;
  if (current.state === "Processing") processing();
  for (let attempt = 0; current.state === "Processing" && attempt < 20; attempt += 1) {
    if (signal.aborted) throw new Error("Media upload cancelled.");
    await new Promise((resolve) => setTimeout(resolve, Math.min(500 + attempt * 250, 2_000)));
    if (signal.aborted) throw new Error("Media upload cancelled.");
    current = await getSocialMediaStatus(current.assetReferenceId);
  }
  if (current.state === "Rejected") throw new Error("The media file was rejected during processing.");
  if (current.state !== "Ready") throw new Error("Media processing timed out. Please try again.");
  return current;
}

export function SocialComposer({ userName, onPublished }: { userName: string; onPublished?: (post: SocialPostItem) => void }): React.JSX.Element {
  const [expanded, setExpanded] = React.useState(false);
  const [content, setContent] = React.useState("");
  const [media, setMedia] = React.useState<SelectedSocialMedia | null>(null);
  const [pending, setPending] = React.useState(false);
  const [phase, setPhase] = React.useState<"idle" | "uploading" | "processing" | "failed">("idle");
  const [progress, setProgress] = React.useState<{ loaded: number; total: number; percent: number } | null>(null);
  const [failure, setFailure] = React.useState<string | null>(null);
  const [committedPostId, setCommittedPostId] = React.useState<string | null>(null);
  const pendingRef = React.useRef(false);
  const controllerRef = React.useRef<AbortController | null>(null);
  const publishedIdsRef = React.useRef(new Set<string>());

  React.useEffect(() => { const open = () => setExpanded(true); window.addEventListener("social:compose", open); return () => window.removeEventListener("social:compose", open); }, []);
  React.useEffect(() => () => controllerRef.current?.abort(), []);

  async function publish(event?: React.FormEvent) {
    event?.preventDefault();
    if (pendingRef.current) return;
    const text = content.trim();
    if (text.length > CHARACTER_LIMIT) { setFailure(`Posts can be up to ${CHARACTER_LIMIT} characters.`); return; }
    if (!text && !media) return;
    pendingRef.current = true; setPending(true); setFailure(null);
    const controller = new AbortController(); controllerRef.current = controller;
    try {
      let publication: SocialPostPublicationResult;
      if (committedPostId) {
        publication = { kind: "published", post: await hydrateSocialPost(committedPostId) };
      } else {
        let assetReferenceId: string | null = null;
        if (media) {
          setPhase("uploading");
          const uploaded = await uploadSocialMediaWithProgress(media.file, { signal: controller.signal, onProgress: setProgress });
          assetReferenceId = (await waitUntilReady(uploaded, controller.signal, () => setPhase("processing"))).assetReferenceId;
        }
        publication = await createSocialPost({ content: text, visibility: "Public", assetReferenceId, tags: tagsFrom(text) });
      }
      if (publication.kind === "needs-hydration") {
        const message = "Your post was published. Retry to add it to the feed.";
        setCommittedPostId(publication.postId);
        setFailure(message);
        setPhase("failed");
        toast.error(message);
        return;
      }
      const post = publication.post;
      if (!publishedIdsRef.current.has(post.id)) { publishedIdsRef.current.add(post.id); onPublished?.(post); }
    setContent(""); setMedia(null); setProgress(null); setPhase("idle"); setCommittedPostId(null); setExpanded(false); toast.success("Post published.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "The post could not be published.";
      setFailure(message); setPhase("failed"); toast.error(message);
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
      pendingRef.current = false; setPending(false);
    }
  }

  if (!expanded) return <section id="social-composer" className="px-4 py-3 sm:px-6"><div className="flex items-center gap-3 rounded-xl bg-card p-2.5 text-card-foreground"><span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/15 text-xs font-bold text-foreground">{initials(userName)}</span><button type="button" aria-label="Share your progress" onClick={() => setExpanded(true)} className="min-w-0 flex-1 rounded-lg px-2 py-2 text-left text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground">Share your progress…</button></div></section>;
  return <section id="social-composer" className="px-4 py-4 sm:px-6"><form onSubmit={publish} className="rounded-xl bg-card p-4 text-card-foreground"><div className="flex items-start gap-3"><span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/15 text-xs font-bold text-foreground">{initials(userName)}</span><Textarea autoFocus rows={3} maxLength={CHARACTER_LIMIT} value={content} onChange={(event) => setContent(event.target.value)} placeholder="What are you building?" aria-describedby="social-composer-character-count" className="min-h-20 flex-1 resize-none border-0 bg-transparent px-1 py-1 text-sm shadow-none focus-visible:ring-0" /><Button type="button" variant="ghost" size="icon-sm" disabled={pending} onClick={() => setExpanded(false)} aria-label="Close composer"><X className="size-4" /></Button></div><div className="ml-12 mt-3"><SocialMediaPicker value={media} onChange={setMedia} disabled={pending} /></div>{progress ? <div className="ml-12 mt-2" aria-live="polite"><div role="progressbar" aria-label="Media upload progress" aria-valuemin={0} aria-valuemax={100} aria-valuenow={progress.percent} className="h-2 overflow-hidden rounded-full bg-primary/20"><div className="h-full bg-primary" style={{ width: `${progress.percent}%` }} /></div><p className="mt-1 text-xs text-muted-foreground">Uploading {progress.loaded} of {progress.total} bytes ({progress.percent}%)</p></div> : null}{phase === "processing" ? <p className="ml-12 mt-2 text-xs text-muted-foreground" aria-live="polite">Processing media…</p> : null}{failure ? <div className="ml-12 mt-2 flex items-center gap-2" role="alert"><p className="text-xs text-destructive">{failure}</p>{media || committedPostId ? <Button type="button" variant="ghost" size="sm" onClick={() => void publish()}>{committedPostId ? "Retry feed update" : "Retry upload"}</Button> : null}</div> : null}<div className="ml-12 mt-3 flex items-center justify-between gap-3"><span id="social-composer-character-count" className="text-xs text-muted-foreground" aria-live="polite">{content.length}/{CHARACTER_LIMIT} characters</span><div className="flex gap-2">{pending ? <Button type="button" variant="ghost" size="sm" onClick={() => controllerRef.current?.abort()}>Cancel upload</Button> : null}<Button type="submit" size="sm" disabled={pending || ((!content.trim() && !media) && !committedPostId)} aria-label="Publish">{pending ? <Loader2 className="size-4 animate-spin" /> : <Send className="size-4" />}{pending ? "Publishing…" : "Publish"}</Button></div></div></form></section>;
}
