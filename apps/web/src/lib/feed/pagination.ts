"use client";

import type { FeedScope, SocialFeedPage } from "./contracts";

export class SocialFeedPaginationError extends Error {
  readonly status: number;

  constructor(status: number) {
    super("The social feed page could not be loaded.");
    this.name = "SocialFeedPaginationError";
    this.status = status;
  }
}

export async function loadSocialFeedPage(input: {
  scope: FeedScope;
  cursor: string;
  tag?: string | null;
  signal: AbortSignal;
}): Promise<SocialFeedPage> {
  const params = new URLSearchParams({ scope: input.scope, cursor: input.cursor });
  if (input.tag) params.set("tag", input.tag);

  const response = await fetch(`/api/social/feed?${params.toString()}`, {
    headers: { Accept: "application/json" },
    signal: input.signal,
  });
  if (!response.ok) throw new SocialFeedPaginationError(response.status);
  return response.json() as Promise<SocialFeedPage>;
}
