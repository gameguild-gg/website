"use client";

import * as React from "react";
import { useCallback } from "react";
import { $patchStyleText } from "@lexical/selection";
import {
  $addUpdateTag,
  $getSelection,
  $isNodeSelection,
  ElementFormatType,
  FORMAT_ELEMENT_COMMAND,
  FORMAT_TEXT_COMMAND,
  INDENT_CONTENT_COMMAND,
  LexicalEditor,
  OUTDENT_CONTENT_COMMAND,
  SKIP_SELECTION_FOCUS_TAG,
  TextFormatType,
} from "lexical";
import {
  AlignCenterIcon,
  AlignJustifyIcon,
  AlignLeftIcon,
  AlignRightIcon,
  BulletedListIcon,
  CapitalizeIcon,
  CaseIcon,
  CheckListIcon,
  ClearFormatIcon,
  CodeBlockIcon,
  Heading1Icon,
  Heading2Icon,
  Heading3Icon,
  Heading4Icon,
  Heading5Icon,
  Heading6Icon,
  HighlightIcon,
  IndentIcon,
  LowercaseIcon,
  NumberedListIcon,
  OutdentIcon,
  ParagraphIcon,
  QuoteIcon,
  StrikethroughIcon,
  SubscriptIcon,
  SuperscriptIcon,
  UppercaseIcon,
} from "../../icons";
import { $isEquationNode } from "../../features/equation/equation-node";
import {
  DropDown,
  DropDownDivider,
  DropDownItem,
} from "../../shared/ui/dropdown";
import { SHORTCUTS } from "../shortcuts/shortcuts";
import { blockTypeToBlockName, DEFAULT_FONT_SIZE } from "./format-config";
import {
  clearFormatting,
  formatBulletList,
  formatCheckList,
  formatCode,
  formatHeading,
  formatNumberedList,
  formatParagraph,
  formatQuote,
} from "./format-commands";

import {
  $getEnclosingCodeNode,
  CODE_FONT_FAMILY_VALUE,
  upsertCssProperty,
} from "./format-state";

type FontGroup =
  "Sans-serif" | "Serif" | "Display" | "Monospace" | "Accessibility";
type FontOption = {
  value: string;
  label: string;
  group: FontGroup;
  hint?: string;
};

const FONT_FAMILY_OPTIONS: readonly FontOption[] = [
  { value: "Arial", label: "Arial", group: "Sans-serif" },
  { value: "Helvetica", label: "Helvetica", group: "Sans-serif" },
  { value: "Verdana", label: "Verdana", group: "Sans-serif" },
  { value: "Tahoma", label: "Tahoma", group: "Sans-serif" },
  { value: "Trebuchet MS", label: "Trebuchet MS", group: "Sans-serif" },
  { value: "Calibri", label: "Calibri", group: "Sans-serif" },
  { value: "Segoe UI", label: "Segoe UI", group: "Sans-serif" },
  { value: "system-ui", label: "System UI", group: "Sans-serif" },
  { value: "Times New Roman", label: "Times New Roman", group: "Serif" },
  { value: "Georgia", label: "Georgia", group: "Serif" },
  { value: "Garamond", label: "Garamond", group: "Serif" },
  { value: "Palatino", label: "Palatino", group: "Serif" },
  { value: "Comic Sans MS", label: "Comic Sans MS", group: "Display" },
  { value: "Impact", label: "Impact", group: "Display" },
  { value: CODE_FONT_FAMILY_VALUE, label: "Monospace", group: "Monospace" },
  { value: "Courier New", label: "Courier New", group: "Monospace" },
  { value: "Consolas", label: "Consolas", group: "Monospace" },
  { value: "Menlo", label: "Menlo", group: "Monospace" },
  { value: "Monaco", label: "Monaco", group: "Monospace" },
  { value: "Source Code Pro", label: "Source Code Pro", group: "Monospace" },
  {
    value: "'Atkinson Hyperlegible', Arial, sans-serif",
    label: "Atkinson Hyperlegible",
    group: "Accessibility",
    hint: "Low Vision (Braille Institute)",
  },
  {
    value: "Lexend, Arial, sans-serif",
    label: "Lexend",
    group: "Accessibility",
    hint: "Reading / Dyslexia",
  },
  {
    value: "'Lexend Deca', Arial, sans-serif",
    label: "Lexend Deca",
    group: "Accessibility",
    hint: "Fluent Reading",
  },
  {
    value: "'OpenDyslexic', 'Comic Sans MS', sans-serif",
    label: "OpenDyslexic",
    group: "Accessibility",
    hint: "Dyslexia",
  },
  {
    value: "'Andika', Arial, sans-serif",
    label: "Andika",
    group: "Accessibility",
    hint: "Literacy / Autism",
  },
  {
    value: "'Nunito', Arial, sans-serif",
    label: "Nunito",
    group: "Accessibility",
    hint: "High Readability",
  },
];

