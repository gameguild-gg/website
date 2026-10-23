'use client';

import {
  updateProfileAction,
  updateProfileVisibilityAction,
  type ProfileFormInput,
  type ProfileVisibilitySetting,
} from '@/lib/user-settings/actions';
import { Link } from '@/i18n/navigation';
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from '@game-guild/ui/components/avatar';
import { Button } from '@game-guild/ui/components/button';
import {
  Card,
  CardAction,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from '@game-guild/ui/components/card';
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
} from '@game-guild/ui/components/field';
import { Input } from '@game-guild/ui/components/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@game-guild/ui/components/select';
import { Separator } from '@game-guild/ui/components/separator';
import { Textarea } from '@game-guild/ui/components/textarea';
import { Eye, ExternalLink, ImagePlus, Link2, Save, UserRound } from 'lucide-react';
import { useTranslations } from 'next-intl';
import { useRouter } from 'next/navigation';
import * as React from 'react';
import { toast } from 'sonner';

interface ProfileFormProps {
  defaultValues: ProfileFormInput;
  accountName: string;
  accountEmail: string;
  defaultVisibility: ProfileVisibilitySetting | null;
  hasCommunityProfile: boolean;
  publicProfilePath: string;
}

type FieldName = keyof ProfileFormInput;
type FieldErrors = Partial<Record<FieldName, string>>;

const FIELD_LIMITS = {
  handle: 80,
  displayName: 100,
  bio: 1000,
  location: 100,
  website: 255,
  jobTitle: 100,
  company: 100,
  avatarUrl: 500,
  bannerUrl: 500,
} as const;

const URL_FIELDS = ['website', 'avatarUrl', 'bannerUrl'] as const;

const VISIBILITY_VALUES = ['public', 'members', 'private'] as const;

function isHttpUrl(value: string): boolean {
  try {
    const url = new URL(value);
    return url.protocol === 'http:' || url.protocol === 'https:';
  } catch {
    return false;
  }
}

function handleInitials(name: string) {
  return name.split(/\s+/).filter(Boolean).slice(0, 2).map((part) => part[0]).join('').toUpperCase();
}

