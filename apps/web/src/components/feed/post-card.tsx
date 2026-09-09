import { Link } from "@/i18n/navigation";
import type { PostCardData } from "@/lib/posts/queries";
import {
  Bookmark,
  FlaskConical,
  Gamepad2,
  Heart,
  MessageCircle,
  MoreHorizontal,
  Pin,
  Play,
  Repeat2,
} from "lucide-react";
import Image from "next/image";

function initials(name: string | null, authorId: string): string {
  const source = name?.trim() || authorId.replace(/[-_]+/g, " ");
  if (!source) return "GG";
  const parts = source.split(/\s+/).filter(Boolean);
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return `${parts[0][0]}${parts[parts.length - 1][0]}`.toUpperCase();
}

function timeAgo(iso: string): string {
  const then = new Date(iso).getTime();
  if (Number.isNaN(then)) return "";
  const minutes = Math.round((Date.now() - then) / 60_000);
  if (minutes < 60) return `${Math.max(1, minutes)}m`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours}h`;
  const days = Math.round(hours / 24);
  if (days < 7) return `${days}d`;
  return new Date(then).toLocaleDateString();
}

const TOKEN_PATTERN = /(#[\p{L}\p{N}_]+|@[\p{L}\p{N}_]+)/gu;

function Caption({ text }: { text: string }): React.JSX.Element {
  const nodes: React.ReactNode[] = [];
  let lastIndex = 0;
  let key = 0;

  for (const match of text.matchAll(TOKEN_PATTERN)) {
    const index = match.index ?? 0;
    if (index > lastIndex) nodes.push(text.slice(lastIndex, index));
    nodes.push(
      <span key={`token-${key++}`} className="font-semibold text-primary">
        {match[0]}
      </span>,
    );
    lastIndex = index + match[0].length;
  }
  if (lastIndex < text.length) nodes.push(text.slice(lastIndex));

  return (
    <p className="whitespace-pre-wrap break-words text-[15px] leading-6 text-foreground">
      {nodes}
    </p>
  );
}

function EngagementButton({
  icon,
  count,
  label,
  activeClassName = "hover:text-foreground",
}: {
  icon: React.ReactNode;
  count: number;
  label: string;
  activeClassName?: string;
}): React.JSX.Element {
  return (
    <button
      type="button"
      aria-label={label}
      className={`inline-flex min-h-9 items-center gap-2 rounded-lg px-2 text-sm text-muted-foreground transition-colors hover:bg-accent ${activeClassName}`}
    >
      {icon}
      {count > 0 ? <span className="tabular-nums">{count}</span> : null}
    </button>
  );
}

function BuildContext({
  project,
}: {
  project: NonNullable<PostCardData["project"]>;
}): React.JSX.Element {
  return (
    <div className="flex flex-col gap-3 bg-accent/30 px-4 py-3 sm:flex-row sm:items-center">
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-lg border border-primary/20 bg-primary/10 text-primary">
          <Gamepad2 className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-semibold text-foreground">
            <span>{project.title}</span>
            {project.version ? (
              <span className="font-mono text-xs font-normal text-muted-foreground">
                {project.version}
              </span>
            ) : null}
            {project.platform ? (
              <span className="text-xs font-normal text-muted-foreground">
                · {project.platform}
              </span>
            ) : null}
          </p>
          {project.status ? (
            <p className="mt-0.5 text-xs font-medium text-success">
              {project.status}
            </p>
          ) : null}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {project.buildHref ? (
          <Link
            href={project.buildHref}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-border bg-accent/50 px-3 text-xs font-semibold text-foreground transition-colors hover:bg-accent"
          >
            <Play className="size-3.5 fill-current" aria-hidden="true" />
            Play build
          </Link>
        ) : null}
        {project.playtestHref ? (
          <Link
            href={project.playtestHref}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-success px-3 text-xs font-bold text-success-foreground transition-colors hover:bg-success/85"
          >
            <FlaskConical className="size-3.5" aria-hidden="true" />
            Join playtest
          </Link>
        ) : null}
      </div>
    </div>
  );
}

export function PostCard({ post }: { post: PostCardData }): React.JSX.Element {
  const authorLabel = post.authorName?.trim() || post.authorId.slice(0, 8);

  return (
    <article
      data-testid="post-card"
      className="bg-card py-2 text-card-foreground"
    >
      <header className="flex items-center gap-3 px-4 py-4 sm:px-6">
        <span className="rounded-full bg-gradient-to-br from-primary via-highlight to-success p-[2px]">
          <span className="flex size-10 items-center justify-center rounded-full border-2 border-background bg-muted text-xs font-bold text-foreground">
            {post.authorAvatarUrl ? (
              <Image
                src={post.authorAvatarUrl}
                alt=""
                width={40}
                height={40}
                className="size-10 rounded-full object-cover"
              />
            ) : (
              initials(post.authorName, post.authorId)
            )}
          </span>
        </span>
        <div className="min-w-0 flex-1">
          <p className="flex min-w-0 items-center gap-1.5 text-sm">
            <span className="truncate font-semibold text-foreground">
              {authorLabel}
            </span>
            {post.authorHandle ? (
              <span className="hidden truncate text-muted-foreground/70 sm:inline">
                {post.authorHandle}
              </span>
            ) : null}
            {post.isPinned ? (
              <Pin
                className="size-3 shrink-0 text-primary"
                aria-label="Pinned"
              />
            ) : null}
          </p>
          <p suppressHydrationWarning className="truncate text-xs text-muted-foreground">
            {post.communityLabel ? `${post.communityLabel} · ` : ""}
            {post.project?.title ? `${post.project.title} · ` : ""}
            {timeAgo(post.createdAt)}
            {post.isEdited ? " · edited" : ""}
          </p>
        </div>
        <button
          type="button"
          className="flex size-9 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
        >
          <MoreHorizontal className="size-5" aria-hidden="true" />
          <span className="sr-only">Post options</span>
        </button>
      </header>

      {post.mediaUrl ? (
        <div className="relative aspect-[4/3] w-full overflow-hidden bg-background sm:aspect-[16/7]">
          <Image
            src={post.mediaUrl}
            alt={`${post.project?.title ?? "Community"} preview shared by ${authorLabel}`}
            fill
            priority={post.id === "demo-neon-rift"}
            className="object-cover"
            sizes="(min-width: 1280px) 47.5rem, 100vw"
          />
        </div>
      ) : null}

      {post.project ? <BuildContext project={post.project} /> : null}

      <div className="px-4 pt-4 sm:px-6">
        <div className="flex items-center gap-1">
          <EngagementButton
            icon={<Heart className="size-[19px]" aria-hidden="true" />}
            count={post.likesCount}
            label={`${post.likesCount} likes`}
            activeClassName="hover:text-destructive"
          />
          <EngagementButton
            icon={<MessageCircle className="size-[19px]" aria-hidden="true" />}
            count={post.commentsCount}
            label={`${post.commentsCount} comments`}
          />
          <EngagementButton
            icon={<Repeat2 className="size-[19px]" aria-hidden="true" />}
            count={post.sharesCount}
            label={`${post.sharesCount} reposts`}
            activeClassName="hover:text-success"
          />
          <button
            type="button"
            className="ml-auto flex size-9 items-center justify-center rounded-lg text-muted-foreground transition-colors hover:bg-accent hover:text-foreground"
          >
            <Bookmark className="size-[19px]" aria-hidden="true" />
            <span className="sr-only">Save post</span>
          </button>
        </div>
        <div className="mt-2">
          <Caption text={post.content} />
        </div>
      </div>

      {post.commentPreview?.length ? (
        <div className="mt-4 px-4 py-3 sm:px-6">
          <div className="space-y-3">
            {post.commentPreview.map((comment) => (
              <div key={comment.id} className="flex items-start gap-3 text-sm">
                <span className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full bg-accent text-[10px] font-bold text-accent-foreground">
                  {initials(comment.authorName, comment.id)}
                </span>
                <p className="min-w-0 flex-1 leading-5 text-muted-foreground">
                  <span className="mr-1.5 font-semibold text-foreground">
                    {comment.authorName}
                  </span>
                  {comment.content}
                </p>
                {comment.likesCount ? (
                  <span className="flex shrink-0 items-center gap-1 text-xs text-muted-foreground/70">
                    <Heart className="size-3" aria-hidden="true" />{" "}
                    {comment.likesCount}
                  </span>
                ) : null}
              </div>
            ))}
          </div>
        </div>
      ) : (
        <div className="h-4" />
      )}
    </article>
  );
}
