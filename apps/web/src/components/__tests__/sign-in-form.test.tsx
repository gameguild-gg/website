import { describe, it, expect, vi, beforeEach } from 'vitest';
import { screen, waitFor } from '@testing-library/react';
import {
  createMockUseAuth,
  renderWithUser,
  type MockUseAuthReturn,
} from '@/test/auth-test-helpers';

/* ------------------------------------------------------------------ */
/*  Module mocks                                                       */
/* ------------------------------------------------------------------ */

let mockAuth: MockUseAuthReturn;

vi.mock('@game-guild/client/react', () => ({
  useAuth: () => mockAuth,
}));

vi.mock('@/i18n/navigation', () => ({
  Link: ({
    children,
    href,
    locale,
    ...rest
  }: {
    children: React.ReactNode;
    href: string;
    locale?: string;
  }) => (
    <a href={locale ? `/${locale}${href}` : href} data-locale={locale} {...rest}>
      {children}
    </a>
  ),
}));

// Must import AFTER mocks
const { SignInForm } = await import('@/components/sign-in-form');
const { GoogleSignInButton } = await import('@/components/google-sign-in-button');
const { __resetGisForTest } = await import('@/components/use-google-identity-service');

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
    renderWithUser(
      <SignInForm
        magicLinkLabel="Sign in with an email link"
        emailCodeLabel="Sign in with an email code"
      />,
    );

    expect(screen.getByText('Sign up')).toHaveAttribute('href', '/sign-up?redirectTo=%2F');
    expect(screen.getByText('Forgot your password?')).toHaveAttribute(
      'href',
      '/forgot-password'
    );
    expect(screen.getByText('Sign in with an email link')).toHaveAttribute(
      'href',
      '/magic-link'
    );
    expect(screen.getByText('Sign in with an email code')).toHaveAttribute(
      'href',
      '/email-code'
    );
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

  it('forwards remember-me opt-in to signIn', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.type(screen.getByLabelText('Password'), 'password123');
    await user.click(screen.getByLabelText('Remember me on this device'));
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(mockAuth.signIn).toHaveBeenCalledWith('credentials', {
      email: 'test@example.com',
      password: 'password123',
      rememberMe: true,
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

    expect(screen.getByLabelText('Email')).toHaveAttribute(
      'aria-invalid',
      'true'
    );
  });

  it('sets aria-invalid on password field when validation fails', async () => {
    const { user } = renderWithUser(<SignInForm />);

    await user.type(screen.getByLabelText('Email'), 'test@example.com');
    await user.click(screen.getByRole('button', { name: /sign in$/i }));

    expect(screen.getByLabelText('Password')).toHaveAttribute(
      'aria-invalid',
      'true'
    );
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
    const { container } = renderWithUser(
      <SignInForm className="custom-class" />
    );

    expect(container.firstChild).toHaveClass('custom-class');
  });

  /* ---------- Providers slot ---------- */

  it('renders providers slot above Email when passed', () => {
    renderWithUser(
      <SignInForm providers={<div data-testid="slot-sentinel">PROVIDER</div>} />
    );

    const sentinel = screen.getByTestId('slot-sentinel');
    const email = screen.getByLabelText('Email');
    expect(
      sentinel.compareDocumentPosition(email) & Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();
  });

  it('does not render divider when no providers passed', () => {
    renderWithUser(<SignInForm />);

    expect(screen.queryByText('or sign in with email')).not.toBeInTheDocument();
  });

  it('renders divider with providers passed', () => {
    renderWithUser(<SignInForm providers={<span>GOOGLE</span>} />);

    expect(screen.getByText('or sign in with email')).toBeInTheDocument();
  });

  it('existing rendering assertions still pass with providers slot', () => {
    renderWithUser(<SignInForm providers={<span>GOOGLE</span>} />);

    expect(screen.getByText('Welcome back')).toBeInTheDocument();
    expect(screen.getByLabelText('Email')).toBeInTheDocument();
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /sign in$/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /login with google/i })).not.toBeInTheDocument();
  });
});

/* ------------------------------------------------------------------ */
/*  Providers slot composition (real GoogleSignInButton)               */
/* ------------------------------------------------------------------ */

