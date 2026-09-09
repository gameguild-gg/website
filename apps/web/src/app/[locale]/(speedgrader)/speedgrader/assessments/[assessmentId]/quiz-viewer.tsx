'use client';

interface QuizAnswerView {
  key: string;
  value: string;
}

interface QuizGradeItemView {
  blockId: string;
  label: string;
}

/** Render one StructuredAnswer (grading feature vocabulary) readably. */
function answerToText(answer: unknown): string {
  if (answer == null || typeof answer !== 'object') {
    return String(answer ?? '');
  }
  const record = answer as Record<string, unknown>;
  const parts: string[] = [];
  if (Array.isArray(record.selectedOptionIds)) {
    parts.push(`Selected: ${record.selectedOptionIds.join(', ')}`);
  }
  if (record.textAnswers && typeof record.textAnswers === 'object') {
    const texts = Object.entries(record.textAnswers as Record<string, unknown>)
      .map(([prompt, reply]) => `${prompt}: ${String(reply)}`)
      .join('; ');
    if (texts) parts.push(texts);
  }
  if (record.categorizations && typeof record.categorizations === 'object') {
    const cats = Object.entries(record.categorizations as Record<string, unknown>)
      .map(([bucket, ids]) => `${bucket}: ${(Array.isArray(ids) ? ids : []).join(', ')}`)
      .join('; ');
    if (cats) parts.push(cats);
  }
  if (Array.isArray(record.ordering) && record.ordering.length > 0) {
    parts.push(`Order: ${(record.ordering as unknown[]).join(' > ')}`);
  }
  if (typeof record.rating === 'number') {
    parts.push(`Rating: ${record.rating}`);
  }
  return parts.join(' · ') || JSON.stringify(answer);
}

/**
 * Parse a quiz answer envelope into renderable question/answer rows.
 * Tolerant of the legacy `{answer: string}` shape and of an embedded
 * GradeResult-style auto-grade (`gradeResult` key).
 */
export function parseQuizPayload(payload: string): {
  answers: QuizAnswerView[];
  gradeItems: QuizGradeItemView[];
} {
  let parsed: unknown;
  try {
    parsed = JSON.parse(payload);
  } catch {
    return { answers: [{ key: 'Response', value: payload }], gradeItems: [] };
  }
  if (parsed == null || typeof parsed !== 'object') {
    return {
      answers: [{ key: 'Response', value: String(parsed) }],
      gradeItems: [],
    };
  }
  const record = parsed as Record<string, unknown>;

  const answers: QuizAnswerView[] = [];
  if (typeof record.answer === 'string') {
    answers.push({ key: 'answer', value: record.answer });
  }
  if (record.answers && typeof record.answers === 'object') {
    for (const [key, value] of Object.entries(record.answers as Record<string, unknown>)) {
      answers.push({ key, value: answerToText(value) });
    }
  }

  const gradeItems: QuizGradeItemView[] = [];
  const grade = record.gradeResult ?? record.GradeResult;
  if (grade && typeof grade === 'object') {
    const items = (grade as Record<string, unknown>).items;
    if (Array.isArray(items)) {
      for (const item of items) {
        if (item && typeof item === 'object') {
          const row = item as Record<string, unknown>;
          gradeItems.push({
            blockId: String(row.contentBlockId ?? ''),
            label: `${row.isCorrect === true ? 'correct' : (row.status ?? 'graded')} · ${String(row.score ?? '?')}/${String(row.maxScore ?? '?')}`,
          });
        }
      }
    }
  }

  return { answers, gradeItems };
}

/** Quiz response viewer: readable question/answer list and review statuses. */
export function QuizViewer({ payload }: { payload: string }): React.JSX.Element {
  const { answers, gradeItems } = parseQuizPayload(payload);
  return (
    <div data-testid="quiz-viewer" className="space-y-3 rounded-md border bg-card p-4">
      <dl className="space-y-3">
        {answers.map((answer) => (
          <div key={answer.key} className="space-y-1">
            <dt data-testid={`quiz-question-${answer.key}`} className="text-sm font-medium text-foreground">
              {answer.key}
            </dt>
            <dd className="whitespace-pre-wrap rounded bg-muted p-2 text-sm text-muted-foreground">{answer.value}</dd>
          </div>
        ))}
      </dl>
      {gradeItems.length > 0 && (
        <ul className="space-y-1 border-t pt-3 text-sm">
          {gradeItems.map((item) => (
            <li key={item.blockId} data-testid={`quiz-grade-${item.blockId}`}>
              <span className="font-medium">{item.blockId}</span> — {item.label}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
