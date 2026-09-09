'use client';

import { Button } from '@game-guild/ui/components/button';
import { Plus } from 'lucide-react';

export function SocialHeaderCreateButton() {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon"
      className="hidden size-9 rounded-full text-foreground hover:bg-accent sm:inline-flex"
      aria-label="Create post"
      title="Create post"
      onClick={() => window.dispatchEvent(new Event('social:compose'))}
    >
      <Plus className="size-4" aria-hidden="true" />
    </Button>
  );
}
