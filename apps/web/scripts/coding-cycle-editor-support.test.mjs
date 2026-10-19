import assert from "node:assert/strict";
import { test } from "node:test";
import { assertRenderedEditorSource, renderedEditorSourceMatches } from "./coding-cycle-editor-support.mjs";

const source = '// fixture-marker\nint main() {\n    print("a b");\n    return 5;\n}\n';
const rendered = ["// fixture-marker", "int\u00a0main()\u00a0{", '        print("a\u00a0b");', "        return\u00a05;", "    }", ""];

test("the complete rendered program tolerates Monaco NBSP and auto-indentation", () => {
  assert.equal(renderedEditorSourceMatches(rendered, source), true);
});

test("a visible marker alone cannot replace the complete typed program", () => {
  assert.equal(renderedEditorSourceMatches(["// fixture-marker"], source), false);
});

test("stale starter content and an extra tail are rejected", () => {
  assert.equal(renderedEditorSourceMatches(["int main() { return 0; }"], source), false);
  assert.equal(renderedEditorSourceMatches([...rendered, "return 0;"], source), false);
});

test("interior whitespace in string literals and changed executable tokens remain significant", () => {
  assert.equal(renderedEditorSourceMatches(rendered.map((line) => line.replace("a\u00a0b", "ab")), source), false);
  assert.equal(renderedEditorSourceMatches(rendered.map((line) => line.replace("return\u00a05", "return\u00a00")), source), false);
});

test("missing interior lines fail and only surrounding blank lines are ignored", () => {
  assert.equal(renderedEditorSourceMatches(rendered.filter((line) => !line.includes("print")), source), false);
  assert.equal(renderedEditorSourceMatches(["", ...rendered, ""], source.replace(/\n/g, "\r\n")), true);
});

test("empty or non-string observations do not certify typed code", () => {
  assert.equal(renderedEditorSourceMatches([], ""), false);
  assert.throws(() => renderedEditorSourceMatches([undefined], source), TypeError);
});

function observedEditor(observations) {
  let reads = 0;
  const selectors = [];
  const visibility = [];
  return {
    editor: {
      locator(selector) {
        selectors.push(selector);
        if (selector === ".view-lines") return { async waitFor(options) { visibility.push(options); } };
        assert.equal(selector, ".view-lines .view-line");
        return { async allTextContents() { return observations[Math.min(reads++, observations.length - 1)]; } };
      },
    },
    selectors,
    visibility,
    get reads() { return reads; },
  };
}

test("observation polls only the selected editor and works without a window.monaco hook", async () => {
  const observed = observedEditor([[], ["// fixture-marker"], rendered]);
  const waits = [];
  // The fake controls observation timing only; the hosted full cycle exercises
  // actual keyboard input, Monaco rendering, execution and persisted submission.
  const page = { async waitForTimeout(delay) { waits.push(delay); } };
  await assertRenderedEditorSource(page, observed.editor, source);
  assert.equal(observed.reads, 3);
  assert.deepEqual(waits, [100, 100]);
  assert.deepEqual(observed.visibility, [{ state: "visible", timeout: 5_000 }]);
  assert.ok(observed.selectors.every((selector) => selector.startsWith(".view-lines")));
});

test("a permanently incomplete editor fails after the bounded observations", async () => {
  const observed = observedEditor([["// fixture-marker"]]);
  let waits = 0;
  await assert.rejects(
    assertRenderedEditorSource({ async waitForTimeout() { waits++; } }, observed.editor, source),
    /does not match the complete rendered program/,
  );
  assert.equal(observed.reads, 50);
  assert.equal(waits, 49);
});
