import { auth } from "@/auth";
import { PostCard } from "@/components/feed/post-card";
import { Link } from "@/i18n/navigation";
import { loadSocialPost } from "@/lib/feed/queries";
import { Button } from "@game-guild/ui/components/button";
import { ArrowLeft } from "lucide-react";
import { notFound } from "next/navigation";

export default async function SocialPostPage({
  params,
}: {
  params: Promise<{ locale: string; postId: string }>;
}): Promise<React.JSX.Element> {
  const [{ postId }, session] = await Promise.all([params, auth()]);
  const post = await loadSocialPost(postId).catch(() => null);
  if (!post) notFound();
  const currentUserId = session && typeof session !== "function" ? session.user?.id ?? null : null;
  return (
    <main className="mx-auto min-h-[calc(100svh-4rem)] w-full max-w-[820px] bg-background pb-16">
      <div className="px-4 py-4 sm:px-6">
        <Button asChild variant="ghost" size="sm"><Link href="/"><ArrowLeft className="size-4" /> Back to feed</Link></Button>
      </div>
      <PostCard item={post} currentUserId={currentUserId} />
    </main>
  );
}
