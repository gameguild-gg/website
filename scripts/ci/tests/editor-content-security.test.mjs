import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import path from "node:path";
import test from "node:test";
import { fileURLToPath } from "node:url";
import vm from "node:vm";

const root = fileURLToPath(new URL("../../..", import.meta.url));
const webRequire = createRequire(
  process.env.GAMEGUILD_EDITOR_TEST_WEB_PACKAGE ??
    new URL("../../../apps/web/package.json", import.meta.url),
);
const { JSDOM } = webRequire("jsdom");
const React = webRequire("react");
const ts = webRequire("typescript");
const emojiPath = "packages/features/lexical-surface/src/editor-ui/emoji/emoji-picker-plugin.tsx";
const nodePath = "packages/features/lexical-surface/src/features/excalidraw/excalidraw-node.tsx";
const imagePath = "packages/features/lexical-surface/src/features/excalidraw/excalidraw-image.tsx";

async function emojiSearch(queryString, emojiOptions) {
  const source = await readFile(path.join(root, emojiPath), "utf8");
  const startMarker = "const options = useMemo(() => {";
  const endMarker = "\n  }, [emojiOptions, queryString]);";
  const start = source.indexOf(startMarker);
  const end = source.indexOf(endMarker, start);
  assert.notEqual(start, -1);
  assert.notEqual(end, -1);
  const body = source.slice(start + startMarker.length, end)
    .replace("let regex: RegExp;", "let regex;");
  return Array.from(vm.runInNewContext(`(() => {${body}\n})()`, {
    queryString, emojiOptions, MAX_EMOJI_SUGGESTION_COUNT: 10,
  }));
}

const emojiOptions = [
  { title: "😀 grinning", keywords: ["smile", "happy"] },
  { title: "❤️ heart", keywords: ["love", "affection"] },
  { title: "🐈 cat", keywords: ["pet", "animal"] },
  { title: "owned a.b", keywords: ["literal.dot"] },
  { title: "owned axb", keywords: ["plain"] },
];
for (const [query, expected] of [
  ["GRIN", [0]], ["HaPpY", [0]], ["love", [1]], ["pet", [2]],
  ["😀", [0]], ["missing", []], ["a.b", [3]], ["literal.dot", [3]],
  [".*", []], ["[", []], ["(a+)+$", []], ["cat|heart", []],
  ["^", []], ["$", []], ["\\", []],
]) {
  test(`emoji query is a case-insensitive literal: ${query}`, async () => {
    assert.deepEqual(await emojiSearch(query, emojiOptions), expected.map((i) => emojiOptions[i]));
  });
}

test("emoji defaults and matching results preserve order and the ten-item limit", async () => {
  const options = Array.from({ length: 20 }, (_, index) => ({ title: `owned ${index}`, keywords: [] }));
  for (const query of [null, "", "owned"]) {
    assert.deepEqual(await emojiSearch(query, options), options.slice(0, 10));
  }
});

function domFixture() {
  // Default jsdom settings do not execute scripts or load external resources.
  const dom = new JSDOM("<!doctype html><html><body><div id='root'></div></body></html>");
  const keys = ["window", "document", "navigator", "HTMLElement", "SVGElement", "Node", "IS_REACT_ACT_ENVIRONMENT"];
  const descriptors = new Map(keys.map((key) => [key, Object.getOwnPropertyDescriptor(globalThis, key)]));
  for (const key of keys) {
    Object.defineProperty(globalThis, key, { configurable: true, writable: true,
      value: key === "IS_REACT_ACT_ENVIRONMENT" ? true : key === "window" ? dom.window : dom.window[key] });
  }
  let writes = 0;
  const descriptor = Object.getOwnPropertyDescriptor(dom.window.Element.prototype, "innerHTML");
  Object.defineProperty(dom.window.Element.prototype, "innerHTML", {
    ...descriptor,
    set(value) { writes++; descriptor.set.call(this, value); },
  });
  return {
    document: dom.window.document,
    get writes() { return writes; },
    close() {
      dom.window.close();
      for (const key of keys) {
        const original = descriptors.get(key);
        if (original) Object.defineProperty(globalThis, key, original);
        else delete globalThis[key];
      }
    },
  };
}

