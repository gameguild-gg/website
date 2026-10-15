import { fireEvent, screen, waitFor } from '@testing-library/react';
import { render } from '@testing-library/react';
import { createHash } from 'node:crypto';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuditReplayConsole } from './audit-replay-console';

/** Builds one canonical capture document (exact field order, seven-digit UTC fractions)
 * with its real digest, mirroring what the guarded GET capture returns. */
function buildSnapshot(): { json: string; sha256: string } {
  const user = '60000000-0000-0000-0000-000000000590';
  const session = '60000000-0000-0000-0000-000000000591';
  const secondUser = '60000000-0000-0000-0000-000000000592';
  const events = [
    {
      id: '60000000-0000-0000-0000-000000000601', tenantId: null, createdAt: '2026-10-13T10:00:00.0000000Z',
      actionType: 'Read', resourceType: 'Document', resourceId: 'doc-1', userId: user, sessionId: session,
      correlationId: 'request-1', success: true,
    },
    {
      id: '60000000-0000-0000-0000-000000000602', tenantId: null, createdAt: '2026-10-13T10:00:01.0000000Z',
      actionType: 'Delete', resourceType: 'Document', resourceId: 'doc-1', userId: secondUser, sessionId: null,
      correlationId: 'request-2', success: false,
    },
  ];
  const json = JSON.stringify({
    schemaVersion: 1,
    tenantId: null,
    totalCount: events.length,
    startUtc: '2026-10-13T10:00:00.0000000Z',
    endUtc: '2026-10-13T10:00:02.0000000Z',
    capturedAtUtc: '2026-10-13T10:00:03.0000000Z',
    events,
  });
  return { json, sha256: createHash('sha256').update(json, 'utf8').digest('hex') };
}

describe('AuditReplayConsole', () => {
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.clearAllMocks();
    fetchMock = vi.fn();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  function capturedResponse(): Response {
    const { json, sha256 } = buildSnapshot();
    return new Response(JSON.stringify({ json, sha256, tenantId: null }), {
      status: 200,
      headers: { 'content-type': 'application/json' },
    });
  }

  it('captures a verified snapshot and renders the reconstructed timeline', async () => {
    fetchMock.mockResolvedValue(capturedResponse());

    render(<AuditReplayConsole />);

    const button = screen.getByRole('button', { name: /capture snapshot/i });
    await fireEvent.click(button);

    await waitFor(() => expect(screen.getByTestId('replay-timeline')).toBeInTheDocument());
    expect(fetchMock).toHaveBeenCalledWith(expect.stringContaining('/api/audit/replay?'), expect.objectContaining({
      credentials: 'same-origin',
      cache: 'no-store',
    }));
    // Both events are rendered chronologically; the failed one is flagged.
    const items = screen.getByTestId('replay-events');
    expect(items.textContent).toContain('Read');
    expect(items.textContent).toContain('Delete');
    expect(items.textContent).toContain('failed');
    // Per-user session journeys summarize the parallel activity.
    expect(screen.getByTestId('replay-journeys').textContent).toContain('1 events');
    expect(screen.getByTestId('replay-journeys').textContent).toContain('60000000-0000-0000-0000-000000000590');
    expect(screen.getByTestId('replay-digest').textContent).toContain('2 events');
  });

  it('never renders a timeline when the digest does not match the snapshot bytes', async () => {
    const { json } = buildSnapshot();
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ json, sha256: '0'.repeat(64), tenantId: null }), { status: 200 }),
    );

    render(<AuditReplayConsole />);
    await fireEvent.click(screen.getByRole('button', { name: /capture snapshot/i }));

    await waitFor(() => expect(screen.getByText(/does not match the expected digest/i)).toBeInTheDocument());
    expect(screen.queryByTestId('replay-timeline')).toBeNull();
  });

  it('surfaces proxy rejections', async () => {
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ error: 'Authentication required.' }), { status: 401 }),
    );

    render(<AuditReplayConsole />);
    await fireEvent.click(screen.getByRole('button', { name: /capture snapshot/i }));

    await waitFor(() => expect(screen.getByText('Authentication required.')).toBeInTheDocument());
    expect(screen.queryByTestId('replay-timeline')).toBeNull();
  });

  it('filters the timeline by user id', async () => {
    fetchMock.mockResolvedValue(capturedResponse());
    render(<AuditReplayConsole />);
    await fireEvent.click(screen.getByRole('button', { name: /capture snapshot/i }));
    await waitFor(() => expect(screen.getByTestId('replay-timeline')).toBeInTheDocument());

    const filter = screen.getByLabelText('Filter by user ID');
    await fireEvent.change(filter, { target: { value: '000000000592' } });

    const items = screen.getByTestId('replay-events');
    expect(items.textContent).toContain('Delete');
    expect(items.textContent).not.toContain('Read');
  });
});