const FONT_SIZE_OPTIONS = [
  8, 9, 10, 11, 12, 14, 18, 24, 30, 36, 48, 60, 72, 96,
] as const;
const MIN_FONT_SIZE = 8;
const MAX_FONT_SIZE = 400;

export function BlockFormatDropDown({
  editor,
  blockType,
  disabled,
  compact = false,
  preserveSelection = false,
}: {
  editor: LexicalEditor;
  blockType: keyof typeof blockTypeToBlockName;
  disabled?: boolean;
  compact?: boolean;
  preserveSelection?: boolean;
}) {
  const options = [
    [
      "paragraph",
      "Normal",
      ParagraphIcon,
      () => formatParagraph(editor),
      SHORTCUTS.NORMAL,
    ],
    [
      "h1",
      "Heading 1",
      Heading1Icon,
      () => formatHeading(editor, blockType, "h1"),
      SHORTCUTS.HEADING1,
    ],
    [
      "h2",
      "Heading 2",
      Heading2Icon,
      () => formatHeading(editor, blockType, "h2"),
      SHORTCUTS.HEADING2,
    ],
    [
      "h3",
      "Heading 3",
      Heading3Icon,
      () => formatHeading(editor, blockType, "h3"),
      SHORTCUTS.HEADING3,
    ],
    [
      "h4",
      "Heading 4",
      Heading4Icon,
      () => formatHeading(editor, blockType, "h4"),
      undefined,
    ],
    [
      "h5",
      "Heading 5",
      Heading5Icon,
      () => formatHeading(editor, blockType, "h5"),
      undefined,
    ],
    [
      "h6",
      "Heading 6",
      Heading6Icon,
      () => formatHeading(editor, blockType, "h6"),
      undefined,
    ],
    [
      "bullet",
      "Bullet List",
      BulletedListIcon,
      () => formatBulletList(editor, blockType),
      SHORTCUTS.BULLET_LIST,
    ],
    [
      "number",
      "Numbered List",
      NumberedListIcon,
      () => formatNumberedList(editor, blockType),
      SHORTCUTS.NUMBERED_LIST,
    ],
    [
      "check",
      "Check List",
      CheckListIcon,
      () => formatCheckList(editor, blockType),
      SHORTCUTS.CHECK_LIST,
    ],
    [
      "quote",
      "Quote",
      QuoteIcon,
      () => formatQuote(editor, blockType),
      SHORTCUTS.QUOTE,
    ],
    [
      "code",
      "Code Block",
      CodeBlockIcon,
      () => formatCode(editor, blockType),
      SHORTCUTS.CODE_BLOCK,
    ],
  ] as const;

  return (
    <DropDown
      disabled={disabled}
      buttonLabel={blockTypeToBlockName[blockType]}
      buttonClassName={
        compact
          ? "w-[128px] truncate justify-start"
          : "w-[140px] truncate justify-start"
      }
      buttonAriaLabel="Formatting options for text style"
      preserveSelection={preserveSelection}
    >
      {options.map(([type, label, Icon, onClick, shortcut]) => (
        <DropDownItem
          key={type}
          active={blockType === type}
          onClick={onClick}
          shortcut={shortcut}
        >
          <Icon className="w-4 h-4" /> {label}
        </DropDownItem>
      ))}
    </DropDown>
  );
}

