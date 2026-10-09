import { render } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import type { MonacoRuntime } from './monaco-runtime';

const hooks = vi.hoisted(() => ({
  beforeMount: undefined as ((runtime: MonacoRuntime) => void | Promise<void>) | undefined,
  registerPaths: vi.fn(),
}));

vi.mock('@/components/block-content-editor/lib/monaco', () => ({
  BaseMonacoEditor: (props: { beforeMount: typeof hooks.beforeMount }) => {
    hooks.beforeMount = props.beforeMount;
    return null;
  },
}));
vi.mock('next-themes', () => ({ useTheme: () => ({ resolvedTheme: 'light', theme: 'light' }) }));
vi.mock('@/components/block-content-editor/lib/shiki/highlighter', () => ({ isShikiActive: () => false }));
vi.mock('./monaco-file-system', () => ({ registerPathCompletionProvider: hooks.registerPaths }));
vi.mock('../dialogs/link-confirm-dialog', () => ({ LinkConfirmDialog: () => null }));

import { MonacoCodeEditor } from './monaco-code-editor';

describe('MonacoCodeEditor runtime setup', () => {
  it('configures TypeScript from the current top-level Monaco namespace', async () => {
    const typescriptDefaults = { setCompilerOptions: vi.fn(), setDiagnosticsOptions: vi.fn() };
    const javascriptDefaults = { setCompilerOptions: vi.fn(), setDiagnosticsOptions: vi.fn() };
    const runtime = {
      languages: {},
      typescript: {
        typescriptDefaults,
        javascriptDefaults,
        ScriptTarget: { ES2020: 7 },
        ModuleResolutionKind: { NodeJs: 2 },
        ModuleKind: { ESNext: 99 },
        JsxEmit: { React: 2 },
      },
    } as MonacoRuntime;

    render(<MonacoCodeEditor value="const value = 1" language="typescript" />);
    expect(hooks.beforeMount).toBeDefined();
    await hooks.beforeMount?.(runtime);

    expect(typescriptDefaults.setCompilerOptions).toHaveBeenCalledWith(expect.objectContaining({ target: 7, module: 99 }));
    expect(javascriptDefaults.setCompilerOptions).toHaveBeenCalledWith(expect.objectContaining({ target: 7, module: 99 }));
    expect(typescriptDefaults.setDiagnosticsOptions).toHaveBeenCalledWith(expect.objectContaining({ noSyntaxValidation: false }));
    expect(hooks.registerPaths).toHaveBeenCalledWith(runtime);
  });
});
