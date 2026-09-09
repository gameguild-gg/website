'use client';

import { useRouter } from '@/i18n/navigation';
import {
  saveTestingEventTemplate,
  setTestingEventTemplateArchived,
} from '@/lib/testing-lab/events-actions';
import type {
  TestingLabQuestionnaireSchema,
  TestingLabTestingEventTemplateProjection,
} from '@game-guild/client';
import { Alert, AlertDescription } from '@game-guild/ui/components/alert';
import { Badge } from '@game-guild/ui/components/badge';
import { Button } from '@game-guild/ui/components/button';
import { Checkbox } from '@game-guild/ui/components/checkbox';
import {
  Field,
  FieldContent,
  FieldDescription,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from '@game-guild/ui/components/field';
import { Input } from '@game-guild/ui/components/input';
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@game-guild/ui/components/select';
import { Separator } from '@game-guild/ui/components/separator';
import { Textarea } from '@game-guild/ui/components/textarea';
import { cn } from '@game-guild/ui/lib/utils';
import {
  AlertCircle,
  Archive,
  CheckCircle2,
  Loader2,
  Plus,
  RotateCcw,
} from 'lucide-react';
import { useState, useTransition, type FormEvent } from 'react';
import { QuestionnaireBuilder } from './questionnaire-builder';

function emptySchema(title: string): TestingLabQuestionnaireSchema {
  return { title, questions: [] };
}

export function TestingEventTemplateManagement({
  templates,
}: {
  templates: TestingLabTestingEventTemplateProjection[];
}) {
  const [selectedId, setSelectedId] = useState<string | null>(
    templates.find((template) => !template.isArchived)?.id ?? null,
  );
  const selected =
    templates.find((template) => template.id === selectedId) ?? null;

  return (
    <div className="grid gap-8 xl:grid-cols-[240px_minmax(0,1fr)]">
      <aside className="flex flex-col gap-3">
        <Button
          type="button"
          variant={selectedId === null ? 'secondary' : 'outline'}
          className="w-full justify-start"
          onClick={() => setSelectedId(null)}
        >
          <Plus data-icon="inline-start" />
          New calendar
        </Button>
        <div className="flex flex-col gap-1" aria-label="Event calendars">
          {templates.map((template) => (
            <button
              key={template.id}
              type="button"
              onClick={() => setSelectedId(template.id ?? null)}
              className={cn(
                'w-full rounded-md px-3 py-2.5 text-left transition-colors',
                selectedId === template.id
                  ? 'bg-accent text-accent-foreground'
                  : 'hover:bg-muted/50',
              )}
            >
              <span className="flex items-center justify-between gap-2">
                <span className="truncate text-sm font-medium">
                  {template.name || 'Untitled calendar'}
                </span>
                {template.isArchived ? (
                  <Badge variant="outline">Archived</Badge>
                ) : null}
              </span>
              <span className="mt-1 block text-xs text-muted-foreground">
                Revision {template.currentRevisionNumber ?? 1}
              </span>
            </button>
          ))}
          {templates.length === 0 ? (
            <p className="px-3 py-4 text-sm text-muted-foreground">
              No calendars yet. Create one to group events and reuse defaults.
            </p>
          ) : null}
        </div>
      </aside>
      <TemplateEditor key={selected?.id ?? 'new'} template={selected} />
    </div>
  );
}

function TemplateEditor({
  template,
}: {
  template: TestingLabTestingEventTemplateProjection | null;
}) {
  const revision = template?.currentRevision;
  const [applicationSchema, setApplicationSchema] =
    useState<TestingLabQuestionnaireSchema>(
      revision?.projectApplicationSchema ?? emptySchema('Project application'),
    );
  const [registrationSchema, setRegistrationSchema] =
    useState<TestingLabQuestionnaireSchema>(
      revision?.testerRegistrationSchema ?? emptySchema('Tester registration'),
    );
  const [pending, startTransition] = useTransition();
  const [result, setResult] = useState<
    Awaited<ReturnType<typeof saveTestingEventTemplate>> | null
  >(null);
  const router = useRouter();

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const formData = new FormData(event.currentTarget);
    formData.set(
      'projectApplicationSchemaJson',
      JSON.stringify(applicationSchema),
    );
    formData.set(
      'testerRegistrationSchemaJson',
      JSON.stringify(registrationSchema),
    );
    startTransition(async () => {
      const next = await saveTestingEventTemplate(formData);
      setResult(next);
      if (next.success) router.refresh();
    });
  }

  function toggleArchived() {
    if (!template?.id) return;
    const formData = new FormData();
    formData.set('templateId', template.id);
    if (template.isArchived) formData.set('restore', 'true');
    startTransition(async () => {
      const next = await setTestingEventTemplateArchived(formData);
      setResult(next);
      if (next.success) router.refresh();
    });
  }

  return (
    <form className="flex max-w-4xl flex-col gap-7" onSubmit={submit}>
      {template?.id ? (
        <input type="hidden" name="templateId" value={template.id} />
      ) : null}
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h2 className="font-semibold">
            {template
              ? 'Create a new calendar revision'
              : 'Create an event calendar'}
          </h2>
          <p className="text-sm text-muted-foreground">
            Events keep the selected revision, so future calendar changes never
            alter existing sessions.
          </p>
        </div>
        {template ? (
          <Button
            type="button"
            size="sm"
            variant="outline"
            disabled={pending}
            onClick={toggleArchived}
          >
            {template.isArchived ? (
              <RotateCcw data-icon="inline-start" />
            ) : (
              <Archive data-icon="inline-start" />
            )}
            {template.isArchived ? 'Restore' : 'Archive'}
          </Button>
        ) : null}
      </div>

      <Separator />

      <FieldGroup className="gap-6">
        <div className="grid gap-5 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="template-name">Calendar name</FieldLabel>
            <Input
              id="template-name"
              name="name"
              defaultValue={template?.name ?? ''}
              required
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="template-description">
              Description
            </FieldLabel>
            <Input
              id="template-description"
              name="description"
              defaultValue={template?.description ?? ''}
            />
          </Field>
        </div>
        <Field>
          <FieldLabel htmlFor="template-rules">General rules</FieldLabel>
          <Textarea
            id="template-rules"
            name="generalRules"
            rows={3}
            defaultValue={revision?.generalRules ?? ''}
            required
          />
        </Field>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="template-candidate-instructions">
              Candidate instructions
            </FieldLabel>
            <Textarea
              id="template-candidate-instructions"
              name="candidateInstructions"
              rows={3}
              defaultValue={revision?.candidateInstructions ?? ''}
              required
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="template-tester-instructions">
              Tester instructions
            </FieldLabel>
            <Textarea
              id="template-tester-instructions"
              name="testerInstructions"
              rows={3}
              defaultValue={revision?.testerInstructions ?? ''}
              required
            />
          </Field>
        </div>
      </FieldGroup>

      <Separator />

      <FieldSet className="gap-5">
        <FieldLegend>Event defaults</FieldLegend>
        <div className="grid gap-5 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="template-mode">Event format</FieldLabel>
            <Select
              name="defaultMode"
              defaultValue={revision?.defaultMode ?? 'Online'}
            >
              <SelectTrigger id="template-mode" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectItem value="Online">Online</SelectItem>
                  <SelectItem value="InPerson">In person</SelectItem>
                  <SelectItem value="Hybrid">Hybrid</SelectItem>
                </SelectGroup>
              </SelectContent>
            </Select>
          </Field>
          <Field>
            <FieldLabel htmlFor="template-approval">
              Project review
            </FieldLabel>
            <Select
              name="defaultApprovalMode"
              defaultValue={revision?.defaultApprovalMode ?? 'ManagerOnly'}
            >
              <SelectTrigger id="template-approval" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectGroup>
                  <SelectItem value="ManagerOnly">
                    Event managers decide
                  </SelectItem>
                  <SelectItem value="Committee">
                    Review committee votes
                  </SelectItem>
                </SelectGroup>
              </SelectContent>
            </Select>
          </Field>
        </div>
        <Field orientation="horizontal">
          <Checkbox
            id="template-feedback"
            name="defaultRequiresFeedback"
            defaultChecked={revision?.defaultRequiresFeedback ?? true}
          />
          <FieldContent>
            <FieldLabel htmlFor="template-feedback">
              Require developer feedback by default
            </FieldLabel>
            <FieldDescription>
              Attendance is completed after the required project feedback is
              submitted.
            </FieldDescription>
          </FieldContent>
        </Field>
      </FieldSet>

      <Separator />

      <section className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h3 className="font-medium">Project application form</h3>
          <p className="text-sm text-muted-foreground">
            Questions for project teams applying to this calendar.
          </p>
        </div>
        <QuestionnaireBuilder
          value={applicationSchema}
          onChange={setApplicationSchema}
        />
      </section>

      <Separator />

      <section className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <h3 className="font-medium">Tester registration form</h3>
          <p className="text-sm text-muted-foreground">
            Questions for testers joining events in this calendar.
          </p>
        </div>
        <QuestionnaireBuilder
          value={registrationSchema}
          onChange={setRegistrationSchema}
        />
      </section>

      {result ? (
        <Alert variant={result.success ? 'default' : 'destructive'}>
          {result.success ? <CheckCircle2 /> : <AlertCircle />}
          <AlertDescription>
            {result.success ? result.message : result.error}
          </AlertDescription>
        </Alert>
      ) : null}

      <div className="flex flex-wrap items-center justify-end gap-3">
        <Button type="submit" disabled={pending || template?.isArchived}>
          {pending ? (
            <Loader2 data-icon="inline-start" className="animate-spin" />
          ) : null}
          {template ? 'Save new revision' : 'Create calendar'}
        </Button>
        {template?.isArchived ? (
          <p className="text-xs text-muted-foreground">
            Restore this calendar before creating another revision.
          </p>
        ) : null}
      </div>
    </form>
  );
}