describe("SignInForm MFA", () => {
  beforeEach(() => {
    mockAuth = createMockUseAuth();
  });

  it("completes the limited challenge without resending a password or tenant", async () => {
    mockAuth.mfaChallenge = {
      mfaToken: "x".repeat(43),
      availableMethods: ["TOTP", "BackupCode"],
    };
    const { user } = renderWithUser(<SignInForm redirectTo="/dashboard" />);
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
    await user.type(screen.getByLabelText("Authentication code"), "123456");
    await user.click(
      screen.getByRole("button", { name: "Verify and sign in" }),
    );
    expect(mockAuth.signIn).toHaveBeenCalledWith("credentials", {
      mfaToken: "x".repeat(43),
      method: "Totp",
      code: "123456",
      redirectTo: "/dashboard",
    });
  });

  it("shows the server setup key inside the existing form after starting enrollment", async () => {
    mockAuth.mfaChallenge = {
      mfaToken: "x".repeat(43),
      availableMethods: ["TOTP"],
    };
    mockAuth.startMfaEnrollment.mockResolvedValue({
      success: true,
      secretKey: "LOCAL-AUTHENTICATOR-KEY",
      expiresAt: new Date(Date.now() + 300000).toISOString(),
      qrCodeUri: "otpauth://totp/Example",
    });
    const { user } = renderWithUser(<SignInForm />);
    await user.click(
      screen.getByRole("button", { name: "Set up authenticator" }),
    );
    expect(
      await screen.findByText("LOCAL-AUTHENTICATOR-KEY"),
    ).toBeInTheDocument();
    expect(mockAuth.signIn).not.toHaveBeenCalled();
  });

  it("displays recovery codes once and offers acknowledgment", () => {
    mockAuth.mfaEnrollmentBackupCodes = ["ONE-TIME-RECOVERY"];
    renderWithUser(<SignInForm />);
    expect(screen.getByText("ONE-TIME-RECOVERY")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "I saved my codes — continue" }),
    ).toBeInTheDocument();
    expect(screen.queryByLabelText("Password")).not.toBeInTheDocument();
  });
});

describe('SignInForm providers slot composition', () => {
  let initializeMock: ReturnType<typeof vi.fn>;
  let renderButtonMock: ReturnType<typeof vi.fn>;
  let promptMock: ReturnType<typeof vi.fn>;
  let disableAutoSelectMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    mockAuth = createMockUseAuth();
    initializeMock = vi.fn();
    renderButtonMock = vi.fn();
    promptMock = vi.fn();
    disableAutoSelectMock = vi.fn();

    // Reset the module-level singleton guards so the GIS hook gets a clean
    // initialize call, then pre-seed window.google so the hook's
    // "script already loaded" branch short-circuits (no real <script>).
    __resetGisForTest();
    (globalThis as unknown as { google: unknown }).google = {
      accounts: {
        id: {
          initialize: initializeMock,
          renderButton: renderButtonMock,
          prompt: promptMock,
          disableAutoSelect: disableAutoSelectMock,
        },
      },
    };
    process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID = 'test-google-client-id';
  });

  afterEach(() => {
    delete (globalThis as unknown as { google?: unknown }).google;
  });

  it('renders the Google button above Email inside the same Card, with one divider', async () => {
    renderWithUser(<SignInForm providers={<GoogleSignInButton />} />);

    // GIS surface must hydrate the branded button before layout assertions.
    await waitFor(() => {
      expect(renderButtonMock).toHaveBeenCalledTimes(1);
    });

    // (a) Ordering: Google button container precedes the Email label.
    const googleBtn = screen.getByTestId('google-sign-in-button');
    const emailInput = screen.getByLabelText('Email');
    expect(
      googleBtn.compareDocumentPosition(emailInput) &
        Node.DOCUMENT_POSITION_FOLLOWING
    ).toBeTruthy();

    // (b) Unification: both share the same nearest Card root.
    const cardRoot = googleBtn.closest('[data-slot="card"]');
    const emailCard = emailInput.closest('[data-slot="card"]');
    expect(cardRoot).not.toBeNull();
    expect(cardRoot).toBe(emailCard);

    // (c) "or sign in with email" divider present exactly once.
    expect(screen.getByText('or sign in with email')).toBeInTheDocument();
    expect(screen.getAllByText('or sign in with email')).toHaveLength(1);
  });
});
