// `<emception-run>` custom element.
//
// Surface:
//   - Attributes: kebab-case mirror of `ViewConfigInput`. Recognized names
//     come from the canonical `ATTRIBUTE_SCHEMA` exported by
//     `emception` so the schema lives in exactly one place. Unknown
//     attributes are ignored (matching HTML's permissive parsing model).
//   - Slots:
//       <textarea slot="stdin">   — initial stdin payload
//       <canvas slot="canvas">    — rendering surface for SDL / GUI presets
//   - Events:
//       `emception-ready`  — fired after first successful run
//       `emception-exit`   — fired with `{ exitCode }` when a run finishes
//   - Properties: the element exposes `api: BrowserEmceptionAPI | null`.
//     Setting this attaches the element to a pre-built browser API.
//     If the `autorun` attribute is present the element triggers an
//     initial compile+run automatically.
//   - Methods: `run()` — trigger a compile+run cycle imperatively.
//
// All non-DOM work (attribute parsing) is done by `emception`;
// the compile+run pipeline uses `compileAndRun` from `@emception/browser`.

import { compileAndRun, type EmceptionAPI as BrowserEmceptionAPI } from '@gameguild/emception-browser';
import { parseAttributesToInput, EVENT_DOM_NAMES, ToolchainPreset, type ViewConfigInput } from 'emception';

export const ELEMENT_NAME = 'emception-run';

const TEMPLATE = `
<style>
  :host { display: block; font-family: system-ui, sans-serif; }
  [part='shell'] { display: grid; gap: 0.5rem; }
  [part='output'] {
    background: #111; color: #eee; padding: 0.5rem;
    font: 12px ui-monospace, monospace; min-height: 4rem;
    white-space: pre-wrap; overflow: auto;
  }
  [part='canvas-slot'][hidden] { display: none; }
  [part='stdin-slot'][hidden] { display: none; }
</style>
<div part="shell">
  <div part="output"></div>
  <div part="canvas-slot"><slot name="canvas"></slot></div>
  <div part="stdin-slot"><slot name="stdin"></slot></div>
</div>
`;

/**
 * The custom element class. Exported so consumers can subclass or
 * register under a different tag name.
 */
export class EmceptionRunElement extends HTMLElement {
    static get observedAttributes(): string[] {
        return [
            'preset',
            'manifest-url',
            'workspace',
            'source',
            'seed-url',
            'build-url',
            'seed-policy',
            'autorun',
            'canvas',
            'show-hidden',
            'show-solution',
            'output',
            'flags',
            'ldflags',
            'libs',
            'include-paths',
            'lib-paths',
        ];
    }

    private outputEl: HTMLDivElement | null = null;
    private canvasSlotEl: HTMLDivElement | null = null;
    private stdinSlotEl: HTMLDivElement | null = null;
    private currentApi: BrowserEmceptionAPI | null = null;
    private apiUnsubs: Array<() => void> = [];

    connectedCallback(): void {
        if (!this.shadowRoot) {
            const root = this.attachShadow({ mode: 'open' });
            root.innerHTML = TEMPLATE;
        }
        const root = this.shadowRoot!;
        this.outputEl = root.querySelector('[part="output"]')!;
        this.canvasSlotEl = root.querySelector('[part="canvas-slot"]') as HTMLDivElement;
        this.stdinSlotEl = root.querySelector('[part="stdin-slot"]') as HTMLDivElement;
        this.refreshSlotVisibility();
    }

    disconnectedCallback(): void {
        this.currentApi = null;
    }

    attributeChangedCallback(): void {
        this.refreshSlotVisibility();
    }

    /**
     * Read the currently-attached browser `EmceptionAPI` (if any).
     * Setting this to a non-null value stores it; if `autorun` is set
     * a compile+run cycle starts immediately. Setting to null detaches.
     *
     * Subscribes to every event in `EVENT_DOM_NAMES` and re-broadcasts
     * each as a bubbling + composed `CustomEvent` named
     * `emception-<name>` with the original payload as `detail`.
     */
    get api(): BrowserEmceptionAPI | null {
        return this.currentApi;
    }

