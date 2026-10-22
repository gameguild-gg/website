import {
  assertApplicableCheckpoint,
  getScenarioDefinition,
  selectQuizDefinitions,
} from "./definitions.mjs";
import {
  readManifest,
  withScenarioLock,
  writeReport,
} from "./manifest.server.mjs";
import { writeCandidateValidationReport } from "./validation.server.mjs";

/**
 * @param {{
 *   scenarioKey: string;
 *   checkpoint: string;
 *   quizKeys?: string[];
 *   fresh?: boolean;
 *   apiBaseUrl: string;
 *   webBaseUrl: string;
 *   onProgress?: (message: string) => void;
 *   onStaleLock?: (owner: { pid: number; acquiredAt?: string }) => void;
 * }} input
 */
export async function prepareQuizGradingScenario({
  scenarioKey,
  checkpoint,
  quizKeys = [],
  fresh = false,
  apiBaseUrl,
  webBaseUrl,
  onProgress = () => {},
  onStaleLock = () => {},
}) {
  const definition = getScenarioDefinition(scenarioKey);
  const quizzes = selectQuizDefinitions(definition, quizKeys);
  assertApplicableCheckpoint(definition, checkpoint, quizzes);

  const { resetScenario } = await import("./cleanup.server.mjs");
  const { prepareScenario } = await import("./provision.server.mjs");
  const { verifyScenario } = await import("./verify.server.mjs");

  return withScenarioLock(
    scenarioKey,
    async () => {
      if (fresh) {
        onProgress(`Resetting the owned execution for "${scenarioKey}".`);
        await resetScenario(scenarioKey);
      }

      onProgress(`Preparing "${scenarioKey}" through ${checkpoint}.`);
      const manifest = await prepareScenario({
        scenarioKey,
        checkpoint,
        quizKeys,
        apiBaseUrl,
        webBaseUrl,
        onProgress,
      });

      onProgress("Verifying the persisted scenario state.");
      const { report } = await verifyScenario(scenarioKey);
      await writeReport(scenarioKey, report);
      const validationArtifacts = await writeCandidateValidationReport(
        manifest,
        report,
      );

      return { manifest, report, validationArtifacts };
    },
    { onStaleLock },
  );
}

/**
 * @param {{
 *   scenarioKey: string;
 *   strict?: boolean;
 *   completedAutomations?: string[];
 *   results?: Array<{ caseId: string; status: string; evidence?: string[] }>;
 *   onStaleLock?: (owner: { pid: number; acquiredAt?: string }) => void;
 * }} input
 */
export async function verifyQuizGradingScenario({
  scenarioKey,
  strict = true,
  completedAutomations = ["api"],
  results = [],
  onStaleLock = () => {},
}) {
  const { verifyScenario } = await import("./verify.server.mjs");
  return withScenarioLock(
    scenarioKey,
    async () => {
      const { manifest, report } = await verifyScenario(scenarioKey, {
        strict,
      });
      await writeReport(scenarioKey, report);
      const validationArtifacts = await writeCandidateValidationReport(
        manifest,
        report,
        { completedAutomations, results },
      );
      return { manifest, report, validationArtifacts };
    },
    { onStaleLock },
  );
}

/**
 * @param {{
 *   scenarioKey: string;
 *   onStaleLock?: (owner: { pid: number; acquiredAt?: string }) => void;
 * }} input
 */
export async function resetQuizGradingScenario({
  scenarioKey,
  onStaleLock = () => {},
}) {
  getScenarioDefinition(scenarioKey);
  const { resetScenario } = await import("./cleanup.server.mjs");
  return withScenarioLock(scenarioKey, () => resetScenario(scenarioKey), {
    onStaleLock,
  });
}

export async function inspectQuizGradingScenario(scenarioKey) {
  getScenarioDefinition(scenarioKey);
  return readManifest(scenarioKey, { optional: true });
}
