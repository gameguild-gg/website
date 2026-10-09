import { fixupConfigRules } from "@eslint/compat";
import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import typescriptEslint from "typescript-eslint";

const baselineRules = new Set([
  "@next/next/no-before-interactive-script-outside-document",
  "@next/next/no-img-element",
  "@next/next/no-location-assign-relative-destination",
  "@typescript-eslint/no-unused-expressions",
  "@typescript-eslint/no-unused-vars",
  "import/no-anonymous-default-export",
  "jsx-a11y/alt-text",
  "react-hooks/exhaustive-deps",
]);

function promoteBaselineRules(configs) {
  return configs.map((config) => ({
    ...config,
    rules: Object.fromEntries(
      Object.entries(config.rules ?? {}).map(([rule, setting]) => [
        rule,
        baselineRules.has(rule)
          ? Array.isArray(setting)
            ? ["error", ...setting.slice(1)]
            : "error"
          : setting,
      ]),
    ),
  }));
}

const eslintConfig = defineConfig([
  ...fixupConfigRules(promoteBaselineRules(nextVitals)),
  ...fixupConfigRules(promoteBaselineRules(nextTs)),
  // Type-aware TS rules (Codacy parity: no-unsafe-*), same shape as
  // packages/tooling/eslint/src/index.js — kept local because apps/web
  // composes eslint-config-next presets instead of @game-guild/eslint-config.
  // eslint-config-next/typescript already registers the @typescript-eslint
  // plugin (same instance), and fixupConfigRules wraps that registration —
  // re-declaring plugins here trips the "Cannot redefine plugin" guard, so
  // the plugin key is stripped from the added configs.
  ...typescriptEslint.configs.recommendedTypeChecked.map(({ plugins: _plugins, ...config }) => config),
  {
    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
  },
  // Plain JS files are rarely in a tsconfig project; drop type-aware linting
  // there (resets projectService:false) so config files don't fail the
  // project service lookup.
  {
    files: ["**/*.{js,mjs,cjs}"],
    ...typescriptEslint.configs.disableTypeChecked,
  },
  // Test files are excluded from tsconfig.json by design (Next.js projects
  // don't type-check vitest files); without a TS project the project service
  // cannot parse them. Drop type-aware rules there — same tradeoff as JS.
  {
    files: [
      "**/*.test.{ts,tsx,mts,js,jsx,mjs}",
      "**/*.spec.{ts,tsx,mts,js,jsx,mjs}",
      "src/test/**/*",
      "**/__tests__/**/*",
    ],
    ...typescriptEslint.configs.disableTypeChecked,
  },
  {
    // ESLint suppressions only baseline errors. Promote warning-only rules so
    // legacy debt is recorded explicitly and every new violation still fails CI.
    linterOptions: {
      reportUnusedDisableDirectives: "error",
    },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    ".next-*/**",
    "out/**",
    "build/**",
    "coverage/**",
    "next-env.d.ts",
    // Versioned browser assets are generated or vendored and linted at source.
    "public/**",
    "test-results/**",
  ]),
]);

export default eslintConfig;
