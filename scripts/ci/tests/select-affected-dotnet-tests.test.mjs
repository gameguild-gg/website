import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

import { selectAffectedDotnetTestNames } from "../select-affected-dotnet-tests.mjs";

const availableProjects = [
  "GameGuild.API.UnitTests",
  "GameGuild.Projects.UnitTests",
  "GameGuild.SharedKernel.UnitTests",
  "GameGuild.TestingLab.UnitTests",
];

for (const configuration of ["Debug", "Release"]) {
  test(`solution C# projects build under the requested ${configuration} configuration`, () => {
    const solution = readFileSync(
      new URL("../../../apps/api/GameGuild.sln", import.meta.url),
      "utf8",
    );
    const projects = [
      ...solution.matchAll(
        /Project\("\{[^}]+\}"\) = "([^"]+)", "([^"]+\.csproj)", "(\{[^}]+\})"/g,
      ),
    ];
    assert.ok(projects.length > 0, "The solution must contain C# projects");
    const configurationLines = new Set(
      solution.split(/\r?\n/).map((line) => line.trim()),
    );
    for (const [, name, , guid] of projects) {
      for (const mapping of ["ActiveCfg", "Build.0"]) {
        const expected = `${guid}.${configuration}|Any CPU.${mapping} = ${configuration}|Any CPU`;
        assert.ok(
          configurationLines.has(expected),
          `${name}: missing ${expected}`,
        );
      }
    }
  });
}

test("selects the test project matching a changed API module", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      ["apps/api/Source/Modules/GameGuild.TestingLab/TestingEvent.cs"],
      availableProjects,
    ),
    ["GameGuild.API.UnitTests", "GameGuild.SharedKernel.UnitTests", "GameGuild.TestingLab.UnitTests"],
  );
});

test("selects multiple module tests without duplicates", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      [
        "apps/api/Source/Modules/GameGuild.Projects/Project.cs",
        "apps/api/Source/Modules/GameGuild.Projects/ProjectVersion.cs",
        "apps/api/Source/Modules/GameGuild.TestingLab/TestingEvent.cs",
      ],
      availableProjects,
    ),
    ["GameGuild.API.UnitTests", "GameGuild.Projects.UnitTests", "GameGuild.SharedKernel.UnitTests", "GameGuild.TestingLab.UnitTests"],
  );
});

test("falls back to core tests for API infrastructure changes", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      ["apps/api/Source/GameGuild.API/Program.cs"],
      availableProjects,
    ),
    ["GameGuild.API.UnitTests", "GameGuild.SharedKernel.UnitTests"],
  );
});

test("test-only API changes retain the matching project and core security coverage", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      ["apps/api/tests/GameGuild.Projects.UnitTests/ProjectTests.cs"],
      availableProjects,
    ),
    ["GameGuild.API.UnitTests", "GameGuild.Projects.UnitTests", "GameGuild.SharedKernel.UnitTests"],
  );
});

test("selects the matching integration test project for a changed test", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      [
        "apps/api/tests/GameGuild.Identity.Authentication.IntegrationTests/RefreshTokenPostgreSqlFlowTests.cs",
      ],
      [
        ...availableProjects,
        "GameGuild.Identity.Authentication.IntegrationTests",
      ],
    ),
    ["GameGuild.API.UnitTests", "GameGuild.Identity.Authentication.IntegrationTests", "GameGuild.SharedKernel.UnitTests"],
  );
});

test("never selects benchmark harnesses for the affected API test run", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      [
        "apps/api/tests/GameGuild.Identity.Authorization.PerformanceTests/PermissionBulkCheckBenchmarks.cs",
        "apps/api/tests/GameGuild.Commerce.Billing.PerformanceTests/Program.cs",
      ],
      [
        ...availableProjects,
        "GameGuild.Identity.Authorization.PerformanceTests",
        "GameGuild.Commerce.Billing.PerformanceTests",
      ],
    ),
    [],
  );
});


for (const module of ["Authentication", "Authorization", "Context"]) {
  test("identity " + module + " changes select the mandatory identity and host security suites", () => {
    assert.deepEqual(
      selectAffectedDotnetTestNames(
        ["apps/api/Source/Modules/GameGuild.Identity." + module + "/Services/Example.cs"],
        [
          ...availableProjects,
          "GameGuild.Identity.Authentication.UnitTests",
          "GameGuild.Identity.Authorization.UnitTests",
        ],
      ),
      [
        "GameGuild.API.UnitTests",
        "GameGuild.Identity.Authentication.UnitTests",
        "GameGuild.Identity.Authorization.UnitTests",
        "GameGuild.SharedKernel.UnitTests",
      ],
    );
  });
}

test("mandatory identity security selection deduplicates multiple source changes", () => {
  assert.deepEqual(
    selectAffectedDotnetTestNames(
      [
        "apps/api/Source/Modules/GameGuild.Identity.Authentication/Services/SessionManagementService.cs",
        "apps/api/Source/Modules/GameGuild.Identity.Authorization/Services/PermissionAuditService.cs",
        "apps/api/tests/GameGuild.Identity.Authentication.UnitTests/Services/SessionManagementServiceTests.cs",
      ],
      [
        ...availableProjects,
        "GameGuild.Identity.Authentication.UnitTests",
        "GameGuild.Identity.Authorization.UnitTests",
      ],
    ),
    [
      "GameGuild.API.UnitTests",
      "GameGuild.Identity.Authentication.UnitTests",
      "GameGuild.Identity.Authorization.UnitTests",
      "GameGuild.SharedKernel.UnitTests",
    ],
  );
});

for (const filePath of [
  "apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Code/CodeAssessmentTypeAdapter.cs",
  "apps/api/tests/GameGuild.Learning.Assessments.UnitTests/CodeGradingAdapterContractTests.cs",
  "apps/api/Source/GameGuild.API/Program.cs",
  "apps/api/Source/Modules/GameGuild.Future/Example.cs",
]) {
  test("API changes retain the mandatory security suites: " + filePath, () => {
    const selected = selectAffectedDotnetTestNames([filePath], [
      ...availableProjects,
      "GameGuild.Identity.Authentication.UnitTests",
      "GameGuild.Identity.Authorization.UnitTests",
      "GameGuild.Learning.Assessments.UnitTests",
    ]);
    for (const required of [
      "GameGuild.API.UnitTests",
      "GameGuild.Identity.Authentication.UnitTests",
      "GameGuild.Identity.Authorization.UnitTests",
      "GameGuild.SharedKernel.UnitTests",
    ]) assert.ok(selected.includes(required), required);
    assert.equal(selected.includes("GameGuild.Learning.Assessments.UnitTests"), filePath.includes("GameGuild.Learning.Assessments"));
  });
}

test("non-API changes do not select API security projects", () => {
  assert.deepEqual(selectAffectedDotnetTestNames([
    "apps/web/src/page.tsx",
    "tools/emception/grading/toolchain-binding.mjs",
    "docs/architecture/example.md",
  ], availableProjects), []);
});

test("core security selection only includes available projects", () => {
  assert.deepEqual(selectAffectedDotnetTestNames([
    "apps/api/Source/Modules/GameGuild.Learning.Assessments/Example.cs",
  ], ["GameGuild.Learning.Assessments.UnitTests"]), ["GameGuild.Learning.Assessments.UnitTests"]);
});
