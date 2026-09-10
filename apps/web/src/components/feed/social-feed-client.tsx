"use client";

import type { FeedScope, SocialFeedItem, SocialPostItem } from "@/lib/feed/contracts";
import * as React from "react";
import { InfinitePostFeed } from "./infinite-post-feed";
import { SocialComposer } from "./social-composer";

export function SocialFeedClient({ userName, scope, tag, initialItems, initialNextCursor, currentUserId }: {
  userName: string;
  scope: FeedScope;
  tag?: string | null;
  initialItems: SocialFeedItem[];
  initialNextCursor: string | null;
  currentUserId?: string | null;
}): React.JSX.Element {
  const identity = `${scope}:${tag ?? ""}`;
  const [published, setPublished] = React.useState<{ item: SocialPostItem; identity: string } | null>(null);
  const publish = React.useCallback((item: SocialPostItem) => {
    setPublished((current) => current?.item.id === item.id && current.identity === identity ? current : { item, identity });
  }, [identity]);
  return <><SocialComposer userName={userName} onPublished={publish} /><InfinitePostFeed scope={scope} tag={tag} initialItems={initialItems} initialNextCursor={initialNextCursor} currentUserId={currentUserId} publishedItem={published?.item} publishedIdentity={published?.identity} /></>;
}