function createSvg(document, label) {
  const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
  svg.setAttribute("viewBox", "0 0 640 480");
  const group = document.createElementNS(svg.namespaceURI, "g");
  const text = document.createElementNS(svg.namespaceURI, "text");
  text.textContent = label;
  group.appendChild(text);
  svg.appendChild(group);
  return svg;
}

async function exportSvg(document, content, width = 640, height = 480) {
  const source = await readFile(path.join(root, nodePath), "utf8");
  const marker = "exportDOM(editor: LexicalEditor): DOMExportOutput {";
  const start = source.indexOf(marker);
  const end = source.indexOf("\n  }", start);
  assert.notEqual(start, -1);
  assert.notEqual(end, -1);
  const method = vm.runInNewContext(`(function(editor) {${source.slice(start + marker.length, end)}\n})`, { document });
  const data = JSON.stringify({ text: "owned <&\" label", elements: [] });
  const result = method.call({ __width: width, __height: height, __data: data, getKey: () => "owned-key" }, {
    getElementByKey(key) { assert.equal(key, "owned-key"); return content; },
  });
  return { ...result, data };
}

test("Excalidraw export deep-clones the generated SVG without parsing markup", async () => {
  const fixture = domFixture();
  try {
    const svg = createSvg(fixture.document, "owned <svg> & text");
    const sourceContainer = fixture.document.createElement("div");
    sourceContainer.appendChild(svg);
    const { element, data } = await exportSvg(fixture.document, sourceContainer);
    assert.equal(fixture.writes, 0);
    assert.notEqual(element.firstElementChild, svg);
    assert.equal(svg.parentElement, sourceContainer);
    assert.equal(element.firstElementChild.namespaceURI, svg.namespaceURI);
    assert.equal(element.querySelector("text").textContent, "owned <svg> & text");
    assert.equal(element.firstElementChild.getAttribute("viewBox"), "0 0 640 480");
    assert.equal(element.style.width, "640px");
    assert.equal(element.style.height, "480px");
    assert.equal(element.getAttribute("data-lexical-excalidraw-json"), data);
  } finally { fixture.close(); }
});

test("Excalidraw export keeps inherited dimensions and absent SVG content", async () => {
  const fixture = domFixture();
  try {
    for (const content of [null, fixture.document.createElement("div")]) {
      const { element, data } = await exportSvg(fixture.document, content, "inherit", "inherit");
      assert.equal(element.childNodes.length, 0);
      assert.equal(element.style.width, "inherit");
      assert.equal(element.style.height, "inherit");
      assert.equal(element.getAttribute("data-lexical-excalidraw-json"), data);
    }
  } finally { fixture.close(); }
});

async function imageComponent(exportToSvg) {
  const source = await readFile(path.join(root, imagePath), "utf8");
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS,
      jsx: ts.JsxEmit.ReactJSX, esModuleInterop: true },
    reportDiagnostics: true,
    fileName: "excalidraw-image.tsx",
  });
  assert.deepEqual(compiled.diagnostics.filter((d) => d.category === ts.DiagnosticCategory.Error), []);
  const module = { exports: {} };
  vm.runInNewContext(compiled.outputText, {
    module, exports: module.exports,
    require(name) {
      if (name === "react" || name === "react/jsx-runtime") return webRequire(name);
      if (name === "@excalidraw/excalidraw") return { exportToSvg };
      throw new Error(`Unadmitted component dependency: ${name}`);
    },
  });
  return module.exports.default;
}

