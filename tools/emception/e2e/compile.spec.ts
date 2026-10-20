/**
 * End-to-end test: Compile & Run button → xterm output.
 *
 * This test boots the Emception web IDE in a real browser, waits for the
 * WASM toolchain to become ready, clicks "Compile & Run", and verifies
 * the expected output ("Hello from WebAssembly!") appears in the xterm
 * terminal.  It also demonstrates interactive terminal input.
 *
 * The WASM boot can take 30-60 s (first run), and emcc compilation is
 * typically 10-60 s on a fast machine, so timeouts are generous.
 *
 * All [Emception:*] browser console logs are captured and dumped after
 * each test for full pipeline visibility: boot → VFS → WASM loading →
 * process spawn → compilation → WASM generation → execution.
 *
 * NOTE: xterm.js 5.x uses a canvas renderer — the visible text is painted
 * on a <canvas>, not in DOM text nodes.  The only DOM-accessible text lives
 * in xterm's accessibility / live-region layer.  We use Playwright's
 * `toContainText()` which delegates to the accessibility tree, so it sees
 * the same text as the page snapshot.
 */

import { expect, test } from './fixtures';
import type { Page } from '@playwright/test';

interface MonacoTestModel {
    uri?: { path?: string };
    setValue(value: string): void;
}

type EmceptionWindow = Window & {
    __setFileContent?: (path: string, content: string) => void;
    __emception_filesRef__?: {
        current?: Record<string, { content?: string }>;
    };
    monaco?: {
        editor?: { getModels?: () => MonacoTestModel[] };
    };
};

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Locator for the terminal container. */
const terminal = (page: Page) => page.getByTestId('terminal');

/** Locator for the status badge. */
const status = (page: Page) => page.getByTestId('status');

/** Locator for the compile button. */
const compileBtn = (page: Page) => page.getByTestId('compile-button');

/**
 * Focus the interactive TTYBridge terminal.
 *
 * There is now a single xterm Terminal instance shared between Ide.tsx and
 * TTYBridge, so there is only one `textarea[aria-label="Terminal input"]`.
 */
async function focusShellTerminal(page: Page) {
    const textarea = page.locator('textarea[aria-label="Terminal input"]');
    await textarea.focus();
}

interface CapturedLog {
    timestamp: number;
    type: string;
    text: string;
}

/**
 * Set up browser console log capture for [Emception:*] messages.
 * Returns an array that will be populated with captured log entries.
 * ALL [Emception:*] logs are printed in real-time for debugging.
 */
