import '@testing-library/jest-dom/vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { hydrateRoot, type Root } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { beforeEach, describe, expect, it, vi } from 'vitest';

const mocks = vi.hoisted(() => ({
  renameSession: vi.fn(),
  terminateSession: vi.fn(),
  terminateOtherSessions: vi.fn(),
  terminateAllSessions: vi.fn(),
  trustCurrentDevice: vi.fn(),
  revokeTrustedDevice: vi.fn(),
  toastSuccess: vi.fn(),
  toastError: vi.fn(),
}));

vi.mock('@/lib/auth/sessions-actions', () => ({
  renameSessionAction: mocks.renameSession,
  terminateSessionAction: mocks.terminateSession,
  terminateOtherSessionsAction: mocks.terminateOtherSessions,
  terminateAllSessionsAction: mocks.terminateAllSessions,
  trustCurrentDeviceAction: mocks.trustCurrentDevice,
  revokeTrustedDeviceAction: mocks.revokeTrustedDevice,
}));

vi.mock('next-intl', () => ({
  useTranslations: (namespace: string) => (key: string, values?: Record<string, unknown>) => {
    let message = `${namespace}.${key}`;
    for (const [name, value] of Object.entries(values ?? {})) {
      message = message.replace(`{${name}}`, String(value));
    }
    return message;
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: mocks.toastSuccess,
    error: mocks.toastError,
  },
}));

const { SessionsManager } = await import('./sessions-manager');

const CURRENT_SESSION_ID = '11111111-1111-1111-1111-111111111111';
const OTHER_SESSION_ID = '22222222-2222-2222-2222-222222222222';
const TRUSTED_DEVICE_ID = '33333333-3333-3333-3333-333333333333';

function makeData() {
  return {
    sessions: [
      {
        id: CURRENT_SESSION_ID,
        isCurrent: true,
        isTrustedDevice: false,
        lastUsedAt: '2026-10-13T12:00:00Z',
        ipAddress: '203.0.113.10',
        deviceInfo: {
          deviceName: 'Work laptop',
          browser: 'Chrome',
          operatingSystem: 'Windows',
          isMobile: false,
        },
        location: { displayLocation: 'Sao Paulo, Brazil' },
      },
      {
        id: OTHER_SESSION_ID,
        isCurrent: false,
        isTrustedDevice: true,
        lastUsedAt: '2026-10-12T08:30:00Z',
        ipAddress: '198.51.100.7',
        deviceInfo: { deviceName: null, browser: 'Safari', operatingSystem: 'iOS', isMobile: true },
        location: { displayLocation: 'Lisbon, Portugal' },
      },
    ],
    analysis: {
      activeSessionCount: 2,
      totalDeviceCount: 2,
      riskLevel: 'Low' as const,
      riskFactors: [],
    },
    trustedDevices: [
      {
        id: TRUSTED_DEVICE_ID,
        deviceName: 'Old phone',
        trustedAt: '2026-09-01T10:00:00Z',
        expiresAt: null,
        lastUsedAt: '2026-10-01T10:00:00Z',
      },
    ],
  };
}

