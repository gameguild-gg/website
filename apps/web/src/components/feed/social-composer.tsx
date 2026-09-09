"use client";

import {
  createPostAction,
  type CreatePostActionState,
} from "@/lib/posts/actions";
import { Button } from "@game-guild/ui/components/button";
import { FlaskConical, Gamepad2, ImageIcon, Send, X } from "lucide-react";
import * as React from "react";
import { toast } from "sonner";

const INITIAL_CREATE_POST_STATE: CreatePostActionState = {
  success: false,
  message: null,
};

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

export function SocialComposer({
  userName,
}: {
  userName: string;
}): React.JSX.Element {
  const [expanded, setExpanded] = React.useState(false);
  const [state, action, pending] = React.useActionState(
    createPostAction,
    INITIAL_CREATE_POST_STATE,
  );
  const formRef = React.useRef<HTMLFormElement>(null);

  React.useEffect(() => {
    const openComposer = () => setExpanded(true);
    window.addEventListener("social:compose", openComposer);
    return () => window.removeEventListener("social:compose", openComposer);
  }, []);

  React.useEffect(() => {
    if (!state.message) return;
    if (state.success) {
      toast.success(state.message);
      formRef.current?.reset();
    } else {
      toast.error(state.message);
    }
  }, [state]);

  if (!expanded) {
    return (
      <section id="social-composer" className="px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3 rounded-xl bg-card p-2.5 text-card-foreground">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary/25 to-highlight/25 text-xs font-bold text-foreground">
            {initials(userName)}
          </span>
          <button
            type="button"
            onClick={() => setExpanded(true)}
            className="min-w-0 flex-1 rounded-lg px-2 py-2 text-left text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          >
            Share your progress…
          </button>
          <div className="hidden items-center gap-1 sm:flex">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-muted-foreground hover:bg-accent hover:text-primary"
            >
              <ImageIcon className="size-4" /> Photo
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-muted-foreground hover:bg-accent hover:text-success"
            >
              <Gamepad2 className="size-4" /> Build
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-muted-foreground hover:bg-accent hover:text-highlight"
            >
              <FlaskConical className="size-4" /> Playtest
            </Button>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section id="social-composer" className="px-4 py-4 sm:px-6">
      <form
        ref={formRef}
        action={action}
        className="rounded-xl bg-card p-4 text-card-foreground"
      >
        <div className="flex items-start gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-primary/25 to-highlight/25 text-xs font-bold text-foreground">
            {initials(userName)}
          </span>
          <div className="min-w-0 flex-1 space-y-3">
            <textarea
              name="content"
              autoFocus
              rows={4}
              maxLength={4000}
              required
              placeholder="What are you building?"
              className="w-full resize-none bg-transparent text-sm leading-6 text-foreground outline-none placeholder:text-muted-foreground/70"
            />
            <input
              name="mediaUrl"
              type="url"
              placeholder="Optional image URL"
              className="h-9 w-full rounded-lg border border-input bg-background/50 px-3 text-xs text-foreground outline-none placeholder:text-muted-foreground/70 focus:border-ring focus:ring-1 focus:ring-ring"
            />
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={() => setExpanded(false)}
            className="text-muted-foreground hover:bg-accent hover:text-accent-foreground"
          >
            <X className="size-4" />
            <span className="sr-only">Close composer</span>
          </Button>
        </div>
        <div className="mt-4 flex items-center justify-between border-t border-border pt-3">
          <p className="text-xs text-muted-foreground/70">
            Visible to the GameGuild community
          </p>
          <Button
            type="submit"
            size="sm"
            disabled={pending}
            className="bg-primary text-primary-foreground hover:bg-primary/85"
          >
            <Send className="size-4" />
            {pending ? "Publishing…" : "Publish"}
          </Button>
        </div>
      </form>
    </section>
  );
}
