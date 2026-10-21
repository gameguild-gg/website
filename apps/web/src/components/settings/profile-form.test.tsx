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
    let message: string = key;
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

    expect(screen.getByLabelText(/displayName/i)).toBeInTheDocument();
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
    expect(screen.getByText('visibility.requiresProfile')).toBeInTheDocument();
  });

  it('blocks submission on client-side validation errors without calling the action', () => {
    render(<ProfileForm {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/display name/i), { target: { value: '' } });
    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    expect(mocks.updateProfileAction).not.toHaveBeenCalled();
    expect(screen.getByText('errors.required')).toBeInTheDocument();
  });

  it('rejects invalid URLs before submitting', () => {
    render(<ProfileForm {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/website/i), { target: { value: 'not-a-url' } });
    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    expect(mocks.updateProfileAction).not.toHaveBeenCalled();
    expect(screen.getByText('errors.url')).toBeInTheDocument();
  });

  it('saves the profile through the server action and refreshes server data', async () => {
    mocks.updateProfileAction.mockResolvedValue({ success: true, data: undefined });
    render(<ProfileForm {...defaultProps} />);

    fireEvent.submit(screen.getByRole('button', { name: /save/i }).closest('form')!);

    await waitFor(() => expect(mocks.updateProfileAction).toHaveBeenCalledWith(defaultProps.defaultValues));
    await waitFor(() => expect(mocks.refresh).toHaveBeenCalled());
    expect(mocks.toastSuccess).toHaveBeenCalledWith('saved');
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
