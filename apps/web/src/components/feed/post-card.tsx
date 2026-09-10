"use client";

import { Link } from "@/i18n/navigation";
import {
  createPostComment,
  deletePostComment,
  deleteSocialPost,
  loadPostCommentsAction,
  recordPostView,
  repostPost,
  savePost,
  setPostReaction,
  sharePost,
  updateSocialPost,
  updatePostComment,
} from "@/lib/feed/actions";
import type {
  PostComment,
  SocialFeedItem,
  SocialReaction,
} from "@/lib/feed/contracts";
import { Button } from "@game-guild/ui/components/button";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@game-guild/ui/components/alert-dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@game-guild/ui/components/dropdown-menu";
import { Textarea } from "@game-guild/ui/components/textarea";
import {
  Bookmark,
  CalendarDays,
  CheckCircle2,
  ChevronDown,
  Clock3,
  Heart,
  Loader2,
  MessageCircle,
  MoreHorizontal,
  Pencil,
  Repeat2,
  Send,
  Share2,
  Trash2,
  Users,
} from "lucide-react";
import Image from "next/image";
import * as React from "react";
import { toast } from "sonner";
import { formatSocialDate, formatSocialDateTime } from "@/lib/feed/format";

const REACTIONS: Array<{
  value: SocialReaction;
  label: string;
  symbol: string;
}> = [
  { value: "Like", label: "Like", symbol: "👍" },
  { value: "Love", label: "Love", symbol: "❤️" },
  { value: "Insightful", label: "Insightful", symbol: "💡" },
  { value: "Celebrate", label: "Celebrate", symbol: "🎉" },
  { value: "Support", label: "Support", symbol: "🙌" },
  { value: "Curious", label: "Curious", symbol: "🤔" },
];

function initials(name: string | null | undefined, id: string | null | undefined) {
  const parts = (name?.trim() || id?.replace(/[-_]+/g, " ") || "GG")
    .split(/\s+/)
    .filter(Boolean);
  if (parts.length > 1) {
    return `${parts[0]?.[0] ?? ""}${parts.at(-1)?.[0] ?? ""}`.toUpperCase();
  }
  return parts[0]?.slice(0, 2).toUpperCase() || "GG";
}

function timeAgo(iso: string) {
  const milliseconds = new Date(iso).getTime();
  if (Number.isNaN(milliseconds)) return "";
  const minutes = Math.max(
    1,
    Math.round((Date.now() - milliseconds) / 60_000),
  );
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.round(hours / 24);
  return days < 7
    ? `${days}d`
    : formatSocialDate(milliseconds);
}