export function FontDropDown({
  editor,
  value,
  disabled,
  compact = false,
  preserveSelection = false,
}: {
  editor: LexicalEditor;
  value: string;
  style?: "font-family";
  disabled?: boolean;
  compact?: boolean;
  preserveSelection?: boolean;
}) {
  const label =
    FONT_FAMILY_OPTIONS.find((option) => option.value === value)?.label ??
    value;
  const groups = [
    ...new Set(FONT_FAMILY_OPTIONS.map((option) => option.group)),
  ];
  const apply = useCallback(
    (fontFamily: string) => {
      editor.update(() => {
        $addUpdateTag(SKIP_SELECTION_FOCUS_TAG);
        const selection = $getSelection();
        if (!selection) return;
        const codeNode = $getEnclosingCodeNode();
        if (codeNode) {
          codeNode.setStyle(
            upsertCssProperty(codeNode.getStyle(), "font-family", fontFamily),
          );
        } else {
          $patchStyleText(selection, { "font-family": fontFamily });
        }
      });
    },
    [editor],
  );

  return (
    <DropDown
      disabled={disabled}
      buttonLabel={label}
      buttonLabelStyle={{ fontFamily: value }}
      buttonClassName={
        compact
          ? "w-[120px] truncate justify-start"
          : "w-[140px] truncate justify-start"
      }
      buttonAriaLabel="Formatting options for font family"
      preserveSelection={preserveSelection}
      contentMaxHeight="min(320px, 60vh)"
    >
      {groups.flatMap((group, groupIndex) => [
        groupIndex > 0 ? <DropDownDivider key={`${group}-divider`} /> : null,
        <div
          key={`${group}-heading`}
          className="px-2 pt-1 pb-0.5 text-[10px] uppercase text-gray-500 dark:text-gray-400"
        >
          {group}
        </div>,
        ...FONT_FAMILY_OPTIONS.filter((option) => option.group === group).map(
          (option) => (
            <DropDownItem
              key={option.value}
              active={value === option.value}
              closeOnClick={false}
              onClick={() => apply(option.value)}
            >
              <span className="flex min-w-0 flex-col">
                <span className="truncate" style={{ fontFamily: option.value }}>
                  {option.label}
                </span>
                {option.hint && (
                  <span className="truncate text-[10px] text-gray-500 dark:text-gray-400">
                    {option.hint}
                  </span>
                )}
              </span>
            </DropDownItem>
          ),
        ),
      ])}
    </DropDown>
  );
}

