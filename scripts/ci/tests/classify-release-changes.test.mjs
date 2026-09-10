import assert from "node:assert/strict";
import test from "node:test";

import { classifyReleaseChanges } from "../classify-release-changes.mjs";

const expectedEmptyClassification = {
  api: false,
  web: false,
  learning: false,
  apiRuntimeChanged: false,
  webRuntimeChanged: false,
  learningRuntimeChanged: false,
  testingLab: false,
  economyCritical: false,
  openApi: false,
  migration: false,
  runtimeChanged: false,
};

test("documentation and CI changes do not deploy runtime services", () => {
  assert.deepEqual(
    classifyReleaseChanges([
      "docs/deployment-smoke.md",
      ".github/workflows/pr-verify.yml",
      "scripts/ci/classify-release-changes.mjs",
    ]),
    expectedEmptyClassification,
  );
});

test("a Web component change deploys only Web", () => {
  assert.deepEqual(
    classifyReleaseChanges(["apps/web/src/components/ui/button.tsx"]),
    {
      ...expectedEmptyClassification,
      web: true,
      webRuntimeChanged: true,
      runtimeChanged: true,
    },
  );
});

test("Testing Lab Web and API changes select focused gates and both services", () => {
  assert.deepEqual(
    classifyReleaseChanges([
      "apps/web/src/components/testing-lab/testing-event-management.tsx",
      "apps/api/Source/Modules/GameGuild.TestingLab/TestingEvent.cs",
    ]),
    {
      ...expectedEmptyClassification,
      api: true,
      web: true,
      apiRuntimeChanged: true,
      webRuntimeChanged: true,
      testingLab: true,
      openApi: true,
      runtimeChanged: true,
    },
  );
});

test("an API contract change requires OpenAPI verification", () => {
  assert.deepEqual(
    classifyReleaseChanges([
      "apps/api/Source/Modules/GameGuild.Projects/Controllers/ProjectsController.cs",
    ]),
    {
      ...expectedEmptyClassification,
      api: true,
      apiRuntimeChanged: true,
      openApi: true,
      runtimeChanged: true,
    },
  );
});

test("an EF migration selects API, migration, and OpenAPI gates", () => {
  assert.deepEqual(
    classifyReleaseChanges([
      "apps/api/Source/Infrastructure/Persistence/Migrations/20260831_AddReleaseIdentity.cs",
    ]),
    {
      ...expectedEmptyClassification,
      api: true,
      apiRuntimeChanged: true,
      openApi: true,
      migration: true,
      runtimeChanged: true,
    },
  );
});

test("a shared UI package deploys each JavaScript consumer", () => {
  assert.deepEqual(
    classifyReleaseChanges(["packages/infrastructure/ui/src/components/button.tsx"]),
    {
      ...expectedEmptyClassification,
      web: true,
      learning: true,
      webRuntimeChanged: true,
      learningRuntimeChanged: true,
      runtimeChanged: true,
    },
  );
});

test("Economy changes always require the complete Economy release gate", () => {
  assert.deepEqual(
    classifyReleaseChanges([
      "apps/api/Source/Modules/GameGuild.Finance.Economy/Wallets/Wallet.cs",
    ]),
    {
      ...expectedEmptyClassification,
      api: true,
      apiRuntimeChanged: true,
      economyCritical: true,
      openApi: true,
      runtimeChanged: true,
    },
  );
});

test("a root dependency lock change rebuilds Node runtimes without selecting Economy", () => {
  assert.deepEqual(classifyReleaseChanges(["pnpm-lock.yaml"]), {
    ...expectedEmptyClassification,
    web: true,
    learning: true,
    webRuntimeChanged: true,
    learningRuntimeChanged: true,
    runtimeChanged: true,
  });
});

test("unknown runtime configuration changes fail conservatively", () => {
  assert.deepEqual(classifyReleaseChanges(["deploy/runtime-policy.yaml"]), {
    ...expectedEmptyClassification,
    api: true,
    web: true,
    learning: true,
    apiRuntimeChanged: true,
    webRuntimeChanged: true,
    learningRuntimeChanged: true,
    runtimeChanged: true,
  });
});

test("test-only changes select verification without rebuilding a runtime image", () => {
  assert.deepEqual(
    classifyReleaseChanges(["apps/web/src/components/ui/button.test.tsx"]),
    {
      ...expectedEmptyClassification,
      web: true,
    },
  );
});
