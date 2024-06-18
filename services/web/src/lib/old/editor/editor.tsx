import { LexicalComposer } from '@lexical/react/LexicalComposer';
import { RichTextPlugin } from '@lexical/react/LexicalRichTextPlugin';
import { ContentEditable } from '@lexical/react/LexicalContentEditable';
import { HistoryPlugin } from '@lexical/react/LexicalHistoryPlugin';
import { AutoFocusPlugin } from '@lexical/react/LexicalAutoFocusPlugin';
import LexicalErrorBoundary from '@lexical/react/LexicalErrorBoundary';
import { HeadingNode, QuoteNode } from '@lexical/rich-text';
import { TableCellNode, TableNode, TableRowNode } from '@lexical/table';
import { ListItemNode, ListNode } from '@lexical/list';
import { CodeHighlightNode, CodeNode } from '@lexical/code';
import { AutoLinkNode, LinkNode } from '@lexical/link';
import { LinkPlugin } from '@lexical/react/LexicalLinkPlugin';
import { ListPlugin } from '@lexical/react/LexicalListPlugin';
import { MarkdownShortcutPlugin } from '@lexical/react/LexicalMarkdownShortcutPlugin';
import { TRANSFORMERS } from '@lexical/markdown';
// import ToolbarPlugin from '@/lib/old/editor/plugins/ToolbarPlugin';
// import TreeViewPlugin from '@/lib/old/editor/plugins/TreeViewPlugin';
// import CodeHighlightPlugin from '@/lib/old/editor/plugins/CodeHighlightPlugin';
import { AutoLinkPlugin } from '@lexical/react/LexicalAutoLinkPlugin';
// import ListMaxIndentLevelPlugin from '@/lib/old/editor/plugins/ListMaxIndentLevelPlugin';
import ExampleTheme from '@/lib/old/editor/themes/exampleTheme';
import type { InitialConfigType } from '@lexical/react/LexicalComposer';

// todo: export editor
