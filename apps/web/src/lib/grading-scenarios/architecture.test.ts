import { readFileSync, readdirSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

const webSource = resolve(process.cwd(), "src");
const directory = resolve(webSource, "lib/grading-scenarios");

const genericSources = [
  resolve(directory, "contracts.ts"),
  resolve(directory, "service.server.ts"),
  resolve(directory, "actions.ts"),
  resolve(
    webSource,
    "components/learning/grading-validation/grading-scenario-lab.tsx",
  ),
];

describe("grading scenario architecture", () => {
  it("keeps assessment-type dependencies behind registered adapters", () => {
    for (const sourcePath of genericSources) {
      const source = readFileSync(sourcePath, "utf8");
      expect(source).not.toMatch(
        /quiz-grading-scenario|grading-adapter-quiz|@game-guild\/quiz|adapters\/quiz/,
      );
    }

    const registry = readFileSync(
      resolve(directory, "registry.server.ts"),
      "utf8",
    );
    const quizAdapter = readFileSync(
      resolve(directory, "adapters/quiz/adapter.server.ts"),
      "utf8",
    );
    expect(registry).toContain("./adapters/quiz/adapter.server");
    expect(quizAdapter).toContain("./operations.server.mjs");
    expect(quizAdapter).not.toContain("scripts/quiz-grading-scenario");
  });

  it("keeps application source independent from scripts and e2e", () => {
    for (const sourcePath of sourceFiles(webSource)) {
      const source = readFileSync(sourcePath, "utf8");
      expect(source).not.toMatch(
        /(?:from\s+|import\s*\()\s*["'][^"']*(?:scripts\/|e2e\/)/,
      );
    }
  });
});

function sourceFiles(root: string): string[] {
  return readdirSync(root, { withFileTypes: true }).flatMap((entry) => {
    const path = resolve(root, entry.name);
    if (entry.isDirectory()) return sourceFiles(path);
    if (/\.(?:test|spec)\.(?:ts|tsx|js|jsx|mjs)$/.test(entry.name)) return [];
    return /\.(?:ts|tsx|js|jsx|mjs)$/.test(entry.name) ? [path] : [];
  });
}