    set api(next: BrowserEmceptionAPI | null) {
        if (this.currentApi === next) return;
        for (const unsub of this.apiUnsubs) {
            try { unsub(); } catch { /* subscriber may already be gone */ }
        }
        this.apiUnsubs = [];
        this.currentApi = next;
        if (!next) return;
        for (const [name, domName] of Object.entries(EVENT_DOM_NAMES)) {
            const unsub = next.on(name as keyof typeof EVENT_DOM_NAMES, (detail: unknown) => {
                this.dispatchEvent(
                    new CustomEvent(domName, { detail, bubbles: true, composed: true }),
                );
            });
            if (typeof unsub === 'function') this.apiUnsubs.push(unsub);
        }
        if (this.hasAttribute('autorun')) {
            void this.run();
        }
    }

    /**
     * Snapshot the current element attributes as a `ViewConfigInput`
     * (kebab→camel + flat→nested via `ATTRIBUTE_SCHEMA`). Returns the
     * input shape; callers can run it through `normalizeViewConfig` if
     * they need defaults applied.
     */
    readConfig(): ViewConfigInput {
        const attrs: Record<string, string> = {};
        for (const a of Array.from(this.attributes)) attrs[a.name] = a.value;
        return parseAttributesToInput(attrs);
    }

    /**
     * Trigger a compile + run cycle using the attached API.
     * Reads `source` and `preset` from element attributes; reads initial
     * stdin from a `<textarea slot="stdin">` child if present.
     * Dispatches `emception-exit` when the run finishes.
     */
    async run(): Promise<void> {
        const api = this.currentApi;
        if (!api) return;

        const source = this.getAttribute('source') ?? '';
        const presetAttr = this.getAttribute('preset') ?? 'cpp';
        const toolchain = presetAttr as ToolchainPreset;
        const stdinEl = this.querySelector<HTMLTextAreaElement>(':scope > [slot="stdin"]');
        const stdin = stdinEl?.value ?? stdinEl?.textContent ?? '';

        if (this.outputEl) this.outputEl.textContent = '';

        const result = await compileAndRun(api, {
            toolchain,
            source,
            stdin: stdin || undefined,
            onStdout: (t) => {
                if (this.outputEl) this.outputEl.append(t);
            },
            onStderr: (t) => {
                if (this.outputEl) this.outputEl.append(t);
            },
        });

        this.dispatchEvent(
            new CustomEvent('emception-exit', {
                detail: { exitCode: result.exitCode, finalPhase: result.finalPhase },
                bubbles: true,
                composed: true,
            }),
        );

        if (result.exitCode === 0) {
            this.dispatchEvent(
                new CustomEvent('emception-ready', {
                    detail: {},
                    bubbles: true,
                    composed: true,
                }),
            );
        }
    }

    private refreshSlotVisibility(): void {
        if (!this.canvasSlotEl || !this.stdinSlotEl) return;
        const cfg = this.readConfig();
        const hasCanvasSlotted = !!this.querySelector(':scope > [slot="canvas"]');
        this.canvasSlotEl.hidden = !(cfg.canvas || hasCanvasSlotted);
        const hasStdinSlotted = !!this.querySelector(':scope > [slot="stdin"]');
        this.stdinSlotEl.hidden = !hasStdinSlotted;
    }
}

/**
 * Register the element with `window.customElements`. Idempotent — safe
 * to call multiple times. Returns the constructor that ended up
 * registered (which may be a previously-registered one).
 */
export function registerEmceptionRun(tag: string = ELEMENT_NAME): CustomElementConstructor {
    if (typeof customElements === 'undefined') {
        throw new Error('@emception/webcomponent: customElements is not available in this environment.');
    }
    const existing = customElements.get(tag);
    if (existing) return existing;
    customElements.define(tag, EmceptionRunElement);
    return EmceptionRunElement;
}

// Auto-register on first import in browser-like environments.
if (typeof customElements !== 'undefined' && !customElements.get(ELEMENT_NAME)) {
    customElements.define(ELEMENT_NAME, EmceptionRunElement);
}