const TOKEN_PATTERN = /(#[\p{L}\p{N}_]+|@[\p{L}\p{N}_]+)/gu;

function Caption({ text }: { text: string }) {
  const nodes: React.ReactNode[] = [];
  let last = 0;
  for (const [key, match] of [...text.matchAll(TOKEN_PATTERN)].entries()) {
    const start = match.index ?? 0;
    if (start > last) nodes.push(text.slice(last, start));
    nodes.push(
      <span key={key} className="font-semibold text-primary">
        {match[0]}
      </span>,
    );
    last = start + match[0].length;
  }
  if (last < text.length) nodes.push(text.slice(last));
  return (
    <p className="whitespace-pre-wrap break-words text-[15px] leading-6 text-foreground">
      {nodes}
    </p>
  );
}

function countComments(comments: PostComment[]): number {
  return comments.reduce(
    (total, comment) => total + 1 + countComments(comment.replies ?? []),
    0,
  );
}

function Avatar({
  name,
  id,
  url,
}: {
  name: string;
  id: string;
  url: string | null;
}) {
  return (
    <span className="flex size-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-muted text-xs font-bold text-foreground">
      {url ? (
        <Image
          src={url}
          alt=""
          width={40}
          height={40}
          unoptimized
          className="size-10 object-cover"
        />
      ) : (
        initials(name, id)
      )}
    </span>
  );
}

function TestingSessionCard({ item }: { item: SocialFeedItem }) {
  const session = item.testingSession;
  if (!session) return null;
  const start = new Date(session.startsAt);
  return (
    <article
      data-testid="post-card"
      className="bg-card px-4 py-5 text-card-foreground sm:px-6"
    >
      <div className="flex items-start gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-primary/12 text-primary">
          <CalendarDays className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-semibold uppercase tracking-wide text-primary">
            Testing Lab
          </p>
          <h2 className="mt-1 text-base font-semibold text-foreground">
            {session.name}
          </h2>
          <div className="mt-3 flex flex-wrap gap-x-5 gap-y-2 text-sm text-muted-foreground">
            <span className="inline-flex items-center gap-1.5">
              <Clock3 className="size-4" aria-hidden="true" />
              {formatSocialDateTime(start)}
            </span>
            <span className="inline-flex items-center gap-1.5">
              <Users className="size-4" aria-hidden="true" />
              {session.availableTesterCount} spots available
            </span>
          </div>
        </div>
        <Button asChild size="sm">
          <Link href={`/testing-lab/events/${item.id}`}>View session</Link>
        </Button>
      </div>
    </article>
  );
}

function CommentList({
  comments,
  currentUserId,
  onReply,
  onReload,
  postId,
}: {
  comments: PostComment[];
  currentUserId?: string | null;
  onReply: (comment: PostComment) => void;
  onReload: () => Promise<void>;
  postId: string;
}) {
  const [editingId, setEditingId] = React.useState<string | null>(null);
  const [editValue, setEditValue] = React.useState("");
  const [editingPending, setEditingPending] = React.useState(false);

  async function saveComment(comment: PostComment) {
    const value = editValue.trim();
    if (!value || editingPending) return;
    setEditingPending(true);
    try {
      await updatePostComment(postId, comment.id, value);
      await onReload();
      setEditingId(null);
      setEditValue("");
    } catch (error) {
      toast.error(error instanceof Error ? error.message : "Comment could not be updated.");
    } finally {
      setEditingPending(false);
    }
  }

  return (
    <div className="space-y-4">
      {comments.map((comment) => (
        <div key={comment.id} className="flex gap-3">
          <Avatar
            name={comment.authorName}
            id={comment.authorId}
            url={comment.authorAvatarUrl}
          />
          <div className="min-w-0 flex-1">
            <div className="rounded-xl bg-accent/45 px-3 py-2.5">
              <p className="text-xs font-semibold text-foreground">
                {comment.authorName}
              </p>
              {editingId === comment.id ? (
                <div className="mt-2 space-y-2">
                  <Textarea
                    aria-label="Edit comment"
                    rows={2}
                    maxLength={2000}
                    autoFocus
                    value={editValue}
                    onChange={(event) => setEditValue(event.target.value)}
                  />
                  <div className="flex justify-end gap-2">
                    <Button type="button" size="xs" variant="ghost" disabled={editingPending} onClick={() => setEditingId(null)}>
                      Cancel
                    </Button>
                    <Button type="button" size="xs" disabled={editingPending || !editValue.trim()} onClick={() => void saveComment(comment)}>
                      Save
                    </Button>
                  </div>
                </div>
              ) : (
                <p className="mt-0.5 whitespace-pre-wrap text-sm leading-5 text-foreground">
                  {comment.content}
                </p>
              )}
            </div>
            <button
              type="button"
              onClick={() => onReply(comment)}
              className="mt-1 px-2 text-xs text-muted-foreground hover:text-foreground"
            >
              Reply
            </button>
            {currentUserId === comment.authorId ? (
              <>
                <button
                  type="button"
                  className="mt-1 px-2 text-xs text-muted-foreground hover:text-foreground"
                  onClick={() => {
                    setEditingId(comment.id);
                    setEditValue(comment.content);
                  }}
                >
                  Edit
                </button>
                <AlertDialog>
                  <AlertDialogTrigger asChild>
                    <button type="button" className="mt-1 px-2 text-xs text-muted-foreground hover:text-destructive">
                      Delete
                    </button>
                  </AlertDialogTrigger>
                  <AlertDialogContent>
                    <AlertDialogHeader>
                      <AlertDialogTitle>Delete comment?</AlertDialogTitle>
                      <AlertDialogDescription>This permanently removes your comment and its replies.</AlertDialogDescription>
                    </AlertDialogHeader>
                    <AlertDialogFooter>
                      <AlertDialogCancel>Cancel</AlertDialogCancel>
                      <AlertDialogAction
                        onClick={async () => {
                          try {
                            await deletePostComment(postId, comment.id);
                            await onReload();
                          } catch (error) {
                            toast.error(error instanceof Error ? error.message : "Comment could not be deleted.");
                          }
                        }}
                      >
                        Delete
                      </AlertDialogAction>
                    </AlertDialogFooter>
                  </AlertDialogContent>
                </AlertDialog>
              </>
            ) : null}
            {(comment.replies ?? []).length > 0 ? (
              <div className="mt-3 border-l border-border/50 pl-3">
                <CommentList
                  comments={comment.replies ?? []}
                  currentUserId={currentUserId}
                  onReply={onReply}
                  onReload={onReload}
                  postId={postId}
                />
              </div>
            ) : null}
          </div>
        </div>
      ))}
    </div>
  );
}

export function PostCard({
  item,
  currentUserId,
}: {
  item: SocialFeedItem;
  currentUserId?: string | null;
}): React.JSX.Element {
  const [reaction, setReactionState] = React.useState(item.viewer.reaction);
  const [reactionCount, setReactionCount] = React.useState(
    item.engagement.reactionsCount,
  );
  const [saved, setSaved] = React.useState(item.viewer.isSaved);
  const [reposted, setReposted] = React.useState(item.viewer.hasReposted);
  const [repostCount, setRepostCount] = React.useState(item.engagement.repostsCount);
  const [commentCount, setCommentCount] = React.useState(item.engagement.commentsCount);
  const [commentsOpen, setCommentsOpen] = React.useState(false);
  const [comments, setComments] = React.useState<PostComment[]>([]);
  const [commentsLoading, setCommentsLoading] = React.useState(false);
  const [commentText, setCommentText] = React.useState("");
  const [replyingTo, setReplyingTo] = React.useState<PostComment | null>(null);
  const [pending, startTransition] = React.useTransition();
  const [editing, setEditing] = React.useState(false);
  const [content, setContent] = React.useState(item.post?.content ?? "");
  const [editContent, setEditContent] = React.useState(content);
  const [reactionPending, setReactionPending] = React.useState(false);
  const [savePending, setSavePending] = React.useState(false);
  const [repostPending, setRepostPending] = React.useState(false);
  const [commentPending, setCommentPending] = React.useState(false);
  const [deleted, setDeleted] = React.useState(false);
  const [deleteOpen, setDeleteOpen] = React.useState(false);
  const repostPendingRef = React.useRef(false);
  const articleRef = React.useRef<HTMLElement>(null);

  React.useEffect(() => {
    if (item.kind === "TestingSession" || typeof IntersectionObserver === "undefined") return;
    const node = articleRef.current;
    if (!node) return;
    let recorded = false;
    const observer = new IntersectionObserver(
      (entries) => {
        if (recorded || !entries.some((entry) => entry.isIntersecting && entry.intersectionRatio >= 0.5)) return;
        recorded = true;
        observer.disconnect();
        void recordPostView(item.id).catch(() => {
          // Analytics must never interrupt reading the feed.
        });
      },
      { threshold: 0.5 },
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [item.id, item.kind]);

  if (item.kind === "TestingSession") {
    return <TestingSessionCard item={item} />;
  }
  const post = item.post;
  if (!post || deleted) return <></>;

  async function react(next: SocialReaction | null) {
    if (reactionPending) return;
    const previous = reaction;
    const previousCount = reactionCount;
    setReactionState(next);
    setReactionCount((count) =>
      Math.max(0, count + (previous ? -1 : 0) + (next ? 1 : 0)),
    );
    setReactionPending(true);
    try {
      await setPostReaction(item.id, next);
    } catch (error) {
      setReactionState(previous);
      setReactionCount(previousCount);
      toast.error(
        error instanceof Error ? error.message : "Reaction could not be saved.",
      );
    } finally {
      setReactionPending(false);
    }
  }

  async function toggleComments() {
    const opening = !commentsOpen;
    setCommentsOpen(opening);
    if (!opening || comments.length > 0) return;
    setCommentsLoading(true);
    try {
      setComments(await loadPostCommentsAction(item.id));
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : "Comments could not be loaded.",
      );
    } finally {
      setCommentsLoading(false);
    }
  }

  async function reloadComments() {
    const nextComments = await loadPostCommentsAction(item.id);
    setComments(nextComments);
    setCommentCount(countComments(nextComments));
  }

  async function submitComment(event: React.FormEvent) {
    event.preventDefault();
    if (commentPending) return;
    const value = commentText.trim();
    if (!value) return;
    setCommentPending(true);
    try {
      await createPostComment(item.id, {
        content: value,
        ...(replyingTo ? { parentCommentId: replyingTo.id } : {}),
      });
      await reloadComments();
      setCommentText("");
      setReplyingTo(null);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : "Comment could not be published.",
      );
    } finally {
      setCommentPending(false);
    }
  }

  async function repost() {
    if (repostPendingRef.current || reposted) return;
    repostPendingRef.current = true;
    setRepostPending(true);
    setReposted(true);
    setRepostCount((count) => count + 1);
    try {
      await repostPost(item.id);
      toast.success("Reposted.");
    } catch (error) {
      setReposted(false);
      setRepostCount((count) => Math.max(0, count - 1));
      toast.error(error instanceof Error ? error.message : "Post could not be reposted.");
    } finally {
      repostPendingRef.current = false;
      setRepostPending(false);
    }
  }

  async function toggleSave() {
    if (savePending) return;
    const previous = saved;
    const next = !previous;
    setSaved(next);
    setSavePending(true);
    try {
      const state = await savePost(item.id, next);
      setSaved(state.isSaved);
    } catch (error) {
      setSaved(previous);
      toast.error(error instanceof Error ? error.message : "Post could not be saved.");
    } finally {
      setSavePending(false);
    }
  }

  async function share() {
    try {
      await sharePost(item.id);
      const url = `${window.location.origin}/social/posts/${item.id}`;
      if (navigator.share) {
        await navigator.share({
          title: `${item.author.displayName} on GameGuild`,
          url,
        });
      } else {
        await navigator.clipboard.writeText(url);
        toast.success("Post link copied.");
      }
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : "Post could not be shared.",
      );
    }
  }

  const selectedReaction = REACTIONS.find(
    (entry) => entry.value === reaction,
  );

  return (
    <article
      ref={articleRef}
      data-testid="post-card"
      className="bg-card py-2 text-card-foreground"
    >
      <header className="flex items-center gap-3 px-4 py-4 sm:px-6">
        <Link
          href={`/social/profiles/${item.author.handle || item.author.userId}`}
          className="rounded-full bg-gradient-to-br from-primary via-highlight to-success p-[2px]"
        >
          <span className="block rounded-full border-2 border-background">
            <Avatar
              name={item.author.displayName}
              id={item.author.userId}
              url={item.author.avatarUrl}
            />
          </span>
        </Link>
        <div className="min-w-0 flex-1">
          <p className="flex items-center gap-1.5 text-sm">
            <span className="truncate font-semibold text-foreground">
              {item.author.displayName}
            </span>
            {item.author.isVerified ? (
              <CheckCircle2
                className="size-3.5 shrink-0 text-primary"
                aria-label="Verified"
              />
            ) : null}
            <span className="hidden truncate text-muted-foreground sm:inline">
              @{item.author.handle}
            </span>
          </p>
          <p
            suppressHydrationWarning
            className="text-xs text-muted-foreground"
          >
            {timeAgo(item.createdAt)}
            {post.isEdited ? " · edited" : ""}
          </p>
        </div>
        {item.viewer.canEdit || item.viewer.canDelete ? (
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon-sm" aria-label="Post options">
                <MoreHorizontal className="size-5" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {item.viewer.canEdit ? (
                <DropdownMenuItem onClick={() => setEditing(true)}>
                  <Pencil /> Edit post
                </DropdownMenuItem>
              ) : null}
              {item.viewer.canEdit && item.viewer.canDelete ? (
                <DropdownMenuSeparator />
              ) : null}
              {item.viewer.canDelete ? (
                <DropdownMenuItem
                  variant="destructive"
                  onClick={() => setDeleteOpen(true)}
                >
                  <Trash2 /> Delete post
                </DropdownMenuItem>
              ) : null}
            </DropdownMenuContent>
          </DropdownMenu>
        ) : null}
      </header>

      {post.mediaUrl ? (
        post.mediaType?.toLowerCase().startsWith("video") ? (
          <video
            src={post.mediaUrl}
            controls
            preload="metadata"
            className="max-h-[620px] w-full bg-background object-contain"
          />
        ) : (
          <div className="relative aspect-[4/3] max-h-[620px] w-full overflow-hidden bg-background">
            <Image
              src={post.mediaUrl}
              alt={`Media shared by ${item.author.displayName}`}
              fill
              unoptimized
              className="object-cover"
              sizes="(min-width: 1280px) 47.5rem, 100vw"
            />
          </div>
        )
      ) : null}

      <div className="px-4 pt-4 sm:px-6">
        {editing ? (
          <form
            className="space-y-2"
            onSubmit={(event) => {
              event.preventDefault();
              const value = editContent.trim();
              if (!value) return;
              startTransition(async () => {
                try {
                  await updateSocialPost(item.id, value);
                  setContent(value);
                  setEditing(false);
                  toast.success("Post updated.");
                } catch (error) {
                  toast.error(
                    error instanceof Error
                      ? error.message
                      : "Post could not be updated.",
                  );
                }
              });
            }}
          >
            <Textarea
              value={editContent}
              onChange={(event) => setEditContent(event.target.value)}
              aria-label="Edit post"
            />
            <div className="flex justify-end gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setEditing(false)}
              >
                Cancel
              </Button>
              <Button type="submit" size="sm" disabled={pending}>
                Save
              </Button>
            </div>
          </form>
        ) : (
          <Caption text={content} />
        )}

        {post.repostedPost ? (
          <div className="mt-3 rounded-xl bg-accent/40 p-3">
            <p className="text-xs font-semibold text-foreground">
              {post.repostedPost.author.displayName}{" "}
              <span className="font-normal text-muted-foreground">
                @{post.repostedPost.author.handle}
              </span>
            </p>
            <p className="mt-1 text-sm text-foreground">
              {post.repostedPost.content}
            </p>
          </div>
        ) : null}

        {item.tags.length > 0 ? (
          <div className="mt-3 flex flex-wrap gap-2">
            {item.tags.map((tag) => (
              <Link
                key={tag}
                href={`/?tag=${encodeURIComponent(tag)}`}
                className="text-xs font-medium text-primary"
              >
                #{tag}
              </Link>
            ))}
          </div>
        ) : null}

        <div className="mt-3 flex items-center gap-1">
          <button
                        type="button"
            onClick={() => void react(reaction ? null : "Like")}
            disabled={reactionPending}
            aria-label={reaction ? "Remove reaction" : "React to post"}
            aria-pressed={Boolean(reaction)}
            className={`inline-flex min-h-9 items-center gap-2 rounded-lg px-2 text-sm transition hover:bg-accent ${reaction ? "text-destructive" : "text-muted-foreground hover:text-foreground"}`}
          >
            {selectedReaction ? (
              <span aria-hidden="true">{selectedReaction.symbol}</span>
            ) : (
              <Heart className="size-[19px]" />
            )}
            {reactionCount > 0 ? reactionCount : null}
          </button>
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <button
                type="button"
                aria-label="Choose reaction"
                className="flex size-7 items-center justify-center rounded-md text-muted-foreground hover:bg-accent"
              >
                <ChevronDown className="size-3.5" />
              </button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="start" className="flex min-w-0 gap-1 p-2">
              {REACTIONS.map((entry) => (
                <DropdownMenuItem
                  key={entry.value}
                  onClick={() => void react(entry.value)}
                  disabled={reactionPending}
                  className="flex size-10 justify-center p-0 text-lg"
                  aria-label={entry.label}
                >
                  {entry.symbol}
                </DropdownMenuItem>
              ))}
            </DropdownMenuContent>
          </DropdownMenu>
          <button
            type="button"
            onClick={() => void toggleComments()}
            aria-label={`${commentCount} comments`}
            className="inline-flex min-h-9 items-center gap-2 rounded-lg px-2 text-sm text-muted-foreground hover:bg-accent hover:text-foreground"
          >
            <MessageCircle className="size-[19px]" />
            {commentCount > 0
              ? commentCount
              : null}
          </button>
          <button
            type="button"
            onClick={() => void repost()}
            disabled={repostPending || reposted}
            aria-label="Repost"
            aria-pressed={reposted}
            className={`inline-flex min-h-9 items-center gap-2 rounded-lg px-2 text-sm hover:bg-accent disabled:opacity-70 ${reposted ? "text-success" : "text-muted-foreground hover:text-success"}`}
          >
            <Repeat2 className="size-[19px]" />
            {repostCount > 0
              ? repostCount
              : null}
          </button>
          <button
            type="button"
            onClick={() => void share()}
            aria-label="Share post"
            className="flex size-9 items-center justify-center rounded-lg text-muted-foreground hover:bg-accent hover:text-foreground"
          >
            <Share2 className="size-[19px]" />
          </button>
          <button
            type="button"
            onClick={() => void toggleSave()}
            disabled={savePending}
                        aria-label={saved ? "Remove saved post" : "Save post"}
            aria-pressed={saved}
            className={`ml-auto flex size-9 items-center justify-center rounded-lg hover:bg-accent ${saved ? "text-primary" : "text-muted-foreground hover:text-foreground"}`}
          >
            <Bookmark
              className={`size-[19px] ${saved ? "fill-current" : ""}`}
            />
          </button>
        </div>
      </div>

      {commentsOpen ? (
        <section
          aria-label="Comments"
          className="mt-2 bg-accent/15 px-4 py-4 sm:px-6"
        >
          {commentsLoading ? (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" /> Loading comments…
            </p>
          ) : (
            <CommentList
              comments={comments}
              currentUserId={currentUserId}
              onReply={setReplyingTo}
              onReload={reloadComments}
              postId={item.id}
            />
          )}
          <form onSubmit={submitComment} className="mt-4 flex items-end gap-2">
            <div className="min-w-0 flex-1">
              {replyingTo ? (
                <p className="mb-1 text-xs text-muted-foreground">
                  Replying to {replyingTo.authorName}
                  <button
                    type="button"
                    onClick={() => setReplyingTo(null)}
                    className="ml-1 text-primary"
                  >
                    Cancel
                  </button>
                </p>
              ) : null}
              <Textarea
                value={commentText}
                onChange={(event) => setCommentText(event.target.value)}
                rows={1}
                maxLength={2000}
                placeholder="Add a comment…"
                className="min-h-10 resize-none"
              />
            </div>
            <Button
              type="submit"
              size="icon"
              disabled={commentPending || !commentText.trim()}
              aria-label="Publish comment"
            >
              <Send className="size-4" />
            </Button>
          </form>
        </section>
      ) : null}
      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete post?</AlertDialogTitle>
            <AlertDialogDescription>This permanently removes the post and its discussion.</AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              disabled={pending}
              onClick={() => {
                startTransition(async () => {
                  try {
                    await deleteSocialPost(item.id);
                    setDeleted(true);
                    setDeleteOpen(false);
                    toast.success("Post deleted.");
                  } catch (error) {
                    toast.error(error instanceof Error ? error.message : "Post could not be deleted.");
                  }
                });
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </article>
  );
}
