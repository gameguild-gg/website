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
      <section className="border-b border-white/10 px-4 py-3 sm:px-6">
        <div className="flex items-center gap-3 rounded-xl border border-white/10 bg-[#0e1422] p-2.5">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-[#48c7ff]/25 to-[#8b5cf6]/25 text-xs font-bold text-white">
            {initials(userName)}
          </span>
          <button
            type="button"
            onClick={() => setExpanded(true)}
            className="min-w-0 flex-1 rounded-lg px-2 py-2 text-left text-sm text-slate-400 transition-colors hover:bg-white/[0.04] hover:text-slate-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#48c7ff]"
          >
            Share your progress…
          </button>
          <div className="hidden items-center gap-1 sm:flex">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-slate-400 hover:bg-white/[0.06] hover:text-[#48c7ff]"
            >
              <ImageIcon className="size-4" /> Photo
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-slate-400 hover:bg-white/[0.06] hover:text-[#49e6a2]"
            >
              <Gamepad2 className="size-4" /> Build
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setExpanded(true)}
              className="text-slate-400 hover:bg-white/[0.06] hover:text-[#a78bfa]"
            >
              <FlaskConical className="size-4" /> Playtest
            </Button>
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="border-b border-white/10 px-4 py-4 sm:px-6">
      <form
        ref={formRef}
        action={action}
        className="rounded-xl border border-white/10 bg-[#0e1422] p-4 shadow-xl shadow-black/10"
      >
        <div className="flex items-start gap-3">
          <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-[#48c7ff]/25 to-[#8b5cf6]/25 text-xs font-bold text-white">
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
              className="w-full resize-none bg-transparent text-sm leading-6 text-white outline-none placeholder:text-slate-500"
            />
            <input
              name="mediaUrl"
              type="url"
              placeholder="Optional image URL"
              className="h-9 w-full rounded-lg border border-white/10 bg-[#070a12] px-3 text-xs text-slate-200 outline-none placeholder:text-slate-500 focus:border-[#48c7ff] focus:ring-1 focus:ring-[#48c7ff]"
            />
          </div>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={() => setExpanded(false)}
            className="text-slate-400 hover:bg-white/[0.06] hover:text-white"
          >
            <X className="size-4" />
            <span className="sr-only">Close composer</span>
          </Button>
        </div>
        <div className="mt-4 flex items-center justify-between border-t border-white/10 pt-3">
          <p className="text-xs text-slate-500">
            Visible to the GameGuild community
          </p>
          <Button
            type="submit"
            size="sm"
            disabled={pending}
            className="bg-[#48c7ff] text-[#06111a] hover:bg-[#7ad7ff]"
          >
            <Send className="size-4" />
            {pending ? "Publishing…" : "Publish"}
          </Button>
        </div>
      </form>
    </section>
  );
}
