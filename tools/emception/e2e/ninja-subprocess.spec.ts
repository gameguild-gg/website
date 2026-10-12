/**
 * Strict E2E tests for ninja and cmake subprocess dispatch.
 *
 * Unlike build-tools.spec.ts, these tests FAIL HARD on errors rather than
 * skipping — they exist to pinpoint exactly where the toolchain breaks.
 *
 * The tests use the public API exposed on window.__emception_api__
 * to get structured {exitCode, stdout, stderr} results, avoiding terminal
 * scraping ambiguity.
 */

import { expect, test, type Page } from '@playwright/test';
import type { BrowserEmceptionAPI } from '@gameguild/emception-browser';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const status = (page: Page) => page.getByTestId('status');

interface CapturedLog {
    timestamp: number;
    type: string;
    text: string;
}

function captureAllLogs(page: Page): CapturedLog[] {
    const logs: CapturedLog[] = [];
    const t0 = Date.now();

    page.on('console', (msg) => {
        logs.push({
            timestamp: Date.now() - t0,
            type: msg.type(),
            text: msg.text(),
        });
    });

    page.on('pageerror', (err) => {
        logs.push({
            timestamp: Date.now() - t0,
            type: 'pageerror',
            text: err.message,
        });
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

/**
 * Read the full xterm terminal buffer content.
 * Reads directly from xterm.js's buffer API (exposed as window.__xterm__)
 * rather than scraping DOM, which is unreliable with canvas rendering.
 */
async function getTerminalText(page: Page): Promise<string> {
    return page.evaluate(() => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const term = (window as any).__xterm__;
        if (!term?.buffer?.active) return '';
        const buf = term.buffer.active;
        const lines: string[] = [];
        for (let i = 0; i < buf.length; i++) {
            const line = buf.getLine(i);
            if (line) lines.push(line.translateToString(true));
        }
        return lines.join('\n');
    });
}

async function bootToolchain(page: Page) {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(status(page)).toHaveText('Ready', { timeout: 120_000 });
    // Wait for shell prompt to appear in xterm buffer
    await expect(async () => {
        const text = await getTerminalText(page);
        expect(text).toContain('Browser Toolchain Shell');
    }).toPass({ timeout: 15_000 });
}

/**
 * Call the public API's run() directly from the browser context.
 * Returns structured { exitCode, stdout, stderr }.
 */
async function runTool(
    page: Page,
    tool: string,
    argv: string[],
    options?: { cwd?: string; env?: Record<string, string> },
): Promise<{ exitCode: number; stdout: string; stderr: string }> {
    return page.evaluate(
        async ({ tool, argv, options }) => {
            const api = (window as Window & { __emception_api__?: BrowserEmceptionAPI }).__emception_api__;
            if (!api) throw new Error('__emception_api__ not exposed on window');

            const result = await api.run(tool, argv, {
                cwd: options?.cwd ?? '/home/user',
                env: options?.env,
            });
            return {
                exitCode: result.exitCode,
                stdout: result.stdout,
                stderr: result.stderr,
            };
        },
        { tool, argv, options },
    );
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

test.describe('Ninja subprocess dispatch (strict)', () => {
    test('ninja --version returns exit code 0 and prints a version string', async ({
        page,
    }) => {
        test.setTimeout(3 * 60 * 1000); // 3 min total
        const logs = captureAllLogs(page);

        await bootToolchain(page);

        console.log('Calling ninja --version via the public API...');
        const result = await runTool(page, 'ninja', ['ninja', '--version']);

        console.log('ninja --version result:', JSON.stringify(result, null, 2));
        dumpLogs(logs, 'NINJA --version');

        expect(result.exitCode, 'ninja --version should exit 0').toBe(0);
        expect(
            result.stdout,
            'ninja --version stdout should contain a version number',
        ).toMatch(/\d+\.\d+/);
    });

    test('ninja (no args, no build.ninja) exits with error about missing build file', async ({
        page,
    }) => {
        test.setTimeout(3 * 60 * 1000);
        const logs = captureAllLogs(page);

        await bootToolchain(page);

        console.log('Calling ninja (no args) via the public API...');
        const result = await runTool(page, 'ninja', ['ninja']);

        console.log('ninja (no args) result:', JSON.stringify(result, null, 2));
        dumpLogs(logs, 'NINJA no-args');

        // Ninja without build.ninja should exit non-zero and mention the file
        expect(result.exitCode, 'ninja should exit non-zero without build.ninja').not.toBe(0);
        const combined = (result.stdout + result.stderr).toLowerCase();
        expect(
            combined.includes('build.ninja') ||
            combined.includes('no such file') ||
            combined.includes('ninja:'),
            'ninja should complain about missing build.ninja',
        ).toBe(true);
    });
});

test.describe('CMake subprocess dispatch (strict)', () => {
    test('cmake --version returns exit code 0 and prints version', async ({
        page,
    }) => {
        test.setTimeout(3 * 60 * 1000);
        const logs = captureAllLogs(page);

        await bootToolchain(page);

        console.log('Calling cmake --version via the public API...');
        const result = await runTool(page, 'cmake', ['cmake', '--version']);

        console.log('cmake --version result:', JSON.stringify(result, null, 2));
        dumpLogs(logs, 'CMAKE --version');

        expect(result.exitCode, 'cmake --version should exit 0').toBe(0);
        expect(result.stdout.toLowerCase()).toContain('cmake');
        expect(result.stdout).toMatch(/\d+\.\d+/);
    });

    test('cmake -G Ninja configures a minimal project (subprocess dispatch)', async ({
        page,
    }) => {
        test.setTimeout(5 * 60 * 1000); // 5 min — cmake configure is slow
        const logs = captureAllLogs(page);

        await bootToolchain(page);

        // Create a minimal CMake project in VFS
        console.log('Writing minimal CMake project to VFS...');
        await page.evaluate(async () => {
            const api = (window as Window & { __emception_api__?: BrowserEmceptionAPI }).__emception_api__;
            if (!api) throw new Error('__emception_api__ not exposed');

            const enc = new TextEncoder();
            await api.workspace.writeFile(
                '/tmp/test-cmake/CMakeLists.txt',
                enc.encode(
                    [
                        'cmake_minimum_required(VERSION 3.10)',
                        'project(hello C)',
                        'add_executable(hello main.c)',
                    ].join('\n'),
                ),
            );
            await api.workspace.writeFile(
                '/tmp/test-cmake/main.c',
                enc.encode(
                    [
                        '#include <stdio.h>',
                        'int main() { printf("Hello!\\n"); return 0; }',
                    ].join('\n'),
                ),
            );
        });

        // cmake configure with Ninja generator — this triggers subprocess
        // dispatch: cmake spawns ninja --version internally.
        console.log('Running cmake -G Ninja -B build ...');
        const result = await runTool(
            page,
            'cmake',
            ['cmake', '-G', 'Ninja', '-B', '/tmp/test-cmake/build', '-S', '/tmp/test-cmake'],
            { cwd: '/tmp/test-cmake' },
        );

        console.log('cmake configure result:', JSON.stringify({
            exitCode: result.exitCode,
            stdout: result.stdout.slice(-1000),
            stderr: result.stderr.slice(-1000),
        }, null, 2));
        dumpLogs(logs, 'CMAKE CONFIGURE');

        expect(result.exitCode, 'cmake configure should exit 0').toBe(0);
        const combined = (result.stdout + result.stderr).toLowerCase();
        expect(
            combined.includes('configuring done') ||
            combined.includes('build files have been written') ||
            combined.includes('generating done'),
            'cmake should report successful configuration',
        ).toBe(true);
    });
});
