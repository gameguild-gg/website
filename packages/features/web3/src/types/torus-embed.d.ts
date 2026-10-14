/**
 * Ambient declaration for the lazily-imported `@toruslabs/torus-embed` SDK.
 *
 * The web3 package must type-check without the SDK installed (it is an
 * optional runtime dependency, loaded via dynamic `import()` and failing
 * closed when absent). Only the surface the adapter touches is declared;
 * see `TorusAdapterOptions.loadEmbed` to inject an alternative embed.
 */
import type { Eip1193Provider } from './index';

declare module '@toruslabs/torus-embed' {
  /** Torus embed widget (the subset used by GameGuild SIWE). */
  export default class Torus {
    /** EIP-1193 provider exposed by the embed once initialized. */
    provider?: Eip1193Provider;
    constructor(options?: { baseUrl?: string; enableLogging?: boolean });
    init(options?: Record<string, unknown>): Promise<unknown>;
    login(): Promise<unknown>;
    logout(): Promise<unknown>;
    cleanUp(): Promise<unknown>;
  }
}