function captureEmceptionLogs(page: Page): CapturedLog[] {
    const logs: CapturedLog[] = [];
    const t0 = Date.now();

    page.on('console', (msg) => {
        const text = msg.text();
        const type = msg.type();

        // Capture and print ALL [Emception:*] logs in real-time
        if (text.includes('[Emception:')) {
            const ts = `+${((Date.now() - t0) / 1000).toFixed(1)}s`;
            logs.push({ timestamp: Date.now() - t0, type, text });
            console.log(`  ${ts} [${type}] ${text}`);
        }

        // Also capture errors/warnings for debugging
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

/** Dump captured logs to stdout with relative timestamps. */
function dumpLogs(logs: CapturedLog[], label: string) {
    console.log(`\n===== ${label}: ${logs.length} log entries =====`);
    for (const log of logs) {
        const ts = `+${(log.timestamp / 1000).toFixed(1)}s`;
        const prefix = log.type === 'log' ? '' : ` [${log.type}]`;
        console.log(`  ${ts}${prefix} ${log.text}`);
    }
    console.log(`===== END ${label} =====\n`);
}

/** Check that a specific [Emception:*] milestone appears in the logs. */
function assertLogContains(logs: CapturedLog[], pattern: string, label: string) {
    const found = logs.some(l => l.text.includes(pattern));
    if (!found) {
        dumpLogs(logs, `MISSING MILESTONE: ${label}`);
    }
    expect(found, `Expected log milestone: "${label}" (pattern: "${pattern}")`).toBe(true);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Compile & Run', () => {
    // ------------------------------------------------------------------
    // 1. Default hello-world — full compile + run pipeline with log verification
    // ------------------------------------------------------------------
    test('default hello-world compiles and prints output', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        // Navigate and wait for the page to fully load.
        // Use 'domcontentloaded' rather than 'networkidle' because Next.js dev
        // mode keeps an HMR websocket open, so networkidle never fires.
        await page.goto('/', { waitUntil: 'domcontentloaded' });

        try {
            // Wait for the toolchain to boot
            console.log('Waiting for boot (Ready status)...');
            await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
            await expect(compileBtn(page)).toBeEnabled();

            // Verify boot milestones in logs
            assertLogContains(logs, 'BOOT START', 'Boot started');
            assertLogContains(logs, 'BOOT COMPLETE', 'Boot completed');

            // The MiniShell banner should already be in the terminal
            await expect(terminal(page)).toContainText('Browser Toolchain Shell', { timeout: 10_000 });

            // Switch to cpp-terminal workspace so the compile path uses WASI (not SDL3)
            await page.getByTestId('workspace-picker').selectOption('cpp-terminal');

            // Wait for workspace switch to render
            await page.waitForTimeout(1000);

            // Override file content directly in React state (bypasses Monaco timing issues).
            // The __setFileContent helper is exposed by Ide.tsx for e2e tests.
            const helloCode = [
                '#include <iostream>',
                'int main() {',
                '  std::cout << "Hello from WebAssembly!" << std::endl;',
                '  return 0;',
                '}',
            ].join('\n');
            await page.evaluate(({ path, code }) => {
                const setContent = (window as EmceptionWindow).__setFileContent;
                if (setContent) {
                    setContent(path, code);
                    return true;
                }
                return false;
            }, { path: '/home/user/cpp-terminal/main.cpp', code: helloCode });

            // Verify the file was actually updated in React state
            const fileContent = await page.evaluate((path) => {
                const filesRef = (window as EmceptionWindow).__emception_filesRef__;
                return filesRef?.current?.[path]?.content ?? null;
            }, '/home/user/cpp-terminal/main.cpp');
            console.log(`File content after __setFileContent (first 80 chars): "${fileContent?.substring(0, 80)}..."`);
            expect(fileContent, '__setFileContent should have updated the file').toContain('Hello from WebAssembly');

            // Wait for React state update to propagate
            await page.waitForTimeout(500);

            console.log('Boot complete. Clicking Compile & Run...');

            // Click "Compile & Run"
            await compileBtn(page).click();

            // Status should transition to Compiling
            await expect(status(page)).toHaveText('Compiling...', { timeout: 5_000 });
            console.log('Compilation started. Waiting for completion (up to 5 min)...');

            // Wait for status to change from "Compiling..." to anything else
            // (could be success, failure, or error)
            await expect(status(page)).not.toHaveText('Compiling...', { timeout: 300_000 });

            const finalStatus = await status(page).textContent();
            console.log(`Final status: "${finalStatus}"`);

            // If it's an error state, dump logs and fail with the error message
            if (finalStatus && /Error/.test(finalStatus)) {
                dumpLogs(logs, 'COMPILE ERROR');
                expect(finalStatus, 'Compilation ended with error status').not.toMatch(/Error/);
            }

            // Status may show "Compilation successful" or "Done (Xs)" if the
            // program already finished (no-stdin programs are very fast).
            if (finalStatus && !/Compilation successful|Done/.test(finalStatus)) {
                dumpLogs(logs, 'COMPILE NOT SUCCESSFUL');
            }

            // Verify it was successful — accept both "Compilation successful" and "Done (Xs)"
            expect(finalStatus).toMatch(/Compilation successful|Done/);

            console.log('Compilation successful! Checking for output...');

            // Verify the program output appears in the terminal
            await expect(terminal(page)).toContainText('Hello from WebAssembly!', { timeout: 60_000 });

            console.log('Output verified!');

            // Dump full log timeline
            dumpLogs(logs, 'HELLO-WORLD PIPELINE');

            // Verify key pipeline milestones from the logs
            // Boot
            assertLogContains(logs, 'Step 1/6 done: manifest loaded', 'Manifest loaded');
            assertLogContains(logs, 'Step 2/6 done: LazyFS ready', 'LazyFS initialized');
            assertLogContains(logs, 'Step 5/6 done: all components created', 'Components created');

            // Compile (direct clang + wasm-ld fast path)
            assertLogContains(logs, 'COMPILE & RUN START', 'IDE compile started');
            assertLogContains(logs, 'RUN: clang', 'clang tool run started');
            assertLogContains(logs, 'RUN COMPLETE: clang', 'clang completed');
            assertLogContains(logs, 'RUN: wasm-ld', 'wasm-ld tool run started');
            assertLogContains(logs, 'RUN COMPLETE: wasm-ld', 'wasm-ld completed');

            // Compilation output
            assertLogContains(logs, 'Compilation output: main.wasm=', 'Output files generated');

            // Execution (standalone WASI runtime)
            assertLogContains(logs, 'WASI RUN', 'WASI runtime started');
            assertLogContains(logs, 'WASI COMPLETE', 'WASI execution completed');

            assertLogContains(logs, 'COMPILE & RUN COMPLETE', 'Full pipeline completed');

        } finally {
            // Always dump logs, even on failure
            if (logs.length > 0) {
                dumpLogs(logs, 'FINAL LOG DUMP (test end)');
            }
        }
    });

    // ------------------------------------------------------------------
    // 1b. Compile timing — verify the direct clang+wasm-ld fast path
    //     completes within a reasonable time budget (< 15s compile).
    // ------------------------------------------------------------------
    test('cpp-terminal compiles within time budget (direct path)', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });

        try {
            await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
            await expect(compileBtn(page)).toBeEnabled();

            // Switch to cpp-terminal workspace (uses DEFAULT_CODE which prompts
            // "Enter your name:" and echoes "Hello, <name>! Welcome to WebAssembly!")
            await page.getByTestId('workspace-picker').selectOption('cpp-terminal');

            // Record compilation start time
            const compileStart = Date.now();

            await compileBtn(page).click();
            await expect(status(page)).toHaveText('Compiling...', { timeout: 5_000 });
            console.log('Compilation started. Timing...');

            // Wait for compilation to finish
            await expect(status(page)).not.toHaveText('Compiling...', { timeout: 300_000 });
            const compileEnd = Date.now();
            const compileDuration = (compileEnd - compileStart) / 1000;

            const finalStatus = await status(page).textContent();
            console.log(`Compilation status: "${finalStatus}" — took ${compileDuration.toFixed(1)}s`);
            expect(finalStatus).toMatch(/Compilation successful/);

            // Wait for "Enter your name:" in terminal (program is running, waiting for stdin)
            const term = terminal(page);
            await expect(term).toContainText('Enter your name:', { timeout: 60_000 });
            console.log('Program is running and waiting for stdin.');

            // Verify stdin still works without ASYNCIFY
            await focusShellTerminal(page);
            await page.keyboard.type('timing-test', { delay: 30 });
            await page.keyboard.press('Enter');
            await expect(term).toContainText('Hello, timing-test! Welcome to WebAssembly!', { timeout: 30_000 });

            const totalEnd = Date.now();
            const totalDuration = (totalEnd - compileStart) / 1000;
            console.log(`Total compile+run+stdin: ${totalDuration.toFixed(1)}s`);
            console.log(`Compile-only: ${compileDuration.toFixed(1)}s`);

            // Extract timing from logs
            const clangRun = logs.find(l => l.text.includes('RUN COMPLETE: clang'));
            const lldRun = logs.find(l => l.text.includes('RUN COMPLETE: wasm-ld'));
            if (clangRun) console.log(`  clang finished at: +${(clangRun.timestamp / 1000).toFixed(1)}s`);
            if (lldRun) console.log(`  wasm-ld finished at: +${(lldRun.timestamp / 1000).toFixed(1)}s`);

            // Verify NO emcc/Python overhead
            const emccLog = logs.find(l => l.text.includes('RUN: emcc'));
            expect(emccLog, 'Should NOT use emcc Python pipeline').toBeUndefined();
            const wasmOptLog = logs.find(l => l.text.includes('wasm-opt') && l.text.includes('asyncify'));
            expect(wasmOptLog, 'Should NOT run wasm-opt asyncify').toBeUndefined();

            // Timing assertion: direct path should compile fast.
            // (old emcc path was ~20s; direct path is ~5s locally)
            // CI runners are 2-3x slower — give 3x headroom there.
            const budget = process.env.CI ? 45 : 15;
            expect(compileDuration, `Compile took ${compileDuration.toFixed(1)}s — should be < ${budget}s`).toBeLessThan(budget);

            dumpLogs(logs, 'TIMING TEST');
        } finally {
            if (logs.length > 0) {
                dumpLogs(logs, 'TIMING TEST (final)');
            }
        }
    });

    // ------------------------------------------------------------------
    // 2. Terminal is interactive after boot
    // ------------------------------------------------------------------
    test('terminal is interactive after boot', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });

        // Verify the MiniShell banner is in the terminal log
        const term = terminal(page);
        await expect(term).toContainText('Browser Toolchain Shell', { timeout: 10_000 });
        await expect(term).toContainText('Type "help" for available commands', { timeout: 5_000 });

        // Focus the TTYBridge terminal (the second xterm textarea)
        await focusShellTerminal(page);

        // Type a command — MiniShell echoes each character and processes on Enter
        await page.keyboard.type('echo hello', { delay: 50 });
        await page.keyboard.press('Enter');

        // MiniShell's echo builtin writes "hello" via tty.writeLine
        await expect(term).toContainText('hello', { timeout: 15_000 });

        dumpLogs(logs, 'INTERACTIVE TERMINAL');
        assertLogContains(logs, 'BOOT COMPLETE', 'Boot completed');
    });

    // ------------------------------------------------------------------
    // 3. stdin — compile a program that reads from stdin, type input,
    //    verify the program echoes it back.
    // ------------------------------------------------------------------
    test('stdin works — program reads user input', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();

        // Switch to cpp-terminal workspace so the compile path uses WASI (not SDL3)
        await page.getByTestId('workspace-picker').selectOption('cpp-terminal');

        // Wait for workspace switch to render
        await page.waitForTimeout(1000);

        // Replace editor content with a C program that reads stdin
        const stdinProgram = [
            '#include <stdio.h>',
            'int main() {',
            '    char buf[64];',
            '    printf("PROMPT:\\n");',
            '    fflush(stdout);',
            '    if (fgets(buf, sizeof buf, stdin)) {',
            '        printf("GOT:%s\\n", buf);',
            '    } else {',
            '        printf("GOT:EOF\\n");',
            '    }',
            '    return 0;',
            '}',
        ].join('\n');

        // Set file content directly in React state (bypasses Monaco timing issues)
        await page.evaluate(({ path, code }) => {
            const setContent = (window as EmceptionWindow).__setFileContent;
            if (setContent) setContent(path, code);
        }, { path: '/home/user/cpp-terminal/main.cpp', code: stdinProgram });

        // Wait for React state update to propagate
        await page.waitForTimeout(500);

        // Click compile & run
        console.log('Compiling stdin test program...');
        await compileBtn(page).click();
        await expect(status(page)).toHaveText('Compiling...', { timeout: 5_000 });
        await expect(status(page)).not.toHaveText('Compiling...', { timeout: 300_000 });

        const finalStatus = await status(page).textContent();
        console.log(`Compilation status: "${finalStatus}"`);
        expect(finalStatus).toMatch(/Compilation successful/);

        // Wait for "PROMPT:" in the terminal (program is now waiting for stdin)
        const term = terminal(page);
        await expect(term).toContainText('PROMPT:', { timeout: 60_000 });
        console.log('Program printed PROMPT — now typing input...');

        // Focus the terminal and type input
        await focusShellTerminal(page);
        await page.keyboard.type('hello', { delay: 50 });
        await page.keyboard.press('Enter');

        // The program should echo back what we typed
        await expect(term).toContainText('GOT:hello', { timeout: 30_000 });
        console.log('stdin test passed — program received input!');

        // Verify fd_read was actually called (diagnostic log)
        assertLogContains(logs, 'fd_read called', 'fd_read was invoked');
        assertLogContains(logs, 'WASI COMPLETE', 'WASI execution completed');

        dumpLogs(logs, 'STDIN TEST');
    });

    // ------------------------------------------------------------------
    // 4. Python stdin — switch to Python workspace, run the default script
    //    that calls input() + print(), type a name, verify output appears.
    // ------------------------------------------------------------------
    test('python stdin works — input() and print() round-trip', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();

        // Switch to Python workspace
        await page.getByTestId('workspace-picker').selectOption('python');

        // Wait for workspace switch to complete
        await page.waitForFunction(() => {
            return document.querySelector('[data-testid="compile-button"]')?.textContent?.includes('Run');
        }, { timeout: 10_000 });

        // Wait a moment for Monaco to settle and set our custom script
        await page.waitForTimeout(500);
        const modelSet = await page.evaluate((code) => {
            const models = (window as EmceptionWindow).monaco?.editor?.getModels?.() ?? [];
            // Find the Python model (main.py)
            const pyModel = models.find((m) => m.uri?.path?.endsWith('.py')) ?? models[0];
            if (pyModel) {
                pyModel.setValue(code);
                return true;
            }
            return false;
        }, [
            'name = input("PROMPT:")',
            'print(f"GOT:{name}")',
        ].join('\n'));
        console.log(`Monaco model set: ${modelSet}`);

        // Click Run
        console.log('Running Python stdin test program...');
        await compileBtn(page).click();

        // Wait for "PROMPT:" in the terminal (Python is running, waiting for stdin)
        const term = terminal(page);
        await expect(term).toContainText('PROMPT:', { timeout: 120_000 });
        console.log('Python printed PROMPT — now typing input...');

        // Focus the terminal and type input
        await focusShellTerminal(page);
        await page.keyboard.type('alice', { delay: 50 });
        await page.keyboard.press('Enter');

        // The program should print back what we typed
        await expect(term).toContainText('GOT:alice', { timeout: 60_000 });
        console.log('Python stdin test passed — input()/print() round-trip works!');

        dumpLogs(logs, 'PYTHON STDIN TEST');
    });

    // ------------------------------------------------------------------
    // 5. stdin backspace — verify that backspace editing works correctly
    //    so the WASM program receives the edited text, not raw bytes.
    // ------------------------------------------------------------------
    test('stdin backspace editing works correctly', async ({ page }) => {
        const logs = captureEmceptionLogs(page);

        await page.goto('/', { waitUntil: 'domcontentloaded' });
        await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
        await expect(compileBtn(page)).toBeEnabled();

        // Switch to cpp-terminal workspace so the compile path uses WASI (not SDL3)
        await page.getByTestId('workspace-picker').selectOption('cpp-terminal');

        // Wait for workspace switch to render
        await page.waitForTimeout(1000);

        // C program that prints what it reads from stdin
        const backspaceProgram = [
            '#include <stdio.h>',
            'int main() {',
            '    char buf[64];',
            '    printf("PROMPT:\\n");',
            '    fflush(stdout);',
            '    if (fgets(buf, sizeof buf, stdin)) {',
            '        printf("GOT:%s", buf);',
            '    }',
            '    return 0;',
            '}',
        ].join('\n');

        // Set file content directly in React state (bypasses Monaco timing issues)
        await page.evaluate(({ path, code }) => {
            const setContent = (window as EmceptionWindow).__setFileContent;
            if (setContent) setContent(path, code);
        }, { path: '/home/user/cpp-terminal/main.cpp', code: backspaceProgram });

        // Wait for React state update to propagate
        await page.waitForTimeout(500);
        await page.waitForTimeout(500);

        // Compile & run
        await compileBtn(page).click();
        await expect(status(page)).toHaveText('Compiling...', { timeout: 5_000 });
        await expect(status(page)).not.toHaveText('Compiling...', { timeout: 300_000 });
        expect(await status(page).textContent()).toMatch(/Compilation successful/);

        // Wait for the program to prompt for input
        const term = terminal(page);
        await expect(term).toContainText('PROMPT:', { timeout: 60_000 });

        // Type "alu", backspace to delete 'u', then "ex" → should yield "alex"
        await focusShellTerminal(page);
        await page.keyboard.type('alu', { delay: 50 });
        await page.keyboard.press('Backspace');
        await page.keyboard.type('ex', { delay: 50 });
        await page.keyboard.press('Enter');

        // The program should print the edited text, not the raw keystrokes
        await expect(term).toContainText('GOT:alex', { timeout: 30_000 });

        dumpLogs(logs, 'BACKSPACE TEST');
    });
});
