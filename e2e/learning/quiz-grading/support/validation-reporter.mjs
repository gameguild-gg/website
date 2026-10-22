import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname } from "node:path";
import { getValidationCase } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs";
import { scenarioDirectory } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/manifest.server.mjs";

export function browserResultJournalPath(scenarioKey) {
  return `${scenarioDirectory(scenarioKey)}/browser-results.json`;
}

export function readBrowserResultJournal(scenarioKey) {
  try {
    return JSON.parse(
      readFileSync(browserResultJournalPath(scenarioKey), "utf8"),
    );
  } catch (error) {
    if (error?.code === "ENOENT") return { schemaVersion: 1, cases: {} };
    throw error;
  }
}

export default class QuizGradingValidationReporter {
  constructor() {
    this.scenarioKey = process.env.QUIZ_GRADING_SCENARIO;
    this.journal = { schemaVersion: 1, cases: {} };
  }

  onBegin() {
    if (!this.scenarioKey) return;
    this.persist();
  }

  onTestEnd(test, result) {
    if (!this.scenarioKey) return;
    const caseId = caseIdFromTitle(test.title);
    if (!caseId) return;
    const current = this.journal.cases[caseId] ?? [];
    current.push({
      file: test.location.file.replaceAll("\\", "/"),
      status: result.status,
    });
    this.journal.cases[caseId] = current;
    this.persist();
  }

  persist() {
    const target = browserResultJournalPath(this.scenarioKey);
    mkdirSync(dirname(target), { recursive: true, mode: 0o700 });
    writeFileSync(target, `${JSON.stringify(this.journal, null, 2)}\n`, {
      encoding: "utf8",
      mode: 0o600,
    });
  }
}

function caseIdFromTitle(title) {
  const stableId = title.match(/\bQG-[A-Z0-9-]+\b/)?.[0];
  if (stableId) return stableId;
  const targetKey = title.split(":", 1)[0]?.trim();
  if (!targetKey) return null;
  try {
    return getValidationCase(targetKey).id;
  } catch {
    return null;
  }
}
