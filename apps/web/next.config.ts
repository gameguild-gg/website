import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";
import path from "node:path";
import { COI_LEARN_RULES } from "./src/lib/emception/coi-headers";

const configuredDevOrigins = process.env.NEXT_ALLOWED_DEV_ORIGINS?.split(",")
  .map((origin) => origin.trim())
  .filter(Boolean);

const nextConfig: NextConfig = {
  // ponytail: isolated E2E boots a second `next dev` on a dedicated port
  // alongside the user's running dev server; without a separate distDir the
  // two fight over the .next/ dev-server lock. Gated by env so production
  // builds are unaffected.
  ...(process.env.CODING_CYCLE_E2E === "1"
    ? { distDir: ".next-e2e-coding-cycle" }
    : {}),
  allowedDevOrigins: configuredDevOrigins ?? [
    "gameguild.localhost",
    "learning.gameguild.localhost",
    "gameguild.127.0.0.1.sslip.io",
    "learning.gameguild.127.0.0.1.sslip.io",
  ],
  reactCompiler: true,
  output: "standalone",
  outputFileTracingRoot: path.resolve(__dirname, "../.."),
  transpilePackages: [
    "@game-guild/ui",
    "@game-guild/auth-components",
    "@game-guild/block-content-editor",
    "@game-guild/block-list",
    "@game-guild/community-members",
    "@game-guild/content-rendering",
    "@game-guild/lexical-surface",
    "@game-guild/quiz",
    "@game-guild/quiz-surface",
    "@game-guild/courses",
    "@game-guild/dotnet-wasm",
    "@game-guild/emception-ui",
    "emception",
    "@gameguild/emception-browser",
    "@gameguild/emception-react",
    "@gameguild/emception-webcomponent",
    "@gameguild/emception-ide",
    "@gameguild/emception-xterm",
    "mermaid",
    "@mermaid-js/parser",
    "langium",
    "vscode-jsonrpc",
    "chevrotain",
  ],
  images: {
    // ponytail: any https host allowed; tighten back to allowlist if image proxy abuse appears
    remotePatterns: [{ protocol: "https", hostname: "**" }],
    // Previous allowlist:
    // remotePatterns: [
    //   { protocol: "https", hostname: "placehold.co" },
    //   { protocol: "https", hostname: "i.imgur.com" },
    //   { protocol: "https", hostname: "images.unsplash.com" },
    //   { protocol: "https", hostname: "cdn.gameguild.gg" },
    //   { protocol: "https", hostname: "www.python.org" },
    // ],
  },
  experimental: {
    authInterrupts: true,
    cpus: 1,
  },
  turbopack: {
    resolveAlias: {
      module: {
        browser: "./src/lib/browser-node-module-stub.ts",
      },
      "node:module": {
        browser: "./src/lib/browser-node-module-stub.ts",
      },
    },
  },
  async redirects() {
    return [
      {
        source: "/console/community/testing-lab/:path*",
        destination: "/workspace/testing-lab/:path*",
        permanent: true,
      },
      {
        source: "/:locale/console/community/testing-lab/:path*",
        destination: "/:locale/workspace/testing-lab/:path*",
        permanent: true,
      },
      { source: "/dashboard/teams/:path*", destination: "/my/teams/:path*", permanent: true },
      { source: "/dashboard/projects/:path*", destination: "/my/projects/:path*", permanent: true },
      { source: "/dashboard/invitations", destination: "/my/invitations", permanent: true },
      { source: "/dashboard/settings/account", destination: "/my/settings/account", permanent: true },
      { source: "/:locale/dashboard/teams/:path*", destination: "/:locale/my/teams/:path*", permanent: true },
      { source: "/:locale/dashboard/projects/:path*", destination: "/:locale/my/projects/:path*", permanent: true },
      { source: "/:locale/dashboard/invitations", destination: "/:locale/my/invitations", permanent: true },
      { source: "/:locale/dashboard/settings/account", destination: "/:locale/my/settings/account", permanent: true },
    ];
  },
  async rewrites() {
    return [
      {
        source: "/langs/:file*.wasm",
        destination: "/langs/:file*.wasm.gz",
      },
      {
        source: "/langs/:file*.js",
        destination: "/langs/:file*.js.gz",
      },
      {
        source: "/langs/:file*.json",
        destination: "/langs/:file*.json.gz",
      },
      {
        source: "/pyodide/:file*.js",
        destination: "/pyodide/:file*.js.gz",
      },
    ];
  },
  async headers() {
    return [
      {
        // ponytail: COOP only — GIS popup sign-in breaks under COEP
        // credentialless (opener postMessage severed, Google session
        // stripped from the GIS iframe). COEP stays on emception/learn
        // routes only, where SharedArrayBuffer actually needs it.
        source: "/sign-in",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin-allow-popups" },
        ],
      },
      {
        source: "/:locale/sign-in",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin-allow-popups" },
        ],
      },
      {
        source: "/api/auth/:path*",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin-allow-popups" },
        ],
      },
      {
        source: "/:locale/workspace/economy/kyc",
        headers: [
          {
            key: "Content-Security-Policy",
            value: "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval' https://static.sumsub.com; style-src 'self' 'unsafe-inline' https://static.sumsub.com; frame-src 'self' https://*.sumsub.com; connect-src 'self' https://*.sumsub.com wss://*.sumsub.com; img-src 'self' data: blob: https://*.sumsub.com; media-src 'self' blob: https://*.sumsub.com; worker-src 'self' blob:;",
          },
          {
            key: "Permissions-Policy",
            value: "camera=(self \"https://api.sumsub.com\"), microphone=(self \"https://api.sumsub.com\")",
          },
        ],
      },
      {
        source: "/block-content-editor/:path((?!doc-editor|quiz-editor).*)",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "Cross-Origin-Embedder-Policy", value: "credentialless" },
        ],
      },
      {
        source:
          "/:locale/block-content-editor/:path((?!doc-editor|quiz-editor).*)",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "Cross-Origin-Embedder-Policy", value: "credentialless" },
        ],
      },
      ...COI_LEARN_RULES,
      // A cross-origin-isolated page may only spawn Workers whose script
      // response also carries COEP, else Chromium blocks it with
      // ERR_BLOCKED_BY_RESPONSE and the emception boot hangs forever.
      {
        source: "/_next/static/chunks/:file(emception-toolchain.*)",
        headers: [
          { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
          { key: "Cross-Origin-Embedder-Policy", value: "credentialless" },
        ],
      },
      {
        source: "/wasm/:path*.wasm",
        headers: [
          { key: "Content-Encoding", value: "gzip" },
          { key: "Content-Type", value: "application/wasm" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/langs/:path*.wasm",
        headers: [
          { key: "Content-Encoding", value: "gzip" },
          { key: "Content-Type", value: "application/wasm" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/langs/:path*.js",
        headers: [
          { key: "Content-Encoding", value: "gzip" },
          { key: "Content-Type", value: "application/javascript" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/langs/:path*.json",
        headers: [
          { key: "Content-Encoding", value: "gzip" },
          { key: "Content-Type", value: "application/json" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/langs/:path*.zip",
        headers: [
          { key: "Content-Type", value: "application/zip" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/pyodide/:path*.js",
        headers: [
          { key: "Content-Encoding", value: "gzip" },
          { key: "Content-Type", value: "application/javascript" },
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/managed/:path*",
        headers: [
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
      {
        source: "/mathlive/:path*",
        headers: [
          {
            key: "Cache-Control",
            value: "public, max-age=31536000, immutable",
          },
          { key: "Cross-Origin-Resource-Policy", value: "cross-origin" },
        ],
      },
    ];
  },
  webpack: (config, { isServer, webpack }) => {
    if (process.env.GAMEGUILD_DISABLE_WEBPACK_CACHE === "1") {
      config.cache = false;
    }

    const resolvePackageDir = (pkg: string): string => {
      try {
        return path.dirname(require.resolve(`${pkg}/package.json`));
      } catch {
        const nested = [
          path.resolve(
            __dirname,
            "node_modules/vscode-languageserver/node_modules",
            pkg,
          ),
          path.resolve(
            __dirname,
            "../../node_modules/vscode-languageserver/node_modules",
            pkg,
          ),
        ];

        return (
          nested.find((candidate) =>
            require("node:fs").existsSync(candidate),
          ) ?? pkg
        );
      }
    };

    config.resolve.alias = {
      ...config.resolve.alias,
      // The isolated coding-cycle runner must exercise the packages checked
      // out beside this app. A developer may have another worktree mounted
      // through node_modules, which would otherwise make a green browser run
      // validate stale Emception code instead of the change under test.
      ...(process.env.CODING_CYCLE_E2E === "1"
        ? {
            // `$` makes this entry exact. Without it, webpack consumes every
            // `@game-guild/emception-ui/assessment/*` import with this root
            // alias before it reaches the file aliases below.
            "@game-guild/emception-ui$": path.resolve(
              __dirname,
              "../../packages/infrastructure/ui-emception/src",
            ),
            "@game-guild/emception-ui/assessment/editor": path.resolve(
              __dirname,
              "../../packages/infrastructure/ui-emception/src/assessment/editor.ts",
            ),
            "@game-guild/emception-ui/assessment/plan": path.resolve(
              __dirname,
              "../../packages/infrastructure/ui-emception/src/assessment/plan.ts",
            ),
            "@game-guild/emception-ui/assessment/presets": path.resolve(
              __dirname,
              "../../packages/infrastructure/ui-emception/src/assessment/presets.ts",
            ),
            "@game-guild/emception-ui/assessment/storage": path.resolve(
              __dirname,
              "../../packages/infrastructure/ui-emception/src/assessment/workspace-storage.ts",
            ),
            emception: path.resolve(
              __dirname,
              "../../tools/emception/packages/core/dist",
            ),
            "@gameguild/emception-browser": path.resolve(
              __dirname,
              "../../tools/emception/packages/browser/dist",
            ),
            "@gameguild/emception-ide": path.resolve(
              __dirname,
              "../../tools/emception/packages/ide/dist",
            ),
          }
        : {}),
      "@game-guild/dotnet-wasm": path.resolve(
        __dirname,
        "../../packages/infrastructure/wasm/dotnet/src/index.ts",
      ),
      "vscode-jsonrpc": resolvePackageDir("vscode-jsonrpc"),
      "vscode-languageserver-protocol": resolvePackageDir(
        "vscode-languageserver-protocol",
      ),
      "vscode-languageserver-types": resolvePackageDir(
        "vscode-languageserver-types",
      ),
    };

    config.resolve.fallback = {
      ...config.resolve.fallback,
      module: false,
      fs: false,
      path: false,
      canvas: false,
      crypto: false,
      stream: false,
      buffer: false,
    };

    config.module.rules.push({
      test: /\.md$/,
      type: "asset/source",
    });

    // Handle Vite-style `?raw` imports (used by @gameguild/emception-browser
    // to import subprocess_shim.py as a string). Webpack needs an explicit
    // rule for the resourceQuery; without it, the .py file is parsed as JS.
    config.module.rules.push({
      resourceQuery: /raw/,
      type: "asset/source",
    });

    if (isServer) {
      config.plugins ??= [];
      config.plugins.push(
        new webpack.DefinePlugin({
          self: "globalThis",
        }),
      );
    }

    return config;
  },
};

const withNextIntl = createNextIntlPlugin();
export default withNextIntl(nextConfig);
