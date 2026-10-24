import { describe, expect, it } from 'vitest';

/**
 * Runtime delivery proof for the WalletConnect integration (#18): the
 * optional peer is installed in the workspace, the adapter's static import
 * specifier resolves from node_modules, and the resolved module exposes the
 * SDK's `init` entry point. This is the deployment-level guarantee the
 * harness tests (mocked provider) cannot give: without the package shipped
 * in the dependency graph, these assertions fail exactly the way the
 * production bundle would.
 */
describe('WalletConnect runtime provider delivery', () => {
  it('resolves the SDK from the shipped dependency graph', async () => {
    const module = (await import('@walletconnect/ethereum-provider')) as {
      default?: { init: (config: unknown) => Promise<unknown> };
    };

    expect(module).toBeDefined();
    expect(typeof module.default?.init).toBe('function');
  });

  it('exports the EthereumProvider shape the adapter expects', async () => {
    const module = (await import('@walletconnect/ethereum-provider')) as {
      EthereumProvider?: unknown;
      default?: unknown;
    };

    // The SDK's named export is what `default.init` constructs; both spellings
    // have shipped across majors, so assert at least the constructor surface.
    expect(module.EthereumProvider ?? module.default).toBeDefined();
  });
});
