import type { SocialStoryPreview } from "@/lib/posts/demo";
import { Plus } from "lucide-react";

function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join("")
    .toUpperCase();
}

export function BuildStories({
  userName,
  stories,
}: {
  userName: string;
  stories: SocialStoryPreview[];
}): React.JSX.Element {
  return (
    <section
      aria-label="Builds from your circles"
      className="overflow-hidden border-b border-white/10 px-4 py-4 sm:px-6"
    >
      <div className="flex gap-4 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        <button
          type="button"
          className="group flex w-[4.5rem] shrink-0 flex-col items-center gap-2 text-center"
        >
          <span className="relative flex size-14 items-center justify-center rounded-full border border-white/15 bg-[#111827] text-sm font-bold text-white transition-transform group-hover:scale-[1.03]">
            {initials(userName)}
            <span className="absolute -bottom-0.5 -right-0.5 flex size-5 items-center justify-center rounded-full border-2 border-[#070a12] bg-[#48c7ff] text-[#06111a]">
              <Plus className="size-3" aria-hidden="true" />
            </span>
          </span>
          <span className="w-full truncate text-[11px] font-medium text-slate-300">
            Your story
          </span>
        </button>

        {stories.map((story) => (
          <button
            key={story.id}
            type="button"
            className="group flex w-[4.5rem] shrink-0 flex-col items-center gap-2 text-center"
          >
            <span
              className={`rounded-full bg-gradient-to-br p-[2px] ${story.accent}`}
            >
              <span className="flex size-[3.25rem] items-center justify-center rounded-full border-2 border-[#070a12] bg-[#111827] text-xs font-bold text-white transition-transform group-hover:scale-[1.03]">
                {initials(story.name)}
              </span>
            </span>
            <span className="w-full truncate text-[11px] font-medium text-slate-300">
              {story.name}
            </span>
          </button>
        ))}
      </div>
    </section>
  );
}
