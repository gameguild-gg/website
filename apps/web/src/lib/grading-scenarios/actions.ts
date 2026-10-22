"use server";

import { getGradingScenarioLabAccess } from "./access.server";
import type {
  GradingScenarioActionResult,
  GradingScenarioSelection,
  PrepareGradingScenarioSelection,
} from "./contracts";
import {
  inspectGradingScenario,
  prepareGradingScenario,
  resetGradingScenario,
  verifyGradingScenario,
} from "./service.server";
import { revalidatePath } from "next/cache";

const LAB_PATH = "/workspace/learning/grading-validation";

function requiredText(value: unknown, label: string): string {
  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`${label} is required.`);
  }
  return value.trim();
}

function selection(value: GradingScenarioSelection): GradingScenarioSelection {
  return {
    adapterKey: requiredText(value?.adapterKey, "Adapter"),
    scenarioKey: requiredText(value?.scenarioKey, "Scenario"),
  };
}

async function accessContext() {
  const access = await getGradingScenarioLabAccess();
  if (!access.allowed || !access.webBaseUrl) {
    throw new Error(
      "The grading validation lab is unavailable for this request.",
    );
  }
  return { webBaseUrl: access.webBaseUrl };
}

function failure(error: unknown): GradingScenarioActionResult {
  return {
    success: false,
    error:
      error instanceof Error
        ? error.message
        : "The grading scenario operation failed.",
  };
}

export async function prepareGradingScenarioAction(
  value: PrepareGradingScenarioSelection,
): Promise<GradingScenarioActionResult> {
  try {
    const access = await accessContext();
    const selected = selection(value);
    const checkpoint = requiredText(value?.checkpoint, "Checkpoint");
    const targetKeys = Array.isArray(value?.targetKeys)
      ? [...new Set(value.targetKeys.map((key) => requiredText(key, "Target")))]
      : [];
    if (targetKeys.length === 0)
      throw new Error("Select at least one scenario target.");

    const progress: string[] = [];
    const result = await prepareGradingScenario(selected.adapterKey, {
      scenarioKey: selected.scenarioKey,
      targetKeys,
      checkpoint,
      fresh: value.fresh === true,
      apiBaseUrl:
        process.env.API_URL ||
        process.env.NEXT_PUBLIC_API_URL ||
        "http://localhost:8080",
      webBaseUrl: access.webBaseUrl,
      onProgress: (message) => progress.push(message),
    });
    revalidatePath(LAB_PATH);
    return {
      success: true,
      message: `Scenario prepared through ${checkpoint}.`,
      state: result.state,
      progress: result.progress.length > 0 ? result.progress : progress,
    };
  } catch (error) {
    return failure(error);
  }
}

export async function verifyGradingScenarioAction(
  value: GradingScenarioSelection,
): Promise<GradingScenarioActionResult> {
  try {
    await accessContext();
    const selected = selection(value);
    const result = await verifyGradingScenario(
      selected.adapterKey,
      selected.scenarioKey,
    );
    revalidatePath(LAB_PATH);
    return {
      success: true,
      message:
        result.state.health === "ready"
          ? "Scenario verification passed."
          : "Scenario verification completed with failures.",
      state: result.state,
      progress: result.progress,
    };
  } catch (error) {
    return failure(error);
  }
}

export async function inspectGradingScenarioAction(
  value: GradingScenarioSelection,
): Promise<GradingScenarioActionResult> {
  try {
    await accessContext();
    const selected = selection(value);
    const state = await inspectGradingScenario(
      selected.adapterKey,
      selected.scenarioKey,
    );
    revalidatePath(LAB_PATH);
    return {
      success: true,
      message: "Scenario state refreshed.",
      state,
      progress: [],
    };
  } catch (error) {
    return failure(error);
  }
}

export async function resetGradingScenarioAction(
  value: GradingScenarioSelection,
): Promise<GradingScenarioActionResult> {
  try {
    await accessContext();
    const selected = selection(value);
    const result = await resetGradingScenario(
      selected.adapterKey,
      selected.scenarioKey,
    );
    if (result.failures.length > 0) {
      throw new Error(`Scenario reset failed: ${result.failures.join("; ")}`);
    }
    const state = await inspectGradingScenario(
      selected.adapterKey,
      selected.scenarioKey,
    );
    revalidatePath(LAB_PATH);
    return {
      success: true,
      message:
        result.removed.length > 0
          ? `Removed ${result.removed.length} owned resources.`
          : "No owned resources required removal.",
      state,
      progress: [...result.removed, ...result.skipped],
    };
  } catch (error) {
    return failure(error);
  }
}
