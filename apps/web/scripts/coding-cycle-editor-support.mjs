// @ts-check
/**
 * Compare the complete visible program, preserving token and string contents.
 * @param {readonly string[]} lines Visible lines of the selected editor.
 * @param {string} source Complete public fixture source.
 * @returns {boolean} Whether every expected line matches.
 */
export function renderedEditorSourceMatches(lines, source) {
  if (!Array.isArray(lines) || lines.some((line) => typeof line !== "string") || typeof source !== "string") {
    throw new TypeError("Rendered editor lines and expected source must be strings.");
  }
  /** @param {readonly string[]} values */
  const normalize = (values) => {
    // Monaco renders ordinary spaces as NBSP and can auto-indent inserted lines.
    // Preserve interior whitespace: changing a string literal must still fail.
    const normalized = values.map((line) => line.replace(/\u00a0/g, " ").trim());
    while (normalized[0] === "") normalized.shift();
    while (normalized.at(-1) === "") normalized.pop();
    return normalized.join("\n");
  };
  const expected = normalize(source.replace(/\r\n/g, "\n").split("\n"));
  return expected.length > 0 && normalize(lines) === expected;
}

/**
 * Observe the selected editor after real keyboard input, without private globals.
 * This fixed Code-cycle fixture fits in the visible editor; missing/virtualized
 * lines fail rather than falling back to a marker elsewhere on the page.
 * @param {import('playwright').Page} page Browser page used only for bounded polling.
 * @param {import('playwright').Locator} editor The active Monaco editor locator.
 * @param {string} source The complete public fixture program that was typed.
 * @returns {Promise<void>} Resolves only when every expected line is rendered.
 */
export async function assertRenderedEditorSource(page, editor, source) {
  await editor.locator(".view-lines").waitFor({ state: "visible", timeout: 5_000 });
  for (let attempt = 0; attempt < 50; attempt++) {
    const lines = await editor.locator(".view-lines .view-line").allTextContents();
    if (renderedEditorSourceMatches(lines, source)) return;
    if (attempt < 49) await page.waitForTimeout(100);
  }
  throw new Error("Typed code does not match the complete rendered program in the active editor.");
}
