import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const compilerRequire = createRequire(process.env.GAMEGUILD_EDITOR_TEST_WEB_PACKAGE ??
  new URL("../../../apps/web/package.json", import.meta.url));
const ts = compilerRequire("typescript");
const source = await readFile(path.join(root,
  "apps/web/src/components/block-content-editor/extras/source-code/xml/xml-syntax-highlighter.tsx"), "utf8");
const syntax = ts.createSourceFile("xml-syntax-highlighter.tsx", source,
  ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
const providers = [];
function findProvider(node) {
  if (ts.isCallExpression(node) &&
      node.expression.getText(syntax) === "monaco.languages.setMonarchTokensProvider" &&
      node.arguments[0]?.getText(syntax) === '"xml"') providers.push(node.arguments[1]);
  ts.forEachChild(node, findProvider);
}
findProvider(syntax);
assert.equal(providers.length, 1);
assert.ok(ts.isObjectLiteralExpression(providers[0]));
// Admit only the actual literal token definition; no component, hook or editor runs.
const literalKinds = new Set([ts.SyntaxKind.ObjectLiteralExpression,
  ts.SyntaxKind.ArrayLiteralExpression, ts.SyntaxKind.PropertyAssignment,
  ts.SyntaxKind.Identifier, ts.SyntaxKind.StringLiteral, ts.SyntaxKind.RegularExpressionLiteral]);
function admitLiteral(node) {
  assert.ok(literalKinds.has(node.kind), "Unexpected executable token initializer");
  if (ts.isIdentifier(node)) assert.ok(ts.isPropertyAssignment(node.parent) && node.parent.name === node);
  ts.forEachChild(node, admitLiteral);
}
admitLiteral(providers[0]);
const tokens = vm.runInNewContext("(" + providers[0].getText(syntax) + ")", {}, { timeout: 1000 });
const openingRules = tokens.tokenizer.root.filter(([, action]) =>
  action?.token === "tag" && action?.bracket === "@open");
assert.equal(openingRules.length, 1);
const [opening, action] = openingRules[0];
// Monarch Rule.setRegex anchors each rule at the current tokenizer position.
const rule = new RegExp("^(?:" + opening.source + ")", opening.flags);
function captures(input) {
  const match = rule.exec(input);
  return match ? Array.from(match, (value) => value ?? null) : null;
}

test("preserves token state and opening-tag action", () => {
  assert.deepEqual(Object.fromEntries(Object.entries(tokens.tokenizer).map(([name, rules]) => [name, rules.length])),
    { root: 13, comment: 3, cdata: 3, tagContent: 8 });
  assert.deepEqual(JSON.parse(JSON.stringify(action)), { token: "tag", bracket: "@open", next: "@tagContent" });
  assert.equal(opening.flags, "");
});
const valid = [
  ["<root>", "root", null], ["<A12>", "A12", null],
  ["<ns:root>", "ns:root", null], ["<a.b:c-d>", "a.b:c-d", null],
  ["<root >", "root", " "], ["<root   >", "root", "   "],
  ["<root\t>", "root", "\t"], ["<root\r\n>", "root", "\r\n"],
  ['<root x="1">', "root", ' x="1"'], ["<root x='1' y='2'>", "root", " x='1' y='2'"],
  ["<root x=1>", "root", " x=1"], ["<root />", "root", " /"],
  ["<root\t\t x='1' />", "root", "\t\t x='1' /"],
];
for (const [input, name, attributes] of valid) {
  test("preserves opening-tag match and captures: " + JSON.stringify(input), () =>
    assert.deepEqual(captures(input + "trailing"), [input, name, attributes]));
}
for (const whitespace of ["\v", "\f", "\u00a0", "\u1680", "\u2003", "\u2028", "\u2029", "\ufeff"]) {
  test("preserves whitespace class: " + whitespace.codePointAt(0), () => {
    const input = "<root" + whitespace.repeat(3) + "x='1'>";
    assert.deepEqual(captures(input), [input, "root", whitespace.repeat(3) + "x='1'"]);
  });
}
for (const input of ["", "root", "<", "</root>", '<?xml version="1.0"?>',
  "<!DOCTYPE root>", "<!--", "<![CDATA[", "<1root>", "<root attr='x'", "prefix<root>"]) {
  test("leaves other root tokens and incomplete tags unmatched: " + JSON.stringify(input), () =>
    assert.equal(captures(input), null));
}
function boundedMatch(input) {
  // The VM deadline bounds the adversarial control even against the old pattern.
  return vm.runInNewContext("rule.test(input)", { rule, input }, { timeout: 1000 });
}
test("rejects a long incomplete whitespace suffix within the bounded VM", () => {
  assert.equal(boundedMatch("<root" + " ".repeat(200_000)), false);
});
test("rejects a long incomplete attribute suffix within the bounded VM", () => {
  assert.equal(boundedMatch("<root" + " ".repeat(200_000) + "x='1'"), false);
});
test("keeps long complete tags valid", () => {
  const whitespace = " ".repeat(200_000);
  const input = "<root" + whitespace + "x='1'>";
  assert.equal(boundedMatch(input), true);
  assert.deepEqual(captures(input), [input, "root", whitespace + "x='1'"]);
});
