'use client';

import { Button } from '@game-guild/ui/components/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@game-guild/ui/components/input';
import { Label } from '@game-guild/ui/components/label';
import { Textarea } from '@game-guild/ui/components/textarea';
import { usePathname } from 'next/navigation';
import React, { type FormEvent, useState, useTransition } from 'react';
import { submitProposalInterestAction } from '@/lib/proposals/actions';

type InterestFormState = {
  name: string;
  email: string;
  message: string;
};

const initialState: InterestFormState = {
  name: '',
  email: '',
  message: '',
};

/**
 * Interest form on the public proposal detail page (issue #105): posts a
 * marketing lead with source `course-proposal`, reusing the existing pipeline.
 */
export function ProposalInterestForm({ courseTitle }: { courseTitle: string }): React.JSX.Element {
  const pathname = usePathname();
  const [form, setForm] = useState<InterestFormState>(initialState);
  const [feedback, setFeedback] = useState<{ type: 'success' | 'error'; message: string } | null>(null);
  const [isPending, startTransition] = useTransition();

  function updateField<K extends keyof InterestFormState>(field: K, value: InterestFormState[K]) {
    setForm((current) => ({ ...current, [field]: value }));
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFeedback(null);

    startTransition(async () => {
      const result = await submitProposalInterestAction({
        ...form,
        pagePath: pathname,
      });

      if (!result.success) {
        setFeedback({ type: 'error', message: result.error });
        return;
      }

      setFeedback({ type: 'success', message: result.message });
      setForm(initialState);
    });
  }

  return (
    <Card id="interest">
      <CardHeader>
        <CardTitle>Interested in this course?</CardTitle>
        <CardDescription>
          Tell us what you would want out of “{courseTitle}” — demand signals help us schedule production.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="interest-name">Name</Label>
              <Input
                id="interest-name"
                value={form.name}
                onChange={(event) => updateField('name', event.target.value)}
                required
                autoComplete="name"
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="interest-email">Email</Label>
              <Input
                id="interest-email"
                type="email"
                value={form.email}
                onChange={(event) => updateField('email', event.target.value)}
                required
                autoComplete="email"
              />
            </div>
          </div>
          <div className="space-y-2">
            <Label htmlFor="interest-message">What would you want to learn?</Label>
            <Textarea
              id="interest-message"
              value={form.message}
              onChange={(event) => updateField('message', event.target.value)}
              rows={4}
              required
              minLength={10}
            />
          </div>
          {feedback && (
            <p
              role="status"
              className={feedback.type === 'error' ? 'text-sm text-destructive' : 'text-sm text-primary'}
            >
              {feedback.message}
            </p>
          )}
          <Button type="submit" disabled={isPending}>
            {isPending ? 'Sending…' : 'Register interest'}
          </Button>
        </form>
      </CardContent>
    </Card>
  );
}
