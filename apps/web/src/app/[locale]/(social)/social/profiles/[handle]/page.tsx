import { auth } from "@/auth";
import { SocialProfileView } from "@/components/feed/social-profile";
import {
  loadSocialProfileByHandle,
  loadSocialProfileCollections,
} from "@/lib/feed/queries";
import { notFound } from "next/navigation";

export async function generateMetadata({
  params,
}: {
  params: Promise<{ handle: string }>;
}) {
  const { handle } = await params;
  const profile = await loadSocialProfileByHandle(handle).catch(() => null);

  return {
    title: profile ? `${profile.displayName} (@${profile.handle}) | GameGuild` : "Profile not found | GameGuild",
    description: profile?.headline || profile?.bio || undefined,
  };
}

export default async function SocialProfilePage({
  params,
}: {
  params: Promise<{ locale: string; handle: string }>;
}): Promise<React.JSX.Element> {
  const [{ handle }, session] = await Promise.all([params, auth()]);
  const profile = await loadSocialProfileByHandle(handle);
  if (!profile) notFound();
  const currentUserId = session && typeof session !== "function" ? session.user?.id ?? null : null;
  let posts: Awaited<ReturnType<typeof loadSocialProfileCollections>>["posts"] = [];
  let projects: Awaited<ReturnType<typeof loadSocialProfileCollections>>["projects"] = [];
  let collectionsFailed = false;
  try {
    ({ posts, projects } = await loadSocialProfileCollections(profile.userId));
  } catch {
    collectionsFailed = true;
  }
  return (
    <SocialProfileView
      profile={profile}
      currentUserId={currentUserId}
      posts={posts}
      projects={projects}
      collectionsFailed={collectionsFailed}
    />
  );
}
