import { QUESTION_TYPE_CASES } from "../../../../apps/web/src/lib/grading-scenarios/adapters/quiz/capability-catalog.mjs";

const DRIVERS = Object.freeze({
  "single-choice": answerChoice,
  "multiple-choice": answerChoice,
  "true-false": answerTrueFalse,
  "fill-text": answerFillBlank,
  "fill-number": answerFillBlank,
  "fill-dropdown": answerFillBlank,
  "fill-word-bank": answerFillBlank,
  "short-answer": answerShortAnswer,
  essay: answerEssay,
  matching: answerMatching,
  ordering: answerOrdering,
  categorization: answerCategorization,
  rating: answerRating,
  numeric: answerNumeric,
  formula: answerFormula,
  hotspot: answerHotspot,
  highlight: answerHighlight,
});

export const QUESTION_TYPE_DRIVER_KEYS = Object.freeze(Object.keys(DRIVERS));

export function validateQuestionTypeDrivers(cases = QUESTION_TYPE_CASES) {
  const missing = cases.filter(
    (candidate) => typeof DRIVERS[candidate.browser?.driver] !== "function",
  );
  if (missing.length > 0) {
    throw new Error(
      `Question cases without browser drivers: ${missing
        .map(
          (candidate) =>
            `${candidate.id} (${candidate.browser?.driver ?? "unset"})`,
        )
        .join(", ")}.`,
    );
  }
  return cases;
}

export function getQuestionTypeDriver(validationCase) {
  const key = validationCase.browser?.driver;
  const driver = DRIVERS[key];
  if (!driver) {
    throw new Error(
      `Validation case ${validationCase.id} has no browser driver for "${key ?? "unset"}".`,
    );
  }
  return driver;
}

export async function answerQuestionTypeCase(
  page,
  validationCase,
  answerKind = "correct",
) {
  const answer = validationCase.fixture.answers?.[answerKind];
  if (!answer) {
    throw new Error(
      `Validation case ${validationCase.id} has no ${answerKind} browser answer.`,
    );
  }
  await getQuestionTypeDriver(validationCase)(page, {
    answer,
    entry: validationCase.fixture.entry,
    validationCase,
  });
}

async function answerChoice(page, { answer, entry }) {
  const selectedIds = answer.optionIds ?? [answer.optionId];
  for (const optionId of selectedIds) {
    const option = entry.options.find((candidate) => candidate.id === optionId);
    if (!option) throw new Error(`Unknown choice option "${optionId}".`);
    await page.getByRole("button", { name: option.text, exact: true }).click();
  }
}

async function answerTrueFalse(page, { answer }) {
  await page
    .getByRole("button", { name: answer.value ? "True" : "False", exact: true })
    .click();
}

