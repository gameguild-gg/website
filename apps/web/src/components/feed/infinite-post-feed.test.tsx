import '@testing-library/jest-dom/vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  loadPostsAction: vi.fn(),
}));

vi.mock('@/lib/posts/actions', () => ({
  loadPostsAction: mocks.loadPostsAction,
}));
vi.mock('next/image', () => ({
  default: (props: Record<string, unknown>) => <img alt="" {...props} />,
}));

import { InfinitePostFeed } from './infinite-post-feed';
import type { PostCardData } from '@/lib/posts/queries';

function post(id: string, content = id): PostCardData {
  return {
    id,
    authorId: 'author-1',
    authorName: 'Ada Builder',
    content,
    mediaUrl: null,
    mediaType: null,
    likesCount: 3,
    commentsCount: 1,
    createdAt: new Date().toISOString(),
  };
}

function intersectionCallback() {
  // jsdom lacks IntersectionObserver; capture the registered callback
  let callback: IntersectionObserverCallback | undefined;
  const observe = vi.fn();
  const stub = class {
    constructor(cb: IntersectionObserverCallback) {
      callback = cb;
    }
    observe = observe;
    disconnect = vi.fn();
  };
  vi.stubGlobal('IntersectionObserver', stub);
  return {
    trigger(entries: boolean) {
      callback?.([{ isIntersecting: entries } as IntersectionObserverEntry], {} as IntersectionObserver);
    },
    observe,
  };
}

describe('InfinitePostFeed', () => {
  beforeEach(() => vi.clearAllMocks());
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it('renders the SSR page as post cards', () => {
    const io = intersectionCallback();

    render(<InfinitePostFeed stream="trending" initialItems={[post('p-1'), post('p-2')]} initialNextSkip={2} />);

    expect(screen.getAllByTestId('post-card')).toHaveLength(2);
    expect(screen.getByText('p-1')).toBeInTheDocument();
    expect(io.observe).toHaveBeenCalled();
  });

  it('appends the next page when the sentinel intersects and paginates onward', async () => {
    const io = intersectionCallback();
    mocks.loadPostsAction
      .mockResolvedValueOnce({ items: [post('p-3'), post('p-4')], nextSkip: 4 })
      .mockResolvedValueOnce({ items: [post('p-5')], nextSkip: 5 });

    render(<InfinitePostFeed stream="trending" initialItems={[post('p-1'), post('p-2')]} initialNextSkip={2} />);

    io.trigger(true);
    await waitFor(() => expect(mocks.loadPostsAction).toHaveBeenCalledWith('trending', 2));
    await waitFor(() => expect(screen.getAllByTestId('post-card')).toHaveLength(4));

    io.trigger(true);
    await waitFor(() => expect(mocks.loadPostsAction).toHaveBeenCalledWith('trending', 4));
    await waitFor(() => expect(screen.getAllByTestId('post-card')).toHaveLength(5));
  });

  it('stops paginating once the stream reports no more pages', async () => {
    const io = intersectionCallback();
    mocks.loadPostsAction.mockResolvedValue({ items: [], nextSkip: null });

    render(<InfinitePostFeed stream="trending" initialItems={[post('p-1')]} initialNextSkip={1} />);

    io.trigger(true);
    await waitFor(() => expect(mocks.loadPostsAction).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByText(/all caught up/i)).toBeInTheDocument());

    io.trigger(true);
    await new Promise((r) => setTimeout(r, 20));
    expect(mocks.loadPostsAction).toHaveBeenCalledTimes(1);
  });

  it('deduplicates posts already rendered', async () => {
    const io = intersectionCallback();
    mocks.loadPostsAction.mockResolvedValue({ items: [post('p-1'), post('p-2')], nextSkip: 2 });

    render(<InfinitePostFeed stream="public" initialItems={[post('p-1')]} initialNextSkip={1} />);

    io.trigger(true);
    await waitFor(() => expect(mocks.loadPostsAction).toHaveBeenCalled());
    await waitFor(() => expect(screen.getAllByTestId('post-card')).toHaveLength(2));
  });

  it('shows the empty state for an empty initial page', () => {
    intersectionCallback();

    render(<InfinitePostFeed stream="feed" initialItems={[]} initialNextSkip={null} />);

    expect(screen.getByText(/your feed is ready for its first build/i)).toBeInTheDocument();
  });
});
