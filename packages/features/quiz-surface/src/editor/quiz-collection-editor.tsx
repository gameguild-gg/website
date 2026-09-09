"use client";

import { useEffect, useRef, useState } from "react";
import {
  createEmptyQuizAnswer,
  createSingleChoiceEntry,
  formatQuizPoints,
  toQuizLearnerEntry,
  type QuizAnswer,
  type QuizEntry,
} from "@game-guild/quiz";
import {
  nextQuizContentItemId,
  type QuizContentItem,
} from "@game-guild/quiz-content";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@game-guild/ui/components/alert-dialog";
import {
  ChevronDown,
  ChevronUp,
  GripVertical,
  HelpCircle,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { QuizPlayer, type QuizSubmissionResult } from "../player/quiz-player";
import { QuizPracticePlayer } from "../player/quiz-practice-player";
import { QuizWrapper } from "../shared/quiz-wrapper";
import type { QuizSubmissionMode } from "../shared/submission-mode";
import { quizQuestionLabels } from "./editor-registry";
import { QuizEditorDialog } from "./quiz-editor-dialog";
import {
  QuizCollectionDragPreview,
  useQuizCollectionDragDrop,
} from "./quiz-collection-drag-drop";

export interface QuizCollectionEditorProps {
  items: QuizContentItem[];
  onChange: (items: QuizContentItem[]) => void;
  createItemId?: (items: QuizContentItem[]) => string;
  submissionMode?: QuizSubmissionMode;
  readOnly?: boolean;
  onDragStateChange?: (dragging: boolean) => void;
}

interface EditingQuestion {
  itemIndex: number | null;
  insertIndex: number;
  value: QuizEntry;
}

export function QuizCollectionEditor({
  items,
  onChange,
  createItemId = nextQuizContentItemId,
  submissionMode = "local-practice",
  readOnly = false,
  onDragStateChange,
}: QuizCollectionEditorProps) {
  const [editing, setEditing] = useState<EditingQuestion | null>(null);
  const [deleteIndex, setDeleteIndex] = useState<number | null>(null);
  const scrollToIndexRef = useRef<number | null>(null);
  const itemRefs = useRef<Map<number, HTMLDivElement>>(new Map());
  const {
    containerRef,
    dragIndex,
    dropTargetIndex,
    handleContainerDragLeave,
    handleContainerDragOver,
    handleDragEnd,
    handleDragStart,
    isDragging,
    setDropTargetIndex,
  } = useQuizCollectionDragDrop({
    items,
    onChange,
    onDragStateChange,
    scrollToIndexRef,
  });

  const startInsert = (insertIndex: number) => {
    setEditing({
      itemIndex: null,
      insertIndex,
      value: createSingleChoiceEntry(""),
    });
  };

  const commitQuestion = (entry: QuizEntry) => {
    if (!editing) return;

    if (editing.itemIndex === null) {
      const next = [...items];
      next.splice(editing.insertIndex, 0, {
        id: createItemId(items),
        entry,
      });
      onChange(next);
    } else {
      onChange(
        items.map((item, index) =>
          index === editing.itemIndex ? { ...item, entry } : item,
        ),
      );
    }

    setEditing(null);
  };

  const moveQuestion = (from: number, to: number) => {
    if (to < 0 || to >= items.length || from === to) return;
    const next = [...items];
    const [moved] = next.splice(from, 1);
    if (!moved) return;
    next.splice(to, 0, moved);
    onChange(next);
    scrollToIndexRef.current = to;
  };

  useEffect(() => {
    if (scrollToIndexRef.current === null) return;
    const index = scrollToIndexRef.current;
    scrollToIndexRef.current = null;
    const timeout = window.setTimeout(() => {
      requestAnimationFrame(() => {
        itemRefs.current
          .get(index)
          ?.scrollIntoView({ behavior: "smooth", block: "center" });
      });
    }, 150);
    return () => window.clearTimeout(timeout);
  });

  if (items.length === 0 && readOnly) {
    return (
      <div className="rounded-md border px-6 py-16 text-center text-sm text-muted-foreground">
        This quiz has no questions yet.
      </div>
    );
  }

  return (
    <div className="space-y-0">
      {items.length === 0 && !readOnly && (
        <div className="flex flex-col items-center justify-center py-20">
          <button
            type="button"
            onClick={() => startInsert(0)}
            className="mb-4 flex h-14 w-14 cursor-pointer items-center justify-center rounded-full border-2 border-dashed border-gray-300 text-gray-400 transition-all hover:scale-110 hover:border-blue-400 hover:text-blue-500 dark:border-gray-600 dark:hover:border-blue-500 dark:hover:text-blue-400"
            aria-label="Add first question"
          >
            <Plus className="h-7 w-7" />
          </button>
          <p className="text-sm font-medium text-gray-500 dark:text-gray-400">
            Add your first question
          </p>
          <p className="mt-1 text-xs text-gray-400 dark:text-gray-500">
            Choose from 15 question types
          </p>
        </div>
      )}

      {items.length > 0 && (
        <div
          ref={containerRef}
          onDragOver={isDragging ? handleContainerDragOver : undefined}
          onDragLeave={isDragging ? handleContainerDragLeave : undefined}
        >
          {!readOnly && !isDragging && (
            <InsertQuestionLine onInsert={() => startInsert(0)} />
          )}

          {isDragging && dropTargetIndex === 0 && dragIndex !== null && (
            <QuizCollectionDragPreview
              onDragOver={handleContainerDragOver}
              onDrop={handleDragEnd}
            />
          )}

          {items.map((item, index) => (
            <div
              key={item.id}
              ref={(element) => {
                if (element) itemRefs.current.set(index, element);
                else itemRefs.current.delete(index);
              }}
            >
              <article
                data-quiz-block-card
                className={`group/card overflow-hidden rounded-lg border bg-white transition-all duration-300 dark:bg-gray-900 ${
                  dragIndex === index
                    ? "border-dashed border-blue-300 opacity-30 dark:border-blue-600"
                    : "border-gray-200 shadow-sm hover:shadow-md dark:border-gray-700"
                }`}
                onDragOver={
                  isDragging
                    ? (event) => {
                        event.preventDefault();
                        event.dataTransfer.dropEffect = "move";
                        const rect = event.currentTarget.getBoundingClientRect();
                        const middle = rect.top + rect.height / 2;
                        setDropTargetIndex(
                          event.clientY < middle ? index : index + 1,
                        );
                      }
                    : undefined
                }
                onDrop={
                  isDragging
                    ? (event) => {
                        event.preventDefault();
                        handleDragEnd();
                      }
                    : undefined
                }
              >
                <header className="flex items-center gap-2 border-b border-gray-200 bg-gray-50 px-3 py-2 dark:border-gray-700 dark:bg-gray-800">
                  {!readOnly && (
                    <div
                      draggable
                      className="shrink-0 cursor-grab active:cursor-grabbing"
                      role="button"
                      tabIndex={0}
                      aria-label={`Drag question ${index + 1}`}
                      title="Drag to reorder"
                      onDragStart={(event) => {
                        const card = event.currentTarget.closest(
                          "[data-quiz-block-card]",
                        ) as HTMLElement | null;
                        if (card) {
                          event.dataTransfer.setDragImage(
                            card,
                            card.offsetWidth / 2,
                            20,
                          );
                        }
                        event.dataTransfer.effectAllowed = "move";
                        handleDragStart(index);
                      }}
                      onDragEnd={handleDragEnd}
                    >
                      <GripVertical className="h-4 w-4 text-gray-300 dark:text-gray-600" />
                    </div>
                  )}

                  <div className="flex min-w-0 items-center gap-1.5">
                    <HelpCircle className="h-4 w-4 shrink-0 text-gray-500 dark:text-gray-400" />
                    <span className="truncate text-xs font-medium text-gray-600 dark:text-gray-300">
                      {quizQuestionLabels[item.entry.type]}
                    </span>
                  </div>

                  <span className="shrink-0 rounded bg-gray-100 px-1.5 py-0.5 font-mono text-[11px] text-gray-400 dark:bg-gray-700 dark:text-gray-500">
                    #{index + 1}
                  </span>
                  {item.entry.points !== undefined && (
                    <span className="text-xs text-gray-400 dark:text-gray-500">
                      {formatQuizPoints(item.entry.points)} pts
                    </span>
                  )}

                  <div className="flex-1" />

                  {!readOnly && (
                    <div className="flex items-center gap-0.5">
                      <button
                        type="button"
                        className="rounded p-1 text-blue-500 transition-colors hover:bg-blue-50 hover:text-blue-700 dark:text-blue-400 dark:hover:bg-blue-950/40 dark:hover:text-blue-300"
                        aria-label={`Edit question ${index + 1}`}
                        title="Open focused editor"
                        onClick={() =>
                          setEditing({
                            itemIndex: index,
                            insertIndex: index,
                            value: item.entry,
                          })
                        }
                      >
                        <Pencil className="h-3.5 w-3.5" />
                      </button>

                      <div className="flex items-center gap-0.5 opacity-0 transition-opacity group-hover/card:opacity-100 group-focus-within/card:opacity-100">
                        <button
                          type="button"
                          disabled={index === 0}
                          className="rounded p-1 text-gray-400 transition-colors hover:bg-gray-200/60 hover:text-gray-700 disabled:cursor-not-allowed disabled:opacity-30 dark:hover:bg-gray-700/60 dark:hover:text-gray-200"
                          aria-label={`Move question ${index + 1} up`}
                          title="Move up"
                          onClick={() => moveQuestion(index, index - 1)}
                        >
                          <ChevronUp className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          disabled={index === items.length - 1}
                          className="rounded p-1 text-gray-400 transition-colors hover:bg-gray-200/60 hover:text-gray-700 disabled:cursor-not-allowed disabled:opacity-30 dark:hover:bg-gray-700/60 dark:hover:text-gray-200"
                          aria-label={`Move question ${index + 1} down`}
                          title="Move down"
                          onClick={() => moveQuestion(index, index + 1)}
                        >
                          <ChevronDown className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          className="rounded p-1 text-gray-400 transition-colors hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-950/40 dark:hover:text-red-400"
                          aria-label={`Delete question ${index + 1}`}
                          title="Remove question"
                          onClick={() => setDeleteIndex(index)}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                  )}
                </header>

                <div className="p-4">
                  <div className="prose prose-sm max-w-none dark:prose-invert">
                    <QuizBlockPreview
                      entry={item.entry}
                      submissionMode={submissionMode}
                    />
                  </div>
                </div>
              </article>

              {isDragging &&
                dropTargetIndex === index + 1 &&
                dragIndex !== null && (
                  <QuizCollectionDragPreview
                    onDragOver={handleContainerDragOver}
                    onDrop={handleDragEnd}
                  />
                )}

              {!readOnly && !isDragging && (
                <InsertQuestionLine
                  onInsert={() => startInsert(index + 1)}
                />
              )}
            </div>
          ))}
        </div>
      )}

      {editing && (
        <QuizEditorDialog
          open={true}
          value={editing.value}
          submissionMode={submissionMode}
          onOpenChange={(open) => {
            if (!open) setEditing(null);
          }}
          onCommit={commitQuestion}
        />
      )}

      <AlertDialog
        open={deleteIndex !== null}
        onOpenChange={(open) => {
          if (!open) setDeleteIndex(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete question?</AlertDialogTitle>
            <AlertDialogDescription>
              This question will be permanently removed from the quiz.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => {
                if (deleteIndex !== null) {
                  onChange(items.filter((_, index) => index !== deleteIndex));
                }
                setDeleteIndex(null);
              }}
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}

function InsertQuestionLine({ onInsert }: { onInsert: () => void }) {
  return (
    <div className="group/insert relative flex items-center py-1.5">
      <div className="h-px flex-1 bg-gray-200 transition-colors group-hover/insert:bg-blue-400 dark:bg-gray-700 dark:group-hover/insert:bg-blue-500" />
      <button
        type="button"
        className="relative z-10 mx-2 flex h-7 w-7 cursor-pointer items-center justify-center rounded-full border-2 border-gray-300 bg-white text-gray-400 opacity-40 transition-all hover:scale-110 group-hover/insert:border-blue-400 group-hover/insert:text-blue-500 group-hover/insert:opacity-100 dark:border-gray-600 dark:bg-gray-900 dark:group-hover/insert:border-blue-500 dark:group-hover/insert:text-blue-400"
        aria-label="Insert question"
        title="Insert question here"
        onClick={onInsert}
      >
        <Plus className="h-4 w-4" />
      </button>
      <div className="h-px flex-1 bg-gray-200 transition-colors group-hover/insert:bg-blue-400 dark:bg-gray-700 dark:group-hover/insert:bg-blue-500" />
    </div>
  );
}

function QuizBlockPreview({
  entry,
  submissionMode,
}: {
  entry: QuizEntry;
  submissionMode: "local-practice" | "server-graded";
}) {
  const [answer, setAnswer] = useState<QuizAnswer>(() =>
    createEmptyQuizAnswer(entry.type),
  );
  const [submissionResult, setSubmissionResult] =
    useState<QuizSubmissionResult>({ status: "idle" });

  useEffect(() => {
    setAnswer(createEmptyQuizAnswer(entry.type));
    setSubmissionResult({ status: "idle" });
  }, [entry]);

  if (submissionMode === "server-graded") {
    return (
      <QuizWrapper>
        <QuizPlayer
          entry={toQuizLearnerEntry(entry)}
          answer={answer}
          onAnswerChange={setAnswer}
          onSubmit={() =>
            setSubmissionResult({ status: "pending" })
          }
          submissionResult={submissionResult}
        />
      </QuizWrapper>
    );
  }

  return (
    <QuizWrapper>
      <QuizPracticePlayer
        entry={entry}
        answer={answer}
        onAnswerChange={setAnswer}
      />
    </QuizWrapper>
  );
}
