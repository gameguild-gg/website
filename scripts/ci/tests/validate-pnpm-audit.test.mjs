import assert from "node:assert/strict";
import { test } from "node:test";
import { validateAuditReport } from "../validate-pnpm-audit.mjs";

const report = () => ({
  advisories: {},
  metadata: {
    vulnerabilities: { info: 0, low: 0, moderate: 0, high: 0, critical: 0 },
  },
});
const patchedReport = () => {
  const result = report();
  result.metadata.vulnerabilities.high = 1;
  result.advisories["1240992"] = {
    module_name: "braces",
    github_advisory_id: "GHSA-vfj7-8cjw-p6xm",
    url: "https://github.com/advisories/GHSA-vfj7-8cjw-p6xm",
    cves: ["CVE-2026-93687"],
    findings: [{ version: "3.0.3" }],
  };
  return result;
};

test("clean audit reports pass", () => {
  assert.deepEqual(validateAuditReport(report(), 0), {
    advisories: 0,
    verifiedLocalPatches: 0,
  });
});
test("only the exact advisory with verified installed patches passes", () => {
  assert.deepEqual(validateAuditReport(patchedReport(), 1, true), {
    advisories: 1,
    verifiedLocalPatches: 1,
  });
  assert.throws(() => validateAuditReport(patchedReport(), 1), /Unmitigated/);
});
test("missing metadata, registry errors, and inconsistent exit codes fail", () => {
  for (const invalid of [
    {},
    { error: { code: "ENETUNREACH" } },
    { advisories: {}, metadata: {} },
  ]) {
    assert.throws(
      () => validateAuditReport(invalid, 1, true),
      /complete advisory report/,
    );
  }
  for (const exitCode of [1, 2, NaN])
    assert.throws(
      () => validateAuditReport(report(), exitCode, true),
      /exit code/,
    );
  assert.throws(
    () => validateAuditReport(patchedReport(), 0, true),
    /exit code/,
  );
});
test("filtered or malformed reports cannot hide an advisory", () => {
  for (const advisories of [[], "not an object", 42]) {
    assert.throws(
      () => validateAuditReport({ ...report(), advisories }, 0, true),
      /complete advisory report/,
    );
  }
  const filtered = report();
  filtered.metadata.vulnerabilities.high = 1;
  assert.throws(() => validateAuditReport(filtered, 1, true), /disagree/);
  for (const count of [undefined, -1, 0.5, "1"]) {
    const invalid = report();
    invalid.metadata.vulnerabilities.high = count;
    assert.throws(
      () => validateAuditReport(invalid, 1, true),
      /Invalid.*count/,
    );
  }
});
test("different packages, advisories, CVEs, and unpatched versions fail", () => {
  for (const mutation of [
    { module_name: "another-package" },
    { github_advisory_id: "GHSA-another-advisory" },
    { url: "https://github.com/advisories/GHSA-another-advisory" },
    { cves: ["CVE-2026-93687", "CVE-another"] },
    { cves: [] },
    { findings: [{ version: "3.0.2" }] },
    { findings: [] },
    { findings: [{ version: "3.0.3" }, { version: "3.0.2" }] },
  ]) {
    const invalid = patchedReport();
    Object.assign(invalid.advisories["1240992"], mutation);
    assert.throws(() => validateAuditReport(invalid, 1, true), /Unmitigated/);
  }
});
test("an additional advisory fails even when the braces patch is verified", () => {
  const invalid = patchedReport();
  invalid.advisories.other = {
    module_name: "other",
    github_advisory_id: "GHSA-other",
  };
  invalid.metadata.vulnerabilities.low = 1;
  assert.throws(
    () => validateAuditReport(invalid, 1, true),
    /Unmitigated.*GHSA-other/,
  );
});

const patchedSprintfReport = () => {
  const result = report();
  result.metadata.vulnerabilities.moderate = 1;
  result.advisories["1241202"] = {
    module_name: "sprintf-js",
    github_advisory_id: "GHSA-hp3w-g68c-fv3c",
    url: "https://github.com/advisories/GHSA-hp3w-g68c-fv3c",
    cves: ["CVE-2026-97058"],
    findings: [{ version: "1.1.3" }],
  };
  return result;
};

test("sprintf advisory requires its own verified installed patch", () => {
  assert.deepEqual(validateAuditReport(patchedSprintfReport(), 1, false, true), {
    advisories: 1,
    verifiedLocalPatches: 1,
  });
  assert.throws(() => validateAuditReport(patchedSprintfReport(), 1, true), /Unmitigated/);
  assert.throws(() => validateAuditReport(patchedSprintfReport(), 0, false, true), /exit code/);
});

test("sprintf mitigation cannot accept another package, advisory, CVE or version", () => {
  for (const mutation of [
    { module_name: "another-package" },
    { github_advisory_id: "GHSA-another-advisory" },
    { url: "https://github.com/advisories/GHSA-another-advisory" },
    { cves: ["CVE-2026-97058", "CVE-another"] },
    { cves: [] },
    { findings: [{ version: "1.0.3" }] },
    { findings: [] },
    { findings: [{ version: "1.1.3" }, { version: "1.0.3" }] },
  ]) {
    const invalid = patchedSprintfReport();
    Object.assign(invalid.advisories["1241202"], mutation);
    assert.throws(() => validateAuditReport(invalid, 1, true, true), /Unmitigated/);
  }
});

