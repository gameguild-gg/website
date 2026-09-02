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
      <span key={`token-${key++}`} className="font-semibold text-[#48c7ff]">
        {match[0]}
      </span>,
    );
    lastIndex = index + match[0].length;
  }
  if (lastIndex < text.length) nodes.push(text.slice(lastIndex));

  return (
    <p className="whitespace-pre-wrap break-words text-[15px] leading-6 text-slate-200">
      {nodes}
    </p>
  );
}

function EngagementButton({
  icon,
  count,
  label,
  activeClassName = "hover:text-white",
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
      className={`inline-flex min-h-9 items-center gap-2 rounded-lg px-2 text-sm text-slate-400 transition-colors hover:bg-white/[0.05] ${activeClassName}`}
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
    <div className="flex flex-col gap-3 bg-white/[0.025] px-4 py-3 sm:flex-row sm:items-center">
      <div className="flex min-w-0 flex-1 items-center gap-3">
        <span className="flex size-10 shrink-0 items-center justify-center rounded-lg border border-[#48c7ff]/20 bg-[#48c7ff]/10 text-[#7dd3fc]">
          <Gamepad2 className="size-5" aria-hidden="true" />
        </span>
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm font-semibold text-white">
            <span>{project.title}</span>
            {project.version ? (
              <span className="font-mono text-xs font-normal text-slate-400">
                {project.version}
              </span>
            ) : null}
            {project.platform ? (
              <span className="text-xs font-normal text-slate-400">
                · {project.platform}
              </span>
            ) : null}
          </p>
          {project.status ? (
            <p className="mt-0.5 text-xs font-medium text-[#49e6a2]">
              {project.status}
            </p>
          ) : null}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {project.buildHref ? (
          <Link
            href={project.buildHref}
            className="inline-flex h-9 items-center gap-2 rounded-lg border border-white/15 bg-white/[0.04] px-3 text-xs font-semibold text-slate-100 transition-colors hover:border-white/25 hover:bg-white/[0.08]"
          >
            <Play className="size-3.5 fill-current" aria-hidden="true" />
            Play build
          </Link>
        ) : null}
        {project.playtestHref ? (
          <Link
            href={project.playtestHref}
            className="inline-flex h-9 items-center gap-2 rounded-lg bg-[#49e6a2] px-3 text-xs font-bold text-[#04140e] transition-colors hover:bg-[#79efba]"
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
      className="bg-[#0d1524] py-2"
    >
      <header className="flex items-center gap-3 px-4 py-4 sm:px-6">
        <span className="rounded-full bg-gradient-to-br from-[#48c7ff] via-[#8b5cf6] to-[#49e6a2] p-[2px]">
          <span className="flex size-10 items-center justify-center rounded-full border-2 border-[#0e1422] bg-[#172033] text-xs font-bold text-white">
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
            <span className="truncate font-semibold text-white">
              {authorLabel}
            </span>
            {post.authorHandle ? (
              <span className="hidden truncate text-slate-500 sm:inline">
                {post.authorHandle}
              </span>
            ) : null}
            {post.isPinned ? (
              <Pin
                className="size-3 shrink-0 text-[#48c7ff]"
                aria-label="Pinned"
              />
            ) : null}
          </p>
          <p suppressHydrationWarning className="truncate text-xs text-slate-400">
            {post.communityLabel ? `${post.communityLabel} · ` : ""}
            {post.project?.title ? `${post.project.title} · ` : ""}
            {timeAgo(post.createdAt)}
            {post.isEdited ? " · edited" : ""}
          </p>
        </div>
        <button
          type="button"
          className="flex size-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-white/[0.05] hover:text-white"
        >
          <MoreHorizontal className="size-5" aria-hidden="true" />
          <span className="sr-only">Post options</span>
        </button>
      </header>

      {post.mediaUrl ? (
        <div className="relative aspect-[4/3] w-full overflow-hidden bg-black sm:aspect-[16/7]">
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
            activeClassName="hover:text-rose-400"
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
            activeClassName="hover:text-[#49e6a2]"
          />
          <button
            type="button"
            className="ml-auto flex size-9 items-center justify-center rounded-lg text-slate-400 transition-colors hover:bg-white/[0.05] hover:text-white"
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
                <span className="mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full bg-white/[0.07] text-[10px] font-bold text-slate-200">
                  {initials(comment.authorName, comment.id)}
                </span>
                <p className="min-w-0 flex-1 leading-5 text-slate-300">
                  <span className="mr-1.5 font-semibold text-white">
                    {comment.authorName}
                  </span>
                  {comment.content}
                </p>
                {comment.likesCount ? (
                  <span className="flex shrink-0 items-center gap-1 text-xs text-slate-500">
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
