"use client";

import { lazy, Suspense, useCallback, useState } from "react";
import { Label } from "@game-guild/ui/components/label";
import { Skeleton } from "@/components/ui/skeleton";

const MonacoCodeEditor = lazy(async () => {
  const mod = await import("@/components/block-content-editor/extras/code-studio/monaco-code-editor");
  return { default: mod.MonacoCodeEditor };
});

interface LessonCodeEditorProps {
  initialValue: string;
  language: "markdown" | "html";
  onChange: (value: string) => void;
  onCursorOffsetChange?: (offset: number) => void;
  placeholder?: string;
}

function EditorLoadingState() {
  return (
    <div className="space-y-3 rounded-lg border border-gray-200 p-4 dark:border-gray-700">
      <Skeleton className="h-10 w-full" />
      <Skeleton className="h-[300px] w-full" />
    </div>
  );
}

export function LessonCodeEditor({
  initialValue,
  language,
  onChange,
  onCursorOffsetChange,
  placeholder,
}: LessonCodeEditorProps) {
  const [value, setValue] = useState<string>(initialValue);

  const handleChange = useCallback(
    (next: string) => {
      setValue(next);
      onChange(next);
    },
    [onChange],
  );

  return (
    <div className="space-y-2">
      <Label>Body</Label>
      {placeholder ? (
        <p className="text-muted-foreground text-xs">{placeholder}</p>
      ) : null}
      <div className="overflow-hidden rounded-lg border border-gray-200 dark:border-gray-700" style={{ height: "400px" }}>
        <Suspense fallback={<EditorLoadingState />}>
          <MonacoCodeEditor
            value={value}
            language={language}
            ariaLabel="Lesson body"
            onChange={handleChange}
            onCursorOffsetChange={onCursorOffsetChange}
            height="100%"
          />
        </Suspense>
      </div>
    </div>
  );
}