export function FontSizeStepper({
  editor,
  value,
  disabled,
  preserveSelection = false,
}: {
  editor: LexicalEditor;
  value: string;
  disabled?: boolean;
  preserveSelection?: boolean;
}) {
  const currentNumber = React.useMemo(() => {
    const parsed = Number.parseInt(value.replace(/px$/, ""), 10);
    return Number.isFinite(parsed) ? parsed : DEFAULT_FONT_SIZE;
  }, [value]);
  const [inputValue, setInputValue] = React.useState(String(currentNumber));
  React.useEffect(() => setInputValue(String(currentNumber)), [currentNumber]);

  const apply = useCallback(
    (size: number) => {
      const clamped = Math.max(
        MIN_FONT_SIZE,
        Math.min(MAX_FONT_SIZE, Math.round(size)),
      );
      editor.update(() => {
        $addUpdateTag(SKIP_SELECTION_FOCUS_TAG);
        const selection = $getSelection();
        if (!selection) return;
        if ($isNodeSelection(selection)) {
          const equation = selection.getNodes().find($isEquationNode);
          if (equation && $isEquationNode(equation)) {
            equation.setFontSize(clamped / DEFAULT_FONT_SIZE);
            return;
          }
        }
        const codeNode = $getEnclosingCodeNode();
        if (codeNode) {
          codeNode.setStyle(
            upsertCssProperty(codeNode.getStyle(), "font-size", `${clamped}px`),
          );
        } else {
          $patchStyleText(selection, { "font-size": `${clamped}px` });
        }
      });
    },
    [editor],
  );

  const commit = () => {
    const parsed = Number.parseInt(inputValue, 10);
    if (Number.isFinite(parsed)) apply(parsed);
    else setInputValue(String(currentNumber));
  };

  return (
    <div className="inline-flex items-center gap-0.5">
      <button
        type="button"
        disabled={disabled || currentNumber <= MIN_FONT_SIZE}
        onClick={() => apply(currentNumber - 1)}
        title="Decrease font size"
        aria-label="Decrease font size"
        className="inline-flex h-8 w-7 items-center justify-center rounded text-sm hover:bg-gray-100 disabled:pointer-events-none disabled:opacity-40 dark:hover:bg-gray-800"
      >
        −
      </button>
      <div className="inline-flex h-8 items-center rounded border border-gray-200 dark:border-gray-700">
        <input
          type="text"
          inputMode="numeric"
          disabled={disabled}
          value={inputValue}
          onChange={(event) =>
            setInputValue(event.target.value.replace(/[^0-9]/g, ""))
          }
          onBlur={commit}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              commit();
              event.currentTarget.blur();
            }
          }}
          aria-label="Font size"
          className="h-full w-9 bg-transparent text-center text-sm tabular-nums outline-none"
        />
        <DropDown
          buttonAriaLabel="Choose font size"
          buttonClassName="!h-7 !rounded-none !px-1"
          preserveSelection={preserveSelection}
        >
          {FONT_SIZE_OPTIONS.map((size) => (
            <DropDownItem
              key={size}
              active={currentNumber === size}
              onClick={() => apply(size)}
            >
              {size}
            </DropDownItem>
          ))}
        </DropDown>
      </div>
      <button
        type="button"
        disabled={disabled ?? currentNumber >= MAX_FONT_SIZE}
        onClick={() => apply(currentNumber + 1)}
        title="Increase font size"
        aria-label="Increase font size"
        className="inline-flex h-8 w-7 items-center justify-center rounded text-sm hover:bg-gray-100 disabled:pointer-events-none disabled:opacity-40 dark:hover:bg-gray-800"
      >
        +
      </button>
    </div>
  );
}

const ELEMENT_FORMAT_OPTIONS: Record<
  Exclude<ElementFormatType, "">,
  { Icon: typeof AlignLeftIcon; IconRTL: typeof AlignLeftIcon; name: string }
> = {
  center: {
    Icon: AlignCenterIcon,
    IconRTL: AlignCenterIcon,
    name: "Center Align",
  },
  end: { Icon: AlignRightIcon, IconRTL: AlignLeftIcon, name: "End Align" },
  justify: {
    Icon: AlignJustifyIcon,
    IconRTL: AlignJustifyIcon,
    name: "Justify Align",
  },
  left: { Icon: AlignLeftIcon, IconRTL: AlignLeftIcon, name: "Left Align" },
  right: { Icon: AlignRightIcon, IconRTL: AlignRightIcon, name: "Right Align" },
  start: { Icon: AlignLeftIcon, IconRTL: AlignRightIcon, name: "Start Align" },
};

