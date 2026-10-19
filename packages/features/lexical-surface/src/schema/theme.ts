/**
 * Consolidated Tailwind theme for every LexicalSurface. Ported from
 * facebook/lexical playground
 * (PlaygroundEditorTheme.css) to Tailwind classes with dark-mode variants.
 *
 * Theme entries cover both core Lexical nodes and the package schema.
 */
import type { EditorThemeClasses } from "lexical";

type AlignmentThemeClasses = Record<
  "textLeft" | "textCenter" | "textRight" | "textJustify" | "textStart" | "textEnd",
  string
>;

export const LEXICAL_SURFACE_THEME: EditorThemeClasses = {
  ltr: "text-left",
  rtl: "text-right",
  paragraph: "my-2 relative",
  quote:
    "border-l-4 border-gray-300 dark:border-gray-600 pl-4 italic text-gray-600 dark:text-gray-400 my-2",
  heading: {
    h1: "text-3xl font-bold my-3",
    h2: "text-2xl font-bold my-2",
    h3: "text-xl font-bold my-2",
    h4: "text-lg font-bold my-1",
    h5: "text-base font-bold my-1",
    h6: "text-sm font-bold my-1 uppercase tracking-wide",
  },
  list: {
    nested: {
      listitem: "list-none",
    },
    ol: "list-decimal list-inside ml-4 my-2",
    // List with items checkbox (role="checkbox") lose the default <ul> marker
    // via :has(), avoiding bullet + checkbox.
    ul: "list-disc list-inside ml-4 my-2 [&:has(>li[role=checkbox])]:list-none [&:has(>li[role=checkbox])]:ml-0 [&:has(>li[role=checkbox])]:pl-0",
    listitem: "my-0.5",
    // Checkbox checked: blue filled box + white check (✓ via rotation).
    listitemChecked:
      "relative list-none pl-7 my-1 leading-snug cursor-pointer line-through text-gray-500 dark:text-gray-400 select-none before:content-[''] before:absolute before:left-0 before:top-[3px] before:w-[18px] before:h-[18px] before:rounded-[4px] before:border before:border-blue-500 before:bg-blue-500 before:transition-colors after:content-[''] after:absolute after:left-[6px] after:top-[5px] after:w-[6px] after:h-[11px] after:border-r-2 after:border-b-2 after:border-white after:rotate-45",
    // Checkbox void: white box with neutral border.
    listitemUnchecked:
      "relative list-none pl-7 my-1 leading-snug cursor-pointer text-gray-800 dark:text-gray-200 select-none before:content-[''] before:absolute before:left-0 before:top-[3px] before:w-[18px] before:h-[18px] before:rounded-[4px] before:border before:border-gray-400 dark:before:border-gray-500 before:bg-white dark:before:bg-gray-900 before:transition-colors hover:before:border-blue-500",
  },
  hashtag:
    "bg-blue-50 dark:bg-blue-900/30 text-blue-600 dark:text-blue-300 rounded px-1",
  hr: "my-4 border-0 border-t border-gray-300 dark:border-gray-700",
  hrSelected: "my-4 border-0 border-t-2 border-blue-500 outline-none",
  image: "inline-block max-w-full",
  link: "text-blue-600 dark:text-blue-400 underline hover:text-blue-800 dark:hover:text-blue-300 cursor-pointer",
  text: {
    bold: "font-bold",
    code: "bg-gray-100 dark:bg-gray-800 px-1 py-0.5 rounded font-mono text-sm",
    italic: "italic",
    strikethrough: "line-through",
    subscript: "align-sub text-xs",
    superscript: "align-super text-xs",
    underline: "underline",
    underlineStrikethrough: "[text-decoration:underline_line-through]",
    lowercase: "lowercase",
    uppercase: "uppercase",
    capitalize: "capitalize",
    highlight: "bg-yellow-200 dark:bg-yellow-700/50 px-0.5 rounded",
  },
  code: [
    "block relative bg-gray-100 dark:bg-gray-800 rounded font-mono text-[13px] my-2 overflow-x-auto leading-[1.53] [tab-size:2]",
    // Padding: extra-left for the line number gutter, extra-top for the language label.
    "pt-2 pr-2 pb-2 pl-[52px]",
    // Line number gutter (reads the data-gutter attribute populated by @lexical/code).
    "before:content-[attr(data-gutter)] before:absolute before:left-0 before:top-0 before:bottom-0",
    "before:bg-gray-200 dark:before:bg-gray-700 before:text-gray-500 dark:before:text-gray-400",
    "before:border-r before:border-gray-300 dark:before:border-gray-600",
    "before:py-2 before:px-2 before:whitespace-pre-wrap before:text-right before:min-w-[28px] before:select-none",
    // Language label in the top-right corner (data-highlight-language).
    "after:content-[attr(data-highlight-language)] after:absolute after:top-0 after:right-1 after:p-1",
    "after:text-[10px] after:uppercase after:text-gray-500 dark:after:text-gray-400 after:select-none",
  ].join(" "),
  codeHighlight: {
    atrule: "text-purple-600 dark:text-purple-400",
    attr: "text-purple-600 dark:text-purple-400",
    boolean: "text-red-600 dark:text-red-400",
    builtin: "text-amber-600 dark:text-amber-400",
    cdata: "text-gray-500 dark:text-gray-400",
    char: "text-amber-600 dark:text-amber-400",
    class: "text-yellow-600 dark:text-yellow-400",
    "class-name": "text-yellow-600 dark:text-yellow-400",
    comment: "text-gray-500 dark:text-gray-400 italic",
    constant: "text-red-600 dark:text-red-400",
    deleted: "text-red-600 dark:text-red-400",
    doctype: "text-gray-500 dark:text-gray-400",
    entity: "text-orange-600 dark:text-orange-400",
    function: "text-blue-600 dark:text-blue-400",
    important: "text-red-600 dark:text-red-400 font-bold",
    inserted: "text-green-600 dark:text-green-400",
    keyword: "text-purple-600 dark:text-purple-400",
    namespace: "text-orange-600 dark:text-orange-400",
    number: "text-red-600 dark:text-red-400",
    operator: "text-gray-700 dark:text-gray-300",
    prolog: "text-gray-500 dark:text-gray-400",
    property: "text-blue-600 dark:text-blue-400",
    punctuation: "text-gray-700 dark:text-gray-300",
    regex: "text-green-600 dark:text-green-400",
    selector: "text-amber-600 dark:text-amber-400",
    string: "text-amber-600 dark:text-amber-400",
    symbol: "text-red-600 dark:text-red-400",
    tag: "text-red-600 dark:text-red-400",
    url: "text-blue-600 dark:text-blue-400 underline",
    variable: "text-orange-600 dark:text-orange-400",
  },
  mark: "bg-yellow-100 dark:bg-yellow-900/30 border-b border-yellow-400 dark:border-yellow-600",
  markOverlap:
    "bg-yellow-200 dark:bg-yellow-800/40 border-b border-yellow-500 dark:border-yellow-500",
  embedBlock: {
    base: "select-none",
    focus: "outline outline-2 outline-blue-500 rounded",
  },
  table: "border-collapse my-2 table-fixed w-full max-w-full",
  tableScrollableWrapper: "overflow-x-auto my-2",
  tableRow: "",
  tableCell:
    "border border-gray-300 dark:border-gray-700 align-top p-2 relative break-words",
  tableCellHeader:
    "bg-gray-100 dark:bg-gray-800 font-bold border border-gray-300 dark:border-gray-700 align-top p-2 text-start break-words",
  tableCellSelected: "bg-blue-100/40 dark:bg-blue-900/30",
  tableSelected: "outline outline-2 outline-blue-500",
  tableSelection: "[caret-color:transparent]",
  tableAddColumns:
    "absolute top-0 right-[-12px] h-full w-3 bg-gray-200 dark:bg-gray-700 hover:bg-blue-500 cursor-pointer",
  tableAddRows:
    "absolute bottom-[-12px] left-0 w-full h-3 bg-gray-200 dark:bg-gray-700 hover:bg-blue-500 cursor-pointer",
  tableCellActionButton:
    "absolute top-1 right-1 w-5 h-5 rounded bg-gray-200 dark:bg-gray-700 hover:bg-gray-300 dark:hover:bg-gray-600 flex items-center justify-center",
  tableCellActionButtonContainer: "absolute top-1 right-1",
  tableCellResizer: "absolute right-[-2px] top-0 h-full w-1 cursor-col-resize",
  layoutContainer: "my-2 gap-3",
  layoutItem: "min-h-[40px] p-2 rounded",
  indent: "[--lexical-indent:40px]",
  // Tailwind utility hooks for ElementFormatPlugin alignment classes.
  // Lexical writes these as theme classes on the block element.
  // (These keys aren't part of the official type, but Lexical accepts
  // arbitrary string keys at runtime.)
  ...({
    textLeft: "text-left",
    textCenter: "text-center",
    textRight: "text-right",
    textJustify: "text-justify",
    textStart: "text-start",
    textEnd: "text-end",
  } satisfies AlignmentThemeClasses),
};
