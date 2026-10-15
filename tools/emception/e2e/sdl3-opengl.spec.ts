/**
 * End-to-end test: SDL3 + OpenGL ES 3 (WebGL2) canvas path.
 *
 * Selects the "C++ SDL3 — OpenGL ES 3.0" preset, clicks "Compile & Run",
 * and verifies:
 *   1. Status transitions through "Compiling..." → "SDL3 done (…s)".
 *   2. Terminal shows "SDL3 detected" and "SDL3 rendering in canvas tab".
 *   3. The canvas element is visible with non-zero dimensions.
 *   4. The canvas has a WebGL2 context (OpenGL ES 3.0 maps to WebGL2).
 *
 * The preset uses canvasPreset:'sdl' so the SDL3 runtime and compile path
 * are identical to the SDL3 renderer demo — only the C++ source differs.
 * Allow generous timeouts (up to 15 minutes) for the wasm-ld link step.
 */

import { expect, test } from './fixtures';
import type { Page } from '@playwright/test';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const terminal = (page: Page) => page.getByTestId('terminal');
const status = (page: Page) => page.getByTestId('status');
const compileBtn = (page: Page) => page.getByTestId('compile-button');
const canvasEl = (page: Page) => page.getByTestId('sdl-canvas');
const workspacePicker = (page: Page) => page.getByTestId('workspace-picker');

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

type EmceptionWindow = Window & {
    __emception_filesRef__?: {
        current?: Record<string, { content?: string }>;
    };
};

