/**
 * End-to-end test: SDL3 compile path.
 *
 * Opens the browser IDE, which defaults to `/src/sdl-main.cpp` (the SDL3
 * bouncing-ball demo).  After the WASM toolchain boots the test clicks
 * "Compile & Run" and verifies:
 *
 *   1. The status transitions through "Compiling..." → "SDL3 done (…s)".
 *   2. The terminal shows the SDL3 detection/loading messages.
 *   3. The SDL Canvas tab becomes visible in the right dock group.
 *   4. The <canvas data-testid="sdl-canvas"> element is rendered and has a
 *      non-zero size, indicating SDL3/Emscripten injected its script.
 *
 * SDL3 compilation is significantly heavier than a plain hello-world because
 * it links against the precompiled ~10 MB libSDL3.a archive, so timeouts are
 * set generously (up to 10 minutes for the compile step).
 *
 * The `window.Module.canvas` injection strategy means we do NOT use an
 * emscripten HTML template — the compiled `main.js` (SINGLE_FILE=1, wasm
 * embedded as base64) is injected as a `<script>` tag whose canvas output
 * targets the IDE's own canvas element.
 *
 * NOTE: requires the CDN / sysroot to include libSDL3.a.  If not present
 * the emcc link step will fail, and the test will report a clear status
 * ("SDL3 compilation failed") rather than a timeout.
 */

import { expect, test, type Page } from '@playwright/test';

// ---------------------------------------------------------------------------
// Helpers  (mirrors compile.spec.ts conventions)
// ---------------------------------------------------------------------------

const terminal = (page: Page) => page.getByTestId('terminal');
const status = (page: Page) => page.getByTestId('status');
const compileBtn = (page: Page) => page.getByTestId('compile-button');
const sdlCanvas = (page: Page) => page.getByTestId('sdl-canvas');

interface CapturedLog {
    timestamp: number;
    type: string;
    text: string;
}

interface LogCapture {
    logs: CapturedLog[];
    /** Rejects with an Error if the page crashes (OOM / renderer kill). */
    crash: Promise<never>;
}

function captureEmceptionLogs(page: Page): LogCapture {
    const logs: CapturedLog[] = [];
    const t0 = Date.now();

    page.on('console', (msg) => {
        const text = msg.text();
        const type = msg.type();

        if (text.includes('[Emception:')) {
            const ts = `+${((Date.now() - t0) / 1000).toFixed(1)}s`;
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  ${ts} [${type}] ${text}`);
        }
        if ((type === 'error' || type === 'warning') && !text.includes('[Emception:')) {
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  [browser ${type}] ${text}`);
        }
    });

    page.on('pageerror', (err) => {
        logs.push({ timestamp: Date.now() - t0, type: 'pageerror', text: err.message });
        console.log(`  [browser pageerror] ${err.message}`);
    });

    const crash = new Promise<never>((_, reject) => {
        page.on('crash', () => {
            const err = new Error(
                'Page crashed (renderer killed — likely OOM). ' +
                'The WASM main loop probably allocated too much memory. ' +
                'Check WebAssembly.Memory limits in Ide.tsx.',
            );
            logs.push({ timestamp: Date.now() - t0, type: 'crash', text: err.message });
            console.log(`  [PAGE CRASHED] ${err.message}`);
            reject(err);
        });
    });

    return { logs, crash };
}

