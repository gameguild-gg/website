import { render } from '@testing-library/react';
import type { editor, languages } from 'monaco-editor';
import { describe, expect, it, vi } from 'vitest';
import { XMLSyntaxHighlighter } from './xml-syntax-highlighter';

describe('XML comment tokenization', () => {
  it('keeps comments in a dedicated state across lines until their closing delimiter', () => {
    const setMonarchTokensProvider = vi.fn<(language: string, tokens: languages.IMonarchLanguage) => { dispose: () => void }>(() => ({ dispose: vi.fn() }));
    const monaco = {
      languages: { getLanguages: () => [], register: vi.fn(), setMonarchTokensProvider },
      editor: { setModelLanguage: vi.fn() },
    } as unknown as typeof import('monaco-editor');
    const codeEditor = { getModel: () => null } as unknown as editor.IStandaloneCodeEditor;

    render(<XMLSyntaxHighlighter monaco={monaco} editor={codeEditor} />);

    const tokens = setMonarchTokensProvider.mock.calls[0]?.[1];
    expect(tokens?.tokenizer.root).toContainEqual([/<!--/, { token: 'comment', next: '@comment' }]);
    expect(tokens?.tokenizer.comment).toEqual([
      [/--!?>/, { token: 'comment', next: '@pop' }],
      [/[^-]+/, 'comment'],
      [/-/, 'comment'],
    ]);
    const closingRule = tokens?.tokenizer.comment?.[0];
    const closingPattern = Array.isArray(closingRule) ? closingRule[0] : undefined;
    expect(closingPattern).toBeInstanceOf(RegExp);
    if (!(closingPattern instanceof RegExp)) throw new Error('XML comment closing rule is missing');
    expect(closingPattern.test('-->')).toBe(true);
    expect(closingPattern.test('--!>')).toBe(true);
    expect(closingPattern.test('->')).toBe(false);

    const bodyRule = tokens?.tokenizer.comment?.[1];
    const bodyPattern = Array.isArray(bodyRule) ? bodyRule[0] : undefined;
    if (!(bodyPattern instanceof RegExp)) throw new Error('XML comment body rule is missing');
    expect(bodyPattern.test('line1\nline2 ')).toBe(true);
  });

  it('tokenizes CDATA through a dedicated state so sections can span lines', () => {
    const setMonarchTokensProvider = vi.fn<(language: string, tokens: languages.IMonarchLanguage) => { dispose: () => void }>(() => ({ dispose: vi.fn() }));
    const monaco = {
      languages: { getLanguages: () => [], register: vi.fn(), setMonarchTokensProvider },
      editor: { setModelLanguage: vi.fn() },
    } as unknown as typeof import('monaco-editor');
    const codeEditor = { getModel: () => null } as unknown as editor.IStandaloneCodeEditor;

    render(<XMLSyntaxHighlighter monaco={monaco} editor={codeEditor} />);

    const tokens = setMonarchTokensProvider.mock.calls[0]?.[1];
    expect(tokens?.tokenizer.root).toContainEqual([/<!\[CDATA\[/, { token: 'comment', next: '@cdata' }]);
    expect(tokens?.tokenizer.cdata).toEqual([
      [/\]\]>/, { token: 'comment', next: '@pop' }],
      [/[^\]]+/, 'comment'],
      [/\]/, 'comment'],
    ]);
    const cdataBodyRule = tokens?.tokenizer.cdata?.[1];
    const cdataBodyPattern = Array.isArray(cdataBodyRule) ? cdataBodyRule[0] : undefined;
    if (!(cdataBodyPattern instanceof RegExp)) throw new Error('CDATA body rule is missing');
    expect(cdataBodyPattern.test('raw <content>\nspanning lines ')).toBe(true);
  });

  it('matches quoted attribute values that span multiple lines', () => {
    const setMonarchTokensProvider = vi.fn<(language: string, tokens: languages.IMonarchLanguage) => { dispose: () => void }>(() => ({ dispose: vi.fn() }));
    const monaco = {
      languages: { getLanguages: () => [], register: vi.fn(), setMonarchTokensProvider },
      editor: { setModelLanguage: vi.fn() },
    } as unknown as typeof import('monaco-editor');
    const codeEditor = { getModel: () => null } as unknown as editor.IStandaloneCodeEditor;

    render(<XMLSyntaxHighlighter monaco={monaco} editor={codeEditor} />);

    const tokens = setMonarchTokensProvider.mock.calls[0]?.[1];
    const tagContentRules = tokens?.tokenizer.tagContent ?? [];
    const stringRule = tagContentRules.find(
      (rule) => Array.isArray(rule) && rule[0] instanceof RegExp && rule[0].source === '"([^"\\\\]|\\\\.)*"',
    );
    expect(stringRule).toBeDefined();
    const pattern = Array.isArray(stringRule ?? []) ? (stringRule as [RegExp, unknown])[0] : undefined;
    if (!(pattern instanceof RegExp)) throw new Error('tagContent string rule is missing');
    expect(pattern.test('"multi\nline"')).toBe(true);
    expect(pattern.test('"single"')).toBe(true);
    expect(pattern.test('"unterminated')).toBe(false);
  });
});
