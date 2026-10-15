"use client";

import { useCallback, useMemo, useState } from "react";
import { QuizCollectionEditor } from "@game-guild/quiz-surface/editor";
import { sumQuizItemPoints } from "@game-guild/grading-adapter-quiz";
import {
  disableQuizContentGrading,
  enableQuizContentGrading,
  isQuizPastDue,
  isValidQuizRules,
  parseQuizContentDocument,
  quizContentItemsToDocument,
  quizDocumentToContentItems,
  quizDocumentToGradingItems,
  readQuizContentGrading,
  readQuizContentRules,
  serializeQuizContentDocument,
  updateQuizContentRules,
  type QuizContentDocument,
  type QuizContentItem,
  type QuizRules,
} from "@game-guild/quiz-content";
import { Badge } from "@game-guild/ui/components/badge";
import { Input } from "@game-guild/ui/components/input";
import { Label } from "@game-guild/ui/components/label";
import { Switch } from "@game-guild/ui/components/switch";

/** Converts a UTC ISO timestamp to the `datetime-local` input format. */
function isoToDatetimeLocal(iso: string | undefined): string {
  if (!iso) return "";
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return "";
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Converts a `datetime-local` input value back to a UTC ISO timestamp. */
function datetimeLocalToIso(value: string): string | undefined {
  if (!value) return undefined;
  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? undefined : date.toISOString();
}

type QuizEditorMode = "edit" | "preview";

interface QuizContentEditorProps {
  initialContent: Record<string, unknown> | null | undefined;
  onChange: (content: Record<string, unknown>) => void;
  mode?: QuizEditorMode;
}

export function QuizContentEditor({
  initialContent,
  onChange,
  mode = "edit",
}: QuizContentEditorProps) {
  const initialDocument = useMemo(
    () => parseQuizContentDocument(initialContent).document,
    [initialContent],
  );
  const [document, setDocument] = useState<QuizContentDocument>(initialDocument);
  const renderedDocument = mode === "preview" ? initialDocument : document;
  const quizItems = useMemo(
    () => quizDocumentToContentItems(renderedDocument),
    [renderedDocument],
  );
  const rules = useMemo(
    () => readQuizContentRules(document),
    [document],
  );
  const gradingConfig = useMemo(
    () => readQuizContentGrading(renderedDocument),
    [renderedDocument],
  );
  const gradingEnabled = gradingConfig !== null;
  const gradedItemCount = gradingConfig ? Object.keys(gradingConfig.items).length : 0;
  const gradedPoints = useMemo(
    () => sumQuizItemPoints(quizDocumentToGradingItems(renderedDocument)),
    [renderedDocument],
  );

  const commitDocument = useCallback(
    (nextDocument: QuizContentDocument) => {
      const serialized = serializeQuizContentDocument(nextDocument);
      setDocument(serialized);
      onChange({ ...serialized });
    },
    [onChange],
  );

  const handleQuizItemsChange = useCallback(
    (items: QuizContentItem[]) => {
      commitDocument(
        quizContentItemsToDocument({
          items,
          rules: document.rules,
          grading: document.grading,
        }),
      );
    },
    [commitDocument, document.grading, document.rules],
  );

  const handleGradingEnabledChange = useCallback(
    (enabled: boolean) => {
      commitDocument(
        enabled
          ? enableQuizContentGrading(document)
          : disableQuizContentGrading(document),
      );
    },
    [commitDocument, document],
  );

  const handleRulesChange = useCallback(
    (nextRules: QuizRules) => {
      // Guard the serializer: never commit a rules payload the parser rejects.
      if (!isValidQuizRules(nextRules)) return;
      commitDocument(updateQuizContentRules(document, nextRules));
    },
    [commitDocument, document],
  );

  const handleDueAtChange = useCallback(
    (value: string) => {
      handleRulesChange({
        ...rules,
        dueAt: datetimeLocalToIso(value),
      });
    },
    [handleRulesChange, rules],
  );

  const handleTimeLimitChange = useCallback(
    (value: string) => {
      const parsed = value === "" ? undefined : Number.parseInt(value, 10);
      handleRulesChange({
        ...rules,
        timeLimitMinutes:
          parsed !== undefined && Number.isFinite(parsed) && parsed > 0
            ? parsed
            : undefined,
      });
    },
    [handleRulesChange, rules],
  );

  const handleMaxAttemptsChange = useCallback(
    (value: string) => {
      const parsed = value === "" ? undefined : Number.parseInt(value, 10);
      handleRulesChange({
        ...rules,
        maxAttempts:
          parsed !== undefined && Number.isFinite(parsed) && parsed > 0
            ? parsed
            : undefined,
      });
    },
    [handleRulesChange, rules],
  );

  if (mode === "preview") {
    return (
      <QuizCollectionEditor
        items={quizItems}
        onChange={handleQuizItemsChange}
        submissionMode={
          gradingEnabled ? "server-graded" : "local-practice"
        }
        readOnly={true}
      />
    );
  }

  return (
    <div className="space-y-4">
      <div className="space-y-2">
        <Label>Quiz editor</Label>
        <p className="text-muted-foreground text-xs">
          Use the quiz block editor to build your questions.
        </p>
      </div>

      <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <Label htmlFor="quiz-grading-enabled">Grading</Label>
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              <Badge
                variant={gradingEnabled ? "secondary" : "outline"}
              >
                {gradingEnabled
                  ? `${gradedItemCount} items`
                  : "Off"}
              </Badge>
              {gradingEnabled && (
                <span>{gradedPoints} configured pts</span>
              )}
            </div>
          </div>
          <Switch
            id="quiz-grading-enabled"
            checked={gradingEnabled}
            onCheckedChange={handleGradingEnabledChange}
          />
        </div>

        {gradingEnabled && (
          <div className="mt-3 flex flex-wrap gap-2">
            <Badge variant="outline">Assessment</Badge>
          </div>
        )}
      </div>

      <div className="rounded-lg border border-gray-200 p-4 dark:border-gray-700">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div className="space-y-1">
            <Label>Quiz rules</Label>
            <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
              {rules?.dueAt ? (
                <Badge variant={isQuizPastDue(rules) ? "destructive" : "secondary"}>
                  Due {new Date(rules.dueAt).toLocaleString()}
                </Badge>
              ) : null}
              {rules?.timeLimitMinutes ? (
                <Badge variant="outline">{rules.timeLimitMinutes} min limit</Badge>
              ) : null}
              {rules?.maxAttempts ? (
                <Badge variant="outline">{rules.maxAttempts} attempts</Badge>
              ) : null}
              {!rules?.dueAt && !rules?.timeLimitMinutes && !rules?.maxAttempts ? (
                <span>No rules configured — the quiz is always available.</span>
              ) : null}
            </div>
          </div>
        </div>

        <div className="mt-3 grid gap-3 sm:grid-cols-3">
          <div className="space-y-1">
            <Label htmlFor="quiz-rules-due-at">Due date</Label>
            <Input
              id="quiz-rules-due-at"
              type="datetime-local"
              value={isoToDatetimeLocal(rules?.dueAt)}
              onChange={(event) => handleDueAtChange(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="quiz-rules-time-limit">Time limit (minutes)</Label>
            <Input
              id="quiz-rules-time-limit"
              type="number"
              min={1}
              max={1440}
              value={rules?.timeLimitMinutes ?? ""}
              onChange={(event) => handleTimeLimitChange(event.target.value)}
            />
          </div>
          <div className="space-y-1">
            <Label htmlFor="quiz-rules-max-attempts">Max attempts</Label>
            <Input
              id="quiz-rules-max-attempts"
              type="number"
              min={1}
              max={100}
              value={rules?.maxAttempts ?? ""}
              onChange={(event) => handleMaxAttemptsChange(event.target.value)}
            />
          </div>
        </div>
      </div>

      <QuizCollectionEditor
        items={quizItems}
        onChange={handleQuizItemsChange}
        submissionMode={
          gradingEnabled ? "server-graded" : "local-practice"
        }
      />
    </div>
  );
}
