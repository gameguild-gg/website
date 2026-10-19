/**
 * End-to-end test: Editor input responsiveness after **successful** SDL3
 * compilation + canvas render.
 *
 * This is the real regression test for the keyboard-capture bug: after SDL3
 * renders to a canvas the global SDL keyboard event handler can steal focus
 * from the Monaco editor.  The test must:
 *
 *   1. Boot IDE, wait for "Ready"
 *   2. Edit sdl-main.cpp (add a comment), verify it
 *   3. Click Play → wait for SDL3 compilation to **succeed** ("SDL3 done")
 *   4. Verify the SDL canvas is visible and rendered
 *   5. Switch back to the editor tab, focus it
 *   6. Type new text via real keyboard (the actual responsiveness check)
 *   7. Verify the typed text appears in the editor
 *
 * SDL3 compilation is heavy (~minutes).  The test uses the global Playwright
 * config timeout (5 min) rather than an artificial 90s cap.
 */

import { expect, test } from './fixtures';
import type { Page } from '@playwright/test';

const status = (page: Page) => page.getByTestId('status');
const compileBtn = (page: Page) => page.getByTestId('compile-button');
const sdlCanvas = (page: Page) => page.getByTestId('sdl-canvas');

function captureEmceptionLogs(page: Page): void {
    page.on('console', (msg) => {
        const text = msg.text();
        if (text.includes('[Emception:') || msg.type() === 'error') {
            console.log(`  [${msg.type()}] ${text}`);
        }
    });
}

/** Get Monaco editor content via its JS API (more reliable than DOM scraping). */
async function getMonacoValue(page: Page): Promise<string> {
    return await page.evaluate(() => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const models = (window as any).monaco?.editor?.getModels?.();
        if (models && models.length > 0) return models[0].getValue() as string;
        return '';
    });
}

/** Append text to the end of the Monaco editor via its JS API. */
async function appendToMonaco(page: Page, text: string): Promise<void> {
    await page.evaluate((t) => {
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const models = (window as any).monaco?.editor?.getModels?.();
        if (models && models.length > 0) {
            const model = models[0];
            const fullRange = model.getFullModelRange();
            const lastLine = fullRange.endLineNumber;
            const lastCol = model.getLineMaxColumn(lastLine);
            model.pushEditOperations(
                [],
                [{ range: { startLineNumber: lastLine, startColumn: lastCol, endLineNumber: lastLine, endColumn: lastCol }, text: t }],
                () => null,
            );
        }
    }, text);
}

test('editor accepts input after SDL3 compilation', async ({ page }) => {
    console.log('\n=== Editor Input After SDL Compilation ===\n');
    captureEmceptionLogs(page);

    // 1. Boot IDE
    console.log('1. Booting IDE...');
    await page.goto('/', { waitUntil: 'domcontentloaded', timeout: 30_000 });
    await expect(status(page)).toContainText('Ready', { timeout: 120_000 });
    console.log('   ✓ Ready\n');

    // 2. Edit sdl-main.cpp — append a comment via Monaco API
    console.log('2. Editing sdl-main.cpp...');
    const sdlTab = page.locator('div').filter({ hasText: /sdl-main\.cpp/ }).first();
    await expect(sdlTab).toBeVisible({ timeout: 5_000 });
    await sdlTab.click();

    // Wait for Monaco editor to initialize models (it loads async)
    await page.waitForFunction(
        () => !!(window as any).monaco?.editor?.getModels?.()?.length,
        { timeout: 30_000 },
    );
    await page.waitForTimeout(500);

    await appendToMonaco(page, '\n// BEFORE_COMPILE');
    await page.waitForTimeout(300);

    const beforeContent = await getMonacoValue(page);
    expect(beforeContent).toContain('BEFORE_COMPILE');
    console.log('   ✓ Comment added and visible\n');

    // 3. Click Play — SDL3 compilation (may take several minutes)
    console.log('3. Clicking Play...');
    await expect(compileBtn(page)).toBeVisible({ timeout: 5_000 });
    await compileBtn(page).click();
    await expect(status(page)).toContainText('Compiling', { timeout: 10_000 });
    console.log('   ✓ Compilation started\n');

    // 4. Wait for compilation to finish — MUST succeed
    console.log('4. Waiting for SDL3 compilation to succeed...');
    await expect(status(page)).not.toContainText('Compiling', { timeout: 4 * 60_000 });
    const finalStatus = await status(page).textContent();
    console.log(`   Status: "${finalStatus}"`);

    // Assert it succeeded, not just "finished"
    expect(finalStatus, 'SDL3 compilation must succeed (not fail with FROZEN_CACHE or other error)')
        .toMatch(/SDL3 done/);
    console.log('   ✓ SDL3 compilation succeeded!\n');

    // 5. Verify canvas is visible and rendered
    console.log('5. Verifying SDL canvas...');
    await expect(sdlCanvas(page)).toBeVisible({ timeout: 10_000 });
    const canvasBox = await sdlCanvas(page).boundingBox();
    expect(canvasBox).not.toBeNull();
    expect(canvasBox!.width).toBeGreaterThan(100);
    expect(canvasBox!.height).toBeGreaterThan(100);
    console.log(`   ✓ Canvas visible (${canvasBox!.width}×${canvasBox!.height})\n`);

    // 6. Switch back to editor and focus it
    console.log('6. Focusing editor...');
    await sdlTab.click();
    await page.waitForTimeout(300);

    const editor = page.locator('.monaco-editor').first();
    await editor.click();
    await page.waitForTimeout(500);
    console.log('   ✓ Editor focused\n');

    // 7. Type new text via real keyboard (the actual responsiveness test!)
    console.log('7. Typing in editor via keyboard (real user simulation)...');
    await page.keyboard.press('Control+End');
    await page.waitForTimeout(200);
    await page.keyboard.press('Enter');
    await page.keyboard.type('// AFTER_COMPILE', { delay: 50 });
    await page.waitForTimeout(500);
    console.log('   ✓ Text typed\n');

    // 8. Verify the new text appears (via Monaco API)
    console.log('8. Verifying text appears...');
    const afterContent = await getMonacoValue(page);
    const tail = afterContent.substring(Math.max(0, afterContent.length - 120));
    console.log('   Editor tail:', JSON.stringify(tail));
    expect(afterContent).toContain('AFTER_COMPILE');
    console.log('   ✓ Verified — editor is responsive after SDL3!\n');

    console.log('✅ PASS - Editor is responsive after successful SDL3 compilation!\n');
});
