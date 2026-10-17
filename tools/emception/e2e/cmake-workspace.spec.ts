/**
 * End-to-end test: CMake workspace preset via the IDE UI.
 *
 * This test boots the Emception web IDE, switches to the "CMake Project"
 * workspace preset using the dropdown picker, clicks "Compile & Run",
 * and verifies the cmake configure → ninja build → wasi-run pipeline.
 */

import { expect, test } from './fixtures';
import type { Page } from '@playwright/test';

// ---------------------------------------------------------------------------
// Helpers (same pattern as compile.spec.ts / build-tools.spec.ts)
// ---------------------------------------------------------------------------

const terminal = (page: Page) => page.getByTestId('terminal');
const status = (page: Page) => page.getByTestId('status');
const compileBtn = (page: Page) => page.getByTestId('compile-button');
const workspacePicker = (page: Page) => page.getByTestId('workspace-picker');

interface CapturedLog {
    timestamp: number;
    type: string;
    text: string;
}

function captureEmceptionLogs(page: Page): CapturedLog[] {
    const logs: CapturedLog[] = [];
    const t0 = Date.now();

    page.on('console', (msg) => {
        const type = msg.type();
        const text = msg.text();

        if (text.includes('[Emception:')) {
            const ts = `+${((Date.now() - t0) / 1000).toFixed(1)}s`;
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  ${ts} [${type}] ${text}`);
        }

        if (type === 'error' || type === 'warning') {
            if (!text.includes('[Emception:')) {
                logs.push({ timestamp: Date.now() - t0, type, text });
                console.log(`  [browser ${type}] ${text}`);
            }
        }
    });

    page.on('pageerror', (err) => {
        logs.push({ timestamp: Date.now() - t0, type: 'pageerror', text: err.message });
        console.log(`  [browser pageerror] ${err.message}`);
    });

    return logs;
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

/** Read all text from the xterm terminal buffer via the accessibility tree. */
async function getTerminalText(page: Page): Promise<string> {
    return (await terminal(page).textContent()) ?? '';
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('CMake Workspace Preset', () => {
    test('switch to CMake preset and run cmake configure → ninja → run', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        // Navigate and wait for the page to load
        await page.goto('/', { waitUntil: 'domcontentloaded' });

        try {
            // Wait for the toolchain to boot
            console.log('Waiting for boot (Ready status)...');
            await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
            await expect(compileBtn(page)).toBeEnabled();

            // Verify the workspace picker is visible
            await expect(workspacePicker(page)).toBeVisible();

            // Switch to CMake Project preset
            console.log('Switching to CMake Project workspace...');
            await workspacePicker(page).selectOption('cmake');

            // Give the workspace a moment to switch (files reset, tabs change)
            await page.waitForTimeout(500);

            // Verify workspace switched — the status should still be Ready
            await expect(status(page)).toHaveText('Ready', { timeout: 5_000 });

            // Click Compile & Run (this triggers cmake configure → ninja → wasi-run)
            console.log('Clicking Compile & Run for CMake project...');
            await compileBtn(page).click();

            // Status should transition to something related to compilation
            // The cmake-build path shows "CMake configure..." first
            await expect(status(page)).not.toHaveText('Ready', { timeout: 10_000 });
            const phaseStatus = await status(page).textContent();
            console.log(`Status after click: "${phaseStatus}"`);

            // Wait for the entire cmake configure → ninja → run pipeline to finish
            // or fail. The status will eventually stop being a "building" status.
            console.log('Waiting for cmake pipeline to complete (up to 5 min)...');

            // Wait until status is no longer any of the building states
            await expect(status(page)).not.toHaveText('CMake configure...', { timeout: 300_000 });

            // At this point cmake either succeeded and moved to ninja or failed
            const statusAfterConfigure = await status(page).textContent();
            console.log(`Status after cmake configure: "${statusAfterConfigure}"`);

            // Read terminal for diagnostic output
            const terminalText = await getTerminalText(page);
            console.log('Terminal output (last 2000 chars):', terminalText.slice(-2000));

            // ── Verify no CMAKE_ROOT error ──────────────────────────
            const hasCmakeRootError = terminalText.includes('Could not find CMAKE_ROOT') ||
                terminalText.includes('not been installed correctly');

            if (hasCmakeRootError) {
                dumpLogs(logs, 'CMAKE_ROOT ERROR');
            }
            expect(hasCmakeRootError, 'CMake should find CMAKE_ROOT with bundled Modules/Templates').toBe(false);

            // ── If cmake configure failed for another reason ────────────
            if (statusAfterConfigure === 'CMake configure failed') {
                dumpLogs(logs, 'CMAKE CONFIGURE FAILED');
                expect(statusAfterConfigure, 'CMake configure failed unexpectedly').not.toBe('CMake configure failed');
            }

            // ── Wait for ninja to finish if configure succeeded ─────────
            if (statusAfterConfigure === 'Ninja build...') {
                console.log('CMake configure succeeded, waiting for Ninja build...');
                await expect(status(page)).not.toHaveText('Ninja build...', { timeout: 300_000 });
                const afterNinja = await status(page).textContent();
                console.log(`Status after Ninja: "${afterNinja}"`);

                if (afterNinja?.includes('Build failed')) {
                    dumpLogs(logs, 'NINJA BUILD FAILED');
                    expect(afterNinja, 'Ninja build failed').not.toMatch(/Build failed/);
                }
            }

            // ── Final state: should have run successfully ───────────────
            const finalStatus = await status(page).textContent();
            console.log(`Final status: "${finalStatus}"`);

            // Verify the program output appears in the terminal
            await expect(terminal(page)).toContainText('Hello from CMake + Ninja + Emscripten!', { timeout: 60_000 });

            console.log('CMake workspace test PASSED!');
        } finally {
            dumpLogs(logs, 'CMAKE WORKSPACE');
        }
    });

    test('cmake preset files are loaded correctly after workspace switch', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });

        try {
            console.log('Waiting for boot...');
            await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });

            // Switch to CMake preset
            await workspacePicker(page).selectOption('cmake');
            await page.waitForTimeout(500);

            // Verify that the editor shows CMake project files by checking
            // if the Monaco editor has content from the cmake preset
            const editorContent = await page.evaluate(() => {
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                const models = (window as any).monaco?.editor?.getModels?.() ?? [];
                return models.map((m: { uri: { path: string }; getValue: () => string }) => ({
                    path: m.uri.path,
                    content: m.getValue(),
                }));
            });

            console.log('Editor models after switch:', editorContent.map((m: { path: string }) => m.path));

            // Check that CMakeLists.txt content is present somewhere
            const hasCMakeLists = editorContent.some(
                (m: { path: string; content: string }) =>
                    m.content.includes('cmake_minimum_required') || m.path.includes('CMakeLists'),
            );
            expect(hasCMakeLists, 'CMakeLists.txt should be available in the editor').toBe(true);

            // Check that main.cpp with the cmake-specific content is present
            const hasMainCpp = editorContent.some(
                (m: { path: string; content: string }) =>
                    m.content.includes('Hello from CMake + Ninja + Emscripten!'),
            );
            expect(hasMainCpp, 'main.cpp should contain the CMake preset hello world').toBe(true);

            console.log('CMake preset file verification PASSED!');
        } finally {
            dumpLogs(logs, 'CMAKE PRESET FILES');
        }
    });
});
