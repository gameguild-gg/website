export type GradingScenarioHealth = "not-prepared" | "ready" | "unhealthy";

export interface GradingScenarioTargetSummary {
  key: string;
  title: string;
  kind: string;
  workflow: string[];
  capability: string | null;
  validationCaseId: string | null;
  maxCheckpoint: string;
}

export interface GradingScenarioCapabilitySummary {
  id: string;
  suite: string;
  capability: string;
  automation: string;
  requirement: string;
  status: string;
  owner: string;
  reason: string | null;
  tracking: string | null;
  targetKey: string | null;
}

export interface GradingScenarioDefinitionSummary {
  key: string;
  title: string;
  subject: string;
  targets: GradingScenarioTargetSummary[];
}

export interface GradingScenarioAdapterSummary {
  key: string;
  title: string;
  targetLabel: string;
  targetLabelPlural: string;
  defaultScenarioKey: string;
  checkpoints: string[];
  scenarios: GradingScenarioDefinitionSummary[];
  capabilityCatalog: GradingScenarioCapabilitySummary[];
}

export interface GradingScenarioPersonaSummary {
  key: string;
  label: string;
  email: string;
  username: string;
  password: string;
  userId: string | null;
}

export interface GradingScenarioLink {
  key: string;
  label: string;
  href: string;
  audience: string;
  targetKey: string | null;
}

export interface GradingScenarioCheck {
  name: string;
  ok: boolean;
  detail: string | null;
}

export interface GradingScenarioTargetState {
  key: string;
  title: string;
  declaredCheckpoint: string;
  confirmedCheckpoint: string | null;
}

export interface GradingScenarioState {
  adapterKey: string;
  scenarioKey: string;
  health: GradingScenarioHealth;
  prepared: boolean;
  runId: string | null;
  marker: string | null;
  declaredCheckpoint: string | null;
  confirmedCheckpoint: string | null;
  nextCheckpoint: string | null;
  updatedAt: string | null;
  diagnostic: string | null;
  targets: GradingScenarioTargetState[];
  personas: GradingScenarioPersonaSummary[];
  links: GradingScenarioLink[];
  checks: GradingScenarioCheck[];
}

export interface GradingScenarioLabModel {
  adapters: GradingScenarioAdapterSummary[];
  states: GradingScenarioState[];
}

export interface PrepareGradingScenarioInput {
  scenarioKey: string;
  targetKeys: string[];
  checkpoint: string;
  fresh: boolean;
  apiBaseUrl: string;
  webBaseUrl: string;
  onProgress?: (message: string) => void;
}

export interface GradingScenarioSelection {
  adapterKey: string;
  scenarioKey: string;
}

export interface PrepareGradingScenarioSelection extends GradingScenarioSelection {
  targetKeys: string[];
  checkpoint: string;
  fresh: boolean;
}

export interface GradingScenarioOperationResult {
  state: GradingScenarioState;
  progress: string[];
}

export interface GradingScenarioResetResult {
  scenario: string;
  removed: string[];
  skipped: string[];
  failures: string[];
}

export interface GradingScenarioAdapter {
  summary(): GradingScenarioAdapterSummary;
  inspect(scenarioKey: string): Promise<GradingScenarioState>;
  prepare(
    input: PrepareGradingScenarioInput,
  ): Promise<GradingScenarioOperationResult>;
  verify(scenarioKey: string): Promise<GradingScenarioOperationResult>;
  reset(scenarioKey: string): Promise<GradingScenarioResetResult>;
}

export type GradingScenarioActionResult =
  | {
      success: true;
      message: string;
      state: GradingScenarioState;
      progress: string[];
    }
  | { success: false; error: string };
