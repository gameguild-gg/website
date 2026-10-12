import assert from "node:assert/strict";
import { existsSync, readFileSync, readdirSync, realpathSync } from "node:fs";
import { createRequire } from "node:module";
import { dirname, join, resolve } from "node:path";
import { test } from "node:test";
import { fileURLToPath } from "node:url";
import { runInNewContext } from "node:vm";

import { readPnpmSecurityConfig } from "../read-pnpm-config.mjs";

const repositoryRoot = resolve(
  dirname(fileURLToPath(import.meta.url)),
  "../../..",
);
const virtualStore = join(repositoryRoot, "node_modules/.pnpm");
const require = createRequire(import.meta.url);
const packageRoots = new Set();

// Check each consumer's actual package resolution, including the hoisted alias.
for (const entry of readdirSync(virtualStore, { withFileTypes: true })) {
  if (!entry.isDirectory() || entry.name.startsWith("braces@")) continue;
  const candidate = join(virtualStore, entry.name, "node_modules/braces");
  if (existsSync(candidate)) packageRoots.add(realpathSync(candidate));
}
for (const candidate of [
  join(virtualStore, "node_modules/braces"),
  join(repositoryRoot, "node_modules/braces"),
]) {
  if (existsSync(candidate)) packageRoots.add(realpathSync(candidate));
}
assert.ok(
  packageRoots.size > 0,
  "The installed dependency graph must contain braces",
);

const nestedPattern = (depth, left = "{", right = "}") =>
  left.repeat(depth) + "a" + right.repeat(depth);
const nestedAst = (depth) => {
  let ast = { type: "text", value: "a" };
  for (let index = 0; index < depth; index++)
    ast = { type: "brace", nodes: [ast] };
  return { type: "root", nodes: [ast] };
};

for (const packageRoot of packageRoots) {
  const braces = require(packageRoot);
  const label = packageRoot.replace(repositoryRoot, "");

  test(`${label}: parser rejects excessive brace and parenthesis nesting`, () => {
    for (const [left, right] of [
      ["{", "}"],
      ["(", ")"],
    ]) {
      assert.doesNotThrow(() => braces.parse(nestedPattern(100, left, right)));
      assert.throws(
        () => braces.parse(nestedPattern(101, left, right)),
        /exceeds max depth/,
      );
      assert.throws(
        () => braces.parse(nestedPattern(4000, left, right)),
        /exceeds max depth/,
      );
    }
  });

  for (const method of ["compile", "expand", "stringify"]) {
    test(`${label}: ${method} rejects caller-supplied deep ASTs`, () => {
      assert.doesNotThrow(() => braces[method](nestedAst(100)));
      assert.throws(() => braces[method](nestedAst(101)), /exceeds max depth/);
      assert.throws(() => braces[method](nestedAst(4000)), /exceeds max depth/);
    });
  }

  test(`${label}: public string operations reject nesting below the character limit`, () => {
    for (const method of ["compile", "expand"]) {
      assert.throws(
        () => braces[method](nestedPattern(4000)),
        /exceeds max depth/,
      );
    }
    assert.throws(() => braces(nestedPattern(4000)), /exceeds max depth/);
  });

  test(`${label}: stricter limits and the hard ceiling apply`, () => {
    for (const method of ["parse", "compile", "expand"]) {
      assert.doesNotThrow(() => braces[method]("{a}", { maxDepth: 1.5 }));
      assert.throws(
        () => braces[method]("{{a}}", { maxDepth: 1.5 }),
        /exceeds max depth/,
      );
      assert.throws(
        () => braces[method](nestedPattern(101), { maxDepth: 10000 }),
        /exceeds max depth/,
      );
    }
    for (const method of ["compile", "expand", "stringify"]) {
      assert.throws(
        () => braces[method](nestedAst(2), { maxDepth: 1.5 }),
        /exceeds max depth/,
      );
      assert.throws(
        () => braces[method](nestedAst(101), { maxDepth: 10000 }),
        /exceeds max depth/,
      );
    }
  });

  test(`${label}: ordinary glob patterns and invalid-brace escaping remain compatible`, () => {
    assert.deepEqual(braces.expand("apps/{api,web}/**"), [
      "apps/api/**",
      "apps/web/**",
    ]);
    assert.deepEqual(braces.expand("file{1..3}.js"), [
      "file1.js",
      "file2.js",
      "file3.js",
    ]);
    assert.deepEqual(braces.expand("foo/({a,b})"), ["foo/(a)", "foo/(b)"]);
    const expression = new RegExp(
      `^${braces.compile("apps/{api,web}/index")}$`,
    );
    assert.ok(expression.test("apps/api/index"));
    assert.ok(expression.test("apps/web/index"));
    assert.ok(!expression.test("apps/other/index"));
    for (const pattern of [
      "{{a}}",
      "{a,{b}}",
      "{{x}y}",
      "{a,{b,{c}}",
      "{}{a}",
    ]) {
      assert.equal(
        braces.stringify(braces.parse(pattern), { escapeInvalid: true }),
        pattern,
      );
    }
  });

  test(`${label}: expansion rejects cyclic AST parent chains without hanging`, () => {
    for (const multipleParents of [false, true]) {
      const ast = { type: "paren", nodes: [{ type: "text", value: "a" }] };
      ast.parent = multipleParents ? { type: "paren", parent: ast } : ast;
      assert.throws(
        () =>
          runInNewContext(
            "expand(ast)",
            { expand: braces.expand, ast },
            { timeout: 1000 },
          ),
        /AST parent chain contains a cycle/,
      );
    }
  });
}

test("the installed security patch is verified without suppressing scanner advisories", () => {
  const pnpmConfig = readPnpmSecurityConfig(repositoryRoot);
  assert.equal(
    pnpmConfig.patchedDependencies?.["braces@3.0.3"],
    "patches/braces@3.0.3.patch",
  );
  assert.equal(pnpmConfig.auditConfig, undefined);
  const scannerExceptions = readFileSync(
    join(repositoryRoot, ".trivyignore"),
    "utf8",
  )
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith("#"));
  assert.deepEqual(scannerExceptions, []);
  const patch = readFileSync(
    join(repositoryRoot, "patches/braces@3.0.3.patch"),
    "utf8",
  );
  assert.ok(patch.includes("+  MAX_DEPTH: 100,"));
  for (const packageRoot of packageRoots) {
    assert.match(packageRoot, /braces@3\.0\.3_patch_hash=/);
    assert.equal(require(join(packageRoot, "lib/constants.js")).MAX_DEPTH, 100);
  }
});