test("both exact locally verified mitigations pass and an additional advisory still fails", () => {
  const combined = patchedReport();
  combined.metadata.vulnerabilities.moderate = 1;
  combined.advisories["1241202"] = patchedSprintfReport().advisories["1241202"];
  assert.deepEqual(validateAuditReport(combined, 1, true, true), {
    advisories: 2,
    verifiedLocalPatches: 2,
  });
  combined.advisories.other = { module_name: "other", github_advisory_id: "GHSA-other" };
  combined.metadata.vulnerabilities.low = 1;
  assert.throws(() => validateAuditReport(combined, 1, true, true), /Unmitigated.*GHSA-other/);
});

test("metadata counts every workspace finding of a verified advisory", () => {
  const multiple = patchedReport();
  multiple.advisories["1240992"].findings.push({ version: "3.0.3" });
  multiple.metadata.vulnerabilities.high = 2;
  assert.deepEqual(validateAuditReport(multiple, 1, true), {
    advisories: 1,
    verifiedLocalPatches: 1,
  });
  multiple.metadata.vulnerabilities.high = 1;
  assert.throws(() => validateAuditReport(multiple, 1, true), /disagree/);
});

test("multiple findings of both verified patches retain all metadata checks", () => {
  const multiple = patchedReport();
  multiple.advisories["1240992"].findings.push({ version: "3.0.3" });
  multiple.metadata.vulnerabilities.high = 2;
  multiple.advisories["1241202"] = patchedSprintfReport().advisories["1241202"];
  multiple.advisories["1241202"].findings.push({ version: "1.1.3" });
  multiple.metadata.vulnerabilities.moderate = 2;
  assert.deepEqual(validateAuditReport(multiple, 1, true, true), {
    advisories: 2,
    verifiedLocalPatches: 2,
  });
  multiple.metadata.vulnerabilities.moderate = 3;
  assert.throws(() => validateAuditReport(multiple, 1, true, true), /disagree/);
});

test("a new Next.js advisory fails for every workspace installation", () => {
  for (const github_advisory_id of [
    "GHSA-3w37-wq28-93x7",
    "GHSA-4jqv-mc3x-m676",
    "GHSA-39w2-rjm5-chcv",
    "GHSA-f87g-xv8r-7p7x",
    "GHSA-mcj8-r9mp-w47p",
    "GHSA-cjq9-62q9-8jv4",
  ]) {
    const invalid = patchedReport();
    invalid.advisories.next = {
      module_name: "next",
      github_advisory_id,
      findings: [
        { version: "16.3.6", paths: ["apps__web>next"] },
        { version: "16.3.6", paths: ["demos__emception-ide-next>next"] },
      ],
    };
    invalid.metadata.vulnerabilities.moderate = 2;
    assert.throws(
      () => validateAuditReport(invalid, 1, true, true),
      new RegExp(`Unmitigated.*${github_advisory_id}`),
    );
  }
});

test("each severity rejects non-integer and non-numeric vulnerability counts", () => {
  for (const level of ["info", "low", "moderate", "high", "critical"]) {
    for (const count of [null, false, true, NaN, Infinity, Number.MAX_SAFE_INTEGER + 1]) {
      const invalid = report();
      invalid.metadata.vulnerabilities[level] = count;
      assert.throws(() => validateAuditReport(invalid, 0, true, true), new RegExp("Invalid pnpm audit vulnerability count: " + level));
    }
  }
});

test("both mitigations reject malformed CVE and finding collections", () => {
  for (const factory of [patchedReport, patchedSprintfReport]) {
    for (const mutation of [
      { cves: undefined }, { cves: null }, { cves: "CVE-2026-93687" }, { cves: {} },
      { findings: undefined }, { findings: null }, { findings: {} }, { findings: [{}] },
    ]) {
      const invalid = factory();
      Object.assign(Object.values(invalid.advisories)[0], mutation);
      assert.throws(() => validateAuditReport(invalid, 1, true, true), /Unmitigated/);
    }
  }
});

test("a combined report requires independent verification of both installed patches", () => {
  const combined = patchedReport();
  combined.advisories["1241202"] = patchedSprintfReport().advisories["1241202"];
  combined.metadata.vulnerabilities.moderate = 1;
  for (const [bracesVerified, sprintfVerified, rejectedId] of [
    [false, false, "GHSA-vfj7-8cjw-p6xm"],
    [false, true, "GHSA-vfj7-8cjw-p6xm"],
    [true, false, "GHSA-hp3w-g68c-fv3c"],
  ]) {
    assert.throws(() => validateAuditReport(combined, 1, bracesVerified, sprintfVerified), new RegExp("Unmitigated.*" + rejectedId));
  }
  assert.deepEqual(validateAuditReport(combined, 1, true, true), { advisories: 2, verifiedLocalPatches: 2 });
});
