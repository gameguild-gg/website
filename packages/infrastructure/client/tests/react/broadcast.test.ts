/**
 * Tests for BroadcastChannel Session Sync
 */

import { describe, it, expect, vi, afterEach } from 'vitest';

describe('createAuthBroadcast', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
    vi.resetModules();
  });

  it('should return no-op when window is undefined (SSR)', async () => {
    // In node environment, window is already undefined
    const { createAuthBroadcast } = await import('../../src/integrations/react/broadcast.js');

    const onMessage = vi.fn();
    const broadcast = createAuthBroadcast(onMessage);

    // Should not throw
    broadcast.send({ type: 'session-update', timestamp: Date.now() });
    broadcast.close();

    expect(onMessage).not.toHaveBeenCalled();
  });

  it('should return no-op when BroadcastChannel is undefined but window exists', async () => {
    // Simulate browser without BroadcastChannel
    vi.stubGlobal('window', { location: {} });
    // Make sure BroadcastChannel is not defined
    vi.stubGlobal('BroadcastChannel', undefined);

    vi.resetModules();
    const { createAuthBroadcast } = await import('../../src/integrations/react/broadcast.js');

    const onMessage = vi.fn();
    const broadcast = createAuthBroadcast(onMessage);

    broadcast.send({ type: 'session-update', timestamp: Date.now() });
    broadcast.close();

    expect(onMessage).not.toHaveBeenCalled();
  });

  // These error-path fixtures expose only the channel methods used by the helper.
  // They do not establish real browser cross-tab behavior.

  it('should handle postMessage errors gracefully', async () => {
    vi.stubGlobal('window', { location: {} });

    const mockChannel: Pick<BroadcastChannel, 'postMessage' | 'close' | 'onmessage'> = {
      postMessage: vi.fn(() => {
        throw new Error('Channel closed');
      }),
      close: vi.fn(),
      onmessage: null,
    };

    const createChannel = vi.fn(function MockBroadcastChannel(_name: string) {
      return mockChannel;
    });
    vi.stubGlobal('BroadcastChannel', createChannel);

    vi.resetModules();
    const { createAuthBroadcast } = await import('../../src/integrations/react/broadcast.js');

    const broadcast = createAuthBroadcast(vi.fn());
    expect(createChannel).toHaveBeenCalledExactlyOnceWith('gg-auth-sync');
    expect(() => broadcast.send({ type: 'session-update', timestamp: Date.now() })).not.toThrow();
    expect(mockChannel.postMessage).toHaveBeenCalledOnce();
  });

  it('should handle close errors gracefully', async () => {
    vi.stubGlobal('window', { location: {} });

    const mockChannel: Pick<BroadcastChannel, 'postMessage' | 'close' | 'onmessage'> = {
      postMessage: vi.fn(),
      close: vi.fn(() => {
        throw new Error('Already closed');
      }),
      onmessage: null,
    };

    const createChannel = vi.fn(function MockBroadcastChannel(_name: string) {
      return mockChannel;
    });
    vi.stubGlobal('BroadcastChannel', createChannel);

    vi.resetModules();
    const { createAuthBroadcast } = await import('../../src/integrations/react/broadcast.js');

    const broadcast = createAuthBroadcast(vi.fn());
    expect(createChannel).toHaveBeenCalledExactlyOnceWith('gg-auth-sync');
    expect(() => broadcast.close()).not.toThrow();
    expect(mockChannel.close).toHaveBeenCalledOnce();
  });

  it('should handle BroadcastChannel constructor error', async () => {
    vi.stubGlobal('window', { location: {} });
    const constructorError = new Error('Not supported');
    const createChannel = vi.fn(function UnsupportedBroadcastChannel(_name: string) {
      throw constructorError;
    });
    vi.stubGlobal('BroadcastChannel', createChannel);

    vi.resetModules();
    const { createAuthBroadcast } = await import('../../src/integrations/react/broadcast.js');

    const onMessage = vi.fn();
    const broadcast = createAuthBroadcast(onMessage);

    expect(createChannel).toHaveBeenCalledExactlyOnceWith('gg-auth-sync');
    expect(createChannel.mock.results[0]).toMatchObject({ type: 'throw', value: constructorError });
    expect(() => broadcast.send({ type: 'sign-out', timestamp: 0 })).not.toThrow();
    expect(() => broadcast.close()).not.toThrow();
  });
});