async function answerFillBlank(page, { answer, entry }) {
  for (const blank of entry.blanks) {
    const value = answer.values[blank.id];
    if (value == null) continue;
    if (blank.input.type === "DROPDOWN") {
      await page.getByRole("combobox").click();
      await page.getByRole("option", { name: value, exact: true }).click();
      continue;
    }
    if (blank.input.type === "WORDBANK") {
      const source = page
        .locator('[data-testid^="word-bank-word-"]')
        .filter({ hasText: new RegExp(`^${escapeRegExp(value)}$`) })
        .first();
      const target = page.getByTestId(`word-bank-blank-${blank.id}`);
      await dragHtml5(page, source, target);
      await target.getByText(value, { exact: true }).waitFor({
        state: "visible",
      });
      continue;
    }
    const placeholder = blank.input.type === "TEXT" ? "..." : undefined;
    const input = placeholder
      ? page.getByPlaceholder(placeholder).first()
      : page.locator('input:not([type="hidden"])').last();
    await input.fill(value);
  }
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function dragHtml5(page, source, target) {
  const dataTransfer = await page.evaluateHandle(() => new DataTransfer());
  try {
    await source.dispatchEvent("dragstart", { dataTransfer });
    await target.dispatchEvent("dragenter", { dataTransfer });
    await target.dispatchEvent("dragover", { dataTransfer });
    await target.dispatchEvent("drop", { dataTransfer });
  } finally {
    await dataTransfer.dispose();
  }
}

async function answerShortAnswer(page, { answer }) {
  await page.getByPlaceholder("Type your answer...").fill(answer.value);
}

async function answerEssay(page, { answer }) {
  const editor = page
    .locator('.essay-lexical-editor [contenteditable="true"]')
    .first();
  await editor.fill(answer.plainText);
}

async function answerMatching(page, { answer, entry }) {
  for (const pair of entry.pairs) {
    const right = answer.matches[pair.id];
    if (!right) continue;
    await page.getByText(pair.left, { exact: true }).last().click();
    await page.getByText(right, { exact: true }).last().click();
  }
}

async function answerOrdering(page, { answer, entry }) {
  const textById = new Map(entry.items.map((item) => [item.id, item.text]));
  for (
    let targetIndex = 0;
    targetIndex < answer.itemIds.length;
    targetIndex += 1
  ) {
    const text = textById.get(answer.itemIds[targetIndex]);
    if (!text)
      throw new Error(
        `Unknown ordering item "${answer.itemIds[targetIndex]}".`,
      );
    const itemId = answer.itemIds[targetIndex];
    const row = page.getByTestId(`ordering-item-${itemId}`);
    let positioned = false;
    for (let guard = 0; guard < entry.items.length * 2; guard += 1) {
      const visibleIds = await page
        .locator('[data-testid^="ordering-item-"]')
        .evaluateAll((nodes) =>
          nodes.map((node) =>
            node.getAttribute("data-testid")?.replace("ordering-item-", ""),
          ),
        );
      const currentIndex = visibleIds.indexOf(itemId);
      if (currentIndex === targetIndex) {
        positioned = true;
        break;
      }
      if (currentIndex < 0) {
        throw new Error(`Ordering item "${itemId}" is not rendered.`);
      }
      await row
        .getByRole("button", {
          name: `Move ${text} ${currentIndex > targetIndex ? "up" : "down"}`,
          exact: true,
        })
        .click();
    }
    if (!positioned) {
      throw new Error(
        `Ordering item "${itemId}" did not reach index ${targetIndex}.`,
      );
    }
  }
}

async function answerCategorization(page, { answer, entry }) {
  const categoryById = new Map(
    entry.categories.map((category) => [category.id, category.name]),
  );
  for (const item of entry.items) {
    for (const categoryId of answer.categoryIdsByItem[item.id] ?? []) {
      const categoryName = categoryById.get(categoryId);
      if (!categoryName) throw new Error(`Unknown category "${categoryId}".`);
      const source = page.getByTestId(`categorization-item-${item.id}`);
      const target = page.getByTestId(`categorization-drop-${categoryId}`);
      await dragHtml5(page, source, target);
      await source.waitFor({ state: "hidden" });
      await target.getByText(item.text, { exact: true }).waitFor({
        state: "visible",
      });
    }
  }
}

async function answerRating(page, { answer }) {
  await page
    .getByRole("button", { name: String(answer.value), exact: true })
    .click();
}

async function answerNumeric(page, { answer }) {
  await page
    .getByPlaceholder("Type your computed result here...")
    .fill(answer.value);
}

async function answerFormula(page, { answer }) {
  const mathField = page.locator("math-field").first();
  await mathField.waitFor({ state: "visible" });
  await mathField.evaluate((element, value) => {
    element.value = value;
    element.dispatchEvent(new Event("input", { bubbles: true }));
  }, answer.expression);
}

async function answerHotspot(_page, { validationCase }) {
  throw new Error(
    `${validationCase.id} is ${validationCase.browser.status}: ${validationCase.browser.reason}`,
  );
}

async function answerHighlight(page, { answer, entry }) {
  for (const span of answer.spans) {
    const text = entry.plainText.slice(span.start, span.end);
    await page.getByText(text, { exact: true }).last().click();
  }
}

validateQuestionTypeDrivers();