function captureEmceptionLogs(page: Page): LogCapture {
    const logs: CapturedLog[] = [];
    const t0 = Date.now();

    page.on('console', (msg) => {
        const text = msg.text();
        const type = msg.type();
        if (text.includes('[Emception:')) {
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  +${((Date.now() - t0) / 1000).toFixed(1)}s [${type}] ${text}`);
        }
        if ((type === 'error' || type === 'warning') && !text.includes('[Emception:')) {
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  [browser ${type}] ${text}`);
        }
    });

    page.on('pageerror', (err) => {
        logs.push({ timestamp: Date.now() - t0, type: 'pageerror', text: err.message });
        console.log(`  [pageerror] ${err.message}`);
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

test.describe('SDL3 + OpenGL ES 3 canvas', () => {
    test('SDL3+OpenGL demo compiles and renders to canvas', async ({ page }) => {
        // wasm-ld linking against libSDL3.a + libimgui.a can take a while — allow 15 min.
        test.setTimeout(15 * 60_000);
        const { logs, crash } = captureEmceptionLogs(page);

        // Start fresh — no persisted workspace state.
        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await page.evaluate(() => localStorage.clear());
        await page.reload({ waitUntil: 'domcontentloaded' });

        // ── Boot ──────────────────────────────────────────────────────────────
        console.log('Waiting for toolchain boot (Ready status)…');
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();
        console.log('Boot complete.');

        // ── Select the SDL3+OpenGL preset ─────────────────────────────────────
        console.log('Selecting cpp-sdl3-opengl preset…');
        await workspacePicker(page).selectOption('cpp-sdl3-opengl');
        // Wait for the preset switch to settle (editor remounts).
        await page.waitForTimeout(1_000);

        // Verify the OpenGL demo source is in the workspace files.
        await page.waitForFunction(() => {
            const w = window as EmceptionWindow;
            const filesRef = w.__emception_filesRef__;
            return !!filesRef?.current?.['/home/user/cpp-sdl3-opengl/sdl-opengl.cpp']?.content;
        }, { timeout: 30_000 });
        const editorContent: string = await page.evaluate(() => {
            const w = window as EmceptionWindow;
            return (w.__emception_filesRef__?.current?.['/home/user/cpp-sdl3-opengl/sdl-opengl.cpp']?.content as string) ?? '';
        });
        expect(editorContent).toContain('#include <GLES3/gl3.h>');
        expect(editorContent).toContain('SDL_GL_CreateContext');
        console.log('SDL3+OpenGL demo code confirmed in workspace files.');

        // ── Compile ───────────────────────────────────────────────────────────
        console.log('Clicking Compile & Run…');
        await compileBtn(page).click();

        await expect(status(page)).toHaveText('Compiling...', { timeout: 10_000 });
        console.log('Compilation started. Waiting for wasm-ld link + load (up to 10 min)…');

        // Race compilation against page crash.
        await Promise.race([
            expect(status(page)).not.toHaveText('Compiling...', { timeout: 10 * 60_000 }),
            crash,
        ]);

        const finalStatus = await Promise.race([status(page).textContent(), crash]);
        console.log(`Final status: "${finalStatus}"`);

        if (finalStatus && /compilation failed/.test(finalStatus)) {
            dumpLogs(logs, 'SDL3+OpenGL COMPILE FAILED');
            expect(
                finalStatus,
                'SDL3+OpenGL compilation failed — check GLES3/gl3.h and libimgui.a are in the sysroot',
            ).not.toMatch(/compilation failed/);
        }

        if (finalStatus && !/SDL3 done/.test(finalStatus)) {
            dumpLogs(logs, 'UNEXPECTED STATUS');
        }

        expect(finalStatus).toMatch(/SDL3 done/);
        console.log('SDL3+OpenGL compilation succeeded!');

        // ── Terminal messages ─────────────────────────────────────────────────
        await Promise.race([expect(terminal(page)).toContainText('SDL3 detected', { timeout: 5_000 }), crash]);
        await Promise.race([
            expect(terminal(page)).toContainText('SDL3 rendering in canvas tab', { timeout: 5_000 }),
            crash,
        ]);

        // ── No GL context crash ───────────────────────────────────────────────
        // The "entry invocation error" log appears when SDL_AppInit throws (e.g.
        // GLctx undefined because SDL_GL_CreateContext was never called).
        // The terminal must NOT contain this error for the OpenGL pipeline to be
        // considered working.
        await expect(terminal(page)).not.toContainText('entry invocation error', { timeout: 2_000 })
            .catch(() => {
                dumpLogs(logs, 'SDL3+OpenGL ENTRY INVOCATION ERROR');
                throw new Error('SDL3 entry invocation error detected — GLctx was undefined. SDL_GL_CreateContext must be called in SDL_AppInit.');
            });

        // ── Canvas visible ────────────────────────────────────────────────────
        await Promise.race([expect(canvasEl(page)).toBeVisible({ timeout: 15_000 }), crash]);

        const canvasSize = await Promise.race([canvasEl(page).boundingBox(), crash]);
        console.log(`Canvas bounding box: ${JSON.stringify(canvasSize)}`);
        expect(canvasSize).not.toBeNull();
        expect(canvasSize!.width).toBeGreaterThan(100);
        expect(canvasSize!.height).toBeGreaterThan(100);

        console.log('SDL3+OpenGL canvas is visible and has non-zero dimensions!');

        // ── Stability: wait 8s for WASM main loop ─────────────────────────────
        console.log('Waiting 8s to verify page stability while WASM main loop runs…');
        await Promise.race([
            new Promise<void>((resolve) => setTimeout(resolve, 8_000)),
            crash,
        ]);
        console.log('Page stable after 8s.');

        // ── WebGL2 context (OpenGL ES 3.0 = WebGL2 in the browser) ────────────
        const hasWebGL2 = await Promise.race([
            page.evaluate(() => {
                const c = document.querySelector<HTMLCanvasElement>('[data-testid="sdl-canvas"]');
                if (!c) return false;
                return !!(c.getContext('webgl2') ?? c.getContext('experimental-webgl2'));
            }),
            crash,
        ]);
        expect(hasWebGL2, 'SDL3+OpenGL ES 3 demo should acquire a WebGL2 context').toBe(true);
        console.log('WebGL2 context confirmed — OpenGL ES 3.0 pipeline is active.');

        dumpLogs(logs, 'SDL3+OpenGL PASS');
    });
});
