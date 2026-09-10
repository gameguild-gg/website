import { auth } from "@/auth";
import { SocialProfileView } from "@/components/feed/social-profile";
import { loadSocialProfileByHandle } from "@/lib/feed/queries";
import { notFound } from "next/navigation";

export default async function SocialProfilePage({
  params,
}: {
  params: Promise<{ locale: string; handle: string }>;
}): Promise<React.JSX.Element> {
  const [{ handle }, session] = await Promise.all([params, auth()]);
  const profile = await loadSocialProfileByHandle(handle);
  if (!profile) notFound();
  const currentUserId = session && typeof session !== "function" ? session.user?.id ?? null : null;
  return <SocialProfileView profile={profile} currentUserId={currentUserId} />;
}
