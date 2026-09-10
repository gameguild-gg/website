'use client';

import { updateProfileAction } from '@/lib/user-settings/actions';
import type { ProfileFormInput } from '@/lib/user-settings/actions';
import { Button } from '@game-guild/ui/components/button';
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@game-guild/ui/components/field';
import { Input } from '@game-guild/ui/components/input';
import { Textarea } from '@game-guild/ui/components/textarea';
import { Monitor, Save } from 'lucide-react';
import { useTranslations } from 'next-intl';
import * as React from 'react';
import { toast } from 'sonner';

interface ProfileFormProps {
  defaultValues: ProfileFormInput;
  accountName: string;
  accountEmail: string;
}

type FieldErrors = Partial<Record<keyof ProfileFormInput, string>>;

const FIELD_LIMITS = {
  handle: 80,
  displayName: 100,
  bio: 1000,
  location: 100,
  website: 255,
  jobTitle: 100,
  company: 100,
} as const;

export function ProfileForm({ defaultValues, accountName, accountEmail }: ProfileFormProps) {
  const t = useTranslations('settings.profile');
  const [fieldErrors, setFieldErrors] = React.useState<FieldErrors>({});
  const [isPending, startTransition] = React.useTransition();

  function clearFieldError(field: keyof ProfileFormInput) {
    setFieldErrors((prev) => {
      if (!prev[field]) return prev;
      const next = { ...prev };
      delete next[field];
      return next;
    });
  }

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setFieldErrors({});

    const formData = new FormData(event.currentTarget);
    const input: ProfileFormInput = {
      handle: String(formData.get('handle') ?? ''),
      displayName: String(formData.get('displayName') ?? ''),
      bio: String(formData.get('bio') ?? ''),
      location: String(formData.get('location') ?? ''),
      website: String(formData.get('website') ?? ''),
      jobTitle: String(formData.get('jobTitle') ?? ''),
      company: String(formData.get('company') ?? ''),
    };

    const errors: FieldErrors = {};
    for (const field of Object.keys(FIELD_LIMITS) as Array<keyof ProfileFormInput>) {
      if (input[field].length > FIELD_LIMITS[field]) {
        errors[field] = t('errors.tooLong', { limit: FIELD_LIMITS[field] });
      }
    }

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }

    startTransition(async () => {
      const result = await updateProfileAction(input);
      if (result.success) {
        toast.success(t('saved'));
      } else {
        toast.error(t('saveFailed'), { description: result.error });
      }
    });
  }

  return (
    <form onSubmit={handleSubmit} noValidate>
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="profile-account">{t('accountLabel')}</FieldLabel>
          <div className="flex min-h-10 items-center gap-3 rounded-md border bg-muted/40 px-3 py-2">
            <Monitor className="size-4 shrink-0 text-muted-foreground" />
            <div className="min-w-0">
              <p className="truncate text-sm font-medium">{accountName}</p>
              <p className="truncate text-xs text-muted-foreground">{accountEmail}</p>
            </div>
          </div>
          <FieldDescription>{t('accountDescription')}</FieldDescription>
        </Field>

        <Field>
          <FieldLabel htmlFor="profile-handle">{t('handle')}</FieldLabel>
          <div className="relative">
            <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-muted-foreground">@</span>
            <Input
              id="profile-handle"
              name="handle"
              type="text"
              required
              pattern="[a-zA-Z0-9_-]+"
              maxLength={FIELD_LIMITS.handle}
              defaultValue={defaultValues.handle}
              disabled={isPending}
              aria-invalid={Boolean(fieldErrors.handle)}
              className="pl-7"
              onChange={() => clearFieldError('handle')}
            />
          </div>
          {fieldErrors.handle && <FieldError>{fieldErrors.handle}</FieldError>}
          <FieldDescription>{t('handleDescription')}</FieldDescription>
        </Field>

        <Field>
          <FieldLabel htmlFor="profile-display-name">{t('displayName')}</FieldLabel>
          <Input
            id="profile-display-name"
            name="displayName"
            type="text"
            maxLength={FIELD_LIMITS.displayName}
            defaultValue={defaultValues.displayName}
            disabled={isPending}
            aria-invalid={Boolean(fieldErrors.displayName)}
            onChange={() => clearFieldError('displayName')}
          />
          {fieldErrors.displayName && <FieldError>{fieldErrors.displayName}</FieldError>}
        </Field>

        <Field>
          <FieldLabel htmlFor="profile-bio">{t('bio')}</FieldLabel>
          <Textarea
            id="profile-bio"
            name="bio"
            rows={4}
            maxLength={FIELD_LIMITS.bio}
            defaultValue={defaultValues.bio}
            disabled={isPending}
            aria-invalid={Boolean(fieldErrors.bio)}
            onChange={() => clearFieldError('bio')}
          />
          {fieldErrors.bio && <FieldError>{fieldErrors.bio}</FieldError>}
          <FieldDescription>{t('optional')}</FieldDescription>
        </Field>

        <div className="grid gap-6 sm:grid-cols-2">
          <Field>
            <FieldLabel htmlFor="profile-location">{t('location')}</FieldLabel>
            <Input
              id="profile-location"
              name="location"
              type="text"
              maxLength={FIELD_LIMITS.location}
              defaultValue={defaultValues.location}
              disabled={isPending}
              aria-invalid={Boolean(fieldErrors.location)}
              onChange={() => clearFieldError('location')}
            />
            {fieldErrors.location && <FieldError>{fieldErrors.location}</FieldError>}
          </Field>
          <Field>
            <FieldLabel htmlFor="profile-website">{t('website')}</FieldLabel>
            <Input
              id="profile-website"
              name="website"
              type="url"
              placeholder="https://"
              maxLength={FIELD_LIMITS.website}
              defaultValue={defaultValues.website}
              disabled={isPending}
              aria-invalid={Boolean(fieldErrors.website)}
              onChange={() => clearFieldError('website')}
            />
            {fieldErrors.website && <FieldError>{fieldErrors.website}</FieldError>}
          </Field>
          <Field>
            <FieldLabel htmlFor="profile-job-title">{t('jobTitle')}</FieldLabel>
            <Input
              id="profile-job-title"
              name="jobTitle"
              type="text"
              maxLength={FIELD_LIMITS.jobTitle}
              defaultValue={defaultValues.jobTitle}
              disabled={isPending}
              aria-invalid={Boolean(fieldErrors.jobTitle)}
              onChange={() => clearFieldError('jobTitle')}
            />
            {fieldErrors.jobTitle && <FieldError>{fieldErrors.jobTitle}</FieldError>}
          </Field>
          <Field>
            <FieldLabel htmlFor="profile-company">{t('company')}</FieldLabel>
            <Input
              id="profile-company"
              name="company"
              type="text"
              maxLength={FIELD_LIMITS.company}
              defaultValue={defaultValues.company}
              disabled={isPending}
              aria-invalid={Boolean(fieldErrors.company)}
              onChange={() => clearFieldError('company')}
            />
            {fieldErrors.company && <FieldError>{fieldErrors.company}</FieldError>}
          </Field>
        </div>

        <Field>
          <Button type="submit" disabled={isPending}>
            <Save className="size-4" />
            {isPending ? t('saving') : t('save')}
          </Button>
        </Field>
      </FieldGroup>
    </form>
  );
}