export function ElementFormatDropdown({
  editor,
  value,
  isRTL,
  disabled,
  compact = false,
  preserveSelection = false,
}: {
  editor: LexicalEditor;
  value: ElementFormatType;
  isRTL: boolean;
  disabled?: boolean;
  compact?: boolean;
  preserveSelection?: boolean;
}) {
  const option = ELEMENT_FORMAT_OPTIONS[value || "left"];
  const Icon = isRTL ? option.IconRTL : option.Icon;
  const alignments = [
    ["left", "Left Align", AlignLeftIcon, SHORTCUTS.LEFT_ALIGN],
    ["center", "Center Align", AlignCenterIcon, SHORTCUTS.CENTER_ALIGN],
    ["right", "Right Align", AlignRightIcon, SHORTCUTS.RIGHT_ALIGN],
    ["justify", "Justify Align", AlignJustifyIcon, SHORTCUTS.JUSTIFY_ALIGN],
  ] as const;
  return (
    <DropDown
      disabled={disabled}
      buttonLabel={compact ? undefined : option.name}
      buttonIcon={<Icon className="w-4 h-4" />}
      buttonClassName={
        compact ? "w-8 !px-2" : "w-[140px] truncate justify-start"
      }
      buttonAriaLabel="Formatting options for text alignment"
      preserveSelection={preserveSelection}
    >
      {alignments.map(([format, label, AlignmentIcon, shortcut]) => (
        <DropDownItem
          key={format}
          onClick={() => editor.dispatchCommand(FORMAT_ELEMENT_COMMAND, format)}
          shortcut={shortcut}
        >
          <AlignmentIcon className="w-4 h-4" /> {label}
        </DropDownItem>
      ))}
      <DropDownDivider />
      <DropDownItem
        onClick={() =>
          editor.dispatchCommand(OUTDENT_CONTENT_COMMAND, undefined)
        }
        shortcut={SHORTCUTS.OUTDENT}
      >
        <OutdentIcon className="w-4 h-4" /> Outdent
      </DropDownItem>
      <DropDownItem
        onClick={() =>
          editor.dispatchCommand(INDENT_CONTENT_COMMAND, undefined)
        }
        shortcut={SHORTCUTS.INDENT}
      >
        <IndentIcon className="w-4 h-4" /> Indent
      </DropDownItem>
    </DropDown>
  );
}

export function CaseFormatDropDown({
  editor,
  disabled,
  preserveSelection = false,
  ...state
}: {
  editor: LexicalEditor;
  disabled?: boolean;
  isLowercase: boolean;
  isUppercase: boolean;
  isCapitalize: boolean;
  isStrikethrough: boolean;
  isSubscript: boolean;
  isSuperscript: boolean;
  isHighlight: boolean;
  preserveSelection?: boolean;
}) {
  const formats: readonly [
    TextFormatType,
    string,
    typeof LowercaseIcon,
    boolean,
    string?,
  ][] = [
    [
      "lowercase",
      "Lowercase",
      LowercaseIcon,
      state.isLowercase,
      SHORTCUTS.LOWERCASE,
    ],
    [
      "uppercase",
      "Uppercase",
      UppercaseIcon,
      state.isUppercase,
      SHORTCUTS.UPPERCASE,
    ],
    [
      "capitalize",
      "Capitalize",
      CapitalizeIcon,
      state.isCapitalize,
      SHORTCUTS.CAPITALIZE,
    ],
    [
      "strikethrough",
      "Strikethrough",
      StrikethroughIcon,
      state.isStrikethrough,
      SHORTCUTS.STRIKETHROUGH,
    ],
    [
      "subscript",
      "Subscript",
      SubscriptIcon,
      state.isSubscript,
      SHORTCUTS.SUBSCRIPT,
    ],
    [
      "superscript",
      "Superscript",
      SuperscriptIcon,
      state.isSuperscript,
      SHORTCUTS.SUPERSCRIPT,
    ],
    ["highlight", "Highlight", HighlightIcon, state.isHighlight],
  ];
  return (
    <DropDown
      disabled={disabled}
      buttonIcon={<CaseIcon className="w-4 h-4" />}
      buttonAriaLabel="Formatting options for additional text styles"
      preserveSelection={preserveSelection}
    >
      {formats.map(([format, label, Icon, active, shortcut]) => (
        <DropDownItem
          key={format}
          active={active}
          onClick={() => editor.dispatchCommand(FORMAT_TEXT_COMMAND, format)}
          shortcut={shortcut}
        >
          <Icon className="w-4 h-4" /> {label}
        </DropDownItem>
      ))}
      <DropDownItem
        onClick={() => clearFormatting(editor)}
        shortcut={SHORTCUTS.CLEAR_FORMATTING}
      >
        <ClearFormatIcon className="w-4 h-4" /> Clear Formatting
      </DropDownItem>
    </DropDown>
  );
}
