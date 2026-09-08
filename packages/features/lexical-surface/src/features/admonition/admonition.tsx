import * as React from "react";
import { cn } from "@game-guild/ui/lib/utils";

export type AdmonitionType =
  | "note"
  | "abstract"
  | "info"
  | "tip"
  | "success"
  | "question"
  | "warning"
  | "failure"
  | "danger"
  | "bug"
  | "example"
  | "quote"
  | "important"
  | "caution"
  | "attention"
  | "hint"
  | "check"
  | "summary";

export type AdmonitionDesign =
  "default" | "compact" | "bordered" | "vertical-bar";

// Static class strings per type — Tailwind only generates utilities for
// literal class names it can scan, so `border-${accent}-…` templates never
// produce CSS.
const CLASSES_BY_TYPE: Record<AdmonitionType, string> = {
  note: "border-blue-300 bg-blue-50 dark:border-blue-700 dark:bg-blue-950/30",
  abstract: "border-sky-300 bg-sky-50 dark:border-sky-700 dark:bg-sky-950/30",
  info: "border-cyan-300 bg-cyan-50 dark:border-cyan-700 dark:bg-cyan-950/30",
  tip: "border-lime-300 bg-lime-50 dark:border-lime-700 dark:bg-lime-950/30",
  success: "border-green-300 bg-green-50 dark:border-green-700 dark:bg-green-950/30",
  question: "border-amber-300 bg-amber-50 dark:border-amber-700 dark:bg-amber-950/30",
  warning: "border-yellow-300 bg-yellow-50 dark:border-yellow-700 dark:bg-yellow-950/30",
  failure: "border-red-300 bg-red-50 dark:border-red-700 dark:bg-red-950/30",
  danger: "border-orange-300 bg-orange-50 dark:border-orange-700 dark:bg-orange-950/30",
  bug: "border-stone-300 bg-stone-50 dark:border-stone-700 dark:bg-stone-950/30",
  example: "border-teal-300 bg-teal-50 dark:border-teal-700 dark:bg-teal-950/30",
  quote: "border-pink-300 bg-pink-50 dark:border-pink-700 dark:bg-pink-950/30",
  important: "border-purple-300 bg-purple-50 dark:border-purple-700 dark:bg-purple-950/30",
  caution: "border-rose-300 bg-rose-50 dark:border-rose-700 dark:bg-rose-950/30",
  attention: "border-fuchsia-300 bg-fuchsia-50 dark:border-fuchsia-700 dark:bg-fuchsia-950/30",
  hint: "border-emerald-300 bg-emerald-50 dark:border-emerald-700 dark:bg-emerald-950/30",
  check: "border-indigo-300 bg-indigo-50 dark:border-indigo-700 dark:bg-indigo-950/30",
  summary: "border-violet-300 bg-violet-50 dark:border-violet-700 dark:bg-violet-950/30",
};

export function Admonition({
  type,
  design,
  title,
  content,
  customBorderColor,
  customTextColor,
}: {
  type: AdmonitionType;
  design: AdmonitionDesign;
  title?: React.ReactNode;
  content?: React.ReactNode;
  customBorderColor?: string;
  customTextColor?: string;
}) {
  const className = cn(
    "rounded-md border p-4",
    design === "compact" && "border-l-4 py-3",
    design === "bordered" && "bg-transparent",
    design === "vertical-bar" &&
      "border-y-0 border-r-0 border-l-4 rounded-none",
    !customBorderColor && CLASSES_BY_TYPE[type],
  );

  return (
    <section
      className={className}
      style={customBorderColor ? { borderColor: customBorderColor } : undefined}
    >
      {title && (
        <div className="mb-1 font-semibold" style={{ color: customTextColor }}>
          {title}
        </div>
      )}
      {content && (
        <div className="text-sm" style={{ color: customTextColor }}>
          {content}
        </div>
      )}
    </section>
  );
}
