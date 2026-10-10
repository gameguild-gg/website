import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const compilerRequire = createRequire(process.env.GAMEGUILD_EDITOR_TEST_WEB_PACKAGE ??
  new URL("../../../apps/web/package.json", import.meta.url));
const ts = compilerRequire("typescript");
const files = ["index.ts", "types/index.ts", "lib/index.ts", "lib/utils.ts",
  "lib/web3-context.ts", "lib/web3-reducer.ts", "hooks/use-web3.ts",
  "components/web3-provider.tsx"];
const syntax = new Map();
for (const file of files) {
  const source = await readFile(path.join(root, "packages/features/web3/src", file), "utf8");
  const parsed = ts.createSourceFile(file, source, ts.ScriptTarget.Latest, true,
    file.endsWith(".tsx") ? ts.ScriptKind.TSX : ts.ScriptKind.TS);
  syntax.set(file, parsed);
  test(`scanner can parse Web3 source: ${file}`, () => {
    assert.deepEqual(parsed.parseDiagnostics.map(d => ({ code: d.code,
      message: ts.flattenDiagnosticMessageText(d.messageText, " ") })), []);
  });
}

// Source inspection only: never import React, ethers, hooks or the provider.
const provider = syntax.get("components/web3-provider.tsx");
const calls = [];
function visit(node) {
  if (ts.isCallExpression(node) && node.expression.getText(provider) === "useReducer") calls.push(node);
  ts.forEachChild(node, visit);
}
visit(provider);

test("initializes the existing reducer with its exported default state", () => {
  assert.equal(calls.length, 1);
  assert.deepEqual(calls[0].arguments.map(arg => arg.getText(provider)),
    ["web3Reducer", "defaultWeb3State"]);
});

test("imports the initial state from the same existing reducer module", () => {
  const declaration = provider.statements.find(node => ts.isImportDeclaration(node) &&
    node.moduleSpecifier.text === "../lib/web3-reducer");
  assert.ok(declaration && ts.isNamedImports(declaration.importClause.namedBindings));
  assert.ok(declaration.importClause.namedBindings.elements.some(node => node.name.text === "defaultWeb3State"));
});

test("does not reference an undeclared initializer", () => {
  assert.ok(!provider.text.includes("createInitialWeb3State"));
});

test("preserves the provider export and child rendering boundary", () => {
  assert.match(provider.text, /export const Web3Provider\s*=/);
  assert.match(provider.text, /<Web3Context\.Provider value=\{value\}>[\s\S]*\{children\}[\s\S]*<\/Web3Context\.Provider>/);
});
