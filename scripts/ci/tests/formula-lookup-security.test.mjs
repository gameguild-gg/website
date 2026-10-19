import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import test from "node:test";
import { fileURLToPath, pathToFileURL } from "node:url";
import vm from "node:vm";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const webPackage = process.env.GAMEGUILD_EDITOR_TEST_WEB_PACKAGE ??
  fileURLToPath(new URL("../../../apps/web/package.json", import.meta.url));
const compilerRequire = createRequire(webPackage);
const ts = compilerRequire("typescript");
const mathliveRoot = path.join(path.dirname(webPackage), "node_modules/mathlive");
const mathliveManifest = JSON.parse(await readFile(path.join(mathliveRoot, "package.json"), "utf8"));
const mathlive = await import(pathToFileURL(path.join(mathliveRoot, mathliveManifest.exports["./ssr"].import)).href);
const source = await readFile(path.join(root,
  "packages/features/quiz/src/formula/formula-expression.ts"), "utf8");
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  reportDiagnostics: true, fileName: "formula-expression.ts",
});
assert.equal(compiled.diagnostics.filter((d) => d.category === ts.DiagnosticCategory.Error).length, 0);
const module = { exports: {} };
vm.runInNewContext(compiled.outputText, {
  module, exports: module.exports,
  require(name) {
    if (name === "mathlive/ssr") return mathlive;
    throw new Error("Unadmitted formula dependency: " + name);
  },
}, { timeout: 1000 });
const { evaluateFormula, validateFormula } = module.exports;

const arithmetic = [
  ["2 + 3 * 4", 14], ["2^3^2", 512], ["(2 + 3) * 4", 20],
  ["-2 + 3", 1], [".5 + 1.25", 1.75], ["2. + .5", 2.5],
];
for (const [expression, expected] of arithmetic) {
  test("preserves arithmetic: " + expression, () => assert.equal(evaluateFormula(expression, {}), expected));
}
const functions = [
  ["sqrt(9)", 3], ["abs(0-3)", 3], ["sin(0)", 0], ["cos(0)", 1],
  ["tan(0)", 0], ["log(100)", 2], ["ln(1)", 0], ["exp(0)", 1],
  ["ceil(1.2)", 2], ["floor(1.9)", 1], ["round(1.5)", 2],
  ["min(4,2,3)", 2], ["max(4,2,3)", 4], ["pow(2,3)", 8],
];
for (const [expression, expected] of functions) {
  test("preserves known function: " + expression, () => assert.equal(evaluateFormula(expression, {}), expected));
}
for (const name of ["pi", "PI", "e", "E"]) {
  test("preserves constant precedence: " + name, () =>
    assert.equal(evaluateFormula(name, { [name]: 99 }), name.toLowerCase() === "pi" ? Math.PI : Math.E));
}
test("preserves ordinary variables and nested functions", () => {
  assert.equal(evaluateFormula("sqrt(x) + max(2, y)", { x: 9, y: 5 }), 8);
  assert.equal(evaluateFormula("x + y", { x: -3, y: 0.5 }), -2.5);
  assert.equal(validateFormula("sqrt(x) + max(2, y)", ["x", "y"]), null);
});
test("preserves non-enumerable own numeric variables", () => {
  const variables = Object.defineProperty({}, "x", { value: 4 });
  assert.equal(evaluateFormula("x + 1", variables), 5);
});
test("preserves the actual SSR LaTeX converter", () => {
  assert.equal(evaluateFormula(String.raw`\frac{6}{2}`, {}), 3);
  assert.equal(evaluateFormula(String.raw`\sqrt{9}`, {}), 3);
});
const specialNames = ["constructor", "__proto__", "toString", "hasOwnProperty", "valueOf"];
for (const name of specialNames) {
  test("allows an explicitly declared numeric variable: " + name, () => {
    const variables = Object.fromEntries([[name, 7]]);
    assert.equal(evaluateFormula(name + " + 1", variables), 8);
    assert.equal(validateFormula(name + " + 1", [name]), null);
  });
  test("rejects an undeclared prototype name: " + name, () => {
    assert.throws(() => evaluateFormula(name, {}), /Unknown identifier/);
    assert.match(validateFormula(name, []), /Unknown identifier/);
  });
}
test("rejects inherited numeric variables", () => {
  const variables = Object.create({ inherited: 7 });
  assert.throws(() => evaluateFormula("inherited + 1", variables), /Unknown identifier/);
});
test("does not read an inherited getter", () => {
  let reads = 0;
  const variables = Object.create({ get inherited() { reads++; return 7; } });
  assert.throws(() => evaluateFormula("inherited", variables), /Unknown identifier/);
  assert.equal(reads, 0);
});
const invalidValues = [NaN, Infinity, -Infinity, "7", null, true, {}, []];
for (let i = 0; i < invalidValues.length; i++) {
  test("rejects a non-finite or non-number variable: " + i, () =>
    assert.throws(() => evaluateFormula("x", { x: invalidValues[i] }), /Invalid variable/));
}
test("does not invoke an object value's string conversion", () => {
  let calls = 0;
  const value = { toString() { calls++; return "7"; } };
  assert.throws(() => evaluateFormula("x", { x: value }), /Invalid variable/);
  assert.equal(calls, 0);
});
for (const [expression, message] of [["secret + 1", /Unknown identifier/],
  ["1 / 0", /Division by zero/], ["1..2", /Invalid number/], ["(1 + 2", /Mismatched parentheses/]]) {
  test("preserves invalid-expression rejection: " + expression, () =>
    assert.throws(() => evaluateFormula(expression, {}), message));
}
test("validation rejects empty and malformed formulas", () => {
  assert.equal(validateFormula(" ", []), "Formula cannot be empty");
  assert.notEqual(validateFormula("1..2 + x", ["x"]), null);
});