test("Excalidraw React rendering mounts and replaces the generated SVG directly", async () => {
  const fixture = domFixture();
  const { createRoot } = webRequire("react-dom/client");
  const reactRoot = createRoot(fixture.document.getElementById("root"));
  try {
    const generated = [];
    const requests = [];
    const Image = await imageComponent(async (request) => {
      requests.push(request);
      const svg = createSvg(fixture.document, `owned scene ${generated.length}`);
      generated.push(svg);
      return svg;
    });
    const imageContainerRef = React.createRef();
    const appState = { owned: "state" };
    const elements = [{ owned: "element" }];
    const files = { owned: "file" };
    for (const [index, width, height] of [[0, 640, 480], [1, "inherit", "inherit"]]) {
      const currentElements = index === 0 ? elements : [...elements];
      await React.act(async () => {
        reactRoot.render(React.createElement(Image, { appState, elements: currentElements, files,
          imageContainerRef, width, height, rootClassName: "owned-svg" }));
      });
      assert.equal(fixture.writes, 0);
      assert.equal(imageContainerRef.current.childNodes.length, 1);
      assert.equal(imageContainerRef.current.firstChild, generated[index]);
      assert.equal(imageContainerRef.current.querySelector("text").textContent, `owned scene ${index}`);
      assert.equal(generated[index].getAttribute("width"), "100%");
      assert.equal(generated[index].getAttribute("height"), "100%");
      assert.equal(imageContainerRef.current.className, "owned-svg");
      assert.equal(imageContainerRef.current.style.width, width === "inherit" ? "" : "640px");
      assert.equal(imageContainerRef.current.style.height, height === "inherit" ? "" : "480px");
      assert.equal(requests[index].appState, appState);
      assert.equal(requests[index].elements, currentElements);
      assert.equal(requests[index].files, files);
    }
  } finally {
    await React.act(async () => reactRoot.unmount());
    fixture.close();
  }
});


test("Excalidraw React rendering keeps the newest async export when an older export resolves late", async () => {
  const fixture = domFixture();
  const { createRoot } = webRequire("react-dom/client");
  const reactRoot = createRoot(fixture.document.getElementById("root"));
  try {
    const pending = [];
    const Image = await imageComponent(() => new Promise((resolve) => pending.push(resolve)));
    const imageContainerRef = React.createRef();
    const props = { appState: {}, files: {}, imageContainerRef };
    await React.act(async () => {
      reactRoot.render(React.createElement(Image, { ...props, elements: [{ scene: "old" }] }));
    });
    assert.equal(pending.length, 1);
    assert.equal(imageContainerRef.current.childNodes.length, 0);
    await React.act(async () => {
      reactRoot.render(React.createElement(Image, { ...props, elements: [{ scene: "new" }] }));
    });
    assert.equal(pending.length, 2);
    const newest = createSvg(fixture.document, "newest scene");
    await React.act(async () => { pending[1](newest); });
    assert.equal(imageContainerRef.current.firstChild, newest);
    const stale = createSvg(fixture.document, "stale scene");
    await React.act(async () => { pending[0](stale); });
    assert.equal(imageContainerRef.current.firstChild, newest);
    assert.equal(imageContainerRef.current.childNodes.length, 1);
    assert.equal(stale.parentElement, null);
    assert.equal(fixture.writes, 0);
  } finally {
    await React.act(async () => reactRoot.unmount());
    fixture.close();
  }
});

test("Excalidraw React rendering does not mount a pending SVG after unmount", async () => {
  const fixture = domFixture();
  const { createRoot } = webRequire("react-dom/client");
  const reactRoot = createRoot(fixture.document.getElementById("root"));
  let mounted = true;
  try {
    let resolveExport;
    const Image = await imageComponent(() => new Promise((resolve) => { resolveExport = resolve; }));
    await React.act(async () => {
      reactRoot.render(React.createElement(Image, { appState: {}, files: {}, elements: [],
        imageContainerRef: React.createRef() }));
    });
    assert.equal(typeof resolveExport, "function");
    await React.act(async () => reactRoot.unmount());
    mounted = false;
    const late = createSvg(fixture.document, "late scene");
    await React.act(async () => { resolveExport(late); });
    assert.equal(late.parentElement, null);
    assert.equal(fixture.document.getElementById("root").childNodes.length, 0);
    assert.equal(fixture.writes, 0);
  } finally {
    if (mounted) await React.act(async () => reactRoot.unmount());
    fixture.close();
  }
});
