import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import { createMockUseAuth, renderWithUser, type MockUseAuthReturn } from '@/test/auth-test-helpers';

/* ------------------------------------------------------------------ */
/*  Module mocks                                                       */
/* ------------------------------------------------------------------ */

let mockAuth: MockUseAuthReturn;

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...rest }: { children: React.ReactNode; href: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

vi.mock('next-intl', () => ({
  useLocale: () => 'en-US',
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({ children, href, locale: _locale, ...rest }: { children: React.ReactNode; href: string; locale?: string }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

// Must import AFTER mocks
const { SignInForm } = await import('@/components/sign-in-form');

/* ------------------------------------------------------------------ */
/*  Tests                                                              */
/* ------------------------------------------------------------------ */

describe('SignInForm', () => {
  beforeEach(() => {
    mockAuth = createMockUseAuth();
  });

  /* ---------- Rendering ---------- */

  it('renders the login form with all required elements', () => {
    renderWithUser(<SignInForm />);

    expect(screen.getByText('Welcome back')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in$/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /login with apple/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /login with google/i })).not.toBeInTheDocument();
  });

  it('renders navigation links', () => {
    renderWithUser(<SignInForm />);

    expect(screen.getByText('Sign up')).toHaveAttribute('href', '/sign-up?redirectTo=%2F');
    expect(screen.getByText('Forgot your password?')).toHaveAttribute('href', '/forgot-password');
    // Terms of Service / Privacy Policy moved to the shared (auth) layout footer.
  });

  /* ---------- Client-side validation ---------- */

  it('shows error when email is empty on submit', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(screen.getByText('Email is required.')).toBeInTheDocument();
    expect(mockAuth.signIn).not.toHaveBeenCalled();
  });

  it('shows error when password is empty on submit', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(screen.getByText('Password is required.')).toBeInTheDocument();
    expect(mockAuth.signIn).not.toHaveBeenCalled();
  });

  it('clears field error when user starts typing', async () => {
    const { user } = renderWithUser(<SignInForm />);

    // Trigger email error
    await user.click(screen.getByRole('button', { name: /sign in$/i }));
    expect(screen.getByText('Email is required.')).toBeInTheDocument();

    // Start typing clears error
    await user.type(screen.getByLabelText('Email'), 'a');
    expect(screen.queryByText('Email is required.')).not.toBeInTheDocument();
  });

  /* ---------- Successful submission ---------- */

  it('calls signIn with credentials on valid submission', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.type(screen.getByLabelText('Password'), 'password123');
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(mockAuth.clearError).toHaveBeenCalled();
    expect(mockAuth.signIn).toHaveBeenCalledWith('credentials', {
      email: 'test@example.com',
      password: 'password123',
      rememberMe: false,
      redirectTo: '/',
    });
  });

  /* ---------- Loading state ---------- */

  it('shows loading text and disables inputs while loading', () => {
    mockAuth = createMockUseAuth({ isLoading: true });
    renderWithUser(<SignInForm />);

    expect(screen.getByRole('button', { name: /signing in/i })).toBeDisabled();
    expect(screen.getByLabelText('Email')).toBeDisabled();
    expect(screen.getByLabelText('Password')).toBeDisabled();
  });

  /* ---------- Error display ---------- */

  it('displays API error from useAuth', () => {
    mockAuth = createMockUseAuth({
      error: new Error('Invalid credentials'),
    });
    renderWithUser(<SignInForm />);

    expect(screen.getByText('Invalid credentials')).toBeInTheDocument();
  });

  it('clears error on new submission', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.type(screen.getByLabelText('Password'), 'pass');
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(mockAuth.clearError).toHaveBeenCalled();
  });

  /* ---------- Accessibility ---------- */

  it('sets aria-invalid on email field when validation fails', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true');
  });

  it('sets aria-invalid on password field when validation fails', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(screen.getByLabelText('Password')).toHaveAttribute('aria-invalid', 'true');
  });

  /* ---------- Handles signIn rejection ---------- */

  it('handles signIn rejection gracefully', async () => {
    mockAuth = createMockUseAuth({
      signIn: vi.fn().mockRejectedValue(new Error('Network error')),
    });
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.type(screen.getByLabelText('Password'), 'password123');

    // Should not throw
    await user.click(screen.getByRole('button', { name: /sign in$/i }));
  });

  /* ---------- className forwarding ---------- */

  it('forwards className prop', () => {
    const { container } = renderWithUser(<SignInForm className="custom-class" />);

    expect(container.firstChild).toHaveClass('custom-class');
  });
});
