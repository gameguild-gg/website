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
  const [publishedItem, setPublishedItem] = React.useState<SocialPostItem | null>(null);
  const publish = React.useCallback((item: SocialPostItem) => {
    setPublishedItem((current) => current?.id === item.id ? current : item);
  }, []);
  return <><SocialComposer userName={userName} onPublished={publish} /><InfinitePostFeed scope={scope} tag={tag} initialItems={initialItems} initialNextCursor={initialNextCursor} currentUserId={currentUserId} publishedItem={publishedItem} /></>;
}
