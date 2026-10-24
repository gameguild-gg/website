import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
import { integrationTestFilter } from "../api-integration-filter.mjs";

const groups = JSON.parse(readFileSync(new URL("../economy-integration-shards.json", import.meta.url), "utf8"));
const shards = ["api-integration-1", "api-integration-2", "api-integration-3", "api-integration-4"];

// Evaluate VSTest's substring terms independently of the filter generator.
function selected(filter, name) {
  return filter.split("|").some((alternative) => alternative.split("&").every((term) => {
    const match = /^FullyQualifiedName(!?~)(.+)$/u.exec(term);
    assert.ok(match, "Unexpected test-filter grammar");
    return match[1] === "~" ? name.includes(match[2]) : !name.includes(match[2]);
  }));
}

test("every configured class belongs to exactly its declared shard", () => {
  for (const [shard, names] of Object.entries(groups)) {
    for (const name of names) {
      assert.deepEqual(shards.filter((key) => selected(integrationTestFilter(groups, key), name + ".Case")), [shard]);
    }
  }
});

test("new classes and longer class names are automatically in the complement", () => {
  for (const name of ["GameGuild.API.IntegrationTests.FutureRegression.Case", ...Object.values(groups).flat().map((name) => name + "Extended.Case")]) {
    assert.deepEqual(shards.filter((key) => selected(integrationTestFilter(groups, key), name)), ["api-integration-4"]);
  }
});

test("overlapping groups are rejected before any test process starts", () => {
  const copy = structuredClone(groups);
  copy["api-integration-2"].push(copy["api-integration-1"][0]);
  assert.throws(() => integrationTestFilter(copy, "api-integration-4"), /disjoint/u);
});

test("missing, empty and unknown groups cannot silently drop tests", () => {
  for (const mutate of [
    (copy) => { delete copy["api-integration-1"]; },
    (copy) => { copy["api-integration-1"] = []; },
    (copy) => { copy["api-integration-5"] = ["GameGuild.API.IntegrationTests.Other"]; },
  ]) {
    const copy = structuredClone(groups);
    mutate(copy);
    assert.throws(() => integrationTestFilter(copy, "api-integration-4"), TypeError);
  }
  assert.throws(() => integrationTestFilter(groups, "api-integration-5"), /Unknown/u);
});

test("class names cannot introduce extra filter operators", () => {
  const copy = structuredClone(groups);
  copy["api-integration-1"].push("GameGuild.API.IntegrationTests.Other|FullyQualifiedName~Anything");
  assert.throws(() => integrationTestFilter(copy, "api-integration-1"), /valid/u);
});
