export interface QuizTargetDefinition {
  key: string;
  title: string;
  subject: string;
  reviewMethods: string[];
  officialCapability?: string;
  validationCaseId?: string;
  maxSupportedCheckpoint?: string;
}

export interface QuizDefinition {
  key: string;
  title: string;
  subject: string;
  quizzes: QuizTargetDefinition[];
}

export interface QuizPersona {
  email: string;
  username: string;
  password: string;
  userId?: string | null;
}

export interface QuizManifest {
  definitionKey: string;
  quizKeys: string[];
  runId: string;
  marker: string;
  checkpoint: string;
  updatedAt?: string;
  personas: Record<string, QuizPersona>;
  resources: {
    quizzes: Record<string, { checkpoint: string }>;
  };
  urls: Record<string, unknown>;
}

export interface QuizOperationResult {
  manifest: QuizManifest;
}

export interface QuizResetReport {
  scenario: string;
  removed?: string[];
  skipped?: string[];
  failures?: string[];
}

export interface QuizScenarioReport {
  healthy?: boolean;
  confirmedCheckpoint?: string;
  nextCheckpoint?: string | null;
  checkpoints?: Record<string, { declared?: string; confirmed?: string }>;
  checks?: Array<{ name?: string; ok?: boolean; detail?: string }>;
}

export interface QuizValidationCase {
  id: string;
  key?: string;
  quizKey?: string;
  suite: string;
  capability: string;
  automation: string;
  requirement: string;
  baseline: {
    status: string;
    owner: string;
    reason?: string | null;
    tracking?: string | null;
  };
}
