'use client';

import { isGitHubLinkedAction } from '@/lib/community/github-star-prompt';
import { cn } from '@/lib/utils';
import { Button, buttonVariants } from '@game-guild/ui/components/button';
import { Star, X } from 'lucide-react';
import { useEffect, useState } from 'react';

const DISMISSED_STORAGE_KEY = 'gg-github-star-prompt-dismissed';
const SESSION_CHECK_KEY = 'gg-github-star-prompt-checked';

/** Public repository metadata endpoint used for the star check (issue #90). */
const REPO_API_URL = 'https://api.github.com/repos/gameguild-gg/gameguild';
const REPO_URL = 'https://github.com/gameguild-gg/gameguild';

interface GitHubRepoSnapshot {
  stargazersCount: number;
}

/**
 * Dismissible "Star us" prompt for GitHub-linked members (issue #90).
 *
 * Behavior:
 * - Only considers accounts linked to GitHub (server action over
 *   HEAD /v1/auth/external-logins); non-GitHub users never see it.
 * - Checks `GET /repos/gameguild-gg/gameguild` for the repository's starred
 *   state. v1 caveat, documented here deliberately: the public repo endpoint
 *   exposes `stargazers_count` (rendered on the card) but not a per-viewer
 *   starred flag — the user's GitHub OAuth token is not persisted server-side,
 *   so `/user/starred/:owner/:repo` cannot be called on their behalf yet.
 *   Until that lands, GitHub-linked members see the prompt unless dismissed.
 * - Rate limited: at most one check per browser session (sessionStorage).
 * - Dismissible: dismissal persists across sessions (localStorage).
 */
export function GitHubStarPrompt({ className }: { className?: string }) {
  const [visible, setVisible] = useState(false);
  const [starCount, setStarCount] = useState<number | null>(null);

  useEffect(() => {
    let cancelled = false;

    async function maybeShowPrompt() {
      try {
        if (localStorage.getItem(DISMISSED_STORAGE_KEY) === '1') return;
        // Rate limit: run the check at most once per browser session.
        if (sessionStorage.getItem(SESSION_CHECK_KEY) === '1') return;
        sessionStorage.setItem(SESSION_CHECK_KEY, '1');

        const linked = await isGitHubLinkedAction();
        if (!linked || cancelled) return;

        const response = await fetch(REPO_API_URL, {
          headers: { Accept: 'application/vnd.github+json' },
        });
        if (!response.ok || cancelled) return;

        const repo = (await response.json()) as Partial<GitHubRepoSnapshot>;
        if (typeof repo.stargazersCount === 'number') {
          setStarCount(repo.stargazersCount);
        }
        setVisible(true);
      } catch {
        // Any failure (offline, API down) silently skips the prompt.
      }
    }

    void maybeShowPrompt();
    return () => {
      cancelled = true;
    };
  }, []);

  if (!visible) return null;

  function dismiss() {
    localStorage.setItem(DISMISSED_STORAGE_KEY, '1');
    setVisible(false);
  }

  return (
    <aside
      role="complementary"
      aria-label="Support GameGuild on GitHub"
      data-testid="github-star-prompt"
      className={cn(
        'fixed inset-x-4 bottom-4 z-50 mx-auto flex max-w-md items-center gap-3 rounded-2xl border border-border bg-card p-4 text-card-foreground shadow-lg',
        className,
      )}
    >
      <Star className="size-5 shrink-0 text-primary" aria-hidden="true" />
      <p className="min-w-0 flex-1 text-sm leading-5">
        Enjoying GameGuild? Star the repository on GitHub
        {starCount !== null ? ` — join ${starCount.toLocaleString()} stargazers` : ''}.
      </p>
      <div className="flex shrink-0 items-center gap-1">
        <a
          href={REPO_URL}
          target="_blank"
          rel="noopener noreferrer"
          onClick={dismiss}
          className={cn(buttonVariants({ size: 'sm' }), 'no-underline')}
        >
          Star us
        </a>
        <Button
          type="button"
          variant="ghost"
          size="icon"
          aria-label="Dismiss star prompt"
          onClick={dismiss}
        >
          <X className="size-4" aria-hidden="true" />
        </Button>
      </div>
    </aside>
  );
}
