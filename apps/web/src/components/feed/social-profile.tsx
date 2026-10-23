"use client";

import { cn } from "@game-guild/ui/lib/utils";
import { Link } from "@/i18n/navigation";
import { followCreator } from "@/lib/feed/actions";
import type {
  SocialProfile,
  SocialProfilePost,
  SocialProfileProject,
} from "@/lib/feed/contracts";
import { Button, buttonVariants } from "@game-guild/ui/components/button";
import {
  Tabs,
  TabsContent,
  TabsList,
  TabsTrigger,
} from "@game-guild/ui/components/tabs";
import {
  ArrowLeft,
  BadgeCheck,
  ExternalLink,
  FolderKanban,
  Link2,
  MapPin,
  MessageSquareText,
  Pencil,
} from "lucide-react";
import Image from "next/image";
import { useLocale, useTranslations } from "next-intl";
import * as React from "react";
import { toast } from "sonner";

function initials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase();
}

export function SocialProfileView({
  profile,
  currentUserId,
  posts,
  projects,
  collectionsFailed,
}: {
  profile: SocialProfile;
  currentUserId: string | null;
  posts: SocialProfilePost[];
  projects: SocialProfileProject[];
  collectionsFailed?: boolean;
}): React.JSX.Element {
  const t = useTranslations("socialProfile");
  const locale = useLocale();
  const [following, setFollowing] = React.useState(profile.isFollowing);
  const [followerCount, setFollowerCount] = React.useState(profile.followerCount);
  const [pending, setPending] = React.useState(false);
  const isOwn = profile.userId === currentUserId;

  const dateFormatter = React.useMemo(() => {
    try {
      return new Intl.DateTimeFormat(locale, { year: "numeric", month: "short", day: "numeric" });
    } catch {
      return new Intl.DateTimeFormat("en-US", { year: "numeric", month: "short", day: "numeric" });
    }
  }, [locale]);

  async function toggleFollow() {
    if (pending) return;
    const previous = following;
    const previousFollowerCount = followerCount;
    const next = !previous;
    setFollowing(next);
    setFollowerCount(Math.max(0, previousFollowerCount + (next ? 1 : -1)));
    setPending(true);
    try {
      const state = await followCreator(profile.userId, next);
      setFollowing(state.isFollowing);
      setFollowerCount(
        Math.max(
          0,
          previousFollowerCount +
            (state.isFollowing === previous ? 0 : state.isFollowing ? 1 : -1),
        ),
      );
    } catch (error) {
      setFollowing(previous);
      setFollowerCount(previousFollowerCount);
      toast.error(error instanceof Error ? error.message : t("followFailed"));
    } finally {
      setPending(false);
    }
  }

  async function copyProfileLink() {
    try {
      await navigator.clipboard.writeText(window.location.href);
      toast.success(t("linkCopied"));
    } catch {
      toast.error(t("linkCopyFailed"));
    }
  }

  const stats: Array<{ label: string; value: number }> = [
    { label: t("stats.posts"), value: profile.postCount },
    { label: t("stats.projects"), value: profile.projectCount },
    { label: t("stats.followers"), value: followerCount },
    { label: t("stats.following"), value: profile.followingCount },
  ];

  return (
    <main className="mx-auto min-h-[calc(100svh-4rem)] w-full max-w-[820px] bg-background pb-16 text-foreground">
      <div className="flex items-center justify-between gap-2 px-4 py-4 sm:px-6">
        <Link
          href="/"
          className={cn(buttonVariants({ variant: "ghost", size: "sm" }))}
        >
          <ArrowLeft className="size-4" /> {t("backToFeed")}
        </Link>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => void copyProfileLink()}
        >
          <Link2 className="size-4" aria-hidden />
          {t("share")}
        </Button>
      </div>

      <section className="mx-4 overflow-hidden rounded-2xl border bg-card text-card-foreground shadow-sm sm:mx-6">
        <div className="relative h-40 bg-accent sm:h-52">
          {profile.bannerUrl ? (
            <Image src={profile.bannerUrl} alt="" fill unoptimized className="object-cover" sizes="820px" />
          ) : (
            <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,color-mix(in_oklab,var(--primary)_30%,transparent),transparent_55%)]" />
          )}
        </div>
        <div className="px-4 pb-6 sm:px-6">
          <div className="-mt-12 flex items-end justify-between gap-4 sm:-mt-14">
            <span className="relative flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-full text-xl font-bold ring-4 ring-card bg-muted sm:size-28">
              {profile.avatarUrl ? (
                <Image src={profile.avatarUrl} alt={t("avatarAlt", { name: profile.displayName })} fill unoptimized className="object-cover" sizes="112px" />
              ) : (
                initials(profile.displayName)
              )}
            </span>
            <div className="flex items-center gap-2">
              {!isOwn ? (
                <Button
                  type="button"
                  variant={following ? "secondary" : "default"}
                  disabled={pending}
                  aria-label={following
                    ? t("unfollowName", { name: profile.displayName })
                    : t("followName", { name: profile.displayName })}
                  aria-pressed={following}
                  onClick={() => void toggleFollow()}
                >
                  {following ? t("following") : t("follow")}
                </Button>
              ) : (
                <Link
                  href="/workspace/settings/profile"
                  className={cn(buttonVariants({ variant: "secondary" }))}
                >
                  <Pencil className="size-4" aria-hidden /> {t("editProfile")}
                </Link>
              )}
            </div>
          </div>

          <div className="mt-4">
            <h1 className="flex items-center gap-1.5 text-xl font-semibold sm:text-2xl">
              {profile.displayName}
              {profile.isVerified ? <BadgeCheck className="size-5 text-primary" aria-label={t("verified")} /> : null}
            </h1>
            <p className="text-sm text-muted-foreground">@{profile.handle}</p>
            {profile.headline ? <p className="mt-3 text-sm font-medium">{profile.headline}</p> : null}
            {profile.bio ? <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-muted-foreground">{profile.bio}</p> : null}
            <div className="mt-3 flex flex-wrap gap-x-4 gap-y-2 text-xs text-muted-foreground">
              {profile.location ? <span className="inline-flex items-center gap-1"><MapPin className="size-3.5" aria-hidden /> {profile.location}</span> : null}
              {profile.websiteUrl ? (
                <a href={profile.websiteUrl} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 text-primary hover:underline">
                  {t("website")} <ExternalLink className="size-3" aria-hidden />
                </a>
              ) : null}
            </div>
          </div>

          <dl className="mt-5 grid grid-cols-2 gap-4 rounded-xl border bg-muted/30 p-4 sm:grid-cols-4">
            {stats.map(({ label, value }) => (
              <div key={label} className="min-w-0">
                <dt className="text-xs text-muted-foreground">{label}</dt>
                <dd className="font-semibold tabular-nums">{value}</dd>
              </div>
            ))}
          </dl>
        </div>
      </section>

      <div className="space-y-8 px-4 py-8 sm:px-6">
        {collectionsFailed ? (
          <p role="alert" className="rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive">
            {t("collectionsError")}
          </p>
        ) : null}

        <Tabs defaultValue="projects">
          <TabsList aria-label={t("tabsLabel")}>
            <TabsTrigger value="projects">
              {t("tabs.projects")} <span className="text-muted-foreground tabular-nums">{projects.length}</span>
            </TabsTrigger>
            <TabsTrigger value="posts">
              {t("tabs.posts")} <span className="text-muted-foreground tabular-nums">{posts.length}</span>
            </TabsTrigger>
          </TabsList>

          <TabsContent value="projects" className="mt-4">
            {projects.length > 0 ? (
              <div className="grid gap-4 sm:grid-cols-2">
                {projects.map((project) => (
                  <article key={project.id} className="overflow-hidden rounded-xl border bg-card text-card-foreground transition-shadow hover:shadow-sm">
                    {project.imageUrl ? (
                      <div className="relative aspect-video bg-muted">
                        <Image src={project.imageUrl} alt="" fill unoptimized className="object-cover" sizes="(min-width: 640px) 386px, 100vw" />
                      </div>
                    ) : null}
                    <div className="space-y-2 p-4">
                      <h3 className="font-semibold">
                        <Link href={`/projects/${project.slug}`} className="hover:underline">{project.title}</Link>
                      </h3>
                      {project.shortDescription ? <p className="line-clamp-2 text-sm text-muted-foreground">{project.shortDescription}</p> : null}
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed px-4 py-10 text-center text-sm text-muted-foreground">
                <FolderKanban className="size-6" aria-hidden />
                {t("emptyProjects")}
              </div>
            )}
          </TabsContent>

          <TabsContent value="posts" className="mt-4">
            {posts.length > 0 ? (
              <div className="space-y-4">
                {posts.map((post) => (
                  <article key={post.id} className="overflow-hidden rounded-xl border bg-card p-4 text-card-foreground">
                    <p className="whitespace-pre-wrap text-sm leading-6">{post.content}</p>
                    {post.mediaUrl && post.mediaType?.startsWith("image/") ? (
                      <div className="relative mt-4 aspect-video overflow-hidden rounded-lg bg-muted">
                        <Image src={post.mediaUrl} alt="" fill unoptimized className="object-cover" sizes="772px" />
                      </div>
                    ) : null}
                    {post.mediaUrl && post.mediaType?.startsWith("video/") ? (
                      <video src={post.mediaUrl} controls preload="metadata" className="mt-4 aspect-video w-full rounded-lg bg-black" />
                    ) : null}
                    <div className="mt-4 flex items-center justify-between gap-4">
                      <time dateTime={post.createdAt} className="text-xs text-muted-foreground">
                        {dateFormatter.format(new Date(post.createdAt))}
                      </time>
                      <Link href={`/social/posts/${post.id}`} className="inline-flex text-sm font-medium text-primary hover:underline">{t("openPost")}</Link>
                    </div>
                  </article>
                ))}
              </div>
            ) : (
              <div className="flex flex-col items-center gap-2 rounded-xl border border-dashed px-4 py-10 text-center text-sm text-muted-foreground">
                <MessageSquareText className="size-6" aria-hidden />
                {t("emptyPosts")}
              </div>
            )}
          </TabsContent>
        </Tabs>
      </div>
    </main>
  );
}
