/**
 * Shared test helpers for auth component testing.
 *
 * Provides mock factories for useAuth, useSession, and router,
 * plus a render wrapper that includes all required providers.
 */

import React, { type ReactElement } from "react";
import { render, type RenderOptions } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";

/* ------------------------------------------------------------------ */
/*  Mock types (mirrors @game-guild/client/react exports)             */
/* ------------------------------------------------------------------ */

export interface MockUseAuthReturn {
  mfaChallenge: { mfaToken: string; availableMethods: string[] } | null;
  startMfaEnrollment: ReturnType<typeof vi.fn>;
  mfaEnrollmentBackupCodes: string[] | null;
  clearMfa: ReturnType<typeof vi.fn>;
  signIn: ReturnType<typeof vi.fn>;
  signUp: ReturnType<typeof vi.fn>;
  signOut: ReturnType<typeof vi.fn>;
  isLoading: boolean;
  error: Error | null;
  clearError: ReturnType<typeof vi.fn>;
}

/* ------------------------------------------------------------------ */
/*  Factories                                                          */
/* ------------------------------------------------------------------ */

export function createMockUseAuth(
  overrides: Partial<MockUseAuthReturn> = {},
): MockUseAuthReturn {
  return {
    mfaChallenge: null,
    startMfaEnrollment: vi.fn(),
    mfaEnrollmentBackupCodes: null,
    clearMfa: vi.fn(),
    signIn: vi.fn().mockResolvedValue(undefined),
    signUp: vi.fn().mockResolvedValue(undefined),
    signOut: vi.fn().mockResolvedValue(undefined),
    isLoading: false,
    error: null,
    clearError: vi.fn(),
    ...overrides,
  };
}

export function createMockRouter(overrides: Record<string, unknown> = {}) {
  return {
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
    ...overrides,
  };
}

/* ------------------------------------------------------------------ */
/*  Custom render                                                      */
/* ------------------------------------------------------------------ */

export function renderWithUser(
  ui: ReactElement,
  options?: Omit<RenderOptions, "wrapper">,
) {
  return {
    user: userEvent.setup(),
    ...render(ui, options),
  };
}