describe('SessionsManager', () => {
  beforeEach(() => vi.clearAllMocks());

  it('keeps every server-rendered session mutation disabled until hydration', () => {
    const container = document.createElement('div');
    container.innerHTML = renderToString(<SessionsManager data={makeData()} locale="en-US" />);
    const buttons = container.querySelectorAll('button');
    expect(buttons.length).toBeGreaterThan(0);
    for (const button of buttons) {
      expect(button).toBeDisabled();
    }
    expect(mocks.trustCurrentDevice).not.toHaveBeenCalled();
  });

  it('enables the hydrated controls and sends the first Trust click exactly once', async () => {
    const user = userEvent.setup();
    const data = makeData();
    const container = document.createElement('div');
    container.innerHTML = renderToString(<SessionsManager data={data} locale="en-US" />);
    document.body.appendChild(container);
    const recoverableError = vi.fn();
    mocks.trustCurrentDevice.mockResolvedValue({ success: true, status: 'success' });
    let root: Root | undefined;
    try {
      const trust = within(container).getByRole('button', { name: 'settings.security.trustedDevices.trustCurrentButton' });
      expect(trust).toBeDisabled();
      await act(async () => {
        root = hydrateRoot(container, <SessionsManager data={data} locale="en-US" />, { onRecoverableError: recoverableError });
      });
      await waitFor(() => expect(trust).toBeEnabled());
      await user.click(trust);
      await waitFor(() => expect(mocks.trustCurrentDevice).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(mocks.toastSuccess).toHaveBeenCalled());
      expect(recoverableError).not.toHaveBeenCalled();
    } finally {
      if (root) await act(async () => root?.unmount());
      container.remove();
    }
  });

  it('renders sessions with device, location, and the current-session badge', () => {
    render(<SessionsManager data={makeData()} locale="en-US" />);

    expect(screen.getByTestId(`session-row-${CURRENT_SESSION_ID}`)).toBeInTheDocument();
    expect(screen.getByTestId('current-session-badge')).toBeInTheDocument();
    expect(screen.getByText('Work laptop')).toBeInTheDocument();
    expect(screen.getByText(/Sao Paulo, Brazil/)).toBeInTheDocument();
    expect(screen.getByText(/Lisbon, Portugal/)).toBeInTheDocument();
  });

  it('shows the security analysis risk badge and trusted devices', () => {
    render(<SessionsManager data={makeData()} locale="en-US" />);

    expect(screen.getByText('settings.security.analysis.risk.low')).toBeInTheDocument();
    expect(screen.getByTestId(`trusted-device-row-${TRUSTED_DEVICE_ID}`)).toBeInTheDocument();
    expect(screen.getByText('Old phone')).toBeInTheDocument();
  });

  it('renames a session through the server action', async () => {
    const user = userEvent.setup();
    mocks.renameSession.mockResolvedValue({ success: true, status: 'success' });
    render(<SessionsManager data={makeData()} locale="en-US" />);

    await user.click(screen.getAllByRole('button', { name: 'settings.security.rename.aria' })[0]);
    const input = screen.getByTestId(`rename-input-${CURRENT_SESSION_ID}`);
    await user.clear(input);
    await user.type(input, 'Main desktop');
    await user.click(screen.getByTestId(`rename-save-${CURRENT_SESSION_ID}`));

    await waitFor(() =>
      expect(mocks.renameSession).toHaveBeenCalledWith(CURRENT_SESSION_ID, 'Main desktop'),
    );
    await waitFor(() => expect(mocks.toastSuccess).toHaveBeenCalled());
  });

  it('rejects empty session names without calling the server', async () => {
    const user = userEvent.setup();
    render(<SessionsManager data={makeData()} locale="en-US" />);

    await user.click(screen.getAllByRole('button', { name: 'settings.security.rename.aria' })[0]);
    await user.clear(screen.getByTestId(`rename-input-${CURRENT_SESSION_ID}`));
    await user.click(screen.getByTestId(`rename-save-${CURRENT_SESSION_ID}`));

    expect(mocks.renameSession).not.toHaveBeenCalled();
    expect(mocks.toastError).toHaveBeenCalledWith('settings.security.rename.nameRequired');
  });

  it('terminates another session and revokes a trusted device through server actions', async () => {
    const user = userEvent.setup();
    mocks.terminateSession.mockResolvedValue({ success: true, status: 'success' });
    mocks.revokeTrustedDevice.mockResolvedValue({ success: true, status: 'success' });
    render(<SessionsManager data={makeData()} locale="en-US" />);

    // The terminate button exposes an aria-label (which wins over the visible text), and the
    // identity translation mock does not interpolate the {name} placeholder, so the accessible
    // name is the bare key.
    await user.click(screen.getByRole('button', { name: 'settings.security.sessions.terminateAria' }));
    await waitFor(() => expect(mocks.terminateSession).toHaveBeenCalledWith(OTHER_SESSION_ID));

    await user.click(screen.getByRole('button', { name: 'settings.security.trustedDevices.revoke' }));
    await waitFor(() => expect(mocks.revokeTrustedDevice).toHaveBeenCalledWith(TRUSTED_DEVICE_ID));
  });
});
