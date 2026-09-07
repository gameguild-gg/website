import { readFileSync, readdirSync } from "node:fs";
import { extname, join, resolve } from "node:path";
import { describe, expect, it } from "vitest";

const PACKAGE_ROOT = process.cwd();
const SOURCE_ROOT = resolve(PACKAGE_ROOT, "src");
const PACKAGE_JSON = resolve(PACKAGE_ROOT, "package.json");

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    return [".ts", ".tsx"].includes(extname(entry.name)) ? [path] : [];
  });
}

describe("package architecture", () => {
  it("keeps the source root limited to responsibility directories", () => {
    const entries = readdirSync(SOURCE_ROOT).sort();

    expect(entries).toEqual([
      "__tests__",
      "capabilities",
      "css.d.ts",
      "editor-ui",
      "features",
      "icons",
      "index.ts",
      "schema",
      "shared",
      "surface",
    ]);
  });

  it("keeps package internals behind relative imports", () => {
    const violations = sourceFiles(SOURCE_ROOT).filter((path) => {
      const source = readFileSync(path, "utf8");
      return /from\s+["']@game-guild\/lexical-surface\//.test(source);
    });

    expect(violations).toEqual([]);
  });

  it("keeps diagram feature roots limited to contracts and responsibility folders", () => {
    const mermaidEntries = readdirSync(
      resolve(SOURCE_ROOT, "features", "mermaid"),
    ).sort();
    const vegaLiteEntries = readdirSync(
      resolve(SOURCE_ROOT, "features", "vega-lite"),
    ).sort();

    expect(mermaidEntries).toEqual([
      "README.md",
      "editor",
      "index.ts",
      "lexical",
      "mermaid-data.ts",
      "rendering",
      "templates",
      "theme",
    ]);
    expect(vegaLiteEntries).toEqual([
      "README.md",
      "data",
      "editor",
      "index.ts",
      "lexical",
      "rendering",
      "templates",
      "theme",
      "vega-lite-data.ts",
    ]);
  });

  it("does not import application-owned modules", () => {
    const violations = sourceFiles(SOURCE_ROOT).filter((path) => {
      const source = readFileSync(path, "utf8");
      return /from\s+["'][^"']*(?:apps\/web|block-content-editor)/.test(source);
    });

    expect(violations).toEqual([]);
  });

  it("keeps shared infrastructure independent from document features", () => {
    const violations = sourceFiles(resolve(SOURCE_ROOT, "shared")).filter(
      (path) => {
        const source = readFileSync(path, "utf8");
        return /from\s+["'][^"']*features\//.test(source);
      },
    );

    expect(violations).toEqual([]);
  });

  it("keeps shared formatting independent from concrete toolbars", () => {
    const violations = sourceFiles(
      resolve(SOURCE_ROOT, "editor-ui", "formatting"),
    ).filter((path) => {
      const source = readFileSync(path, "utf8");
      return /from\s+["'][^"']*(?:top-toolbar|floating-toolbar)/.test(source);
    });

    expect(violations).toEqual([]);
  });

  it("exposes only the documented root entry point", () => {
    const manifest = JSON.parse(readFileSync(PACKAGE_JSON, "utf8")) as {
      exports: Record<string, string>;
    };

    expect(manifest.exports).toEqual({ ".": "./src/index.ts" });
  });

  it("owns the runtime dependencies of its built-in diagram features", () => {
    const manifest = JSON.parse(readFileSync(PACKAGE_JSON, "utf8")) as {
      dependencies: Record<string, string>;
    };

    expect(manifest.dependencies).toEqual(
      expect.objectContaining({
        "@monaco-editor/react": expect.any(String),
        "@shikijs/monaco": expect.any(String),
        "d3-dsv": expect.any(String),
        dompurify: expect.any(String),
        mermaid: expect.any(String),
        "monaco-editor": expect.any(String),
        shiki: expect.any(String),
        vega: expect.any(String),
        "vega-lite": expect.any(String),
        "vega-themes": expect.any(String),
      }),
    );
  });

  it("keeps React host-owned and avoids framework theme dependencies", () => {
    const manifest = JSON.parse(readFileSync(PACKAGE_JSON, "utf8")) as {
      dependencies: Record<string, string>;
      peerDependencies: Record<string, string>;
    };

    expect(manifest.peerDependencies).toEqual(
      expect.objectContaining({
        react: expect.any(String),
        "react-dom": expect.any(String),
      }),
    );
    expect(manifest.dependencies).not.toHaveProperty("react");
    expect(manifest.dependencies).not.toHaveProperty("react-dom");
    expect(manifest.dependencies).not.toHaveProperty("next-themes");
  });

  it("does not add hidden network or dynamic CommonJS asset dependencies", () => {
    const violations = sourceFiles(SOURCE_ROOT).filter((path) => {
      const source = readFileSync(path, "utf8");
      return /fonts\.googleapis|cdn\.jsdelivr|\brequire\s*\(/.test(source);
    });

    expect(violations).toEqual([]);
  });

  it("does not configure Mermaid with loose security", () => {
    const violations = sourceFiles(SOURCE_ROOT).filter((path) => {
      const source = readFileSync(path, "utf8");
      return /securityLevel\s*:\s*["']loose["']/.test(source);
    });

    expect(violations).toEqual([]);
  });
});
