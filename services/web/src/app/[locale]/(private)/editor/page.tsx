'use client';

import { CodeiumEditor } from '@codeium/react-code-editor';
import { editor } from 'monaco-editor';
import { Monaco } from '@monaco-editor/react';
import React, { useEffect, useRef, useState } from 'react';
import * as Comlink from 'comlink';
import Emception from './emception';

type EmceptionWrapper = {
  worker: Comlink.Remote<Emception> | null;
};

interface CodeEditorProps {
  descriptionText: string;
  initialCode: string;
}

export default function CodeEditor() {
  const [emceptionLoaded, setEmceptionLoaded] = useState(false);
  const [emception, setEmception] = useState<EmceptionWrapper>({
    worker: null,
  });

  const initialCode = `#include <iostream>
int main(){
    // add your code here
}
`;
  const descriptionText = 'Description Goes Here';

  const [code, setCode] = useState<string>(
    initialCode
      ? initialCode
      : `#include <iostream>
int main(){
  // add your code here
}
`,
  );
  const [description, setDescription] = useState<string>(
    descriptionText ? descriptionText : 'Description Goes Here',
  );

  const [editor, setEditor] = useState<editor.IStandaloneCodeEditor | null>(
    null,
  );
  const [monaco, setMonaco] = useState<Monaco | null>(null);
  const containerRef = useRef<HTMLDivElement>(null);
  const isDragging = useRef<boolean>(false);
  const [leftWidth, setLeftWidth] = useState<number>(20);

  async function loadEmception(): Promise<any> {
    showNotification('Loading emception...');
    if (emceptionLoaded) return;
    setEmceptionLoaded(true);

    // todo: is it possible to not refer as url?
    const emceptionWorker = new Worker(
      new URL('./emception.worker.ts', import.meta.url),
      { type: 'module' },
    );

    emceptionWorker.onerror = (e) => {
      console.error(e);
      console.error('Emception worker error');
    };

    const emception: Comlink.Remote<Emception> = Comlink.wrap(emceptionWorker);

    setEmception({ worker: emception });

    emception.onstdout.bind(console.log);
    emception.onstderr.bind(console.log);
    emception.onprocessstart.bind(console.log);
    emception.onprocessend.bind(console.log);

    await emception.init();

    console.log('Post init');
    console.log('Ready');
  }

  useEffect(() => {
    const handleMouseMove = (e: MouseEvent) => {
      if (isDragging.current && containerRef.current) {
        const newLeftWidth =
          ((e.clientX - containerRef.current.getBoundingClientRect().left) /
            containerRef.current.offsetWidth) *
          100;
        setLeftWidth(newLeftWidth);
      }
    };

    const handleMouseUp = () => {
      isDragging.current = false;
    };

    document.addEventListener('mousemove', handleMouseMove);
    document.addEventListener('mouseup', handleMouseUp);

    return () => {
      document.removeEventListener('mousemove', handleMouseMove);
      document.removeEventListener('mouseup', handleMouseUp);
    };
  }, []);

  const editorDidMount = (
    editor: editor.IStandaloneCodeEditor,
    monaco: Monaco,
  ): void => {
    console.log('editor is ready');
    setEditor(editor);
    setMonaco(monaco);
  };

  const handleSaveShortcut = (e: KeyboardEvent) => {
    if ((e.ctrlKey || e.metaKey) && e.key === 's') {
      e.preventDefault();
      console.log('Control+S pressed');
      // Add your save logic here
    }
  };

  useEffect(() => {
    document.addEventListener('keydown', handleSaveShortcut);

    return () => {
      document.removeEventListener('keydown', handleSaveShortcut);
    };
  }, []);

  return (
    <main
      style={{ height: '100vh', display: 'flex', flexDirection: 'row' }}
      ref={containerRef}
    >
      <div
        style={{
          width: `${leftWidth}%`,
          height: '100%',
          backgroundColor: '#f0f0f0',
        }}
      >
        {description}
      </div>
      <div
        style={{
          width: '5px',
          cursor: 'col-resize',
          backgroundColor: '#ccc',
          height: '100%',
        }}
        onMouseDown={() => (isDragging.current = true)}
      />
      <div style={{ width: `${100 - leftWidth}%`, height: '100%' }}>
        <CodeiumEditor
          language="cpp"
          theme="vs-dark"
          defaultValue={code}
          height={'100%'}
          width={'100%'}
          onMount={editorDidMount}
          options={{
            wordWrap: 'on',
            minimap: { enabled: true },
            autoIndent: 'full',
            automaticLayout: true,
            bracketPairColorization: { enabled: true },
            cursorBlinking: 'smooth',
          }}
        />
      </div>
    </main>
  );
}
