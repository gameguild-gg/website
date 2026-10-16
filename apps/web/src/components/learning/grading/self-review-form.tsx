'use client';

import { useMemo, useState } from 'react';
import type {
  SelfReviewEvidenceV1,
  SelfReviewRuntimeStateV1,
} from '@game-guild/grading';
import { Button } from '@game-guild/ui/components/button';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import { Loader2, Save, Send } from 'lucide-react';
import {
  pointsToScoreUnits,
  scoreUnitsToPoints,
} from '@/lib/learning/academic-values';

export interface SelfReviewFormItem {
  itemId: string;
  label: string;
  maxScore: number;
}

interface SelfReviewFormProps {
  items: SelfReviewFormItem[];
  state: SelfReviewRuntimeStateV1;
  busy?: boolean;
  onSaveDraft?: (evidence: SelfReviewEvidenceV1) => Promise<void>;
  onSubmit: (evidence: SelfReviewEvidenceV1) => Promise<void>;
}

interface ItemDraft {
  points: string;
  feedback: string;
}

export function SelfReviewForm({
  items,
  state,
  busy = false,
  onSaveDraft,
  onSubmit,
}: SelfReviewFormProps): React.JSX.Element {
  const [drafts, setDrafts] = useState<Record<string, ItemDraft>>({});
  const [overallFeedback, setOverallFeedback] = useState('');
  const [localError, setLocalError] = useState<string | null>(null);
  const readOnly = state.status === 'completed' || state.submittedEvidence != null;

  const syncDeps: unknown[] = [
    items,
    state.draftVersion,
    state.reviewStageId,
    state.submittedEvidence,
  ];
  const [lastSyncDeps, setLastSyncDeps] = useState<unknown[] | null>(null);
  if (
    lastSyncDeps === null ||
    lastSyncDeps.length !== syncDeps.length ||
    lastSyncDeps.some((dep, index) => !Object.is(dep, syncDeps[index]))
  ) {
    setLastSyncDeps(syncDeps);
    const evidence = state.submittedEvidence ?? state.draft;
    const byItem = new Map(
      (evidence?.items ?? []).map((item) => [item.itemId, item]),
    );
    setDrafts(
      Object.fromEntries(
        items.map((item) => {
          const current = byItem.get(item.itemId);
          return [
            item.itemId,
            {
              points:
                current == null ? '' : String(scoreUnitsToPoints(current.score)),
              feedback: current?.feedback ?? '',
            },
          ];
        }),
      ),
    );
    setOverallFeedback(evidence?.feedback ?? '');
    setLocalError(null);
  }

  const evidence = useMemo(() => {
    try {
      const reviewItems = items.map((item) => {
        const current = drafts[item.itemId];
        const score = pointsToScoreUnits(current?.points ?? '');
        if (score < 0 || score > item.maxScore) {
          throw new Error(
            `Score for ${item.label} must be between 0 and ${scoreUnitsToPoints(item.maxScore)}.`,
          );
        }
        const feedback = current?.feedback.trim() || null;
        if (state.requireFeedback && !feedback && !overallFeedback.trim()) {
          throw new Error(`Feedback is required for ${item.label}.`);
        }
        return { itemId: item.itemId, score, feedback };
      });
      return {
        value: {
          schemaVersion: 1,
          items: reviewItems,
          feedback: overallFeedback.trim() || null,
        } satisfies SelfReviewEvidenceV1,
        error: null,
      };
    } catch (error) {
      return {
        value: null,
        error: error instanceof Error ? error.message : 'Invalid self review.',
      };
    }
  }, [drafts, items, overallFeedback, state.requireFeedback]);

  async function execute(
    action: (next: SelfReviewEvidenceV1) => Promise<void>,
  ) {
    if (!evidence.value) {
      setLocalError(evidence.error);
      return;
    }
    setLocalError(null);
    await action(evidence.value);
  }

  return (
    <section className="space-y-4 border-t pt-6">
      <div>
        <h3 className="font-semibold">Self review</h3>
        <p className="text-sm text-muted-foreground">
          {state.instructions || 'Assess your response before the workflow continues.'}
        </p>
      </div>

      {items.map((item) => (
        <div
          key={item.itemId}
          className="grid gap-3 border-b pb-4 last:border-b-0 md:grid-cols-[minmax(0,1fr)_8rem]"
        >
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor={`self-feedback-${item.itemId}`}>
              {item.label}
            </label>
            <Textarea
              id={`self-feedback-${item.itemId}`}
              value={drafts[item.itemId]?.feedback ?? ''}
              onChange={(event) =>
                setDrafts((current) => ({
                  ...current,
                  [item.itemId]: {
                    ...(current[item.itemId] ?? { points: '' }),
                    feedback: event.target.value,
                  },
                }))
              }
              placeholder="Explain your assessment"
              rows={2}
              disabled={readOnly || busy}
            />
          </div>
          <div className="space-y-2">
            <label className="text-sm font-medium" htmlFor={`self-score-${item.itemId}`}>
              Score / {scoreUnitsToPoints(item.maxScore)}
            </label>
            <Input
              id={`self-score-${item.itemId}`}
              type="number"
              min={0}
              max={scoreUnitsToPoints(item.maxScore)}
              step="0.01"
              value={drafts[item.itemId]?.points ?? ''}
              onChange={(event) =>
                setDrafts((current) => ({
                  ...current,
                  [item.itemId]: {
                    ...(current[item.itemId] ?? { feedback: '' }),
                    points: event.target.value,
                  },
                }))
              }
              disabled={readOnly || busy}
            />
          </div>
        </div>
      ))}

      <Textarea
        aria-label="Overall self-review feedback"
        value={overallFeedback}
        onChange={(event) => setOverallFeedback(event.target.value)}
        placeholder="Overall reflection"
        rows={3}
        disabled={readOnly || busy}
      />

      {(localError || evidence.error) && !readOnly && (
        <p role="alert" className="text-sm text-destructive">
          {localError || evidence.error}
        </p>
      )}

      {readOnly ? (
        <p className="text-sm text-muted-foreground">Self review submitted.</p>
      ) : (
        <div className="flex flex-wrap gap-2">
          {onSaveDraft && (
            <Button
              type="button"
              variant="outline"
              disabled={busy || !evidence.value}
              onClick={() => void execute(onSaveDraft)}
            >
              {busy ? <Loader2 className="mr-2 size-4 animate-spin" /> : <Save className="mr-2 size-4" />}
              Save draft
            </Button>
          )}
          <Button
            type="button"
            disabled={busy || !evidence.value}
            onClick={() => void execute(onSubmit)}
          >
            {busy ? <Loader2 className="mr-2 size-4 animate-spin" /> : <Send className="mr-2 size-4" />}
            Submit self review
          </Button>
        </div>
      )}
    </section>
  );
}
