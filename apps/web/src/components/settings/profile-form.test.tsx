import '@testing-library/jest-dom/vitest';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  updateProfileAction: vi.fn(),
  updateProfileVisibilityAction: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
  refresh: vi.fn(),
}));

vi.mock('@/lib/user-settings/actions', () => ({
  updateProfileAction: mocks.updateProfileAction,
  updateProfileVisibilityAction: mocks.updateProfileVisibilityAction,
}));
vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, ...props }: React.ComponentProps<'a'>) => <a {...props}>{children}</a>,
}));
vi.mock('next/navigation', () => ({
  useRouter: () => ({ refresh: mocks.refresh }),
}));
vi.mock('sonner', () => ({
  toast: { success: mocks.toastSuccess, error: mocks.toastError },
}));
vi.mock('next-intl', () => ({
  useTranslations: () => (key: string, values?: Record<string, unknown>) => {
    let message: string = {
      accountLabel: 'Signed-in account',
      accountDescription: 'Your account identity is managed by your sign-in provider.',
      'sections.account': 'Account',
      'sections.accountDescription': 'Your account identity is managed by your sign-in provider.',
      'sections.community': 'Community presence',
      'sections.communityDescription': 'How you appear across feeds, profiles, and community spaces.',
      'sections.details': 'Links and details',
      'sections.detailsDescription': 'Optional details shown on your public profile.',
      'sections.images': 'Profile images',
      'sections.imagesDescription': 'Link an avatar and banner image for your public profile.',
      handle: 'Community handle',
      handleDescription: 'Your unique community address. Use letters, numbers, underscores, or hyphens.',
      displayName: 'Display name',
      bio: 'Bio',
      bioCount: '{current}/{limit} characters',
      optional: 'Optional',
      location: 'Location',
      website: 'Website',
      jobTitle: 'Job title',
      company: 'Company',
      avatarUrl: 'Avatar image URL',
      avatarUrlDescription: 'Square image, at least 200×200px.',
      bannerUrl: 'Banner image URL',
      bannerUrlDescription: 'Wide image, at least 1200×360px.',
      save: 'Save changes',
      saving: 'Saving...',
      saved: 'Profile saved.',
      saveFailed: 'We could not save your profile.',
      viewPublicProfile: 'View public profile',
      'visibility.label': 'Who can view your profile',
      'visibility.placeholder': 'Select visibility',
      'visibility.hint': 'Private profiles return not-found for other members.',
      'visibility.requiresProfile': 'Save your profile once to unlock visibility controls.',
      'errors.tooLong': 'Use at most {limit} characters.',
      'errors.required': 'This field is required.',
      'errors.handleRequired': 'Use at least 3 characters.',
      'errors.url': 'Enter a valid http(s) URL.',
    }[key] ?? key;
    for (const [name, value] of Object.entries(values ?? {})) {
      message = message.replace(`{${name}}`, String(value));
    }
    return message;
  },
}));

import { ProfileForm } from './profile-form';

const defaultProps = {
  accountName: 'Ari Account',
  accountEmail: 'ari@example.com',
  defaultValues: {
    handle: 'ari-games',
    displayName: 'Ari',
    bio: 'Designs games.',
    location: 'São Paulo',
    website: 'https://example.com',
    jobTitle: 'Designer',
    company: 'GameGuild',
    avatarUrl: '',
    bannerUrl: '',
  },
  defaultVisibility: 'public' as const,
  hasCommunityProfile: true,
  publicProfilePath: '/social/profiles/ari-games',
};

describe('ProfileForm', () => {
  afterEach(cleanup);

  it('renders the sectioned fields including image URLs and visibility', () => {
    render(<ProfileForm {...defaultProps} />);

    expect(screen.getByLabelText(/display name/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/community handle/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/avatar image url/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/banner image url/i)).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: /who can view your profile/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view public profile/i })).toHaveAttribute(
      'href',
      '/social/profiles/ari-games',
    );
  });

  it('disables the visibility control until a community profile exists', () => {
    render(<ProfileForm {...defaultProps} defaultVisibility={null} hasCommunityProfile={false} />);

    expect(screen.getByRole('combobox', { name: /who can view your profile/i })).toBeDisabled();
    expect(
      screen.getByText('Save your profile once to unlock visibility controls.'),
    ).toBeInTheDocument();
  });

  it('blocks submission on client-side validation errors without calling the action', () => {
    render(<ProfileForm {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: '' } });
    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    expect(mocks.updateProfileAction).not.toHaveBeenCalled();
    expect(screen.getByText('This field is required.')).toBeInTheDocument();
  });

  it('rejects invalid URLs before submitting', () => {
    render(<ProfileForm {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/website/i), { target: { value: 'not-a-url' } });
    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    expect(mocks.updateProfileAction).not.toHaveBeenCalled();
    expect(screen.getByText('Enter a valid http(s) URL.')).toBeInTheDocument();
  });

  it('saves the profile through the server action and refreshes server data', async () => {
    mocks.updateProfileAction.mockResolvedValue({ success: true, data: undefined });
    render(<ProfileForm {...defaultProps} />);

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => expect(mocks.updateProfileAction).toHaveBeenCalledWith(defaultProps.defaultValues));
    await waitFor(() => expect(mocks.refresh).toHaveBeenCalled());
    expect(mocks.toastSuccess).toHaveBeenCalledWith('Profile saved.');
  });

  it('surfaces action failures without refreshing', async () => {
    mocks.updateProfileAction.mockResolvedValue({
      success: false,
      error: 'That community handle is already taken. Choose another one.',
    });
    render(<ProfileForm {...defaultProps} />);

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => expect(mocks.toastError).toHaveBeenCalled());
    expect(mocks.refresh).not.toHaveBeenCalled();
  });
});