function dumpLogs(logs: CapturedLog[], label: string) {
    console.log(`\n===== ${label}: ${logs.length} log entries =====`);
    for (const log of logs) {
        const ts = `+${(log.timestamp / 1000).toFixed(1)}s`;
        const prefix = log.type === 'log' ? '' : ` [${log.type}]`;
        console.log(`  ${ts}${prefix} ${log.text}`);
    }
    console.log(`===== END ${label} =====\n`);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('SDL3 compile & canvas render', () => {
    /**
     * Core test: boot → SDL3 compile → canvas visible.
     *
     * The IDE defaults to `/src/sdl-main.cpp` as the active editor tab
     * (see `activeTabId` initial state in Ide.tsx), so no need to change
     * the editor content.  We do clear localStorage to ensure the default
     * state regardless of any previous session.
     */
    test('SDL3 demo compiles and renders to canvas', async ({ page }) => {
        const { logs, crash } = captureEmceptionLogs(page);

        // Navigate and immediately clear any persisted IDE state so we always
        // start with the SDL3 demo as the active file.
        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await page.evaluate(() => localStorage.clear());
        await page.reload({ waitUntil: 'domcontentloaded' });

        // ── Boot ──────────────────────────────────────────────────────────────
        console.log('Waiting for toolchain boot (Ready status)…');
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();
        console.log('Boot complete.');

        // Verify the active editor contains SDL3 code (the default demo).
        // Monaco loads its AMD modules asynchronously — wait until window.monaco
        // is populated (set in Ide.tsx's handleEditorDidMount callback) before
        // querying the editor models.
        await page.waitForFunction(
            () => !!(window as any).monaco?.editor?.getModels?.()?.length,
            { timeout: 30_000 },
        );
        const editorContent: string = await page.evaluate(() => {
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const w = window as any;
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            const model = w.monaco?.editor?.getModels?.()?.find((m: any) => m.uri?.path?.includes('sdl-main'));
            return (model?.getValue?.() as string) ?? '';
        });
        expect(editorContent).toContain('#include <SDL3/SDL.h>');
        console.log('SDL3 demo code confirmed in editor.');

        // ── Compile ───────────────────────────────────────────────────────────
        console.log('Clicking Compile & Run…');
        await compileBtn(page).click();

        await expect(status(page)).toHaveText('Compiling...', { timeout: 10_000 });
        console.log('Compilation started. Waiting for SDL3 link + load (up to 10 min)…');

        // SDL3 link takes much longer than plain emcc due to libSDL3.a size.
        // Allow up to 10 minutes for the full compile → inject cycle.
        // Race against page crash — if the renderer is killed (OOM) the crash
        // promise rejects immediately with a clear diagnostic message.
        await Promise.race([
            expect(status(page)).not.toHaveText('Compiling...', { timeout: 10 * 60_000 }),
            crash,
        ]);

        const finalStatus = await Promise.race([status(page).textContent(), crash]);
        console.log(`Final status: "${finalStatus}"`);

        // If SDL3 link failed, dump logs and produce a clear failure message.
        if (finalStatus && /SDL3 compilation failed/.test(finalStatus)) {
            dumpLogs(logs, 'SDL3 COMPILE FAILED');
            expect(finalStatus, 'SDL3 compilation failed — check libSDL3.a is in the sysroot').not.toMatch(
                /SDL3 compilation failed/,
            );
        }

        if (finalStatus && !/SDL3 done/.test(finalStatus)) {
            dumpLogs(logs, 'UNEXPECTED STATUS');
        }

        expect(finalStatus).toMatch(/SDL3 done/);
        console.log('SDL3 compilation succeeded!');

        // ── Terminal messages ─────────────────────────────────────────────────
        await Promise.race([expect(terminal(page)).toContainText('SDL3 detected', { timeout: 5_000 }), crash]);
        await Promise.race([expect(terminal(page)).toContainText('SDL3 rendering in canvas tab', { timeout: 5_000 }), crash]);

        // ── Canvas visible ────────────────────────────────────────────────────
        await Promise.race([expect(sdlCanvas(page)).toBeVisible({ timeout: 10_000 }), crash]);

        const canvasSize = await Promise.race([sdlCanvas(page).boundingBox(), crash]);
        console.log(`Canvas bounding box: ${JSON.stringify(canvasSize)}`);
        expect(canvasSize).not.toBeNull();
        expect(canvasSize?.width).toBeGreaterThan(100);
        expect(canvasSize!.height).toBeGreaterThan(100);

        console.log('SDL canvas is visible and has non-zero dimensions!');

        // ── Stability: wait 8s for WASM main loop — crash happens here if OOM ──
        console.log('Waiting 8s to verify page stability while WASM main loop runs…');
        await Promise.race([
            new Promise<void>((resolve) => setTimeout(resolve, 8_000)),
            crash,
        ]);
        console.log('Page stable after 8s.');

        // ── Canvas WebGL check ────────────────────────────────────────────────
        await page.waitForTimeout(1000);
        const canvasState: { has2d: boolean; hasWebGL: boolean; hasWebGL2: boolean; width: number; height: number } =
            await Promise.race([
                page.evaluate(() => {
                    const canvas = document.getElementById('canvas') as HTMLCanvasElement | null;
                    if (!canvas) return { has2d: false, hasWebGL: false, hasWebGL2: false, width: 0, height: 0 };
                    return {
                        has2d: canvas.getContext('2d') !== null,
                        hasWebGL: canvas.getContext('webgl') !== null,
                        hasWebGL2: canvas.getContext('webgl2') !== null,
                        width: canvas.width,
                        height: canvas.height,
                    };
                }),
                crash,
            ]);
        console.log(`Canvas context state: ${JSON.stringify(canvasState)}`);
        expect(canvasState.width, 'SDL3 should have initialised a non-trivial canvas width').toBeGreaterThan(100);
        expect(canvasState.height, 'SDL3 should have initialised a non-trivial canvas height').toBeGreaterThan(100);
        expect(
            canvasState.has2d || canvasState.hasWebGL || canvasState.hasWebGL2,
            'SDL3 should have acquired a rendering context on #canvas',
        ).toBe(true);

        dumpLogs(logs, 'SDL3 PIPELINE');
    });

    /**
     * Regression test: verify that re-compiling the SDL3 demo (clicking
     * Compile & Run a second time) cleans up the old script and re-injects,
     * leaving the canvas still visible and functional.
     */
    test('SDL3 re-compile cleans up previous script and re-renders', async ({ page }) => {
        const { logs, crash } = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await page.evaluate(() => localStorage.clear());
        await page.reload({ waitUntil: 'domcontentloaded' });

        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();

        // ── First compile ─────────────────────────────────────────────────────
        console.log('First compile…');
        await compileBtn(page).click();
        await Promise.race([expect(status(page)).not.toHaveText('Compiling...', { timeout: 10 * 60_000 }), crash]);
        expect(await Promise.race([status(page).textContent(), crash])).toMatch(/SDL3 done/);
        await Promise.race([expect(sdlCanvas(page)).toBeVisible({ timeout: 10_000 }), crash]);

        // ── Second compile ────────────────────────────────────────────────────
        console.log('Second compile (re-compile test)…');
        await compileBtn(page).click();
        await expect(status(page)).toHaveText('Compiling...', { timeout: 10_000 });
        await Promise.race([expect(status(page)).not.toHaveText('Compiling...', { timeout: 10 * 60_000 }), crash]);
        expect(await Promise.race([status(page).textContent(), crash])).toMatch(/SDL3 done/);

        // Canvas should still be visible after re-compile
        await Promise.race([expect(sdlCanvas(page)).toBeVisible({ timeout: 10_000 }), crash]);

        // Stability: 8s after second compile
        console.log('Waiting 8s to verify no crash after re-compile…');
        await Promise.race([new Promise<void>((resolve) => setTimeout(resolve, 8_000)), crash]);
        console.log('Page stable after re-compile.');

        dumpLogs(logs, 'SDL3 RE-COMPILE');
    });
});
