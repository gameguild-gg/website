'use client';

import { CodeiumEditor } from '@codeium/react-code-editor';
import { editor } from 'monaco-editor';
import { Monaco } from '@monaco-editor/react';
import React, { useState } from 'react';

// codeium editor page
export default function Editor() {
  // inner value of the code editor
  const [code, setCode] = useState<string>(`#include <iostream>
int main(){
  // add your code here
}
`);

  // editor
  const [editor, setEditor] = useState<editor.IStandaloneCodeEditor | null>(
    null,
  );
  // monaco
  const [monaco, setMonaco] = useState<Monaco | null>(null);

  const editorDidMount = (
    editor: editor.IStandaloneCodeEditor,
    monaco: Monaco,
  ): void => {
    // editor is ready
    console.log('editor is ready');
    setEditor(editor);
    setMonaco(monaco);
  };

  return (
    <div>
      <CodeiumEditor
        language="cpp"
        theme="vs-dark"
        defaultValue={code}
        height={'100vh'}
        onChange={(value, editor) => {
          if (value) setCode(value);
        }}
        onMount={editorDidMount}
      />
    </div>
  );
}
