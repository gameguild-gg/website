import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { attachEditorModelReader, EDITOR_MODEL_READER } from '../dist/components/editor-model-reader.js';

function editorFixture(node = {}) {
  let current = { uri: { path: '/home/user/main.cpp' }, getValue: () => 'starter' };
  const listeners = new Set();
  return {
    node,
    editor: {
      getDomNode: () => node,
      getModel: () => current,
      onDidDispose(listener) {
        listeners.add(listener);
        return { dispose() { listeners.delete(listener); } };
      },
    },
    setModel(model) { current = model; },
    disposeEditor() { for (const listener of listeners) listener(); },
    listeners,
  };
}

describe('mounted editor model diagnostics', () => {
  it('does not register a reader or listener without a DOM node', () => {
    const fixture = editorFixture(null);
    attachEditorModelReader(fixture.editor);
    assert.equal(fixture.listeners.size, 0);
  });

  it('reads the model belonging to the selected DOM node', () => {
    const fixture = editorFixture();
    attachEditorModelReader(fixture.editor);
    assert.deepEqual(fixture.node[EDITOR_MODEL_READER](), {
      path: '/home/user/main.cpp', content: 'starter',
    });
  });

  it('reads the current model after typing or a model switch', () => {
    const fixture = editorFixture();
    attachEditorModelReader(fixture.editor);
    fixture.setModel({ uri: { path: '/home/user/main.cpp' }, getValue: () => '// e2e-student-fix' });
    assert.equal(fixture.node[EDITOR_MODEL_READER]().content, '// e2e-student-fix');
    fixture.setModel({ uri: { path: '/home/user/other.cpp' }, getValue: () => 'other file' });
    assert.equal(fixture.node[EDITOR_MODEL_READER]().path, '/home/user/other.cpp');
  });

  it('reports an absent current model without fabricating content', () => {
    const fixture = editorFixture();
    attachEditorModelReader(fixture.editor);
    fixture.setModel(null);
    assert.equal(fixture.node[EDITOR_MODEL_READER](), null);
  });

  it('keeps the reader non-enumerable without publishing code in DOM attributes', () => {
    const fixture = editorFixture();
    attachEditorModelReader(fixture.editor);
    assert.equal(Object.getOwnPropertyDescriptor(fixture.node, EDITOR_MODEL_READER).enumerable, false);
    assert.deepEqual(Object.keys(fixture.node), []);
  });

  it('removes its own reader when the editor is disposed', () => {
    const fixture = editorFixture();
    attachEditorModelReader(fixture.editor);
    fixture.disposeEditor();
    assert.equal(fixture.node[EDITOR_MODEL_READER], undefined);
  });

  it('does not remove a replacement reader owned by another editor', () => {
    const node = {};
    const first = editorFixture(node);
    const replacement = editorFixture(node);
    attachEditorModelReader(first.editor);
    attachEditorModelReader(replacement.editor);
    const reader = node[EDITOR_MODEL_READER];
    first.disposeEditor();
    assert.equal(node[EDITOR_MODEL_READER], reader);
    replacement.disposeEditor();
    assert.equal(node[EDITOR_MODEL_READER], undefined);
  });

  it('isolates two mounted editors instead of selecting from a shared registry', () => {
    const first = editorFixture();
    const second = editorFixture();
    second.setModel({ uri: { path: '/home/user/second.cpp' }, getValue: () => 'second' });
    attachEditorModelReader(first.editor);
    attachEditorModelReader(second.editor);
    assert.equal(first.node[EDITOR_MODEL_READER]().path, '/home/user/main.cpp');
    assert.equal(second.node[EDITOR_MODEL_READER]().path, '/home/user/second.cpp');
  });
});
