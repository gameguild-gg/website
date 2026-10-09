import { readFileSync } from "node:fs";
import { readdir, readFile } from "node:fs/promises";
import { createRequire } from "node:module";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const repoRoot = resolve(process.cwd(), "../..");

function readRepoFile(relativePath: string): string {
  return readFileSync(resolve(repoRoot, relativePath), "utf8");
}

async function readSourceFiles(
  relativePath: string,
): Promise<Array<{ file: string; source: string }>> {
  const absolutePath = resolve(repoRoot, relativePath);
  const entries = await readdir(absolutePath, { withFileTypes: true });
  const sources = await Promise.all(
    entries.map(async (entry) => {
      const filePath = resolve(absolutePath, entry.name);
      const repoRelativePath = `${relativePath}/${entry.name}`;

      if (entry.isDirectory()) {
        if (entry.name === "docs") return [];
        return readSourceFiles(repoRelativePath);
      }

      if (!/\.[cm]?[jt]sx?$/.test(entry.name)) return [];
      return [
        { file: repoRelativePath, source: await readFile(filePath, "utf8") },
      ];
    }),
  );

  return sources.flat();
}

describe("web runtime hardening", () => {
  it("keeps React Compiler enabled with the Devtron build capacity", () => {
    const nextConfig = readRepoFile("apps/web/next.config.ts");
    const economyGate = readRepoFile("scripts/ci/verify-economy.sh");
    const dockerfile = readRepoFile("apps/web/Dockerfile");

    expect(nextConfig).toContain("reactCompiler: true");
    expect(nextConfig).toContain(
      'process.env.npm_lifecycle_event === "dev:fast"',
    );
    expect(nextConfig).toContain(
      "../../packages/infrastructure/client/dist-fast/index.js",
    );
    expect(nextConfig).toContain(
      'outputFileTracingRoot: path.resolve(__dirname, "../..")',
    );
    expect(nextConfig).toContain(
      'process.env.GAMEGUILD_DISABLE_WEBPACK_CACHE === "1"',
    );
    expect(economyGate).toContain("GAMEGUILD_DISABLE_WEBPACK_CACHE=1");
    expect(dockerfile).toContain("NODE_OPTIONS=--max-old-space-size=4096");
    expect(dockerfile).toContain("GAMEGUILD_DISABLE_WEBPACK_CACHE=1");
    expect(dockerfile).not.toContain("NODE_OPTIONS=--max-old-space-size=2048");
  });

  it("uses POSIX-safe quoting when resolving the emception toolchain version", () => {
    const dockerfile = readRepoFile("apps/web/Dockerfile");

    expect(dockerfile).toContain(
      `node -p 'require("./tools/emception/packages/toolchain/package.json").version'`,
    );
    expect(dockerfile).not.toContain(
      `node -p \\"require('./tools/emception/packages/toolchain/package.json').version\\"`,
    );
  });

  it("keeps candidate layers proxy-safe by pruning non-runtime native packages", () => {
    const dockerfile = readRepoFile("apps/web/Dockerfile");

    expect(dockerfile).toContain("@swc+core-linux-x64-musl@*");
    expect(dockerfile).toContain("@img+sharp-linuxmusl-x64@*");
    expect(dockerfile).toContain("@img+sharp-libvips-linuxmusl-x64@*");
    expect(dockerfile).toContain("@esbuild+linux-x64@*");

    expect(
      readRepoFile(".github/actions/build-candidate/action.yml"),
    ).toContain("mode=min");

    expect(dockerfile).toContain("/runtime-sharded/root ./");
    expect(dockerfile).not.toContain("/repo/apps/web/.next/standalone ./");
    for (let shard = 0; shard < 8; shard += 1) {
      expect(dockerfile).toContain(
        `/runtime-sharded/shards/${shard} ./node_modules/.pnpm/`,
      );
    }
  });

  it("uses lightweight container readiness and keeps the web health endpoint available", () => {
    const compose = readRepoFile("compose.coolify.yaml");

    expect(compose).toContain(
      "fetch('http://127.0.0.1:3000/manifest.webmanifest')",
    );
    expect(readRepoFile("apps/web/src/app/api/health/route.ts")).toContain(
      "status: 'healthy'",
    );
  });

  it("logs route handler failures with structured runtime events", () => {
    expect(readRepoFile("apps/web/src/app/api/health/route.ts")).toContain(
      "logWebRequest",
    );
  });

  it("does not depend on the internal Next loadable alias", async () => {
    expect(readRepoFile("apps/web/next.config.ts")).not.toContain(
      "next/dist/server/route-modules/app-page/vendored/contexts/loadable",
    );

    const dynamicImports = (
      await readSourceFiles("apps/web/src/components/block-content-editor")
    ).filter(({ source }) => source.includes("next/dynamic"));

    expect(dynamicImports.map(({ file }) => file)).toEqual([]);
  });

  it("loads the Emception Python shim as source in both bundlers", () => {
    const nextConfig = readRepoFile("apps/web/next.config.ts");
    const loaderPath = resolve(
      repoRoot,
      "apps/web/scripts/turbopack-raw-source-loader.cjs",
    );
    const rawSourceLoader = createRequire(import.meta.url)(loaderPath) as (
      source: string,
    ) => string;

    expect(nextConfig).toContain('"*.py"');
    expect(nextConfig).toContain("turbopack-raw-source-loader.cjs");
    expect(nextConfig).toContain("resourceQuery: /raw/");
    expect(rawSourceLoader('print("ready")\n')).toBe(
      'export default "print(\\\"ready\\\")\\n";',
    );
  });

  it("limits Tailwind discovery to executable UI source", () => {
    const sharedStyles = readRepoFile("packages/ui/src/styles/globals.css");
    const webStyles = readRepoFile("apps/web/src/styles/globals.css");

    expect(sharedStyles).toContain('@import "tailwindcss" source(none)');
    expect(webStyles).toContain('@source "../**/*.{js,jsx,ts,tsx}"');
    expect(webStyles).not.toContain("*.md");
    expect(webStyles).not.toContain("*.ipynb");
  });

  it("keeps the regular and fast development orchestrators separate", () => {
    const rootPackage = JSON.parse(readRepoFile("package.json")) as {
      scripts: Record<string, string>;
    };
    const regularDev = readRepoFile("scripts/dev.mjs");
    const fastDev = readRepoFile("scripts/dev-fast.mjs");

    expect(rootPackage.scripts.dev).toBe("node scripts/dev.mjs");
    expect(rootPackage.scripts["dev:fast"]).toBe("node scripts/dev-fast.mjs");
    expect(regularDev).not.toContain("dev:fast");
    expect(regularDev).not.toContain("build:fast");
    expect(fastDev).toContain('registerDevProcess("dev-fast")');
    expect(fastDev).toContain('"build:fast"');
  });
});