export function ProfileForm({
  defaultValues,
  accountName,
  accountEmail,
  defaultVisibility,
  hasCommunityProfile,
  publicProfilePath,
}: ProfileFormProps) {
  const t = useTranslations('settings.profile');
  const router = useRouter();
  const [fieldErrors, setFieldErrors] = React.useState<FieldErrors>({});
  const [bioLength, setBioLength] = React.useState(defaultValues.bio.length);
  const [isPending, startTransition] = React.useTransition();
  const [visibility, setVisibility] = React.useState<ProfileVisibilitySetting | null>(defaultVisibility);
  const [isVisibilityPending, startVisibilityTransition] = React.useTransition();

  function clearFieldError(field: FieldName) {
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
      avatarUrl: String(formData.get('avatarUrl') ?? ''),
      bannerUrl: String(formData.get('bannerUrl') ?? ''),
    };

    const errors: FieldErrors = {};
    if (input.handle.trim().replace(/^@/, '').length < 3) {
      errors.handle = t('errors.handleRequired');
    }
    if (input.displayName.trim().length === 0) {
      errors.displayName = t('errors.required');
    }
    for (const field of Object.keys(FIELD_LIMITS) as Array<FieldName>) {
      if (input[field].length > FIELD_LIMITS[field]) {
        errors[field] = t('errors.tooLong', { limit: FIELD_LIMITS[field] });
      }
    }
    for (const field of URL_FIELDS) {
      if (input[field].trim() && !isHttpUrl(input[field].trim())) {
        errors[field] = t('errors.url');
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
        router.refresh();
      } else {
        toast.error(t('saveFailed'), { description: result.error });
      }
    });
  }

  function handleVisibilityChange(next: ProfileVisibilitySetting) {
    const previous = visibility;
    setVisibility(next);
    startVisibilityTransition(async () => {
      const result = await updateProfileVisibilityAction(next);
      if (result.success) {
        toast.success(t('visibility.saved'));
      } else {
        if (previous !== null) setVisibility(previous);
        toast.error(t('visibility.saveFailed'), { description: result.error });
      }
    });
  }

  const avatarPreview = defaultValues.avatarUrl.trim();

  return (
    <form onSubmit={handleSubmit} noValidate>
      <FieldGroup>
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <UserRound className="size-4 text-muted-foreground" aria-hidden />
              {t('sections.account')}
            </CardTitle>
            <CardDescription>{t('sections.accountDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
            <div className="flex min-h-10 items-center gap-3 rounded-md border bg-muted/40 px-3 py-2">
              <span className="sr-only">{t('accountLabel')}</span>
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">{accountName}</p>
                <p className="truncate text-xs text-muted-foreground">{accountEmail}</p>
              </div>
            </div>
            <FieldDescription>{t('accountDescription')}</FieldDescription>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <UserRound className="size-4 text-muted-foreground" aria-hidden />
              {t('sections.community')}
            </CardTitle>
            <CardDescription>{t('sections.communityDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <Field data-invalid={fieldErrors.handle ? 'true' : undefined}>
              <FieldLabel htmlFor="profile-handle">{t('handle')}</FieldLabel>
              <div className="relative">
                <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-sm text-muted-foreground">@</span>
                <Input
                  id="profile-handle"
                  name="handle"
                  type="text"
                  required
                  pattern="[a-zA-Z0-9_-]+"
                  minLength={3}
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

            <Field data-invalid={fieldErrors.displayName ? 'true' : undefined}>
              <FieldLabel htmlFor="profile-display-name">
                {t('displayName')}
                <span className="text-destructive" aria-hidden> *</span>
              </FieldLabel>
              <Input
                id="profile-display-name"
                name="displayName"
                type="text"
                required
                maxLength={FIELD_LIMITS.displayName}
                defaultValue={defaultValues.displayName}
                disabled={isPending}
                aria-invalid={Boolean(fieldErrors.displayName)}
                onChange={() => clearFieldError('displayName')}
              />
              {fieldErrors.displayName && <FieldError>{fieldErrors.displayName}</FieldError>}
            </Field>

            <Field data-invalid={fieldErrors.bio ? 'true' : undefined}>
              <FieldLabel htmlFor="profile-bio">{t('bio')}</FieldLabel>
              <Textarea
                id="profile-bio"
                name="bio"
                rows={4}
                maxLength={FIELD_LIMITS.bio}
                defaultValue={defaultValues.bio}
                disabled={isPending}
                aria-invalid={Boolean(fieldErrors.bio)}
                onChange={(event) => {
                  setBioLength(event.currentTarget.value.length);
                  clearFieldError('bio');
                }}
              />
              {fieldErrors.bio && <FieldError>{fieldErrors.bio}</FieldError>}
              <FieldDescription>
                {t('bioCount', { current: bioLength, limit: FIELD_LIMITS.bio })}
              </FieldDescription>
            </Field>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Link2 className="size-4 text-muted-foreground" aria-hidden />
              {t('sections.details')}
            </CardTitle>
            <CardDescription>{t('sections.detailsDescription')}</CardDescription>
          </CardHeader>
          <CardContent>
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
                  onChange={() => clearFieldError('location')}
                />
                <FieldDescription>{t('optional')}</FieldDescription>
              </Field>
              <Field data-invalid={fieldErrors.website ? 'true' : undefined}>
                <FieldLabel htmlFor="profile-website">{t('website')}</FieldLabel>
                <Input
                  id="profile-website"
                  name="website"
                  type="url"
                  inputMode="url"
                  placeholder="https://"
                  maxLength={FIELD_LIMITS.website}
                  defaultValue={defaultValues.website}
                  disabled={isPending}
                  aria-invalid={Boolean(fieldErrors.website)}
                  onChange={() => clearFieldError('website')}
                />
                {fieldErrors.website && <FieldError>{fieldErrors.website}</FieldError>}
                <FieldDescription>{t('optional')}</FieldDescription>
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
                  onChange={() => clearFieldError('jobTitle')}
                />
                <FieldDescription>{t('optional')}</FieldDescription>
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
                  onChange={() => clearFieldError('company')}
                />
                <FieldDescription>{t('optional')}</FieldDescription>
              </Field>
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <ImagePlus className="size-4 text-muted-foreground" aria-hidden />
              {t('sections.images')}
            </CardTitle>
            <CardDescription>{t('sections.imagesDescription')}</CardDescription>
          </CardHeader>
          <CardContent className="space-y-6">
            <div className="flex items-center gap-4">
              <Avatar className="size-16">
                {avatarPreview ? <AvatarImage src={avatarPreview} alt="" /> : null}
                <AvatarFallback>{handleInitials(defaultValues.displayName || accountName)}</AvatarFallback>
              </Avatar>
              <Field className="flex-1" data-invalid={fieldErrors.avatarUrl ? 'true' : undefined}>
                <FieldLabel htmlFor="profile-avatar-url">{t('avatarUrl')}</FieldLabel>
                <Input
                  id="profile-avatar-url"
                  name="avatarUrl"
                  type="url"
                  inputMode="url"
                  placeholder="https://"
                  maxLength={FIELD_LIMITS.avatarUrl}
                  defaultValue={defaultValues.avatarUrl}
                  disabled={isPending}
                  aria-invalid={Boolean(fieldErrors.avatarUrl)}
                  onChange={() => clearFieldError('avatarUrl')}
                />
                {fieldErrors.avatarUrl && <FieldError>{fieldErrors.avatarUrl}</FieldError>}
                <FieldDescription>{t('avatarUrlDescription')}</FieldDescription>
              </Field>
            </div>
            <Separator />
            <Field data-invalid={fieldErrors.bannerUrl ? 'true' : undefined}>
              <FieldLabel htmlFor="profile-banner-url">{t('bannerUrl')}</FieldLabel>
              <Input
                id="profile-banner-url"
                name="bannerUrl"
                type="url"
                inputMode="url"
                placeholder="https://"
                maxLength={FIELD_LIMITS.bannerUrl}
                defaultValue={defaultValues.bannerUrl}
                disabled={isPending}
                aria-invalid={Boolean(fieldErrors.bannerUrl)}
                onChange={() => clearFieldError('bannerUrl')}
              />
              {fieldErrors.bannerUrl && <FieldError>{fieldErrors.bannerUrl}</FieldError>}
              <FieldDescription>{t('bannerUrlDescription')}</FieldDescription>
            </Field>
          </CardContent>
          <CardFooter>
            <Button type="submit" disabled={isPending}>
              <Save className="size-4" />
              {isPending ? t('saving') : t('save')}
            </Button>
          </CardFooter>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Eye className="size-4 text-muted-foreground" aria-hidden />
              {t('visibility.title')}
            </CardTitle>
            <CardDescription>{t('visibility.description')}</CardDescription>
            {hasCommunityProfile ? (
              <CardAction>
                <Link
                  href={publicProfilePath}
                  target="_blank"
                  rel="noreferrer"
                  className="text-sm font-medium text-primary hover:underline inline-flex items-center gap-1"
                >
                  {t('viewPublicProfile')} <ExternalLink className="size-3.5" aria-hidden />
                </Link>
              </CardAction>
            ) : null}
          </CardHeader>
          <CardContent>
            <Field>
              <FieldLabel htmlFor="profile-visibility">{t('visibility.label')}</FieldLabel>
              <Select
                value={visibility ?? undefined}
                onValueChange={(value) => {
                  if (value !== null && (VISIBILITY_VALUES as readonly string[]).includes(value)) {
                    handleVisibilityChange(value as ProfileVisibilitySetting);
                  }
                }}
                disabled={!hasCommunityProfile || isVisibilityPending || visibility === null}
              >
                <SelectTrigger id="profile-visibility" className="w-full sm:w-72">
                  <SelectValue placeholder={t('visibility.placeholder')} />
                </SelectTrigger>
                <SelectContent>
                  {VISIBILITY_VALUES.map((value) => (
                    <SelectItem key={value} value={value}>
                      {t(`visibility.${value}`)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              <FieldDescription>
                {hasCommunityProfile ? t('visibility.hint') : t('visibility.requiresProfile')}
              </FieldDescription>
            </Field>
          </CardContent>
        </Card>
      </FieldGroup>
    </form>
  );
}
