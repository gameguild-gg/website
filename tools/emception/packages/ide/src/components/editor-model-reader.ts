import type { OnMount } from '@monaco-editor/react';

export const EDITOR_MODEL_READER = Symbol.for('gameguild.emception.editor-model-reader');

type EditorAccess = Pick<Parameters<OnMount>[0], 'getDomNode' | 'getModel' | 'onDidDispose'>;

/** Attach read-only diagnostics to the editor that actually owns this DOM node. */
export function attachEditorModelReader(editor: EditorAccess): void {
  const node = editor.getDomNode();
  if (!node) return;

  const readModel = () => {
    const model = editor.getModel();
    return model ? { path: model.uri.path, content: model.getValue() } : null;
  };

  Object.defineProperty(node, EDITOR_MODEL_READER, {
    configurable: true,
    enumerable: false,
    value: readModel,
  });
  editor.onDidDispose(() => {
    const current: unknown = Reflect.get(node, EDITOR_MODEL_READER);
    if (current === readModel) Reflect.deleteProperty(node, EDITOR_MODEL_READER);
  });
}
