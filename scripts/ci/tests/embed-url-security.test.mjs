import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const compilerRequire = createRequire(
  process.env.GAMEGUILD_EDITOR_TEST_WEB_PACKAGE ??
    new URL("../../../apps/web/package.json", import.meta.url),
);
const ts = compilerRequire("typescript");
const embedRoot = path.join(root, "packages/features/lexical-surface/src/features/embeds");

function evaluate(source, fileName, bindings = {}) {
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS,
      jsx: ts.JsxEmit.ReactJSX },
    reportDiagnostics: true, fileName,
  });
  assert.equal(compiled.diagnostics.filter((d) => d.category === ts.DiagnosticCategory.Error).length, 0);
  const module = { exports: {} };
  vm.runInNewContext(compiled.outputText, {
    ...bindings, URL, module, exports: module.exports,
    require(name) { throw new Error("Unadmitted embed parser dependency: " + name); },
  }, { timeout: 1000 });
  return module.exports;
}

async function loadActualConfigs() {
  let parsers = {};
  try {
    const parserSource = await readFile(path.join(embedRoot, "embed-url.ts"), "utf8");
    parsers = evaluate(parserSource, "embed-url.ts");
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
  }
  const pluginSource = await readFile(path.join(embedRoot, "auto-embed-plugin.tsx"), "utf8");
  const plugin = ts.createSourceFile("auto-embed-plugin.tsx", pluginSource,
    ts.ScriptTarget.Latest, true, ts.ScriptKind.TSX);
  const names = ["YoutubeEmbedConfig", "TwitterEmbedConfig", "FigmaEmbedConfig"];
  const declarations = plugin.statements.filter((statement) =>
    ts.isVariableStatement(statement) && statement.declarationList.declarations.some((declaration) =>
      names.includes(declaration.name.getText(plugin))));
  assert.equal(declarations.length, 3);
  // Evaluate the actual config initializers; editor command closures are never called.
  return evaluate(declarations.map((statement) => statement.getFullText(plugin)).join("\n"),
    "embed-configs.tsx", parsers);
}

const configs = await loadActualConfigs();
const youtube = "jNQXAC9IVRw";
const figma = "LKQ4FJ4bTnCSjedbRpk931";
const valid = [
  ["YoutubeEmbedConfig", "https://www.youtube.com/watch?v=" + youtube, youtube],
  ["YoutubeEmbedConfig", "https://youtube.com/watch?feature=shared&v=" + youtube + "&t=1", youtube],
  ["YoutubeEmbedConfig", "https://m.youtube.com/watch?v=" + youtube, youtube],
  ["YoutubeEmbedConfig", "https://youtu.be/" + youtube + "?si=owned", youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com/embed/" + youtube, youtube],
  ["YoutubeEmbedConfig", "https://www.youtube-nocookie.com/embed/" + youtube, youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com/v/" + youtube, youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com/u/w/" + youtube, youtube],
  ["TwitterEmbedConfig", "https://x.com/jack/status/20", "20"],
  ["TwitterEmbedConfig", "https://x.com/i/web/status/20", "20"],
  ["TwitterEmbedConfig", "https://twitter.com/jack/status/20?ref=owned", "20"],
  ["TwitterEmbedConfig", "https://twitter.com/#!/jack/status/20", "20"],
  ["TwitterEmbedConfig", "https://x.com/jack/statuses/20", "20"],
  ["FigmaEmbedConfig", "https://www.figma.com/file/" + figma + "/Sample-File", figma],
  ["FigmaEmbedConfig", "https://figma.com/proto/" + figma + "/Sample?node-id=1-2#owned", figma],
  ["FigmaEmbedConfig", "https://www.figma.com/file/" + figma, figma],
];
for (const [name, input, id] of valid) {
  test(name + " preserves a valid provider URL: " + input, async () => {
    const result = await configs[name].parseUrl(input);
    assert.ok(result);
    assert.equal(result.id, id);
    assert.equal(result.url, new URL(input).href);
  });
}

const invalid = [
  ["YoutubeEmbedConfig", "https://evil.example/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com.evil.example/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "https://evil.example/youtu.be/" + youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com@127.0.0.1/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "https://owned:credential@www.youtube.com/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "http://www.youtube.com/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com:444/watch?v=" + youtube],
  ["YoutubeEmbedConfig", "https://www.youtube.com/watch?v=short"],
  ["YoutubeEmbedConfig", "https://www.youtube.com/watch?v=abcdefghij!"],
  ["YoutubeEmbedConfig", "https://www.youtube.com/watch?v=abcdefghij%22"],
  ["YoutubeEmbedConfig", "https://www.youtube.com/watch/extra?v=" + youtube],
  ["YoutubeEmbedConfig", "https://youtu.be/" + youtube + "/extra"],
  ["TwitterEmbedConfig", "https://x.com.evil.example/jack/status/20"],
  ["TwitterEmbedConfig", "https://x.com@127.0.0.1/jack/status/20"],
  ["TwitterEmbedConfig", "https://owned:credential@twitter.com/jack/status/20"],
  ["TwitterEmbedConfig", "https://x.com:444/jack/status/20"],
  ["TwitterEmbedConfig", "http://x.com/jack/status/20"],
  ["TwitterEmbedConfig", "https://x.com/jack/statuseses/20"],
  ["TwitterEmbedConfig", "https://x.com/jack/status/20script"],
  ["TwitterEmbedConfig", "https://x.com/jack/status/20/extra"],
  ["FigmaEmbedConfig", "https://figmaXcom/file/" + figma],
  ["FigmaEmbedConfig", "https://www.figmaXcom/file/" + figma],
  ["FigmaEmbedConfig", "https://www.figma.com.evil.example/file/" + figma],
  ["FigmaEmbedConfig", "https://evil.example/https://www.figma.com/file/" + figma],
  ["FigmaEmbedConfig", "https://figma.com@127.0.0.1/file/" + figma],
  ["FigmaEmbedConfig", "https://owned:credential@www.figma.com/file/" + figma],
  ["FigmaEmbedConfig", "https://www.figma.com:444/file/" + figma],
  ["FigmaEmbedConfig", "http://www.figma.com/file/" + figma],
  ["FigmaEmbedConfig", "https://www.figma.com/file/short"],
  ["FigmaEmbedConfig", "https://www.figma.com/file/" + "A".repeat(129)],
  ["FigmaEmbedConfig", "https://www.figma.com/file/" + "A".repeat(21) + "%22"],
];
for (const name of ["YoutubeEmbedConfig", "TwitterEmbedConfig", "FigmaEmbedConfig"]) {
  for (const input of ["", "not a URL", "/watch?v=" + youtube,
    "javascript:alert(1)", "data:text/html,owned", "https://localhost/owned"]) {
    invalid.push([name, input]);
  }
}
for (const [name, input] of invalid) {
  test(name + " rejects an untrusted or malformed URL: " + input, async () => {
    assert.equal(await configs[name].parseUrl(input), null);
  });
}
